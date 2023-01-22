// Behavior originally contributed by Caytchen.
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
// DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_Message
//
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Drawing;

using Honorbuddy.QuestBehaviorCore;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
#endregion


namespace Honorbuddy.Quest_Behaviors.Message
{
	[CustomBehaviorFileName(@"Message")]
	public class Message : CustomForcedBehavior
	{
		public Message(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				ColorLog = Color.FromKnownColor(GetAttributeAsNullable<KnownColor>("LogColor", false, null, null) ?? KnownColor.DarkGray);
				QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
				QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;
				Text = GetAttributeAs<string>("Text", true, ConstrainAs.StringNonEmpty, null) ?? "";
				UpdateGoalText = GetAttributeAsNullable<bool>("UpdateGoalText", false, null, null)
									?? (GetAttributeAsNullable<KnownColor>("GoalColor", false, null, null) != null);
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
		public Color ColorLog { get; private set; }
		public int QuestId { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }
		public string Text { get; private set; }
		public bool UpdateGoalText { get; private set; }

		// Private variables for internal state
		private bool _isBehaviorDone;

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }


		#region Overrides of CustomForcedBehavior

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
				System.Windows.Media.Color newColor = System.Windows.Media.Color.FromArgb(ColorLog.A, ColorLog.R, ColorLog.G, ColorLog.B);

				Text = Text
					.Replace(@"\n", System.Environment.NewLine)
					.Replace(@"\t", "\t");

				Logging.Write(newColor, "[Profile Message]: " + Text);

				if (UpdateGoalText)
				{ TreeRoot.GoalText = Text; }

				_isBehaviorDone = true;
			}
		}

		#endregion
	}
}
