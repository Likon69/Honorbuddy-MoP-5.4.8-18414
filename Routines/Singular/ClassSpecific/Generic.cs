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
using System;
using CommonBehaviors.Actions;


namespace Singular.ClassSpecific
{
    public static class Generic
    {
        // [Behavior(BehaviorType.Combat, priority: 999)]
        public static Composite CreateUseTrinketsBehaviour()
        {
            // Saving Settings via GUI will now force reinitialize so we can build the behaviors
            // basead upon the settings rather than continually checking the settings in the Btree
            // 
            // 

            if (SingularSettings.Instance.Trinket1Usage == TrinketUsage.Never
                && SingularSettings.Instance.Trinket2Usage == TrinketUsage.Never
                && SingularSettings.Instance.GloveUsage == TrinketUsage.Never )
            {
                return new Action(ret => { return RunStatus.Failure; });
            }

            PrioritySelector ps = new PrioritySelector();

            if (SingularSettings.IsTrinketUsageWanted(TrinketUsage.OnCooldown))
            {
                ps.AddChild(Item.UseEquippedTrinket(TrinketUsage.OnCooldown));
            }

            if (SingularSettings.IsTrinketUsageWanted(TrinketUsage.OnCooldownInCombat))
            {
                ps.AddChild( 
                    new Decorator( 
                        ret => {
                            if ( !StyxWoW.Me.Combat || !StyxWoW.Me.GotTarget)
                                return false;
                            bool isMelee = StyxWoW.Me.IsMelee();
                            if (isMelee)
                                return StyxWoW.Me.CurrentTarget.IsWithinMeleeRange;
                            return !StyxWoW.Me.IsMoving && StyxWoW.Me.CurrentTarget.SpellDistance() < 40;
                            }, 
                        Item.UseEquippedTrinket(TrinketUsage.OnCooldownInCombat)
                        )
                    );
            }

            if (SingularSettings.IsTrinketUsageWanted(TrinketUsage.LowHealth))
            {
                ps.AddChild( new Decorator( ret => StyxWoW.Me.HealthPercent < SingularSettings.Instance.PotionHealth,
                                            Item.UseEquippedTrinket( TrinketUsage.LowHealth)));
            }

            if (SingularSettings.IsTrinketUsageWanted(TrinketUsage.LowPower))
            {
                ps.AddChild( new Decorator( ret => StyxWoW.Me.PowerPercent < SingularSettings.Instance.PotionMana,
                                            Item.UseEquippedTrinket(TrinketUsage.LowPower)));
            }

            if (SingularSettings.IsTrinketUsageWanted(TrinketUsage.CrowdControlled ))
            {
                ps.AddChild( new Decorator( ret => Unit.IsCrowdControlled( StyxWoW.Me),
                                            Item.UseEquippedTrinket( TrinketUsage.CrowdControlled )));
            }

            if (SingularSettings.IsTrinketUsageWanted(TrinketUsage.CrowdControlledSilenced ))
            {
                ps.AddChild( new Decorator( ret => StyxWoW.Me.Silenced && Unit.IsCrowdControlled( StyxWoW.Me),
                                            Item.UseEquippedTrinket(TrinketUsage.CrowdControlledSilenced)));
            }

            return ps;
        }

        // [Behavior(BehaviorType.Combat, priority: 998)]
        public static Composite CreateRacialBehaviour()
        {
            PrioritySelector pri = new PrioritySelector();

            if (SpellManager.HasSpell("Stoneform"))
            {
                pri.AddChild(                         
                    new Decorator(
                        ret => {
                            if ( !Spell.CanCastHack("Stoneform") )
                                return false;
                            if ( StyxWoW.Me.GetAllAuras().Any(a => a.Spell.Mechanic == WoWSpellMechanic.Bleeding || a.Spell.DispelType == WoWDispelType.Disease || a.Spell.DispelType == WoWDispelType.Poison))
                                return true;
                            if (Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() > 2)
                                return true;
                            if (StyxWoW.Me.GotTarget && StyxWoW.Me.CurrentTarget.CurrentTargetGuid == StyxWoW.Me.Guid && StyxWoW.Me.CurrentTarget.MaxHealth > (StyxWoW.Me.MaxHealth * 2))
                                return true;
                            return false;
                            },
                        Spell.OffGCD( Spell.BuffSelf("Stoneform"))
                        )
                    );
            }

            if (SpellManager.HasSpell("Escape Artist"))
            {
                pri.AddChild(
                    Spell.OffGCD( Spell.BuffSelf("Escape Artist", req => Unit.HasAuraWithMechanic(StyxWoW.Me, WoWSpellMechanic.Rooted, WoWSpellMechanic.Snared)))
                    );
            }

            if (SpellManager.HasSpell("Gift of the Naaru"))
            {
                pri.AddChild(
                    Spell.OffGCD( Spell.BuffSelf("Gift of the Naaru", req => StyxWoW.Me.HealthPercent < SingularSettings.Instance.GiftNaaruHP))
                    );
            }

            if (SpellManager.HasSpell("Shadowmeld"))
            {
                pri.AddChild(
                    Spell.OffGCD( Spell.BuffSelf("Shadowmeld", ret => NeedShadowmeld()) )
                    );
            }

            // add racials cast within range during Combat
            Composite combatRacials = CreateCombatRacialInRangeBehavior();
            if (combatRacials != null)
                pri.AddChild( combatRacials);

            // just fail if no combat racials
            if ( !SingularSettings.Instance.UseRacials || !pri.Children.Any() )
                return new ActionAlwaysFail();

            return new Throttle(
                TimeSpan.FromMilliseconds(250), 
                new Decorator( req => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(), pri )
                );
        }

        private static Composite CreateCombatRacialInRangeBehavior()
        {
            PrioritySelector priCombat = new PrioritySelector();

            // not a racial, but best place to handle it
            if (SpellManager.HasSpell("Lifeblood"))
            {
                priCombat.AddChild(
                    Spell.OffGCD(Spell.BuffSelf("Lifeblood", ret => !PartyBuff.WeHaveBloodlust))
                    );
            }

            if (SpellManager.HasSpell("Berserking"))
            {
                priCombat.AddChild(
                    Spell.OffGCD(Spell.BuffSelf("Berserking", ret => !PartyBuff.WeHaveBloodlust))
                    );
            }

            if (SpellManager.HasSpell("Blood Fury"))
            {
                priCombat.AddChild(
                    Spell.OffGCD(Spell.BuffSelf("Blood Fury", ret => true))
                    );
            }

            if (priCombat.Children.Any())
            {
                return new Decorator(
                    req =>
                    {
                        if (!StyxWoW.Me.Combat || !StyxWoW.Me.GotTarget)
                            return false;
                        if (StyxWoW.Me.IsMelee())
                            return StyxWoW.Me.CurrentTarget.IsWithinMeleeRange;
                        return !StyxWoW.Me.IsMoving && StyxWoW.Me.CurrentTarget.SpellDistance() < 40;
                    },
                    priCombat
                    );
            }

            return null;
        }

        private static bool NeedShadowmeld()
        {
            if ( !SingularSettings.Instance.ShadowmeldThreatDrop || StyxWoW.Me.Race != WoWRace.NightElf )
                return false;

            if ( !Spell.CanCastHack("Shadowmeld") )
                return false;

            if (StyxWoW.Me.IsInGroup())
            {
                if (Group.MeIsTank)
                    return false;

                if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                {
                    if (!Unit.NearbyUnfriendlyUnits.Any(unit => unit.CurrentTargetGuid == StyxWoW.Me.Guid && (unit.Class == WoWClass.Hunter || unit.Class == WoWClass.Mage || unit.Class == WoWClass.Priest || unit.Class == WoWClass.Warlock)))
                    {
                        return true;    // since likely a ranged target
                    }
                }
                else if (!Unit.NearbyUnfriendlyUnits.Any(unit => unit.CurrentTargetGuid == StyxWoW.Me.Guid))
                {
                    return false;
                }

                if (Group.AnyTankNearby)
                    return true;
            }

            // need to add logic to wait for pats, or for PVP losing ranged targets may be enough
            return false;
        }

        // [Behavior(BehaviorType.Combat, priority: 997)]
        public static Composite CreatePotionAndHealthstoneBehavior()
        {
            return Item.CreateUsePotionAndHealthstone(SingularSettings.Instance.PotionHealth, SingularSettings.Instance.PotionMana);
        }
    }


    public static class NoContextAvailable
    {
        public static Composite CreateDoNothingBehavior()
        {
            return new Throttle( 15,
                new Action( r => Logger.Write( "No Context Available - do nothing while we wait"))
                );
        }
    }
}
