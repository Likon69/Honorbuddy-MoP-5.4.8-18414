using System.Linq;
using System.Runtime.Remoting.Contexts;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;

using Styx.CommonBot;
using Styx.TreeSharp;

using Styx.Helpers;
using System;
using Styx.WoWInternals;
using Action = Styx.TreeSharp.Action;
using Styx.WoWInternals.WoWObjects;

using Styx.Common;
using System.Drawing;
using CommonBehaviors.Actions;

namespace Singular.ClassSpecific.Warrior
{
    public class Protection
    {

        #region Common

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WarriorSettings WarriorSettings { get { return SingularSettings.Instance.Warrior(); } }
        [Behavior(BehaviorType.Pull, WoWClass.Warrior, WoWSpec.WarriorProtection, WoWContext.All)]
        public static Composite CreateProtectionNormalPull()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Helpers.Common.CreateAutoAttack(false),

                Helpers.Common.CreateDismount("Pulling"),

                Spell.WaitForCastOrChannel(),

                new Decorator(
                    req => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Common.CreateAttackFlyingOrUnreachableMobs(),

                        Common.CreateChargeBehavior(),

                        //Buff up (or use to generate Rage)
                        new Throttle( TimeSpan.FromSeconds(1), 
                            new PrioritySelector(

                                Spell.Cast("Battle Shout", 
                                    ret => !Me.HasMyAura("Commanding Shout") 
                                        && (!Me.HasPartyBuff(PartyBuffType.AttackPower) || Me.CurrentRage < 20)),

                                Spell.Cast("Commanding Shout", 
                                    ret => !Me.HasMyAura("Battle Shout") 
                                        && (!Me.HasPartyBuff(PartyBuffType.Stamina) || Me.CurrentRage < 20))
                                )
                            ),

                        Spell.Cast( "Shield Slam", req => HasShieldInOffHand ),

                        // just in case user botting a Prot Warrior without a shield
                        Spell.Cast("Revenge"),
                        Spell.Cast("Devastate", ret => !Me.CurrentTarget.HasAura("Weakened Armor", 3)),
                        Spell.Cast("Thunder Clap", ret => Spell.UseAOE && Me.CurrentTarget.SpellDistance() < 8f && !Me.CurrentTarget.ActiveAuras.ContainsKey("Weakened Blows")),

                        // filler to try and do something more than auto attack at this point
                        Spell.Cast("Devastate"),
                        Spell.Cast("Heroic Strike"),

                        CheckThatShieldIsEquippedIfNeeded()
                        )
                    )
                );
        }
        #endregion

        #region Normal

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Warrior, WoWSpec.WarriorProtection, WoWContext.All)]
        public static Composite CreateProtectionNormalPreCombatBuffs()
        {
            return new PrioritySelector(

                // no shield means no shield slam, so use Battle Stance for more Rage generation for 
                // ... those Prot warriors the owner didnt see fit to give a shield
                Spell.BuffSelf( stance => HasShieldInOffHand ? "Defensive Stance" : "Battle Stance", req => true),

                PartyBuff.BuffGroup( "Battle Shout", ret => WarriorSettings.Shout == WarriorShout.BattleShout ),

                PartyBuff.BuffGroup( "Commanding Shout", ret => WarriorSettings.Shout == WarriorShout.CommandingShout )
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Warrior, WoWSpec.WarriorProtection, WoWContext.All)]
        public static Composite CreateProtectionCombatBuffs()
        {
            return new Decorator(
                req => !Unit.IsTrivial(Me.CurrentTarget),
                new Throttle(    // throttle these because most are off the GCD
                    new PrioritySelector(
                        Spell.OffGCD(Spell.Cast("Demoralizing Shout", ret => Unit.NearbyUnfriendlyUnits.Any(m => m.SpellDistance() < 10))),

                        Spell.OffGCD( 
                            new PrioritySelector(
                                Spell.BuffSelf("Shield Wall", ret => Me.HealthPercent < WarriorSettings.WarriorShieldWallHealth),
                                Spell.BuffSelf("Shield Barrier", ret => Me.HealthPercent < WarriorSettings.WarriorShieldBarrierHealth),
                                Spell.BuffSelf("Shield Block", ret => Me.HealthPercent < WarriorSettings.WarriorShieldBlockHealth),
                                Spell.BuffSelf("Savage Defense", ret => Me.HealthPercent < WarriorSettings.WarriorShieldBlockHealth && !StyxWoW.Me.HasAura("Shield Block") && Spell.GetSpellCooldown("Shield Block").TotalSeconds > 0)
                                )
                            ),

                        Spell.OffGCD(
                            new PrioritySelector(
                                Spell.OffGCD(Spell.BuffSelf("Last Stand", ret => Me.HealthPercent < WarriorSettings.WarriorLastStandHealth)),
                                Spell.OffGCD(Common.CreateWarriorEnragedRegeneration())
                                )
                            ),

                        new Decorator(
                            req => Me.GotTarget && Me.CurrentTarget.IsWithinMeleeRange,
                            new PrioritySelector(
                        // Symbiosis

                                new Decorator(
                                    ret => Me.CurrentTarget.IsBoss() || Me.CurrentTarget.IsPlayer || (!Me.IsInGroup() && AoeCount >= 3),
                                    new PrioritySelector(
                                        Spell.OffGCD( Spell.Cast("Recklessness")),
                                        Spell.OffGCD( Spell.Cast("Skull Banner")),
                        // Spell.Cast("Demoralizing Banner", ret => !Me.CurrentTarget.IsBoss() && UseAOE),
                                        Spell.OffGCD( Spell.Cast("Avatar"))
                                        )
                                    ),

                                Spell.Cast("Bloodbath"),
                                Spell.Cast("Berserker Rage")
                                )
                            )

                // new Action(ret => { UseTrinkets(); return RunStatus.Failure; }),
                // Spell.Cast("Deadly Calm", ret => TalentManager.HasGlyph("Incite") || Me.CurrentRage >= RageDump)
                        )
                    )
                );
        }

        static WoWUnit intTarget;

        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorProtection, WoWContext.All)]
        public static Composite CreateProtectionNormalCombat()
        {
            TankManager.NeedTankTargeting = (SingularRoutine.CurrentWoWContext == WoWContext.Instances);

            return new PrioritySelector(
                ctx => TankManager.Instance.FirstUnit ?? Me.CurrentTarget,

                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Helpers.Common.CreateAutoAttack(true),

                Spell.WaitForCast(FaceDuring.Yes),

                Common.CheckIfWeShouldCancelBladestorm(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
        
                    new PrioritySelector(

                        CreateDiagnosticOutputBehavior(),

                        Common.CreateVictoryRushBehavior(),

                        Spell.Cast("Execute", 
                            ret => SingularRoutine.CurrentWoWContext != WoWContext.Instances 
                                && Me.CurrentRage > RageDump 
                                && Me.CurrentTarget.HealthPercent < 20),

                        new Decorator( 
                            ret => SingularSettings.Instance.EnableTaunting && SingularRoutine.CurrentWoWContext == WoWContext.Instances,
                            CreateTauntBehavior()
                            ),

                        Spell.Buff("Piercing Howl", ret => Me.CurrentTarget.SpellDistance() < 15 && Me.CurrentTarget.IsPlayer && !Me.CurrentTarget.HasAnyAura("Piercing Howl", "Hamstring") && SingularSettings.Instance.Warrior().UseWarriorSlows),
                        Spell.Buff("Hamstring", ret => Me.CurrentTarget.IsPlayer && !Me.CurrentTarget.HasAnyAura("Piercing Howl", "Hamstring") && SingularSettings.Instance.Warrior().UseWarriorSlows),

                        Common.CreateDisarmBehavior(),

                        CreateProtectionInterrupt(),

                        // special "in combat" pull logic for mobs not tagged and out of melee range
                        Common.CreateWarriorCombatPullMore(),

                        // Handle Ultimatum procs 
                        // Handle Glyph of Incite procs
                        // Dump Rage
                        new Throttle(
                            new Decorator(
                                ret => HasUltimatum || Me.HasAura("Glyph of Incite") || Me.CurrentRage > RageDump,
                                new PrioritySelector(
                                    Spell.Cast("Cleave", ret => Me.IsInGroup() && UseAOE),
                                    Spell.Cast("Heroic Strike")
                                    )
                                )
                            ),

                        // Handle proccing Glyph of Incite buff
                        // Spell.Cast( "Devastate", ret => TalentManager.HasGlyph("Incite") && Me.HasAura("Deadly Calm") && !Me.HasAura("Glyph of Incite")),

                        // Multi-target?  get the debuff on them
                        new Decorator(
                            ret => UseAOE,
                            new PrioritySelector(
                                Spell.Cast("Thunder Clap", on => Unit.UnfriendlyUnits(8).FirstOrDefault()),
                                Spell.Cast("Bladestorm", on => Unit.UnfriendlyUnits(8).FirstOrDefault(), ret => AoeCount >= 4),
                                Spell.Cast("Shockwave", on => Unit.UnfriendlyUnits(8).FirstOrDefault(u => Me.IsSafelyFacing(u)), ret => Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 10f) >= 3),
                                Spell.Cast("Dragon Roar", on => Unit.UnfriendlyUnits(8).FirstOrDefault(u => Me.IsSafelyFacing(u)), ret => Me.CurrentTarget.SpellDistance() <= 8 || Me.CurrentTarget.IsWithinMeleeRange)
                                )
                            ),

                        // Generate Rage
                        Spell.Cast("Shield Slam", ret => Me.CurrentRage < RageBuild && HasShieldInOffHand),
                        Spell.Cast("Revenge", ret => Me.CurrentRage < RageBuild ),
                        Spell.Cast("Devastate", ret => !Me.CurrentTarget.HasAura("Weakened Armor", 3) && Unit.NearbyGroupMembers.Any(m => m.Class == WoWClass.Druid)),
                        Spell.Cast("Thunder Clap", ret => Me.CurrentTarget.SpellDistance() < 8f && !Me.CurrentTarget.ActiveAuras.ContainsKey("Weakened Blows")),

                        // Filler
                        Spell.Cast("Devastate"),
                        Spell.Cast("Heroic Strike", req => !SpellManager.HasSpell("Devastate") || !HasShieldInOffHand),

                        //Charge
                        Common.CreateChargeBehavior(),

                        new Action( ret => {
                            if ( Me.GotTarget && Me.CurrentTarget.IsWithinMeleeRange && Me.IsSafelyFacing(Me.CurrentTarget))
                                Logger.WriteDebug("--- we did nothing!");
                            return RunStatus.Failure;
                            })
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        static Composite CreateTauntBehavior()
        {
            // limit all taunt attempts to 1 per second max since Mocking Banner and Taunt have no GCD
            // .. it will keep us from casting both for the same mob we lost aggro on
            return new Throttle( 1, 1,
                new PrioritySelector(
                    ctx => TankManager.Instance.NeedToTaunt.FirstOrDefault(),

                    Spell.CastOnGround("Mocking Banner",
                        on => (WoWUnit) on,
                        ret => TankManager.Instance.NeedToTaunt.Any() && Clusters.GetCluster(TankManager.Instance.NeedToTaunt.FirstOrDefault(), TankManager.Instance.NeedToTaunt, ClusterType.Radius, 15f).Count() >= 2),

                    Spell.Cast("Taunt", ret => TankManager.Instance.NeedToTaunt.FirstOrDefault()),

                    Spell.Cast("Storm Bolt", ctx => TankManager.Instance.NeedToTaunt.FirstOrDefault(i => i.SpellDistance() < 30 && Me.IsSafelyFacing(i))),

                    Spell.Cast("Intervene", 
                        ctx => TankManager.Instance.NeedToTaunt.FirstOrDefault(
                            mob => Group.Healers.Any(healer => mob.CurrentTargetGuid == healer.Guid && healer.Distance < 25)),
                        ret => MovementManager.IsClassMovementAllowed && Group.Healers.Count( h => h.IsAlive && h.Distance < 40) == 1
                        )
                    )
                );
        }

        static Composite CreateProtectionInterrupt()
        {
            return new Throttle(
                new PrioritySelector(
                    new Action(ret =>
                    {
                        intTarget = Unit.NearbyUnfriendlyUnits.FirstOrDefault(i => i.IsCasting && i.CanInterruptCurrentSpellCast && i.IsWithinMeleeRange && Me.IsSafelyFacing(i));
                        return RunStatus.Failure;
                    }),

                    Spell.Cast("Pummel", ctx => intTarget),

                    new Action(ret =>
                    {
                        intTarget = Unit.NearbyUnfriendlyUnits.FirstOrDefault(i => i.IsCasting && i.CanInterruptCurrentSpellCast && i.Distance < 10);
                        return RunStatus.Failure;
                    }),

                    Spell.Cast("Disrupting Shout", ctx => intTarget),

                    new Action(ret =>
                    {
                        intTarget = Unit.NearbyUnfriendlyUnits.FirstOrDefault(i => i.IsCasting && i.CanInterruptCurrentSpellCast && i.Distance < 30 && Me.IsSafelyFacing(i));
                        return RunStatus.Failure;
                    }),

                    Spell.Cast("Storm Bolt", ctx => intTarget)
                    )
                );
        }

        static bool UseAOE
        {            
            get
            {
                if (Me.GotTarget && Me.CurrentTarget.IsPlayer)
                    return false;

                return AoeCount >= 2 && Spell.UseAOE;
            }
        }

        static int AoeCount
        {
            get
            {
                return Unit.UnfriendlyUnits(8).Count();
            }
        }

        static int RageBuild
        {
            get
            {
                return (int)Me.MaxRage - 5;
            }
        }

        static int RageDump
        {
            get
            {
                return (int)Me.MaxRage - 20;
            }
        }

        static bool HasUltimatum
        {
            get
            {
                return Me.ActiveAuras.ContainsKey("Ultimatum");
            }
        }

        private static Composite _checkShield = null;

        public static Composite CheckThatShieldIsEquippedIfNeeded()
        {
            if (_checkShield == null)
            {
                _checkShield = new ThrottlePasses(60,
                    new Sequence(
                        new DecoratorContinue(
                            ret => !Me.Disarmed && !HasShieldInOffHand && SpellManager.HasSpell("Shield Slam"),
                            new Action(ret => Logger.Write(Color.HotPink, "User Error: a{0} requires a Shield in offhand to cast Shield Slam", TalentManager.CurrentSpec.ToString().CamelToSpaced()))
                            ),
                        new ActionAlwaysFail()
                        )
                    );
            }
            return _checkShield;
        }

        public static bool HasShieldInOffHand
        {
            get
            {
                return IsShield(Me.Inventory.Equipped.OffHand);
            }
        }

        public static bool IsShield(WoWItem hand)
        {
            return hand != null && hand.ItemInfo.ItemClass == WoWItemClass.Armor && hand.ItemInfo.InventoryType == InventoryType.Shield;
        }

        private static Composite CreateDiagnosticOutputBehavior()
        {
            return new ThrottlePasses( 1,
                new Decorator(
                    ret => SingularSettings.Debug,
                    new Action(ret =>
                        {
                        Logger.WriteDebug(".... h={0:F1}%/r={1:F1}%, Ultim={2}, Targ={3} {4:F1}% @ {5:F1} yds Melee={6} Facing={7}",
                            Me.HealthPercent,
                            Me.CurrentRage,
                            HasUltimatum,
                            !Me.GotTarget ? "(null)" : Me.CurrentTarget.SafeName(),
                            !Me.GotTarget ? 0 : Me.CurrentTarget.HealthPercent,
                            !Me.GotTarget ? 0 : Me.CurrentTarget.Distance,
                            Me.GotTarget && Me.CurrentTarget.IsWithinMeleeRange ,
                            Me.GotTarget && Me.IsSafelyFacing( Me.CurrentTarget  )
                            );
                        return RunStatus.Failure;
                        })
                    )
                );
        }

        #endregion

    }
}