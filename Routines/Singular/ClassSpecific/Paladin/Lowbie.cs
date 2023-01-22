using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;

using Styx.TreeSharp;


namespace Singular.ClassSpecific.Paladin
{
    public class Lowbie
    {
        [Behavior(BehaviorType.Combat,WoWClass.Paladin,0)]
        public static Composite CreateLowbiePaladinCombat()
        {
            return
                new PrioritySelector(
                    Helpers.Common.EnsureReadyToAttackFromMelee(),
                    Helpers.Common.CreateAutoAttack(true),
                    Helpers.Common.CreateInterruptBehavior(),
                    Spell.Cast("Crusader Strike"),
                    Spell.Cast("Judgment"),
                    Movement.CreateMoveToMeleeBehavior(true)
                    );
        }
        [Behavior(BehaviorType.Pull, WoWClass.Paladin, 0)]
        public static Composite CreateLowbiePaladinPull()
        {
            return
                new PrioritySelector(
                    Helpers.Common.EnsureReadyToAttackFromMelee(),
                    Helpers.Common.CreateAutoAttack(true),
                    Spell.Cast("Judgment"),
                    Movement.CreateMoveToMeleeBehavior(true)
                    );
        }
        [Behavior(BehaviorType.Heal, WoWClass.Paladin, 0)]
        public static Composite CreateLowbiePaladinHeal()
        {
            return
                new PrioritySelector(
                    Spell.Cast("Word of Glory", ret => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent < 50),
                    Spell.Cast("Holy Light", ret => StyxWoW.Me, ret => StyxWoW.Me.HealthPercent < 40)
                    );
        }
        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Paladin, 0)]
        public static Composite CreateLowbiePaladinPreCombatBuffs()
        {
            return
                new PrioritySelector(
                    Spell.BuffSelf("Seal of Command")
                    );
        }
        [Behavior(BehaviorType.CombatBuffs, WoWClass.Paladin, 0)]
        public static Composite CreateLowbiePaladinCombatBuffs()
        {
            return
                new PrioritySelector(
                    Spell.BuffSelf("Seal of Command")
                    );
        }
    }
}
