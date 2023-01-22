using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HighVoltz.Professionbuddy.ComponentBase;
using HighVoltz.Professionbuddy.Dynamic;
using HighVoltz.Professionbuddy.PropertyGridUtilities;
using HighVoltz.Professionbuddy.PropertyGridUtilities.Editors;
using Styx;
using Styx.CommonBot.Database;
using Styx.CommonBot.Frames;
using Styx.CommonBot.Profiles;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace HighVoltz.Professionbuddy.Components
{
	[PBXmlElement("SellItem", new[] { "SellItemAction" })]
	public sealed class SellItemAction : PBAction
	{
		public enum SellItemActionType
		{
			Specific,
			Greys,
			Whites,
			Greens,
			Blues,
			Epics,
		}

		private uint _entry;
		private WoWPoint _loc;

		public SellItemAction()
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
				new DisplayNameAttribute(Strings["Action_Common_ItemEntry"]));

			Properties["Count"] = new MetaProp(
				"Count",
				typeof (DynamicProperty<int>),
				new TypeConverterAttribute(
					typeof (DynamicProperty<int>.DynamivExpressionConverter)),
				new DisplayNameAttribute(Strings["Action_Common_Count"]));

			Properties["SellItemType"] = new MetaProp(
				"SellItemType",
				typeof (SellItemActionType),
				new DisplayNameAttribute(
					Strings["Action_SellItemAction_SellItemType"]));

			Properties["Sell"] = new MetaProp(
				"Sell",
				typeof (DepositWithdrawAmount),
				new DisplayNameAttribute(Strings["Action_Common_Sell"]));

			ItemID = "";
			Count = new DynamicProperty<int>("Count", this, "0");
			_loc = WoWPoint.Zero;
			Location = _loc.ToInvariantString();
			NpcEntry = 0u;
			Sell = DepositWithdrawAmount.All;

			Properties["Location"].PropertyChanged += LocationChanged;
			Properties["SellItemType"].Value = SellItemActionType.Specific;
			Properties["SellItemType"].PropertyChanged += SellItemActionPropertyChanged;
			Properties["Sell"].PropertyChanged += SellChanged;
		}

		[PBXmlAttribute]
		public DepositWithdrawAmount Sell
		{
			get { return Properties.GetValue<DepositWithdrawAmount>("Sell"); }
			set { Properties["Sell"].Value = value; }
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

		[PBXmlAttribute]
		public SellItemActionType SellItemType
		{
			get { return Properties.GetValue<SellItemActionType>("SellItemType"); }
			set { Properties["SellItemType"].Value = value; }
		}

		[PBXmlAttribute]
		public string ItemID
		{
			get { return Properties.GetValue<string>("ItemID"); }
			set { Properties["ItemID"].Value = value; }
		}

		[PBXmlAttribute]
		[TypeConverter(typeof (DynamicProperty<int>.DynamivExpressionConverter))]
		public DynamicProperty<int> Count
		{
			get { return Properties.GetValue<DynamicProperty<int>>("Count"); }
			set { Properties["Count"].Value = value; }
		}

		public override string Name
		{
			get { return Strings["Action_SellItemAction_Name"]; }
		}

		public override string Title
		{
			get
			{
				return string.Format(
					"({0}) " +
					(SellItemType == SellItemActionType.Specific
						? ItemID.ToString(CultureInfo.InvariantCulture) + " x{1} "
						: SellItemType.ToString()),
					Name,
					Count);
			}
		}

		public override string Help
		{
			get { return Strings["Action_SellItemAction_Help"]; }
		}

		private void SellChanged(object sender, MetaPropArgs e)
		{
			Properties["Sell"].Show = Sell == DepositWithdrawAmount.Amount;
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

		private void SellItemActionPropertyChanged(object sender, MetaPropArgs e)
		{
			switch (SellItemType)
			{
				case SellItemActionType.Specific:
					Properties["Count"].Show = true;
					Properties["ItemID"].Show = true;
					break;
				default:
					Properties["Count"].Show = false;
					Properties["ItemID"].Show = false;
					break;
			}
			RefreshPropertyGrid();
		}

		protected override async Task Run()
		{
			if (MerchantFrame.Instance == null || !MerchantFrame.Instance.IsVisible)
			{
				WoWPoint movetoPoint = _loc;
				if (_entry == 0)
					_entry = NpcEntry;
				if (_entry == 0)
				{
					MoveToAction.GetLocationFromDB(MoveToAction.MoveToType.NearestVendor, 0);
					NpcResult npcResults = NpcQueries.GetNearestNpc(
						StyxWoW.Me.MapId,
						StyxWoW.Me.Location,
						UnitNPCFlags.Vendor);
					_entry = (uint) npcResults.Entry;
					movetoPoint = npcResults.Location;
				}
				WoWUnit unit = ObjectManager.GetObjectsOfType<WoWUnit>().Where(o => o.Entry == _entry).
					OrderBy(o => o.Distance).FirstOrDefault();
				if (unit != null)
					movetoPoint = unit.Location;
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
							return;
						}
					}
				}
			}
			else
			{
				if (SellItemType == SellItemActionType.Specific)
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
					List<WoWItem> itemList = StyxWoW.Me.BagItems.Where(u => idList.Contains(u.Entry)).
						Take(Sell == DepositWithdrawAmount.All ? int.MaxValue : Count).ToList();
					using (StyxWoW.Memory.AcquireFrame())
					{
						foreach (WoWItem item in itemList)
							item.UseContainerItem();
					}
				}
				else
				{
					List<WoWItem> itemList = null;
					IEnumerable<WoWItem> itemQuery = from item in Me.BagItems
						where !ProtectedItemsManager.Contains(item.Entry)
							&& !ProfessionbuddyBot.Instance.TradeskillTools.Contains(item.Entry)
						select item;
					switch (SellItemType)
					{
						case SellItemActionType.Greys:
							itemList = itemQuery.Where(i => i.Quality == WoWItemQuality.Poor).ToList();
							break;
						case SellItemActionType.Whites:
							itemList = itemQuery.Where(i => i.Quality == WoWItemQuality.Common).ToList();
							break;
						case SellItemActionType.Greens:
							itemList = itemQuery.Where(i => i.Quality == WoWItemQuality.Uncommon).ToList();
							break;
						case SellItemActionType.Blues:
							itemList = itemQuery.Where(i => i.Quality == WoWItemQuality.Rare).ToList();
							break;
					}
					if (itemList != null)
					{
						using (StyxWoW.Memory.AcquireFrame())
						{
							foreach (WoWItem item in itemList)
							{
								item.UseContainerItem();
							}
						}
					}
				}
				PBLog.Log("SellItemAction Completed for {0}", ItemID);
				IsDone = true;
			}
		}

		public override void Reset()
		{
			base.Reset();
			_entry = 0;
		}

		public override IPBComponent DeepCopy()
		{
			return new SellItemAction
					{
						Count = Count,
						ItemID = ItemID,
						SellItemType = SellItemType,
						NpcEntry = NpcEntry,
						Location = Location,
						Sell = Sell
					};
		}

	}
}