using System;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;

using Styx.CommonBot;
using Styx.TreeSharp;
using CommonBehaviors.Actions;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using System.Drawing;

namespace Singular.ClassSpecific.Rogue
{
    public class Subtlety
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static RogueSettings RogueSettings { get { return SingularSettings.Instance.Rogue(); } }
        private static bool HasTalent(RogueTalents tal) { return TalentManager.IsSelected((int)tal); } 

        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Rogue, WoWSpec.RogueSubtlety, WoWContext.Normal | WoWContext.Battlegrounds | WoWContext.Instances)]
        public static Composite CreateRogueSubtletyNormalPull()
        {
            return new PrioritySelector(
                Helpers.Common.CreateDismount("Pulling"),
                Common.CreateRoguePullBuffs(),      // needed because some Bots not calling this behavior
                Safers.EnsureTarget(),
                Common.CreateRoguePullSkipNonPickPocketableMob(),
                Common.CreateRogueControlNearbyEnemyBehavior(),
                Common.CreateRogueMoveBehindTarget(),
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),
                    
                new Decorator(
                    ret => !Spell.IsGlobalCooldown() && Me.GotTarget && Me.IsSafelyFacing(Me.CurrentTarget),
                    new PrioritySelector(

                        CreateSubteltyDiagnosticOutputBehavior("Pull"),

                        Common.CreateRoguePullPickPocketButDontAttack(),

                        Common.CreateRogueOpenerBehavior(),
                        Common.CreatePullMobMovingAwayFromMe(),
                        Common.CreateAttackFlyingOrUnreachableMobs(),

                        // ok, everything else failed so just hit him!!!!
                        Spell.OffGCD(Spell.Buff("Premeditation", req => Common.IsStealthed && Me.ComboPoints < 4 && Me.IsWithinMeleeRange)),
                        Spell.Cast("Hemorrhage")
                        )
                    )
                );
        }
        [Behavior(BehaviorType.Combat, WoWClass.Rogue, WoWSpec.RogueSubtlety, WoWContext.Normal | WoWContext.Battlegrounds)]
        public static Composite CreateRogueSubtletyNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateRogueMoveBehindTarget(),
                Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCastOrChannel(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        // updated time to death tracking values before we need them
                        new Action(ret => { Me.CurrentTarget.TimeToDeath(); return RunStatus.Failure; }),

                        Helpers.Common.CreateInterruptBehavior(),
                        Common.CreateDismantleBehavior(),

                        Common.CreateRogueOpenerBehavior(),

                        Spell.Buff("Premeditation", req => Common.IsStealthed && Me.ComboPoints <= 3),

                        new Decorator(
                            ret => Common.AoeCount > 1 && !Me.CurrentTarget.IsPlayer,
                            new PrioritySelector(
                                Spell.BuffSelf("Shadow Dance", ret => Common.AoeCount >= 3),
                                Spell.Cast("Eviscerate", ret => Me.ComboPoints >= 5 && Common.AoeCount < 7 && !Me.CurrentTarget.HasAuraExpired("Crimson Tempest", 7)),
                                Spell.Cast("Crimson Tempest", ret => Me.ComboPoints >= 5),
                                Spell.BuffSelf("Fan of Knives", ret => Common.AoeCount >= RogueSettings.FanOfKnivesCount )
                                )
                            ),
                        new Decorator(
                            ret => Common.AoeCount >= 3 && !Me.CurrentTarget.IsPlayer,
                            new PrioritySelector(
                                Spell.Cast("Slice and Dice", on => Me, ret => Me.ComboPoints > 0 && Me.HasAuraExpired("Slice and Dice", 2)),
                                Spell.Cast("Crimson Tempest", ret => Me.ComboPoints >= 5),
                                Spell.BuffSelf("Fan of Knives", ret => Common.AoeCount >= RogueSettings.FanOfKnivesCount ),
                                Spell.Cast("Hemorrhage", ret => !SpellManager.HasSpell("Fan of Knives")),
                                Movement.CreateMoveToMeleeBehavior(true)
                                )
                            ),

                        Spell.BuffSelf("Shadow Dance",
                            ret => Me.GotTarget
                                && !Common.IsStealthed
                                && !Me.HasAuraExpired("Slice and Dice", 3)
                                && Me.ComboPoints < 2),

                        Spell.Cast("Slice and Dice", on => Me, ret => Me.ComboPoints > 0 && Me.HasAuraExpired("Slice and Dice", 2)),
                        Spell.Buff("Rupture", true, ret => Me.ComboPoints >= 5),
                        Spell.Cast("Eviscerate", ret => Me.ComboPoints >= 5),

                        // lets try a big hit if stealthed and behind before anything
                        Spell.Cast(sp => "Ambush", chkMov => false, on => Me.CurrentTarget, req => Common.IsAmbushNeeded(), canCast: Common.RogueCanCastOpener),

                        Spell.Buff("Hemorrhage"),
                        Spell.Cast("Backstab", ret => Me.IsSafelyBehind(Me.CurrentTarget) && Common.HasDaggerInMainHand),
                        Spell.BuffSelf("Fan of Knives", ret => !Me.CurrentTarget.IsPlayer && Common.AoeCount >= RogueSettings.FanOfKnivesCount),

                // following cast is as a Combo Point builder if we can't cast Backstab
                        Spell.Cast("Hemorrhage", ret => Me.CurrentEnergy >= 35 || !SpellManager.HasSpell("Backstab") || !Me.IsSafelyBehind(Me.CurrentTarget)),

                        Common.CheckThatDaggersAreEquippedIfNeeded()
                        )
                    )
                );
        }

        #endregion

        #region Instance Rotation

        [Behavior(BehaviorType.Combat, WoWClass.Rogue, WoWSpec.RogueSubtlety, WoWContext.Instances)]
        public static Composite CreateRogueSubtletyInstanceCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateRogueMoveBehindTarget(),
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                
                Spell.WaitForCastOrChannel(),
                new Decorator(
                    ret => Me.GotTarget && !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        // updated time to death tracking values before we need them
                        new Action(ret => { Me.CurrentTarget.TimeToDeath(); return RunStatus.Failure; }),
                        Helpers.Common.CreateInterruptBehavior(),
                        Common.CreateDismantleBehavior(),

                        Spell.Buff("Premeditation", req => Common.IsStealthed && Me.ComboPoints <= 3),

                        new Decorator(
                            ret => Common.AoeCount >= 3 && Spell.UseAOE,
                            new PrioritySelector(
                                Spell.Cast("Slice and Dice", on => Me, ret => Me.ComboPoints > 0 && Me.HasAuraExpired("Slice and Dice", 2)),
                                Spell.Cast("Crimson Tempest", ret => Me.ComboPoints >= 5),
                                Spell.BuffSelf("Fan of Knives", ret => Common.AoeCount >= RogueSettings.FanOfKnivesCount)
                                )
                            ),

                        Spell.BuffSelf("Shadow Dance", ret => Me.CurrentTarget.MeIsBehind && !Me.HasAura("Stealth")),
                        Spell.BuffSelf("Vanish", ret => Me.CurrentTarget.IsBoss() && Me.CurrentTarget.MeIsBehind),

                        Spell.Cast("Slice and Dice", on => Me, ret => Me.ComboPoints >= (Me.CurrentTarget.IsBoss() ? 5 : 1) && Me.HasAuraExpired("Slice and Dice", 2)),
                        Spell.Buff("Rupture", true, ret => Me.ComboPoints == 5),
                        Spell.Cast("Eviscerate", ret => Me.ComboPoints == 5),

                        Spell.Cast(sp => "Ambush", chkMov => false, on => Me.CurrentTarget, req => Common.IsAmbushNeeded(), canCast: Common.RogueCanCastOpener),
                        Spell.Buff("Hemorrhage"),
                        Spell.Cast("Backstab", ret => Me.CurrentTarget.MeIsBehind && Common.HasDaggerInMainHand ),
                        Spell.Cast("Hemorrhage", ret => !Me.CurrentTarget.MeIsBehind || !Common.HasDaggerInMainHand),

                        Common.CheckThatDaggersAreEquippedIfNeeded()
                        )
                    )
                );
        }

        #endregion

        [Behavior(BehaviorType.Heal, WoWClass.Rogue, WoWSpec.RogueSubtlety, priority: 99)]
        public static Composite CreateRogueHeal()
        {
            return CreateSubteltyDiagnosticOutputBehavior("Combat");
        }

        private static Composite CreateSubteltyDiagnosticOutputBehavior(string sState = "")
        {
            if (!SingularSettings.Debug)
                return new Action(ret => { return RunStatus.Failure; });

            return new ThrottlePasses(1,
                new Action(ret =>
                {
                    string sMsg;
                    sMsg = string.Format(".... [{0}] h={1:F1}%, e={2:F1}%, moving={3}, stealth={4}, aoe={5}, recup={6}, slic={7}, rawc={8}, combo={9}, aoe={10}",
                        sState,
                        Me.HealthPercent,
                        Me.CurrentEnergy,
                        Me.IsMoving,
                        Common.IsStealthed,
                        Common.AoeCount,
                        (int)Me.GetAuraTimeLeft("Recuperate", true).TotalSeconds,
                        (int)Me.GetAuraTimeLeft("Slice and Dice", true).TotalSeconds,
                        Me.RawComboPoints,
                        Me.ComboPoints,
                        Common.AoeCount
                        );

                    WoWUnit target = Me.CurrentTarget;
                    if (target != null)
                    {
                        sMsg += string.Format(
                            ", {0}, {1:F1}%, {2} secs, {3:F1} yds, behind={4}, loss={5}, rupture={6}",
                            target.SafeName(),
                            target.HealthPercent,
                            target.TimeToDeath(),
                            target.Distance,
                            Me.IsSafelyBehind(target),
                            target.InLineOfSpellSight,
                            (int)target.GetAuraTimeLeft("Rupture", true).TotalSeconds
                            );
                    }

                    Logger.WriteDebug(Color.LightYellow, sMsg);
                    return RunStatus.Failure;
                })
                );
        }
    }
}
