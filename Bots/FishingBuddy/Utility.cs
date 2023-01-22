using System;
using System.Linq;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.WoWInternals;
using Styx.WoWInternals.DB;
using Styx.WoWInternals.WoWObjects;

namespace Bots.FishingBuddy
{
	internal static class Utility
	{
		// Hats are listed in best to worst order.
		internal static readonly uint[] FishingHatIds =
		{
			117405, // Nat's Drinking Hat
			118380, // Hightfish Cap
			118393, // Tentacled Hat
			88710, // Nat's Hat
			33820, // Weather-Beaten Fishing Hat
		};

		public static bool IsItemInBag(uint entry)
		{
			return StyxWoW.Me.BagItems.Any(i => i.Entry == entry);
		}

		public static WoWItem GetItemInBag(uint entry)
		{
			return StyxWoW.Me.BagItems.FirstOrDefault(i => i.Entry == entry);
		}

		public static bool EquipWeapons()
		{
			if (StyxWoW.Me.ChanneledCastingSpellId != 0)
				SpellManager.StopCasting();

			bool is2Hand = false;
			// equip right hand weapon
			uint mainHandID = FishingBuddySettings.Instance.MainHand;
			WoWItem equippedMainHand = StyxWoW.Me.Inventory.Equipped.MainHand;
			WoWItem wantedMainHand = null;
			if (equippedMainHand == null || (equippedMainHand.Entry != mainHandID && IsItemInBag(mainHandID)))
			{
				wantedMainHand = StyxWoW.Me.BagItems
					.FirstOrDefault(i => i.Entry == FishingBuddySettings.Instance.MainHand);
				if (wantedMainHand == null)
				{
					Logging.Write("Could not find a mainhand weapon to equip");
					return false;
				}
				is2Hand = wantedMainHand.ItemInfo.InventoryType == InventoryType.TwoHandWeapon
						|| wantedMainHand.ItemInfo.InventoryType == InventoryType.Ranged;
				wantedMainHand.UseContainerItem();
			}

			// equip left hand weapon
			uint offhandID = FishingBuddySettings.Instance.OffHand;
			WoWItem equippedOffHand = StyxWoW.Me.Inventory.Equipped.OffHand;

			if ((!is2Hand && offhandID > 0 && (equippedOffHand == null || equippedOffHand.Entry != offhandID)))
			{
				WoWItem wantedOffHand = StyxWoW.Me.BagItems
					.FirstOrDefault(i => i.Entry == FishingBuddySettings.Instance.OffHand && i != wantedMainHand);
				if (wantedOffHand == null)
				{
					Logging.Write("Could not find a offhand weapon to equip");
					return false;
				}
				wantedOffHand.UseContainerItem();
			}
			return true;
		}

		internal static WoWItem GetFishingHat()
		{
			foreach (var fishingHatId in FishingHatIds)
			{
				var hat = StyxWoW.Me.CarriedItems.FirstOrDefault(i => i.Entry == fishingHatId && HasRequiredSkillLevel(i.ItemInfo));

				if (hat != null)
					return hat;
			}
			return null;
		}

		public static bool EquipMainHat()
		{
			if (StyxWoW.Me.Combat)
				return false;

			if (StyxWoW.Me.ChanneledCastingSpellId != 0)
				SpellManager.StopCasting();

			WoWItem currentHat = StyxWoW.Me.Inventory.Equipped.Head;

			// if not wearing a fishing hat then return
			if (currentHat != null && !FishingHatIds.Contains(currentHat.Entry))
			{
				return false;
			}

			WoWItem regularHat = null;
			// try to find a hat to wear automatically
			if (FishingBuddySettings.Instance.Hat == 0)
			{
				var bestHat = StyxWoW.Me.BagItems.Where(i => i != null && i.IsValid && i.ItemInfo.EquipSlot == InventoryType.Head)
					.OrderByDescending(i => i.ItemInfo.Level)
					.FirstOrDefault();
				if (bestHat != null)
				{
					FishingBuddySettings.Instance.Hat = bestHat.Entry;
					FishingBuddySettings.Instance.Save();
					regularHat = bestHat;
				}
			}

			if (currentHat != null && currentHat.Entry == FishingBuddySettings.Instance.Hat)
				return false;

			if (regularHat == null)
			{
				regularHat = StyxWoW.Me.BagItems.FirstOrDefault(
					i => i != null && i.IsValid && i.Entry == FishingBuddySettings.Instance.Hat);
			}

			if (regularHat == null)
				return false;

			regularHat.UseContainerItem();
			return true;
		}

		internal static bool EquipItem(WoWItem item, WoWInventorySlot slot)
		{
			if (item == null || !item.IsValid)
				return false;

			FishingBuddyBot.Log("Equipping {0}", item.SafeName);
			Lua.DoString("ClearCursor()");
			item.PickUp();
			Lua.DoString(string.Format("PickupInventoryItem({0})", (int)slot + 1));
			return true;
		}

		public static void BlacklistPool(WoWGameObject pool, TimeSpan time, string reason)
		{
			Blacklist.Add(pool.Guid, BlacklistFlags.Node, time, reason);
			BotPoi.Clear(reason);
		}

		public static int GetBonusFishingSkillOnEquip(WoWItem item) {
			/*
			if (item.Effects == null)
				return 0;

			foreach (var effect in item.Effects)
			{
				if (effect.TriggerType != ItemEffectTriggerType.OnEquip || effect.Spell == null)
					continue;

				foreach (var spellEffect in effect.Spell.SpellEffects)
				{
					if (spellEffect.AuraType != WoWApplyAuraType.ModSkill && spellEffect.EffectType != WoWSpellEffectType.ApplyAura)
						continue;

					if ((SkillLine)spellEffect.MiscValueA == SkillLine.Fishing)
						return spellEffect.BasePoints;
				}
			}
			*/
			return 0;
		}

		public static bool HasRequiredSkillLevel(ItemInfo itemInfo)
		{
			return itemInfo.RequiredSkillId == 0
				   || itemInfo.RequiredSkillLevel <= StyxWoW.Me.GetSkill(itemInfo.RequiredSkillId).CurrentValue;
		}

	}
}