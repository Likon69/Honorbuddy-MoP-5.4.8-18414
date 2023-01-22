using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;

using Styx.CommonBot;
using Styx.TreeSharp;
using Singular.Settings;
using Styx.WoWInternals.WoWObjects;
using System.Drawing;
using Styx.WoWInternals;

namespace Singular.ClassSpecific.Mage
{
    public class Lowbie
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static MageSettings MageSettings { get { return SingularSettings.Instance.Mage(); } }

        [Behavior(BehaviorType.Pull, WoWClass.Mage, 0)]
        public static Composite CreateLowbieMagePull()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),
                Spell.WaitForCastOrChannel(FaceDuring.Yes),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                         CreateLowbieDiagnosticOutputBehavior("Pull"),
                         Helpers.Common.CreateInterruptBehavior(),
                         Common.CreateMagePolymorphOnAddBehavior(),

                         Spell.BuffSelf("Frost Nova", ret => LowbieNeedsFrostNova),
                         // only Fire Blast if already in Combat
                         Spell.Cast("Fire Blast", ret => Me.CurrentTarget.Combat && Me.CurrentTarget.IsTargetingMeOrPet),
                         // otherwise take advantage of casting without incoming damage
                         Spell.Cast("Frostfire Bolt")
                         )
                     )
                 );
        }

        private static bool LowbieNeedsFrostNova
        {
            get {
                return Unit.UnfriendlyUnits(12).Any(u => u.IsHostile || (u.Combat && u.IsTargetingMyStuff()))
                    && !Unit.UnfriendlyUnits(12).Any(u => !u.Combat && u.IsNeutral); 
            }
        }

        [Behavior(BehaviorType.Heal, WoWClass.Mage, 0)]
        public static Composite CreateLowbieMageHeal()
        {
            return CreateLowbieDiagnosticOutputBehavior("Combat");
        }

        [Behavior(BehaviorType.Combat, WoWClass.Mage, 0)]
        public static Composite CreateLowbieMageCombat()
        {
            return new PrioritySelector(
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Helpers.Common.EnsureReadyToAttackFromLongRange(),
                Spell.WaitForCastOrChannel(FaceDuring.Yes),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                         Helpers.Common.CreateInterruptBehavior(),
                         Common.CreateMageAvoidanceBehavior(),
                         Common.CreateMagePolymorphOnAddBehavior(),

                         Spell.BuffSelf("Frost Nova", ret => LowbieNeedsFrostNova),
                         Spell.Cast("Fire Blast"),
                         Spell.Cast("Frostfire Bolt")
                         )
                     )
                 );
        }

        #region Diagnostics

        private static Composite CreateLowbieDiagnosticOutputBehavior(string s)
        {
            return new Decorator(
                ret => SingularSettings.Debug,
                new Throttle(1,
                    new Action(ret =>
                    {
                        string log;

                        log = string.Format(".... [{0}] h={1:F1}%/m={2:F1}%",
                            s,
                            Me.HealthPercent,
                            Me.ManaPercent
                            );

                        WoWUnit target = Me.CurrentTarget;
                        if (target != null)
                        {
                            log += string.Format(", ttd={0}, th={1:F1}%, dist={2:F1}, face={3}, loss={4}, ffire={5}, slowed={6}, frozen={7}",
                                target.TimeToDeath(),
                                target.HealthPercent,
                                target.Distance,
                                Me.IsSafelyFacing(target),
                                target.InLineOfSpellSight,
                                (long)target.GetAuraTimeLeft("Frostfire Bolt", true).TotalMilliseconds,
                                target.IsSlowed().ToYN(),
                                target.TreatAsFrozen().ToYN()
                                );

                            if (target.HasAura("Frost Nova"))
                                log += string.Format(", frostnova={0}", (long)target.GetAuraTimeLeft("Frost Nova", true).TotalMilliseconds);

                            foreach (WoWAura aura in target.GetAllAuras())
                            {
                                foreach (var se in aura.Spell.SpellEffects)
                                {
                                    Logger.WriteDebug("Diag: {0} #{1}, auratype={2} effectype={3}", aura.Name, aura.SpellId, se.AuraType.ToString(), se.EffectType.ToString());
                                }
                            }
                        }

                        Logger.WriteDebug(Color.AntiqueWhite, log);
                    })
                    )
                );
        }

        #endregion
    }
}
