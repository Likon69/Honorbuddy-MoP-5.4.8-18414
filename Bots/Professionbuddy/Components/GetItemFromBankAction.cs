using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Design;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HighVoltz.Professionbuddy.ComponentBase;
using HighVoltz.Professionbuddy.Dynamic;
using HighVoltz.Professionbuddy.PropertyGridUtilities;
using HighVoltz.Professionbuddy.PropertyGridUtilities.Editors;
using Styx;
using Styx.Helpers;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace HighVoltz.Professionbuddy.Components
{
	[PBXmlElement("GetItemfromBank", new[] { "GetItemfromBankAction" })]
    public sealed class GetItemfromBankAction : PBAction
    {
        // number of times the recipe will be crafted
        public enum BankWithdrawlItemType
        {
            SpecificItem,
            Materials,
        }

        private const long GbankItemThrottle = 1000;

        private const string WithdrawItemFromGBankLuaFormat = @"
            local tabnum = GetNumGuildBankTabs()  
            local bagged = 0  
            local itemID = {0}  
            local  sawItem = 0   
            local amount = {1}  
            for tab = 1,tabnum do  
               local _,_,iv,_,nw, rw = GetGuildBankTabInfo(tab)   
               if iv and (nw > 0 or nw == -1) and (rw == -1 or rw > 0) then  
                  SetCurrentGuildBankTab(tab)  
                  for slot = 1, 98 do  
                     local _,c,l=GetGuildBankItemInfo(tab, slot)  
                     local id = tonumber(string.match(GetGuildBankItemLink(tab, slot) or '','|Hitem:(%d+)'))  
                     if l == nil and c > 0 and (id == itemID or itemID == 0) then  
                        sawItem = 1  
                        if c  <= amount then  
                           AutoStoreGuildBankItem(tab, slot)  
                           return c  
                        else  
                           local itemf  = GetItemFamily(id)  
                           for bag =0 ,NUM_BAG_SLOTS do  
                              local fs,bfamily = GetContainerNumFreeSlots(bag)  
                              if fs > 0 and (bfamily == 0 or bit.band(itemf, bfamily) > 0) then  
                                 local freeSlots = GetContainerFreeSlots(bag)  
                                 SplitGuildBankItem(tab, slot, amount)  
                                 PickupContainerItem(bag, freeSlots[1])  
                                 return amount-c  
                              end  
                           end  
                        end  
                     end  
                  end  
               end  
            end  
            if sawItem == 0 then return -1 else return bagged end   
";
        // thanks to Stove for fixing problem with PutItemInBag not working - http://www.thebuddyforum.com/members/134034-stove.html 

        private const string WithdrawItemFromPersonalBankLuaFormat = @"
            local numSlots = GetNumBankSlots()  
            local splitUsed = 0  
            local bagged = 0  
            local amount = {1}  
            local itemID = {0}  
            local bag1 = numSlots + 4   
            while bag1 >= -1 do  
               if bag1 == 4 then  
                  bag1 = -1  
               end  
               for slot1 = 1, GetContainerNumSlots(bag1) do  
                  local _,c,l=GetContainerItemInfo(bag1, slot1)  
                  local id = GetContainerItemID(bag1,slot1)  
                  if l ~= 1 and c and c > 0 and (id == itemID or itemID == 0) then  
                     if c + bagged <= amount  then  
                        UseContainerItem(bag1,slot1)  
                        bagged = bagged + c  
                     else  
                        local itemf  = GetItemFamily(id)  
                        for bag2 = 0,4 do  
                           local fs,bfamily = GetContainerNumFreeSlots(bag2)  
                           if fs > 0 and (bfamily == 0 or bit.band(itemf, bfamily) > 0) then  
                              local freeSlots = GetContainerFreeSlots(bag2)  
                              SplitContainerItem(bag1,slot1,amount - bagged)  
                              if bag2 == 0 then PutItemInBackpack() else 
					            slotTable=GetContainerFreeSlots(bag2) 
					            first=slotTable[1] 
					            PickupContainerItem(bag2, first)
					          end  
                              return  
                           end  
                           bag2 = bag2 -1  
                        end  
                     end  
                     if bagged >= amount then return end  
                  end  
               end  
               bag1 = bag1 -1  
            end";
      

        private static Stopwatch _queueServerSW;
        private readonly Stopwatch _gbankItemThrottleSW = new Stopwatch();
        private Dictionary<uint, int> _itemList;
        private Stopwatch _itemsSW;
        private WoWPoint _loc;
	    private int _withdrawCnt;

        public GetItemfromBankAction()
        {
            Properties["Amount"] = new MetaProp("Amount", typeof (DynamicProperty<int>),
                                                new TypeConverterAttribute(
                                                    typeof (DynamicProperty<int>.DynamivExpressionConverter)),
                                                new DisplayNameAttribute(Strings["Action_Common_Amount"]));

            Properties["ItemID"] = new MetaProp("ItemID", typeof (string),
                                                new DisplayNameAttribute(Strings["Action_Common_ItemEntries"]));

            Properties["MinFreeBagSlots"] = new MetaProp("MinFreeBagSlots", typeof (uint),
                                                         new DisplayNameAttribute(
                                                             Strings["Action_Common_MinFreeBagSlots"]));

            Properties["GetItemfromBankType"] = new MetaProp("GetItemfromBankType",
                                                             typeof (BankWithdrawlItemType),
                                                             new DisplayNameAttribute(
                                                                 Strings["Action_Common_ItemsToWithdraw"]));

            Properties["Bank"] = new MetaProp("Bank", typeof (BankType),
                                              new DisplayNameAttribute(Strings["Action_Common_Bank"]));

            Properties["AutoFindBank"] = new MetaProp("AutoFindBank", typeof (bool),
                                                      new DisplayNameAttribute(Strings["Action_Common_AutoFindBank"]));

            Properties["Location"] = new MetaProp("Location", typeof (string),
                                                  new EditorAttribute(typeof (LocationEditor),
                                                                      typeof (UITypeEditor)),
                                                  new DisplayNameAttribute(Strings["Action_Common_Location"]));

            Properties["NpcEntry"] = new MetaProp("NpcEntry",
                                                  typeof (uint),
                                                  new EditorAttribute(typeof (EntryEditor),
                                                                      typeof (UITypeEditor)),
                                                  new DisplayNameAttribute(Strings["Action_Common_NpcEntry"]));

            Properties["WithdrawAdditively"] = new MetaProp("WithdrawAdditively", typeof (bool),
                                                            new DisplayNameAttribute(
                                                                Strings["Action_Common_WithdrawAdditively"]));

            Properties["Withdraw"] = new MetaProp("Withdraw", typeof (DepositWithdrawAmount),
                                                  new DisplayNameAttribute(Strings["Action_Common_Withdraw"]));

            Amount = new DynamicProperty<int>("Amount", this, "1");
            ItemID = "";
            MinFreeBagSlots = 2u;
            GetItemfromBankType = BankWithdrawlItemType.SpecificItem;
            Bank = BankType.Personal;
            AutoFindBank = true;
            _loc = WoWPoint.Zero;
            Location = _loc.ToInvariantString();
            NpcEntry = 0u;
            WithdrawAdditively = true;
            Withdraw = DepositWithdrawAmount.All;

            Properties["Location"].Show = false;
            Properties["NpcEntry"].Show = false;
            Properties["Amount"].Show = false;

            Properties["AutoFindBank"].PropertyChanged += AutoFindBankChanged;
            Properties["GetItemfromBankType"].PropertyChanged += GetItemfromBankActionPropertyChanged;
            Properties["Location"].PropertyChanged += LocationChanged;
            Properties["Withdraw"].PropertyChanged += WithdrawChanged;
        }

        #region Callbacks

        private void WithdrawChanged(object sender, MetaPropArgs e)
        {
            Properties["Amount"].Show = Withdraw == DepositWithdrawAmount.Amount;
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

        private void AutoFindBankChanged(object sender, MetaPropArgs e)
        {
            if (AutoFindBank)
            {
                Properties["Location"].Show = false;
                Properties["NpcEntry"].Show = false;
            }
            else
            {
                Properties["Location"].Show = true;
                Properties["NpcEntry"].Show = true;
            }
            RefreshPropertyGrid();
        }

        private void GetItemfromBankActionPropertyChanged(object sender, MetaPropArgs e)
        {
            switch (GetItemfromBankType)
            {
                case BankWithdrawlItemType.SpecificItem:
                    Properties["Amount"].Show = true;
                    Properties["ItemID"].Show = true;
                    Properties["WithdrawAdditively"].Show = true;
                    break;
                case BankWithdrawlItemType.Materials:
                    Properties["Amount"].Show = false;
                    Properties["ItemID"].Show = false;
                    Properties["WithdrawAdditively"].Show = false;
                    break;
            }
            RefreshPropertyGrid();
        }

        #endregion

        [PBXmlAttribute]
        public DepositWithdrawAmount Withdraw
        {
            get { return Properties.GetValue<DepositWithdrawAmount>("Withdraw"); }
            set { Properties["Withdraw"].Value = value; }
        }

        [PBXmlAttribute]
        public BankType Bank
        {
            get { return Properties.GetValue<BankType>("Bank"); }
            set { Properties["Bank"].Value = value; }
        }

        [PBXmlAttribute]
        public uint MinFreeBagSlots
        {
            get { return Properties.GetValue<uint>("MinFreeBagSlots"); }
            set { Properties["MinFreeBagSlots"].Value = value; }
        }

        [PBXmlAttribute]
        public BankWithdrawlItemType GetItemfromBankType
        {
            get { return Properties.GetValue<BankWithdrawlItemType>("GetItemfromBankType"); }
            set { Properties["GetItemfromBankType"].Value = value; }
        }

        [PBXmlAttribute]
        public string ItemID
        {
            get { return Properties.GetValue<string>("ItemID"); }
            set { Properties["ItemID"].Value = value; }
        }

        [PBXmlAttribute]
        public uint NpcEntry
        {
            get { return Properties.GetValue<uint>("NpcEntry"); }
            set { Properties["NpcEntry"].Value = value; }
        }

        [PBXmlAttribute]
        [TypeConverter(typeof (DynamicProperty<int>.DynamivExpressionConverter))]
        public DynamicProperty<int> Amount
        {
            get { return Properties.GetValue<DynamicProperty<int>>("Amount"); }
            set { Properties["Amount"].Value = value; }
        }

        [PBXmlAttribute]
        public bool AutoFindBank
        {
            get { return Properties.GetValue<bool>("AutoFindBank"); }
            set { Properties["AutoFindBank"].Value = value; }
        }

        [PBXmlAttribute]
        public bool WithdrawAdditively
        {
            get { return  Properties.GetValue<bool>("WithdrawAdditively"); }
            set { Properties["WithdrawAdditively"].Value = value; }
        }

        [PBXmlAttribute]
        public string Location
        {
            get { return Properties.GetValue<string>("Location"); }
            set { Properties["Location"].Value = value; }
        }

        public override string Name
        {
            get { return Strings["Action_GetItemFromBankAction_Name"]; }
        }

        public override string Title
        {
            get
            {
                return string.Format("{0}: " + (GetItemfromBankType == BankWithdrawlItemType.SpecificItem
                                                    ? " {1} {2}"
                                                    : ""), Name, ItemID, Amount);
            }
        }

        public override string Help
        {
            get { return Strings["Action_GetItemFromBankAction_Help"]; }
        }

		protected async override Task Run()
		{
			if ((Bank == BankType.Guild && !Util.IsGBankFrameOpen) ||
							(Bank == BankType.Personal && !Util.IsBankFrameOpen))
			{
				MoveToBanker();
			}
			else
			{
				if (_itemsSW == null)
				{
					_itemsSW = new Stopwatch();
					_itemsSW.Start();
				}
				else if (_itemsSW.ElapsedMilliseconds < Util.WowWorldLatency * 3)
				{
					return;
				}
				if (_itemList == null)
				{
					_itemList = BuildItemList();
				}
				// no bag space... 
				if (Util.BagRoomLeft(_itemList.Keys.FirstOrDefault()) <= MinFreeBagSlots || !_itemList.Any())
				{
					IsDone = true;
				}
				else
				{
					uint itemID = _itemList.Keys.FirstOrDefault();
					bool done;
					if (Bank == BankType.Personal)
					{
						done = GetItemFromBank(itemID, _itemList[itemID]);
					}
					else
					{
						// throttle the amount of items being withdrawn from gbank per sec
						if (!_gbankItemThrottleSW.IsRunning)
							_gbankItemThrottleSW.Start();
						if (_gbankItemThrottleSW.ElapsedMilliseconds < GbankItemThrottle)
							return;

						int ret = GetItemFromGBank(itemID, _itemList[itemID]);
						if (ret >= 0)
						{
							_gbankItemThrottleSW.Restart();
							_withdrawCnt += ret;
						}
						_itemList[itemID] = ret < 0 ? 0 : _itemList[itemID] - ret;
						done = _itemList[itemID] <= 0;
					}
					if (done)
					{
						if (itemID == 0)
							PBLog.Log("Done withdrawing all items from {0} Bank", Bank);
						else
							PBLog.Log("Done withdrawing {0} itemID:{1} from {2} Bank", _withdrawCnt, itemID, Bank);
						_itemList.Remove(itemID);
						_withdrawCnt = 0;
					}
				}
				_itemsSW.Restart();
			}
		}

        private WoWObject GetLocalBanker()
        {
            WoWObject bank = null;
            List<WoWObject> bankers;
            if (Bank == BankType.Guild)
			{
				bankers = (from banker in ObjectManager.ObjectList
                           where
                               (banker is WoWGameObject &&
                                ((WoWGameObject) banker).SubType == WoWGameObjectType.GuildBank) ||
                               (banker is WoWUnit && ((WoWUnit) banker).IsGuildBanker && ((WoWUnit) banker).IsAlive &&
                                ((WoWUnit) banker).CanSelect)
                           select banker).ToList();
			}
            else
			{
				bankers = (from banker in ObjectManager.ObjectList
                           where (banker is WoWUnit &&
                                  ((WoWUnit) banker).IsBanker &&
                                  ((WoWUnit) banker).IsAlive &&
                                  ((WoWUnit) banker).CanSelect)
                           select banker).ToList();
			}

            if (!AutoFindBank && NpcEntry != 0)
                bank = bankers.Where(b => b.Entry == NpcEntry).OrderBy(o => o.Distance).FirstOrDefault();
            else if (AutoFindBank || _loc == WoWPoint.Zero || NpcEntry == 0)
                bank = bankers.OrderBy(o => o.Distance).FirstOrDefault();
            else if (StyxWoW.Me.Location.Distance(_loc) <= 90)
            {
                bank = bankers.Where(o => o.Location.Distance(_loc) < 10).
                    OrderBy(o => o.Distance).FirstOrDefault();
            }
            return bank;
        }

        private void MoveToBanker()
        {
            WoWPoint movetoPoint = _loc;
            WoWObject bank = GetLocalBanker();
            if (bank != null)
                movetoPoint = WoWMathHelper.CalculatePointFrom(Me.Location, bank.Location, 3);
                // search the database
            else if (movetoPoint == WoWPoint.Zero)
            {
                movetoPoint =
                    MoveToAction.GetLocationFromDB(
                        Bank == BankType.Personal
                            ? MoveToAction.MoveToType.NearestBanker
                            : MoveToAction.MoveToType.NearestGB, NpcEntry);
            }
            if (movetoPoint == WoWPoint.Zero)
            {
                IsDone = true;
                PBLog.Warn(Strings["Error_UnableToFindBank"]);
            }
            if (movetoPoint.Distance(StyxWoW.Me.Location) > 4)
            {
                Util.MoveTo(movetoPoint);
            }
                // since there are many personal bank replacement addons I can't just check if frame is open and be generic.. using events isn't reliable
            else if (bank != null)
            {
                bank.Interact();
            }
            else
            {
                IsDone = true;
                PBLog.Warn(Strings["Error_UnableToFindBank"]);
            }
        }

	    private Dictionary<uint, int> BuildItemList()
        {
            var items = new Dictionary<uint, int>();
            switch (GetItemfromBankType)
            {
                case BankWithdrawlItemType.SpecificItem:
                    //List<uint> idList = new List<uint>();
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
                            if (WithdrawAdditively)
                                items.Add(itemID, Withdraw == DepositWithdrawAmount.All ? int.MaxValue : Amount);
                            else
                                items.Add(itemID, Amount - Util.GetCarriedItemCount(itemID));
                        }
                    }
                    break;
                case BankWithdrawlItemType.Materials:
                    foreach (var kv in PB.MaterialList)
                        items.Add(kv.Key, kv.Value);
                    break;
            }
            return items;
        }

        // indexes are {0} = ItemID, {1} = amount to deposit

        /// <summary>
        /// Withdraws items from gbank
        /// </summary>
        /// <param name="id">item ID</param>
        /// <param name="amount">amount to withdraw.</param>
        /// <returns>the amount withdrawn.</returns>
        public int GetItemFromGBank(uint id, int amount)
        {
            if (_queueServerSW == null)
            {
                _queueServerSW = new Stopwatch();
                _queueServerSW.Start();
                Lua.DoString("for i=GetNumGuildBankTabs(), 1, -1 do QueryGuildBankTab(i) end ");
                PBLog.Log("Queuing server for gbank info");
                return 0;
            }
            if (_queueServerSW.ElapsedMilliseconds < 2000)
            {
                return 0;
            }
            string lua = string.Format(WithdrawItemFromGBankLuaFormat, id, amount);
            var retVal = Lua.GetReturnVal<int>(lua, 0);
            return retVal;
        }

        // indexes are {0} = ItemID, {1} = amount to deposit
        public bool GetItemFromBank(uint id, int amount)
        {
            string lua = string.Format(WithdrawItemFromPersonalBankLuaFormat, id, amount);
            Lua.DoString(lua);
            return true;
        }

        public override void Reset()
        {
            base.Reset();
            _queueServerSW = null;
            _itemList = null;
            _itemsSW = null;
	        _withdrawCnt = 0;
        }

		public override IPBComponent DeepCopy()
		{
			return new GetItemfromBankAction
			{
				ItemID = ItemID,
				Amount = Amount,
				Bank = Bank,
				GetItemfromBankType = GetItemfromBankType,
				_loc = _loc,
				AutoFindBank = AutoFindBank,
				NpcEntry = NpcEntry,
				Location = Location,
				MinFreeBagSlots = MinFreeBagSlots,
				WithdrawAdditively = WithdrawAdditively,
				Withdraw = Withdraw
			};
		}

    }
}