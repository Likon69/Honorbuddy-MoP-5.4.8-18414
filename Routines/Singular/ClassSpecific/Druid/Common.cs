#region

using System;
using System.Collections.Generic;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Settings;
using Styx;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using Styx.CommonBot;
using Singular.Managers;
using CommonBehaviors.Actions;
using System.Drawing;
using Styx.CommonBot.POI;

#endregion

namespace Singular.ClassSpecific.Druid
{
    public class Common
    {
        public static ShapeshiftForm WantedDruidForm { get; set; }
        private static DruidSettings DruidSettings { get { return SingularSettings.Instance.Druid(); } }
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        public static bool HasTalent(DruidTalents tal) { return TalentManager.IsSelected((int)tal); }

        public const WoWSpec DruidAllSpecs = (WoWSpec)int.MaxValue;

        [Behavior(BehaviorType.Initialize, WoWClass.Druid)]
        public static Composite CreateDruidInitialize()
        {
            if (SingularRoutine.CurrentWoWContext == WoWContext.Normal || SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
            {
                if ( TalentManager.CurrentSpec == WoWSpec.DruidBalance || TalentManager.CurrentSpec == WoWSpec.DruidRestoration)
                {
                    Kite.CreateKitingBehavior(null, null, null);
                }
            }

            return null;
        }


        #region PreCombat Buffs

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid)]
        public static Composite CreateDruidPreCombatBuff()
        {
            // Cast motw if player doesn't have it or if in instance/bg, out of combat and 'Buff raid with Motw' is true or if in instance and in combat and both CatRaidRebuff and 'Buff raid with Motw' are true
            return new PrioritySelector(

                PartyBuff.BuffGroup( "Mark of the Wild", ret => !Me.HasAura("Prowl") && !Me.Combat ),
                Spell.BuffSelf( "Mark of the Wild", ret => !Me.HasAura("Prowl") && !Me.IsInGroup() )

                /*   This logic needs work. 
                new Decorator(
                    ret =>
                    !Me.HasAura("Bear Form") &&
                    DruidSettings.PvPStealth && (Battlegrounds.IsInsideBattleground 
                    || Me.CurrentMap.IsArena) &&
                    !Me.Mounted && !Me.HasAura("Travel Form"),
                    Spell.BuffSelf("Cat Form")
                    ),
                new Decorator(
                    ret =>
                    Me.HasAura("Cat Form") &&
                    (DruidSettings.PvPStealth && (Battlegrounds.IsInsideBattleground 
                    || Me.CurrentMap.IsArena)),
                    Spell.BuffSelf("Prowl")
                    )*/
                );
        }

        #endregion

        [Behavior(BehaviorType.LossOfControl, WoWClass.Druid)]
        public static Composite CreateDruidLossOfControlBehavior()
        {
            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(
                    new Sequence(
                        Spell.BuffSelf("Barkskin"),
                        Common.SymbBuff(Symbiosis.Dispersion, on => Me, req => Me.Stunned || Me.Fleeing || Me.Silenced ),
                        new Action( r => Logger.Write( Color.LightCoral, "Loss of Control - BARKSKIN!!!!"))
                        )
                    )
                );
        }
        

        #region Combat Buffs

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, DruidAllSpecs, WoWContext.Normal)]
        public static Composite CreateDruidCombatBuffsNormal()
        {
            return new Decorator(
                req => !Unit.IsTrivial(Me.CurrentTarget),
                new PrioritySelector(
                    Spell.BuffSelf("Innervate", ret => StyxWoW.Me.ManaPercent <= DruidSettings.InnervateMana),
                    Spell.Cast("Barkskin", ctx => Me, ret => Me.HealthPercent < DruidSettings.Barkskin || Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() >= 3),
                    Spell.Cast("Disorenting Roar", ctx => Me, ret => Me.HealthPercent < 40 || Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() >= 3),

                    // will hibernate only if can cast in form, or already left form for some other reason
                    Spell.Buff("Hibernate",
                        ctx => Unit.NearbyUnitsInCombatWithMeOrMyStuff.FirstOrDefault(
                            u => (u.IsBeast || u.IsDragon)
                                && (Me.HasAura("Predatory Swiftness") || (!u.IsMoving && Me.Shapeshift == ShapeshiftForm.Normal))
                                && (!Me.GotTarget || Me.CurrentTarget.Location.Distance(u.Location) > 10)
                                && Me.CurrentTargetGuid != u.Guid
                                && !u.HasAnyAura("Hibernate", "Cyclone", "Entangling Roots")
                                )
                            ),

                    // will root only if can cast in form, or already left form for some other reason
                    Spell.Buff("Entangling Roots",
                        ctx => Unit.NearbyUnitsInCombatWithMeOrMyStuff.FirstOrDefault(
                                u => (Me.HasAura("Predatory Swiftness") || Me.Shapeshift == ShapeshiftForm.Normal || Me.Shapeshift == ShapeshiftForm.Moonkin)
                                    && Me.CurrentTargetGuid != u.Guid
                                    && u.SpellDistance() > 15
                                    && !u.HasAnyAura("Hibernate", "Cyclone", "Entangling Roots", "Sunfire", "Moonfire")
                                ),
                        req => !Me.HasAura("Starfall")
                        ),

                    // combat buffs - make sure we have target and in range and other checks
                    // ... to avoid wastine cooldowns
                    new Decorator(
                        ret => Me.GotTarget
                            && (Me.CurrentTarget.IsPlayer || Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() >= 3)
                            && Me.SpellDistance(Me.CurrentTarget) < ((TalentManager.CurrentSpec == WoWSpec.DruidFeral || TalentManager.CurrentSpec == WoWSpec.DruidGuardian) ? 8 : 40)
                            && Me.CurrentTarget.InLineOfSight
                            && Me.IsSafelyFacing(Me.CurrentTarget),
                        new PrioritySelector(
                            Spell.BuffSelf("Celestial Alignment", ret => Spell.GetSpellCooldown("Celestial Alignment") == TimeSpan.Zero && PartyBuff.WeHaveBloodlust),

                            Spell.OffGCD(Spell.Cast("Force of Nature", req => TalentManager.CurrentSpec != WoWSpec.DruidRestoration && Me.CurrentTarget.TimeToDeath() > 8)),

                    // to do:  time ICoE at start of eclipse
                            Spell.BuffSelf("Incarnation: Chosen of Elune"),
                            Spell.BuffSelf("Nature's Vigil")
                            )
                        )
                    )
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.Battlegrounds)]
        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Instances | WoWContext.Battlegrounds)]
        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.Instances | WoWContext.Battlegrounds)]
        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidRestoration, WoWContext.Instances | WoWContext.Battlegrounds)]
        public static Composite CreateDruidCombatBuffsInstance()
        {
            return new PrioritySelector(

                CreateRebirthBehavior(ctx => Group.Tanks.FirstOrDefault(t => !t.IsMe && t.IsDead) ?? Group.Healers.FirstOrDefault(h => !h.IsMe && h.IsDead)),

                Spell.Buff("Innervate", ret => StyxWoW.Me.ManaPercent <= DruidSettings.InnervateMana),
                Spell.Cast("Barkskin", ctx => Me, ret => Me.HealthPercent < DruidSettings.Barkskin || Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() >= 3),
                Spell.Cast("Disorenting Roar", ctx => Me, ret => Me.HealthPercent < 40 || Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() >= 3),

                // combat buffs - make sure we have target and in range and other checks
                // ... to avoid wastine cooldowns
                new Decorator(
                    ret => Me.GotTarget 
                        && (Me.CurrentTarget.IsPlayer || Me.CurrentTarget.IsBoss())
                        && Me.SpellDistance( Me.CurrentTarget) < ((TalentManager.CurrentSpec == WoWSpec.DruidFeral || TalentManager.CurrentSpec == WoWSpec.DruidGuardian) ? 8 : 40)
                        && Me.CurrentTarget.InLineOfSight 
                        && Me.IsSafelyFacing(Me.CurrentTarget),
                    new PrioritySelector(
                        Spell.BuffSelf("Celestial Alignment", ret => Spell.GetSpellCooldown("Celestial Alignment") == TimeSpan.Zero && PartyBuff.WeHaveBloodlust),
                        new Sequence(
                            Spell.OffGCD(Spell.Cast("Force of Nature", req => TalentManager.CurrentSpec != WoWSpec.DruidRestoration && Me.CurrentTarget.TimeToDeath() > 8)),
                            new ActionAlwaysFail()
                            ),
                        // to do:  time ICoE at start of eclipse
                        Spell.BuffSelf("Incarnation: Chosen of Elune"),
                        Spell.BuffSelf("Nature's Vigil")
                        )
                    )
                );
        }

/*
        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Instances, 1)]
        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidFeral , WoWContext.Instances, 1)]
        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidGuardian , WoWContext.Instances, 1)]
        public static Composite CreateNonRestoDruidInstanceCombatBuffs()
        {
            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(

                    new Decorator(
                        ret => HasTalent(DruidTalents.DreamOfCenarius) && !Me.HasAura("Dream of Cenarius"),
                        new PrioritySelector(
                            Spell.Heal("Healing Touch", ret => Me.ActiveAuras.ContainsKey("Predatory Swiftness")),
                            CreateNaturesSwiftnessHeal(on => GetBestHealTarget())
                            )
                        )
                    )
                );
        }
*/
        #endregion

        #region Heal

        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.Normal)]
        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.Battlegrounds)]
        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.Normal)]
        public static Composite CreateDruidNonRestoHealNormal()
        {
            return new PrioritySelector(

                // defensive check first
                Spell.BuffSelf("Survival Instincts", ret => TalentManager.CurrentSpec == WoWSpec.DruidFeral && Me.HealthPercent < DruidSettings.SurvivalInstinctsHealth),

                // keep rejuv up 
                Spell.Cast("Rejuvenation", on => Me,
                    ret =>
                    {
                        if (!Me.HasAuraExpired("Rejuvenation", 1))
                            return false;
                        if (TalentManager.CurrentSpec == WoWSpec.DruidGuardian && Me.HasAura("Heart of the Wild") && Me.HealthPercent < 95)
                            return true;
                        return !Group.MeIsTank && Me.PredictedHealthPercent(includeMyHeals: true) < DruidSettings.SelfRejuvenationHealth;
                    }),

                Spell.Cast( "Healing Touch", on => 
                    {
                        WoWUnit target = null;
                        if (Me.HasAura("Predatory Swiftness"))
                        {
                            // heal self if needed
                            if (Me.HealthPercent < DruidSettings.PredSwiftnessHealingTouchHealth)
                                target = Me;
                            // already checked self, so skip group searches
                            else if (SingularRoutine.CurrentWoWContext == WoWContext.Normal)
                                target = null;  
                            // heal others if needed
                            else if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds || (Me.GotTarget && Me.CurrentTarget.IsPlayer))
                                target = Unit.GroupMembers.Where(p => p.IsAlive && p.PredictedHealthPercent() < DruidSettings.PredSwiftnessPvpHeal && p.DistanceSqr < 40 * 40).FirstOrDefault();
                            // heal anyone if buff about to expire
                            else if (Me.GetAuraTimeLeft("Predatory Swiftness", true).TotalMilliseconds.Between(500, 2000))
                                target = Unit.GroupMembers.Where(p => p.IsAlive && p.DistanceSqr < 40 * 40 && p.HealthPercent < 30).OrderBy(k => k.PredictedHealthPercent()).FirstOrDefault();

                            if (target != null)
                            {
                                Logger.WriteDebug("PredSwift Heal @ actual:{0:F1}% predict:{1:F1}% and moving:{2} in form:{3}", target.HealthPercent, target.PredictedHealthPercent(includeMyHeals: true), target.IsMoving, target.Shapeshift);
                            }
                        }
                        return target;
                    }) ,

                Spell.Cast("Renewal", on => Me, ret => Me.HealthPercent < DruidSettings.SelfRenewalHealth),
                Spell.BuffSelf("Cenarion Ward", ret => Me.HealthPercent < DruidSettings.SelfCenarionWardHealth),

                CreateNaturesSwiftnessHeal(ret => Me.HealthPercent < DruidSettings.SelfNaturesSwiftnessHealth),

                Spell.Cast("Disorienting Roar", ret => Me.HealthPercent <= DruidSettings.DisorientingRoarHealth && DruidSettings.DisorientingRoarCount <= Unit.NearbyUnfriendlyUnits.Count(u => u.Aggro || (u.Combat && u.IsTargetingMeOrPet))),
                Spell.Cast("Might of Ursoc", ret => Me.HealthPercent < DruidSettings.SelfNaturesSwiftnessHealth),

                // heal out of form at this point (try to Barkskin at least)
                new Throttle(Spell.BuffSelf("Barkskin", ret => Me.HealthPercent < DruidSettings.Barkskin)),

                // for a lowbie Feral or a Bear not serving as Tank in a group
                new Decorator(
                    ret => Me.HealthPercent < DruidSettings.SelfHealingTouchHealth && !SpellManager.HasSpell("Predatory Swiftness") && !Group.MeIsTank && SingularRoutine.CurrentWoWContext != WoWContext.Instances,
                    new PrioritySelector(
                        Spell.Cast("Rejuvenation", on => Me, ret => Me.HasAuraExpired("Rejuvenation", 1)),
                        Spell.Cast("Healing Touch", on => Me)
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.Instances )]
        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.Battlegrounds)]
        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.Instances)]
        public static Composite CreateDruidNonRestoHealInstances()
        {
            return new PrioritySelector(

                // defensive check first
                Spell.BuffSelf("Survival Instincts", ret => TalentManager.CurrentSpec == WoWSpec.DruidFeral && Me.HealthPercent < DruidSettings.SurvivalInstinctsHealth),

                Spell.Cast("Renewal", on => Me, ret => Me.HealthPercent < DruidSettings.SelfRenewalHealth),
                Spell.BuffSelf("Cenarion Ward", ret => Me.HealthPercent < DruidSettings.SelfCenarionWardHealth),

                CreateNaturesSwiftnessHeal(ret => Me.HealthPercent < DruidSettings.SelfNaturesSwiftnessHealth),

                Spell.Cast("Might of Ursoc", ret => Me.HealthPercent < DruidSettings.SelfNaturesSwiftnessHealth),

                // heal out of form at this point (try to Barkskin at least)
                new Throttle(Spell.BuffSelf("Barkskin", ret => Me.HealthPercent < DruidSettings.Barkskin)),

                new Decorator(
                    req => !Group.AnyHealerNearby,
                    CreateDruidNonRestoHealNormal()
                    )
                );
        }

        public static Composite CreateNaturesSwiftnessHeal(SimpleBooleanDelegate requirements = null)
        {
            return CreateNaturesSwiftnessHeal(on => Me, requirements);
        }

        public static Composite CreateNaturesSwiftnessHeal(UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements = null)
        {
            return new Decorator(
                ret => onUnit != null && onUnit(ret) != null && requirements != null && requirements(ret),
                new Sequence(
                    Spell.BuffSelf("Nature's Swiftness"),
                    new Wait(TimeSpan.FromMilliseconds(500), ret => Me.HasAura("Nature's Swiftness"), new ActionAlwaysSucceed()),
                    Spell.Cast("Healing Touch", ret => false, onUnit, req => true)
                    )
                );
        }

        public static WoWUnit GetBestHealTarget()
        {
            if (SingularRoutine.CurrentWoWContext == WoWContext.Normal || Me.HealthPercent < 40)
                return Me;

            return Unit.NearbyFriendlyPlayers.Where(p=>p.IsAlive).OrderBy(k=>k.PredictedHealthPercent()).FirstOrDefault();
        }

        #endregion

        #region Rest

        [Behavior(BehaviorType.Rest, WoWClass.Druid, WoWSpec.DruidBalance)]
        [Behavior(BehaviorType.Rest, WoWClass.Druid, WoWSpec.DruidFeral)]
        [Behavior(BehaviorType.Rest, WoWClass.Druid, WoWSpec.DruidGuardian)]
        public static Composite CreateNonRestoDruidRest()
        {
            return new PrioritySelector(

                new Decorator(
                    ret => !Me.HasAura("Drink") && !Me.HasAura("Food")
                        && Me.PredictedHealthPercent(includeMyHeals: true) < (Me.Shapeshift == ShapeshiftForm.Normal ? 85 : SingularSettings.Instance.MinHealth)
                        && ((Me.HasAuraExpired("Rejuvenation", 1) && Spell.CanCastHack("Rejuvenation", Me)) || Spell.CanCastHack("Healing Touch", Me)),
                    new PrioritySelector(
                        Movement.CreateEnsureMovementStoppedBehavior( reason:"to heal"),
                        new Action(r => { Logger.WriteDebug("Rest Heal @ actual:{0:F1}% predict:{1:F1}% and moving:{2} in form:{3}", Me.HealthPercent, Me.PredictedHealthPercent(includeMyHeals: true), Me.IsMoving, Me.Shapeshift ); return RunStatus.Failure; }),
                        Spell.BuffSelf("Rejuvenation", req => !SpellManager.HasSpell("Healing Touch")),
                        Spell.Cast("Healing Touch",
                            mov => true,
                            on => Me,
                            req => true,
                            cancel => Me.HealthPercent > 92)
                        )
                    ),

                Rest.CreateDefaultRestBehaviour(null, "Revive"),
                CreateDruidMovementBuff()
                );
        }

        #endregion

        internal static Composite CreateProwlBehavior(SimpleBooleanDelegate req = null)
        {
            return new Sequence(
                Spell.BuffSelf("Prowl", ret => Me.Shapeshift == ShapeshiftForm.Cat && (req == null || req(ret))),
                new Wait(TimeSpan.FromMilliseconds(500), ret => Me.HasAura("Prowl"), new ActionAlwaysSucceed())
                );
        }

        public static Composite CreateRebirthBehavior(UnitSelectionDelegate onUnit)
        {
            if (TalentManager.CurrentSpec == WoWSpec.DruidGuardian)
                return Helpers.Common.CreateCombatRezBehavior("Rebirth", on => ((WoWUnit)on).SpellDistance() < 40 && ((WoWUnit)on).InLineOfSpellSight, requirements => true);

            return Helpers.Common.CreateCombatRezBehavior("Rebirth", filter => true, reqd => !Me.HasAnyAura("Nature's Swiftness", "Predatory Swiftness"));
        }

        public static Composite CreateFaerieFireBehavior(UnitSelectionDelegate onUnit, SimpleBooleanDelegate Required)
        {
            if (onUnit == null)
                onUnit = on => Me.CurrentTarget;

            // Fairie Fire has a 1.5 sec GCD, Faerie Swarm 0.0.  Handle both here
            return new ThrottlePasses( 1, TimeSpan.FromMilliseconds(500),
                new Sequence(
                    Spell.Buff("Faerie Fire", on => onUnit(on), ret => Required(ret)),
                    new DecoratorContinue( req => HasTalent(DruidTalents.FaerieSwarm), new ActionAlwaysFail())
                    )
                );
        }

        private static bool IsBotPoiWithinMovementBuffRange()
        {
            int minDistKillPoi = 10;
            int minDistOtherPoi = 10;
            int maxDist = Styx.Helpers.CharacterSettings.Instance.MountDistance;

            if (Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Pull || Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.PullBuffs)
                maxDist = Math.Max( 100, Styx.Helpers.CharacterSettings.Instance.MountDistance);

            if (!Me.IsMelee())
                minDistKillPoi += 40;

            if (BotPoi.Current == null || BotPoi.Current.Type == PoiType.None)
                return false;

            double dist = -1;
            if (BotPoi.Current.Type != PoiType.Kill || BotPoi.Current.AsObject.ToUnit() == null)
            {
                dist = Me.Location.Distance(BotPoi.Current.Location);
                if ( dist < minDistOtherPoi)
                    return false;
            }
            else 
            {
                WoWUnit unit = BotPoi.Current.AsObject.ToUnit();
                if (unit.SpellDistance() < minDistKillPoi)
                    return false;
            }

            // always speedbuff if indoors and cannot mount
            if (Me.IsIndoors && !Mount.CanMount())
                return true;

            // always speedbuff if riding not trained yet
            if (Me.GetSkill(SkillLine.Riding).CurrentValue == 0)
                return true;

            // calc distance if we havent already
            if (dist == -1)
                dist = Me.Location.Distance(BotPoi.Current.Location);

            // speedbuff if dist within maxdist
            if (dist <= maxDist)
                return true;

            // otherwise no speedbuff wanted
            return false;
        }

        public static Composite CreateDruidMovementBuff()
        {

            return new Throttle( 5,
                new Decorator(
                    req =>  !Spell.IsCastingOrChannelling() && !Spell.IsGlobalCooldown()
                        && MovementManager.IsClassMovementAllowed
                        && SingularRoutine.CurrentWoWContext != WoWContext.Instances
                        && Me.IsMoving 
                        && Me.IsAlive
                        && !Me.OnTaxi 
                        && !Me.InVehicle 
                        && !Me.IsOnTransport
                        && !Utilities.EventHandlers.IsShapeshiftSuppressed
                        && BotPoi.Current != null
                        && BotPoi.Current.Type != PoiType.None
                        && BotPoi.Current.Type != PoiType.Hotspot
                        && !Me.IsAboveTheGround()
                        ,
                    new Sequence(
                        new PrioritySelector(
                            new Decorator(
                                ret => DruidSettings.UseTravelForm
                                    && !Me.Mounted
                                    && !Me.IsSwimming 
                                    && !Me.HasAnyShapeshift( ShapeshiftForm.Travel, ShapeshiftForm.FlightForm, ShapeshiftForm.EpicFlightForm)
                                    && SpellManager.HasSpell("Cat Form")
                                    && IsBotPoiWithinMovementBuffRange(),
                                new Sequence(
                                    new Action(r => Logger.WriteDebug("DruidMoveBuff: poitype={0} poidist={1:F1} indoors={2} canmount={3} riding={4}",
                                        BotPoi.Current.Type,
                                        BotPoi.Current.Location.Distance(Me.Location),
                                        Me.IsIndoors.ToYN(),
                                        Mount.CanMount().ToYN(),
                                        Me.GetSkill(SkillLine.Riding).CurrentValue
                                        )),
                                    new PrioritySelector(
                                        Spell.BuffSelf("Travel Form", req => Me.IsOutdoors && BotPoi.Current.Type != PoiType.Kill),
                                        Spell.BuffSelf("Cat Form")
                                        )
                                    )
                                ),
                            new Decorator( 
                                req => AllowAquaticForm 
                                    && BotPoi.Current.Location.Distance(Me.Location) >= 10
                                    && Me.Shapeshift != ShapeshiftForm.Aqua
                                    && Spell.CanCastHack("Aquatic Form", Me, false), 
                                Spell.BuffSelf( "Aquatic Form")
                                )
                            ),

                            Helpers.Common.CreateWaitForLagDuration()
                        )
                    )
                );
        }

        public static bool AllowAquaticForm
        {
            get
            {
                const int ABYSSAL_SEAHORSE = 75207;
                const int SUBDUED_SEAHORSE = 98718;
                const int SEA_TURTLE = 64731;

                if (!DruidSettings.UseAquaticForm)
                    return false;

                if (!Me.IsSwimming)
                    return false;

                if (Me.Shapeshift != ShapeshiftForm.Aqua)
                {
                    if (Me.Combat)
                    return false;

                    if (!SpellManager.HasSpell("Aquatic Form"))
                        return false;

                    MirrorTimerInfo breath = StyxWoW.Me.GetMirrorTimerInfo(MirrorTimerType.Breath);
                    if (!breath.IsVisible)
                    {
                        if (Me.Mounted && (Me.MountDisplayId == ABYSSAL_SEAHORSE || Me.MountDisplayId == SUBDUED_SEAHORSE || Me.MountDisplayId == SEA_TURTLE))
                            return false;
                    }

                    Mount.MountWrapper mount = Mount.Mounts.FirstOrDefault(m => m.IsSummoned);
                    Logger.WriteDebug("DruidSwimBuff: breathvisible={0} canmount={1} mounted={2} mountdispid={3} creatureid={4} spellid={5} name={6} type={7}",
                        breath.IsVisible.ToYN(),
                        Mount.CanMount().ToYN(),
                        Me.Mounted.ToYN(),
                        Me.MountDisplayId,
                        mount == null ? 0 : mount.CreatureId,
                        mount == null ? 0 : mount.CreatureSpellId,
                        mount == null ? "n/a" : mount.Name,
                        mount == null ? "n/a" : mount.Type.ToString()
                        );
                }

                return true;
            }
        }

        public static Composite CreateMoveBehindTargetWhileProwling()
        {
            return new Decorator(
                req => DruidSettings.MoveBehindTargets && Me.HasAura("Prowl"),
                Movement.CreateMoveBehindTargetBehavior()
                );
        }
        

        #region Symbiosis Support

        private static WoWUnit _targetSymb;
        // private static HashSet<string> _preSymb = null;
        public static WoWClass SymbiosisWithClass = WoWClass.None;

        public static Composite CreateDruidCastSymbiosis(UnitSelectionDelegate onUnit)
        {
            return new Decorator(
                ret => DruidSettings.UseSymbiosis
                    && !Me.IsMoving
                    && (SingularRoutine.CurrentWoWContext == WoWContext.Instances || (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds && PVP.PrepTimeLeft < secsBeforeBattle))
                    && SpellManager.HasSpell("Symbiosis")
                    && !Me.HasAura("Symbiosis"),
                new Sequence(
                    new Action(r => _targetSymb = onUnit(r)),
                    new Decorator(
                        ret => _targetSymb != null,
                        new Sequence(
                            // new Action( r => _preSymb = new HashSet<string>( SpellManager.Spells.Select( kvp => kvp.Value.Name).ToArray()) ),
                            new Action(r => _targetSymb.Target()),
                            new Wait(1, until => Me.CurrentTargetGuid == _targetSymb.Guid, new ActionAlwaysSucceed()),
                            Spell.Cast("Symbiosis", mov => false, on => _targetSymb, ret => _targetSymb.Distance < 30, cancel => false, LagTolerance.Yes ),
                            new Action(r => Blacklist.Add(_targetSymb.Guid, BlacklistFlags.Combat, TimeSpan.FromSeconds(30))),
                            new Action(r => Me.ClearTarget()),
                            new Wait( TimeSpan.FromMilliseconds(500), until => Me.HasAura("Symbiosis"), new ActionAlwaysSucceed()),
                            new Action( r => Logger.Write( "^Symbiosis: linked with {0}, gained spell {1}", _targetSymb.SafeName(), GetDruidSymbiosisOverrideName(_targetSymb )))
/*
                            new Action(r => {
                                var newSpell = SpellManager.Spells.Where( kvp => !_preSymb.Contains( kvp.Key )).FirstOrDefault();
                                if ( newSpell.Equals(default(KeyValuePair<string,WoWSpell>)))
                                    Logger.WriteDebug("Symbiosis: unable to find a spell gained ?");
                                else
                                {
                                    Logger.Write("Symbiosis: we gained {0} #{1}", newSpell.Value.Name, newSpell.Value.Id);
                                    Logger.WriteDebug("Symbiosis: CanCast {0} is {1}", newSpell.Value.Name, Spell.CanCastHack(newSpell.Value.Name, true));
                                }
                                })
*/
                            )
                        )
                    )
                );
        }

        public static bool IsValidSymbiosisTarget(WoWPlayer p)
        {
            if (p.IsHorde != Me.IsHorde)
                return false;

            if (p.IsHostile)
                return false;

            if (!p.IsAlive)
                return false;

            if (p.Level < 87)
                return false;

            if (p.Combat)
                return false;

            if (Blacklist.Contains(p.Guid, BlacklistFlags.Combat))
                return false;

            if (p.Distance > 28)
                return false;

            if (p.HasAura("Symbiosis"))
                return false;

            if (!p.InLineOfSpellSight)
                return false;

            return true;
        }

        private static int _secsBeforeBattle = 0;

        public static int secsBeforeBattle
        {
            get
            {
                if (_secsBeforeBattle == 0)
                    _secsBeforeBattle = new Random().Next(8, 14);

                return _secsBeforeBattle;
            }

            set
            {
                _secsBeforeBattle = value;
            }
        }

        public static Composite SymbCast( Symbiosis id, UnitSelectionDelegate on, SimpleBooleanDelegate req)
        {
            return new Decorator(
                ret => on(ret) != null
                    && SpellManager.HasSpell((int)id)
                    && req(ret),
                new Action(ret => 
                    {
                        WoWSpell spell = WoWSpell.FromId((int)id);
                        if (spell == null)
                            return RunStatus.Failure;

                        if (!SymbCanCast(spell, on(ret)))
                            return RunStatus.Failure;

                        // check we can cast it on target
                        // if (!Spell.CanCastHack(spell, on(ret), false, false, true))
                        //     return RunStatus.Failure;

                        Logger.Write(string.Format("*Symbiosis: {0} on {1}", spell.Name, on(ret).SafeName()));
                        if (!SpellManager.Cast(spell, on(ret)))
                        {
                            Logger.WriteDebug(Color.LightPink, "cast of Symbiosis: {0} on {1} failed!", spell.Name, on(ret).SafeName());
                            return RunStatus.Failure;
                        }

                        SingularRoutine.UpdateDiagnosticCastingState();
                        return RunStatus.Success;                     
                    })
                );
        }

        /// <summary>
        /// replacement for Spell.CanCastHack() since that does a lookup on SpellManager.Spells and 
        /// ability gained from Druid does not presently appear in that list after buff established (note: class receiving
        /// Symbiosis from Druid does get updated.)
        /// </summary>
        /// <param name="spell"></param>
        /// <param name="unit"></param>
        /// <returns></returns>

        public static bool SymbCanCast(Symbiosis id, WoWUnit unit)
        {
            WoWSpell spell = WoWSpell.FromId((int)id);
            if (spell == null)
                return false;

            return SymbCanCast(spell, unit);
        }

        private static bool SymbCanCast(WoWSpell spell, WoWUnit unit)
        {
            if (spell.Cooldown)
                return false;

            // check range
            if (unit != null && !spell.IsSelfOnlySpell)
            {
                if (spell.IsMeleeSpell && !unit.IsWithinMeleeRange)
                    return false;
                if (spell.HasRange && (unit.Distance > (spell.MaxRange + unit.CombatReach + 1) || unit.Distance < (spell.MinRange + unit.CombatReach + 1.66666675f)))
                    return false;
                if (!unit.InLineOfSpellSight)
                    return false;
            }

            if ((spell.CastTime != 0u || Spell.IsFunnel(spell)) && Me.IsMoving && !Spell.HaveAllowMovingWhileCastingAura(spell))
                return false;

            if (Me.ChanneledCastingSpellId == 0)
            {
                if (Spell.IsCasting())
                    return false;

                if (spell.Cooldown)
                    return false;
            }

            if (Me.CurrentPower < spell.PowerCost)
                return false;

            return true;
        }

        public static Composite SymbBuff( Symbiosis id, UnitSelectionDelegate on, SimpleBooleanDelegate req)
        {
            return new Decorator(
                ret => on(ret) != null && !Me.HasAura((int) id), 
                SymbCast(id, on, req)
                );
        }

        /// <summary>
        /// definately a hackaround, but until SpellManager dictionary gets updated for Druid when Symbiosis cast we are stuck with it
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        private static string GetDruidSymbiosisOverrideName(WoWUnit target)
        {
            if (!Me.HasAura("Symbiosis"))
                return "-no link-";

            int row = EnumValueRow[target.Class];
            int col = EnumValueColumn[TalentManager.CurrentSpec];
            int offset = (row * 4) + col;
            return SymbSpellNames[offset];
        }

        private static Dictionary<WoWSpec, int> EnumValueColumn = new Dictionary<WoWSpec, int>()
        {
            { WoWSpec.DruidBalance, 0 },
            { WoWSpec.DruidGuardian, 1 },
            { WoWSpec.DruidFeral, 2 },
            { WoWSpec.DruidRestoration, 3 },
        };

        private static Dictionary<WoWClass, int> EnumValueRow = new Dictionary<WoWClass, int>()
        {
            { WoWClass.DeathKnight, 0 },
            { WoWClass.Hunter , 1 },
            { WoWClass.Mage , 2 },
            { WoWClass.Monk , 3 },
            { WoWClass.Paladin , 4 },
            { WoWClass.Priest , 5 },
            { WoWClass.Rogue , 6 },
            { WoWClass.Shaman , 7 },
            { WoWClass.Warlock , 8 },
            { WoWClass.Warrior , 9 },
        };

        private static string[] SymbSpellNames = new string[]
    {
        /*               BALANCE                GUARDIAN               FERAL                 RESTORATION                  */
        /* DK      */    "Anti-Magic Shell"  ,  "Bone Shield"     ,    "Death Coil"      ,   "Icebound Fortitude"     ,
        /* Hunter  */    "Misdirection"    ,    "Ice Trap"        ,    "Play Dead"       ,   "Deterrence"            ,
        /* Mage    */    "Mirror Image"     ,   "Frost Armor"     ,    "Frost Nova"      ,   "Ice Block"              ,
        /* Monk    */    "Grapple Weapon"   ,   "Elusive Brew"    ,    "Clash"          ,    "Fortifying Brew"        ,
        /* Paladin */    "Hammer Of Justice" ,  "Consecration"    ,    "Divine Shield"   ,   "Cleanse"               ,
        /* Priest  */    "Mass Dispel"      ,   "Fear Ward"       ,    "Dispersion"     ,    "Leap Of Faith"           ,
        /* Rogue   */    "Cloak Of Shadows"  ,  "Feint"           ,    "Redirect"       ,    "Evasion"               ,
        /* Shaman  */    "Purge"           ,    "Lightning Shield",    "Feral Spirit"    ,   "Spiritwalker's Grace"    ,
        /* Warlock */    "Unending Resolve" ,   "Life Tap"        ,    "Soul Swap"       ,   "Demonic Circle Teleport" ,
        /* Warrior */    "Intervene"       ,    "Spell Reflection",    "Shattering Blow" ,   "Intimidating Roar"      ,
    };

        #endregion

#if NOT_IN_USE
        public static Composite CreateEscapeFromCc()
        {
            return
                new PrioritySelector(
                    Spell.Cast("Dash",
                               ret =>
                               DruidSettings.PvPRooted &&
                               Me.HasAuraWithMechanic(WoWSpellMechanic.Rooted) &&
                               Me.Shapeshift == ShapeshiftForm.Cat),
                    new Decorator(
                        ret =>
                        (DruidSettings.PvPRooted &&
                         Me.HasAuraWithMechanic(WoWSpellMechanic.Rooted) &&
                         Me.Shapeshift == ShapeshiftForm.Cat && SpellManager.HasSpell("Dash") &&
                         SpellManager.Spells["Dash"].Cooldown),
                        new Sequence(
                            new Action(ret => SpellManager.Cast(WoWSpell.FromId(77764))
                                )
                            )),
                    new Decorator(
                        ret =>
                        (DruidSettings.PvPSnared &&
                         Me.HasAuraWithMechanic(WoWSpellMechanic.Snared) &&
                         !Me.ActiveAuras.ContainsKey("Crippling Poison") &&
                         Me.Shapeshift == ShapeshiftForm.Cat),
                        new Sequence(
                            new Action(ret => Lua.DoString("RunMacroText(\"/Cast !Cat Form\")")
                                )
                            )
                        ),
                    new Decorator(
                        ret =>
                        (DruidSettings.PvPSnared &&
                         Me.HasAuraWithMechanic(WoWSpellMechanic.Snared) &&
                         !Me.ActiveAuras.ContainsKey("Crippling Poison") &&
                         Me.Shapeshift == ShapeshiftForm.Bear),
                        new Sequence(
                            new Action(ret => Lua.DoString("RunMacroText(\"/Cast !Bear Form\")")
                                )
                            )
                        )
                    );
        }

        public static Composite CreateCycloneAdd()
        {
            return
                new PrioritySelector(
                    ctx =>
                    Unit.NearbyUnfriendlyUnits.OrderByDescending(u => u.CurrentHealth).FirstOrDefault(IsViableForCyclone),
                    new Decorator(
                        ret =>
                        ret != null && DruidSettings.PvPccAdd &&
                        Me.ActiveAuras.ContainsKey("Predatory Swiftness") &&
                        Unit.NearbyUnfriendlyUnits.All(u => !u.HasMyAura("Polymorph")),
                        new PrioritySelector(
                            Spell.Buff("Cyclone", ret => (WoWUnit) ret))));
        }

        private static bool IsViableForCyclone(WoWUnit unit)
        {
            if (unit.IsCrowdControlled())
                return false;

            if (unit.CreatureType != WoWCreatureType.Beast && unit.CreatureType != WoWCreatureType.Humanoid)
                return false;

            if (Me.CurrentTarget != null && Me.CurrentTarget == unit)
                return false;

            if (!unit.Combat)
                return false;

            if (!unit.IsTargetingMeOrPet && !unit.IsTargetingMyPartyMember)
                return false;

            if (Me.GroupInfo.IsInParty &&
                Me.PartyMembers.Any(p => p.CurrentTarget != null && p.CurrentTarget == unit))
                return false;

            return true;
        }
#endif

    }
    #region Nested type: Talents

    public enum DruidTalents
    {
        FelineSwiftness = 1,
        DispacerBeast,
        WildCharge,
        YserasGift,
        Renewal,
        CenarionWard,
        FaerieSwarm,
        MassEntanglement,
        Typhoon,
        SoulOfTheForest,
        Incarnation,
        ForceOfNature,
        DisorientingRoar,
        UrsolsVortex,
        MightyBash,
        HeartOfTheWild,
        DreamOfCenarius,
        NaturesVigil
    }

    #endregion

#region Symbiosis Druid Spells Gained

    public enum Symbiosis
    {
        /*               BALANCE                      GUARDIAN                     FERAL                       RESTORATION                  */
        /* DK      */    AntiMagicShell  = 110570,    BoneShield      = 122285,    DeathCoil      = 122282,    IceboundFortitude     = 110575,
        /* Hunter  */    Misdirection    = 110588,    IceTrap         = 110600,    PlayDead       = 110597,    Deterrence            = 110617,
        /* Mage    */    MirrorImage     = 110621,    FrostArmor      = 110694,    FrostNova      = 110693,    IceBlock              = 110696,
        /* Monk    */    GrappleWeapon   = 000000,    ElusiveBrew     = 126543,    Clash          = 126449,    FortifyingBrew        = 126456,
        /* Paladin */    HammerOfJustice = 110698,    Consecration    = 110701,    DivineShield   = 110700,    Cleanse               = 122288,
        /* Priest  */    MassDispel      = 110707,    FearWard        = 110717,    Dispersion     = 110715,    LeapOfFaith           = 110718,
        /* Rogue   */    CloakOfShadows  = 110788,    Feint           = 122289,    Redirect       = 110730,    Evasion               = 110791,
        /* Shaman  */    Purge           = 110802,    LightningShield = 110803,    FeralSpirit    = 110807,    SpiritwalkersGrace    = 110806,
        /* Warlock */    UnendingResolve = 122291,    LifeTap         = 122290,    SoulSwap       = 110810,    DemonicCircleTeleport = 112970,
        /* Warrior */    Intervene       = 122292,    SpellReflection = 113002,    ShatteringBlow = 112997,    IntimidatingRoar      = 113004,
    }

#endregion

}