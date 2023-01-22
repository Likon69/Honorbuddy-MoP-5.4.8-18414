using System;
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
using HighVoltz.Professionbuddy.PropertyGridUtilities.Converters;
using HighVoltz.Professionbuddy.PropertyGridUtilities.Editors;
using Styx;
using Styx.Helpers;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace HighVoltz.Professionbuddy.Components
{
    internal struct BuyItemEntry
    {
        public uint BuyAmount; // amount to buy
        public uint Id; // item ID 
        public string Name; // localized name
    }

	[PBXmlElement("BuyItemFromAH", new[] { "BuyItemFromAhAction" })]
    public sealed class BuyItemFromAhAction : PBAction
    {
        public enum ItemType
        {
            Item,
            RecipeMats,
            MaterialList,
        }

        private const string BuyFromAHLuaFormat = @"
        local A,totalA= GetNumAuctionItems('list')  
        local amountBought={0}  
        local want={1}  
        local each={3}  
        local useBid={4}  
        local buyPrice=0  
		local me = UnitName('player')
        for index=1, A do  
           local name,_,cnt,_,_,_,_,minBid,minInc,buyout,bidNum,highBidder,highBidderFullName,owner,ownerFullName,sold,id=GetAuctionItemInfo('list', index) 
           if useBid == 1 and buyout > each*cnt and (highBidder == nil or highBidder ~= me or highBidderFullName ~= nil) then  
              if bidNum == nil then  
                 buyPrice = minBid + minInc  
              else  
                 buyPrice = bidNum + minInc  
              end  
           else  
              buyPrice = buyout  
           end  
           if id == {2} and buyPrice > 0 and buyPrice <= each*cnt and amountBought < want and me ~= owner then  
              amountBought = amountBought + cnt  
              PlaceAuctionBid('list', index,buyPrice)  
           end  
           if amountBought >=  want then return -1 end  
        end  
        return amountBought  
";

        private int _counter;
        private WoWPoint _loc;
        private int _page;
        private Stopwatch _queueTimer = new Stopwatch();
        private List<BuyItemEntry> _toBuyList;
        private List<BuyItemEntry> _toQueryNameList;
        private int _totalAuctions;

        public BuyItemFromAhAction()
        {
            Properties["ItemID"] = new MetaProp("ItemID", typeof (string),
                                                new DisplayNameAttribute(Strings["Action_Common_ItemEntries"]));

            Properties["MaxBuyout"] = new MetaProp("MaxBuyout", typeof (GoldEditor),
                                                   new TypeConverterAttribute(typeof (GoldEditorConverter)),
                                                   new DisplayNameAttribute(Strings["Action_Common_MaxBuyout"]));

            Properties["Amount"] = new MetaProp("Amount", typeof (DynamicProperty<int>),
                                                new TypeConverterAttribute(
                                                    typeof (DynamicProperty<int>.DynamivExpressionConverter)),
                                                new DisplayNameAttribute(Strings["Action_Common_Amount"]));

            Properties["ItemListType"] = new MetaProp("ItemListType", typeof (ItemType),
                                                      new DisplayNameAttribute(Strings["Action_Common_Buy"]));

            Properties["AutoFindAh"] = new MetaProp("AutoFindAh", typeof (bool),
                                                    new DisplayNameAttribute(Strings["Action_Common_AutoFindAH"]));

            Properties["BuyAdditively"] = new MetaProp("BuyAdditively", typeof (bool),
                                                       new DisplayNameAttribute(
                                                           Strings["Action_Common_BuyAdditively"]));

            Properties["BidOnItem"] = new MetaProp("BidOnItem", typeof (bool),
                                                   new DisplayNameAttribute(
                                                       Strings["Action_BuyItemFromAhAction_BidOnItem"]));

            Properties["Location"] = new MetaProp("Location", typeof (string),
                                                  new EditorAttribute(typeof (LocationEditor),
                                                                      typeof (UITypeEditor)),
                                                  new DisplayNameAttribute(Strings["Action_Common_Location"]));

            ItemID = "";
            Amount = new DynamicProperty<int>("Amount", this, "1");
            ItemListType = ItemType.Item;
            AutoFindAh = true;
            _loc = WoWPoint.Zero;
            Location = _loc.ToInvariantString();
            MaxBuyout = new GoldEditor("100g0s0c");
            BidOnItem = false;
            BuyAdditively = true;

            Properties["AutoFindAh"].PropertyChanged += AutoFindAHChanged;
            Properties["ItemListType"].PropertyChanged += BuyItemFromAhActionPropertyChanged;
            Properties["Location"].PropertyChanged += LocationChanged;
            Properties["Amount"].Show = true;
            Properties["Location"].Show = false;
        }

        [PBXmlAttribute]
        public ItemType ItemListType
        {
			get { return Properties.GetValue<ItemType>("ItemListType"); }
            set { Properties["ItemListType"].Value = value; }
        }

        [PBXmlAttribute]
        public string ItemID
        {
            get { return Properties.GetValue<string>("ItemID"); }
            set { Properties["ItemID"].Value = value; }
        }

        [PBXmlAttribute]
        [TypeConverter(typeof (GoldEditorConverter))]
        public GoldEditor MaxBuyout
        {
            get { return Properties.GetValue<GoldEditor>("MaxBuyout"); }
            set { Properties["MaxBuyout"].Value = value; }
        }

        [PBXmlAttribute]
        [TypeConverter(typeof (DynamicProperty<int>.DynamivExpressionConverter))]
        public DynamicProperty<int> Amount
        {
	        get { return Properties.GetValue<DynamicProperty<int>>("Amount"); }
	        set { Properties["Amount"].Value = value; }
        }

        [PBXmlAttribute]
        public bool BuyAdditively
        {
            get { return Properties.GetValue<bool>("BuyAdditively"); }
            set { Properties["BuyAdditively"].Value = value; }
        }

        [PBXmlAttribute]
        public bool AutoFindAh
        {
            get { return (bool) Properties["AutoFindAh"].Value; }
            set { Properties["AutoFindAh"].Value = value; }
        }

        [PBXmlAttribute]
        public bool BidOnItem
        {
            get { return (bool) Properties["BidOnItem"].Value; }
            set { Properties["BidOnItem"].Value = value; }
        }

        [PBXmlAttribute]
        public string Location
        {
            get { return (string) Properties["Location"].Value; }
            set { Properties["Location"].Value = value; }
        }

        public override string Name
        {
            get { return Strings["Action_BuyItemFromAhAction_Name"]; }
        }

        public override string Title
        {
            get
            {
                return string.Format("{0}: {1} " + (ItemListType != ItemType.MaterialList ? "x" + Amount : ""), Name,
                                     ItemListType != ItemType.MaterialList
                                         ? ItemID
                                         : Strings["Action_Common_Material"]);
            }
        }

        public override string Help
        {
            get { return Strings["Action_BuyItemFromAhAction_Help"]; }
        }

        private void LocationChanged(object sender, MetaPropArgs e)
        {
            _loc = Util.StringToWoWPoint((string) ((MetaProp) sender).Value);
            Properties["Location"].PropertyChanged -= LocationChanged;
            Properties["Location"].Value = string.Format(CultureInfo.InvariantCulture, "{0}, {1}, {2}", _loc.X, _loc.Y, _loc.Z);
            Properties["Location"].PropertyChanged += LocationChanged;
            RefreshPropertyGrid();
        }

        private void AutoFindAHChanged(object sender, MetaPropArgs e)
        {
            Properties["Location"].Show = !AutoFindAh;
            RefreshPropertyGrid();
        }

        private void BuyItemFromAhActionPropertyChanged(object sender, MetaPropArgs e)
        {
            if (ItemListType == ItemType.MaterialList)
            {
                Properties["ItemID"].Show = false;
                Properties["Amount"].Show = false;
                Properties["BuyAdditively"].Show = false;
            }
            else
            {
                Properties["ItemID"].Show = true;
                Properties["Amount"].Show = true;
                Properties["BuyAdditively"].Show = true;
            }
            RefreshPropertyGrid();
        }


	    protected async override Task Run()
		{
			using (StyxWoW.Memory.AcquireFrame())
			{
				if (
					Lua.GetReturnVal<int>(
						"if AuctionFrame and AuctionFrame:IsVisible() then return 1 else return 0 end ", 0) == 0)
				{
					MovetoAuctioneer();
				}
				else
				{
					if (_toQueryNameList == null)
					{
						_toQueryNameList = BuildItemList();
						_toBuyList = new List<BuyItemEntry>();
					}
					if ((_toQueryNameList == null || _toQueryNameList.Count == 0) && _toBuyList.Count == 0)
					{
						IsDone = true;
						return;
					}
					if (_toQueryNameList != null && _toQueryNameList.Count > 0)
					{
						string name = GetLocalName(_toQueryNameList[0].Id);
						if (!string.IsNullOrEmpty(name))
						{
							BuyItemEntry item = _toQueryNameList[0];
							item.Name = name;
							_toBuyList.Add(item);
							_toQueryNameList.RemoveAt(0);
						}
					}
					if (_toBuyList.Count > 0)
					{
						if (BuyFromAH(_toBuyList[0]))
						{
							_toBuyList.RemoveAt(0);
						}
					}
				}
			}
		}

        //indexs are {0}=ItemsCounter,NumOfItemToBuy, {1}=ItemID, {2}=maxBuyout, {3}=BidOnItem ? 1 : 0

        private bool BuyFromAH(BuyItemEntry bie)
        {
            bool done = false;
            if (!_queueTimer.IsRunning)
            {
                string lua = string.Format("QueryAuctionItems(\"{0}\" ,nil,nil,nil,nil,nil,{1}) return 1",
                                           bie.Name.ToFormatedUTF8(), _page);
                Lua.GetReturnVal<int>(lua, 0);
                ProfessionbuddyBot.Debug("Searching AH for {0}", bie.Name);
                _queueTimer.Start();
            }
            else if (_queueTimer.ElapsedMilliseconds <= 10000)
            {
                if (Lua.GetReturnVal<int>("if CanSendAuctionQuery('list') == 1 then return 1 else return 0 end ", 0) ==
                    1)
                {
                    _totalAuctions = Lua.GetReturnVal<int>("return GetNumAuctionItems('list')", 1);
                    _queueTimer.Stop();
                    _queueTimer.Reset();
                    if (_totalAuctions > 0)
                    {
                        string lua = string.Format(BuyFromAHLuaFormat,
                                                   _counter, bie.BuyAmount, bie.Id, MaxBuyout.TotalCopper,
                                                   BidOnItem ? 1 : 0);
                        _counter = Lua.GetReturnVal<int>(lua, 0);
                        if (_counter == -1 || ++_page >= (int) Math.Ceiling((double) _totalAuctions/50))
                            done = true;
                    }
                    else
                        done = true;
                }
            }
            else
            {
                done = true;
            }
            if (done)
            {
                _queueTimer = new Stopwatch();
                _totalAuctions = 0;
                _counter = 0;
                _page = 0;
            }
            return done;
        }

        private void MovetoAuctioneer()
        {
            WoWPoint movetoPoint = _loc;
            WoWUnit auctioneer;
            if (AutoFindAh || movetoPoint == WoWPoint.Zero)
            {
                auctioneer = ObjectManager.GetObjectsOfType<WoWUnit>().Where(o => o.IsAuctioneer && o.IsAlive)
                    .OrderBy(o => o.Distance).FirstOrDefault();
            }
            else
            {
                auctioneer = ObjectManager.GetObjectsOfType<WoWUnit>().Where(o => o.IsAuctioneer
                                                                                  && o.Location.Distance(_loc) < 5)
                    .OrderBy(o => o.Distance).FirstOrDefault();
            }
            if (auctioneer != null)
                movetoPoint = WoWMathHelper.CalculatePointFrom(Me.Location, auctioneer.Location, 3);
            else if (movetoPoint == WoWPoint.Zero)
                movetoPoint = MoveToAction.GetLocationFromDB(MoveToAction.MoveToType.NearestAH, 0);
            if (movetoPoint == WoWPoint.Zero)
            {
                PBLog.Warn(Strings["Error_UnableToFindAuctioneer"]);
            }
            if (movetoPoint.Distance(StyxWoW.Me.Location) > 4.5)
            {
                Util.MoveTo(movetoPoint);
            }
            else if (auctioneer != null)
            {
                auctioneer.Interact();
            }
        }

        private string GetLocalName(uint id)
        {
            ProfessionbuddyBot.Debug("Queueing server for Item: {0}", id);
            return Util.GetItemCacheName(id);
        }

        private List<BuyItemEntry> BuildItemList()
        {
            var list = new List<BuyItemEntry>();
            var idList = new List<uint>();
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
                    idList.Add(itemID);
                }
            }
            else
            {
                PBLog.Warn(Strings["Error_NoItemEntries"]);
                IsDone = true;
            }

            switch (ItemListType)
            {
                case ItemType.Item:
		            list.AddRange(
			            from id in idList
			            let amount = !BuyAdditively ? Amount - Util.GetCarriedItemCount(id) : Amount
			            where amount > 0
			            select new BuyItemEntry {BuyAmount = (uint) amount, Id = id});
                    break;
                case ItemType.MaterialList:
                    list.AddRange(
                        PB.MaterialList.Select(kv => new BuyItemEntry {Id = kv.Key, BuyAmount = (uint) kv.Value}));
                    break;
                case ItemType.RecipeMats:
                    list.AddRange(from id in idList
                                  select (from tradeskill in PB.TradeSkillList
                                          where tradeskill.KnownRecipes.ContainsKey(id)
                                          select tradeskill.KnownRecipes[id]).FirstOrDefault()
                                  into recipe
                                  where recipe != null
                                  from ingred in recipe.Ingredients
                                  let toBuyAmount =
                                      (int) ((ingred.Required*Amount) - Ingredient.GetInBagItemCount(ingred.ID))
                                  where toBuyAmount > 0
                                  select new BuyItemEntry {Id = ingred.ID, BuyAmount = (uint) toBuyAmount});
                    break;
            }
            return list;
        }

        public override void Reset()
        {
            base.Reset();
            _toQueryNameList = null;
            _toBuyList = null;
        }

	    public override IPBComponent DeepCopy()
	    {
			return new BuyItemFromAhAction
			{
				ItemID = ItemID,
				MaxBuyout = new GoldEditor(MaxBuyout.ToString()),
				Amount = Amount,
				ItemListType = ItemListType,
				AutoFindAh = AutoFindAh,
				Location = Location,
				BidOnItem = BidOnItem,
				BuyAdditively = BuyAdditively,
			};
	    }
    }

}