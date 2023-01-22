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

#region Summary and Documentation
// Stops the Quest Bot.  Will write 'Msg' to the log and Goal Text.
// Also write the line number it halted at for easily locating in profile.
// 
// Useful for testing assumptions in quest profile and during profile
// development to force profile to automatically stop at designated point
// 
// ##Syntax##
// [optional] QuestId: Id of the quest (default is 0)
// [optional] Msg: text value to display (default says stopped by profile)
// [optional] Color: color to use for message in log (default is red)
// [optional] CloseWoW: closes WoW and exits Honorbuddy with an exit code of 12 which signals relogers to stop logging in (default is false)
// 
// Note:  QuestId behaves the same as on every other behavior.  If 0, then
// halt always occurs.  Otherwise, for non-zero QuestId only halts if the
// character has the quest and its not completed
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using Honorbuddy.QuestBehaviorCore;
using Honorbuddy.Quest_Behaviors.WaitTimerBehavior;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;

#endregion


namespace Honorbuddy.Quest_Behaviors.Halt
{
	[CustomBehaviorFileName(@"Halt")]
	public class Halt : CustomForcedBehavior
	{
		public Halt(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
				QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;

				Color = GetAttributeAsNullable<Color>("Color", false, null, null) ?? Color.Red;
				Message = GetAttributeAs<string>("Message", false, ConstrainAs.StringNonEmpty, new[] { "Msg", "Text" }) ?? "Quest Profile HALT";
				CloseWoW = GetAttributeAsNullable<bool>("CloseWoW", false, null, null) ?? false;
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
		public Color Color { get; private set; }
		public string Message { get; private set; }
		public bool CloseWoW { get; private set; }
		public int QuestId { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }

		#region Overrides of CustomForcedBehavior

		protected override Composite CreateBehavior()
		{
			return (null);
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
				return (!UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
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
				QBCLog.DeveloperInfo("\n\n    " + Message + "\n");

				this.UpdateGoalText(QuestId, Message);
				if (CloseWoW)
				{
					QBCLog.Info("Bot shutdown requested by Halt quest behavior.");
					TreeRoot.Shutdown(12, true);
				}
				else
				{
					TreeRoot.Stop("Bot stop requested by Halt quest behavior.");
				}
			}
		}

		#endregion
	}
}

