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
using HighVoltz.Professionbuddy.PropertyGridUtilities.Editors;
using Styx;
using Styx.CommonBot.Frames;
using Styx.Helpers;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace HighVoltz.Professionbuddy.Components
{
	[PBXmlElement("BuyItem", new[] { "BuyItemAction" })]
	public sealed class BuyItemAction : PBAction
	{
		public enum BuyItemActionType
		{
			SpecificItem,
			Material,
		}


		private const string BuyItemFormat =
			@"local amount={1}    
        local id={0}    
        local stackSize    
        local index = -1    
        local quantity    
        for i=1,GetMerchantNumItems() do    
            local link=GetMerchantItemLink(i)    
            if link and link:find(id) then    
                index=i    
                stackSize=GetMerchantItemMaxStack(i)    
                break 
            end    
        end    
        if index == -1 then return -1 end    
        while amount>0 do    
            if amount>=stackSize then    
                quantity=stackSize    
            else    
                quantity=amount    
            end    
            BuyMerchantItem(index, quantity)    
            amount=amount-quantity    
        end    
        return 1";

		private Stopwatch _concludingSw = new Stopwatch();
		// add pause at the end to give objectmanager a chance to update.

		private WoWPoint _loc;

		public BuyItemAction()
		{
			Properties["Location"] = new MetaProp(
				"Location",
				typeof (string),
				new EditorAttribute(
					typeof (LocationEditor),
					typeof (UITypeEditor)),
				new DisplayNameAttribute(Strings["Action_Common_Location"]));

			Properties["NpcEntry"] = new MetaProp(
				"NpcEntry",
				typeof (uint),
				new EditorAttribute(
					typeof (EntryEditor),
					typeof (UITypeEditor)),
				new DisplayNameAttribute(Strings["Action_Common_NpcEntry"]));

			Properties["ItemID"] = new MetaProp(
				"ItemID",
				typeof (string),
				new DisplayNameAttribute(Strings["Action_Common_ItemEntries"]));

			Properties["Count"] = new MetaProp(
				"Count",
				typeof (DynamicProperty<int>),
				new TypeConverterAttribute(
					typeof (DynamicProperty<int>.DynamivExpressionConverter)),
				new DisplayNameAttribute(Strings["Action_Common_Count"]));

			Properties["BuyItemType"] = new MetaProp(
				"BuyItemType",
				typeof (BuyItemActionType),
				new DisplayNameAttribute(Strings["Action_Common_Buy"]));

			Properties["BuyAdditively"] = new MetaProp(
				"BuyAdditively",
				typeof (bool),
				new DisplayNameAttribute(
					Strings["Action_Common_BuyAdditively"]));
			ItemID = "";
			Count = new DynamicProperty<int>("Count", this, "1"); // dynamic expression
			BuyItemType = BuyItemActionType.Material;
			_loc = WoWPoint.Zero;
			Location = _loc.ToInvariantString();
			NpcEntry = 0u;
			BuyAdditively = true;

			Properties["ItemID"].Show = false;
			Properties["Count"].Show = false;
			Properties["BuyAdditively"].Show = false;
			Properties["Location"].PropertyChanged += LocationChanged;
			Properties["BuyItemType"].PropertyChanged += BuyItemActionPropertyChanged;
		}

		[PBXmlAttribute]
		public uint NpcEntry
		{
			get { return Properties.GetValue<uint>("NpcEntry"); }
			set { Properties["NpcEntry"].Value = value; }
		}

		[PBXmlAttribute]
		public string Location
		{
			get { return Properties.GetValue<string>("Location"); }
			set { Properties["Location"].Value = value; }
		}

		[PBXmlAttribute( "ItemID", new[]{"Entry"} )]
		public string ItemID
		{
			get { return Properties.GetValue<string>("ItemID"); }
			set { Properties["ItemID"].Value = value; }
		}

		[PBXmlAttribute]
		public BuyItemActionType BuyItemType
		{
			get { return Properties.GetValue<BuyItemActionType>("BuyItemType"); }
			set { Properties["BuyItemType"].Value = value; }
		}

		[PBXmlAttribute]
		[TypeConverter(typeof (DynamicProperty<int>.DynamivExpressionConverter))]
		public DynamicProperty<int> Count
		{
			get { return Properties.GetValue<DynamicProperty<int>>("Count"); }
			set { Properties["Count"].Value = value; }
		}

		[PBXmlAttribute]
		public bool BuyAdditively
		{
			get { return Properties.GetValue<bool>("BuyAdditively"); }
			set { Properties["BuyAdditively"].Value = value; }
		}

		public override string Name
		{
			get { return Strings["Action_BuyItemAction_Name"]; }
		}

		public override string Title
		{
			get
			{
				return string.Format(
					"{0}: " + (BuyItemType == BuyItemActionType.SpecificItem ? "{1} x{2}" : "{3}"),
					Name,
					ItemID,
					Count,
					Strings["Action_Common_Material"]);
			}
		}

		public override string Help
		{
			get { return Strings["Action_BuyItemAction_Help"]; }
		}

		private void LocationChanged(object sender, MetaPropArgs e)
		{
			_loc = Util.StringToWoWPoint((string) ((MetaProp) sender).Value);
			Properties["Location"].PropertyChanged -= LocationChanged;
			Properties["Location"].Value = string.Format(CultureInfo.InvariantCulture, "{0}, {1}, {2}", _loc.X, _loc.Y, _loc.Z);
			Properties["Location"].PropertyChanged += LocationChanged;
			RefreshPropertyGrid();
		}

		private void BuyItemActionPropertyChanged(object sender, MetaPropArgs e)
		{
			switch (BuyItemType)
			{
				case BuyItemActionType.Material:
					Properties["ItemID"].Show = false;
					Properties["Count"].Show = false;
					Properties["BuyAdditively"].Show = false;
					break;
				case BuyItemActionType.SpecificItem:
					Properties["ItemID"].Show = true;
					Properties["Count"].Show = true;
					Properties["BuyAdditively"].Show = true;
					break;
			}
			RefreshPropertyGrid();
		}

		protected override async Task Run()
		{
			if (MerchantFrame.Instance == null || !MerchantFrame.Instance.IsVisible)
			{
				WoWPoint movetoPoint = _loc;
				WoWUnit unit = ObjectManager.GetObjectsOfType<WoWUnit>().Where(o => o.Entry == NpcEntry).
					OrderBy(o => o.Distance).FirstOrDefault();
				if (unit != null)
					movetoPoint = WoWMathHelper.CalculatePointFrom(Me.Location, unit.Location, 3);
				else if (movetoPoint == WoWPoint.Zero)
					movetoPoint = MoveToAction.GetLocationFromDB(MoveToAction.MoveToType.NpcByID, NpcEntry);
				if (movetoPoint != WoWPoint.Zero && StyxWoW.Me.Location.Distance(movetoPoint) > 4.5)
				{
					Util.MoveTo(movetoPoint);
				}
				else if (unit != null)
				{
					unit.Target();
					unit.Interact();
				}
				if (GossipFrame.Instance != null && GossipFrame.Instance.IsVisible &&
					GossipFrame.Instance.GossipOptionEntries != null)
				{
					foreach (GossipEntry ge in GossipFrame.Instance.GossipOptionEntries)
					{
						if (ge.Type == GossipEntry.GossipEntryType.Vendor)
						{
							GossipFrame.Instance.SelectGossipOption(ge.Index);
							break;
						}
					}
				}
			}
			else
			{
				// check if we have merchant frame open at correct NPC
				if (NpcEntry > 0 && Me.GotTarget && Me.CurrentTarget.Entry != NpcEntry)
				{
					MerchantFrame.Instance.Close();
					return;
				}
				if (!_concludingSw.IsRunning)
				{
					if (BuyItemType == BuyItemActionType.SpecificItem)
					{
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
							return;
						}
						foreach (uint id in idList)
						{
							int count = !BuyAdditively ? Math.Max(Count - Util.GetCarriedItemCount(id), 0) : Count;
							if (count > 0)
								BuyItem(id, (uint) count);
						}
					}
					else if (BuyItemType == BuyItemActionType.Material)
					{
						foreach (var kv in ProfessionbuddyBot.Instance.MaterialList)
						{
							// only buy items if we don't have enough in bags...
							int amount = kv.Value - (int) Ingredient.GetInBagItemCount(kv.Key);
							if (amount > 0)
								BuyItem(kv.Key, (uint) amount);
						}
					}
					_concludingSw.Start();
				}
				if (_concludingSw.ElapsedMilliseconds >= 2000)
				{
					PBLog.Log("BuyItemAction Completed");
					IsDone = true;
				}
			}
		}

		// Credits to Inrego
		// Index are {0}=ItemID, {1}=Amount
		// returns 1 if item is found, otherwise -1

		public static void BuyItem(uint id, uint count)
		{
			string lua = string.Format(BuyItemFormat, id, count);
			bool found = Lua.GetReturnVal<int>(lua, 0) == 1;
			PBLog.Log("item {0} {1}", id, found ? "bought " : "not found");
		}

		public override void Reset()
		{
			base.Reset();
			_concludingSw = new Stopwatch();
		}

		public override IPBComponent DeepCopy()
		{
			return new BuyItemAction
					{
						Count = Count,
						ItemID = ItemID,
						BuyItemType = BuyItemType,
						Location = Location,
						NpcEntry = NpcEntry,
						BuyAdditively = BuyAdditively
					};
		}
	}
}