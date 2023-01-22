using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Styx;
using Styx.Common.Helpers;
using Styx.WoWInternals.WoWObjects;

namespace Bots.FishingBuddy
{
	internal static partial class Coroutines
	{
		private readonly static WaitTimer BaitRecastTimer = WaitTimer.TenSeconds;

		private static readonly Dictionary<uint, string> BaitDictionary = new Dictionary<uint, string>
																   {
																	   {110293, "Abyssal Gulper Eel Bait"},
																	   {110294, "Blackwater Whiptail Bait"},
																	   {110290, "Blind Lake Sturgeon Bait"},
																	   {110289, "Fat Sleeper Bait"},
																	   {110291, "Fire Ammonite Bait"},
																	   {110274, "Jawless Skulker Bait"},
																	   {110292, "Sea Scorpion Bait"},
																	   {116755, "Nat's Hookshot Bait"},
                                                                       {128229, "Felmouth Frenzy Bait"},
																   };

		public async static Task<bool> ApplyBait()
		{
			if (FishingBuddySettings.Instance.Poolfishing || !FishingBuddySettings.Instance.UseBait)
				return false;

			if (!BaitRecastTimer.IsFinished)
				return false;

			if (StyxWoW.Me.IsCasting || GotBaitOnPole)
				return false;

			BaitRecastTimer.Reset();

			var bait = Baits.FirstOrDefault();
			if (bait == null)
				return false;

			FishingBuddyBot.Log("Attaching bait: {0} to pole", bait.SafeName);
			bait.Use();
			await Coroutine.Wait(2000, () => GotBaitOnPole);
			return true;
		}


	    public static IEnumerable<WoWItem> Baits
	    {
	        get
	        {
                // Order list so user-prefered baits are at the beginning. 
	            return StyxWoW.Me.BagItems.Where(i => BaitDictionary.ContainsKey(i.Entry))
	                .OrderBy(i => i.Entry != FishingBuddySettings.Instance.UseBaitPreference);
	        }
	    }

	    public static bool GotBaitOnPole
		{
			get
			{
				return StyxWoW.Me.GetAllAuras().Any(a => BaitDictionary.ContainsValue(a.Name));
			}
		}

	}
}
