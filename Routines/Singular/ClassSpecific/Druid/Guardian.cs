using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Settings;
using Styx;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using System.Drawing;

using Action = Styx.TreeSharp.Action;
using Singular.Managers;

namespace Singular.ClassSpecific.Druid
{
    class Guardian
    {
        private static DruidSettings Settings
        {
            get { return SingularSettings.Instance.Druid(); }
        }

        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        #region Common

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.All)]
        public static Composite CreateGuardianNormalPreCombatBuffs()
        {
            return new PrioritySelector();
        }

        [Behavior(BehaviorType.Pull, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.All)]
        public static Composite CreateGuardianNormalPull()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                // Auto Attack
                Helpers.Common.CreateAutoAttack(false),

                Spell.WaitForCast(FaceDuring.Yes),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        //Shoot flying targets
                        new Decorator(
                            ret => Me.CurrentTarget.IsFlying || !Styx.Pathing.Navigator.CanNavigateFully(Me.Location, Me.CurrentTarget.Location),
                            new PrioritySelector(
                                Spell.Cast("Moonfire"),
                                Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 27f, 22f)
                                )
                            ),

                        Spell.BuffSelf("Bear Form", req => !Utilities.EventHandlers.IsShapeshiftSuppressed),
                        CreateGuardianWildChargeBehavior(),
                        Common.CreateFaerieFireBehavior( on => Me.CurrentTarget, req => true)
                        )
                    )
                );
        }

        #endregion

        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidGuardian, priority: 99)]
        public static Composite CreateDruidNonRestoHeal()
        {
            return new PrioritySelector(
                CreateGuardianDiagnosticOutputBehavior( "Combat")
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.All, 1)]
        public static Composite CreateGuardianNormalCombatBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Bear Form", req => !Utilities.EventHandlers.IsShapeshiftSuppressed),

                new Decorator(
                    req => Me.GotTarget && !Me.CurrentTarget.IsTrivial(),
                    new PrioritySelector(
                        // Enrage ourselves back up to 60 rage for SD/FR usage.
                        Spell.BuffSelf("Enrage", ret=>StyxWoW.Me.RagePercent <= 40),

                        // Symbiosis
                        Common.SymbCast(Symbiosis.BoneShield, on => Me, ret => !Me.HasAura("Bone Shield")),
                        Common.SymbCast(Symbiosis.ElusiveBrew, on => Me, ret => StyxWoW.Me.HealthPercent <= 60 && !Me.HasAura("Elusive Brew")),
                        Common.SymbCast(Symbiosis.SpellReflection, on => Me, ret => Unit.NearbyUnfriendlyUnits.Any(u => u.IsCasting && u.CurrentTargetGuid == Me.Guid && u.CurrentCastTimeLeft.TotalMilliseconds.Between(200,2000))),
                        // Common.SymbCast(Symbiosis.LightningShield, on => Me, ret => !Me.HasAura("Lightning Shield")),
                        Common.SymbCast(Symbiosis.FrostArmor, on => Me, ret => Me.GotTarget && Me.CurrentTarget.IsPlayer && !Me.HasAura("Frost Armor"))
                        )
                    ),

                Spell.BuffSelf("Frenzied Regeneration", ret => Me.HealthPercent < Settings.TankFrenziedRegenerationHealth && Me.CurrentRage >=60),
                Spell.BuffSelf("Frenzied Regeneration", ret => Me.HealthPercent < 30 && Me.CurrentRage >= 15),
                Spell.BuffSelf("Savage Defense", ret => Me.HealthPercent <= Settings.TankSavageDefense),
                Spell.BuffSelf("Might of Ursoc", ret => Me.HealthPercent <= Settings.TankMightOfUrsoc),
                Spell.BuffSelf("Survival Instincts", ret => Me.HealthPercent <= Settings.TankSurvivalInstinctsHealth),
                Spell.BuffSelf("Barkskin", ret => Me.HealthPercent <= Settings.TankFeralBarkskin),
                Spell.Cast("Renewal", on => Me, ret => Me.HealthPercent <= Settings.SelfRenewalHealth)
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.All)]
        public static Composite CreateGuardianNormalCombat()
        {
            TankManager.NeedTankTargeting = (SingularRoutine.CurrentWoWContext == WoWContext.Instances);

           // Logger.Write("guardian loop.");
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                CreateGuardianWildChargeBehavior(),
                Helpers.Common.CreateAutoAttack(false),

                Spell.WaitForCast(FaceDuring.Yes),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Helpers.Common.CreateInterruptBehavior(),

                        CreateGuardianTauntBehavior(),

                        Spell.Cast("Maul", ret => Me.CurrentRage >= 90 && StyxWoW.Me.HasAura("Tooth and Claw")),

                        Spell.Cast("Mangle"),
                        Spell.Cast("Thrash", req => Me.CurrentTarget.HasAuraExpired("Thrash",1) || Me.CurrentTarget.HasAuraExpired("Weakened Blows", 1)),

                        Spell.Cast("Bear Hug", 
                            ret => SingularRoutine.CurrentWoWContext != WoWContext.Instances
                                && !Me.HasAura("Berserk") 
                                && !Unit.NearbyUnfriendlyUnits.Any(u => u.Guid != Me.CurrentTargetGuid && u.CurrentTargetGuid == Me.Guid)),

                        new Decorator(
                            ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance < 8) >= 2,
                            new PrioritySelector(
                                Spell.Cast("Berserk"),
                                Spell.Cast("Thrash"),
                                Spell.Cast("Swipe")
                                )
                            ),
                        Spell.Cast("Lacerate"),
                        Common.CreateFaerieFireBehavior( on => Me.CurrentTarget, req => true),

                        // Symbiosis
                        Common.SymbCast(Symbiosis.Consecration, on => Me, req => Me.CurrentTarget.SpellDistance() < 8),

                        Spell.Cast("Maul", ret => Me.CurrentTarget.CurrentTargetGuid != Me.Guid || SingularRoutine.CurrentWoWContext != WoWContext.Instances),

                        CreateGuardianWildChargeBehavior()
                        )
                    )
            );
        }

        private static Composite CreateGuardianTauntBehavior()
        {
            if ( !SingularSettings.Instance.EnableTaunting )
                return new ActionAlwaysFail();

            return new Decorator(
                ret => TankManager.Instance.NeedToTaunt.Any()
                    && TankManager.Instance.NeedToTaunt.FirstOrDefault().InLineOfSpellSight,
                new Throttle(TimeSpan.FromMilliseconds(1500),
                    new PrioritySelector(
                // Direct Taunt
                        Spell.Cast("Growl",
                            ctx => TankManager.Instance.NeedToTaunt.FirstOrDefault(),
                            ret => true),

                        new Decorator(
                            ret => TankManager.Instance.NeedToTaunt.Any()   /*recheck just before referencing member*/
                                && Me.SpellDistance(TankManager.Instance.NeedToTaunt.FirstOrDefault()) > 10,

                            new PrioritySelector(
                                CreateGuardianWildChargeBehavior(on => TankManager.Instance.NeedToTaunt.FirstOrDefault())
                                )
                            )
                        )
                    )
                );

        }

        private static Throttle CreateGuardianWildChargeBehavior( UnitSelectionDelegate onUnit = null)
        {
            return new Throttle(7,
                new Sequence(
                    Spell.CastHack("Wild Charge", onUnit ?? (on => Me.CurrentTarget), ret => MovementManager.IsClassMovementAllowed && (Me.CurrentTarget.Distance + Me.CurrentTarget.CombatReach).Between( 10, 25)),
                    new Action( ret => StopMoving.Clear() ),
                    new Wait(1, until => !Me.GotTarget || Me.CurrentTarget.IsWithinMeleeRange, new ActionAlwaysSucceed())
                    )
                );
        }


        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.Instances, 2)]
        public static Composite CreateGuardianPreCombatBuffForSymbiosisInstances(UnitSelectionDelegate onUnit)
        {
            return Common.CreateDruidCastSymbiosis(on => GetGuardianBestSymbiosisTargetInstances());
        }

        private static WoWPlayer GetGuardianBestSymbiosisTargetInstances()
        {
            return Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.DeathKnight)     // bone shield
                ?? (Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Paladin)        // consecration
                    ?? (Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Warrior)    // spell reflect
                        ?? Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Monk)    // evasive brew
                        )
                    );
        }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.Battlegrounds, 2)]
        public static Composite CreateGuardianPreCombatBuffForSymbiosisPvp(UnitSelectionDelegate onUnit)
        {
            return Common.CreateDruidCastSymbiosis(on => GetGuardianBestSymbiosisTargetPVP());
        }

        private static WoWPlayer GetGuardianBestSymbiosisTargetPVP()
        {
            return Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Warrior)             // spell reflect
                ?? (Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Mage)               // frost armor
                    ?? (Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.DeathKnight)    // bone shield
                        ?? (Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Paladin)    // consecration
                            ?? Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Monk)    // evasive brew
                            )
                        )
                    );
        }

        #region Diagnostics

        private static Composite CreateGuardianDiagnosticOutputBehavior(string context = null)
        {
            if (context == null)
                context = "...";
            else
                context = "<<" + context + ">>";

            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            return new ThrottlePasses(1,
                new Action(ret =>
                {
                    string log;
                    log = string.Format(context + " h={0:F1}%/rage={1:F1}%/mana={2:F1}%, shape={3}, savage={4}, tooclaw={5}, brsrk={6}",
                        Me.HealthPercent,
                        Me.RagePercent,
                        Me.ManaPercent,
                        Me.Shapeshift.ToString().Length < 4 ? Me.Shapeshift.ToString() : Me.Shapeshift.ToString().Substring(0, 4),
                        (long)Me.GetAuraTimeLeft("Savage Defense", true).TotalMilliseconds,
                        (long)Me.GetAuraTimeLeft("Tooth and Claw", true).TotalMilliseconds,
                        (long)Me.GetAuraTimeLeft("Berserk", true).TotalMilliseconds
                        );

                    WoWUnit target = Me.CurrentTarget;
                    if (target != null)
                    {
                        log += string.Format(", th={0:F1}%, dist={1:F1}, inmelee={2}, face={3}, loss={4}, dead={5} secs, lacerat={6}, thrash={7}, weakarmor={8}",
                            target.HealthPercent,
                            target.Distance,
                            target.IsWithinMeleeRange.ToYN(),
                            Me.IsSafelyFacing(target).ToYN(),
                            target.InLineOfSpellSight.ToYN(),
                            target.TimeToDeath(),
                            (long)target.GetAuraTimeLeft("Rake", true).TotalMilliseconds,
                            (long)target.GetAuraTimeLeft("Lacerate", true).TotalMilliseconds,
                            (long)target.GetAuraTimeLeft("Weakened Armor", true).TotalMilliseconds
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