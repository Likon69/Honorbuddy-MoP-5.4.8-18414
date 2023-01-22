using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using System;
using System.Linq;
using Action = Styx.TreeSharp.Action;

namespace Singular.ClassSpecific.Paladin
{
    public class Common
    {
        private static PaladinSettings PaladinSettings { get { return SingularSettings.Instance.Paladin(); } }
        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Paladin)]
        public static Composite CreatePaladinPreCombatBuffs()
        {
            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(
                    CreatePaladinBlessBehavior(),
                    CreatePaladinSealBehavior(),
                    new Decorator(
                        ret => TalentManager.CurrentSpec != WoWSpec.PaladinHoly,
                        new PrioritySelector(
                            Spell.BuffSelf("Righteous Fury", ret => TalentManager.CurrentSpec == WoWSpec.PaladinProtection && StyxWoW.Me.GroupInfo.IsInParty)
                            )
                        )
                    )
                );
        }

        [Behavior(BehaviorType.LossOfControl, WoWClass.Paladin)]
        public static Composite CreatePaladinLossOfControlBehavior()
        {
            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(
                    Spell.BuffSelf("Hand of Freedom")
                    )
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Paladin, (WoWSpec)int.MaxValue, WoWContext.Normal, 1)]
        public static Composite CreatePaladinCombatBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Devotion Aura", req => Me.Silenced),
                new Decorator(
                    req => !Unit.IsTrivial(Me.CurrentTarget),
                    new PrioritySelector(
                        Spell.Cast("Repentance", 
                            onUnit => 
                            Unit.NearbyUnfriendlyUnits
                            .Where(u => (u.IsPlayer || u.IsDemon || u.IsHumanoid || u.IsDragon || u.IsGiant || u.IsUndead)
                                    && Me.CurrentTargetGuid != u.Guid
                                    && (u.Aggro || u.PetAggro || (u.Combat && u.IsTargetingMeOrPet))
                                    && !u.IsCrowdControlled()
                                    && u.Distance.Between(10, 30) && Me.IsSafelyFacing(u) && u.InLineOfSpellSight && (!Me.GotTarget || u.Location.Distance(Me.CurrentTarget.Location) > 10))
                            .OrderByDescending(u => u.Distance)
                            .FirstOrDefault()
                            )
                        )
                    )
                );
        }



        /// <summary>
        /// cast Blessing of Kings or Blessing of Might based upon configuration setting.
        /// 
        /// </summary>
        /// <returns></returns>
        private static Composite CreatePaladinBlessBehavior()
        {
            return
                new PrioritySelector(

                        PartyBuff.BuffGroup( 
                            "Blessing of Kings", 
                            ret => PaladinSettings.Blessings == PaladinBlessings.Auto || PaladinSettings.Blessings == PaladinBlessings.Kings,
                            "Blessing of Might"),

                        PartyBuff.BuffGroup(
                            "Blessing of Might",
                            ret => PaladinSettings.Blessings == PaladinBlessings.Auto || PaladinSettings.Blessings == PaladinBlessings.Might, 
                            "Blessing of Kings")
                    );
        }

        /// <summary>
        /// behavior to cast appropriate seal 
        /// </summary>
        /// <returns></returns>
        public static Composite CreatePaladinSealBehavior()
        {
            return new Throttle( TimeSpan.FromMilliseconds(500),
                new Sequence(
                    new Action( ret => _seal = GetBestSeal() ),
                    new Decorator(
                        ret => _seal != PaladinSeal.None
                            && !Me.HasMyAura(SealSpell(_seal))
                            && Spell.CanCastHack(SealSpell(_seal), Me),
                        Spell.Cast( s => SealSpell(_seal), on => Me, ret => !Me.HasAura(SealSpell(_seal)))
                        )
                    )
                );
        }

        static PaladinSeal _seal;

        static string SealSpell( PaladinSeal s)
        { 
            return "Seal of " + s.ToString(); 
        }

        /// <summary>
        /// determines the best PaladinSeal value to use.  Attempts to use 
        /// user setting first, but defaults to something reasonable otherwise
        /// </summary>
        /// <returns>PaladinSeal to use</returns>
        public static PaladinSeal GetBestSeal()
        {
            if (PaladinSettings.Seal == PaladinSeal.None)
                return PaladinSeal.None;

            if (TalentManager.CurrentSpec == WoWSpec.None)
                return SpellManager.HasSpell("Seal of Command") ? PaladinSeal.Command : PaladinSeal.None;

            PaladinSeal bestSeal = Settings.PaladinSeal.Truth;
            if (PaladinSettings.Seal != Settings.PaladinSeal.Auto )
                bestSeal = PaladinSettings.Seal;
            else
            {
                switch (TalentManager.CurrentSpec)
                {
                    case WoWSpec.PaladinHoly:
                        if (Me.IsInGroup())
                            bestSeal = Settings.PaladinSeal.Insight;
                        break;

                    // Seal Twisting.  fixed bug in prior implementation that would cause it
                    // .. to flip seal too quickly.  When we have Insight and go above 5%
                    // .. would cause casting another seal, which would take back below 5% and
                    // .. and recast Insight.  Wait till we build up to 30% if we do this to 
                    // .. avoid wasting mana and gcd's
                    case WoWSpec.PaladinRetribution:
                    case WoWSpec.PaladinProtection:
                        if (Me.ManaPercent < 5 || (Me.ManaPercent < 30 && Me.HasMyAura("Seal of Insight")))
                            bestSeal = Settings.PaladinSeal.Insight;
                        else if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                            bestSeal = Settings.PaladinSeal.Truth;
                        else if (Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) >= 4)
                            bestSeal = Settings.PaladinSeal.Righteousness;
                        break;
                }
            }

            if (!SpellManager.HasSpell(SealSpell(bestSeal)))
                bestSeal = Settings.PaladinSeal.Command;

            if (bestSeal == Settings.PaladinSeal.Command && SpellManager.HasSpell("Seal of Truth"))
                bestSeal = Settings.PaladinSeal.Truth;

            return bestSeal;
        }

        public static Composite CreateWordOfGloryBehavior(UnitSelectionDelegate onUnit )
        {
            if ( HasTalent( PaladinTalents.EternalFlame ))
            {
                if (onUnit == null)
                    return new ActionAlwaysFail();

                return new PrioritySelector(
                    ctx => onUnit(ctx),

                    Spell.Cast(
                        "Eternal Flame",
                        on => (WoWUnit) on,
                        ret => ret is WoWPlayer && PaladinSettings.KeepEternalFlameUp && Group.Tanks.Contains((WoWPlayer)onUnit(ret)) && !Group.Tanks.Any(t => t.HasMyAura("Eternal Flame"))),

                    Spell.Cast(
                        "Eternal Flame",
                        on => (WoWUnit) on,
                        ret => StyxWoW.Me.CurrentHolyPower >= 3 && ((WoWUnit)ret).HealthPercent <= SingularSettings.Instance.Paladin().SelfEternalFlameHealth)

                    );
            }

            return new PrioritySelector(
                new Decorator(
                    req => Me.HealthPercent <= Math.Max( PaladinSettings.SelfWordOfGloryHealth1, Math.Max( PaladinSettings.SelfWordOfGloryHealth2, PaladinSettings.SelfWordOfGloryHealth3))
                        && Me.ActiveAuras.ContainsKey("Divine Purpose"),
                    Spell.Cast("Word of Glory", onUnit)
                    ),
                new Decorator(
                    req => Me.CurrentHolyPower >= 1 && Me.HealthPercent <= PaladinSettings.SelfWordOfGloryHealth1,
                    Spell.Cast("Word of Glory", onUnit)
                    ),
                new Decorator(
                    req => Me.CurrentHolyPower >= 2 && Me.HealthPercent <= PaladinSettings.SelfWordOfGloryHealth2,
                    Spell.Cast("Word of Glory", onUnit)
                    ),
                new Decorator(
                    req => Me.CurrentHolyPower >= 3 && Me.HealthPercent <= PaladinSettings.SelfWordOfGloryHealth3,
                    Spell.Cast("Word of Glory", onUnit)
                    )
                );
        }

        public static Composite CreatePaladinBlindingLightBehavior()
        {
            if (SingularRoutine.CurrentWoWContext == WoWContext.Instances)
                return new ActionAlwaysFail();

            return Spell.Cast(
                sp => "Blinding Light",
                mov => true,
                on => Me.CurrentTarget,
                req =>
                {
                    if (!Spell.UseAOE)
                        return false;
                    if (Unit.UnitsInCombatWithUsOrOurStuff(10).Count(u => u.IsSafelyFacing(Me, 130f)) > 2)
                        return true;
                    if (Me.CurrentTarget.IsPlayer && Me.CurrentTarget.SpellDistance() < 10 && Me.CurrentTarget.IsFacing(Me))
                        return true;
                    return false;
                },
                cancel => Me.CurrentTarget.IsPlayer && (Me.CurrentTarget.SpellDistance() > 10 || !Me.CurrentTarget.IsFacing(Me))
                );


        }

        /// <summary>
        /// invoke on CurrentTarget if not tagged. use ranged instant casts if possible.  this  
        /// is a blend of abilities across all specializations
        /// </summary>
        /// <returns></returns>
        public static Composite CreatePaladinPullMore()
        {
            if (SingularRoutine.CurrentWoWContext != WoWContext.Normal)
                return new ActionAlwaysFail();

            return new Throttle(
                2,
                new Decorator(
                    req => Me.GotTarget
                        && !Me.CurrentTarget.IsPlayer
                        && !Me.CurrentTarget.IsTagged
                        && !Me.CurrentTarget.IsWithinMeleeRange,
                    new PrioritySelector(
                        Spell.Cast("Judgement"),
                        Spell.Cast("Exorcism"),
                        Spell.Cast("Hammer of Wrath"),
                        Spell.Cast("Reckoning"),
                        Spell.Cast("Hammer of Justice"),
                        Spell.Cast("Holy Shock")
                        )
                    )
                );
        }


        public static bool HasTalent(PaladinTalents tal)
        {
            return TalentManager.IsSelected((int)tal);
        }
    }

    public enum PaladinTalents
    {
        SpeedOfLight = 1,
        LongArmOfTheLaw,
        PursuitOfJustice,
        FistOfJustice,
        Repentance,
        EvilIsaPointOfView,
        SelflessHealer,
        EternalFlame,
        SacredShield,
        HandOfPurity,
        UnbreakableSpirit,
        Clemency,
        HolyAvenger,
        SanctifiedWrath,
        DivinePurpose,
        HolyPrism,
        LightsHammer,
        ExecutionSentence
    }
}
