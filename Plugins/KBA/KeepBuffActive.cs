using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;

using KBA.GUI;
using KBA.Helpers;

using Styx;
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.Helpers;
using Styx.Localization;
using Styx.Plugins;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace KBA
{

    public class KeepBuffActive : HBPlugin
    {
        #region not HB related
        public static string GlobalSettingsPath { get { return string.Format("{0}\\Settings\\KBA\\", Utilities.AssemblyDirectory); } }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (!m_bDirty)
            {
                m_Sb.Remove(0, m_Sb.Length);
                m_Sb.Append(e.FullPath);
                m_Sb.Append(" ");
                m_Sb.Append(e.ChangeType.ToString());
                m_Sb.Append("    ");
                m_Sb.Append(DateTime.Now.ToString());
                Logger.InfoLog(m_Sb.ToString());
                m_bDirty = true;
            }
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            if (!m_bDirty)
            {
                m_Sb.Remove(0, m_Sb.Length);
                m_Sb.Append(e.OldFullPath);
                m_Sb.Append(" ");
                m_Sb.Append(e.ChangeType.ToString());
                m_Sb.Append(" ");
                m_Sb.Append("to ");
                m_Sb.Append(e.Name);
                m_Sb.Append("    ");
                m_Sb.Append(DateTime.Now.ToString());
                Logger.InfoLog(m_Sb.ToString());
                m_bDirty = true;
            }
        }

        #endregion
        #region HB related stuff
        public override string Name {get { return ("KBA"); } }

        public override string Author { get { return "Stormchasing"; } }

        public override Version Version { get { return new Version(1, 0, 3); } }

        public override bool WantButton { get { return true; } }

        public override string ButtonText { get { return "Setup your Buffs"; } }
        private static IEnumerable<WoWAura> currentAuras;
        // timer used in our behavior tree
        private static WaitTimer waitNextMessage = WaitTimer.FiveSeconds;
        private StringBuilder m_Sb;
        private bool m_bDirty;
        private System.IO.FileSystemWatcher m_Watcher;
        private bool m_bIsWatching;
        public KeepBuffActive()
        { 
        }

        /// <summary>
        /// Keeps the current ids of possible HeroismBuffs
        /// </summary>
        private static HashSet<int> HeroismBuff = new HashSet<int>
        {
            32182, //Bloodlust
            2825, // Heroism
            80353, // Timewarp
            90355, // Ancient Hysteria
        };

        /// <summary>
        /// Initialize() equals Enabled
        /// ---
        /// called when user clicks the enable checkbox for the plugin, -or when
        /// HonorBuddy loads plugins initially if already initialized
        /// </summary>

        public override void OnEnable()
        {
            base.OnEnable();
            Logger.InfoLog("Enabled");
            // create events and initialization
            m_Sb = new StringBuilder();
            m_bDirty = false;
            m_bIsWatching = false;

            if (m_bIsWatching)
            {
                m_bIsWatching = false;
                m_Watcher.EnableRaisingEvents = false;
                m_Watcher.Dispose();
            }
            else
            {
                var toWatch = string.Format("{0}\\Settings\\KBA", Utilities.AssemblyDirectory);
                if (System.IO.Directory.Exists(toWatch))
                {
                    Logger.InfoLog("Existing Installation, we're watching {0}",toWatch);
                    m_bIsWatching = true;

                    m_Watcher = new System.IO.FileSystemWatcher();
                    m_Watcher.Filter = "*.xml";
                    m_Watcher.Path = toWatch;//GlobalSettingsPath;

                    m_Watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite
                                         | NotifyFilters.FileName | NotifyFilters.DirectoryName;
                    m_Watcher.Changed += new FileSystemEventHandler(OnChanged);
                    m_Watcher.Created += new FileSystemEventHandler(OnChanged);
                    m_Watcher.Deleted += new FileSystemEventHandler(OnChanged);
                    m_Watcher.Renamed += new RenamedEventHandler(OnRenamed);
                    m_Watcher.EnableRaisingEvents = true;
                }
                else
                {
                    Logger.InfoLog("Fresh install, we are initializing settings");
                }
            }

            // hook events
            BotEvents.OnBotStarted += OnBotStartHandler;
            BotEvents.OnBotStopped += OnBotStopHandler;
        }

        /// <summary>
        /// called when user unchecks your plugin -or- when HonorBuddy is closing.  Any 
        /// resource, hook, or event you implemented in Initialize() must be released here 
        /// to avoid the side-effects of your plugin continuing to run after the user disables
        /// </summary>
        public override void OnDisable()
        {
            Logger.InfoLog("Disabled");
            if (m_bIsWatching)
            {
                m_bIsWatching = false;
                m_Watcher.EnableRaisingEvents = false;
                m_Watcher.Dispose();
            }
            // unhook events
            BotEvents.OnBotStarted -= OnBotStartHandler;
            BotEvents.OnBotStopped -= OnBotStopHandler;

            // stop the behavior tree and release
            base.OnDisable();
        }


        /// <summary>
        /// Should be called to handle the internal logic of item and spell usage
        /// </summary>
        /// 
        public static void HandleItemOrSpell(BuffEntry s)
        {
            string Description = s.ObjectName; ;
            string ItemName = s.ObjectName;
            string BuffName = s.BuffName;
            int ItemId = s.ObjectId;
            int BuffId = s.BuffId;
            bool IsObjectSpell = false;
            bool IsObjectItem = false;
            bool MeHasAura = false;
            WoWItem item = null;

            if (currentAuras == null) return;
            //Check the Buff
            if (BuffId > 0)
            {
                MeHasAura = currentAuras.Any<WoWAura>(a => a.SpellId == BuffId);
            }
            else
            {
                if(BuffName.Length>0)
                MeHasAura = currentAuras.Any<WoWAura>(a => a.Name == BuffName);
            
            }
            //Check the ObjectId
            if (ItemId > 0)
            {
                IsObjectSpell = SpellManager.HasSpell(ItemId);
                item = StyxWoW.Me.BagItems.FirstOrDefault(x => x.Entry == ItemId && x.Usable && x.Cooldown <= 0);
                IsObjectItem = item!=null;

            }
            else
            {
                if (ItemName.Length > 0)
                {
                    IsObjectSpell = SpellManager.HasSpell(ItemName);
                    item = StyxWoW.Me.BagItems.FirstOrDefault(x => x.Name == ItemName && x.Usable && x.Cooldown <= 0);
                    IsObjectItem = item!=null;
                }
            }
            //Try to use the Spell
            if (IsObjectSpell && !MeHasAura)
            {
                Logger.InfoLog("Refreshing Spell {0}", s);

                if (ItemId >= 0)
                {
                    if (SpellManager.CanCast(ItemId))
                    {
                        SpellManager.Cast(ItemId, StyxWoW.Me);
                        Logger.InfoLog("Casting Spell by ObjectID {0}", s);
                    }
                }
                else 
                {
                    if (ItemName.Length > 0)
                    {
                        SpellManager.Cast(ItemName, StyxWoW.Me);
                        Logger.InfoLog("Casting Spell by ObjectName {0}", s);
                    }
                }
            }
            //Try to use the Item
            if (IsObjectItem && !MeHasAura)
            {
                Logger.InfoLog("Refreshing Item {0}", s);
                Logger.InfoLog(" [BagItem] {0} - {1}", item.Entry, item.Name);
                item.UseContainerItem();
            }
        }


        /// <summary>
        /// Pulse() 
        /// </summary>
        public override void Pulse()
        {
            try
            {
                if (!StyxWoW.Me.IsValid || StyxWoW.GameState == GameState.Zoning || !StyxWoW.IsInWorld) return;
                if (!StyxWoW.Me.Mounted && !StyxWoW.Me.IsDead && !StyxWoW.Me.IsGhost && !StyxWoW.Me.IsFlying && !StyxWoW.Me.OnTaxi && !StyxWoW.Me.IsOnTransport)
                {
                    var itList = Buffs.Instance.SpellList.Spells.Where(q => q.BuffLocation != BuffWhere.NoWhere && q.BuffCondition != BuffWhen.Never);
                    if (itList.Count() > 0)
                    {
                        var cMap = StyxWoW.Me.CurrentMap;
                        currentAuras = StyxWoW.Me.GetAllAuras();
                        foreach (var itUse in itList)
                        {
                            bool execCast = true;
                            BuffWhere loc = itUse.BuffLocation;
                            BuffWhen when = itUse.BuffCondition;
                            #region check BuffLocation
                            switch (loc)
                            {
                                case BuffWhere.Everywhere:
                                    break;
                                case BuffWhere.Dungeon:
                                    if (!cMap.IsScenario && !cMap.IsDungeon && !cMap.IsInstance)
                                        execCast = false;
                                    break;
                                case BuffWhere.Party:
                                    if (!StyxWoW.Me.GroupInfo.IsInParty)
                                        execCast = false;
                                    break;
                                case BuffWhere.Raid:
                                    if (!cMap.IsRaid)
                                        execCast = false;
                                    if (!StyxWoW.Me.GroupInfo.IsInRaid)
                                        execCast = false;
                                    break;
                                case BuffWhere.PartyOrRaid:
                                    if (!StyxWoW.Me.GroupInfo.IsInRaid && !StyxWoW.Me.GroupInfo.IsInParty)
                                        execCast = false;
                                    break;
                                case BuffWhere.TimelessIsle:
                                    if (StyxWoW.Me.ZoneId != 6757)
                                        execCast = false;
                                    break;
                                default:
                                    execCast = false;
                                    break;
                            }
                            #endregion

                            #region check BuffCondition
                            switch (when)
                            {
                                case BuffWhen.Everytime:
                                    break;
                                case BuffWhen.HeroismOrBloodlust:
                                    if (!StyxWoW.Me.HasAnyAura(HeroismBuff))
                                        execCast = false;
                                    break;
                                case BuffWhen.LowHp:
                                    if (StyxWoW.Me.HealthPercent > 40)
                                        execCast = false;
                                    break;
                                case BuffWhen.LowMana:
                                    if (StyxWoW.Me.ManaPercent > 25)
                                        execCast = false;
                                    break;
                                default:
                                    execCast = false;
                                    break;
                            }
                            #endregion
                            #region Build the casting
                            if (execCast == true)
                            {
                                HandleItemOrSpell(itUse);
                            }
                            #endregion
                        }
                    }
                }
                return;
            }
            catch (Exception e)
            {
                // Restart on any exception.
                Logger.DebugLog(e.ToString());
                throw;
            }
        }

        /// <summary>
        /// What to do when the Button is pressed
        /// </summary>
        public override void OnButtonPress()
        {
            var config = new ConfigForm();
            config.ShowDialog();
        }
        /// <summary>
        /// sample Bot Start event handler
        /// </summary>
        public void OnBotStartHandler(EventArgs args)
        {
            //CreateFileWatcher
            SpellList s = Buffs.Instance.SpellList;
            Logger.InfoLog("Bot started, local plugin data initialized.");
            Buffs.Instance.SpellList.LogEntries();
        }

        /// <summary>
        /// sample Bot Stop event handler
        /// </summary>
        public void OnBotStopHandler(EventArgs args)
        {
            Logger.InfoLog("Bot stopped, local plugin data reset.");
            Buffs.Instance.SpellList.LogEntries();
        }
    #endregion

    }
    internal static class UnitExtensions
    {
        public static bool HasAnyAura(this WoWUnit unit, params string[] auraNames)
        {
            var auras = unit.GetAllAuras();
            var hashes = new HashSet<string>(auraNames);
            return auras.Any(a => hashes.Contains(a.Name));
        }

        public static bool HasAnyAura(this WoWUnit unit, params int[] auraIDs)
        {
            return auraIDs.Any(unit.HasAura);
        }

        public static bool HasAnyAura(this WoWUnit unit, HashSet<int> auraIDs)
        {
            var auras = unit.GetAllAuras();
            return auras.Any(a => auraIDs.Contains(a.SpellId));
        }
    }
}
