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
using System.Collections.Generic;
using System.Linq;

using Styx.Common.Helpers;
using Styx.WoWInternals.WoWObjects;

#endregion


namespace Honorbuddy.QuestBehaviorCore
{
	// NB: The HBcore's blacklist will preserve 'blacklist state' between profile calls to this behavior.
	// The 'blacklist state' will also be preserved if Honorbuddy is stop/startedby the user.
	// We don't want either of these to happen--we want the blacklist state to be pristine between profile
	// calls to this behavior, or if Honorbuddy is stop/started.  Thus, we roll our own blacklist
	// which will be disposed along with the behavior.
	public class LocalBlacklist
	{
		public LocalBlacklist()
			: this(TimeSpan.FromSeconds(30))
		{
		}

		public LocalBlacklist(TimeSpan maxSweepTime)
		{
			_sweepTimer = new WaitTimer(maxSweepTime) { WaitTime = maxSweepTime };
		}

		private readonly Dictionary<ulong, DateTime> _blackList = new Dictionary<ulong, DateTime>();
		private readonly WaitTimer _sweepTimer = null;


		public void Add(ulong guid, TimeSpan timeSpan)
		{
			RemoveExpired();
			_blackList[guid] = DateTime.Now.Add(timeSpan);
		}


		public void Add(WoWObject wowObject, TimeSpan timeSpan)
		{
			if (wowObject != null)
				{ Add(wowObject.Guid, timeSpan); }
		}


		public bool Contains(ulong guid)
		{
			DateTime expiry;
			if (_blackList.TryGetValue(guid, out expiry))
				{ return (expiry > DateTime.Now); }

			return false;
		}


		public bool Contains(WoWObject wowObject)
		{
			return (wowObject != null) && Contains(wowObject.Guid);
		}


		public void RemoveExpired()
		{
			if (_sweepTimer.IsFinished)
			{
				DateTime now = DateTime.Now;

				List<ulong> expiredEntries = (from key in _blackList.Keys
												where (_blackList[key] < now)
												select key).ToList();

				foreach (ulong entry in expiredEntries)
					{ _blackList.Remove(entry); }

				_sweepTimer.Reset();
			}
		}
	}
}