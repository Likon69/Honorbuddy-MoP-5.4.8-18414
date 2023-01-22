using System;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.WoWInternals;
using Styx.CommonBot;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;

using Rest = Singular.Helpers.Rest;
using Action = Styx.TreeSharp.Action;
using CommonBehaviors.Actions;
using Styx.Common.Helpers;
using System.Collections.Generic;
using System.Drawing;

namespace Singular.ClassSpecific.Druid
{
    public class Resto
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static DruidSettings DruidSettings { get { return SingularSettings.Instance.Druid(); } }
        public static bool HasTalent(DruidTalents tal) { return TalentManager.IsSelected((int)tal); }

        const int PriEmergencyBase = 500;
        const int PriHighBase = 400;
        const int PriAoeBase = 300;
        const int PriHighAtone = 200;
        const int PriSingleBase = 100;
        const int PriLowBase = 0;

        const int MUSHROOM_ID = 47649;

        static IEnumerable<WoWUnit> Mushrooms
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>().Where(o => o.Entry == MUSHROOM_ID && o.CreatedByUnitGuid == StyxWoW.Me.Guid && o.Distance <= 40 ); 
            }
        }

        static int MushroomCount
        {
            get { return Mushrooms.Count(); }
        }


        [Behavior(BehaviorType.Rest, WoWClass.Druid, WoWSpec.DruidRestoration)]
        public static Composite CreateRestoDruidRest()
        {
            return new PrioritySelector(
                CreateRestoNonCombatHeal(true),
                Rest.CreateDefaultRestBehaviour(),
                Spell.Resurrect("Revive"),
                CreateRestoNonCombatHeal(false)
                );
        }

        public static Composite CreateRestoDruidHealOnlyBehavior()
        {
            return CreateRestoDruidHealOnlyBehavior(false, true);
        }

        public static Composite CreateRestoDruidHealOnlyBehavior(bool selfOnly)
        {
            return CreateRestoDruidHealOnlyBehavior(selfOnly, false);
        }

        public static Composite CreateRestoDruidHealOnlyBehavior(bool selfOnly, bool moveInRange)
        {
            HealerManager.NeedHealTargeting = true;

            return CreateHealingOnlyBehavior(selfOnly, moveInRange);

#if OLD_STUFF_SAVED_BUT_NOT_USED
            const uint mapleSeedId = 17034;

            return new
                PrioritySelector(
                ctx => selfOnly ? StyxWoW.Me : HealerManager.Instance.FirstUnit,
                    new Decorator(
                        ret => ret != null && (moveInRange || ((WoWUnit)ret).InLineOfSpellSight && ((WoWUnit)ret).DistanceSqr < 40 * 40),
                        new PrioritySelector(
                        Spell.WaitForCastOrChannel(),
                        new Decorator(
                            ret => moveInRange,
                            Movement.CreateMoveToLosBehavior(ret => (WoWUnit)ret)),
                        // Ensure we're in range of the unit to heal, and it's in LOS.
                        //CreateMoveToAndFace(35f, ret => (WoWUnit)ret),
                        //Cast Lifebloom on tank if
                        //1- Tank doesn't have lifebloom
                        //2- Tank has less then 3 stacks of lifebloom
                        //3- Tank has 3 stacks of lifebloom but it will expire in 3 seconds
                        Spell.Cast(
                            "Lifebloom",
                            ret => (WoWUnit)ret,
                            ret =>
                            StyxWoW.Me.Combat &&
                                // Keep 3 stacks up on the tank/leader at all times.
                                // If we're in ToL form, we can do rolling LBs for everyone. So ignore the fact that its the leader or not.
                                // LB is cheap, and VERY powerful in ToL form since you can spam it on the entire raid, for a cheap HoT and quite good 'bloom'
                            ((RaFHelper.Leader != null && (WoWUnit)ret == RaFHelper.Leader) || StyxWoW.Me.Shapeshift == ShapeshiftForm.TreeOfLife) &&
                            ((WoWUnit)ret).HealthPercent > 60 &&
                            (!((WoWUnit)ret).HasAura("Lifebloom") || ((WoWUnit)ret).Auras["Lifebloom"].StackCount < 3 ||
                             ((WoWUnit)ret).Auras["Lifebloom"].TimeLeft <= TimeSpan.FromSeconds(3))),
                        //Cast rebirth if the tank is dead. Check for Unburdened Rebirth glyph or Maple seed reagent
                        Spell.Cast(
                            "Rebirth",
                            ret => (WoWUnit)ret,
                            ret => SingularSettings.Instance.CombatRezTarget != CombatRezTarget.None && StyxWoW.Me.Combat && RaFHelper.Leader != null && (WoWUnit)ret == RaFHelper.Leader &&
                                   ((WoWUnit)ret).IsDead && (TalentManager.HasGlyph("Unburdened Rebirth") || StyxWoW.Me.BagItems.Any(i => i.Entry == mapleSeedId))),
                        Spell.Cast(
                            "Tranquility",
                            mov => true,
                            on => Me,
                            ret => StyxWoW.Me.Combat && StyxWoW.Me.GroupInfo.IsInParty && Unit.NearbyFriendlyPlayers.Count(
                                p =>
                                p.IsAlive && p.HealthPercent <= DruidSettings.TranquilityHealth && p.Distance <= 30) >=
                                   DruidSettings.TranquilityCount,
                            cancel => false
                            ),
                        //Use Innervate on party members if we have Glyph of Innervate
                        Spell.Buff(
                            "Innervate",
                            ret => (WoWUnit)ret,
                            ret =>
                            TalentManager.HasGlyph("Innervate") && StyxWoW.Me.Combat && (WoWUnit)ret != StyxWoW.Me &&
                            StyxWoW.Me.ManaPercent <= DruidSettings.InnervateMana &&
                            ((WoWUnit)ret).PowerType == WoWPowerType.Mana && ((WoWUnit)ret).ManaPercent <= DruidSettings.InnervateMana),
                        Spell.Cast(
                            "Swiftmend",
                            ret => (WoWUnit)ret,
                            ret => StyxWoW.Me.Combat && ((WoWUnit)ret).HealthPercent <= DruidSettings.Swiftmend &&
                                   (((WoWUnit)ret).HasAura("Rejuvenation") || ((WoWUnit)ret).HasAura("Regrowth"))),
                        Spell.Cast(
                            "Wild Growth",
                            ret => (WoWUnit)ret,
                            ret => StyxWoW.Me.GroupInfo.IsInParty && Unit.NearbyFriendlyPlayers.Count(
                                p => p.IsAlive && p.HealthPercent <= DruidSettings.WildGrowthHealth &&
                                     p.Location.DistanceSqr(((WoWUnit)ret).Location) <= 30*30) >= DruidSettings.WildGrowthCount),
                        Spell.Cast(
                            "Regrowth",
                            ret => (WoWUnit)ret,
                            ret => !((WoWUnit)ret).HasMyAura("Regrowth") && ((WoWUnit)ret).HealthPercent <= DruidSettings.Regrowth),
                        Spell.Cast(
                            "Healing Touch",
                            ret => (WoWUnit)ret,
                            ret => ((WoWUnit)ret).HealthPercent <= DruidSettings.HealingTouch),
                        Spell.Cast(
                            "Nourish",
                            ret => (WoWUnit)ret,
                            ret => ((WoWUnit)ret).HealthPercent <= DruidSettings.Nourish &&
                                   ((((WoWUnit)ret).HasAura("Rejuvenation") || ((WoWUnit)ret).HasAura("Regrowth") ||
                                    ((WoWUnit)ret).HasAura("Lifebloom") || ((WoWUnit)ret).HasAura("Wild Growth")))),
                        Spell.Cast(
                            "Rejuvenation",
                            ret => (WoWUnit)ret,
                            ret => !((WoWUnit)ret).HasMyAura("Rejuvenation") &&
                                   ((WoWUnit)ret).HealthPercent <= DruidSettings.Rejuvenation),
                        new Decorator(
                            ret => StyxWoW.Me.Combat && StyxWoW.Me.GotTarget && Unit.NearbyFriendlyPlayers.Count(u => u.IsInMyPartyOrRaid) == 0,
                            new PrioritySelector(
                                Helpers.Common.EnsureReadyToAttackFromLongRange(),
                                Helpers.Common.CreateInterruptBehavior(),
                                Spell.Buff("Moonfire"),
                                Spell.Cast("Starfire", ret => StyxWoW.Me.HasAura("Fury of Stormrage")),
                                Spell.Cast("Wrath"),
                                Movement.CreateMoveToUnitBehavior(35f, on=> Me.CurrentTarget )
                                )),
                        new Decorator(
                            ret => moveInRange,
                            Movement.CreateMoveToUnitBehavior(35f, ret => (WoWUnit)ret))
                        )));
#endif
        }

        private static WoWUnit _moveToHealTarget = null;
        private static WoWUnit _lastMoveToTarget = null;

        private static int HealthToPriority(int nHealth)
        {
            return nHealth == 0 ? 0 : 200 - nHealth;
        }

        
        // temporary lol name ... will revise after testing
        public static Composite CreateHealingOnlyBehavior(bool selfOnly, bool moveInRange)
        {
            if (SingularRoutine.CurrentWoWContext == WoWContext.Normal)
                return new ActionAlwaysFail();

            HealerManager.NeedHealTargeting = true;
            PrioritizedBehaviorList behavs = new PrioritizedBehaviorList();
            int cancelHeal = (int)Math.Max(SingularSettings.Instance.IgnoreHealTargetsAboveHealth, Math.Max(DruidSettings.Heal.Rejuvenation, Math.Max(DruidSettings.Heal.HealingTouch, Math.Max(DruidSettings.Heal.Nourish, DruidSettings.Heal.Regrowth))));

            Logger.WriteDebugInBehaviorCreate("Druid Healing: will cancel cast of direct heal if health reaches {0:F1}%", cancelHeal);

            #region Cleanse

            if (SingularSettings.Instance.DispelDebuffs != RelativePriority.None)
            {
                int dispelPriority = (SingularSettings.Instance.DispelDebuffs == RelativePriority.HighPriority) ? 999 : -999;
                behavs.AddBehavior(dispelPriority, "Nature's Cure", "Nature's Cure", Dispelling.CreateDispelBehavior());
            }

            #endregion

            #region Save the Group

            // Tank: Rebirth
            if (SingularSettings.Instance.CombatRezTarget != CombatRezTarget.None )
            {
                behavs.AddBehavior(799, "Rebirth Tank/Healer", "Rebirth",
                    Helpers.Common.CreateCombatRezBehavior( "Rebirth", filter => true, requirements => true)
                    );
            }

            if (DruidSettings.Heal.HeartOfTheWild != 0)
            {
                behavs.AddBehavior(795, "Heart of the Wild @ " + DruidSettings.Heal.HeartOfTheWild + "% MinCount: " + DruidSettings.Heal.CountHeartOfTheWild, "Heart of the Wild",
                    new Decorator(
                        ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                        Spell.BuffSelf(
                            "Heart of the Wild",
                            req => ((WoWUnit)req).HealthPercent < DruidSettings.Heal.HeartOfTheWild
                                && DruidSettings.Heal.CountHeartOfTheWild <= HealerManager.Instance.TargetList
                                    .Count(p => p.IsAlive && p.HealthPercent <= DruidSettings.Heal.HeartOfTheWild && p.Location.DistanceSqr(((WoWUnit)req).Location) <= 30 * 30)
                            )
                        )
                    );
            }

            if (DruidSettings.Heal.NaturesSwiftness != 0)
            {
                behavs.AddBehavior(797, "Nature's Swiftness Heal @ " + DruidSettings.Heal.NaturesSwiftness + "%", "Nature's Swiftness",
                    new Decorator(
                        req => ((WoWUnit)req).HealthPercent < DruidSettings.Heal.NaturesSwiftness
                            && !Spell.IsSpellOnCooldown("Nature's Swiftness")
                            && Spell.CanCastHack("Rejuvenation", (WoWUnit)req, skipWowCheck: true),
                        new Sequence(
                            Spell.BuffSelf("Nature's Swiftness"),
                            new PrioritySelector(
                                Spell.Cast("Regrowth", on => (WoWUnit)on, req => true, cancel => false),
                                Spell.Cast("Healing Touch", on => (WoWUnit)on, req => true, cancel => false),
                                Spell.Cast("Nourish", on => (WoWUnit)on, req => true, cancel => false)
                                )
                            )
                        )
                    );
            }

            if (DruidSettings.Heal.Tranquility != 0)
                behavs.AddBehavior(798, "Tranquility @ " + DruidSettings.Heal.Tranquility + "% MinCount: " + DruidSettings.Heal.CountTranquility, "Tranquility",
                    new Decorator(
                        ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                        Spell.Cast(
                            "Tranquility", 
                            mov => true,
                            on => (WoWUnit)on,
                            req => HealerManager.Instance.TargetList.Count( h => h.IsAlive && h.HealthPercent < DruidSettings.Heal.Tranquility && h.SpellDistance() < 40) >= DruidSettings.Heal.CountTranquility,
                            cancel => false
                            )
                        )
                    );

            if (DruidSettings.Heal.SwiftmendDirectHeal != 0)
            {
                behavs.AddBehavior(797, "Swiftmend Direct @ " + DruidSettings.Heal.SwiftmendDirectHeal + "%", "Swiftmend",
                    new Decorator(
                        ret => (!Spell.IsSpellOnCooldown("Swiftmend") || Spell.GetCharges("Force of Nature") > 0)
                            && ((WoWUnit)ret).PredictedHealthPercent(includeMyHeals: true) < DruidSettings.Heal.SwiftmendDirectHeal
                            && (StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid)
                            && Spell.CanCastHack("Rejuvenation", (WoWUnit)ret, skipWowCheck: true),
                        new Sequence(
                            new DecoratorContinue(
                                req => !((WoWUnit)req).HasAnyAura("Rejuvenation", "Regrowth"),
                                new PrioritySelector(
                                    Spell.Buff("Rejuvenation", on => (WoWUnit)on),
                                    Spell.Cast("Regrowth", on => (WoWUnit)on, req => !TalentManager.HasGlyph("Regrowth"), cancel => false)
                                    )
                                ),
                            new Wait(TimeSpan.FromMilliseconds(500), until => ((WoWUnit)until).HasAnyAura("Rejuvenation","Regrowth"), new ActionAlwaysSucceed()),
                            new Wait(TimeSpan.FromMilliseconds(1500), until => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(), new ActionAlwaysSucceed()),
                            new PrioritySelector(
                                Spell.Cast("Force of Nature", on => (WoWUnit)on, req => Spell.GetCharges("Force of Nature") > 1),
                                Spell.Cast("Swiftmend", on => (WoWUnit)on)
                                )
                            )
                        )
                    );
            }

            if (DruidSettings.Heal.Genesis != 0)
                behavs.AddBehavior(798, "Genesis @ " + DruidSettings.Heal.Genesis + "% MinCount: " + DruidSettings.Heal.CountGenesis, "Genesis",
                    new Decorator(
                        ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                        Spell.Cast(
                            "Genesis",
                            mov => true,
                            on => HealerManager.Instance.TargetList.FirstOrDefault( h => h.IsAlive && h.HasAura("Rejuvenation")),
                            req => HealerManager.Instance.TargetList.Count(h => h.IsAlive && h.HealthPercent < DruidSettings.Heal.Genesis && h.SpellDistance() < 40) >= DruidSettings.Heal.CountGenesis,
                            cancel => false
                            )
                        )
                    );

            #endregion

            #region Tank Buffing

            // Tank: Lifebloom
            behavs.AddBehavior(99 + PriHighBase, "Lifebloom - Tank", "Lifebloom",
                Spell.Cast("Lifebloom", on =>
                {
                    WoWUnit unit = GetLifebloomTarget();
                    if (unit != null && (unit.Combat || Me.Combat) 
                        && (unit.GetAuraStacks("Lifebloom") < 3 || unit.GetAuraTimeLeft("Lifebloom").TotalMilliseconds < 2800) 
                        && Spell.CanCastHack("Lifebloom", unit, skipWowCheck: true))
                    {
                        Logger.WriteDebug("Buffing Lifebloom ON TANK: {0}", unit.SafeName());
                        return unit;
                    }
                    return null;
                })
                );

            // Tank: Rejuv if Lifebloom not trained yet
            if (DruidSettings.Heal.Rejuvenation != 0 && !SpellManager.HasSpell("Lifebloom"))
            {
                behavs.AddBehavior(98 + PriHighBase, "Rejuvenation - Tank", "Rejuvenation",
                    Spell.Cast("Rejuvenation", on =>
                    {
                        WoWUnit unit = GetBestTankTargetFor("Rejuvenation");
                        if (unit != null && Spell.CanCastHack("Rejuvenation", unit, skipWowCheck: true))
                        {
                            Logger.WriteDebug("Buffing Rejuvenation ON TANK: {0}", unit.SafeName());
                            return unit;
                        }
                        return null;
                    })
                    );
            }

            if (DruidSettings.Heal.Ironbark != 0)
            {
                if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                    behavs.AddBehavior(HealthToPriority(DruidSettings.Heal.Ironbark) + PriHighBase, "Ironbark @ " + DruidSettings.Heal.Ironbark + "%", "Ironbark",
                        Spell.Buff("Ironbark", on => (WoWUnit)on, req => ((WoWUnit)req).HealthPercent < DruidSettings.Heal.Ironbark)
                        );
                else
                    behavs.AddBehavior(HealthToPriority(DruidSettings.Heal.Ironbark) + PriHighBase, "Ironbark - Tank @ " + DruidSettings.Heal.Ironbark + "%", "Ironbark",
                        Spell.Buff("Ironbark", on => Group.Tanks.FirstOrDefault(u => u.IsAlive && u.HealthPercent < DruidSettings.Heal.CenarionWard && !u.HasAura("Ironbark")))
                        );
            }

            if (DruidSettings.Heal.CenarionWard != 0)
            {
                if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                    behavs.AddBehavior(HealthToPriority(DruidSettings.Heal.CenarionWard) + PriHighBase, "Cenarion Ward @ " + DruidSettings.Heal.CenarionWard + "%", "Cenarion Ward",
                        Spell.Buff("Cenarion Ward", on => (WoWUnit)on, req => ((WoWUnit)req).HealthPercent < DruidSettings.Heal.CenarionWard)
                        );
                else
                    behavs.AddBehavior(HealthToPriority(DruidSettings.Heal.CenarionWard) + PriHighBase, "Cenarion Ward - Tanks @ " + DruidSettings.Heal.CenarionWard + "%", "Cenarion Ward",
                        Spell.Buff("Cenarion Ward", on => Group.Tanks.FirstOrDefault( u => u.IsAlive && u.HealthPercent < DruidSettings.Heal.CenarionWard && !u.HasAura("Cenarion Ward")))
                        );
            }

            if (DruidSettings.Heal.NaturesVigil != 0)
            {
                if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                    behavs.AddBehavior(HealthToPriority(DruidSettings.Heal.NaturesVigil) + PriHighBase, "Nature's Vigil @ " + DruidSettings.Heal.NaturesVigil + "%", "Nature's Vigil",
                        Spell.Buff("Nature's Vigil", on => (WoWUnit)on, req => ((WoWUnit)req).HealthPercent < DruidSettings.Heal.NaturesVigil)
                        );
                else
                    behavs.AddBehavior(HealthToPriority(DruidSettings.Heal.NaturesVigil) + PriHighBase, "Nature's Vigil - Tank @ " + DruidSettings.Heal.NaturesVigil + "%", "Nature's Vigil",
                        Spell.Buff("Nature's Vigil", on => Group.Tanks.FirstOrDefault(u => u.IsAlive && u.HealthPercent < DruidSettings.Heal.NaturesVigil && !u.HasAura("Nature's Vigil")))
                        );
            }

            if (DruidSettings.Heal.TreeOfLife != 0)
            {
                behavs.AddBehavior(HealthToPriority(DruidSettings.Heal.TreeOfLife) + PriHighBase, "Incarnation: Tree of Life @ " + DruidSettings.Heal.TreeOfLife + "% MinCount: " + DruidSettings.Heal.CountTreeOfLife, "Incarnation: Tree of Life",
                    Spell.BuffSelf("Incarnation: Tree of Life", 
                        req => ((WoWUnit)req).HealthPercent < DruidSettings.Heal.TreeOfLife
                            && DruidSettings.Heal.CountTreeOfLife <= HealerManager.Instance.TargetList.Count(h => h.IsAlive && h.HealthPercent < DruidSettings.Heal.TreeOfLife))
                    );
            }

            #endregion

            #region Atonement Only
            // only Atonement healing if above Health %
            if (AddAtonementBehavior() && DruidSettings.Heal.DreamOfCenariusAbovePercent > 0)
            {
                behavs.AddBehavior( 100 + PriHighAtone, "DreamOfCenarius Above " + DruidSettings.Heal.DreamOfCenariusAbovePercent + "%", "Wrath",
                    new Decorator(
                        req => (Me.Combat || SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds) && HealerManager.Instance.TargetList.Count(h => h.HealthPercent < DruidSettings.Heal.DreamOfCenariusAbovePercent) < DruidSettings.Heal.DreamOfCenariusAboveCount,
                        new PrioritySelector(
                            HealerManager.CreateAttackEnsureTarget(),
                            Helpers.Common.EnsureReadyToAttackFromLongRange(),
                            new Decorator(
                                req => Unit.ValidUnit(Me.CurrentTarget),
                                new PrioritySelector(
                                    new Action(r => { Logger.WriteDebug("--- DreamOfCenarius only! ---"); return RunStatus.Failure; }),
                                    Movement.CreateFaceTargetBehavior(),
                                    Spell.Cast("Wrath", mov => true, on => Me.CurrentTarget, req => true, cancel => false)
                                    )
                                )
                            )
                        )
                    );
            }
            #endregion

            #region AoE Heals

            int maxDirectHeal = Math.Max(DruidSettings.Heal.Nourish, Math.Max(DruidSettings.Heal.HealingTouch, DruidSettings.Heal.Regrowth));

            if (DruidSettings.Heal.WildGrowth != 0)
                behavs.AddBehavior(HealthToPriority(DruidSettings.Heal.WildGrowth) + PriAoeBase, "Wild Growth @ " + DruidSettings.Heal.WildGrowth + "% MinCount: " + DruidSettings.Heal.CountWildGrowth, "Wild Growth",
                    new Decorator(
                        ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                        new PrioritySelector(
                    // ctx => HealerManager.GetBestCoverageTarget("Wild Growth", Settings.Heal.WildGrowth, 40, 30, Settings.Heal.CountWildGrowth),
                            Spell.Cast(
                                "Wild Growth",
                                on => (WoWUnit)on,
                                req => ((WoWUnit)req).HealthPercent < DruidSettings.Heal.WildGrowth
                                    && DruidSettings.Heal.CountWildGrowth <= HealerManager.Instance.TargetList
                                        .Count(p => p.IsAlive && p.HealthPercent <= DruidSettings.Heal.WildGrowth && p.Location.DistanceSqr(((WoWUnit)req).Location) <= 30 * 30))
                            )
                        )
                    );

            if (DruidSettings.Heal.WildMushroomBloom != 0)
                behavs.AddBehavior(HealthToPriority(DruidSettings.Heal.WildMushroomBloom) + PriAoeBase, "Wild Mushroom: Bloom @ " + DruidSettings.Heal.WildMushroomBloom + "% MinCount: " + DruidSettings.Heal.CountMushroomBloom, "Wild Mushroom: Bloom",
                    new Decorator(
                        ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                        CreateMushroomBloom()
                        ) 
                    );
/*
            if (Settings.Heal.SwiftmendAOE != 0)
                behavs.AddBehavior(HealthToPriority(Settings.Heal.SwiftmendAOE) + PriAoeBase, "Swiftmend @ " + Settings.Heal.SwiftmendAOE + "% MinCount: " + Settings.Heal.CountSwiftmendAOE, "Swiftmend",
                    new Decorator(
                        ret => StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid,
                        new PrioritySelector(
                            ctx => HealerManager.GetBestCoverageTarget("Swiftmend", Settings.Heal.SwiftmendAOE, 40, 10, Settings.Heal.CountSwiftmendAOE
                                , mainTarget: Unit.NearbyGroupMembersAndPets.Where(p => p.HealthPercent < Settings.Heal.SwiftmendAOE && p.SpellDistance() <= 40 && p.IsAlive && p.HasAnyOfMyAuras("Rejuvenation", "Regrowth"))),
                            Spell.Cast("Force of Nature", on => (WoWUnit)on, req => Spell.GetCharges("Force of Nature") > 1),
                            Spell.Cast(spell => "Swiftmend", mov => false, on => (WoWUnit)on, req => true, skipWowCheck: true)
                            )
                        )
                    );
*/
            #endregion

            #region Direct Heals

            // Regrowth above ToL: Lifebloom so we use Clearcasting procs 
            behavs.AddBehavior(200 + PriSingleBase, "Regrowth on Clearcasting", "Regrowth",
                new PrioritySelector(
                    Spell.Cast("Regrowth",
                        mov => !Me.HasAnyAura("Nature's Swiftness", "Incarnation: Tree of Life"),
                        on => {
                            WoWUnit target = (WoWUnit)on;
                            if (target.HealthPercent > 95)
                            {
                                WoWUnit lbTarget = GetLifebloomTarget();
                                if (lbTarget != null && lbTarget.GetAuraStacks("Lifebloom") >= 3 && lbTarget.GetAuraTimeLeft("Lifebloom").TotalMilliseconds.Between(500,10000))
                                {
                                    return lbTarget;
                                }
                            }
                            return target;
                        },
                        req => Me.GetAuraTimeLeft("Clearcasting").TotalMilliseconds > 1500,
                        cancel => false
                        )
                    )
                );

            // ToL: Lifebloom
            if (DruidSettings.Heal.TreeOfLife != 0 && Common.HasTalent(DruidTalents.Incarnation))
            {
                behavs.AddBehavior(199 + PriSingleBase, "Lifebloom - Tree of Life", "Lifebloom",
                    Spell.Cast("Lifebloom", 
                        mov => false,
                        on => HealerManager.Instance.TargetList.FirstOrDefault( h => (h.GetAuraStacks("Lifebloom") < 3 || h.GetAuraTimeLeft("Lifebloom").TotalMilliseconds < 2500) && Spell.CanCastHack("Lifebloom", h, skipWowCheck: true)),
                        req => Me.GetAuraTimeLeft("Incarnation") != TimeSpan.Zero,
                        cancel => false
                        )
                    );
            }

            behavs.AddBehavior(198 + PriSingleBase, "Rejuvenation @ " + DruidSettings.Heal.Rejuvenation + "%", "Rejuvenation",
                new PrioritySelector(
                    Spell.Buff("Rejuvenation",
                        true,
                        on => (WoWUnit)on,
                        req => ((WoWUnit)req).HealthPercent < DruidSettings.Heal.Rejuvenation,
                        1
                        )
                    )
                );

            if (DruidSettings.Heal.Nourish != 0)
            {
                // roll 3 Rejuvs if Glyph of Rejuvenation equipped
                if (TalentManager.HasGlyph("Rejuvenation"))
                {
                    // make priority 1 higher than Noursh (-1 here due to way HealthToPriority works)
                    behavs.AddBehavior(HealthToPriority(DruidSettings.Heal.Nourish-1) + PriSingleBase, "Roll 3 Rejuvenations for Glyph", "Rejuvenation",
                        new PrioritySelector(
                            Spell.Buff("Rejuvenation",
                                true,
                                on =>
                                {
                                    // iterate through so we can stop at either 3 with rejuv or first without
                                    int cntHasAura = 0;
                                    foreach (WoWUnit h in HealerManager.Instance.TargetList)
                                    {
                                        if (h.IsAlive)
                                        {
                                            if (!h.HasKnownAuraExpired("Rejuvenation", 1))
                                            {
                                                cntHasAura++;
                                                if (cntHasAura >= 3)
                                                    return null;
                                            }
                                            else
                                            {
                                                if (h.InLineOfSpellSight)
                                                {
                                                    return h;
                                                }
                                            }
                                        }
                                    }

                                    return null;
                                },
                                req => true,
                                1
                                )
                            )
                        );
                }

                behavs.AddBehavior(HealthToPriority(DruidSettings.Heal.Nourish) + PriSingleBase, "Nourish @ " + DruidSettings.Heal.Nourish + "%", "Nourish",
                new PrioritySelector(
                    Spell.Cast("Nourish",
                        mov => true,
                        on => (WoWUnit)on,
                        req => ((WoWUnit)req).HealthPercent < DruidSettings.Heal.Nourish && ((WoWUnit)req).HasAnyOfMyAuras("Rejuvenation", "Regrowth", "Lifebloom", "Wild Growth"),
                        cancel => ((WoWUnit)cancel).HealthPercent > cancelHeal
                        )
                    )
                );
            }

            if (DruidSettings.Heal.HealingTouch != 0)
            {
                int regrowthInstead = 0;
                string whyRegrowth = "";
                if (SpellManager.HasSpell("Regrowth"))
                {
                    if (TalentManager.HasGlyph("Regrowth"))
                    {
                        regrowthInstead = Math.Max(DruidSettings.Heal.HealingTouch, DruidSettings.Heal.HealingTouch);
                        whyRegrowth = "Glyphed Regrowth (instead of Healing Touch) @ ";
                    }
                    else if (TalentManager.HasGlyph("Regrowth"))
                    {
                        regrowthInstead = Math.Max(DruidSettings.Heal.HealingTouch, DruidSettings.Heal.HealingTouch);
                        whyRegrowth = "Regrowth (since Healing Touch unknown) @ ";
                    }                        
                }

                if (regrowthInstead != 0)
                {
                    behavs.AddBehavior(HealthToPriority(DruidSettings.Heal.HealingTouch) + PriSingleBase, whyRegrowth + regrowthInstead + "%", "Regrowth",
                        new PrioritySelector(
                            Spell.Cast("Regrowth",
                                mov => true,
                                on => (WoWUnit)on,
                                req => ((WoWUnit)req).HealthPercent < regrowthInstead,
                                cancel => ((WoWUnit)cancel).HealthPercent > cancelHeal
                                )
                            )
                        );
                }
                else
                {
                    behavs.AddBehavior(HealthToPriority(DruidSettings.Heal.HealingTouch) + PriSingleBase, "Healing Touch @ " + DruidSettings.Heal.HealingTouch + "%", "Healing Touch",
                        new PrioritySelector(
                            Spell.Cast("Healing Touch",
                                mov => true,
                                on => (WoWUnit)on,
                                req => ((WoWUnit)req).HealthPercent < DruidSettings.Heal.HealingTouch,
                                cancel => ((WoWUnit)cancel).HealthPercent > cancelHeal
                                )
                            )
                        );
                }
            }

            if (DruidSettings.Heal.Regrowth != 0)
                behavs.AddBehavior(HealthToPriority(DruidSettings.Heal.Regrowth) + PriSingleBase, "Regrowth @ " + DruidSettings.Heal.Regrowth + "%", "Regrowth",
                new PrioritySelector(
                    Spell.Cast("Regrowth",
                        mov => true,
                        on => (WoWUnit)on,
                        req => ((WoWUnit)req).HealthPercent < DruidSettings.Heal.Regrowth,
                        cancel => ((WoWUnit)cancel).HealthPercent > cancelHeal
                        )
                    )
                );

            #endregion

            #region Lowest Priority Healer Tasks

            behavs.AddBehavior(3, "Rejuvenation while Moving @ " + SingularSettings.Instance.IgnoreHealTargetsAboveHealth + "%", "Rejuvenation",
                new Decorator(
                    req => Me.IsMoving,
                    Spell.Cast("Rejuvenation",
                        mov => false,
                        on => HealerManager.Instance.TargetList.FirstOrDefault(h => h.IsAlive && h.HealthPercent < SingularSettings.Instance.IgnoreHealTargetsAboveHealth && !h.HasMyAura("Rejuvenation") && Spell.CanCastHack("Rejuvenation", h, true)),
                        req => true
                        )
                    )
                );

            if (DruidSettings.Heal.WildMushroomBloom != 0)
                behavs.AddBehavior(2, "Wild Mushroom: Set", "Wild Mushroom",
                    CreateMushroomSetBehavior()
                    );



            // Atonement
            if (AddAtonementBehavior() && (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds || SingularRoutine.CurrentWoWContext == WoWContext.Instances) && DruidSettings.Heal.DreamOfCenariusWhenIdle)
            {
                // check less than # below DreamOfCenarius Health
                behavs.AddBehavior(1, "DreamOfCenarius when Idle = " + DruidSettings.Heal.DreamOfCenariusWhenIdle.ToString(), "Wrath",
                    new Decorator(
                        req => (Me.Combat || SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds),
                        new PrioritySelector(
                            HealerManager.CreateAttackEnsureTarget(),
                            Helpers.Common.EnsureReadyToAttackFromLongRange(),
                            new Decorator(
                                req => Unit.ValidUnit(Me.CurrentTarget),
                                new PrioritySelector(
                                    new Action(r => { Logger.WriteDebug("--- DreamOfCenarius when idle ---"); return RunStatus.Failure; }),
                                    Movement.CreateFaceTargetBehavior(),
                                    Spell.Cast("Wrath", mov => true, on => Me.CurrentTarget, req => true, cancel => false)
                                    )
                                )
                            )
                        )
                    );
            }

            #endregion

            behavs.OrderBehaviors();

            if (selfOnly == false && Singular.Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Heal)
                behavs.ListBehaviors();

            return new PrioritySelector(
                ctx => selfOnly ? StyxWoW.Me : HealerManager.FindLowestHealthTarget(), // HealerManager.Instance.FirstUnit,

                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown() && ret != null,
                    behavs.GenerateBehaviorTree()
                    ),

                new Decorator(
                    ret => moveInRange,
                    Movement.CreateMoveToUnitBehavior(
                        ret => Battlegrounds.IsInsideBattleground ? (WoWUnit)ret : Group.Tanks.Where(a => a.IsAlive).OrderBy(a => a.Distance).FirstOrDefault(),
                        35f
                        )
                    )
                );
        }

        private static bool AddAtonementBehavior()
        {
            if (!HasTalent(DruidTalents.DreamOfCenarius))
                return false;

            return Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Heal
                || Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.PullBuffs
                || Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Pull
                || Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.CombatBuffs
                || Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Combat;
        }


        /// <summary>
        /// non-combat heal to top people off and avoid lifebloom, buffs, etc.
        /// </summary>
        /// <param name="selfOnly"></param>
        /// <returns></returns>
        public static Composite CreateRestoNonCombatHeal(bool selfOnly = false)
        {
            return new PrioritySelector(
                ctx => selfOnly || !Me.IsInGroup() ? StyxWoW.Me : HealerManager.FindLowestHealthTarget(), // HealerManager.Instance.FirstUnit,

                new Decorator( 
                    req => req != null && ((WoWUnit)req).Combat && ((WoWUnit)req).PredictedHealthPercent(includeMyHeals: true) < SingularSettings.Instance.IgnoreHealTargetsAboveHealth,
                    CreateRestoDruidHealOnlyBehavior()
                    ),

                new Decorator(
                    req => req != null && !((WoWUnit)req).Combat && ((WoWUnit)req).PredictedHealthPercent(includeMyHeals: true) < SingularSettings.Instance.IgnoreHealTargetsAboveHealth,

                    new Sequence(
                        new Action(on => Logger.WriteDebug("NonCombatHeal on {0}: health={1:F1}% predicted={2:F1}% +mine={3:F1}", ((WoWUnit)on).SafeName(), ((WoWUnit)on).HealthPercent, ((WoWUnit)on).PredictedHealthPercent(), ((WoWUnit)on).PredictedHealthPercent(includeMyHeals: true))),
                        new PrioritySelector(
                            // BUFFS First
                            Spell.Buff("Rejuvenation", true, on => (WoWUnit)on, req => ((WoWUnit)req).PredictedHealthPercent(includeMyHeals: true) < 95, 1),
                            Spell.Buff("Regrowth", true, on => (WoWUnit)on, req => ((WoWUnit)req).PredictedHealthPercent(includeMyHeals: true) < 80 && !TalentManager.HasGlyph("Regrowth"), 1),

                            // Direct Heals After
                            Spell.Cast("Healing Touch", on => (WoWUnit)on, req => ((WoWUnit)req).PredictedHealthPercent(includeMyHeals: true) < 65),
                            Spell.Cast("Regrowth", on => (WoWUnit)on, req => ((WoWUnit)req).PredictedHealthPercent(includeMyHeals: true) < 75),
                            Spell.Cast("Nourish", on => (WoWUnit)on, req => ((WoWUnit)req).PredictedHealthPercent(includeMyHeals: true) < 85),

                            // if Moving, spread Rejuv around on those that need to be topped off
                            new Decorator(
                                req => Me.IsMoving,
                                new PrioritySelector(
                                    ctx => HealerManager.Instance.TargetList.FirstOrDefault( h => h.HealthPercent < 95 && !h.HasMyAura("Rejuvenation") && Spell.CanCastHack("Rejuvenation", (WoWUnit) ctx, skipWowCheck: true)),
                                    Spell.Buff("Rejuvenation", on => (WoWUnit) on)
                                    )
                                )
                            )
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidRestoration)]
        public static Composite CreateRestoDruidHealBehavior()
        {
            return new PrioritySelector(
                CreateRestoDiagnosticOutputBehavior(),

                HealerManager.CreateStayNearTankBehavior(),

                new Decorator(
                    ret => HealerManager.Instance.TargetList.Any( h => h.Distance < 40 && h.IsAlive && !h.IsMe),
                    new PrioritySelector(
                        CreateRestoDruidHealOnlyBehavior()
                        )
                    )
                );
        }


        [Behavior(BehaviorType.Pull, WoWClass.Druid, WoWSpec.DruidRestoration)]
        public static Composite CreateRestoDruidPull()
        {
            return new PrioritySelector(
                HealerManager.CreateStayNearTankBehavior(),
                new Decorator(
                    req => !HealerManager.Instance.TargetList.Any(h => h.IsAlive && !h.IsMe && h.Distance < 40),
                    new PrioritySelector(
                        Helpers.Common.EnsureReadyToAttackFromLongRange(),
                        Spell.WaitForCastOrChannel(),
                        new Decorator(
                            req => !Spell.IsGlobalCooldown(),
                            new PrioritySelector(
                                Helpers.Common.CreateInterruptBehavior(),
                                Spell.Buff("Moonfire"),
                                Spell.Cast("Wrath"),
                                Movement.CreateMoveToUnitBehavior(on => Me.CurrentTarget, 35f, 30f)
                                )
                            )
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Druid, WoWSpec.DruidRestoration)]
        public static Composite CreateRestoDruidCombat()
        {
            return new PrioritySelector(
                HealerManager.CreateStayNearTankBehavior(),
                new Decorator(
                    req => HealerManager.AllowHealerDPS(),
                    new PrioritySelector(
                        Helpers.Common.EnsureReadyToAttackFromLongRange(),
                        Spell.WaitForCastOrChannel(),
                        new Decorator(
                            req => !Spell.IsGlobalCooldown(),
                            new PrioritySelector(
                                Helpers.Common.CreateInterruptBehavior(),
                                Spell.Buff("Moonfire"),
                                Spell.Cast("Wrath", on => Me.CurrentTarget, req => true, cancel => HealerManager.CancelHealerDPS()),
                                Movement.CreateMoveToUnitBehavior(on => Me.CurrentTarget, 35f, 30f)
                                )
                            )
                        )
                    )
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidRestoration)]
        public static Composite CreateRestoDruidCombatBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Innervate", ret => StyxWoW.Me.ManaPercent < 15 || StyxWoW.Me.ManaPercent <= DruidSettings.InnervateMana),
                Spell.BuffSelf("Barkskin", ret => StyxWoW.Me.HealthPercent <= DruidSettings.Barkskin || Unit.NearbyUnitsInCombatWithMe.Any()),

                // Symbiosis
                Common.SymbBuff(Symbiosis.IceboundFortitude, on => Me, ret => Me.HealthPercent < DruidSettings.Barkskin),
                Common.SymbBuff(Symbiosis.Deterrence, on => Me, ret => Me.HealthPercent < DruidSettings.Barkskin),
                Common.SymbBuff(Symbiosis.Evasion, on => Me, ret => Me.HealthPercent < DruidSettings.Barkskin),
                Common.SymbBuff(Symbiosis.FortifyingBrew, on => Me, ret => Me.HealthPercent < DruidSettings.Barkskin),
                Common.SymbBuff(Symbiosis.IntimidatingRoar, on => Me.CurrentTarget, ret => Me.CurrentTarget.SpellDistance() < 10 && Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 8 * 8) > 1),

                Common.SymbBuff(Symbiosis.SpiritwalkersGrace, on => Me, ret => Me.IsMoving && Me.Combat)
                );
        }


        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid, WoWSpec.DruidRestoration, WoWContext.Battlegrounds | WoWContext.Instances, 2)]
        public static Composite CreateRestoPreCombatBuffForSymbiosis(UnitSelectionDelegate onUnit)
        {
            return Common.CreateDruidCastSymbiosis(on => GetRestoBestSymbiosisTarget());
        }

        public static WoWUnit GetBestTankTargetFor(string hotName, int stacks = 1, float health = 100f)
        {
/*
            // fast test unless RaFHelper.Leader is whacked
            try
            {
                if (RaFHelper.Leader != null && RaFHelper.Leader.SpellDistance() < 40 && RaFHelper.Leader.IsAlive)
                {
                    if (SingularSettings.Debug)
                        Logger.WriteDebug("GetBestTankTargetFor('{0}'): found Leader {1} @ {2:F1}%, hasmyaura={3}", hotName, RaFHelper.Leader.SafeName(), RaFHelper.Leader.HealthPercent, RaFHelper.Leader.HasMyAura(hotName));
                    return RaFHelper.Leader;
                }
            }
            catch { }
*/
            WoWUnit hotTarget = null;
            hotTarget = Group.Tanks
                .Where(u => u.IsAlive && u.Combat && u.HealthPercent < health && u.SpellDistance() < 40
                    && (u.GetAuraStacks(hotName) < stacks || u.GetAuraTimeLeft(hotName).TotalSeconds < 3) && u.InLineOfSpellSight).OrderBy(u => u.HealthPercent)
                .FirstOrDefault();

            if (hotTarget != null && SingularSettings.Debug)
                Logger.WriteDebug("GetBestTankTargetFor('{0}'): found Tank {1} @ {2:F1}%, hasmyaura={3}", hotName, hotTarget.SafeName(), hotTarget.HealthPercent, hotTarget.HasMyAura(hotName));

            return hotTarget;
        }

        public static WoWUnit GetLifebloomTarget(float health = 100f)
        {
            string hotName = "Lifebloom";
            int stacks = 3;
/*
            // fast test unless RaFHelper.Leader is whacked
            try
            {
                if (RaFHelper.Leader != null && RaFHelper.Leader.SpellDistance() < 40 && RaFHelper.Leader.IsAlive)
                {
                    if (SingularSettings.Debug)
                        Logger.WriteDebug("GetLifebloomTarget({0:F1}%): tank {1} @ {2:F1}%, stacks={3}", health, RaFHelper.Leader.SafeName(), RaFHelper.Leader.HealthPercent, RaFHelper.Leader.GetAuraStacks(hotName));
                    return RaFHelper.Leader;
                }
            }
            catch { }
*/
            WoWUnit hotTarget = Group.Tanks.FirstOrDefault(u => u.IsAlive && u.SpellDistance() < 40 && u.HasMyAura(hotName) && u.InLineOfSpellSight);
            if (hotTarget == null)
            {
                hotTarget = Group.Tanks
                    .Where(u => u.IsAlive && u.HealthPercent < health && u.SpellDistance() < 40 && u.InLineOfSpellSight)
                    .OrderBy(u => u.HealthPercent)
                    .FirstOrDefault();
            }

            if (hotTarget != null && SingularSettings.Debug)
                Logger.WriteDebug("GetLifebloomTarget({0:F1}%): tank {1} @ {2:F1}%, stacks={3}", health, hotTarget.SafeName(), hotTarget.HealthPercent, hotTarget.GetAuraStacks(hotName));

            return hotTarget;
        }


        private static WoWUnit GetRestoBestSymbiosisTarget()
        {
            WoWUnit target = null;

            if ( SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds )
                target = Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Warrior);

            if ( target == null)
                target = Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.DeathKnight);
            if ( target == null)
                target = Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Hunter);
            if ( target == null)
                target = Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Rogue);
            if ( target == null)
                target = Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Monk);

            return target;
        }

        private static int checkMushroomCount { get; set; }

        private static Composite CreateMushroomSetBehavior()
        {
            Composite castShroom;

            if (!TalentManager.HasGlyph("the Sprouting Mushroom"))
                castShroom = Spell.CastOnGround("Wild Mushroom", on => (WoWUnit)on, req => true, false);
            else
                castShroom = Spell.CastOnGround("Wild Mushroom", loc => ((WoWUnit)loc).Location, req => req != null, false);

            return new Throttle( 5,
                new Decorator(
                    req => {
                        WoWUnit mushroom = Mushrooms.FirstOrDefault();
                        if (RaFHelper.Leader != null && RaFHelper.Leader.IsValid && (mushroom == null || mushroom.SpellDistance(RaFHelper.Leader) > 10))
                        {
                            if (RaFHelper.Leader.IsAlive && RaFHelper.Leader.Combat && !RaFHelper.Leader.IsMoving
                                && RaFHelper.Leader.GotTarget && RaFHelper.Leader.SpellDistance(RaFHelper.Leader.CurrentTarget) < 15)
                            return true;
                        }
                        return false;
                        },

                    new PrioritySelector(
                        ctx => RaFHelper.Leader,

                        // Make sure we arenIf bloom is coming off CD, make sure we drop some more shrooms. 3 seconds is probably a little late, but good enough.
                    // .. also, waitForSpell must be false since Wild Mushroom does not stop targeting after click like other click on ground spells
                    // .. will wait locally and fall through to cancel targeting regardless
                        new Sequence(
                            castShroom,
                            new Action(ctx => Lua.DoString("SpellStopTargeting()"))
                            )
                        )
                    )
                );

        }

        private static Composite CreateMushroomBloom()
        {
            return new PrioritySelector(

                new Action(r => {
                        checkMushroomCount = Mushrooms.Count();
                        return RunStatus.Failure;
                    }),

                Spell.Cast("Wild Mushroom: Bloom", req => {
                    if (checkMushroomCount == 0 || ((WoWUnit)req).HealthPercent >= DruidSettings.Heal.WildMushroomBloom)
                        return false;

                    List<WoWUnit> shrooms = Mushrooms.ToList();
                    int nearBy = HealerManager.Instance.TargetList.Where( h => h.HealthPercent < DruidSettings.Heal.WildMushroomBloom && shrooms.Any( m => m.SpellDistance(h) < 10)).Count();
                    Logger.WriteDebug("MushroomBloom: {0} shrooms near {1} targets needing heal", shrooms.Count(), nearBy);
                    return nearBy >= DruidSettings.Heal.CountMushroomBloom;
                    })
                );
        }


        #region Diagnostics

        private static Composite CreateRestoDiagnosticOutputBehavior()
        {
            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            return new ThrottlePasses(1, 1,
                new Action(ret =>
                {
                    string log;
                    log = string.Format(".... h={0:F1}%/m={1:F1}%, form:{2}, mushcnt={3}",
                        Me.HealthPercent,
                        Me.ManaPercent,
                        Me.Shapeshift.ToString(),
                        MushroomCount
                        );

                    WoWUnit target = Me.CurrentTarget;
                    if (target != null)
                    {
                        log += string.Format(", th={0:F1}%/tm={1:F1}%, dist={2:F1}, face={3}, loss={4}, sfire={5}, mfire={6}",
                            target.HealthPercent,
                            target.ManaPercent,
                            target.Distance,
                            Me.IsSafelyFacing(target),
                            target.InLineOfSpellSight,
                            (long)target.GetAuraTimeLeft("Sunfire", true).TotalMilliseconds,
                            (long)target.GetAuraTimeLeft("Moonfire", true).TotalMilliseconds
                            );
                    }

                    Logger.WriteDebug(Color.AntiqueWhite, log);
                    return RunStatus.Failure;
                })
                );
        }

        #endregion
    }

}
