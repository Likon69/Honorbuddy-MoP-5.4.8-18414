using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Design;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;
using Buddy.Coroutines;
using HighVoltz.Professionbuddy.ComponentBase;
using HighVoltz.Professionbuddy.Dynamic;
using HighVoltz.Professionbuddy.PropertyGridUtilities;
using HighVoltz.Professionbuddy.PropertyGridUtilities.Editors;
using Styx;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Frames;
using Styx.Helpers;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace HighVoltz.Professionbuddy.Components
{
	[PBXmlElement("MailItem", new[] { "MailItemAction" })]
	public sealed class MailItemAction : PBAction
	{
		private const string MailItemLuaFormat = @"
            local mailItemI =1  
            local freeBagSlots = 0  
            local amount = {1}  
            local itemId = {0}  
            local bagged =0  
            for i=0,NUM_BAG_SLOTS do  
               freeBagSlots = freeBagSlots + GetContainerNumFreeSlots(i)  
            end  
            local bagInfo={{}}  
            for bag = 0,NUM_BAG_SLOTS do  
               for slot=1,GetContainerNumSlots(bag) do  
                  local id = GetContainerItemID(bag,slot) or 0  
                  local _,c,l = GetContainerItemInfo(bag, slot)  
                  if id == itemId and l == nil then  
                     table.insert(bagInfo,{{bag,slot,c}})  
                  end  
               end  
            end  
            local sortF = function (a,b)  
               if a == nil and b == nil or b == nil then return false end  
               if a == nil or  a[3] < b[3] then return true else return false end  
            end  
            if #bagInfo == 0 then return -1 end  
            table.sort(bagInfo,sortF)  
            local bagI = #bagInfo  
            while bagI > 0 do  
               if GetSendMailItem(mailItemI) == nil then  
                  while bagInfo[bagI][3] > amount-bagged and bagI >1 do bagI = bagI - 1 end  
                  if bagInfo[bagI][3] + bagged <= amount or freeBagSlots == 0 then  
                     PickupContainerItem(bagInfo[bagI][1], bagInfo[bagI][2])  
                     ClickSendMailItemButton(mailItemI)  
                     bagged = bagged + bagInfo[bagI][3]  
                     bagI = bagI - 1  
                     return bagged  
                  else  
                     local cnt = bagInfo[bagI][3]-amount  
                     SplitContainerItem(bagInfo[bagI][1],bagInfo[bagI][2], cnt)  
                     local bagSpaces ={{}}    for b=NUM_BAG_SLOTS,0,-1 do  
                        bagSpaces = GetContainerFreeSlots(b)  
                        if #bagSpaces > 0 then  
                           PickupContainerItem(b,bagSpaces[#bagSpaces])  
                           return 0  
                        end  
                     end  
                  end  
               end  
               if bagged >= amount then return -1 end  
               mailItemI = mailItemI + 1  
               if mailItemI > ATTACHMENTS_MAX_SEND then  
                  return bagged  
               end  
            end  
            return bagged 
        ";

		private Dictionary<uint, int> _itemList;
		private WoWPoint _loc;
		private string _mailSubject;

		public MailItemAction()
		{
			Properties["ItemID"] = new MetaProp(
				"ItemID",
				typeof (string),
				new DisplayNameAttribute(Strings["Action_Common_ItemEntries"]));

			Properties["AutoFindMailBox"] = new MetaProp(
				"AutoFindMailBox",
				typeof (bool),
				new DisplayNameAttribute(
					Strings["Action_Common_AutoFindMailbox"]));

			Properties["Location"] = new MetaProp(
				"Location",
				typeof (string),
				new EditorAttribute(
					typeof (LocationEditor),
					typeof (UITypeEditor)),
				new DisplayNameAttribute(Strings["Action_Common_Location"]));

			Properties["Category"] = new MetaProp(
				"Category",
				typeof (WoWItemClass),
				new DisplayNameAttribute(Strings["Action_Common_ItemCategory"]));

			Properties["SubCategory"] = new MetaProp(
				"SubCategory",
				typeof (WoWItemTradeGoodsClass),
				new DisplayNameAttribute(
					Strings["Action_Common_ItemSubCategory"]));

			Properties["Amount"] = new MetaProp(
				"Amount",
				typeof (DynamicProperty<int>),
				new TypeConverterAttribute(
					typeof (DynamicProperty<int>.DynamivExpressionConverter)),
				new DisplayNameAttribute(Strings["Action_Common_Amount"]));

			Properties["Mail"] = new MetaProp(
				"Mail",
				typeof (DepositWithdrawAmount),
				new DisplayNameAttribute(Strings["Action_Common_Mail"]));

			Properties["ItemQuality"] = new MetaProp(
				"ItemQuality",
				typeof (WoWItemQuality),
				new DisplayNameAttribute(Strings["Action_Common_ItemQuality"]));

			Properties["ItemSelection"] = new MetaProp(
				"ItemSelection",
				typeof (ItemSelectionType),
				new DisplayNameAttribute(Strings["Action_Common_ItemSelection"]));

			ItemID = "";
			AutoFindMailBox = true;
			_loc = WoWPoint.Zero;
			Location = _loc.ToInvariantString();
			Category = WoWItemClass.TradeGoods;
			SubCategory = WoWItemTradeGoodsClass.None;
			Amount = new DynamicProperty<int>("Amount", this, "0");
			Mail = DepositWithdrawAmount.All;
			ItemQuality = WoWItemQuality.Uncommon;
			ItemSelection = ItemSelectionType.Category;

			Properties["Location"].Show = false;
			Properties["ItemID"].Show = false;
			Properties["ItemQuality"].Show = false;
			Properties["AutoFindMailBox"].PropertyChanged += AutoFindMailBoxChanged;
			Properties["Location"].PropertyChanged += LocationChanged;
			Properties["Category"].PropertyChanged += CategoryChanged;
			Properties["Mail"].PropertyChanged += MailChanged;
			Properties["ItemSelection"].PropertyChanged += ItemSelectionChanged;
		}

		#region Callbacks

		private void MailChanged(object sender, MetaPropArgs e)
		{
			Properties["Amount"].Show = Mail == DepositWithdrawAmount.Amount;
			RefreshPropertyGrid();
		}

		private void LocationChanged(object sender, MetaPropArgs e)
		{
			_loc = Util.StringToWoWPoint((string) ((MetaProp) sender).Value);
			Properties["Location"].PropertyChanged -= LocationChanged;
			Properties["Location"].Value = string.Format(CultureInfo.InvariantCulture, "{0}, {1}, {2}", _loc.X, _loc.Y, _loc.Z);
			Properties["Location"].PropertyChanged += LocationChanged;
			RefreshPropertyGrid();
		}

		private void ItemSelectionChanged(object sender, MetaPropArgs e)
		{
			switch (ItemSelection)
			{
				case ItemSelectionType.IDs:
					Properties["ItemID"].Show = true;
					Properties["Category"].Show = false;
					Properties["SubCategory"].Show = false;
					Properties["ItemQuality"].Show = false;
					break;
				case ItemSelectionType.Category:
					Properties["ItemID"].Show = false;
					Properties["Category"].Show = true;
					Properties["SubCategory"].Show = true;
					Properties["ItemQuality"].Show = false;
					break;
				case ItemSelectionType.Quality:
					Properties["ItemID"].Show = false;
					Properties["Category"].Show = false;
					Properties["SubCategory"].Show = false;
					Properties["ItemQuality"].Show = true;
					break;
			}
			RefreshPropertyGrid();
		}

		private void CategoryChanged(object sender, MetaPropArgs e)
		{
			object subCategory = Callbacks.GetSubCategory(Category);
			Properties["SubCategory"] = new MetaProp(
				"SubCategory",
				subCategory.GetType(),
				new DisplayNameAttribute("Item SubCategory"));
			SubCategory = subCategory;
			RefreshPropertyGrid();
		}

		private void AutoFindMailBoxChanged(object sender, MetaPropArgs e)
		{
			Properties["Location"].Show = !AutoFindMailBox;
			RefreshPropertyGrid();
		}

		#endregion

		[PBXmlAttribute]
		public DepositWithdrawAmount Mail
		{
			get { return Properties.GetValue<DepositWithdrawAmount>("Mail"); }
			set { Properties["Mail"].Value = value; }
		}

		public WoWItemClass Category
		{
			get { return Properties.GetValue<WoWItemClass>("Category"); }
			set { Properties["Category"].Value = value; }
		}

		public object SubCategory
		{
			get { return Properties["SubCategory"].Value; }
			set { Properties["SubCategory"].Value = value; }
		}

		[PBXmlAttribute("ItemID", new[] {"Entry"})]
		public string ItemID
		{
			get { return Properties.GetValue<string>("ItemID"); }
			set { Properties["ItemID"].Value = value; }
		}

		[PBXmlAttribute]
		[TypeConverter(typeof (DynamicProperty<int>.DynamivExpressionConverter))]
		public DynamicProperty<int> Amount
		{
			get { return Properties.GetValue<DynamicProperty<int>>("Amount"); }
			set { Properties["Amount"].Value = value; }
		}

		[PBXmlAttribute]
		public bool AutoFindMailBox
		{
			get { return Properties.GetValue<bool>("AutoFindMailBox"); }
			set { Properties["AutoFindMailBox"].Value = value; }
		}

		[PBXmlAttribute]
		public string Location
		{
			get { return Properties.GetValue<string>("Location"); }
			set { Properties["Location"].Value = value; }
		}

		[PBXmlAttribute]
		public WoWItemQuality ItemQuality
		{
			get { return Properties.GetValue<WoWItemQuality>("ItemQuality"); }
			set { Properties["ItemQuality"].Value = value; }
		}

		[PBXmlAttribute]
		public ItemSelectionType ItemSelection
		{
			get { return Properties.GetValue<ItemSelectionType>("ItemSelection"); }
			set { Properties["ItemSelection"].Value = value; }
		}

		public override string Name
		{
			get { return Strings["Action_MailItemAction_Name"]; }
		}

		public override string Title
		{
			get
			{
				return string.Format(
					"{0}: to:{1} {2} ",
					Name,
					CharacterSettings.Instance.MailRecipient,
					ItemSelection == ItemSelectionType.Category
						? string.Format("{0} {1}", Category, SubCategory)
						: (ItemSelection == ItemSelectionType.Quality ? ItemQuality.ToString() : ItemID));
			}
		}

		public override string Help
		{
			get { return Strings["Action_MailItemAction_Help"]; }
		}

		protected override async Task Run()
		{
			if (!MailFrame.Instance.IsVisible)
			{
				await OpenMailbox();
				return;
			}

			if (_itemList == null)
				_itemList = BuildItemList();

			if (string.IsNullOrEmpty(_mailSubject))
				_mailSubject = " ";

			if (!_itemList.Any())
			{
				if (NumberOfSlotsUsedInSendMail > 0)
					await MailItems(CharacterSettings.Instance.MailRecipient, _mailSubject);

				IsDone = true;
				PBLog.Log(
					"Done sending {0} via mail",
					ItemSelection == ItemSelectionType.Category
						? string.Format(
							"Items that belong to category {0} and subcategory {1}",
							Category,
							SubCategory)
						: (ItemSelection == ItemSelectionType.IDs
							? string.Format("Items that match Id of {0}", ItemID)
							: string.Format("Items of quality {0}", ItemQuality)));
				return;
			}

			MailFrame.Instance.SwitchToSendMailTab();
			uint itemID = _itemList.Keys.FirstOrDefault();
			WoWItem item = Me.BagItems.FirstOrDefault(i => i.Entry == itemID);
			_mailSubject = item != null ? item.Name : " ";

			PBLog.Debug("MailItem: placing {0} into a Send Mail slot", item != null ? item.Name:itemID.ToString());

			if (NumberOfSlotsUsedInSendMail >= 12)
				await MailItems(CharacterSettings.Instance.MailRecipient, _mailSubject);

			int amountPlaced = PlaceItemInMail(itemID, _itemList[itemID]);
			if (amountPlaced >= 0)
			{
				// we need to wait for item split to finish if ret >= 0
				await CommonCoroutines.SleepForLagDuration();
			}

			_itemList[itemID] = amountPlaced < 0 ? 0 : _itemList[itemID] - amountPlaced;

			bool doneWithItem = _itemList[itemID] <= 0;
			if (doneWithItem)
				_itemList.Remove(itemID);
		}

		private int NumberOfSlotsUsedInSendMail
		{
			get
			{
				return
					Lua.GetReturnVal<int>(
						"local cnt = 0 for i=1,ATTACHMENTS_MAX_SEND do if GetSendMailItem(i) ~= nil then cnt = cnt + 1 end end return cnt",
						0);
			}
		}

		private async Task MailItems(string recipient, string subject)
		{
			var lua = string.Format("SendMail ('{0}', '{1}' ,'');  ",
				recipient.ToFormatedUTF8(), subject.ToFormatedUTF8());
			PBLog.Debug("MailItem: Sending mail");
			Lua.DoString(lua);
			await CommonCoroutines.SleepForLagDuration();
		}

		private async Task OpenMailbox()
		{
			WoWPoint movetoPoint = _loc;
			WoWGameObject mailbox;
			if (AutoFindMailBox || movetoPoint == WoWPoint.Zero)
			{
				mailbox =
					ObjectManager.GetObjectsOfType<WoWGameObject>().Where(
						o => o.SubType == WoWGameObjectType.Mailbox && o.CanUse())
						.OrderBy(o => o.DistanceSqr).FirstOrDefault();
			}
			else
			{
				mailbox =
					ObjectManager.GetObjectsOfType<WoWGameObject>().Where(
						o => o.SubType == WoWGameObjectType.Mailbox
							&& o.Location.Distance(_loc) < 10 && o.CanUse())
						.OrderBy(o => o.DistanceSqr).FirstOrDefault();
			}
			if (mailbox != null)
				movetoPoint = mailbox.Location;

			if (movetoPoint == WoWPoint.Zero)
			{
				PBLog.Warn(Strings["Error_UnableToFindMailbox"]);
				return;
			}

			if (mailbox == null || !mailbox.WithinInteractRange)
			{
				await CommonCoroutines.MoveTo(movetoPoint);
				return;
			}

			if (Me.IsMoving)
				await CommonCoroutines.StopMoving();
			mailbox.Interact();
		}

		private Dictionary<uint, int> BuildItemList()
		{
			var itemList = new Dictionary<uint, int>();
			IEnumerable<WoWItem> tmpItemlist = from item in Me.BagItems
				where !item.IsConjured && !item.IsSoulbound && !item.IsDisabled
				select item;
			switch (ItemSelection)
			{
				case ItemSelectionType.Category:
					foreach (WoWItem item in tmpItemlist)
					{
						// skip tradeskill tools. If tools need to be mailed then they should be selected by ID
						if (ProfessionbuddyBot.Instance.TradeskillTools.Contains(item.Entry))
							continue;
						if (item.ItemInfo.ItemClass == Category &&
							SubCategoryCheck(item) && !itemList.ContainsKey(item.Entry))
						{
							itemList.Add(
								item.Entry,
								Mail == DepositWithdrawAmount.Amount
									? Amount
									: Util.GetCarriedItemCount(item.Entry));
						}
					}
					break;
				case ItemSelectionType.IDs:
					string[] entries = ItemID.Split(',');
					if (entries.Length > 0)
					{
						foreach (string entry in entries)
						{
							uint itemID;
							if (!uint.TryParse(entry.Trim(), out itemID))
							{
								PBLog.Warn(Strings["Error_NotAValidItemEntry"], entry.Trim());
								continue;
							}
							itemList.Add(
								itemID,
								Mail == DepositWithdrawAmount.Amount
									? Amount
									: Util.GetCarriedItemCount(itemID));
						}
					}
					else
					{
						PBLog.Warn("No ItemIDs are specified");
						IsDone = true;
					}
					break;
				case ItemSelectionType.Quality:
					foreach (WoWItem item in tmpItemlist)
					{
						// skip tradeskill tools. If tools need to be mailed then they should be selected by ID
						if (ProfessionbuddyBot.Instance.TradeskillTools.Contains(item.Entry))
							continue;
						if (item.Quality == ItemQuality)
						{
							itemList.Add(
								item.Entry,
								Mail == DepositWithdrawAmount.Amount
									? Amount
									: Util.GetCarriedItemCount(item.Entry));
						}
					}
					break;
			}
			return itemList;
		}

		private bool SubCategoryCheck(WoWItem item)
		{
			var sub = (int) SubCategory;
			if (sub == -1 || sub == 0)
				return true;
			PropertyInfo firstOrDefault =
				item.ItemInfo.GetType().GetProperties().FirstOrDefault(t => t.PropertyType == SubCategory.GetType());
			if (firstOrDefault != null)
			{
				object val = firstOrDefault.GetValue(item.ItemInfo, null);
				if (val != null && (int) val == sub)
					return true;
			}
			return false;
		}

		// format indexs are ItemID=0, Amount=1

		// return -1 if done, 0 if spliting item else the amount of items placed in mail.
		private int PlaceItemInMail(uint id, int amount)
		{
			// format indexs are ItemID=0, Amount=1, MailRecipient=2
			string lua = string.Format(MailItemLuaFormat, id, amount);
			return Lua.GetReturnVal<int>(lua, 0);
		}

		public override void Reset()
		{
			_itemList = null;
			base.Reset();
		}

		public override IPBComponent DeepCopy()
		{
			return new MailItemAction
					{
						ItemID = ItemID,
						_loc = _loc,
						AutoFindMailBox = AutoFindMailBox,
						Location = Location,
						ItemQuality = ItemQuality,
						ItemSelection = ItemSelection,
						Category = Category,
						SubCategory = SubCategory,
						Amount = Amount,
						Mail = Mail
					};
		}

		public override void OnProfileLoad(XElement element)
		{
			XAttribute cat = element.Attribute("Category");
			XAttribute subCatAttr = element.Attribute("SubCategory");
			XAttribute subCatTypeAttr = element.Attribute("SubCategoryType");
			if (cat != null)
			{
				Category = (WoWItemClass) Enum.Parse(typeof (WoWItemClass), cat.Value);
				cat.Remove();
			}
			if (subCatAttr != null && subCatTypeAttr != null)
			{
				Type subCategoryType = subCatTypeAttr.Value != "SubCategoryType"
					? Util.GetSubCategoryType(subCatTypeAttr.Value)
					: typeof (SubCategoryType);
				SubCategory = Enum.Parse(subCategoryType, subCatAttr.Value);
				subCatAttr.Remove();
				subCatTypeAttr.Remove();
			}

			// add backwards compatibility for the UseCategory attribute
			XAttribute useCategoryAttr = element.Attribute("UseCategory");
			if (useCategoryAttr != null)
			{
				var value = bool.Parse(useCategoryAttr.Value);
				ItemSelection = value ? ItemSelectionType.Category : ItemSelectionType.IDs;
			}
			base.OnProfileLoad(element);
		}

		public override void OnProfileSave(XElement element)
		{
			element.Add(new XAttribute("Category", Category.ToString()));
			element.Add(new XAttribute("SubCategoryType", SubCategory.GetType().Name));
			element.Add(new XAttribute("SubCategory", SubCategory.ToString()));
			base.OnProfileSave(element);
		}

	}
}