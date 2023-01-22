#region

using System;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using System.Collections.Generic;
using CommonBehaviors.Actions;
using Styx.WoWInternals;
using System.Drawing;
using Styx.CommonBot.POI;
using Styx.Helpers;

#endregion

namespace Singular.ClassSpecific.Druid
{
    public class Feral
    {
        public delegate IEnumerable<WoWUnit> EnumWoWUnitDelegate(object context);

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static DruidSettings DruidSettings { get { return SingularSettings.Instance.Druid(); } }

        #region Common

        [Behavior(BehaviorType.Rest, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.All, 1)]
        public static Composite CreateFeralDruidRest()
        {
            return new PrioritySelector(
                new Throttle(10,
                    new Decorator(
                        ret => SpellManager.HasSpell("Savage Roar")
                            && Me.RawComboPoints > 0
                            && Me.ComboPointsTarget != 0
                            && null != ObjectManager.GetObjectByGuid<WoWUnit>(Me.ComboPointsTarget)
                            && Me.GetAuraTimeLeft("Savage Roar", true).TotalSeconds < (Me.RawComboPoints * 6 + 6),
                        new Sequence(
                            new Action(r => Logger.WriteDebug("cast Savage Roar to use {0} points on corpse of {1} since buff has {2} seconds left", Me.RawComboPoints, ObjectManager.GetObjectByGuid<WoWUnit>(Me.ComboPointsTarget).SafeName(), Me.GetAuraTimeLeft("Savage Roar", true).TotalSeconds)),
                            CastSavageRoar( on => ObjectManager.GetObjectByGuid<WoWUnit>(Me.ComboPointsTarget), req => true)
                            )
                        )
                    ),

                Common.CreateProwlBehavior(ret => Me.HasAura("Drink") || Me.HasAura("Food")),

                new Decorator(
                    ret => !Me.HasAura("Drink") && !Me.HasAura("Food")
                        && Me.HasAura("Predatory Swiftness")
                        && (Me.PredictedHealthPercent(includeMyHeals: true) < 95),
                    new PrioritySelector(
                        new Action(r => { Logger.WriteDebug("Druid Rest Swift Heal @ {0:F1}% and moving:{1} in form:{2}", Me.HealthPercent, Me.IsMoving, Me.Shapeshift); return RunStatus.Failure; }),
                        Spell.Cast("Healing Touch",
                            mov => true,
                            on => Me,
                            req => true,
                            cancel => Me.HealthPercent > 95 )
                        )
                    )

                // remainder of rest behavior in common.cs CreateNonRestoDruidRest()                     
                );
        }


        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.Battlegrounds, 2)]
        public static Composite CreateFeralPreCombatBuffForSymbiosisBattlegrounds( )
        {
            return Common.CreateDruidCastSymbiosis(on => GetFeralBestSymbiosisTargetForBattlegrounds());
        }

        private static WoWPlayer GetFeralBestSymbiosisTargetForBattlegrounds()
        {
            return (Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Priest)
                ?? (Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Paladin)
                ?? Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Shaman)
                ?? (Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Warrior)
                ?? Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.DeathKnight))));
        }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.Instances, 2)]
        public static Composite CreateFeralPreCombatBuffForSymbiosisInstances()
        {
            return Common.CreateDruidCastSymbiosis(on => GetFeralBestSymbiosisTargetForInstances());
        }

        private static WoWPlayer GetFeralBestSymbiosisTargetForInstances()
        {
            return (Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Specialization == WoWSpec.PaladinHoly)
                ?? (Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Specialization == WoWSpec.PaladinProtection)
                ?? (Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Specialization == WoWSpec.WarriorProtection)
                ?? (Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Monk)
                ?? (Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Priest)
                ?? Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Shaman)
                ?? (Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Warrior)
                ?? Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.DeathKnight)))))));
        }

        [Behavior(BehaviorType.Pull, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.All)]
        public static Composite CreateFeralNormalPull()
        {
            return new PrioritySelector(
                CreateFeralDiagnosticOutputBehavior( "Pull" ),
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Helpers.Common.CreateAutoAttack(false),

                Spell.WaitForCast(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(), 
                    new PrioritySelector(

                        //Shoot flying targets
                        new Decorator(
                            ret => Me.CurrentTarget.IsAboveTheGround(),
                            new PrioritySelector(
                                new Action(r =>
                                {
                                    Logger.WriteDebug("Target appears airborne: flying={0} aboveground={1}",
                                        Me.CurrentTarget.IsFlying.ToYN(),
                                        Me.CurrentTarget.IsAboveTheGround().ToYN()
                                        );
                                    return RunStatus.Failure;
                                }),
                                Common.CreateFaerieFireBehavior(on => Me.CurrentTarget, req => Me.CurrentTarget.Distance < 35),
                                Spell.Cast("Moonfire", ret => Me.CurrentTarget.Distance < 40),
                                Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 27f, 22f)
                                )
                            ),

                        Spell.BuffSelf("Cat Form", req => !Utilities.EventHandlers.IsShapeshiftSuppressed),

                        Common.CreateProwlBehavior(),

                        CreateFeralWildChargeBehavior(),

                        // only Dash if we dont have WC or WC was cast more than 2 seconds ago

                        new Decorator(
                            ret => Me.HasAura("Prowl"),
                            new PrioritySelector(
                                Spell.BuffSelf("Dash", 
                                    ret => MovementManager.IsClassMovementAllowed && Me.IsMoving
                                        && ((Me.CurrentTarget.Distance > 15 && Spell.GetSpellCooldown("Wild Charge", 999).TotalSeconds > 3)
                                            || Spell.GetSpellCooldown("Wild Charge", 999).TotalSeconds > 40)
                                    ),
                                Spell.Cast("Ravage", ret => Me.IsSafelyBehind(Me.CurrentTarget) && SingularRoutine.CurrentWoWContext == WoWContext.Instances ),
                                Spell.Cast("Pounce")
                                )
                            ),
                        Spell.Buff("Rake"),
                        Spell.Cast("Mangle")
                        )
                    ),

                // Move to Melee, going behind target if prowling 
                Common.CreateMoveBehindTargetWhileProwling(),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        private static Throttle CreateFeralWildChargeBehavior()
        {
            // save WC for later if Dash is active. also throttle to deal with possible pathing issues
            return new Throttle(7,
                new Sequence(
                    Spell.CastHack("Wild Charge", ret => MovementManager.IsClassMovementAllowed && !Me.HasAura("Dash") && (Me.CurrentTarget.Distance + Me.CurrentTarget.CombatReach).Between(10, 25)),
                    new Wait(1, until => !Me.GotTarget || Me.CurrentTarget.IsWithinMeleeRange, new ActionAlwaysSucceed())
                    )
                );
        }

        public static Composite CastSavageRoar(UnitSelectionDelegate on, SimpleBooleanDelegate required)
        {
            int spellId = TalentManager.HasGlyph("Savagery") ? 127538 : 52610;
            return Spell.Cast( spellId, on, required);
        }

        public static Composite CastThrash( UnitSelectionDelegate on, SimpleBooleanDelegate required, int seconds = 2)
        {
            return Spell.Cast( "Thrash", on, req => required(req) && on(req).HasAuraExpired("Thrash",seconds, true));
            // return Spell.Buff("Thrash", true, on, required, 2);
            //  return Spell.Buff(106832, on => Me.CurrentTarget, req => Me.HasAura("Omen of Clarity") && Me.CurrentTarget.HasAuraExpired("Thrash", 3)),
        }

        #endregion

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.All)]
        public static Composite CreateFeralNormalPreCombatBuffs()
        {
            return new Decorator(
                ret => !Spell.IsCastingOrChannelling() && !Spell.IsGlobalCooldown(), 
                new PrioritySelector(

                    // cast cat form 
                    // since this check comes while not in combat (so will be doing other things like Questing) need to add some checks:
                    // - only if Moving
                    // - only if Not Swimming
                    // - only if Not Flying
                    // - only if Not in one of the various forms for travel 
                    // - only if No Recent Shapefhift Error (since form may have resulted from error in picking up Quest, completing Quest objectives, or turning in Quest)
                    new Throttle(
                        10, 
                        Spell.BuffSelf( 
                            "Cat Form", 
                            req => !Utilities.EventHandlers.IsShapeshiftSuppressed
                                && Me.IsMoving
                                && !Me.IsFlying && !Me.IsSwimming 
                                && !Me.HasAnyShapeshift( ShapeshiftForm.Travel, ShapeshiftForm.Aqua, ShapeshiftForm.FlightForm, ShapeshiftForm.EpicFlightForm)
                            )
                        ),

                    Common.CreateProwlBehavior(
                        ret => DruidSettings.ProwlAlways
                            && BotPoi.Current.Type != PoiType.Loot
                            && BotPoi.Current.Type != PoiType.Skin
                            && !ObjectManager.GetObjectsOfType<WoWUnit>().Any(u => u.IsDead && ((CharacterSettings.Instance.LootMobs && u.CanLoot && u.Lootable) || (CharacterSettings.Instance.SkinMobs && u.Skinnable && u.CanSkin)) && u.Distance < CharacterSettings.Instance.LootRadius)
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.All, 999)]
        public static Composite CreateFeralCombatHeal()
        {
            return new PrioritySelector(
                new Action(ret => { _currTargetTimeToDeath = Me.CurrentTarget.TimeToDeath(); return RunStatus.Failure; }),
                CreateFeralDiagnosticOutputBehavior("Combat"),
                Common.SymbBuff( Symbiosis.DivineShield, on => Me, req => Me.HealthPercent < 20)
                );
        }


        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.All, 1)]
        public static Composite CreateFeralCombatBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Cat Form", req => !Utilities.EventHandlers.IsShapeshiftSuppressed)
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.Normal | WoWContext.Battlegrounds)]
        public static Composite CreateFeralNormalCombat()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Helpers.Common.CreateAutoAttack(false),

                Spell.WaitForCast(FaceDuring.Yes),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(), 
                    new PrioritySelector(

                        // updated time to death tracking values before we need them
                        new Action( ret => { Me.CurrentTarget.TimeToDeath(); return RunStatus.Failure; } ),

                        Helpers.Common.CreateInterruptBehavior(),

                        CreateFeralAoeCombat(),

#region Symbiosis
                        new Decorator(
                            ret => Me.HasAura( "Symbiosis") && !Me.HasAura("Prowl"),
                            new PrioritySelector(
                                Common.SymbCast(Symbiosis.FeralSpirit, on => Me.CurrentTarget, ret => SingularRoutine.CurrentWoWContext != WoWContext.Instances || Me.CurrentTarget.IsBoss() || Unit.NearbyUnfriendlyUnits.Count( u => u.IsTargetingMeOrPet ) >= 2),
                                Common.SymbCast(Symbiosis.ShatteringBlow, on => Me.CurrentTarget, ret => Me.CurrentTarget.IsPlayer && Me.HasAnyAura("Ice Block", "Hand of Protection", "Divine Shield")),
                                Common.SymbCast(Symbiosis.DeathCoil, on => Me.CurrentTarget, ret => !Me.CurrentTarget.IsWithinMeleeRange),
                                Common.SymbCast(Symbiosis.Clash, on => Me.CurrentTarget, ret => !Me.CurrentTarget.IsWithinMeleeRange)
                                )
                            ),
#endregion

                        //Single target
                        Common.CreateFaerieFireBehavior( on => Me.CurrentTarget, req => !Me.CurrentTarget.HasAura("Weakened Armor", 3)),

                        ///
                        /// Savage Roar - original spell id = 52610, override is 127538.  both spells valid but there is not an obvious need for the
                        /// override.  Additionally, CanCast AWLAYS fails for 127538 meaning CanCast("Spell Manager") always fails.  workaround
                        /// is to cast by id
                        ///
                        CastSavageRoar( on => Me.CurrentTarget, req => !Me.HasAura("Savage Roar") && (Me.ComboPoints > 1 || TalentManager.HasGlyph("Savagery"))),

                        new Throttle( Spell.BuffSelf("Tiger's Fury", 
                                   ret => Me.EnergyPercent <= 35 
                                       && !Me.ActiveAuras.ContainsKey("Clearcasting")
                                       && !Me.HasAura("Berserk")
                                       )),

                        new Throttle( 
                            Spell.BuffSelf("Berserk", 
                                ret => Me.HasAura("Tiger's Fury") 
                                    && Me.GotTarget && (Me.CurrentTarget.IsBoss() || Me.CurrentTarget.IsPlayer || (SingularRoutine.CurrentWoWContext != WoWContext.Instances && Me.CurrentTarget.TimeToDeath() >= 20 ))
                                )
                            ),

                        new Throttle( Spell.Cast("Nature's Vigil", ret => Me.HasAura("Berserk"))),
                        Spell.Cast("Incarnation", ret => Me.HasAura("Berserk")),

                        // bite if rip on for awhile or target dying soon
                        Spell.Cast("Ferocious Bite", 
                            ret => (Me.ComboPoints >= 5 && !Me.CurrentTarget.HasAuraExpired("Rip", 6))
                                || Me.ComboPoints >= Me.CurrentTarget.TimeToDeath(99)),

                        Spell.Cast("Rip",
                            ret => Me.ComboPoints >= 5
                                && Me.CurrentTarget.TimeToDeath() >= 7
                                && Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds < 1),

                        Spell.Cast("Ravage"),

                        Spell.Buff("Rake", ret => Me.CurrentTarget.GetAuraTimeLeft("Rake", true).TotalSeconds < 3),

                        Spell.Cast("Shred", 
                            ret =>  (Me.CurrentTarget.MeIsSafelyBehind || (TalentManager.HasGlyph("Shred") && (Me.HasAnyAura("Tiger's Fury", "Berserk"))))),

                        Spell.Cast("Mangle"),

                        Spell.OffGCD(Spell.Cast("Force of Nature", req => TalentManager.CurrentSpec != WoWSpec.DruidRestoration && Me.CurrentTarget.TimeToDeath() > 8)),

                        new Decorator(
                            ret => MovementManager.IsClassMovementAllowed && Me.IsMoving && Me.CurrentTarget.Distance > (Me.CurrentTarget.IsPlayer ? 10 : 15),
                            new PrioritySelector(
                                CreateFeralWildChargeBehavior(),
                                Spell.BuffSelf("Dash", ret => Spell.GetSpellCooldown("Wild Charge", 0).TotalSeconds < 13 )
                                )
                            )

                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        static long _currTargetTimeToDeath { get; set; }

        [Behavior(BehaviorType.Combat, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.Instances )]
        public static Composite CreateFeralCombatInstances()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Helpers.Common.CreateAutoAttack(false),

                Spell.WaitForCast(FaceDuring.Yes),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        // updated time to death tracking values before we need them
                        Helpers.Common.CreateInterruptBehavior(),

                        CreateFeralAoeCombat(),

            #region Symbiosis
                        new Decorator(
                            ret => Me.HasAura("Symbiosis") && !Me.HasAura("Prowl"),
                            new PrioritySelector(
                                Common.SymbCast(Symbiosis.FeralSpirit, on => Me.CurrentTarget, ret => SingularRoutine.CurrentWoWContext != WoWContext.Instances || Me.CurrentTarget.IsBoss() || Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) >= 2),
                                Common.SymbCast(Symbiosis.ShatteringBlow, on => Me.CurrentTarget, ret => Me.CurrentTarget.IsPlayer && Me.HasAnyAura("Ice Block", "Hand of Protection", "Divine Shield")),
                                Common.SymbCast(Symbiosis.DeathCoil, on => Me.CurrentTarget, ret => !Me.CurrentTarget.IsWithinMeleeRange),
                                Common.SymbCast(Symbiosis.Clash, on => Me.CurrentTarget, ret => !Me.CurrentTarget.IsWithinMeleeRange)
                                )
                            ),
            #endregion

                        // 1. Keep Faerie Fire up (if no other armor debuff).
                        Common.CreateFaerieFireBehavior(on => Me.CurrentTarget, req => !Me.CurrentTarget.HasAura("Weakened Armor", 3)),

                        new Decorator(
                            ret => Me.GotTarget
                                && Me.SpellDistance(Me.CurrentTarget) < 8,

                            new PrioritySelector(
                                // 2. Keep Savage Roar up
                                CastSavageRoar( on => Me.CurrentTarget, req => Me.HasAuraExpired("Savage Roar", 2) && (Me.ComboPoints > 0 || TalentManager.HasGlyph("Savagery"))),

#if INITIAL
                                new Throttle(
                                    new Decorator(
                                        req => Me.HasAura("Savage Roar") && Me.GotTarget && Me.CurrentTarget.IsWithinMeleeRange,
                                        new Sequence(
                                            Spell.BuffSelf("Tiger's Fury", req => !Me.HasAura("Berserk") && (!Spell.IsSpellOnCooldown("Berserk") || Spell.GetSpellCooldown("Berserk").TotalSeconds > 15)),
                                            new Action( r => Logger.WriteDebug("Burst(Instance): Tiger Fury cast, so check if burst needed")),
                                            // if TF (its off GCD) succeeded, do all Burst checks now
                                            new DecoratorContinue( 
                                                req => !Me.CurrentTarget.IsBoss(),
                                                new Action( r => Logger.WriteDebug("Burst(Instance): not a boss so skipping"))
                                                ),
                                            new DecoratorContinue(
                                                req => Me.CurrentTarget.IsBoss(),
                                                new PrioritySelector(
                                                    new Action(r => { Logger.WriteDebug("Burst(Instance): IsBoss, so trying burst buffs"); return RunStatus.Failure; }),
                                                    new PrioritySelector(
                                                        ctx => (int) Spell.GetSpellCooldown("Berserk").TotalSeconds,
                                                        new Action( r=> { Logger.WriteDebug("Burst(Instance): Berserk has {0} secs on cooldown remaining", (int) r); return RunStatus.Failure; }),
                                                        Spell.OffGCD(Spell.BuffSelf("Nature's Vigil", req => ((int) req) > 85))
                                                        ),
                                                    new Decorator(
                                                        req => Spell.CanCastHack("Berserk", Me, skipWowCheck: true),
                                                        new PrioritySelector(
                                                            new Action(r => { Logger.WriteDebug("Burst(Instance): Berserk available, so trying max burst buffs"); return RunStatus.Failure; }),
                                                            Spell.OffGCD(Spell.BuffSelf("Berserk") ),
                                                            Spell.OffGCD(Spell.BuffSelf("Incarnation: King of the Jungle")),
                                                            Spell.OffGCD(Spell.BuffSelf("Nature's Vigil")),
                                                            Spell.OffGCD(Spell.Cast("Force of Nature"))
                                                            )
                                                        ),
                                                    // succeed if we are here regardless to finish Sequence
                                                    new Action( r => Logger.WriteDebug("Burst(Instance): end of burst buffs"))
                                                    )
                                                ),

                                            // fail this Sequence if it hasn't already since all casts are Off GCD
                                            new ActionAlwaysFail()
                                            )
                                        )
                                    ),
#else
                                Spell.OffGCD(
                                    new Decorator(
                                        req => Me.HasAura("Savage Roar") && Me.GotTarget && Me.CurrentTarget.IsWithinMeleeRange,
                                        new Sequence(
                                            // Tigers Fury if no Berserk aura active and a cooldown isn't about to become available
                                            Spell.BuffSelf( "Tiger's Fury", 
                                                req => !Me.HasAura("Berserk") 
                                                    && !Spell.GetSpellCooldown("Berserk").TotalSeconds.Between(double.Epsilon, 5)
                                                    && !Spell.GetSpellCooldown("Nature's Vigil").TotalSeconds.Between(double.Epsilon, 5)),
                                            // following only if Tiger's Fury cast successful
                                            new PrioritySelector(
                                                Spell.OffGCD(Spell.BuffSelf("Nature's Vigil", req => Spell.GetSpellCooldown("Berserk").TotalSeconds > 30)),
                                                new Sequence(
                                                    Spell.BuffSelf("Berserk", req => !Spell.GetSpellCooldown("Nature's Vigil").TotalSeconds.Between(double.Epsilon, 30)),
                                                    new PrioritySelector(
                                                        Spell.OffGCD(Spell.BuffSelf("Incarnation: King of the Jungle")),
                                                        Spell.OffGCD(Spell.BuffSelf("Nature's Vigil")),
                                                        Spell.OffGCD(Spell.Cast("Force of Nature"))
                                                        )
                                                    )
                                                )
                                            )
                                        )
                                        // fall through since off gcd
                                    ),

#endif

                                // 4. Use Nature’s Swiftness/Healing touch to generate Wrath of Cenarius procs when GCD will not cause energy cap*
                                // 5. Use Predatory Swiftness to generate Dream of Cenarius procs when GCD will not cause energy cap, preferably at 4CP.**
                                new Decorator(
                                    ret => Me.EnergyPercent < 85 
                                        && Common.HasTalent( DruidTalents.DreamOfCenarius) 
                                        && !Me.HasAura("Wrath of Cenarius")
                                        && Me.ComboPoints >= 5,
                                    new Sequence(
                                        new PrioritySelector(
                                            new Decorator(  ret => Me.HasAura("Predatory Swiftness"), new ActionAlwaysSucceed()),
                                            Spell.BuffSelf( "Nature's Swiftness")
                                            ),
                                        Spell.Cast( "Healing Touch", on => Me )
                                        )
                                    ),

                                new Decorator(
                                    ret => DruidSettings.FeralSpellPriority != Singular.Settings.DruidSettings.SpellPriority.Noxxic,
                                    new PrioritySelector(
                                        // 5b. made a higher priority to prioritize consuming Omen of Clarity with Thrash if needed
                                        CastThrash( on => Me.CurrentTarget, req => Me.HasAura("Clearcasting")),

                                        // 6. Ferocious Bite if the boss has less than 25% hp remaining and Rip is near expiring.
                                        Spell.Cast("Ferocious Bite", req => Me.CurrentTarget.HealthPercent < 25 && Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalMilliseconds > 250),

                                        // 8. Keep 5 combo point Rip up.
                                        Spell.Buff("Rip", true, on => Me.CurrentTarget, req => Me.ComboPoints >= 5, 3),

                                        // 7. Ferocious Bite if you have 5 CP and at least 6 - 10 seconds on Savage Roar and Rip
                                        Spell.Cast("Ferocious Bite", 
                                            req => Me.ComboPoints >= 5
                                                && !Me.CurrentTarget.HasAuraExpired("Rip", 6)
                                                && !Me.HasAuraExpired("Savage Roar", TalentManager.HasGlyph("Savagery") ? 1 : 6)
                                                ),

                                        // 9. Keep Rake up
                                        Spell.Buff("Rake", true, on => Me.CurrentTarget, req => true, 3),

                                        // 10. Spend Omen of Clarity procs on Thrash if Thrash has less than 6 seconds remaining.
                                        CastThrash(on => Me.CurrentTarget, req => Me.HasAura("Clearcasting"), 6),

                                        // 11. Ravage to generate combo points if Ravage is available (Incarnation)
                                        Spell.Cast("Ravage", req => Me.ComboPoints < 5 && (Me.IsSafelyBehind(Me.CurrentTarget) || Me.HasAnyAura(102543, 81022))),

                                        // 12. Shred to generate combo points if Shred is available (Behind boss, berserk w/glyph, etc)
                                        Spell.Cast("Shred", req => Me.ComboPoints < 5 && (Me.IsSafelyBehind(Me.CurrentTarget) || (TalentManager.HasGlyph("Shred") && Me.HasAnyAura("Tiger's Fury", "Berserk")))),

                                        // 13. Use Mangle to generate combo points.
                                        Spell.Cast("Mangle", req => Me.ComboPoints < 5 ),

                                        // 14. Maintain Thrash bleed if it will not interfere with Rake, Rip, or SR uptimes.
                                        CastThrash( on => Me.CurrentTarget, 
                                            req =>  Me.GetAuraTimeLeft("Savage Roar").TotalSeconds >= 6
                                                && Me.CurrentTarget.GetAuraTimeLeft("Rake").TotalSeconds >= 6
                                                && Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalSeconds >= 6)
                                        )
                                    ),

                                new Decorator(
                                    ret => DruidSettings.FeralSpellPriority == Singular.Settings.DruidSettings.SpellPriority.Noxxic,
                                    new PrioritySelector(

                                        // spend Combo Points
                                        new Decorator(
                                            req => Me.ComboPoints >= 5,
                                            new PrioritySelector(
                                                Spell.Cast("Ferocious Bite", req => Me.CurrentTarget.HealthPercent < 25 && Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalMilliseconds > 250),
                                                Spell.Buff("Rip", true, on => Me.CurrentTarget, req => true, 6),
                                                Spell.Cast("Ferocious Bite")
                                                )
                                            ),

                                        // build Combo Points
                                        new Decorator(
                                            req => Me.ComboPoints < 5,
                                            new PrioritySelector(
                                                // note:  id used to fix Thrash Spell Override bug (similar to Savage Roar)
                                                CastThrash( on => Me.CurrentTarget, req => Me.HasAura("Clearcasting")),

                                                Spell.Buff("Rake", true, on => Me.CurrentTarget, req => true, 3),

                                                // following if 3 - 4 targets
                                                CastThrash( on => Me.CurrentTarget, req => _aoeCount >= 3, 2),
                                                Spell.Cast("Swipe", req => _aoeCount >= 3),

                                                // following only if 2 or less targets
                                                Spell.Cast("Ravage", req => (Me.IsSafelyBehind(Me.CurrentTarget) || Me.HasAnyAura(102543, 81022))),
                                                Spell.Cast("Shred", req => (Me.IsSafelyBehind(Me.CurrentTarget) || (TalentManager.HasGlyph("Shred") && Me.HasAnyAura("Tiger's Fury", "Berserk")))),

                                                // otherwise mangle mangle mangle
                                                Spell.Cast("Mangle")
                                                )
                                            )
                                        )
                                    )
                                )
                            ),

                        new Decorator(
                            ret => MovementManager.IsClassMovementAllowed && Me.IsMoving && Me.CurrentTarget.Distance > (Me.CurrentTarget.IsPlayer ? 10 : 15),
                            new PrioritySelector(
                                CreateFeralWildChargeBehavior(),
                                Spell.BuffSelf("Dash", ret => Spell.GetSpellCooldown("Wild Charge", 0).TotalSeconds < 13)
                                )
                            )

                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        private static int _aoeCount;
        private static IEnumerable<WoWUnit> _aoeColl;

        private static Composite CreateFeralAoeCombat()
        {
            // disable AOE for PVP
            if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                return new ActionAlwaysFail();

            return new PrioritySelector(

                new Action( ret => {
                    BuildAoeCollection();
                    return RunStatus.Failure;
                    }),

                new Decorator(ret => Spell.UseAOE && _aoeCount >= 5 && Me.Level >= 22,

                    new PrioritySelector(

                        CastSavageRoar(on => Me.CurrentTarget, ret => !Me.HasAura("Savage Roar") && (Me.ComboPoints > 1 || TalentManager.HasGlyph("Savagery"))),

                        CastThrash( on => Me.CurrentTarget, ret => _aoeColl.Any(m => !m.HasMyAura("Thrash")) || Me.HasAura("Clearcasting"), 0),

                        Spell.BuffSelf("Tiger's Fury",
                            ret => Me.EnergyPercent <= 35
                                && !Me.ActiveAuras.ContainsKey("Clearcasting")
                                && !Me.HasAura("Berserk")),

                        Spell.BuffSelf("Berserk", ret => Me.HasAura("Tiger's Fury") && SingularRoutine.CurrentWoWContext != WoWContext.Instances),

                        // bite if rip good for awhile or target dying soon
                        Spell.Cast("Ferocious Bite",
                            ret => (Me.ComboPoints >= 5 && !Me.CurrentTarget.HasAuraExpired("Rip", 6))
                                || Me.ComboPoints >= _currTargetTimeToDeath),

                        Spell.Cast("Rip",
                            ret => Me.ComboPoints >= 5
                                && _currTargetTimeToDeath >= 8
                                && Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds < 1),

                        Spell.Cast("Swipe", ret => Me.CurrentTarget.IsWithinMeleeRange),

                        Movement.CreateMoveToMeleeBehavior(true)
                        )
                    ),

                // otherwise, try and keep Rake up on mobs allowing some AoE dmg without breaking CC
                Spell.Cast( "Rake", 
                    ret => _aoeColl.FirstOrDefault( 
                        m => m.Guid != Me.CurrentTargetGuid 
                            && m.IsWithinMeleeRange 
                            && !m.HasMyAura("Rake") 
                            && Me.IsSafelyFacing(m) 
                            && !m.IsCrowdControlled()))
                );
        }


        private static void BuildAoeCollection()
        {
            if (!Spell.UseAOE)
                _aoeColl = new List<WoWUnit>(){ Me.CurrentTarget };
            else
            {
                _aoeColl = Unit.UnfriendlyUnits(8);
                if (_aoeColl.Any(m => m.IsCrowdControlled()))
                {
                    _aoeColl = new List<WoWUnit>() { Me.CurrentTarget };
                }
            }

            _aoeCount = _aoeColl.Count();
        }

        #region Diagnostics

        private static Composite CreateFeralDiagnosticOutputBehavior(string context = null)
        {
            if (context == null)
                context = "...";
            else
                context = "<<" + context + ">>";

            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            return new ThrottlePasses(1,
                new Action(ret =>
                {
                    string log;
                    log = string.Format(context + " h={0:F1}%/e={1:F1}%/m={2:F1}%, shape={3}, prowl={4}, savage={5}, tfury={6}, brsrk={7}, predswf={8}, omen={9}, pts={10}",
                        Me.HealthPercent,
                        Me.EnergyPercent,
                        Me.ManaPercent,
                        Me.Shapeshift.ToString().Length < 4 ? Me.Shapeshift.ToString() : Me.Shapeshift.ToString().Substring(0, 4),
                        Me.HasAura("Prowl").ToYN(),
                        (long)Me.GetAuraTimeLeft("Savage Roar", true).TotalMilliseconds,
                        (long)Me.GetAuraTimeLeft("Tiger's Fury", true).TotalMilliseconds,
                        (long)Me.GetAuraTimeLeft("Berserk", true).TotalMilliseconds,
                        (long)Me.GetAuraTimeLeft("Predatory Swiftness", true).TotalMilliseconds,
                        (long)Me.GetAuraTimeLeft("Clearcasting", true).TotalMilliseconds,
                        Me.ComboPoints 
                        );

                    WoWUnit target = Me.CurrentTarget;
                    if (target != null)
                    {
                        log += string.Format(", th={0:F1}%, dist={1:F1}, inmelee={2}, face={3}, loss={4}, dead={5} secs, rake={6}, rip={7}, thrash={8}",
                            target.HealthPercent,
                            target.Distance,
                            target.IsWithinMeleeRange.ToYN(),
                            Me.IsSafelyFacing(target).ToYN(),
                            target.InLineOfSpellSight.ToYN(),
                            target.TimeToDeath(),
                            (long)target.GetAuraTimeLeft("Rake", true).TotalMilliseconds,
                            (long)target.GetAuraTimeLeft("Rip", true).TotalMilliseconds,
                            (long)target.GetAuraTimeLeft("Thrash", true).TotalMilliseconds
                            );

                        if (SingularRoutine.CurrentWoWContext == WoWContext.Instances)
                        {
                            log += string.Format(", refsvg={0}, refrip={1}",
                                Me.HasAuraExpired("Savage Roar", TalentManager.HasGlyph("Savagery") ? 1 : 6).ToYN(),
                                target.HasAuraExpired("Rip", 6).ToYN()
                                );
                        }
                    }

                    Logger.WriteDebug(Color.AntiqueWhite, log);
                    return RunStatus.Failure;
                })
                );
        }

        #endregion
    }
}