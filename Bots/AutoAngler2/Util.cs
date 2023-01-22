using System;
using System.Diagnostics;
using System.Linq;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.Helpers;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace HighVoltz.AutoAngler
{
    static class Utils
    {
	    public static readonly Random Rnd = new Random();

        public static bool IsLureOnPole
        {
            get
            {
                bool useHatLure = false;

                var head = StyxWoW.Me.Inventory.GetItemBySlot((uint)WoWEquipSlot.Head);
                if (head != null && (head.Entry == 88710 || head.Entry == 33820))
                    useHatLure = true;

                var lure = StyxWoW.Me.BagItems.FirstOrDefault(r => r.Entry == 85973);
				if (AutoAnglerBot.Instance.MySettings.Poolfishing && lure != null && !StyxWoW.Me.HasAura(125167))
                {
                    return false;
                }

                //if poolfishing, dont need lure say we have one
				if (AutoAnglerBot.Instance.MySettings.Poolfishing && !useHatLure && !AutoAnglerBot.FishAtHotspot)
                    return true;

                var ret = Lua.GetReturnValues("return GetWeaponEnchantInfo()");
                return ret != null && ret.Count > 0 && ret[0] == "1";
            }
        }

	    static TimeCachedValue<uint> _wowPing;
        /// <summary>
        /// Returns WoW's ping, refreshed every 30 seconds.
        /// </summary>
        public static uint WoWPing
        {
            get
            {
	            return _wowPing ??
						(_wowPing = new TimeCachedValue<uint>(TimeSpan.FromSeconds(30), () => Lua.GetReturnVal<uint>("return GetNetStats()", 3)));
            }
        }

        public static bool IsItemInBag(uint entry)
        {
			return StyxWoW.Me.BagItems.Any(i => i.Entry == entry);
        }

        public static WoWItem GetIteminBag(uint entry)
        {
            return StyxWoW.Me.BagItems.FirstOrDefault(i => i.Entry == entry);
        }

        public static void EquipWeapon()
        {
            bool is2Hand = false;
            // equip right hand weapon
			uint mainHandID = AutoAnglerBot.Instance.MySettings.MainHand;
            WoWItem mainHand = StyxWoW.Me.Inventory.Equipped.MainHand;
            if (mainHand == null || (mainHand.Entry != mainHandID && Utils.IsItemInBag(mainHandID)))
            {
				var weapon = StyxWoW.Me.BagItems.FirstOrDefault(i => i.Entry == AutoAnglerBot.Instance.MySettings.MainHand);
                is2Hand = weapon.ItemInfo.InventoryType == InventoryType.TwoHandWeapon || weapon.ItemInfo.InventoryType == InventoryType.Ranged;
				Utils.EquipItemByID(AutoAnglerBot.Instance.MySettings.MainHand);
            }

            // equip left hand weapon
			uint offhandID = AutoAnglerBot.Instance.MySettings.OffHand;
            WoWItem offhand = StyxWoW.Me.Inventory.Equipped.OffHand;

            if ((!is2Hand && offhandID > 0 &&
                 (offhand == null || (offhand.Entry != offhandID && Utils.IsItemInBag(offhandID)))))
            {
				Utils.EquipItemByID(AutoAnglerBot.Instance.MySettings.OffHand);
            }
        }

        public static void UseItemByID(int id)
        {
            Lua.DoString("UseItemByName(\"" + id + "\")");
        }

        public static void EquipItemByName(String name)
        {
            Lua.DoString("EquipItemByName (\"" + name + "\")");
        }

        public static void EquipItemByID(uint id)
        {
            Lua.DoString("EquipItemByName ({0})", id);
        }

        public static void BlacklistPool(WoWGameObject pool, TimeSpan time, string reason)
        {
            Blacklist.Add(pool.Guid, time);
			AutoAnglerBot.Instance.Log("Blacklisting {0} for {1} Reason: {2}", pool.Name, time, reason);
            BotPoi.Current = new BotPoi(PoiType.None);
        }
    }
}