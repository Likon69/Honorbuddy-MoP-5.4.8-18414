using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Behaviors;
using Bots.DungeonBuddy.Helpers;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

namespace Bots.DungeonBuddy.Dungeon_Scripts.Mists_of_Pandaria
{
	public class GatesOfRetribution : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 717; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(1242.479, 600.5036, 318.0787); }
		}

		#region Movement

		public override MoveResult MoveTo(WoWPoint location)
		{
			// Don't move to towers...
			if (location.Distance2DSqr(_leftTowerLoc) < 20*20 || location.Distance2DSqr(_rightTowerLoc) < 20*20)
				return MoveResult.Moved;
			return base.MoveTo(location);
		}

		#endregion

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			bool galakrasIsAlive = ScriptHelpers.IsBossAlive("Galakras");

			units.RemoveAll(
				ret =>
				{
					WoWUnit unit = ret.ToUnit();
					if (unit != null)
					{
						if (unit.Entry == DoomlordId)
							return true;

						if (unit.Entry == KorkronShadowmageId && unit.Location.DistanceSqr(_doomLordLoc) < 20*20)
							return true;

						// ignore blademaster if no tanks are within melee range.
						if (unit.Entry == BlindBlademasterId &&
							!ScriptHelpers.GroupMembers.Any(g => g.IsTank && g.IsAlive && g.Location.Distance(unit.Location) < unit.MeleeRange()))
						{
							return true;
						}

						// ignore the dark shamans when they're getting pulled just to reset encounter.
						if (_darkShamanMobIds.Contains(unit.Entry) && unit.HealthPercent > 97 
							&& PercentOfGroupmembersInCombat < 50 
							&& unit.ThreatInfo.ThreatStatus == ThreatStatus.UnitNotInThreatTable)
						{
							return true;
						}

						if (unit.Entry != GeneralNazgrimId && _ignoreNazgrimAdds)
							return true;

						if (galakrasIsAlive)
						{
							WoWPoint unitLoc = unit.Location;
							// remove dps targets that are in towers
							if (unitLoc.Distance2DSqr(_leftTowerLoc) < 20*20 || unitLoc.Distance2DSqr(_rightTowerLoc) < 20*20)
								return true;
						}

						switch (unit.Entry)
						{
								// only range should deal with these since they do aoe around them.
							case FoulSlimeId:
								return Me.IsMelee();
							case KorkronIronbladeId:
								return _ironstormSpellIds.Contains(unit.CastingSpellId) && Me.IsMeleeDps();
							case GeneralNazgrimId:
								return unit.HasAura("Defensive Stance");
						}
					}
					return false;
				});
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			foreach (WoWObject obj in incomingunits)
			{
				var unit = obj as WoWUnit;
				if (unit != null)
				{
					if (Me.IsRange() &&
						(unit.Entry == HealingTideTotemId || unit.Entry == HealingTideTotem2Id || unit.Entry == DragonmawWarBannerId))
						outgoingunits.Add(unit);
				}
			}
		}

		public override void RemoveHealTargetsFilter(List<WoWObject> units)
		{
			if (!ScriptHelpers.IsBossAlive("Galakras"))
				return;
			units.RemoveAll(
				u =>
				{
					// remove healing targets that are in towers
					if (u.Location.Distance2DSqr(_leftTowerLoc) < 20*20 || u.Location.Distance2DSqr(_rightTowerLoc) < 20*20)
						return true;
					return false;
				});

		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (Targeting.TargetPriority priority in units)
			{
				var unit = priority.Object as WoWUnit;
				if (unit != null)
				{
					switch (unit.Entry)
					{
						case KorkronAssassinId:
							priority.Score += (Me.HasAura("Assassin's Mark") && Me.Auras["Assassin's Mark"].CreatorGuid == unit.Guid)
								? 10000
								: 3500;
							break;
						case KorkronBannerId:
							priority.Score += 4500;
							break;
							// These are casted by Dragonmaw Tidal Shaman and should be killed immediately
						case HealingTideTotemId:
						case HealingTideTotem2Id:
							// Foul Slime will spawn during the Kor'kron Dark Shaman encounter 
						case FoulSlimeId:
							// add that spawns during General Nazgrim encounter. Needs to be killed 1st since these heal.
						case KorkronWarshamanId:
							priority.Score += 4000;
							break;
							// these are casted by Dragonmaw Flagbearer and should be killed immediately
						case DragonmawWarBannerId:
							priority.Score += 3500;
							break;
						case KorkronDemolisherId:
						case KorkronArcweaverId:
							priority.Score += 3000;
							break;
							// Summoned by Korgra the Snake. These will pop up behind random raid members and do a frontal attack.
						case DragonmawEbonStalkerId:
						case KorkronIronbladeId:
							priority.Score += 2500;
							break;
					}
				}
			}
		}

		public override void OnEnter()
		{
			if (Me.IsTank())
			{
				Alert.Show(
					"Tanking Not Supported",
					string.Format(
						"Tanking is not supported in the {0} script. If you wish to stay in raid and play manually then press 'Continue'. Otherwise you will automatically leave raid.",
						Name),
					30,
					true,
					true,
					null,
					() => Lua.DoString("LeaveParty()"),
					"Continue",
					"Leave");
			}
			else
			{
				Alert.Show(
					"Do Not AFK",
					"It is highly recommended you do not afk while in a raid and be prepared to intervene if needed in the event something goes wrong or you're asked to perform a certain task.",
					20,
					true,
					false,
					null,
					null,
					"Ok");
			}
			_ignoreNazgrimAdds = false;
		}

		#endregion

		#region Root Behavior

		private const uint BlindBlademasterId = 72131;
		private const uint DoomlordId = 72564;
		private readonly WaitTimer _isNotMovingTimer = WaitTimer.OneSecond;

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0)]
		public Composite RootBehavior()
		{
			return new PrioritySelector(
								// reset the isMovingTimer whenever bot is not moving. This timer is used to avoid some stuff while not moving.
				// Because we need to move when running out we use a timer to avoid 'stop n go' type behavior.
				new Action(
					ctx =>
					{
						if (!Me.IsMoving)
							_isNotMovingTimer.Reset();
						return RunStatus.Failure;
					}),
				CreateBehavior_IgnoreTrash());
		}

		private Composite CreateBehavior_IgnoreTrash()
		{
			return new PrioritySelector(
				ctx => Targeting.Instance.FirstUnit,
				new Action(
					ctx =>
					{
						// Blacklist blind blademasters if they're being skipped. 
						// It would be easier to just remove them from targting however raid following behavior does not run while bot has aggro (Me.IsActuallyInCombat) I'll change this tho.
						if (Me.Combat)
						{
							foreach (WoWUnit bm in Targeting.Instance.TargetList
								.Where(t => t.Entry == BlindBlademasterId || (t.Entry == KorkronShadowmageId && t.Location.DistanceSqr(_doomLordLoc) < 20 * 20) || t.Entry == DoomlordId))
							{
								bool blacklisted = Blacklist.Contains(bm, BlacklistFlags.Combat);
								// if a tank decides to engage the blind blademaster then remove from blacklist.
								if (ScriptHelpers.GroupMembers.Any(g => g.IsTank && g.IsAlive && g.Location.Distance(bm.Location) < bm.MeleeRange()))
								{
									if (blacklisted)
										Blacklist.Clear(b => b.Guid == bm.Guid);
									continue;
								}

								if (blacklisted)
									continue;

								Blacklist.Add(bm, BlacklistFlags.Combat, TimeSpan.FromMinutes(3), string.Format("Skipping {0}", bm.SafeName));
							}
						}
						return RunStatus.Failure;
					})
				);
		}

		bool IsMoving
		{
			 get { return _isNotMovingTimer.IsFinished; }
		}

		#endregion

		#region Galakras

		private const uint HealingTideTotemId = 73086;
		private const uint DragonmawWarBannerId = 73088;
		private const uint FlameArrowsMissileId = 147552;
		private const uint FlameArrowsSpellId = 146764;
		private const uint KorkronDemolisherId = 72947;
		private const uint BombardMissileId = 148301;
		private const uint PoisonCloudSpellId = 147706;
		private const uint HighEnforcerThranokId = 72355;
		private const uint FlamesofGalakrondSpellIdSpellId = 146991;
		private const uint DragonmawEbonStalkerId = 72352;
		private const uint DragonmawProtoDrakeId = 72943;
		private const uint GalakrasId = 72249;

		private readonly WoWPoint _leftTowerLoc = new WoWPoint(1363.51, -4840.369, 71.84079);
		private readonly WoWPoint _rightTowerLoc = new WoWPoint(1462.703, -4803.909, 68.46022);

		[EncounterHandler(72249, "Galakras")]
		public Composite CreateBehavior_Galakras()
		{
			// these are casted by Dragonmaw Flameslinger
			AddAvoidLocation(
				ctx => true,
				4,
				o => ((WoWMissile) o).ImpactPosition,
				() => WoWMissile.InFlightMissiles.Where(m => m.SpellId == FlameArrowsMissileId));
			AddAvoidObject(
				ctx => true,
				4,
				o =>
				{
					var areaTriger = o as WoWAreaTrigger;
					return areaTriger != null && areaTriger.SpellId == FlameArrowsSpellId;
				});

			// avoid the bombardment from demolishers.
			AddAvoidLocation(
				ctx => true,
				6,
				o => ((WoWMissile) o).ImpactPosition,
				() => WoWMissile.InFlightMissiles.Where(m => m.SpellId == BombardMissileId));

			// avoid the poisonous cloud lanched by Korgra the Snake
			AddAvoidLocation(
				ctx => true,
				7,
				o => ((WoWMissile) o).ImpactPosition,
				() => WoWMissile.InFlightMissiles.Where(m => m.SpellId == PoisonCloudSpellId));

			AddAvoidObject(
				ctx => true,
				7,
				o =>
				{
					var areaTriger = o as WoWAreaTrigger;
					return areaTriger != null && areaTriger.SpellId == PoisonCloudSpellId;
				});

			// avoid this guy while he's spinning around
			AddAvoidObject(ctx => true, 10, o => o.Entry == HighEnforcerThranokId && o.ToUnit().HasAura("Skull Cracker"));

			// a ball of fire casted by the boss. 
			AddAvoidObject(
				ctx => true,
				8,
				o =>
				{
					var areaTriger = o as WoWAreaTrigger;
					return areaTriger != null && areaTriger.SpellId == FlamesofGalakrondSpellIdSpellId;
				});

			// [Dragonmaw Ebon Stalker : Melee Version] Avoid the front. These mobs do a 'Shadow Assault' frontal attack
			AddAvoidObject(
				ctx => Me.IsMeleeDps(),
				4.5f,
				o => o.Entry == DragonmawEbonStalkerId && o.ToUnit().IsAlive,
				o => WoWMathHelper.CalculatePointInFront(o.Location, o.Rotation, 4.5f));

			// [Dragonmaw Ebon Stalker : Ranged Version] Avoid the front. These mobs do a 'Shadow Assault' frontal attack
			AddAvoidObject(ctx => Me.IsRange(), 9f, o => o.Entry == DragonmawEbonStalkerId && o.ToUnit().IsAlive);

			// [Dragonmaw Proto Drake : Melee Version] Avoid the front. These mobs do a flame breath attack
			AddAvoidObject(
				ctx => Me.IsMeleeDps(),
				7f,
				o => o.Entry == DragonmawProtoDrakeId && o.ZDiff < 10 && o.ToUnit().IsAlive,
				o => WoWMathHelper.CalculatePointInFront(o.Location, o.Rotation, 7f));

			// [Dragonmaw Proto Drake : Ranged Version] Avoid mob. These mobs do a flame breath attack and tanks like to spin them around in LFR
			AddAvoidObject(
				ctx => Me.IsRange(),
				14f,
				o => o.Entry == DragonmawProtoDrakeId && o.ToUnit().IsAlive && o.ZDiff < 10);

			// [Galakras : Melee Version] Avoid the front. Boss does a flame breath attack
			AddAvoidObject(
				ctx => Me.IsMeleeDps(),
				10f,
				o => o.Entry == GalakrasId && o.ZDiff < 15 && o.ToUnit().IsAlive,
				o => WoWMathHelper.CalculatePointInFront(o.Location, o.Rotation, 10f));

			// [Galakras : Ranged Version] Avoid mob. Boss does a flame breath attack
			AddAvoidObject(
				ctx => Me.IsRange(),
				20f,
				o => o.Entry == GalakrasId && o.ZDiff < 15 && o.ToUnit().IsAlive);


			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit);
		}


		[LocationHandler(1425.201, -4888.474, 11.2178, 200, "Galakras Area")]
		public async Task<bool> GalakrasAreaHandler(WoWPoint location)
		{
			// ranged classes should not wander too far from raid. (not a problem for melee)
			return
				await
					ScriptHelpers.StayAtLocationWhile(
						() => Me.IsRange() && !Targeting.Instance.IsEmpty(),
						GetRangedGroupCenter(),
						"Group",
						25);
		}

		#endregion

		#region Iron Juggernaut

		private const uint BorerDrillId = 71906;
		private const uint ExplosiveTarId = 71950;
		private const uint CrawlerMineId = 72050;
		private const uint CutterLaserId = 72026;
		private const uint CutterLaserSpellId = 144576;
		private const uint MortarBlastMissile1Id = 146027;
		private const uint MortarBlastMissile2Id = 144316;
		private const uint DemolisherCannonMissileId = 144153;
		private const uint IronJuggernautId = 71466;

		[EncounterHandler(71466, "Iron Juggernaute")]
		public Func<WoWUnit, Task<bool>> CreateBehavior_IronJuggernaut()
		{
			// Don't stand under Iron Juggernaute. With all the avoidance the bot tends to end up under the boss a lot. 
			// This prevents that.
			// Range 22.5 is too big, 21.5 too small.
			AddAvoidObject(ctx => true, o => o.ToUnit().Combat ? 8 : 22.5f, o => o.Entry == IronJuggernautId && o.ToUnit().IsAlive);
			// don't stand in front of Iron Juggernaute
			AddAvoidObject(
				ctx => true,
				o => 10,
				o => o.Entry == IronJuggernautId && o.ToUnit().Combat,
				o => WoWMathHelper.CalculatePointInFront(o.Location, o.Rotation, 10));

			// Borer Drill. NM don't avoid these. already avoiding tons of stuff as is. Its LFR.
			//AddAvoidObject(ctx => true, 4, o => o.Entry == BorerDrillId && o.ToUnit().HasAura("Borer Drill"));

			// Explosive Tar
			AddAvoidObject(ctx => true, 5.5f, o => o.Entry == ExplosiveTarId, ignoreIfBlocking: true);

			// Mortar Blast
			AddAvoidLocation(
				ctx => true,
				8.5f,
				o => ((WoWMissile) o).ImpactPosition,
				() => WoWMissile.InFlightMissiles.Where(m => m.SpellId == MortarBlastMissile1Id || m.SpellId == MortarBlastMissile2Id));

			// Cutter Laser
			AddAvoidObject(ctx => true, o => Me.IsMoving ? 10 : 7, CutterLaserId);
			AddAvoidObject(
				ctx => true,
				3,
				o =>
				{
					var areaTriger = o as WoWAreaTrigger;
					return areaTriger != null && areaTriger.SpellId == CutterLaserSpellId;
				});


			return async boss =>
			{
				// ranged classes should no wander too far from raid. (not a problem for melee)
				if (Me.IsRange()
					&& await ScriptHelpers.StayAtLocationWhile(
						() => ScriptHelpers.IsViable(boss) && boss.Combat,
						GetRangedGroupCenter(),
						"group",
						15))
				{
					return true;
				}
				return false;
			};
		}

		#endregion

		#region Kor'kron Dark Shaman

		private const uint EarthbreakerHarommId = 71859;
		private const uint WavebinderKardrisId = 71858;
		private const uint ToxicStormId = 71801;
		private const uint ToxicTornadoId = 71817;
		private const uint FoulGeyserMissileId = 143992;
		private const uint FallingAshId = 71789;
		private const uint FoulStreamSpellId = 144090;
		private const uint FoulSlimeId = 71825;
		private const uint AshElementalId = 71827;

		private readonly WaitTimer _updateRangedGroupCenterPos = WaitTimer.FiveSeconds;
		private WoWPoint _rangedGroupCenter = WoWPoint.Zero;
		private readonly uint[] _darkShamanMobIds = {71859, 71858, 71923, 71921};
		[EncounterHandler(71859, "Earthbreaker Haromm", BossRange = 200)]
		[EncounterHandler(71858, "Wavebinder Kardris", BossRange = 200)]
		[EncounterHandler(71923, "Bloodclaw", BossRange = 200)]
		[EncounterHandler(71921, "Darkfang", BossRange = 200)]
		public Func<WoWUnit, Task<bool>> KorkronDarkShamanLogic()
		{
			// stay away from the foul slime. If it's targeting player then don't run cause you'll be running all over the place..
			AddAvoidObject(ctx => !Me.IsMelee(), 6, o => o.Entry == FoulSlimeId && o.ToUnit().CurrentTargetGuid != Me.Guid);
			// Avoid Toxic Storm
			AddAvoidObject(ctx => !Me.IsMelee() || !IsMoving, o => Me.IsRange() && Me.IsMoving ? 12 : 10f, ToxicStormId);
			// Avoid Toxic Tornado
			AddAvoidObject(ctx => true, 6, ToxicTornadoId);
			// Foul Geyser
			AddAvoidLocation(
				ctx => true,
				3,
				o => ((WoWMissile) o).ImpactPosition,
				() => WoWMissile.InFlightMissiles.Where(m => m.SpellId == FoulGeyserMissileId));

			// Falling ash. Very important to avoid. This is instant death if caught in it.
			AddAvoidObject(ctx => true, 18, FallingAshId);

			// Avoid the Ash Elements. These are un-attackable stationary NPCs that will attack nearby players.
			AddAvoidObject(ctx => !IsMoving, 4, AshElementalId);

			// Step out of the Foul Stream path when it's targeting another player.
			// ToDo: Foul Stream
			AddAvoidObject(
				ctx => true,
				7,
				o =>
				{
					if (o.Entry != WavebinderKardrisId)
						return false;
					var unit = (WoWUnit) o;
					return unit.CastingSpellId == FoulStreamSpellId && unit.GotTarget && unit.CurrentTargetGuid != Me.Guid;
				},
				o => Me.Location.GetNearestPointOnSegment(o.Location, o.ToUnit().CurrentTarget.Location));


			WoWPoint? foulStreamMovetoPoint = null;

			return async boss =>
			{
				// Groups will usually send in a mage or rogue to aggro bosses and then drop aggro to cause them to reset and 
				// spawn outside the building.
				// Bot should NOT try to attack the bosses when this is done.
				if (Me.IsFollower() && boss.HealthPercent > 97 && PercentOfGroupmembersInCombat < 50)
				{
					Logger.Write("Waiting for boss to get pulled out of building");
					return true;
				}

				if (boss.CastingSpellId == FoulStreamSpellId)
				{
					Logger.Write("Boss is casting foul stream!");
					if (boss.CurrentTargetGuid == Me.Guid)
					{
						// run to an empty side of boss if targeted with foul stream
						if (foulStreamMovetoPoint == null)
						{
							foulStreamMovetoPoint = GetBestFoulStreamMoveTo(boss);
						}
						return (await CommonCoroutines.MoveTo(foulStreamMovetoPoint.Value, "Moving Foul stream out of raid")).IsSuccessful();

					}
				}

				// reset the foul stream moveto location to it's default value since it's no-longer valid
				if (foulStreamMovetoPoint.HasValue)
				{
					foulStreamMovetoPoint = null;
				}

				// ranged classes should no wander too far from raid. (not a problem for melee)
				return Me.IsRange() && await ScriptHelpers.StayAtLocationWhile(
					() => ScriptHelpers.IsViable(boss) && boss.Combat,
					GetRangedGroupCenter(),
					"group",
					17);
			};
		}

		private WoWPoint GetRangedGroupCenter()
		{
			if (_rangedGroupCenter == WoWPoint.Zero || _updateRangedGroupCenterPos.IsFinished)
			{
				_rangedGroupCenter = ScriptHelpers.GetGroupCenterLocation(g => g.IsRange, 20);
				_updateRangedGroupCenterPos.Reset();
			}
			return _rangedGroupCenter;
		}

		private WoWPoint GetBestFoulStreamMoveTo(WoWUnit boss)
		{
			IEnumerable<WoWPoint> groupLocs = Me.RaidMembers.Where(r => !r.IsMe).Select(r => r.Location);
			WoWPoint bossLoc = boss.Location;
			WoWPoint myLoc = Me.Location;
			// the idea is to find a nearby spot where the least amount of raid members would get hit by Foul Stream
			return
				GetCirclePointsAroundLocation(bossLoc, myLoc.Distance(bossLoc), 10)
					.OrderBy(l => groupLocs.Count(gl => gl.GetNearestPointOnSegment(bossLoc, l).Distance2DSqr(gl) < 3.5f*3.5f))
					.ThenBy(l => l.DistanceSqr(myLoc))
					.FirstOrDefault(l => l.DistanceSqr(myLoc) < 30*30 && Navigator.CanNavigateFully(myLoc, l));
		}

		private IEnumerable<WoWPoint> GetCirclePointsAroundLocation(WoWPoint center, float dist, float stepDegrees = 40)
		{
			for (float ang = 0; ang < 360; ang += stepDegrees)
			{
				float radians = WoWMathHelper.DegreesToRadians(ang);
				yield return center.RayCast(radians, dist);
			}
		}

		private PerFrameCachedValue<float> _percentOfGroupmembersInCombat;
		private float PercentOfGroupmembersInCombat
		{
			get
			{
				return _percentOfGroupmembersInCombat ?? (_percentOfGroupmembersInCombat = new PerFrameCachedValue<float>(
				() =>
				{
					var groupMembers = Me.RaidMembers;
					// don't divide by zero
					if (groupMembers.Count == 0)
						return 0;
					return groupMembers.Count(g => g.Combat) / (float)groupMembers.Count * 100;
				}));
			}
		}

		#endregion

		#region General Nazgrim

		private const uint GeneralNazgrimId = 71515;
		private const uint KorkronWarshamanId = 71519;
		private const uint HealingTideTotem2Id = 72563;
		private const uint KorkronArcweaverId = 71517;
		private const uint KorkronAssassinId = 71518;
		private const uint KorkronIronbladeId = 71516;
		private const uint KorkronBannerId = 71626;
		private const uint AfterShockId = 71697;
		private const uint RavagerId = 71762;
		private const uint KorkronShadowmageId = 72150;
		private readonly WoWPoint _doomLordLoc = new WoWPoint(1802.584, -4338.643, -11.04997);
		private readonly int[] _ironstormSpellIds = {145566, 143420};
		private bool _ignoreNazgrimAdds;

		[EncounterHandler(71515, "General Nazgrim")]
		public Composite CreateBehavior_GeneralNazgrim()
		{
			// these npcs spawn and cast a Aftershock presumably in a line in front of them.
			AddAvoidObject(
				ctx => true,
				7,
				o => o.Entry == AfterShockId,
				o =>
				{
					var unit = (WoWUnit) o;
					WoWPoint endPoint = unit.Location.RayCast(unit.Rotation, 17);
					return Me.Location.GetNearestPointOnSegment(unit.Location, endPoint);
				});

			AddAvoidLocation( ctx => true, 7, o => (WoWPoint) o, GetAfterShockLocations);

			AddAvoidObject(ctx => true, o => Me.IsRange() && Me.IsMoving ? 12 : 9, RavagerId);
			AddAvoidObject(
				ctx => true,
				8,
				o => o.Entry == KorkronIronbladeId && _ironstormSpellIds.Contains(o.ToUnit().CastingSpellId));

			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				new Action(
					ctx =>
					{
						_ignoreNazgrimAdds = boss.HasAura("Berserk");
						return RunStatus.Failure;
					}));
		}

		// this generates a sequence of locations in straight lines
		IEnumerable<object> GetAfterShockLocations()
		{
			var afterShocks = ObjectManager.GetObjectsOfTypeFast<WoWUnit>().Where(o => o.Entry == AfterShockId).ToList();
			if (!afterShocks.Any())
				yield break;

			foreach (var afterShock in afterShocks)
			{
				for (float dist = 0; dist <= 17.5; dist += 3.5f)
				{
					yield return afterShock.Location.RayCast(afterShock.Rotation, dist);
				}
			}
		}
		#endregion
	}
}