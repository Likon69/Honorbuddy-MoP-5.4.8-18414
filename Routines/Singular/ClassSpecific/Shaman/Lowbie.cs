using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;

using Styx.TreeSharp;
using Singular.Settings;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using System.Drawing;

namespace Singular.ClassSpecific.Shaman
{
    class Lowbie
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static ShamanSettings ShamanSettings { get { return SingularSettings.Instance.Shaman(); } }

        [Behavior(BehaviorType.Rest, WoWClass.Shaman, 0)]
        public static Composite CreateShamanLowbieRest()
        {
            return Rest.CreateDefaultRestBehaviour("Healing Surge", "Ancestral Spirit");
        }

        [Behavior(BehaviorType.PreCombatBuffs | BehaviorType.CombatBuffs, WoWClass.Shaman, 0)]
        public static Composite CreateShamanLowbiePreCombatBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Lightning Shield")
                );
        }
        [Behavior(BehaviorType.Pull, WoWClass.Shaman, 0)]
        public static Composite CreateShamanLowbiePull()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMediumRange(),
                Spell.WaitForCast(FaceDuring.Yes),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        CreateLowbieDiagnosticOutputBehavior(),
                        Spell.Cast("Earth Shock", ret => Me.CurrentTarget.Combat && Me.CurrentTarget.IsTargetingMeOrPet),
                        Spell.Cast("Lightning Bolt")
                        )
                    )
                );
        }
        [Behavior(BehaviorType.Heal, WoWClass.Shaman, 0)]
        public static Composite CreateShamanLowbieHeal()
        {
            return new PrioritySelector(
                Spell.Cast(
                    "Healing Surge",
                    mov => true,
                    on => Me,
                    req => Me.PredictedHealthPercent(includeMyHeals: true) <= ShamanSettings.SelfHealingSurge,
                    cancel => Me.HealthPercent > 85)
                );
        }
        [Behavior(BehaviorType.Combat, WoWClass.Shaman, 0)]
        public static Composite CreateShamanLowbieCombat()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMediumRange(),
                Spell.WaitForCast(FaceDuring.Yes),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        CreateLowbieDiagnosticOutputBehavior(),
                        Helpers.Common.CreateAutoAttack(true),
                        Spell.Cast("Earth Shock", req => Me.CurrentTarget.Distance < 15 || !Me.CurrentTarget.IsMoving  ),      // always use
                        Spell.Cast("Primal Strike"),    // always use
                        Spell.Cast("Lightning Bolt")
                        )
                    )
                );
        }

        #region Diagnostics

        private static Composite CreateLowbieDiagnosticOutputBehavior()
        {
            return new ThrottlePasses(1, 1,
                new Decorator(
                    ret => SingularSettings.Debug,
                    new Action(ret =>
                    {
                        string line = string.Format(".... h={0:F1}%/m={1:F1}%",
                            Me.HealthPercent,
                            Me.ManaPercent
                            );

                        WoWUnit target = Me.CurrentTarget;
                        if (target == null)
                            line += ", target=(null)";
                        else
                            line += string.Format(", target={0}[{1}] @ {2:F1} yds, th={3:F1}%, tlos={4}, tloss={5}",
                                target.SafeName(),
                                target.Level,
                                target.Distance,
                                target.HealthPercent,
                                target.InLineOfSight,
                                target.InLineOfSpellSight
                                );

                        Logger.WriteDebug(Color.Yellow, line);
                        return RunStatus.Failure;
                    }))
                );
        }

        #endregion
    }
}
