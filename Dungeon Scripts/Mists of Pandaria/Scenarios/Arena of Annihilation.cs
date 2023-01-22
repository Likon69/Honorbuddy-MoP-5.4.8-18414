
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.Helpers;
using Styx.Patchables;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
using AvoidanceManager = Bots.DungeonBuddy.Avoidance.AvoidanceManager;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Mists_of_Pandaria
{
	public class ArenaOfAnnihilation : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 511; }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (unit != null)
					{
						if (unit.Entry == ScarShellId && Me.IsMelee() && (unit.CastingSpellId == StoneSpinId || unit.HasAura(StoneSpinId)))
							return true;

						if (unit.Entry == CloudbenderKoboId && Me.IsMelee() && (unit.CastingSpellId == CycloneKickId || unit.HasAura(CycloneKickId)))
							return true;

						if (unit.Entry == LittleLiuyangId && Me.IsMelee() && (unit.HasAura("Flame Wall") || unit.CastingSpellId == FlameWallSpellId))
							return true;

						if (unit.Entry == FlamecoaxingSpiritId && _lilLiuyang != null && _lilLiuyang.IsValid && Me.IsMelee() &&
							unit.Location.Distance(_lilLiuyang.Location) < 17)
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
					if (_bosses.Contains(unit.Entry) && Me.IsTank())
						outgoingunits.Add(unit);

					if (unit.Entry == FlamecoaxingSpiritId)
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
					if (unit.Entry == FlamecoaxingSpiritId)
						priority.Score += 5000;

					if (unit.Entry == BatuId)
						priority.Score -= 5000;
				}
			}
		}

		#endregion

		private const uint TigerTempleGongId = 212974;
		private const uint ScarShellId = 63311;
		private const uint JolGrumId = 63312;
		private const int StoneSpinId = 123928;
		private const uint LittleLiuyangId = 63313;
		private const uint FlamecoaxingSpiritId = 63539;
		private const uint ChaganFirehoofId = 63318;
		private const uint BatuId = 63872;
		private const uint CloudbenderKoboId = 63316;
		private const int CycloneKickId = 125579;
		private const int FlameWallSpellId = 123966;
		private const uint MakiWaterbladeId = 64280;
		private const uint SatayByuId = 64281;
		private const int WhirlpoolId = 125564;

		private readonly uint[] _bosses = new[] { ScarShellId, JolGrumId, LittleLiuyangId, ChaganFirehoofId, CloudbenderKoboId, MakiWaterbladeId, SatayByuId };
		private WoWUnit _lilLiuyang;

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0)]
		public Composite RootEncounter()
		{
			return new PrioritySelector();
		}


		[ObjectHandler(212974, "Gong", 200)]
		public Composite GongHandler()
		{
			WoWGameObject gong = null;
			var gongLoc = new WoWPoint(3844.063, 527.6622, 639.6908);
			return
				new PrioritySelector(
					new Decorator(
						ctx =>
							ScriptHelpers.CurrentScenarioInfo.CurrentStage.NumberOfSteps > 1 &&
							(!ScriptHelpers.CurrentScenarioInfo.CurrentStage.GetStep(1).IsComplete ||
							 ScriptHelpers.CurrentScenarioInfo.CurrentStageNumber <= 5 && !ObjectManager.GetObjectsOfType<WoWUnit>().Any(u => _bosses.Contains(u.Entry))),
						new PrioritySelector(
							ctx => gong = ctx as WoWGameObject,
							new Decorator(ctx => !gong.CanUseNow(), new Action(ctx => Navigator.MoveTo(gongLoc))),
							new Decorator(ctx => Me.IsMoving, new Action(ctx => WoWMovement.MoveStop())),
							new Action(ctx => gong.Interact()))));
		}


		[EncounterHandler(63315, "Gurgthock", Mode = CallBehaviorMode.Proximity)]
		public Composite GurgthockEncounter()
		{
			return new PrioritySelector(new Decorator<WoWUnit>(unit => unit.QuestGiverStatus == QuestGiverStatus.Available, ScriptHelpers.CreatePickupQuest(63315)));
		}

		[EncounterHandler(63314, "Wodin the Troll-Servant", Mode = CallBehaviorMode.Proximity)]
		public Composite WodintheTrollServantEncounter()
		{
			return new PrioritySelector(new Decorator<WoWUnit>(unit => unit.QuestGiverStatus == QuestGiverStatus.TurnIn, ScriptHelpers.CreateTurninQuest(63314)));
		}


		[EncounterHandler(63311, "Scar-Shell")]
		public Composite ScarShellEncounter()
		{
			WoWUnit boss = null;
			AddAvoidObject(
				ctx => true,
				12,
				u => u.Entry == ScarShellId && (u.ToUnit().CastingSpellId == StoneSpinId || u.ToUnit().HasAura(StoneSpinId)),
				o =>
				{
					var start = o.Location;
					var end = start.RayCast(WoWMathHelper.NormalizeRadian(o.Rotation), 30);
					return Me.Location.GetNearestPointOnSegment(start, end);
				});

			return new PrioritySelector(ctx => boss = ctx as WoWUnit, new Decorator(ctx => Targeting.Instance.IsEmpty(), new PrioritySelector(new ActionAlwaysSucceed())));
		}

		[EncounterHandler(63312, "Jol'Grum")]
		public Composite JolGrumEncounter()
		{
			WoWUnit boss = null;
			//const int headbuttId = 123931;

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit
				// note: no point in trying to avoid head butt. cast too fast.
				//ScriptHelpers.CreateAvoidUnitAnglesBehavior(ctx => boss.CastingSpellId == headbuttId && boss.IsSafelyFacing(Me) && boss.Distance < 17, ctx => boss, new ScriptHelpers.AngleSpan(0, 150))
				);
		}

		[EncounterHandler(63313, "Little Liuyang")]
		public Composite LittleLiuyangEncounter()
		{
			const int flamelineId = 123959;
			const uint flameWallId = 63534;

			AddAvoidObject(ctx => Me.IsRange() && !Targeting.Instance.IsEmpty() && Targeting.Instance.FirstUnit.Distance <= 40 || !Me.IsMoving, 6, flameWallId);
			AddAvoidObject(
				ctx => Me.IsRange(),
				4,
				u => u.Entry == flamelineId,
				u => Me.Location.GetNearestPointOnSegment(_lilLiuyang.Location, WoWMathHelper.CalculatePointFrom(u.Location, _lilLiuyang.Location, 40)));
			AddAvoidObject(ctx => Me.IsMelee(), 4, flamelineId);
			AddAvoidObject(ctx => true, 17, u => u.Entry == LittleLiuyangId && (u.ToUnit().HasAura("Flame Wall") || u.ToUnit().CastingSpellId == FlameWallSpellId));

			return new PrioritySelector(
				ctx => _lilLiuyang = ctx as WoWUnit,
				// mesh issues make it hard for melee to run out.
				new Decorator(
					ctx => (_lilLiuyang.HasAura("Flame Wall") || _lilLiuyang.CastingSpellId == FlameWallSpellId) && _lilLiuyang.Distance < 17.5,
					new Action(ctx => Navigator.PlayerMover.MoveTowards(WoWMathHelper.CalculatePointFrom(Me.Location, _lilLiuyang.Location, 19)))),
				new Decorator(ctx => Me.IsTank() && Targeting.Instance.IsEmpty(), new ActionAlwaysSucceed()));
		}

		[EncounterHandler(63318, "Chagan Firehoof")]
		public Composite ChaganFirehoofEncounter()
		{
			WoWUnit boss = null;
			const uint trailBlazeUnitId = 63544;
			const int trailBlazeSpellId = 123976;

			AddAvoidObject(
				ctx => true,
				4,
				u => u.Entry == trailBlazeUnitId,
				o => Me.Location.GetNearestPointOnSegment(o.Location, o.Location.RayCast(WoWMathHelper.NormalizeRadian(o.Rotation), 10)));
			AddAvoidObject(ctx => true, 4, trailBlazeSpellId);

			return new PrioritySelector(ctx => boss = ctx as WoWUnit);
		}

		[EncounterHandler(63316, "Cloudbender Kobo")]
		public Composite CloudbenderKoboEncounter()
		{
			WoWUnit boss = null;
			const uint twisterId = 66991;
			const uint stormCloudId = 64308;

			AddAvoidObject(ctx => true, 7, twisterId);
			AddAvoidObject(ctx => true, 4, stormCloudId);
			AddAvoidObject(ctx => true, 15, u => u.Entry == CloudbenderKoboId && u.ToUnit().CastingSpellId == CycloneKickId);
			// kite if low on health..
			AddAvoidObject(ctx => Me.HealthPercent < 30, 15, u => u.Entry == CloudbenderKoboId && u.ToUnit().CurrentTargetGuid == Me.CurrentTargetGuid);

			return new PrioritySelector(ctx => boss = ctx as WoWUnit, new Decorator(ctx => Targeting.Instance.IsEmpty(), new ActionAlwaysSucceed()));
		}

		// tank and spank

		[EncounterHandler(64280, "Maki Waterblade")]
		public Composite MakiWaterbladeEncounter()
		{
			WoWUnit boss = null;
			//AddAvoidObject(ctx => true, 10, u => u.Entry == MakiWaterbladeId && u.ToUnit().HasAura("Whirlpool"));
			return new PrioritySelector(ctx => boss = ctx as WoWUnit);
		}

		// tank and spank
		[EncounterHandler(64281, "Satay Byu")]
		public Composite SatayByuEncounter()
		{
			WoWUnit boss = null;
			//AddAvoidObject(ctx => true, 10, u => u.Entry == MakiWaterbladeId && u.ToUnit().HasAura("Whirlpool"));
			return new PrioritySelector(ctx => boss = ctx as WoWUnit);
		}
	}
}