using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Styx;
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot.Coroutines;
using Styx.WoWInternals.WoWObjects;

namespace Bots.FishingBuddy
{
	internal static partial class Coroutines
	{
	    private const uint CaptainsRumseysLager_ItemId = 34832;

		public async static Task<bool> Consume()
		{
            // There's only one known consumable and its beneficial buff is applied instantly. 
            var item = Me.BagItems.FirstOrDefault(i => i.Entry == CaptainsRumseysLager_ItemId);
		    if (item == null || Me.HasAura("Captain Rumsey's Lager"))
		        return false;
		    item.Use();
		    await CommonCoroutines.SleepForLagDuration();
			return true;
		}

	}
}
