// Originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

#region Usings

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bots.Grind;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.CommonBot.Coroutines;
using Action = Styx.TreeSharp.Action;

#endregion

namespace Honorbuddy.QuestBehaviorCore
{
	public partial class UtilityCoroutine
	{
		public class WarnIfBagsFull : CoroutineTask<bool>
		{
			private ThrottleCoroutineTask _throttle;

			public override async Task<bool> Run()
			{
				if (LootTargeting.LootMobs && Me.FreeBagSlots <= 0)
					await (_throttle ?? (_throttle = new ThrottleCoroutineTask(TimeSpan.FromMinutes(10), LogWarning)));
				return false;
			}

			private async Task LogWarning()
			{
				QBCLog.Error("Honorbuddy may not be looting because your bags are full.");
			}
		}
	}
}