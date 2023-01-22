/*
 * NOTE:    DO NOT POST ANY MODIFIED VERSIONS OF THIS TO THE FORUMS.
 * 
 *          DO NOT UTILIZE ANY PORTION OF THIS PLUGIN WITHOUT
 *          THE PRIOR PERMISSION OF AUTHOR.  PERMITTED USE MUST BE
 *          ACCOMPANIED BY CREDIT/ACKNOWLEDGEMENT TO ORIGINAL AUTHOR.
 * 
 * Author:  Bobby53
 * 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Threading;
using System.Diagnostics;

using Levelbot.Actions.Combat;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.WoWInternals.WoWObjects;
using CommonBehaviors.Actions;
using Action = Styx.TreeSharp.Action;
using Sequence = Styx.TreeSharp.Sequence;
using Styx.WoWInternals;

using Bobby53;
using Styx.CommonBot;

namespace Styx.Bot.CustomBots
{
    public partial class LazyRaider : BotBase
    {
        public static IncludeTargetsFilterDelegate includeTargets;
        public static RemoveTargetsFilterDelegate removeTargets;
        public static WeighTargetsDelegate weighTargets;

        private static void TargetFilterSetup()
        {
            TargetFilterClear();

            Targeting.Instance.Clear();
            Targeting.Instance.IncludeElites = true;
            Targeting.Instance.IncludeWorldPlayers = true;
            Targeting.Instance.MaxTargets = 15;

            if (Battlegrounds.IsInsideBattleground)
            {
                includeTargets = IncludeTargetsFilter;
                removeTargets = RemoveTargetsFilter;
                weighTargets = PvpWeighTargetsFilter;
            }
            else if (IsInGroup)
            {
                includeTargets = IncludeTargetsFilter;
                removeTargets = RemoveTargetsFilter;
                weighTargets = GroupWeighTargetsFilter;
            }
            else
            {
                includeTargets = IncludeTargetsFilter;
                removeTargets = RemoveTargetsFilter;
                weighTargets = SoloWeighTargetsFilter;
            }

            Targeting.Instance.IncludeTargetsFilter += includeTargets;
            Targeting.Instance.RemoveTargetsFilter += removeTargets;
            Targeting.Instance.WeighTargetsFilter += weighTargets;
        }

        private static void TargetFilterClear()
        {
            if (includeTargets != null)
                Targeting.Instance.IncludeTargetsFilter -= includeTargets;
            if (removeTargets != null)
                Targeting.Instance.RemoveTargetsFilter -= removeTargets;
            if (weighTargets != null)
                Targeting.Instance.WeighTargetsFilter -= weighTargets;

            includeTargets = null;
            removeTargets = null;
            weighTargets = null;
        }

#if WE_DONT_NEED
        private static void IncludeTargetsFilter(List<WoWObject> incomingUnits, HashSet<WoWObject> outgoingUnits)
        {
            if ( Battlegrounds.IsInsideBattleground )
                PvpIncludeTargetsFilter( incomingUnits, outgoingUnits );
            else if ( !IsInGroup ) 
                SoloIncludeTargetsFilter( incomingUnits, outgoingUnits );
            else 
                GroupIncludeTargetsFilter( incomingUnits, outgoingUnits );

            // Dlog( "IncludeTargetsFilter: ");
        }

        private static void RemoveTargetsFilter(List<WoWObject> units)
        {
            if ( Battlegrounds.IsInsideBattleground )
                PvpRemoveTargetsFilter(units);
            else if ( !IsInGroup ) 
                SoloRemoveTargetsFilter(units);
            else
                GroupRemoveTargetsFilter(units);

            // Dlog("RemoveTargetsFilter: ");
        }

        private static void WeighTargetsFilter(List<Targeting.TargetPriority> units)
        {
            if ( Battlegrounds.IsInsideBattleground )
                PvpWeighTargetsFilter(units);
            else if ( !IsInGroup ) 
                SoloWeighTargetsFilter(units);
            else 
                GroupWeighTargetsFilter(units);

            // Dump("WeighTargetsFilter", units);
        }
#endif

        private static void IncludeTargetsFilter(List<WoWObject> incomingUnits, HashSet<WoWObject> outgoingUnits)
        {
            bool foundMyTarget = false;

            foreach (var unit in incomingUnits.Select(obj => obj.ToUnit()))
            {
                if (!foundMyTarget && unit.Guid == Me.CurrentTargetGuid)
                    foundMyTarget = true;

                if (!unit.IsAlive || (SpellDistanceSqr(Me,unit) > 45 * 45))
                    continue;

                outgoingUnits.Add(unit);
            }

            // add our current target if not already in list
            if (!foundMyTarget && Me.GotTarget && Me.CurrentTarget.IsAlive)
            {
                outgoingUnits.Add(Me.CurrentTarget);
            }
        }

        private static void RemoveTargetsFilter(List<WoWObject> units)
        {
            units.RemoveAll(obj =>
            {
                var u = obj as WoWUnit;
                bool removeObj;
                try
                {
                    // limit to enemies in line of spell sight
                    removeObj = (u == null || !obj.IsValid || !u.IsAlive || !IsEnemy(u) || !u.InLineOfSpellSight || u.ControllingPlayer != null || obj.Entry == 53488);
                }
                catch
                {
                    removeObj = true;
                }
                return removeObj;
            });
        }

        private static void GroupWeighTargetsFilter(List<Targeting.TargetPriority> units)
        {
            bool ImaTank = GetGroupRoleAssigned(Me) == WoWPartyMember.GroupRole.Tank;
            ulong guidMyCurrentTarget = Me.CurrentTargetGuid;
            ulong guidTank = Tank.Guid;
            ulong guidTankCurrentTarget = Tank.Player != null ? Tank.Player.CurrentTargetGuid : 0;
            int sizeDistanceBand = Me.IsMelee() ? 5 : 40;   // choose a distance band priority based upon type of toon
            
            foreach (var o in units)
            {
                WoWUnit u = o.Object.ToUnit();

                if (!u.IsAlive)
                {
                    o.Score = 0;
                    continue;
                }

                if (LazyRaiderSettings.Instance.AutoTargetOnlyIfNotValidTarget && guidMyCurrentTarget != 0 && u.Guid == guidMyCurrentTarget)
                {
                    o.Score = 9999;
                }
                else if (ImaTank)
                {
                    o.Score = 100;
                    if (u.IsTargetingMyPartyMember)
                        o.Score += 100;
                }
                else if (u.CurrentTarget == Tank.Player)
                {
                    o.Score = 1000;
                    o.Score -= 50 * (u.SpellDistance() / sizeDistanceBand);  // prioritize in distance bands
                    o.Score += (u.HealthPercent / 100) * sizeDistanceBand;   // prioritize highest health within a band
                }
            }
        }

        private static void PvpWeighTargetsFilter(List<Targeting.TargetPriority> units)
        {
            ulong guidMyCurrentTarget = Me.CurrentTargetGuid;
            foreach (var p in units)
            {
                WoWUnit u = p.Object.ToUnit();
                if (!u.IsAlive)
                    p.Score = 0;
                else if (LazyRaiderSettings.Instance.AutoTargetOnlyIfNotValidTarget && guidMyCurrentTarget != 0 && u.Guid == guidMyCurrentTarget)
                    p.Score = 9999;
                else 
                    p.Score = 200 - u.HealthPercent;
            }
        }

        private static void SoloWeighTargetsFilter(List<Targeting.TargetPriority> units)
        {
            bool isMelee = Me.IsMelee();

            ulong guidMyCurrentTarget = Me.CurrentTargetGuid;
            foreach (var p in units)
            {
                WoWUnit u = p.Object.ToUnit();
                if (!u.IsAlive)
                    p.Score = 0;
                else if (LazyRaiderSettings.Instance.AutoTargetOnlyIfNotValidTarget && guidMyCurrentTarget != 0 && u.Guid == guidMyCurrentTarget)
                    p.Score = 9999;
                else
                    p.Score = 200 - u.HealthPercent;
            }
        }

        private static int PlayersAttacking(WoWUnit unit)
        {
            return Me.PartyMembers.Count(p => p.CurrentTarget != null && p.CurrentTarget == unit) * 100;
        }

        public static bool IsEnemy(WoWUnit u)
        {
            return u != null
                && u.CanSelect
                && u.Attackable
                && u.IsAlive
                && (IsEnemyNPC(u) || IsEnemyPlayer(u));
        }

        private static bool IsEnemyNPC(WoWUnit u)
        {
            return !u.IsPlayer
                && (u.IsHostile || (u.IsNeutral && u.Combat && (u.IsTargetingMyPartyMember || u.IsTargetingMyRaidMember || u.IsTargetingMeOrPet )));
        }

        private static bool IsEnemyPlayer(WoWUnit u)
        {
            return u.IsPlayer 
                && (u.ToPlayer().IsHorde != StyxWoW.Me.IsHorde || (Battlegrounds.IsInsideBattleground && !u.ToPlayer().IsInMyPartyOrRaid ));
        }

        private static void Dump(string tag, List<Targeting.TargetPriority> units)
        {
            Dlog("=== TARGET {0} ===", tag);
            foreach (Targeting.TargetPriority pri in units)
            {
                var u = pri.Object as WoWUnit;
                // Dlog("   {0} {1}", pri.Score, u == null ? "-null-" : Safe_UnitName(u));
                Dlog("   {0}{1:F2} {2:F2} {3}", u.Guid == StyxWoW.Me.CurrentTargetGuid ? "*" : " ", pri.Score, u.HealthPercent, u.Name );
            }
        }

        /// <summary>
        /// get the effective distance between two mobs accounting for their 
        /// combat reaches (hitboxes)
        /// </summary>
        /// <param name="unit">unit</param>
        /// <param name="other">Me if null, otherwise second unit</param>
        /// <returns></returns>
        public static float SpellDistance(WoWUnit unit, WoWUnit other = null)
        {
            // abort if mob null
            if (unit == null)
                return 0;

            // optional arg implying Me, then make sure not Mob also
            if (other == null)
                other = StyxWoW.Me;

            // pvp, then keep it close
            float dist = other.Location.Distance(unit.Location);
            dist -= other.CombatReach + unit.CombatReach;
            return Math.Max(0, dist);
        }

        public static float SpellDistanceSqr(WoWUnit unit, WoWUnit other = null)
        {
            // abort if mob null
            if (unit == null)
                return 0;

            // optional arg implying Me, then make sure not Mob also
            if (other == null)
                other = StyxWoW.Me;

            // pvp, then keep it close
            float dist = other.CombatReach + unit.CombatReach;
            dist *= dist;
            dist = other.Location.DistanceSqr(unit.Location) - dist;
            return Math.Max(0, dist);
        }

    }

}
