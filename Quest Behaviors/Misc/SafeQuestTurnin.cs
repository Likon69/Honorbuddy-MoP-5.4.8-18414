// Behavior originally contributed by Nesox.
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
// Removes all units from targeting while running.
// ##Syntax##
// QuestId: Id of the quest, duh!
// QuestName: Name of the quest.
// TurnInName: Name of the npc to turn in quest at.
// TurnInId: id of the npc to turn in the quest at.
// X: X axis of this npc
// Y: Y axis of this npc
// Z: Z axis of this npc
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;

using Bots.Quest.QuestOrder;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
#endregion


namespace Honorbuddy.Quest_Behaviors.SafeQuestTurnin
{

	[CustomBehaviorFileName(@"Misc\SafeQuestTurnin")]
	public class SafeQuestTurnin : CustomForcedBehavior
	{
		public SafeQuestTurnin(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				QuestId = GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null) ?? 0;
				QuestName = GetAttributeAs<string>("QuestName", true, ConstrainAs.StringNonEmpty, null) ?? string.Empty;
				QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;
				TurnInId = GetAttributeAsNullable<int>("TurnInId", true, ConstrainAs.MobId, null) ?? 0;
				TurnInName = GetAttributeAs<string>("TurnInName", true, ConstrainAs.StringNonEmpty, null) ?? string.Empty;
				TurnInLocation = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
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
		public int QuestId { get; private set; }
		public string QuestName { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }
		public int TurnInId { get; private set; }
		public string TurnInName { get; private set; }
		public WoWPoint TurnInLocation { get; private set; }

		// Private properties
		private ForcedQuestTurnIn QuestTurnIn { get; set; }

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }

		private static void Instance_RemoveTargetsFilter(List<WoWObject> units)
		{
			units.Clear();
		}


		#region Overrides of CustomForcedBehavior

		protected override Composite CreateBehavior() { return QuestTurnIn.Branch; }

        public override void OnFinished()
        {
            Targeting.Instance.RemoveTargetsFilter -= Instance_RemoveTargetsFilter;
            if (QuestTurnIn != null)
            {
                QuestTurnIn.OnFinished();
                QuestTurnIn = null;
            }
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
        }

		public override bool IsDone
		{
			get
			{
				return (((QuestTurnIn != null) && QuestTurnIn.IsDone)     // normal completion
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

				QuestTurnIn = new ForcedQuestTurnIn((uint)QuestId, QuestName, (uint)TurnInId, TurnInName, TurnInLocation);

				if (QuestTurnIn == null)
					{ QBCLog.Fatal("Unable to complete {0}", this.GetType().Name); }

				Targeting.Instance.RemoveTargetsFilter += Instance_RemoveTargetsFilter;
				QuestTurnIn.OnStart();
			}
		}
		#endregion
	}
}
