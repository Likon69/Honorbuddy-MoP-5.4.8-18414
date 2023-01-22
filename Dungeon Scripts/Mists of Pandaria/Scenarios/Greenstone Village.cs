

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Frames;
using Styx.CommonBot.POI;
using Styx.CommonBot.Routines;
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
	public class GreenstoneVillage : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 492; }
		}

		public override void OnEnter() { }

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (unit != null) { }
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
				if (unit != null) { }
			}
		}

		#endregion

		private readonly CircularQueue<WoWPoint> _stageThreeBarrelPickupPath = new CircularQueue<WoWPoint>
		{
			new WoWPoint(1999.786, -1809.391, 196.2217),
			new WoWPoint(2103.951, -1891.53, 215.4706),
			new WoWPoint(2108.24, -2015.222, 223.0112),
			new WoWPoint(1879.935, -2026.1, 211.0177)
		};

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0)]
		public Composite RootEncounter()
		{
			return new PrioritySelector();
		}

		[ScenarioStage(1, "Rescue the Villages")]
		public Composite CreateStageOneBehavior()
		{
			ScenarioStage stage = null;
			var laAndLiupoLoc = new WoWPoint(1956.958, -1974.793, 210.9048);
			var portlyShungLoc = new WoWPoint(1948.438, -1946.309, 207.2751);
			var mayorLinLoc = new WoWPoint(1985.447, -1918.776, 203.9203);
			var swanLoc = new WoWPoint(2023.34, -1913.529, 207.2751);
			var scribeRinjiLoc = new WoWPoint(2015.751, -1882.88, 207.6642);
			var meilaLoc = new WoWPoint(2050.594, -1975.653, 215.5079);

			return new Decorator(
				ctx => Targeting.Instance.IsEmpty(),
				new PrioritySelector(
					ctx => stage = ScriptHelpers.CurrentScenarioInfo.CurrentStage,
					new Decorator(ctx => !stage.GetStep(1).IsComplete && BotPoi.Current.Type == PoiType.None, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(laAndLiupoLoc))),
					new Decorator(ctx => !stage.GetStep(4).IsComplete && BotPoi.Current.Type == PoiType.None, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(portlyShungLoc))),
					new Decorator(ctx => !stage.GetStep(2).IsComplete && BotPoi.Current.Type == PoiType.None, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(mayorLinLoc))),
					new Decorator(ctx => !stage.GetStep(6).IsComplete && BotPoi.Current.Type == PoiType.None, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(swanLoc))),
					new Decorator(ctx => !stage.GetStep(5).IsComplete && BotPoi.Current.Type == PoiType.None, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(scribeRinjiLoc))),
					new Decorator(ctx => !stage.GetStep(3).IsComplete && BotPoi.Current.Type == PoiType.None, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(meilaLoc)))));
		}

		[ScenarioStage(2)]
		public Composite CreateStageTwoBehavior()
		{
			var tzuLoc = new WoWPoint(1937.594, -1865.617, 203.3163);
			return new Decorator(ctx => Targeting.Instance.IsEmpty(), new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(tzuLoc)));
		}

		[ScenarioStage(3)]
		public Composite CreateStageThreeBehavior()
		{
			WoWUnit barrel = null;
			const uint burgledBarrelId = 62682;
			var barrelTurninLoc = new WoWPoint(1942.195, -1870.816, 203.68);
			return
				new PrioritySelector(
					ctx =>
					barrel = (from obj in ObjectManager.GetObjectsOfType<WoWUnit>()
							  where obj.Entry == burgledBarrelId && Me.RaidMembers.All(r => r.TransportGuid != obj.Guid)
							  let myLoc = Me.Location
							  let objLoc = obj.Location
							  where !Me.PartyMembers.Any(p => !p.IsMe && p.IsSafelyFacing(obj, 45) && p.Location.DistanceSqr(objLoc) < myLoc.DistanceSqr(objLoc))
							  && Navigator.CanNavigateFully(myLoc, objLoc)
							  orderby obj.Distance
							  select obj).FirstOrDefault(),

					new Decorator(
						ctx => Me.TransportGuid > 0 && Me.Transport.Entry == burgledBarrelId,
						new Action(ctx => Navigator.GetRunStatusFromMoveResult(Navigator.MoveTo(barrelTurninLoc)))),
					new Decorator(
						ctx => Me.TransportGuid == 0 || Me.Transport.Entry != burgledBarrelId,
						new PrioritySelector(
							new Decorator(
								ctx => barrel != null && Targeting.Instance.IsEmpty(),
								new PrioritySelector(
									new Decorator(ctx => barrel.Distance >= 40, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(barrel.Location))),
									new Decorator(
										ctx => barrel.Distance < 40 && !ScriptHelpers.WillPullAggroAtLocation(barrel.Location),
										new PrioritySelector(
											new Decorator(
												ctx => !barrel.WithinInteractRange, new Action(ctx => Navigator.GetRunStatusFromMoveResult(Navigator.MoveTo(barrel.Location)))),
											new Decorator(
												ctx => barrel.WithinInteractRange,
												new PrioritySelector(
													new Decorator(ctx => Me.Shapeshift != ShapeshiftForm.Normal, new AlwaysFailAction<object>(ctx => Lua.DoString("CancelShapeshiftForm()"))),
													new Decorator(ctx => Me.Mounted, new ActionRunCoroutine(ctx => CommonCoroutines.Dismount())),
													new Action(ctx => barrel.Interact()))))))),
							new Decorator(
								ctx => barrel == null && Me.IsTank() && Targeting.Instance.IsEmpty(),
								new PrioritySelector(
									new Decorator(
										ctx => _stageThreeBarrelPickupPath.Peek().DistanceSqr(Me.Location) < 6 * 6, new Action(ctx => _stageThreeBarrelPickupPath.Dequeue())),
									new Action(ctx => Navigator.GetRunStatusFromMoveResult(Navigator.MoveTo(_stageThreeBarrelPickupPath.Peek()))))))));
		}

		[ScenarioStage(4)]
		public Composite CreateStageFourBehavior()
		{
			var stageLoc = new WoWPoint(2192.587, -1897.972, 219.044);
			const uint kiriJadeEyesId = 62990;
			const uint stonecutterLonId = 62989;
			const uint barrelChestHuoId = 62988;
			WoWUnit rescueTarget = null;

			return new PrioritySelector(
				ctx =>
				{
					ScenarioStage stage = ScriptHelpers.CurrentScenarioInfo.CurrentStage;
					rescueTarget = (from unit in ObjectManager.GetObjectsOfType<WoWUnit>()
									where
										(unit.Entry == kiriJadeEyesId && !stage.GetStep(1).IsComplete || unit.Entry == barrelChestHuoId && !stage.GetStep(2).IsComplete ||
										 unit.Entry == stonecutterLonId && !stage.GetStep(3).IsComplete) && unit.Distance < 40
									orderby unit.DistanceSqr
									select unit).FirstOrDefault();
					return ctx;
				},
				new Decorator(
					ctx => rescueTarget != null && !Me.Combat && !ScriptHelpers.WillPullAggroAtLocation(rescueTarget.Location),
					new PrioritySelector(
						new Decorator(
							ctx => !rescueTarget.WithinInteractRange, new Action(ctx => Navigator.GetRunStatusFromMoveResult(Navigator.MoveTo(rescueTarget.Location)))),
						new Decorator(
							ctx => rescueTarget.WithinInteractRange,
							new PrioritySelector(
								new Decorator(ctx => Me.Mounted, new ActionRunCoroutine(ctx => CommonCoroutines.Dismount())),
								 new Decorator(ctx => Me.Shapeshift != ShapeshiftForm.Normal, new AlwaysFailAction<object>(ctx => Lua.DoString("CancelShapeshiftForm()"))),
								 new Action(ctx => rescueTarget.Interact()))))),
				new Decorator(
					ctx => Targeting.Instance.TargetList.Any(u => u.Distance < 10),
					new Action(
						ctx =>
						{
							Lua.DoString("ExtraActionButton1:Click()");
							return RunStatus.Failure;
						})),
				ScriptHelpers.CreateClearArea(() => stageLoc, 70),
				new Decorator(ctx => Me.IsTank() && Me.Location.Distance(stageLoc) > 20 && Targeting.Instance.IsEmpty(), new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(stageLoc))));
		}

		[ScenarioStage(5)]
		public Composite CreateStageFiveBehavior()
		{
			var lastBossLoc = new WoWPoint(2228.099, -1883.991, 223.0198);
			return new Decorator(
				ctx => Targeting.Instance.IsEmpty(),
				new PrioritySelector(
					new Decorator(
						ctx => Me.IsTank() && Me.Location.Distance(lastBossLoc) < 10 && Targeting.Instance.IsEmpty(),
						new PrioritySelector(new Decorator(ctx => !Me.Combat, RoutineManager.Current.RestBehavior), new ActionAlwaysSucceed())),
					new Decorator(ctx => Targeting.Instance.IsEmpty(), new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(lastBossLoc)))));
		}

		[EncounterHandler(66772, "Beast of Jade")]
		public Composite BeastofJadeEncounter()
		{
			WoWUnit boss = null;
			const int jadeStatueId = 119364;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit, ScriptHelpers.CreateInterruptCast(ctx => boss, jadeStatueId));
		}

		[EncounterHandler(61156, "Vengeful Hui")]
		public Composite VengefulHuiEncounter()
		{
			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				new Decorator(
					ctx => Targeting.Instance.TargetList.Any(u => u.Distance < 10),
					new Action(
						ctx =>
						{
							Lua.DoString("ExtraActionButton1:Click()");
							return RunStatus.Failure;
						})));
		}
	}
}