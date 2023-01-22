
using System.Collections.Generic;
using System.Linq;

using Singular.Settings;
using Singular.Helpers;

using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using System;
using System.Drawing;
using CommonBehaviors.Actions;

using Action = Styx.TreeSharp.Action;

namespace Singular.Managers
{
    /*
     * Targeting works like so, in order of being called
     * 
     * GetInitialObjectList - Return a list of initial objects for the targeting to use.
     * RemoveTargetsFilter - Remove anything that doesn't belong in the list.
     * IncludeTargetsFilter - If you want to include units regardless of the remove filter
     * WeighTargetsFilter - Weigh each target in the list.     
     *
     */

    internal class HealerManager : HealTargeting
    {
        public const int EmergencyHealPercent = 35;
        public const int EmergencyHealOutOfDanger = 45;

        private static WoWUnit _SavingHealUnit;

        private delegate void HealerCancelDpsLogDelegate(Color clr, string message, params object[] args);
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static HmmContext Hmm(object ctx) { return (HmmContext)ctx; }

        private static readonly WaitTimer _tankReset = WaitTimer.ThirtySeconds;

        // private static ulong _tankGuid;

        static HealerManager()
        {
            // Make sure we have a singleton instance!
            HealTargeting.Instance = Instance = new HealerManager();
        }

        public new static HealerManager Instance { get; private set; }

        // following property is set by BT implementations for spec + context
        // .. and controls whether we should include Healing support
        public static bool NeedHealTargeting { get; set; }

        protected override List<WoWObject> GetInitialObjectList()
        {
            // Targeting requires a list of WoWObjects - so it's not bound to any specific type of object. Just casting it down to WoWObject will work fine.
            // return ObjectManager.ObjectList.Where(o => o is WoWPlayer).ToList();
            List<WoWObject> heallist;
            if (Me.GroupInfo.IsInRaid || Me.GroupInfo.IsInParty)
            {
                if (!SingularSettings.Instance.IncludeCompanionssAsHealTargets)
                    heallist = ObjectManager.ObjectList
                        .Where(o => o is WoWPlayer && o.ToPlayer().IsInMyRaid)
                        .ToList();
                else
                    heallist = ObjectManager.ObjectList
                        .Where(o => (o is WoWPlayer && o.ToPlayer().IsInMyRaid) || (o is WoWUnit && o.ToUnit().SummonedByUnitGuid == Me.Guid && !o.ToUnit().IsPet))
                        .ToList();
            }
            else
            {
                if (!SingularSettings.Instance.IncludeCompanionssAsHealTargets)
                    heallist = new List<WoWObject>() { Me };
                else
                    heallist = ObjectManager.ObjectList
                    .Where(o => o is WoWUnit && o.ToUnit().SummonedByUnitGuid == Me.Guid && !o.ToUnit().IsPet)
                    .ToList();
            }

            return heallist;
        }

        private static ulong lastCompanion = 0;
        private static ulong lastFocus = 0;

        protected override void DefaultIncludeTargetsFilter(List<WoWObject> incomingUnits, HashSet<WoWObject> outgoingUnits)
        {
            bool foundMe = false;
            bool isHorde = StyxWoW.Me.IsHorde;
            ulong focusGuid = Me.FocusedUnitGuid;
            bool foundFocus = false;

            foreach (WoWObject incomingUnit in incomingUnits)
            {
                try
                {
                    if (incomingUnit.IsMe)
                        foundMe = true;
                    else if (incomingUnit.Guid == focusGuid)
                        foundFocus = true;
                    else if (SingularSettings.Debug && incomingUnit.Guid != lastCompanion && SingularSettings.Instance.IncludeCompanionssAsHealTargets && incomingUnit is WoWUnit && incomingUnit.ToUnit().SummonedByUnitGuid == Me.Guid)
                    {
                        // temporary code only used to verify a companion found!
                        lastCompanion = incomingUnit.Guid;
                        Logger.WriteDebug( Color.White, "HealTargets: including found companion {0}#{1}", incomingUnit.Name, incomingUnit.Entry);
                    }

                    outgoingUnits.Add(incomingUnit);

                    var player = incomingUnit as WoWPlayer;
                    if (SingularSettings.Instance.IncludePetsAsHealTargets && player != null && player.GotAlivePet)
                        outgoingUnits.Add(player.Pet);
                }
                catch (System.AccessViolationException)
                {
                }
                catch (Styx.InvalidObjectPointerException)
                {
                }
            }

            if (!foundMe)
            {
                outgoingUnits.Add(Me);
                if (SingularSettings.Instance.IncludePetsAsHealTargets && Me.GotAlivePet)
                    outgoingUnits.Add(Me.Pet);
            }

            /*
            if (StyxWoW.Me.GotTarget && StyxWoW.Me.CurrentTarget.IsFriendly && !StyxWoW.Me.CurrentTarget.IsPlayer)
                outgoingUnits.Add(StyxWoW.Me.CurrentTarget);
            */
            try
            {
                if (Me.FocusedUnit != null && Me.FocusedUnit.IsFriendly )
                {
                    if (!foundFocus)
                    {
                        outgoingUnits.Add(StyxWoW.Me.FocusedUnit);
                        if (Me.FocusedUnit.GotAlivePet)
                            outgoingUnits.Add(Me.FocusedUnit.Pet);
                    }

                    if (SingularSettings.Debug && Me.FocusedUnit.Guid != lastFocus)
                    {
                        lastFocus = Me.FocusedUnit.Guid;
                        Logger.WriteDebug(Color.White, "HealTargets: including focused unit {0}#{1}", Me.FocusedUnit.Name, Me.FocusedUnit.Entry);
                    }
                }
            }
            catch
            {
            }
        }

        protected override void DefaultRemoveTargetsFilter(List<WoWObject> units)
        {
            bool isHorde = StyxWoW.Me.IsHorde;
            int maxHealRangeSqr;

            if (MovementManager.IsMovementDisabled)
                maxHealRangeSqr = 40 * 40;
            else
                maxHealRangeSqr = SingularSettings.Instance.MaxHealTargetRange * SingularSettings.Instance.MaxHealTargetRange;

            WoWPoint myLoc = Me.Location;

            for (int i = units.Count - 1; i >= 0; i--)
            {
                WoWUnit unit = units[i].ToUnit();
                try
                {
                    if (unit == null || !unit.IsValid || unit.IsDead || unit.HealthPercent <= 0 || !unit.IsFriendly)
                    {
                        units.RemoveAt(i);
                        continue;
                    }

                    WoWPlayer p = unit as WoWPlayer;
                    if (p == null && unit.IsPet)
                    {
                        var ownedByRoot = unit.OwnedByRoot;
                        if (ownedByRoot != null && ownedByRoot.IsPlayer)
                            p = unit.OwnedByRoot.ToPlayer();
                    }

                    if (p != null)
                    {
                        // Make sure we ignore dead/ghost players. If we need res logic, they need to be in the class-specific area.
                        if (p.IsGhost)
                        {
                            units.RemoveAt(i);
                            continue;
                        }

                        // They're not in our party/raid. So ignore them. We can't heal them anyway.
                        /*
                        if (!p.IsInMyPartyOrRaid)
                        {
                            units.RemoveAt(i);
                            continue;
                        }
                        */
                        /*
                                            if (!p.Combat && p.HealthPercent >= SingularSettings.Instance.IgnoreHealTargetsAboveHealth)
                                            {
                                                units.RemoveAt(i);
                                                continue;
                                            }
                         */
                    }

                    // If we have movement turned off, ignore people who aren't in range.
                    // Almost all healing is 40 yards, so we'll use that. If in Battlegrounds use a slightly larger value to expane our 
                    // healing range, but not too large that we are running all over the bg zone 
                    // note: reordered following tests so only one floating point distance comparison done due to evalution of DisableAllMovement
                    if (unit.Location.DistanceSqr(myLoc) > maxHealRangeSqr)
                    {
                        units.RemoveAt(i);
                        continue;
                    }
                }
                catch (System.AccessViolationException)
                {
                    units.RemoveAt(i);
                    continue;
                }
                catch (Styx.InvalidObjectPointerException)
                {
                    units.RemoveAt(i);
                    continue;
                }
            }
        }

        protected override void DefaultTargetWeight(List<TargetPriority> units)
        {
            var tanks = GetMainTankGuids();
            var inBg = Battlegrounds.IsInsideBattleground;
            var amHolyPally = TalentManager.CurrentSpec == WoWSpec.PaladinHoly;
            var myLoc = Me.Location;

            foreach (TargetPriority prio in units)
            {
                WoWUnit u = prio.Object.ToUnit();
                if (u == null || !u.IsValid)
                {
                    prio.Score = -9999f;
                    continue;
                }

                // The more health they have, the lower the score.
                // This should give -500 for units at 100%
                // And -50 for units at 10%
                try
                {
                    prio.Score = u.IsAlive ? 500f : -500f;
                    prio.Score -= u.HealthPercent * 5;

                    // If they're out of range, give them a bit lower score.
                    if (u.Location.DistanceSqr(myLoc) > 41 * 41)
                    {
                        prio.Score -= 50f;
                    }

                    // If they're out of LOS, again, lower score!
                    if (!u.InLineOfSpellSight)
                    {
                        prio.Score -= 100f;
                    }

                    // Give tanks more weight. If the tank dies, we all die. KEEP HIM UP.
                    if (tanks.Contains(u.Guid) && u.HealthPercent != 100 &&
                        // Ignore giving more weight to the tank if we have Beacon of Light on it.
                        (!amHolyPally || !u.Auras.Any(a => a.Key == "Beacon of Light" && a.Value.CreatorGuid == StyxWoW.Me.Guid)))
                    {
                        prio.Score += 100f;
                    }

                    // Give myself more weight. If the Healer dies, we all die. KEEP YOURSELF UP!
                    if (Me.Guid == u.Guid && WoWPartyMember.GroupRole.Healer == (Me.Role & WoWPartyMember.GroupRole.Healer))
                    {
                        prio.Score += 100f;
                    }

                    // Give flag carriers more weight in battlegrounds. We need to keep them alive!
                    if (inBg && u.IsPlayer && u.Auras.Keys.Any(a => a.ToLowerInvariant().Contains("flag")))
                    {
                        prio.Score += 125f;
                    }
                }
                catch (System.AccessViolationException)
                {
                    prio.Score = -9999f;
                    continue;
                }
                catch (Styx.InvalidObjectPointerException)
                {
                    prio.Score = -9999f;
                    continue;
                }
            }
        }

        public override void Pulse()
        {
            if (NeedHealTargeting)
                base.Pulse();
        }

        public static HashSet<ulong> GetMainTankGuids()
        {
            var infos = StyxWoW.Me.GroupInfo.RaidMembers;

            return new HashSet<ulong>(
                from pi in infos
                where (pi.Role & WoWPartyMember.GroupRole.Tank) != 0
                select pi.Guid);
        }

        public static WoWUnit SavingHealUnit
        {
            get
            {
                if (_SavingHealUnit != null && SavingHealTimer.IsFinished)
                {
                    try
                    {
                        // note: need to use HealthPercent (not GetPredictedHealthPercent() so we know they are out of danger
                        if (_SavingHealUnit.IsValid && _SavingHealUnit.HealthPercent < EmergencyHealOutOfDanger && _SavingHealUnit.SpellDistance() < 40f)
                            SavingHealTimer.Reset();
                        else
                        {
                            Logger.WriteDiagnostic("SavingHealUnit: reset since {0} @ {1:F1}% and out of danger", _SavingHealUnit.SafeName(), _SavingHealUnit.HealthPercent);
                            _SavingHealUnit = null;
                        }
                    }
                    catch
                    {
                        _SavingHealUnit = null;
                    }
                }
                return _SavingHealUnit;
            }

            set
            {
                _SavingHealUnit = value;
                if (_SavingHealUnit != null)
                {
                    SavingHealTimer.Reset();
                    Logger.WriteDiagnostic("SavingHealUnit: next heal forced to target {0} @ {1:F1}%", value.SafeName(), value.HealthPercent);
                }
            }
        }

        private static readonly WaitTimer SavingHealTimer = new WaitTimer(TimeSpan.FromMilliseconds(1500));
        /// <summary>
        /// finds the lowest health target in HealerManager.  HealerManager updates the list over multiple pulses, resulting in 
        /// the .FirstUnit entry often being at higher health than later entries.  This method dynamically searches the current
        /// list and returns the lowest at this moment.
        /// </summary>
        /// <returns></returns>
        public static WoWUnit FindLowestHealthTarget()
        {
#if LOWEST_IS_FIRSTUNIT
            return HealerManager.Instance.FirstUnit;
#else
            double minHealth = 999;
            WoWUnit minUnit = null;

            // iterate the list so we make a single pass through it
            foreach (WoWUnit unit in HealerManager.Instance.TargetList)
            {
                try
                {
                    if (unit.HealthPercent < minHealth)
                    {
                        minHealth = unit.HealthPercent;
                        minUnit = unit;
                    }
                }
                catch
                {
                    // simply eat the exception here
                }
            }

            return minUnit;
#endif
        }

        /// <summary>
        /// finds the lowest health target in HealerManager.  HealerManager updates the list over multiple pulses, resulting in 
        /// the .FirstUnit entry often being at higher health than later entries.  This method dynamically searches the current
        /// list and returns the lowest at this moment.
        /// </summary>
        /// <returns></returns>
        public static WoWUnit FindHighestPriorityTarget()
        {
            WoWUnit target = Group.Tanks.Union( Group.Healers )
                .Where( t => t.IsAlive && t.HealthPercent < 35 && t.SpellDistance() < 40)
                .OrderBy( k => k.HealthPercent )
                .FirstOrDefault();

            return target ?? FindLowestHealthTarget();    
        }

        /// <summary>
        /// check if Healer should be permitted to do straight DPS abilities (with purpose to damage and not indirect heal, buff, mana return, etc.)
        /// </summary>
        /// <returns></returns>
        public static bool AllowHealerDPS()
        {
            WoWContext ctx = SingularRoutine.CurrentWoWContext;
            if (ctx == WoWContext.Normal)
                return true;

            if (SingularRoutine.CurrentHealContext == HealingContext.Raids)
            {
                if (TalentManager.CurrentSpec != WoWSpec.MonkMistweaver || !SingularSettings.Instance.HealerCombatAllow)
                {
                    return false;
                }
            }

            double rangeCheck = SingularSettings.Instance.MaxHealTargetRange * SingularSettings.Instance.MaxHealTargetRange;
            if (!SingularSettings.Instance.HealerCombatAllow && Unit.GroupMembers.Any(m => m.IsAlive && !m.IsMe && m.Distance2DSqr < rangeCheck))
                return false;

            if (Me.ManaPercent < SingularSettings.Instance.HealerCombatMinMana)
                return false;

            if (HealerManager.Instance.FirstUnit != null && HealerManager.Instance.FirstUnit.HealthPercent < SingularSettings.Instance.HealerCombatMinHealth)
                return false;

            return true;
        }

        /// <summary>
        /// check whether a healer DPS be cancelled
        /// </summary>
        /// <returns></returns>
        public static bool CancelHealerDPS()
        {
            // allow combat setting is false, so cast originated by some other logic, so allow it to finish
            if (!SingularSettings.Instance.HealerCombatAllow)
                return false;

            // always let DPS casts while solo complete
            WoWContext ctx = SingularRoutine.CurrentWoWContext;
            if (ctx == WoWContext.Normal)
                return false;

            // allow casts that are close to finishing to finish regardless
            bool castInProgress = Spell.IsCastingOrChannelling();
            if (castInProgress && Me.CurrentCastTimeLeft.TotalMilliseconds < 333 && Me.CurrentChannelTimeLeft.TotalMilliseconds < 333)
            {
                Logger.WriteDebug("CancelHealerDPS: suppressing /cancel since less than 333 ms remaining");
                return false;
            }

            // use a window less than actual to avoid cast/cancel/cast/cancel due to mana hovering at setting level
            string action = castInProgress ? "/cancel" : "!do-not-dps";

            if (Me.ManaPercent < (SingularSettings.Instance.HealerCombatMinMana - 3))
            {
                Logger.WriteDebug(Color.Orange, "{0} because my Mana={1:F1}% fell below Min={2}%", action, Me.ManaPercent, SingularSettings.Instance.HealerCombatMinMana);
                return true;
            }

            // check if group health has dropped below setting
            if (HealerManager.Instance.FirstUnit != null && HealerManager.Instance.FirstUnit.HealthPercent < SingularSettings.Instance.HealerCombatMinHealth)
            {
                Logger.WriteDebug(Color.Orange, "{0} because {1} @ {2:F1}% health fell below Min={3}%", action, HealerManager.Instance.FirstUnit.SafeName(), HealerManager.Instance.FirstUnit.HealthPercent, SingularSettings.Instance.HealerCombatMinHealth);
                return true;
            }

            return false;
        }

        public static WoWUnit GetBestCoverageTarget(string spell, int health, int range, int radius, int minCount, SimpleBooleanDelegate requirements = null, IEnumerable<WoWUnit> mainTarget = null)
        {
            if (!Me.IsInGroup() || !Me.Combat)
                return null;

            if (!Spell.CanCastHack(spell, Me, skipWowCheck: true))
            {
                if (!SingularSettings.DebugSpellCasting)
                    Logger.WriteDebug("GetBestCoverageTarget: CanCastHack says NO to [{0}]", spell);
                return null;
            }

            if (requirements == null)
                requirements = req => true;

            // build temp list of targets that could use heal and are in range + radius
            List<WoWUnit> coveredTargets = HealerManager.Instance.TargetList
                .Where(u => u.IsAlive && u.SpellDistance() < (range + radius) && u.HealthPercent < health && requirements(u))
                .ToList();


            // create a iEnumerable of the possible heal targets wtihin range
            IEnumerable<WoWUnit> listOf;
            if (range == 0)
                listOf = new List<WoWUnit>() { Me };
            else if (mainTarget == null)
                listOf = HealerManager.Instance.TargetList.Where(p => p.IsAlive && p.SpellDistance() <= range);
            else
                listOf = mainTarget;

            // now search list finding target with greatest number of heal targets in radius
            var t = listOf
                .Select(p => new
                {
                    Player = p,
                    Count = coveredTargets
                        .Where(pp => pp.IsAlive && pp.SpellDistance(p) < radius)
                        .Count()
                })
                .OrderByDescending(v => v.Count)
                .DefaultIfEmpty(null)
                .FirstOrDefault();

            if (t != null)
            {
                if (t.Count >= minCount)
                {
                    Logger.WriteDebug("GetBestCoverageTarget('{0}'): found {1} with {2} nearby under {3}%", spell, t.Player.SafeName(), t.Count, health);
                    return t.Player;
                }

                if (SingularSettings.DebugSpellCasting)
                {
                    Logger.WriteDebug("GetBestCoverageTarget('{0}'): not enough found - {1} with {2} nearby under {3}%", spell, t.Player.SafeName(), t.Count, health);
                }
            }

            return null;
        }

        /// <summary>
        /// find best Tank target that is missing Heal Over Time passed
        /// </summary>
        /// <param name="hotName">spell name of HoT</param>
        /// <returns>reference to target that needs the HoT</returns>
        public static WoWUnit GetBestTankTargetForHOT(string hotName, float health = 100f)
        {
            WoWUnit hotTarget = null;
            hotTarget = Group.Tanks.Where(u => u.IsAlive && u.Combat && u.HealthPercent < health && u.DistanceSqr < 40 * 40 && !u.HasMyAura(hotName) && u.InLineOfSpellSight).OrderBy(u => u.HealthPercent).FirstOrDefault();
            if (hotTarget != null)
                Logger.WriteDebug("GetBestTankTargetForHOT('{0}'): found tank {1} @ {2:F1}%, hasmyaura={3} with {4} ms left", hotName, hotTarget.SafeName(), hotTarget.HealthPercent, hotTarget.HasMyAura(hotName), (int)hotTarget.GetAuraTimeLeft("Riptide").TotalMilliseconds);
            return hotTarget;
        }

        /// <summary>
        /// selects the Tank we should stay near.  Priority is RaFHelper.Leader, then First Role.Tank either
        /// in combat or 
        /// </summary>
        public static WoWUnit TankToStayNear
        {
            get
            {
                if (!SingularSettings.Instance.StayNearTank)
                    return null;

                if (RaFHelper.Leader != null && RaFHelper.Leader.IsValid && RaFHelper.Leader.IsAlive && (RaFHelper.Leader.Combat || RaFHelper.Leader.Distance < SingularSettings.Instance.MaxHealTargetRange))
                    return RaFHelper.Leader;

                return Group.Tanks.Where(t => t.IsAlive && (t.Combat || t.Distance < SingularSettings.Instance.MaxHealTargetRange)).OrderBy(t => t.Distance).FirstOrDefault();
            }
        }

        /// <summary>
        /// stays within range of Tank as they move.  settings configurable by user.
        /// </summary>
        /// <param name="gapCloser">ability to close distance more quickly than running (such as Roll)</param>
        /// <returns></returns>
        public static Composite CreateStayNearTankBehavior(Composite gapCloser = null)
        {
            int moveNearTank;
            int stopNearTank;

            if (gapCloser == null)
                gapCloser = new ActionAlwaysFail();

            if (!SingularSettings.Instance.StayNearTank)
                return new ActionAlwaysFail();

            if (SingularRoutine.CurrentWoWContext != WoWContext.Instances)
                return new ActionAlwaysFail();

            if (IsThisBehaviorCalledDuringCombat())
            {
                moveNearTank = Math.Max(5, SingularSettings.Instance.StayNearTankRangeCombat);
                stopNearTank = (moveNearTank * 7) / 10;
            }
            else
            {
                moveNearTank = Math.Max(5, SingularSettings.Instance.StayNearTankRangeRest);
                stopNearTank = (moveNearTank * 6) / 10;     // be slightly more elastic at rest
            }

            Logger.WriteDebug("StayNearTank in {0}: will move towards at {1} yds and stop if within {2} yds", Dynamics.CompositeBuilder.CurrentBehaviorType, moveNearTank, stopNearTank);
            return new PrioritySelector(
                ctx => HealerManager.TankToStayNear,

                // no healing needed, then move within heal range of tank
                new ThrottlePasses(
                    1,
                    TimeSpan.FromSeconds(5),
                    RunStatus.Failure,
                        new Action( t => {
                            if (SingularSettings.Debug)
                            {
                                WoWUnit tankToStayNear = (WoWUnit)t;
                                if (t != null)
                                {
                                    ;
                                }
                                else if (!Group.Tanks.Any())
                                    Logger.WriteDiagnostic(Color.HotPink, "TankToStayNear: no group members with Role=Tank");
                                else
                                {
                                    Logger.WriteDebug(Color.HotPink, "TankToStayNear:  {0} tanks in group", Group.Tanks.Count());
                                    int i = 0;
                                    foreach (var tank in Group.Tanks.OrderByDescending(gt => gt == RaFHelper.Leader).ThenBy(gt => gt.DistanceSqr))
                                    {
                                        Logger.WriteDebug(Color.HotPink, "TankToStayNear[{0}]: {1} Health={2:F1}%, Dist={3:F1}, TankPt={4}, MePt={5}, TankMov={6}, MeMov={7}, LoS={8}, LoSS={9}, Combat={10}, MeCombat={11}, ",
                                            i++,
                                            tank.SafeName(),
                                            tank.HealthPercent,
                                            tank.SpellDistance(),
                                            tank.Location,
                                            Me.Location,
                                            tank.IsMoving.ToYN(),
                                            Me.IsMoving.ToYN(),
                                            tank.InLineOfSight.ToYN(),
                                            tank.InLineOfSpellSight.ToYN(),
                                            tank.Combat.ToYN(),
                                            Me.Combat.ToYN()
                                            );
                                    }

                                    Logger.WriteDebug(Color.HotPink, "TankToStayNear: current TargetList has {0} units", Targeting.Instance.TargetList.Count());
                                    i = 0;
                                    foreach (var target in Targeting.Instance.TargetList)
                                    {
                                        Logger.WriteDebug(Color.HotPink, "CurrentTargets[{0}]: {1} {2:F1}% @ {3:F1}",
                                            i++,
                                            target.SafeName(),
                                            target.HealthPercent,
                                            target.SpellDistance()
                                            );
                                    }
                                }
                            }
                            return RunStatus.Failure;
                        })
                    ),
                new Decorator(
                    ret => ((WoWUnit)ret) != null,
                    new Sequence(
                        new PrioritySelector(
                            gapCloser,
                            Movement.CreateMoveToLosBehavior(unit => ((WoWUnit)unit)),
                            Movement.CreateMoveToUnitBehavior(unit => ((WoWUnit)unit), moveNearTank, stopNearTank)
                            // , Movement.CreateEnsureMovementStoppedBehavior(stopNearTank, unit => (WoWUnit)unit, "in heal range of tank")
                            ),
                        new ActionAlwaysFail()
                        )
                    )
                );
        }

        private static bool IsThisBehaviorCalledDuringCombat()
        {
            return (Dynamics.CompositeBuilder.CurrentBehaviorType & BehaviorType.InCombat) != (BehaviorType)0;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gapCloser"></param>
        /// <returns></returns>
        public static Composite CreateMeleeHealerMovementBehavior(Composite gapCloser = null)
        {
            int moveNearTank;
            int stopNearTank;

            if (SingularRoutine.CurrentWoWContext != WoWContext.Instances)
                return new ActionAlwaysFail();

            if (!SingularSettings.Instance.StayNearTank)
                return Movement.CreateMoveBehindTargetBehavior();

            if (gapCloser == null)
                gapCloser = new ActionAlwaysFail();

            bool incombat = (Dynamics.CompositeBuilder.CurrentBehaviorType & BehaviorType.InCombat) != (BehaviorType)0;
            if (incombat)
            {
                moveNearTank = Math.Max(5, SingularSettings.Instance.StayNearTankRangeCombat);
                stopNearTank = Math.Max(moveNearTank / 2, moveNearTank - 5);
            }
            else
            {
                moveNearTank = Math.Max(5, SingularSettings.Instance.StayNearTankRangeRest);
                stopNearTank = Math.Max(moveNearTank / 2, moveNearTank - 5);
            }

            return new PrioritySelector(
                ctx => (Me.Combat && Me.CurrentTarget != null && Unit.ValidUnit(Me.CurrentTarget))
                    ? Me.CurrentTarget
                    : null,
                new Decorator(
                    ret => ret != null,
                    new Sequence(
                        new PrioritySelector(
                            gapCloser,
                            Movement.CreateMoveToLosBehavior(),
                            Movement.CreateMoveToMeleeBehavior(true),
                            // to account for Mistweaver Monks facing Soothing Mist target automatically
                            new Decorator(
                                req => !Spell.IsChannelling(),
                                Movement.CreateFaceTargetBehavior()
                                )
                            ),                        
                        new ActionAlwaysFail()
                        )
                    ),
                new Decorator(
                    ret => ret == null,
                    new Sequence(
                        CreateStayNearTankBehavior(gapCloser),
                        new ActionAlwaysFail()
                        )
                    )
                );
        }

        public static Composite CreateAttackEnsureTarget()
        {
            if (SingularSettings.DisableAllTargeting || SingularRoutine.CurrentWoWContext != WoWContext.Instances)
                return new ActionAlwaysFail();

            return new PrioritySelector(
                new Decorator(
                    req => Me.GotTarget && !Me.CurrentTarget.IsPlayer,
                    new PrioritySelector(
                        ctx => Unit.HighestHealthMobAttackingTank(),
                        new Decorator(
                            req => req != null && Me.CurrentTargetGuid != ((WoWUnit)req).Guid && (Me.CurrentTarget.HealthPercent + 10) < ((WoWUnit)req).HealthPercent,
                            new Sequence(
                                new Action(on =>
                                {
                                    Logger.Write(Color.LightCoral, "switch to highest health mob {0} @ {1:F1}%", ((WoWUnit)on).SafeName(), ((WoWUnit)on).HealthPercent);
                                    ((WoWUnit)on).Target();
                                }),
                                new Wait(1, req => Me.CurrentTargetGuid == ((WoWUnit)req).Guid, new ActionAlwaysFail())
                                )
                            )
                        )
                    ),
                new Decorator(
                    req => !Me.GotTarget,
                    new Sequence(
                        ctx => Unit.HighestHealthMobAttackingTank(),
                        new Action(on =>
                        {
                            Logger.Write(Color.LightCoral, "target highest health mob {0} @ {1:F1}%", ((WoWUnit)on).SafeName(), ((WoWUnit)on).HealthPercent);
                            ((WoWUnit)on).Target();
                        }),
                        new Wait(1, req => Me.CurrentTargetGuid == ((WoWUnit)req).Guid, new ActionAlwaysFail())
                        )
                    )
                );
        }


        #region Off-heal Checks and Control

        private static bool EnableOffHeal
        {
            get
            {
                if (!SingularSettings.Instance.DpsOffHealAllowed)
                    return false;

                if (Me.GroupInfo.IsInRaid)
                    return false;

                WoWUnit first = HealerManager.Instance.FirstUnit;
                if (first != null)
                {
                    double health = first.PredictedHealthPercent(includeMyHeals: true);
                    if (health < SingularSettings.Instance.DpsOffHealBeginPct)
                    {
                        Logger.WriteDiagnostic("EnableOffHeal: entering off-heal mode since {0} @ {1:F1}%", first.SafeName(), health);
                        return true;
                    }
                }
                
                return false;
            }
        }

        private static bool DisableOffHeal
        {
            get
            {
                if (!SingularSettings.Instance.DpsOffHealAllowed)
                    return true;

                if (Me.GroupInfo.IsInRaid)
                    return true;

                WoWUnit healer = null;
                if (SingularRoutine.CurrentWoWContext != WoWContext.Normal)
                {
                    healer = Group.Healers.FirstOrDefault(h => h.IsAlive && h.Distance < SingularSettings.Instance.MaxHealTargetRange);
                    if (healer == null)
                        return false;
                }

                WoWUnit lowest = FindLowestHealthTarget();
                if (lowest != null && lowest.HealthPercent <= SingularSettings.Instance.DpsOffHealEndPct)
                    return false;

                if (SingularRoutine.CurrentWoWContext == WoWContext.Normal)
                    Logger.WriteDiagnostic("DisableOffHeal: leaving off-heal mode since lowest target is {0} @ {1:F1}% and solo", lowest.SafeName(), lowest.HealthPercent);
                else 
                    Logger.WriteDiagnostic("DisableOffHeal: leaving off-heal mode since lowest target is {0} @ {1:F1}% and {2} is {3:F1} yds away", lowest.SafeName(), lowest.HealthPercent, healer.SafeName(), healer.Distance);
                return true;
            }
        }

        private static bool _actingHealer = false;

        public static bool ActingAsOffHealer
        {
            get
            {
                if (!_actingHealer && EnableOffHeal)
                {
                    _actingHealer = true;
                    Logger.WriteDiagnostic("ActingAsOffHealer: offheal enabled");
                }
                else if (_actingHealer && DisableOffHeal)
                {
                    _actingHealer = false;
                    Logger.WriteDiagnostic("ActingAsOffHealer: offheal disabled");
                }
                return _actingHealer;
            }
        }

        #endregion  
    }

    /// <summary>
    /// cached result of loc(ret), desc(ret), etc for Spell.CastOnGround.  for expensive queries (such as Cluster.GetBestUnitForCluster()) we want to avoid
    /// performing them multiple times.  in some cases we were caching that locally in the context parameter of a wrapping PrioritySelector
    /// but doing it here enforces for all calls, so will reduce list scans and cycles required even for targets selected by auras present/absent
    /// </summary>
    internal class HmmContext
    {
        internal WoWPoint loc;
        internal WoWUnit target;
        internal WoWUnit tank;
        internal bool behind;
        internal object context;

        // always create passing the existing context so it is preserved for delegate usage
        internal HmmContext(object ctx)
        {
            context = ctx;
        }

        // always create passing the existing context so it is preserved for delegate usage
        internal HmmContext(WoWUnit tank, WoWUnit target, object ctx)
        {
            this.tank = tank;
            this.target = target;
            this.context = ctx;
            behind = target != null && target.IsBoss() && !Singular.Lists.BossList.AvoidRearBosses.Contains(target.Entry);
        }
    }

    class PrioritizedBehaviorList
    {
        class PrioritizedBehavior
        {
            public int Priority { get; set; }
            public string Name { get; set; }
            public Composite behavior { get; set; }

            public PrioritizedBehavior(int p, string s, Composite bt)
            {
                Priority = p;
                Name = s;
                behavior = bt;
            }
        }

        List<PrioritizedBehavior> blist = new List<PrioritizedBehavior>();

        public void AddBehavior(int pri, string behavName, string spellName, Composite bt)
        {
            if (pri == 0)
                Logger.WriteDebug("Skipping Behavior [{0}] configured for Priority {1}", behavName, pri);
            else if (!String.IsNullOrEmpty(spellName) && !SpellManager.HasSpell(spellName))
                Logger.WriteDebug("Skipping Behavior [{0}] since spell '{1}' is not known by this character", behavName, spellName);
            else
                blist.Add(new PrioritizedBehavior(pri, behavName, bt));
        }

        public void OrderBehaviors()
        {
            blist = blist.OrderByDescending(b => b.Priority).ToList();
        }

        public Composite GenerateBehaviorTree()
        {
            if (!SingularSettings.Debug)
                return new PrioritySelector(blist.Select(b => b.behavior).ToArray());

            PrioritySelector pri = new PrioritySelector();
            foreach (PrioritizedBehavior pb in blist)
            {
                pri.AddChild(new CallTrace(pb.Name, pb.behavior));
            }

            return pri;
        }

        public void ListBehaviors()
        {
            if (Dynamics.CompositeBuilder.SilentBehaviorCreation)
                return;

            foreach (PrioritizedBehavior hs in blist)
            {
                Logger.WriteDebug(Color.GreenYellow, "   Priority {0} for Behavior [{1}]", hs.Priority.ToString().AlignRight(4), hs.Name);
            }
        }
    }

}