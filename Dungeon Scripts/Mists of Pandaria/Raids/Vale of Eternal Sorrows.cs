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
	public class ValeOfEternalSorrows : Dungeon
	{
		#region Overrides of Dungeon

		private readonly WoWPoint _entrance = new WoWPoint(1242.479, 600.5036, 318.0787);

		public override uint DungeonId
		{
			get { return 726; }
		}

		public override WoWPoint Entrance
		{
			get { return _entrance; }
		}

		#region Movement

		private static WoWPoint _scarredValeCorner1 = new WoWPoint(1215.411, 979.4381, 417.3395);
		private static WoWPoint _scarredValeCorner2 = new WoWPoint(970.0, 809.8154, 505.797);

		private bool PointIsInScarredVale(WoWPoint point)
		{
			return point.X <= _scarredValeCorner1.X && point.Y <= _scarredValeCorner1.Y && point.X >= _scarredValeCorner2.X &&
					point.Y >= _scarredValeCorner2.Y;
		}

		#endregion

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					WoWUnit unit = ret.ToUnit();
					if (unit != null)
					{
						if ((unit.Entry == HeSoftfootId || unit.Entry == RookStonetoeId || unit.Entry == SunTenderheartId) &&
							unit.CurrentHealth <= 1)
							return true;
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
					if (unit.Entry == ShaPuddleId)
						outgoingunits.Add(unit);
					else if (unit.Entry == ManifestationofCorruptionPhasedId || unit.Entry == EssenceofCorruptionPhasedId)
						outgoingunits.Add(unit);
				}
			}
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
						// Fallen Protectors encounter add
						case EmbodiedMiseryId:
							priority.Score += 2000;
							continue;
						case EmbodiedGloomId: // Fallen Protectors encounter add
						case ReflectionId: // Sha of Pride encounter add
						case EmbodiedSorrowId: // Fallen Protectors encounter add
							priority.Score += 3000;
							continue;
						case EmbodiedAnguishId: // Fallen Protectors encounter add
							priority.Score += 5000;
							continue;
						case ManifestationofCorruptionId: // Norushen encounter add
							//  remove all previous weights and only factor distance in the final weight.
							priority.Score = (Me.IsRange() || !AvoidBlindHatred ? 4000 : -4000) - unit.Distance;
							continue;
						case ManifestationofCorruptionPhasedId: // Norushen Look Within dps only encounter add
							priority.Score = 4000 - unit.Distance; //  remove all previous weights and only factor distance in the final weight.
							continue;
						case EssenceofCorruptionId: // Norushen encounter add
							//  remove all previous weights and only factor distance in the final weight.
							priority.Score = (Me.IsRange() || !AvoidBlindHatred ? 3000 : -3000) - unit.Distance;
							continue;
						case EssenceofCorruptionPhasedId: // Norushen Look Within dps only encounter add
							priority.Score = 3000 - unit.Distance; //  remove all previous weights and only factor distance in the final weight.
							continue;

						case ShaPuddleId: // Immerseus encounter. 
						case DesperationSpawnId: //  Fallen Protectors encounter. 
							priority.Score = 2000 - unit.Distance; // remove all previous weights and only factor distance in the final weight.
							continue;
						case ManifestationOfPrideId: // Sha of Pride encounter add
							if (Me.IsRange())
								priority.Score += 3000;
							continue;
					}
				}
			}
		}

		public override void OnEnter()
		{
			_trashMobsToAvoid = new List<DynamicBlackspot>
								{
									new DynamicBlackspot(
										ShouldAvoidLeftEntranceSide,
										() => _leftEntranceTrashPackLoc,
										LfgDungeon.MapId,
										15,
										10,
										"Left Entrance Steps"),
									new DynamicBlackspot(
										ShouldAvoidRightEntranceSide,
										() => _rightEntranceTrashPackLoc,
										LfgDungeon.MapId,
										15,
										10,
										"Right Entrance Steps")
								};

			_trashMobsToAvoid.AddRange(GetScaredValeBlackspots());

			DynamicBlackspotManager.AddBlackspots(_trashMobsToAvoid);

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
		}

		public override void OnExit()
		{
			DynamicBlackspotManager.RemoveBlackspots(_trashMobsToAvoid);
			_trashMobsToAvoid = null;
		}

		public override void IncludeHealTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			// because some combat routine's HealTargeting class will only include WoWPlayer types we will need to pull any other type from ObjectManager directly
			foreach (WoWObject healTarget in ObjectManager.ObjectList.Where(o => _healTargetIds.Contains(o.Entry)))
			{
				outgoingunits.Add(healTarget);
			}
		}

		public override void RemoveHealTargetsFilter(List<WoWObject> units)
		{
			bool immerseusEncounterInProcess = _immerseus != null && _immerseus.IsValid && _immerseus.Combat;
			units.RemoveAll(
				o =>
				{
					var unit = o as WoWUnit;
					if (unit == null) return false;
					// remove all heal targets that are not in range. Everyone is spread out so just heal stuff that is in range.
					if (immerseusEncounterInProcess && unit.Distance > 40)
						return true;
					return false;
				});
		}

		public override void WeighHealTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (Targeting.TargetPriority priority in units)
			{
				var unit = priority.Object as WoWUnit;
				if (unit != null)
				{
					switch (unit.Entry)
					{
						case RookStonetoeLookWithinHealerSpawnId:
						case SunTenderheartLookWithinHealerSpawnId:
						case LevenDawnbladeLookWithinHealerSpawnId:
							priority.Score += 100000;
							break;
						case ContaminatedPuddleId:
							priority.Score -= unit.Distance;
							break;
					}
				}
			}
		}

		#endregion

		#region Root Behavior

		private const uint ShaVortexMissileSpellVisualId = 33181;
		private const uint VortexMissileSpellId = 147308;

		private const int SwirlZoneVisualId = 33691;


		private readonly uint[] _healTargetIds =
		{
			RookStonetoeLookWithinHealerSpawnId, SunTenderheartLookWithinHealerSpawnId, LevenDawnbladeLookWithinHealerSpawnId,
			ContaminatedPuddleId
		};

		private readonly WoWPoint _leftEntranceTrashPackLoc = new WoWPoint(1365.71, 419.9323, 263.1555);

		private readonly WaitTimer _leftSideTimer = new WaitTimer(TimeSpan.FromSeconds(5));
		private readonly WoWPoint _rightEntranceTrashPackLoc = new WoWPoint(1527.006, 428.5891, 261.0348);
		private readonly WaitTimer _rightSideTimer = new WaitTimer(TimeSpan.FromSeconds(5));


		private List<DynamicBlackspot> _trashMobsToAvoid;

		[EncounterHandler(0)]
		public Composite RootBehavior()
		{
			// Spirling missiles used by trash on the way to Immereus 
			AddAvoidLocation(
				ctx => true,
				2,
				o => ((WoWMissile)o).Position,
				() => WoWMissile.InFlightMissiles.Where(m => m.SpellId == VortexMissileSpellId));
			// Rushing Waters abilty used by trash on the way to Immereus 
			AddAvoidObject(
				ctx => true,
				2,
				o => o is WoWAreaTrigger && ((WoWAreaTrigger)o).SpellVisualId == SwirlZoneVisualId && o.Distance < 20);
			// Avoid these missles while clearing Sha of Pride trash 
			AddAvoidLocation(
				ctx => true,
				4,
				o => ((WoWMissile)o).ImpactPosition,
				() => WoWMissile.InFlightMissiles.Where(m => m.SpellVisualId == ShaVortexMissileSpellVisualId));

			return new PrioritySelector(
				ScriptHelpers.CreateCancelCinematicIfPlaying()
				);
		}


		private bool ShouldAvoidLeftEntranceSide()
		{
			if (!_leftSideTimer.IsFinished) return true;
			bool aliveTrash =
				ScriptHelpers.GetUnfriendlyNpsAtLocation(_leftEntranceTrashPackLoc, 20, unit => unit.IsHostile).Any();
			if (aliveTrash)
				_leftSideTimer.Reset();
			bool aliveTrashOnRight =
				ScriptHelpers.GetUnfriendlyNpsAtLocation(_rightEntranceTrashPackLoc, 20, unit => unit.IsHostile).Any();
			return aliveTrash && !aliveTrashOnRight;
		}

		private bool ShouldAvoidRightEntranceSide()
		{
			if (!_rightSideTimer.IsFinished) return true;
			bool aliveTrash =
				ScriptHelpers.GetUnfriendlyNpsAtLocation(_rightEntranceTrashPackLoc, 20, unit => unit.IsHostile).Any();
			if (aliveTrash)
				_rightSideTimer.Reset();
			bool aliveTrashOnLeft =
				ScriptHelpers.GetUnfriendlyNpsAtLocation(_leftEntranceTrashPackLoc, 20, unit => unit.IsHostile).Any();
			return aliveTrash && !aliveTrashOnLeft;
		}

		#endregion

		#region Immerseus

		private const uint ImmerseusId = 71543;
		private const uint ShaPuddleId = 71603;
		private const uint ContaminatedPuddleId = 71604;
		private const uint LesserShaPuddleId = 73197;
		private const uint SwirlId = 71548;
		private const int SwirlTargetId = 71550;
		private const uint ShaSplashSpellId = 143298;
		private const uint SwirlSpellId = 143410;
		private const uint ShaBoltId = 71544;
		private const uint SeepingShaSpellId = 143281;
		private WoWUnit _immerseus;

		[EncounterHandler(71543, "Immerseus", Mode = CallBehaviorMode.Proximity, BossRange = 90)]
		public Composite ImmerseusBehavior()
		{
			WoWPoint outOfCombatWaitPoint = WoWPoint.Zero;

			// Avoid moving on top of the boss because he does an aoe centered at his location that does a lot of damage and a knockback
			AddAvoidObject(ctx => true, 30, o => o is WoWAreaTrigger && ((WoWAreaTrigger)o).SpellId == SeepingShaSpellId);

			// avoid the sha splashes caused by Sha Bolt.
			// Sidestep during split when these things move towards boss
			AddAvoidObject(ctx => true, 3, o => o.Entry == ShaBoltId && o.Distance < 20);
			// Swirl
			AddAvoidObject(ctx => true, 3, o => o.Entry == SwirlId && o.ToUnit().HasAura("Swirl") && o.Distance < 20);
			return new PrioritySelector(
				ctx => _immerseus = ctx as WoWUnit,
				// out of combat behavior
				new Decorator(
					ctx => !Me.Combat && Targeting.Instance.IsEmpty() && !_immerseus.Combat && !_immerseus.IsFriendly,
					new PrioritySelector(
				// initialize the 'outOfCombatWaitPoint' location
						new Decorator(
							ctx => outOfCombatWaitPoint == WoWPoint.Zero,
							new Action(
								ctx =>
									outOfCombatWaitPoint =
										GetRandomPointAroundLocation(
											_immerseus.Location,
											(int)_immerseus.RotationDegrees + (Me.IsMelee() ? 120 : 60),
											(int)_immerseus.RotationDegrees + (Me.IsMelee() ? 240 : 300),
											31,
											Me.IsMelee() ? _immerseus.MeleeRange() : _immerseus.CombatReach + 35))),
				// wait at the 'outOfCombatWaitPoint' location unti encounter starts.
						new Decorator(
							ctx => outOfCombatWaitPoint.Distance(Me.Location) > 10,
							new Action(ctx => Navigator.MoveTo(outOfCombatWaitPoint))),
						new Decorator(ctx => outOfCombatWaitPoint.Distance(Me.Location) <= 10, new ActionAlwaysSucceed()))),
				// in combat behavior
				new Decorator(
					ctx => _immerseus.Combat,
					new PrioritySelector(
				// prevent raid following logic to kick in when there's nothing to kill. 
						new Decorator(
							ctx =>
								Targeting.Instance.IsEmpty() &&
								(!Me.IsHealer() || HealTargeting.Instance.IsEmpty() ||
								HealTargeting.Instance.FirstUnit.GetPredictedHealthPercent() > 85),
							new ActionAlwaysSucceed()),

						new Action(
							ctx =>
							{
								// reset the 'outOfCombatWaitPoint' value when entering combat.
								outOfCombatWaitPoint = WoWPoint.Zero;
								return RunStatus.Failure;
							}))));
		}

		#endregion

		#region The Fallen Protectors

		private const uint RookStonetoeId = 71475;
		private const uint HeSoftfootId = 71479;
		private const uint SunTenderheartId = 71480;

		private const uint EmbodiedMiseryId = 71476;
		private const uint EmbodiedSorrowId = 71481;
		private const uint EmbodiedGloomId = 71477;

		private const uint EmbodiedAnguishId = 71478;
		private const uint DesperationSpawnId = 71993;

		private const uint CorruptionShockMissileSpellId = 144020;
		private const int CorruptionShockSpellId = 143958;
		private const uint DefiledGroundSpellId = 143960;
		private const int CorruptionKickSpellId = 143007;
		private const uint CorruptedBrewMissileSpellId = 143021;
		private const uint NoxiousPoisonSpellId = 143235;
		private const int FixateSpellId = 143292;
		private const int ShaSearSpellId = 143423;
		private const int VengefulStrikesSpellId = 144396;
		private const int MarkOfAnguishSpellId = 143842;

		[LocationHandler(1212.693, 1031.918, 418.0828, 75, "The Fallen Protectors")]
		public Composite TheFallenProtectorsBehavior()
		{
			var encounterLoc = new WoWPoint(1212.693, 1031.918, 418.0828);

			var encounter = new TheFallenProtectorEncounter();
			// Avoid Corruption Shock Missile's landing location.
			AddAvoidLocation(
				ctx => true,
				4,
				o => ((WoWMissile)o).ImpactPosition,
				() => WoWMissile.InFlightMissiles.Where(m => m.SpellId == CorruptionShockMissileSpellId));

			// Avoid Corruption Brew Missile's landing location.
			AddAvoidLocation(
				ctx => true,
				5,
				o => ((WoWMissile)o).ImpactPosition,
				() => WoWMissile.InFlightMissiles.Where(m => m.SpellId == CorruptedBrewMissileSpellId));

			// Avoid defiled ground 
			AddAvoidObject(ctx => true, 9, o => o is WoWAreaTrigger && ((WoWAreaTrigger)o).SpellId == DefiledGroundSpellId);

			// Avoid noxious poison pools
			AddAvoidObject(ctx => true, 3, o => o is WoWAreaTrigger && ((WoWAreaTrigger)o).SpellId == NoxiousPoisonSpellId);

			// avoid corruption kick
			AddAvoidObject(ctx => true, 10, o => o.Entry == RookStonetoeId && o.ToUnit().HasAura(CorruptionKickSpellId));

			// Run away from players that have the 'Sha Sear' aura or stay away from them if bot has the aura.
			AddAvoidObject(
				ctx => true,
				5,
				o => o is WoWPlayer && !o.IsMe && (Me.HasAura(ShaSearSpellId) || o.ToPlayer().HasAura(ShaSearSpellId)));

			return new PrioritySelector(
				ctx => encounter.Refresh(),
				// Transfer mark of Anguish to one of the tanks. 
				new Decorator(
					ctx => Me.HasAura("Mark of Anguish"),
				// set context to nearest tank that is alive.
					new Sequence(
						ctx =>
							ScriptHelpers.GroupMembers.Where(g => g.IsTank && g.IsAlive && g.Location.Distance(Me.Location) <= 40)
								.OrderBy(g => g.Location.DistanceSqr(Me.Location))
								.FirstOrDefault(),
				// break sequence if context is null
						new DecoratorContinue(ctx => ctx == null, new ActionAlwaysFail()),
						new ActionLogger("Transfering Mark of Anguish to tank"),
						new Helpers.Action<WoWPlayer>(tank => tank.Target()),
				// wait for target change to complete
						new WaitContinue<WoWPlayer>(2, tank => Me.CurrentTargetGuid == tank.Guid, new ActionAlwaysSucceed()),
						new Action(ctx => SpellActionButton.ExtraActionButton.Use()))),
				// Handle Dark Meditation;
				ScriptHelpers.CreateWaitAtLocationWhile(
					ctx => encounter.DarkMeditationLocation != WoWPoint.Zero,
					ctx => encounter.DarkMeditationLocation,
					"Dark Meditation Location"),
				// Handle InfernoStrike;
				ScriptHelpers.CreateWaitAtLocationWhile(
					ctx => encounter.InfernoStrikeTarget != WoWPoint.Zero && encounter.DarkMeditationLocation == WoWPoint.Zero,
					ctx => encounter.InfernoStrikeTarget,
					"Inferno Strike Location"),
				// the frontal cone of Rook Stonetoe because of his Vengeful Strikes ability
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(
					ctx =>
						encounter.RookStonetoe != null && encounter.RookStonetoe.Distance <= 10 &&
						encounter.RookStonetoe.HasAura(VengefulStrikesSpellId),
					ctx => encounter.RookStonetoe,
					new ScriptHelpers.AngleSpan(0, 90)),
				// interrupt Corruption Shock spell.
				ScriptHelpers.CreateInterruptCast(ctx => encounter.EmbodiedGloom, CorruptionShockSpellId),
				// interrupt Sha Sear spell.
				ScriptHelpers.CreateInterruptCast(ctx => encounter.SunTenderheart, ShaSearSpellId),
				// dispell Shadow Word: Bane from party members before it spreads.
				ScriptHelpers.CreateDispelGroup("Shadow Word: Bane", ScriptHelpers.PartyDispelType.Magic));
		}

		private class TheFallenProtectorEncounter
		{
			private const int InfernoStrikeSpellId = 143962;
			private const uint DarkMeditationSpellId = 143546;

			public WoWUnit RookStonetoe { get; private set; }
			public WoWUnit HeSoftfoot { get; private set; }
			public WoWUnit SunTenderheart { get; private set; }
			public WoWUnit EmbodiedMisery { get; private set; }
			public WoWUnit EmbodiedSorrow { get; private set; }
			public WoWUnit EmbodiedGloom { get; private set; }
			public WoWUnit EmbodiedAnguish { get; private set; }
			// returns the InfernoStrike target location or WoWPoint.Zero if there is none.
			public WoWPoint InfernoStrikeTarget { get; private set; }
			// returns the Dark Meditation location or WoWPoint.Zero if there is none.
			public WoWPoint DarkMeditationLocation { get; private set; }

			public TheFallenProtectorEncounter Refresh()
			{
				List<WoWUnit> units = ObjectManager.GetObjectsOfTypeFast<WoWUnit>();
				RookStonetoe = units.FirstOrDefault(u => u.Entry == RookStonetoeId);
				HeSoftfoot = units.FirstOrDefault(u => u.Entry == HeSoftfootId);
				SunTenderheart = units.FirstOrDefault(u => u.Entry == SunTenderheartId);
				EmbodiedMisery = units.FirstOrDefault(u => u.Entry == EmbodiedMiseryId);
				EmbodiedSorrow = units.FirstOrDefault(u => u.Entry == EmbodiedSorrowId);
				EmbodiedGloom = units.FirstOrDefault(u => u.Entry == EmbodiedGloomId);
				EmbodiedAnguish = units.FirstOrDefault(u => u.Entry == EmbodiedAnguishId);

				InfernoStrikeTarget = EmbodiedSorrow != null && EmbodiedSorrow.CastingSpellId == InfernoStrikeSpellId &&
									EmbodiedSorrow.CurrentTargetGuid == Me.Guid
					? ScriptHelpers.GetGroupCenterLocation(u => u.IsRange, 20)
					: WoWPoint.Zero;

				WoWAreaTrigger darkMeditation =
					ObjectManager.GetObjectsOfTypeFast<WoWAreaTrigger>().FirstOrDefault(a => a.SpellId == DarkMeditationSpellId);
				DarkMeditationLocation = darkMeditation != null ? darkMeditation.Location : WoWPoint.Zero;
				return this;
			}
		}

		#endregion

		#region Scarred Vale Mini Bosses

		[EncounterHandler(72661, "Zeal")]
		[EncounterHandler(72662, "Vanity")]
		[EncounterHandler(72663, "Arrogance")]
		public Composite ScarredValeMinibossesBehavior()
		{
			return new PrioritySelector(
				ctx => ((WoWUnit)ctx).CurrentTarget,
				// stack on raid.
				new Decorator<WoWPlayer>(
					tank => tank.DistanceSqr > 6 * 6,
					new Helpers.Action<WoWPlayer>(tank => Navigator.MoveTo(tank.Location))));
		}

		#endregion

		#region Scarred Vale trash avoidance

		private readonly WoWPoint _scaredValeTrashLoc1 = new WoWPoint(1183.918, 957.7109, 416.2369);
		private readonly WoWPoint _scaredValeTrashLoc10 = new WoWPoint(1089.265, 904.6152, 394.0434);
		private readonly WoWPoint _scaredValeTrashLoc11 = new WoWPoint(1081.172, 869.6012, 389.1312);
		private readonly WoWPoint _scaredValeTrashLoc12 = new WoWPoint(1097.571, 873.6885, 399.501);
		private readonly WoWPoint _scaredValeTrashLoc13 = new WoWPoint(1066.532, 874.3652, 386.0892);
		private readonly WoWPoint _scaredValeTrashLoc14 = new WoWPoint(1057.471, 832.0161, 381.4438);
		private readonly WoWPoint _scaredValeTrashLoc15 = new WoWPoint(1018.738, 857.3892, 378.7911);
		private readonly WoWPoint _scaredValeTrashLoc16 = new WoWPoint(1032.511, 890.2089, 378.5344);
		private readonly WoWPoint _scaredValeTrashLoc17 = new WoWPoint(1099.957, 823.1729, 378.9481);
		private readonly WoWPoint _scaredValeTrashLoc18 = new WoWPoint(980.5803, 846.0817, 377.5174);
		private readonly WoWPoint _scaredValeTrashLoc19 = new WoWPoint(947.779, 869.5156, 384.9554);
		private readonly WoWPoint _scaredValeTrashLoc2 = new WoWPoint(1158.991, 984.8674, 421.2036);
		private readonly WoWPoint _scaredValeTrashLoc20 = new WoWPoint(1022.881, 814.024, 381.1033);
		private readonly WoWPoint _scaredValeTrashLoc21 = new WoWPoint(1009.965, 809.5453, 381.5198);
		private readonly WoWPoint _scaredValeTrashLoc3 = new WoWPoint(1145.193, 951.525, 427.6057);
		private readonly WoWPoint _scaredValeTrashLoc4 = new WoWPoint(1131.714, 898.7101, 404.1013);
		private readonly WoWPoint _scaredValeTrashLoc5 = new WoWPoint(1227.602, 920.8539, 400.1874);
		private readonly WoWPoint _scaredValeTrashLoc6 = new WoWPoint(1202.375, 865.4149, 385.3218);
		private readonly WoWPoint _scaredValeTrashLoc7 = new WoWPoint(1149.148, 780.7864, 378.0222);
		private readonly WoWPoint _scaredValeTrashLoc8 = new WoWPoint(1162.884, 848.3898, 379.4589);
		private readonly WoWPoint _scaredValeTrashLoc9 = new WoWPoint(1243.459, 867.0009, 386.7955);

		private readonly WaitTimer _scaredValeTrashTimer1 = WaitTimer.FiveSeconds;
		private readonly WaitTimer _scaredValeTrashTimer10 = WaitTimer.FiveSeconds;
		private readonly WaitTimer _scaredValeTrashTimer11 = WaitTimer.FiveSeconds;
		private readonly WaitTimer _scaredValeTrashTimer12 = WaitTimer.FiveSeconds;
		private readonly WaitTimer _scaredValeTrashTimer13 = WaitTimer.FiveSeconds;
		private readonly WaitTimer _scaredValeTrashTimer14 = WaitTimer.FiveSeconds;
		private readonly WaitTimer _scaredValeTrashTimer15 = WaitTimer.FiveSeconds;
		private readonly WaitTimer _scaredValeTrashTimer16 = WaitTimer.FiveSeconds;
		private readonly WaitTimer _scaredValeTrashTimer17 = WaitTimer.FiveSeconds;
		private readonly WaitTimer _scaredValeTrashTimer18 = WaitTimer.FiveSeconds;
		private readonly WaitTimer _scaredValeTrashTimer19 = WaitTimer.FiveSeconds;
		private readonly WaitTimer _scaredValeTrashTimer2 = WaitTimer.FiveSeconds;
		private readonly WaitTimer _scaredValeTrashTimer20 = WaitTimer.FiveSeconds;
		private readonly WaitTimer _scaredValeTrashTimer21 = WaitTimer.FiveSeconds;
		private readonly WaitTimer _scaredValeTrashTimer3 = WaitTimer.FiveSeconds;
		private readonly WaitTimer _scaredValeTrashTimer4 = WaitTimer.FiveSeconds;
		private readonly WaitTimer _scaredValeTrashTimer5 = WaitTimer.FiveSeconds;
		private readonly WaitTimer _scaredValeTrashTimer6 = WaitTimer.FiveSeconds;
		private readonly WaitTimer _scaredValeTrashTimer7 = WaitTimer.FiveSeconds;
		private readonly WaitTimer _scaredValeTrashTimer8 = WaitTimer.FiveSeconds;
		private readonly WaitTimer _scaredValeTrashTimer9 = WaitTimer.FiveSeconds;

		private bool _shouldAvoidScaredValeTrash1;
		private bool _shouldAvoidScaredValeTrash10;
		private bool _shouldAvoidScaredValeTrash11;
		private bool _shouldAvoidScaredValeTrash12;
		private bool _shouldAvoidScaredValeTrash13;
		private bool _shouldAvoidScaredValeTrash14;
		private bool _shouldAvoidScaredValeTrash15;
		private bool _shouldAvoidScaredValeTrash16;
		private bool _shouldAvoidScaredValeTrash17;
		private bool _shouldAvoidScaredValeTrash18;
		private bool _shouldAvoidScaredValeTrash19;
		private bool _shouldAvoidScaredValeTrash2;
		private bool _shouldAvoidScaredValeTrash20;
		private bool _shouldAvoidScaredValeTrash21;
		private bool _shouldAvoidScaredValeTrash3;
		private bool _shouldAvoidScaredValeTrash4;
		private bool _shouldAvoidScaredValeTrash5;
		private bool _shouldAvoidScaredValeTrash6;
		private bool _shouldAvoidScaredValeTrash7;
		private bool _shouldAvoidScaredValeTrash8;
		private bool _shouldAvoidScaredValeTrash9;

		/* 72655, 72658, 72662, 72788, 72663 */

		private IEnumerable<DynamicBlackspot> GetScaredValeBlackspots()
		{
			yield return new DynamicBlackspot(
				() =>
				{
					if (!_scaredValeTrashTimer1.IsFinished) return _shouldAvoidScaredValeTrash1;
					_scaredValeTrashTimer1.Reset();
					_shouldAvoidScaredValeTrash1 = !Me.Combat && Me.Location.Distance(_scaredValeTrashLoc1) < 100 * 100 &&
													ScriptHelpers.GetUnfriendlyNpsAtLocation(_scaredValeTrashLoc1, 10).Any(u => !u.Combat);
					return _shouldAvoidScaredValeTrash1;
				},
				() => _scaredValeTrashLoc1,
				LfgDungeon.MapId,
				30.86862f,
				name: "Pack of 5 trash mobs #1");

			yield return new DynamicBlackspot(
				() =>
				{
					if (!_scaredValeTrashTimer2.IsFinished) return _shouldAvoidScaredValeTrash2;
					_scaredValeTrashTimer2.Reset();
					_shouldAvoidScaredValeTrash2 = !Me.Combat && Me.Location.Distance(_scaredValeTrashLoc2) < 100 * 100 &&
													ScriptHelpers.GetUnfriendlyNpsAtLocation(_scaredValeTrashLoc2, 10).Any(u => !u.Combat);
					return _shouldAvoidScaredValeTrash2;
				},
				() => _scaredValeTrashLoc2,
				LfgDungeon.MapId,
				23f,
				name: "Amalgamated Hubris #2");

			yield return new DynamicBlackspot(
				() =>
				{
					if (!_scaredValeTrashTimer3.IsFinished) return _shouldAvoidScaredValeTrash3;
					_scaredValeTrashTimer3.Reset();
					_shouldAvoidScaredValeTrash3 = !Me.Combat && Me.Location.Distance(_scaredValeTrashLoc3) < 100 * 100 &&
													ScriptHelpers.GetUnfriendlyNpsAtLocation(_scaredValeTrashLoc3, 10).Any(u => !u.Combat);
					return _shouldAvoidScaredValeTrash3;
				},
				() => _scaredValeTrashLoc3,
				LfgDungeon.MapId,
				23f,
				name: "Amalgamated Hubris #3");

			yield return new DynamicBlackspot(
				() =>
				{
					if (!_scaredValeTrashTimer4.IsFinished) return _shouldAvoidScaredValeTrash4;
					_scaredValeTrashTimer4.Reset();
					_shouldAvoidScaredValeTrash4 = !Me.Combat && Me.Location.Distance(_scaredValeTrashLoc4) < 100 * 100 &&
													ScriptHelpers.GetUnfriendlyNpsAtLocation(_scaredValeTrashLoc4, 10).Any(u => !u.Combat);
					return _shouldAvoidScaredValeTrash4;
				},
				() => _scaredValeTrashLoc4,
				LfgDungeon.MapId,
				23f,
				name: "Vanity #4");

			yield return new DynamicBlackspot(
				() =>
				{
					if (!_scaredValeTrashTimer5.IsFinished) return _shouldAvoidScaredValeTrash5;
					_scaredValeTrashTimer5.Reset();
					_shouldAvoidScaredValeTrash5 = !Me.Combat && Me.Location.Distance(_scaredValeTrashLoc5) < 100 * 100 &&
													ScriptHelpers.GetUnfriendlyNpsAtLocation(_scaredValeTrashLoc5, 10).Any(u => !u.Combat);
					return _shouldAvoidScaredValeTrash5;
				},
				() => _scaredValeTrashLoc5,
				LfgDungeon.MapId,
				28.05087f,
				name: "Pack of 3 trash mobs #5");

			yield return new DynamicBlackspot(
				() =>
				{
					if (!_scaredValeTrashTimer6.IsFinished) return _shouldAvoidScaredValeTrash6;
					_scaredValeTrashTimer6.Reset();
					_shouldAvoidScaredValeTrash6 = !Me.Combat && Me.Location.Distance(_scaredValeTrashLoc6) < 100 * 100 &&
													ScriptHelpers.GetUnfriendlyNpsAtLocation(_scaredValeTrashLoc6, 10).Any(u => !u.Combat);
					return _shouldAvoidScaredValeTrash6;
				},
				() => _scaredValeTrashLoc6,
				LfgDungeon.MapId,
				23f,
				name: "Sha-Infused Defender #6");

			yield return new DynamicBlackspot(
				() =>
				{
					if (!_scaredValeTrashTimer7.IsFinished) return _shouldAvoidScaredValeTrash7;
					_scaredValeTrashTimer7.Reset();
					_shouldAvoidScaredValeTrash7 = !Me.Combat && Me.Location.Distance(_scaredValeTrashLoc7) < 100 * 100 &&
													ScriptHelpers.GetUnfriendlyNpsAtLocation(_scaredValeTrashLoc7, 10).Any(u => !u.Combat);
					return _shouldAvoidScaredValeTrash7;
				},
				() => _scaredValeTrashLoc7,
				LfgDungeon.MapId,
				23f,
				name: "Unknown #7");

			yield return new DynamicBlackspot(
				() =>
				{
					if (!_scaredValeTrashTimer8.IsFinished) return _shouldAvoidScaredValeTrash8;
					_scaredValeTrashTimer8.Reset();
					_shouldAvoidScaredValeTrash8 = !Me.Combat && Me.Location.Distance(_scaredValeTrashLoc8) < 100 * 100 &&
													ScriptHelpers.GetUnfriendlyNpsAtLocation(_scaredValeTrashLoc8, 10).Any(u => !u.Combat);
					return _shouldAvoidScaredValeTrash8;
				},
				() => _scaredValeTrashLoc8,
				LfgDungeon.MapId,
				28.70736f,
				name: "Pack of 3 trash mobs #8");

			yield return new DynamicBlackspot(
				() =>
				{
					if (!_scaredValeTrashTimer9.IsFinished) return _shouldAvoidScaredValeTrash9;
					_scaredValeTrashTimer9.Reset();
					_shouldAvoidScaredValeTrash9 = !Me.Combat && Me.Location.Distance(_scaredValeTrashLoc9) < 100 * 100 &&
													ScriptHelpers.GetUnfriendlyNpsAtLocation(_scaredValeTrashLoc9, 10).Any(u => !u.Combat);
					return _shouldAvoidScaredValeTrash9;
				},
				() => _scaredValeTrashLoc9,
				LfgDungeon.MapId,
				30.6255f,
				name: "Pack of 5 trash mobs #9");

			yield return new DynamicBlackspot(
				() =>
				{
					if (!_scaredValeTrashTimer10.IsFinished) return _shouldAvoidScaredValeTrash10;
					_scaredValeTrashTimer10.Reset();
					_shouldAvoidScaredValeTrash10 = !Me.Combat && Me.Location.Distance(_scaredValeTrashLoc10) < 100 * 100 &&
													ScriptHelpers.GetUnfriendlyNpsAtLocation(_scaredValeTrashLoc10, 10).Any(u => !u.Combat);
					return _shouldAvoidScaredValeTrash10;
				},
				() => _scaredValeTrashLoc10,
				LfgDungeon.MapId,
				23f,
				name: "Amalgamated Hubris #10");

			yield return new DynamicBlackspot(
				() =>
				{
					if (!_scaredValeTrashTimer11.IsFinished) return _shouldAvoidScaredValeTrash11;
					_scaredValeTrashTimer11.Reset();
					_shouldAvoidScaredValeTrash11 = !Me.Combat && Me.Location.Distance(_scaredValeTrashLoc11) < 100 * 100 &&
													ScriptHelpers.GetUnfriendlyNpsAtLocation(_scaredValeTrashLoc11, 10).Any(u => !u.Combat);
					return _shouldAvoidScaredValeTrash11;
				},
				() => _scaredValeTrashLoc11,
				LfgDungeon.MapId,
				29.41147f,
				name: "Pack of 4 trash mobs #11");

			yield return new DynamicBlackspot(
				() =>
				{
					if (!_scaredValeTrashTimer12.IsFinished) return _shouldAvoidScaredValeTrash12;
					_scaredValeTrashTimer12.Reset();
					_shouldAvoidScaredValeTrash12 = !Me.Combat && Me.Location.Distance(_scaredValeTrashLoc12) < 100 * 100 &&
													ScriptHelpers.GetUnfriendlyNpsAtLocation(_scaredValeTrashLoc12, 10).Any(u => !u.Combat);
					return _shouldAvoidScaredValeTrash12;
				},
				() => _scaredValeTrashLoc12,
				LfgDungeon.MapId,
				23f,
				name: "Amalgamated Hubris #12");

			yield return new DynamicBlackspot(
				() =>
				{
					if (!_scaredValeTrashTimer13.IsFinished) return _shouldAvoidScaredValeTrash13;
					_scaredValeTrashTimer13.Reset();
					_shouldAvoidScaredValeTrash13 = !Me.Combat && Me.Location.Distance(_scaredValeTrashLoc13) < 100 * 100 &&
													ScriptHelpers.GetUnfriendlyNpsAtLocation(_scaredValeTrashLoc13, 10).Any(u => !u.Combat);
					return _shouldAvoidScaredValeTrash13;
				},
				() => _scaredValeTrashLoc13,
				LfgDungeon.MapId,
				23f,
				name: "Fragment of Pride #13");

			yield return new DynamicBlackspot(
				() =>
				{
					if (!_scaredValeTrashTimer14.IsFinished) return _shouldAvoidScaredValeTrash14;
					_scaredValeTrashTimer14.Reset();
					_shouldAvoidScaredValeTrash14 = !Me.Combat && Me.Location.Distance(_scaredValeTrashLoc14) < 100 * 100 &&
													ScriptHelpers.GetUnfriendlyNpsAtLocation(_scaredValeTrashLoc14, 10).Any(u => !u.Combat);
					return _shouldAvoidScaredValeTrash14;
				},
				() => _scaredValeTrashLoc14,
				LfgDungeon.MapId,
				27.69311f,
				name: "Pack of 2 trash mobs #14");

			yield return new DynamicBlackspot(
				() =>
				{
					if (!_scaredValeTrashTimer15.IsFinished) return _shouldAvoidScaredValeTrash15;
					_scaredValeTrashTimer15.Reset();
					_shouldAvoidScaredValeTrash15 = !Me.Combat && Me.Location.Distance(_scaredValeTrashLoc15) < 100 * 100 &&
													ScriptHelpers.GetUnfriendlyNpsAtLocation(_scaredValeTrashLoc15, 10).Any(u => !u.Combat);
					return _shouldAvoidScaredValeTrash15;
				},
				() => _scaredValeTrashLoc15,
				LfgDungeon.MapId,
				27.82397f,
				name: "Pack of 2 trash mobs #15");

			yield return new DynamicBlackspot(
				() =>
				{
					if (!_scaredValeTrashTimer16.IsFinished) return _shouldAvoidScaredValeTrash16;
					_scaredValeTrashTimer16.Reset();
					_shouldAvoidScaredValeTrash16 = !Me.Combat && Me.Location.Distance(_scaredValeTrashLoc16) < 100 * 100 &&
													ScriptHelpers.GetUnfriendlyNpsAtLocation(_scaredValeTrashLoc16, 10).Any(u => !u.Combat);
					return _shouldAvoidScaredValeTrash16;
				},
				() => _scaredValeTrashLoc16,
				LfgDungeon.MapId,
				29.4305f,
				name: "Pack of 5 trash mobs #16");

			yield return new DynamicBlackspot(
				() =>
				{
					if (!_scaredValeTrashTimer17.IsFinished) return _shouldAvoidScaredValeTrash17;
					_scaredValeTrashTimer17.Reset();
					_shouldAvoidScaredValeTrash17 = !Me.Combat && Me.Location.Distance(_scaredValeTrashLoc17) < 100 * 100 &&
													ScriptHelpers.GetUnfriendlyNpsAtLocation(_scaredValeTrashLoc17, 10).Any(u => !u.Combat);
					return _shouldAvoidScaredValeTrash17;
				},
				() => _scaredValeTrashLoc17,
				LfgDungeon.MapId,
				32.79678f,
				name: "Pack of 4 trash mobs #17");

			yield return new DynamicBlackspot(
				() =>
				{
					if (!_scaredValeTrashTimer18.IsFinished) return _shouldAvoidScaredValeTrash18;
					_scaredValeTrashTimer18.Reset();
					_shouldAvoidScaredValeTrash18 = !Me.Combat && Me.Location.Distance(_scaredValeTrashLoc18) < 100 * 100 &&
													ScriptHelpers.GetUnfriendlyNpsAtLocation(_scaredValeTrashLoc18, 10).Any(u => !u.Combat);
					return _shouldAvoidScaredValeTrash18;
				},
				() => _scaredValeTrashLoc18,
				LfgDungeon.MapId,
				32.4809f,
				name: "Pack of 5 trash mobs #18");

			yield return new DynamicBlackspot(
				() =>
				{
					if (!_scaredValeTrashTimer19.IsFinished) return _shouldAvoidScaredValeTrash19;
					_scaredValeTrashTimer19.Reset();
					_shouldAvoidScaredValeTrash19 = !Me.Combat && Me.Location.Distance(_scaredValeTrashLoc19) < 100 * 100 &&
													ScriptHelpers.GetUnfriendlyNpsAtLocation(_scaredValeTrashLoc19, 10).Any(u => !u.Combat);
					return _shouldAvoidScaredValeTrash19;
				},
				() => _scaredValeTrashLoc19,
				LfgDungeon.MapId,
				23f,
				name: "Sha-Infused Defender #19");

			yield return new DynamicBlackspot(
				() =>
				{
					if (!_scaredValeTrashTimer20.IsFinished) return _shouldAvoidScaredValeTrash20;
					_scaredValeTrashTimer20.Reset();
					_shouldAvoidScaredValeTrash20 = !Me.Combat && Me.Location.Distance(_scaredValeTrashLoc20) < 100 * 100 &&
													ScriptHelpers.GetUnfriendlyNpsAtLocation(_scaredValeTrashLoc20, 10).Any(u => !u.Combat);
					return _shouldAvoidScaredValeTrash20;
				},
				() => _scaredValeTrashLoc20,
				LfgDungeon.MapId,
				23f,
				name: "Fragment of Pride #20");

			yield return new DynamicBlackspot(
				() =>
				{
					if (!_scaredValeTrashTimer21.IsFinished) return _shouldAvoidScaredValeTrash21;
					_scaredValeTrashTimer21.Reset();
					_shouldAvoidScaredValeTrash21 = !Me.Combat && Me.Location.Distance(_scaredValeTrashLoc21) < 100 * 100 &&
													ScriptHelpers.GetUnfriendlyNpsAtLocation(_scaredValeTrashLoc21, 10).Any(u => !u.Combat);
					return _shouldAvoidScaredValeTrash21;
				},
				() => _scaredValeTrashLoc21,
				LfgDungeon.MapId,
				29.53267f,
				name: "Pack of 4 trash mobs #21");

		}

		#endregion

		#region Norushen

		private const uint BlindHatredId = 72565;
		private const uint BottomlessPitSpellId = 146793;
		private const uint EssenceofCorruptionId = 72263;
		private const uint ManifestationofCorruptionId = 72264;
		private const uint ManifestationofCorruptionPhasedId = 71977;
		private const uint EssenceofCorruptionPhasedId = 71976;
		private const int TearRealitySpellId = 144482;
		WoWUnit _blindHatred = null;

		private uint CorruptionLevel
		{
			get
			{
				WoWAura aura = Me.GetAuraByName("Corruption");
				return aura != null ? aura.StackCount : 0;
			}
		}

		bool AvoidBlindHatred
		{
			get { return _blindHatred != null && _blindHatred.IsValid && _blindHatred.IsAlive; }
		}

		[EncounterHandler(72276, "Amalgam of Corruption")]
		public Composite AmalgamOfCorruptionBehavior()
		{
			WoWUnit boss = null;

			float bossToBlindHatredAng = 0;
			var getBossToBlindHatredRadians = new Func<float>(
				() =>
				{
					if (_blindHatred == null || !_blindHatred.IsValid)
						return 0;
					WoWPoint bossLoc = boss.Location;
					WoWPoint blindHatredLoc = _blindHatred.Location;
					WoWPoint bossToBlindHatredVector = blindHatredLoc - bossLoc;

					return (float)Math.Atan2(bossToBlindHatredVector.Y, bossToBlindHatredVector.X);
				});

			var getLocationOppositeBlindHatred = new Func<object, WoWPoint>(
				ctx =>
				{
					int minRange = 3;
					int maxRange = Me.IsMelee() ? (int)boss.MeleeRange() : 20;
					float ang = WoWMathHelper.NormalizeRadian(bossToBlindHatredAng + (float)Math.PI);
					return boss.Location.RayCast(ang, ScriptHelpers.Rnd.Next(minRange, maxRange));
				});


			return new PrioritySelector(
				ctx =>
				{
					_blindHatred = ObjectManager.GetObjectsOfTypeFast<WoWUnit>().FirstOrDefault(u => u.Entry == BlindHatredId);
					return boss = ctx as WoWUnit;
				},
				new Decorator(
					ctx =>
					{
						if (_blindHatred == null) return false;
						bossToBlindHatredAng = getBossToBlindHatredRadians();
						return WoWMathHelper.IsFacing(boss.Location, bossToBlindHatredAng, Me.Location);
					},
					new Sequence(ScriptHelpers.CreateMoveToContinue(ctx => true, getLocationOppositeBlindHatred, true))));
		}

		#region DPS Look Within phase

		private const int ExpelCorruptionSpellId = 144479;


		[EncounterHandler((int)ManifestationofCorruptionPhasedId, "Manifestation of Corruption Phased")]
		[EncounterHandler((int)EssenceofCorruptionPhasedId, "Essence of CorruptionPhased")]
		public Composite DPSLookWithinBehavior()
		{
			// don't stand on top of these NPCs because they will block even if behind them 
			AddAvoidObject(ctx => true, 2, EssenceofCorruptionPhasedId);
			// used during dps 'Look Within' phase
			AddAvoidObject(ctx => true, 4, o => o is WoWAreaTrigger && ((WoWAreaTrigger)o).SpellId == ExpelCorruptionSpellId);

			WoWUnit boss = null;
			WoWUnit myTarget = null;

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				new Decorator(
					ScriptHelpers.CreateAvoidUnitAnglesBehavior(
						ctx => boss.CastingSpellId == TearRealitySpellId && ((WoWUnit)ctx).Distance < 20,
						ctx => boss,
						new ScriptHelpers.AngleSpan(0, 120))),
				// stand behind the Essence of Corruption.. 
				ScriptHelpers.GetBehindUnit(
					ctx => (myTarget = Me.CurrentTarget) != null && myTarget.Entry == EssenceofCorruptionPhasedId && myTarget.IsFacing(Me),
					ctx => myTarget));
		}

		#endregion

		#region Healer Look Within phase

		private const uint RookStonetoeLookWithinHealerSpawnId = 71996;
		private const uint SunTenderheartLookWithinHealerSpawnId = 72000;
		private const uint LevenDawnbladeLookWithinHealerSpawnId = 71995;

		[EncounterHandler((int)RookStonetoeLookWithinHealerSpawnId, "Rook Stonetoe Phased", Mode = CallBehaviorMode.Proximity)]
		[EncounterHandler((int)SunTenderheartLookWithinHealerSpawnId, "Sun Tenderheart", Mode = CallBehaviorMode.Proximity)]
		[EncounterHandler((int)LevenDawnbladeLookWithinHealerSpawnId, "Leven Dawnblade Phased", Mode = CallBehaviorMode.Proximity)
		]
		public Composite HealerLookWithinBehavior()
		{
			AddAvoidObject(ctx => true, 4, o => o is WoWAreaTrigger && ((WoWAreaTrigger)o).SpellId == BottomlessPitSpellId);

			WoWUnit boss = null;

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				// Dispell "Lingering Corruption" from the friendly NPCs.
				ScriptHelpers.CreateDispelFriendlyUnit("Lingering Corruption", ScriptHelpers.PartyDispelType.Magic, ctx => boss));
		}

		#endregion

		#endregion

		#region Sha of Pride

		private const uint BurstingPrideMissileSpellId = 144910;

		private const uint ManifestationOfPrideId = 71946;
		private const int MockingBlastSpellId = 144379;
		private const uint ReflectionId = 72172;
		private const uint NetherTempestMissileSpellId = 114956;
		private const int SwellingPrideId = 144400;
		private const uint ShadowPrisonId = 72017;
		private const uint ShaOfPrideId = 71734;

		private uint PrideLevel
		{
			get
			{
				WoWAura aura = Me.GetAuraByName("Pride");
				return aura != null ? aura.StackCount : 0;
			}
		}

		[EncounterHandler(71734, "Sha of Pride", Mode = CallBehaviorMode.Proximity, BossRange = 90)]
		public Func<WoWUnit, Task<bool>> ShaOfPrideBehavior()
		{
			var encounter = new ShaOfPrideEncounter();
			// Avoid 'Bursting Pride' missile impact location.
			AddAvoidLocation(
				ctx => true,
				5,
				o => ((WoWMissile)o).ImpactPosition,
				() => WoWMissile.InFlightMissiles.Where(m => m.SpellId == BurstingPrideMissileSpellId));
			// missile strike casted by the 'Reflection' npc when they show up.
			AddAvoidObject(
				ctx => encounter.Boss != null && encounter.Boss.IsValid && encounter.Boss.HasAura("Self-Reflection"),
				2,
				ReflectionId);

			// Run away from players that have the 'Aura of Pride' aura or stay away from them if bot has the aura.
			AddAvoidObject(
				ctx => true,
				8,
				o => o is WoWPlayer && !o.IsMe && (o.ToPlayer().HasAura("Aura of Pride") || Me.HasAura("Aura of Pride")));
			// move away from playes when boss is casting the 'Swelling Pride' ability and player will recieve the 'Bursting Pride' or 'Aura of Pride' abilty
			AddAvoidObject(
				ctx =>
					encounter.Boss != null && encounter.Boss.IsValid && encounter.Boss.CastingSpellId == SwellingPrideId &&
					(PrideLevel >= 25 && PrideLevel < 50 || PrideLevel >= 75),
				8,
				o => o is WoWPlayer && !o.IsMe);
			// avoid running into the boss when he's not in combat.
			AddAvoidObject(ctx => true, 30, o => o.Entry == ShaOfPrideId && !o.ToUnit().Combat && o.ToUnit().IsAlive);

			return async boss =>
			{
				encounter.Refresh(boss);
				if (encounter.HandleProjection)
				{
					await CommonCoroutines.MoveTo(encounter.ProjectionLoc, "Projection");
					return true;
				}
				// free a prisoner
				if (encounter.FreePrisoner)
				{
					await CommonCoroutines.MoveTo(encounter.ShadowPrisonLoc, "Shadow Prisoner");
					return true;
				}
				// dispell dot off party members as long as my pride level is low. (dipelling this will increase pride level by 5)
				if (PrideLevel < 50 && await ScriptHelpers.DispelGroup(
					"Mark of Arrogance",
					ScriptHelpers.PartyDispelType.Magic))
				{
					return true;
				}

				// interupt the 'Mocking Blast' spell that adds cast 
				var manifestationOfPride = ObjectManager.GetObjectsOfTypeFast<WoWUnit>()
					.Where(u => u.Entry == ManifestationOfPrideId && u.CastingSpellId == MockingBlastSpellId)
					.OrderBy(u => u.DistanceSqr)
					.FirstOrDefault();

				if (await ScriptHelpers.InterruptCast(manifestationOfPride, MockingBlastSpellId))
					return true;

				// all range should stack up when not handling prisoners, Gift of Titans or projection.
				return await ScriptHelpers.StayAtLocationWhile(
					() =>!encounter.FreePrisoner && !encounter.HandleProjection,
					ScriptHelpers.GetGroupCenterLocation(p => p.IsRange),
					"Raid location",
					8,
					ScriptHelpers.GroupRoleFlags.Ranged);
			};
		}


		private class ShaOfPrideEncounter
		{
			private const int ProjectionMissileSpellId = 145066;
			public WoWUnit Boss { get; private set; }
			public WoWPoint ShadowPrisonLoc { get; private set; }
			public WoWPoint ProjectionLoc { get; private set; }

			public bool FreePrisoner
			{
				get { return !Me.IsHealer() && !HandleProjection && ShadowPrisonLoc != WoWPoint.Zero; }
			}

			public bool HandleProjection
			{
				get { return ProjectionLoc != WoWPoint.Zero; }
			}

			public ShaOfPrideEncounter Refresh(WoWUnit boss)
			{
				Boss = boss;
				List<WoWPlayer> raidmembers = Me.RaidMembers;

				WoWPoint myLoc = Me.Location;

				ShadowPrisonLoc = (from groupMember in raidmembers
								   where groupMember.HasAura("Corrupted Prison")
								   let groupMemberLocation = groupMember.Location
								   let shadowPrisons = (from unit in ObjectManager.GetObjectsOfTypeFast<WoWUnit>()
														where unit.Entry == ShadowPrisonId
														let unitLoc = unit.Location
														where
															unitLoc.Distance(groupMemberLocation) < 20 &&
															!raidmembers.Any(r => r.Guid != Me.Guid && r != groupMember && r.Location.Distance(unitLoc) < 6)
														select new { Unit = unit, Location = unitLoc }).ToList()
								   where shadowPrisons.Any()
								   select shadowPrisons).SelectMany(p => p).Select(p => p.Location).OrderBy(l => l.DistanceSqr(myLoc)).FirstOrDefault();

				ProjectionLoc =
					WoWMissile.InFlightMissiles.Where(m => m.SpellId == ProjectionMissileSpellId && m.CasterGuid == Me.Guid)
						.Select(m => m.ImpactPosition)
						.FirstOrDefault();

				return this;
			}

			private static WoWPoint GetGiftOfTheTitanMeetLoc()
			{
				WoWPoint myLoc = Me.Location;
				List<WoWPoint> locs =
					Me.RaidMembers.Where(r => !r.IsMe && r.HasAura("Gift of the Titans"))
						.Select(r => r.Location)
						.OrderBy(l => l.DistanceSqr(myLoc))
						.ToList();
				if (!locs.Any())
					return WoWPoint.Zero;
				var avgLoc = new WoWPoint(
					locs.Select(l => l.X).Average(),
					locs.Select(l => l.Y).Average(),
					locs.Select(l => l.Z).Average());
				if (Targeting.Instance.IsEmpty() || IsUnitWithinRange(Targeting.Instance.FirstUnit, avgLoc, 8))
					return avgLoc;
				return WoWPoint.Zero;
			}
		}

		#endregion

		#region Misc

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		private WoWPoint GetRandomPointAroundLocation(WoWPoint point, int startAng, int endAng, float minDist, float maxDist)
		{
			if (endAng < startAng)
				endAng += 360;
			float randomRadians =
				WoWMathHelper.NormalizeRadian(WoWMathHelper.DegreesToRadians(ScriptHelpers.Rnd.Next(startAng, endAng)));
			int randomDist = ScriptHelpers.Rnd.Next((int)minDist, (int)maxDist);
			return point.RayCast(randomRadians, randomDist);
		}

		private static bool IsUnitWithinRange(WoWUnit unit, WoWPoint location, float movementRadius)
		{
			if (unit == null)
				return false;
			float range = Me.IsMelee() ? unit.MeleeRange() + movementRadius : unit.CombatReach + 35 + movementRadius;
			return unit.Location.Distance(location) <= range;
		}

		#endregion
	}
}