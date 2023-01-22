using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.CommonBot.POI;
using Styx.CommonBot;
using System;
using CommonBehaviors.Actions;

using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using Styx.Helpers;

namespace Singular.ClassSpecific.Priest
{
    public class Common
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static PriestSettings PriestSettings { get { return SingularSettings.Instance.Priest(); } }
        public static bool HasTalent( PriestTalents tal ) { return TalentManager.IsSelected((int)tal); }


        [Behavior(BehaviorType.Heal, WoWClass.Priest, context:WoWContext.Battlegrounds, priority:2)]
        public static Composite CreatePriestHealPreface()
        {
            return new PrioritySelector(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(

            #region Avoidance 

                    new Decorator(
                        ret => Unit.NearbyUnitsInCombatWithMeOrMyStuff.Any(u => u.SpellDistance() < 8)
                            && (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds || (Me.GotTarget && Me.CurrentTarget.IsPlayer) || (SingularRoutine.CurrentWoWContext == WoWContext.Normal && (Me.HealthPercent < 50))),
                        CreatePriestAvoidanceBehavior()
                        )

            #endregion 

                    )
                );
        }

        [Behavior(BehaviorType.PreCombatBuffs,WoWClass.Priest)]
        public static Composite CreatePriestPreCombatBuffs()
        {
            return new PrioritySelector(
                        
                Spell.WaitForCast(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        PartyBuff.BuffGroup("Power Word: Fortitude"),
                        //Spell.BuffSelf("Shadow Protection", ret => PriestSettings.UseShadowProtection && Unit.NearbyFriendlyPlayers.Any(u => !u.Dead && !u.IsGhost && (u.IsInMyPartyOrRaid || u.IsMe) && !Unit.HasAura(u, "Shadow Protection", 0))), // we no longer have Shadow resist
                        Spell.BuffSelf("Inner Fire", ret => PriestSettings.UseInnerFire),
                        Spell.BuffSelf("Inner Will", ret => !PriestSettings.UseInnerFire),
                        Spell.BuffSelf("Fear Ward", ret => PriestSettings.UseFearWard),

                        Spell.BuffSelf("Shadowform"),

                        CreatePriestMovementBuffOnTank("PreCombat")
                        )
                    )
                );
        }

        [Behavior(BehaviorType.LossOfControl, WoWClass.Priest)]
        public static Composite CreatePriestLossOfControlBehavior()
        {
            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(
                    Spell.Cast("Guardian Spirit", on => Me, ret => Me.Stunned && Me.HealthPercent < 20),
                    Spell.Cast("Pain Suppression", on => Me, ret => Me.Stunned),
                    Spell.Cast("Dispersion", on => Me, ret => Me.HealthPercent < 60)
                    )
                );
        }

        private static bool CanCastFortitudeOn(WoWUnit unit)
        {
            //return !unit.HasAura("Blood Pact") &&
            return !unit.HasAura("Power Word: Fortitude") &&
                   !unit.HasAura("Qiraji Fortitude") &&
                   !unit.HasAura("Commanding Shout");
        }

        public static Composite CreatePriestMovementBuff()
        {
            if (!SpellManager.HasSpell("Angelic Feather") && !TalentManager.IsSelected((int)PriestTalents.BodyAndSoul))
                return new ActionAlwaysFail();

            return new Decorator(
                ret => MovementManager.IsClassMovementAllowed
                    && StyxWoW.Me.IsAlive
                    && Me.IsMoving
                    && !StyxWoW.Me.Mounted
                    && !StyxWoW.Me.IsOnTransport
                    && !StyxWoW.Me.OnTaxi
                    && !StyxWoW.Me.HasAnyAura("Angelic Feather", "Body and Soul")
                    && !StyxWoW.Me.IsAboveTheGround(),

                new PrioritySelector(
                    Spell.WaitForCast(),
                    new Throttle(3,
                        new Decorator(
                            ret => !Spell.IsGlobalCooldown(),
                            new PrioritySelector(

                                Spell.BuffSelf("Power Word: Shield",
                                    ret => TalentManager.IsSelected((int)PriestTalents.BodyAndSoul)
                                        && !StyxWoW.Me.HasAnyAura("Body and Soul", "Weakened Soul")),

                                new Decorator(
                                    ret => SpellManager.HasSpell("Angelic Feather")
                                        && !StyxWoW.Me.HasAura("Angelic Feather"),
                                    new Sequence(
                                        Spell.CastOnGround(
                                            "Angelic Feather", 
                                            loc => Me.Location.RayCast(Me.RenderFacing, 1.5f), 
                                            req => true, 
                                            waitForSpell: false, 
                                            tgtDescRtrv:  desc => string.Format("Speed Boost on {0}", Me.SafeName())
                                            ),
                                        Helpers.Common.CreateWaitForLagDuration(orUntil => Spell.GetPendingCursorSpell != null),
                                        new Action(ret => Lua.DoString("SpellStopTargeting()"))
                                        )
                                    )
                                )
                            )
                        )
                    )
                );
        }

        public static WoWUnit GetBestTankTargetForPWS(float health = 100f)
        {
            WoWUnit hotTarget = null;
            string hotName = "Power Word: Shield";
            string hotDebuff = "Weakened Soul";

            hotTarget = Group.Tanks.Where(u => u.IsAlive && u.Combat && u.HealthPercent < health && u.DistanceSqr < 40 * 40 && !u.HasAura(hotName) && !u.HasAura(hotDebuff) && u.InLineOfSpellSight).OrderBy(u => u.HealthPercent).FirstOrDefault();
            if (hotTarget != null)
                Logger.WriteDebug("GetBestTankTargetForPWS('{0}'): found tank {1} @ {2:F1}%", hotName, hotTarget.SafeName(), hotTarget.HealthPercent);

            return hotTarget;
        }

        public static Composite CreatePriestMovementBuffOnTank(string mode, bool checkMoving = true)
        {
            return new PrioritySelector(

                new Throttle( 2,
                    new Decorator(
                        req => PriestSettings.UseSpeedBuffOnTank 
                            && (HasTalent(PriestTalents.BodyAndSoul) || HasTalent(PriestTalents.AngelicFeather))
                            && SingularRoutine.CurrentWoWContext == WoWContext.Instances,
                        new PrioritySelector(
                            ctx => Group.Tanks.FirstOrDefault( t => t.IsAlive && t.IsMoving && !t.Combat && !t.HasAnyAura("Body and Soul", "Angelic Feather") && t.SpellDistance() < 40),
                            Spell.Buff("Power Word: Shield", on => (WoWUnit) on, req => HasTalent(PriestTalents.BodyAndSoul) && !((WoWUnit)req).HasAura("Weakened Soul")),
                            new Sequence(
                                Spell.CastOnGround(
                                    "Angelic Feather", 
                                    loc => (loc as WoWUnit).Location.RayCast((loc as WoWUnit).RenderFacing, 1.5f),
                                    req => req != null, 
                                    waitForSpell: false,
                                    tgtDescRtrv: desc => string.Format("Speed Boost Tank {0}", (desc as WoWUnit).SafeName())
                                    ),
                                Helpers.Common.CreateWaitForLagDuration(orUntil => Spell.GetPendingCursorSpell != null),
                                new Action(ret => Lua.DoString("SpellStopTargeting()"))
                                )
                            )
                        )
                    ),

                new Decorator(
                    ret => MovementManager.IsClassMovementAllowed
                        && PriestSettings.UseSpeedBuff
                        && StyxWoW.Me.IsAlive 
                        && (!checkMoving || StyxWoW.Me.IsMoving)
                        && !StyxWoW.Me.Mounted
                        && !StyxWoW.Me.IsOnTransport
                        && !StyxWoW.Me.OnTaxi
                        && (SpellManager.HasSpell("Angelic Feather") || TalentManager.IsSelected((int) PriestTalents.BodyAndSoul))
                        && !StyxWoW.Me.HasAnyAura("Angelic Feather", "Body and Soul")
                        && (BotPoi.Current == null || BotPoi.Current.Type == PoiType.None || BotPoi.Current.Location.Distance(StyxWoW.Me.Location) > 10)
                        && !StyxWoW.Me.IsAboveTheGround(),

                    new PrioritySelector(
                        Spell.WaitForCast(),
                        new Throttle( 3, 
                            new Decorator(
                                ret => !Spell.IsGlobalCooldown(),
                                new PrioritySelector(

                                    Spell.BuffSelf( "Power Word: Shield", 
                                        ret => TalentManager.IsSelected((int) PriestTalents.BodyAndSoul)
                                            && !StyxWoW.Me.HasAnyAura("Body and Soul", "Weakened Soul")),

                                    new Decorator(
                                        ret => SpellManager.HasSpell("Angelic Feather")
                                            && !StyxWoW.Me.HasAura("Angelic Feather"),
                                        new Sequence(
                                            // new Action( ret => Logger.Write( "Speed Buff for {0}", mode ) ),
                                            Spell.CastOnGround("Angelic Feather",
                                                on => StyxWoW.Me,
                                                ret => true,
                                                false),
                                            Helpers.Common.CreateWaitForLagDuration( orUntil => Spell.GetPendingCursorSpell != null ),
                                            new Action(ret => Lua.DoString("SpellStopTargeting()"))
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    )
                );
        }

        #region Avoidance and Disengage

        /// <summary>
        /// creates a Priest specific avoidance behavior based upon settings.  will check for safe landing
        /// zones before using WildCharge or rocket jump.  will additionally do a running away or jump turn
        /// attack while moving away from attacking mob if behaviors provided
        /// </summary>
        /// <param name="nonfacingAttack">behavior while running away (back to target - instants only)</param>
        /// <param name="jumpturnAttack">behavior while facing target during jump turn (instants only)</param>
        /// <returns></returns>
        public static Composite CreatePriestAvoidanceBehavior()
        {
            // use Rocket Jump if available
            return Avoidance.CreateAvoidanceBehavior("", 0, Disengage.Direction.Frontwards, new ActionAlwaysSucceed());
        }

        public static Composite CreateSlowMeleeBehavior()
        {
            return new PrioritySelector(
                ctx => SafeArea.NearestEnemyMobAttackingMe,
                new Decorator(
                    ret => ret != null,
                    new Throttle(2,
                        new PrioritySelector(
                            Spell.Buff("Void Tendrils", onUnit => (WoWUnit)onUnit, req => true),
                            Spell.Buff("Psychic Horror", onUnit => (WoWUnit)onUnit, req => true),
                            Spell.CastOnGround("Psyfiend",
                                loc => ((WoWUnit)loc).Distance <= 20 ? ((WoWUnit)loc).Location : WoWMovement.CalculatePointFrom(((WoWUnit)loc).Location, (float)((WoWUnit)loc).Distance - 20),
                                req => ((WoWUnit)req) != null,
                                false
                                )
                            )
                        )
                    )
                );
        }

        #endregion

        public static Composite CreatePriestDispelBehavior()
        {
            return new PrioritySelector(

                Spell.Cast( "Mass Dispel", 
                    on => Me, 
                    req =>  Me.Combat
                        && PriestSettings.CountMassDispel > 0
                        && Unit.NearbyGroupMembers.Count(u => u.IsAlive && u.SpellDistance() < 15 && Dispelling.CanDispel(u, DispelCapabilities.All)) >= PriestSettings.CountMassDispel),

                Dispelling.CreateDispelBehavior()
                );
        }

        public static Composite CreateShadowfiendBehavior()
        {
            return Spell.Cast("Shadowfiend",
                ret => Me.ManaPercent < PriestSettings.ShadowfiendMana
                    && Me.GotTarget
                    && Unit.ValidUnit(Me.CurrentTarget)
                    && Me.CurrentTarget.TimeToDeath() > 15
                );
        }

        public static Composite CreateHolyFireBehavior()
        {
            if ( SpellManager.HasSpell( "Power Word: Solace"))
                return Spell.Cast("Power Word: Solace", mov => false, on => Me.CurrentTarget, req => true, cancel => false);

            return Spell.Cast("Holy Fire", mov => false, on => Me.CurrentTarget, req => true);
        }

        internal static Composite CreateFadeBehavior()
        {
            if (!PriestSettings.UseFade)
                return new ActionAlwaysFail();

            if (SingularRoutine.CurrentWoWContext == WoWContext.Instances)
                return Spell.BuffSelf("Fade", req => Targeting.GetAggroOnMeWithin(StyxWoW.Me.Location, 30) > 0);

            return Spell.BuffSelf("Fade", req => {
                if (Targeting.Instance.TargetList.Any(p => p.IsPlayer && p.GotAlivePet && p.Pet.CurrentTargetGuid == Me.Guid))
                    return true;

                if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                    return Common.HasTalent(PriestTalents.Phantasm) && Me.HasAuraWithMechanic(WoWSpellMechanic.Snared);

                return false;
            });
        }

        public static Composite CreateLeapOfFaithBehavior()
        {
            if (PriestSettings.UseLeapOfFaith)
            {
                if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                {
                    return new Decorator(
                        req => !Spell.IsSpellOnCooldown("Leap of Faith"),
                        new PrioritySelector(
                            ctx => Unit.NearbyGroupMembers
                                .FirstOrDefault(u => u.HealthPercent.Between(1, 40) && u.Combat && u.Distance > 15 && Unit.UnfriendlyUnits(45).Any(e => e.CurrentTargetGuid == u.Guid && e.Combat && e.HealthPercent > (u.HealthPercent + 10) && u.IsMelee())),

                            Spell.Cast("Leap of Faith", on => (WoWUnit) on)
                            )
                        );
                }

                if (SingularRoutine.CurrentWoWContext == WoWContext.Instances)
                {
                    return new ThrottlePasses( 1, 1,
                        new Decorator(
                            req => !Spell.IsSpellOnCooldown("Leap of Faith") && !Me.IsStandingInBadStuff(),
                            new PrioritySelector(
                                ctx => Unit.NearbyGroupMembers
                                    .FirstOrDefault(u => u.HealthPercent.Between(1, 40) && u.IsStandingInBadStuff()),

                                Spell.Cast("Leap of Faith", on => (WoWUnit)on)
                                )
                            )
                        );
                }
            }

            return new ActionAlwaysFail();
        }
    }

    public enum PriestTalents
    {
        VoidTendrils = 1,
        Psyfiend,
        DominateMind,
        BodyAndSoul,
        AngelicFeather,
        Phantasm,
        FromDarknessComesLight,
        Mindbender,
        SolaceAndInsanity,
        DesperatePrayer,
        SpectralGuise,
        AngelicBulwark,
        TwistOfFate,
        PowerInfusion,
        DivineInsight,
        Cascade,
        DivineStar,
        Halo
    }

}
