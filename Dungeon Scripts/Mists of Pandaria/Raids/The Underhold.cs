using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Behaviors;
using Bots.DungeonBuddy.Helpers;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Bars;
using Styx.CommonBot.Coroutines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using AvoidanceManager = Bots.DungeonBuddy.Avoidance.AvoidanceManager;
using SpellActionButton = Bots.DungeonBuddy.Helpers.SpellActionButton;
using Vector2 = Tripper.Tools.Math.Vector2;

namespace Bots.DungeonBuddy.Dungeon_Scripts.Mists_of_Pandaria
{
	public class TheUnderhold : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 724; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(1242.479, 600.5036, 318.0787); }
		}

		#region Movement

		private const uint LiftHookId = 72972;

		public override async Task<bool> HandleMovement(WoWPoint location)
		{
			return await HandleSpoilsOfPandariaMovement(location);
		}

		#endregion

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			bool combat = Me.Combat;
			bool sopNotComplete = ScriptHelpers.IsBossAlive("Spoils of Pandoria");
			SopQuadrantPair mySopQuad = sopNotComplete ? GetSopQuadrantPairAtLocation(Me.Location) : SopQuadrantPair.None;

			SopQuadrantPair sop_leaderQuad = sopNotComplete && _spoilsOfPandariaLeader != null && _spoilsOfPandariaLeader.IsValid
				? GetSopQuadrantPairAtLocation(_spoilsOfPandariaLeader.Location)
				: SopQuadrantPair.None;


			bool onSpoilsOfPandariaEncounter = sopNotComplete &&
												(mySopQuad != SopQuadrantPair.None || sop_leaderQuad != SopQuadrantPair.None);

			units.RemoveAll(
				ret =>
				{
					WoWUnit unit = ret.ToUnit();
					if (unit != null)
					{
						// remove all units that are not in my quadrant while doing the Spoils of Pandarian encounter
						if (combat && onSpoilsOfPandariaEncounter && !IsInQuadrantPair(mySopQuad, unit.Location))
						{
							return true;
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
					if (unit.Entry == IceTombId && unit.DistanceSqr < 40*40)
					{
						outgoingunits.Add(unit);
					}
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
							// these do heals so need to be dealt with first
						case ZarthikAmberPriestId:
							// break out group members from the ice tomb
						case IceTombId:
							priority.Score += 5000;
							break;
					}
				}
			}
		}

		public override void RemoveHealTargetsFilter(List<WoWObject> units)
		{
			bool combat = Me.Combat;
			bool sopNotComplete = ScriptHelpers.IsBossAlive("Spoils of Pandoria");
			SopQuadrantPair mySopQuadPair = sopNotComplete ? GetSopQuadrantPairAtLocation(Me.Location) : SopQuadrantPair.None;

			SopQuadrantPair leaderQuadPair = sopNotComplete && _spoilsOfPandariaLeader != null && _spoilsOfPandariaLeader.IsValid
				? GetSopQuadrantPairAtLocation(_spoilsOfPandariaLeader.Location)
				: SopQuadrantPair.None;


			bool onSpoilsOfPandariaEncounter = sopNotComplete &&
												(mySopQuadPair != SopQuadrantPair.None || leaderQuadPair != SopQuadrantPair.None);

			units.RemoveAll(
				ret =>
				{
					WoWUnit unit = ret.ToUnit();
					if (unit != null)
					{
						// remove all units that are not in my quadrant while doing the Spoils of Pandarian encounter
						if (combat && onSpoilsOfPandariaEncounter && !IsInQuadrantPair(mySopQuadPair, unit.Location))
						{
							return true;
						}
					}
					return false;
				});
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

			TreeHooks.Instance.InsertHook("Dungeonbot_FollowerMovement", 0, CreateBehavior_FollowerMovement());
		}


		public override void OnExit()
		{
			TreeHooks.Instance.RemoveHook("Dungeonbot_FollowerMovement", CreateBehavior_FollowerMovement());
		}

		#endregion

		#region Root Behavior

		private const uint AcidSpraySpellId = 146978;
		private const uint ScorchedEarthSpellId = 146225;
		private const uint ScorchedEarthMissileId = 146221;

		private readonly WaitTimer _isNotMovingTimer = WaitTimer.OneSecond;

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0)]
		public Composite RootBehavior()
		{
			// avoid the acid spray used by trash.
			AddAvoidObject(
				ctx => true,
				9,
				o =>
				{
					var areaTrigger = o as WoWAreaTrigger;
					return areaTrigger != null && areaTrigger.SpellId == AcidSpraySpellId;
				});

			// avoid Scorched Earth used by trash at entrance
			AddAvoidObject(
				ctx => true,
				4,
				o =>
				{
					var areaTrigger = o as WoWAreaTrigger;
					return areaTrigger != null && areaTrigger.SpellId == ScorchedEarthSpellId;
				});

			// avoid Scorched Earth used by trash at entrance
			AddAvoidLocation(
				ctx => true,
				4,
				m => ((WoWMissile) m).ImpactPosition,
				() => WoWMissile.InFlightMissiles.Where(m => m.SpellId == ScorchedEarthMissileId));

			return new PrioritySelector(
				// reset the isMovingTimer whenever bot is not moving. This timer is used to avoid some stuff while not moving.
				// Because we need to move when running out we use a timer to avoid 'stop n go' type behavior.
				new Action(
					ctx =>
					{
						if (!Me.IsMoving)
							_isNotMovingTimer.Reset();
						return RunStatus.Failure;
					})
				);
		}

		#endregion

		#region Malkorok

		private const int BloodRageSpellId = 142879;
		private const int ArcingSmashSpellId = 143805;
		private const uint BreathOfYShaarjId = 142842;
		private const uint MalkorokId = 71454;
		private const uint ImplosionId = 71470;
		private const uint ArcingSmashId = 71455;

		private readonly WaitTimer _implodingEnergyTimer = new WaitTimer(TimeSpan.FromSeconds(2));
		private WoWPoint _lastImplodingEnergyPoint;
		private WoWPoint _malkorokRoomCenter = new WoWPoint(1914.795, -4950.756, -198.9773);
		private WoWPoint _malkorokSpreadoutPoint = WoWPoint.Zero;

		[EncounterHandler(71454, "Malkorok", Mode = CallBehaviorMode.Proximity)]
		public Composite CreateBehavior_Malkorok()
		{
			const float roomRadius = 40;
			WoWPoint breathOfYShaarjMoveTo = WoWPoint.Zero;

			var arcingSmashTimer = new WaitTimer(TimeSpan.FromSeconds(10));

			var arcingSmashAngles = new List<ScriptHelpers.AngleSpan>();
			// run away from raid members if has the Displaced Energy debuf
			AddAvoidObject(ctx => Me.HasAura("Displaced Energy"), 15, o => !o.IsMe && o is WoWPlayer);
			// don't pull boss and don't stand directly under him
			AddAvoidObject(ctx => true, o => o.ToUnit().Combat ? 3 : 30, o => o.Entry == MalkorokId && o.ToUnit().IsAlive);
			//AddAvoidObject(ctx => Me.IsRange(), 15, o => o.Entry == MalkorokId && o.ToUnit().Combat && !o.ToUnit().HasAura(BloodRageSpellId));

			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				// Out of combat behavior
				new Decorator(
					ctx => !boss.Combat,
					new PrioritySelector(
						// initialize the ranged stand at location during phase one.
						new Decorator(
							ctx => _malkorokSpreadoutPoint == WoWPoint.Zero && Me.IsFollower(),
							new Action(
								ctx =>
								{
									_malkorokSpreadoutPoint = GetRandomPointAroundLocation(_malkorokRoomCenter, 0, 359, 20, roomRadius);
									return RunStatus.Failure;
								}))
						)),
				// InCombat Behavior
				new Decorator(
					ctx => boss.Combat,
					new PrioritySelector(
						// Ranged phase 1 logic.
						new Decorator(
							ctx =>
								Me.IsRangeDps() && boss.CastingSpellId != BloodRageSpellId && !boss.HasAura(BloodRageSpellId)
								&& boss.CastingSpellId != ArcingSmashSpellId && boss.CastingSpellId != BreathOfYShaarjId
								&& !AvoidanceManager.IsRunningOutOfAvoid,
							// Stand in imploding energy
							new PrioritySelector(
								ctx => GetImplodingEnergyMoveTo(),
								new Decorator<WoWPoint>(
									moveto => moveto != WoWPoint.Zero && moveto.DistanceSqr(Me.Location) > 4*4,
									new Helpers.Action<WoWUnit>(unit => Navigator.MoveTo(unit.Location))),
								// move to my phase one location.
								new Decorator<WoWPoint>(
									moveto => moveto == WoWPoint.Zero && Me.Location.DistanceSqr(_malkorokSpreadoutPoint) > 10*10,
									new Action(ctx => Navigator.MoveTo(_malkorokSpreadoutPoint)))
								)),
						// Avoid the Arcing Smash ability
						//71454, "Malkorok
						new Decorator(
							ctx => boss.CastingSpellId == ArcingSmashSpellId,
							new PrioritySelector(
								new Decorator(
									ctx => arcingSmashTimer.IsFinished,
									new Action(
										ctx =>
										{
											var angle = (int) WoWMathHelper.RadiansToDegrees(WoWMathHelper.NormalizeRadian(boss.Rotation));
											arcingSmashAngles.Add(new ScriptHelpers.AngleSpan(angle, 60));
											Logger.Write("Saved angle ({0} deg) of Arcing Smash", angle);
											arcingSmashTimer.Reset();
											return RunStatus.Failure;
										})),
								ScriptHelpers.CreateAvoidUnitAnglesBehavior(ctx => true, ctx => boss, new ScriptHelpers.AngleSpan(0, 60)))),
						// Avoid the Breath Of YShaarjId ability
						new Decorator(
							ctx => boss.CastingSpellId == BreathOfYShaarjId && arcingSmashAngles.Any(),
							new Sequence(
								new Action(
									ctx =>
									{
										Logger.Write(
											"Boss is casting Breath of Y'Shaarj and there are {0} saved arcing Smash angles",
											arcingSmashAngles.Count);
										foreach (ScriptHelpers.AngleSpan ang in arcingSmashAngles)
										{
											Logger.Write("\tAngle: {0}", ang.Angle);
										}
									}),
								new DecoratorContinue(
									ctx => ScriptHelpers.IsLocationInAngleSpans(Me.Location, boss.Location, 0, arcingSmashAngles.ToArray()),
									new Sequence(
										ctx =>
											ScriptHelpers.GetPointOutsideAngleSpans(
												Me.Location,
												boss.Location,
												0,
												roomRadius + 10,
												out breathOfYShaarjMoveTo,
												arcingSmashAngles.ToArray()),
										new DecoratorContinue<bool>(
											success => success,
											ScriptHelpers.CreateMoveToContinue(ctx => true, ctx => breathOfYShaarjMoveTo, true))
										)),
								new Action(ctx => arcingSmashAngles.Clear()))),
						ScriptHelpers.GetBehindUnit(
							ctx =>
								!boss.HasAura(BloodRageSpellId) && boss.CastingSpellId != ArcingSmashSpellId &&
								boss.CastingSpellId != BreathOfYShaarjId && Me.IsMeleeDps() && !boss.MeIsSafelyBehind,
							ctx => boss),
						// Phase 2 logic
						// Stack up on tank
						new Decorator(
							ctx =>
								(boss.CastingSpellId == BloodRageSpellId || boss.HasAura(BloodRageSpellId)) && !AvoidanceManager.IsRunningOutOfAvoid,
							new PrioritySelector(
								ctx => ScriptHelpers.Tank,
								new Decorator<WoWPlayer>(
									player => Me.Location.DistanceSqr(player.Location) > 8*8,
									new Helpers.Action<WoWPlayer>(player => Navigator.MoveTo(player.Location)))))))
				);
		}

		private WoWPoint GetImplodingEnergyMoveTo()
		{
			WoWUnit implodingEnergy = (from unit in ObjectManager.GetObjectsOfTypeFast<WoWUnit>()
				where unit.Entry == ImplosionId
				let loc = unit.Location
				let myDistSqr = Me.Location.DistanceSqr(loc)
				where myDistSqr <= 20*20 && !ScriptHelpers.GroupMembers.Any(g => g.Guid != Me.Guid && g.Location.DistanceSqr(loc) < 5*5)
				orderby myDistSqr
				select unit).FirstOrDefault();
			if (implodingEnergy != null)
			{
				_implodingEnergyTimer.Reset();
				_lastImplodingEnergyPoint = implodingEnergy.Location;
			}
			else if (_implodingEnergyTimer.IsFinished)
			{
				_lastImplodingEnergyPoint = WoWPoint.Zero;
			}
			return _lastImplodingEnergyPoint;
		}

		private WoWPoint GetRandomPointAroundLocation(WoWPoint point, int startAng, int endAng, float minDist, float maxDist)
		{
			if (endAng < startAng)
				endAng += 360;
			float randomRadians = WoWMathHelper.NormalizeRadian(
				WoWMathHelper.DegreesToRadians(ScriptHelpers.Rnd.Next(startAng, endAng)));
			int randomDist = ScriptHelpers.Rnd.Next((int) minDist, (int) maxDist);
			return point.RayCast(randomRadians, randomDist);
		}

		#endregion

		#region Spoils of Pandaria

		private const uint StoneStatueId = 72535;
		private const uint XiangLinId = 73725;
		private const uint JadeTempestSpellId = 148582;
		private const uint KunDaId = 71408;
		private const uint FractureSpellId = 148513;
		private const uint SetToBlowSpellId = 146365;
		private const uint EncapsulatedPheromonesSpellId = 145285;
		// todo. check out this ability
		private const uint MatterScrambleId = 145369;
		// todo. should probably stand in this.
		private const uint CrimsonReconstitutionSpellId = 145270;
		private const uint MoguRuneOfPowerSpellId = 145460;
		private const int ForbiddenMagicId = 145230;
		private const uint ZarthikAmberPriestId = 71397;
		private const uint WindstormSpellId = 145286;
		private const int HardenFleshSpellId = 144922;
		private const int EarthenShardSpellId = 144923;
		private const uint SparkOfLifeId = 71433;
		private const uint GustingBombSpellId = 145714;
		private const uint ThrowExplosivesMissileId = 145702;
		// this spell needs to be casted when player has 'Set to Blow' aura
		private const int ThrowBombSpellId = 146364;
		private const uint AnimatedStrikeSpellId = 145523;
		private const uint SecuredStockpileOfPandarenSpoilsId = 220823;
		private const uint SecuredStockpileofPandarenSpoilsId = 71889;
		private const uint SpoilsOfPandoriaId = 72281;
		private const uint IronGateId = 221799;

		private readonly WoWPoint _northwestQuadWaitPoint = new WoWPoint(1649.238, -5132.08, -271.0277);

		private readonly DungeonArea _sopEastQuadrant = new DungeonArea(
			new Vector2(1567.95f, -5174.406f),
			new Vector2(1573.164f, -5166.262f),
			new Vector2(1610.061f, -5141.231f),
			new Vector2(1616.826f, -5141.278f),
			new Vector2(1635.122f, -5155.649f),
			new Vector2(1671.289f, -5209.318f),
			new Vector2(1637.962f, -5234.132f),
			new Vector2(1630.616f, -5230.665f),
			new Vector2(1603.307f, -5226.608f));

		private readonly DungeonArea _sopNorthQuadrant = new DungeonArea(
			new Vector2(1651.894f, -5117.829f),
			new Vector2(1654.417f, -5111.377f),
			new Vector2(1691.258f, -5086.529f),
			new Vector2(1699.209f, -5087.609f),
			new Vector2(1736.174f, -5137.109f),
			new Vector2(1729.479f, -5164.111f),
			new Vector2(1730.036f, -5172.09f),
			new Vector2(1694.556f, -5193.469f),
			new Vector2(1658.348f, -5140.035f));

		private readonly DungeonArea _sopSouthQuadrant = new DungeonArea(
			new Vector2(1533.919f, -5079.797f),
			new Vector2(1569.365f, -5058.222f),
			new Vector2(1605.661f, -5111.96f),
			new Vector2(1612.014f, -5134.198f),
			new Vector2(1609.584f, -5140.528f),
			new Vector2(1572.684f, -5165.438f),
			new Vector2(1563.07f, -5167.211f),
			new Vector2(1527.79f, -5114.907f));

		private readonly DungeonArea _sopWestQuadrant = new DungeonArea(
			new Vector2(1592.677f, -5042.591f),
			new Vector2(1625.943f, -5017.755f),
			new Vector2(1633.337f, -5021.236f),
			new Vector2(1660.719f, -5025.129f),
			new Vector2(1696.021f, -5077.578f),
			new Vector2(1690.611f, -5085.895f),
			new Vector2(1653.852f, -5110.689f),
			new Vector2(1647.133f, -5110.603f),
			new Vector2(1628.892f, -5096.288f));


		private readonly WoWPoint _southeastQuadWaitPoint = new WoWPoint(1614.535, -5118.924, -271.0277);

		private readonly WoWPoint _sopChestLoc = new WoWPoint(1631.799, -5125.967, -271.1219);

		private readonly WoWPoint _sopChestNearbyLoc = new WoWPoint(1635.176, -5130.125, -271.0277);

		private Composite _followerMovementBehavior;
		private PerFrameCachedValue<WoWUnit> _getSecuredStockpileOfPandariaSpoils;
		private SopQuadrantPair _myAssignedQuadrantPair;

		private WoWPlayer _spoilsOfPandariaLeader;

		private bool DoingSpoilsOfPandariaEncounter
		{
			get { return SecuredStockpileOfPandariaSpoils != null; }
		}

		private bool SpoilsOfPandariaEncounterIsOver
		{
			get
			{
				return ObjectManager.GetObjectsOfTypeFast<WoWGameObject>()
					.Any(g => g.Entry == IronGateId && g.State == WoWGameObjectState.Active);
			}
		}

		private WoWUnit SecuredStockpileOfPandariaSpoils
		{
			get
			{
				return _getSecuredStockpileOfPandariaSpoils ??
						(_getSecuredStockpileOfPandariaSpoils =
							new PerFrameCachedValue<WoWUnit>(
								() =>
									ObjectManager.GetObjectsOfType<WoWUnit>()
										.FirstOrDefault(u => u.Entry == SecuredStockpileofPandarenSpoilsId && u.HasAura("Unstable Defense Systems"))));
			}
		}

		private SopQuadrantPair GetSopQuadrantPairAtLocation(WoWPoint point)
		{
			SopQuadrant quad = GetQuadrantAtLocation(point);
			switch (quad)
			{
				case SopQuadrant.South:
				case SopQuadrant.East:
					return SopQuadrantPair.SouthEast;
				case SopQuadrant.North:
				case SopQuadrant.West:
					return SopQuadrantPair.NorthWest;
				default:
					return SopQuadrantPair.None;
			}
		}

		private SopQuadrant GetQuadrantAtLocation(WoWPoint point)
		{
			if (point.Z > -280 || point.Z < -295)
				return SopQuadrant.None;

			if (_sopSouthQuadrant.IsPointInPoly(point))
				return SopQuadrant.South;

			if (_sopWestQuadrant.IsPointInPoly(point))
				return SopQuadrant.West;

			if (_sopNorthQuadrant.IsPointInPoly(point))
				return SopQuadrant.North;

			if (_sopEastQuadrant.IsPointInPoly(point))
				return SopQuadrant.East;

			return SopQuadrant.None;
		}

		[EncounterHandler(72281, "Spoils of Pandaria", Mode = CallBehaviorMode.Proximity, BossRange = 100)]
		public Func<WoWUnit,Task<bool>> CreateBehavior_SpoilsOfPandaria()
		{
			// todo figure out the radius of this attack
			AddAvoidObject(ctx => true, 10, o => o.Entry == XiangLinId && o.ToUnit().CastingSpellId == JadeTempestSpellId);

			AddAvoidObject(
				ctx => true,
				8,
				o => o.Entry == KunDaId && o.ToUnit().CastingSpellId == FractureSpellId,
				o => WoWMathHelper.CalculatePointInFront(o.Location, o.Rotation, 8));

			// run away from raid members so they don't die with me..
			AddAvoidObject(ctx => Me.HasAura("Set to Blow"), 20, o => !o.IsMe && o is WoWPlayer);

			AddAvoidObject(ctx => true, 4, SparkOfLifeId);

			AddAvoidObject(
				ctx => true,
				5,
				o => o.Entry == StoneStatueId && o.ToUnit().CastingSpellId == AnimatedStrikeSpellId,
				o => WoWMathHelper.CalculatePointInFront(o.Location, o.Rotation, 5));

			AddAvoidObject(
				ctx => !_isNotMovingTimer.IsFinished,
				3.2f,
				o => o is WoWAreaTrigger && ((WoWAreaTrigger) o).SpellId == EncapsulatedPheromonesSpellId);


			AddAvoidObject(
				ctx => true,
				o => Me.HasAura("Set to Blow") ? 3 : 7,
				o => o is WoWAreaTrigger && ((WoWAreaTrigger) o).SpellId == SetToBlowSpellId);


			AddAvoidObject(ctx => true, 3, o => o is WoWAreaTrigger && ((WoWAreaTrigger) o).SpellId == GustingBombSpellId);

			AddAvoidObject(ctx => true, 5, o => o is WoWAreaTrigger && ((WoWAreaTrigger) o).SpellId == WindstormSpellId);

			AddAvoidLocation(
				ctx => true,
				2,
				o => ((WoWMissile) o).ImpactPosition,
				() => WoWMissile.InFlightMissiles.Where(m => m.SpellId == ThrowExplosivesMissileId));

			return async boss =>
			{
				if (!ScriptHelpers.IsBossAlive("Spoils of Pandaria"))
					return false;
				_spoilsOfPandariaLeader = GetSpoilsOfPandoiaLeader();


				// Drop the bombs away from group members.
				if (Me.HasAura("Set to Blow") && ActionBar.Extra.IsActive
					&& !ScriptHelpers.GroupMembers
						.Any(g => g.Guid != Me.Guid && g.IsAlive && g.Location.DistanceSqr(Me.Location) <= 10*10))
				{
					var button = ActionBar.Extra.Buttons.FirstOrDefault();
					if (button != null && button.CanUse)
					{
						Logger.Write("Dropping bombs");
						button.Use();
						return true;
					}
				}

				WoWPoint matterScrableMoveTo = (from missile in WoWMissile.InFlightMissiles
					where missile.SpellId == MatterScrambleId
					let distSqr = missile.ImpactPosition.DistanceSqr(Me.Location)
					where
						distSqr < 20*20 &&
						!GroupMember.GroupMembers.Any(g => g.Guid != Me.Guid && g.Location.DistanceSqr(missile.ImpactPosition) < 4*4)
					orderby distSqr
					select missile.ImpactPosition).FirstOrDefault();

				// handle Matter Scramble. Just move to missile impact location of this ability.  
				if (matterScrableMoveTo != WoWPoint.Zero && Me.IsRangeDps())
				{
					if (Navigator.AtLocation(matterScrableMoveTo)
						|| (await CommonCoroutines.MoveTo(matterScrableMoveTo, "Quadrant leader")).IsSuccessful())
					{
						return true;
					}
				}

				// Move to leader if he's far away.
				if (_spoilsOfPandariaLeader != null && _spoilsOfPandariaLeader.IsAlive &&
					GetQuadrantAtLocation(_spoilsOfPandariaLeader.Location) != SopQuadrant.None &&
					(_spoilsOfPandariaLeader.DistanceSqr > 35*35 || GetQuadrantAtLocation(Me.Location) == SopQuadrant.None)
					&& (await CommonCoroutines.MoveTo(_spoilsOfPandariaLeader.Location, "Quadrant leader")).IsSuccessful())
				{
					return true;
				}

				var currentTarget = Me.CurrentTarget;
				if (currentTarget != null
					&& (await ScriptHelpers.InterruptCast(currentTarget, HardenFleshSpellId, EarthenShardSpellId)
						// dispell Residue (a HOT) from current target if exists
						|| await ScriptHelpers.DispelEnemy("Residue", ScriptHelpers.EnemyDispelType.Magic, currentTarget)
						// dispell Rage of the Empress (damage buff) from current target if exists
						|| await ScriptHelpers.DispelEnemy("Rage of the Empress", ScriptHelpers.EnemyDispelType.Magic, currentTarget)
						|| await ScriptHelpers.DispelEnemy("Enrage", ScriptHelpers.EnemyDispelType.Enrage, currentTarget)))
				{
					return true;
				}

				if (SpoilsOfPandariaEncounterIsOver)
					ScriptHelpers.MarkBossAsDead("Spoils of Pandoria");
				// if there's nothing to do then we don't want to fall through because CR/Follow behavior could make bot 
				// follow group.. something we don't want to do while waiting for spawns. 
				return Targeting.Instance.IsEmpty() && Me.Combat &&
						(!Me.IsHealer() || HealTargeting.Instance.IsEmpty() || HealTargeting.Instance.FirstUnit.HealthPercent >= 90);
			};		
		}

		private Composite CreateBehavior_FollowerMovement()
		{
			WoWPoint mySopWaitPoint = WoWPoint.Zero;
			var sopUpdateWaitPoint = new WaitTimer(TimeSpan.FromSeconds(10));

			return _followerMovementBehavior ??
					(_followerMovementBehavior =
						new PrioritySelector(
							// Malkorok out of combat positoning
							new Decorator(
								ctx =>
									ObjectManager.GetObjectsOfTypeFast<WoWUnit>()
										.Any(u => u.Entry == MalkorokId && u.IsAlive && !u.Combat && _malkorokSpreadoutPoint != WoWPoint.Zero),
								new PrioritySelector(
									ctx =>
									{
										WoWPoint vector = _malkorokSpreadoutPoint - _malkorokRoomCenter;
										return _malkorokRoomCenter.RayCast((float) Math.Atan2(vector.Y, vector.X), 35);
									},
									new Decorator<WoWPoint>(
										moveto => !Navigator.AtLocation(moveto),
										new Helpers.Action<WoWPoint>(moveto => Navigator.MoveTo(moveto))),
									// prevent behavior from dropping down to default raid following behavior.
									new ActionAlwaysSucceed())),
							
							new Decorator(
								ctx =>
									ObjectManager.ObjectList.Any(o => o.Entry == SpoilsOfPandoriaId && o.DistanceSqr < 70*70),
								new PrioritySelector(
									
									// Spoils of Pandaria out of combat positioning
									new Decorator(ctx =>  !Me.Combat && !DoingSpoilsOfPandariaEncounter, 
										new PrioritySelector(
											new Decorator(ctx => sopUpdateWaitPoint.IsFinished,
												new Action(
													ctx =>
													{
														// only use my location if i'm in a 1 man party.
														WoWPoint[] locs = Me.GroupInfo.PartySize > 1
															? Me.PartyMembers.Where(p => !p.IsMe).Select(p => p.Location).ToArray()
															: new[] { Me.Location };

														WoWPoint avgPartyLoc = locs.Aggregate((a, b) => a + b) / locs.Length;

														// pick one of 2 group locations that your party members are nearest too.
														WoWPoint centerMovePoint;
														if (avgPartyLoc.DistanceSqr(_northwestQuadWaitPoint) < avgPartyLoc.DistanceSqr(_southeastQuadWaitPoint))
														{
															centerMovePoint = _northwestQuadWaitPoint;
															_myAssignedQuadrantPair = SopQuadrantPair.NorthWest;
														}
														else
														{
															centerMovePoint = _southeastQuadWaitPoint;
															_myAssignedQuadrantPair = SopQuadrantPair.SouthEast;
														}

														mySopWaitPoint = ScriptHelpers.GetRandomPointAtLocation(centerMovePoint, 5);
														sopUpdateWaitPoint.Reset();
														return RunStatus.Failure;
													})),
											new Decorator(
												ctx => Me.Location.DistanceSqr(mySopWaitPoint) > 10 * 10,
												new Action(ctx => Navigator.MoveTo(mySopWaitPoint))),
											// prevent behavior from dropping down to default raid following behavior.
											new ActionAlwaysSucceed())),

									// prevent bot from following raid while Spoils of Pandaria encounter is in process.
									new Decorator(ctx =>  DoingSpoilsOfPandariaEncounter, 
										new ActionAlwaysSucceed())
									))));
		}

		private bool IsInQuadrantPair(SopQuadrantPair quadPair, WoWPoint destination)
		{
			if (quadPair == SopQuadrantPair.None)
				return false;

			return GetSopQuadrantPairAtLocation(destination) == quadPair;
		}

		[EncounterHandler(71393, "Mogu Shadow Ritualist")]
		public Composite CreateBehavior_MoguShadowRitualist()
		{
			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				// interrupt Forbidden Magic cast
				ScriptHelpers.CreateInterruptCast(ctx => boss, ForbiddenMagicId));
		}


		private WoWAreaTrigger GetNearbyRuneOfPower()
		{
			return ObjectManager.GetObjectsOfTypeFast<WoWAreaTrigger>()
				.Where(a => a.SpellId == MoguRuneOfPowerSpellId)
				.OrderBy(a => a.DistanceSqr)
				.FirstOrDefault(
					a =>
					{
						WoWUnit myTarget = Me.IsHealer ? HealTargeting.Instance.FirstUnit : Targeting.Instance.FirstUnit;
						if (myTarget == null)
							return false;
						float maxRange = Me.IsMeleeDps() ? myTarget.MeleeRange() : 35;
						WoWPoint pos = a.Location;
						if (Me.Location.Distance(pos) > maxRange)
							return false;
						pos.Z += 1;
						return GameWorld.IsInLineOfSpellSight(pos, myTarget.GetTraceLinePos());
					});
		}

		private WoWPlayer GetSpoilsOfPandoiaLeader()
		{
			if (_myAssignedQuadrantPair == SopQuadrantPair.None)
				return null;
			WoWPlayer[] tanks = ScriptHelpers.GroupMembers.Where(g => g.IsTank && g.Player != null).Select(p => p.Player).ToArray();

			// wait for one of the tanks to drop down into gauntlet before deciding which is leader.
			if (tanks.All(t => GetQuadrantAtLocation(t.Location) == SopQuadrant.None))
				return null;

			// return the tank that my party members are nearest to
			return tanks.FirstOrDefault(t => GetSopQuadrantPairAtLocation(t.Location) == _myAssignedQuadrantPair);
		}

		private async Task<bool> HandleSpoilsOfPandariaMovement(WoWPoint moveToDestination)
		{

			// if bot tries to move on top of the chest (no mesh there so would result in a navigation error or it tries to move to another level) -
			// then move to a nearby location.
			if (moveToDestination.Distance2DSqr(_sopChestLoc) < 7 * 7 && moveToDestination.Z > -268)
				return (await CommonCoroutines.MoveTo(_sopChestNearbyLoc)).IsSuccessful();

			// Some CR's (cough Singular) will fight with script and try to move to other side
			// so we need to show them who's boss and not let them.
			if (DoingSpoilsOfPandariaEncounter && ScriptHelpers.IsViable(_spoilsOfPandariaLeader) &&
				moveToDestination.DistanceSqr(_spoilsOfPandariaLeader.Location) > 40*40)
			{
				SopQuadrantPair mySopQuadPair = GetSopQuadrantPairAtLocation(_spoilsOfPandariaLeader.Location);
				SopQuadrantPair otherSopQuadPair = GetSopQuadrantPairAtLocation(moveToDestination);
				if (otherSopQuadPair != mySopQuadPair)
					return true;
			}

			return await HandleLiftUsage(moveToDestination);
		}

		// Handles the lifts in at Spoils of Pandoria encounter
		private async Task<bool> HandleLiftUsage(WoWPoint moveToDestination)
		{
			// only try to use these hooks when encounter is over.
			if (ScriptHelpers.IsBossAlive("Spoils of Pandoria"))
				return false;

			WoWPoint myLoc = Me.Location;
			SopQuadrantPair mySopQuadPair = GetSopQuadrantPairAtLocation(myLoc);

			if (mySopQuadPair == SopQuadrantPair.None || Me.IsOnTransport) 
				return false;

			if (IsInQuadrantPair(mySopQuadPair, moveToDestination)) 
				return false;

			WoWUnit lift =
				ObjectManager.GetObjectsOfTypeFast<WoWUnit>()
					.Where(u => u.Entry == LiftHookId && IsInQuadrantPair(mySopQuadPair, u.Location) && u.HasAura("Lift Hook"))
					.OrderBy(u => u.DistanceSqr)
					.FirstOrDefault();

			if (lift == null)
				return false;

			// need to pretty much stand under the lift before interacting.
			if (lift.Distance2DSqr > 1.5*1.5)
				return (await CommonCoroutines.MoveTo(lift.Location)).IsSuccessful();

			if (Me.IsMoving)
				WoWMovement.MoveStop();

			lift.Interact();
			return true;
		}

		private enum SopQuadrant
		{
			None,
			North,
			East,
			South,
			West
		}

		private enum SopQuadrantPair
		{
			None,
			NorthWest,
			SouthEast,
		}

		#endregion

		#region Thok the Bloodthirsty

		private const uint ThokTheBloodthirstyId = 71529;
		private const int FixateSpellId = 143445;
		private const int IceTombId = 71720;
		private const uint BurningBloodSpellId = 143783;

		private readonly WaitTimer _updateRangedGroupCenterPos = WaitTimer.FiveSeconds;
		private WoWPoint _rangedGroupCenter;

		[EncounterHandler(71529, "Thok the Bloodthirsty")]
		public Func<WoWUnit, Task<bool>> CreateBehavior_ThokTheBloodthirsty()
		{
			// run from Thok when fixated or while he's picking a new target.
			AddAvoidObject(
				ctx => true,
				// Increase the radius when moving to prevent stop/go behavior when standing at the edge of avoid and boss is moving
				ctx => _isNotMovingTimer.IsFinished ? 45 : 40,
				o =>
				{
					if (o.Entry != ThokTheBloodthirstyId)
						return false;
					if (Me.HasAura(FixateSpellId))
						return true;
					var unit = (WoWUnit) o;

					return unit.CastingSpellId == FixateSpellId && !unit.IsChanneling;
				});

			// avoid Thok's front area
			AddAvoidObject(
				ctx => true,
				// Increase the radius when moving to prevent stop/go behavior when standing at the edge of avoid and boss is moving
				ctx => _isNotMovingTimer.IsFinished ? 25 : 30,
				o => o.Entry == ThokTheBloodthirstyId && o.ToUnit().IsAlive,
				o => WoWMathHelper.CalculatePointInFront(o.Location, o.Rotation, 25));
			// avoid Thok's back side when he doesn't have blood frenzy buff, otherwise avoid the center.			
			AddAvoidObject(
				ctx => true,
				// Increase the radius when moving to prevent stop/go behavior when standing at the edge of avoid and boss is moving
				ctx => _isNotMovingTimer.IsFinished? 20: 25,
				o => o.Entry == ThokTheBloodthirstyId && o.ToUnit().IsAlive,
				o => WoWMathHelper.CalculatePointBehind(o.Location, o.Rotation, o.ToUnit().HasAura("Blood Frenzy") ? -10 : 20));

			// avoid the fire on ground.
			AddAvoidObject(
				ctx => true,
				3,
				o =>
				{
					var areaTrigger = o as WoWAreaTrigger;
					return areaTrigger != null && areaTrigger.SpellId == BurningBloodSpellId;
				});

			return async boss =>
			{
				if (await ScriptHelpers.StayAtLocationWhile(
					() => Me.IsRange() && !boss.HasAura("Blood Frenzy"),
					GetRangedGroupCenter(),
					"Group Center",
					10,
					ScriptHelpers.GroupRoleFlags.Ranged))
				{
					return true;
				}
				return false;
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

		#endregion
	}
}