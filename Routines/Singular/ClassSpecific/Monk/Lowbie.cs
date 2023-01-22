using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.TreeSharp;
using System.Collections.Generic;
using Styx.CommonBot;
using Singular.Settings;

namespace Singular.ClassSpecific.Monk
{
    // Basic low level monk class routine by Laria and CnG
    public class Lowbie
    {
       
        [Behavior(BehaviorType.Combat | BehaviorType.Pull, WoWClass.Monk, 0)]
        public static Composite CreateLowbieMonkCombat()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptBehavior(),
                Spell.Cast("Tiger Palm", ret => !SpellManager.HasSpell("Blackout Kick") && StyxWoW.Me.CurrentChi >= 1),
                Spell.Cast("Tiger Palm", ret => SpellManager.HasSpell("Blackout Kick") && StyxWoW.Me.CurrentChi >= 1 && StyxWoW.Me.HasKnownAuraExpired("Tiger Power")),
                Spell.Cast("Blackout Kick", ret => StyxWoW.Me.CurrentChi >= 2),
                Spell.Cast("Jab"),
                //Only roll to get to the mob quicker. 
                Spell.Cast("Roll", ret => MovementManager.IsClassMovementAllowed && !SingularSettings.Instance.Monk().DisableRoll && StyxWoW.Me.CurrentTarget.Distance > 12),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        
    
    }
     
}