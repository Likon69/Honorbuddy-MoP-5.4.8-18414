using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;

using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using Singular.Settings;

namespace Singular.ClassSpecific.DeathKnight
{
    public class Lowbie
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static DeathKnightSettings DeathKnightSettings { get { return SingularSettings.Instance.DeathKnight(); } }

        // Note:  in MOP we would only have Lowbie Death Knights if user doesn't select a spec when character
        // is created.  Previously, you had to complete some quests to get talent points which then 
        // determined your spec, but that is no longer necessary

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, (WoWSpec)0)]
        public static Composite CreateLowbieDeathKnightCombat()
        {
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Helpers.Common.CreateAutoAttack(true),
                        Helpers.Common.CreateInterruptBehavior(),

                        // Anti-magic shell - no cost and doesnt trigger GCD 
                        Spell.BuffSelf("Anti-Magic Shell", ret => Unit.NearbyUnfriendlyUnits.Any(u => (u.IsCasting || u.ChanneledCastingSpellId != 0) && u.CurrentTargetGuid == StyxWoW.Me.Guid)),

                        Common.CreateGetOverHereBehavior(),
                        Spell.Cast("Death Coil"),
                        Spell.Buff("Icy Touch", true, on => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.IsTargetingMeOrPet && u.HasAuraExpired("Frost Fever") && Me.SpellDistance(u) < 30 && Me.IsSafelyFacing(u) && u.InLineOfSpellSight), req => true, 3, "Frost Fever"),
                        Spell.Buff("Plague Strike", true, on => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.IsTargetingMeOrPet && u.HasAuraExpired("Blood Plague") && u.IsWithinMeleeRange && Me.IsSafelyFacing(u) && u.InLineOfSpellSight), req => true, 3, "Blood Plague"),
                        Spell.Cast("Blood Strike"),
                        Spell.Cast("Icy Touch"),
                        Spell.Cast("Plague Strike")
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }
    }
}
