//#define SHOW_BEHAVIOR_LOAD_DESCRIPTION
//#define BOTS_NOT_CALLING_PULLBUFFS
//#define TESTING_WHILE_IN_VEHICLE_COMPLETED

using System;
using System.Linq;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.TreeSharp;
using Singular.ClassSpecific;
using System.Drawing;
using CommonBehaviors.Actions;
using Styx.Common;

using Action = Styx.TreeSharp.Action;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.WoWInternals.WoWObjects;
using Styx.Helpers;
using Styx.WoWInternals;
using Styx.Common.Helpers;
using System.Collections.Generic;

using Singular.ClassSpecific.Monk;

namespace Singular
{
    partial class SingularRoutine
    {
        private Composite _combatBehavior;
        private Composite _combatBuffsBehavior;
        private Composite _healBehavior;
        private Composite _preCombatBuffsBehavior;
        private Composite _pullBehavior;
        private Composite _pullBuffsBehavior;
        private Composite _restBehavior;
        public Composite _lostControlBehavior;
        private Composite _deathBehavior;
        public override Composite CombatBehavior { get { return _combatBehavior; } }
        public override Composite CombatBuffBehavior { get { return _combatBuffsBehavior; } }
        public override Composite HealBehavior { get { return _healBehavior; } }
        public override Composite PreCombatBuffBehavior { get { return _preCombatBuffsBehavior; } }
        public override Composite PullBehavior { get { return _pullBehavior; } }
        public override Composite PullBuffBehavior { get { return _pullBuffsBehavior; } }
        public override Composite RestBehavior { get { return _restBehavior; } }
        public override Composite DeathBehavior { get { return _deathBehavior; } }

        private static ulong _guidLastTarget = 0;
        private static WaitTimer _timerLastTarget = new WaitTimer(TimeSpan.FromSeconds(20));

        public bool RebuildBehaviors(bool silent = false)
        {
            // Logger.PrintStackTrace("RebuildBehaviors called.");
            DetermineCurrentWoWContext();
            InitBehaviorPropertyOverrrides();

            // DO NOT UPDATE: This will cause a recursive event
            // Update the current context. Handled in SingularRoutine.Context.cs
            //UpdateContext();

            if (!silent)
            {
                Logger.WriteFile("");
                Logger.WriteFile("Invoked Initilization Methods");
                Logger.WriteFile("======================================================");
            }

            CompositeBuilder.InvokeInitializers(Me.Class, TalentManager.CurrentSpec, CurrentWoWContext, silent);

            // special behavior - reset KitingBehavior hook prior to calling class specific createion
            TreeHooks.Instance.ReplaceHook(HookName("KitingBehavior"), new ActionAlwaysFail());

            if (!silent)
            {
                Logger.WriteFile("");
                // Logger.WriteFile("{0} {1} {2}", "Pri".AlignRight(4), "Context".AlignLeft(15), "Method");
                Logger.WriteFile("Behaviors Created in Priority Order");
                Logger.WriteFile("======================================================");
            }

            // If these fail, then the bot will be stopped. We want to make sure combat/pull ARE implemented for each class.
            if (!EnsureComposite( silent, true, CurrentWoWContext, BehaviorType.Combat))
            {
                return false;
            }

            if (!EnsureComposite( silent, true, CurrentWoWContext, BehaviorType.Pull))
            {
                return false;
            }

            // If there's no class-specific resting, just use the default, which just eats/drinks when low.
            EnsureComposite( silent, false, CurrentWoWContext, BehaviorType.Rest);
            // if (TreeHooks.Instance.Hooks[HookName(BehaviorType.Rest)] == null)
            if (!TreeHooks.Instance.Hooks.ContainsKey(HookName(BehaviorType.Rest)) || TreeHooks.Instance.Hooks[HookName(BehaviorType.Rest)].Count <= 0)
                TreeHooks.Instance.ReplaceHook(HookName(BehaviorType.Rest), Helpers.Rest.CreateDefaultRestBehaviour());

            // These are optional. If they're not implemented, we shouldn't stop because of it.
            EnsureComposite( silent, false, CurrentWoWContext, BehaviorType.CombatBuffs);
            EnsureComposite( silent, false, CurrentWoWContext, BehaviorType.Heal);
            EnsureComposite( silent, false, CurrentWoWContext, BehaviorType.PullBuffs);
            EnsureComposite( silent, false, CurrentWoWContext, BehaviorType.PreCombatBuffs);
            EnsureComposite( silent, false, CurrentWoWContext, BehaviorType.Death);

            EnsureComposite(silent, false, CurrentWoWContext, BehaviorType.LossOfControl);

#if SHOW_BEHAVIOR_LOAD_DESCRIPTION
            // display concise single line describing what behaviors we are loading
            if (!silent)
            {
                string sMsg = "";
                if (_healBehavior != null)
                    sMsg += (!string.IsNullOrEmpty(sMsg) ? "," : "") + " Heal";
                if (_pullBuffsBehavior != null)
                    sMsg += (!string.IsNullOrEmpty(sMsg) ? "," : "") + " PullBuffs";
                if (_pullBehavior != null)
                    sMsg += (!string.IsNullOrEmpty(sMsg) ? "," : "") + " Pull";
                if (_preCombatBuffsBehavior != null)
                    sMsg += (!string.IsNullOrEmpty(sMsg) ? "," : "") + " PreCombatBuffs";
                if (_combatBuffsBehavior != null)
                    sMsg += (!string.IsNullOrEmpty(sMsg) ? "," : "") + " CombatBuffs";
                if (_combatBehavior != null)
                    sMsg += (!string.IsNullOrEmpty(sMsg) ? "," : "") + " Combat";
                if (_restBehavior != null)
                    sMsg += (!string.IsNullOrEmpty(sMsg) ? "," : "") + " Rest";

                Logger.Write(Color.LightGreen, "Loaded{0} behaviors for {1}: {2}", TalentManager.CurrentSpec.ToString().CamelToSpaced(), context.ToString(), sMsg);
            }
#endif
            if (!silent)
            {
                Logger.WriteFile("");
            }

            return true;
        }

        /// <summary>
        /// initialize all base behaviors.  replaceable portion which will vary by context is represented by a single
        /// HookExecutor that gets assigned elsewhere (typically EnsureComposite())
        /// </summary>
        private void InitBehaviorPropertyOverrrides()
        {
            // be sure to turn off -- routines needing it will enable when rebuilt
            TankManager.NeedTankTargeting = false;
            if (TankManager.Instance != null)
                TankManager.Instance.Clear();

            HealerManager.NeedHealTargeting = false;
            if (HealerManager.Instance != null)
                HealerManager.Instance.Clear();

            // we only do this one time
            if (_restBehavior != null)
                return;

            // note regarding behavior intros....
            // WAIT: Rest and PreCombatBuffs should wait on gcd/cast in progress (return RunStatus.Success)
            // SKIP: PullBuffs, CombatBuffs, and Heal should fall through if gcd/cast in progress (wrap in decorator)
            // HANDLE: Pull and Combat should wait or skip as needed in class specific manner required

            // loss of control behavior must be defined prior to any embedded references by other behaviors
            _lostControlBehavior = new HookExecutor(HookName(BehaviorType.LossOfControl));
            _restBehavior = new HookExecutor(HookName(BehaviorType.Rest));
            _preCombatBuffsBehavior = new HookExecutor(HookName(BehaviorType.PreCombatBuffs));
            _pullBuffsBehavior = new HookExecutor(HookName(BehaviorType.PullBuffs));
            _combatBuffsBehavior = new HookExecutor(HookName(BehaviorType.CombatBuffs));
            _healBehavior = new HookExecutor(HookName(BehaviorType.Heal));
            _pullBehavior = new HookExecutor(HookName(BehaviorType.Pull));
            _combatBehavior = new HookExecutor(HookName(BehaviorType.Combat));
            _deathBehavior = new HookExecutor(HookName(BehaviorType.Death));
        }

        private static bool OkToCallBehaviorsWithCurrentCastingStatus(LagTolerance allow = LagTolerance.Yes)
        {
            if (TalentManager.CurrentSpec == WoWSpec.MonkMistweaver)
                return true;

            if (!Spell.IsGlobalCooldown(allow) && !Spell.IsCastingOrChannelling(allow))
                return true;

            return false;
        }

        private static bool HaveWeLostControl
        { 
            get
            {
                return Me.Fleeing || Me.Stunned;
            } 
        }

        internal static string HookName(string name)
        {
            return "Singular." + name;
        }

        internal static string HookName(BehaviorType typ)
        {
            return "Singular." + typ.ToString();
        }

        static bool _inQuestVehicle = false;

        static bool _inPetCombat = false;

        private static bool AllowBehaviorUsage()
        {
            // Opportunity alert -- the decision whether a Combat Routine should fight or not
            // .. should be made by the caller (BotBase, Quest Behavior, Plugin, etc.) 
            // .. The only reason for calling a Combat Routine is combat.  Anytime we have to
            // .. add this conditional check in the Combat Routine it should be a singlar that
            // .. role/responsibility boundaries are being violated

            // disable if Questing and in a Quest Vehicle (now requires setting as well)
            if (IsQuestBotActive)
            {
                if (_inQuestVehicle != Me.InVehicle)
                {
                    _inQuestVehicle = Me.InVehicle; 
                    if (_inQuestVehicle )
                    {
                        Logger.Write(Color.White, "Singular is {0} while in a Quest Vehicle", SingularSettings.Instance.DisableInQuestVehicle ? "Disabled" : "Enabled");
                        Logger.Write(Color.White, "Change [Disable in Quest Vehicle] setting to '{0}' to change", !SingularSettings.Instance.DisableInQuestVehicle);
                    }
                }

                if (_inQuestVehicle && SingularSettings.Instance.DisableInQuestVehicle)
                    return false;
            }

            // disable if in pet battle and using a plugin/botbase 
            //  ..  that doesn't prevent combat routine from being called
            //  ..  note: this won't allow pet combat to work correclty, it 
            //  ..  only prevents failed movement/spell cast messages from Singular
            //  ..  Pet Combat component to prevent calls to combat routine  as it
            //  ..  has no role in pet combat
            if (!Me.CurrentMap.IsRaid)
            {
                if (_inPetCombat != PetBattleInProgress())
                {
                    _inPetCombat = PetBattleInProgress();
                    if (_inPetCombat)
                    {
                        Logger.Write(Color.White, "Behaviors disabled in Pet Fight");
                    }
                }

                if (_inPetCombat)
                    return false;
            }

            return true;
        }

        private static bool AllowNonCombatBuffing()
        {
            // Opportunity alert -- bots that sit still waiting for a queue to pop
            // .. should avoid calling PreCombatbuff, since it looks odd for long queue times
            // .. for a toon to stay stationary but renew a buff immediately as it expires.

            if (IsBgBotActive && !Battlegrounds.IsInsideBattleground)
                return false;

            if (IsDungeonBuddyActive && !Me.IsInInstance)
                return false;

            if (!AllowBehaviorUsage())
                return false;

            return true;
        }

        private static bool PetBattleInProgress()
        {
            try
            {
                return 1 == Lua.GetReturnVal<int>("return C_PetBattles.IsInBattle()", 0);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Ensures we have a composite for the given BehaviorType.  
        /// </summary>
        /// <param name="error">true: report error if composite not found, false: allow null composite</param>
        /// <param name="type">BehaviorType that should be loaded</param>
        /// <returns>true: composite loaded and saved to hook, false: failure</returns>
        private bool EnsureComposite(bool silent, bool error, WoWContext context, BehaviorType type)
        {
            int count = 0;
            Composite composite;

            // Logger.WriteDebug("Creating " + type + " behavior.");

            composite = CompositeBuilder.GetComposite(Class, TalentManager.CurrentSpec, type, context, out count);

            // handle those composites we need to default if not found
            if (composite == null && type == BehaviorType.Rest)
                composite = Helpers.Rest.CreateDefaultRestBehaviour();

            if ((composite == null || count <= 0) && error)
            {
                StopBot(string.Format("Singular does not support {0} for this {1} {2} in {3} context!", type, StyxWoW.Me.Class, TalentManager.CurrentSpec, context));
                return false;
            }

            composite = AddCommonBehaviorPrefix(composite, type);

            // replace hook we created during initialization
            TreeHooks.Instance.ReplaceHook(HookName(type), composite ?? new ActionAlwaysFail());

            return composite != null;
        }


        private static Composite AddCommonBehaviorPrefix( Composite composite, BehaviorType behav)
        {
            if (behav == BehaviorType.LossOfControl)
            {
                composite = new Decorator(
                    ret => HaveWeLostControl,
                    new PrioritySelector(
                        new Action(r =>
                        {
                            if (!StyxWoW.IsInGame)
                            {
                                Logger.WriteDebug(Color.White, "Not in game...");
                                return RunStatus.Success;
                            }

                            return RunStatus.Failure;
                        }),
                        new ThrottlePasses(1, 1, new Decorator(ret => Me.Fleeing, new Action(r => { Logger.Write(Color.White, "FLEEING! (loss of control)"); return RunStatus.Failure; }))),
                        new ThrottlePasses(1, 1, new Decorator(ret => Me.Stunned, new Action(r => { Logger.Write(Color.White, "STUNNED! (loss of control)"); return RunStatus.Failure; }))),
                        new ThrottlePasses(1, 1, new Decorator(ret => Me.Silenced, new Action(r => { Logger.Write(Color.White, "SILENCED! (loss of control)"); return RunStatus.Failure; }))),
                        new Throttle(1,
                            new PrioritySelector(
                                composite ?? new ActionAlwaysFail(),
                                new Decorator(
                                    ret => SingularSettings.Instance.UseRacials,
                                    new PrioritySelector(
                                        Spell.Cast("Will of the Forsaken", on => Me, ret => Me.Race == WoWRace.Undead && Me.Fleeing),
                                        Spell.Cast("Every Man for Himself", on => Me, ret => Me.Race == WoWRace.Human && (Me.Stunned || Me.Fleeing))
                                        )
                                    ),

                                Item.UseEquippedTrinket(TrinketUsage.CrowdControlled),
                                Item.UseEquippedTrinket(TrinketUsage.CrowdControlledSilenced)
                                )
                            ),
                        new ActionAlwaysSucceed()
                        )
                    );
            }

            if (behav == BehaviorType.Rest)
            {
                Composite combatInRestCheck;
                if (!SingularSettings.Debug)
                    combatInRestCheck = new ActionAlwaysFail();
                else
                    combatInRestCheck = new Throttle(
                        1,
                        new ThrottlePasses(
                            1, 
                            TimeSpan.FromSeconds(1),
                            RunStatus.Failure,
                            new Decorator(
                                req => Me.Combat,
                                new Sequence(
                                    new PrioritySelector(
                                        new ThrottlePasses(
                                            1, 5,
                                            new Action(r => Logger.Write(Color.Yellow, "Bot Error: {0} or plugin7 called Rest behavior while in combat", SingularRoutine.GetBotName()))
                                            ),
                                        new Action(r => Logger.WriteDebug(Color.Yellow, "Bot Error: {0} or plugin8 called Rest behavior while in combat", SingularRoutine.GetBotName()))
                                        ),
                                    new ActionAlwaysFail()
                                    )
                                )
                            )
                        );

                composite = new LockSelector(
                    new CallWatch("Rest",
                        new Decorator(
                            ret => !Me.IsFlying && AllowBehaviorUsage() && !SingularSettings.Instance.DisableNonCombatBehaviors,
                            new PrioritySelector(

//                                TestDynaWait(),

                                combatInRestCheck,

                                // new Action(r => { _guidLastTarget = 0; return RunStatus.Failure; }),
                                new Decorator(
                                    req => !OkToCallBehaviorsWithCurrentCastingStatus(allow: LagTolerance.No),
                                    new ActionAlwaysSucceed()
                                    ),

                                // lost control in Rest -- force a RunStatus.Failure so we don't loop in Rest
                                new Sequence(
                                    SingularRoutine.Instance._lostControlBehavior,
                                    new ActionAlwaysFail()
                                    ),

                                // skip Rest logic if we lost control (since we had to return Fail to prevent Rest loop)
                                new Decorator(
                                    req => !HaveWeLostControl,
                                    composite ?? new ActionAlwaysFail()
                                    )
                                )
                            )
                        )
                    );
            }

            if (behav == BehaviorType.PreCombatBuffs)
            {
                Composite precombatInCombatCheck;
                if (!SingularSettings.Debug)
                    precombatInCombatCheck = new ActionAlwaysFail();
                else
                    precombatInCombatCheck = new Throttle(
                        1,
                        new ThrottlePasses(
                            1,
                            TimeSpan.FromSeconds(1),
                            RunStatus.Failure,
                            new Decorator(
                                req => Me.Combat,
                                new Sequence(
                                    new PrioritySelector(
                                        new ThrottlePasses(
                                            1, 5,
                                            new Action(r => Logger.Write(Color.Yellow, "Bot Error: {0} or plugin4 called PreCombatBuff behavior while in combat", SingularRoutine.GetBotName()))
                                            ),
                                        new Action(r => Logger.WriteDebug(Color.Yellow, "Bot Error: {0} or plugin3 called PreCombatBuff behavior while in combat", SingularRoutine.GetBotName()))
                                        ),
                                    new ActionAlwaysFail()
                                    )
                                )
                            )
                        );

                composite = new LockSelector(
                    new CallWatch("PreCombat",
                        new Decorator(  // suppress non-combat buffing if standing around waiting on DungeonBuddy or BGBuddy queues
                            ret => !Me.Mounted
                                && !SingularSettings.Instance.DisableNonCombatBehaviors
                                && AllowNonCombatBuffing(),
                            new PrioritySelector(
                                new Decorator(
                                    req => !OkToCallBehaviorsWithCurrentCastingStatus(),
                                    Spell.WaitForGcdOrCastOrChannel()
                                    ),
                                Helpers.Common.CreateUseTableBehavior(),
                                Helpers.Common.CreateUseSoulwellBehavior(),
                                Item.CreateUseAlchemyBuffsBehavior(),
                                Item.CreateUseScrollsBehavior(),
                    // Generic.CreateFlasksBehaviour(),
                                composite ?? new ActionAlwaysFail()
                                )
                            )
                        )
                    );
            }

            if (behav == BehaviorType.PullBuffs)
            {
                composite = new LockSelector(
                    new CallWatch("PullBuffs",
                        new Decorator(
                            ret => AllowBehaviorUsage() && OkToCallBehaviorsWithCurrentCastingStatus(),
                            composite ?? new ActionAlwaysFail()
                            )
                        )
                    );
            }

            if (behav == BehaviorType.CombatBuffs)
            {
                Composite behavHealingSpheres = new ActionAlwaysFail();
                if (SingularSettings.Instance.MoveToSpheres)
                {
                    behavHealingSpheres = new ThrottlePasses(
                        1, 1, 
                        new Decorator(
                            ret => Me.HealthPercent < SingularSettings.Instance.SphereHealthPercentInCombat && Singular.ClassSpecific.Monk.Common.AnySpheres(SphereType.Life, SingularSettings.Instance.SphereDistanceInCombat),
                            Singular.ClassSpecific.Monk.Common.CreateMoveToSphereBehavior(SphereType.Life, SingularSettings.Instance.SphereDistanceInCombat)
                            )
                        );
                }

                composite = new LockSelector(
                    new CallWatch("CombatBuffs",
                        new Decorator(
                            ret => AllowBehaviorUsage() && OkToCallBehaviorsWithCurrentCastingStatus(),
                            new PrioritySelector(
                                new Decorator(ret => !HotkeyDirector.IsCombatEnabled, new ActionAlwaysSucceed()),
                                Generic.CreateUseTrinketsBehaviour(),
                                Generic.CreatePotionAndHealthstoneBehavior(),
                                Generic.CreateRacialBehaviour(),
                                behavHealingSpheres,
                                composite ?? new ActionAlwaysFail()
                                )
                            )
                        )
                    );
            }

            if (behav == BehaviorType.Heal)
            {
                composite = new LockSelector(
                    new CallWatch("Heal",
                        SingularRoutine.Instance._lostControlBehavior,
                        new Decorator(
                            ret => Kite.IsKitingActive(),
                            new HookExecutor(HookName("KitingBehavior"))
                            ),
                        new Decorator(
                            ret => AllowBehaviorUsage() && OkToCallBehaviorsWithCurrentCastingStatus(),
                            composite ?? new ActionAlwaysFail()
                            )
                        )
                    );
            }

            if (behav == BehaviorType.Pull)
            {
                composite = new LockSelector(
                    new CallWatch("Pull",
                        new Decorator(
                            ret => AllowBehaviorUsage(), // && (!Me.GotTarget || !Blacklist.Contains(Me.CurrentTargetGuid, BlacklistFlags.Combat)),
                            new PrioritySelector(
                                new Decorator(
                                    ret => !HotkeyDirector.IsCombatEnabled,
                                    new ActionAlwaysSucceed()
                                    ),
#if BOTS_NOT_CALLING_PULLBUFFS
                            _pullBuffsBehavior,
#endif
 CreateLogTargetChanges(BehaviorType.Pull, "<<< PULL >>>"),
                                composite ?? new ActionAlwaysFail()
                                )
                            )
                        )
                    );
            }

            if (behav == BehaviorType.Combat)
            {
                composite = new LockSelector(
                    new CallWatch("Combat",
                        new Decorator(
                            ret => AllowBehaviorUsage(), // && (!Me.GotTarget || !Blacklist.Contains(Me.CurrentTargetGuid, BlacklistFlags.Combat)),
                            new PrioritySelector(
                                new Decorator(
                                    ret => !HotkeyDirector.IsCombatEnabled,
                                    new ActionAlwaysSucceed()
                                    ),
                                CreatePullMorePull(),
                                CreateLogTargetChanges(BehaviorType.Combat, "<<< ADD >>>"),
                                composite ?? new ActionAlwaysFail()
                                )
                            )
                        )
                    );
            }

            if (behav == BehaviorType.Death)
            {
                composite = new LockSelector(
                    new CallWatch("Death",
                        new Decorator(
                            ret => AllowBehaviorUsage(),
                            new PrioritySelector(
                                new Action(r => { ResetCurrentTarget(); return RunStatus.Failure; }),
                                new Decorator(
                                    req => !OkToCallBehaviorsWithCurrentCastingStatus(),
                                    Spell.WaitForGcdOrCastOrChannel()
                                    ),
                                composite ?? new ActionAlwaysFail()
                                )
                            )
                        )
                    );
            }

            return composite;
        }

        public static void ResetCurrentTargetTimer()
        {
            _timerLastTarget.Reset();
            /*
            if (SingularSettings.Debug)
                Logger.WriteDebug("reset target timer to {0:c}", _timerLastTarget.TimeLeft);
            */
        }

        public static void ResetCurrentTarget()
        {
            _guidLastTarget = 0;
        }

        private static Composite CreateLogTargetChanges(BehaviorType behav, string sType)
        {
            return new Action(r =>
                {
                    // there are moments where CurrentTargetGuid != 0 but CurrentTarget == null. following
                    // .. tries to handle by only checking CurrentTarget reference and treating null as guid = 0
                    if (Me.CurrentTargetGuid != _guidLastTarget)
                    {
                        if (Me.CurrentTargetGuid == 0)
                        {
                            if (_guidLastTarget != 0)
                            {
                                if (SingularSettings.Debug)
                                    Logger.WriteDebug(sType + " CurrentTarget now: (null)");
                                _guidLastTarget = 0;
                            }
                        }
                        else
                        {
                            _guidLastTarget = Me.CurrentTargetGuid;
                            ResetCurrentTargetTimer();
                            LogTargetChanges(behav, sType);
                        }
                    }
                    // testing for Me.CurrentTarget != null also to address objmgr not resolving guid yet to avoid NullRef
                    else if (Me.CurrentTargetGuid != 0 && Me.CurrentTarget != null && Me.CurrentTarget.IsValid && !MovementManager.IsMovementDisabled && SingularRoutine.CurrentWoWContext == WoWContext.Normal)  
                    {       
                        // make sure we get into melee range within reasonable time
                        if ((!Me.IsMelee() || Me.CurrentTarget.IsWithinMeleeRange) && Movement.InLineOfSpellSight(Me.CurrentTarget, 5000))
                        {
                            ResetCurrentTargetTimer();
                        }
                        else if (_timerLastTarget.IsFinished)
                        {
                            BlacklistFlags blf = Me.CurrentTarget.Aggro || (Me.GotAlivePet && Me.CurrentTarget.PetAggro) ? BlacklistFlags.Combat : BlacklistFlags.Pull;
                            if (!Blacklist.Contains(_guidLastTarget, blf))
                            {
                                TimeSpan bltime = TimeSpan.FromMinutes(5);

                                Logger.Write(Color.HotPink, "{0} Target {1} out of range/line of sight for {2:F1} seconds, blacklisting for {3:c} and clearing {4}", 
                                    blf, 
                                    Me.CurrentTarget.SafeName(), 
                                    _timerLastTarget.WaitTime.TotalSeconds, 
                                    bltime,
                                    _guidLastTarget == BotPoi.Current.Guid ? "BotPoi" : "Current Target" );

                                Blacklist.Add(_guidLastTarget, blf, TimeSpan.FromMinutes(5));
                                if (_guidLastTarget == BotPoi.Current.Guid)
                                    BotPoi.Clear("Clearing Blacklisted BotPoi");
                                Me.ClearTarget();
                            }
                        }
                    }

                    return RunStatus.Failure;
                });

        }

        private static void LogTargetChanges(BehaviorType behav, string sType)
        {
            if (!SingularSettings.Debug)
                return;

            string info = "";
            WoWUnit target = Me.CurrentTarget;

            if (BotPoi.Current.Guid == Me.CurrentTargetGuid)
                info += string.Format(", IsBotPoi={0}", BotPoi.Current.Type);

            if (Targeting.Instance.TargetList.Contains(Me.CurrentTarget))
                info += string.Format(", TargetIndex={0}", Targeting.Instance.TargetList.IndexOf(Me.CurrentTarget) + 1);

            Logger.WriteDebug(sType + " CurrentTarget now: {0} h={1:F1}%, maxh={2}, d={3:F1} yds, box={4:F1}, player={5}, hostil={6}, faction={7}, loss={8}, face={9}, agro={10}" + info,
                target.SafeName(),
                target.HealthPercent,
                target.MaxHealth,
                target.Distance,
                target.CombatReach,
                target.IsPlayer.ToYN(),
                target.IsHostile.ToYN(),
                target.FactionId,
                target.InLineOfSpellSight.ToYN(),
                Me.IsSafelyFacing(target).ToYN(),
                target.Aggro.ToYN() + (!Me.GotAlivePet ? "" : ", pagro=" + target.PetAggro.ToYN())
                );
        }

        private static int _prevPullDistance = -1;
        private static Bots.Grind.BehaviorFlags _prevBehaviorFlags = Bots.Grind.BehaviorFlags.All;
        private static Styx.CommonBot.Routines.CapabilityFlags _prevCapabilityFlags = Styx.CommonBot.Routines.CapabilityFlags.All;
        private static void MonitorPullDistance()
        {
            if (_prevPullDistance != CharacterSettings.Instance.PullDistance)
            {
                _prevPullDistance = CharacterSettings.Instance.PullDistance;
                Logger.Write(Color.HotPink, "info: Pull Distance set to {0} yds by {1}, Plug-in, Profile, or User", _prevPullDistance, GetBotName());
            }
        }

        private static void MonitorBehaviorFlags()
        {
            if (_prevBehaviorFlags != Bots.Grind.LevelBot.BehaviorFlags)
            {
                _prevBehaviorFlags = Bots.Grind.LevelBot.BehaviorFlags;
                Logger.WriteDiagnostic(Color.HotPink, "info: BehaviorFlags changed to [{0}] by {1}, Plug-in or Profile", _prevBehaviorFlags.ToString(), GetBotName());
            }
        }

        #region Nested type: LockSelector

        /// <summary>
        /// This behavior wraps the child behaviors in a 'FrameLock' which can provide a big performance improvement 
        /// if the child behaviors makes multiple api calls that internally run off a frame in WoW in one CC pulse.
        /// </summary>
        private class LockSelector : PrioritySelector
        {
            delegate RunStatus TickDelegate(object context);

            TickDelegate _TickSelectedByUser;

            public LockSelector(params Composite[] children)
                : base(children)
            {
                if (SingularSettings.Instance.UseFrameLock)
                    _TickSelectedByUser = TickWithFrameLock;
                else
                    _TickSelectedByUser = TickNoFrameLock;
            }

            public override RunStatus Tick(object context)
            {
                return _TickSelectedByUser(context);
            }

            private RunStatus TickWithFrameLock(object context)
            {
                using (StyxWoW.Memory.AcquireFrame())
                {
                    return base.Tick(context);
                }
            }

            private RunStatus TickNoFrameLock(object context)
            {
                return base.Tick(context);
            }

        }

        #endregion

        #region Pull More Support

        [Behavior(BehaviorType.Initialize, priority: 999)]
        public static Composite InitializeBehaviors()
        {
            IsPullMoreActive = IsPullMoreAllowed();

            if (false == IsPullMoreActive)
                Logger.Write("Pull More: will not pull additional mobs during Combat");
            else
            {
                _rangePullMore = Me.IsMelee() ? SingularSettings.Instance.PullMoreDistMelee : SingularSettings.Instance.PullMoreDistRanged;
                Logger.Write("Pull More: will pull up to {0} from mob types=[{1}] within {2} yds during Combat", 
                    SingularSettings.Instance.PullMoreMobCount,
                    SingularSettings.Instance.PullMoreTargetType,
                    _rangePullMore
                    );
            }

            return null;
        }

        private static DateTime _allowPullMoreUntil = DateTime.MinValue;
        private static DateTime _timeoutPullMoreAt = DateTime.MaxValue;
        private static int _rangePullMore;

        public static bool IsPullMoreActive { get; set; }

        private static void UpdatePullMoreConditionals()
        {
            // force to allow pulling more when out of combat
            if (!Me.Combat && (!Me.GotAlivePet || !Me.Pet.Combat))
            {
                _allowPullMoreUntil = DateTime.MinValue;
                _timeoutPullMoreAt = DateTime.MaxValue;
            }
        }

        private static bool IsPullMoreAllowed()
        {
            string needSpell = "";
            switch (TalentManager.CurrentSpec)
            {
                case WoWSpec.DeathKnightBlood:
                case WoWSpec.DeathKnightFrost:
                case WoWSpec.DeathKnightUnholy:
                    needSpell = "Pestilence";
                    break;

                case WoWSpec.DruidBalance:
                    needSpell = "Sunfire";
                    break;

                case WoWSpec.DruidFeral:
                case WoWSpec.DruidGuardian:
                    needSpell = "Swipe";
                    break;

                case WoWSpec.DruidRestoration:
                    needSpell = "Hurricane";
                    break;

                case WoWSpec.HunterBeastMastery:
                case WoWSpec.HunterMarksmanship:
                case WoWSpec.HunterSurvival:
                    needSpell = "Multi-Shot";
                    break;

                case WoWSpec.MageArcane:
                case WoWSpec.MageFire:
                case WoWSpec.MageFrost:
                    needSpell = "Arcane Explosion";
                    break;

                case WoWSpec.MonkBrewmaster:
                    needSpell = "Breath of Fire";
                    break;

                case WoWSpec.MonkMistweaver:
                case WoWSpec.MonkWindwalker:
                    needSpell = "Spinning Crane Kick";
                    break;

                case WoWSpec.PaladinHoly:
                    needSpell = "Holy Prism";
                    break;

                case WoWSpec.PaladinProtection:
                    needSpell = "Avenger's Shield";
                    break;

                case WoWSpec.PaladinRetribution:
                    needSpell = "Hammer of the Righteous";
                    break;

                case WoWSpec.PriestDiscipline:
                case WoWSpec.PriestHoly:
                case WoWSpec.PriestShadow:
                    needSpell = "Shadow Word: Pain";
                    break;

                case WoWSpec.RogueCombat:
                    needSpell = "Blade Flurry";
                    break;

                case WoWSpec.RogueAssassination:
                case WoWSpec.RogueSubtlety:
                    needSpell = "Fan of Knives";
                    break;

                case WoWSpec.ShamanElemental:
                case WoWSpec.ShamanRestoration:
                    needSpell = "Chain Lightning";
                    break;

                case WoWSpec.ShamanEnhancement:
                    needSpell = "Flame Shock";
                    break;

                case WoWSpec.WarlockAffliction:
                case WoWSpec.WarlockDemonology:
                case WoWSpec.WarlockDestruction:
                    needSpell = "Corruption";
                    break;

                case WoWSpec.WarriorArms:
                case WoWSpec.WarriorFury:
                case WoWSpec.WarriorProtection:
                    needSpell = "Thunder Clap";
                    break;
            }

            bool allow = true;

            if (SingularSettings.Instance.UsePullMore == PullMoreUsageType.None)
            {
                allow = false;
                Logger.WriteDiagnostic("Pull More: disabled by user configuration (use:{0}, target:{1}, count:{2}",
                    SingularSettings.Instance.UsePullMore,
                    SingularSettings.Instance.PullMoreTargetType,
                    SingularSettings.Instance.PullMoreMobCount
                    );
            }
            else if (SingularSettings.Instance.UsePullMore == PullMoreUsageType.Auto && !SingularRoutine.IsGrindBotActive)
            {
                allow = false;
                BotBase b = SingularRoutine.GetCurrentBotBase();
                Logger.WriteDiagnostic("Pull More: disabled because use:{0} and botbase:{1} in use",
                    SingularSettings.Instance.UsePullMore,
                    b == null ? "(null)" : b.Name
                    );
            }
            else if ( SingularSettings.Instance.PullMoreTargetType == PullMoreTargetType.None || SingularSettings.Instance.PullMoreMobCount <= 1)
            {
                allow = false;
                Logger.WriteDiagnostic("Pull More: disabled by user configuration (use:{0}, target:{1}, count:{2}",
                    SingularSettings.Instance.UsePullMore,
                    SingularSettings.Instance.PullMoreTargetType,
                    SingularSettings.Instance.PullMoreMobCount
                    );                  
            }
            else if (SingularRoutine.CurrentWoWContext != WoWContext.Normal)
            {
                allow = false;
                Logger.WriteDiagnostic("Pull More: disabled automatically for Context = '{0}'", SingularRoutine.CurrentWoWContext);
            }
            else if (!SpellManager.HasSpell(needSpell))
            {
                allow = false;
                Logger.WriteDiagnostic("Pull More: disabled for{0} characters until [{1}] is learned", TalentManager.CurrentSpec.ToString().CamelToSpaced(), needSpell);
            }

            return allow;
        }

        private static Composite CreatePullMorePullBuffs()
        {
            if (IsPullMoreActive)
                return new ActionAlwaysFail();

            return new ActionAlwaysFail();
        }

        private static DateTime _nextPullMoreWaitingMessage = DateTime.MinValue;

        private static Composite CreatePullMorePull()
        {
            if (false == IsPullMoreActive)
                return new ActionAlwaysFail();

            _rangePullMore = Me.IsMelee() ? SingularSettings.Instance.PullMoreDistMelee : SingularSettings.Instance.PullMoreDistRanged;

            return new Decorator(
                req => HotkeyDirector.IsPullMoreEnabled && (_allowPullMoreUntil == DateTime.MinValue || _allowPullMoreUntil > DateTime.Now),

                new Sequence(
                    new PrioritySelector(

                        new Decorator(
                            req => Me.HealthPercent < SingularSettings.Instance.PullMoreMinHealth,
                            new Action(r => {
                                // disable pull more until we leave combat
                                Logger.WriteDiagnostic(Color.White, "Pull More: health dropped to {0:F1}%, finishing these before pulling more", Me.HealthPercent);
                                _allowPullMoreUntil = DateTime.Now;
                                })
                            ),

                        new Decorator(
                            req => ((DateTime.Now - Singular.Utilities.EventHandlers.LastAttackedByEnemyPlayer).TotalSeconds < 15),
                            new Action(r =>
                            {
                                Logger.WriteDiagnostic(Color.White, "Pull More: attacked by enemy player, disabling pull more until out of combat");
                                _allowPullMoreUntil = DateTime.Now;
                            })
                            ),

                        new PrioritySelector(

                            ctx => Unit.UnitsInCombatWithUsOrOurStuff(40)
                                .FirstOrDefault( u => u.TappedByAllThreatLists || (u.Elite && (u.Level + 8) > Me.Level) || (u.MaxHealth > (Me.MaxHealth * 2))),

                            new Decorator(
                                req => req != null,
                                new Action(r =>
                                {
                                    if ((r as WoWUnit).TappedByAllThreatLists)
                                        Logger.WriteDiagnostic(Color.White, "Pull More: attacked by important quest mob {0} #{1}, disabling pull more until killed", (r as WoWUnit).SafeName(), (r as WoWUnit).Entry);
                                    else if ((r as WoWUnit).Elite)
                                        Logger.WriteDiagnostic(Color.White, "Pull More: attacking non-trivial Elite {0} #{1}, disabling pull more until killed", (r as WoWUnit).SafeName(), (r as WoWUnit).Entry);
                                    else
                                        Logger.WriteDiagnostic(Color.White, "Pull More: attacking non-trivial Mob {0} #{1} maxhealth {2}, disabling pull more until killed", (r as WoWUnit).SafeName(), (r as WoWUnit).Entry, (r as WoWUnit).MaxHealth);

                                    _allowPullMoreUntil = DateTime.Now;
                                })
                                )
                            ),

                        new Action(r => {
                            int mobCount = Unit.UnitsInCombatWithUsOrOurStuff(50).Count();
                            if (mobCount >= SingularSettings.Instance.PullMoreMobCount)
                            {
                                Logger.WriteDiagnostic(Color.White, "Pull More: in combat with {0} mobs, finishing these before pulling more", mobCount);
                                _allowPullMoreUntil = DateTime.Now;
                            }
                            else if (BotPoi.Current == null)
                            {

                            }
                            else if (BotPoi.Current.Type != PoiType.Kill)
                            {

                            }
                            else if (BotPoi.Current.AsObject.ToUnit() == null)
                            {

                            }
                            else
                            {
                                WoWUnit unit = BotPoi.Current.AsObject.ToUnit();
                                if (unit.IsAlive && (!unit.IsTagged || !unit.IsTargetingMyStuff() || !unit.Combat))
                                {
                                    if (DateTime.Now > _timeoutPullMoreAt)
                                    {
                                        Logger.Write(Color.White, "Pull More: could not pull {0} @ {1:F1} yds within {2} seconds, blacklisting",
                                            unit.SafeName(),
                                            unit.SpellDistance(),
                                            SingularSettings.Instance.PullMoreTimeOut
                                            );
                                        Blacklist.Add(unit.Guid, BlacklistFlags.Pull, TimeSpan.FromMinutes(5), "Singular: pull more timed out");
                                        BotPoi.Clear("Singular: pull more timed out");
                                    }
                                    else
                                    {
                                        if (DateTime.Now > _nextPullMoreWaitingMessage)
                                        {
                                            Logger.WriteDebug("Pull More: waiting since current KillPoi {0} not attacking me yet (target={1}, combat={2}, tagged={3})",
                                                unit.SafeName(),
                                                unit.GotTarget ? unit.SafeName() : "(null)",
                                                unit.Combat.ToYN(),
                                                unit.IsTagged.ToYN()
                                                );
                                            _nextPullMoreWaitingMessage = DateTime.Now + TimeSpan.FromSeconds(0.5);
                                        }
                                    }
                                }
                                else
                                {
                                    _timeoutPullMoreAt = DateTime.MaxValue;

                                    // find a target
                                    Func<WoWUnit, bool> whereClause = null;
                                    HashSet<uint> factions;
                                    switch (SingularSettings.Instance.PullMoreTargetType)
                                    {
                                        case PullMoreTargetType.LikeCurrent:
                                            factions = new HashSet<uint>(
                                                ObjectManager.GetObjectsOfType<WoWUnit>(allowInheritance: true, includeMeIfFound: false)
                                                    .Where( u => u.TaggedByMe || u.Aggro || u.PetAggro)
                                                    .Select( u => u.FactionId)
                                                    .ToArray()
                                                );
                                            whereClause = t => factions.Contains(t.FactionId);
                                            break;

                                        case PullMoreTargetType.Hostile:
                                            whereClause = t => t.IsHostile;
                                            break;

                                        default:
                                        case PullMoreTargetType.All:
                                            whereClause = t => true;
                                            break;
                                    }

                                    WoWUnit nextPull = Unit.UnfriendlyUnits()
                                        .Where(
                                            t => !t.IsPlayer
                                                && !t.IsPet
                                                && !t.IsPetBattleCritter
                                                && !t.IsTagged
                                                && (!t.Combat || (t.GotTarget && !t.CurrentTarget.IsPlayer && !t.CurrentTarget.IsPet))
                                                && !Blacklist.Contains(t, BlacklistFlags.Pull | BlacklistFlags.Combat)
                                                && Unit.ValidUnit(t)
                                                && t.Level <= (Me.Level + 2)
                                                && whereClause(t)
                                                && t.SpellDistance() <= _rangePullMore
                                            )
                                        .OrderBy(k => (long)k.DistanceSqr)
                                        .FirstOrDefault();

                                    // set target at botpoi
                                    if (nextPull != null && unit.Guid != nextPull.Guid)
                                    {
                                        Logger.WriteDebug("Pull More: more adds allowed since current KillPoi {0}, target={1}, combat={2}, tagged={3}",
                                            unit.SafeName(),
                                            unit.GotTarget ? unit.SafeName() : "(null)",
                                            unit.Combat.ToYN(),
                                            unit.IsTagged.ToYN()
                                            );

                                        Logger.Write(Color.White, "Pull More: pull mob #{0} - {1} @ {2:F1} yds", mobCount, nextPull.SafeName(), nextPull.SpellDistance());
                                        BotPoi poi = new BotPoi(nextPull, PoiType.Kill, NavType.Run);
                                        Logger.WriteDebug("Setting BotPoi to Kill {0}", nextPull.SafeName());
                                        Styx.CommonBot.POI.BotPoi.Current = poi;
                                        if (Styx.CommonBot.POI.BotPoi.Current.Guid != poi.Guid)
                                            Logger.WriteDiagnostic(Color.White, "Pull More: ERROR, could not set POI: Current: {0}, Wanted: {1}", Styx.CommonBot.POI.BotPoi.Current, poi);
                                        else
                                        {
                                            nextPull.Target();
                                            _timeoutPullMoreAt = DateTime.Now + TimeSpan.FromSeconds(SingularSettings.Instance.PullMoreTimeOut);
                                            if (_allowPullMoreUntil == DateTime.MinValue)
                                                _allowPullMoreUntil = DateTime.Now + TimeSpan.FromSeconds(SingularSettings.Instance.PullMoreMaxTime);

                                            if (Me.Pet != null && (Me.Pet.CurrentTarget == null || Me.Pet.CurrentTargetGuid != Me.Guid))
                                            {
                                                PetManager.Attack(nextPull);
                                            }
                                        }
                                    }
                                }
                            }

                            return RunStatus.Failure;
                            })
                        ),

                    new ActionAlwaysFail()
                    )
                );
        }

        #endregion 

        private static Composite TestDynaWait()
        {
            return new PrioritySelector(
                    new Sequence(
                    new PrioritySelector(
                        new Sequence(
                            new DynaWait(ts => TimeSpan.FromSeconds(2), until => false, new ActionAlwaysSucceed(), true),
                            new Action(r => { Logger.Write("1. Success - Test Failed"); return RunStatus.Success; })
                            ),
                        new Action(r => { Logger.Write("1. Failure - Test Succeeded!"); return RunStatus.Success; })
                        ),
                    new ActionAlwaysFail()
                    ),
                new Sequence(
                    new PrioritySelector(
                        new Sequence(
                            new DynaWait(ts => TimeSpan.FromSeconds(2), until => true, new ActionAlwaysSucceed(), true),
                            new Action(r => { Logger.Write("2. Success - Test Succeeded!"); return RunStatus.Success; })
                            ),
                        new Action(r => { Logger.Write("2. Failure - Test Failed"); return RunStatus.Success; })
                        ),
                    new ActionAlwaysFail()
                    ),

                new Sequence(
                    new PrioritySelector(
                        new Sequence(
                            new DynaWait(ts => TimeSpan.FromSeconds(2), until => true, new ActionAlwaysFail(), true),
                            new Action(r => { Logger.Write("3. Success - Test Failed"); return RunStatus.Success; })
                            ),
                        new Action(r => { Logger.Write("3. Failure - Test Succeeded!"); return RunStatus.Success; })
                        ),
                    new ActionAlwaysFail()
                    ),
                new Sequence(
                    new PrioritySelector(
                        new Sequence(
                            new DynaWaitContinue(ts => TimeSpan.FromSeconds(2), until => false, new ActionAlwaysSucceed(), true),
                            new Action(r => { Logger.Write("4. Success - Test Succeeded!"); return RunStatus.Success; })
                            ),
                        new Action(r => { Logger.Write("4. Failure - Test Failed"); return RunStatus.Success; })
                        ),
                    new ActionAlwaysFail()
                    ),
                new Sequence(
                    new PrioritySelector(
                        new Sequence(
                            new DynaWaitContinue(ts => TimeSpan.FromSeconds(2), until => true, new ActionAlwaysSucceed(), true),
                            new Action(r => { Logger.Write("5. Success - Test Succeeded!"); return RunStatus.Success; })
                            ),
                        new Action(r => { Logger.Write("5. Failure - Test Failed"); return RunStatus.Success; })
                        ),
                    new ActionAlwaysFail()
                    ),

                new Sequence(
                    new PrioritySelector(
                        new Sequence(
                            new DynaWaitContinue(ts => TimeSpan.FromSeconds(2), until => true, new ActionAlwaysFail(), true),
                            new Action(r => { Logger.Write("6. Success - Test Failed"); return RunStatus.Success; })
                            ),
                        new Action(r => { Logger.Write("6. Failure - Test Succeeded!"); return RunStatus.Success; })
                        ),
                    new ActionAlwaysFail()
                    )
                );
        }
    }

    public class CallWatch : PrioritySelector
    {
        public static double SecondsBetweenWarnings { get; set; }

        public static DateTime LastCallToSingular { get; set; }
        public static ulong CountCallsToSingular { get; set; }
        public static TimeSpan TimeSpanSinceLastCall
        {
            get
            {
                TimeSpan since;
                if (LastCallToSingular == DateTime.MinValue)
                    since = TimeSpan.Zero;
                else
                    since = DateTime.Now - LastCallToSingular;
                return since;
            }
        }

        public string Name { get; set; }

        private static bool _init = false;

        private static void Initialize()
        {
            if (_init)
                return;

            _init = true;
            LastCallToSingular = DateTime.MinValue;

            SingularRoutine.OnBotEvent += (src, arg) =>
            {
                // reset time on Start
                if (arg.Event == SingularBotEvent.BotStart)
                    LastCallToSingular = DateTime.Now;
                else if (arg.Event == SingularBotEvent.BotStop)
                {
                    TimeSpan since = TimeSpanSinceLastCall;
                    if (since.TotalSeconds >= SecondsBetweenWarnings)
                    {
                        Logger.WriteDiagnostic(Color.HotPink, "info: {0:F1} seconds since BotBase last called Singular (now in OnBotStop)", since.TotalSeconds);
                    }
                }
            };
        }

        public CallWatch(string name, params Composite[] children)
            : base(children)
        {
            Initialize();

            if (SecondsBetweenWarnings == 0)
                SecondsBetweenWarnings = 5;

            Name = name;
        }
        /*
        protected override IEnumerable<RunStatus> Execute(object context)
        {
            IEnumerable<RunStatus> ret;
            CountCall++;

            if (SingularSettings.Debug)
            {
                if ((DateTime.Now - LastCall).TotalSeconds > WarnTime && LastCall != DateTime.MinValue)
                    Logger.WriteDebug(Color.HotPink, "info: {0:F1} seconds since BotBase last called Singular (now in {1})", (DateTime.Now - LastCall).TotalSeconds, Name);
            }

            if (!CallTrace)
            {
                ret = base.Execute(context);
            }
            else
            {
                DateTime started = DateTime.Now;
                Logger.Write(Color.DodgerBlue, "enter: {0}", Name);
                ret = base.Execute(context);
                Logger.Write(Color.DodgerBlue, "leave: {0}, took {1} ms", Name, (ulong)(DateTime.Now - started).TotalMilliseconds);
            }

            LastCall = DateTime.Now;
            return ret;
        }
        */
        public override RunStatus Tick(object context)
        {
            RunStatus ret;
            CountCallsToSingular++;

            if (SingularSettings.Debug)
            {
                TimeSpan since = TimeSpanSinceLastCall;
                if (since.TotalSeconds > SecondsBetweenWarnings && LastCallToSingular != DateTime.MinValue)
                    Logger.WriteDebug(Color.HotPink, "info: {0:F1} seconds since BotBase last called Singular (now in {1})", since.TotalSeconds, Name);
            }

            if (!SingularSettings.Trace )
            {
                ret = base.Tick(context);
            }
            else
            {
                DateTime started = DateTime.Now;
                Logger.WriteDebug(Color.DodgerBlue, "enter: {0}", Name);
                ret = base.Tick(context);
                Logger.WriteDebug(Color.DodgerBlue, "leave: {0}, status={1} and took {2} ms", Name, ret.ToString(), (ulong)(DateTime.Now - started).TotalMilliseconds);
            }

            LastCallToSingular = DateTime.Now;
            return ret;
        }
    }

    public class CallTrace : PrioritySelector
    {
        public static DateTime LastCall { get; set; }
        public static ulong CountCall { get; set; }
        public static bool TraceActive { get { return SingularSettings.Trace; } }

        public string Name { get; set; }

        private static bool _init = false;

        private static void Initialize()
        {
            if (_init)
                return;

            _init = true;
        }

        public CallTrace(string name, params Composite[] children)
            : base(children)
        {
            Initialize();

            Name = name;
            LastCall = DateTime.MinValue;
        }

        public override RunStatus Tick(object context)
        {
            RunStatus ret;
            CountCall++;

            if (!TraceActive )
            {
                ret = base.Tick(context);
            }
            else
            {
                DateTime started = DateTime.Now;
                Logger.WriteDebug(Color.LightBlue, "... enter: {0}", Name);
                ret = base.Tick(context);
                Logger.WriteDebug(Color.LightBlue, "... leave: {0}, took {1} ms", Name, (ulong)(DateTime.Now - started).TotalMilliseconds);
            }

            return ret;
        }

    }
}
