using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.WoWInternals;

namespace Honorbuddy.QuestBehaviorCore
{
    public static partial class UtilityCoroutine
    {
        public static async Task<bool> PreferAggrodMob()
        {
            if (!StyxWoW.Me.Combat)
                return false;
            var currentTarget = StyxWoW.Me.CurrentTarget;

            // If our current target is aggro'd on us, we're done...
            if (Query.IsViable(currentTarget) && currentTarget.Aggro)
                return false;

            // Otherwise, we want to go find a target that is aggro'd on us, if one available...
            var aggrodMob = Targeting.Instance.TargetList.FirstOrDefault(m => m.Aggro);
            if (!Query.IsViable(aggrodMob))
                return false;

            Utility.Target(aggrodMob, false, PoiType.Kill);
            return true;
        }

        /// <summary>
        /// This coroutine was meant to be used mostly for 'vehicle' encounters.  Normal combat routines
        /// do not work well (or at all) in these situations.  Examples include:<list type="bullet">
        /// <item><description><para> * The 'poles' for "The Lesson of Dry Fur" (http://wowhead.com/quest=29661)
        /// </para></description></item>
        /// <item><description><para> * The 'Great White Plainshawk' for "A Lesson in Bravery" (http://wowhead.com/quest=29918)
        /// </para></description></item>
        /// </list>
        /// </summary>
        public static async Task<bool> MiniCombatRoutine()
        {
            var target = StyxWoW.Me.CurrentTarget;
            if (!Query.IsViableForFighting(target))
            {
                AutoAttackOff();
                return false;
            }
            AutoAttackOn();

            var activeMover = WoWMovement.ActiveMover;
            // Make certain we are facing the target...
			// NB: Since this behavior is frequently employed while we're "on vehicles"
			// "facing" doesn't always work.  If we are unable to 'face' successfully,
			// we don't want that to stop us from trying our special attaks.  Thus, 
            // not returning right away
            if (activeMover != null && !activeMover.IsSafelyFacing(target, 30))
                StyxWoW.Me.SetFacing(target.Location);

            switch (StyxWoW.Me.Class)
            {
                case WoWClass.DeathKnight:
                    return TryCast(49998);                              // Death Strike: http://wowhead.com/spell=49998
               
                case WoWClass.Druid:
                    return (!StyxWoW.Me.HasAura(768) && TryCast(5176))  // Wrath: http://wowhead.com/spell=5176
                        || (!StyxWoW.Me.HasAura(768) && TryCast(768))   // Cat Form: http://wowhead.com/spell=768
                        || TryCast(1822)                                // Rake: http://wowhead.com/spell=1822
                        || TryCast(22568)                               // Ferocious Bite: http://wowhead.com/spell=22568
                        || TryCast(33917);                              // Mangle: http://wowhead.com/spell=33917

                case WoWClass.Hunter:
                    return TryCast(3044)                                // Arcane Shot: http://wowhead.com/spell=3044
                        || TryCast(56641);                              // Steady Shot: http://wowhead.com/spell=56641

				case WoWClass.Mage:
                    return TryCast(44614)                               // Frostfire Bolt: http://wowhead.com/spell=44614
                        || TryCast(126201)                              // Frostbolt: http://wowhead.com/spell=126201
                        || TryCast(2136);                               // Fire Blast: http://wowhead.com/spell=2136

				case WoWClass.Monk:
				    return TryCast(100780)                              // Jab: http://wowhead.com/spell=100780
					    || TryCast(100787);                             // Tiger Palm: http://wowhead.com/spell=100787
    
                case WoWClass.Paladin:
					return TryCast(35395)                               // Crusader Strike: http://wowhead.com/spell=35395
						|| TryCast(20271);                              // Judgment: http://wowhead.com/spell=20271

				case WoWClass.Priest:
				    return (!target.HasAura(589) && TryCast(589))       // Shadow Word: Pain: http://wowhead.com/spell=589
					    || TryCast(15407)                               // Mind Flay: http://wowhead.com/spell=15407
					    || TryCast(585);                                // Smite: http://wowhead.com/spell=585

				case WoWClass.Rogue:
				    return TryCast(2098)                                // Eviscerate: http://wowhead.com/spell=2098
					    || TryCast(1752);                               // Sinster Strike: http://wowhead.com/spell=1752

				case WoWClass.Shaman:
			        return TryCast(17364)                               // Stormstrike: http://wowhead.com/spell=17364
						|| TryCast(403)                                 // Lightning Bolt: http://wowhead.com/spell=403
					    || TryCast(73899);                              // Primal Strike: http://wowhead.com/spell=73899

		        case WoWClass.Warlock:
				    return TryCast(686);                                // Shadow Bolt: http://wowhead.com/spell=686

				case WoWClass.Warrior:
				    return TryCast(78)                                  // Heroic Strike: http://wowhead.com/spell=78
					    || TryCast(34428)                               // Victory Rush: http://wowhead.com/spell=34428
						|| TryCast(23922)                               // Shield Slam: http://wowhead.com/spell=23922
						|| TryCast(20243);                              // Devastate: http://wowhead.com/spell=20243

                default:
                    QBCLog.MaintenanceError("Class({0}) is unhandled", StyxWoW.Me.Class);
					TreeRoot.Stop();
                    return false;
            }
        }

        private static void AutoAttackOff()
        {
            if (StyxWoW.Me.IsAutoAttacking)
            { Lua.DoString("StopAttack()"); }
        }


        private static void AutoAttackOn()
        {
            if (!StyxWoW.Me.IsAutoAttacking)
            { Lua.DoString("StartAttack()"); }
        }


        private static bool TryCast(int spellId)
        {
            if (!SpellManager.CanCast(spellId))
                return false;

            QBCLog.DeveloperInfo("MiniCombatRoutine used {0}", Utility.GetSpellNameFromId(spellId));
            SpellManager.Cast(spellId);
            return true;
        }
    }
}
