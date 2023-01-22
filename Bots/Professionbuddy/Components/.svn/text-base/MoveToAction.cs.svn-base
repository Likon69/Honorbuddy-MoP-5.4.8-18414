using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Design;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HighVoltz.Professionbuddy.ComponentBase;
using HighVoltz.Professionbuddy.PropertyGridUtilities;
using HighVoltz.Professionbuddy.PropertyGridUtilities.Editors;
using Styx;
using Styx.CommonBot.Database;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace HighVoltz.Professionbuddy.Components
{
	[PBXmlElement("MoveTo", new[] { "MoveToAction" })]
	public sealed class MoveToAction : PBAction
	{
		#region MoveToType enum

		public enum MoveToType
		{
			Location,
			NearestMailbox,
			NearestVendor,
			NearestFlight,
			NearestAH,
			NearestRepair,
			NearestReagentVendor,
			NearestBanker,
			NearestGB,
			NpcByID
		}

		#endregion

		#region NavigationType enum

		public enum NavigationType
		{
			Navigator,
			ClickToMove
		};

		#endregion

		private const float SpeedModifer = 1.8f;
		private readonly Stopwatch _concludingSw = new Stopwatch();

		private WoWPoint _loc;
		private WoWPoint _locationDb = WoWPoint.Zero;

		public MoveToAction()
		{
			Properties["Entry"] = new MetaProp(
				"Entry",
				typeof (uint),
				new EditorAttribute(
					typeof (EntryEditor),
					typeof (UITypeEditor)),
				new DisplayNameAttribute(Strings["Action_Common_Entry"]));

			Properties["Location"] = new MetaProp(
				"Location",
				typeof (string),
				new EditorAttribute(
					typeof (LocationEditor),
					typeof (UITypeEditor)),
				new DisplayNameAttribute(Strings["Action_Common_Location"]));

			Properties["MoveType"] = new MetaProp(
				"MoveType",
				typeof (MoveToType),
				new DisplayNameAttribute(Strings["Action_MoveToAction_MoveToType"]));

			Properties["Pathing"] = new MetaProp(
				"Pathing",
				typeof (NavigationType),
				new DisplayNameAttribute(Strings["Action_Common_Use"]));

			Entry = 0u;
			_loc = WoWPoint.Zero;
			Location = _loc.ToInvariantString();
			MoveType = MoveToType.Location;
			Pathing = NavigationType.Navigator;

			Properties["Entry"].Show = false;
			Properties["MoveType"].PropertyChanged += MoveToActionPropertyChanged;
			Properties["Location"].PropertyChanged += LocationChanged;
		}

		[PBXmlAttribute]
		public string Location
		{
			get { return (string) Properties["Location"].Value; }
			set { Properties["Location"].Value = value; }
		}

		[PBXmlAttribute]
		public MoveToType MoveType
		{
			get { return (MoveToType) Properties["MoveType"].Value; }
			set { Properties["MoveType"].Value = value; }
		}

		[PBXmlAttribute]
		public NavigationType Pathing
		{
			get { return (NavigationType) Properties["Pathing"].Value; }
			set { Properties["Pathing"].Value = value; }
		}

		[PBXmlAttribute]
		public uint Entry
		{
			get { return (uint) Properties["Entry"].Value; }
			set { Properties["Entry"].Value = value; }
		}

		public override string Name
		{
			get { return Strings["Action_MoveToAction_Name"]; }
		}

		public override string Title
		{
			get
			{
				return string.Format(
					"{0}: {1} " +
					((MoveType == MoveToType.Location) ? Location : ""),
					Name,
					MoveType);
			}
		}

		public override string Help
		{
			get { return Strings["Action_MoveToAction_Help"]; }
		}

		private void LocationChanged(object sender, MetaPropArgs e)
		{
			_loc = Util.StringToWoWPoint(Location);
			Properties["Location"].PropertyChanged -= LocationChanged;
			Properties["Location"].Value = string.Format(CultureInfo.InvariantCulture, "{0}, {1}, {2}", _loc.X, _loc.Y, _loc.Z);
			Properties["Location"].PropertyChanged += LocationChanged;
			RefreshPropertyGrid();
		}

		private void MoveToActionPropertyChanged(object sender, MetaPropArgs e)
		{
			switch (MoveType)
			{
				case MoveToType.Location:
					Properties["Entry"].Show = false;
					Properties["Location"].Show = true;
					Properties["Pathing"].Show = true;
					break;
				case MoveToType.NpcByID:
					Properties["Entry"].Show = true;
					Properties["Location"].Show = false;
					Properties["Pathing"].Show = false;
					break;
				default:
					Properties["Entry"].Show = false;
					Properties["Location"].Show = false;
					Properties["Pathing"].Show = false;
					break;
			}
			RefreshPropertyGrid();
		}

		protected override async Task Run()
		{
			if (MoveType != MoveToType.Location)
			{
				_loc = GetLocationFromType(MoveType, Entry);
				if (_loc == WoWPoint.Zero)
				{
					if (_locationDb == WoWPoint.Zero)
					{
						_locationDb = GetLocationFromDB(MoveType, Entry);
					}
					_loc = _locationDb;
				}
				if (_loc == WoWPoint.Zero)
				{
					PBLog.Warn(Strings["Error_UnableToFindLocationFromDB"]);
					IsDone = true;
					return;
				}
			}
			if (Entry > 0 && (!StyxWoW.Me.GotTarget || StyxWoW.Me.CurrentTarget.Entry != Entry))
			{
				WoWUnit unit = ObjectManager.GetObjectsOfType<WoWUnit>(true).FirstOrDefault(u => u.Entry == Entry);
				if (unit != null)
					unit.Target();
			}
			float speed = StyxWoW.Me.MovementInfo.CurrentSpeed;
			Navigator.PathPrecision = speed > 7 ? (SpeedModifer*speed)/7f : SpeedModifer;
			if (StyxWoW.Me.Location.Distance(_loc) > 4.5)
			{
				if (Pathing == NavigationType.ClickToMove)
					WoWMovement.ClickToMove(_loc);
				else
					Util.MoveTo(_loc);
			}
			else
			{
				if (!_concludingSw.IsRunning)
					_concludingSw.Start();
				else if (_concludingSw.ElapsedMilliseconds >= 2000)
				{
					IsDone = true;
					PBLog.Log("MoveTo Action completed for type {0}", MoveType);
					_concludingSw.Stop();
					_concludingSw.Reset();
				}
			}
		}

		public static WoWPoint GetLocationFromType(MoveToType type, uint entry)
		{
			WoWObject obj = null;
			if (entry != 0)
			{
				obj = ObjectManager.ObjectList.FirstOrDefault(o => o.Entry == entry);
			}
			if (obj == null)
			{
				switch (type)
				{
					case MoveToType.NearestAH:
						obj =
							ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.IsAuctioneer && u.IsAlive).OrderBy(
								u => u.Distance).FirstOrDefault();
						break;
					case MoveToType.NearestBanker:
						obj =
							ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.IsBanker && u.IsAlive).OrderBy(
								u => u.Distance).FirstOrDefault();
						break;
					case MoveToType.NearestFlight:
						obj =
							ObjectManager.GetObjectsOfType<WoWUnit>().Where(
								u => u.IsFlightMaster && u.IsFriendly && u.IsAlive).OrderBy(u => u.Distance).
								FirstOrDefault();
						break;
					case MoveToType.NearestGB:
						obj = ObjectManager.ObjectList.Where(
							u =>
							{
								var unit = u as WoWUnit;
								if (unit != null)
								{
									var un = unit;
									if (un.IsGuildBanker)
										return true;
								}
								else
								{
									var o = u as WoWGameObject;
									if (o != null)
									{
										var go = o;
										if (go.SubType == WoWGameObjectType.GuildBank)
											return true;
									}
								}
								return false;
							}).OrderBy(u => u.Distance).FirstOrDefault();
						break;
					case MoveToType.NearestMailbox:
						obj =
							ObjectManager.GetObjectsOfType<WoWGameObject>().Where(
								u => u.SubType == WoWGameObjectType.Mailbox).OrderBy(u => u.Distance).FirstOrDefault();
						break;
					case MoveToType.NearestReagentVendor:
						obj =
							ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.IsReagentVendor && u.IsAlive).OrderBy
								(u => u.Distance).FirstOrDefault();
						break;
					case MoveToType.NearestRepair:
						obj =
							ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.IsRepairMerchant && u.IsAlive).
								OrderBy(u => u.Distance).FirstOrDefault();
						break;
					case MoveToType.NearestVendor:
						obj =
							ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.IsAnyVendor && u.IsAlive).OrderBy(
								u => u.Distance).FirstOrDefault();
						break;
				}
			}
			if (obj != null)
			{
				if (obj is WoWUnit && (!StyxWoW.Me.GotTarget || StyxWoW.Me.CurrentTarget != obj))
				{
					((WoWUnit) obj).Target();
				}
				return obj.Location;
			}
			return WoWPoint.Zero;
		}

		public static WoWPoint GetLocationFromDB(MoveToType type, uint entry)
		{
			NpcResult npcResults = null;
			switch (type)
			{
				case MoveToType.NearestAH:
					npcResults = NpcQueries.GetNearestNpc(
						StyxWoW.Me.MapId,
						StyxWoW.Me.Location,
						UnitNPCFlags.Auctioneer);
					break;
				case MoveToType.NearestBanker:
					npcResults = NpcQueries.GetNearestNpc(
						StyxWoW.Me.MapId,
						StyxWoW.Me.Location,
						UnitNPCFlags.Banker);
					break;
				case MoveToType.NearestFlight:
					npcResults = NpcQueries.GetNearestNpc(
						StyxWoW.Me.MapId,
						StyxWoW.Me.Location,
						UnitNPCFlags.Flightmaster);
					break;
				case MoveToType.NearestGB:
					npcResults = NpcQueries.GetNearestNpc(
						StyxWoW.Me.MapId,
						StyxWoW.Me.Location,
						UnitNPCFlags.GuildBanker);
					break;
				case MoveToType.NearestReagentVendor:
					npcResults = NpcQueries.GetNearestNpc(
						StyxWoW.Me.MapId,
						StyxWoW.Me.Location,
						UnitNPCFlags.ReagentVendor);
					break;
				case MoveToType.NearestRepair:
					npcResults = NpcQueries.GetNearestNpc(
						StyxWoW.Me.MapId,
						StyxWoW.Me.Location,
						UnitNPCFlags.Repair);
					break;
				case MoveToType.NearestVendor:
					npcResults = NpcQueries.GetNearestNpc(
						StyxWoW.Me.MapId,
						StyxWoW.Me.Location,
						UnitNPCFlags.Vendor);
					break;
				case MoveToType.NpcByID:
					npcResults = NpcQueries.GetNpcById(entry);
					break;
			}
			if (npcResults != null)
				return npcResults.Location;
			return WoWPoint.Zero;
		}

		public override IPBComponent DeepCopy()
		{
			return new MoveToAction
					{MoveType = MoveType, Entry = Entry, _loc = _loc, Location = Location, Pathing = Pathing};
		}
	}

}