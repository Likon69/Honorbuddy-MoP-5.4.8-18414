//!CompilerOption:Optimize:On

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Media;
using System.Xml.Linq;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Bots.FishingBuddy
{
    public enum PathingType
    {
        Circle,
        Bounce
    }

    public class FishingBuddyBot : BotBase
    {
        private readonly List<uint> _poolsToFish = new List<uint>();

		private PathingType _pathingType = PathingType.Circle;
	    private string _prevProfilePath;
	    private static readonly WaitTimer LoadProfileTimer = new WaitTimer(TimeSpan.FromSeconds(1));
        private static DateTime _botStartTime;

        public FishingBuddyBot()
        {
            Instance = this;
            BotEvents.Profile.OnNewOuterProfileLoaded += Profile_OnNewOuterProfileLoaded;
            Styx.CommonBot.Profiles.Profile.OnUnknownProfileElement += Profile_OnUnknownProfileElement;
        }

		internal bool LootFrameIsOpen { get; private set; }
	
		internal bool ShouldFaceWaterNow { get;  set; }

		internal Dictionary<string, uint> FishCaught { get; private set; }
		
		internal FishingBuddyProfile Profile { get; private set; }
        internal static FishingBuddyBot Instance { get; private set; }

        #region overrides

        private readonly InventoryType[] _2HWeaponTypes =
        {
            InventoryType.TwoHandWeapon,
            InventoryType.Ranged,
        };

        private Composite _root;

        public override string Name
        {
			get { return "FishingBuddy"; }
        }

        public override PulseFlags PulseFlags
        {
            get { return PulseFlags.All & (~PulseFlags.CharacterManager); }
        }

        public override Composite Root
        {
            get { return _root ?? (_root = new ActionRunCoroutine(ctx => Coroutines.RootLogic())); }
        }

        public override bool IsPrimaryType
        {
            get { return true; }
        }

        public override Form ConfigurationForm
        {
            get { return new MainForm(); }
        }

        public override void Pulse() {}

        public override void Initialize()
        {
            try
            {
				WoWItem mainhand = (FishingBuddySettings.Instance.MainHand != 0
					? StyxWoW.Me.BagItems.FirstOrDefault(i => i.Entry == FishingBuddySettings.Instance.MainHand) 
					: null) ?? FindMainHand();

				WoWItem offhand = FishingBuddySettings.Instance.OffHand != 0 
					? StyxWoW.Me.BagItems.FirstOrDefault(i => i.Entry == FishingBuddySettings.Instance.OffHand) 
					: null;

                if ((mainhand == null || !_2HWeaponTypes.Contains(mainhand.ItemInfo.InventoryType)) && offhand == null)
                    offhand = FindOffhand();

				if (mainhand != null)
                    Log("Using {0} for mainhand weapon", mainhand.Name);

                if (offhand != null)
                    Log("Using {0} for offhand weapon", offhand.Name);	           

            }
            catch (Exception ex)
            {
                Logging.WriteException(ex);
            }
        }

        public override void Start()
        {
	        DumpConfiguration();
            _botStartTime = DateTime.Now;
            FishCaught = new Dictionary<string, uint>();

		    Lua.Events.AttachEvent("LOOT_OPENED", LootFrameOpenedHandler);
		    Lua.Events.AttachEvent("LOOT_CLOSED", LootFrameClosedHandler);
		    Lua.Events.AttachEvent("UNIT_SPELLCAST_FAILED", UnitSpellCastFailedHandler);
		    LootTargeting.Instance.IncludeTargetsFilter += LootFilters.IncludeTargetsFilter;

	        Coroutines.OnStart();

			_prevProfilePath = ProfileManager.XmlLocation;

			if (ProfileManager.CurrentOuterProfile == null)
			{
				if (!string.IsNullOrEmpty(FishingBuddySettings.Instance.LastLoadedProfile)
					&& (File.Exists(FishingBuddySettings.Instance.LastLoadedProfile)
					|| IsStoreProfile(FishingBuddySettings.Instance.LastLoadedProfile)))
				{
					ProfileManager.LoadNew(FishingBuddySettings.Instance.LastLoadedProfile);
				}
				else
				{
					ProfileManager.LoadEmpty();
				}
			}
        }

	    private bool IsStoreProfile(string path)
	    {
			return !string.IsNullOrEmpty(path) && path.StartsWith("store://", true, CultureInfo.InvariantCulture);
	    }


        public override void Stop()
        {
			Coroutines.OnStop();

            Log("In {0} days, {1} hours and {2} minutes we have caught",
                (DateTime.Now - _botStartTime).Days,
                (DateTime.Now - _botStartTime).Hours,
                (DateTime.Now - _botStartTime).Minutes);

            foreach (var kv in FishCaught)
            {
                Log("{0} x{1}", kv.Key, kv.Value);
            }

			LootTargeting.Instance.IncludeTargetsFilter -= LootFilters.IncludeTargetsFilter;
            Lua.Events.DetachEvent("LOOT_OPENED", LootFrameOpenedHandler);
            Lua.Events.DetachEvent("LOOT_CLOSED", LootFrameClosedHandler);
			Lua.Events.DetachEvent("UNIT_SPELLCAST_FAILED", UnitSpellCastFailedHandler);

			if (!string.IsNullOrEmpty(_prevProfilePath) && (File.Exists(_prevProfilePath) || IsStoreProfile(_prevProfilePath)))
				ProfileManager.LoadNew(_prevProfilePath);
        }

        #endregion

        #region Handlers

        private void LootFrameClosedHandler(object sender, LuaEventArgs args)
        {
            LootFrameIsOpen = false;
        }

        private void LootFrameOpenedHandler(object sender, LuaEventArgs args)
        {
            LootFrameIsOpen = true;
        }

		private void UnitSpellCastFailedHandler(object sender, LuaEventArgs args)
		{
			var spell = GetWoWSpellFromSpellCastFailedArgs(args);
			if (spell != null && spell.IsValid && spell.Name == "Fishing")
				ShouldFaceWaterNow = true;	
		}



        #endregion

        #region Profile

        private void Profile_OnNewOuterProfileLoaded(BotEvents.Profile.NewProfileLoadedEventArgs args)
        {
            try
            {
				Profile = new FishingBuddyProfile(args.NewProfile, _pathingType, _poolsToFish);
	            if (!string.IsNullOrEmpty(ProfileManager.XmlLocation))
	            {
		            FishingBuddySettings.Instance.LastLoadedProfile = ProfileManager.XmlLocation;
					FishingBuddySettings.Instance.Save();
	            }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex);
            }
        }

        public void Profile_OnUnknownProfileElement(object sender, UnknownProfileElementEventArgs e)
        {
			// hackish way to set variables to default states before loading new profile... wtb OnNewOuterProfileLoading event
			if (LoadProfileTimer.IsFinished)
			{
				_poolsToFish.Clear();
				_pathingType = PathingType.Circle;
				LoadProfileTimer.Reset();
			}

            if (e.Element.Name == "FishingSchool")
            {
                XAttribute entryAttrib = e.Element.Attribute("Entry");
                if (entryAttrib != null)
                {
                    uint entry;
                    UInt32.TryParse(entryAttrib.Value, out entry);
					if (!_poolsToFish.Contains(entry))
                    {
						_poolsToFish.Add(entry);
                        XAttribute nameAttrib = e.Element.Attribute("Name");
                        if (nameAttrib != null)
                            Log( "Adding Pool Entry: {0} to the list of pools to fish from", nameAttrib.Value);
                        else
                            Log("Adding Pool Entry: {0} to the list of pools to fish from", entry);
                    }
                }
                else
                {
                    Err(
                        "<FishingSchool> tag must have the 'Entry' Attribute, e.g <FishingSchool Entry=\"202780\"/>\nAlso supports 'Name' attribute but only used for display purposes");
                }
                e.Handled = true;
            }
            else if (e.Element.Name == "Pathing")
            {
                XAttribute typeAttrib = e.Element.Attribute("Type");
                if (typeAttrib != null)
                {
                    _pathingType = (PathingType)
                        Enum.Parse(typeof (PathingType), typeAttrib.Value, true);
                    
					Log("Setting Pathing Type to {0} Mode", _pathingType);
                }
                else
                {
                    Err(
                        "<Pathing> tag must have the 'Type' Attribute, e.g <Pathing Type=\"Circle\"/>");
                }
                e.Handled = true;
            }
        }

        #endregion

	    WoWSpell GetWoWSpellFromSpellCastFailedArgs(LuaEventArgs args)
	    {
		    if (args.Args.Length < 5)
			    return null;
			return WoWSpell.FromId((int)((double)args.Args[4]));
	    }

        private WoWItem FindMainHand()
        {
			WoWItem mainHand = StyxWoW.Me.Inventory.Equipped.MainHand;
	        if (mainHand == null || mainHand.ItemInfo.WeaponClass == WoWItemWeaponClass.FishingPole)
	        {
		        mainHand = StyxWoW.Me.CarriedItems.OrderByDescending(u => u.ItemInfo.Level).
			        FirstOrDefault(
				        i => i.IsSoulbound && (i.ItemInfo.InventoryType == InventoryType.WeaponMainHand ||
												i.ItemInfo.InventoryType == InventoryType.TwoHandWeapon) &&
							StyxWoW.Me.CanEquipItem(i));

		        if (mainHand != null)
			        FishingBuddySettings.Instance.MainHand = mainHand.Entry;
		        else
			        Err("Unable to find a mainhand weapon to swap to when in combat");
	        }
	        else
	        {
		        FishingBuddySettings.Instance.MainHand = mainHand.Entry;
	        }
			FishingBuddySettings.Instance.Save();
            return mainHand;
        }

        // scans bags for offhand weapon if mainhand isn't 2h and none are equipped and uses the highest ilvl one
        private WoWItem FindOffhand()
        {
			WoWItem offHand = StyxWoW.Me.Inventory.Equipped.OffHand;
	        if (offHand == null)
	        {
		        offHand = StyxWoW.Me.CarriedItems.OrderByDescending(u => u.ItemInfo.Level).
			        FirstOrDefault(
				        i => i.IsSoulbound && (i.ItemInfo.InventoryType == InventoryType.WeaponOffHand ||
												i.ItemInfo.InventoryType == InventoryType.Weapon ||
												i.ItemInfo.InventoryType == InventoryType.Shield) &&
							FishingBuddySettings.Instance.MainHand != i.Entry &&
							StyxWoW.Me.CanEquipItem(i));

		        if (offHand != null)
			        FishingBuddySettings.Instance.OffHand = offHand.Entry;
		        else
			        Err("Unable to find an offhand weapon to swap to when in combat");
	        }
	        else
	        {
		        FishingBuddySettings.Instance.OffHand = offHand.Entry;
	        }
			FishingBuddySettings.Instance.Save();
            return offHand;
        }

		internal static void Log(string text)
		{
			Logging.Write(Colors.DodgerBlue, "FishingBuddy: " + text);
		}

        internal static void Log(string format, params object[] args)
        {
	        Log(string.Format(format, args));
        }

		internal static void Err(string text)
		{
			Logging.Write(Colors.Red, "FishingBuddy: " + text);
		}

		internal static void Err(string format, params object[] args)
		{
			Err(string.Format(format, args));
		}

		internal static void Debug(string text)
        {
			Logging.Write(LogLevel.Diagnostic, Colors.Goldenrod, "FishingBuddy-Debug: " + text);
        }

		internal static void Debug(string format, params object[] args)
		{
			Debug(string.Format(format, args));
		}

	    private void DumpConfiguration()
	    {
		    Debug("AvoidLava: {0}", FishingBuddySettings.Instance.AvoidLava);
			Debug("Fly: {0}", FishingBuddySettings.Instance.Fly);
			Debug("FilletFish: {0}", FishingBuddySettings.Instance.FilletFish);
			Debug("LootNPCs: {0}", FishingBuddySettings.Instance.LootNPCs);
			
			Debug("Hat Id: {0}", FishingBuddySettings.Instance.Hat);
			Debug("MainHand Id: {0}", FishingBuddySettings.Instance.MainHand);
			Debug("OffHand Id: {0}", FishingBuddySettings.Instance.OffHand);

			Debug("MaxFailedCasts: {0}", FishingBuddySettings.Instance.MaxFailedCasts);
			Debug("MaxTimeAtPool: {0}", FishingBuddySettings.Instance.MaxTimeAtPool);
			Debug("NinjaNodes: {0}", FishingBuddySettings.Instance.NinjaNodes);
			Debug("PathPrecision: {0}", FishingBuddySettings.Instance.PathPrecision);
			Debug("Poolfishing: {0}", FishingBuddySettings.Instance.Poolfishing);
			Debug("TraceStep: {0}", FishingBuddySettings.Instance.TraceStep);
			Debug("UseBait: {0}", FishingBuddySettings.Instance.UseBait);
			Debug("UseWaterWalking: {0}", FishingBuddySettings.Instance.UseWaterWalking);
	    }
    }
}