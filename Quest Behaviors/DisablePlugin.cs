// Behavior originally contributed by AxaZol.
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
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_RunMacro
//
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;

using Honorbuddy.QuestBehaviorCore;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
#endregion


namespace Honorbuddy.Quest_Behaviors.DisablePlugin
{
	[CustomBehaviorFileName(@"DisablePlugin")]
	public class DisablePlugins : CustomForcedBehavior
	{
		public DisablePlugins(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				Names = GetAttributeAsArray<string>("Names", true, null, null, ",".ToCharArray());
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



		private string[] Names;

		// Private variables for internal state
		private bool _isBehaviorDone;

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
				foreach(var name in Names)
				{
					this.UpdateGoalText(0, "Disabling plugins: " + string.Join(", ", Names));

					var firstOrDefault = Styx.Plugins.PluginManager.Plugins.FirstOrDefault(x => x.Name == name);
					if (firstOrDefault != null)
						firstOrDefault.Enabled = false;
				}

				_isBehaviorDone = true;
			}
		}

		#endregion
	}
}