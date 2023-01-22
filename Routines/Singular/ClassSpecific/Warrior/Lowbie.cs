using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;

using Styx;


using Styx.TreeSharp;
using Singular.Settings;

namespace Singular.ClassSpecific.Warrior
{
    public class Lowbie
    {
        [Behavior(BehaviorType.Combat, WoWClass.Warrior, 0, WoWContext.Normal)]
        public static Composite CreateLowbieWarriorCombat()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Helpers.Common.CreateAutoAttack(false),
                Helpers.Common.CreateInterruptBehavior(),

                Common.CreateChargeBehavior(),

                // Heal
                Common.CreateVictoryRushBehavior(),

                // AOE
                new Decorator(
                    ret => Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 6f) >= 2,
                    new PrioritySelector(
                        Spell.Cast("Thunder Clap"),
                        Spell.Cast("Heroic Strike"))),
                // DPS
                Spell.Cast("Execute"),
                Spell.Cast("Heroic Strike"),
                Spell.Cast("Thunder Clap", ret => StyxWoW.Me.RagePercent > 50),
                //move to melee
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        [Behavior(BehaviorType.Pull, WoWClass.Warrior, 0, WoWContext.Normal, 500)]
        public static Composite CreateLowbieWarriorPull()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Helpers.Common.CreateAutoAttack(false),
                Common.CreateAttackFlyingOrUnreachableMobs(),
                Common.CreateChargeBehavior(),

                // move to melee
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }
    }
}
