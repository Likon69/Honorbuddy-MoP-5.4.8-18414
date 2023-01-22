#define FIND_LOWEST_AT_THE_MOMENT

using System;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.CommonBot;
using Styx.Helpers;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Styx.WoWInternals.World;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using Styx.WoWInternals;
using CommonBehaviors.Actions;
using System.Collections.Generic;
using System.Drawing;

namespace Singular.ClassSpecific.Priest
{
    public class Disc
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static PriestSettings PriestSettings { get { return SingularSettings.Instance.Priest(); } }
        public static bool HasTalent(PriestTalents tal) { return TalentManager.IsSelected((int)tal); }

        const int PriEmergencyBase = 500;
        const int PriHighBase = 400;
        const int PriHighAtone = 300;
        const int PriAoeBase = 200;
        const int PriSingleBase = 100;
        const int PriLowBase = 0;

        #region Spirit Shell Support

        const int SPIRIT_SHELL_SPELL = 109964;
        private static bool IsSpiritShellEnabled() { return Me.HasAura(SPIRIT_SHELL_SPELL); }
        const int SPIRIT_SHELL_ABSORB = 114908;
        private static bool HasSpiritShellAbsorb(WoWUnit u) { return u.HasAura(SPIRIT_SHELL_ABSORB); }

        private static bool SkipForSpiritShell(WoWUnit u)
        {
            if (IsSpiritShellEnabled())
                return HasSpiritShellAbsorb(u);
            return false;
        }

        private static bool CanWePwsUnit(WoWUnit unit)
        {
            return unit != null && (!unit.HasAura("Weakened Soul") || Me.HasAura("Divine Insight"));
        }

        #endregion 

        [Behavior(BehaviorType.Rest, WoWClass.Priest, WoWSpec.PriestDiscipline)]
        public static Composite CreateDiscRest()
        {
            return new PrioritySelector(
                new Decorator(
                    req => SingularRoutine.CurrentWoWContext != WoWContext.Normal,
                    CreateDiscDiagnosticOutputBehavior("REST")
                    ),

                // Heal self before resting. There is no need to eat while we have 100% mana
                CreateDiscHealOnlyBehavior(true, false),
                // Rest up damnit! Do this first, so we make sure we're fully rested.
                Rest.CreateDefaultRestBehaviour(SingularRoutine.CurrentWoWContext == WoWContext.Normal ? "Flash Heal" : null, "Resurrection"),
                // Make sure we're healing OOC too!
                CreateDiscHealOnlyBehavior(false, false),
                // now buff our movement if possible
                Common.CreatePriestMovementBuffOnTank("Rest")
                );
        }

        public static Composite CreateDiscHealOnlyBehavior(bool selfOnly, bool moveInRange)
        {
            return CreateRestoPriestHealingOnlyBehavior(selfOnly, moveInRange);
        }

        private static WoWUnit _moveToHealTarget = null;
        private static WoWUnit _lastMoveToTarget = null;

        // temporary lol name ... will revise after testing
        public static Composite CreateRestoPriestHealingOnlyBehavior(bool selfOnly, bool moveInRange)
        {
            if (SingularRoutine.CurrentWoWContext == WoWContext.Normal)
                return new ActionAlwaysFail();

            HealerManager.NeedHealTargeting = true;
            PrioritizedBehaviorList behavs = new PrioritizedBehaviorList();
            int cancelHeal = (int)Math.Max(SingularSettings.Instance.IgnoreHealTargetsAboveHealth, Math.Max(PriestSettings.DiscHeal.Heal, Math.Max(PriestSettings.DiscHeal.GreaterHeal, PriestSettings.DiscHeal.Renew)));

            Logger.WriteDebugInBehaviorCreate("Priest Healing: will cancel cast of direct heal if health reaches {0:F1}%", cancelHeal);

            if (SingularSettings.Instance.DispelDebuffs != RelativePriority.None)
            {
                int dispelPriority = (SingularSettings.Instance.DispelDebuffs == RelativePriority.HighPriority) ? 999 : -999;
                behavs.AddBehavior(dispelPriority, "Dispel", null, Common.CreatePriestDispelBehavior());
            }

            #region Save the Group

            if (PriestSettings.DiscHeal.VoidShift != 0)
                behavs.AddBehavior(HealthToPriority(PriestSettings.DiscHeal.VoidShift) + PriEmergencyBase, "Void Shift @ " + PriestSettings.DiscHeal.VoidShift + "%", "Void Shift",
                    Spell.Cast("Void Shift",
                        mov => false,
                        on => (WoWUnit)on,
                        req => ((WoWUnit)req).IsPlayer && ((WoWUnit)req).HealthPercent < PriestSettings.DiscHeal.VoidShift && Me.HealthPercent > ((WoWUnit)req).HealthPercent
                        )
                    );

            behavs.AddBehavior(HealthToPriority(PriestSettings.DiscHeal.PainSuppression) + PriEmergencyBase, "Pain Suppression @ " + PriestSettings.DiscHeal.PainSuppression + "%", "Pain Suppression",
                Spell.Cast("Pain Suppression",
                    mov => false,
                    on => (WoWUnit)on,
                    req => ((WoWUnit)req).IsPlayer && ((WoWUnit)req).HealthPercent < PriestSettings.DiscHeal.PainSuppression
                    )
                );

            behavs.AddBehavior(HealthToPriority(99) + PriEmergencyBase, "Desperate Prayer @ " + PriestSettings.DesperatePrayerHealth + "%", "Desperate Prayer",
                Spell.Cast("Desperate Prayer", ret => Me, ret => Me.Combat && Me.HealthPercent < PriestSettings.DesperatePrayerHealth)
                );

            behavs.AddBehavior(HealthToPriority(PriestSettings.DiscHeal.PowerWordBarrier) + PriEmergencyBase, "Power Word: Barrier @ " + PriestSettings.DiscHeal.PowerWordBarrier + "%", "Power Word: Barrier",
                new Decorator(
                    ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                    new PrioritySelector(
                        context => GetBestPowerWordBarrierTarget(),
                        new Decorator(
                            ret => ret != null,
                            new PrioritySelector(
                                new Sequence(
                                    new Action(r => Logger.WriteDebug("PW:B - attempting cast")),
                                    Spell.CastOnGround("Power Word: Barrier", on => (WoWUnit)on, req => true, false)
                                    ),
                                new Action( ret => {
                                    if (ret != null)
                                    {
                                        if (!((WoWUnit)ret).IsValid)
                                            Logger.WriteDebug("PW:B - FAILED - Healing Target object became invalid");
                                        else if (((WoWUnit)ret).Distance > 40)
                                            Logger.WriteDebug("PW:B - FAILED - Healing Target moved out of range");
                                        else if (!Spell.CanCastHack("Power Word: Barrier"))
                                            Logger.WriteDebug("PW:B - FAILED - Spell.CanCastHack() said NO to Power Word: Barrier");
                                        else if (Styx.WoWInternals.World.GameWorld.IsInLineOfSpellSight(StyxWoW.Me.GetTraceLinePos(), ((WoWUnit)ret).Location))
                                            Logger.WriteDebug("PW:B - FAILED - Spell.CanCastHack() unit location not in Line of Sight");
                                        else if (Spell.IsSpellOnCooldown("Power Word: Barrier"))
                                            Logger.WriteDebug("PW:B - FAILED - Power Word: Barrier is on cooldown");
                                        else
                                            Logger.WriteDebug("PW:B - Something FAILED with Power Word: Barrier cast sequence (target={0}, h={1:F1}%, d={2:F1} yds, spellmax={3:F1} yds, cooldown={4})",
                                                ((WoWUnit)ret).SafeName(), 
                                                ((WoWUnit)ret).HealthPercent, 
                                                ((WoWUnit)ret).Distance,
                                                Spell.ActualMaxRange("Power Word: Barrier", (WoWUnit)ret),
                                                Spell.IsSpellOnCooldown("Power Word: Barrier")
                                                );
                                    }
                                    return RunStatus.Failure;
                                    })
                                )
                            )
                        )
                    )
                );


            #endregion

            #region Tank Buffing

            if (PriestSettings.DiscHeal.PrayerOfHealing != 0)
                behavs.AddBehavior(HealthToPriority(100) + PriHighBase, "Spirit Shell - Group MinCount: " + PriestSettings.DiscHeal.CountPrayerOfHealing, "Prayer of Healing",
                    new Decorator(
                        ret => IsSpiritShellEnabled(),
                        Spell.Cast("Prayer of Healing", on => {
                            WoWUnit unit = HealerManager.GetBestCoverageTarget("Prayer of Healing", 101, 40, 30, PriestSettings.DiscHeal.CountPrayerOfHealing, req => !HasSpiritShellAbsorb((WoWUnit)req));
                            if (unit != null && Spell.CanCastHack("Prayer of Healing", unit, skipWowCheck: true))
                            {
                                Logger.WriteDebug("Buffing Spirit Shell with Prayer of Healing on Group: {0}", unit.SafeName());
                                return unit;
                            }
                            return null;
                            })
                        )
                    );

            behavs.AddBehavior(HealthToPriority(99) + PriHighBase, "Spirit Shell - Tank", "Spirit Shell",
                new Decorator(
                    req => IsSpiritShellEnabled(),
                    Spell.Cast("Greater Heal", on =>
                    {
                        WoWUnit unit = Group.Tanks.Where(t => t.IsAlive && t.Combat && !HasSpiritShellAbsorb(t) && t.SpellDistance() < 40).OrderBy( a => a.Distance).FirstOrDefault();
                        if (unit != null && Spell.CanCastHack("Greater Heal", unit, skipWowCheck: true))
                        {
                            Logger.WriteDebug("Buffing Spirit Shell with Greater Heal on TANK: {0}", unit.SafeName());
                            return unit;
                        }
                        return null;
                    })
                    )
                );

            behavs.AddBehavior(HealthToPriority(98) + PriHighBase, "Power Word: Shield - Tank", "Power Word: Shield",
                Spell.Cast("Power Word: Shield", on =>
                {
                    WoWUnit unit = GetDiscBestTankTargetForPWS();
                    if (unit != null && Spell.CanCastHack("Power Word: Shield", unit, skipWowCheck: true))
                    {
                        Logger.WriteDebug("Buffing Power Word: Shield ON TANK: {0}", unit.SafeName());
                        return unit;
                    }
                    return null;
                })
                );

            if (PriestSettings.DiscHeal.Renew != 0)
            {
                behavs.AddBehavior(HealthToPriority(97) + PriHighBase, "Renew @" + PriestSettings.DiscHeal.Renew + "% while moving", "Renew",
                    Spell.Cast("Renew", on =>
                    {
                        WoWUnit unit = Group.Tanks.Where(u => u.IsAlive && u.HealthPercent < PriestSettings.DiscHeal.Renew && u.DistanceSqr < 40 * 40 && !u.HasAura("Renew") && u.InLineOfSpellSight).OrderBy(u => u.HealthPercent).FirstOrDefault();
                        if (unit != null && Spell.CanCastHack("Renew", unit, skipWowCheck: true))
                        {
                            Logger.WriteDebug("Buffing Renew ON TANK: {0}", unit.SafeName());
                            return unit;
                        }
                        return null;
                    },
                    req => Me.IsMoving && Spell.IsSpellOnCooldown("Power Word: Shield"))
                    );
            }

            #endregion

            #region Atonement Only

            // only Atonement healing if above Health %
            if (AddAtonementBehavior() )
            {
                behavs.AddBehavior(HealthToPriority(PriestSettings.DiscHeal.AtonementAbovePercent) + PriHighAtone, "Atonement Above " + PriestSettings.DiscHeal.AtonementAbovePercent + "%", "Atonement",
                    new Decorator(
                        req => (Me.Combat || SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds) && HealerManager.Instance.TargetList.Count(h => h.HealthPercent < PriestSettings.DiscHeal.AtonementAbovePercent) < PriestSettings.DiscHeal.AtonementAboveCount,
                        new PrioritySelector(
                            HealerManager.CreateAttackEnsureTarget(),

                             CreateDiscAtonementMovement(),

                            new Decorator(
                                req => Unit.ValidUnit(Me.CurrentTarget),
                                new PrioritySelector(
                                    new Action(r => { Logger.WriteDebug("--- atonement only! ---"); return RunStatus.Failure; }),
                                    Movement.CreateFaceTargetBehavior(),
                                    // Spell.BuffSelf("Archangel", req => Me.HasAura("Evangelism", 5)),
                                    Common.CreateHolyFireBehavior(),
                                    Spell.Cast("Penance", mov => true, on => Me.CurrentTarget, req => true, cancel => false),
                                    Spell.Cast("Smite", mov => true, on => Me.CurrentTarget, req => true, cancel => false)
                                    )
                                )
                            )
                        )
                    );                   
            }
            #endregion

            #region AoE Heals

            int maxDirectHeal = Math.Max(PriestSettings.DiscHeal.FlashHeal, Math.Max(PriestSettings.DiscHeal.Heal, PriestSettings.DiscHeal.GreaterHeal));


            if (PriestSettings.DiscHeal.SpiritShell != 0)
                behavs.AddBehavior(201 + PriAoeBase, "Spirit Shell Activate @ " + PriestSettings.DiscHeal.SpiritShell + "% MinCount: " + PriestSettings.DiscHeal.CountSpiritShell, "Spirit Shell",
                    new Decorator(
                        ret => Me.Combat 
                            && Spell.CanCastHack("Spirit Shell", Me, true)
                            && HealerManager.Instance.TargetList.Count(h => h.HealthPercent < PriestSettings.DiscHeal.SpiritShell) >= PriestSettings.DiscHeal.CountSpiritShell,
                        new Sequence(
                            Spell.BuffSelf("Spirit Shell", req => true),
                            new PrioritySelector(
                                new Wait( TimeSpan.FromMilliseconds(500), until => IsSpiritShellEnabled(), new Action( r => Logger.WriteDebug("buff for Spirit Shell now visible"))),
                                new Action( r => Logger.WriteDebug("cast successfull but Spirit Shell buff not visible"))
                                )
                            )
                        )
                    );


            if (PriestSettings.DiscHeal.PrayerOfMending != 0)
            {
                if (!TalentManager.HasGlyph("Focused Mending"))
                {
                    behavs.AddBehavior(200 + PriAoeBase, "Prayer of Mending @ " + PriestSettings.DiscHeal.PrayerOfMending + "% MinCount: " + PriestSettings.DiscHeal.CountPrayerOfMending, "Prayer of Mending",
                        new Decorator(
                            ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                            new PrioritySelector(
                                context => HealerManager.GetBestCoverageTarget("Prayer of Mending", PriestSettings.DiscHeal.PrayerOfMending, 40, 20, PriestSettings.DiscHeal.CountPrayerOfMending),
                                new Decorator(
                                    ret => ret != null && Spell.CanCastHack("Prayer of Mending", (WoWUnit)ret),
                                    new PrioritySelector(
                                        Spell.OffGCD(Spell.BuffSelf("Archangel", req => Me.HasAura("Evangelism", 5))),
                                        Spell.Cast("Prayer of Mending", on => (WoWUnit)on, req => true)
                                        )
                                    )
                                )
                            )
                        );
                }
                else
                {
                    behavs.AddBehavior(200 + PriAoeBase, "Prayer of Mending @ " + PriestSettings.DiscHeal.PrayerOfMending + "% (Glyph of Focused Mending)", "Prayer of Mending",
                        Spell.Cast("Prayer of Mending",
                            mov => true,
                            on => (WoWUnit)on,
                            req => !((WoWUnit)req).IsMe && ((WoWUnit)req).HealthPercent < PriestSettings.DiscHeal.PrayerOfMending && Me.HealthPercent < PriestSettings.DiscHeal.PrayerOfMending,
                            cancel => ((WoWUnit)cancel).HealthPercent > cancelHeal
                            )
                        );
                }
            }

            if (PriestSettings.DiscHeal.DiscLevel90Talent != 0)
                behavs.AddBehavior(HealthToPriority(PriestSettings.DiscHeal.DiscLevel90Talent) + PriAoeBase, "Halo @ " + PriestSettings.DiscHeal.DiscLevel90Talent + "% MinCount: " + PriestSettings.DiscHeal.CountLevel90Talent, "Halo",
                    new Decorator(
                        ret => SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds || SingularRoutine.CurrentWoWContext == WoWContext.Instances,
                        new Decorator(
                            ret => ret != null && HealerManager.Instance.TargetList.Count(u => u.IsAlive && u.HealthPercent < PriestSettings.DiscHeal.DiscLevel90Talent && u.Distance < 30) >= PriestSettings.DiscHeal.CountLevel90Talent
                                && Spell.CanCastHack("Halo", (WoWUnit)ret),
                            new PrioritySelector(
                                Spell.OffGCD(Spell.BuffSelf("Archangel", req => ((WoWUnit)req) != null && Me.HasAura("Evangelism", 5))),
                                Spell.CastOnGround("Halo", on => (WoWUnit)on, req => true)
                                )
                            )
                        )
                    );

            if (PriestSettings.DiscHeal.DiscLevel90Talent != 0)
                behavs.AddBehavior(HealthToPriority(PriestSettings.DiscHeal.DiscLevel90Talent) + PriAoeBase, "Cascade @ " + PriestSettings.DiscHeal.DiscLevel90Talent + "% MinCount: " + PriestSettings.DiscHeal.CountLevel90Talent, "Cascade",
                    new Decorator(
                        ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                        new PrioritySelector(
                            context => HealerManager.GetBestCoverageTarget("Cascade", PriestSettings.DiscHeal.DiscLevel90Talent, 40, 30, PriestSettings.DiscHeal.CountLevel90Talent),
                            new Decorator(
                                ret => ret != null && Spell.CanCastHack("Cascade", (WoWUnit) ret),
                                new PrioritySelector(
                                    Spell.OffGCD(Spell.BuffSelf("Archangel", req => Me.HasAura("Evangelism", 5))),
                                    Spell.Cast("Cascade", on => (WoWUnit)on, req => true)
                                    )
                                )
                            )
                        )
                    );

            if (PriestSettings.DiscHeal.BindingHeal != 0)
            {
                if (!TalentManager.HasGlyph("Binding Heal"))
                {
                    behavs.AddBehavior(HealthToPriority(PriestSettings.DiscHeal.BindingHeal) + PriAoeBase, "Binding Heal @ " + PriestSettings.DiscHeal.BindingHeal + "% MinCount: 2", "Binding Heal",
                        Spell.Cast("Binding Heal",
                            mov => true,
                            on => (WoWUnit)on,
                            req => !((WoWUnit)req).IsMe && ((WoWUnit)req).HealthPercent < PriestSettings.DiscHeal.BindingHeal && Me.HealthPercent < PriestSettings.DiscHeal.BindingHeal,
                            cancel => ((WoWUnit)cancel).HealthPercent > cancelHeal
                            )
                        );
                }
                else
                {
                    behavs.AddBehavior(HealthToPriority(PriestSettings.DiscHeal.BindingHeal) + PriAoeBase, "Binding Heal (glyphed) @ " + PriestSettings.DiscHeal.BindingHeal + "% MinCount: 3", "Binding Heal",
                        Spell.Cast("Binding Heal",
                            mov => true,
                            on => (WoWUnit)on,
                            req => !((WoWUnit)req).IsMe
                                && ((WoWUnit)req).HealthPercent < PriestSettings.DiscHeal.BindingHeal
                                && Me.HealthPercent < PriestSettings.DiscHeal.BindingHeal
                                && HealerManager.Instance.TargetList.Any(h => h.IsAlive && !h.IsMe && h.Guid != ((WoWUnit)req).Guid && h.HealthPercent < PriestSettings.DiscHeal.BindingHeal && h.SpellDistance() < 20),
                            cancel => ((WoWUnit)cancel).HealthPercent > cancelHeal
                            )
                        );
                }
            }
           
            if (PriestSettings.DiscHeal.PrayerOfHealing != 0)
                behavs.AddBehavior(HealthToPriority(PriestSettings.DiscHeal.PrayerOfHealing) + PriAoeBase, "Prayer of Healing @ " + PriestSettings.DiscHeal.PrayerOfHealing + "% MinCount: " + PriestSettings.DiscHeal.CountPrayerOfHealing, "Prayer of Healing",
                    new Decorator(
                        ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                        new PrioritySelector(
                            context => HealerManager.GetBestCoverageTarget("Prayer of Healing", PriestSettings.DiscHeal.PrayerOfHealing, 40, 30, PriestSettings.DiscHeal.CountPrayerOfHealing),
                            CastBuffsBehavior("Prayer of Healing"),
                            Spell.Cast("Prayer of Healing", on => (WoWUnit)on)
                            )
                        )
                    );

            #endregion

            #region Direct Heals

            if (PriestSettings.DiscHeal.PowerWordShield != 0)
            {
                behavs.AddBehavior(99 + PriSingleBase, "Power Word: Shield @ " + PriestSettings.DiscHeal.PowerWordShield + "%", "Power Word: Shield",
                    Spell.Buff("Power Word: Shield", on => (WoWUnit) on, req => ((WoWUnit)req).HealthPercent < PriestSettings.DiscHeal.PowerWordShield && CanWePwsUnit((WoWUnit) req))
                    );
            }

            if (PriestSettings.DiscHeal.Penance != 0)
                behavs.AddBehavior(HealthToPriority(PriestSettings.DiscHeal.Penance) + PriSingleBase, "Penance @ " + PriestSettings.DiscHeal.Penance + "%", "Penance",
                new Decorator(
                    req => ((WoWUnit)req).HealthPercent < PriestSettings.DiscHeal.Penance,
                    new PrioritySelector(
                        CastBuffsBehavior("Penance"),
                        Spell.Cast("Penance",
                            mov => true,
                            on => (WoWUnit)on,
                            req => true,
                            cancel => false
                            )
                        )
                    )
                );

            string cmt = "";
            int flashHealHealth = PriestSettings.DiscHeal.FlashHeal;
            if (!SpellManager.HasSpell("Greater Heal"))
            {
                flashHealHealth = Math.Max(flashHealHealth, PriestSettings.DiscHeal.GreaterHeal);
                cmt = "(Adjusted for Greater Heal)";
            }

            if (!SpellManager.HasSpell("Heal"))
            {
                flashHealHealth = Math.Max(flashHealHealth, PriestSettings.DiscHeal.Heal);
                cmt = "(Adjusted for Heal)";
            }

            if (flashHealHealth != 0)
                behavs.AddBehavior(HealthToPriority(PriestSettings.DiscHeal.FlashHeal) + PriSingleBase, "Flash Heal @ " + flashHealHealth + "% " + cmt, "Flash Heal",
                new Decorator(
                    req => ((WoWUnit)req).HealthPercent < flashHealHealth && !SkipForSpiritShell((WoWUnit)req),
                    new PrioritySelector(
                        CastBuffsBehavior("Flash Heal"),
                        Spell.Cast("Flash Heal",
                            mov => true,
                            on => (WoWUnit)on,
                            req => true,
                            cancel => ((WoWUnit)cancel).HealthPercent > cancelHeal
                            )
                        )
                    )
                );

            if (PriestSettings.DiscHeal.GreaterHeal != 0)
                behavs.AddBehavior(HealthToPriority(PriestSettings.DiscHeal.GreaterHeal) + PriSingleBase, "Greater Heal @ " + PriestSettings.DiscHeal.GreaterHeal + "%", "Greater Heal",
                new Decorator(
                    req => ((WoWUnit)req).HealthPercent < PriestSettings.DiscHeal.GreaterHeal && !SkipForSpiritShell((WoWUnit)req),
                    new PrioritySelector(
                        CastBuffsBehavior("Greater Heal"),
                        Spell.Cast("Greater Heal",
                            mov => true,
                            on => (WoWUnit)on,
                            req => true,
                            cancel => ((WoWUnit)cancel).HealthPercent > cancelHeal
                            )
                        )
                    )
                );

            if (PriestSettings.DiscHeal.Heal != 0)
                behavs.AddBehavior(HealthToPriority(PriestSettings.DiscHeal.Heal) + PriSingleBase, "Heal @ " + PriestSettings.DiscHeal.Heal + "%", "Heal",
                Spell.Cast("Heal",
                    mov => true,
                    on => (WoWUnit)on,
                    req => ((WoWUnit)req).HealthPercent < PriestSettings.DiscHeal.Heal && !SkipForSpiritShell((WoWUnit)req),
                    cancel => ((WoWUnit)cancel).HealthPercent > cancelHeal
                    )
                );

            #endregion

            #region Tank - Low Priority Buffs

            #endregion


            #region Lowest Priority Healer Tasks

            // Atonement
            if (AddAtonementBehavior() && (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds || SingularRoutine.CurrentWoWContext == WoWContext.Instances))
            {
                // check less than # below Atonement Health
                behavs.AddBehavior(1, "Atonement when Idle = " + PriestSettings.DiscHeal.AtonementWhenIdle.ToString(), "Atonement",
                    new Decorator(
                        req => PriestSettings.DiscHeal.AtonementWhenIdle && (Me.Combat || SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds),
                        new PrioritySelector(
                            HealerManager.CreateAttackEnsureTarget(),
                            CreateDiscAtonementMovement(),
                            new Decorator( 
                                req => Unit.ValidUnit( Me.CurrentTarget),
                                new PrioritySelector(
                                    new Action(r => { Logger.WriteDebug("--- atonement when idle ---"); return RunStatus.Failure; }),
                                    Movement.CreateFaceTargetBehavior(),
                                    // Spell.BuffSelf("Archangel", req => Me.HasAura("Evangelism", 5)),
                                    Spell.Cast("Penance", mov => true, on => Me.CurrentTarget, req => true, cancel => false),
                                    Common.CreateHolyFireBehavior(),
                                    Spell.Cast("Smite", mov => true, on => Me.CurrentTarget, req => true, cancel => false)
                                    )
                                )
                            )
                        )
                    );
            }

            #endregion

            behavs.OrderBehaviors();

            if (selfOnly == false && Singular.Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Combat)
                behavs.ListBehaviors();

            return new PrioritySelector(
                ctx => selfOnly ? StyxWoW.Me : HealerManager.FindLowestHealthTarget(), // HealerManager.Instance.FirstUnit,

                // use gcd/cast time to choose dps target and face if needed
                new Decorator(
                    req => Me.Combat && (Spell.IsGlobalCooldown() || Spell.IsCastingOrChannelling()),
                    new PrioritySelector(
                        HealerManager.CreateAttackEnsureTarget(),
                        Movement.CreateFaceTargetBehavior(waitForFacing: false)
                        )
                    ),

                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown() && ret != null,
                    behavs.GenerateBehaviorTree()
                    )

#if OLD_TANK_FOLLOW_CODE
                ,
                new Decorator(
                    ret => moveInRange,
                    Movement.CreateMoveToUnitBehavior(
                        ret => Battlegrounds.IsInsideBattleground ? (WoWUnit)ret : Group.Tanks.Where(a => a.IsAlive).OrderBy(a => a.Distance).FirstOrDefault(),
                        35f
                        )
                    )
#endif
                );
        }

        private static bool AddAtonementBehavior()
        {
            return Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Heal
                || Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.PullBuffs
                || Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Pull
                || Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.CombatBuffs
                || Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Combat;
        }

        private static int HealthToPriority(int nHealth)
        {
            return nHealth == 0 ? 0 : 200 - nHealth;
        }


        [Behavior(BehaviorType.Heal, WoWClass.Priest, WoWSpec.PriestDiscipline)]
        public static Composite CreateDiscHeal()
        {
            return new Decorator(
                ret => !Unit.NearbyGroupMembers.Any(m => m.IsAlive && !m.IsMe),
                new PrioritySelector(
                    Spell.Cast("Desperate Prayer", ret => Me, ret => Me.Combat && Me.HealthPercent < PriestSettings.DesperatePrayerHealth),
                    Spell.BuffSelf("Power Word: Shield", ret => Me.Combat && Me.HealthPercent < PriestSettings.ShieldHealthPercent && CanWePwsUnit((WoWUnit) ret)),

                    // keep heal buffs on if glyphed
                    Spell.BuffSelf("Prayer of Mending", ret => Me.Combat && Me.HealthPercent <= 90),
                    Spell.BuffSelf("Renew", ret => Me.Combat && Me.HealthPercent <= 90),

                    Spell.Cast("Psychic Scream", ret => Me.Combat
                        && PriestSettings.UsePsychicScream
                        && Me.HealthPercent <= PriestSettings.ShadowFlashHealHealth
                        && (Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 10 * 10) >= PriestSettings.PsychicScreamAddCount || (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds && Unit.NearbyUnfriendlyUnits.Any(u => Me.SpellDistance(u) < 8)))),

                    Spell.Cast("Flash Heal",
                        ctx => Me,
                        ret => Me.HealthPercent <= PriestSettings.ShadowFlashHealHealth && !SkipForSpiritShell(Me)),

                    Spell.Cast("Flash Heal",
                        ctx => Me,
                        ret => !Me.Combat && Me.PredictedHealthPercent(includeMyHeals: true) <= 85 && !SkipForSpiritShell(Me))
                    )
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Priest, WoWSpec.PriestDiscipline)]
        public static Composite CreateDiscCombatBuffs()
        {
            return new PrioritySelector(
                new Decorator(
                    req => !Unit.IsTrivial(Me.CurrentTarget),
                    new PrioritySelector(
                        Common.CreateFadeBehavior(),

                        Spell.BuffSelf("Desperate Prayer", ret => StyxWoW.Me.HealthPercent <= PriestSettings.DesperatePrayerHealth),

                        Common.CreateShadowfiendBehavior(),

                        Common.CreateLeapOfFaithBehavior(),

                        Spell.Cast(
                            "Hymn of Hope",
                            on => Me,
                            ret => StyxWoW.Me.ManaPercent <= PriestSettings.HymnofHopeMana && Spell.GetSpellCooldown("Shadowfiend").TotalMilliseconds > 0,
                            cancel => false),

                        // Spell.Cast("Power Word: Solace", req => Me.GotTarget && Unit.ValidUnit(Me.CurrentTarget) && Me.IsSafelyFacing( Me.CurrentTarget) && Me.CurrentTarget.InLineOfSpellSight )
                // Spell.Cast(129250, req => Me.GotTarget && Unit.ValidUnit(Me.CurrentTarget) && Me.IsSafelyFacing(Me.CurrentTarget) && Me.CurrentTarget.InLineOfSpellSight),
                        Spell.CastHack("Power Word: Solace", req => Me.GotTarget && Unit.ValidUnit(Me.CurrentTarget) && Me.IsSafelyFacing(Me.CurrentTarget) && Me.CurrentTarget.InLineOfSpellSight)
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Pull, WoWClass.Priest, WoWSpec.PriestDiscipline, WoWContext.Normal)]
        public static Composite CreateDiscCombatNormalPull()
        {
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromMediumRange(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        CreateDiscDiagnosticOutputBehavior(Dynamics.CompositeBuilder.CurrentBehaviorType.ToString()),

                        Spell.BuffSelf("Power Word: Shield", ret => PriestSettings.UseShieldPrePull && !Me.HasAura("Weakened Soul")),
                        Helpers.Common.CreateInterruptBehavior(),
                        Dispelling.CreatePurgeEnemyBehavior("Dispel Magic"),
                        Spell.Cast("Shadow Word: Death", on => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.HealthPercent < 20 && Me.IsSafelyFacing(u))),
                        Spell.Cast("Smite", mov => true, on => Me.CurrentTarget, req => !Unit.NearbyUnfriendlyUnits.Any(u => u.Aggro), cancel => false),
                        Spell.Buff("Shadow Word: Pain", req => Me.CurrentTarget.HasAuraExpired("Shadow Word: Pain", 1) && Me.CurrentTarget.TimeToDeath(99) >= 8),
                        Spell.Buff("Shadow Word: Pain", true, on =>
                        {
                            WoWUnit unit = Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.Guid != Me.CurrentTargetGuid && u.IsTargetingMeOrPet && !u.HasMyAura("Shadow Word: Pain") && !u.IsCrowdControlled());
                            return unit;
                        }),
                        Spell.Cast("Penance", mov => true, on => Me.CurrentTarget, req => true, cancel => false),
                        Common.CreateHolyFireBehavior(),
                        Spell.Cast("Smite", mov => true, on => Me.CurrentTarget, req => true, cancel => false)
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Priest, WoWSpec.PriestDiscipline, WoWContext.Normal)]
        public static Composite CreateDiscCombatNormalCombat()
        {
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromMediumRange(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        CreateDiscDiagnosticOutputBehavior(Dynamics.CompositeBuilder.CurrentBehaviorType.ToString()),

                        Helpers.Common.CreateInterruptBehavior(),
                        Dispelling.CreatePurgeEnemyBehavior("Dispel Magic"),
                        Spell.Cast("Shadow Word: Death", on => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.HealthPercent < 20 && Me.IsSafelyFacing(u))),
                        Spell.Buff("Shadow Word: Pain", req => Me.CurrentTarget.HasAuraExpired("Shadow Word: Pain", 1) && Me.CurrentTarget.TimeToDeath(99) >= 8),
                        Spell.Buff("Shadow Word: Pain", true, on =>
                        {
                            WoWUnit unit = Unit.NearbyUnfriendlyUnits
                                .FirstOrDefault(
                                    u => (u.TaggedByMe || u.Aggro) 
                                        && u.Guid != Me.CurrentTargetGuid 
                                        && u.IsTargetingMeOrPet 
                                        && !u.HasMyAura("Shadow Word: Pain") 
                                        && !u.IsCrowdControlled()
                                    );
                            return unit;
                        }),
                        Spell.Cast("Penance", mov => true, on => Me.CurrentTarget, req => true, cancel => false),
                        Common.CreateHolyFireBehavior(),
                        Spell.Cast("Smite", mov => true, on => Me.CurrentTarget, req => true, cancel => false)
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Combat | BehaviorType.Pull, WoWClass.Priest, WoWSpec.PriestDiscipline, WoWContext.Battlegrounds )]
        public static Composite CreateDiscCombatPvp()
        {
            return new PrioritySelector(

                CreateDiscDiagnosticOutputBehavior(Dynamics.CompositeBuilder.CurrentBehaviorType.ToString()),

                HealerManager.CreateStayNearTankBehavior(),

                new Decorator(
                    ret => Unit.NearbyGroupMembers.Any(m => m.IsAlive && !m.IsMe),
                    CreateDiscHealOnlyBehavior(false, true)
                    ),

                Helpers.Common.EnsureReadyToAttackFromMediumRange(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown() && HealerManager.AllowHealerDPS(),
                    new PrioritySelector(
                        Helpers.Common.CreateInterruptBehavior(),
                        Dispelling.CreatePurgeEnemyBehavior("Dispel Magic"),
                        Spell.Cast("Shadow Word: Death", on => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.HealthPercent < 20 && Me.IsSafelyFacing(u))),
                        Spell.Cast("Shadow Word: Pain", req => Me.CurrentTarget.IsPlayer && Me.CurrentTarget.HasAuraExpired("Shadow Word: Pain", 1)),
                        Spell.Cast("Penance", mov => true, on => Me.CurrentTarget, req => true, cancel => HealerManager.CancelHealerDPS()),
                        Common.CreateHolyFireBehavior(),
                        Spell.Cast("Smite", mov => true, on => Me.CurrentTarget, req => true, cancel => HealerManager.CancelHealerDPS())
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Combat | BehaviorType.Pull, WoWClass.Priest, WoWSpec.PriestDiscipline, WoWContext.Instances)]
        public static Composite CreateDiscCombatInstances()
        {
            return new PrioritySelector(

                CreateDiscDiagnosticOutputBehavior(Dynamics.CompositeBuilder.CurrentBehaviorType.ToString()),

                HealerManager.CreateStayNearTankBehavior(
                    new Decorator(
                        unit => unit != null 
                            && ((unit as WoWUnit).SpellDistance() > SingularSettings.Instance.StayNearTankRangeCombat + 20 
                                || ((unit as WoWUnit).IsMoving && (unit as WoWUnit).MeIsSafelyBehind && (unit as WoWUnit).SpellDistance() > SingularSettings.Instance.StayNearTankRangeCombat + 10))
                            && Me.IsSafelyFacing(unit as WoWUnit, 60f),
                        Common.CreatePriestMovementBuff()
                        )
                    ),

                new Decorator(
                    ret => Unit.NearbyGroupMembers.Any(m => m.IsAlive && !m.IsMe),
                    CreateDiscHealOnlyBehavior(false, true)
                    ),

                new Decorator(
                    ret => Me.Combat && HealerManager.AllowHealerDPS(),
                    new PrioritySelector(

                        new Decorator(
                            req => !SingularSettings.Instance.StayNearTank,
                            Helpers.Common.EnsureReadyToAttackFromMediumRange()
                            ),

                        Movement.CreateFaceTargetBehavior(),
                        Spell.WaitForCastOrChannel(),

                        new Decorator(
                            ret => !Spell.IsGlobalCooldown(),
                            new PrioritySelector(
                                Helpers.Common.CreateInterruptBehavior(),
                                Dispelling.CreatePurgeEnemyBehavior("Dispel Magic"),
                                Spell.Cast("Shadow Word: Death", on => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.HealthPercent < 20 && Me.IsSafelyFacing(u) && u.InLineOfSpellSight)),
                                Spell.Buff("Shadow Word: Pain", req => Me.CurrentTarget.HasAuraExpired("Shadow Word: Pain", 1) && Me.CurrentTarget.TimeToDeath(99) >= 8),
                                Spell.Buff("Shadow Word: Pain", true, on =>
                                {
                                    WoWUnit unit = Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.Guid != Me.CurrentTargetGuid && u.IsTargetingMeOrPet && !u.HasMyAura("Shadow Word: Pain") && !u.IsCrowdControlled());
                                    return unit;
                                }),
                                Spell.Cast("Penance", mov => true, on => Me.CurrentTarget, req => true, cancel => HealerManager.CancelHealerDPS()),
                                Common.CreateHolyFireBehavior(),
                                Spell.Cast("Smite", mov => true, on => Me.CurrentTarget, req => true, cancel => HealerManager.CancelHealerDPS())
                                )
                            )
                        )
                    )
                );
        }

        private static WoWUnit GetBestPowerWordBarrierTarget()
        {
#if ORIGINAL
            return Clusters.GetBestUnitForCluster(Unit.NearbyFriendlyPlayers.Cast<WoWUnit>(), ClusterType.Radius, 10f);
#else
            if (!Me.IsInGroup() || !Me.Combat)
                return null;

            if (!Spell.CanCastHack("Power Word: Barrier", Me, skipWowCheck: true))
            {
                // Logger.WriteDebug("GetBestHealingRainTarget: CanCastHack says NO to Healing Rain");
                return null;
            }

            // build temp list of targets that could use shield and are in range + radius
            List<WoWUnit> coveredTargets = HealerManager.Instance.TargetList
                .Where(u => u.IsAlive && u.DistanceSqr < 47 * 47 && u.HealthPercent < PriestSettings.DiscHeal.PowerWordBarrier)
                .ToList();

            // search all targets to find best one in best location to use as anchor for cast on ground
            var t = Unit.NearbyGroupMembersAndPets
                .Select(p => new
                {
                    Player = p,
                    Count = coveredTargets
                        .Where(pp => pp.IsAlive && pp.Location.DistanceSqr(p.Location) < 7 * 7)
                        .Count()
                })
                .OrderByDescending(v => v.Count)
                .DefaultIfEmpty(null)
                .FirstOrDefault();

            if (t != null && t.Count >= PriestSettings.DiscHeal.CountPowerWordBarrier)
            {
                Logger.WriteDebug("PowerWordBarrier Target:  found {0} with {1} nearby under {2}%", t.Player.SafeName(), t.Count, PriestSettings.DiscHeal.PowerWordBarrier);
                return t.Player;
            }

            return null;
#endif
        }

        public static WoWUnit GetDiscBestTankTargetForPWS(float health = 100f)
        {
            WoWUnit hotTarget = null;
            string hotName = "Power Word: Shield";
            string hotDebuff = "Weakened Soul";
            bool haveDivineInsight = Me.HasAura("Divine Insight");

            if (haveDivineInsight)
                hotTarget = Group.Tanks.Where(u => u.IsAlive && u.Combat && u.HealthPercent < health && u.DistanceSqr < 40 * 40 && !u.HasAura(hotName) && u.InLineOfSpellSight).OrderBy(u => u.HealthPercent).FirstOrDefault();
            else
                hotTarget = Group.Tanks.Where(u => u.IsAlive && u.Combat && u.HealthPercent < health && u.DistanceSqr < 40 * 40 && !u.HasAura(hotName) && !u.HasAura(hotDebuff) && u.InLineOfSpellSight).OrderBy(u => u.HealthPercent).FirstOrDefault();

            if (hotTarget != null)
                Logger.WriteDebug("GetBestTankTargetForPWS('{0}'): found tank {1} @ {2:F1}%, hasmyaura={3} with {4} ms lefton {5}{6}", hotName, hotTarget.SafeName(), hotTarget.HealthPercent, hotTarget.HasMyAura(hotName), (int)hotTarget.GetAuraTimeLeft(hotDebuff).TotalMilliseconds, hotDebuff, haveDivineInsight ? " Divine Insight active" : "");

            return hotTarget;
        }

        private static Composite CastBuffsBehavior(string castFor)
        {
            return new PrioritySelector(
                new Sequence(
                    Spell.BuffSelf("Inner Focus", req => Me.Combat),
                    new ActionAlwaysFail()
                    ),
                new Sequence(
                    Spell.BuffSelf("Power Infusion", req => Me.Combat && IsSpiritShellEnabled()),
                    new ActionAlwaysFail()
                    )
                );
        }

        private static Composite CreateDiscAtonementMovement()
        {
            // return Helpers.Common.EnsureReadyToAttackFromMediumRange();
            
            if (SingularSettings.Instance.StayNearTank)
                return Movement.CreateFaceTargetBehavior();

            return CreateDiscAtonementMovement();
        }

        #region Diagnostics

        private static DateTime _LastDiag = DateTime.MinValue;

        private static Composite CreateDiscDiagnosticOutputBehavior(string context)
        {
            return new Sequence(
                new Decorator(
                    ret => SingularSettings.Debug,
                    new ThrottlePasses(1, 1,
                        new Action(ret =>
                        {
                            WoWAura chakra = Me.GetAllAuras().Where(a => a.Name.Contains("Chakra")).FirstOrDefault();

                            string line = string.Format(".... [{0}] h={1:F1}%/m={2:F1}%, combat={3}, evang={4}, archa={5}, spiritsh={6}, borrtim={7}, inner={8}",
                                context,
                                Me.HealthPercent,
                                Me.ManaPercent,
                                Me.Combat.ToYN(),
                                (int) Me.GetAuraStacks("Evangelism"),
                                (long) Me.GetAuraTimeLeft("Archangel").TotalMilliseconds,
                                (long) Me.GetAuraTimeLeft(SPIRIT_SHELL_SPELL).TotalMilliseconds,
                                (long) Me.GetAuraTimeLeft("Borrowed Time").TotalMilliseconds,
                                Me.HasAura("Inner Fire") ? "Fire" : (Me.HasAura("Inner Will") ? "Will" : "none")
                               );

                            if (HealerManager.Instance == null || HealerManager.Instance.FirstUnit == null || !HealerManager.Instance.FirstUnit.IsValid)
                                line += ", target=(null)";
                            else
                            {
                                WoWUnit healtarget = HealerManager.Instance.FirstUnit;
                                line += string.Format(", target={0} th={1:F1}%/{2:F1}%,  @ {3:F1} yds, combat={4}, tloss={5}, pw:s={6}, renew={7}, spiritsh={8}",
                                    healtarget.SafeName(),
                                    healtarget.HealthPercent,
                                    healtarget.PredictedHealthPercent(includeMyHeals: true),
                                    healtarget.Distance,
                                    healtarget.Combat.ToYN(),
                                    healtarget.InLineOfSpellSight,
                                    (long)healtarget.GetAuraTimeLeft("Power Word: Shield").TotalMilliseconds,
                                    (long)healtarget.GetAuraTimeLeft("Renew").TotalMilliseconds,
                                    (long)healtarget.GetAuraTimeLeft(SPIRIT_SHELL_ABSORB).TotalMilliseconds
                                    );
                            }

                            Logger.WriteDebug(Color.LightGreen, line);
                            return RunStatus.Failure;
                        }))
                    )
                );
        }

        #endregion
    }
}
