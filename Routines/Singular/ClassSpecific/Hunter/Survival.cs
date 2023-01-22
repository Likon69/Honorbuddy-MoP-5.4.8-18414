using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.CommonBot;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using Styx.WoWInternals.WoWObjects;
using System.Drawing;
using Styx.WoWInternals;

namespace Singular.ClassSpecific.Hunter
{
    public class Survival
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WoWUnit Pet { get { return StyxWoW.Me.Pet; } }
        private static HunterSettings HunterSettings { get { return SingularSettings.Instance.Hunter(); } }

        #region Normal Rotation
        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Hunter, WoWSpec.HunterSurvival, WoWContext.Normal | WoWContext.Instances )]
        public static Composite CreateHunterSurvivalNormalPullAndCombat()
        {
            return new PrioritySelector(

                Common.CreateHunterEnsureReadyToAttackFromLongRange(),

                Spell.WaitForCastOrChannel(),

                new Decorator(

                    ret => !Spell.IsGlobalCooldown(),

                    new PrioritySelector(

                        // updated time to death tracking values before we need them
                        new Action( ret => { Me.CurrentTarget.TimeToDeath(); return RunStatus.Failure; } ),

                        CreateSurvivalDiagnosticOutputBehavior(),

                        Common.CreateMisdirectionBehavior(),
                        // Spell.Buff("Hunter's Mark", ret => Unit.ValidUnit(Me.CurrentTarget) && !TalentManager.HasGlyph("Marked for Death") && !Me.CurrentTarget.IsImmune(WoWSpellSchool.Arcane)),

                        Common.CreateHunterAvoidanceBehavior(null, null),

                        Helpers.Common.CreateInterruptBehavior(),

                        Common.CreateHunterNormalCrowdControl(),

                        Spell.Cast("Tranquilizing Shot", ctx => Me.CurrentTarget.HasAura("Enraged")),

                        Spell.Buff("Concussive Shot",
                            ret => Me.CurrentTarget.CurrentTargetGuid == Me.Guid
                                && Me.CurrentTarget.Distance > Spell.MeleeRange),

                        // Defensive Stuff
                        Spell.Cast("Intimidation",
                            ret => Me.GotTarget
                                && Me.CurrentTarget.IsAlive
                                && Me.GotAlivePet
                                && (!Me.CurrentTarget.GotTarget || Me.CurrentTarget.CurrentTarget == Me)),

                        // AoE Rotation
                        new Decorator(
                            ret => Spell.UseAOE && !(Me.CurrentTarget.IsBoss() || Me.CurrentTarget.IsPlayer) && Unit.UnfriendlyUnitsNearTarget(8f).Count() >= 3,
                            new PrioritySelector(
                                Spell.Cast("Kill Shot", onUnit => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.HealthPercent < 20 && u.Distance < 40 && u.InLineOfSpellSight && Me.IsSafelyFacing(u))),
                                Common.CreateHunterTrapBehavior("Explosive Trap", true, on => Me.CurrentTarget, ret => true),
                                Spell.Cast("Multi-Shot"),
                                Spell.Cast("Explosive Shot"),
                                Spell.Cast("Cobra Shot"),
                                Common.CastSteadyShot(on => Me.CurrentTarget, ret => !SpellManager.HasSpell("Cobra Shot"))
                                )
                            ),

                        // Single Target Rotation
                        Spell.Buff("Serpent Sting"),
                        Spell.Cast("Explosive Shot"),
                        Spell.Cast("Kill Shot", ctx => Me.CurrentTarget.HealthPercent < 20),
                        Spell.Cast("Black Arrow", ret => Me.CurrentTarget.TimeToDeath() > 12 ),
                        Spell.Cast("Arcane Shot", ret => Me.CurrentFocus > 60 || Me.HasAura("Thrill of the Hunt")),
                        Spell.Cast("Cobra Shot"),
                        Common.CastSteadyShot( on => Me.CurrentTarget, ret => !SpellManager.HasSpell("Cobra Shot"))
                        )
                    ),

                Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 35f, 30f)
                );
        }

        #endregion

        #region Battleground Rotation

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Hunter, WoWSpec.HunterSurvival, WoWContext.Battlegrounds)]
        public static Composite CreateHunterSurvivalPvPPullAndCombat()
        {
            return new PrioritySelector(

                Common.CreateHunterEnsureReadyToAttackFromLongRange(),

                Spell.WaitForCastOrChannel(),

                new Decorator(

                    ret => !Spell.IsGlobalCooldown(),

                    new PrioritySelector(

                        // updated time to death tracking values before we need them
                        new Action(ret => { Me.CurrentTarget.TimeToDeath(); return RunStatus.Failure; }),

                        CreateSurvivalDiagnosticOutputBehavior(),

                        Common.CreateHunterAvoidanceBehavior(null, null),

                        Helpers.Common.CreateInterruptBehavior(),

                        Common.CreateHunterPvpCrowdControl(),                      

                        Spell.Cast("Tranquilizing Shot", ctx => Me.CurrentTarget.HasAura("Enraged")),

                        Spell.Buff("Concussive Shot",
                            ret => Me.CurrentTarget.CurrentTargetGuid == Me.Guid
                                && Me.CurrentTarget.Distance > Spell.MeleeRange),

                        // Defensive Stuff
                        Spell.Cast("Intimidation",
                            ret => Me.GotTarget
                                && Me.CurrentTarget.IsAlive
                                && Me.GotAlivePet
                                && (!Me.CurrentTarget.GotTarget || Me.CurrentTarget.CurrentTarget == Me)),

                        // Single Target Rotation
                        Spell.Buff("Serpent Sting"),
                        Spell.Cast("Explosive Shot"),
                        Spell.Cast("Kill Shot", ctx => Me.CurrentTarget.HealthPercent < 20),
                        Spell.Cast("Black Arrow", ret => Me.CurrentTarget.TimeToDeath() > 12),
                        Spell.Cast("Arcane Shot", ret => Me.CurrentFocus > 60 || Me.HasAura("Thrill of the Hunt")),
                        Spell.Cast("Cobra Shot"),
                        Common.CastSteadyShot(on => Me.CurrentTarget, ret => !SpellManager.HasSpell("Cobra Shot"))
                        )
                    )
                );
        }

        #endregion

        private static Composite CreateSurvivalDiagnosticOutputBehavior()
        {
            return new Decorator(
                ret => SingularSettings.Debug,
                new Throttle( 1,
                    new Action(ret =>
                    {
                        string sMsg;
                        sMsg = string.Format(".... h={0:F1}%, focus={1:F1}, moving={2}",
                            Me.HealthPercent,
                            Me.CurrentFocus,
                            Me.IsMoving
                            );

                        if ( !Me.GotAlivePet)
                            sMsg += ", no pet";
                        else
                            sMsg += string.Format( ", peth={0:F1}%", Me.Pet.HealthPercent);

                        WoWUnit target = Me.CurrentTarget;
                        if (target != null)
                        {
                            sMsg += string.Format(
                                ", {0}, {1:F1}%, {2:F1} yds, loss={3}",
                                target.SafeName(),
                                target.HealthPercent,
                                target.Distance,
                                target.InLineOfSpellSight
                                );
                        }

                        Logger.WriteDebug(Color.LightYellow, sMsg);
                        return RunStatus.Failure;
                    })
                    )
                );
        }

    }
}
