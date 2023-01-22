using System.Linq;
using System.Runtime.Remoting.Contexts;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.CommonBot;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Rest = Singular.Helpers.Rest;
using System;

namespace Singular.ClassSpecific.Paladin
{
    public static class Holy
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static PaladinSettings PaladinSettings { get { return SingularSettings.Instance.Paladin(); } }
        public static bool HasTalent(PaladinTalents tal) { return TalentManager.IsSelected((int)tal); }

        [Behavior(BehaviorType.Rest, WoWClass.Paladin, WoWSpec.PaladinHoly)]
        public static Composite CreatePaladinHolyRest()
        {
            return new PrioritySelector(
                Spell.WaitForCast(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        // Heal self before resting. There is no need to eat while we have 100% mana
                        CreatePaladinHealBehavior(true),
                        // Rest up damnit! Do this first, so we make sure we're fully rested.
                        Rest.CreateDefaultRestBehaviour( null, "Redemption"),
                        // Make sure we're healing OOC too!
                        CreatePaladinHealBehavior(false, false)
                        )
                    )
                );
        }
        [Behavior(BehaviorType.Heal, WoWClass.Paladin, WoWSpec.PaladinHoly)]
        public static Composite CreatePaladinHolyHealBehavior()
        {
            return new PrioritySelector(
                Spell.BuffSelf( "Devotion Aura", req => Me.Silenced ),
                CreateRebirthBehavior(),
                CreatePaladinHealBehavior(),
                new Decorator(
                    req => !Unit.NearbyGroupMembers.Any(m => m.IsAlive && !m.IsMe),
                    new PrioritySelector(
                        Spell.Cast("Lay on Hands",
                            mov => false,
                            on => Me,
                            req => Me.PredictedHealthPercent(includeMyHeals: true) <= PaladinSettings.SelfLayOnHandsHealth),
                        Common.CreateWordOfGloryBehavior(on => Me),
                        Spell.Cast("Flash of Light",
                            mov => true,
                            on => Me,
                            req => Me.PredictedHealthPercent(includeMyHeals: true) <= PaladinSettings.SelfFlashOfLightHealth,
                            cancel => Me.HealthPercent > PaladinSettings.SelfFlashOfLightHealth)
                        )
                    )
                );
        }
        [Behavior(BehaviorType.CombatBuffs, WoWClass.Paladin, WoWSpec.PaladinHoly)]
        public static Composite CreatePaladinHolyCombatBuffsBehavior()
        {
            return
                new PrioritySelector(
                    Spell.BuffSelf(
                        "Divine Plea",
                        ret => StyxWoW.Me.ManaPercent <= SingularSettings.Instance.Paladin().DivinePleaMana)
                    );
        }
        [Behavior(BehaviorType.Combat, WoWClass.Paladin, WoWSpec.PaladinHoly)]
        public static Composite CreatePaladinHolyCombatBehavior()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => !Unit.NearbyGroupMembers.Any(m => m.IsAlive && !m.IsMe),
                    new PrioritySelector(
                        Helpers.Common.EnsureReadyToAttackFromMelee(),
                        Spell.WaitForCastOrChannel(),

                        new Decorator( 
                            ret => !Spell.IsGlobalCooldown() && Me.GotTarget,
                            new PrioritySelector(
                                Helpers.Common.CreateAutoAttack(true),
                                Helpers.Common.CreateInterruptBehavior(),

                                Common.CreatePaladinPullMore(),

                                Common.CreatePaladinBlindingLightBehavior(),

                                Spell.Cast("Hammer of Justice", ret => PaladinSettings.StunMobsWhileSolo && SingularRoutine.CurrentWoWContext == WoWContext.Normal),
                                Spell.Buff("Judgment"),
                                Spell.Cast("Hammer of Wrath"),
                                Spell.Cast("Holy Shock"),
                                Spell.Cast("Crusader Strike"),
                                Spell.Cast("Denounce")
                                )
                            ),

                        Movement.CreateMoveToMeleeBehavior(true)
                        )
                    )
                );
        }
        [Behavior(BehaviorType.Pull, WoWClass.Paladin, WoWSpec.PaladinHoly)]
        public static Composite CreatePaladinHolyPullBehavior()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => !StyxWoW.Me.GroupInfo.IsInParty && !StyxWoW.Me.GroupInfo.IsInRaid,
                    new PrioritySelector(
                        Helpers.Common.EnsureReadyToAttackFromMelee(),
                        Spell.Cast("Judgment"),
                        Helpers.Common.CreateAutoAttack(true),
                        Movement.CreateMoveToMeleeBehavior(true)
                        ))
                );
        }

        internal static Composite CreatePaladinHealBehavior()
        {
            return CreatePaladinHealBehavior(false, true);
        }

        internal static Composite CreatePaladinHealBehavior(bool selfOnly)
        {
            return CreatePaladinHealBehavior(selfOnly, false);
        }

        private static WoWUnit _healTarget;

        internal static Composite CreatePaladinHealBehavior(bool selfOnly, bool moveInRange)
        {
            HealerManager.NeedHealTargeting = true;

            return
                new PrioritySelector(
                    ctx => _healTarget = (selfOnly ? StyxWoW.Me : HealerManager.Instance.FirstUnit),
                    new Decorator(
                        ret => ret != null && (selfOnly || moveInRange || ((WoWUnit)ret).InLineOfSpellSight && ((WoWUnit)ret).DistanceSqr < 40 * 40),
                        new PrioritySelector(
                            Spell.WaitForCast(),
                            new Decorator(
                                ret => moveInRange,
                                Movement.CreateMoveToLosBehavior(ret => (WoWUnit)ret)),

                            Spell.Cast(
                                "Beacon of Light",
                                ret => (WoWUnit)ret,
                                ret => ret is WoWPlayer && Group.Tanks.Contains((WoWPlayer)ret) && Group.Tanks.All(t => !t.HasMyAura("Beacon of Light"))),

                           Spell.Cast(
                                "Lay on Hands",
                                ret => (WoWUnit)ret,
                                ret => StyxWoW.Me.Combat && !((WoWUnit)ret).HasAura("Forbearance") &&
                                       ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Paladin().LayOnHandsHealth),
                            Spell.Cast(
                                "Light of Dawn",
                                ret => StyxWoW.Me,
                                ret => StyxWoW.Me.CurrentHolyPower >= 3 &&
                                       Unit.NearbyFriendlyPlayers.Count(p =>
                                           p.HealthPercent <= SingularSettings.Instance.Paladin().LightOfDawnHealth && p != StyxWoW.Me &&
                                           p.DistanceSqr < 30 * 30 && StyxWoW.Me.IsSafelyFacing(p.Location)) >= SingularSettings.Instance.Paladin().LightOfDawnCount),

                            Common.CreateWordOfGloryBehavior( on => (WoWUnit) on ),

                            Spell.Cast(
                                "Holy Shock",
                                ret => (WoWUnit)ret,
                                ret => ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Paladin().HolyShockHealth),
                            Spell.Cast(
                                "Flash of Light",
                                ret => (WoWUnit)ret,
                                ret => ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Paladin().FlashOfLightHealth),
                            Spell.Cast(
                                "Divine Light",
                                ret => (WoWUnit)ret,
                                ret => ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Paladin().DivineLightHealth),
                            Spell.Cast(
                                "Holy Light",
                                ret => (WoWUnit)ret,
                                ret => ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Paladin().HolyLightHealth),
                            new Decorator(
                                ret => StyxWoW.Me.Combat && StyxWoW.Me.GotTarget && Unit.NearbyFriendlyPlayers.Count(u => u.IsInMyPartyOrRaid) == 0,
                                new PrioritySelector(
                                    Helpers.Common.EnsureReadyToAttackFromMelee(),
                                    Helpers.Common.CreateInterruptBehavior(),
                                    Spell.Buff("Judgment"),
                                    Spell.Cast("Hammer of Wrath"),
                                    Spell.Cast("Holy Shock"),
                                    Spell.Cast("Crusader Strike"),
                                    Spell.Cast("Denounce"),
                                    Movement.CreateMoveToMeleeBehavior(true)
                                    )
                                ),
                            new Decorator(
                                ret => moveInRange,
                // Get in range
                                Movement.CreateMoveToUnitBehavior( on => _healTarget, 35f, 30f)
                                )
                            )
                        )
                    );
        }

        private static WoWUnit _rebirthTarget;

        public static Composite CreateRebirthBehavior()
        {
            return new Decorator(
                ret => Me.HasAura("Symbiosis"),
                Helpers.Common.CreateCombatRezBehavior("Rebirth", filter => true, requirements => true)
                );
        }
    }
}
