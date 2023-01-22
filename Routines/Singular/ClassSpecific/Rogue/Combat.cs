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

using System.Drawing;using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;


namespace Singular.ClassSpecific.Rogue
{
    public class Combat
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static RogueSettings RogueSettings { get { return SingularSettings.Instance.Rogue(); } }
        private static bool HasTalent(RogueTalents tal) { return TalentManager.IsSelected((int)tal); } 

        #region Normal Rotation
        [Behavior(BehaviorType.Pull, WoWClass.Rogue, WoWSpec.RogueCombat, WoWContext.Normal | WoWContext.Battlegrounds | WoWContext.Instances )]
        public static Composite CreateRogueCombatPull()
        {
            return new PrioritySelector(
                Common.CreateRoguePullBuffs(),      // needed because some Bots not calling this behavior
                Helpers.Common.CreateDismount("Pulling"),
                Safers.EnsureTarget(),
                Common.CreateRoguePullSkipNonPickPocketableMob(),
                Common.CreateRogueControlNearbyEnemyBehavior(),
                Common.CreateRogueMoveBehindTarget(),
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown() && Me.GotTarget && Me.IsSafelyFacing(Me.CurrentTarget),
                    new PrioritySelector(

                        CreateCombatDiagnosticOutputBehavior("Pull"),

                        Common.CreateRoguePullPickPocketButDontAttack(),

                        Common.CreateRogueOpenerBehavior(),
                        Common.CreatePullMobMovingAwayFromMe(),
                        Common.CreateAttackFlyingOrUnreachableMobs(),

                        // ok, everything else failed so just hit him!!!!
                        Spell.Buff("Revealing Strike", true, ret => true),
                        Spell.Cast("Sinister Strike")
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Rogue, WoWSpec.RogueCombat, WoWContext.Normal | WoWContext.Battlegrounds )]
        public static Composite CreateRogueCombatNormalCombat()
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

                        Spell.BuffSelf("Adrenaline Rush", ret => Me.CurrentEnergy < 20 && !Me.HasAura("Killing Spree")),

                        // Killing Spree if we are at highest level of Bandit's Guise ( Shallow Insight / Moderate Insight / Deep Insight )
                        Spell.Cast("Killing Spree",
                            ret => Me.CurrentEnergy < 30 && Me.HasAura("Deep Insight") && !Me.HasAura("Adrenaline Rush")),

                        CreateBladeFlurryBehavior(),

                        new Decorator(
                            ret => Common.AoeCount >= RogueSettings.AoeSpellPriorityCount && Spell.UseAOE,
                            new PrioritySelector(
                                Spell.Cast("Slice and Dice", on => Me, ret => Me.ComboPoints > 0 && Me.HasAuraExpired("Slice and Dice", 2)),
                                Spell.Cast("Crimson Tempest", ret => Me.ComboPoints >= 5),
                                Spell.BuffSelf("Fan of Knives"),
                                Spell.Cast("Sinister Strike"),
                                Movement.CreateMoveToMeleeBehavior(true)
                                )
                            ),

                        Spell.Buff("Revealing Strike", true, ret => Me.CurrentTarget.IsWithinMeleeRange ),
                        Spell.Cast("Slice and Dice", on => Me, ret => Me.ComboPoints > 0 && Me.HasAuraExpired("Slice and Dice", 2)),

                        Spell.Cast("Eviscerate",
                            ret => Me.ComboPoints >= 5
                                && ( Me.HasAura("Blade Flurry") || Me.CurrentTarget.GetAuraTimeLeft("Rupture", true).TotalSeconds > 6 || Me.CurrentTarget.TimeToDeath() < 6)),

                        Spell.Cast("Eviscerate", ret => !SpellManager.HasSpell("Recuperate") && Me.CurrentTarget.TimeToDeath(999) <= Me.ComboPoints ),

                        Spell.Cast("Rupture",
                            ret => Me.ComboPoints >= 4
                                && !Me.HasAura("Blade Flurry")
                                && Me.CurrentTarget.TimeToDeath() >= 7
                                && Me.CurrentTarget.GetAuraTimeLeft("Rupture", true).TotalSeconds < 1), // && Me.CurrentTarget.HasBleedDebuff()

                        Spell.Cast("Fan of Knives", ret => Common.AoeCount >= RogueSettings.FanOfKnivesCount),
                        Spell.Cast("Sinister Strike")
                        )
                    )
                );
        }

        #endregion


        #region Instance Rotation

        [Behavior(BehaviorType.Combat, WoWClass.Rogue, WoWSpec.RogueCombat, WoWContext.Instances)]
        public static Composite CreateRogueCombatInstanceCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateRogueMoveBehindTarget(),
                Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCastOrChannel(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        new Action(ret => { Me.CurrentTarget.TimeToDeath(); return RunStatus.Failure; }),

                        Helpers.Common.CreateInterruptBehavior(),
                        Common.CreateDismantleBehavior(),

                        // Instance Specific Behavior
                        Spell.Cast("Tricks of the Trade", ret => Common.BestTricksTarget, ret => RogueSettings.UseTricksOfTheTrade),
                        // Spell.Cast("Feint", ret => Me.CurrentTarget.ThreatInfo.RawPercent > 80),

                        // Resume standard priorities
                        Common.CreateRogueMoveBehindTarget(),

                        Spell.BuffSelf("Adrenaline Rush", 
                            ret => Me.CurrentEnergy < 20 && !Me.HasAura("Killing Spree")),

                        // Killing Spree if we are at highest level of Bandit's Guise ( Shallow Insight / Moderate Insight / Deep Insight )
                        Spell.Cast("Killing Spree",
                            ret => Me.CurrentEnergy < 30 && Me.HasAura("Deep Insight") && !Me.HasAura("Adrenaline Rush")),

                        CreateBladeFlurryBehavior(),

                        new Decorator(
                            ret => Common.AoeCount >= RogueSettings.AoeSpellPriorityCount,
                            new PrioritySelector(
                                Spell.Cast("Slice and Dice", on => Me, ret => Me.ComboPoints > 0 && Me.HasAuraExpired("Slice and Dice", 2)),
                                Spell.Cast("Crimson Tempest", ret => Me.ComboPoints >= 5),
                                Spell.Cast("Fan of Knives"),
                                Spell.Cast("Sinister Strike"),
                                Movement.CreateMoveToMeleeBehavior(true)
                                )
                            ),

                        Spell.Cast("Revealing Strike", ret => !Me.CurrentTarget.HasMyAura("Revealing Strike")),
                        Spell.Cast("Slice and Dice", on => Me, ret => Me.ComboPoints > 0 && Me.HasAuraExpired("Slice and Dice", 2)),

                        Spell.Buff("Rupture", true,
                            ret => RogueSettings.CombatUseRuptureFinisher 
                                && Common.AoeCount <= 1
                                && Me.ComboPoints >= 4
                                && Me.CurrentTarget.HasMyAura("Revealing Strike")),
//                              && Me.CurrentTarget.HasBleedDebuff()),

                        Spell.Cast("Eviscerate", ret => Me.ComboPoints == 5),

                        Spell.Cast("Fan of Knives", ret => Common.AoeCount >= RogueSettings.FanOfKnivesCount),
                        Spell.Cast("Sinister Strike")
                        )
                    )
                );
        }

        #endregion

        internal static Composite CreateBladeFlurryBehavior()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Blade Flurry", ret => Spell.UseAOE && Common.AoeCount > 1),
                new Decorator(
                    ret => Me.HasAura("Blade Flurry") && (!Spell.UseAOE || Common.AoeCount <= 1),
                    new Sequence(
                        new Action(ret => Logger.Write("/cancel Blade Flurry")),
                        new Action(ret => Me.CancelAura("Blade Flurry")),
                        new Wait(TimeSpan.FromMilliseconds(500), ret => !Me.HasAura("Blade Flurry"), new ActionAlwaysSucceed())
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Rogue, WoWSpec.RogueCombat, priority: 99)]
        public static Composite CreateRogueHeal()
        {
            return CreateCombatDiagnosticOutputBehavior("Combat");
        }

        private static Composite CreateCombatDiagnosticOutputBehavior(string sState = "")
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
                        (int) Me.GetAuraTimeLeft("Recuperate", true).TotalSeconds,
                        (int) Me.GetAuraTimeLeft("Slice and Dice", true).TotalSeconds,
                        Me.RawComboPoints,
                        Me.ComboPoints,
                        Common.AoeCount 
                        );

                    WoWUnit target = Me.CurrentTarget;
                    if (target != null)
                    {
                        sMsg += string.Format(
                            ", {0}, {1:F1}%, dies {2}, {3:F1} yds, behind={4}, loss={5}, rvlstrk={6}, rupture={7}",
                            target.SafeName(),
                            target.HealthPercent,
                            target.TimeToDeath(),
                            target.Distance,
                            Me.IsSafelyBehind(target),
                            target.InLineOfSpellSight,
                            (int)target.GetAuraTimeLeft("Revealing Strike", true).TotalSeconds,
                            (int) target.GetAuraTimeLeft("Rupture", true).TotalSeconds
                            );
                    }

                    Logger.WriteDebug(Color.LightYellow, sMsg);
                    return RunStatus.Failure;
                })
                );
        }
    }
}
