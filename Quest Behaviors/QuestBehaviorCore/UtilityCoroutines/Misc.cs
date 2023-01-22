using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using Styx.CommonBot.Coroutines;
using Action = System.Action;

namespace Honorbuddy.QuestBehaviorCore
{
	public partial class UtilityCoroutine
	{
		/// <summary>
		///     <para>
		///         This coroutine waits MAXWAITTIMEINMILLISECONDSDELEGATE for item ITEMIDDELEGATE to arrive in our bags.
		///         If the item doesn't arrived within the specified time, the behavior is terminated.
		///     </para>
		/// </summary>
		public class WaitForInventoryItem : CoroutineTask<bool>
		{
			// BT contruction-time properties...
			public WaitForInventoryItem(
				Func<int> itemIdDelegate,
				Action actionOnExpiredTimerDelegate,
				Func<int> maxWaitTimeInMillisecondsDelegate = null)
			{
				Contract.Requires(itemIdDelegate != null, context => "itemIdDelegate != null");
				Contract.Requires(actionOnExpiredTimerDelegate != null, context => "actionOnExpiredTimerDelegate != null");

				ItemIdDelegate = itemIdDelegate;
				ActionOnExpiredTimerDelegate = actionOnExpiredTimerDelegate;
				MaxWaitTimeInMillisecondsDelegate = maxWaitTimeInMillisecondsDelegate ?? (() => 5000);
			}

			private Action ActionOnExpiredTimerDelegate { get; set; }
			private Func<int> ItemIdDelegate { get; set; }
			private Func<int> MaxWaitTimeInMillisecondsDelegate { get; set; }

			// BT visit-time properties...
			private WoWItem CachedWoWItem { get; set; }
			private WaitTimer WatchdogTimer_WaitForItemArrival { get; set; }

			public override async Task<bool> Run()
			{
				var itemId = ItemIdDelegate();
				CachedWoWItem = Me.CarriedItems.FirstOrDefault(i => (i.Entry == itemId));

				if (Query.IsViable(CachedWoWItem))
				{
					// Squelch the timer, so it will be available if the item disappears from our bags...
					WatchdogTimer_WaitForItemArrival = null;
					return false;
				}

				// If timer is not spinning, create & start it...
				if (WatchdogTimer_WaitForItemArrival == null)
				{
					WatchdogTimer_WaitForItemArrival = new WaitTimer(TimeSpan.FromMilliseconds(MaxWaitTimeInMillisecondsDelegate()));
					WatchdogTimer_WaitForItemArrival.Reset();
				}

				// If timer completes, time to call it quits...
				if (WatchdogTimer_WaitForItemArrival.IsFinished)
				{
					QBCLog.ProfileError(
						"{0} has not arrived in our bag within {1}.",
						Utility.GetItemNameFromId(itemId),
						Utility.PrettyTime(WatchdogTimer_WaitForItemArrival.WaitTime));
					ActionOnExpiredTimerDelegate();
				}
				else
				{
					TreeRoot.StatusText = String.Format(
						"Waiting {0} for {1} to arrive in our bags.",
						Utility.PrettyTime(WatchdogTimer_WaitForItemArrival.WaitTime),
						Utility.GetItemNameFromId(itemId));
				}

				return true;
			}
		}
	}
}