// Behavior originally contributed by Bobby53.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//
// WIKI DOCUMENTATION:
//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_AbandonQuest
//

#region Summary and Documentation
// QUICK DOX:
//      Allows you to abandon a quest in your quest log.
//
//  Parameters (required, then optional--both listed alphabetically):
//      QuestId: The id of the quest.
//
//      Type[Default:Incomplete]: The state in which the quest must reside for the abandon
//              to succeed.  The allowed values for this attribute are:
//                  All:        abandon quest if its in log regardless of status
//                  Failed:     abandon quest only if failed
//                  Incomplete: abandon incomplete quests (failed and any not complete)  
//
//      WaitTime [optional; Default: 1500ms]
//          Defines the number of milliseconds to wait after the quest abandon before carrying on.
//          This allows the WoWclient to update its state (and HBcore to 'catch up' to it),
//          before proceeding with the rest of the profile.
//
//  Examples:   
//     <CustomBehavior File="AbandonQuest" QuestId="25499" />
//     <CustomBehavior File="AbandonQuest" QuestId="25499" Type="All" />
//     <CustomBehavior File="AbandonQuest" QuestId="25499" Type="Failed" />
//     <CustomBehavior File="AbandonQuest" QuestId="25499" Type="Incomplete" />
//
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;

using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.AbandonQuest
{
	[CustomBehaviorFileName(@"AbandonQuest")]
	public class AbandonQuest : CustomForcedBehavior
	{
		public enum AbandonType
		{
			All,
			Failed,
			Incomplete
		};


		public AbandonQuest(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				QuestId = GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null) ?? 0;
				Type = GetAttributeAsNullable<AbandonType>("Type", false, null, null) ?? AbandonType.Incomplete;
				WaitTime = GetAttributeAsNullable<int>("WaitTime", false, ConstrainAs.Milliseconds, null) ?? 1500;   
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
		private int QuestId { get; set; }
		private AbandonType Type { get; set; }
		private int WaitTime { get; set; }

		// Private variables for internal state
		private bool _isBehaviorDone;
		private Composite _root;
		private readonly WaitTimer _waitTimerAfterAbandon = new WaitTimer(TimeSpan.Zero);

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }

		#region Overrides of CustomForcedBehavior

		protected override Composite CreateBehavior()
		{
			return (_root ?? (_root = new PrioritySelector(
				// Delay, if necessary...
				new Decorator(context => !_waitTimerAfterAbandon.IsFinished,
					new Action(context =>
					{
						TreeRoot.StatusText = string.Format("Completing {0} wait of {1}",
							Utility.PrettyTime(TimeSpan.FromSeconds((int)_waitTimerAfterAbandon.TimeLeft.TotalSeconds)),
							Utility.PrettyTime(_waitTimerAfterAbandon.WaitTime));
					})),

				new Action(context => { _isBehaviorDone = true; })
			)));
		}

		public override bool IsDone
		{
			get
			{
				return (_isBehaviorDone);
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
				PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

				if (quest == null)
				{
					QBCLog.Warning("Cannot find quest with QuestId({0}).", QuestId);
					_isBehaviorDone = true;
				}

				else if (quest.IsCompleted && (Type != AbandonType.All))
				{
					QBCLog.Warning("Quest({0}, \"{1}\") is Complete--skipping abandon.", QuestId, quest.Name);
					_isBehaviorDone = true;
				}

				else if (!quest.IsFailed && (Type == AbandonType.Failed))
				{
					QBCLog.Warning("Quest({0}, \"{1}\") has not Failed--skipping abandon.", QuestId, quest.Name);
					_isBehaviorDone = true;
				}

				else
				{
					TreeRoot.GoalText = string.Format("Abandoning QuestId({0}): \"{1}\"", QuestId, quest.Name);
					StyxWoW.Me.QuestLog.AbandonQuestById((uint)QuestId);
					QBCLog.Info("Quest({0}, \"{1}\") successfully abandoned", QuestId, quest.Name);

					_waitTimerAfterAbandon.WaitTime = TimeSpan.FromMilliseconds(WaitTime);
					_waitTimerAfterAbandon.Reset();
				}
			}
		}

	    public override void OnFinished()
	    {
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
	        base.OnFinished();
	    }

	    #endregion
	}
}