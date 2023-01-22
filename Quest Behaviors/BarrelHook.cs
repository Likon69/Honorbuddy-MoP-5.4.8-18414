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

using Honorbuddy.QuestBehaviorCore;
using Styx.CommonBot.Profiles;
#endregion



namespace Honorbuddy.Quest_Behaviors.BarrelHook
{
	[CustomBehaviorFileName(@"BarrelHook")]
	[Obsolete(@"Use Hooks\BarrelHook instead")]
	public class BarrelHook : Hooks.BarrelHook
	{
		public BarrelHook(Dictionary<string, string> args) : base(args)
		{
			QBCLog.BehaviorLoggingContext = this;
		}
	}
}
