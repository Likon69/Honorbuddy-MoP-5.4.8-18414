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
using Styx.Pathing;

namespace Singular.ClassSpecific.Warrior
{
    /// <summary>
    /// plaguerized from Apoc's simple Arms Warrior CC 
    /// see http://www.thebuddyforum.com/honorbuddy-forum/combat-routines/warrior/79699-arms-armed-quick-dirty-simple-fast.html#post815973
    /// </summary>
    public class Arms
    {

        #region Common

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WarriorSettings WarriorSettings { get { return SingularSettings.Instance.Warrior(); } }
        private static bool HasTalent(WarriorTalents tal) { return TalentManager.IsSelected((int)tal); }

        [Behavior(BehaviorType.Pull, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.All)]
        public static Composite CreateArmsNormalPull()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Helpers.Common.CreateAutoAttack(false),

                Spell.WaitForCast(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        CreateDiagnosticOutputBehavior("Pull"),

                        new Throttle( 2,
                            Spell.Cast(Common.SelectedShout, 
                                on => Me, 
                                req => !Me.HasAura(Common.SelectedShout) 
                                    || (Me.CurrentRage < (Me.MaxRage - 40) && (Me.SpellDistance() >= 27 || Spell.GetSpellCooldown("Charge").TotalSeconds > 1))
                                )
                            ),

                        Common.CreateAttackFlyingOrUnreachableMobs(),

                        Common.CreateChargeBehavior(),

                        Spell.Cast("Mortal Strike")
                        )
                    )
                );
        }

        #endregion

        #region Normal

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.Normal)]
        [Behavior(BehaviorType.CombatBuffs, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.Instances)]
        public static Composite CreateArmsCombatBuffsNormal()
        {
            return new Throttle(
                new Decorator(
                    ret => Me.GotTarget && Me.CurrentTarget.IsWithinMeleeRange && !Unit.IsTrivial(Me.CurrentTarget),

                    new PrioritySelector(

                        Common.CreateWarriorEnragedRegeneration(),

                        new Decorator(
                            ret => {
                                if (Me.CurrentTarget.IsPlayer || Me.CurrentTarget.IsBoss())
                                    return true;

                                if ( SingularRoutine.CurrentWoWContext != WoWContext.Normal)
                                    return false;

                                if (Me.CurrentTarget.TimeToDeath() > 40)
                                    return true;

                                return Unit.NearbyUnfriendlyUnits.Count(u => u.IsWithinMeleeRange) >= 4;
                                },
                            new PrioritySelector(
                                Spell.OffGCD( Spell.BuffSelf("Die by the Sword", req => Me.HealthPercent < 50)),
                                Spell.OffGCD( Spell.CastOnGround("Demoralizing Banner", on => Me.CurrentTarget, req => true, false)),
                                Spell.OffGCD( Spell.BuffSelf("Skull Banner" )),
                                Spell.OffGCD( Spell.BuffSelf("Avatar")),
                                Spell.OffGCD( Spell.BuffSelf("Bloodbath"))
                                )
                            ),

                        Spell.OffGCD( Spell.BuffSelf("Rallying Cry", req => !Me.IsInGroup() && Me.HealthPercent < 50)),

                        Spell.OffGCD( Spell.Cast("Recklessness", ret => (Spell.CanCastHack("Execute") || Common.Tier14FourPieceBonus || PartyBuff.WeHaveBloodlust) && (StyxWoW.Me.CurrentTarget.TimeToDeath() > 40 || StyxWoW.Me.CurrentTarget.IsBoss() || SingularRoutine.CurrentWoWContext != WoWContext.Instances))),

                        Spell.Cast("Storm Bolt"),  // in normal rotation

                        // Execute is up, so don't care just cast
                        Spell.Cast("Berserker Rage", ret => {
                            if (Me.CurrentTarget.HealthPercent <= 20)
                                return true;
                            if (!Me.ActiveAuras.ContainsKey("Enrage") && Spell.GetSpellCooldown("Colossus Smash").TotalSeconds > 6)
                                return true;
                            return false;
                            }),


                        Spell.BuffSelf(Common.SelectedShout)

                        )
                    )
                );
        }


        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.Normal)]
        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.Instances )]
        public static Composite CreateArmsCombatNormal()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Helpers.Common.CreateAutoAttack(false),

                Spell.WaitForCast(FaceDuring.Yes),

                Common.CheckIfWeShouldCancelBladestorm(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),

                    new PrioritySelector(
                        
                        CreateDiagnosticOutputBehavior("Combat"),

                        Helpers.Common.CreateInterruptBehavior(),

                        Common.CreateVictoryRushBehavior(),

                        Common.CreateDisarmBehavior(),

                        // special "in combat" pull logic for mobs not tagged and out of melee range
                        Common.CreateWarriorCombatPullMore(),

                        new Throttle(
                            new Decorator(
                                ret => Me.HasAura("Glyph of Incite"),
                                Spell.Cast("Heroic Strike")
                                )
                            ),

                        Spell.Buff("Piercing Howl", ret => Me.CurrentTarget.Distance < 10 && Me.CurrentTarget.IsPlayer && !Me.CurrentTarget.HasAnyAura("Piercing Howl", "Hamstring") && SingularSettings.Instance.Warrior().UseWarriorSlows),
                        Spell.OffGCD( Spell.Buff("Hamstring", ret => Me.CurrentTarget.IsPlayer && !Me.CurrentTarget.HasAnyAura("Piercing Howl", "Hamstring") && SingularSettings.Instance.Warrior().UseWarriorSlows) ),

                        CreateArmsAoeCombat(ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance < (u.MeleeDistance() + 1))),

                        new Decorator(
                            ret => WarriorSettings.ArmsSpellPriority == Singular.Settings.WarriorSettings.SpellPriority.IcyVeins,

                            new PrioritySelector(
                                // Noxxic
                                //----------------
                                // 1. Mortal Strike on cooldown. Applies Deep Wounds.
                                Spell.Cast("Mortal Strike"),

                                // 2. Colossus Smash Use as often as possible. Watch for Sudden Death procs.
                                Spell.Cast("Colossus Smash"),

                                // 3. Heroic Leap if Colossus Smash buff is up. Not on the GCD!
/*
                                new Sequence(
                                    Spell.CastOnGround("Heroic Leap", loc => Me.Location, req => Me.GotTarget && Me.CurrentTarget.SpellDistance() < 8 && Me.CurrentTarget.HasAura("Colossus Smash"), false),
                                    new ActionAlwaysFail()),
*/
                                // 4. Heroic Strike to dump Rage (70+) when Colossus Smash is up. Not on the GCD!
                                //      added cast when Colossus Smash not learned yet -OR- target will die soon
                                new Sequence(
                                    Spell.Cast("Heroic Strike", req => NeedHeroicStrikeDumpNoxxic ),
                                    new ActionAlwaysFail()
                                    ),

                                // 5. Execute on cooldown when target is below 20% health.
                                Spell.Cast("Execute"),

                                // 6. Overpower whenever available.
                                Spell.Cast("Overpower"),

                                // 7. Slam to dump Rage (40+) when target is above 20% Health.
                                Spell.Cast("Slam", ret => Me.RagePercent >= 40 && Me.CurrentTarget.HealthPercent > 20),

                                // Added Use of Non-Rage consuming Abilities for players/bosses
                                new Decorator(
                                    ret => Spell.UseAOE && Me.GotTarget && (Me.CurrentTarget.IsPlayer || Me.CurrentTarget.IsBoss()) && Me.CurrentTarget.Distance < 8,
                                    new PrioritySelector(
                                        Spell.Cast("Storm Bolt"),
                                        Spell.BuffSelf("Bladestorm"),
                                        Spell.Cast("Shockwave"),
                                        Spell.Cast("Dragon Roar")
                                        )
                                    ),

                                // Added cast of Shout for rage generation
                                Spell.Cast(Common.SelectedShout, ret => StyxWoW.Me.CurrentRage < (StyxWoW.Me.MaxRage - 20))
                                )
                            ),


                        new Decorator(
                            ret => WarriorSettings.ArmsSpellPriority == Singular.Settings.WarriorSettings.SpellPriority.Noxxic,
                            new PrioritySelector(
                                // Icy-Veins
                                //-------------------------------
                                // 1.    Use Mortal Strike on cooldown.
                                Spell.Cast("Mortal Strike"),

                                // 2.    Use Colossus Smash
                                //           If the Colossus Smash debuff is not active, or if it has less than 1.5 seconds remaining on the target.
                                Spell.Cast("Colossus Smash", ret => Me.CurrentTarget.GetAuraTimeLeft("Colossus Smash").TotalMilliseconds < 1500),

                                // 3.    Use Heroic Strike (remember that it is not on the global cooldown).
                                //           If the debuff applied by Colossus Smash is active and you have 70 or more rage OR
                                //           If you cannot use Execute Icon Execute (the target is above 20% health) and you have 85 or more rage.
                                //           added case of Colossus Smash not trained -OR- target will die soon
                                new Sequence(
                                    Spell.Cast("Heroic Strike", req => NeedHeroicStrikeDumpIcyVeins),
                                    new ActionAlwaysFail()
                                    ),

                                // 4.    Use Execute
                                //           If the target is below 20% health (it is not available otherwise) AND
                                //           If the debuff applied by Colossus Smash is active.
                                Spell.Cast("Execute", ret => Me.CurrentTarget.HasAura("Colossus Smash")),

                                // 5.    Use abilities that cost no rage, such as your tier 4 talents or Impending Victory Icon Impending Victory.
                                new Decorator(
                                    ret => Spell.UseAOE && Me.GotTarget && (Me.CurrentTarget.IsPlayer || Me.CurrentTarget.IsBoss()) && Me.CurrentTarget.Distance < 8,
                                    new PrioritySelector(
                                        Spell.Cast("Storm Bolt"),
                                        Spell.BuffSelf("Bladestorm"),
                                        Spell.Cast("Shockwave"),
                                        Spell.Cast("Dragon Roar")
                                        )
                                    ),

                                // 6.    Use Execute.
                                Spell.Cast("Execute"),

                                // 7.    Use Slam
                                //           If you cannot use Execute Icon Execute (the target is above 20% health) AND
                                //           If you have 90 or more rage.
                                Spell.Cast("Slam", ret => Me.RagePercent >= 90 && Me.CurrentTarget.HealthPercent > 20),
                        
                                // 8.    Use Overpower (remember that it is free of rage cost for 10 seconds after using Execute Icon Execute).
                                Spell.Cast("Overpower"),

                                // 9.    Use Slam
                                //           If you cannot use Execute Icon Execute (the target is above 20% health) AND
                                //           If you have 40 or more rage.
                                Spell.Cast("Slam", ret => Me.RagePercent >= 40 && Me.CurrentTarget.HealthPercent > 20),

                                //10.    Use Battle Shout or Commanding Shout Icon Commanding Shout (depending on which of the two you have chosen to provide for your raid) in order to generate rage when nothing else is available (only if you have less than 85 rage).
                                Spell.Cast( Common.SelectedShout, ret => StyxWoW.Me.CurrentRage < 85 )
                                )
                            ),

                        new Decorator(
                            ret => WarriorSettings.ArmsSpellPriority == Singular.Settings.WarriorSettings.SpellPriority.ElitistJerks,
                            new PrioritySelector(
        #region EXECUTE AVAILABLE
                                new Decorator( ret => Me.CurrentTarget.HealthPercent <= 20,
                                    new PrioritySelector(
                                        Spell.Cast("Colossus Smash"),
                                        Spell.Cast("Execute"),
                                        Spell.Cast("Mortal Strike"),
                                        Spell.Cast("Overpower"),
                                        Spell.Cast("Storm Bolt"),
                                        Spell.Cast("Dragon Roar", ret => (Me.CurrentTarget.IsBoss() || SingularRoutine.CurrentWoWContext != WoWContext.Instances) && (Me.CurrentTarget.Distance <= 8 || Me.CurrentTarget.IsWithinMeleeRange)),
                                        Spell.Cast("Slam"),
                                        Spell.Cast(Common.SelectedShout))
                                    ),
        #endregion

        #region EXECUTE NOT AVAILABLE
                                new Decorator(ret => Me.CurrentTarget.HealthPercent > 20,
                                    new PrioritySelector(
                                        // Only drop DC if we need to use HS for TFB. This lets us avoid breaking HS as a rage dump, when we don't want it to be one.
                                        // Spell.Cast("Deadly Calm", ret => NeedTasteForBloodDump),

                                        new Sequence(
                                            Spell.Cast("Heroic Strike", ret => NeedHeroicStrikeDumpIcyVeins ),
                                            new ActionAlwaysFail()
                                            ),

                                        Spell.Cast("Colossus Smash", ret => !StyxWoW.Me.CurrentTarget.HasAura("Colossus Smash")),
                                        Spell.Cast("Execute"),
                                        Spell.Cast("Mortal Strike"),

                                        //HeroicLeap(),

                                        Spell.Cast("Storm Bolt"),
                                        Spell.Cast("Dragon Roar", ret => (Me.CurrentTarget.IsBoss() || SingularRoutine.CurrentWoWContext != WoWContext.Instances) && (Me.CurrentTarget.SpellDistance() <= 8 || Me.CurrentTarget.IsWithinMeleeRange)),
                                        Spell.Cast("Overpower"),

                                        // Rage dump!
                                        Spell.Cast("Slam", ret => (StyxWoW.Me.RagePercent >= 50 || StyxWoW.Me.CurrentTarget.HasAura("Colossus Smash")) && StyxWoW.Me.CurrentTarget.HealthPercent > 20)
                                        )
                                    )
        #endregion
                                )
                            ),

                        Common.CreateChargeBehavior()
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        private static Composite CreateArmsAoeCombat(SimpleIntDelegate aoeCount)
        {
            return new PrioritySelector(
                Spell.OffGCD( Spell.BuffSelf("Sweeping Strikes", ret => aoeCount(ret) >= 2) ),
                new Decorator(ret => Spell.UseAOE && aoeCount(ret) >= 3,
                    new PrioritySelector(
                        Spell.Cast( "Thunder Clap" ),

                        Spell.Cast("Bladestorm", ret => aoeCount(ret) >= 4),
                        Spell.Cast("Shockwave", ret => Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 10f) >= 3),
                        Spell.Cast("Dragon Roar"),

                        Spell.Cast("Whirlwind"),
                        Spell.Cast("Mortal Strike"),
                        Spell.Cast("Colossus Smash", ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Colossus Smash")),
                        Spell.Cast("Overpower")
                        )
                    )
                );
        }


        [Behavior(BehaviorType.CombatBuffs, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.Battlegrounds)]
        public static Composite CreateArmsCombatBuffsBattlegrounds()
        {
            return new Throttle(
                new Decorator(
                    ret => Me.GotTarget && Me.CurrentTarget.IsWithinMeleeRange,

                    new PrioritySelector(
                        Spell.BuffSelf(Common.SelectedShout),

                        Spell.BuffSelf("Die by the Sword", req => Me.HealthPercent < 70),

                        Spell.BuffSelf("Rallying Cry", req => Me.HealthPercent < 60),

                        Spell.CastOnGround("Demoralizing Banner", on => Me.CurrentTarget, req => true, false),

                        new Decorator(
                            ret => Me.CurrentTarget.IsWithinMeleeRange && Me.CurrentTarget.IsCrowdControlled(),
                            new PrioritySelector(
                                Spell.OffGCD( Spell.BuffSelf("Avatar")),
                                Spell.OffGCD( Spell.BuffSelf("Bloodbath")),
                                Spell.OffGCD( Spell.BuffSelf("Recklessness")),
                                Spell.OffGCD( Spell.Cast("Skull Banner"))
                                )
                            ),

                        // Execute is up, so don't care just cast
                        Spell.Cast("Berserker Rage", ret => Me.CurrentTarget.HealthPercent <= 20),

                        // try to avoid overlapping Enrages
                        Spell.Cast("Berserker Rage", 
                            ret => !Me.ActiveAuras.ContainsKey("Enrage")
                                && Spell.GetSpellCooldown("Mortal Strike").TotalSeconds > 4
                                && Spell.GetSpellCooldown("Colossus Smash").TotalSeconds > 6
                                )
                        )
                    )
                );
        }


        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.Battlegrounds)]
        public static Composite CreateArmsCombatBattlegrounds()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Helpers.Common.CreateAutoAttack(false),

                Spell.WaitForCast(FaceDuring.Yes),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown() && !StyxWoW.Me.HasAura("Bladestorm"),

                    new PrioritySelector(

                        CreateDiagnosticOutputBehavior(),

                        Helpers.Common.CreateInterruptBehavior(),

                        Spell.Cast("Shattering Throw",
                            ret => Me.CurrentTarget.IsPlayer
                                && Me.CurrentTarget.HasAnyAura("Ice Block", "Hand of Protection")),

                        Common.CreateVictoryRushBehavior(),

                        // manufacture some rage
                        Spell.Cast(Common.SelectedShout,
                            ret => StyxWoW.Me.CurrentRage < 50
                                && Me.CurrentTarget.Distance > 10
                                && !Spell.IsSpellOnCooldown("Charge")),
                      
            #region Stun

                // charge them now
                        Common.CreateChargeBehavior(),

                        // another stun on them if possible
                        new Decorator(
                            ret => !Me.CurrentTarget.Stunned && !Me.HasAura("Charge"),
                            new PrioritySelector(
                                Spell.Cast("Shockwave", req => Me.CurrentTarget.SpellDistance() < 10 && Me.IsSafelyFacing(Me.CurrentTarget, 90f)),
                                Spell.Cast("Storm Bolt", req => Spell.IsSpellOnCooldown("Shockwave") || Me.CurrentTarget.SpellDistance() > 10)
                                )
                            ),

            #endregion

            #region Slow

                // slow them down
                        new Decorator(
                            ret => WarriorSettings.UseWarriorSlows
                                && Me.CurrentTarget.IsPlayer
                                && !Me.CurrentTarget.Stunned
                                && !Me.CurrentTarget.IsCrowdControlled()
                                && !Me.CurrentTarget.HasAuraWithEffect(WoWApplyAuraType.ModDecreaseSpeed),
                            new PrioritySelector(
                                Spell.Buff("Piercing Howl", req => Me.CurrentTarget.Distance < 15),
                                Spell.Buff("Hamstring")
                                )
                            ),

                        Spell.Cast("Staggering Shout",
                            ret => Me.CurrentTarget.IsWithinMeleeRange && Me.CurrentTarget.DistanceSqr < 20 * 20
                                && !Me.CurrentTarget.Stunned
                                && !Me.CurrentTarget.IsCrowdControlled()
                                && Me.CurrentTarget.HasAuraWithEffect(WoWApplyAuraType.ModDecreaseSpeed)),

                        Common.CreateDisarmBehavior(),
            #endregion

            #region Damage

                // see if we can get debuff on them
                        Spell.Cast("Colossus Smash", ret => Me.CurrentTarget.HasAuraWithEffect(WoWApplyAuraType.ModDecreaseSpeed) && Me.CurrentTarget.GetAuraTimeLeft("Colossus Smash").TotalMilliseconds < 1500),

                        Spell.Cast("Heroic Strike", req => Me.RagePercent > 85),
                        Spell.Cast("Mortal Strike"),
                        Spell.Cast("Overpower"),
                        Spell.Cast("Slam", req => Me.RagePercent > 65 && Me.CurrentTarget.HasAura("Colossus Smash")),

                        Spell.Cast("Thunder Clap",
                            req => {
                                if (Me.CurrentTarget.SpellDistance() <= 8)
                                {
                                    if (Me.CurrentTarget.IsMelee() && !Me.CurrentTarget.HasAura("Weakened Blows"))
                                        return true;

                                    if (!Me.CurrentTarget.IsWithinMeleeRange || !Me.IsSafelyFacing(Me.CurrentTarget))
                                        return true;
                                }

                                return false;
                            })

            #endregion

)
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }


        private static Composite HeroicLeap()
        {
            return new Decorator(ret => Me.CurrentTarget.HasAura("Colossus Smash") && Spell.CanCastHack("Heroic Leap"),
                new Sequence(
                    new Action(ret =>
                    {
                        var tpos = StyxWoW.Me.CurrentTarget.Location;
                        var trot = StyxWoW.Me.CurrentTarget.Rotation;
                        var leapRight = WoWMathHelper.CalculatePointAtSide(tpos, trot, 5, true);
                        var leapLeft = WoWMathHelper.CalculatePointAtSide(tpos, trot, 5, true);

                        var myPos = StyxWoW.Me.Location;

                        var leftDist = leapLeft.Distance(myPos);
                        var rightDist = leapRight.Distance(myPos);

                        var leapPos = WoWMathHelper.CalculatePointBehind(tpos, trot, 8);

                        if (leftDist > rightDist && leftDist <= 40 && leftDist >= 8)
                            leapPos = leapLeft;
                        else if (rightDist > leftDist && rightDist <= 40 && rightDist >= 8)
                            leapPos = leapRight;
                        else
                            return RunStatus.Failure;

                        Spell.LogCast("Heroic Leap", Me.CurrentTarget);
                        SpellManager.Cast("Heroic Leap");
                        SpellManager.ClickRemoteLocation(leapPos);
                        return RunStatus.Success;
                    }),

                    Movement.CreateFaceTargetBehavior( 20f, true)
                    )
                );
        }


        private static void UseTrinkets()
        {
            var firstTrinket = StyxWoW.Me.Inventory.Equipped.Trinket1;
            var secondTrinket = StyxWoW.Me.Inventory.Equipped.Trinket2;
            var hands = StyxWoW.Me.Inventory.Equipped.Hands;

            if (firstTrinket != null && CanUseEquippedItem(firstTrinket))
                firstTrinket.Use();


            if (secondTrinket != null && CanUseEquippedItem(secondTrinket))
                secondTrinket.Use();

            if (hands != null && CanUseEquippedItem(hands))
                hands.Use();

        }
        private static bool CanUseEquippedItem(WoWItem item)
        {
            string itemSpell = Lua.GetReturnVal<string>("return GetItemSpell(" + item.Entry + ")", 0);
            if (string.IsNullOrEmpty(itemSpell))
                return false;

            return item.Usable && item.Cooldown <= 0;
        }

/*
        static bool NeedTasteForBloodDump
        {
            get
            {
                var tfb = Me.GetAllAuras().FirstOrDefault(a => a.Name == "Taste for Blood" && a.TimeLeft > TimeSpan.Zero && a.StackCount > 0);
                if (tfb != null)
                {
                    // If we have more than 3 stacks, pop HS
                    if (tfb.StackCount >= 3)
                    {
                        Logger.WriteDebug(Color.White, "^Taste for Blood");
                        return true;
                    }

                    // If it's about to drop, and we have at least 2 stacks, then pop HS.
                    // If we have 1 stack, then a slam is better used here.
                    if (tfb.TimeLeft.TotalSeconds < 1 && tfb.StackCount >= 2)
                    {
                        Logger.WriteDebug(Color.White, "^Taste for Blood (falling off)");
                        return true;
                    }
                }
                return false;
            }
        }
*/
        static bool NeedHeroicStrikeDumpIcyVeins
        {
            get
            {
                if (Me.GotTarget && Me.RagePercent >= 70 && Spell.CanCastHack("Heroic Strike", Me.CurrentTarget, skipWowCheck: true))
                {
                    if (Me.RagePercent >= (Me.MaxRage - 15) && (Me.CurrentTarget.HealthPercent > 20 || !SpellManager.HasSpell("Colossus Smash")))
                    {
                        Logger.Write(Color.White, "^Heroic Strike - Rage Dump @ {0}%", (int)Me.RagePercent);
                        return true;
                    }

                    if (Me.CurrentTarget.HasAura("Colossus Smash"))
                    {
                        Logger.Write(Color.White, "^Heroic Strike - Rage Dump @ {0}% with Colossus Smash active", (int)Me.RagePercent);
                        return true;
                    }
                }

                return false;
            }
        }

        static bool NeedHeroicStrikeDumpNoxxic
        {
            get
            {
                if (Me.GotTarget && Me.RagePercent >= 70 && Spell.CanCastHack("Heroic Strike", Me.CurrentTarget, skipWowCheck: true))
                {
                    if (Me.CurrentTarget.HasAura("Colossus Smash") || !SpellManager.HasSpell("Colossus Smash") || Me.CurrentTarget.TimeToDeath() < 8)
                    {
                        Logger.Write(Color.White, "^Heroic Strike - Rage Dump @ {0}%", (int)Me.RagePercent);
                        return true;
                    }
                }
                return false;
            }
        }

        private static Composite CreateDiagnosticOutputBehavior(string context = null)
        {
            if (context == null)
                context = "...";
            else
                context = "<<" + context + ">>";

            return new Decorator(
                ret => SingularSettings.Debug,
                new ThrottlePasses(1,
                    new Action(ret =>
                    {
                        string log;
                        log = string.Format(context + " h={0:F1}%/r={1:F1}%, stance={2}, Enrage={3} Coloss={4} MortStrk={5}",
                            Me.HealthPercent,
                            Me.CurrentRage,
                            Me.Shapeshift,
                            Me.ActiveAuras.ContainsKey("Enrage"),
                            (int) Spell.GetSpellCooldown("Colossus Smash", -1).TotalMilliseconds,
                            (int) Spell.GetSpellCooldown("Mortal Strike", -1).TotalMilliseconds
                            );

                        WoWUnit target = Me.CurrentTarget;
                        if (target != null)
                        {
                            log += string.Format(", th={0:F1}%, dist={1:F1}, inmelee={2}, face={3}, loss={4}, dead={5} secs, flying={6}",
                                target.HealthPercent,
                                target.Distance,
                                target.IsWithinMeleeRange.ToYN(),
                                Me.IsSafelyFacing(target).ToYN(),
                                target.InLineOfSpellSight.ToYN(),
                                target.TimeToDeath(),
                                target.IsFlying.ToYN()
                                );
                        }

                        Logger.WriteDebug(Color.AntiqueWhite, log);
                        return RunStatus.Failure;
                    })
                    )
                );
        }

        #endregion
    }
}