
using System;
using System.Reflection;
using System.Linq;
using System.Windows.Forms;
using Singular.GUI;
using Singular.Helpers;
using Singular.Managers;
using Singular.Utilities;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.WoWInternals.WoWObjects;
using System.Drawing;
using Styx.WoWInternals;
using System.IO;
using System.Collections.Generic;
using Styx.Common;
using Singular.Settings;

using Styx.Common.Helpers;
using Styx.CommonBot.POI;
using System.Text;

namespace Singular
{
    public partial class SingularRoutine : CombatRoutine
    {
        private static LogLevel _lastLogLevel = LogLevel.None;

        public static uint Latency { get; set; }
        private static WaitTimer WaitForLatencyCheck = new WaitTimer(TimeSpan.Zero);

        public static SingularRoutine Instance { get; private set; }

        public override string Name { get { return GetSingularRoutineName(); } }

        public override WoWClass Class { get { return StyxWoW.Me.Class; } }

        public override bool WantButton { get { return true; } }

        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        public CapabilityFlags SupportedCapabilities
        {
            get
            {
                return CapabilityFlags.All;
            }
        }

        public static bool IsAllowed( CapabilityFlags flags)
        {
            return RoutineManager.GetCapabilityState(flags) == CapabilityState.DontCare;
        }

        delegate void OnTargetChange(WoWUnit unit);

        public SingularRoutine()
        {
            Instance = this;
            TrainingDummyBehaviors = WoWContext.Instances;
        }

        public override void Initialize()
        {
            TalentManager.Init();       // initializes CurrentSpec which is referenced everywhere

            SingularSettings.Initialize();

            WriteSupportInfo();

            _lastLogLevel = GlobalSettings.Instance.LogLevel;

            // When we actually need to use it, we will.
            Spell.GcdInitialize();

            EventHandlers.Init();
            MountManager.Init();
            HotkeyDirector.Init();
            MovementManager.Init();
             // SoulstoneManager.Init();   // switch to using Death behavior
            Dispelling.Init();
            PartyBuff.Init();
            Singular.Lists.BossList.Init();

            Targeting.Instance.WeighTargetsFilter += PullMoreWeighTargetsFilter;

            //Logger.Write("Combat log event handler started.");
            // Do this now, so we ensure we update our context when needed.
            BotEvents.Player.OnMapChanged += e =>
            {
                // Don't run this handler if we're not the current routine!
                if (!WeAreTheCurrentCombatRoutine)
                    return;

                // Only ever update the context. All our internal handlers will use the context changed event
                // so we're not reliant on anything outside of ourselves for updates.
                UpdateContext();
            };

            TreeHooks.Instance.HooksCleared += () =>
            {
                // Don't run this handler if we're not the current routine!
                if (!WeAreTheCurrentCombatRoutine)
                    return;

                Logger.Write(Color.White, "Hooks cleared, re-creating behaviors");
                RebuildBehaviors(silent: true);
                Spell.GcdInitialize();   // probably not needed, but quick
            };

            GlobalSettings.Instance.PropertyChanged += (sender, e) =>
            {
                // Don't run this handler if we're not the current routine!
                if (!WeAreTheCurrentCombatRoutine)
                    return;

                // only LogLevel change will impact our behav trees
                // .. as we conditionally include/omit some diagnostic nodes if debugging
                // also need to keep a cached copy of prior value as the event
                // .. fires on the settor, not when the value is different
                if (e.PropertyName == "LogLevel" && _lastLogLevel != GlobalSettings.Instance.LogLevel)
                {
                    _lastLogLevel = GlobalSettings.Instance.LogLevel;
                    Logger.Write(Color.White, "HonorBuddy {0} setting changed to {1}, re-creating behaviors", e.PropertyName, _lastLogLevel.ToString());
                    RebuildBehaviors();
                    Spell.GcdInitialize();   // probably not needed, but quick
                }
            };

            // install botevent handler so we can consolidate validation on whether 
            // .. local botevent handlers should be called or not
            SingularBotEventInitialize();

            Logger.Write("Determining talent spec.");
            try
            {
                TalentManager.Update();
            }
            catch (Exception e)
            {
                StopBot(e.ToString());
            }
            Logger.Write("Current spec is " + TalentManager.CurrentSpec.ToString().CamelToSpaced());

            // write current settings to log file... only written at startup and when Save press in Settings UI
            SingularSettings.Instance.LogSettings();

            // Update the current WoWContext, and fire an event for the change.
            UpdateContext();

            // NOTE: Hook these events AFTER the context update.
            OnWoWContextChanged += (orig, ne) =>
                {
                    Logger.Write(Color.White, "Context changed, re-creating behaviors");
                    SingularRoutine.DescribeContext();
                    RebuildBehaviors();
                    Spell.GcdInitialize();
                    Singular.Lists.BossList.Init();
                };
            RoutineManager.Reloaded += (s, e) =>
                {
                    Logger.Write(Color.White, "Routines were reloaded, re-creating behaviors");
                    RebuildBehaviors(silent:true);
                    Spell.GcdInitialize();
                };


            // create silently since Start button will create a context change (at least first Start)
            // .. which will build behaviors again
            if (!Instance.RebuildBehaviors())
            {
                return;
            }

            //
            if (IsPluginEnabled("DrinkPotions"))
            {
                Logger.Write(Color.White, "info: disabling DrinkPotions plugin, conflicts with Singular potion support");
                SetPluginEnabled("DrinkPotions", false);
            }

            Logger.WriteDebug(Color.White, "Verified behaviors can be created!");
            Logger.Write("Initialization complete!");
        }

        private void PullMoreWeighTargetsFilter(List<Targeting.TargetPriority> units)
        {
            // Singular setting botpoi so if set increase that targets weight
            if (SingularRoutine.IsPullMoreActive && Me.Combat)
            {
                foreach (Styx.CommonBot.Targeting.TargetPriority p in units)
                {
                    WoWUnit u = p.Object.ToUnit();
                    if (BotPoi.Current != null && BotPoi.Current.Guid == u.Guid && !u.IsPlayer && !u.IsTagged)
                        p.Score += 500;
                }
            }
        }

        private static void WriteSupportInfo()
        {
            string singularName = GetSingularRoutineName();  // "Singular v" + GetSingularVersion();
            Logger.Write("Starting " + singularName);

            // save some support info in case we need
            Logger.WriteFile("{0:F1} days since Windows was restarted", TimeSpan.FromMilliseconds(Environment.TickCount).TotalHours / 24.0);
            Logger.WriteFile("{0} FPS currently in WOW", GetFPS());
            Logger.WriteFile("{0} ms of Latency in WOW", SingularRoutine.Latency);
            Logger.WriteFile("{0} local system time", DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss"));

            // verify installed source integrity 
            try
            {
                string singularFolder = GetSingularSourcePath();

                FileCheckList actual = new FileCheckList();
                actual.Generate(singularFolder);

                FileCheckList certified = new FileCheckList();
                certified.Load(Path.Combine(singularFolder, "singular.xml"));

                List<FileCheck> err = certified.Compare(actual);

                List<FileCheck> fcerrors = FileCheckList.Test(GetSingularSourcePath());
                if (!fcerrors.Any())
                    Logger.Write("Installation: integrity verified for {0}", GetSingularVersion());
                else
                {
                  //  Logger.Write(Color.HotPink, "Installation: modified by user - forum support may not be available", singularName);
                    Logger.WriteFile("=== following {0} differ from Singular distribution ===", fcerrors.Count);
                    foreach (var fc in fcerrors)
                    {
                        if ( !File.Exists( fc.Name ))
                            Logger.WriteDiagnostic("   deleted: {0} {1}", fc.Size, fc.Name);
                        else if ( certified.Filelist.Any( f => 0 == String.Compare( f.Name, fc.Name, true)))
                            Logger.WriteDiagnostic("   changed: {0} {1}", fc.Size, fc.Name);
                        else
                            Logger.WriteDiagnostic("   inserted {0} {1}", fc.Size, fc.Name);
                    }
                    Logger.WriteFile(Styx.Common.LogLevel.Diagnostic, "");
                }
            }
            catch (FileNotFoundException e)
            {
                Logger.Write(Color.HotPink, "Installation: file missing - forum support not available");
                Logger.Write(Color.HotPink, "missing file: {0}", e.FileName );
            }
            catch (Exception e)
            {
                Logger.Write(Color.HotPink, "Installation: verification error - forum support not available");
                Logger.WriteFile(e.ToString());
            }
        }

        /// <summary>
        /// Stop the Bot writing a reason to the log file.  
        /// Revised to account for TreeRoot.Stop() now 
        /// throwing an exception if called too early 
        /// before tree is run
        /// </summary>
        /// <param name="reason">text to write to log as reason for Bot Stop request</param>
        private static void StopBot(string reason)
        {
            if (!TreeRoot.IsRunning)
                reason = "Bot Cannot Run: " + reason;
            else
            {
                reason = "Stopping Bot: " + reason;

                if (countRentrancyStopBot == 0)
                {
                    countRentrancyStopBot++;
                    if (TreeRoot.Current != null)
                        TreeRoot.Current.Stop();

                    TreeRoot.Stop();
                }
            }

            Logger.Write(Color.HotPink,reason);
        }

        static int countRentrancyStopBot = 0;

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private static uint GetFPS()
        {
            try
            {
                return (uint)Lua.GetReturnVal<float>("return GetFramerate()", 0);
            }
            catch
            {

            }

            return 0;
        }

        public static string GetSingularRoutineName()
        {
            return "Singular v" + GetSingularVersion();
        }

        public static string GetSingularSourcePath()
        {
            // bit of a hack, but source code folder for the assembly is only 
            // .. available from the filename of the .dll
            FileCheck fc = new FileCheck();
            Assembly singularDll = Assembly.GetExecutingAssembly();
            FileInfo fi = new FileInfo(singularDll.Location);
            int len = fi.Name.LastIndexOf("_");
            string folderName = fi.Name.Substring(0, len);

            folderName = Path.Combine(Styx.Helpers.GlobalSettings.Instance.CombatRoutinesPath, folderName);

            // now check if relative path and if so, append to honorbuddy folder
            if (!Path.IsPathRooted(folderName))
                folderName = Path.Combine(GetHonorBuddyFolder(), folderName);

            return folderName;
        }

        public static string GetHonorBuddyFolder()
        {
            string hbpath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            return hbpath;
        }

        public static bool WeAreTheCurrentCombatRoutine
        {
            get
            {
                return RoutineManager.Current.Name == SingularRoutine.Instance.Name;
            }
        }

        private static bool IsMounted
        {
            get
            {
                if (StyxWoW.Me.Class == WoWClass.Druid)
                {
                    switch (StyxWoW.Me.Shapeshift)
                    {
                        case ShapeshiftForm.FlightForm:
                        case ShapeshiftForm.EpicFlightForm:
                            return true;
                    }
                }

                return StyxWoW.Me.Mounted;
            }
        }

        private ConfigurationForm _configForm;
        public override void OnButtonPress()
        {
            if (_configForm == null || _configForm.IsDisposed || _configForm.Disposing)
            {
                _configForm = new ConfigurationForm();
                _configForm.Height = SingularSettings.Instance.FormHeight;
                _configForm.Width = SingularSettings.Instance.FormWidth;
                TabControl tab = (TabControl)_configForm.Controls["tabControl1"];
                tab.SelectedIndex = SingularSettings.Instance.FormTabIndex;
            }

            _configForm.Show();
        }

        static DateTime _nextNoCallMsgAllowed = DateTime.MinValue;

        static DateTime _nextNotInGameMsgAllowed = DateTime.MinValue;
        static DateTime _nextNotInWorldMsgAllowed = DateTime.MinValue;
        static DateTime _nextCombatDisabledMsgAllowed = DateTime.MinValue;


        public override void Pulse()
        {

#region Pulse - check for conditions that we should not Pulse during

            if (!StyxWoW.IsInGame)
            {
                if (DateTime.Now > _nextNotInGameMsgAllowed)
                {
                    Logger.WriteDebug(Color.HotPink, "info: not in game");
                    _nextNotInGameMsgAllowed = DateTime.Now.AddSeconds(30);
                }
                return;
            }
            _nextNotInGameMsgAllowed = DateTime.MinValue;

            if (!StyxWoW.IsInWorld)
            {
                if (DateTime.Now > _nextNotInWorldMsgAllowed)
                {
                    Logger.WriteDebug(Color.HotPink, "info: not in world");
                    _nextNotInWorldMsgAllowed = DateTime.Now.AddSeconds(30);
                }
                return;
            }
            _nextNotInWorldMsgAllowed = DateTime.MinValue;

            if (WaitForLatencyCheck.IsFinished)
            {
                Latency = StyxWoW.WoWClient.Latency;
                WaitForLatencyCheck.Reset();
            }

            // output messages about pulldistance and behaviorflag changes here
            MonitorPullDistance();
            MonitorBehaviorFlags();

            // now if combat disabled, bail out 
            if (Bots.Grind.BehaviorFlags.Combat != (Bots.Grind.LevelBot.BehaviorFlags & Bots.Grind.BehaviorFlags.Combat))
                return;

            _nextCombatDisabledMsgAllowed = DateTime.MinValue;

#endregion

            // check time since last call and be sure user knows if Singular isn't being called
            if (SingularSettings.Debug)
            {
                TimeSpan since = CallWatch.TimeSpanSinceLastCall;
                if (since.TotalSeconds > (4 * CallWatch.SecondsBetweenWarnings))
                {
                    if (!Me.IsGhost && !Me.Mounted && !Me.IsFlying && DateTime.Now > _nextNoCallMsgAllowed)
                    {
                        Logger.WriteDebug(Color.HotPink, "info: {0:F0} seconds since {1} BotBase last called Singular", since.TotalSeconds, GetBotName());
                        _nextNoCallMsgAllowed = DateTime.Now.AddSeconds(4 * CallWatch.SecondsBetweenWarnings);
                    }
                }
            }

            // talentmanager.Pulse() intense if does work, so return if true
            if (TalentManager.Pulse())
                return;

            // check and output casting state information
            UpdateDiagnosticCastingState();

            UpdatePullMoreConditionals();

            // Update the current context, check if we need to rebuild any behaviors.
            UpdateContext();

            // Double cast shit
            Spell.DoubleCastPreventionDict.RemoveAll(t => DateTime.UtcNow > t);

            // Output if Target changed 
            CheckCurrentTarget();

            // Output if Pet or Pet Target changed
            CheckCurrentPet();

            // Pulse our StopAt manager
            StopMoving.Pulse();

            //Only pulse for classes with pets
            switch (StyxWoW.Me.Class)
            {
                case WoWClass.Hunter:
                case WoWClass.DeathKnight:
                case WoWClass.Warlock:
                case WoWClass.Mage:
                    PetManager.Pulse();
                    break;
            }

            if (HealerManager.NeedHealTargeting)
            {
                BotBase bot = GetCurrentBotBase();
                if (bot != null && (bot.PulseFlags & PulseFlags.Targeting) != PulseFlags.Targeting)
                {
                    HealerManager.Instance.Pulse();
                }
            }

            if (CurrentWoWContext == WoWContext.Instances && Me.IsInGroup() && Group.MeIsTank)
                TankManager.Instance.Pulse();

            HotkeyDirector.Pulse();
        }

        private static ulong _lastPetGuid = 0;
        private static bool _lastPetAlive;

        private void CheckCurrentPet()
        {
            if (!SingularSettings.Debug)
                return;

            if (Me.Pet == null)
            {
                if (_lastPetGuid != 0)
                {
                    _lastPetGuid = 0;
                    if (SingularSettings.Debug)
                        Logger.WriteDebug("YourCurrentPet: (none)");
                }
            }
            else 
            {
                // check for change in current pet
                if ( Me.Pet.Guid != _lastPetGuid || Me.Pet.IsAlive != _lastPetAlive)
                {
                    _lastPetGuid = Me.Pet.Guid;
                    _lastPetAlive = Me.Pet.IsAlive;
                    Logger.WriteDebug("YourCurrentPet: #{0}, Name={1}, Level={2}, Type={3}, Talents={4}", Me.PetNumber, Me.Pet.Name, Me.Pet.Level, Me.Pet.CreatureType, PetManager.GetPetTalentTree());
                }

                // now check pets target
                CheckTarget(Me.Pet.CurrentTarget, ref _lastCheckPetsTargetGuid, "PetsCurrentTarget", (x) => { });
            }

        }

        private static ulong _lastCheckCurrTargetGuid = 0;
        private static ulong _lastCheckPetsTargetGuid = 0;

        private void CheckCurrentTarget()
        {
            CheckTarget(Me.CurrentTarget, ref _lastCheckCurrTargetGuid, "YourCurrentTarget", OnPlayerTargetChange);
        }


        private void CheckTarget(WoWUnit unit, ref ulong prevGuid, string description, OnTargetChange onchg)
        {
            // there are moments where CurrentTargetGuid != 0 but CurrentTarget == null. following
            // .. tries to handle by only checking CurrentTarget reference and treating null as guid = 0
            if (unit == null)
            {
                if (prevGuid != 0)
                {
                    prevGuid = 0;
                    onchg(unit);
                    if (SingularSettings.Debug)
                        Logger.WriteDebug(description + ": changed to: (null)");
                }
            }
            else if (unit.Guid != prevGuid)
            {
                prevGuid = unit.Guid;
                onchg(unit);
                if (SingularSettings.Debug)
                {
                    string info = "";
                    if (Styx.CommonBot.POI.BotPoi.Current.Guid == Me.CurrentTargetGuid)
                        info += string.Format(", IsBotPoi={0}", Styx.CommonBot.POI.BotPoi.Current.Type);

                    if (Styx.CommonBot.Targeting.Instance.TargetList.Contains(Me.CurrentTarget))
                        info += string.Format(", TargetIndex={0}", Styx.CommonBot.Targeting.Instance.TargetList.IndexOf(Me.CurrentTarget) + 1);

                    string playerInfo = "N";
                    if (unit.IsPlayer)
                    {
                        WoWPlayer p = unit.ToPlayer();
                        playerInfo = string.Format("Y, Friend={0}, IsPvp={1}, CtstPvp={2}, FfaPvp={3}", Me.IsHorde == p.IsHorde, p.IsPvPFlagged, p.ContestedPvPFlagged, p.IsFFAPvPFlagged);
                    }
                    else
                    {
                        info += string.Format(", tagme={0}, tagother={1}, tapall={2}",
                            unit.TaggedByMe.ToYN(),
                            unit.TaggedByOther.ToYN(),
                            unit.TappedByAllThreatLists.ToYN()
                            );
                    }

                    Logger.WriteDebug(description + ": changed to: {0} h={1:F1}%, maxh={2}, d={3:F1} yds, box={4:F1}, boss={5}, trivial={6}, player={7}, attackable={8}, neutral={9}, hostile={10}, entry={11}, faction={12}, loss={13}, facing={14}, blacklist={15}, combat={16}, flying={17}, abovgrnd={18}" + info,
                        unit.SafeName(),
                        unit.HealthPercent,
                        unit.MaxHealth,
                        unit.Distance,
                        unit.CombatReach,
                        unit.IsBoss().ToYN(),
                        unit.IsTrivial().ToYN(),
                        playerInfo,

                        unit.Attackable.ToYN(),
                        unit.IsNeutral().ToYN(),
                        unit.IsHostile.ToYN(),
                        unit.Entry,
                        unit.FactionId,
                        unit.InLineOfSpellSight.ToYN(),
                        Me.IsSafelyFacing(unit).ToYN(),
                        Blacklist.Contains(unit.Guid, BlacklistFlags.Combat).ToYN(),
                        unit.Combat.ToYN(),
                        unit.IsFlying.ToYN(),
                        unit.IsAboveTheGround().ToYN()
                        );
                }
            }
        }

        private static void OnPlayerTargetChange(WoWUnit unit)
        {
            // special handling if targeting Training Dummy
            if (ForcedContext == WoWContext.None && unit != null && !IsQuestBotActive && unit.IsTrainingDummy())
            {
                ForcedContext = SingularRoutine.TrainingDummyBehaviors;
                Logger.Write(Color.White, "^Detected Training Dummy: forcing {0} behaviors", CurrentWoWContext.ToString());
            }
            else if (ForcedContext != WoWContext.None && (unit == null || !unit.IsTrainingDummy()))
            {
                ForcedContext = WoWContext.None;
                Logger.Write(Color.White, "^Detected Training Dummy: reverting to {0} behaviors", CurrentWoWContext.ToString());
            }
        }

        private static bool _lastIsGCD = false;
        private static bool _lastIsCasting = false;
        private static bool _lastIsChanneling = false;
        private static DateTime _nextAbcWarning = DateTime.MinValue;
        public static bool UpdateDiagnosticCastingState( bool retVal = false)
        {
            if (SingularSettings.Debug && SingularSettings.DebugSpellCasting)
            {
                if (_lastIsGCD != Spell.IsGlobalCooldown())
                {
                    _lastIsGCD = Spell.IsGlobalCooldown();
                    Logger.WriteDebug("CastingState:  GCD={0} GCDTimeLeft={1}", _lastIsGCD, (int)Spell.GcdTimeLeft.TotalMilliseconds);
                }
                if (_lastIsCasting != Spell.IsCasting())
                {
                    _lastIsCasting = Spell.IsCasting();
                    Logger.WriteDebug("CastingState:  Casting={0} CastTimeLeft={1}", _lastIsCasting, (int)Me.CurrentCastTimeLeft.TotalMilliseconds);
                }
                if (_lastIsChanneling != Spell.IsChannelling())
                {
                    _lastIsChanneling = Spell.IsChannelling();
                    Logger.WriteDebug("ChannelingState:  Channeling={0} ChannelTimeLeft={1}", _lastIsChanneling, (int)Me.CurrentChannelTimeLeft.TotalMilliseconds);
                }

                /// Special: provide diagnostics if healer 
                if ( HealerManager.NeedHealTargeting && (_nextAbcWarning < DateTime.Now) && !Me.IsCasting && !Me.IsChanneling && !Spell.IsGlobalCooldown(LagTolerance.No))
                {
                    WoWUnit low = HealerManager.FindLowestHealthTarget();
                    if (low != null )
                    {
                        float lh = low.PredictedHealthPercent();
                        if ((SingularSettings.Instance.HealerCombatAllow && HealerManager.CancelHealerDPS()) || (!SingularSettings.Instance.HealerCombatAllow && lh < 70))
                        {
                            Logger.WriteDebug("Healer ABC Warning: no cast in progress detected, low health {0} {1:F1}% @ {2:F1} yds", low.SafeName(), lh, low.SpellDistance());
                            _nextAbcWarning = DateTime.Now + TimeSpan.FromSeconds(1);
                        }
                    }
                }
            }
            return retVal;
        }
    }
}