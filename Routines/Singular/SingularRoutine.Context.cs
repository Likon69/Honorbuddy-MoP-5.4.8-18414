using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Routines;
using Styx.WoWInternals.DBC;
using System.Drawing;
using Singular.Helpers;
using Singular.Settings;
using Styx.WoWInternals.WoWObjects;
using Styx.WoWInternals;
using Styx.Common;
using Styx.Plugins;
using System.Dynamic;
using Singular.Managers;

namespace Singular
{
    #region Nested type: WoWContextEventArg

    public class WoWContextEventArg : EventArgs
    {
        public readonly WoWContext CurrentContext;
        public readonly WoWContext PreviousContext;

        public WoWContextEventArg(WoWContext currentContext, WoWContext prevContext)
        {
            CurrentContext = currentContext;
            PreviousContext = prevContext;
        }
    }

    #endregion
    partial class SingularRoutine
    {
        public static event EventHandler<WoWContextEventArg> OnWoWContextChanged;
        private static WoWContext _lastContext = WoWContext.None;
        private static uint _lastMapId = 0;

        internal static WoWContext ForcedContext { get; set; }

        internal static bool IsQuestBotActive { get; set; }
        internal static bool IsBgBotActive { get; set; }
        internal static bool IsDungeonBuddyActive { get; set; }
        internal static bool IsPokeBuddyActive { get; set; }
        internal static bool IsManualMovementBotActive { get; set; }
        internal static bool IsGrindBotActive { get; set; }

        internal static WoWContext _cachedContext = WoWContext.None;
        internal static HealingContext _cachedHealCtx = HealingContext.None;

        internal static WoWContext CurrentWoWContext
        {
            get
            {
                return _cachedContext;
            }
            set
            {
                _cachedContext = value;
            }
        }

        internal static HealingContext CurrentHealContext
        {
            get
            {
                return _cachedHealCtx;
            }
            set
            {
                _cachedHealCtx = value;
            }
        }

        private static void DetermineCurrentWoWContext()
        {
            CurrentWoWContext = _DetermineCurrentWoWContext();
            CurrentHealContext = (CurrentWoWContext == WoWContext.Instances && Me.GroupInfo.IsInRaid)
                ? HealingContext.Raids
                : (HealingContext)CurrentWoWContext;
        }

        private static WoWContext _DetermineCurrentWoWContext()
        {
            if (!StyxWoW.IsInGame)
                return WoWContext.None;

            if (ForcedContext != WoWContext.None)
            {
                if (_lastContext != ForcedContext)
                    Logger.Write(Color.White, "Context: forcing use of {0} behaviors", ForcedContext);

                return ForcedContext;
            }

            Map map = StyxWoW.Me.CurrentMap;

            if (map.IsBattleground || map.IsArena)
            {
                if (_lastContext != WoWContext.Battlegrounds)
                    Logger.Write(Color.White, "Context: using {0} behaviors since in battleground/arena", WoWContext.Battlegrounds);

                return WoWContext.Battlegrounds;
            }

            if (Me.IsInGroup())
            {
                if (Me.IsInInstance)
                {
                    if (_lastContext != WoWContext.Instances)
                        Logger.Write(Color.White, "Context: using {0} behaviors since inside an instance", WoWContext.Instances);

                    return WoWContext.Instances;
                }

                // if (Group.Tanks.Any() || Group.Healers.Any())
                const WoWPartyMember.GroupRole hasGroupRoleMask = WoWPartyMember.GroupRole.Healer | WoWPartyMember.GroupRole.Tank | WoWPartyMember.GroupRole.Damage;
                if ((Me.Role & hasGroupRoleMask) != WoWPartyMember.GroupRole.None)
                {
                    if (_lastContext != WoWContext.Instances)
                        Logger.Write(Color.White, "Context: using {0} behaviors since in group as {1}", WoWContext.Instances, Me.Role & hasGroupRoleMask);

                    return WoWContext.Instances;
                }

                if (_lastContext != WoWContext.Normal)
                    Logger.Write(Color.White, "Context: no Role assigned (Tank/Healer/Damage), so using Normal (SOLO) behaviors");

                return WoWContext.Normal;
            }

            if (_lastContext != WoWContext.Normal)
                Logger.Write(Color.White, "Context: using Normal (SOLO) behaviors since we are not in a group");

            return WoWContext.Normal;
        }

        public static WoWContext TrainingDummyBehaviors { get; set; }

        private bool _contextEventSubscribed;
        private void UpdateContext()
        {
            // Subscribe to the map change event, so we can automatically update the context.
            if (!_contextEventSubscribed)
            {
                // Subscribe to OnBattlegroundEntered. Just 'cause.
                BotEvents.Battleground.OnBattlegroundEntered += e => UpdateContext();
                SingularRoutine.OnBotEvent += (src, arg) =>
                {
                    if (arg.Event == SingularBotEvent.BotStart || arg.Event == SingularBotEvent.BotChanged)
                    {
                        // check if any of the bot detection values have changed which we use to 
                        // .. conditionally build trees
                        DescribeContext();
                        if (UpdateContextStateValues())
                        {
                            RebuildBehaviors();
                        }
                    }
                };
                _contextEventSubscribed = true;
            }

            DetermineCurrentWoWContext();

            // Can't update the context when it doesn't exist.
            if (CurrentWoWContext == WoWContext.None)
                return;

            if(CurrentWoWContext != _lastContext && OnWoWContextChanged!=null)
            {
                // store values that require scanning lists
                UpdateContextStateValues();
                DescribeContext();
                try
                {
                    OnWoWContextChanged(this, new WoWContextEventArg(CurrentWoWContext, _lastContext));
                }
                catch
                {
                    // Eat any exceptions thrown.
                }

                _lastContext = CurrentWoWContext;
                _lastMapId = Me.MapId;
            }
            else if (_lastMapId != Me.MapId)
            {
                DescribeContext();
                _lastMapId = Me.MapId;
            }
        }

        private static bool Changed( bool currVal, ref bool storedVal)
        {
            if (( currVal && storedVal) || (!currVal && !storedVal))
                return false;

            storedVal = currVal;
            return true;
        }

        private static bool UpdateContextStateValues()
        {
            bool questBot= IsBotInUse("Quest");
            bool bgBot= IsBotInUse("BGBuddy", "BG Bot");
            bool dungeonBot= IsBotInUse("DungeonBuddy");
            bool petHack = IsPluginEnabled("Pokébuddy", "Pokehbuddy");
            bool manualBot = IsBotInUse("LazyRaider", "Raid Bot", "Tyrael");

            BotBase bot = GetCurrentBotBase();
            bool grindBot = bot != null && bot.Name.ToUpper().Contains("GRIND");

            bool changed = false;

            if (questBot != IsQuestBotActive )
            {
                changed = true;
                IsQuestBotActive = questBot;
            }

            if (bgBot != IsBgBotActive )
            {
                changed = true;
                IsBgBotActive = bgBot ;
            }

            if (dungeonBot != IsDungeonBuddyActive )
            {
                changed = true;
                IsDungeonBuddyActive = dungeonBot;
            }

            if ( petHack != IsPokeBuddyActive)
            {
                changed = true;
                IsPokeBuddyActive = petHack;
            }

            if (manualBot != IsManualMovementBotActive)
            {
                changed = true;
                IsManualMovementBotActive = manualBot;
            }

            if (grindBot != IsGrindBotActive)
            {
                changed = true;
                IsGrindBotActive = grindBot;
            }

            return changed;
        } 

        public static void DescribeContext()
        {
            string sRace = Me.Race.ToString().CamelToSpaced();
            if (Me.Race == WoWRace.Pandaren)
                sRace = " " + Me.FactionGroup.ToString() + sRace;

            Logging.Write(" "); // spacer before prior log text

            Logger.Write(Color.LightGreen, "Your Level {0}{1} {2} {3} Build is", Me.Level, sRace, SpecializationName(), Me.Class.ToString() );

            Logger.Write(Color.LightGreen, "... running the {0} bot in {1} {2}",
                 GetBotName(),
                 Me.RealZoneText, 
                 !Me.IsInInstance || Battlegrounds.IsInsideBattleground ? "" : "[" + GetInstanceDifficultyName() + "]"
                );

            Logger.WriteFile("   MapId            = {0}", Me.MapId);
            Logger.WriteFile("   ZoneId           = {0}", Me.ZoneId);
/*
            if (Me.CurrentMap != null && Me.CurrentMap.IsValid)
            {
                Logger.WriteFile("   AreaTableId      = {0}", Me.CurrentMap.AreaTableId);
                Logger.WriteFile("   InternalName     = {0}", Me.CurrentMap.InternalName);
                Logger.WriteFile("   IsArena          = {0}", Me.CurrentMap.IsArena.ToYN());
                Logger.WriteFile("   IsBattleground   = {0}", Me.CurrentMap.IsBattleground.ToYN());
                Logger.WriteFile("   IsContinent      = {0}", Me.CurrentMap.IsContinent.ToYN());
                Logger.WriteFile("   IsDungeon        = {0}", Me.CurrentMap.IsDungeon.ToYN());
                Logger.WriteFile("   IsInstance       = {0}", Me.CurrentMap.IsInstance.ToYN());
                Logger.WriteFile("   IsRaid           = {0}", Me.CurrentMap.IsRaid.ToYN());
                Logger.WriteFile("   IsScenario       = {0}", Me.CurrentMap.IsScenario.ToYN());
                Logger.WriteFile("   MapDescription   = {0}", Me.CurrentMap.MapDescription);
                Logger.WriteFile("   MapDescription2  = {0}", Me.CurrentMap.MapDescription2);
                Logger.WriteFile("   MapType          = {0}", Me.CurrentMap.MapType);
                Logger.WriteFile("   MaxPlayers       = {0}", Me.CurrentMap.MaxPlayers);
                Logger.WriteFile("   Name             = {0}", Me.CurrentMap.Name);
            }
*/
            string sRunningAs = "";

            if (Me.CurrentMap == null)
                sRunningAs = "Unknown";
            else if (Me.CurrentMap.IsArena)
                sRunningAs = "Arena";
            else if (Me.CurrentMap.IsBattleground)
                sRunningAs = "Battleground";
            else if (Me.CurrentMap.IsScenario)
                sRunningAs = "Scenario";
            else if (Me.CurrentMap.IsRaid)
                sRunningAs = "Raid";
            else if (Me.CurrentMap.IsDungeon)
                sRunningAs = "Dungeon";
            else if (Me.CurrentMap.IsInstance)
                sRunningAs = "Instance";
            else
                sRunningAs = "Zone: " + Me.CurrentMap.Name;

            Logger.Write(Color.LightGreen, "... {0} using my {1} Behaviors",
                 sRunningAs,
                 CurrentWoWContext == WoWContext.Normal ? "SOLO" : CurrentWoWContext.ToString().ToUpper());

            if (CurrentWoWContext != WoWContext.Battlegrounds && Me.IsInGroup())
            {
                Logger.Write(Color.LightGreen, "... in a group as {0} role with {1} of {2} players", 
                    (Me.Role & (WoWPartyMember.GroupRole.Tank | WoWPartyMember.GroupRole.Healer | WoWPartyMember.GroupRole.Damage)).ToString().ToUpper(),
                     Me.GroupInfo.NumRaidMembers, 
                     (int) Math.Max(Me.CurrentMap.MaxPlayers, Me.GroupInfo.GroupSize)
                    );
            }

            Item.WriteCharacterGearAndSetupInfo();

            Logger.WriteFile(" ");
            Logger.WriteFile("My Current Dynamic Info");
            Logger.WriteFile("=======================");
            Logger.WriteFile("Combat Reach:    {0:F4}", Me.CombatReach);
            Logger.WriteFile("Bounding Height: {0:F4}", Me.BoundingHeight );
            Logger.WriteFile(" ");

#if LOG_GROUP_COMPOSITION
            if (CurrentWoWContext == WoWContext.Instances)
            {
                int idx = 1;
                Logger.WriteFile(" ");
                Logger.WriteFile("Group Comprised of {0} members as follows:", Me.GroupInfo.NumRaidMembers);
                foreach (var pm in Me.GroupInfo.RaidMembers )
                {
                    string role = (pm.Role & ~WoWPartyMember.GroupRole.None).ToString().ToUpper() + "      ";
                    role = role.Substring( 0, 6);                   
                    
                    Logger.WriteFile( "{0} {1} {2} {3} {4} {5}",
                        idx++, 
                        role,
                        pm.IsOnline ? "online " : "offline",
                        pm.Level,
                        pm.HealthMax,
                        pm.Specialization
                        );
                }

                Logger.WriteFile(" ");
            }
#endif

            if (Styx.CommonBot.Targeting.PullDistance < 25)
                Logger.Write(Color.White, "your Pull Distance is {0:F0} yds which is low for any class!!!", Styx.CommonBot.Targeting.PullDistance);
        }

        private static string SpecializationName()
        {
            if (TalentManager.CurrentSpec == WoWSpec.None)
                return "Lowbie";

            string spec = TalentManager.CurrentSpec.ToString().CamelToSpaced();
            int idxLastSpace = spec.LastIndexOf(' ');
            if (idxLastSpace >= 0 && ++idxLastSpace < spec.Length)
                spec = spec.Substring(idxLastSpace);

            return spec;
        }

        public static string GetBotName()
        {
            BotBase bot = null;

            if (TreeRoot.Current != null)
            {
                if (!(TreeRoot.Current is NewMixedMode.MixedModeEx))
                    bot = TreeRoot.Current;
                else
                {
                    NewMixedMode.MixedModeEx mmb = (NewMixedMode.MixedModeEx)TreeRoot.Current;
                    if (mmb != null)
                    {
                        if (mmb.SecondaryBot != null && mmb.SecondaryBot.RequirementsMet)
                            return "Mixed:" + mmb.SecondaryBot.Name;
                        return mmb.PrimaryBot != null ? "Mixed:" + mmb.PrimaryBot.Name : "Mixed:[primary null]";
                    }
                }
            }

            return bot == null ? "(null)" : bot.Name;
        }

        public static BotBase GetCurrentBotBase()
        {
            BotBase bot = TreeRoot.Current;
            if (bot != null)
            {
                if ((bot is NewMixedMode.MixedModeEx))
                {
                    NewMixedMode.MixedModeEx mmb = (NewMixedMode.MixedModeEx)bot;
                    if (mmb != null)
                    {
                        if (mmb.SecondaryBot != null && mmb.SecondaryBot.RequirementsMet)
                            bot = mmb.SecondaryBot;
                        else
                            bot = mmb.PrimaryBot;
                    }
                }
            }

            return bot;
        }

        public static bool IsBotInUse(params string[] nameSubstrings)
        {
            string botName = GetBotName().ToUpper();
            return nameSubstrings.Any( s => botName.Contains(s.ToUpper()));
        }

        public static PluginContainer FindPlugin(string pluginName)
        {
#if OLD_PLUGIN_CHECK
            var lowerNames = nameSubstrings.Select(s => s.ToLowerInvariant()).ToList();
            bool res = Styx.Plugins.PluginManager.Plugins.Any(p => p.Enabled && lowerNames.Contains(p.Name.ToLowerInvariant()));
            return res;
#else
            foreach (PluginContainer pi in Styx.Plugins.PluginManager.Plugins)
            {
                if (pluginName.Equals(pi.Name, StringComparison.InvariantCultureIgnoreCase))
                {
                    return pi;
                }
            }
            return null;
#endif
        }

        public static bool IsPluginEnabled(params string[] nameSubstrings)
        {
            foreach (string s in nameSubstrings)
            {
                PluginContainer pi = FindPlugin(s);
                if (pi != null)
                    return pi.Enabled;
            }
            return false;
        }

        public static bool SetPluginEnabled(string s, bool enabled)
        {
            PluginContainer pi = FindPlugin(s);
            if (pi != null)
            {
                pi.Enabled = enabled;
                return true;
            }
            return false;
        }

        private static int GetInstanceDifficulty()
        {
			int diffidx = Lua.GetReturnVal<int>("local _,_,d=GetInstanceInfo() if d ~= nil then return d end return 1", 0);
            return diffidx;
        }


        private static readonly string[] InstDiff = new[] 
        {
            /* 0*/  "None; not in an Instance",
            /* 1*/  "5-player Normal",
            /* 2*/  "5-player Heroic",
            /* 3*/  "10-player Raid",
            /* 4*/  "25-player Raid",
            /* 5*/  "10-player Heroic Raid",
            /* 6*/  "25-player Heroic Raid",
            /* 7*/  "LFR Raid Instance",
            /* 8*/  "Challenge Mode Raid",
            /* 9*/  "40-player Raid"
        };

        private static string GetInstanceDifficultyName( )
        {
            int diff = GetInstanceDifficulty();
            if (diff >= InstDiff.Length)
                return string.Format("Difficulty {0} Undefined", diff);

            return InstDiff[diff];
        }

        public bool InCinematic()
        {
            bool inCin = Lua.GetReturnVal<bool>("return InCinematic()", 0);
            return inCin;
        }
    }

    /// <summary>
    /// class to dynamically keep Sequence/Selector content info 
    /// .. without having to define a new class everywhere
    /// </summary>
    public class DynamicContext : DynamicObject
    {
        // The inner dictionary.
        Dictionary<string, object> dictionary
            = new Dictionary<string, object>();

        // number of properties defined in instance 
        public int Count
        {
            get
            {
                return dictionary.Count;
            }
        }

        // If you try to get a value of a property  
        // not defined in the class, this method is called. 
        public override bool TryGetMember( GetMemberBinder binder, out object result)
        {
            // Converting the property name to lowercase 
            // so that property names become case-insensitive. 
            string name = binder.Name.ToLower();

            // If the property name is found in a dictionary, 
            // set the result parameter to the property value and return true. 
            // Otherwise, return false. 
            return dictionary.TryGetValue(name, out result);
        }

        // If you try to set a value of a property that is 
        // not defined in the class, this method is called. 
        public override bool TrySetMember( SetMemberBinder binder, object value)
        {
            // Converting the property name to lowercase 
            // so that property names become case-insensitive.
            dictionary[binder.Name.ToLower()] = value;

            // You can always add a value to a dictionary, 
            // so this method always returns true. 
            return true;
        }
    }
}
