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
// DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_WaitTimer
//
// WaitTimer by Nesox
// Simple behavior that forces hb to wait until the timer runs out.
// ##Syntax##
// QuestId[Optional]:
// QuestCompleteRequirement[Optional, Default:NotComplete]:
// QuestInLogRequirement[Optional, Default:InLog]:
// WaitTime: time (in milliseconds) to wait. eg; 15000 for 15 seconds.
// VariantTime[Optional]: a random amount of time between [0..VariantTime] will be selected and added to the WaitTime
//
#endregion


#region Examples
// Wait for 30 seconds or until Quest 12345 completes...
//      <CustomBehavior File="WaitTimer" QuestId="12345" WaitTime="30000" />
//
// Wait between 3 to 8 seconds...
//      <CustomBehavior File="WaitTimer" WaitTime="3000" VariantTime="5000" />
//
// Wait for 30 seconds, or until toon has aura 54321...
//     <CustomBehavior File="WaitTimer" WaitTime="30000" TerminateWhen="Me.HasAura(54321)" />
#endregion


#region Usings

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
#endregion


namespace Honorbuddy.Quest_Behaviors.WaitTimerBehavior // This prevents a conflict with the new Styx.Common.Helpers.WaitTimer
{
	[CustomBehaviorFileName(@"WaitTimer")]
	public class WaitTimer : QuestBehaviorBase
	{
		public WaitTimer(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// NB: Core attributes are parsed by QuestBehaviorBase parent (e.g., QuestId, NonCompeteDistance, etc)

				// Behavior-specific attributes...
				StatusText = GetAttributeAs<string>("GoalText", false, ConstrainAs.StringNonEmpty, null)
					?? "Wait time remaining... {TimeRemaining} of {TimeDuration}.";
				VariantTime = GetAttributeAsNullable<int>("VariantTime", false, ConstrainAs.Milliseconds, null) ?? 0;
				WaitTime = GetAttributeAsNullable<int>("WaitTime", true, ConstrainAs.Milliseconds, null) ?? 1000;
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
		private string StatusText { get; set; }
		private int WaitTime { get; set; }
		private int VariantTime { get; set; }


		protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
		{
			//// EXAMPLE: 
			//UsageCheck_DeprecatedAttribute(xElement,
			//    Args.Keys.Contains("Nav"),
			//    "Nav",
			//    context => string.Format("Automatically converted Nav=\"{0}\" attribute into MovementBy=\"{1}\"."
			//                              + "  Please update profile to use MovementBy, instead.",
			//                              Args["Nav"], MovementBy));
		}

		protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
		{
			//// EXAMPLE:
			//UsageCheck_SemanticCoherency(xElement,
			//    (!MobIds.Any() && !FactionIds.Any()),
			//    context => "You must specify one or more MobIdN, one or more FactionIdN, or both.");
			//
			//const double rangeEpsilon = 3.0;
			//UsageCheck_SemanticCoherency(xElement,
			//    ((RangeMax - RangeMin) < rangeEpsilon),
			//    context => string.Format("Range({0}) must be at least {1} greater than MinRange({2}).",
			//                  RangeMax, rangeEpsilon, RangeMin)); 
		}

		#region Private and Convenience variables
		private Styx.Common.Helpers.WaitTimer _timer;
		private string _waitTimeAsString;
		#endregion


		#region Overrides of CustomForcedBehavior
		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }


		protected override Composite CreateMainBehavior()
		{
			return new ActionRunCoroutine(ctx => MainCoroutine());
		}


		public override void OnFinished()
		{
			// Defend against being called multiple times (just in case)...
			if (!IsOnFinishedRun)
			{
				// QuestBehaviorBase.OnFinished() will set IsOnFinishedRun...
				base.OnFinished();
			}
		}


		public override void OnStart()
		{
			// Let QuestBehaviorBase do basic initializaion of the behavior, deal with bad or deprecated attributes,
			// capture configuration state, install BT hooks, etc.  This will also update the goal text.
			var isBehaviorShouldRun = OnStart_QuestBehaviorCore();

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (isBehaviorShouldRun)
			{
				int waitDuration = WaitTime + (new Random(Environment.TickCount + WaitTime + VariantTime)).Next(VariantTime);

				_timer = new Styx.Common.Helpers.WaitTimer(TimeSpan.FromMilliseconds(waitDuration));
				_waitTimeAsString = Utility.PrettyTime(_timer.WaitTime);

				_timer.Reset();

				this.UpdateGoalText(QuestId, "Waiting for " + _waitTimeAsString);
			}
		}
		#endregion


		#region Main Behaviors
		private async Task<bool> MainCoroutine()
		{
			// If timer is finished, we're done...
			if ((_timer == null) || _timer.IsFinished)
			{
				BehaviorDone();
				return false;
			}

			// Update the status text...
			await Coroutine.Sleep(250);  // throttle updates
			TreeRoot.StatusText = UtilSubstituteInMessage(StatusText);
			return true;
		}
		#endregion


		#region Helpers
		private string UtilSubstituteInMessage(string message)
		{
			if (!string.IsNullOrEmpty(message))
			{
				message = message.Replace("{TimeRemaining}", Utility.PrettyTime(_timer.TimeLeft));
				message = message.Replace("{TimeDuration}", _waitTimeAsString);
			}

			return (message ?? "");
		}
		#endregion



	}
}
