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
    // wowraids.org 
    public class Destruction
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WarlockSettings WarlockSettings { get { return SingularSettings.Instance.Warlock(); } }

        private static int _mobCount;
        private static bool _InstantRoF;

        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Warlock, WoWSpec.WarlockDestruction, WoWContext.All)]
        public static Composite CreateWarlockDestructionNormalPull()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCast(FaceDuring.Yes),

                new Decorator(ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        CreateWarlockDiagnosticOutputBehavior( "Pull" ),
                        Helpers.Common.CreateAutoAttack(true),
                        Spell.Buff("Immolate", true, on => Me.CurrentTarget, ret => true, 3),
                        Spell.Cast("Incinerate")
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Warlock, WoWSpec.WarlockDestruction, WoWContext.All, priority: 999)]
        public static Composite CreateAfflictionHeal()
        {
            return new PrioritySelector(
                CreateWarlockDiagnosticOutputBehavior("Combat")
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Warlock, WoWSpec.WarlockDestruction, WoWContext.Normal)]
        public static Composite CreateWarlockDestructionNormalCombat()
        {
            _InstantRoF = Me.HasAura("Aftermath");

            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCast(FaceDuring.Yes),

                new Decorator(ret => !Spell.IsGlobalCooldown(),

                    new PrioritySelector(

                        Helpers.Common.CreateAutoAttack(true),

                        Helpers.Common.CreateInterruptBehavior(),

                        new Action(ret =>
                        {
                            _mobCount = TargetsInCombat.Count();
                            return RunStatus.Failure;
                        }),

                        CreateAoeBehavior(),

                        // Noxxic
                        Spell.Cast("Shadowburn", ret => Me.CurrentTarget.HealthPercent < 20),
                        Spell.Buff("Immolate", true, on => Me.CurrentTarget, ret => true, 3),
                        Spell.Cast("Conflagrate"),
                        Spell.CastOnGround("Rain of Fire", on => Me.CurrentTarget, req => Spell.UseAOE && _InstantRoF && !Me.CurrentTarget.IsMoving && !Me.CurrentTarget.HasMyAura("Rain of Fire") && !Unit.UnfriendlyUnitsNearTarget(8).Any(u => !u.Aggro || u.IsCrowdControlled()), false),

                        Spell.Cast("Chaos Bolt", ret => Me.CurrentTarget.HealthPercent >= 20 && BackdraftStacks < 3),
                        Spell.Cast("Incinerate"),

                        Spell.Cast("Fel Flame", ret => Me.IsMoving && Me.CurrentTarget.GetAuraTimeLeft("Immolate").TotalMilliseconds.Between(300, 3000)),

                        Spell.Cast("Drain Life", ret => Me.HealthPercent <= WarlockSettings.DrainLifePercentage && !Group.AnyHealerNearby),
                        Spell.Cast("Shadow Bolt")
                        )
                    )
                );

        }


        [Behavior(BehaviorType.Combat, WoWClass.Warlock, WoWSpec.WarlockDestruction, WoWContext.Battlegrounds )]
        [Behavior(BehaviorType.Combat, WoWClass.Warlock, WoWSpec.WarlockDestruction, WoWContext.Instances)]
        public static Composite CreateWarlockDestructionInstanceCombat()
        {
            _InstantRoF = Me.HasAura("Aftermath");

            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCast(FaceDuring.Yes),

                new Decorator(ret => !Spell.IsGlobalCooldown(),

                    new PrioritySelector(

                        Helpers.Common.CreateAutoAttack(true),

                        Helpers.Common.CreateInterruptBehavior(),

                        new Action(ret =>
                        {
                            _mobCount = TargetsInCombat.Count();
                            return RunStatus.Failure;
                        }),

                        CreateAoeBehavior(),

                        new Decorator(
                            req =>
                            {
                                if (Me.HasAnyAura("Dark Soul: Instability", "Skull Banner", "Toxic Power", "Expanded Mind"))
                                    return true;
                                return false;
                            },
                            new PrioritySelector(
                                Spell.Cast("Shadowburn", req => Me.CurrentTarget.HealthPercent < 20),
                                Spell.Cast("Chaos Bolt", req => Me.CurrentTarget.HealthPercent < 20)
                                )
                            ),

                // Noxxic
                        new Decorator(
                            req => WarlockSettings.DestructionSpellPriority == Singular.Settings.WarlockSettings.SpellPriority.Noxxic,
                            new PrioritySelector(
                                Spell.Cast("Shadowburn", ret => Me.CurrentTarget.HealthPercent < 20),
                                Spell.Buff("Immolate", true, on => Me.CurrentTarget, ret => true, 3),
                                Spell.Cast("Conflagrate"),
                                Spell.CastOnGround("Rain of Fire", on => Me.CurrentTarget, req => Spell.UseAOE && _InstantRoF && !Me.CurrentTarget.IsMoving && !Me.CurrentTarget.HasMyAura("Rain of Fire") && !Unit.UnfriendlyUnitsNearTarget(8).Any(u => !u.Aggro || u.IsCrowdControlled()), false),

                                Spell.Cast("Chaos Bolt", ret => Me.CurrentTarget.HealthPercent >= 20 && BackdraftStacks < 3),
                                Spell.Cast("Incinerate"),

                                Spell.Cast("Fel Flame", ret => Me.IsMoving && Me.CurrentTarget.GetAuraTimeLeft("Immolate").TotalMilliseconds.Between(300, 3000))
                                )
                            ),

                        // Icy Veins
                        new Decorator(
                            req => WarlockSettings.DestructionSpellPriority == Singular.Settings.WarlockSettings.SpellPriority.IcyVeins,
                            new PrioritySelector(
                                Spell.Cast("Shadowburn", ret =>
                                {
                                    if (Me.CurrentTarget.HealthPercent < 20)
                                    {
                                        if (CurrentBurningEmbers >= 35)
                                            return true;
                                        if (Me.HasAnyAura("Dark Soul: Instability", "Skull Banner", "Toxic Power", "Expanded Mind"))
                                            return true;
                                        if (Me.CurrentTarget.TimeToDeath(99) < 3)
                                            return true;
                                        if (Me.ManaPercent < 5)
                                            return true;
                                    }
                                    return false;
                                }),

                                Spell.Buff("Immolate", true, on => Me.CurrentTarget, ret => true, 3),
                                Spell.Cast("Conflagrate", req => Spell.GetCharges("Conflagrate") >= 2),
                                Spell.CastOnGround("Rain of Fire", on => Me.CurrentTarget, req => Spell.UseAOE && _InstantRoF && !Me.CurrentTarget.IsMoving && !Me.CurrentTarget.HasMyAura("Rain of Fire") && !Unit.UnfriendlyUnitsNearTarget(8).Any(u => !u.Aggro || u.IsCrowdControlled()), false),

                                Spell.Cast("Chaos Bolt", ret =>
                                {
                                    if (BackdraftStacks < 3)
                                    {
                                        if (CurrentBurningEmbers >= 35)
                                            return true;
                                        if (Me.HasAnyAura("Dark Soul: Instability", "Skull Banner"))
                                            return true;
                                    }
                                    return false;
                                }),

                                Spell.Cast("Conflagrate", req => Spell.GetCharges("Conflagrate") == 1),

                                Spell.Cast("Incinerate"),

                                Spell.Cast("Fel Flame", ret => Me.IsMoving && Me.CurrentTarget.GetAuraTimeLeft("Immolate").TotalMilliseconds.Between(300, 3000))
                                )
                            ),

                        Spell.Cast("Drain Life", ret => Me.HealthPercent <= WarlockSettings.DrainLifePercentage && !Group.AnyHealerNearby),
                        Spell.Cast("Shadow Bolt")
                        )
                    )
                );

        }

        public static Composite CreateAoeBehavior()
        {
            return new Decorator( 
                ret => Spell.UseAOE && _mobCount > 1,
                new PrioritySelector(

                    new Decorator(
                        ret => _mobCount < 4,
                        Spell.Buff("Immolate", true, on => Unit.NearbyUnitsInCombatWithMeOrMyStuff.FirstOrDefault(u => u.HasAuraExpired("Immolate", 3) && Spell.CanCastHack("Immolate", u) && Me.IsSafelyFacing(u, 150)), req => true)
                        ),

                    new PrioritySelector(
                        ctx => Unit.NearbyUnitsInCombatWithMeOrMyStuff.FirstOrDefault(u => u.Guid != Me.CurrentTargetGuid && !u.HasMyAura("Havoc ")),
                        Spell.Buff("Havoc", on => ((WoWUnit)on) ?? Unit.NearbyUnitsInCombatWithMeOrMyStuff.Where(u => u.Guid != Me.CurrentTargetGuid).OrderByDescending( u => u.CurrentHealth).FirstOrDefault())
                        ),

                    new Decorator(
                        ret => _mobCount >= 4,
                        new PrioritySelector(
                            new PrioritySelector(
                                ctx => Clusters.GetBestUnitForCluster( Unit.NearbyUnfriendlyUnits.Where(u => Me.IsSafelyFacing(u)), ClusterType.Radius, 8f),
                                Spell.CastOnGround( "Rain of Fire", 
                                    on => (WoWUnit)on, 
                                    req => req != null 
                                        && _InstantRoF
                                        && !Me.HasAura( "Rain of Fire")
                                        && 3 <= Unit.UnfriendlyUnitsNearTarget((WoWUnit)req, 8).Count()
                                        && !Unit.UnfriendlyUnitsNearTarget((WoWUnit)req, 8).Any(u => !u.Aggro || u.IsCrowdControlled())
                                    )
                                ),

                                Spell.OffGCD(Spell.Buff("Fire and Brimstone", on => Me.CurrentTarget, req => Unit.NearbyUnfriendlyUnits.Count(u => Me.CurrentTarget.Location.Distance(u.Location) <= 10f) >= 4))
                            )
                        )
                    )
                );
        }

        #endregion

        public static double CurrentBurningEmbers
        {
            get
            {
                return Me.GetPowerInfo(WoWPowerType.BurningEmbers).Current;
            }
        }

        static double ImmolateTime(WoWUnit u = null)
        {
            return (u ?? Me.CurrentTarget).GetAuraTimeLeft("Immolate", true).TotalSeconds;
        }

        static IEnumerable<WoWUnit> TargetsInCombat
        {
            get
            {
                return Unit.NearbyUnfriendlyUnits.Where(u => u.Combat && u.IsTargetingMyStuff() && !u.IsCrowdControlled() && StyxWoW.Me.IsSafelyFacing(u));
            }
        }

        static int BackdraftStacks
        {
            get
            {
                return (int) Me.GetAuraStacks("Backdraft");
            }
        }

        private static Composite CreateWarlockDiagnosticOutputBehavior(string s)
        {
            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            return new ThrottlePasses(1, 1,
                new Action(ret =>
                {
                    string msg;
                    msg = string.Format(".... [{0}] h={1:F1}%/m={2:F1}%, embers={3}, backdraft={4}, conflag={5}, aoe={6}",
                        s,
                        Me.HealthPercent,
                        Me.ManaPercent,
                        CurrentBurningEmbers,
                        BackdraftStacks,
                        Spell.GetCharges("Conflagrate"),
                        _mobCount
                        );

                    WoWUnit target = Me.CurrentTarget;
                    if (target != null)
                    {
                        msg += string.Format(
                            ", {0}, {1:F1}%, dies={2} secs, {3:F1} yds, loss={4}, face={5}, immolate={6}, rainfire={7}",
                            target.SafeName(),
                            target.HealthPercent,
                            target.TimeToDeath(),
                            target.Distance,
                            target.InLineOfSpellSight.ToYN(),
                            Me.IsSafelyFacing(target).ToYN(),
                            (long)target.GetAuraTimeLeft("Immolate", true).TotalMilliseconds,
                            target.HasMyAura("Rain of Fire").ToYN()
                            );
                    }

                    Logger.WriteDebug(Color.LightYellow, msg);
                    return RunStatus.Failure;
                })
            );
        }
    }
}