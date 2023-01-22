
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bots.DungeonBuddy.Behaviors;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.POI;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
using AvoidanceManager = Bots.DungeonBuddy.Avoidance.AvoidanceManager;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Mists_of_Pandaria
{
	public class SiegeOfNiuzaoTemple : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 630; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(1441.83, 5089.116, 139.3459); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint (1447.552, 5093.06, 144.0029);}
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (unit != null)
					{
						if (_commanderVojakAdds.Contains(unit.Entry) && Me.IsTank() && unit.ZDiff > 5)
							return true;
						if (unit.Entry == AmberWingId && (Me.IsMelee() || _doAlternativeVojakStart))
							return true;
						if (unit.Entry == SikthikFlyerId)
							return true;
						if (unit.Entry == SikthikDemolisherId && Me.IsTank())
							return true;
						if (unit.Entry == CommanderVojakId && Me.IsMelee() && unit.HasAura("Thousand Blades") )
							return true;
						if (unit.Entry == GeneralPavalakId && unit.HasAura("Bulwark"))
							return true;
					}
					return false;
				});
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			foreach (var obj in incomingunits)
			{
				var unit = obj as WoWUnit;
				if (unit != null)
				{
					if (unit.Entry == SikthikDemolisherId && !_doAlternativeVojakStart && unit.Y < 5380 && Me.IsRangeDps())
						outgoingunits.Add(unit);

					if ((_commanderVojakAdds.Contains(unit.Entry) || unit.Entry == CommanderVojakId) && Me.IsTank() && unit.ZDiff <= 5)
						outgoingunits.Add(unit);
				}
			}
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var priority in units)
			{
				var unit = priority.Object as WoWUnit;
				if (unit != null)
				{
					if (unit.Entry == SikthikDemolisherId)
						priority.Score += 5500;

					if (unit.Entry == SikthikWarriorId && Me.IsDps())
						priority.Score = +5000;
				}
			}
		}

		public override void OnEnter()
		{
			_trashGroupAtGateBs =
			new DynamicBlackspot(
				() => ScriptHelpers.GetUnfriendlyNpsAtLocation(_trashGroupAtGateLoc, 15).Any(),
				() => _trashGroupAtGateLoc,
				LfgDungeon.MapId,
				25,
				name: "Trash group at gate");
			DynamicBlackspotManager.AddBlackspot(_trashGroupAtGateBs);
		}

		public override void OnExit()
		{
			DynamicBlackspotManager.RemoveBlackspot(_trashGroupAtGateBs);
		}

		public override MoveResult MoveTo(WoWPoint location)
		{
			var myLoc = Me.Location;
			// force ranged and melee dps to stand at the platform edge while on Vojak encounter. Melee should not go down
			if (!_doAlternativeVojakStart && (location.Y >= 5314 || location.Z < 178 || myLoc.Z < 178) 
				&& _vojak != null && _vojak.IsValid && _vojak.IsAlive && (Me.IsRange() || Me.IsMeleeDps()))
			{
				if (myLoc.DistanceSqr(_vojakPlatformCenterLoc) < 50 * 50 || myLoc.Z < 178)
				{
					var moveto = myLoc.GetNearestPointOnSegment(_vojakEdgethrowStart, _vojakEdgeThrowEnd);
					if (myLoc.DistanceSqr(moveto) > 1.5 * 1.5)
					{
						Navigator.MoveTo(moveto);
					}                   
				}
				return MoveResult.Moved;
			}
			return base.MoveTo(location);
		}

		#endregion

		#region Root

		private const uint SapPuddleId = 61965;
		private const uint SapPuddle2Id = 61579;
		private const uint SapPuddleBossId = 61613;
		private const uint VizierJinbakId = 61567;
		private const uint AmberWingId = 61699;

		private const int DetonateId = 120001;
		private const uint CommanderVojakId = 61634;
		private const uint SikthikDemolisherId = 61670;
		private const uint HardenResin = 213174;
		private const uint SikthikWarriorId = 61701;
		private const uint SikthikSwarmerId = 63106;
		private const uint SikthikFlyerId = 62091;
		private const uint ShadoPanPrisonerId = 64520;
		private const uint SomewhereInsideQuestId = 31365;
		private const uint GeneralPavalakId = 61485;
		private const uint SikthikSoldierId = 61448;
		private const uint SiegeExplosivesId = 61452;

		private readonly uint[] _commanderVojakAdds = new[] {SikthikDemolisherId, SikthikWarriorId, SikthikSwarmerId};
		private WoWUnit _generalPavalak;

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0)]
		public Func<WoWUnit, Task<bool>> RootBehavior()
		{
			WaitTimer isMovingTimer = WaitTimer.OneSecond;
			AddAvoidObject(
				ctx =>
				{
					if (!Me.IsMoving)
						isMovingTimer.Reset();
					return !isMovingTimer.IsFinished;
				},
				4f,
				u => u.Entry == SapPuddleId && u.ToUnit().HasAura("Sap Puddle") && u.ZDiff < 6);

			return async npc =>
						{
							var hardenedResin = ObjectManager.GetObjectsOfType<WoWGameObject>()
								.FirstOrDefault(u => u.Entry == HardenResin && u.State == WoWGameObjectState.Ready && u.Distance <= 10);
							if (hardenedResin != null)
								return await HardenedResinHandler(hardenedResin);
							return false;
						};
		}

		[EncounterHandler(64517, "Shado-Master Chum Kiu", Mode = CallBehaviorMode.Proximity)]
		public async Task<bool> QuestPickupTurninHandler(WoWUnit npc)
		{
			if (Me.Combat || ScriptHelpers.WillPullAggroAtLocation(npc.Location))
				return false;
			// pickup or turnin quests if any are available.
			return npc.HasQuestAvailable(true)
				? await ScriptHelpers.PickupQuest(npc)
				: npc.HasQuestTurnin() && await ScriptHelpers.TurninQuest(npc);
		}

		[EncounterHandler(64520, "Shado-Pan Prisoner", BossRange = 1000, Mode = CallBehaviorMode.Proximity)]
		public async Task<bool> ShadoPanPrisonerEncounter(WoWUnit npc)
		{
			if (!ScriptHelpers.HasQuest(SomewhereInsideQuestId) || ScriptHelpers.IsQuestInLogComplete(SomewhereInsideQuestId))
				return false;

			if (!npc.CanSelect || Blacklist.Contains(npc, BlacklistFlags.Interact))
				return false;

			if (!BotPoi.Current.Type.IsOneOf(PoiType.None, PoiType.Hotspot))
				return false;

			var npcLoc = npc.Location;

			if (Me.IsActuallyInCombat || ScriptHelpers.WillPullAggroAtLocation(npcLoc))
				return false;

			var myLoc = Me.Location;
			var distSqr = myLoc.DistanceSqr(npcLoc);
			if (!Navigator.CanNavigateWithin(myLoc, npcLoc, 5.5f))
			{
				Blacklist.Add(npc, BlacklistFlags.Interact, TimeSpan.FromMinutes(3), "Unable to navigate to");
				return false;
			}
			
			// only move to 
			if (distSqr > 45 * 45 && (Me.IsFollower() || !Targeting.Instance.IsEmpty()))
				return false;

			if (distSqr > 5.5 * 5.5)
				return (await CommonCoroutines.MoveTo(npcLoc, npc.SafeName)).IsSuccessful();

			await ScriptHelpers.StopMovingIfMoving();
			npc.Interact();
			Blacklist.Add(npc, BlacklistFlags.Interact, TimeSpan.FromMinutes(3), "Interacted with");
			return true;
		}

		#endregion

		#region Trash avoidance

		private readonly WoWPoint _trashGroupAtGateLoc = new WoWPoint(1652.209, 5394.023, 140.0079);
		private DynamicBlackspot _trashGroupAtGateBs;

		#endregion

		#region Vizier Jin'bak

		[EncounterHandler(61567, "Vizier Jin'bak", Mode = CallBehaviorMode.Proximity, BossRange = 120)]
		public Composite VizierJinbakEncounter()
		{
			WoWUnit boss = null;

			AddAvoidObject(
				ctx => boss != null && boss.IsValid && boss.CastingSpellId == DetonateId,
				obj => 5 * (obj.Scale * 4 / 5 + 1),
				SapPuddleBossId);

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				new Decorator(ctx => !boss.CanSelect, ScriptHelpers.CreateClearArea(() => boss.Location, 100, u => u.Z <= 175)));
		}

		#endregion

		#region Commander Vo'jak

		public async Task<bool> HardenedResinHandler(WoWGameObject resin)
		{
			if (!ScriptHelpers.IsViable(resin) || Me.Combat)
				return false;

			Logger.Write("Knocking away hardened resin");
			await ScriptHelpers.StopMovingIfMoving();
			resin.Interact();
			// wait for cast and then for cast to stop
			if (!await Coroutine.Wait(2000, () => Me.IsCasting))
				return false;
			return await Coroutine.Wait(3000, () =>  !Me.IsCasting);
		}

		readonly WoWPoint _vojakEdgethrowStart = new WoWPoint(1482.407, 5312.085, 184.6487);
		readonly WoWPoint _vojakEdgeThrowEnd = new WoWPoint(1558.097, 5317.704, 184.6507);
		readonly WoWPoint _vojakPlatformCenterLoc = new WoWPoint(1521.815, 5294.988, 184.6694);
		WoWUnit _vojak;
		// when true, just follow tank around and nuke adds down (supposedly done in highly geard groups).
		private bool _doAlternativeVojakStart;
		[EncounterHandler(61634, "Commander Vo'jak", Mode = CallBehaviorMode.Proximity, BossRange = 8000)]
		public Func<WoWUnit, Task<bool>> CommanderVojakEncounter()
		{
			var altStratLoc = new WoWPoint(1563.661, 5339.688, 161.214);
			var barrelThrowStart = new WoWPoint(1505.429, 5329.327, 161.2022);
			var barrelThrowEnd = new WoWPoint(1561.556, 5333.001, 161.2266);

			var incAddsLoc = new WoWPoint(1510.148, 5372.519, 139.0868);

			WoWPoint dashingStrikeLoc = WoWPoint.Zero;
			var dashingStrikeTimer = new WaitTimer(TimeSpan.FromSeconds(5));

			AddAvoidObject(
				ctx => true,
				() => _vojakPlatformCenterLoc,
				25,
				15,
				u => u.Entry == CommanderVojakId && (u.ToUnit().HasAura("Thousand Blades") || u.ToUnit().HasAura("Dashing Strike")),
				o =>
				{
					var unit = (WoWUnit)o;
					if (unit.HasAura("Dashing Strike"))
					{ // run away from current target of boss 
						if (dashingStrikeTimer.IsFinished && unit.CurrentTargetGuid > 0)
						{
							dashingStrikeLoc = unit.CurrentTarget.Location;
							dashingStrikeTimer.Reset();
						}
						return dashingStrikeLoc;
					}
					return unit.Location;
				});

			AddAvoidObject( ctx => !Me.IsTank() || (_vojak != null && _vojak.IsValid && !_vojak.HasAura("Rising Speed")), 7.5f, SapPuddle2Id);
			AddAvoidObject(ctx => !Me.IsTank() && !_doAlternativeVojakStart, 20f, u => u.Entry == SikthikDemolisherId && u.ZDiff <= 5);

			return async boss =>
			{
				var leader = ScriptHelpers.Leader;
				var isLeader = leader != null && leader.IsMe;
				_doAlternativeVojakStart = leader != null && leader.Z < 165 && leader.Location.DistanceSqr(altStratLoc) < 25*25;
				var yang = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == 61620);
				var incAdds =
					ScriptHelpers.GetUnfriendlyNpsAtLocation(
						incAddsLoc,
						90,
						u => _commanderVojakAdds.Contains(u.Entry) && u.Z < 176 && u.Y < 5380).ToArray();

				var keg =
					ObjectManager.GetObjectsOfType<WoWUnit>()
						.Where(
							u =>
								u.Entry == 61817 && u.HasAura("Mantid Barrel Active") && u.TransportGuid == 0 &&
								Me.RaidMembers.OrderBy(r => r.Location.DistanceSqr(u.Location)).FirstOrDefault() == Me)
						.OrderBy(u => u.DistanceSqr)
						.FirstOrDefault();

				var causticTarPuddle =
					ObjectManager.GetObjectsOfType<WoWUnit>()
						.Where(u => u.Entry == SapPuddle2Id && u.Z > 182)
						.OrderBy(u => u.DistanceSqr)
						.FirstOrDefault();

				_vojak = boss;

				 // start the event
				if (isLeader && yang != null && yang.CanGossip && Targeting.Instance.IsEmpty())
					return await ScriptHelpers.TalkToNpc(yang);

				// should I pick up a keg of tar?
				if (!_doAlternativeVojakStart && keg != null && !AvoidanceManager.IsRunningOutOfAvoid 
					&& !Me.HasAura("Carrying Caustic Tar")
					&& (Targeting.Instance.IsEmpty() && !_vojak.Attackable 
					|| (Me.IsRangeDps() && Targeting.Instance.TargetList.All(u => u.Entry != SikthikDemolisherId))
					|| (!Me.IsRange() && incAdds.Any())
					|| (_vojak.Z > 182 && _vojak.HasAura("Rising Speed") && causticTarPuddle == null && Me.IsDps())))
				{
					return await ScriptHelpers.InteractWithObject(keg, ignoreCombat:true);
				}

				if (Me.HasAura("Carrying Caustic Tar") && !AvoidanceManager.IsRunningOutOfAvoid)
				{
					var kegThrowToLoc = _vojak.Z > 182 
						? _vojak.Location 
						: Me.Location.GetNearestPointOnSegment(barrelThrowStart, barrelThrowEnd);
				   
					var kegThrowFromLoc =
								_vojak.Z > 182
									? Me.Location.GetNearestPointOnSegment(_vojak.Location, WoWMathHelper.CalculatePointFrom(Me.Location, _vojak.Location, 25))
									: Me.Location.GetNearestPointOnSegment(_vojakEdgethrowStart, _vojakEdgeThrowEnd);

					if (PreciseMoveto(kegThrowFromLoc))
					{
						if (Navigator.AtLocation(kegThrowFromLoc))
							SpellManager.ClickRemoteLocation(kegThrowToLoc);
						return true;
					}
				}

				if (_vojak.HasAura("Rising Speed") && causticTarPuddle != null && isLeader && _vojak.CurrentTargetGuid == Me.Guid)
				{
					var moveTo = WoWMathHelper.CalculatePointFrom(_vojak.Location, causticTarPuddle.Location, -5);
					// force boss to walk through a tar puddle so Rising Speed buff is removed.
					return(await CommonCoroutines.MoveTo(moveTo, "side of a tar puddle that is opposite of boss")).IsSuccessful();
				}

				// tank stuff in the center of the playform.
				if (isLeader && Targeting.Instance.TargetList.All(u => u.CurrentTargetGuid == Me.Guid)
					&& await ScriptHelpers.TankUnitAtLocation(_vojakPlatformCenterLoc, 5))
				{
					return true;
				}

				if (await ScriptHelpers.DispelGroup("Caustic Tar", ScriptHelpers.PartyDispelType.Magic))
					return true;

				// stay on playform.
				if (isLeader && Targeting.Instance.IsEmpty())
				{
					if (Me.Location.DistanceSqr(_vojakPlatformCenterLoc) > 30*30)
						return (await CommonCoroutines.MoveTo(_vojakPlatformCenterLoc, "Vojak Platform")).IsSuccessful();
					
					// do nothing while waiting for incoming mobs.
					return true;
				}          
				return false;
			};
		}

		private bool PreciseMoveto(WoWPoint destination)
		{
			var myLoc = Me.Location;
			// Use the navigator if not close to destination
			// otherwise use CTM directly since the Navigator will CTM past destination
			if (myLoc.Distance(destination) > 15 || Math.Abs(myLoc.Z - destination.Z) > 3)
			{
				var moveResult = Navigator.MoveTo(destination);
				if (moveResult == MoveResult.PathGenerationFailed || moveResult == MoveResult.Failed)
					return false;
			}
			else
			{
				Navigator.PlayerMover.MoveTowards(destination);
			}
			return true;
		}

		#endregion

		#region General Pa'valak

		[EncounterHandler(61485, "General Pa'valak", BossRange = 300, Mode = CallBehaviorMode.Proximity)]
		public Composite GeneralPavalakEncounter()
		{
			WoWUnit trash = null;
			WoWUnit bomb = null;
			WoWPoint bombTarget = WoWPoint.Zero;

			var trashAreaCenterLoc = new WoWPoint(1694.098, 5275.564, 124.18);
			var trashPullToLoc = new WoWPoint(1715.117, 5240.054, 123.7021);
			const uint bladeRushId = 63720;
			// run from blade rush target.
			AddAvoidObject(ctx => true, 8, bladeRushId);
			// run from the bombs that have armed
			AddAvoidObject(ctx => true, 7, u => u.Entry == SiegeExplosivesId && u.ToUnit().HasAura("Bomb Armed"));
			

			return new PrioritySelector(
				ctx => _generalPavalak = ctx as WoWUnit,
				new Decorator(
				// clear the area near the boss before engaging
					ctx => !_generalPavalak.Combat,
					new PrioritySelector(
						ctx => trash = (from unit in ObjectManager.GetObjectsOfTypeFast<WoWUnit>()
									   where unit != _generalPavalak && unit.IsAlive && unit.CanSelect && unit.Attackable && unit.IsHostile
									   let distSqr = unit.Location.DistanceSqr(trashAreaCenterLoc)
									   where distSqr < 70 * 70
									   orderby distSqr descending 
									   select unit).FirstOrDefault(),
						new Decorator(
							ctx => trash != null,
							new PrioritySelector(
								new Decorator(ctx => Me.Location.Distance(trashPullToLoc) > 40, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(trashPullToLoc))),
								new Decorator(
									ctx => Me.Location.Distance(trashPullToLoc) <= 40, ScriptHelpers.CreatePullNpcToLocation(ctx => true, ctx => trash, ctx => trashPullToLoc, 6)))))),
				// the boss combat behavior. 
				new Decorator(
					ctx => _generalPavalak.Combat,
					new PrioritySelector(
						ctx =>
						bomb =
						ObjectManager.GetObjectsOfType<WoWUnit>()
									 .Where(
										 u =>
										 u.Entry == SiegeExplosivesId && !u.HasAura("Bomb Armed") && u.Distance <= 30 &&
										 !ScriptHelpers.GroupMembers.Any(
											 g =>
											 !g.IsHealer && g.Player != null && g.Player.IsSafelyFacing(u, 45) &&
											 g.Location.Distance(u.Location) < Me.Location.Distance(u.Location)))
									 .OrderBy(u => u.DistanceSqr)
									 .FirstOrDefault(),
						new Decorator(
							ctx => Me.HasAura("Siege Explosive"),
							new PrioritySelector(
								ctx => bombTarget = GetBestBombThrowLocation(),
								new Decorator(ctx => bombTarget != WoWPoint.Zero,
									new PrioritySelector(
										new ActionLogger("Thowing bomb at adds"),
										new Action(ctx => SpellManager.ClickRemoteLocation(bombTarget)))),
								new Decorator(ctx => bombTarget == WoWPoint.Zero && !_generalPavalak.HasAura("Bulwark"),
									new PrioritySelector(
										new ActionLogger("Throwing bomb at boss"),
										new Action(ctx => SpellManager.ClickRemoteLocation(_generalPavalak.Location)))))),
				// pickup a bomb and throw it.
						new Decorator(
							ctx => bomb != null && !Me.HasAura("Siege Explosive") && (Me.IsDps() || Targeting.Instance.TargetList.All(t => t.CurrentTargetGuid == Me.Guid) && Me.IsTank()),
							new PrioritySelector(
								new Decorator(
									ctx => bomb.WithinInteractRange,
									new Sequence(
										new Action(ctx => bomb.Interact()),
										new WaitContinue(2, ctx => Me.HasAura("Siege Explosive"), new ActionAlwaysSucceed()),
										new DecoratorContinue(
											ctx => Me.HasAura("Siege Explosive"),
											new Sequence(
												ctx => GetBestBombThrowLocation(),
												new DecoratorContinue(ctx => bombTarget != WoWPoint.Zero, new Action(ctx => SpellManager.ClickRemoteLocation(bombTarget))))))),
								new Decorator(ctx => !bomb.WithinInteractRange, new Action(ctx => Navigator.GetRunStatusFromMoveResult(Navigator.MoveTo(bomb.Location)))))),
						new Decorator(ctx => _generalPavalak.HasAura("Bulwark"), ScriptHelpers.CreateTankUnitAtLocation(ctx => _generalPavalak.Location, 25)),
						ScriptHelpers.CreateDispelGroup("Temptest", ScriptHelpers.PartyDispelType.Magic),
				// spread out a little for blade rush.
						ScriptHelpers.CreateSpreadOutLogic(ctx => !_generalPavalak.HasAura("Bulwark"), ctx => _generalPavalak.Location, 5, 30))));
		}

		private WoWPoint GetBestBombThrowLocation()
		{
			using (StyxWoW.Memory.AcquireFrame())
			{
				var myLoc = Me.Location;
				// cache locations to improve performance...
				var targets = (from unit in ObjectManager.GetObjectsOfType<WoWUnit>()
							   where unit.Entry == SikthikSoldierId && unit.IsAlive && unit.CurrentTargetGuid != 0
							   let loc = unit.Location
							   let facing = WoWMathHelper.NormalizeRadian(unit.Rotation)
							   where loc.DistanceSqr(myLoc) < 60 * 60
							   select new { Unit = unit, Location = loc }).ToList();

				var bombTarget =
					targets.OrderByDescending(t => targets.Count(c => c.Location.DistanceSqr(t.Location) <= 10 * 10))
					.FirstOrDefault();
				if (bombTarget != null)
				{
					var unit = bombTarget.Unit;
					Logger.Write("Throwing at {0} hp {2}/{1}", unit, unit.MaxHealth, unit.CurrentHealth);
					return bombTarget.Location;
				}
				return WoWPoint.Zero;
			}
		}

		#endregion

		#region Wing Leader Ner'onok

		private const uint GustingWindsSpellId = 121282;

		[EncounterHandler(62205, "Wing Leader Ner'onok")]
		public Func<WoWUnit,Task<bool>>  WingLeaderNeronokEncounter()
		{
			const int causticPitchMissileId = 121441;
			const int causticPitchId = 121443;
			const int quickDryResinId = 121447;
			const int gustingWindsId = 121284;
			var isMovingTimer = new WaitTimer(TimeSpan.FromSeconds(2));
			var southWestLoc = new WoWPoint(1812.138, 5247.078, 131.1701);
			var northEastLoc = new WoWPoint(1882.172, 5184.058, 131.1692);
			bool moveToBoss = false;

			AddAvoidObject(
				ctx =>
				{
					if (moveToBoss) return false;
					if (!Me.IsMoving)
						isMovingTimer.Reset();
					return !isMovingTimer.IsFinished;
				},
				2.5f,
				o => o.Entry == causticPitchId, 
				ignoreIfBlocking: true);

			AddAvoidLocation(
				ctx => 
				{
					if (moveToBoss) return false;
					if (!Me.IsMoving)
						isMovingTimer.Reset();
					return !isMovingTimer.IsFinished;
				}, 
				2.5f,
				o => ((WoWMissile)o).ImpactPosition, 
				() => WoWMissile.InFlightMissiles.Where(m => m.SpellId == causticPitchMissileId),
				true);

			return async boss =>
			{
				if (await ScriptHelpers.InterruptCast(boss, gustingWindsId))
					return true;

				// pre-emptively move to the opposite when boss if flying.
				if (boss.Z > 140)
				{
					moveToBoss = true;
					WoWPoint moveTo;
					string moveToName;
					if (boss.IsSafelyFacing(southWestLoc))
					{
						moveTo = southWestLoc;
						moveToName = "South west end of bridge";
					}
					else
					{
						moveTo = northEastLoc;
						moveToName = "North east end of bridge";
					}
					return (await CommonCoroutines.MoveTo(moveTo, moveToName)).IsSuccessful();
				}
				if (!boss.InLineOfSpellSight)
				{
					moveToBoss = true;
					return (await CommonCoroutines.MoveTo(boss.Location, boss.SafeName)).IsSuccessful();
				}
				moveToBoss = false;

				if (boss.ChanneledCastingSpellId == GustingWindsSpellId)
				{
					Navigator.NavigationProvider.StuckHandler.Reset();
					await CommonCoroutines.MoveTo(boss.Location);
					return true;
				}

				if (Me.HasAura(quickDryResinId))
				{
					WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
					await Coroutine.Sleep(120);
					WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend);
					return true;
				}
				return false;
			};
		}

		#endregion

	}

	public class SiegeOfNiuzaoTempleHeroic : SiegeOfNiuzaoTemple
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 554; }
		}

		#endregion
	}
}