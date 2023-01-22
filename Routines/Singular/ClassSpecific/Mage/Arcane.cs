using System;
using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;

using Styx.CommonBot;
using Styx.Helpers;


using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using Singular.Settings;
using System.Drawing;

namespace Singular.ClassSpecific.Mage
{
    public class Arcane
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static MageSettings MageSettings { get { return SingularSettings.Instance.Mage(); } }

        #region Normal Rotation

        private static bool useArcaneNow;

        [Behavior(BehaviorType.Pull, WoWClass.Mage, WoWSpec.MageArcane, WoWContext.Normal)]
        public static Composite CreateMageArcaneNormalPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        CreateArcaneDiagnosticOutputBehavior(),

                        Helpers.Common.CreateAutoAttack(true),
                        Helpers.Common.CreateInterruptBehavior(),

                        Common.CreateMagePullBuffs(),
                        Spell.BuffSelf("Arcane Power"),

                        new Action(r =>
                        {
                            useArcaneNow = false;
                            uint ac = Me.GetAuraStacks("Arcane Charge");
                            if (ac >= 4)
                                useArcaneNow = true;
                            else
                            {
                                long ttd = Me.CurrentTarget.TimeToDeath();
                                if (ttd > 6)
                                    useArcaneNow = ac >= 2;
                                else if (ttd > 3)
                                    useArcaneNow = ac >= 1;
                                else
                                    useArcaneNow = ttd >= 0;
                            }
                            return RunStatus.Failure;
                        }),

                        Spell.Cast("Arcane Missiles", ret => useArcaneNow && Me.HasAura("Arcane Missiles!")),
                        Spell.Cast("Arcane Barrage", ret => useArcaneNow),

                        // grinding or questing, if target meets these cast Flame Shock if possible
                        // 1. mob is less than 12 yds, so no benefit from delay in Lightning Bolt missile arrival
                        // 2. area has another player competing for mobs (we want to tag the mob quickly)
                        new Decorator(
                            ret => StyxWoW.Me.CurrentTarget.Distance < 12
                                || ObjectManager.GetObjectsOfType<WoWPlayer>(true, false).Any(p => p.Location.DistanceSqr(StyxWoW.Me.CurrentTarget.Location) <= 40 * 40),
                            new PrioritySelector(
                                Spell.Cast("Fire Blast"),
                                Spell.Cast("Arcane Barrage")
                                )
                            ),

                        Spell.Cast("Arcane Blast"),

                        Spell.Cast("Frostfire Bolt", ret => !SpellManager.HasSpell("Arcane Blast"))
                        )
                    ),

                Movement.CreateMoveToUnitBehavior(on => StyxWoW.Me.CurrentTarget, 39f, 34f)
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Mage, WoWSpec.MageArcane)]
        public static Composite CreateMageArcaneHeal()
        {
            return new PrioritySelector(
                CreateArcaneDiagnosticOutputBehavior("Combat")
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Mage, WoWSpec.MageArcane, WoWContext.Normal )]
        public static Composite CreateMageArcaneNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCastOrChannel(),

                new Decorator( 
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Common.CreateMageAvoidanceBehavior(),

                        Helpers.Common.CreateAutoAttack(true),
                        Helpers.Common.CreateInterruptBehavior(),

                        Spell.BuffSelf("Arcane Power"),
/*
                        new Decorator(
                            ret => !Unit.NearbyUnfriendlyUnits.Any(u => u.DistanceSqr < 10 * 10 && u.IsCrowdControlled()),
                            new PrioritySelector(
                                Spell.BuffSelf("Frost Nova",
                                    ret => Unit.NearbyUnfriendlyUnits.Any(u =>
                                                    u.DistanceSqr <= 8 * 8 && !u.IsFrozen() && !u.Stunned))
                                )),
*/
                        // AoE comes first
                        new Decorator(
                            ret => Spell.UseAOE && Me.Level >= 25 && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3,
                            new PrioritySelector(
                                // Movement.CreateEnsureMovementStoppedBehavior(5f),
                                Spell.Cast("Fire Blast", ret => TalentManager.HasGlyph("Fire Blast") && Me.CurrentTarget.HasAnyAura("Frost Bomb", "Living Bomb", "Nether Tempest")),
                                Spell.CastOnGround("Flamestrike", loc => Me.CurrentTarget.Location),
                                Spell.Cast("Arcane Barrage", ret => Me.HasAura("Arcane Charge", Math.Min(6, Unit.UnfriendlyUnitsNearTarget(10f).Count()))),
                                Spell.Cast("Arcane Explosion", ret => Unit.NearbyUnfriendlyUnits.Count(t => t.Distance <= 10) >= 3),
                                Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 8f, 5f)
                                )
                            ),

                        // Movement.CreateEnsureMovementStoppedBehavior(35f),
                        Common.CreateMagePolymorphOnAddBehavior(),

                        new Action( r => {
                            useArcaneNow = false;
                            uint ac = Me.GetAuraStacks("Arcane Charge");
                            if (ac >= 4)
                                useArcaneNow = true;
                            else
                            {
                                long ttd = Me.CurrentTarget.TimeToDeath();
                                if (ttd > 6)
                                    useArcaneNow = ac >= 2;
                                else if (ttd > 3)
                                    useArcaneNow = ac >= 1;
                                else
                                    useArcaneNow = ttd >= 0;
                            }
                            return RunStatus.Failure;
                            }),

                        // Living Bomb in CombatBuffs()
                        Spell.Cast("Arcane Missiles", ret => useArcaneNow && Me.HasAura("Arcane Missiles!")),
                        Spell.Cast("Arcane Barrage", ret => useArcaneNow),
                        Spell.Cast("Arcane Blast"),

                        Spell.Cast("Frostfire Bolt", ret => !SpellManager.HasSpell("Arcane Blast"))
                        )
                    )
                );
        }

        #endregion

        #region Battleground Rotation
        [Behavior(BehaviorType.Pull|BehaviorType.Combat, WoWClass.Mage, WoWSpec.MageArcane, WoWContext.Battlegrounds)]
        public static Composite CreateArcaneMagePvPPullAndCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCastOrChannel(),

                new Decorator( 
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
       
                        Common.CreateMageAvoidanceBehavior(),

                        Common.CreateMagePullBuffs(),

                        // Defensive stuff
                        Spell.BuffSelf("Ice Block", ret => Me.HealthPercent < 10 && !Me.ActiveAuras.ContainsKey("Hypothermia")),
                        Spell.BuffSelf("Frost Nova", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.Distance <= 11 && !u.TreatAsFrozen())),
                        Common.CreateMagePolymorphOnAddBehavior(),

                        Spell.BuffSelf("Arcane Power"),
                        Spell.BuffSelf("Mirror Image"),
                        Spell.BuffSelf("Flame Orb"),
                        Spell.Cast("Arcane Missiles", ret => Me.HasAura("Arcane Missiles!")),
                        Spell.Cast("Arcane Barrage", ret => Me.GetAuraByName("Arcane Charge") != null && Me.GetAuraByName("Arcane Charge").StackCount >= 4),
                        Spell.Cast("Frostfire Bolt", ret => !SpellManager.HasSpell("Arcane Blast")),
                        Spell.Cast("Arcane Blast")
                        )
                    )
                );
        }
        

        #endregion

        #region Instance Rotation

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Mage, WoWSpec.MageArcane, WoWContext.Instances)]
        public static Composite CreateMageArcaneInstancePullAndCombat()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCastOrChannel(FaceDuring.Yes),

                new Decorator( 
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Helpers.Common.CreateAutoAttack(true),
                        Helpers.Common.CreateInterruptBehavior(),

                        Common.CreateMagePullBuffs(),
                        Spell.BuffSelf("Arcane Power"),

                        // AoE comes first
                        new Decorator(
                            ret => Spell.UseAOE && Me.Level >= 25 && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3,
                            new PrioritySelector(
                                // Movement.CreateEnsureMovementStoppedBehavior(5f),
                                Spell.Cast("Fire Blast", ret => TalentManager.HasGlyph("Fire Blast") && Me.CurrentTarget.HasAnyAura("Frost Bomb", "Living Bomb", "Nether Tempest")),
                                Spell.CastOnGround("Flamestrike", loc => Me.CurrentTarget.Location),
                                Spell.Cast("Arcane Barrage", ret => Me.HasAura( "Arcane Charge", Math.Min( 6, Unit.UnfriendlyUnitsNearTarget(10f).Count()))),
                                Spell.Cast("Arcane Explosion", ret => Unit.NearbyUnfriendlyUnits.Count(t => t.Distance <= 10) >= 3),
                                Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 8f, 5f)
                                )
                            ),

                        // Movement.CreateEnsureMovementStoppedBehavior(35f),

                        // Living Bomb in CombatBuffs()
                        Spell.Cast("Arcane Missiles", ret => Me.HasAura("Arcane Missiles!", 2)),
                        Spell.Cast("Arcane Barrage", ret => Me.GetAuraStacks("Arcane Charge") >= 4),
                        Spell.Cast("Arcane Blast"),

                        Spell.Cast("Frostfire Bolt", ret => !SpellManager.HasSpell("Arcane Blast"))
                        )
                    ),

                Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 39f, 34f)
                );
        }

        #endregion

        #region Diagnostics

        private static Composite CreateArcaneDiagnosticOutputBehavior(string state = null)
        {
            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            return new ThrottlePasses(1, 1,
                new Action(ret =>
                {
                    string line = string.Format(".... [{0}] h={1:F1}%/m={2:F1}%, moving={3}, arcchg={4} {5:F0} ms, arcmiss={6} {7:F0} ms",
                        state ?? Dynamics.CompositeBuilder.CurrentBehaviorType.ToString(),
                        Me.HealthPercent,
                        Me.ManaPercent,
                        Me.IsMoving,
                        Me.GetAuraStacks("Arcane Charge"),
                        Me.GetAuraTimeLeft("Arcane Charge").TotalMilliseconds,
                        Me.GetAuraStacks("Arcane Missiles!"),
                        Me.GetAuraTimeLeft("Arcane Missiles!").TotalMilliseconds
                        );

                    WoWUnit target = Me.CurrentTarget;
                    if (target == null)
                        line += ", target=(null)";
                    else
                        line += string.Format(", target={0} @ {1:F1} yds, h={2:F1}%, face={3}, loss={4}, livbomb={5:F0} ms",
                            target.SafeName(),
                            target.Distance,
                            target.HealthPercent,
                            Me.IsSafelyFacing(target),
                            target.InLineOfSpellSight,
                            target.GetAuraTimeLeft("Living Bomb").TotalMilliseconds 
                            );

                    Logger.WriteDebug(Color.Wheat, line);
                    return RunStatus.Failure;
                })
                );
        }

        #endregion
    }
}
