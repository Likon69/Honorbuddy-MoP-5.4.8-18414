#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Drawing;

using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.CommonBot.Routines;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Common = Singular.ClassSpecific.Druid.Common;
using Singular.Settings;
using Styx.Common;
using Styx.Common.Helpers;

#endregion

namespace Singular.Utilities
{
    public static class EventHandlers
    {
        private static bool _combatLogAttached;
        private static bool _combatFilterAdded;

        public static void Init()
        {
            // get locale specific messasge strings we'll check for
            InitializeLocalizedValues();

            // set default values for timed error states
            LastLineOfSightFailure = DateTime.MinValue;
            LastUnitNotInfrontFailure = DateTime.MinValue;
            SuppressShapeshiftUntil = DateTime.MinValue;

            // hook combat log event if we are debugging or not in performance critical circumstance
            if (SingularSettings.Debug || (SingularRoutine.CurrentWoWContext != WoWContext.Battlegrounds && !StyxWoW.Me.CurrentMap.IsRaid))
                AttachCombatLogEvent();

            // add context handler that reacts to context change with above rules for logging
            SingularRoutine.OnWoWContextChanged += HandleContextChanged;

            // hook PVP start timer so we can identify end of prep phase
            PVP.AttachStartTimer();

            // also hook wow error messages
            Lua.Events.AttachEvent("UI_ERROR_MESSAGE", HandleErrorMessage);

            // hook LOOT_BIND_CONFIRM to handle popup appearing when applying certain spells to weapon
            // Lua.Events.AttachEvent("AUTOEQUIP_BIND_CONFIRM", HandleLootBindConfirm);
            // Lua.Events.AttachEvent("LOOT_BIND_CONFIRM", HandleLootBindConfirm);
            Lua.Events.AttachEvent("END_BOUND_TRADEABLE", HandleEndBoundTradeable);

            Lua.Events.AttachEvent("PARTY_MEMBER_DISABLE", HandlePartyMemberDisable);
            Lua.Events.AttachEvent("PARTY_MEMBER_ENABLE", HandlePartyMemberEnable);
        }

        private static void InitializeLocalizedValues()
        {
            // get localized copies of spell failure error messages
            LocalizedLineOfSightFailure = GetSymbolicLocalizeValue( "SPELL_FAILED_LINE_OF_SIGHT");
            LocalizedUnitNotInfrontFailure = GetSymbolicLocalizeValue( "SPELL_FAILED_UNIT_NOT_INFRONT");
            LocalizedNoPocketsToPickFailure = GetSymbolicLocalizeValue( "SPELL_FAILED_TARGET_NO_POCKETS");
            LocalizedAlreadyPickPocketedError = GetSymbolicLocalizeValue("ERR_ALREADY_PICKPOCKETED");
            LocalizedNoPathAvailableFailure = GetSymbolicLocalizeValue("SPELL_FAILED_NOPATH");

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

        internal static void HandleContextChanged(object sender, WoWContextEventArg e)
        {
            // Since we hooked this in ctor, make sure we are the selected CC
            if (RoutineManager.Current == null || RoutineManager.Current.Name != SingularRoutine.Instance.Name)
                return;

            if (SingularSettings.Debug || (SingularRoutine.CurrentWoWContext != WoWContext.Battlegrounds && !StyxWoW.Me.CurrentMap.IsRaid))
                AttachCombatLogEvent();
            else
                DetachCombatLogEvent();
        }

        /// <summary>
        /// time of last "Target not in line of sight" spell failure.
        /// Used by movement functions for situations where the standard
        /// LoS and LoSS functions are true but still fails in WOW.
        /// See CreateMoveToLosBehavior() for usage
        /// </summary>
        public static DateTime LastLineOfSightFailure { get; set; }
        public static DateTime LastUnitNotInfrontFailure { get; set; }
        public static DateTime LastNoPathFailure { get; set; }
        public static DateTime SuppressShapeshiftUntil { get; set; }
        public static bool IsShapeshiftSuppressed { get { return SuppressShapeshiftUntil > DateTime.Now; } }

        public static WoWUnit LastLineOfSightTarget { get; set; }
        public static ulong LastNoPathTarget { get; set; }

        public static Dictionary<ulong, int> MobsThatEvaded = new Dictionary<ulong, int>();

        public static WoWUnit AttackingEnemyPlayer { get; set; }
        public static DateTime LastAttackedByEnemyPlayer { get; set; }

        public static DateTime LastRedErrorMessage { get; set; }

        /// <summary>
        /// the value of localized values for testing certain types of spell failures
        /// </summary>
        private static string LocalizedLineOfSightFailure;
        private static string LocalizedUnitNotInfrontFailure;
        private static string LocalizedNoPocketsToPickFailure;
        private static string LocalizedAlreadyPickPocketedError;
        private static string LocalizedNoPathAvailableFailure;

        // a combination of errors and spell failures we search for Druid shape shift errors
        private static Dictionary<string,string> LocalizedShapeshiftMessages;

        private static void AttachCombatLogEvent()
        {
            if (_combatLogAttached || _combatFilterAdded)
                DetachCombatLogEvent();

            // DO NOT EDIT THIS UNLESS YOU KNOW WHAT YOU'RE DOING!
            // This ensures we only capture certain combat log events, not all of them.
            // This saves on performance, and possible memory leaks. (Leaks due to Lua table issues.)
            Lua.Events.AttachEvent("COMBAT_LOG_EVENT_UNFILTERED", HandleCombatLog);
            _combatLogAttached = true;

            string filterCriteria = "return";

            if (SingularRoutine.CurrentWoWContext == WoWContext.Normal && SingularSettings.Instance.TargetWorldPvpRegardless)
                filterCriteria += 
                    " ("
                    + " args[8] == UnitGUID('player')"
                    + " and args[8] ~= args[4]"
                    + ")"
                    + " or";
                // filterCriteria += " (args[8] == UnitGUID('player') and args[8] ~= args[4] and 0x000 == bit.band(tonumber('0x'..strsub(guid, 3,5)),0x00f)) or";
            else if (SingularRoutine.CurrentWoWContext == WoWContext.Instances)
                filterCriteria +=
                    " (args[2] == 'UNIT_DIED') or";

            // standard portion of filter
            filterCriteria += 
                " ("
                + " args[4] == UnitGUID('player')"
                + " and"
                +   " ("
                +   " args[2] == 'SPELL_MISSED'"
                +   " or args[2] == 'RANGE_MISSED'"
                +   " or args[2] == 'SWING_MISSED'"
                +   " or args[2] == 'SPELL_CAST_FAILED'"
                +   " )"
                +" )";

            _combatFilterAdded = Lua.Events.AddFilter("COMBAT_LOG_EVENT_UNFILTERED", filterCriteria);
            if (!_combatFilterAdded)
            {
                Logger.Write( "ERROR: Could not add combat log event filter! - Performance may be horrible, and things may not work properly!");
            }

            Logger.WriteDebug("Attached combat log");
        }
        
        private static void DetachCombatLogEvent()
        {
            if (_combatFilterAdded)
            {
                Logger.WriteDebug("Removed combat log filter");
                Lua.Events.RemoveFilter("COMBAT_LOG_EVENT_UNFILTERED");
                _combatFilterAdded = false;
            }

            if (_combatLogAttached)
            {
                Logger.WriteDebug("Detached combat log");
                Lua.Events.DetachEvent("COMBAT_LOG_EVENT_UNFILTERED", HandleCombatLog);
                _combatLogAttached = false;
            }
        }


        static ulong guidLastEnemy;

        private static void HandleCombatLog(object sender, LuaEventArgs args)
        {
            // Since we hooked this in ctor, make sure we are the selected CC
            if (RoutineManager.Current.Name != SingularRoutine.Instance.Name)
                return;

            // convert args to usable form
            var e = new CombatLogEventArgs(args.EventName, args.FireTimeStamp, args.Args);

            if (e.DestGuid == StyxWoW.Me.Guid)
            {
                if (e.SourceGuid != StyxWoW.Me.Guid)
                {
                    WoWUnit enemy = e.SourceUnit;
                    if (Unit.ValidUnit(enemy) && enemy.IsPlayer)
                    {
                        AttackingEnemyPlayer = enemy;
                        LastAttackedByEnemyPlayer = DateTime.Now;

                        // if (guidLastEnemy != enemy.Guid)
                        {
                            guidLastEnemy = enemy.Guid;
                            string extra = "";
                            if (e.Args.GetUpperBound(0) >= 12)
                                extra = string.Format(" with {0}", e.SpellName);

                            Logger.WriteDiagnostic("GankDetect: attacked by Level {0} {1}{2}", enemy.Level, enemy.SafeName(), extra);
                        }
                    }
                }

                return;
            }

            // Logger.WriteDebug("[CombatLog] " + e.Event + " - " + e.SourceName + " - " + e.SpellName);

            switch (e.Event)
            {
                default:
                    if ( SingularSettings.Debug )
                        Logger.WriteDebug("[CombatLog] filter out this event -- " + e.Event + " - " + e.SourceName + " - " + e.SpellName);
                    break;

                // spell_cast_failed only passes filter in Singular debug mode
                case "SPELL_CAST_FAILED":
                    Logger.WriteDiagnostic("[CombatLog] {0} {1}#{2} failure: '{3}'", e.Event, e.Spell.Name, e.SpellId, e.Args[14] );
                    if ( e.Args[14].ToString() == LocalizedLineOfSightFailure )
                    {
                        ulong guid = 0;
                        try
                        {
                            LastLineOfSightTarget = e.DestUnit;
                            guid = LastLineOfSightTarget == null ? 0 : LastLineOfSightTarget.Guid;
                        }
                        catch
                        {
                        }

                        if (guid == 0)
                        {
                            Logger.WriteFile("[CombatLog] no valid destunit so using CurrentTarget");
                            LastLineOfSightTarget = StyxWoW.Me.CurrentTarget;
                            guid = StyxWoW.Me.CurrentTargetGuid;
                        }

                        LastLineOfSightFailure = DateTime.Now;
                        Logger.WriteFile("[CombatLog] cast failed due to los reported at {0} on target {1:X}", LastLineOfSightFailure.ToString("HH:mm:ss.fff"), e.DestGuid );
                    }
                    else if (!MovementManager.IsMovementDisabled && StyxWoW.Me.Class == WoWClass.Warrior && e.Args[14].ToString() == LocalizedNoPathAvailableFailure)
                    {
                        LastNoPathFailure = DateTime.Now;
                        LastNoPathTarget = StyxWoW.Me.CurrentTargetGuid;
                        Logger.WriteFile("[CombatLog] cast failed due to no path available to current target");
                    }
                    else if (!SingularRoutine.IsManualMovementBotActive && (StyxWoW.Me.Class == WoWClass.Druid || StyxWoW.Me.Class == WoWClass.Shaman))
                    {
                        if (LocalizedShapeshiftMessages.ContainsKey(e.Args[14].ToString()))
                        {
                            string symbolicName = LocalizedShapeshiftMessages[e.Args[14].ToString()];
                            SuppressShapeshiftUntil = DateTime.Now.Add( TimeSpan.FromSeconds(30));
                            Logger.Write(Color.White, "/cancel{0} - due to Shapeshift Error '{1}' on cast, suppress form until {2}!", StyxWoW.Me.Shapeshift.ToString().CamelToSpaced(), symbolicName, SuppressShapeshiftUntil.ToString("HH:mm:ss.fff"));
                            Lua.DoString("CancelShapeshiftForm()");
                        }
                    }
                    else if (StyxWoW.Me.Class == WoWClass.Rogue && SingularSettings.Instance.Rogue().UsePickPocket)
                    {
                        if (e.Args[14].ToString() == LocalizedNoPocketsToPickFailure)
                        {
                            // args on this event don't match standard SPELL_CAST_FAIL
                            // -- so, Singular only casts on current target so use that assumption
                            WoWUnit unit = StyxWoW.Me.CurrentTarget;
                            if (unit == null)
                                Logger.WriteFile("[CombatLog] has no pockets event did not have a valid unit");
                            else
                            {
                                Logger.WriteDebug("[CombatLog] {0} has no pockets, blacklisting from pick pocket for 2 minutes", unit.SafeName());
                                Blacklist.Add(unit.Guid, BlacklistFlags.Node, TimeSpan.FromMinutes(2), "Singular: has no pockets to pick");
                            }
                        }
                    }
                    break;

#if SOMEONE_USES_LAST_SPELL_AT_SOME_POINT
                case "SPELL_AURA_APPLIED":
                case "SPELL_CAST_SUCCESS":
                    if (e.SourceGuid != StyxWoW.Me.Guid)
                    {
                        return;
                    }

                    // Update the last spell we cast. So certain classes can 'switch' their logic around.
                    Spell.LastSpellCast = e.SpellName;
                    //Logger.WriteDebug("Successfully cast " + Spell.LastSpellCast);

                    // following commented block should not be needed since rewrite of Pet summon
                    //
                    //// Force a wait for all summoned minions. This prevents double-casting it.
                    //if (StyxWoW.Me.Class == WoWClass.Warlock && e.SpellName.StartsWith("Summon "))
                    //{
                    //    StyxWoW.SleepForLagDuration();
                    //}
                    break;
#endif

                case "SWING_MISSED":
                    if (e.Args[11].ToString() == "EVADE")
                    {
                        HandleEvadeBuggedMob(args, e);
                    }
                    else if (e.Args[11].ToString() == "IMMUNE")
                    {
                        WoWUnit unit = e.DestUnit;
                        if (unit != null && !unit.IsPlayer)
                        {
                            Logger.WriteDebug("{0} is immune to Physical spell school", unit.Name);
                            SpellImmunityManager.Add(unit.Entry, WoWSpellSchool.Physical );
                        }
                    }
                    break;

                case "SPELL_MISSED":
                case "RANGE_MISSED":
                    // Why log misses?  Because users of classes with DoTs testing on training dummies
                    // .. that they don't have enough +Hit for will get DoT spam.  This allows easy
                    // .. diagnosis of false reports of rotation issues where a user simply isn't geared
                    // .. this happens more at the beginning of an expansion especially
                    if (SingularSettings.Debug)
                    {
                        Logger.WriteDebug(
                            "[CombatLog] {0} {1}#{2} {3}",
                            e.Event,
                            e.Spell.Name,
                            e.SpellId,
                            e.Args[14]
                            );
                    }

                    if (e.Args[14].ToString() == "EVADE")
                    {
                        HandleEvadeBuggedMob(args, e);
                    }
                    else if (e.Args[14].ToString() == "IMMUNE")
                    {
                        WoWUnit unit = e.DestUnit;
                        if (unit != null && !unit.IsPlayer)
                        {
                            Logger.WriteDebug("{0} is immune to {1} spell school", unit.Name, e.SpellSchool);
                            SpellImmunityManager.Add(unit.Entry, e.SpellSchool);
                        }
                    }
                    break;

                case "UNIT_DIED":
                    try 
                    { 
                        WoWUnit corpse = e.SourceUnit;
                        WoWPartyMember pm = Unit.GroupMemberInfos.First( m => m.Guid == corpse.Guid);
                        Logger.WriteDiagnostic( "Combat Log: Role={0} {1} Died!", pm.Role & (~WoWPartyMember.GroupRole.Leader), corpse.SafeName());                    
                    }
                    catch 
                    {
                    }
                    break;
            }
        }

        private static void HandleEvadeBuggedMob(LuaEventArgs args, CombatLogEventArgs e)
        {
            WoWUnit unit = e.DestUnit;
            ulong guid = e.DestGuid;

            if (unit == null && StyxWoW.Me.CurrentTarget != null)
            {
                unit = StyxWoW.Me.CurrentTarget;
                guid = StyxWoW.Me.CurrentTargetGuid;
                Logger.Write("Evade: bugged mob guid:{0}, so assuming current target instead", args.Args[7]);
            }

            if (unit != null)
            {
                if ( !MobsThatEvaded.ContainsKey( unit.Guid ))
                    MobsThatEvaded.Add( unit.Guid, 0);

                MobsThatEvaded[unit.Guid]++;
                if (MobsThatEvaded[unit.Guid] <= 5)
                {
                    Logger.Write("Mob {0} has evaded {1} times.  Keeping an eye on {2:X0} for now!", unit.SafeName(), MobsThatEvaded[unit.Guid], unit.Guid);
                }
                else
                {
                    const int MinutesToBlacklist = 5;

                    if (Blacklist.Contains(unit.Guid, BlacklistFlags.Combat))
                        Logger.Write(Color.LightGoldenrodYellow, "Mob {0} has evaded {1} times. Previously blacklisted {2:X0} for {3} minutes!", unit.SafeName(), MobsThatEvaded[unit.Guid], unit.Guid, MinutesToBlacklist);
                    else
                    {
                        Logger.Write(Color.LightGoldenrodYellow, "Mob {0} has evaded {1} times. Blacklisting {2:X0} for {3} minutes!", unit.SafeName(), MobsThatEvaded[unit.Guid], unit.Guid, MinutesToBlacklist);
                        Blacklist.Add(unit.Guid, BlacklistFlags.Combat, TimeSpan.FromMinutes(MinutesToBlacklist));
                        if (!Blacklist.Contains(unit.Guid, BlacklistFlags.Combat))
                        {
                            Logger.Write(Color.Pink, "error: blacklist does not contain entry for {0} so adding {1}", unit.SafeName(), unit.Guid);
                        }
                    }

                    if (BotPoi.Current.Guid == unit.Guid)
                    {
                        Logger.Write("EvadeHandling: Current BotPOI type={0} is Evading, clearing now...", BotPoi.Current.Type);
                        BotPoi.Clear("Singular recognized Evade bugged mob");
                    }

                    if (StyxWoW.Me.CurrentTargetGuid == guid)
                    {
                        foreach (var target in Targeting.Instance.TargetList)
                        {
                            if (target.IsAlive && Unit.ValidUnit(target) && !Blacklist.Contains(target, BlacklistFlags.Combat))
                            {
                                Logger.Write(Color.Pink, "Setting target to {0} to get off evade bugged mob!", target.SafeName());
                                target.Target();
                                return;
                            }
                        }

                        Logger.Write(Color.Pink, "BotBase has 0 entries in Target list not blacklisted -- nothing else we can do at this point!");
                        // StyxWoW.Me.ClearTarget();
                    }
                }

            }

            /// line below was originally in Evade logic, but commenting to avoid Sleeps
            // StyxWoW.SleepForLagDuration();
        }

        private static void HandleErrorMessage(object sender, LuaEventArgs args)
        {
            // Since we hooked this in ctor, make sure we are the selected CC
            if (RoutineManager.Current.Name != SingularRoutine.Instance.Name)
                return;

            bool handled = false;
            LastRedErrorMessage = DateTime.Now;

            if (SingularSettings.Debug)
            {
                Logger.WriteDebug("[WoWRedError] {0}", args.Args[0].ToString());
            }

            if (StyxWoW.Me.Class == WoWClass.Rogue && SingularSettings.Instance.Rogue().UsePickPocket && args.Args[0].ToString() == LocalizedAlreadyPickPocketedError)
            {
                if (StyxWoW.Me.GotTarget)
                {
                    WoWUnit unit = StyxWoW.Me.CurrentTarget;
                    Logger.WriteDebug("WowRedError Handler: already pick pocketed {0}, blacklisting from pick pocket for 2 minutes", unit.SafeName());
                    Blacklist.Add(unit.Guid, BlacklistFlags.Node, TimeSpan.FromMinutes(2), "Singular: already pick pocketed mob");
                    handled = true;
                }
            }

            //  !SingularRoutine.IsManualMovementBotActive
            if ( SingularRoutine.IsQuestBotActive)
            {
                if (StyxWoW.Me.Class == WoWClass.Shaman || (StyxWoW.Me.Class == WoWClass.Druid && StyxWoW.Me.Shapeshift != ShapeshiftForm.FlightForm && StyxWoW.Me.Shapeshift != ShapeshiftForm.EpicFlightForm))
                {
                    if (LocalizedShapeshiftMessages.ContainsKey(args.Args[0].ToString()))
                    {
                        string symbolicName = LocalizedShapeshiftMessages[args.Args[0].ToString()];
                        SuppressShapeshiftUntil = DateTime.Now.Add(TimeSpan.FromSeconds(30));
                        Logger.Write(Color.White, "/cancel{0} - due to Error '{1}', suppress form until {2}!", StyxWoW.Me.Shapeshift.ToString().CamelToSpaced(), symbolicName, SuppressShapeshiftUntil.ToString("HH:mm:ss.fff"));
                        Lua.DoString("CancelShapeshiftForm()");
                        handled = true;
                    }
                }
            }
        }

        private static void HandlePartyMemberEnable(object sender, LuaEventArgs args)
        {
            // Since we hooked this in ctor, make sure we are the selected CC
            if (RoutineManager.Current.Name != SingularRoutine.Instance.Name)
                return;

            WoWPartyMember pm = Unit.GroupMemberInfos.FirstOrDefault(g => g.ToPlayer() != null && string.Equals(g.ToPlayer().Name, args.Args[0].ToString(), StringComparison.InvariantCulture));
            string name = "(null)";
            string status = "(unknown)";

            if (pm == null)
            {
                Logger.WriteDiagnostic("Group Member: {0} enabled but could not be found", args.Args[0].ToString());
            }
            else
            {
                WoWUnit o = ObjectManager.GetObjectByGuid<WoWUnit>(pm.Guid);
                name = o.Name;
                status = "Alive";
                Logger.WriteDiagnostic("Group Member {0}: {1} {2}", pm.RaidRank, name, status);
            }
        }


        private static void HandlePartyMemberDisable(object sender, LuaEventArgs args)
        {
            // Since we hooked this in ctor, make sure we are the selected CC
            if (RoutineManager.Current.Name != SingularRoutine.Instance.Name)
                return;

            WoWPartyMember pm = Unit.GroupMemberInfos.FirstOrDefault(g => g.ToPlayer() != null && string.Equals(g.ToPlayer().Name, args.Args[0].ToString(), StringComparison.InvariantCulture));
            string name = "(null)";
            string status = "(unknown)";

            if (pm == null)
            {
                Logger.WriteDiagnostic("Group Member: {0} disabled but could not be found", args.Args[0].ToString());
            }
            else
            {
                WoWUnit o = ObjectManager.GetObjectByGuid<WoWUnit>(pm.Guid);
                name = o.Name;
                if (!o.IsAlive)
                    status = "Died!";
                else if (!pm.IsOnline)
                    status = "went Offline";

                Logger.WriteDiagnostic("Group Member {0}: {1} {2}", pm.RaidRank, name, status);
            }
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

        private static void HandleEndBoundTradeable(object sender, LuaEventArgs args)
        {
             // Since we hooked this in ctor, make sure we are the selected CC
            if (RoutineManager.Current.Name != SingularRoutine.Instance.Name)
                return;

            string argval = args.Args[0].ToString();
            Logger.Write(Color.LightGreen, "EndBoundTradeable: confirming '{0}'", argval);
            string cmd = string.Format("EndBoundTradeable('{0}')", argval);
            Logger.WriteDiagnostic("END_BOUND_TRADEABLE: confirm with \"{0}\"", cmd);
            Lua.DoString(cmd);
        }
    }
}