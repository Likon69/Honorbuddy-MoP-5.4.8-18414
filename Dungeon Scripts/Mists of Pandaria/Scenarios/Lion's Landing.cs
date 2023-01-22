

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
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
	public class LionsLanding : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 590; }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (unit != null)
					{

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
					var currentTarget = unit.CurrentTarget;
					if (currentTarget != null)
					{
						if (currentTarget.Entry == DagginWindbeardId || unit.Entry == AdmiralTaylorId || unit.Entry == HighMarshalTwinbraidId)
							outgoingunits.Add(unit);
					}
				}
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

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0)]
		public Composite RootBehavior()
		{
			const uint tankTargetId = 59566;
			AddAvoidObject(ctx => true, 6, tankTargetId);
			AddAvoidObject(ctx => true, 4, HighExplosiveBombId);
			return new PrioritySelector();
		}

		const uint DagginWindbeardId = 68581;

		[ScenarioStage(1, "Stage One")]
		public Composite StageOneEncounter()
		{
			WoWUnit dagginWindbeard = null;
			return new PrioritySelector(
				ctx => dagginWindbeard = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == DagginWindbeardId),
				new Decorator(ctx => Me.IsTank() && Targeting.Instance.IsEmpty() && dagginWindbeard != null, ScriptHelpers.CreateTalkToNpc(ctx => dagginWindbeard)));
		}

		const uint AdmiralTaylorId = 68685;

		[ScenarioStage(2, "Stage Two")]
		public Composite StageTwoEncounter()
		{
			WoWUnit admiralTaylor = null;
			var stageLoc = new WoWPoint(-1104.568, -1087.226, 5.143423);

			return new PrioritySelector(
				ctx => admiralTaylor = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == AdmiralTaylorId),
				new Decorator(
					ctx => Targeting.Instance.IsEmpty() && (admiralTaylor == null || admiralTaylor.Distance > 5), new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(stageLoc))),
				new Decorator(
					ctx => Me.IsTank() && Targeting.Instance.IsEmpty() && admiralTaylor != null && admiralTaylor.Distance <= 5,
					ScriptHelpers.CreateTalkToNpc(ctx => admiralTaylor)));
		}

		private const uint HighExplosiveBombId = 67523;
		[ScenarioStage(3, "Stage Three")]
		public Composite StageThreeEncounter()
		{
			var eastLoc = new WoWPoint(-1037.897, -1131.91, 14.96357);
			var westLoc = new WoWPoint(-1018.294, -1090.855, 11.36404);
			var southLoc = new WoWPoint(-1068.06, -1104.521, 11.31481);

			ScenarioStage stage = null;
			return new PrioritySelector(
				ctx => stage = ctx as ScenarioStage,
				// south attackers
				new Decorator(ctx => Targeting.Instance.IsEmpty() && !stage.GetStep(2).IsComplete, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(southLoc))),
				// west attackers
				new Decorator(
					ctx => Targeting.Instance.IsEmpty() && stage.GetStep(2).IsComplete && !stage.GetStep(1).IsComplete, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(eastLoc))),
				// south attackers
				new Decorator(
					ctx => Targeting.Instance.IsEmpty() && stage.GetStep(2).IsComplete && stage.GetStep(1).IsComplete && !stage.GetStep(3).IsComplete,
					new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(westLoc))));
		}

		const uint HighMarshalTwinbraidId = 68851;
		[ScenarioStage(4, "Stage Four")]
		public Composite StageFourEncounter()
		{
			WoWUnit highMarshalTwinbraid = null;

			var stageLoc = new WoWPoint(-956.0608, -1071.24, 12.46541);

			ScenarioStage stage = null;
			return new PrioritySelector(
				ctx =>
				{
					highMarshalTwinbraid = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == HighMarshalTwinbraidId);
					return stage = ctx as ScenarioStage;
				},
				new Decorator(
					ctx => Targeting.Instance.IsEmpty() && (highMarshalTwinbraid == null || highMarshalTwinbraid.Distance > 5), new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(stageLoc))),
				new Decorator(
					ctx => Me.IsTank() && Targeting.Instance.IsEmpty() && highMarshalTwinbraid != null && highMarshalTwinbraid.Distance <= 5 && !stage.GetStep(1).IsComplete,
					ScriptHelpers.CreateTalkToNpc(ctx => highMarshalTwinbraid)));
		}


		[ScenarioStage(5, "Stage Five")]
		public Composite StageFiveEncounter()
		{
			const uint amberKearnenId = 68871;
			const uint sullyThePickleMcLearyId = 68883;
			const uint mishkaId = 68870;

			var step1Loc = new WoWPoint(-971.1353, -1132.188, 30.82936);
			var step3Loc = new WoWPoint(-1043.577, -1151.649, 14.99451);
			var step2Loc = new WoWPoint(-1113.838, -1154.368, 13.35851);

			ScenarioStage stage = null;
			return new PrioritySelector(
				ctx => stage = ctx as ScenarioStage,
				new Decorator(
					ctx => Targeting.Instance.IsEmpty() && !stage.GetStep(1).IsComplete,
					new PrioritySelector(
						new Decorator(ctx => step1Loc.Distance(Me.Location) > 5, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(step1Loc))),
						new Decorator(ctx => step1Loc.Distance(Me.Location) <= 5, ScriptHelpers.CreateTalkToNpc(amberKearnenId)))),
				new Decorator(
					ctx => Targeting.Instance.IsEmpty() && stage.GetStep(1).IsComplete && !stage.GetStep(3).IsComplete,
					new PrioritySelector(
						new Decorator(ctx => step3Loc.Distance(Me.Location) > 5, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(step3Loc))),
						new Decorator(ctx => step3Loc.Distance(Me.Location) <= 5, ScriptHelpers.CreateTalkToNpc(sullyThePickleMcLearyId)))),
				new Decorator(
					ctx => Targeting.Instance.IsEmpty() && stage.GetStep(1).IsComplete && stage.GetStep(3).IsComplete && !stage.GetStep(2).IsComplete,
					new PrioritySelector(
						new Decorator(ctx => step2Loc.Distance(Me.Location) > 5, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(step2Loc))),
						new Decorator(ctx => step2Loc.Distance(Me.Location) <= 5, ScriptHelpers.CreateTalkToNpc(mishkaId)))));
		}

		[ScenarioStage(6, "Stage Six")]
		public Composite StageSixEncounter()
		{
			const uint placeRocketsHereId = 68886;
			const uint placeBoomsticksHereId = 68885;
			const uint placeBombsHereId = 68884;

			var stageLoc = new WoWPoint(-979.4883, -1059.25, 12.7329);

			ScenarioStage stage = null;
			return new PrioritySelector(
				ctx => stage = ctx as ScenarioStage,
				new Decorator(ctx => stageLoc.Distance(Me.Location) > 20, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(stageLoc))),
				new Decorator(
					ctx => stageLoc.Distance(Me.Location) <= 20,
					new PrioritySelector(
						new Decorator(ctx => !stage.GetStep(1).IsComplete, ScriptHelpers.CreateTalkToNpc(placeBombsHereId)),
						new Decorator(ctx => stage.GetStep(1).IsComplete && !stage.GetStep(2).IsComplete, ScriptHelpers.CreateTalkToNpc(placeBoomsticksHereId)),
						new Decorator(
							ctx => stage.GetStep(1).IsComplete && stage.GetStep(2).IsComplete && !stage.GetStep(3).IsComplete, ScriptHelpers.CreateTalkToNpc(placeRocketsHereId)))));
		}


		[ScenarioStage(7, "Stage Seven")]
		public Composite StageSevenEncounter()
		{
			const uint reaverBombsId = 216725;
			WoWGameObject bombs = null;
			var stageLoc = new WoWPoint(-1909.725, 2384.62, 6.49155);

			return new PrioritySelector(
				ctx => bombs = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(g => g.Entry == reaverBombsId),
				new Decorator(ctx => bombs != null && !Lua.GetReturnVal<bool>("return ExtraActionButton1:IsVisible()", 0), ScriptHelpers.CreateInteractWithObject(ctx => bombs)),
				new Decorator(ctx => !Targeting.Instance.IsEmpty() && Targeting.Instance.FirstUnit.Distance <= 30 && !Lua.GetReturnVal<bool>("return ExtraActionButton1.cooldown:IsVisible()", 0),
					new Action(ctx =>
					{
						Lua.DoString("ExtraActionButton1:Click()");
						SpellManager.ClickRemoteLocation(Targeting.Instance.FirstUnit.Location);
					})));
		}

		const uint ThaumaturgeSaresseId = 67692;

		[EncounterHandler(67692, "Thaumaturge Saresse")]
		public Composite ThaumaturgeSaresseEncounter()
		{
			WoWUnit boss = null;
			const int reforgedBody = 135529;
			const int reforgedMind = 135532;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateInterruptCast(ctx => boss, reforgedBody, reforgedMind)
				);
		}

	}
}