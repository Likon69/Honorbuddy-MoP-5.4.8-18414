using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.CommonBot;
using Styx.Helpers;


using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using System;
using System.Drawing;
using System.Collections.Generic;

namespace Singular.ClassSpecific.Mage
{
    public class Frost
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static MageSettings MageSettings { get { return SingularSettings.Instance.Mage(); } }

        [Behavior(BehaviorType.Rest, WoWClass.Mage, WoWSpec.MageFrost, WoWContext.All, 1)]
        public static Composite CreateMageFrostRest()
        {
            return new PrioritySelector(
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        new Decorator(
                            ret => !Me.HasAura("Drink") && !Me.HasAura("Food"),
                            new PrioritySelector(
                                CreateSummonWaterElemental(),
                                Common.CreateHealWaterElemental()
                                )
                            ),

                        Singular.Helpers.Rest.CreateDefaultRestBehaviour(),

                        new Decorator(ctx => SingularSettings.Instance.DisablePetUsage && Me.GotAlivePet,
                            new Sequence(
                                new Action(ctx => Logger.Write("/dismiss Pet")),
                                Spell.Cast("Dismiss Pet", on => Me.Pet, req => true, cancel => false),
                // new Action(ctx => SpellManager.Cast("Dismiss Pet")),
                                new WaitContinue(TimeSpan.FromMilliseconds(1500), ret => !Me.GotAlivePet, new ActionAlwaysSucceed())
                                )
                            )
                        )
                    )
                );

            return new Decorator(
                ret => !Spell.IsCasting() && !Spell.IsGlobalCooldown(),
                new PrioritySelector(

                    Common.CreateHealWaterElemental()

                    )
                );
        }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Mage, WoWSpec.MageFrost, WoWContext.All, 1)]
        public static Composite CreateMageFrostPreCombatbuffs()
        {
            return new Decorator(
                ret => !Spell.IsCasting() && !Spell.IsGlobalCooldown(),
                new PrioritySelector(

                    CreateSummonWaterElemental()

                    )
                );
        }

        #region Normal Rotation
        [Behavior(BehaviorType.Pull, WoWClass.Mage, WoWSpec.MageFrost, WoWContext.Normal)]
        public static Composite CreateMageFrostNormalPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Helpers.Common.EnsureReadyToAttackFromLongRange(),
                Spell.WaitForCastOrChannel(FaceDuring.Yes),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

						Common.CreateMagePullBuffs(),

            #region PULL WITH INSTANT IF NEEDED
                        new Decorator(
                            ret =>
                            {
                                WoWPlayer nearby = ObjectManager.GetObjectsOfType<WoWPlayer>(true, false).FirstOrDefault(p => !p.IsMe && p.DistanceSqr <= 40 * 40);
                                if (nearby != null)
                                {
                                    Logger.WriteDiagnostic("NormalPull: doing fast pull since player {0} nearby @ {1:F1} yds", nearby.SafeName(), nearby.Distance);
                                    return true;
                                }
                                return false;
                            },
                            new PrioritySelector(
                                Spell.Buff("Nether Tempest", true, on => Me.CurrentTarget, req => true, 1),
                                Spell.Buff("Living Bomb", true, on => Me.CurrentTarget, req => true, 0),
                                Spell.Cast("Ice Lance"),
                                Spell.Cast("Fire Blast")
                                )
                            ),
            #endregion

                        // Pull with Ice Lance if trivial or FoF built up
                        CreateIceLanceFoFBehavior(),

                        // Otherwise.... lets set a Bomb first
                        new Decorator(
                            req => !Me.CurrentTarget.IsTrivial(),
                            new PrioritySelector(
                                Spell.Buff("Nether Tempest", true, on => Me.CurrentTarget, req => true, 1),
                                Spell.Buff("Living Bomb", true, on => Me.CurrentTarget, req => true, 0),
                                Spell.Buff("Frost Bomb", true, on => Me.CurrentTarget, req => true, 0)
                                )
                            ),

                        Spell.Cast("Frostbolt", ret => !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                        Spell.Cast("Frostfire Bolt"),
                        Spell.Cast("Ice Lance", req => Me.IsMoving ),
                        Spell.Cast("Fire Blast")
                        )
                    ),

                Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 38f, 33f)
                );
        }

        public class ILInfo 
        {
            public WoWUnit Unit;
            public uint StacksOfFOF;
            public ILInfo( WoWUnit u, uint i)
            {
                Unit = u;
                StacksOfFOF = i;
            }

            public static ILInfo Ref(object o)
            {
                return (o as ILInfo);
            }
        }

        private static Sequence CreateIceLanceFoFBehavior(UnitSelectionDelegate on = null)
        {
            if (on == null)
                on = u => Me.CurrentTarget;

            return new Sequence(
                ctx => new ILInfo( on(ctx), Me.GetAuraStacks("Fingers of Frost")),
                new Decorator(
                    // req => Spell.CanCastHack("Ice Lance", (req as ILInfo).Unit) && ((req as ILInfo).Unit != null && ((req as ILInfo).StacksOfFOF > 0 || (req as ILInfo).Unit.IsTrivial())),
                    req => ILInfo.Ref(req).Unit != null && ILInfo.Ref(req).StacksOfFOF > 0 && Spell.CanCastHack("Ice Lance", ILInfo.Ref(req).Unit),
                    new Sequence(
                        new Action(r => { if (SingularSettings.Debug) Logger.WriteDebug("Ice Lance: casting since FoFStks={0} and MobTrivial={1}", ILInfo.Ref(r).StacksOfFOF, ILInfo.Ref(r).Unit.IsTrivial().ToYN()); }),
                        Spell.Cast("Ice Lance", on),    // ret => Unit.NearbyUnfriendlyUnits.Count(t => t.Distance <= 10) < 4),
                        Helpers.Common.CreateWaitForLagDuration(
                            until => ILInfo.Ref(until).StacksOfFOF == 0
                                || ILInfo.Ref(until).StacksOfFOF != Me.GetAuraStacks("Fingers of Frost")
                                || (ILInfo.Ref(until).Unit != null && ILInfo.Ref(until).Unit.IsTrivial())
                            )
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Mage, WoWSpec.MageFrost)]
        public static Composite CreateMageFrostHeal()
        {
            return new PrioritySelector(
                CreateFrostDiagnosticOutputBehavior("Combat")
                );
        }

        [Behavior(BehaviorType.PullBuffs, WoWClass.Mage, WoWSpec.MageFrost)]
        public static Composite CreateMageFrostPullBuffs()
        {
            return new PrioritySelector(
                CreateFrostDiagnosticOutputBehavior("Pull")
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Mage, WoWSpec.MageFrost, WoWContext.Normal)]
        public static Composite CreateMageFrostNormalCombat()
        {
            return new PrioritySelector(
                 Safers.EnsureTarget(),
                 Common.CreateStayAwayFromFrozenTargetsBehavior(),
                 Helpers.Common.EnsureReadyToAttackFromLongRange(),
                 Movement.CreateFaceTargetBehavior( 5f, true),
                 Spell.WaitForCastOrChannel(FaceDuring.Yes),

                 new Decorator(
                     ret => !Spell.IsGlobalCooldown(),
                     new PrioritySelector(

                        Common.CreateMageAvoidanceBehavior(),

                        CreateSummonWaterElemental(),

                        Helpers.Common.CreateAutoAttack(true),
                        Helpers.Common.CreateInterruptBehavior(),

                        // stack buffs for some burst... only every few minutes, but we'll use em if we got em
                        new Decorator(
                             req => Me.GotTarget && !Me.CurrentTarget.IsTrivial() && (Me.CurrentTarget.IsPlayer || Me.CurrentTarget.TimeToDeath(-1) > 40 || Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() >= 3),
                             new Decorator(
                                 req => (Me.HasAura("Invoker's Energy") || !Common.HasTalent(MageTalents.Invocation))
                                    && (Me.Level < 77 || Me.HasAura("Brain Freeze"))
                                    && (Me.Level < 24 || Me.HasAura("Fingers of Frost")) 
                                    && (Me.Level < 36 || Spell.CanCastHack("Icy Veins", Me, skipWowCheck: true)),
                                 new PrioritySelector(

                                     Spell.BuffSelf("Mirror Image"),

                                     Spell.OffGCD(
                                         new Sequence(
                                             new PrioritySelector(
                                                Spell.BuffSelf("Presence of Mind", req => Spell.GetSpellCooldown("Icy Veins").TotalMinutes > 1.5 || !Spell.IsSpellOnCooldown("Icy Veins")),
                                                new Decorator(req => !Common.HasTalent(MageTalents.PresenceOfMind), new ActionAlwaysSucceed())
                                                ),
                                             new PrioritySelector(
                                                Spell.BuffSelf("Icy Veins"),
                                                new Decorator(req => !SpellManager.HasSpell("Icy Veins"), new ActionAlwaysSucceed())
                                                ),
                                             new PrioritySelector(
                                                Spell.BuffSelf("Alter Time"),
                                                new Decorator(req => !SpellManager.HasSpell("Alter Time"), new ActionAlwaysSucceed())
                                                )
                                            )
                                        )
                                    )
                                )
                            ),

                        new Decorator(ret => Spell.UseAOE && Me.Level >= 25 && Unit.UnfriendlyUnitsNearTarget(10).Count() > 2 && !Unit.UnfriendlyUnitsNearTarget(10).Any(u => u.TreatAsFrozen()),
                            new PrioritySelector(
                                ctx => Clusters.GetBestUnitForCluster(Unit.UnfriendlyUnitsNearTarget(8), ClusterType.Radius, 8),
                                // Movement.CreateEnsureMovementStoppedBehavior(5f),
                                new Throttle(1,
                                    new Decorator(
                                        req => !Spell.IsSpellOnCooldown("Freeze"),
                                        new Sequence(
                                            CastFreeze(on => (WoWUnit) on),
                                            Helpers.Common.CreateWaitForLagDuration(),
                                            new WaitContinue(TimeSpan.FromMilliseconds(450), until => (until as WoWUnit).IsFrozen(), new ActionAlwaysSucceed())
                                            )
                                        )
                                    ),
                                Spell.CastOnGround("Flamestrike", on => (WoWUnit) on, req => true, waitForSpell: false),
                                Spell.Cast("Frozen Orb", req => Spell.UseAOE && Me.IsSafelyFacing(Me.CurrentTarget, 5f) && !Unit.NearbyUnfriendlyUnits.Any(u => u.IsSensitiveDamage() && Me.IsSafelyFacing(u, 20))),
                                Spell.Cast("Fire Blast", ret => TalentManager.HasGlyph("Fire Blast") && Me.CurrentTarget.HasAnyAura("Frost Bomb", "Living Bomb", "Nether Tempest")),

                                // Pull with Ice Lance if trivial or FoF built up
                                CreateIceLanceFoFBehavior(),

                                Spell.Cast("Arcane Explosion", ret => Unit.NearbyUnfriendlyUnits.Count(t => t.Distance <= 10) >= 4),
                                new Decorator(
                                    ret => Unit.UnfriendlyUnitsNearTarget(10).Count() >= 4,
                                    Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 10f, 7f)
                                    )
                                )
                            ),

                        // Movement.CreateEnsureMovementStoppedBehavior(35f),

                        Common.CreateMagePolymorphOnAddBehavior(),

                        // move these instnats really high in priority so we don't waste freezes, etc
                        CreateFrostfireBoltBrainFreezeBehavior(),

                        // Pull with Ice Lance if trivial or FoF built up
                        CreateIceLanceFoFBehavior(),

                        new PrioritySelector(
                            ctx => Unit.UnfriendlyUnits(12).FirstOrDefault(u => u.CurrentTargetGuid == Me.Guid && !u.IsCrowdControlled()),
                            new Decorator(
                                ret => ret != null,
                                new Sequence(
                                    new PrioritySelector(
                                        CastFreeze(on => (WoWUnit) on, req => !Unit.UnfriendlyUnitsNearTarget(12).Any(u => u.IsCrowdControlled())),
                                        Spell.BuffSelf("Frost Nova", req => !Unit.UnfriendlyUnits(12).Any(u => u.IsCrowdControlled())),
                                        Spell.Cast( 
                                            "Cone of Cold", 
                                            on => Unit.UnfriendlyUnits(12)
                                                .Where( u => Me.IsSafelyFacing(u,60f))
                                                .OrderBy( u => (long) u.Distance2DSqr )
                                                .FirstOrDefault()
                                            )
                                        ),
                                    Helpers.Common.CreateWaitForLagDuration()
                                    // , new WaitContinue(TimeSpan.FromMilliseconds(350), until => (until as WoWUnit).IsFrozen() || (, new ActionAlwaysSucceed())
                                        /*
                                        ,
                                    new Action(r => Logger.WriteDebug("MageAvoidance: move after freezing targets! requesting KITING!!!")),
                                    Common.CreateMageAvoidanceBehavior(null, null, dis => (dis as WoWUnit).IsFrozen(), kite => (kite as WoWUnit).IsFrozen())
                                         */
                                    )
                                )
                            ),

                        // nether tempest in CombatBuffs
                        Spell.Cast("Frozen Orb", req => Spell.UseAOE && !Unit.NearbyUnfriendlyUnits.Any(u => u.IsSensitiveDamage() && Me.IsSafelyFacing(u, 150))),

                        // on mobs that will live a long time, build up the debuff... otherwise react to procs more quickly
                        // this is the main element that departs from normal instance rotation
                        Spell.Cast("Frostbolt", 
                            ret => Me.CurrentTarget.TimeToDeath(20) >= 20
                                && (!Me.CurrentTarget.HasAura("Frostbolt", 3) || Me.CurrentTarget.HasAuraExpired("Frostbolt", 3)) 
                                && !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),

                        new Throttle( 1,
                            new Decorator( 
                                ret => !Me.HasAura("Fingers of Frost", 2),
                                CastFreeze( on => Clusters.GetBestUnitForCluster(Unit.UnfriendlyUnitsNearTarget(8), ClusterType.Radius, 8))
                                )
                            ),

                        CreateFrostfireBoltBrainFreezeBehavior(),

                        Spell.Cast("Ice Lance", ret => {
                            if (!Me.IsMoving)
                                return false;
                            if (Spell.HaveAllowMovingWhileCastingAura())
                                return false;
                            if (Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost))
                                return false;
                            if (!Spell.CanCastHack("Ice Lance", Me.CurrentTarget))
                                return false;

                            Logger.WriteDebug("Ice Lance: casting for instant attack while moving");
                            return true;
                            }),

                        Spell.Cast("Frostbolt", ret => !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),

                        new Decorator(
                            ret => !Me.CurrentTarget.IsImmune(WoWSpellSchool.Fire) && (Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) || !SpellManager.HasSpell("Frostbolt")),
                            new PrioritySelector(
                                Spell.Cast("Fire Blast"),
                                Spell.Cast("Frostfire Bolt")
                                )
                            )
                        )
                    ),

                Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 38f, 33f)
                );
        }

        #endregion

        #region Battleground Rotation

        private static HashSet<int> alterTimeBuffs = new HashSet<int>
            {
                12043,  // Presence of Mind
                114003, // Invocation
                44549,  // Brain Freeze
                12472   // Icy Veins
            };

        [Behavior(BehaviorType.Pull|BehaviorType.Combat, WoWClass.Mage, WoWSpec.MageFrost, WoWContext.Battlegrounds)]
        public static Composite CreateMageFrostPvPCombat()
        {
            return new PrioritySelector(
                 Safers.EnsureTarget(),
                 Common.CreateStayAwayFromFrozenTargetsBehavior(),
                 Helpers.Common.EnsureReadyToAttackFromLongRange(),
                 Spell.WaitForCastOrChannel(FaceDuring.Yes),

                 new Decorator(
                     ret => !Spell.IsGlobalCooldown(),
                     new PrioritySelector(

                        Common.CreateMageAvoidanceBehavior(),

                        CreateSummonWaterElemental(),
                        Common.CreateMagePullBuffs(),

                        Helpers.Common.CreateAutoAttack(true),
                        Helpers.Common.CreateInterruptBehavior(),

                         // Snipe Kills
                        new PrioritySelector(
                            ctx => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.HealthPercent.Between(1, 10) && u.SpellDistance() < 40 && Me.IsSafelyFacing(u, 150)),
                            CreateFrostfireBoltBrainFreezeBehavior(on => (WoWUnit)on),
                            CreateIceLanceFoFBehavior(on => (WoWUnit)on)
                            ),

                         // Defensive stuff
                // Spell.BuffSelf("Blink", ret => MovementManager.IsClassMovementAllowed && (Me.IsStunned() || Me.IsRooted())),
                         // Spell.BuffSelf("Mana Shield", ret => Me.HealthPercent <= 75),

                        new Decorator(
                            ret => Unit.NearbyUnfriendlyUnits.Any(u => u.Distance <= 15 && !u.IsCrowdControlled()),
                            new PrioritySelector(
                                CastFreeze(on => Me.CurrentTarget),
                                Spell.BuffSelf(
                                "Frost Nova",
                                    ret => Unit.NearbyUnfriendlyUnits.Any(u => u.Distance <= 11 && !u.TreatAsFrozen())
                                    )
                                )
                            ),

                        Spell.CastOnGround("Ring of Frost", onUnit => Me.CurrentTarget, req => Me.CurrentTarget.TreatAsFrozen() && Me.CurrentTarget.SpellDistance() < 30, true),

                        Common.CreateUseManaGemBehavior(ret => Me.ManaPercent < 80),

                        Common.CreateMagePolymorphOnAddBehavior(),

                        // Stack some for burst if possible
                         new Decorator(
                             req => (Me.HasAura("Invoker's Energy") || !Common.HasTalent(MageTalents.Invocation)),
                             new PrioritySelector(

                                 Spell.BuffSelf( "Mirror Image"),

                                 Spell.OffGCD(
                                     new Sequence(
                                         new PrioritySelector(
                                            Spell.BuffSelf("Presence of Mind", req => Spell.GetSpellCooldown("Icy Veins").TotalMinutes > 1.5 || !Spell.IsSpellOnCooldown("Icy Veins")),
                                            new Decorator( req => !Common.HasTalent(MageTalents.PresenceOfMind), new ActionAlwaysSucceed())
                                            ),
                                         new PrioritySelector(
                                            Spell.BuffSelf("Icy Veins"),
                                            new Decorator( req => !SpellManager.HasSpell("Icy Veins"), new ActionAlwaysSucceed())
                                            ),
                                         new PrioritySelector(
                                            Spell.OffGCD( Spell.BuffSelf("Alter Time")),
                                            new Decorator( req => !SpellManager.HasSpell("Alter Time"), new ActionAlwaysSucceed())
                                            )
                                        )
                                    )
                                )
                            ),

                        // 3 min
                         Spell.BuffSelf("Mage Ward", ret => Me.HealthPercent <= 75),

                         Spell.BuffSelf("Alter Time", req =>
                         {
                             int count = Me.GetAllAuras().Count(a => a.TimeLeft.TotalSeconds > 0 && alterTimeBuffs.Contains(a.SpellId));
                             return count >= 2;
                         }),

                         // Rotation
                         new Decorator(
                             req => Me.CurrentTarget.IsRooted() && !Spell.IsSpellOnCooldown("Frozen Orb"),
                             new PrioritySelector(
                                 ctx => Me.IsSafelyFacing(Me.CurrentTarget, 7.5f),
                                 new Decorator( req => !(bool) req, Movement.CreateFaceTargetBehavior( 7.5f)),
                                 Spell.Cast("Frozen Orb", req => (bool) req)
                                 )
                             ),

                         Spell.Cast("Frost Bomb", ret => Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3),
                         Spell.Cast("Deep Freeze", ret => Me.ActiveAuras.ContainsKey("Fingers of Frost") || Me.CurrentTarget.TreatAsFrozen()),

                        CreateFrostfireBoltBrainFreezeBehavior(),

                         Spell.Cast("Ice Lance",
                             ret => Me.ActiveAuras.ContainsKey("Fingers of Frost") || Me.CurrentTarget.TreatAsFrozen() || (Me.IsMoving && !Spell.HaveAllowMovingWhileCastingAura())),

                         Spell.Cast("Frostbolt")
                         )
                    ),

                 Movement.CreateMoveToUnitBehavior(on => StyxWoW.Me.CurrentTarget, 38f, 33f)
                 );
        }

        private static Composite CreateFrostfireBoltBrainFreezeBehavior( UnitSelectionDelegate on = null)
        {
            if (on == null)
                on = u => Me.CurrentTarget;

            return new Sequence(
                Spell.Cast("Frostfire Bolt", on, ret => Me.HasAura("Brain Freeze"), cancel => false),
                new Wait(1, until => !Me.IsCasting, new ActionAlwaysSucceed()),
                Helpers.Common.CreateWaitForLagDuration(until => !Me.HasAura("Brain Freeze"))
                );
        }

        #endregion

        #region Instance Rotation
        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Mage, WoWSpec.MageFrost, WoWContext.Instances)]
        public static Composite CreateMageFrostInstanceCombat()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),
                Movement.CreateFaceTargetBehavior( 5f),
                Spell.WaitForCastOrChannel(FaceDuring.Yes),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Helpers.Common.CreateAutoAttack(true),
                        Helpers.Common.CreateInterruptBehavior(),

                        Common.CreateMagePullBuffs(),
                        Spell.Cast("Icy Veins", req => Me.GotTarget && Me.CurrentTarget.SpellDistance() < 40),

                        new Decorator(ret => Spell.UseAOE && Me.Level >= 25 && Unit.UnfriendlyUnitsNearTarget(10).Count() > 1,
                            new PrioritySelector(
                                new Throttle(1,
                                    new Decorator(
                                        ret => !Me.HasAura("Fingers of Frost", 2),
                                        CastFreeze(on => Clusters.GetBestUnitForCluster(Unit.UnfriendlyUnitsNearTarget(8), ClusterType.Radius, 8))
                                        )
                                    ),
                                // Movement.CreateEnsureMovementStoppedBehavior(5f),
                                Spell.CastOnGround("Flamestrike", loc => Me.CurrentTarget.Location),
                                Spell.Cast("Frozen Orb", req => !Unit.NearbyUnfriendlyUnits.Any(u => u.IsSensitiveDamage() && Me.IsSafelyFacing(u, 150))),
                                Spell.Cast("Fire Blast", ret => TalentManager.HasGlyph("Fire Blast") && Me.CurrentTarget.HasAnyAura("Frost Bomb", "Living Bomb", "Nether Tempest")),
                                Spell.Cast("Ice Lance", ret => Unit.NearbyUnfriendlyUnits.Count(t => t.Distance <= 10) < 4),
                                Spell.Cast("Arcane Explosion", ret => Unit.NearbyUnfriendlyUnits.Count(t => t.Distance <= 10) >= 4),
                                new Decorator(
                                    ret => Unit.UnfriendlyUnitsNearTarget(10).Count() >= 4,
                                    Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 10f, 5f)
                                    )
                                )
                            ),

                        Movement.CreateEnsureMovementStoppedBehavior(25f),

                        Spell.Cast("Frozen Orb", req => Spell.UseAOE && Me.GetAuraStacks("Fingers of Frost") < 2 && Me.IsSafelyFacing(Me.CurrentTarget, 15f) && !Unit.NearbyUnfriendlyUnits.Any(u => u.IsSensitiveDamage() && Me.IsSafelyFacing(u, 20))),
                        new Sequence(
                            Spell.Cast("Frostfire Bolt", ret => Me.HasAura("Brain Freeze")),
                            Helpers.Common.CreateWaitForLagDuration(until => !Me.HasAura("Brain Freeze"))
                            ),
                        Spell.Cast("Ice Lance", ret => (Me.IsMoving && !Spell.HaveAllowMovingWhileCastingAura()) || Me.GetAuraStacks("Fingers of Frost") > 0),
                        Spell.Cast("Frostbolt"),

                        Spell.Cast("Frostfire Bolt", ret => !SpellManager.HasSpell("Frostbolt"))
                        )
                    ),

                Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 30f, 25f)
                );
        }

        #endregion

        public static Composite CreateSummonWaterElementalOld()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => SingularSettings.Instance.DisablePetUsage && Me.GotAlivePet,
                    new Action( ret => Lua.DoString("PetDismiss()"))
                    ),

                new Decorator(
                    ret => !SingularSettings.Instance.DisablePetUsage
                        && (!Me.GotAlivePet || Me.Pet.Distance > 40)
                        && PetManager.PetSummonAfterDismountTimer.IsFinished
                        && Spell.CanCastHack("Summon Water Elemental"),
                    new Sequence(
                        new Action(ret => PetManager.CallPet("Summon Water Elemental")),
                        Helpers.Common.CreateWaitForLagDuration()
                        )
                    )
                );
        }

        public static Composite CreateSummonWaterElemental()
        {
            return new Decorator(
                ret => SingularRoutine.IsAllowed(Styx.CommonBot.Routines.CapabilityFlags.PetSummoning)
                    && !SingularSettings.Instance.DisablePetUsage
                    && (!Me.GotAlivePet || Me.Pet.Distance > 40)
                    && PetManager.PetSummonAfterDismountTimer.IsFinished
                    && Spell.CanCastHack("Summon Water Elemental"),

                new Sequence(
                    // wait for possible auto-spawn if supposed to have a pet and none present
                    new DecoratorContinue(
                        ret => !Me.GotAlivePet && !SingularSettings.Instance.DisablePetUsage,
                        new Sequence(
                            new Action(ret => Logger.WriteDebug("Summon Water Elemental:  waiting briefly for live pet to appear")),
                            new WaitContinue(
                                TimeSpan.FromMilliseconds(1000),
                                ret => Me.GotAlivePet,
                                new Sequence(
                                    new Action(ret => Logger.WriteDebug("Summon Water Elemental:  live pet detected")),
                                    new Action(r => { return RunStatus.Failure; })
                                    )
                                )
                            )
                        ),

                    // dismiss pet if not supposed to have one
                    new DecoratorContinue(
                        ret => Me.GotAlivePet && SingularSettings.Instance.DisablePetUsage,
                        new Sequence(
                            new Action(ret => Logger.WriteDebug("Summon Water Elemental:  dismissing pet")),
                            new Action(ctx => Lua.DoString("PetDismiss()")),
                            new WaitContinue(
                                TimeSpan.FromMilliseconds(1000),
                                ret => !Me.GotAlivePet,
                                new Action(ret => {
                                    Logger.WriteDebug("Summon Water Elemental:  dismiss complete");
                                    return RunStatus.Success;
                                    })
                                )
                            )
                        ),

                    // summon pet if we still need to
                    new DecoratorContinue(
                        ret => !Me.GotAlivePet && !SingularSettings.Instance.DisablePetUsage,
                        new Sequence(
                            new Action(ret => Logger.WriteDebug("Summon Water Elemental:  about to summon pet")),

                            // Heal() used intentionally here (has spell completion logic not present in Cast())
                            Spell.Cast(n => "Summon Water Elemental",
                                chkMov => true,
                                onUnit => Me,
                                req => true,
                                cncl => false),

                            // make sure we see pet alive before continuing
                            new Wait(1, ret => Me.GotAlivePet, new ActionAlwaysSucceed()),
                            new Action(ret => Logger.WriteDebug("Summon Water Elemental:  now have alive pet"))
                            )
                        )
                    )
                );
        }


        
        /// <summary>
        /// Cast "Freeze" pet ability on a target.  Uses a local store for location to
        /// avoid target position changing during cast preparation and being out of
        /// range after range check
        /// </summary>
        /// <param name="onUnit">target to cast on</param>
        /// <returns></returns>
        public static Composite CastFreeze( UnitSelectionDelegate onUnit, SimpleBooleanDelegate require = null)
        {
            if (onUnit == null)
                return new ActionAlwaysFail();

            if (require == null)
                require = req => true;

            return new Sequence(
                new Decorator( 
                    ret => onUnit(ret) != null && require(ret), 
                    new Action( ret => _locFreeze = onUnit(ret).Location)
                    ),
                new Throttle( TimeSpan.FromMilliseconds(250),
                    Pet.CastPetActionOnLocation(
                        "Freeze",
                        on => _locFreeze,
                        ret => Me.Pet.ManaPercent >= 12
                            && Me.Pet.Location.Distance(_locFreeze) < 45
                            && !Me.CurrentTarget.TreatAsFrozen()
                        )
                    )
                );
        }

        static private WoWPoint _locFreeze;

        #region Diagnostics

        private static Composite CreateFrostDiagnosticOutputBehavior(string state = null)
        {
            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            return new ThrottlePasses(1, 1,
                new Action(ret =>
                {
                    string log;

                    log = string.Format(".... [{0}] h={1:F1}%/m={2:F1}%, mov={3}, pet={4:F1}%, fof={5}, brnfrz={6}",
                        state ?? Dynamics.CompositeBuilder.CurrentBehaviorType.ToString(),
                        Me.HealthPercent,
                        Me.ManaPercent,
                        Me.IsMoving.ToYN(),
                        Me.GotAlivePet ? Me.Pet.HealthPercent : 0,
                        Me.GetAuraStacks("Fingers of Frost"),
                        (long)Me.GetAuraTimeLeft("Brain Freeze", true).TotalMilliseconds
                        );

                    WoWUnit target = Me.CurrentTarget;
                    if (target != null)
                    {
                        log += string.Format(", ttd={0}, th={1:F1}%, dist={2:F1}, tmov={3}, melee={4}, face={5}, loss={6}, fboltstks={7}",
                            target.TimeToDeath(),
                            target.HealthPercent,
                            target.Distance,
                            target.IsMoving.ToYN(),
                            target.IsWithinMeleeRange.ToYN(),
                            Me.IsSafelyFacing(target).ToYN(),
                            target.InLineOfSpellSight.ToYN(),
                            target.GetAuraStacks("Frostbolt", true)
                            );

                        if (Common.HasTalent(MageTalents.NetherTempest))
                            log += string.Format(", nethtmp={0}", (long)target.GetAuraTimeLeft("Nether Tempest", true).TotalMilliseconds);
                        else if (Common.HasTalent(MageTalents.LivingBomb ))
                            log += string.Format( ", livbomb={0}", (long)target.GetAuraTimeLeft("Living Bomb", true).TotalMilliseconds);
                        else if (Common.HasTalent(MageTalents.FrostBomb))
                            log += string.Format( ", frstbmb={0}", (long)target.GetAuraTimeLeft("Frost Bomb", true).TotalMilliseconds);

                        if (target.HasAura("Freeze"))
                            log += string.Format(", freeze={0}", (long)target.GetAuraTimeLeft("Freeze", true).TotalMilliseconds);
                        else if (target.HasAura("Frost Nova"))
                            log += string.Format(", frostnova={0}", (long)target.GetAuraTimeLeft("Frost Nova", true).TotalMilliseconds);
                        else if (target.HasAura("Ring of Frost"))
                            log += string.Format(", ringfrost={0}", (long)target.GetAuraTimeLeft("Ring of Frost", true).TotalMilliseconds);
                        else if (target.HasAura("Frostjaw"))
                            log += string.Format(", frostjaw={0}", (long)target.GetAuraTimeLeft("Frostjaw", true).TotalMilliseconds);
                        else if (target.HasAura("Ice Ward"))
                            log += string.Format(", iceward={0}", (long)target.GetAuraTimeLeft("Ice Ward", true).TotalMilliseconds);

                        if (target.IsImmune(WoWSpellSchool.Frost))
                            log += ", immune=Y";

                        log += string.Format(", isfrozen={0}", target.TreatAsFrozen().ToYN());
                    }

                    Logger.WriteDebug(Color.AntiqueWhite, log);
                    return RunStatus.Failure;
                })
                );
        }

        #endregion
    }
}
