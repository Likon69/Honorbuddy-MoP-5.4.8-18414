// Behavior originally contributed by Unknown.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//
// TODO:
// * Great idea here, don't want to lose it...
//    http://www.thebuddyforum.com/honorbuddy-forum/developer-forum/25863-quest-behavior-castspellon-help.html#post281838
//    (We need to build a very small timer into this behavior).
//

#region Summary and Documentation
// DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_ForceSetVendor
//
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;

using Honorbuddy.QuestBehaviorCore;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
#endregion


namespace Honorbuddy.Quest_Behaviors.ForceSetVendor
{
	[CustomBehaviorFileName(@"ForceSetVendor")]
	public class ForceSetVendor : CustomForcedBehavior
	{
		public enum VendorType
		{
			Mail,
			Repair,
			Sell,
			Train,
		}

		/// <summary>
		/// Behavior for forcing train/mail/vendor/repair
		/// Example usage: <CustomBehavior QuestId="14324" File="ForceSetVendor" VendorType="Train" />
		/// QuestId is optional, if you don't use it make sure you put this tag inside an 'If'
		/// </summary> 
		public ForceSetVendor(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// Deprecation warnings...
				if (args.ContainsKey("VendorType"))
				{
					QBCLog.Warning("The VendorType attribute has been deprecated.\n"
								+ "Please replace it with DoMail/DoRepair/DoSell/DoTrain='true'");
				}

				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				DoMail = GetAttributeAsNullable<bool>("DoMail", false, null, null) ?? false;
				DoRepair = GetAttributeAsNullable<bool>("DoRepair", false, null, null) ?? false;
				DoSell = GetAttributeAsNullable<bool>("DoSell", false, null, null) ?? false;
				DoTrain = GetAttributeAsNullable<bool>("DoTrain", false, null, null) ?? false;
				QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
				QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;

				// "VendorType" attribute is required if no Do* attribute is specified
				VendorType? type = GetAttributeAsNullable<VendorType>("VendorType", !(DoMail || DoRepair || DoSell || DoTrain), null, null);
				if (type.HasValue)
				{
					switch (type.Value)
					{
						case VendorType.Mail:
							DoMail = true;
							break;
						case VendorType.Repair:
							DoRepair = true;
							break;
						case VendorType.Sell:
							DoSell = true;
							break;
						case VendorType.Train:
							DoTrain = true;
							break;
						default:
							IsAttributeProblem = true;
							throw (new NotImplementedException("Unexpected VendorType"));
					}
				}
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
		public bool DoMail { get; private set; }
		public bool DoRepair { get; private set; }
		public bool DoSell { get; private set; }
		public bool DoTrain { get; private set; }
		public int QuestId { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }

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
				CharacterSettings.Instance.FindVendorsAutomatically = true;
				Vendors.ForceMail |= DoMail;
				Vendors.ForceRepair |= DoRepair;
				Vendors.ForceSell |= DoSell;
				Vendors.ForceTrainer |= DoTrain;

				List<string> reasons = new List<string>();

				if (DoMail)
					{ reasons.Add("Mail"); }
				if (DoRepair)
					{ reasons.Add("Repair"); }
				if (DoSell)
					{ reasons.Add("Sell"); }
				if (DoTrain)
					{ reasons.Add("Train"); }

				this.UpdateGoalText(QuestId, "Scheduled run for " + string.Join(", ", reasons.ToArray()));

				// When IsDone returns true after OnStart call, the bot won't be ticked and ForcedBehaviorExecutor will advance to the next node.
				// Therefore we are not setting _isBehaviorDone here to let the bot tick at least once, so VendorBehavior can pick up our changes.
				//_isBehaviorDone = true;
			}
		}

		public override void OnTick()
		{
			_isBehaviorDone = true;
		}

		#endregion
	}
}
