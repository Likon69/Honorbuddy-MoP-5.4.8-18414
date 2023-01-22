/*
 * NOTE:    DO NOT POST ANY MODIFIED VERSIONS OF THIS TO THE FORUMS.
 * 
 *          DO NOT UTILIZE ANY PORTION OF THIS PLUGIN WITHOUT
 *          THE PRIOR PERMISSION OF AUTHOR.  PERMITTED USE MUST BE
 *          ACCOMPANIED BY CREDIT/ACKNOWLEDGEMENT TO ORIGINAL AUTHOR.
 * 
 * Author:  Bobby53
 * 
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Bobby53;
using CommonBehaviors.Actions;
using Levelbot.Actions.Combat;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Sequence = Styx.TreeSharp.Sequence;
using System.Windows.Media;

namespace Styx.Bot.CustomBots
{
    public partial class LazyRaider : BotBase
    {
        #region Property Decls

        public static readonly Version Version = new Version(2, 0, 16);

        public static LocalPlayer Me { get { return StyxWoW.Me; } }
        public static bool IsInGroup { get { return Me.GroupInfo.IsInRaid || Me.GroupInfo.IsInParty; } }
        public static List<WoWPlayer> GroupMembers { get { return !Me.GroupInfo.IsInRaid ? Me.PartyMembers : Me.RaidMembers; } }
        public static IEnumerable<WoWPartyMember> GroupMemberInfos { get { return !Me.GroupInfo.IsInRaid ? Me.GroupInfo.PartyMembers : Me.GroupInfo.RaidMembers; } }


        public static WoWPoint _lastDest;
        public static bool haveFoundTankAtGroupForming;
        public static bool IamTheTank = false;
        public static byte PreviousTicksPerSecond;

        public static bool Paused = false;

        public static PulseFlags pulseFlags;

        public SelectTankForm _frm;
        public static bool ImaTank { get { return GetGroupRoleAssigned(Me) == WoWPartyMember.GroupRole.Tank; } }

        private static Composite _root;
        private const string LazyRaiderHookName = "LazyRaider.Root";

        #endregion

        #region Overrides of BotBase

        public override string Name
        {
            get { return "LazyRaider"; }
        }

        public override System.Windows.Forms.Form ConfigurationForm
        {
            get
            {
                if (_frm == null)
                    _frm = new SelectTankForm();
                return _frm;
            }
        }

        public override Composite Root
        {
            get
            {
                if (_root == null)
                    _root = new HookExecutor(LazyRaiderHookName);
                return _root;
            }
        }

        public static void RebuildBehaviors()
        {
            Composite lazyRoot = null;
            if (LazyRaiderSettings.Instance.RaidBot)
            {
                lazyRoot = new Decorator(
                    ret => StyxWoW.Me.Combat,

                    CreateFrameFactory(
                        RoutineManager.Current.HealBehavior,
                        RoutineManager.Current.CombatBuffBehavior ?? new Action(ret => { return RunStatus.Failure; }),
                        new Decorator(
                            ret => StyxWoW.Me.GotTarget && !StyxWoW.Me.CurrentTarget.IsFriendly,
                            RoutineManager.Current.CombatBehavior
                            )
                        )
                    );
            }
            else
            {
                lazyRoot = new PrioritySelector(
                            new Decorator(ret => Paused, new ActionAlwaysSucceed()),
                            new Decorator(ret => !(Me.Mounted && LazyRaiderSettings.Instance.DismountOnlyWithTankOrUser),
                                CreateCombatBehavior()
                                ),
                            CreateDetectTankBehavior(),
                            CreateFollowBehavior()
                            );
            }

            TreeHooks.Instance.ReplaceHook(LazyRaiderHookName, lazyRoot);
        }

        public override PulseFlags PulseFlags
        {
            get
            {
                return !Paused ? pulseFlags : PulseFlags.Objects | PulseFlags.Lua;
            }
        }

        public override bool RequirementsMet { get { return true; } }
        public override bool RequiresProfile { get { return false; } }
        public override bool RequiresProfileScope { get { return false; } }

        public override void Initialize()
        {
            Log("Version {0} initialized", Version);
            if (RoutineManager.Current == null)
                Dlog("no combatclass is loaded");
            else
                Dlog("{0} Combat Class: {1} loaded", RoutineManager.Current.Class.ToString(), RoutineManager.Current.Name);

            Dlog("=== Active Plugins");
            if (CharacterSettings.Instance.EnabledPlugins != null)
            {
                foreach (string pi in CharacterSettings.Instance.EnabledPlugins)
                {
                    Dlog("{0}", pi);
                }
            }

            Dlog("");
            LazyRaiderSettings.Instance.Load();
            Styx.CommonBot.Profiles.ProfileManager.LoadEmpty();
            RefreshSettingsCache();
            Log("Blank Profile loaded");

            base.Initialize();
        }

        public override void Start()
        {
            RebuildBehaviors();

            PreviousTicksPerSecond = TreeRoot.TicksPerSecond;

            Dlog("Start: currfps={0:F0} maxfps={1} combat={2} oldtps={3}", GetFramerate(), MaxFPS(), Me.Combat, TreeRoot.TicksPerSecond);
            BotEvents.Player.OnMapChanged += Player_OnMapChanged;

            TargetFilterSetup();

            Lua.Events.AttachEvent("PARTY_MEMBERS_CHANGED", HandlePartyMembersChanged);
            Lua.Events.AttachEvent("MODIFIER_STATE_CHANGED", HandleModifierStateChanged);
            Styx.Helpers.GlobalSettings.Instance.LogoutForInactivity = false;
            Log("Version {0} Started", Version);

            if (LazyRaiderSettings.Instance.PauseWhileKeyPressed)
                Paused = false;

            if (Paused)
                Log(Colors.Orange, "LazyRaider currently PAUSED - Press {0} in WOW to continue...", LazyRaiderSettings.Instance.PauseKey.ToString());
            else if (LazyRaiderSettings.Instance.PauseKey != LazyRaiderSettings.Keypress.None)
                Log(Colors.Orange, "Pause LazyRaider anytime by pressing {0} key in WOW", LazyRaiderSettings.Instance.PauseKey.ToString());
            else
                Log(Colors.Orange, "LazyRaider Pause feature not active");

            _lastDest = new WoWPoint();
        }

        public override void Stop()
        {
            TreeRoot.TicksPerSecond = PreviousTicksPerSecond;
            Lua.Events.DetachEvent("MODIFIER_STATE_CHANGED", HandleModifierStateChanged);
            Lua.Events.DetachEvent("PARTY_MEMBERS_CHANGED", HandlePartyMembersChanged);

            TargetFilterClear();

            BotEvents.Player.OnMapChanged -= Player_OnMapChanged;
            Styx.Helpers.GlobalSettings.Instance.LogoutForInactivity = true;
            Log("Version {0} Stopped", Version);
        }

        #endregion

        #region MISC

        public static void RefreshSettingsCache()
        {
            // reset the BT so values can be applied next tick
            RebuildBehaviors();

            // set pulseFlags
            pulseFlags = PulseFlags.Objects | PulseFlags.Lua;

            if (LazyRaiderSettings.Instance.AutoTarget && !LazyRaiderSettings.Instance.RaidBot)
                pulseFlags |= PulseFlags.Targeting;

            if (!LazyRaiderSettings.Instance.DisablePlugins)
                pulseFlags |= PulseFlags.Plugins;

            if (LazyRaiderSettings.Instance.CharacterManager)
                pulseFlags |= PulseFlags.CharacterManager;

            // set the FPS to use
            TreeRoot.TicksPerSecond = (byte)LazyRaiderSettings.Instance.FPS;

            Dlog("RaidBot set to {0}", LazyRaiderSettings.Instance.RaidBot);
            Dlog("NoTank set to {0}", LazyRaiderSettings.Instance.NoTank);
            Dlog("FollowTank set to {0}", LazyRaiderSettings.Instance.FollowTank);
            Dlog("FollowDistance set to {0}", LazyRaiderSettings.Instance.FollowDistance);
            Dlog("AutoTankSelect set to {0}", LazyRaiderSettings.Instance.AutoTankSelect);
            Dlog("AutoTarget set to {0}", LazyRaiderSettings.Instance.AutoTarget);
            Dlog("AutoAttack set to {0}", LazyRaiderSettings.Instance.AutoAttack);
            Dlog("CharacterManager set to {0}", LazyRaiderSettings.Instance.CharacterManager);

            if (LazyRaiderSettings.Instance.RaidBot)
                Log("RaidBot mode active, all targeting/following disabled");
            else if (LazyRaiderSettings.Instance.PauseKey == LazyRaiderSettings.Keypress.None)
                Log("FPS={0}, DisablePlugins={1}, LockMemory={2}, NoPauseKey", LazyRaiderSettings.Instance.FPS, LazyRaiderSettings.Instance.DisablePlugins, LazyRaiderSettings.Instance.LockMemory);
            else
                Log("FPS={0}, DisablePlugins={1}, LockMemory={2}, PauseKey={3}", LazyRaiderSettings.Instance.FPS, LazyRaiderSettings.Instance.DisablePlugins, LazyRaiderSettings.Instance.LockMemory, LazyRaiderSettings.Instance.PauseKey );
        }

        public static bool IsGameStable()
        {
            return StyxWoW.IsInGame && Me != null && Me.IsValid;
        }

        public static int MaxFPS()
        {
            double fps = 0;
            string strFps = GetCVar("maxFPS");
            if (!double.TryParse(strFps, out fps))
                Log("error: MaxFPS CVar reported unexpected value '{0}'", strFps);

            return (int) fps;
        }

        public static string GetCVar(string cvar)
        {
            try
            {
                List<string> ret = Lua.GetReturnValues("return GetCVar(\"" + cvar + "\")");
                if (ret != null && ret.Count > 0)
                {
                    return ret[0];
                }
            }
            catch
            {
            }
            return String.Empty;
        }

        public static double GetFramerate()
        {
            double dRate = 0;
            List<string> ret = Lua.GetReturnValues("return GetFramerate()");
            if (ret != null && ret.Count > 0)
            {
                double.TryParse(ret[0], out dRate);
            }
            return dRate;
        }

        private static uint lineCount = 0;

        public static void Status(string msg, params object[] args)
        {
            TreeRoot.StatusText = "[Lazy] " + String.Format(msg, args);
        }

        public static void Log(string msg, params object[] args)
        {
            Log(Colors.Gold, msg, args);
        }

        public static void Log(Color clr, string msg, params object[] args)
        {
            try
            {
                // following linecount hack is to stop dup line suppression of Log window
                Logging.Write(clr, "[LazyRaider] " + msg + (++lineCount % 2 == 0 ? "" : " "), args);
            }
            catch (Exception e)
            {
                Logging.Write(Colors.Red, "An Exception occured. Check debug log for details.");
                Logging.WriteDiagnostic(">>> EXCEPTION: occurred logging msg: \n\t\"" + SafeLogException(msg) + "\"");
                Logging.WriteException(e);
            }
        }

        public static void Dlog(string msg, params object[] args)
        {
            try
            {
                // following linecount hack is to stop dup line suppression of Log window
                Logging.WriteDiagnostic ("+LazyRaider+ " + msg + (((++lineCount) & 1) == 0 ? "" : " "), args);
            }
            catch (Exception e)
            {
                Logging.Write(Colors.Red, "An Exception occured. Check debug log for details.");
                Logging.WriteDiagnostic (">>> EXCEPTION: occurred logging msg: \n\t\"" + SafeLogException(msg) + "\"");
                Logging.WriteException(e);
            }
        }

        public static string SafeLogException(string msg)
        {
            msg = msg.Replace("{", "(");
            msg = msg.Replace("}", ")");
            return msg;
        }

        #endregion

        #region Behaviors

        #region Combat Behavior

        private static bool NeedPull(object context)
        {
            var target = Me.CurrentTarget;

            if (!LazyRaiderSettings.Instance.AutoAttack)
                return false;

            if (target == null)
                return false;

            if (!IsEnemy(target))
                return false;

            if (!target.InLineOfSight)
                return false;

            if (ImaTank || !IsAnyTankInCombat())
                return false;

            if (target.Distance > Targeting.PullDistance)
                return false;

            return true;
        }

        private static Composite CreateDetectTankBehavior()
        {
            return new PrioritySelector(
                new Decorator(ret => !(LazyRaiderSettings.Instance.NoTank || Tank.IsLeader()),
                    new Action(delegate
                        {
                            return SyncTankWithRaFLeader() ? RunStatus.Success : RunStatus.Failure;
                        })
                    )
                );
        }

        private static Composite CreateCombatBehavior()
        {
            return new PrioritySelector(

                CreateFrameFactory(
                    new Decorator(ret => StyxWoW.Me.Combat,
                        new PrioritySelector(
                            RoutineManager.Current.HealBehavior ?? new ActionAlwaysFail(),
                            RoutineManager.Current.CombatBuffBehavior ?? new ActionAlwaysFail(),
                            RoutineManager.Current.CombatBehavior
                            )
                        )
                    ),

                new Decorator(ret => !StyxWoW.Me.Combat,

                    new PrioritySelector(

                        CreateFrameFactory(
                            new PrioritySelector(
                                RoutineManager.Current.RestBehavior ?? new ActionAlwaysFail(),
                                RoutineManager.Current.PreCombatBuffBehavior ?? new ActionAlwaysFail()
                                )
                            ),

                        new Decorator(ret => LazyRaiderSettings.Instance.AutoTarget && Targeting.Instance.TargetList.Any(),
                            new Decorator(ret => BotPoi.Current.AsObject != Targeting.Instance.FirstUnit,
                                new Sequence(
                                    new Action(ret => BotPoi.Current = new BotPoi(Targeting.Instance.FirstUnit, PoiType.Kill)),
                                    new Action(ret => BotPoi.Current.AsObject.ToUnit().Target()),
                                    new Action(ret => Log("set BotPoi to kill target {0}", BotPoi.Current.AsObject.ToUnit().SafeName))
                                    )
                                )
                            ),

                        new Decorator(
                            NeedPull,
                            new PrioritySelector(

                                new Decorator(ctx => RoutineManager.Current.PullBuffBehavior != null,
                                    RoutineManager.Current.PullBuffBehavior),

                                new Decorator(ctx => RoutineManager.Current.PullBehavior != null,
                                    RoutineManager.Current.PullBehavior),

                                new ActionPull()
                                )
                            )
                        )
                    )
                );
        }

        private static bool IsAnyTankInCombat()
        {
            if (Tank.Player != null && Tank.Player.Combat)
                return true;

            return GroupMembers.Any(m => m.Combat && GetGroupRoleAssigned(m) == WoWPartyMember.GroupRole.Tank);
        }

        private static bool SetTankTarget()
        {
            WoWUnit target = null;

            if (Tank.Player != null && Tank.Player.Combat && IsEnemy(Tank.Player.CurrentTarget))
                target = Tank.Player.CurrentTarget;

            if (target != null)
            {
                BotPoi.Current = new BotPoi(target, PoiType.Kill);
                target.Target();
                return true;
            }

            return false;
        }

        #endregion

        #region Find Leader Behavior

        private static bool SyncTankWithRaFLeader()
        {
            // try
            {
                // check if Tank we saved is now in range 
                if (Tank.Current != null && Tank.Current.ToPlayer() != null)
                {
                    Dlog("SyncTankWithRaFLeader: Tank now in range, so setting RaFHelper");
                    Tank.SetAsLeader();
                    return false;
                }

                // otherwise, tank out of range pointer for WoWPlayer so clear
                if (RaFHelper.Leader != null)
                {
                    Dlog("SyncTankWithRaFLeader: Tank doesn't match RaFHelper so clearing");
                    RaFHelper.ClearLeader();
                }

                // user wants to control choosing new when we lost tank
                if (!LazyRaiderSettings.Instance.AutoTankSelect)
                {
                    if (haveFoundTankAtGroupForming)
                    {
                        return false;
                    }
                }

                // have Tank, so keep it until user changes
                if (Tank.Current != null && Tank.Current.IsOnline)
                    return false;

                WoWPartyMember tank = (from pm in GroupMemberInfos
                                       where GetGroupRoleAssigned(pm) == WoWPartyMember.GroupRole.Tank
                                           && pm.Guid != Me.Guid
                                           && pm.IsOnline 
                                           && pm.ToPlayer() != null
                                       orderby pm.Location3D.Distance(Me.Location) ascending
                                       select pm).FirstOrDefault();
                if (tank != null)
                {
                    haveFoundTankAtGroupForming = true;

                    Tank.Current = tank;
                    Log("Tank set to {0} based upon role", LazyRaider.Safe_UnitName(Tank.Current));
                    TreeRoot.StatusText = String.Format("[lr] tank is {0}", Safe_UnitName(tank));
                    return true;
                }

                return false;
            }
/*
            catch (ThreadAbortException) { throw; }
            catch
            {
                return true;
            }
 */ 
        }

        private void Player_OnMapChanged(BotEvents.Player.MapChangedEventArgs args)
        {
            RebuildBehaviors();
            TargetFilterSetup();
        }

        private enum InGroupState { Undefined, YES, NO };
        private InGroupState checkLast = InGroupState.Undefined;

        private void HandlePartyMembersChanged(object sender, LuaEventArgs args)
        {
            if (haveFoundTankAtGroupForming)
            {
                if (!IsInGroup)
                {
                    haveFoundTankAtGroupForming = false;
                    Dlog("we left group so resetting tank search");
                }
            }

            InGroupState checkNow = IsInGroup ? InGroupState.YES : InGroupState.NO;
            if (checkLast != checkNow)
            {
                checkLast = checkNow;
                Dlog("in group status changed to: {0}", checkNow.ToString());
                TargetFilterSetup();
            }
        }

        private void HandleModifierStateChanged(object sender, LuaEventArgs args)
        {
            if ( LazyRaiderSettings.Instance.PauseKey == LazyRaiderSettings.Keypress.None )
                return;

            if ( args.Args[0].ToString() == LazyRaiderSettings.Instance.PauseKey.ToString())
            {
                bool isPressed = Convert.ToInt32(args.Args[1]) == 1;
                if (LazyRaiderSettings.Instance.PauseWhileKeyPressed)
                {
                    Paused = isPressed;
                    if (isPressed)
                        TellUser("PAUSED, release {0} key to continue...", LazyRaiderSettings.Instance.PauseKey.ToString());
                    else
                        TellUser("RUNNING, press {0} key to pause...", LazyRaiderSettings.Instance.PauseKey.ToString());
                }
                else if (isPressed)
                {
                    Paused = !Paused;
                    if (Paused)
                        TellUser("PAUSED, press {0} in WOW to continue...", LazyRaiderSettings.Instance.PauseKey.ToString());
                    else
                        TellUser("RUNNING, press {0} key to pause...", LazyRaiderSettings.Instance.PauseKey.ToString());
                }
            }
        }

        private static void TellUser(string template, params object[] args)
        {
            string msg = string.Format(template, args);
            Log(Colors.Yellow, string.Format("Hotkey: " + msg));
            if (LazyRaiderSettings.Instance.ShowPauseMessageInGame)
                Lua.DoString(string.Format("print('{0}')", msg));
        }

        public static string Safe_UnitName(WoWUnit unit)
        {
            if (unit == null)
                return "(null)";

            return unit.Class.ToString() + " (max health:" + unit.MaxHealth + ")";
        }

        public static string Safe_UnitName(WoWPartyMember pm)
        {
            if (pm == null)
                return "(null)";

            WoWPlayer p = pm.ToPlayer();
            if (p != null)
                return Safe_UnitName(p);

            return GetGroupRoleAssigned(pm).ToString() + " (max health:" + pm.HealthMax + ")";
        }

        const int ROLEMASK = (int)WoWPartyMember.GroupRole.None | (int)WoWPartyMember.GroupRole.Tank | (int)WoWPartyMember.GroupRole.Healer | (int)WoWPartyMember.GroupRole.Damage;

        public static WoWPartyMember.GroupRole GetGroupRoleAssigned(WoWPartyMember pm)
        {
            WoWPartyMember.GroupRole role = WoWPartyMember.GroupRole.None;
            if (pm != null)
            {
                role = (WoWPartyMember.GroupRole)((int)pm.Role & ROLEMASK);
            }

            return role;
        }

        public static WoWPartyMember.GroupRole GetGroupRoleAssigned(WoWPlayer p)
        {
            WoWPartyMember.GroupRole role = WoWPartyMember.GroupRole.None;
            if (p != null && IsInGroup)
            {
                if (p.IsMe)
                {
                    role = (WoWPartyMember.GroupRole)((int)Me.Role & ROLEMASK);
                }
                else
                {
                    WoWPartyMember pm = GroupMemberInfos.FirstOrDefault(t => t.Guid == p.Guid);
                    if (pm != null)
                        role = GetGroupRoleAssigned(pm);
                }
            }
            return role;
        }

        #endregion

        #region Follow Behavior

        private static bool botMovement = false;

        private static Composite CreateFollowBehavior()
        {
            return new Sequence(

                new PrioritySelector(

                    new Decorator(ret => !LazyRaiderSettings.Instance.FollowTank,
                        new ActionAlwaysSucceed()),

                    new Decorator(ret => !IsInGroup,
                        new ActionAlwaysSucceed()),

                    new Decorator(ret => Tank.Current == null,
                        new ActionAlwaysSucceed()),

                    new Decorator(ret => Me.CurrentHealth <= 1,     // if dead or ghost
                        new ActionAlwaysSucceed()),

                    new Decorator(ret => Tank.Health <= 1,     // if dead or ghost
                        new ActionAlwaysSucceed()),

                    new Decorator(ret => NeedToMount(),
                        new Action(delegate
                        {
                            WaitForMount();
                        })),

                    new Decorator(ret => NeedToDismount(),
                        new Action(delegate
                        {
                            WaitForDismount();
                        })),

                    new Decorator(ret => Tank.Distance > LazyRaiderSettings.Instance.FollowDistance
                                        || (RaFHelper.Leader != null && !RaFHelper.Leader.InLineOfSpellSight),
                        new Action(delegate
                        {
                            WoWPoint pt = Tank.Location;
                            if (pt.DistanceSqr(_lastDest) > 1 || !Me.IsMoving)
                            {
                                _lastDest = pt;
                                WoWPoint newPoint = WoWMovement.CalculatePointFrom(pt, (float) 0.85 * LazyRaiderSettings.Instance.FollowDistance);
                                Log("move to tank @ {0:F1} yds", pt.Distance(Me.Location));
                            
                                if ( RaFHelper.Leader != null && RaFHelper.Leader.Mounted && (RaFHelper.Leader.IsFlying || RaFHelper.Leader.IsSwimming) )
                                    Flightor.MoveTo(newPoint);
                                else 
                                    Navigator.MoveTo(newPoint);

                                botMovement = true;
                            }

                            return RunStatus.Success;
                        }))
                    )

//                    , new ActionAlwaysFail()      // force this to fail to allow fall through to HB 
                );
        }

        #endregion

        #endregion

        public static bool NeedToDismount()
        {
            return Me.Mounted
                && CharacterSettings.Instance.UseMount
                && RaFHelper.Leader != null
                && Tank.Distance <= LazyRaiderSettings.Instance.FollowDistance
                && !RaFHelper.Leader.Mounted;
        }

        public static bool NeedToMount()
        {
            return !Me.Mounted
                && CharacterSettings.Instance.UseMount
                && Tank.Current != null
                && (Tank.Distance > NeedToMountDistance || RaFHelper.Leader.Mounted)
                && Me.IsOutdoors
                && Mount.CanMount();
        }

        public static int NeedToMountDistance
        {
            get
            {
                return Math.Max(CharacterSettings.Instance.MountDistance, LazyRaiderSettings.Instance.FollowDistance + 20);
            }
        }

        public static void WaitForDismount()
        {
            while (IsGameStable() && Me.CurrentHealth > 1 && Me.Mounted)
            {
                Lua.DoString("Dismount()");
                // Mount.Dismount();  // HB API forces Stop also, so use LUA to keep running and let Squire or CC stop if needed
                StyxWoW.SleepForLagDuration();
            }
        }

        public static void WaitForMount()
        {
            if (Me.Combat || Me.IsIndoors || !CharacterSettings.Instance.UseMount)
                return;

            WaitForStop();
            WoWPoint ptStop = Me.Location;

            var timeOut = new Stopwatch();
            timeOut.Start();

            if (Mount.Mounts.Count() == 0 || !Mount.CanMount())
                return;

            Log("Attempting to mount via HB...");
            Mount.MountUp( LazyLocationRetriever );
            StyxWoW.SleepForLagDuration();

            while (IsGameStable() && Me.CurrentHealth > 1 && Me.IsCasting)
            {
                Thread.Sleep(75);
            }

            if (!Me.Mounted)
            {
                Log("unable to mount after {0} ms", timeOut.ElapsedMilliseconds);
                if (ptStop.Distance(Me.Location) != 0)
                    Log("character was stopped but somehow moved {0:F3} yds while trying to mount", ptStop.Distance(Me.Location));
            }
            else
            {
                Log("Mounted");
            }
        }

        public static WoWPoint LazyLocationRetriever()
        {
            return WoWPoint.Empty;
        }

        public static void WaitForStop()
        {
            // excessive attempt to make sure HB doesn't have any cached movement
            WoWMovement.MoveStop(WoWMovement.MovementDirection.All);
            WoWMovement.MoveStop();
            Navigator.PlayerMover.MoveStop();

            do
            {
                StyxWoW.SleepForLagDuration();
            } while (IsGameStable() && Me.CurrentHealth > 1 && Me.IsMoving);
        }

        private static Composite CreateFrameFactory(params Composite[] children)
        {
            if (LazyRaiderSettings.Instance.LockMemory)
                return new FrameLockSelector(children);

            return new PrioritySelector(children);
        }

        public class FrameLockSelector : PrioritySelector
        {
            public FrameLockSelector(params Composite[] children)
                : base(children)
            {
            }

            public override RunStatus Tick(object context) 
            { 
                using (StyxWoW.Memory.AcquireFrame()) 
                { 
                    return base.Tick(context); 
                } 
            }
        }
    }

    public static class Extensions
    {
        /// <summary>
        /// get the effective distance between two mobs accounting for their 
        /// combat reaches (hitboxes)
        /// </summary>
        /// <param name="unit">unit</param>
        /// <param name="other">Me if null, otherwise second unit</param>
        /// <returns></returns>
        public static float SpellDistance(this WoWUnit unit, WoWUnit other = null)
        {
            // abort if mob null
            if (unit == null)
                return 0;

            // optional arg implying Me, then make sure not Mob also
            if (other == null)
                other = StyxWoW.Me;

            // pvp, then keep it close
            float dist = other.Location.Distance(unit.Location);
            dist -= other.CombatReach + unit.CombatReach;
            if (dist < 0)
                return 0;

            return dist;
        }

        public static bool IsMelee(this WoWPlayer p)
        {
            return p.Intellect < (p.Strength + p.Agility);
        }



    }

}

