// Behavior originally contributed by Natfoth.
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
// Allows you to Interact with Mobs that are Nearby.
// ##Syntax##
// QuestId: Id of the quest.
// NpcId: Id of the Mob to interact with.
// X,Y,Z: The general location where theese objects can be found
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Database;
using Styx.CommonBot.Frames;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.ForceSetVendor
{
	[CustomBehaviorFileName(@"ForceTrainRiding")]
	public class ForceTrainRiding : CustomForcedBehavior
	{
		public ForceTrainRiding(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				MobId = GetAttributeAsNullable<int>("MobId", true, ConstrainAs.MobId, new[] { "NpcId", "NpcID" }) ?? 0;
				QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
				QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;
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
		public int MobId { get; private set; }
		public int QuestId { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }

		// Private variables for internal state
		private bool _isBehaviorDone;
		private Composite _root;

		// Private properties
		public int Counter { get; set; }
		private List<WoWUnit> MobList
		{
			get
			{
				return (ObjectManager.GetObjectsOfType<WoWUnit>()
									 .Where(u => u.Entry == MobId && !u.IsDead)
									 .OrderBy(u => u.Distance).ToList());
			}
		}
		private NpcResult RidingTrainer { get { return (NpcQueries.GetNpcById((uint)MobId)); } }

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }

		#region Overrides of CustomForcedBehavior.

		protected override Composite CreateBehavior()
		{
			return _root ?? (_root =
				new PrioritySelector(

					new Decorator(ret => Counter >= 1,
						new Action(ret => _isBehaviorDone = true)),

						new PrioritySelector(

							new Decorator(ret => Counter > 0,
								new Sequence(

									new Action(ret => TreeRoot.StatusText = "Finished!"),
									new Action(ret => _isBehaviorDone = true),
									new WaitContinue(1,
										new Action(delegate
										{
											_isBehaviorDone = true;
											return RunStatus.Success;
										}))
									)
								),

							new Decorator(ret => MobList.Count > 0 && !MobList[0].WithinInteractRange,
								new Action(ret => Navigator.MoveTo(MobList[0].Location))),

							new Decorator(ret => MobList.Count > 0 && MobList[0].WithinInteractRange,
								new Sequence(
									new DecoratorContinue(ret => StyxWoW.Me.IsMoving,
                                        new ActionRunCoroutine(ctx => CommonCoroutines.StopMoving())),					
                                    new Action(ret => TreeRoot.StatusText = "Opening Trainer - " + MobList[0].SafeName + " X: " + MobList[0].X + " Y: " + MobList[0].Y + " Z: " + MobList[0].Z),
									new Action(ret => MobList[0].Interact()),
									new WaitContinue(5,
										ret => TrainerFrame.Instance.IsVisible,
										new Action(ret => TrainerFrame.Instance.BuyAll())),
									new Action(ret => TrainerFrame.Instance.Close()),
									new Action(ret => Counter++)
									)
							),

							new Decorator(ret => RidingTrainer != null,
								new Action(ret => Navigator.MoveTo(RidingTrainer.Location))
								),

							new Action(ret => Counter++)
					)));
		}

        public override void OnFinished()
        {
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
        }

		public override bool IsDone
		{
			get
			{
				return (_isBehaviorDone     // normal completion
						|| !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
			}
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
				this.UpdateGoalText(QuestId);
			}
		}

		#endregion
	}
}

