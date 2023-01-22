using System;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Lists;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.CommonBot;
using Styx.Helpers;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using Styx.Common;
using System.Drawing;
using System.Collections.Generic;
using CommonBehaviors.Actions;
using Styx.Pathing;
using Styx.Common.Helpers;
using Styx.CommonBot.Routines;

namespace Singular.ClassSpecific.Druid
{
    public class Balance
    {
        # region Properties & Fields

        private enum EclipseType
        {
            None,
            Solar,
            Lunar
        };
        
        private static EclipseType eclipseLastCheck = EclipseType.None;
        public static bool newEclipseDotNeeded;

        private static int StarfallRange { get { return TalentManager.HasGlyph("Focus") ? 20 : 40; } }

        private static int CurrentEclipse { get { return BitConverter.ToInt32(BitConverter.GetBytes(StyxWoW.Me.CurrentEclipse), 0); } }

        private static DruidSettings DruidSettings
        {
            get { return SingularSettings.Instance.Druid(); }
        }

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WoWUnit Target { get { return Me.CurrentTarget; } }

        static int MushroomCount
        {
            get { return ObjectManager.GetObjectsOfType<WoWUnit>().Where(o => o.Entry == 47649 && o.Distance <= 40).Count(o => o.CreatedByUnitGuid == StyxWoW.Me.Guid); }
        }

        static WoWUnit BestAoeTarget
        {
            get { return Clusters.GetBestUnitForCluster(Unit.NearbyUnfriendlyUnits.Where(u => u.Combat && !u.IsCrowdControlled()), ClusterType.Radius, 8f); }
        }

        #endregion

        #region Heal


        private static WoWUnit _CrowdControlTarget;

        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidBalance)]
        public static Composite CreateDruidBalanceHeal()
        {
            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(

                    CreateBalanceDiagnosticOutputBehavior(),

            #region Avoidance 

                    Spell.Cast("Typhoon",
                        ret => Me.CurrentTarget.SpellDistance() < 8 
                            && (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds || (SingularRoutine.CurrentWoWContext == WoWContext.Normal && Me.HealthPercent < 50))
                            && Me.CurrentTarget.Class != WoWClass.Priest
                            && (Me.CurrentTarget.Class != WoWClass.Warlock || Me.CurrentTarget.CastingSpellId == 1949 /*Hellfire*/ || Me.CurrentTarget.HasAura("Immolation Aura"))
                            && Me.CurrentTarget.Class != WoWClass.Hunter
                            && Me.IsSafelyFacing(Me.CurrentTarget, 90f)),

                    new Decorator(
                        ret => Unit.NearbyUnitsInCombatWithMeOrMyStuff.Any(u => u.SpellDistance() < 8)
                            && (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds || (SingularRoutine.CurrentWoWContext == WoWContext.Normal && Me.HealthPercent < 50)),
                        CreateDruidAvoidanceBehavior(CreateSlowMeleeBehavior(), null, null)
                        ),

            #endregion 

                    Spell.Cast("Rejuvenation", on => Me, req => Me.HealthPercent <= DruidSettings.MoonBeastRejuvenationHealth && Me.HasAuraExpired("Rejuvenation", 1)),

                    Common.CreateNaturesSwiftnessHeal(ret => Me.HealthPercent < DruidSettings.SelfNaturesSwiftnessHealth ),
                    Spell.BuffSelf("Renewal", ret => Me.HealthPercent < DruidSettings.SelfRenewalHealth),
                    Spell.BuffSelf("Cenarion Ward", ret => Me.HealthPercent < DruidSettings.SelfCenarionWardHealth),

                    new Decorator(
                        ret => Me.HealthPercent < DruidSettings.SelfHealingTouchHealth || (_CrowdControlTarget != null && _CrowdControlTarget.IsValid && (_CrowdControlTarget.IsCrowdControlled() || Spell.DoubleCastPreventionDict.ContainsAny( _CrowdControlTarget, "Disorienting Roar", "Mighty Bash", "Cyclone", "Hibernate"))),
                        new PrioritySelector(

                            Spell.Buff("Disorienting Roar", req => !Me.CurrentTarget.Stunned && !Me.CurrentTarget.IsCrowdControlled()),
                            Spell.Buff("Mighty Bash", req => !Me.CurrentTarget.Stunned && !Me.CurrentTarget.IsCrowdControlled()),

                            new Decorator(
                                ret => 1 == Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count(),
                                new PrioritySelector(
                                    new Action(r =>
                                    {
                                        if (_CrowdControlTarget == null || !_CrowdControlTarget.IsValid || _CrowdControlTarget.Distance > 40)
                                        {
                                            _CrowdControlTarget = Unit.NearbyUnfriendlyUnits
                                                .Where(u => u.CurrentTargetGuid == Me.Guid && u.Combat && !u.IsCrowdControlled())
                                                .OrderByDescending(k => k.IsPlayer)
                                                .ThenBy(k => k.Guid == Me.CurrentTargetGuid)
                                                .ThenBy(k => k.Distance2DSqr)
                                                .FirstOrDefault();
                                        }
                                        return RunStatus.Failure;
                                    }),

                                    Spell.Buff("Hibernate", true, ctx => _CrowdControlTarget, req => _CrowdControlTarget.IsBeast || _CrowdControlTarget.IsDragon, "Hibernate", "Cyclone"),
                                    Spell.Buff("Cyclone", true, ctx => _CrowdControlTarget, req => true, "Hibernate", "Cyclone")
                                    )
                                ),

                            // heal out of form at this point (try to Barkskin at least to prevent spell pushback)
                            new Throttle(Spell.BuffSelf("Barkskin")),

                            new Decorator(
                                req => !Group.AnyHealerNearby && (Me.CurrentTarget.TimeToDeath() > 15 || Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() > 1),
                                new PrioritySelector(
                                    Spell.BuffSelf("Nature's Vigil"),
                                    Spell.BuffSelf("Heart of the Wild")
                                    )
                                ),

                            new PrioritySelector(
                                Spell.Cast("Rejuvenation", on => Me, ret => Me.HasAuraExpired("Rejuvenation")),
                                Spell.Cast("Healing Touch", mov => true, on => Me, req => Me.PredictedHealthPercent(includeMyHeals: true) < 90, req => Me.HealthPercent > 95)
                                )
                            )
                        ),

                    Spell.Cast("Healing Touch", on => Me, req => Me.HealthPercent <= DruidSettings.MoonBeastHealingTouch && Me.HasAuraExpired("Rejuvenation", 1))
                    )
                );
        }

        #endregion

        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Normal)]
        public static Composite CreateBalancePullNormal()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        // grinding or questing, if target meets these criteria cast an instant to tag quickly
                        // 1. mob is less than 12 yds, so no benefit from delay in Wrath/Starsurge missile arrival
                        // 2. area has another player possibly competing for mobs (we want to tag the mob quickly)
                        new Decorator(
                            ret => Me.CurrentTarget.Distance < 12
                                || ObjectManager.GetObjectsOfType<WoWPlayer>(false, false).Any(p => p.Location.DistanceSqr(Me.CurrentTarget.Location) <= 40 * 40),
                            new PrioritySelector(
                                Spell.Buff("Sunfire", ret => GetEclipseDirection() == EclipseType.Solar),
                                Spell.Buff("Moonfire")
                                )
                            ),

                        // otherwise, start with a bigger hitter with cast time so we can follow 
                        // with an instant to maximize damage at initial aggro
                        Spell.Cast("Starsurge", on => Me.CurrentTarget, req => true, cancel => false),
                        Spell.Cast("Wrath", ret => GetEclipseDirection() == EclipseType.Solar),

                        // we must be moving if here so throw an instant of some type
                        Spell.Buff("Sunfire", ret => GetEclipseDirection() == EclipseType.Solar),
                        Spell.Buff("Moonfire")
                        )
                    ),

                Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 38f, 33f)
                // Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 38f, 33f)
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Normal)]
        public static Composite CreateDruidBalanceNormalCombat()
        {
            Common.WantedDruidForm = ShapeshiftForm.Moonkin;
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCast(FaceDuring.Yes),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        new Decorator(
                            ret => Me.HealthPercent < 40 && Unit.NearbyUnitsInCombatWithMeOrMyStuff.Any(u => u.IsWithinMeleeRange),
                            CreateDruidAvoidanceBehavior(CreateSlowMeleeBehavior(), null, null)
                            ),

                        // Spell.Buff("Entangling Roots", ret => Me.CurrentTarget.Distance > 12),
                        Spell.Buff("Faerie Swarm", ret => Me.CurrentTarget.IsMoving && Me.CurrentTarget.Distance > 20),

                        Spell.BuffSelf("Innervate",
                            ret => StyxWoW.Me.ManaPercent <= DruidSettings.InnervateMana),

                        // yes, only 8yds because we are knocking back only if close to melee range
                        Spell.Cast("Typhoon",
                            ret => Clusters.GetClusterCount(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 8f) >= 1),

                        Spell.Cast("Mighty Bash", ret => Me.CurrentTarget.IsWithinMeleeRange),

                        new Decorator(
                            ret => Spell.UseAOE && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3,
                            new PrioritySelector(

                                // CreateMushroomSetAndDetonateBehavior(),

                                Spell.OffGCD(Spell.Cast("Force of Nature", req => !Me.CurrentTarget.IsTrivial() && Me.CurrentTarget.TimeToDeath() > 8)),

                                // Starfall:  verify either not glyphed -or- at least 3 targets have DoT
                                Spell.Cast("Starfall", req => !TalentManager.HasGlyph("Guided Stars") || Unit.NearbyUnfriendlyUnits.Count(u => u.HasAnyOfMyAuras("Sunfire", "Moonfire")) >= 3),

                                new PrioritySelector(
                                    ctx => Unit.NearbyUnfriendlyUnits.Where(u => u.Combat && !u.IsCrowdControlled() && Me.IsSafelyFacing(u)).ToList(),
                                    Spell.Buff("Moonfire", ret => ((List<WoWUnit>)ret).FirstOrDefault(u => u.HasAuraExpired("Moonfire", 2))),
                                    Spell.Buff("Sunfire", ret => ((List<WoWUnit>)ret).FirstOrDefault(u => u.HasAuraExpired("Sunfire", 2))),

                                    CastHurricaneBehavior( on => Me.CurrentTarget)
                                    )
                                )
                            ),

                        Helpers.Common.CreateInterruptBehavior(),

                        // detonate any left over mushrooms that may exist now we are below AoE count
                        Spell.Cast("Wild Mushroom: Detonate", ret => Spell.UseAOE && MushroomCount > 0),

                        // make sure we always have DoTs 
                        new Sequence(
                            Spell.Buff("Sunfire", true, on => Me.CurrentTarget, req => true, 2),
                            new Action(ret => Logger.WriteDebug("Adding DoT:  Sunfire"))
                            ),

                        new Sequence(
                            Spell.Buff("Moonfire", true, on => Me.CurrentTarget, req => true, 2),
                            new Action(ret => Logger.WriteDebug("Adding DoT:  Moonfire"))
                            ),

                        CreateDoTRefreshOnEclipse(),

                        Spell.Cast("Starsurge", on => Me.CurrentTarget, req => true, cancel => false),
                        Spell.Cast("Starfall", ret => Me.CurrentTarget.IsPlayer || (Me.CurrentTarget.Elite && (Me.CurrentTarget.Level + 10) >= Me.Level)),

                        new PrioritySelector(
                            ctx => GetEclipseDirection() == EclipseType.Lunar,
                            Spell.Cast("Starfire", ret => !(bool)ret),
                            Spell.Cast("Wrath", ret => (bool)ret)
                            )
                        )
                    )
                );
        }

        private static Composite CastHurricaneBehavior( UnitSelectionDelegate onUnit)
        {
            return new Sequence(
                ctx => onUnit(ctx),

                Spell.CastOnGround("Hurricane", on => (WoWUnit) on, req => Me.HealthPercent > 40 && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3, false),

                new Wait(
                    TimeSpan.FromMilliseconds(1000),
                    until => Spell.IsCastingOrChannelling() && Unit.NearbyUnfriendlyUnits.Any(u => u.HasMyAura("Hurricane")),
                    new ActionAlwaysSucceed()
                    ),
                new Wait(
                    TimeSpan.FromSeconds(10),
                    until =>
                    {
                        if (!Spell.IsCastingOrChannelling())
                        {
                            Logger.Write("Hurricane: no longer casting");
                            return true;
                        }
                        if (Me.HealthPercent < 30)
                        {
                            Logger.Write("/cancel Hurricane since my health at {0:F1}%", Me.HealthPercent);
                            return true;
                        }
                        int cnt = Unit.NearbyUnfriendlyUnits.Count(u => u.HasMyAura("Hurricane"));
                        if (cnt < 3)
                        {
                            Logger.Write("/cancel Hurricane since only {0} targets effected", cnt);
                            return true;
                        }

                        return false;
                    },
                    new ActionAlwaysSucceed()
                    ),
                new DecoratorContinue(
                    req => Spell.IsChannelling(),
                    new Action(r => SpellManager.StopCasting())
                    ),
                new WaitContinue(
                    TimeSpan.FromMilliseconds(500),
                    until => !Spell.IsChannelling(),
                    new ActionAlwaysSucceed()
                    )
                )
            ;
        }

        #endregion

        #region Battleground Rotation
        [Behavior(BehaviorType.Pull, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Battlegrounds)]
        [Behavior(BehaviorType.Combat, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Battlegrounds)]
        public static Composite CreateDruidBalancePvPCombat()
        {
            Common.WantedDruidForm = ShapeshiftForm.Moonkin;

            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                // Ensure we do /petattack if we have treants up.
                Helpers.Common.CreateAutoAttack(true),

                Spell.WaitForCast(FaceDuring.Yes),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(), 
                    new PrioritySelector(

                        Spell.BuffSelf("Moonkin Form", req => !Utilities.EventHandlers.IsShapeshiftSuppressed),

                        new PrioritySelector(
                            ctx => Unit.NearbyUnfriendlyUnits.FirstOrDefault( u => u.IsCasting && u.Distance <30 && u.CurrentTargetGuid == Me.Guid ),
                            new Sequence(
                                // following sequence takes advantage of Cast() monitoring spells with a cancel delegate for their entire cast
                                Spell.Cast( "Solar Beam", on => (WoWUnit) on),
                                new PrioritySelector(
                                    Spell.WaitForGcdOrCastOrChannel(),
                                    new ActionAlwaysSucceed()
                                    ),
                                new PrioritySelector(
                                    Spell.CastOnGround("Ursol's Vortex", on => (WoWUnit)on, req => Me.GotTarget, false),
                                    Spell.Cast("Entangling Roots", on => (WoWUnit)on),
                                    new ActionAlwaysSucceed()
                                    )
                                )
                            ),

                        // Helpers.Common.CreateInterruptBehavior(),

                        Spell.Cast("Mighty Bash", ret => Me.CurrentTarget.IsWithinMeleeRange),

                        Spell.Cast("Typhoon",
                            ret => Clusters.GetClusterCount(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 8f) >= 1),

                        // use every Shooting Stars proc
                        Spell.Cast("Starsurge", on => Me.CurrentTarget, req => Me.ActiveAuras.ContainsKey("Shooting Stars"), cancel => false),

                        // Spread MF/IS on Rouges / Feral Druids first
                        Common.CreateFaerieFireBehavior(
                            on => (WoWUnit)Unit.NearbyUnfriendlyUnits.FirstOrDefault(p => (p.Class == WoWClass.Rogue || p.Shapeshift == ShapeshiftForm.Cat) && !p.HasAnyAura("Faerie Fire", "Faerie Swarm") && p.Distance < 35 && Me.IsSafelyFacing(p) && p.InLineOfSpellSight), 
                            req => true),

                        // More DoTs!!  Dot EVERYTHING (including pets) to boost Shooting Stars proc chance
                        new PrioritySelector(
                            ctx => Unit.NearbyUnfriendlyUnits.FirstOrDefault( u => !u.HasAllMyAuras( "Moonfire", "Sunfire") && Me.IsSafelyFacing(u) && u.InLineOfSpellSight ),
                            Spell.Buff( "Moonfire", on => (WoWUnit) on),
                            Spell.Buff( "Sunfire", on => (WoWUnit) on)
                            ),

                        Spell.Cast( "Starfall"),

                        new Decorator(
                            ret => !Unit.NearbyUnfriendlyUnits.Any(u => u.CurrentTargetGuid == Me.Guid),
                            new PrioritySelector(
                                ctx => GetEclipseDirection() == EclipseType.Lunar,
                                Spell.Cast("Starfire", ret => !(bool) ret ),
                                Spell.Cast("Wrath", ret => (bool) ret)
                                )
                            ),
#if SPAM_INSTANT_TO_AVOID_SPELLLOCK
                        new Decorator(
                            ret => Unit.NearbyUnfriendlyUnits.Any(u => u.CurrentTargetGuid == Me.Guid),
                            new PrioritySelector(
                                Spell.Buff("Moonfire", req => !Me.HasAura("Eclipse (Solar)")),
                                Spell.Buff("Sunfire")
                                )
                            ),
#endif
                        // Now on any group target missing Weakened Armor
                        Spell.Buff("Fairie Fire",
                        on => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.Distance < 35 && !u.HasAura("Weakened Armor")
                                                                    && Unit.GroupMembers.Any(m => m.CurrentTargetGuid == u.Guid)
                                                                    && Me.IsSafelyFacing(u) && u.InLineOfSpellSight)),

                        // Now any enemy missing Weakened Armor
                        Spell.Buff("Fairie Fire", 
                            on => Unit.NearbyUnfriendlyUnits.FirstOrDefault( u => u.Distance < 35 && !u.HasAura( "Weakened Armor") && Me.IsSafelyFacing(u) && u.InLineOfSpellSight ))

                        )
                    )
                );
        }

        #endregion


        #region Instance Rotation
        [Behavior(BehaviorType.Pull, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Instances)]
        [Behavior(BehaviorType.Combat, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Instances)]
        public static Composite CreateDruidBalanceInstanceCombat()
        {
            Common.WantedDruidForm = ShapeshiftForm.Moonkin;
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCast(FaceDuring.Yes),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Spell.Buff("Innervate",
                            ret => (from healer in Group.Healers 
                                    where healer != null && healer.IsAlive && healer.Distance < 30 && healer.ManaPercent <= 15
                                    select healer).FirstOrDefault()),

                        Spell.BuffSelf("Innervate",
                            ret => StyxWoW.Me.ManaPercent <= DruidSettings.InnervateMana),

                        Spell.BuffSelf("Moonkin Form", req => !Utilities.EventHandlers.IsShapeshiftSuppressed),

                        // Spell.Cast("Mighty Bash", ret => Me.CurrentTarget.IsWithinMeleeRange),

                        new PrioritySelector(
                            ctx => !Spell.UseAOE ? 1 : Unit.UnfriendlyUnitsNearTarget(10f).Count(),

                            new Decorator(
                                req => ((int)req) > 1,
                                new PrioritySelector(

                                    // CreateMushroomSetAndDetonateBehavior(),

                                    Spell.Cast("Starfall", ret => StyxWoW.Me),

                                    Spell.CastOnGround("Hurricane", on => Me.CurrentTarget, req => ((int)req) > 6, false),

                                    new PrioritySelector(
                                        ctx => Unit.NearbyUnfriendlyUnits.Where(u => u.Combat && !u.IsCrowdControlled() && Me.IsSafelyFacing(u)).ToList(),
                                        Spell.Buff("Sunfire", on => (WoWUnit) ((List<WoWUnit>)on).FirstOrDefault(u => u.HasAuraExpired("Sunfire", 2))),
                                        Spell.CastOnGround("Hurricane", on => Me.CurrentTarget, req => (!Spell.UseAOE ? 1 : Unit.UnfriendlyUnitsNearTarget(10f).Count()) > 3, false),
                                        Spell.Buff("Moonfire", on => (WoWUnit) ((List<WoWUnit>)on).FirstOrDefault(u => u.HasAuraExpired("Moonfire", 2)))
                                        )
                                    )
                                )
                            ),

                        Helpers.Common.CreateInterruptBehavior(),

                        // make sure we always have DoTs 
                        new Sequence(
                            Spell.Buff("Sunfire", on => Me.CurrentTarget.HasAuraExpired("Sunfire", 2)),
                            new Action(ret => Logger.WriteDebug("Adding DoT:  Sunfire"))
                            ),

                        new Sequence(
                            Spell.Buff("Moonfire", on => Me.CurrentTarget.HasAuraExpired("Moonfire", 2)),
                            new Action(ret => Logger.WriteDebug("Adding DoT:  Moonfire"))
                            ),

                        new Decorator( 
                            ret => Me.HasAura("Celestial Alignment"),
                            new PrioritySelector(
                                // to do: make last two casts DoTs if possible... 
                                Spell.Cast("Starsurge", on => Me.CurrentTarget, req => true, cancel => false),
                                Spell.Cast("Starfire")
                                )
                            ),

                        CreateDoTRefreshOnEclipse(),

                        Spell.Cast("Starsurge", on => Me.CurrentTarget, req => true, cancel => false),
                        Spell.Cast("Starfall"),

                        new PrioritySelector(
                            ctx => GetEclipseDirection() == EclipseType.Lunar,
                            Spell.Cast("Starfire", ret => !(bool) ret ),
                            Spell.Cast("Wrath", ret => (bool) ret)
                            )
                        )
                    )
                );
        }

        /// <summary>
        /// creates behavior to recast DoTs when an Eclipse occurs.  this creates
        /// a more powerful DoT then when cast out of associate Eclipse so no
        /// check on overwriting existing DoT is made.  in the event that
        /// a more powerful version already exists, only one failed attempt
        /// (red message) should occur
        /// </summary>
        /// <returns></returns>
        private static Composite CreateDoTRefreshOnEclipse()
        {
            return new PrioritySelector(

                new Action(ret =>
                {
                    EclipseType eclipseCurrent;
                    if (StyxWoW.Me.HasAura("Eclipse (Solar)"))
                        eclipseCurrent = EclipseType.Solar;
                    else if (StyxWoW.Me.HasAura("Eclipse (Lunar)"))
                        eclipseCurrent = EclipseType.Lunar;
                    else
                        eclipseCurrent = EclipseType.None;

                    if (eclipseLastCheck != eclipseCurrent)
                    {
                        eclipseLastCheck = eclipseCurrent;
                        newEclipseDotNeeded = eclipseLastCheck != EclipseType.None;
                    }

                    return RunStatus.Failure;
                }),

                new Sequence(
                    Spell.Buff("Moonfire", ret => newEclipseDotNeeded && eclipseLastCheck == EclipseType.Lunar),
                    new Action(ret =>
                    {
                        newEclipseDotNeeded = false;
                        Logger.WriteDebug("Refresh DoT: Moonfire");
                    })
                    ),

                new Sequence(
                    Spell.Buff("Sunfire", ret => newEclipseDotNeeded && eclipseLastCheck == EclipseType.Solar),
                    new Action(ret =>
                    {
                        newEclipseDotNeeded = false;
                        Logger.WriteDebug("Refresh DoT: Sunfire");
                    })
                    )

                );
        }

        private static EclipseType GetEclipseDirection()
        {
#if USE_LUA_FOR_ECLIPSE
            return Lua.GetReturnVal<string>("return GetEclipseDirection();", 0);
#else
            WoWAura eclipse = Me.GetAllAuras().FirstOrDefault(a => a.SpellId == 67483 || a.SpellId == 67484);
            if (eclipse == null)
                return EclipseType.None;

            if (eclipse.SpellId == 67483)
                return EclipseType.Solar;

            return EclipseType.Lunar; // 67484
#endif
        }

        #endregion


        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Battlegrounds | WoWContext.Instances, 2)]
        public static Composite CreateBalancePreCombatBuffBattlegrounds()
        {
            return new PrioritySelector(
                Common.CreateDruidCastSymbiosis(on => GetBalanceBestSymbiosisTarget()),
                new Decorator(
                    ret => SingularRoutine.CurrentWoWContext != WoWContext.Battlegrounds || !Unit.NearbyUnfriendlyUnits.Any(),
                    new PrioritySelector(
                        Spell.BuffSelf( "Astral Communion", ret => PVP.PrepTimeLeft < 20 && !Me.HasAnyAura("Eclipse (Lunar)","Eclipse (Solar)"))
                        )
                    )
                );
        }


        [Behavior(BehaviorType.PullBuffs, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.All, 2)]
        public static Composite CreateBalancePullBuff()
        {
            return new PrioritySelector(
                CreateBalanceDiagnosticOutputBehavior(),
                Spell.BuffSelf("Moonkin Form", req => !Utilities.EventHandlers.IsShapeshiftSuppressed)
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Normal, 2)]
        public static Composite CreateBalanceCombatBuffNormal()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Moonkin Form", req => !Utilities.EventHandlers.IsShapeshiftSuppressed)
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Battlegrounds | WoWContext.Instances, 2)]
        public static Composite CreateBalanceCombatBuffBattlegrounds()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Moonkin Form", req => !Utilities.EventHandlers.IsShapeshiftSuppressed),

                // Symbiosis
/*
                Common.SymbCast("Mirror Image", on => Me.CurrentTarget, ret => Me.GotTarget && Me.Shapeshift == ShapeshiftForm.Moonkin),
                Common.SymbCast("Hammer of Justice", on => Me.CurrentTarget, ret => Me.GotTarget && !Me.CurrentTarget.IsBoss() && (Me.CurrentTarget.IsCasting || Me.CurrentTarget.IsPlayer)),

                Common.SymbBuff("Unending Resolve", on => Me, ret => Me.HealthPercent < DruidSettings.Barkskin),
                Common.SymbBuff("Anti-Magic Shell", on => Me, ret => Unit.NearbyUnfriendlyUnits.Any(u => (u.IsCasting || u.ChanneledCastingSpellId != 0) && u.CurrentTargetGuid == Me.Guid)),
                // add mass dispel ...
                Common.SymbBuff("Cloak of Shadows", on => Me, ret => Me.ActiveAuras.Any(a => a.Value.IsHarmful && a.Value.IsActive && a.Value.Spell.DispelType != WoWDispelType.None))
*/
                Common.SymbCast( Symbiosis.MirrorImage, on => Me.CurrentTarget, ret => Me.GotTarget && Me.Shapeshift == ShapeshiftForm.Moonkin),
                Common.SymbCast( Symbiosis.HammerOfJustice, on => Me.CurrentTarget, ret => Me.GotTarget && !Me.CurrentTarget.IsBoss() && (Me.CurrentTarget.IsCasting || Me.CurrentTarget.IsPlayer)),

                Common.SymbBuff( Symbiosis.UnendingResolve, on => Me, ret => Me.HealthPercent < DruidSettings.Barkskin),
                Common.SymbBuff( Symbiosis.AntiMagicShell, on => Me, ret => Unit.NearbyUnfriendlyUnits.Any(u => (u.IsCasting || u.ChanneledCastingSpellId != 0) && u.CurrentTargetGuid == Me.Guid)),
                // add mass dispel ...
                Common.SymbBuff( Symbiosis.CloakOfShadows, on => Me, ret => Me.ActiveAuras.Any(a => a.Value.IsHarmful && a.Value.IsActive && a.Value.Spell.DispelType != WoWDispelType.None))
                );
        }

        private static WoWUnit GetBalanceBestSymbiosisTarget()
        {
            WoWUnit target = null;
            if (target == null)
                target = Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Mage);
            if (target == null)
                target = Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Warlock);
            if (target == null)
                target = Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.DeathKnight);
            if (target == null)
                target = Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Paladin);
            if (target == null)
                target = Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Rogue);
            // if (target == null)
            //    target = Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Priest);

            return target;
        }

        #region Avoidance and Disengage

        /// <summary>
        /// creates a Druid specific avoidance behavior based upon settings.  will check for safe landing
        /// zones before using WildCharge or rocket jump.  will additionally do a running away or jump turn
        /// attack while moving away from attacking mob if behaviors provided
        /// </summary>
        /// <param name="nonfacingAttack">behavior while running away (back to target - instants only)</param>
        /// <param name="jumpturnAttack">behavior while facing target during jump turn (instants only)</param>
        /// <returns></returns>
        public static Composite CreateDruidAvoidanceBehavior(Composite slowAttack, Composite nonfacingAttack, Composite jumpturnAttack)
        {
            return Avoidance.CreateAvoidanceBehavior( "Wild Charge", 20, Disengage.Direction.Backwards, new ActionAlwaysSucceed() );
        }

        private static Composite CreateSlowMeleeBehavior()
        {
            return new PrioritySelector(
                ctx => SafeArea.NearestEnemyMobAttackingMe,
                new Decorator(
                    ret => ret != null,
                    new PrioritySelector(
                        new Throttle( 2,
                            new PrioritySelector(
                                new Decorator( 
                                    req => (req as WoWUnit).IsCrowdControlled(),
                                    new Action(r => Logger.WriteDebug("SlowMelee: closest mob already crowd controlled"))
                                    ),
                                Spell.CastOnGround("Ursol's Vortex", on => (WoWUnit)on, req => Me.GotTarget, false),
                                Spell.Buff("Disorienting Roar", onUnit => (WoWUnit)onUnit, req => true),
                                Spell.Buff("Mass Entanglement", onUnit => (WoWUnit)onUnit, req => true),
                                Spell.Buff("Mighty Bash", onUnit => (WoWUnit)onUnit, req => true),
                                new Throttle( 1, Spell.Buff("Faerie Swarm", onUnit => (WoWUnit)onUnit, req => true)),
                                new Throttle( 2, Spell.Buff("Entangling Roots", false, on => (WoWUnit) on, req => Unit.NearbyUnitsInCombatWithUsOrOurStuff.Any(u => u.Guid != (req as WoWUnit).Guid ))),
                                new Sequence(
                                    Spell.Cast("Typhoon", mov => false, on => (WoWUnit) on, req => (req as WoWUnit).SpellDistance() < 28 && Me.IsSafelyFacing((WoWUnit)req, 60)),
                                    new WaitContinue(TimeSpan.FromMilliseconds(500), until => (until as WoWUnit).SpellDistance() > 30, new ActionAlwaysSucceed()),
                                    new ActionAlwaysFail()
                                    )
/*
                                new Sequence(                                   
                                    Spell.CastOnGround("Wild Mushroom",
                                        on => (WoWUnit) on,
                                        req => req != null && !Spell.IsSpellOnCooldown("Wild Mushroom: Detonate")
                                        ),
                                    new Action( r => Logger.WriteDebug( "SlowMelee: waiting for Mushroom to appear")),
                                    new WaitContinue( TimeSpan.FromMilliseconds(1500), until => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling() && MushroomCount > 0, new ActionAlwaysSucceed()),
                                    new Action(r => Logger.WriteDebug("SlowMelee: found {0} mushrooms", MushroomCount)),
                                    Spell.Cast("Wild Mushroom: Detonate")
                                    )
*/ 
                                )
                            )
                        )
                    )
                );
        }

        #endregion


        private static WaitTimer detonateTimer = new WaitTimer(TimeSpan.FromSeconds(4));
        private static int checkMushroomCount { get; set; }

        private static Composite CreateMushroomSetAndDetonateBehavior()
        {
            return new Decorator( 
                req => Spell.UseAOE, 
                new PrioritySelector(

                    new Action( r => { 
                        checkMushroomCount = MushroomCount; 
                        return RunStatus.Failure; 
                        }),

                    // detonate if we have 3 shrooms -or- or timer since last shroom cast has expired
                    // Spell.Cast("Wild Mushroom: Detonate", ret => checkMushroomCount >= 3 || (checkMushroomCount > 0 && detonateTimer.IsFinished)),
                    Spell.Cast("Wild Mushroom: Detonate", ret => checkMushroomCount > 0),

                    // Make sure we arenIf Detonate is coming off CD, make sure we drop some more shrooms. 3 seconds is probably a little late, but good enough.
                    // .. also, waitForSpell must be false since Wild Mushroom does not stop targeting after click like other click on ground spells
                    // .. will wait locally and fall through to cancel targeting regardless
                    new Sequence(
                        // Spell.CastOnGround("Wild Mushroom", on => BestAoeTarget, req => checkMushroomCount < 3 && Spell.GetSpellCooldown("Wild Mushroom: Detonate").TotalSeconds < 3f, false),
                        Spell.CastOnGround("Wild Mushroom", on => BestAoeTarget, req => checkMushroomCount < 1 && Spell.GetSpellCooldown("Wild Mushroom: Detonate").TotalSeconds < 1f, false),
                        new Action(ctx => detonateTimer.Reset()), 
                        new Action( ctx => Lua.DoString("SpellStopTargeting()"))                       
                        )
                    )
                );

        }

        #region Diagnostics

        private static Composite CreateBalanceDiagnosticOutputBehavior()
        {
            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            return new ThrottlePasses(1, 1,
                new Action(ret =>
                {
                    string log;
                    WoWAura eclips = Me.GetAllAuras().FirstOrDefault(a => a.Name == "Eclipse (Solar)" || a.Name == "Eclipse (Lunar)");
                    string eclipsString = eclips == null ? "None" : (eclips.Name == "Eclipse (Solar)" ? "Solar" : "Lunar");

                    log = string.Format(".... h={0:F1}%/m={1:F1}%, form:{2}, eclps={3}, towards={4}, eclps#={5}, mushcnt={6}",
                        Me.HealthPercent,
                        Me.ManaPercent,
                        Me.Shapeshift.ToString(),
                        eclipsString,
                        GetEclipseDirection().ToString(),
                        Me.CurrentEclipse,
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
