// Behavior originally contributed by mastahg.
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


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.ResonatingBlow
{
	[CustomBehaviorFileName(@"SpecificQuests\26659-Deepholm-ResonatingBlow")]
	public class Steping : CustomForcedBehavior
	{

		public Steping(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				//Location = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ??WoWPoint.Empty;
				QuestId = GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null) ?? 0;
				MobIds = GetAttributeAsNullable<int>("MobId", true, ConstrainAs.MobId, null) ?? 0;
				QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = QuestInLogRequirement.InLog;
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
		private int MobIds { get; set; }
		private int QuestId { get; set; }
		private QuestCompleteRequirement QuestRequirementComplete { get; set; }
		private QuestInLogRequirement QuestRequirementInLog { get; set; }

		// Private variables for internal state
		private bool _isBehaviorDone;
		private bool IsOnFinishedRun { get; set; }
		private ulong _lastguid;
		private Composite _root;

		private LocalPlayer Me { get { return (StyxWoW.Me); } }


		private List<WoWUnit> Stage1
		{
			get
			{//40794 40803 31146
				return
					(ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == 45191 && u.WithinInteractRange && u != CurrentStone
						&& u.Z > Me.Z).OrderByDescending(u => u.Distance).ToList());
			}
		}


		private WoWUnit CurrentStone
		{
			get { return (ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == 45191 && u.GetAllAuras().FirstOrDefault(x => x.CreatorGuid == Me.Guid && x.Name == "Ride Vehicle") != null)); }
		}


		private List<WoWUnit> Good
		{
			get
			{   //40794 40803 31146
				return
					ObjectManager.GetObjectsOfType<WoWUnit>()
					.Where(u => 
						u.IsValid
						&& (u.Entry == 45191)
						&& u.WithinInteractRange
						&& u != CurrentStone
						&& u.Guid != _lastguid)
					.OrderBy(u => u.Location.DistanceSqr(Dragon.Location))
					.ToList();
			}
		}


		private WoWUnit Dragon
		{
			get
			{
				return 
					ObjectManager.GetObjectsOfType<WoWUnit>()
					.FirstOrDefault(u => u.IsValid 
										&& (u.Entry == MobIds));
			}
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


		private Composite DoDps
		{
			get
			{
				return
					new Sequence(new Action(ret => Navigator.PlayerMover.MoveStop()),
					new PrioritySelector(
						new Decorator(ret => RoutineManager.Current.CombatBehavior != null, RoutineManager.Current.CombatBehavior),
						new Action(c => RoutineManager.Current.Combat())));
			}
		}


		private Composite MoveCloser
		{
			get
			{
				return new Action(delegate
				{
					if (Stage1.Count > 0)
					{
						var target = Stage1[0];

						target.Interact();
						try
						{
							_lastguid = CurrentStone.Guid;
						}
						catch (NullReferenceException e)
						{
							_lastguid = 0;
							QBCLog.Error(e.ToString());
						}
					}
				});
			}
		}


		private Composite NewStone
		{
			get
			{
				return new Action(delegate
				{

					if (Good.Count > 0)
					{
						Good[0].Interact();
						_lastguid = CurrentStone.Guid;
					}
				});
			}
		}


		private Composite DragonMethod
		{
			get
			{
				return new PrioritySelector(new Decorator(ret => !Me.HasAura("Searing Breath"), DoDps),
											new WaitContinue(4, ret => Me.HasAura("Searing Breath"), NewStone));
			}
		}


		private Composite Stepping
		{
			get
			{
				return new Decorator(ret => Me.Z < 254, MoveCloser);
			}
		}


		private Composite CreateBehavior_QuestbotMain()
		{
			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, Stepping, DragonMethod)));
		}


		#region Overrides of CustomForcedBehavior

		public override bool IsDone
		{
			get
			{
				return (_isBehaviorDone     // normal completion
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