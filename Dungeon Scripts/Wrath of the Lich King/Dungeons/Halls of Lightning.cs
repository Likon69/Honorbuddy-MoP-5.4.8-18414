using System;
using System.Collections.Generic;
using System.Linq;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.Helpers;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;

using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Wrath_of_the_Lich_King
{
	public class HallsOfLightning : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 207; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(9185.314, -1386.893, 1110.216); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(1330.962, 275.4182, 52.81053); }
		}

		public override bool IsFlyingCorpseRun
		{
			get { return true; }
		}

		public override void OnEnter()
		{
			_volkhanCleartrashPath.CycleTo(_volkhanCleartrashPath.First);
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			var tank = ScriptHelpers.Tank;
			var slagsAtackingTank = units.Count(u => u.Entry == SlagId && tank != null && u.ToUnit().CurrentTargetGuid == tank.Guid);
			var tankY = tank != null && tank.IsAlive ? tank.Y : Me.Y;
			units.RemoveAll(
				ret =>
				{
					WoWUnit unit = ret.ToUnit();
					if (unit != null)
					{
						if (ret.Entry == SlagId && (!unit.Combat || (Me.IsTank() && unit.CurrentTargetGuid == Me.Guid || Me.IsRange()) && slagsAtackingTank < 6 && tankY > -220))
							return true;
						if (ret.Entry == SparkofIonarId)
							return true;
						if (ret.Entry == GeneralBjarngrimId && ret.ToUnit().HasAura("Whirlwind") && Me.IsDps() && Me.IsMelee())
							return true;
						if (ret.Entry == LokenId && _lightningNovaIds.Contains(ret.ToUnit().CastingSpellId) && Me.IsMelee())
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
				if (unit != null) { }
			}
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var priority in units)
			{
				var unit = priority.Object as WoWUnit;
				if (unit != null)
				{
					if (unit.Entry == StormforgedLieutenantId && StyxWoW.Me.IsDps())
						priority.Score += 500;
					else if (unit.Entry == StormforgedMenderId && StyxWoW.Me.IsDps())
						priority.Score += 600;
				}
			}
		}

		#endregion


		[EncounterHandler(56027, "Stormherald Eljrrin", Mode = CallBehaviorMode.Proximity, BossRange = 35)]
		public Composite QuestPickupHandler()
		{
			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				new Decorator(
					ctx => !Me.Combat && !ScriptHelpers.WillPullAggroAtLocation(unit.Location) && unit.QuestGiverStatus == QuestGiverStatus.Available,
					ScriptHelpers.CreatePickupQuest(ctx => unit)),
				new Decorator(
					ctx => !Me.Combat && !ScriptHelpers.WillPullAggroAtLocation(unit.Location) && unit.QuestGiverStatus == QuestGiverStatus.TurnIn,
					ScriptHelpers.CreateTurninQuest(ctx => unit)));
		}

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		private readonly CircularQueue<WoWPoint> _volkhanCleartrashPath = new CircularQueue<WoWPoint>
		{
			new WoWPoint(1361.804, -188.0309, 52.02539),
			new WoWPoint(1366.668, -145.0833, 52.0296),
			new WoWPoint(1362.529, -184.9581, 52.02528),
			new WoWPoint(1303.465, -190.1832, 52.02384),
			new WoWPoint(1299.959, -140.8916, 52.00739)
		};

		private WoWUnit _sparks;

		private const uint SlagId = 28585;
		private const uint StormforgedLieutenantId = 29240;
		private const uint GeneralBjarngrimId = 28586;
		private const uint StormforgedMenderId = 28582;
		const uint SparkofIonarId = 28926;

		[EncounterHandler(28586, "General Bjarngrim", Mode = CallBehaviorMode.Proximity, BossRange = 300)]
		public Composite GeneralBjarngrimEncounter()
		{
			WoWUnit boss = null;
			var runToPoint = new WoWPoint(1331.974, 101.1538, 40.18048);
			AddAvoidObject(ctx => Me.IsFollower() && !Me.IsCasting, 8, o => o.Entry == GeneralBjarngrimId && (o.ToUnit().HasAura("Whirlwind") || Me.IsRange() && !o.ToUnit().IsMoving));

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				// run from boss if tank and I'm fighting stuff or he's near adds that would aggro if boss is pulled.
				new Decorator(
					ctx =>
					StyxWoW.Me.IsLeader() && !boss.Combat && boss.DistanceSqr <= 75 * 75 &&
					(StyxWoW.Me.IsActuallyInCombat || ScriptHelpers.GetUnfriendlyNpsAtLocation(boss.Location, 35, u => u.Entry != StormforgedLieutenantId && u != boss).Any()),
					new Sequence(ScriptHelpers.CreateMoveToContinue(ctx => boss.DistanceSqr <= 90 * 90 && !boss.Combat, ctx => runToPoint, true))),
				new Decorator(
					ctx => boss.Combat,
					new PrioritySelector(
						ScriptHelpers.CreateAvoidUnitAnglesBehavior(ctx => !Me.IsTank() && boss.CurrentTargetGuid != Me.Guid && !boss.IsMoving && boss.Distance < 8, ctx => boss, new ScriptHelpers.AngleSpan(0, 180)),
						ScriptHelpers.CreateTankFaceAwayGroupUnit(8))));
		}

		[EncounterHandler(28587, "Volkhan", Mode = CallBehaviorMode.Proximity, BossRange = 110)]
		public Composite VolkhanEncounter()
		{
			WoWUnit boss = null;
			var roomCenterLoc = new WoWPoint(1331.7, -165.3561, 52.02283);
			var shatteringStompIds = new[] { 52237, 59529 };
			const uint brittleGolemId = 28681;
			AddAvoidObject(ctx => boss != null && boss.IsValid && shatteringStompIds.Contains(boss.CastingSpellId), 10, brittleGolemId);

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				// clear the room while being careful not to aggro boss.
				new Decorator(
					ctx =>
					StyxWoW.Me.IsTank() && ScriptHelpers.GetUnfriendlyNpsAtLocation(roomCenterLoc, 51, u => u.Z >= 50 && u != boss).Any() &&
					(Targeting.Instance.FirstUnit == null || Targeting.Instance.FirstUnit.DistanceSqr > 25 * 25),
					new PrioritySelector(
						ctx => _volkhanCleartrashPath.Peek(),
						new Decorator(ctx => StyxWoW.Me.Location.Distance2DSqr((WoWPoint)ctx) < 5 * 5, new Action(ctx => _volkhanCleartrashPath.Dequeue())),
						new Action(ctx => Navigator.GetRunStatusFromMoveResult(Navigator.MoveTo((WoWPoint)ctx)))))
				);
		}

		[EncounterHandler(28546, "Ionar", Mode = CallBehaviorMode.CurrentBoss)]
		public Composite StatueEncounter()
		{
			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				// for the statues that come alive..
				new Decorator(
					ctx => StyxWoW.Me.IsTank() && (boss == null || !boss.Combat) && StyxWoW.Me.Combat && Targeting.Instance.FirstUnit == null && StyxWoW.Me.IsMoving,
					new Action(ctx => WoWMovement.MoveStop())));
		}

		[EncounterHandler(28926, "Spark of Ionar")]
		[EncounterHandler(28546, "Ionar")]
		public Composite IonarEncounter()
		{
			WoWUnit boss = null;


			AddAvoidObject(
				ctx => Me.IsFollower(),
				8,
				u => u is WoWPlayer && u != StyxWoW.Me && (u.ToPlayer().HasAura("Static Overload") || Me.HasAura("Static Overload")));

			var tankLoc = new WoWPoint(1081.995, -261.8092, 61.20797);
			var runtoLoc = new WoWPoint(1177.047, -231.855, 52.36805);
			var disperseTimer = new WaitTimer(TimeSpan.FromSeconds(5));

			return new PrioritySelector(
				ctx =>
				{
					_sparks = ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == 28926).OrderBy(u => u.DistanceSqr).FirstOrDefault();
					return boss = ctx as WoWUnit;
				},
				new Action(
					ctx =>
					{
						if (boss != null && boss.CastingSpellId == 52770)
							disperseTimer.Reset();
						return RunStatus.Failure;
					}),
				new Decorator(
					ctx => !disperseTimer.IsFinished || (_sparks != null && _sparks.DistanceSqr <= 20 * 20),
					new Action(ctx => Navigator.GetRunStatusFromMoveResult(Navigator.MoveTo(runtoLoc)))),
				new Decorator(ctx => _sparks != null && Me.IsTank(), new ActionAlwaysSucceed()),

				new Decorator(ctx => Me.IsLeader() && boss.CurrentTargetGuid == StyxWoW.Me.Guid, ScriptHelpers.CreateTankUnitAtLocation(ctx => tankLoc, 7)));
		}

		private const uint LokenId = 28923;
		readonly int[] _lightningNovaIds = new int[] { 59835, 52960 };

		[EncounterHandler(28923, "Loken")]
		public Composite LokenEncounter()
		{
			WoWUnit boss = null;

			AddAvoidObject(ctx => true, 20, o => o.Entry == LokenId && _lightningNovaIds.Contains(o.ToUnit().CastingSpellId));

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				new Decorator(
					ctx => StyxWoW.Me.IsRange() && boss.DistanceSqr > 13 * 13,
					new Action(ctx => Navigator.GetRunStatusFromMoveResult(Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(StyxWoW.Me.Location, boss.Location, 10))))));
		}
	}
}