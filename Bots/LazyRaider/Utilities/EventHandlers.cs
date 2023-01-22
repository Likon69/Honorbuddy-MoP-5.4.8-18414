#region

using System;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.CommonBot.Routines;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using System.Globalization;
using Styx.Common;
using System.Drawing;
using System.Collections.Generic;
using Styx.Common.Helpers;

#endregion

namespace Styx.Bot.CustomBots
{
    public static class EventHandlers
    {
        private static bool _combatLogAttached;

        public static void Init()
        {
            // get locale specific messasge strings we'll check for
            InitializeLocalizedValues();

            // set default values for timed error states
            LastLineOfSightFailure = DateTime.MinValue;
            LastUnitNotInfrontFailure = DateTime.MinValue;
            LastShapeshiftFailure = DateTime.MinValue;

            // hook combat log event if we are debugging or not in performance critical circumstance
            AttachCombatLogEvent();
        }

        private static void InitializeLocalizedValues()
        {
            // get localized copies of spell failure error messages
            LocalizedLineOfSightFailure = GetSymbolicLocalizeValue( "SPELL_FAILED_LINE_OF_SIGHT");
            LocalizedUnitNotInfrontFailure = GetSymbolicLocalizeValue( "SPELL_FAILED_UNIT_NOT_INFRONT");
            LocalizedNoPocketsToPickFailure = GetSymbolicLocalizeValue( "SPELL_FAILED_TARGET_NO_POCKETS");
            LocalizedAlreadyPickPocketedError = GetSymbolicLocalizeValue("ERR_ALREADY_PICKPOCKETED");

            // monitor ERR_ strings in Error Message Handler
            LocalizedShapeshiftMessages = new Dictionary<string, string>();

            LocalizedShapeshiftMessages.AddSymbolicLocalizeValue( "ERR_CANT_INTERACT_SHAPESHIFTED");
            LocalizedShapeshiftMessages.AddSymbolicLocalizeValue( "ERR_MOUNT_SHAPESHIFTED");
            LocalizedShapeshiftMessages.AddSymbolicLocalizeValue( "ERR_NOT_WHILE_SHAPESHIFTED");
            LocalizedShapeshiftMessages.AddSymbolicLocalizeValue( "ERR_NO_ITEMS_WHILE_SHAPESHIFTED");
            LocalizedShapeshiftMessages.AddSymbolicLocalizeValue( "ERR_SHAPESHIFT_FORM_CANNOT_EQUIP");
            LocalizedShapeshiftMessages.AddSymbolicLocalizeValue( "ERR_TAXIPLAYERSHAPESHIFTED");
            LocalizedShapeshiftMessages.AddSymbolicLocalizeValue( "SPELL_FAILED_CUSTOM_ERROR_125");
            LocalizedShapeshiftMessages.AddSymbolicLocalizeValue( "SPELL_FAILED_CUSTOM_ERROR_99");
            LocalizedShapeshiftMessages.AddSymbolicLocalizeValue( "SPELL_FAILED_NOT_SHAPESHIFT");
            LocalizedShapeshiftMessages.AddSymbolicLocalizeValue( "SPELL_FAILED_NO_ITEMS_WHILE_SHAPESHIFTED");
            LocalizedShapeshiftMessages.AddSymbolicLocalizeValue( "SPELL_NOT_SHAPESHIFTED");
            LocalizedShapeshiftMessages.AddSymbolicLocalizeValue( "SPELL_NOT_SHAPESHIFTED_NOSPACE");
        }

        /// <summary>
        /// time of last "Target not in line of sight" spell failure.
        /// Used by movement functions for situations where the standard
        /// LoS and LoSS functions are true but still fails in WOW.
        /// See CreateMoveToLosBehavior() for usage
        /// </summary>
        public static DateTime LastLineOfSightFailure { get; set; }
        public static DateTime LastUnitNotInfrontFailure { get; set; }
        public static DateTime LastShapeshiftFailure { get; set; }

        public static WoWUnit LastLineOfSightTarget { get; set; }

        public static Dictionary<ulong, int> MobsThatEvaded = new Dictionary<ulong, int>();

        /// <summary>
        /// the value of localized values for testing certain types of spell failures
        /// </summary>
        private static string LocalizedLineOfSightFailure;
        private static string LocalizedUnitNotInfrontFailure;
        private static string LocalizedNoPocketsToPickFailure;
        private static string LocalizedAlreadyPickPocketedError;

        // a combination of errors and spell failures we search for Druid shape shift errors
        private static Dictionary<string,string> LocalizedShapeshiftMessages;

        private static void AttachCombatLogEvent()
        {
            if (_combatLogAttached)
                return;

            // DO NOT EDIT THIS UNLESS YOU KNOW WHAT YOU'RE DOING!
            // This ensures we only capture certain combat log events, not all of them.
            // This saves on performance, and possible memory leaks. (Leaks due to Lua table issues.)
            Lua.Events.AttachEvent("COMBAT_LOG_EVENT_UNFILTERED", HandleCombatLog);

            string filterCriteria =
                "return args[4] == UnitGUID('player')"
                + " and (args[2] == 'SPELL_MISSED'"
                + " or args[2] == 'RANGE_MISSED'"
                + " or args[2] == 'SWING_MISSED'"
                + " or args[2] == 'SPELL_CAST_FAILED')";

            if (!Lua.Events.AddFilter("COMBAT_LOG_EVENT_UNFILTERED", filterCriteria ))
            {
                LazyRaider.Log( "ERROR: Could not add combat log event filter! - Performance may be horrible, and things may not work properly!");
            }

            LazyRaider.Dlog("Attached combat log");
            _combatLogAttached = true;
        }
        
        private static void DetachCombatLogEvent()
        {
            if (!_combatLogAttached)
                return;

            LazyRaider.Dlog("Removed combat log filter");
            Lua.Events.RemoveFilter("COMBAT_LOG_EVENT_UNFILTERED");
            LazyRaider.Dlog("Detached combat log");
            Lua.Events.DetachEvent("COMBAT_LOG_EVENT_UNFILTERED", HandleCombatLog);
            _combatLogAttached = false;
        }

        private static ulong[] _guidRaidIcon = new ulong[256];

        private static void HandleCombatLog(object sender, LuaEventArgs args)
        {
            var e = new CombatLogEventArgs(args.EventName, args.FireTimeStamp, args.Args);
            if (e.SourceGuid != StyxWoW.Me.Guid)
                return;
        }

        private static string GetSymbolicLocalizeValue(string symbolicName)
        {
            string localString = Lua.GetReturnVal<string>("return " + symbolicName, 0);
            return localString;
        }

        private static void AddSymbolicLocalizeValue( this Dictionary<string,string> dict, string symbolicName)
        {
            string localString = GetSymbolicLocalizeValue(symbolicName);
            if (!string.IsNullOrEmpty(localString) && !dict.ContainsKey(localString))
            {
                dict.Add(localString, symbolicName);
            }
        }
    }
}