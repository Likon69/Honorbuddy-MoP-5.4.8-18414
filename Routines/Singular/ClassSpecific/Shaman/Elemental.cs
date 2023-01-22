using System.Linq;

using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Settings;
using Singular.Managers;
using Styx.Common;
using System.Drawing;
using System;
using Styx.Helpers;
using CommonBehaviors.Actions;


namespace Singular.ClassSpecific.Shaman
{
    public class Elemental
    {
        #region Common

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static ShamanSettings ShamanSettings { get { return SingularSettings.Instance.Shaman(); } }

        // private static int NormalPullDistance { get { return Math.Max( 35, CharacterSettings.Instance.PullDistance); } }

        [Behavior(BehaviorType.PreCombatBuffs | BehaviorType.CombatBuffs, WoWClass.Shaman, WoWSpec.ShamanElemental, WoWContext.Normal|WoWContext.Instances)]
        public static Composite CreateElementalPreCombatBuffsNormal()
        {
            return new PrioritySelector(
                Common.CreateShamanImbueMainHandBehavior(Imbue.Flametongue),

                Common.CreateShamanDpsShieldBehavior(),
                Totems.CreateRecallTotems()
                );
        }

        [Behavior(BehaviorType.PreCombatBuffs | BehaviorType.CombatBuffs, WoWClass.Shaman, WoWSpec.ShamanElemental, WoWContext.Battlegrounds )]
        public static Composite CreateElementalPreCombatBuffsPvp()
        {
            return new PrioritySelector(
                Common.CreateShamanImbueMainHandBehavior(Imbue.Flametongue),

                Common.CreateShamanDpsShieldBehavior(),
                Totems.CreateRecallTotems()
                );
        }

        [Behavior(BehaviorType.Rest, WoWClass.Shaman, WoWSpec.ShamanElemental)]
        public static Composite CreateElementalRest()
        {
            return new PrioritySelector(
                Spell.WaitForCast(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Common.CreateShamanDpsHealBehavior(),

                        Rest.CreateDefaultRestBehaviour("Healing Surge", "Ancestral Spirit"),

                        Common.CreateShamanMovementBuff()
                        )
                    )
                );
        }

        /// <summary>
        /// perform diagnostic output logging at highest priority behavior that occurs while in Combat
        /// </summary>
        /// <returns></returns>
        [Behavior(BehaviorType.Heal | BehaviorType.Pull, WoWClass.Shaman, WoWSpec.ShamanElemental, WoWContext.All, 999)]
        public static Composite CreateElementalLogDiagnostic()
        {
            return CreateElementalDiagnosticOutputBehavior();
        }


        [Behavior(BehaviorType.Heal, WoWClass.Shaman, WoWSpec.ShamanElemental)]
        public static Composite CreateElementalHeal()
        {
            return Common.CreateShamanDpsHealBehavior( );
        }

        #endregion

        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Shaman, WoWSpec.ShamanElemental, WoWContext.Normal)]
        public static Composite CreateElementalNormalPull()
        {
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCastOrChannel(),

                new Decorator( 
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Common.CreateShamanDpsShieldBehavior(),

                        // grinding or questing, if target meets these cast Flame Shock if possible
                        // 1. mob is less than 12 yds, so no benefit from delay in Lightning Bolt missile arrival
                        // 2. area has another player competing for mobs (we want to tag the mob quickly)
                        new Decorator(
                            ret =>{
                                if (StyxWoW.Me.CurrentTarget.IsHostile && StyxWoW.Me.CurrentTarget.Distance < 12)
                                {
                                    Logger.WriteDiagnostic("NormalPull: fast pull since hostile target is {0:F1} yds away", StyxWoW.Me.CurrentTarget.Distance);
                                    return true;
                                }
                                WoWPlayer nearby = ObjectManager.GetObjectsOfType<WoWPlayer>(true, false).FirstOrDefault(p => !p.IsMe && p.DistanceSqr <= 40 * 40);
                                if (nearby != null)
                                {
                                    Logger.WriteDiagnostic("NormalPull: fast pull since player {0} nearby @ {1:F1} yds", nearby.SafeName(), nearby.Distance);
                                    return true;
                                }
                                return false;
                                },
                            new PrioritySelector(
                                // have a big attack loaded up, so don't waste it
                                Spell.Cast("Earth Shock", ret => StyxWoW.Me.HasAura("Lightning Shield", 5)),
                                Spell.Buff("Flame Shock", true, req => SpellManager.HasSpell("Lava Burst")),
                                Spell.Cast("Unleash Weapon", ret => Common.IsImbuedForDPS(StyxWoW.Me.Inventory.Equipped.MainHand)),
                                Spell.Cast("Earth Shock", ret => !SpellManager.HasSpell("Flame Shock"))
                                )
                            ),

                        Totems.CreateTotemsBehavior(),

                        // have a big attack loaded up, so don't waste it
                        Spell.Cast("Earth Shock", ret => StyxWoW.Me.HasAura("Lightning Shield", 5)),

                        // otherwise, start with Lightning Bolt so we can follow with an instant
                        // to maximize damage at initial aggro
                        Spell.Cast("Lightning Bolt"),

                        // we are moving so throw an instant of some type
                        Spell.Buff("Flame Shock", true, req => SpellManager.HasSpell("Lava Burst")),
                        Spell.Buff("Lava Burst", true, req => Me.GotTarget && Me.CurrentTarget.HasMyAura("Flame Shock")),
                        Spell.Cast("Earth Shock"),
                        Spell.Cast("Unleash Weapon", ret => Common.IsImbuedForDPS(StyxWoW.Me.Inventory.Equipped.MainHand))
                        )
                    )

                // Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 38f, 33f)
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Shaman, WoWSpec.ShamanElemental, WoWContext.Normal)]
        public static Composite CreateElementalNormalCombat()
        {
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCastOrChannel(),

                new Decorator( 
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Helpers.Common.CreateInterruptBehavior(),
                        Dispelling.CreatePurgeEnemyBehavior("Purge"),

                        new Decorator( 
                            ret => Common.GetImbue( StyxWoW.Me.Inventory.Equipped.MainHand) == Imbue.None,
                            Common.CreateShamanImbueMainHandBehavior(Imbue.Flametongue)),

                        Common.CreateShamanDpsShieldBehavior(),

                        Spell.BuffSelf("Thunderstorm", ret => Unit.NearbyUnfriendlyUnits.Count( u => u.Distance < 10f ) >= 3),

                        Totems.CreateTotemsBehavior(),

                        new Decorator(
                            ret => Spell.UseAOE && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3 && !Unit.UnfriendlyUnitsNearTarget(10f).Any(u => u.IsCrowdControlled()),
                            new PrioritySelector(
                                new Action( act => { Logger.WriteDebug("performing aoe behavior"); return RunStatus.Failure; }),

                                Spell.CastOnGround("Earthquake", 
                                    on => StyxWoW.Me.CurrentTarget,
                                    req => StyxWoW.Me.CurrentTarget != null 
                                        && StyxWoW.Me.CurrentTarget.Distance < 34
                                        && (StyxWoW.Me.ManaPercent > 60 || StyxWoW.Me.HasAura( "Clearcasting")) 
                                        && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 6),

                                Spell.Cast("Chain Lightning", ret => Clusters.GetBestUnitForCluster(Unit.UnfriendlyUnitsNearTarget(15f), ClusterType.Chained, 12))
                                )
                            ),

                        Spell.Cast("Elemental Blast", on => Me.CurrentTarget, req => true, cancel => false),
                        Spell.Cast("Unleash Elements", ret => Common.HasTalent(ShamanTalents.UnleashedFury)),

                        Spell.Buff("Flame Shock", true, req => SpellManager.HasSpell("Lava Burst") || Me.CurrentTarget.TimeToDeath(-1) > 30),

                        Spell.Cast("Lava Burst", on => Me.CurrentTarget, req => true, cancel => false),
                        Spell.Cast("Earth Shock",
                            ret => StyxWoW.Me.HasAura("Lightning Shield", 5) &&
                                   StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Flame Shock", true).TotalSeconds > 3),
                        Spell.Cast("Earth Shock", req => !SpellManager.HasSpell("Lava Burst")),

                        Spell.Cast("Unleash Elements", ret => 
                            StyxWoW.Me.IsMoving &&
                            !Spell.HaveAllowMovingWhileCastingAura() &&
                            Common.IsImbuedForDPS( StyxWoW.Me.Inventory.Equipped.MainHand)),

                        Spell.Cast("Chain Lightning", ret => Spell.UseAOE && Spell.UseAOE && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 2 && !Unit.UnfriendlyUnitsNearTarget(10f).Any(u => u.IsCrowdControlled())),
                        Spell.Cast("Lightning Bolt")
                        )
                    )

                // Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 38f, 33f)
                // Movement.CreateMoveToRangeAndStopBehavior(ret => Me.CurrentTarget, ret => NormalPullDistance)
                );
        }

        #endregion

        #region Battleground Rotation

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Shaman, WoWSpec.ShamanElemental, WoWContext.Battlegrounds)]
        public static Composite CreateElementalPvPPullAndCombat()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCastOrChannel(),

                new Decorator( 
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Helpers.Common.CreateInterruptBehavior(),
                        Dispelling.CreatePurgeEnemyBehavior("Purge"),

                        new Decorator(
                            ret => Common.GetImbue(StyxWoW.Me.Inventory.Equipped.MainHand) == Imbue.None,
                            Common.CreateShamanImbueMainHandBehavior(Imbue.Flametongue)),

                        Common.CreateShamanDpsShieldBehavior(),

                        Totems.CreateTotemsPvPBehavior(),

                        // Burst if 7 Stacks
                        new Decorator(
                            ret => Me.GotTarget && Me.CurrentTarget.SpellDistance() < 40 && Me.HasAura("Lightning Shield", 7) && Spell.GetSpellCooldown("Earth Shock") == TimeSpan.Zero,
                            new PrioritySelector(
                                new Action( r => { Logger.Write( Color.White, "Burst Rotation"); return RunStatus.Failure;} ),
                                Spell.Cast( "Unleash Elements", ret => Common.IsImbuedForDPS( Me.Inventory.Equipped.MainHand)),
                                Spell.Cast( "Elemental Blast"),
                                Spell.Cast( "Lava Burst"),
                                Spell.BuffSelf("Ascendance", req => ShamanSettings.UseAscendance),       // this is more to buff following sequence since we leave burst after Earth Shock
                                Spell.Cast( "Earth Shock")
                                // Spell.Cast( "Lightning Bolt")       // filler in case Shocks on cooldown
                                )
                            ),

                        // If targeted, cast as many instants as possible
                        new Decorator(
                            ret => !Unit.NearbyUnfriendlyUnits.Any( u => u.CurrentTargetGuid == Me.Guid ),
                            new PrioritySelector(
                                new Decorator(
                                    ret => !Me.HasAura("Lightning Shield",  7),
                                    new PrioritySelector(
                                        Spell.Buff("Flame Shock", true, on => Me.CurrentTarget, req => true, 9),
                                        Spell.Buff("Flame Shock", true, on => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => Me.IsSafelyFacing(u) && u.InLineOfSpellSight), req => Spell.GetSpellCastTime("Lava Burst") != TimeSpan.Zero)
                                        )
                                    ),
                                Spell.Cast("Unleash Elements", req => Common.IsImbuedForDPS(StyxWoW.Me.Inventory.Equipped.MainHand) && !Me.HasAura("Lightning Shield", 4)),
                                Spell.Cast("Lava Burst", ret => Spell.GetSpellCastTime("Lava Burst") == TimeSpan.Zero),
                                Spell.Cast("Lava Beam"),
                                Spell.BuffSelf("Searing Totem", ret => Me.GotTarget && Me.CurrentTarget.Distance < Totems.GetTotemRange(WoWTotem.Searing) && !Totems.Exist( WoWTotemType.Fire)),
                                Spell.BuffSelf("Thunderstorm", ret => Unit.NearbyUnfriendlyUnits.Any( u => u.IsWithinMeleeRange )),
                                Spell.Cast("Primal Strike") // might as well
                                )
                            ),

                        // Else cast freely

                        Spell.Cast("Unleash Elements", req => Common.IsImbuedForDPS(StyxWoW.Me.Inventory.Equipped.MainHand) && !Me.HasAura("Lightning Shield", 4)),
                        Spell.Cast("Elemental Blast", ret => !Me.HasAura("Lightning Shield", 5)),
                        Spell.Buff("Flame Shock", true, on => Me.CurrentTarget, req => true, 9),
                        Spell.Buff("Flame Shock", true, on => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => Me.IsSafelyFacing(u) && u.InLineOfSpellSight), req => Spell.GetSpellCastTime("Lava Burst") != TimeSpan.Zero),
                        Spell.Cast("Lava Burst"),
                        Spell.BuffSelf("Searing Totem", ret => Me.GotTarget && Me.CurrentTarget.Distance < Totems.GetTotemRange(WoWTotem.Searing) && !Totems.Exist(WoWTotemType.Fire)),
                        Spell.Cast("Lightning Bolt")
                        )
                    )
                );
        }

        #endregion

        #region Instance Rotation

        private static bool _doWeWantAcendance;

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Shaman, WoWSpec.ShamanElemental, WoWContext.Instances)]
        public static Composite CreateElementalInstancePullAndCombat()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Movement.CreateEnsureMovementStoppedBehavior(33f),

                Spell.WaitForCastOrChannel(),

                new PrioritySelector(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Helpers.Common.CreateInterruptBehavior(),
                        Dispelling.CreatePurgeEnemyBehavior("Purge"),

                        Common.CreateShamanImbueMainHandBehavior(Imbue.Flametongue),

                        Common.CreateShamanDpsShieldBehavior(),

                        Totems.CreateTotemsInstanceBehavior(),

                        new Decorator(
                            ret => Spell.UseAOE && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3 && !Unit.UnfriendlyUnitsNearTarget(10f).Any(u => u.IsCrowdControlled()),
                            new PrioritySelector(
                                new Action(act => { Logger.WriteDebug("performing aoe behavior"); return RunStatus.Failure; }),
                                Spell.CastOnGround("Earthquake", ret => StyxWoW.Me.CurrentTarget.Location),
                                Spell.Cast("Chain Lightning", ret => Clusters.GetBestUnitForCluster(Unit.UnfriendlyUnitsNearTarget(15f), ClusterType.Chained, 12))
                                )),

                        Spell.Cast("Elemental Blast", on => Me.CurrentTarget, req => true, cancel => false),

                        Spell.Buff("Flame Shock", true, on => Me.CurrentTarget, req => true, 3),

                        Spell.Cast("Unleash Elements", ret => Common.HasTalent(ShamanTalents.UnleashedFury)),

                        Spell.OffGCD(Spell.Cast("Ascendance", req => ShamanSettings.UseAscendance && Me.CurrentTarget.IsBoss() && Me.CurrentTarget.SpellDistance() < 40 && !Me.IsMoving)),

                        Spell.Cast("Lava Burst", on => Me.CurrentTarget, req => true, cancel => false),
                        Spell.Cast("Earth Shock",
                            ret => Me.HasAura("Lightning Shield", 5)
                                && Me.CurrentTarget.GetAuraTimeLeft("Flame Shock", true).TotalSeconds > 3),
                        Spell.Cast("Unleash Elements",
                            ret => StyxWoW.Me.IsMoving
                                && !Spell.HaveAllowMovingWhileCastingAura()
                                && Common.IsImbuedForDPS(StyxWoW.Me.Inventory.Equipped.MainHand)),
                        Spell.Cast("Chain Lightning", ret => Spell.UseAOE && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 2 && !Unit.UnfriendlyUnitsNearTarget(10f).Any(u => u.IsCrowdControlled())),
                        Spell.Cast("Lightning Bolt")
                        )
                    )
                );
        }

        #endregion

        #region Diagnostics

        private static Composite CreateElementalDiagnosticOutputBehavior()
        {
            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            return new ThrottlePasses(1, 1,
                new Action(ret =>
                {
                    uint stks = 0;
                    string shield;

                    WoWAura aura = Me.GetAuraByName("Lightning Shield");
                    if (aura != null)
                    {
                        stks = aura.StackCount;
                        if (!Me.HasAura("Lightning Shield", (int)stks))
                            Logger.WriteDebug(Color.MediumVioletRed, "Inconsistancy Error:  have {0} stacks but Me.HasAura('Lightning Shield', {0}) was False!!!!", stks, stks);
                    }
                    else
                    {
                        aura = Me.GetAuraByName("Water Shield");
                        if (aura == null ) 
                        {
                            aura = Me.GetAuraByName("Earth Shield");
                            if (aura != null)
                                stks = aura.StackCount;
                        }
                    }

                    shield = aura == null ? "(null)" : aura.Name;
                        
                    string line = string.Format(".... [{0}] h={1:F1}%/m={2:F1}%, shield={3}, stks={4}, moving={5}",
                        CompositeBuilder.CurrentBehaviorType.ToString(),
                        Me.HealthPercent,
                        Me.ManaPercent,
                        shield,
                        stks,
                        Me.IsMoving.ToYN()
                        );

                    WoWUnit target = Me.CurrentTarget;
                    if (target == null)
                        line += ", target=(null)";
                    else
                        line += string.Format(", target={0} @ {1:F1} yds, th={2:F1}%, face={3} loss={4}, fs={5}",
                            target.SafeName(),
                            target.Distance,
                            target.HealthPercent,
                            Me.IsSafelyFacing(target).ToYN(),
                            target.InLineOfSpellSight.ToYN(),
                            (long)target.GetAuraTimeLeft("Flame Shock").TotalMilliseconds
                            );

                    Logger.WriteDebug(Color.Yellow, line);
                    return RunStatus.Failure;
                })
                );
        }

        #endregion
    }
}
