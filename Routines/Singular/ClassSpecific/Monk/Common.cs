using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using Singular.Settings;
using Singular.Managers;
using Styx.Common.Helpers;

namespace Singular.ClassSpecific.Monk
{

    public enum SphereType
    {
        Chi = 3145,     // created by After Life
        Life = 3319,    // created by After Life
        Healing = 2866  // created by Healing Sphere spell
    }

    public class Common
    {
        private static MonkSettings MonkSettings { get { return SingularSettings.Instance.Monk(); } }
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        public static bool HasTalent(MonkTalents tal) { return TalentManager.IsSelected((int)tal); }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Monk, (WoWSpec)int.MaxValue, WoWContext.All, 1)]
        public static Composite CreateMonkPreCombatBuffs()
        {
            return new PrioritySelector(
                // behaviors handling group buffing... handles special moments like
                // .. during the buff spam parade during battleground preparation, etc.
                // .. check our own buffs in PullBuffs and CombatBuffs if needed
                PartyBuff.BuffGroup("Legacy of the White Tiger"),
                PartyBuff.BuffGroup("Legacy of the Emperor")
                );
        }

        [Behavior(BehaviorType.LossOfControl, WoWClass.Monk, (WoWSpec) int.MaxValue, WoWContext.Normal | WoWContext.Battlegrounds )]
        public static Composite CreateMonkLossOfControlBehavior()
        {
            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(
                    Spell.BuffSelf("Dematerialize"),
                    Spell.BuffSelf("Nimble Brew", ret => Me.Stunned || Me.Fleeing || Me.HasAuraWithMechanic( WoWSpellMechanic.Horrified )),
                    Spell.BuffSelf("Dampen Harm", ret => Me.Stunned && Unit.NearbyUnitsInCombatWithMeOrMyStuff.Any()),
                    Spell.BuffSelf("Tiger's Lust", ret => Me.Rooted && !Me.HasAuraWithEffect( WoWApplyAuraType.ModIncreaseSpeed)),
                    Spell.BuffSelf("Life Cocoon", req => Me.Stunned && TalentManager.HasGlyph("Life Cocoon") && Unit.NearbyUnitsInCombatWithMeOrMyStuff.Any())
                    )
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Monk, (WoWSpec)int.MaxValue, WoWContext.All, 2)]
        public static Composite CreateMonkCombatBuffs()
        {
            UnitSelectionDelegate onunitRop;

            if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                onunitRop = on => Unit.UnfriendlyUnits(8).Any(u => u.CurrentTargetGuid == Me.Guid && u.IsPlayer) ? Me : Group.Healers.FirstOrDefault( h => h.SpellDistance() < 40 && Unit.UnfriendlyUnits((int) h.Distance2D + 8).Any( u => u.CurrentTargetGuid == h.Guid && u.SpellDistance(h) < 8));
            else // Instances and Normal - just protect self
                onunitRop = on => Unit.UnfriendlyUnits(8).Count(u => u.CurrentTargetGuid == Me.Guid) > 1 ? Me : null;

            return new PrioritySelector(
                
                Spell.BuffSelf( "Legacy of the White Tiger"),
                Spell.BuffSelf( "Legacy of the Emperor"),

                new Decorator(
                    req => !Unit.IsTrivial(Me.CurrentTarget),
                    new PrioritySelector(               
                        // check our individual buffs here
                        Spell.Buff("Disable", ret => Me.GotTarget && Me.CurrentTarget.IsPlayer && Me.CurrentTarget.ToPlayer().IsHostile && !Me.CurrentTarget.HasAuraWithEffect(WoWApplyAuraType.ModDecreaseSpeed)),
                        Spell.Buff("Ring of Peace", onunitRop)
                        )
                    ),

                CreateChiBurstBehavior()
                );
        }

        [Behavior(BehaviorType.Rest, WoWClass.Monk, WoWSpec.MonkBrewmaster)]
        [Behavior(BehaviorType.Rest, WoWClass.Monk, WoWSpec.MonkWindwalker)]
        public static Composite CreateMonkRest()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => !StyxWoW.Me.HasAura("Drink") && !StyxWoW.Me.HasAura("Food"),
                    new PrioritySelector(
                        // pickup free heals from Life Spheres
                        new Decorator(
                            ret => Me.HealthPercent < 95 && AnySpheres(SphereType.Life, SingularSettings.Instance.SphereDistanceAtRest ),
                            CreateMoveToSphereBehavior(SphereType.Life, SingularSettings.Instance.SphereDistanceAtRest)
                            ),
                        // pickup free chi from Chi Spheres
                        new Decorator(
                            ret => Me.CurrentChi < Me.MaxChi && AnySpheres(SphereType.Chi, SingularSettings.Instance.SphereDistanceAtRest),
                            CreateMoveToSphereBehavior(SphereType.Chi, SingularSettings.Instance.SphereDistanceAtRest)
                            ),

                        // heal ourselves... confirm we have spell and enough energy already or waiting for energy regen will
                        // .. still be faster than eating
                        new Decorator(
                            ret => Me.HealthPercent >= MonkSettings.RestHealingSphereHealth
                                && SpellManager.HasSpell("Healing Sphere") 
                                && (Me.CurrentEnergy > 40 || Spell.EnergyRegenInactive() >= 10),
                            new Sequence(
                                // in Rest only, wait up to 4 seconds for Energy Regen and Spell Cooldownb 
                                new Wait(4, ret => Me.Combat || (Me.CurrentEnergy >= 40 && Spell.GetSpellCooldown("Healing Sphere") == TimeSpan.Zero), new ActionAlwaysSucceed()),
                                Common.CreateHealingSphereBehavior(Math.Max(80, SingularSettings.Instance.MinHealth)),
                                Helpers.Common.CreateWaitForLagDuration(ret => Me.Combat)
                                )
                            )
                        )
                    ),

                // Rest up damnit! Do this first, so we make sure we're fully rested.
                Rest.CreateDefaultRestBehaviour( null, "Resuscitate")
                );
        }
        
        /// <summary>
        /// a SpellManager.CanCast replacement to allow checking whether a spell can be cast 
        /// without checking if another is in progress, since Monks need to cast during
        /// a channeled cast already in progress
        /// </summary>
        /// <param name="name">name of the spell to cast</param>
        /// <param name="unit">unit spell is targeted at</param>
        /// <returns></returns>
        public static bool CanCastLikeMonk(string name, WoWUnit unit)
        {
            WoWSpell spell;
            if (!SpellManager.Spells.TryGetValue(name, out spell))
            {
                return false;
            }

            uint latency = SingularRoutine.Latency * 2;
            TimeSpan cooldownLeft = spell.CooldownTimeLeft;
            if (cooldownLeft != TimeSpan.Zero && cooldownLeft.TotalMilliseconds >= latency)
                return false;

            if (spell.IsMeleeSpell)
            {
                if (!unit.IsWithinMeleeRange)
                {
                    Logger.WriteDebug("CanCastSpell: cannot cast wowSpell {0} @ {1:F1} yds", spell.Name, unit.Distance);
                    return false;
                }
            }
            else if (spell.IsSelfOnlySpell)
            {
                ;
            }
            else if (spell.HasRange)
            {
                if (unit == null)
                {
                    return false;
                }

                if (unit.Distance < spell.MinRange)
                {
                    Logger.WriteDebug("SpellCast: cannot cast wowSpell {0} @ {1:F1} yds - minimum range is {2:F1}", spell.Name, unit.Distance, spell.MinRange);
                    return false;
                }

                if (unit.Distance >= spell.MaxRange)
                {
                    Logger.WriteDebug("SpellCast: cannot cast wowSpell {0} @ {1:F1} yds - maximum range is {2:F1}", spell.Name, unit.Distance, spell.MaxRange);
                    return false;
                }
            }

            if (Me.CurrentPower < spell.PowerCost)
            {
                Logger.WriteDebug("CanCastSpell: wowSpell {0} requires {1} power but only {2} available", spell.Name, spell.PowerCost, Me.CurrentMana);
                return false;
            }

            if (Me.IsMoving && spell.CastTime > 0)
            {
                Logger.WriteDebug("CanCastSpell: wowSpell {0} is not instant ({1} ms cast time) and we are moving", spell.Name, spell.CastTime);
                return false;
            }

            return true;
        }


        /// <summary>
        ///   Creates a behavior to cast a spell by name, with special requirements, on a specific unit. Returns
        ///   RunStatus.Success if successful, RunStatus.Failure otherwise.
        /// </summary>
        /// <remarks>
        ///   Created 5/2/2011.
        /// </remarks>
        /// <param name = "name">The name.</param>
        /// <param name="checkMovement"></param>
        /// <param name = "onUnit">The on unit.</param>
        /// <param name = "requirements">The requirements.</param>
        /// <returns>.</returns>
        public static Composite CastLikeMonk(string name, UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements)
        {
            return new PrioritySelector(
                new Decorator(ret => requirements != null && onUnit != null && requirements(ret) && onUnit(ret) != null && name != null && CanCastLikeMonk(name, onUnit(ret)),
                    new PrioritySelector(
                        new Sequence(
                            // cast the spell
                            new Action(ret =>
                            {
                                wasMonkSpellQueued = (Spell.GcdActive || Me.IsCasting || Me.ChanneledSpell != null);
                                Logger.Write(Color.Aquamarine, string.Format("*{0} on {1} at {2:F1} yds at {3:F1}%", name, onUnit(ret).SafeName(), onUnit(ret).Distance, onUnit(ret).HealthPercent));
                                SpellManager.Cast(name, onUnit(ret));
                            }),
                            // if spell was in progress before cast (we queued this one) then wait in progress one to finish
                            new WaitContinue( 
                                new TimeSpan(0, 0, 0, 0, (int) SingularRoutine.Latency << 1),
                                ret => !wasMonkSpellQueued || !(Spell.GcdActive || Me.IsCasting || Me.ChanneledSpell != null),
                                new ActionAlwaysSucceed()
                                ),
                            // wait for this cast to appear on the GCD or Spell Casting indicators
                            new WaitContinue(
                                new TimeSpan(0, 0, 0, 0, (int) SingularRoutine.Latency << 1),
                                ret => Spell.GcdActive || Me.IsCasting || Me.ChanneledSpell != null,
                                new ActionAlwaysSucceed()
                                )
                            )
                        )
                    )
                );
        }
         
        private static bool wasMonkSpellQueued = false;

        // delay casting instant ranged abilities if we just cast Roll/FSK
        public readonly static WaitTimer RollTimer = new WaitTimer(TimeSpan.FromMilliseconds(1500));


        public static Composite CreateMonkCloseDistanceBehavior( SimpleIntDelegate minDist = null, UnitSelectionDelegate onUnit = null, SimpleBooleanDelegate canReq = null)
        {
            /*
            new Decorator(
                unit => (unit as WoWUnit).SpellDistance() > 10
                    && Me.IsSafelyFacing(unit as WoWUnit, 5f),
                Spell.Cast("Roll")
                )
            */

            if (!MovementManager.IsClassMovementAllowed)
                return new ActionAlwaysFail();

            bool hasFSKGlpyh = TalentManager.HasGlyph("Flying Serpent Kick");
            bool hasTigersLust = HasTalent(MonkTalents.TigersLust);

            if (minDist == null)
                minDist = min => Me.Combat ? 10 : 12;

            if (onUnit == null)
                onUnit = on => Me.CurrentTarget;

            if (canReq == null)
                canReq = req => true;

            return new Throttle( 1,
                new PrioritySelector(
                    ctx => onUnit(ctx),
                    new Decorator(
                        req => {
                            if (!canReq(req))
                                return false;

                            float dist = Me.SpellDistance(req as WoWUnit);
                            if ( dist <= minDist(req))
                                return false;

                            if ((req as WoWUnit).IsAboveTheGround())
                                return false;

                            float facingPrecision = (req as WoWUnit).SpellDistance() < 15 ? 6f : 4f;
                            if (!Me.IsSafelyFacing(req as WoWUnit, facingPrecision))
                                return false;

                            bool isObstructed = Movement.MeshTraceline(Me.Location, (req as WoWUnit).Location);
                            if (isObstructed == true)
                                return false;

                            return true;
                            },
                        new PrioritySelector(
                            Spell.BuffSelf(
                                "Tiger's Lust", 
                                req => hasTigersLust 
                                    && !Me.HasAuraWithEffect(WoWApplyAuraType.ModIncreaseSpeed)
                                    && Me.HasAuraWithEffect(WoWApplyAuraType.ModRoot, WoWApplyAuraType.ModDecreaseSpeed)
                                ),

                            new Sequence( 
                                Spell.Cast(
                                    "Flying Serpent Kick", 
                                    on => (WoWUnit) on,
                                    ret => TalentManager.CurrentSpec == WoWSpec.MonkWindwalker
                                        && !Me.Auras.ContainsKey("Flying Serpent Kick")
                                        && ((ret as WoWUnit).SpellDistance() > 25 || Spell.IsSpellOnCooldown("Roll"))
                                    ),
                                /* wait until in progress */
                                new PrioritySelector(
                                    new Wait(
                                        TimeSpan.FromMilliseconds(750),
                                        until => Me.Auras.ContainsKey("Flying Serpent Kick"),
                                        new Action( r => Logger.WriteDebug("CloseDistance: Flying Serpent Kick detected towards {0} @ {1:F1} yds in progress", (r as WoWUnit).SafeName(), (r as WoWUnit).SpellDistance()))
                                        ),
                                    new Action( r => {
                                        Logger.WriteDebug("CloseDistance: failure - did not see Flying Serpent Kick aura appear - lag?");
                                        return RunStatus.Failure;
                                        })
                                    ),

                                /* cancel when in range */
                                new Wait( 
                                    2,
                                    until => {
                                        if (!Me.Auras.ContainsKey("Flying Serpent Kick"))
                                        {
                                            Logger.WriteDebug("CloseDistance: Flying Serpent Kick completed on {0} @ {1:F1} yds and {2} behind me", (until as WoWUnit).SafeName(), (until as WoWUnit).SpellDistance(), (until as WoWUnit).IsBehind(Me) ? "IS" : "is NOT");
                                            return true;
                                        }

                                        if (!hasFSKGlpyh)
                                        {
                                            if (((until as WoWUnit).IsWithinMeleeRange || (until as WoWUnit).SpellDistance() < 8f))
                                            {
                                                Logger.Write(Color.White, "/cancel Flying Serpent Kick in melee range of {0} @ {1:F1} yds", (until as WoWUnit).SafeName(), (until as WoWUnit).SpellDistance());
                                                return true;
                                            }

                                            if ((until as WoWUnit).IsBehind(Me))
                                            {
                                                Logger.Write(Color.White, "/cancel Flying Serpent Kick flew past {0} @ {1:F1} yds", (until as WoWUnit).SafeName(), (until as WoWUnit).SpellDistance());
                                                return true;
                                            }
                                        }

                                        return false;
                                        },
                                    new PrioritySelector(
                                        new Decorator(
                                            req => !Me.Auras.ContainsKey("Flying Serpent Kick"),
                                            new ActionAlwaysSucceed()
                                            ),
                                        new Sequence(
                                            new Action( r => {
                                                if (hasFSKGlpyh)
                                                {
                                                    Logger.WriteDebug("CloseDistance: FSK is glyphed, should not be here - notify developer!");
                                                }
                                                else
                                                {
                                                    Logger.WriteDebug("CloseDistance: casting Flying Serpent Kick to cancel");
                                                    SpellManager.Cast("Flying Serprent Kick");
                                                }
                                            }),
                                            /* wait until cancel takes effect */
                                            new PrioritySelector(
                                                new Wait(
                                                    TimeSpan.FromMilliseconds(450),
                                                    until => !Me.Auras.ContainsKey("Flying Serpent Kick"),
                                                    new Action( r => Logger.WriteDebug("CloseDistance: Flying Serpent Kick aura no longer exists"))
                                                    ),
                                                new Action( r => {
                                                    Logger.WriteDebug("CloseDistance: error - Flying Serpent Kick was not removed - lag?");
                                                    })
                                                )
                                            )
                                        )
                                    )
                                ),

                            Spell.BuffSelf("Tiger's Lust", req => hasTigersLust ),

                            new Sequence(
                                Spell.Cast("Roll", on => (WoWUnit)on, req => !MonkSettings.DisableRoll ),
                                new PrioritySelector(
                                    new Wait(
                                        TimeSpan.FromMilliseconds(500), 
                                        until => Me.Auras.ContainsKey("Roll"),
                                        new Action(r => Logger.WriteDebug("CloseDistance: Roll in progress"))
                                        ),
                                    new Action( r => {
                                        Logger.WriteDebug("CloseDistance: failure - did not detect Roll in progress aura- lag?");
                                        return RunStatus.Failure;
                                        })
                                    ),
                                new Wait(
                                    TimeSpan.FromMilliseconds(950), 
                                    until => !Me.Auras.ContainsKey("Roll"),
                                    new Action(r => Logger.WriteDebug("CloseDistance: Roll has ended"))
                                    )
                                )
                            )
                        )
                    )
                );
        }

        public static WoWObject FindClosestSphere(SphereType typ, float range)
        {
            range *= range;
            return ObjectManager.ObjectList
                .Where(o => o.Type == WoWObjectType.AiGroup && o.Entry == (uint)typ && o.DistanceSqr < range && !Blacklist.Contains(o.Guid, BlacklistFlags.Combat))
                .OrderBy( o => o.DistanceSqr )
                .FirstOrDefault();
        }

        public static bool AnySpheres(SphereType typ, float range)
        {
            WoWObject sphere = FindClosestSphere(typ, range);
            return sphere != null && sphere.Distance < 20;
        }

        public static WoWPoint FindSphereLocation(SphereType typ, float range)
        {
            WoWObject sphere = FindClosestSphere(typ, range);
            return sphere != null ? sphere.Location : WoWPoint.Empty;
        }

        private static ulong guidSphere = 0;
        private static WoWPoint locSphere = WoWPoint.Empty;
        private static DateTime timeAbortSphere = DateTime.Now;

        public static Composite CreateMoveToSphereBehavior(SphereType typ, float range)
        {
            return new Decorator(
                ret => SingularSettings.Instance.MoveToSpheres && !MovementManager.IsMovementDisabled,

                new PrioritySelector(

                    // check we haven't gotten out of range due to fall / pushback / port / etc
                    new Decorator( 
                        ret => guidSphere != 0 && Me.Location.Distance(locSphere) > range,
                        new Action(ret => { guidSphere = 0; locSphere = WoWPoint.Empty; })
                        ),

                    // validate the sphere we are moving to
                    new Action(ret =>
                    {
                        WoWObject sph = FindClosestSphere(typ, range);
                        if (sph == null)
                        {
                            guidSphere = 0; locSphere = WoWPoint.Empty;
                            return RunStatus.Failure;
                        }

                        if (sph.Guid == guidSphere)
                            return RunStatus.Failure;

                        guidSphere = sph.Guid;
                        locSphere = sph.Location;
                        timeAbortSphere = DateTime.Now + TimeSpan.FromSeconds(5);
                        Logger.WriteDebug("MoveToSphere: Moving {0:F1} yds to {1} Sphere {2} @ {3}", sph.Distance, typ, guidSphere, locSphere);
                        return RunStatus.Failure;
                    }),

                    new Decorator( 
                        ret => DateTime.Now > timeAbortSphere, 
                        new Action( ret => {
                            Logger.WriteDebug("MoveToSphere: blacklisting timed out {0} sphere {1} at {2}", typ, guidSphere, locSphere);
                            Blacklist.Add(guidSphere, BlacklistFlags.Combat, TimeSpan.FromMinutes(5));
                            })
                        ),

                    // move to the sphere if out of range
                    new Decorator(
                        ret => guidSphere != 0 && Me.Location.Distance(locSphere) > 1,
                        Movement.CreateMoveToLocationBehavior(ret => locSphere, true, ret => 0f)
                        ),

                    // pause briefly until its consumed
                    new Wait( 
                        1, 
                        ret => {  
                            WoWObject sph = FindClosestSphere(typ, range);
                            return sph == null || sph.Guid != guidSphere ;
                            },
                        new Action( r => { return RunStatus.Failure; } )
                        ),
                        
                    // still exist?  black list it then
                    new Decorator( 
                        ret => {  
                            WoWObject sph = FindClosestSphere(typ, range);
                            return sph != null && sph.Guid == guidSphere ;
                            },
                        new Action( ret => {
                            Logger.WriteDebug("MoveToSphere: blacklisting unconsumed {0} sphere {1} at {2}", typ, guidSphere, locSphere);
                            Blacklist.Add(guidSphere, BlacklistFlags.Combat, TimeSpan.FromMinutes(5));
                            })
                        )
                    )
                );
        }

        public static Sequence CreateHealingSphereBehavior( int sphereBelowHealth)
        {
            // healing sphere keeps spell on cursor for up to 3 casts... need to stop targeting after 1
            return new Sequence(
                Spell.CastOnGround("Healing Sphere",
                    on => Me,
                    ret => Me.HealthPercent < sphereBelowHealth 
                        && (Me.PowerType != WoWPowerType.Mana)
                        && !Common.AnySpheres(SphereType.Healing, 1f),
                    false),
                new WaitContinue( TimeSpan.FromMilliseconds(500), ret => Spell.GetPendingCursorSpell != null, new ActionAlwaysSucceed()),
                new Action(ret => Lua.DoString("SpellStopTargeting()")),
                new WaitContinue( 
                    TimeSpan.FromMilliseconds(750), 
                    ret => Me.Combat || (Spell.GetSpellCooldown("Healing Sphere") == TimeSpan.Zero && !Common.AnySpheres(SphereType.Healing, 1f)), 
                    new ActionAlwaysSucceed()
                    )
                );
        }

        /// <summary>
        /// cast grapple weapon, dealing with issues of mobs immune to that spell
        /// </summary>
        /// <returns></returns>
        public static Composite CreateGrappleWeaponBehavior()
        {
            if (!MonkSettings.UseGrappleWeapon)
                return new ActionAlwaysFail();

            return new Throttle(15,
                new Sequence(
                    ctx => {
                        WoWUnit target;
                        if (Spell.IsSpellOnCooldown("Grapple Weapon"))
                            target = null;
                        else
                            target = Unit.NearbyUnitsInCombatWithMeOrMyStuff
                                        .FirstOrDefault(u => IsGrappleWeaponCandidate(u) && u.SpellDistance() < 40 && Me.IsSafelyFacing(u, 150));
                        return new CtxGrapple( target);
                    },

                    Spell.Cast("Grapple Weapon", on => (on as CtxGrapple).target),

                    // if cast successful, make sure we return success to Throttle even if Wait for weapon fails
                    new PrioritySelector(
                        new Sequence(
                            new Wait( TimeSpan.FromMilliseconds(350), until => Me.Inventory.Equipped.MainHand != (until as CtxGrapple).mainhand, new ActionAlwaysSucceed()),
                            new Action( mh => Logger.Write(Color.White, "/grappleweapon: equipped [{0}] #{1}", Me.Inventory.Equipped.MainHand.Name, Me.Inventory.Equipped.MainHand.Entry ))
                            ),
                        new PrioritySelector(
                            ctx => Me.GetAllAuras().FirstOrDefault(a => a.SpellId == 123231 || a.SpellId == 123232 || a.SpellId == 123234),
                            new Decorator(
                                req => req != null,
                                new Action( r => Logger.Write( Color.White, "/grappleweapon: received buff [{0}] #{1}", (r as WoWAura).Name, (r as WoWAura).SpellId))
                                )
                            ),
                        new Action(r => Blacklist.Add((r as CtxGrapple).target.IsPlayer ? (r as CtxGrapple).target.Guid : (r as CtxGrapple).target.Entry, BlacklistFlags.Node, TimeSpan.FromDays(7), "Singular: failed Grapple Weapon on this target"))
                        )
                    )
                );
        }

        class CtxGrapple
        {
            public WoWUnit target { get; set; }
            public WoWItem mainhand { get; set; }
            public WoWAura aura { get; set; }

            public CtxGrapple( WoWUnit t)
            {
                mainhand = Me.Inventory.Equipped.MainHand;
                target = t;
            }
        }

        private static bool IsGrappleWeaponCandidate(WoWUnit u)
        {
            if (u.IsPlayer)
            {
                if (Blacklist.Contains(u.Guid, BlacklistFlags.Node))
                {
                    return false;
                }
            }
            else
            {
                if (!u.IsHumanoid || Blacklist.Contains(u.Entry, BlacklistFlags.Node))
                {
                    return false;
                }
            }

            if (u.CurrentTarget.Disarmed || u.CurrentTarget.IsCrowdControlled())
                return false;

            return true;
        }

        public static Composite CreateChiBurstBehavior()
        {
            if ( !HasTalent(MonkTalents.ChiBurst))
                return new ActionAlwaysFail();

            if (TalentManager.CurrentSpec == WoWSpec.MonkMistweaver && SingularRoutine.CurrentWoWContext != WoWContext.Normal)
            {
                return new Decorator(
                    req => !Spell.IsSpellOnCooldown("Chi Burst") && !Me.CurrentTarget.IsBoss()
                        && 3 <= Clusters.GetPathToPointCluster(Me.Location.RayCast(Me.RenderFacing, 40f), HealerManager.Instance.TargetList.Where(m => Me.IsSafelyFacing(m, 25)), 5f).Count(),
                    Spell.Cast("Chi Burst",
                        mov => true,
                        ctx => Me,
                        ret => Me.HealthPercent < MonkSettings.ChiWavePct,
                        cancel => false
                        )
                    );
            }

            return new Decorator(
                req => !Spell.IsSpellOnCooldown("Chi Burst") && !Me.CurrentTarget.IsBoss() 
                    && 3 <= Clusters.GetPathToPointCluster( Me.Location.RayCast(Me.RenderFacing, 40f), Unit.NearbyUnfriendlyUnits.Where( m => Me.IsSafelyFacing(m,25)), 5f).Count(),
                Spell.Cast("Chi Burst",
                    mov => true,
                    ctx => Me,
                    ret => Me.HealthPercent < MonkSettings.ChiWavePct,
                    cancel => false
                    )
                );
            }   
    }

    public enum MonkTalents
    {
        Celerity = 1,
        TigersLust,
        Momumentum,
        ChiWave,
        ZenSphere,
        ChiBurst,
        PowerStrikes,
        Ascension,
        ChiBrew,
        RingOfPeace,
        ChargingOxWave,
        LegSweep,
        HealingElixirs,
        DampenHarm,
        DiffuseMagic,
        RushingJadeWind,
        InvokeXuenTheWhiteTiger,
        ChiTorpedo
    }

}