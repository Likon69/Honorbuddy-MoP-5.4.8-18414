//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//

#region Summary and Documentation
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;

using Bots.Grind;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.SealingTheWay
{
	[CustomBehaviorFileName(@"SpecificQuests\26501-Deepholm-SealingTheWay")]
	public class SealingTheWay : CustomForcedBehavior
	{
		public SealingTheWay(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				QuestId = 26501;//GetAttributeAsNullable<int>("QuestId",false, ConstrainAs.QuestId(this), null) ?? 0;
				QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = QuestInLogRequirement.InLog;
				MobIds = new uint[] { 50635, 50638, 50643, 50636 };
			}

			catch (Exception except)
			{
				// Maintenance problems occur for a number of reasons.  The primary two are...
				// * Changes were made to the behavior, and boundary conditions weren't properly tested.
				// * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
				// In any case, we pinpoint the source of the problem area here, and hopefully it
				// can be quickly resolved.
				QBCLog.Exception(except);
				IsAttributeProblem = true;
			}
		}


		// Attributes provided by caller
		private uint[] MobIds { get; set; }
		private int QuestId { get; set; }
		private QuestCompleteRequirement QuestRequirementComplete { get; set; }
		private QuestInLogRequirement QuestRequirementInLog { get; set; }


		// Private variables for internal state
		private bool _isBehaviorDone;
		private bool IsOnFinishedRun { get; set; }
		private Composite _root;


		// Private properties
		private LocalPlayer Me
		{
			get { return (StyxWoW.Me); }
		}


		private Composite DoneYet
		{
			get
			{
				return new Decorator(ret => Me.IsQuestComplete(QuestId),
					new Action(delegate
					{
						TreeRoot.StatusText = "Finished!";
						_isBehaviorDone = true;
						return RunStatus.Success;
					}));
			}
		}


		private WoWUnit Geomancer(WoWPoint loc)
		{
			return
				ObjectManager.GetObjectsOfType<WoWUnit>()
				.Where(u =>
					(u.Entry == 43170)
					&& u.IsAlive
					&& (u.Location.Distance(loc) <= 5))
				.OrderBy(u => u.DistanceSqr)
				.FirstOrDefault();
		}


		private WoWUnit Bad(WoWPoint loc)
		{
			return
				ObjectManager.GetObjectsOfType<WoWUnit>()
				.Where(u =>
					u.IsAlive
					&& !u.IsPlayer
					&& (u.CurrentTarget != null)
					&& (u.CurrentTarget == Geomancer(loc) || u.CurrentTarget == Me))
				.OrderBy(u => u.DistanceSqr)
				.FirstOrDefault();
		}


		private Composite DoDps
		{
			get
			{
				return
					new PrioritySelector(
						new Decorator(ret => RoutineManager.Current.CombatBehavior != null, RoutineManager.Current.CombatBehavior),
						new Action(c => RoutineManager.Current.Combat()));
			}
		}

		private Composite DoPull
		{
			get
			{
				return
					new PrioritySelector(
						new Decorator(ret => RoutineManager.Current.PullBehavior != null, RoutineManager.Current.PullBehavior),
						new Action(c => RoutineManager.Current.Pull()));
			}
		}


		private WoWPoint[] Spots = new WoWPoint[]
		{
			new WoWPoint(411.33,1659.2,348.8838),
			new WoWPoint(420.792,1718.1,349.4922),
			new WoWPoint(457.47,1727.42,348.5146),
			new WoWPoint(491.014,1659.59,348.2862)
		};


		private Composite Part1()
		{
			return new Decorator(r => !Me.IsQuestObjectiveComplete(QuestId, 1),
			new PrioritySelector(
				new Decorator(r => Me.CurrentTarget != null && Me.CurrentTarget.IsDead,
					new Action(r => Me.ClearTarget())),
				new Decorator(r => Geomancer(Spots[0]) != null && Geomancer(Spots[0]).Distance > 10,
					new Action(r => Flightor.MoveTo(Geomancer(Spots[0]).Location))),
				new Decorator(r => (Me.CurrentTarget == null || (Me.CurrentTarget != null && Me.CurrentTarget.IsFriendly)) && Bad(Spots[0]) != null,
					new Action(r => Bad(Spots[0]).Target())),
				new Decorator(r => (Me.CurrentTarget == null || (Me.CurrentTarget != null && Me.CurrentTarget.IsFriendly)) && (Geomancer(Spots[0]).CurrentTarget != null),
					new Action(r => Geomancer(Spots[0]).CurrentTarget.Target())),
				new Decorator(r => !Me.Combat && Bad(Spots[0]) != null, DoPull),
				new Decorator(r => Me.Combat && Bad(Spots[0]) != null && !Me.CurrentTarget.IsFriendly, DoDps),
					new Decorator(r => Bad(Spots[0]) == null,
						UseItem(0))));
		}


		private Composite Part2()
		{
			return new Decorator(r => Me.IsQuestObjectiveComplete(QuestId, 1) && !Me.IsQuestObjectiveComplete(QuestId, 2),
			new PrioritySelector(
				new Decorator(r => Me.CurrentTarget != null && Me.CurrentTarget.IsDead,
					new Action(r => Me.ClearTarget())),
				new Decorator(r => Geomancer(Spots[1]) != null && Geomancer(Spots[1]).Distance > 10,
					new Action(r => Flightor.MoveTo(Geomancer(Spots[1]).Location))),
				new Decorator(r => (Me.CurrentTarget == null || (Me.CurrentTarget != null && Me.CurrentTarget.IsFriendly)) && Bad(Spots[1]) != null,
					new Action(r => Bad(Spots[1]).Target())),
				new Decorator(r => (Me.CurrentTarget == null || (Me.CurrentTarget != null && Me.CurrentTarget.IsFriendly)) && (Geomancer(Spots[1]).CurrentTarget != null),
					new Action(r => Geomancer(Spots[1]).CurrentTarget.Target())),
				new Decorator(r => !Me.Combat && Bad(Spots[1]) != null, DoPull),
				new Decorator(r => Me.Combat && Bad(Spots[1]) != null && !Me.CurrentTarget.IsFriendly, DoDps),
					new Decorator(r => Bad(Spots[1]) == null,
						UseItem(1))));
		}


		private Composite Part3()
		{
			return new Decorator(r => Me.IsQuestObjectiveComplete(QuestId, 1) && Me.IsQuestObjectiveComplete(QuestId, 2) && !Me.IsQuestObjectiveComplete(QuestId, 3),
			new PrioritySelector(
				new Decorator(r => Me.CurrentTarget != null && Me.CurrentTarget.IsDead,
					new Action(r => Me.ClearTarget())),
				new Decorator(r => Geomancer(Spots[2]) != null && Geomancer(Spots[2]).Distance > 10,
					new Action(r => Flightor.MoveTo(Geomancer(Spots[2]).Location))),
				new Decorator(r => (Me.CurrentTarget == null || (Me.CurrentTarget != null && Me.CurrentTarget.IsFriendly)) && Bad(Spots[2]) != null,
					new Action(r => Bad(Spots[2]).Target())),
				new Decorator(r => (Me.CurrentTarget == null || (Me.CurrentTarget != null && Me.CurrentTarget.IsFriendly)) && (Geomancer(Spots[2]).CurrentTarget != null),
					new Action(r => Geomancer(Spots[2]).CurrentTarget.Target())),
				new Decorator(r => !Me.Combat && Bad(Spots[2]) != null, DoPull),
				new Decorator(r => Me.Combat && Bad(Spots[2]) != null && !Me.CurrentTarget.IsFriendly, DoDps),
					new Decorator(r => Bad(Spots[2]) == null,
						UseItem(2))));
		}


		private Composite Part4()
		{
			return new Decorator(r => Me.IsQuestObjectiveComplete(QuestId, 1) && Me.IsQuestObjectiveComplete(QuestId, 2) && Me.IsQuestObjectiveComplete(QuestId, 3) && !Me.IsQuestObjectiveComplete(QuestId, 4),
			new PrioritySelector(
				new Decorator(r => Me.CurrentTarget != null && Me.CurrentTarget.IsDead,
					new Action(r => Me.ClearTarget())),
				new Decorator(r => Geomancer(Spots[3]) != null && Geomancer(Spots[3]).Distance > 10,
					new Action(r => Flightor.MoveTo(Geomancer(Spots[3]).Location))),
				new Decorator(r => (Me.CurrentTarget == null || (Me.CurrentTarget != null && Me.CurrentTarget.IsFriendly)) && Bad(Spots[3]) != null,
					new Action(r => Bad(Spots[3]).Target())),
				new Decorator(r => (Me.CurrentTarget == null || (Me.CurrentTarget != null && Me.CurrentTarget.IsFriendly)) && (Geomancer(Spots[3]).CurrentTarget != null),
					new Action(r => Geomancer(Spots[3]).CurrentTarget.Target())),
				new Decorator(r => !Me.Combat && Bad(Spots[3]) != null, DoPull),
				new Decorator(r => Me.Combat && Bad(Spots[3]) != null && !Me.CurrentTarget.IsFriendly, DoDps),
					new Decorator(r => Bad(Spots[3]) == null,
						UseItem(3))));
		}


		private WoWItem Rock
		{
			get { return Me.BagItems.FirstOrDefault(x => x.Entry == 58885); }
		}


		private Composite UseItem(int x)
		{

			return new Action(delegate
			{
				var g = Geomancer(Spots[x]);
				if (g.Distance > 5)
					Navigator.MoveTo(g.Location);
				g.Target();
				Rock.Use();
			});

		}


		private Composite CreateBehavior_QuestbotMain()
		{
			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone,
				new PrioritySelector(
					DoneYet,
					LevelBot.CreateCombatBehavior(),
					Part1(),
					Part2(),
					Part3(),
					Part4()
			)));
		}


		#region Overrides of CustomForcedBehavior

		public override bool IsDone
		{
			get
			{
				return (_isBehaviorDone // normal completion
						|| !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
			}
		}


		public override void OnFinished()
		{
			// Defend against being called multiple times (just in case)...
			if (IsOnFinishedRun)
				{ return; }

			// Clean up resources...
			TreeHooks.Instance.RemoveHook("Questbot_Main", CreateBehavior_QuestbotMain());

			TreeRoot.GoalText = string.Empty;
			TreeRoot.StatusText = string.Empty;

			// QuestBehaviorBase.OnFinished() will set IsOnFinishedRun...
			base.OnFinished();
			IsOnFinishedRun = true;
		}


		public override void OnStart()
		{
			// This reports problems, and stops BT processing if there was a problem with attributes...
			// We had to defer this action, as the 'profile line number' is not available during the element's
			// constructor call.
			OnStart_HandleAttributeProblem();

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (!IsDone)
			{
				TreeHooks.Instance.InsertHook("Questbot_Main", 0, CreateBehavior_QuestbotMain());

				this.UpdateGoalText(QuestId);
			}
		}
		#endregion
	}
}