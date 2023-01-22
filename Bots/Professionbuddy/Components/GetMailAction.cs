using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Design;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HighVoltz.Professionbuddy.ComponentBase;
using HighVoltz.Professionbuddy.PropertyGridUtilities;
using HighVoltz.Professionbuddy.PropertyGridUtilities.Converters;
using HighVoltz.Professionbuddy.PropertyGridUtilities.Editors;
using Styx;
using Styx.CommonBot.Frames;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace HighVoltz.Professionbuddy.Components
{
	[PBXmlElement("GetMail", new[] { "GetMailAction" })]
	public sealed class GetMailAction : PBAction
	{
		#region GetMailActionType enum

		public enum GetMailActionType
		{
			AllItems,
			Specific,
		}

		#endregion

		private const string MailFormat = @"
            local numItems,totalItems = GetInboxNumItems()  
            local foundMail=0  
            local newMailCheck = {0}  
            local maxCopperCOD = {1}
            for index=numItems,1,-1 do  
               local _,_,sender,subj,gold,cod,_,itemCnt,_,_,hasText,canReply,IsGM=GetInboxHeaderInfo(index)  
               if sender ~= nil and cod == 0 and itemCnt == nil and gold == 0 and canReply == nil and IsGM == nil then  
                  DeleteInboxItem(index)  
               end  
               local itemStackCnt = 0
               for i=1, ATTACHMENTS_MAX_RECEIVE do
                  local   _,_,cnt = GetInboxItem(index,i)
                  itemStackCnt = itemStackCnt + cnt
               end
               if ((itemCnt and itemCnt >0) or (gold and gold > 0)) and (cod == 0 or maxCopperCOD and maxCopperCOD >= cod/itemStackCnt ) then  
                  for i=1,ATTACHMENTS_MAX_RECEIVE do  
                     if gold and gold > 0 then TakeInboxMoney(index) foundMail = 1 break end  
                     if GetInboxItem(index,i) ~= nil then  
                        TakeInboxItem (index,i)  
                        foundMail = 1  
                        break  
                     end  
                  end  
               end  
               if foundMail == 1 then break end  
            end  
            local beans = BeanCounterMail and BeanCounterMail:IsVisible()  
            if foundMail == 0 and ((newMailCheck == 1 and HasNewMail() == nil) or newMailCheck ==0 ) and totalItems == numItems and beans ~= 1 then return 1 else return 0 end   
        ";

		// format index. {0} = ItemID {1}=CheckForNewMail which can be only 1 or 0
		private const string MailByIdFormat = @"
            local numItems,totalItems = GetInboxNumItems()  
            local foundMail=0  
            local newMailCheck = {1}  
            local maxCopperCOD = {2}
            for index=numItems,1,-1 do  
               local _,_,sender,subj,gold,cod,_,itemCnt,_,_,hasText=GetInboxHeaderInfo(index)  
               if sender ~= nil and cod == 0 and itemCnt == nil and gold == 0 and hasText == nil then  
                  DeleteInboxItem(index)  
               end  
               local itemStackCnt = 0
               for i=1, ATTACHMENTS_MAX_RECEIVE do
                  local itemlink = GetInboxItemLink(index, i2)  
                  if itemlink ~= nil and string.find(itemlink,'{0}') then 
                        local   _,_,cnt = GetInboxItem(index,i)
                        itemStackCnt = itemStackCnt + cnt
                  end
               end
               if itemCnt and itemCnt >0 and (cod == 0 or maxCopperCOD and maxCopperCOD >= cod/itemStackCnt ) then  
                  for i2=1, ATTACHMENTS_MAX_RECEIVE do  
                     local itemlink = GetInboxItemLink(index, i2)  
                     if itemlink ~= nil and string.find(itemlink,'{0}') then  
                        foundMail = foundMail + 1  
                        TakeInboxItem(index, i2)  
                        break  
                     end  
                  end  
               end  
               if foundMail == 1 then break end  
            end  
            if (foundMail == 0 and ((newMailCheck == 1 and HasNewMail() == nil) or newMailCheck ==0 )) or (foundMail == 0 and (numItems == 50 and totalItems >= 50)) then return 1 else return 0 end  
";

		private Stopwatch _concludingSW = new Stopwatch();
		private List<uint> _idList;
		private WoWPoint _loc;
		private WoWGameObject _mailbox;
		private Stopwatch _refreshInboxSW = new Stopwatch();
		private Stopwatch _throttleSW = new Stopwatch();
		private Stopwatch _timeoutSW = new Stopwatch();
		private Stopwatch _waitForContentToShowSW = new Stopwatch();

		public GetMailAction()
		{
			//CheckNewMail
			Properties["ItemID"] = new MetaProp(
				"ItemID",
				typeof (string),
				new DisplayNameAttribute(Strings["Action_Common_ItemEntries"]));

			Properties["MinFreeBagSlots"] = new MetaProp(
				"MinFreeBagSlots",
				typeof (int),
				new DisplayNameAttribute(
					Strings["Action_Common_MinFreeBagSlots"]));

			Properties["CheckNewMail"] = new MetaProp(
				"CheckNewMail",
				typeof (bool),
				new DisplayNameAttribute(
					Strings["Action_GetMailAction_CheckNewMail"]));

			Properties["GetMailType"] = new MetaProp(
				"GetMailType",
				typeof (GetMailActionType),
				new DisplayNameAttribute(Strings["Action_GetMailAction_Name"]));

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

			Properties["MaxCODAmount"] = new MetaProp(
				"MaxCODAmount",
				typeof (GoldEditor),
				new TypeConverterAttribute(typeof (GoldEditorConverter)),
				new DisplayNameAttribute(Strings["Action_Common_MaxCODPrice"]));

			ItemID = "";
			CheckNewMail = true;
			MaxCODAmount = new GoldEditor("0g0s0c");
			GetMailType = GetMailActionType.AllItems;
			AutoFindMailBox = true;
			_loc = WoWPoint.Zero;
			Location = _loc.ToInvariantString();
			MinFreeBagSlots = 0;

			Properties["GetMailType"].PropertyChanged += GetMailActionPropertyChanged;
			Properties["AutoFindMailBox"].PropertyChanged += AutoFindMailBoxChanged;
			Properties["ItemID"].Show = false;
			Properties["Location"].Show = false;
			Properties["Location"].PropertyChanged += LocationChanged;
		}

		[PBXmlAttribute]
		public GetMailActionType GetMailType
		{
			get { return Properties.GetValue<GetMailActionType>("GetMailType"); }
			set { Properties["GetMailType"].Value = value; }
		}

		[PBXmlAttribute("ItemID", new[] {"Entry"})]
		public string ItemID
		{
			get { return Properties.GetValue<string>("ItemID"); }
			set { Properties["ItemID"].Value = value; }
		}

		[PBXmlAttribute]
		public bool CheckNewMail
		{
			get { return Properties.GetValue<bool>("CheckNewMail"); }
			set { Properties["CheckNewMail"].Value = value; }
		}

		[PBXmlAttribute]
		[TypeConverter(typeof (GoldEditorConverter))]
		public GoldEditor MaxCODAmount
		{
			get { return Properties.GetValue<GoldEditor>("MaxCODAmount"); }
			set { Properties["MaxCODAmount"].Value = value; }
		}

		[PBXmlAttribute]
		public int MinFreeBagSlots
		{
			get { return Properties.GetValue<int>("MinFreeBagSlots"); }
			set { Properties["MinFreeBagSlots"].Value = value; }
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

		public override string Name
		{
			get { return Strings["Action_GetMailAction_Name"]; }
		}

		public override string Title
		{
			get
			{
				return string.Format(
					"{0}: {1} " + (GetMailType == GetMailActionType.Specific
						? " - " +
						ItemID
						: ""),
					Name,
					GetMailType);
			}
		}

		public override string Help
		{
			get { return Strings["Action_GetMailAction_Help"]; }
		}

		private void LocationChanged(object sender, MetaPropArgs e)
		{
			_loc = Util.StringToWoWPoint((string) ((MetaProp) sender).Value);
			Properties["Location"].PropertyChanged -= LocationChanged;
			Properties["Location"].Value = string.Format(CultureInfo.InvariantCulture, "{0}, {1}, {2}", _loc.X, _loc.Y, _loc.Z);
			Properties["Location"].PropertyChanged += LocationChanged;
			RefreshPropertyGrid();
		}

		private void AutoFindMailBoxChanged(object sender, MetaPropArgs e)
		{
			Properties["Location"].Show = !AutoFindMailBox;
			RefreshPropertyGrid();
		}

		private void GetMailActionPropertyChanged(object sender, MetaPropArgs e)
		{
			Properties["ItemID"].Show = GetMailType != GetMailActionType.AllItems;
			RefreshPropertyGrid();
		}


		protected override async Task Run()
		{
			if (!_timeoutSW.IsRunning)
				_timeoutSW.Start();

			if (_timeoutSW.ElapsedMilliseconds > 300000)
				IsDone = true;

			if (MailFrame.Instance == null || !MailFrame.Instance.IsVisible)
			{
				WoWPoint movetoPoint = _loc;
				if (AutoFindMailBox || movetoPoint == WoWPoint.Zero)
				{
					_mailbox =
						ObjectManager.GetObjectsOfType<WoWGameObject>().Where(
							o => o.SubType == WoWGameObjectType.Mailbox && o.CanUse())
							.OrderBy(o => o.DistanceSqr).FirstOrDefault();
				}
				else
				{
					_mailbox =
						ObjectManager.GetObjectsOfType<WoWGameObject>().Where(
							o => o.SubType == WoWGameObjectType.Mailbox
								&& o.Location.Distance(_loc) < 10 && o.CanUse())
							.OrderBy(o => o.DistanceSqr).FirstOrDefault();
				}
				if (_mailbox != null)
					movetoPoint = _mailbox.Location;
				if (movetoPoint == WoWPoint.Zero)
				{
					PBLog.Warn(Strings["Error_UnableToFindMailbox"]);
					return;
				}

				if (_mailbox == null || !_mailbox.WithinInteractRange)
				{
					Util.MoveTo(movetoPoint);
				}
				else if (_mailbox != null)
				{
					if (Me.IsMoving)
						WoWMovement.MoveStop();
					_mailbox.Interact();
				}
				return;
			}
			// mail frame is open.
			if (_idList == null)
				_idList = BuildItemList();
			if (!_refreshInboxSW.IsRunning)
				_refreshInboxSW.Start();
			if (!_waitForContentToShowSW.IsRunning)
				_waitForContentToShowSW.Start();
			if (_waitForContentToShowSW.ElapsedMilliseconds < 3000)
				return;

			if (!_concludingSW.IsRunning)
			{
				if (_refreshInboxSW.ElapsedMilliseconds < 64000)
				{
					if (MinFreeBagSlots > 0 && Me.FreeNormalBagSlots - MinFreeBagSlots <= 4)
					{
						if (!_throttleSW.IsRunning)
							_throttleSW.Start();
						if (_throttleSW.ElapsedMilliseconds < 4000 - (Me.FreeNormalBagSlots - MinFreeBagSlots)*1000)
							return;
						_throttleSW.Reset();
						_throttleSW.Start();
					}
					if (GetMailType == GetMailActionType.AllItems)
					{
						string lua = string.Format(MailFormat, CheckNewMail ? 1 : 0, MaxCODAmount.TotalCopper);
						if (Me.FreeNormalBagSlots <= MinFreeBagSlots || Lua.GetReturnValues(lua)[0] == "1")
							_concludingSW.Start();
					}
					else
					{
						if (_idList.Count > 0 && Me.FreeNormalBagSlots > MinFreeBagSlots)
						{
							string lua = string.Format(MailByIdFormat, _idList[0], CheckNewMail ? 1 : 0, MaxCODAmount.TotalCopper);

							if (Lua.GetReturnValues(lua)[0] == "1")
								_idList.RemoveAt(0);
						}
						else
							_concludingSW.Start();
					}
				}
				else
				{
					_refreshInboxSW.Reset();
					MailFrame.Instance.Close();
				}
			}
			if (_concludingSW.ElapsedMilliseconds > 2000)
				IsDone = true;
			if (IsDone)
			{
				PBLog.Log("Mail retrieval of items:{0} finished", GetMailType);
			}
		}

		private List<uint> BuildItemList()
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
			}
			return idList;
		}

		public override void Reset()
		{
			base.Reset();
			_waitForContentToShowSW = new Stopwatch();
			_concludingSW = new Stopwatch();
			_timeoutSW = new Stopwatch();
			_refreshInboxSW = new Stopwatch();
			_throttleSW = new Stopwatch();
		}

		public override IPBComponent DeepCopy()
		{
			return new GetMailAction
					{
						ItemID = ItemID,
						GetMailType = GetMailType,
						_loc = _loc,
						AutoFindMailBox = AutoFindMailBox,
						Location = Location,
						MinFreeBagSlots = MinFreeBagSlots,
						CheckNewMail = CheckNewMail,
						MaxCODAmount = MaxCODAmount,
					};
		}

	}

}