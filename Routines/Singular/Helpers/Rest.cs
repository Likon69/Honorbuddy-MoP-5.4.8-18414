using System.Linq;
using CommonBehaviors.Actions;

using Singular.Settings;
using Styx;

using Styx.CommonBot;
using Styx.CommonBot.Inventory;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Styx.TreeSharp;
using System;

using Action = Styx.TreeSharp.Action;
using System.Drawing;
using Singular.Managers;

namespace Singular.Helpers
{
    internal static class Rest
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        private static bool CorpseAround
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>(true, false).Any(
                    u => u.Distance < 5 && u.IsDead &&
                         (u.CreatureType == WoWCreatureType.Humanoid || u.CreatureType == WoWCreatureType.Undead));
            }
        }

        private static bool PetInCombat
        {
            get { return Me.GotAlivePet && Me.PetInCombat; }
        }

        /// <summary>
        /// implements standard Rest behavior.  self-heal optional and typically used by DPS that have healing spell, 
        /// as healing specs are better served using a spell appropriate to amount of healing needed.  ressurrect
        /// is optional and done only if spell name passed
        /// </summary>
        /// <param name="spellHeal">name of healing spell</param>
        /// <param name="spellRez">name of ressurrect spell</param>
        /// <returns></returns>
        public static Composite CreateDefaultRestBehaviour(string spellHeal = null, string spellRez = null)
        {
            return new PrioritySelector(

                new Decorator(
                    ret => !Me.IsDead && !Me.IsGhost,
                    new PrioritySelector(

                // Self-heal if possible
                        new Decorator(
                            ret => spellHeal != null
                                && Me.HealthPercent <= 85  // not redundant... this eliminates unnecessary GetPredicted... checks
                                && SpellManager.HasSpell(spellHeal) && Spell.CanCastHack(spellHeal, Me)
                                && Me.PredictedHealthPercent(includeMyHeals: true) <= 85 && !Me.HasAnyAura("Drink", "Food"),
                            new Sequence(
                                Movement.CreateEnsureMovementStoppedBehavior(reason: "to heal"),
                                new Action(r => Logger.WriteDebug("Rest Heal - {0} @ {1:F1}% Predict:{2:F1}% and moving:{3}, cancast:{4}", spellHeal, Me.HealthPercent, Me.PredictedHealthPercent(includeMyHeals: true), Me.IsMoving, Spell.CanCastHack(spellHeal, Me, skipWowCheck: false)) ),
                                new Sequence(
                                    ctx => (int) Me.HealthPercent,
                                    Spell.Cast(spellHeal,
                                        mov => true,
                                        on => Me,
                                        req => true,
                                        cancel => Me.HealthPercent > 90
                                        ),
                                    new WaitContinue( TimeSpan.FromMilliseconds(500), until => Me.HealthPercent > (1.1 * ((int)until)), new ActionAlwaysSucceed()),
                                    new Action( r => Logger.WriteDebug("Rest - After Heal Attempted: {0:F1}% Predicted: {1:F1}%", Me.HealthPercent, Me.PredictedHealthPercent(includeMyHeals: true)))
                                    ),
                                new Action( r => Logger.WriteDebug("Rest - After Heal Skipped: {0:F1}% Predicted: {1:F1}%", Me.HealthPercent, Me.PredictedHealthPercent(includeMyHeals: true)))
                                )
                            ),

                // Make sure we wait out res sickness. 
                        Helpers.Common.CreateWaitForRessSickness(),

                // Cannibalize support goes before drinking/eating. changed to a Sequence with wait because Rest behaviors that had a 
                // .. WaitForCast() before call to DefaultRest would prevent cancelling when health/mana reached
                        new Decorator(
                            ret => SingularSettings.Instance.UseRacials
                                && (Me.PredictedHealthPercent(includeMyHeals: true) <= SingularSettings.Instance.MinHealth || (Me.PowerType == WoWPowerType.Mana && Me.ManaPercent <= SingularSettings.Instance.MinMana))
                                && Spell.CanCastHack("Cannibalize")
                                && CorpseAround,
                            new Sequence(
                                new DecoratorContinue(ret => Me.IsMoving, Movement.CreateEnsureMovementStoppedBehavior(reason: "to cannibalize")),
                                new Wait(1, ret => !Me.IsMoving, new ActionAlwaysSucceed()),
                                new Action(ret => Logger.Write("*Cannibalize @ health:{0:F1}%{1}", Me.HealthPercent, (Me.PowerType != WoWPowerType.Mana) ? "" : string.Format(" mana:{0:F1}%", Me.ManaPercent))),
                                new Action(ret => SpellManager.Cast("Cannibalize")),

                                // wait until Cannibalize in progress
                                new WaitContinue(
                                    1,
                                    ret => Me.CastingSpell != null && Me.CastingSpell.Name == "Cannibalize",
                                    new ActionAlwaysSucceed()
                                    ),
                // wait until cast or healing complete. use actual health percent here
                                new WaitContinue(
                                    10,
                                    ret => Me.CastingSpell == null
                                        || Me.CastingSpell.Name != "Cannibalize"
                                        || (Me.HealthPercent > 95 && (Me.PowerType != WoWPowerType.Mana || Me.ManaPercent > 95)),
                                    new ActionAlwaysSucceed()
                                    ),
                // show completion message and cancel cast if needed
                                new Action(ret =>
                                {
                                    bool stillCasting = Me.CastingSpell != null && Me.CastingSpell.Name == "Cannibalize";
                                    Logger.WriteFile("{0} @ health:{1:F1}%{2}",
                                        stillCasting ? "/cancel Cannibalize" : "Cannibalize ended",
                                        Me.HealthPercent,
                                        (Me.PowerType != WoWPowerType.Mana) ? "" : string.Format(" mana:{0:F1}%", Me.ManaPercent)
                                        );

                                    if (stillCasting)
                                    {
                                        SpellManager.StopCasting();
                                    }
                                })
                                )
                            ),

                // use a bandage if enabled (it's quicker)
                        new Decorator(
                            ret => Me.IsAlive && Me.PredictedHealthPercent(includeMyHeals: true) <= SingularSettings.Instance.MinHealth,
                            Item.CreateUseBandageBehavior()
                            ),

                // Check if we're allowed to eat (and make sure we have some food. Don't bother going further if we have none.
                        new Decorator(
                            ret => !Me.IsSwimming && Me.PredictedHealthPercent(includeMyHeals: true) <= SingularSettings.Instance.MinHealth
                                && !Me.HasAura("Food") && Consumable.GetBestFood(false) != null,
                            new PrioritySelector(
                                Movement.CreateEnsureMovementStoppedBehavior(reason: "to eat"),
                                new Sequence(
                                    new Action(
                                        ret =>
                                        {
                                            Styx.CommonBot.Rest.FeedImmediate();
                                        }),
                                    Helpers.Common.CreateWaitForLagDuration()
                                    )
                                )
                            ),

                // Make sure we're a class with mana, if not, just ignore drinking all together! Other than that... same for food.
                        new Decorator(
                            ret => !Me.IsSwimming && (Me.PowerType == WoWPowerType.Mana || Me.Class == WoWClass.Druid)
                                && Me.ManaPercent <= SingularSettings.Instance.MinMana && !Me.HasAura("Drink") && Consumable.GetBestDrink(false) != null,
                            new PrioritySelector(
                                Movement.CreateEnsureMovementStoppedBehavior(reason: "to drink"),
                                new Sequence(
                                    new Action(ret =>
                                        {
                                            Styx.CommonBot.Rest.DrinkImmediate();
                                        }),
                                    Helpers.Common.CreateWaitForLagDuration()
                                    )
                                )
                            ),

                // This is to ensure we STAY SEATED while eating/drinking. No reason for us to get up before we have to.
                        new Decorator(
                            ret => (Me.HasAura("Food") && Me.HealthPercent < 95)
                                || (Me.HasAura("Drink") && Me.PowerType == WoWPowerType.Mana && Me.ManaPercent < 95),
                            new ActionAlwaysSucceed()
                            ),

                // wait here if we are moving -OR- do not have food or drink
                        new Decorator(
                            ret => WaitForRegenIfNoFoodDrink(),

                            new PrioritySelector(
                                new Decorator(
                                    ret => Me.IsMoving,
                                    new PrioritySelector(
                                        new Throttle(5, new Action(ret => Logger.Write("Still moving... waiting until Bot stops"))),
                                        new ActionAlwaysSucceed()
                                        )
                                    ),
                                new Decorator(
                                    req => Me.IsSwimming,
                                    new PrioritySelector(
                                        new Throttle(15, new Action(ret => Logger.Write("Swimming. Waiting to recover our health/mana back"))),
                                        new ActionAlwaysSucceed()
                                        )
                                    ),
                                new PrioritySelector(
                                    new Throttle(15, new Action(ret => Logger.Write("We have no food/drink. Waiting to recover our health/mana back"))),
                                    new ActionAlwaysSucceed()
                                    )
                                )
                            ),

                // rez anyone near us if appropriate
                        new Decorator(ret => spellRez != null, Spell.Resurrect(spellRez)),

                // hack:  some bots not calling PreCombatBuffBehavior, so force the call if we are about done resting
                // SingularRoutine.Instance.PreCombatBuffBehavior,

                        Movement.CreateWorgenDarkFlightBehavior()
                        )
                    )
                );
        }

        /// <summary>
        /// checks if we should stay in current spot and wait for health and/or mana to regen.
        /// called when we have no food/drink
        /// </summary>
        /// <returns></returns>
        private static bool WaitForRegenIfNoFoodDrink()
        {
            // never wait in a battleground
            if (Me.CurrentMap.IsBattleground)
                return false;

            // always wait for health to regen
            if (Me.HealthPercent < SingularSettings.Instance.MinHealth)
                return true;
            
            // non-mana users don't wait mana
            if (Me.PowerType != WoWPowerType.Mana)
                return false;

            // ferals and guardians dont wait on mana either
            if (TalentManager.CurrentSpec == WoWSpec.DruidFeral || TalentManager.CurrentSpec == WoWSpec.DruidGuardian )
                return false;
                
            // wait for mana if too low
            if (Me.ManaPercent < SingularSettings.Instance.MinMana)
                return true;

            return false;
        }
    }
}
