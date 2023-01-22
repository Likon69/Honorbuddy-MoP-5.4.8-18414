using System;
using System.Linq;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;

using Styx;

using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Singular.Settings;

using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using Styx.Common;
using System.Collections.Generic;
using CommonBehaviors.Actions;
using System.Drawing;


namespace Singular.ClassSpecific.Warlock
{
    public class Affliction
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WarlockSettings WarlockSettings { get { return SingularSettings.Instance.Warlock(); } }

        private static int _mobCount;

        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Warlock, WoWSpec.WarlockAffliction, WoWContext.All)]
        public static Composite CreateWarlockAfflictionNormalPull()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCast(FaceDuring.Yes),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        new Action(ret =>
                        {
                            _mobCount = Common.TargetsInCombat.Count();
                            return RunStatus.Failure;
                        }),

                        CreateWarlockDiagnosticOutputBehavior("Pull"),
                        Helpers.Common.CreateAutoAttack(true),
                        CreateApplyDotsBehavior(onUnit => Me.CurrentTarget, ret => true)
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Warlock, WoWSpec.WarlockAffliction, priority: 999)]
        public static Composite CreateWarlockAfflictionHeal()
        {
            return CreateWarlockDiagnosticOutputBehavior("Combat");
        }


        [Behavior(BehaviorType.Combat, WoWClass.Warlock, WoWSpec.WarlockAffliction, WoWContext.Normal)]
        [Behavior(BehaviorType.Combat, WoWClass.Warlock, WoWSpec.WarlockAffliction, WoWContext.Instances)]
        public static Composite CreateWarlockAfflictionNormalCombat()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Helpers.Common.CreateAutoAttack(true),

                // Movement.CreateEnsureMovementStoppedBehavior(35f),

                new Action(r => { if ( Me.GotTarget) Me.CurrentTarget.TimeToDeath(); return RunStatus.Failure; } ),

                // cancel an early drain soul if done to proc 1 soulshard
                new Decorator(
                    ret => Me.GotTarget && Me.ChanneledSpell != null,
                    new PrioritySelector(
                        new Decorator(
                            ret => Me.ChanneledSpell.Name == "Drain Soul"
                                && Me.CurrentSoulShards > 0
                                && Me.CurrentTarget.HealthPercent > 20 && SpellManager.HasSpell("Malefic Grasp"),
                            new Sequence(
                                new Action(ret => Logger.WriteDebug("/cancel Drain Soul on {0} now we have {1} shard", Me.CurrentTarget.SafeName(), Me.CurrentSoulShards)),
                                new Action(ret => SpellManager.StopCasting()),
                                new WaitContinue(TimeSpan.FromMilliseconds(500), ret => Me.ChanneledSpell == null, new ActionAlwaysSucceed())
                                )
                            ),

                        // cancel malefic grasp if target health < 20% and cast drain soul (revisit and add check for minimum # of dots)
                        new Decorator(
                            ret => Me.ChanneledSpell.Name == "Malefic Grasp"
                                // && Me.CurrentSoulShards < Me.MaxSoulShards
                                && Me.CurrentTarget.HealthPercent <= 20,
                            new Sequence(
                                new Action(ret => Logger.WriteDebug("/cancel Malefic Grasp on {0} @ {1:F1}%", Me.CurrentTarget.SafeName(), Me.CurrentTarget.HealthPercent)),
                                new Action(ret => SpellManager.StopCasting()),
                // Helpers.Common.CreateWaitForLagDuration( ret => Me.ChanneledSpell == null ),
                                new WaitContinue(TimeSpan.FromMilliseconds(500), ret => Me.ChanneledSpell == null, new ActionAlwaysSucceed()),
                                Spell.Cast("Drain Soul", ret => Me.CurrentTarget.HasAnyAura("Agony", "Corruption", "Haunt", "Unstable Affliction"))
                                )
                            )
                        )
                    ),

                Spell.WaitForCastOrChannel(),
                Helpers.Common.CreateAutoAttack(true),

                new Decorator(ret => !Spell.IsGlobalCooldown(),

                    new PrioritySelector(
                        Helpers.Common.CreateInterruptBehavior(),

                        new Action(ret =>
                        {
                            _mobCount = Common.TargetsInCombat.Count();
                            return RunStatus.Failure;
                        }),

                        CreateAoeBehavior(),

                        // following Drain Soul only while Solo combat to maximize Soul Shard generation
                        Spell.Cast("Drain Soul",
                            ret => !Me.IsInGroup()
                                && Me.CurrentTarget.HealthPercent < 5
                                && !Me.CurrentTarget.IsPlayer
                                && !Me.CurrentTarget.Elite
                                && Me.CurrentSoulShards < 1),

                        CreateApplyDotsBehavior(
                            on => Me.CurrentTarget,
                //                            ret => Me.CurrentTarget.HealthPercent < 20 || Me.CurrentTarget.HasAnyAura("Agony", "Corruption", "Unstable Affliction")),
                            req => (Me.CurrentTarget.IsPlayer || Me.CurrentTarget.HealthPercent > 20 || Me.TimeToDeath() > 15)
                                && !Me.CurrentTarget.HasAnyOfMyAuras("Agony", "Corruption", "Unstable Affliction", "Haunt")),

                        Spell.Cast("Malefic Grasp", ret => Me.CurrentTarget.HealthPercent > 20),
                        Spell.Cast("Shadow Bolt", ret => !SpellManager.HasSpell("Malefic Grasp")),
                        Spell.Cast("Drain Soul"),

                        Spell.Cast("Fel Flame", ret => Me.IsMoving),

                        // only a lowbie should hit this
                        Spell.Cast("Drain Life", ret => !SpellManager.HasSpell("Malefic Grasp"))
                        )
                    )
                );

        }


        [Behavior(BehaviorType.Combat, WoWClass.Warlock, WoWSpec.WarlockAffliction, WoWContext.Battlegrounds )]
        public static Composite CreateWarlockAfflictionPvpCombat()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                // cancel an early drain soul if done to proc 1 soulshard
                new Decorator(
                    ret => Me.GotTarget && Me.ChanneledSpell != null,
                    new PrioritySelector(
                        new Decorator(
                            ret => Me.ChanneledSpell.Name == "Drain Soul"
                                && Me.CurrentSoulShards > 0
                                && Me.CurrentTarget.HealthPercent > 20 && SpellManager.HasSpell("Malefic Grasp"),
                            new Sequence(
                                new Action(ret => Logger.WriteDebug("/cancel Drain Soul on {0} now we have {1} shard", Me.CurrentTarget.SafeName(), Me.CurrentSoulShards)),
                                new Action(ret => SpellManager.StopCasting()),
                                new WaitContinue(TimeSpan.FromMilliseconds(500), ret => Me.ChanneledSpell == null, new ActionAlwaysSucceed())
                                )
                            ),

                        // cancel malefic grasp if target health < 20% and cast drain soul (revisit and add check for minimum # of dots)
                        new Decorator(
                            ret => Me.ChanneledSpell.Name == "Malefic Grasp"
                                && Me.CurrentSoulShards < Me.MaxSoulShards
                                && Me.CurrentTarget.HealthPercent <= 20,
                            new Sequence(
                                new Action(ret => Logger.WriteDebug("/cancel Malefic Grasp on {0} @ {1:F1}%", Me.CurrentTarget.SafeName(), Me.CurrentTarget.HealthPercent)),
                                new Action(ret => SpellManager.StopCasting()),
                                // Helpers.Common.CreateWaitForLagDuration( ret => Me.ChanneledSpell == null ),
                                new WaitContinue(TimeSpan.FromMilliseconds(500), ret => Me.ChanneledSpell == null, new ActionAlwaysSucceed()),
                                Spell.Cast("Drain Soul", ret => Me.CurrentTarget.HasAnyAura("Agony", "Corruption", "Haunt", "Unstable Affliction"))
                                )
                            )
                        )
                    ),

                Spell.WaitForCastOrChannel(),
                Helpers.Common.CreateAutoAttack(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),

                    new PrioritySelector(
                        Helpers.Common.CreateInterruptBehavior(),

                        new Action(ret =>
                        {
                            _mobCount = Common.TargetsInCombat.Count();
                            return RunStatus.Failure;
                        }),

#if NOT_NOW
                        new Throttle( 4, 
                            Spell.Cast( "Curse of Enfeeblement", 
                                on => Unit.NearbyUnfriendlyUnits
                                        .Where( u => !u.IsCrowdControlled() && u.IsTargetingMeOrPet 
                                            && ((u.PowerType != WoWPowerType.Mana && !u.HasAura("Weakened Blows")) || (u.PowerType == WoWPowerType.Mana && !u.HasAura("Slow Casting"))))
                                        .OrderBy( u => u.Distance )
                                        .FirstOrDefault()
                                )
                            ),
#endif
                        // make sure Primary Target is loaded up with DoTs
                        CreateApplyDotsBehaviorPvp(on => Me.CurrentTarget, ret => true),

                        // Glyph of Siphon Life? then spam Corruption around....
                        new Decorator(
                            ret => TalentManager.HasGlyph( "Siphon Life"),
                            Spell.Buff( "Corruption", true, ctx => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.HasAuraExpired("Corruption", 2)), req => true, 2)
                            ),

                        // now go around the room with instant DoTs
                        new PrioritySelector(
                            ctx => Unit.NearbyUnfriendlyUnits.FirstOrDefault( u => !u.HasAllMyAuras( "Corruption", "Agony")),
                            CreateApplyDotsBehaviorPvp( on => (WoWUnit) on, ret => true)
                            ),

                        // fear target if targeting me and not cc'd (prio by distance)
                        new Throttle(2, 8,
                            new PrioritySelector(
                                ctx => Unit.NearbyUnfriendlyUnits
                                        .Where(u => !u.IsCrowdControlled() && u.CurrentTargetGuid == Me.Guid)
                                        .OrderBy(u => u.Distance)
                                        .FirstOrDefault(),

                                Spell.Buff("Howl of Terror", on => (WoWUnit)on, req => Spell.IsSpellOnCooldown("Fear") || 1 < Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet && Me.SpellDistance(u) < 10f)),
                                Spell.Buff("Mortal Coil", on => (WoWUnit)on, req => Me.HealthPercent < 70),
                                Spell.Buff("Fear", on => Common.GetBestFearTarget())
                                )
                            ),

                        // now try to spread some affliction
                        new PrioritySelector(
                            ctx => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => !u.HasAllMyAuras("Unstable Affliction")),
                            CreateApplyDotsBehaviorPvp(on => (WoWUnit)on, ret => true)
                            ),

                        Spell.Cast("Malefic Grasp", ret => Me.CurrentTarget.HealthPercent > 20),
                        Spell.Cast("Shadow Bolt", ret => !SpellManager.HasSpell("Malefic Grasp")),
                        Spell.Cast("Drain Soul"),

                        // only a lowbie should hit this
                        Spell.Cast("Drain Life", ret => !SpellManager.HasSpell("Malefic Grasp"))
                        )
                    )
                );

        }

        public static Composite CreateAoeBehavior()
        {
            return new Decorator(
                ret => Spell.UseAOE,
                new PrioritySelector(

                    new Decorator(
                        ret => _mobCount >= 4 && SpellManager.HasSpell("Seed of Corruption"),
                        new PrioritySelector(
                            // if current target doesn't have CotE, then Soulburn+CotE
                            new Decorator(
                                req => !Me.CurrentTarget.HasAura("Curse of the Elements"),
                                new Sequence(
                                    Common.CreateCastSoulburn(req => true),
                                    Spell.Buff("Curse of the Elements")
                                    )
                                ),
                            // roll SoC on targets in combat that we are facing
                            new PrioritySelector(
                                ctx => Common.TargetsInCombat.FirstOrDefault(m => !m.HasAura("Seed of Corruption")),
                                new Sequence(
                                    new PrioritySelector(
                                        Common.CreateCastSoulburn(req => req != null),
                                        new ActionAlwaysSucceed()
                                        ),
                                    Spell.Cast("Seed of Corruption", on => (WoWUnit)on)
                                    )
                                )
                            )
                        ),
                    new Decorator(
                        ret => _mobCount >= 2,
                        new PrioritySelector(
                            CreateApplyDotsBehavior(ctx => Common.TargetsInCombat.FirstOrDefault(m => m.HasAuraExpired("Agony", 2)), soulBurn => true),
                            // CreateApplyDotsBehavior(ctx => Common.TargetsInCombat.FirstOrDefault(m => m.HasAuraExpired("Corruption", 0)), soulBurn => true)
                            CreateApplyDotsBehavior(ctx => Common.TargetsInCombat.FirstOrDefault(m => m.HasAuraExpired("Unstable Affliction", 2)), soulBurn => true)
                            )
                        )
                    )
                );
        }

        private static int _dotCount = 0;

        public static Composite CreateApplyDotsBehavior(UnitSelectionDelegate onUnit, SimpleBooleanDelegate soulBurn)
        {
            return new PrioritySelector(

                    new Decorator(
                        ret => !Me.HasAura("Soulburn"),
                        new PrioritySelector(
                            // target below 20% we have a higher prior on Haunt (but skip if soulburn already up...)
                           Spell.Buff("Haunt", 
                                true,
                                ctx => onUnit(ctx),
                                req => Me.CurrentSoulShards > 0
                                    && Me.CurrentTarget.HealthPercent < 20
                                    && !Me.HasAura("Soulburn"),
                                2),

                            // otherwise, save 2 shards for Soulburn and instant pet rez if needed (unless Misery buff up)
                            Spell.Buff("Haunt", true, ctx => onUnit(ctx), req => Me.CurrentSoulShards > 2 || Me.HasAura("Dark Soul: Misery"), 2)
                            )
                        ),

                    new Sequence(
                       Common.CreateCastSoulburn(
                            ret => soulBurn(ret)
                                && onUnit != null && onUnit(ret) != null
                                && onUnit(ret).CurrentHealth > 1
                                && SpellManager.HasSpell("Soul Swap")
                                && (onUnit(ret).HasAuraExpired("Agony", 3) || onUnit(ret).HasAuraExpired("Corruption", 3) || onUnit(ret).HasAuraExpired("Unstable Affliction", 3))
                                && onUnit(ret).InLineOfSpellSight
                                && Me.CurrentSoulShards > 0),

                        CreateCastSoulSwap(onUnit)
                        // , new Action( r => Logger.WriteDebug("CreateApplyDotsBehavior: Soulburn tree"))
                        ),

                    new Action( ret => {
                        _dotCount = 0;
                        if (onUnit != null && onUnit(ret) != null)
                        {
                            // if mob dying very soon, skip DoTs
                            if (onUnit(ret).TimeToDeath(99) < 4)
                                _dotCount = 4;
                            else
                            {
                                if (!onUnit(ret).HasAuraExpired("Agony", 3))
                                    ++_dotCount;
                                if (!onUnit(ret).HasAuraExpired("Corruption", 3))
                                    ++_dotCount;
                                if (!onUnit(ret).HasAuraExpired("Unstable Affliction", 3))
                                    ++_dotCount;
                                if (!onUnit(ret).HasAuraExpired("Haunt", 3))
                                    ++_dotCount;
                            }
                        }
                        // Logger.WriteDebug("CreateApplyDotsBehavior: DotCount={0}", _dotCount );
                        return RunStatus.Failure;
                        }),
                    new Decorator(
                        req => _dotCount < 4,
                        new PrioritySelector(
                            Spell.Buff("Agony", true, onUnit, ret => true, 3),
                            Spell.Buff("Corruption", true, onUnit, ret => true, 3),
                            Spell.Buff("Unstable Affliction", true, onUnit, req => true, 3)
                            )
                        )
                    );
        }

        public static Composite CreateApplyDotsBehaviorPvp(UnitSelectionDelegate onUnit, SimpleBooleanDelegate soulBurn)
        {
            return new PrioritySelector(

                    // target has all my DoTs but not Haunt -- make sure Soulburn isn't active
                   Spell.Buff("Haunt", 
                        true,
                        ctx => onUnit(ctx), 
                        req => Me.CurrentSoulShards > 0
                            && onUnit(req).HasAllMyAuras("Agony", "Corruption", "Unstable Affliction")
                            && !Me.HasAura("Soulburn"),
                        2),

                    // soulburn + soulswap sequence if requested
                    new Sequence(
                       Common.CreateCastSoulburn(
                            ret => soulBurn(ret)
                                && Me.CurrentSoulShards > 0
                                && onUnit != null && onUnit(ret) != null
                                && onUnit(ret).CurrentHealth > 1
                                && SpellManager.HasSpell("Soul Swap")
                                && onUnit(ret).HasAuraExpired("Unstable Affliction")
                                && !Me.HasAura("Soulburn") 
                                ),

                        CreateCastSoulSwap(onUnit)
                        ),

                    Spell.Buff("Corruption", true, ctx => onUnit(ctx), req => true, 3),
                    Spell.Buff("Agony", true, ctx => onUnit(ctx), req => true, 3),
                    Spell.Buff("Unstable Affliction", true, ctx => onUnit(ctx), req => true, 3)
                    );
        }

        #endregion

        public static Composite CreateCastSoulSwap(UnitSelectionDelegate onUnit)
        {
            return new Throttle(
                new Decorator(
                    ret => Me.HasAura("Soulburn")
                        && onUnit != null && onUnit(ret) != null
                        && onUnit(ret).IsAlive
                        && (onUnit(ret).HasAuraExpired("Agony") || onUnit(ret).HasAuraExpired("Corruption") || onUnit(ret).HasAuraExpired("Unstable Affliction"))
                        && SpellManager.HasSpell("Soul Swap")
                        && onUnit(ret).Distance <= 40
                        && onUnit(ret).InLineOfSpellSight,
                    new Action(ret =>
                    {
                        Logger.Write(string.Format("*Soul Swap on {0}", onUnit(ret).SafeName()));
                        SpellManager.Cast("Soul Swap", onUnit(ret));
                    })
                    )
                );
        }

        private WoWUnit GetBestAoeTarget()
        {
            WoWUnit unit = null;

            if (SpellManager.HasSpell("Seed of Corruption"))
                unit = Clusters.GetBestUnitForCluster(Common.TargetsInCombat.Where(m => !m.HasAura("Seed of Corruption")), ClusterType.Radius, 15f);

            if (SpellManager.HasSpell("Agony"))
                unit = Common.TargetsInCombat.FirstOrDefault(t => !t.HasMyAura("Agony"));

            return unit;
        }

        private static Composite CreateWarlockDiagnosticOutputBehavior(string sState = null)
        {
            if (!SingularSettings.Debug)
                return new Action(ret => { return RunStatus.Failure; });

            return new ThrottlePasses(1,
                new Action(ret =>
                {
                    string sMsg;
                    sMsg = string.Format(".... [{0}] h={1:F1}%, m={2:F1}%, moving={3}, pet={4:F0}% @ {5:F0} yds, soulburn={6}",
                        sState,
                        Me.HealthPercent,
                        Me.ManaPercent,
                        Me.IsMoving.ToYN(),
                        Me.GotAlivePet ? Me.Pet.HealthPercent : 0,
                        Me.GotAlivePet ? Me.Pet.Distance : 0,
                        (long)Me.GetAuraTimeLeft("Soulburn", true).TotalMilliseconds

                        );

                    WoWUnit target = Me.CurrentTarget;
                    if (target != null)
                    {
                        sMsg += string.Format(
                            ", {0}, {1:F1}%, dies={2} secs, {3:F1} yds, loss={4}, face={5}, agony={6}, corr={7}, ua={8}, haunt={9}, seed={10}, aoe={11}",
                            target.SafeName(),
                            target.HealthPercent,
                            target.TimeToDeath(),
                            target.Distance,
                            target.InLineOfSpellSight.ToYN(),
                            Me.IsSafelyFacing(target).ToYN(),
                            (long)target.GetAuraTimeLeft("Agony", true).TotalMilliseconds,
                            (long)target.GetAuraTimeLeft("Corruption", true).TotalMilliseconds,
                            (long)target.GetAuraTimeLeft("Unstable Affliction", true).TotalMilliseconds,
                            (long)target.GetAuraTimeLeft("Haunt", true).TotalMilliseconds,
                            (long)target.GetAuraTimeLeft("Seed of Corruption", true).TotalMilliseconds,
                            _mobCount
                            );
                    }

                    Logger.WriteDebug(Color.LightYellow, sMsg);
                    return RunStatus.Failure;
                })
                );
        }
    }
}