using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Styx.Common.Helpers;
using Styx.Helpers;
using Styx.TreeSharp;
using Singular.Helpers;
using Singular.Settings;
using Styx.WoWInternals.WoWObjects;
using Styx;
using Singular.Managers;
using Singular.Dynamics;
using Styx.CommonBot;
using CommonBehaviors.Actions;
using System.Drawing;
using Styx.Pathing;
using Action = Styx.TreeSharp.Action;

namespace Singular.ClassSpecific.Warrior
{
    static class Common
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WarriorSettings WarriorSettings { get { return SingularSettings.Instance.Warrior(); } }
        public static bool HasTalent(WarriorTalents tal) { return TalentManager.IsSelected((int)tal); }

        public static bool Tier14TwoPieceBonus { get { return Me.HasAura("Item - Warrior T14 DPS 2P Bonus"); } }
        public static bool Tier14FourPieceBonus { get { return Me.HasAura("Item - Warrior T14 DPS 4P Bonus"); } }

        public static float DistanceChargeBehavior { get; set; }
        public static int VictoryRushHealth { get; set; }
       
        [Behavior(BehaviorType.Initialize, WoWClass.Warrior)]
        public static Composite CreateWarriorInitialize()
        {
            // removed combatreach because of # of missed Charges
            DistanceChargeBehavior = 25f;

            if (TalentManager.HasGlyph("Long Charge"))
            {
                Logger.Write("[glyph of charge] Recognized - Charge Distance increased by 5 yds");
                DistanceChargeBehavior = 30f;
            }

            string spellVictory = "Victory Rush";
            VictoryRushHealth = 90;
            if (SpellManager.HasSpell("Impending Victory"))
            {
                Logger.Write("[impending victory talent] Recognized");
                VictoryRushHealth = 80;
                spellVictory = "Impending Victory";
            }
            else if (TalentManager.HasGlyph("Victory Rush"))
            {
                Logger.Write("[glyph of victory rush] Recognized");
                VictoryRushHealth -= (100 - VictoryRushHealth) / 2;
            }

            if (WarriorSettings.VictoryRushOnCooldown)
            {
                Logger.Write("[victory rush on cooldown] User Setting will cause [{0}] cast on cooldown", spellVictory);
                VictoryRushHealth = 100;
            }
            else
            {
                Logger.WriteDebug("[victory rush] will cast if health <= {0}%", VictoryRushHealth);
            }

            Logger.Write("[charge distance] Charge cast at targets within {0:F1} yds", DistanceChargeBehavior);

            DistanceChargeBehavior -= 0.2f;    // should not be needed, but is  -- based on log files and observations we need this adjustment

            return null;
        }

        [Behavior(BehaviorType.Rest, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.All)]
        [Behavior(BehaviorType.Rest, WoWClass.Warrior, WoWSpec.WarriorFury, WoWContext.All)]
        public static Composite CreateWarriorRest()
        {
            return new PrioritySelector(

                CheckIfWeShouldCancelBladestorm(),

                Singular.Helpers.Rest.CreateDefaultRestBehaviour(),

                new Decorator(
                    req => TalentManager.CurrentSpec == WoWSpec.WarriorProtection,
                    ClassSpecific.Warrior.Protection.CheckThatShieldIsEquippedIfNeeded()
                    )
                );
        }


        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Warrior)]
        public static Composite CreateWarriorNormalPreCombatBuffs()
        {
            return
                new PrioritySelector(
                    Spell.BuffSelf(SelectedStance.ToString().CamelToSpaced(), ret => StyxWoW.Me.Shapeshift != (ShapeshiftForm)SelectedStance),
                    Spell.BuffSelf(Common.SelectedShout)
                    );
        }

        [Behavior(BehaviorType.LossOfControl, WoWClass.Warrior)]
        public static Composite CreateWarriorLossOfControlBehavior()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Berserker Rage", ret => Me.Fleeing || (Me.Stunned && Me.HasAuraWithMechanic(Styx.WoWInternals.WoWSpellMechanic.Sapped))),
                // StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Fleeing, WoWSpellMechanic.Sapped, WoWSpellMechanic.Incapacitated, WoWSpellMechanic.Horrified)),

                CreateWarriorEnragedRegeneration()
                );
        }

        public static Composite CreateWarriorCombatPullMore()
        {
            return new Throttle(
                2,
                new Decorator(
                    req => SingularRoutine.CurrentWoWContext == WoWContext.Normal
                        && Me.GotTarget
                        && !Me.CurrentTarget.IsPlayer
                        && !Me.CurrentTarget.IsTagged
                        && !Me.HasAura("Charge")
                        && !Me.CurrentTarget.HasAnyOfMyAuras("Charge Stun", "Warbringer")
                        && !Me.CurrentTarget.IsWithinMeleeRange,
                    new PrioritySelector(
                        Common.CreateChargeBehavior(),
                        Spell.Cast("Heroic Throw", req => Me.IsSafelyFacing(Me.CurrentTarget)),
                        Spell.Cast("Taunt"),
                        new Decorator(
                            req => SpellManager.HasSpell("Throw") && Me.CurrentTarget.SpellDistance() < 27,
                            new Sequence(
                                new Action( r => StopMoving.Now() ),
                                new Wait( 1, until => !Me.IsMoving, new ActionAlwaysSucceed()),
                                Spell.Cast( s => "Throw", mov => true, on => Me.CurrentTarget, req => true, cancel => false ),
                                new WaitContinue( 1, until => !Me.IsCasting, new ActionAlwaysSucceed())
                                )
                            )
                        )
                    )
                );
        }

        public static Composite CreateWarriorEnragedRegeneration()
        {
            return new Decorator(
                req => Me.HealthPercent < WarriorSettings.WarriorEnragedRegenerationHealth && !Spell.IsSpellOnCooldown("Enraged Regeneration"),
                new Sequence(
                    new PrioritySelector(
                        Spell.BuffSelf("Berserker Rage"),
                        new ActionAlwaysSucceed()
                        ),
                    Spell.BuffSelf("Enraged Regeneration")
                    )
                );
        }

        public static string SelectedShout
        {
            get { return WarriorSettings.Shout.ToString().CamelToSpaced().Substring(1); }
        }

        public static WarriorStance  SelectedStance
        {
            get
            {
                var stance = WarriorSettings.Stance;
                if (stance == WarriorStance.Auto)
                {
                    switch (TalentManager.CurrentSpec)
                    {
                        case WoWSpec.WarriorArms:
                            stance = WarriorStance.BattleStance;
                            break;
                        case WoWSpec.WarriorFury:
                            stance = WarriorStance.BerserkerStance;
                            break;
                        default:
                        case WoWSpec.WarriorProtection:
                            stance = WarriorStance.DefensiveStance;
                            break;
                    }
                }

                return stance ;
            }
        }

        /// <summary>
        /// keep a single copy of Charge Behavior so the wrapping Throttle will account for 
        /// uses across multiple behaviors that reference this method
        /// </summary>
        private static Composite _singletonChargeBehavior = null;

        public static Composite CreateChargeBehavior()
        {
            if (!WarriorSettings.UseWarriorCloser)
                return new ActionAlwaysFail();


            if (!WarriorSettings.UseWarriorCloser)
                return new ActionAlwaysFail();

            if (_singletonChargeBehavior == null)
            {
                _singletonChargeBehavior = new Throttle(TimeSpan.FromMilliseconds(1500),
                    new PrioritySelector(
                        ctx => Me.CurrentTarget,
                        new Decorator(
                            req => MovementManager.IsClassMovementAllowed 
                                && req != null 
                                && ((req as WoWUnit).Guid != Singular.Utilities.EventHandlers.LastNoPathTarget || Singular.Utilities.EventHandlers.LastNoPathFailure < DateTime.Now - TimeSpan.FromMinutes(15))
                                && !Me.HasAura("Charge")
                                && !(req as WoWUnit).HasAnyOfMyAuras( "Charge Stun", "Warbringer")
                                && Me.IsSafelyFacing( req as WoWUnit)
                                && (req as WoWUnit).InLineOfSight,

                            new PrioritySelector(
                                // note: use Distance here -- even though to a WoWUnit, hitbox does not come into play
                                Spell.Cast("Charge", req => (req as WoWUnit).Distance.Between( 8, DistanceChargeBehavior)),

                                //  Leap to close distance
                                // note: use Distance rather than SpellDistance since spell is to point on ground
                                Spell.CastOnGround("Heroic Leap",
                                    on => (WoWUnit) on,
                                    req => (req as WoWUnit).Distance.Between( 8, 40),
                                    false
                                    )
                                )
                            )
                        )
                    );
            }

            return _singletonChargeBehavior;
        }


        public static Composite CreateVictoryRushBehavior()
        {
            // health below determined %
            // user wants to cast on cooldown without regard to health
            // we have aura AND (target is about to die OR aura expires in less than 3 secs)
            return new Throttle( 
                new Decorator(
                    ret => WarriorSettings.VictoryRushOnCooldown
                        || Me.HealthPercent <= VictoryRushHealth
                        || Me.HasAura("Victorious") && (Me.HasAuraExpired("Victorious", 3) || (Me.GotTarget && Me.CurrentTarget.TimeToDeath() < 7)),
                    new PrioritySelector(
                        Spell.Cast("Impending Victory"),
                        Spell.Cast("Victory Rush", ret => Me.HasAura("Victorious"))
                        )
                    )
                );
        }

        public static Composite CreateDisarmBehavior()
        {
            if ( !WarriorSettings.UseDisarm )
                return new ActionAlwaysFail();

            if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
            {
                return new Throttle(15, 
                    Spell.Cast("Disarm", on => {
                        if (Spell.IsSpellOnCooldown("Disarm"))
                            return null;

                        WoWUnit unit = Unit.NearbyUnfriendlyUnits.FirstOrDefault(
                            u => u.IsWithinMeleeRange
                                && (u.IsMelee() || u.Class == WoWClass.Hunter)
                                && !Me.CurrentTarget.Disarmed 
                                && !Me.CurrentTarget.IsCrowdControlled()
                                && Me.IsSafelyFacing(u, 150)
                                );
                        return unit;
                        })
                    );
            }

            return new Throttle( 15, Spell.Cast("Disarm", req => !Me.CurrentTarget.Disarmed && !Me.CurrentTarget.IsCrowdControlled()));
        }

        /// <summary>
        /// checks if in a relatively balanced fight where atleast 3 of your
        /// teammates will benefti from long cooldowns.  fight must be atleast 3 v 3
        /// and size difference between factions nearby in fight cannot be greater
        /// than size / 3 + 1.  For example:
        /// 
        /// Yes:  3 v 3, 3 v 4, 3 v 5, 6 v 9, 9 v 13
        /// No :  2 v 3, 3 v 6, 4 v 7, 6 v 10, 9 v 14
        /// </summary>
        public static bool IsPvpFightWorthBanner
        {
            get 
            {
                int friends = Unit.NearbyFriendlyPlayers.Count(f => f.IsAlive);
                if (friends < 3)
                    return false;

                int enemies = Unit.NearbyUnfriendlyUnits.Count();
                if (enemies < 3)
                    return false;

                int diff = Math.Abs(friends - enemies);
                return diff <= ((friends / 3) + 1);
            }
        }


        public static Composite CreateAttackFlyingOrUnreachableMobs()
        {
            return new Decorator(
                ret =>
                {
                    if (!Me.GotTarget)
                        return false;

                    if (Me.CurrentTarget.IsPlayer)
                        return false;

                    if (Me.CurrentTarget.IsFlying)
                    {
                        Logger.Write(Color.White, "Ranged Attack: {0} is Flying! using Ranged attack....", Me.CurrentTarget.SafeName());
                        return true;
                    }

                    if ((DateTime.Now - Singular.Utilities.EventHandlers.LastNoPathFailure).TotalSeconds < 1f)
                    {
                        Logger.Write(Color.White, "Ranged Attack: No Path Available error just happened, so using Ranged attack ....", Me.CurrentTarget.SafeName());
                        return true;
                    }
/*
                    if (Me.CurrentTarget.IsAboveTheGround())
                    {
                    Logger.Write(Color.White, "{0} is {1:F1) yds above the ground! using Ranged attack....", Me.CurrentTarget.SafeName(), Me.CurrentTarget.HeightOffTheGround());
                    return true;
                    }
*/
                    double heightCheck = Me.CurrentTarget.MeleeDistance();
                    if (Me.CurrentTarget.Distance2DSqr < heightCheck * heightCheck && Math.Abs(Me.Z - Me.CurrentTarget.Z) >= heightCheck )
                    {
                        Logger.Write(Color.White, "Ranged Attack: {0} appears to be off the ground! using Ranged attack....", Me.CurrentTarget.SafeName());
                        return true;
                    }
                    
                    WoWPoint dest = Me.CurrentTarget.Location;
                    if (!Me.CurrentTarget.IsWithinMeleeRange && !Styx.Pathing.Navigator.CanNavigateFully(Me.Location, dest))
                    {
                        Logger.Write(Color.White, "Ranged Attack: {0} is not Fully Pathable! using ranged attack....", Me.CurrentTarget.SafeName());
                        return true;
                    }

                    return false;
                },
                new PrioritySelector(
                    Spell.Cast("Heroic Throw"),
                    new Sequence(
                        new PrioritySelector(
                            Movement.CreateEnsureMovementStoppedBehavior( 27f, on => Me.CurrentTarget, reason: "To cast Throw"),
                            new ActionAlwaysSucceed()
                            ),
                        new Wait( 1, until => !Me.IsMoving, new ActionAlwaysSucceed()),
                        Spell.Cast("Throw")
                        )
                    )
                );
        }

        public static Composite CheckIfWeShouldCancelBladestorm()
        {
            if (!HasTalent(WarriorTalents.Bladestorm))
                return new ActionAlwaysFail();

            return new ThrottlePasses(
                1, 
                TimeSpan.FromSeconds(1),
                RunStatus.Failure,
                new Decorator(
                    req => StyxWoW.Me.HasAura("Bladestorm"),
                    new Action(r =>
                    {
                        // check if it makes sense to cancel to resume normal speed movement and charge
                        if (Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Rest || !Me.Combat || !Unit.UnfriendlyUnits(20).Any(u => u.Aggro || (u.IsPlayer && u.IsHostile)))
                        {
                            Logger.WriteDebug("Bladestorm: cancel since out of combat or no targets within 20 yds");
                            Me.CancelAura("Bladestorm");
                            return RunStatus.Success;
                        }
                        return RunStatus.Failure;
                    })
                    )
                );
        }


    }

    enum WarriorTalents
    {
        None = 0,
        Juggernaut,
        DoubleTime,
        Warbringer,
        EnragedRegeneration,
        SecondWind,
        ImpendingVictory,
        StaggeringShout,
        PiercingHowl,
        DisruptingShout,
        Bladestorm,
        Shockwave,
        DragonRoar,
        MassSpellReflection,
        Safeguard,
        Vigilance,
        Avatar,
        Bloodbath,
        StormBolt
    }
}
