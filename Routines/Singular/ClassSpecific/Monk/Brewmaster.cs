using System;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Common.Helpers;
using Styx.TreeSharp;
using System.Collections.Generic;
using Styx.CommonBot;
using Action = Styx.TreeSharp.Action;
using Styx.WoWInternals.WoWObjects;

namespace Singular.ClassSpecific.Monk
{
    public class Brewmaster
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static MonkSettings MonkSettings { get { return SingularSettings.Instance.Monk(); } }
        public static bool HasTalent(MonkTalents tal) { return TalentManager.IsSelected((int)tal); }

        private static readonly WaitTimer _clashTimer = new WaitTimer(TimeSpan.FromSeconds(3));
        // ToDo  Summon Black Ox Statue, Chi Burst and 

        private static bool UseChiBrew
        {
            get
            {
                return TalentManager.IsSelected((int)MonkTalents.ChiBrew) && Me.CurrentChi == 0 && Me.CurrentTargetGuid > 0 &&
                       (SingularRoutine.CurrentWoWContext == WoWContext.Instances && Me.CurrentTarget.IsBoss() || SingularRoutine.CurrentWoWContext != WoWContext.Instances);
            }
        }

        // fix for when clash target is out of range due to long server generatord paths.
        private static Composite TryCastClashBehavior()
        {
            return new Decorator(
                ctx => _clashTimer.IsFinished && Spell.CanCastHack("Clash", Me.CurrentTarget) && Singular.Utilities.EventHandlers.LastNoPathTarget != Me.CurrentTargetGuid,
                new Sequence(new Action(ctx => SpellManager.Cast("Clash")), new Action(ctx => _clashTimer.Reset())));
        }

        [Behavior(BehaviorType.Pull, WoWClass.Monk, WoWSpec.MonkBrewmaster, WoWContext.Instances)]
        public static Composite CreateBrewmasterMonkPullInstances()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),
                new Decorator(
                    req => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Helpers.Common.CreateAutoAttack(true),
                        Helpers.Common.CreateInterruptBehavior(),
                        CreateDizzyingHazeSingleton(),
                        CreateCloseDistanceBehavior(),
                        Spell.Cast("Tiger Palm"),
                        Spell.Cast("Expel Harm", ret => Me.HealthPercent < 80 && Me.CurrentTarget.Distance < 10),
                        Spell.Cast("Jab")
                        )
                    )
                );
        }

        private static Composite _dishaze = null;
        private static Composite CreateDizzyingHazeSingleton()
        {
            if (_dishaze == null)
            {
                _dishaze = new Throttle(
                    TimeSpan.FromMilliseconds(2500),
                    new PrioritySelector(
                        ctx => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => (u.SpellDistance() > 15 || (u.IsMoving && !u.IsWithinMeleeRange && Me.IsSafelyBehind(u)))),
                        Spell.CastOnGround("Dizzying Haze", on => on as WoWUnit, req => true, false)
                        )
                    );

            }

            return _dishaze;
        }

        [Behavior(BehaviorType.Pull, WoWClass.Monk, WoWSpec.MonkBrewmaster, WoWContext.Normal)]
        [Behavior(BehaviorType.Pull, WoWClass.Monk, WoWSpec.MonkBrewmaster, WoWContext.Battlegrounds)]
        public static Composite CreateBrewmasterMonkPullNormal()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),
                new Decorator(
                    req => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Helpers.Common.CreateInterruptBehavior(),
                        CreateCloseDistanceBehavior(),
                        CreateDizzyingHazeSingleton(),
                        Spell.Cast("Tiger Palm"),
                        Spell.Cast("Expel Harm", ret => Me.HealthPercent < 80 && Me.CurrentTarget.Distance < 10),
                        Spell.Cast("Jab")
                //Only roll to get to the mob quicker. 
                        )
                    )
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Monk, WoWSpec.MonkBrewmaster, priority: 1)]
        public static Composite CreateBrewmasterMonkCombatBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Stance of the Sturdy Ox"),

                new Decorator(
                    req => !Unit.IsTrivial( Me.CurrentTarget),
                    new PrioritySelector(
                        Spell.BuffSelf(
                            "Avert Harm",
                            ctx =>
                            {
                                if (!MonkSettings.UseAvertHarm || !Me.GroupInfo.IsInParty)
                                    return false;
                                var nearbyGroupMembers = Me.RaidMembers.Where(r => !r.IsMe && r.Distance <= 10).ToList();
                                return nearbyGroupMembers.Any() && nearbyGroupMembers.Average(u => u.HealthPercent) <= MonkSettings.AvertHarmGroupHealthPct;
                            }),

                        Spell.BuffSelf("Zen Meditation", req => Targeting.Instance.TargetList.Any( u => u.IsCasting && u.CurrentTargetGuid != Me.Guid && Me.GroupInfo.IsInCurrentRaid(u.CurrentTargetGuid) && u.SpellDistance(u.CurrentTarget) < 20)),

                        Spell.BuffSelf("Fortifying Brew", ctx => Me.HealthPercent <= MonkSettings.FortifyingBrewPct),
                        Spell.BuffSelf("Guard", ctx => Me.HasAura("Power Guard")),
                        Spell.BuffSelf("Elusive Brew", ctx => MonkSettings.UseElusiveBrew && Me.HasAura("Elusive Brew", MonkSettings.ElusiveBrewMinumumCount)),
                        Spell.Cast("Chi Brew", ctx => UseChiBrew),
                        Spell.BuffSelf("Zen Sphere", ctx => HasTalent(MonkTalents.ZenSphere) && Me.HealthPercent < 90)
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Monk, WoWSpec.MonkBrewmaster, priority: 1)]
        public static Composite CreateBrewmasterMonkHeal()
        {
            return new PrioritySelector(
                // use chi wave as a heal.
                Spell.Cast(
                    "Chi Wave",
                    ctx =>
                    TalentManager.IsSelected((int)MonkTalents.ChiWave) &&
                    (Me.HealthPercent < MonkSettings.ChiWavePct ||
                     Me.RaidMembers.Count(m => m.DistanceSqr <= 20 * 20 && m.HealthPercent <= MonkSettings.ChiWavePct) >= 3))

                //Spell.Cast("Zen Sphere", ctx => TalentManager.IsSelected((int)MonkTalents.ZenSphere) && Me.RaidMembers.Count(m => m.DistanceSqr <= 20 * 20 && m.HealthPercent <= 70) >= 3)
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkBrewmaster, WoWContext.Normal)]
        public static Composite CreateBrewmasterMonkNormalCombat()
        {
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    req => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Helpers.Common.CreateAutoAttack(true),
                        Helpers.Common.CreateInterruptBehavior(),

                        Common.CreateGrappleWeaponBehavior(),

                        // keep these auras up!
                        Spell.Cast("Tiger Palm", ret => Me.HasKnownAuraExpired("Tiger Power", 1) || (SpellManager.HasSpell("Brewmaster Training") && Me.HasKnownAuraExpired("Power Guard", 1))),
                        Spell.Cast("Blackout Kick", ctx => SpellManager.HasSpell("Brewmaster Training") && Me.HasKnownAuraExpired("Shuffle", 1)),

                        // AOE
                        new Decorator(
                            req => Spell.UseAOE && Unit.NearbyUnfriendlyUnits.Count(u => u.SpellDistance() <= 8) >= 3,
                            new PrioritySelector(
                                ctx => Unit.NearbyUnfriendlyUnits.FirstOrDefault( u => Me.IsSafelyFacing(u,150f)),

                                // cast heal in anticipation of damage
                                Spell.BuffSelf("Zen Sphere", ctx => HasTalent(MonkTalents.ZenSphere) && Me.HealthPercent < 90),

                                new Decorator( 
                                    req => req != null,
                                    new PrioritySelector(
                                        // throw debuff on them to hit themselves and slow
                                        CreateDizzyingHazeSingleton(),

                                        // throw DoT on them
                                        Spell.Cast("Breath of Fire", 
                                            ctx => Clusters.GetCluster(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 8)
                                                .Any(u => u.HasAura("Dizzying Haze") && !u.HasAura("Breath of Fire"))),

                                        // aoe stuns (one per 3 seconds at most)
                                        new Throttle(3,
                                            new PrioritySelector(
                                                Spell.Cast("Charging Ox Wave", ctx => HasTalent(MonkTalents.ChargingOxWave) && Clusters.GetClusterCount(Me, Unit.NearbyUnfriendlyUnits.Where(u => !u.IsStunned()), ClusterType.Cone, 30) >= (Me.HealthPercent > 50 ? 3 : 1)),
                                                Spell.Cast("Leg Sweep", req => HasTalent(MonkTalents.LegSweep) && Unit.UnitsInCombatWithUsOrOurStuff(5).Count(u => !u.IsStunned()) >= (Me.HealthPercent > 50 ? 3 : 1))
                                                )
                                            ),
                                        Spell.Cast(sp => HasTalent(MonkTalents.RushingJadeWind) ? "Rushing Jade Wind" : "Spinning Crane Kick", req => Spell.UseAOE && Unit.NearbyUnfriendlyUnits.Count(u => u.SpellDistance() <= 8) >= MonkSettings.SpinningCraneKickCnt)
                                        )
                                    )
                                )
                            ),

                        // execute if we can
                        Spell.Cast("Touch of Death", ret => Me.CurrentChi >= 3 && Me.HasAura("Death Note")),

                        // stun if configured to do so
                        Spell.Cast("Leg Sweep", ret => Spell.UseAOE && MonkSettings.StunMobsWhileSolo && SingularRoutine.CurrentWoWContext == WoWContext.Normal && Me.CurrentTarget.IsWithinMeleeRange),

                        // make sure I have aggro.
                        Spell.Cast("Provoke", ret => TankManager.Instance.NeedToTaunt.FirstOrDefault(), ret => SingularSettings.Instance.EnableTaunting),
                        Spell.Cast("Keg Smash", ctx => Me.CurrentChi < (Me.MaxChi - 2)),

                        // use dizzying only if target not in range of keg smash and running away or further than roll distance
                        // ... avoid crowd controlled targets 
                        new Throttle( TimeSpan.FromMilliseconds(2500),                           
                            Spell.CastOnGround(
                                "Dizzying Haze", 
                                on => Me.CurrentTarget, 
                                req => !Me.CurrentTarget.HasMyAura("Dizzying Haze")
                                    && !Me.CurrentTarget.IsCrowdControlled()
                                    && (Me.CurrentTarget.SpellDistance() > 15 || (Me.CurrentTarget.IsMoving && !Me.CurrentTarget.IsWithinMeleeRange && Me.IsSafelyBehind(Me.CurrentTarget))), 
                                waitForSpell: false
                                )
                            ),

                        Spell.Cast("Tiger Palm", ret => Me.CurrentChi >= 1 && Me.HasKnownAuraExpired("Tiger Power")),
                        Spell.Cast("Blackout Kick", ret => Me.CurrentChi >= 2),
                        Spell.Cast("Jab"),

                        CreateCloseDistanceBehavior()
                        )
                    )
                );
        }


        [Behavior(BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkBrewmaster, WoWContext.Battlegrounds)]
        public static Composite CreateBrewmasterMonkPvpCombat()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                CreateCloseDistanceBehavior(),

                Helpers.Common.CreateInterruptBehavior(),

                Common.CreateGrappleWeaponBehavior(),

                Spell.Cast("Touch of Death", ret => Me.CurrentChi >= 3 && Me.HasAura("Death Note")),
                // slow if current target running away
                new Throttle( TimeSpan.FromMilliseconds(1500),                           
                    Spell.CastOnGround("Dizzying Haze", on => Me.CurrentTarget, req => Me.CurrentTarget.IsMoving && !Me.IsWithinMeleeRange && Me.IsSafelyBehind(Me.CurrentTarget), waitForSpell: false)
                    ),
                Spell.Cast("Tiger Palm", ret => Me.CurrentChi >= 1 && Me.HasKnownAuraExpired("Tiger Power")),
                Spell.Cast("Blackout Kick", ret => Me.CurrentChi >= 2),
                Spell.Cast("Jab")
                );
        }

        #region Instance

        [Behavior(BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkBrewmaster, WoWContext.Instances)]
        public static Composite CreateBrewmasterMonkInstanceCombat()
        {
            TankManager.NeedTankTargeting = (SingularRoutine.CurrentWoWContext == WoWContext.Instances);

            var powerStrikeTimer = new WaitTimer(TimeSpan.FromSeconds(20));

            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                CreateCloseDistanceBehavior(),
                Helpers.Common.CreateAutoAttack(true),

                Spell.WaitForCastOrChannel(),

                new Decorator(
                    req => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Helpers.Common.CreateInterruptBehavior(),

                        // Execute if we can
                        Spell.Cast("Touch of Death", ret => Me.CurrentChi >= 3 && Me.HasAura("Death Note")),

                        // make sure I have aggro.
                        Spell.Cast("Provoke", ret => TankManager.Instance.NeedToTaunt.FirstOrDefault(), ret => SingularSettings.Instance.EnableTaunting),

                        // highest priority -- Keg Smash for threat and debuff
                        Spell.Cast("Keg Smash", req => Me.MaxChi - Me.CurrentChi >= 2 && Clusters.GetCluster(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8).Any(u => !u.HasAura("Weakened Blows"))),

                        // taunt if needed
                        new Throttle( TimeSpan.FromMilliseconds(1500),                           
                            Spell.CastOnGround("Dizzying Haze", on => TankManager.Instance.NeedToTaunt.FirstOrDefault(), req => TankManager.Instance.NeedToTaunt.Any(), false)
                            ),

                        // AOE
                        new Decorator(
                            req => Spell.UseAOE && Unit.UnfriendlyUnits(8).Count() >= 3,
                            new PrioritySelector(
                        // cast breath of fire to apply the dot.
                                Spell.Cast("Breath of Fire", ctx => Clusters.GetCluster(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 8).Count(u => u.HasAura("Dizzying Haze") && !u.HasAura("Breath of Fire")) >= 3),
                                Spell.BuffSelf("Zen Sphere", ctx => HasTalent( MonkTalents.ZenSphere) && Me.HealthPercent < 90),
                        // aoe stuns
                                Spell.Cast("Charging Ox Wave", ctx => TalentManager.IsSelected((int)MonkTalents.ChargingOxWave) && Clusters.GetClusterCount(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 30) >= 3),
                                Spell.Cast("Leg Sweep", ctx => TalentManager.IsSelected((int)MonkTalents.LegSweep))
                                )
                            ),

                        // ***** Spend Chi *****
                        // Spell.Cast("Rushing Jade Wind", ctx => TalentManager.IsSelected((int)MonkTalents.RushingJadeWind) && (!Me.HasAura("Shuffle") || Me.Auras["Shuffle"].TimeLeft <= TimeSpan.FromSeconds(1))),
                        Spell.Cast("Blackout Kick", ctx => !SpellManager.HasSpell("Brewmaster Training") || Me.HasKnownAuraExpired("Shuffle", 1)),
                        Spell.Cast("Tiger Palm", ret => Me.HasKnownAuraExpired("Tiger Power", 1) || (SpellManager.HasSpell("Brewmaster Training") && Me.HasKnownAuraExpired("Power Guard", 1))),

                        Spell.BuffSelf("Purifying Brew", ctx => Me.HasAura("Stagger") && Me.CurrentChi >= 3),

                        // ***** Generate Chi *****
                        new Decorator(
                            req => Me.CurrentChi < Me.MaxChi,
                            new PrioritySelector(
                                Spell.Cast("Keg Smash", ctx => Me.MaxChi - Me.CurrentChi >= 2),
                                Spell.Cast("Rushing Jade Wind", req => Spell.UseAOE && HasTalent(MonkTalents.RushingJadeWind) && Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr <= 8 * 8) >= 3),
                                Spell.Cast("Spinning Crane Kick", req => Spell.UseAOE && Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr <= 8 * 8) >= MonkSettings.SpinningCraneKickCnt),

                                // jab with power strike talent is > expel Harm if off CD.
                                new Decorator(ctx => TalentManager.IsSelected((int)MonkTalents.PowerStrikes) && Me.MaxChi - Me.CurrentChi >= 2 && Spell.CanCastHack("Jab") && powerStrikeTimer.IsFinished,
                                    new Sequence(
                                        new Action(ctx => powerStrikeTimer.Reset()),
                                        Spell.Cast("Jab")
                                        )
                                    ),

                                Spell.Cast("Expel Harm", req => Me.HealthPercent < 90 && Unit.NearbyUnfriendlyUnits.Any(u => u.DistanceSqr <= 10 * 10)),
                                Spell.Cast("Jab")
                                )
                            ),

                        // filler:
                        // cast on cooldown when Level < 26 (Guard) or Level >= 34 (Brewmaster Training), otherwise try to save some Chi for Guard if available
                        Spell.Cast("Tiger Palm", ret => SpellManager.HasSpell("Brewmaster Training") || Me.CurrentChi > 2 || !SpellManager.HasSpell("Guard") || Spell.GetSpellCooldown("Guard").TotalSeconds > 4)

                        )
                    )
                );
        }

        #endregion

        public static Composite CreateCloseDistanceBehavior()
        {
            return new Throttle(TimeSpan.FromMilliseconds(1500),
                new Decorator(
                    ret => MovementManager.IsClassMovementAllowed && Me.GotTarget,
                    new PrioritySelector(
                        ctx => Me.CurrentTarget,
                        new Decorator( 
                            req => !((WoWUnit)req).IsAboveTheGround() && ((WoWUnit)req).SpellDistance() > 10 && Me.IsSafelyFacing(((WoWUnit)req), 10f),
                            new Sequence(
                                new PrioritySelector(
                                    Spell.Cast("Clash", on => (WoWUnit) on),
                                    Spell.Cast("Roll", on => (WoWUnit) on, req => !MonkSettings.DisableRoll)
                                    )
                                )
                            )
                        )
                    )
                );

        }


    }
}