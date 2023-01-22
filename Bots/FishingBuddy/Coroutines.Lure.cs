using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot.Coroutines;
using Styx.WoWInternals;
using Styx.WoWInternals.DB;
using Styx.WoWInternals.WoWObjects;

namespace Bots.FishingBuddy
{
	static partial class Coroutines
	{
		private readonly static WaitTimer LureRecastTimer = WaitTimer.TenSeconds;


		private static readonly List<Lure> Lures = new List<Lure>
													{
														new Lure(6529, "Shiny Bauble", 25),
														new Lure(6530, "Nightcrawlers", 50),
														new Lure(6532, "Bright Baubles", 75),
														new Lure(6811, "Aquadynamic Fish Lens", 50),
														new Lure(6533, "Aquadynamic Fish Attractor", 100),
														new Lure(7307, "Flesh Eating Worm", 75),
														new Lure(34861, "Sharpened Fish Hook", 100),
														new Lure(46006, "Glow Worm", 100),
														new Lure(62673, "Feathered Lure", 100),
														new Lure(67404, "Glass Fishing Bobber", 15),
														new Lure(68049, "Heat-Treated Spinning Lure", 150),
														new Lure(118391, "Worm Supreme", 200),
													};

		// does nothing if no lures are in bag
		public async static Task<bool> ApplyLure()
		{
			if (FishingBuddySettings.Instance.Poolfishing )
				return false;

			if (StyxWoW.Me.IsCasting || HasLureOnPole)
				return false;

			if (!LureRecastTimer.IsFinished)
				return false;
			
			LureRecastTimer.Reset();
			var mainHand = StyxWoW.Me.Inventory.Equipped.MainHand;

			if (mainHand == null || mainHand.ItemInfo.WeaponClass != WoWItemWeaponClass.FishingPole)
				return false;

			// use any item with a lure effect
			WoWItem wearableItem = GetWearableItemWithLureEffect();
			if (wearableItem != null)
			{
				FishingBuddyBot.Log("Appling lure from {0} to fishing pole", wearableItem.SafeName);
				wearableItem.Use();
				await CommonCoroutines.SleepForLagDuration();
				return true;
			}



			var bestLure = (from item in StyxWoW.Me.BagItems
				let lure = Lures.FirstOrDefault(l => l.ItemId == item.Entry)
				where lure != null
				orderby lure.BonusSkill descending
				select item).FirstOrDefault();

			if (bestLure == null || !bestLure.Use())
				return false;

			FishingBuddyBot.Log("Appling {0} to fishing pole", bestLure.SafeName);
			await CommonCoroutines.SleepForLagDuration();
			return true;
		}

		private static WoWItem GetWearableItemWithLureEffect()
		{
			var item = StyxWoW.Me.Inventory.Equipped.MainHand;

			if (item != null && item.ItemInfo.WeaponClass == WoWItemWeaponClass.FishingPole && HasUsableEffect(item))
				return item;

			item = StyxWoW.Me.Inventory.Equipped.Head;

			if (item != null && Utility.FishingHatIds.Contains(item.Entry) && HasUsableEffect(item))
				return item;

			return null;
		}

		private static bool HasUsableEffect(WoWItem item)
        {
            return false;
			/*
			return item.Effects != null 
				&& item.Effects.Any(e => e.TriggerType == ItemEffectTriggerType.OnUse && e.Spell != null && !e.Spell.Cooldown);
			*/
		}

		public static bool HasLureOnPole
		{
			get
			{
				var ret = Lua.GetReturnValues("return GetWeaponEnchantInfo()");
				return ret != null && ret.Count > 0 && ret[0] == "1";
			}
		}

		private class Lure
		{
			public Lure(int itemId, string name, int bonusSkill)
			{
				ItemId = itemId;
				Name = name;
				BonusSkill = bonusSkill;
			}

			public int ItemId { get; private set; }
			public string Name { get; private set; }
			public int BonusSkill { get; private set; }
		}

	}
}
