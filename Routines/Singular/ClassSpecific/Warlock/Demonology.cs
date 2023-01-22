using System;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;

using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Styx;
using System.Linq;
using Singular.Settings;

using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using System.Drawing;
using CommonBehaviors.Actions;
using Styx.Common.Helpers;

namespace Singular.ClassSpecific.Warlock
{
    public class Demonology
    {

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WarlockSettings WarlockSettings { get { return SingularSettings.Instance.Warlock(); } }
        private static uint CurrentDemonicFury { get { return Me.GetCurrentPower(WoWPowerType.DemonicFury); } }

        private static int _mobCount;
        public static readonly WaitTimer demonFormRestTimer = new WaitTimer(TimeSpan.FromSeconds(3));

        private static DateTime _lastSoulFire = DateTime.MinValue;

        #region Normal Rotation

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Warlock, WoWSpec.WarlockDemonology, WoWContext.All)]
        public static Composite CreateWarlockDemonologyNormalCombat()
        {
            Kite.CreateKitingBehavior(CreateSlowMeleeBehavior(), null, null);

            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCast(FaceDuring.Yes),

                new Decorator(ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        // calculate key values
                        new Action(ret =>
                        {
                            Me.CurrentTarget.TimeToDeath();
                            _mobCount = Common.TargetsInCombat.Where(t => t.Distance <= (Me.MeleeDistance(t) + 3)).Count();
                            return RunStatus.Failure;
                        }),

                        CreateWarlockDiagnosticOutputBehavior(Dynamics.CompositeBuilder.CurrentBehaviorType.ToString()),

                        Helpers.Common.CreateAutoAttack(true),
                        new Decorator(
                            ret => Me.GotAlivePet && Me.GotTarget && Me.Pet.CurrentTarget != Me.CurrentTarget,
                            new Action(ret =>
                            {
                                PetManager.CastPetAction("Attack");
                                return RunStatus.Failure;
                            })
                            ),

                        Helpers.Common.CreateInterruptBehavior(),

                        // even though AOE spell, keep on CD for single target unless AoE turned off
                        new Decorator(
                            ret => Spell.UseAOE && WarlockSettings.FelstormMobCount > 0 && Common.GetCurrentPet() == WarlockPet.Felguard && !Spell.IsSpellOnCooldown("Command Demon") && Me.Pet.GotTarget,
                            new Sequence(
                                new PrioritySelector(
                                    ctx =>
                                    {
                                        int mobCount = Unit.UnfriendlyUnits().Where(t => Me.Pet.SpellDistance(t) < 8f).Count();
                                        if (mobCount > 0)
                                        {
                                            // try not to waste Felstorm if mob will die soon anyway
                                            if (mobCount == 1)
                                            {
                                                if (Me.Pet.CurrentTargetGuid == Me.CurrentTargetGuid && !Me.CurrentTarget.IsPlayer && Me.CurrentTarget.TimeToDeath() < 6)
                                                {
                                                    Logger.WriteDebug("Felstorm: found {0} mobs within 8 yds of Pet, but saving Felstorm since it will die soon", mobCount, !Me.Pet.GotTarget ? -1f : Me.Pet.SpellDistance(Me.Pet.CurrentTarget));
                                                    return 0;
                                                }
                                            }

                                            if (SingularSettings.Debug)
                                                Logger.WriteDebug("Felstorm: found {0} mobs within 8 yds of Pet; Pet is {1:F1} yds from its target", mobCount, !Me.Pet.GotTarget ? -1f : Me.Pet.SpellDistance(Me.Pet.CurrentTarget));
                                        }
                                        return mobCount;
                                    },
                                    Spell.Cast("Wrathstorm", req => ((int)req) >= WarlockSettings.FelstormMobCount),
                                    Spell.Cast("Felstorm", req => ((int)req) >= WarlockSettings.FelstormMobCount)
                /*
                                                    ,
                                                    new Decorator(
                                                        req => Me.Pet.GotTarget && !Me.Pet.CurrentTarget.HasAuraWithEffect(WoWApplyAuraType.ModHealingReceived),
                                                        new PrioritySelector(
                                                            Spell.Cast( "Mortal Cleave", req => (int)req < WarlockSettings.FelstormMobCount || Spell.IsSpellOnCooldown("Felstorm")),
                                                            Spell.Cast( "Legion Strike", req => (int) req < WarlockSettings.FelstormMobCount || Spell.IsSpellOnCooldown("Felstorm"))
                                                            )
                                                        )
                */
                                    ),
                                new ActionAlwaysFail()  // no GCD on Felstorm, allow to fall through
                                )
                            ),

                        Avoidance.CreateAvoidanceBehavior("Demonic Leap", 20, Disengage.Direction.Frontwards, new ActionAlwaysSucceed() ),

            #region Felguard Use

 new Decorator(
                            ret => Common.GetCurrentPet() == WarlockPet.Felguard && Me.GotTarget && Me.CurrentTarget.Fleeing,
                            Pet.CastPetAction("Axe Toss")
                            ),

            #endregion


            #region CurrentTarget DoTs

                // check two main DoTs so we cast based upon current state before we look at entering/leaving Metamorphosis
                        Spell.Cast("Corruption", req => !Me.HasAura("Metamorphosis") && Me.CurrentTarget.HasAuraExpired("Corruption", 3)),
                        new Throttle(1,
                            new Sequence(
                                Spell.CastHack("Metamorphosis: Doom", "Doom", on => Me.CurrentTarget, req => Me.HasAura("Metamorphosis") && (Me.CurrentTarget.HasAuraExpired("Metamorphosis: Doom", "Doom", 10) && DoesCurrentTargetDeserveToGetDoom()) || NeedToReapplyDoom()),
                                new WaitContinue(TimeSpan.FromMilliseconds(350), canRun => Me.CurrentTarget.HasAura("Doom"), new ActionAlwaysSucceed())
                                )
                            ),

            #endregion

            #region Enter/Exit Metamorphosis based upon needs and fury levels

                // manage metamorphosis. don't use Spell.Cast family so we can manage the use of CanCast()
                        new Decorator(
                            ret => NeedToApplyMetamorphosis(),
                            new Sequence(
                                new Action(ret => Logger.Write(Color.White, "^Applying Metamorphosis Buff")),
                                new Action(ret => SpellManager.Cast("Metamorphosis", Me)),
                                new WaitContinue(
                                    TimeSpan.FromMilliseconds(450),
                                    canRun => Me.HasAura("Metamorphosis"),
                                    new Action(r =>
                                    {
                                        demonFormRestTimer.Reset();
                                        return RunStatus.Success;
                                    })
                                    )
                                )
                            ),

                        new Decorator(
                            ret => NeedToCancelMetamorphosis(),
                            new Sequence(
                                new Action(ret => Logger.Write(Color.White, "^Cancel Metamorphosis Buff")),
                // new Action(ret => Lua.DoString("CancelUnitBuff(\"player\",\"Metamorphosis\");")),
                                new Action(ret => Me.CancelAura("Metamorphosis")),
                                new WaitContinue(TimeSpan.FromMilliseconds(450), canRun => !Me.HasAura("Metamorphosis"), new ActionAlwaysSucceed())
                                )
                            ),
            #endregion

            #region AOE

                // must appear after Mob count and Metamorphosis handling
                        CreateDemonologyAoeBehavior(),

            #endregion

            #region Single Target

                // when 2 stacks present, don't throttle cast
                        new Sequence(
                            ctx =>
                            {
                                uint stacks = Me.GetAuraStacks("Molten Core");
                                if (stacks > 0 && (DateTime.Now - _lastSoulFire).TotalMilliseconds < 250)
                                    stacks--;
                                return stacks;
                            },
                            Spell.Cast("Soul Fire", mov => true, on => Me.CurrentTarget, req => ((uint)req) > 0, cancel => false),
                            new Action(r => _lastSoulFire = DateTime.Now)
                            ),


                        new Decorator(
                            ret => Me.HasAura("Metamorphosis"),
                            new PrioritySelector(
                                Spell.CastHack("Metamorphosis: Touch of Chaos", "Touch of Chaos", on => Me.CurrentTarget, req => true),
                                Spell.Cast("Soul Fire", ret => Me.Level < 25 /* dont know Touch of Chaos -or- Shadow Bolt */ ),
                                Spell.Cast("Shadow Bolt")
                                )
                            ),

                        new Decorator(
                            ret => !Me.HasAura("Metamorphosis"),
                            new PrioritySelector(
                                CreateHandOfGuldanBehavior(),
                                Spell.Cast("Shadow Bolt"),
                                Spell.Cast("Fel Flame", req => Me.IsMoving)
                                )
                            )

            #endregion
)
                    )
                );
        }


        #region Handle Forcing Reapply of Doom if Needed due to Buff/Proc

        static ulong _guidLastUberDoom = 0;
        static DateTime _timeNextUberDoom = DateTime.Now;

        private static bool NeedToReapplyDoom()
        {
            if (Me.HasAura("Perfect Aim") && (_guidLastUberDoom != Me.CurrentTargetGuid || _timeNextUberDoom < DateTime.Now))
            {
                _guidLastUberDoom = Me.CurrentTargetGuid;
                _timeNextUberDoom = DateTime.Now + TimeSpan.FromSeconds(60);
                Logger.Write(Color.White, "^Perfect Aim: applying 100% Critical Doom");
                return true;
            }

            return false;
        }

        #endregion

        private static uint endMoltenCore = 0;
        private static uint stackMoltenCore = 0;

        private static Composite CreateHandOfGuldanBehavior()
        {
            return new Throttle(
                TimeSpan.FromMilliseconds(2000),
                new Decorator( 
                    ret => Me.CurrentTarget.HasAuraExpired("Hand of Gul'dan", "Shadowflame", 1),
                    new PrioritySelector(
                        ctx => TalentManager.HasGlyph("Hand of Gul'dan"),
                        Spell.CastOnGround("Hand of Gul'dan", on => Me.CurrentTarget, req => Me.GotTarget && (bool)req),
                        Spell.Cast("Hand of Gul'dan", req => !(bool) req)
                        )
                    )
                );
        }

        private static bool NeedToApplyMetamorphosis()
        {
            bool hasAura = Me.HasAura("Metamorphosis");
            bool shouldCast = false;

            if (!hasAura && Me.GotTarget)
            {
                string msg = "";
                // check if we need Doom and have enough fury for 2 secs in form plus cast
                if (CurrentDemonicFury >= 72 && Me.CurrentTarget.HasAuraExpired("Metamorphosis: Doom", "Doom") && DoesCurrentTargetDeserveToGetDoom())
                {
                    shouldCast = true;
                    msg = "true, because target needs Doom";
                }
                // check if we have Corruption and we need to dump fury
                else if (CurrentDemonicFury >= WarlockSettings.FurySwitchToDemon && !Me.CurrentTarget.HasKnownAuraExpired("Corruption", secs: 0, myAura: true))
                {
                    shouldCast = true;
                    msg = "true, because Fury above " + WarlockSettings.FurySwitchToDemon.ToString() + " and has Corruption";
                }
                else if (Me.HasAnyAura("Dark Soul: Knowledge", "Perfect Aim"))
                {
                    shouldCast = true;
                    msg = "true, because has Dark Soul";
                }

                // if we need to cast, check that we can
                if (shouldCast)
                {
                    if (SingularSettings.Debug && !String.IsNullOrWhiteSpace(msg))
                        Logger.WriteDebug("Apply Metamorphosis: " + msg);

                    shouldCast = Spell.CanCastHack("Metamorphosis", Me, false);
                }
            }

            return shouldCast;
        }

        private static bool DoesCurrentTargetDeserveToGetDoom()
        {
            if (SingularRoutine.CurrentWoWContext != WoWContext.Normal)
                return true;

            if ( Me.CurrentTarget.IsPlayer )
                return true;

            if ( Me.CurrentTarget.Elite && (Me.CurrentTarget.Level + 10) >= Me.Level )
                return true;

            return Me.CurrentTarget.TimeToDeath() > 45;
        }

        private static bool NeedToCancelMetamorphosis()
        {
            bool hasAura = Me.HasAura("Metamorphosis");
            bool shouldCancel = false;

            if (hasAura && Me.GotTarget)
            {
                string msg = "";

                // switch back if not enough fury to cast anything (abc - always be casting)
                if (CurrentDemonicFury < 40)
                {
                    shouldCancel = true;
                    msg = "true, because Fury < 40";
                }
                // check if we should stay in demon form because of buff
                else if (Me.HasAnyAura("Dark Soul: Knowledge", "Perfect Aim"))
                {
                    shouldCancel = false;
                    msg = "false, because Dark Soul or Perfect Aim active";
                }
                // check if we should stay in demon form because of Doom falling off
                else if (CurrentDemonicFury >= 60 && Me.CurrentTarget.HasAuraExpired("Metamorphosis: Doom", "Doom"))
                {
                    shouldCancel = false;
                    msg = "false, because Doom has expired on target";
                }
                // finally... now check if we should cancel 
                else if ( Me.CurrentTarget.HasKnownAuraExpired("Corruption", 0, myAura: true))
                {
                    shouldCancel = true;
                    msg = "true, because Corruption fell off target";
                }
                else if (CurrentDemonicFury < WarlockSettings.FurySwitchToCaster)
                {
                    shouldCancel = true;
                    msg = "true, because Fury < " + WarlockSettings.FurySwitchToCaster.ToString();
                }

                // do not need to check CanCast() on the cancel since we cancel the aura...
                if (SingularSettings.Debug && !String.IsNullOrWhiteSpace(msg))
                    Logger.WriteDebug("Cancel Metamorphosis: " + msg);
            }

            return shouldCancel;
        }

        #endregion

        #region AOE

        private static Composite CreateDemonologyAoeBehavior()
        {
            return new Decorator(
                ret => Spell.UseAOE,
                new PrioritySelector(
/*
                    new Decorator(
                        ret => Common.GetCurrentPet() == WarlockPet.Felguard && Unit.NearbyUnfriendlyUnits.Count(u => u.Location.DistanceSqr(Me.Pet.Location) < 8 * 8) > 1,
                        Pet.CreateCastPetAction("Felstorm")
                        ),
*/
                    new Decorator(
                        ret => Me.HasAura("Metamorphosis"),
                        new PrioritySelector(
                            Spell.Cast("Hellfire", ret => _mobCount >= 4 && SpellManager.HasSpell("Hellfire") && !Me.HasAura("Immolation Aura")),
                            new Decorator(
                                ret => _mobCount >= 2 && Common.TargetsInCombat.Count(t => !t.HasAuraExpired("Metamorphosis: Doom", "Doom", 1)) < Math.Min( _mobCount, 3),
                                Spell.CastHack( "Metamorphosis: Doom", "Doom", on => Common.TargetsInCombat.FirstOrDefault(m => m.HasAuraExpired("Metamorphosis: Doom", "Doom", 1)), req => true)
                                )
                            )
                        ),

                    new Decorator(
                        ret => !Me.HasAura("Metamorphosis"),
                        new PrioritySelector(
                            new Decorator(
                                ret => _mobCount >= 2 && Common.TargetsInCombat.Count(t => !t.HasAuraExpired("Corruption")) < Math.Min( _mobCount, 3),
                                Spell.Cast("Corruption", ctx => Common.TargetsInCombat.FirstOrDefault(m => m.HasAuraExpired("Corruption")))
                                )
                            )
                        )
                    )
                );
        }


        private static WoWUnit GetAoeDotTarget( string dotName)
        {
            WoWUnit unit = null;
            if (SpellManager.HasSpell(dotName))
                unit = Common.TargetsInCombat.FirstOrDefault(t => !t.HasAuraExpired(dotName));

            return unit;
        }

        #endregion

        private static Composite CreateSlowMeleeBehavior()
        {
            return new PrioritySelector(
                ctx => SafeArea.NearestEnemyMobAttackingMe,
                new Decorator(
                    ret => ret != null,
                    new PrioritySelector(
                        new Throttle(2,
                            new PrioritySelector(
                                Spell.CastHack("Metamorphosis: Chaos Wave", "Chaos Wave", on => Me.CurrentTarget, req => Me.HasAura("Metamorphosis")),
                                Spell.Buff("Shadowfury", onUnit => (WoWUnit)onUnit),
                                Spell.Buff("Howl of Terror", onUnit => (WoWUnit)onUnit),
                                new Decorator(
                                    ret => !Me.HasAura("Metamorphosis"),
                                    new PrioritySelector(
                                        Spell.Buff("Hand of Gul'dan", onUnit => (WoWUnit)onUnit, req => !TalentManager.HasGlyph("Hand of Gul'dan")),
                                        Spell.CastOnGround("Hand of Gul'dan", on => (WoWUnit)on, req => Me.GotTarget && TalentManager.HasGlyph("Hand of Gul'dan"), false)
                                        )
                                    ),
                                Spell.Buff("Mortal Coil", onUnit => (WoWUnit)onUnit),
                                Spell.Buff("Fear", onUnit => (WoWUnit)onUnit)
                                )
                            )
                        )
                    )
                );
        }

        private static Composite CreateWarlockDiagnosticOutputBehavior(string s)
        {
            return new ThrottlePasses(1, 1,
                new Decorator(
                    ret => SingularSettings.Debug,
                    new Action(ret =>
                    {
                        WoWUnit target = Me.CurrentTarget;
                        uint lstks = !Me.HasAura("Molten Core") ? 0 : Me.ActiveAuras["Molten Core"].StackCount;

                        string msg;

                        msg = string.Format(".... [{0}] h={1:F1}%/m={2:F1}%, fury={3}, metamor={4}, mcore={5}, darksoul={6}, aoecnt={7}",
                            s,
                             Me.HealthPercent,
                             Me.ManaPercent,
                             CurrentDemonicFury,
                             Me.HasAura("Metamorphosis"),
                             lstks,
                             Me.HasAura("Dark Soul: Knowledge"),
                             _mobCount
                             );

                        if (target != null)
                        {
                            msg += string.Format(", enemy={0}% @ {1:F1} yds, face={2}, loss={3}, corrupt={4}, doom={5}, shdwflm={6}, ttd={7}",
                                (int)target.HealthPercent,
                                target.Distance,
                                Me.IsSafelyFacing(target).ToYN(),
                                target.InLineOfSpellSight.ToYN(),
                                (long)target.GetAuraTimeLeft("Corruption", true).TotalMilliseconds,
                                (long)target.GetAuraTimeLeft("Doom", true).TotalMilliseconds,
                                (long)target.GetAuraTimeLeft("Shadowflame", true).TotalMilliseconds,
                                target.TimeToDeath()
                                );
                        }

                        Logger.WriteDebug(Color.Wheat, msg);
                        return RunStatus.Failure;
                    })
                )
            );
        }

    }
}