using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HighVoltz.Professionbuddy.ComponentBase;
using HighVoltz.Professionbuddy.PropertyGridUtilities;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.Profiles;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace HighVoltz.Professionbuddy.Components
{
	[PBXmlElement("Disenchant", new[] { "DisenchantAction" })]
	public sealed class DisenchantAction : PBAction
	{
		#region DeActionType enum

		public enum DeActionType
		{
			Mill = 0,
			Prospect,
			Disenchant
		}

		#endregion

		#region DeItemQualites enum

		public enum DeItemQualites
		{
			Epic,
			Rare,
			Uncommon
		}

		#endregion

		#region ItemTargetType enum

		public enum ItemTargetType
		{
			Specific,
			All
		}

		#endregion

		private readonly List<ulong> _blacklistedItems = new List<ulong>();
		private readonly Stopwatch _castTimer = new Stopwatch();
		private readonly Stopwatch _lootSw = new Stopwatch();
		private ulong _lastItemGuid;
		private uint _lastStackSize;
		private int _tries;

		public DisenchantAction()
		{
			Properties["ActionType"] = new MetaProp(
				"ActionType",
				typeof (DeActionType),
				new DisplayNameAttribute(Strings["Action_Common_ActionType"]));

			Properties["ItemTarget"] = new MetaProp(
				"ItemTarget",
				typeof (ItemTargetType),
				new DisplayNameAttribute(Strings["Action_Common_ItemTarget"]));

			Properties["ItemQuality"] = new MetaProp(
				"ItemQuality",
				typeof (DeItemQualites),
				new DisplayNameAttribute(Strings["Action_Common_ItemQuality"]));

			Properties["ItemId"] = new MetaProp(
				"ItemId",
				typeof (int),
				new DisplayNameAttribute(Strings["Action_Common_ItemEntry"]));

			ActionType = DeActionType.Disenchant;
			ItemTarget = ItemTargetType.All;
			ItemQuality = DeItemQualites.Uncommon;
			ItemId = 0;
			Properties["ItemId"].Show = false;
			Properties["ActionType"].PropertyChanged += ActionTypeChanged;
			Properties["ItemTarget"].PropertyChanged += ItemTargetChanged;
		}

		[PBXmlAttribute]
		public DeActionType ActionType
		{
			get { return Properties.GetValue<DeActionType>("ActionType"); }
			set { Properties["ActionType"].Value = value; }
		}

		[PBXmlAttribute]
		public ItemTargetType ItemTarget
		{
			get { return Properties.GetValue<ItemTargetType>("ItemTarget"); }
			set { Properties["ItemTarget"].Value = value; }
		}

		[PBXmlAttribute]
		public DeItemQualites ItemQuality
		{
			get { return Properties.GetValue<DeItemQualites>("ItemQuality"); }
			set { Properties["ItemQuality"].Value = value; }
		}

		[PBXmlAttribute]
		public int ItemId
		{
			get { return Properties.GetValue<int>("ItemId"); }
			set { Properties["ItemId"].Value = value; }
		}

		private int SpellId
		{
			get
			{
				switch (ActionType)
				{
					case DeActionType.Disenchant:
						return 13262;
					case DeActionType.Mill:
						return 51005;
					case DeActionType.Prospect:
						return 31252;
				}
				return 0;
			}
		}

		public override string Name
		{
			get { return Strings["Action_DisenchantAction_Name"]; }
		}

		public override string Title
		{
			get
			{
				return string.Format(
					"{0}: {1} {2}",
					ActionType,
					ItemTarget == ItemTargetType.Specific
						? ItemId.ToString(CultureInfo.InvariantCulture)
						: Strings["Action_Common_All"]
					,
					ItemTarget == ItemTargetType.All && ActionType == DeActionType.Disenchant
						? ItemQuality.ToString()
						: "");
			}
		}

		public override string Help
		{
			get { return Strings["Action_DisenchantAction_Help"]; }
		}

		private uint WaitForLagTimeMs
		{
			get { return (3*Util.WowWorldLatency) + 1000; }
		}

		private void ActionTypeChanged(object sender, MetaPropArgs e)
		{
			Properties["ItemQuality"].Show = ActionType == DeActionType.Disenchant;
			RefreshPropertyGrid();
		}

		private void ItemTargetChanged(object sender, MetaPropArgs e)
		{
			Properties["ItemId"].Show = ItemTarget == ItemTargetType.Specific;
			RefreshPropertyGrid();
		}

		private uint GetCastTime()
		{
			uint castime;
			switch (ActionType)
			{
				case DeActionType.Disenchant:
					castime = 1500;
					break;
				case DeActionType.Mill:
					castime = 1000;
					break;
				default:
					castime = 2000;
					break;
			}
			return castime + WaitForLagTimeMs;
		}

		protected override async Task Run()
		{
			if (Me.IsFlying)
				return;
			if (_lootSw.IsRunning && _lootSw.ElapsedMilliseconds < WaitForLagTimeMs)
				return;
			if (LootFrame.Instance != null && LootFrame.Instance.IsVisible)
			{
				LootFrame.Instance.LootAll();
				_lootSw.Reset();
				_lootSw.Start();
				return;
			}
			uint timeToWait = GetCastTime();

			if (!Me.IsCasting && (!_castTimer.IsRunning || _castTimer.ElapsedMilliseconds >= timeToWait))
			{
				List<WoWItem> itemList = BuildItemList();
				if (itemList == null || !itemList.Any())
				{
					IsDone = true;
					PBLog.Log("Done {0}ing", ActionType);
				}
				else
				{
					// skip 'locked' items
					int index = 0;
					for (; index <= itemList.Count; index++)
					{
						if (!itemList[index].IsDisabled)
							break;
					}
					if (index < itemList.Count)
					{
						if (itemList[index].Guid == _lastItemGuid && _lastStackSize == itemList[index].StackCount)
						{
							if (++_tries >= 3)
							{
								PBLog.Log(
									"Unable to {0} {1}, BlackListing",
									ActionType,
									itemList[index].Name);
								if (!_blacklistedItems.Contains(_lastItemGuid))
									_blacklistedItems.Add(_lastItemGuid);
								return;
							}
						}
						else
						{
							_tries = 0;
						}
						WoWSpell spell = WoWSpell.FromId(SpellId);
						if (spell != null)
						{
							TreeRoot.GoalText = string.Format("{0}: {1}", ActionType, itemList[index].Name);
							PBLog.Log(TreeRoot.GoalText);
							//Lua.DoString("CastSpellByID({0}) UseContainerItem({1}, {2})",
							//    spellId, ItemList[index].BagIndex + 1, ItemList[index].BagSlot + 1);
							spell.CastOnItem(itemList[index]);
							_lastItemGuid = itemList[index].Guid;
							_lastStackSize = itemList[index].StackCount;
							_castTimer.Reset();
							_castTimer.Start();
						}
						else
						{
							IsDone = true;
						}
					}
				}
			}
		}

		private List<WoWItem> BuildItemList()
		{
			using (StyxWoW.Memory.AcquireFrame())
			{
				int skillLevel = 0;
				// cache the skillevel for this pulse..
				if (ActionType == DeActionType.Disenchant)
					skillLevel = StyxWoW.Me.GetSkill(SkillLine.Enchanting).CurrentValue;
				else if (ActionType == DeActionType.Mill)
					skillLevel = StyxWoW.Me.GetSkill(SkillLine.Inscription).CurrentValue;
				else if (ActionType == DeActionType.Prospect)
					skillLevel = StyxWoW.Me.GetSkill(SkillLine.Jewelcrafting).CurrentValue;

				IEnumerable<WoWItem> itemQuery = from item in StyxWoW.Me.BagItems
					where !IsBlackListed(item) &&
						((ItemTarget == ItemTargetType.Specific && item.Entry == ItemId) ||
						ItemTarget == ItemTargetType.All)
					select item;

				switch (ActionType)
				{
					case DeActionType.Disenchant:
						return itemQuery.Where(
							i => !ProtectedItemsManager.Contains(i.Entry)
								&& i.CanDisenchant(skillLevel) && CheckItemQuality(i)
								&& !ProfessionbuddyBot.Instance.TradeskillTools.Contains(i.Entry)).ToList();
					case DeActionType.Mill:
						return itemQuery.Where(i => i.CanMill(skillLevel) && i.StackCount >= 5).ToList();
					case DeActionType.Prospect:
						return itemQuery.Where(i => i.CanProspect(skillLevel) && i.StackCount >= 5).ToList();
				}
			}
			return null;
		}

		private bool IsBlackListed(WoWItem item)
		{
			return _blacklistedItems.Contains(item.Guid);
		}

		private bool CheckItemQuality(WoWItem item)
		{
			bool returnVal = ItemQuality == DeItemQualites.Uncommon && item.Quality == WoWItemQuality.Uncommon;
			if (ItemQuality == DeItemQualites.Rare &&
				(item.Quality == WoWItemQuality.Uncommon || item.Quality == WoWItemQuality.Rare))
			{
				returnVal = true;
			}
			if (ItemQuality == DeItemQualites.Epic && (item.Quality == WoWItemQuality.Uncommon ||
														item.Quality == WoWItemQuality.Rare ||
														item.Quality == WoWItemQuality.Epic))
			{
				returnVal = true;
			}
			return returnVal;
		}
		
		public override IPBComponent DeepCopy()
		{			
			return new DisenchantAction
					{
						ActionType = ActionType,
						ItemTarget = ItemTarget,
						ItemQuality = ItemQuality,
						ItemId = ItemId
					};
		}
	}

	internal static class WoWitemExt
	{
		public static bool CanMill(this WoWItem item, int skillLevel)
		{
			var requiredLevel = item.MinInscriptionLevelReq();
			return requiredLevel >= 0 && skillLevel >= requiredLevel;
			// returns true if item is found in the dictionary and player meets the level requirement
			//return MillableHerbList.ContainsKey(item.Entry) && MillableHerbList[item.Entry] <= skillLevel;
		}

		public static bool CanProspect(this WoWItem item, int skillLevel)
		{
			var requiredLevel = item.MinJewelCraftLevelReq();
			return requiredLevel >= 0 && skillLevel >= requiredLevel;
		}

		public static bool CanDisenchant(this WoWItem item, int skillLevel)
		{
			if (item.Quality < WoWItemQuality.Uncommon)
				return false;
			if (item.ItemInfo.ItemClass != WoWItemClass.Armor && item.ItemInfo.ItemClass != WoWItemClass.Weapon)
				return false;
			if (item.DurabilityPercent < 100)
				return false;

			var requiredLevel = item.MinEnchantLevelReq();

			return requiredLevel >= 0 && skillLevel >= requiredLevel;
		}
	}
}