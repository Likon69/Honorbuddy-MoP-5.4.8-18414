// Behavior originally contributed by AknA.
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
// This is an variation of WaitTimer done by Nesox.
// InstanceTimer is a Quest Behavior developed to prevent that you get "You've entered too many instances".
// When you enter a instance you start the timer.
// When you have done your instance run you check the timer to see how long you have been in the instance.
// Calculated from that InstanceTimer will create a WaitTimer from that.
// 
// To start the timer use :
// <CustomBehavior File="Misc\InstanceTimer" Timer="Start" />
//
// To check how long you have been in instance and create a wait timer use:
// <CustomBehavior File="Misc\InstanceTimer" Timer="Check" />
// 
// The default wait time is 12min 30sec - the time you spent in instance.
// If you want to alter the wait time use :
// <CustomBehavior File="Misc\InstanceTimer" Timer="Check" WaitTime="10000" />
// WaitTime is in milliseconds and in above case is 10 seconds - the time you spent in instance.
//
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Xml.Linq;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Patchables;
using Styx.TreeSharp;

#endregion


namespace Styx.Bot.Quest_Behaviors {
	[CustomBehaviorFileName(@"Misc\InstanceTimer")]
	public class InstanceTimer : QuestBehaviorBase
	{
		public InstanceTimer(Dictionary<string, string> args)
		: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				GoalText = GetAttributeAs("GoalText", false, ConstrainAs.StringNonEmpty, null) ?? "Waiting for {TimeRemaining}  of  {TimeDuration}";
			    Timer = GetAttributeAsNullable<TimerCommand>("Timer", true, null, null) ?? TimerCommand.Start;
                WaitTime = GetAttributeAsNullable("WaitTime", false, ConstrainAs.Milliseconds, null) ?? 750000;
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
        private string GoalText { get; set; }
        private int WaitTime { get; set; }
		private TimerCommand Timer { get;  set; }

		// Private variables for internal state
		private Composite _root;
		private Common.Helpers.WaitTimer _waitTimer;
		private string _waitTimeAsString;
	    private readonly static Stopwatch InInstanceTimer = new Stopwatch();

		private string UtilSubstituteInMessage(string message)
		{
		    message = message.Replace("{TimeRemaining}", Utility.PrettyTime(_waitTimer.TimeLeft));
			message = message.Replace("{TimeDuration}", _waitTimeAsString);

			return (message);
		}

		#region Overrides of CustomForcedBehavior

		protected override Composite CreateMainBehavior() 
        {
            return _root ?? (_root = new ActionRunCoroutine(ctx => MainCoroutine()));
		}

        protected async Task<bool> MainCoroutine()
        {
            if (_waitTimer == null || _waitTimer.IsFinished)
            {
                BehaviorDone();
                return false;
            }

            TreeRoot.GoalText = (!string.IsNullOrEmpty(GoalText)
                ? UtilSubstituteInMessage(GoalText)
                : "Waiting for timer expiration");

            TreeRoot.StatusText = "Wait time remaining... "
                                  + Utility.PrettyTime(_waitTimer.TimeLeft)
                                  + "... of "
                                  + _waitTimeAsString;
            return true;
        }

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
            UsageCheck_SemanticCoherency(xElement, Timer == TimerCommand.Check && !InInstanceTimer.IsRunning,
                context => "You must start the timer (by setting Timer=\"Start\") first before checking the timer.");
        }

		public override void OnStart() 
		{
            var isBehaviorShouldRun = OnStart_QuestBehaviorCore();

            if (isBehaviorShouldRun)
			{
				if (Timer == TimerCommand.Start)
				{
				    _waitTimer = null;
                    InInstanceTimer.Restart();
					QBCLog.Info("Started.");
                    BehaviorDone();
				}
                else if (Timer == TimerCommand.Check)
				{
                    InInstanceTimer.Stop();
                    QBCLog.Info("Your instance run took " + Utility.PrettyTime(InInstanceTimer.Elapsed));
				    if (InInstanceTimer.ElapsedMilliseconds >= WaitTime)
				    {
				        _waitTimer = null;
                        BehaviorDone();
				    }
                    else
					{
					    var waitTimeSpan = TimeSpan.FromMilliseconds(WaitTime) - InInstanceTimer.Elapsed;
                        _waitTimer = new Common.Helpers.WaitTimer(waitTimeSpan);
                        _waitTimeAsString = Utility.PrettyTime(_waitTimer.WaitTime);
                        _waitTimer.Reset();
                        QBCLog.Info("Waiting for " + _waitTimeAsString);
					}                    
				}
			}
		}

	    #endregion

	    enum TimerCommand
	    {
	        Start,
            Check
	    }
	}
}
