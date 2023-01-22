using System;
using System.Linq;

using Singular.Managers;
using Styx;
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.Helpers;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using Styx.WoWInternals;
using CommonBehaviors.Actions;
using Singular.Settings;
using Singular.ClassSpecific;
using Styx.WoWInternals.WoWObjects;
using Singular.ClassSpecific.Warlock;
using System.Drawing;
using Styx.CommonBot.POI;

namespace Singular.Helpers
{
    internal static class Common
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        /// <summary>
        /// 
        /// </summary>
        public static bool UseLongCoolDownAbility
        {
            get
            {
                if (!Me.GotTarget)
                    return false;

                if (SingularRoutine.CurrentWoWContext == WoWContext.Instances)
                    return Me.CurrentTarget.IsBoss();

                if (Me.CurrentTarget.IsPlayer)
                    return Me.CurrentTarget.TimeToDeath() > 3;

                if (Me.CurrentTarget.TimeToDeath() > 30)
                    return true;

                return Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count(u => u.Guid != Me.CurrentTargetGuid) >= 3;
            }
        }

        /// <summary>
        ///  Creates a behavior to start auto attacking to current target.
        /// </summary>
        /// <remarks>
        ///  Created 23/05/2011
        /// </remarks>
        /// <param name="includePet"> This will also toggle pet auto attack. </param>
        /// <returns></returns>
        public static Composite CreateAutoAttack(bool includePet)
        {
            PrioritySelector prio = new PrioritySelector();
            // const int spellIdAutoShot = 75;
            bool autoAttack = 
                   TalentManager.CurrentSpec == WoWSpec.None
                || Me.Class == WoWClass.DeathKnight
                || TalentManager.CurrentSpec == WoWSpec.DruidGuardian
                || TalentManager.CurrentSpec == WoWSpec.DruidFeral
                || TalentManager.CurrentSpec == WoWSpec.MonkBrewmaster
                || TalentManager.CurrentSpec == WoWSpec.MonkWindwalker
                || TalentManager.CurrentSpec == WoWSpec.PaladinProtection
                || TalentManager.CurrentSpec == WoWSpec.PaladinRetribution
                || Me.Class == WoWClass.Rogue
                || TalentManager.CurrentSpec == WoWSpec.ShamanEnhancement
                || Me.Class == WoWClass.Warrior;

            if (autoAttack)
            {
                prio.AddChild(
                    new Throttle(TimeSpan.FromMilliseconds(500),
                        new Decorator(
                            ret => !StyxWoW.Me.IsAutoAttacking && Me.GotTarget && Me.CurrentTarget.IsWithinMeleeRange, // && StyxWoW.Me.AutoRepeatingSpellId != spellIdAutoShot,
                            new Action(ret =>
                                {
                                    WoWUnit unit = Me.CurrentTarget;
                                    Logger.Write(Color.White, "/startattack on {0} @ {1:F1} yds", unit.SafeName(), unit.SpellDistance());
                                    Lua.DoString("StartAttack()");
                                    return RunStatus.Failure;
                                })
                            )
                        )
                    );
            }

            if ( includePet)
            {
                if (SingularRoutine.CurrentWoWContext != WoWContext.Normal || !SingularSettings.Instance.PetTankAdds)
                {
                    // pet assist: always keep pet on my target
                    prio.AddChild(
                        new ThrottlePasses(
                            1, 
                            TimeSpan.FromMilliseconds(500),
                            RunStatus.Failure,
                            new Decorator(
                        // check pet targeting same target as Me
                                ret => Me.GotAlivePet && (!Me.Pet.GotTarget || Me.Pet.CurrentTargetGuid != Me.CurrentTargetGuid),
                                new Action(delegate
                                    {
                                        PetManager.Attack(Me.CurrentTarget);
                                        return RunStatus.Failure;
                                    })
                                )
                            )
                        );
                }
                else
                {
                    // pet tank: if pet's target isn't targeting Me, check if we should switch to one that is targeting Me
                    prio.AddChild(
                        new ThrottlePasses(
                            1,
                            TimeSpan.FromMilliseconds(500),
                            RunStatus.Failure,
                            new Decorator(
                                ret => Me.GotAlivePet && (!Me.Pet.GotTarget || Me.Pet.CurrentTarget.CurrentTargetGuid != Me.Guid),
                                new PrioritySelector(
                                    ctx => {
                                        WoWUnit aggroedOnMe = 
                                            Unit.NearbyUnfriendlyUnits
                                                .Where(u => u.Combat && u.GotTarget && u.CurrentTarget.IsMe && !u.IsCrowdControlled())
                                                .OrderBy(u => u.Location.DistanceSqr(Me.Pet.Location))
                                                .FirstOrDefault(); 
                                        return aggroedOnMe ?? Me.CurrentTarget;
                                    },
                                    new Decorator(
                                        ret => ret != null && Me.Pet.CurrentTargetGuid != ((WoWUnit)ret).Guid,
                                        new Action(r =>
                                        {
                                            if (SingularSettings.Debug)
                                            {
                                                string reason;
                                                if (Me.CurrentTarget != null && (r as WoWUnit).Guid == Me.CurrentTargetGuid && Me.CurrentTarget.CurrentTargetGuid != Me.Guid)
                                                    reason = "MyCurrTarget";
                                                else
                                                    reason = "PickupAggro";

                                                Logger.WriteDebug("PetManager: [reason={0}] sending Pet at {1} @ {2:F1} yds from Pet", reason, (r as WoWUnit).SafeName(), Me.Pet.SpellDistance(r as WoWUnit));
                                            }

                                            PetManager.Attack(r as WoWUnit);
                                            return RunStatus.Failure;
                                        })
                                        )
                                    )
                                )
                            )
                        );
                }
            }

            return prio;
        }

        /// <summary>
        ///  Creates a behavior to start shooting current target with the wand.
        /// </summary>
        /// <remarks>
        ///  Created 23/05/2011
        /// </remarks>
        /// <returns></returns>
        public static Composite CreateUseWand()
        {
            return CreateUseWand(ret => true);
        }

        /// <summary>
        ///  Creates a behavior to start shooting current target with the wand if extra conditions are met.
        /// </summary>
        /// <param name="extra"> Extra conditions to check to start shooting. </param>
        /// <returns></returns>
        public static Composite CreateUseWand(SimpleBooleanDelegate extra)
        {
#if USE_WANDS
            return new PrioritySelector(
                new Decorator(
                    ret => Item.HasWand && !StyxWoW.Me.IsWanding() && extra(ret),
                    new Action(ret => SpellManager.Cast("Shoot")))
                );
#else
            return new ActionAlwaysFail();
#endif
        }

        private static WoWUnit _unitInterrupt = null;

        /// <summary>Creates an interrupt spell cast composite. This attempts to use spells in order of range (shortest to longest).  
        /// behavior consists only of spells that apply to current toon based upon class, spec, and race
        /// </summary>
        public static Composite CreateInterruptBehavior()
        {
            if ( SingularSettings.Instance.InterruptTarget == CheckTargets.None )
                return new ActionAlwaysFail();

            Composite actionSelectTarget;
            if (SingularSettings.Instance.InterruptTarget == CheckTargets.Current)
                actionSelectTarget = new Action( 
                    ret => {
                        _unitInterrupt = null;
                        if (Me.Class == WoWClass.Shaman && ClassSpecific.Shaman.Totems.Exist(WoWTotem.Grounding))
                            return RunStatus.Failure;

                        WoWUnit u = Me.CurrentTarget;
                        _unitInterrupt = IsInterruptTarget(u) ? u : null;
                        if (_unitInterrupt != null && SingularSettings.Debug)
                            Logger.WriteDebug("Possible Interrupt Target: {0} @ {1:F1} yds casting {2} #{3} for {4} ms", _unitInterrupt.SafeName(), _unitInterrupt.Distance, _unitInterrupt.CastingSpell.Name, _unitInterrupt.CastingSpell.Id, _unitInterrupt.CurrentCastTimeLeft.TotalMilliseconds );

                        return _unitInterrupt == null ? RunStatus.Failure : RunStatus.Success;
                    }
                    );
            else // if ( SingularSettings.Instance.InterruptTarget == InterruptType.All )
            {
                actionSelectTarget = new Action( 
                    ret => {
                        _unitInterrupt = null;
                        if (Me.Class == WoWClass.Shaman && ClassSpecific.Shaman.Totems.Exist(WoWTotem.Grounding))
                            return RunStatus.Failure;

                        _unitInterrupt = Unit.NearbyUnitsInCombatWithMeOrMyStuff.Where(u => IsInterruptTarget(u)).OrderBy(u => u.Distance).FirstOrDefault();
                        if (_unitInterrupt != null && SingularSettings.Debug)
                            Logger.WriteDebug("Possible Interrupt Target: {0} @ {1:F1} yds casting {2} #{3} for {4} ms", _unitInterrupt.SafeName(), _unitInterrupt.Distance, _unitInterrupt.CastingSpell.Name, _unitInterrupt.CastingSpell.Id, _unitInterrupt.CurrentCastTimeLeft.TotalMilliseconds);

                        return _unitInterrupt == null ? RunStatus.Failure : RunStatus.Success;
                        }
                    );
            }

            PrioritySelector prioSpell = new PrioritySelector();

            #region Pet Spells First!

            if (Me.Class == WoWClass.Warlock)
            {
                // this will be either a Optical Blast or Spell Lock
                prioSpell.AddChild( 
                    Spell.Cast( 
                        "Command Demon", 
                        on => _unitInterrupt, 
                        ret => _unitInterrupt != null 
                            && _unitInterrupt.Distance < 40 
                            && Singular.ClassSpecific.Warlock.Common.GetCurrentPet() == WarlockPet.Felhunter 
                        )
                    );
            }

            #endregion

            #region Melee Range

            if ( Me.Class == WoWClass.Paladin )
                prioSpell.AddChild( Spell.Cast("Rebuke", ctx => _unitInterrupt));

            if ( Me.Class == WoWClass.Rogue)
            {
                prioSpell.AddChild( Spell.Cast("Kick", ctx => _unitInterrupt));
                if ( TalentManager.HasGlyph("Gouge"))
                    prioSpell.AddChild(Spell.Cast("Gouge", ctx => _unitInterrupt, ret => !_unitInterrupt.IsBoss() && Me.IsSafelyFacing(_unitInterrupt, 150f)));
                else
                    prioSpell.AddChild(Spell.Cast("Gouge", ctx => _unitInterrupt, ret => !_unitInterrupt.IsBoss() && Me.IsSafelyFacing(_unitInterrupt, 150f) && _unitInterrupt.IsSafelyFacing(Me, 150f)));
            }

            if ( Me.Class == WoWClass.Warrior)
                prioSpell.AddChild( Spell.Cast("Pummel", ctx => _unitInterrupt));

            if ( Me.Class == WoWClass.Monk )
                prioSpell.AddChild( Spell.Cast("Spear Hand Strike", ctx => _unitInterrupt));

            if ( Me.Class == WoWClass.Druid)
            {
                // Spell.Cast("Skull Bash (Cat)", ctx => _unitInterrupt, ret => StyxWoW.Me.Shapeshift == ShapeshiftForm.Cat));
                // Spell.Cast("Skull Bash (Bear)", ctx => _unitInterrupt, ret => StyxWoW.Me.Shapeshift == ShapeshiftForm.Bear));
                prioSpell.AddChild( Spell.Cast("Skull Bash", ctx => _unitInterrupt, ret => StyxWoW.Me.Shapeshift == ShapeshiftForm.Bear || StyxWoW.Me.Shapeshift == ShapeshiftForm.Cat));
                prioSpell.AddChild( Spell.Cast("Mighty Bash", ctx => _unitInterrupt, ret => !_unitInterrupt.IsBoss() && _unitInterrupt.IsWithinMeleeRange));
            }

            if ( Me.Class == WoWClass.DeathKnight)
                prioSpell.AddChild( Spell.Cast("Mind Freeze", ctx => _unitInterrupt));

            if ( Me.Race == WoWRace.Pandaren )
                prioSpell.AddChild( Spell.Cast("Quaking Palm", ctx => _unitInterrupt));

            #endregion

            #region 8 Yard Range

            if ( Me.Race == WoWRace.BloodElf )
                prioSpell.AddChild(Spell.Cast("Arcane Torrent", ctx => _unitInterrupt, req => _unitInterrupt.Distance < 8 && !Unit.NearbyUnfriendlyUnits.Any(u => u.IsSensitiveDamage( 8))));

            if ( Me.Race == WoWRace.Tauren)
                prioSpell.AddChild(Spell.Cast("War Stomp", ctx => _unitInterrupt, ret => _unitInterrupt.Distance < 8 && !_unitInterrupt.IsBoss() && !Unit.NearbyUnfriendlyUnits.Any(u => u.IsSensitiveDamage( 8))));

            #endregion

            #region 10 Yards

            if (Me.Class == WoWClass.Paladin)
                prioSpell.AddChild( Spell.Cast("Hammer of Justice", ctx => _unitInterrupt));

            if (TalentManager.CurrentSpec == WoWSpec.DruidBalance )
                prioSpell.AddChild( Spell.Cast("Hammer of Justice", ctx => _unitInterrupt));

            if (Me.Class == WoWClass.Warrior) 
                prioSpell.AddChild( Spell.Cast("Disrupting Shout", ctx => _unitInterrupt));

            #endregion

            #region 25 yards

            if ( Me.Class == WoWClass.Shaman)
                prioSpell.AddChild( Spell.Cast("Wind Shear", ctx => _unitInterrupt, req => Me.IsSafelyFacing(_unitInterrupt)));

            #endregion

            #region 30 yards
            // Druid
            if (TalentManager.HasGlyph("Fae Silence"))
                prioSpell.AddChild(Spell.Cast("Faerie Fire", ctx => _unitInterrupt, req => Me.Shapeshift == ShapeshiftForm.Bear));

            if (TalentManager.CurrentSpec == WoWSpec.PaladinProtection)
                prioSpell.AddChild( Spell.Cast("Avenger's Shield", ctx => _unitInterrupt));

            if (Me.Class == WoWClass.Warrior && TalentManager.HasGlyph("Gag Order"))
                // Gag Order only works on non-bosses due to it being a silence, not an interrupt!
                prioSpell.AddChild( Spell.Cast("Heroic Throw", ctx => _unitInterrupt, ret =>  !_unitInterrupt.IsBoss()));

            if ( Me.Class == WoWClass.Priest ) 
                prioSpell.AddChild( Spell.Cast("Silence", ctx => _unitInterrupt));

            if (Me.Class == WoWClass.DeathKnight)
                prioSpell.AddChild(Spell.Cast("Strangulate", ctx => _unitInterrupt));

            if (Me.Class == WoWClass.Mage)
                prioSpell.AddChild(Spell.Cast("Frostjaw", ctx => _unitInterrupt));

            #endregion

            #region 40 yards

            if ( Me.Class == WoWClass.Mage)
                prioSpell.AddChild( Spell.Cast("Counterspell", ctx => _unitInterrupt));

            if (Me.Class == WoWClass.Hunter)
                prioSpell.AddChild(Spell.Cast("Counter Shot", ctx => _unitInterrupt));

            if (TalentManager.CurrentSpec == WoWSpec.HunterMarksmanship)
                prioSpell.AddChild(Spell.Cast("Silencing Shot", ctx => _unitInterrupt));

            if (Me.Class == WoWClass.Druid)
                prioSpell.AddChild( Spell.Cast("Solar Beam", ctx => _unitInterrupt, ret => StyxWoW.Me.Shapeshift == ShapeshiftForm.Moonkin));

            if (TalentManager.CurrentSpec == WoWSpec.ShamanElemental || TalentManager.CurrentSpec == WoWSpec.ShamanEnhancement )
                prioSpell.AddChild( Spell.Cast("Solar Beam", ctx => _unitInterrupt, ret => true));

            #endregion

            return new ThrottlePasses( 2, TimeSpan.FromMilliseconds(500),  
                new Sequence(
                    actionSelectTarget,               
                    // majority of these are off GCD, so throttle all to avoid most fail messages
                    prioSpell 
                    )
                );
        }

        private static bool IsInterruptTarget(WoWUnit u)
        {
            if (u == null || !u.IsCasting)
                return false;

            if (!SingularSettings.Debug)
                return u.CanInterruptCurrentSpellCast && u.InLineOfSight;

            if (!u.CanInterruptCurrentSpellCast)
                ;   // Logger.WriteDebug("IsInterruptTarget: {0} casting {1} but CanInterruptCurrentSpellCast == false", u.SafeName(), (u.CastingSpell == null ? "(null)" : u.CastingSpell.Name));
            else if (!u.InLineOfSpellSight)
                ;   // Logger.WriteDebug("IsInterruptTarget: {0} casting {1} but LoSS == false", u.SafeName(), (u.CastingSpell == null ? "(null)" : u.CastingSpell.Name));
            else if (u.CurrentCastTimeLeft.TotalMilliseconds < 250)
                ;
            else
                return true;

            return false;
        }


        /// <summary>
        /// Creates a dismount composite that only stops if we are flying.
        /// </summary>
        /// <param name="reason">The reason to dismount</param>
        /// <returns></returns>
        public static Composite CreateDismount(string reason)
        {
            return new Decorator(
                ret => StyxWoW.Me.Mounted && !MovementManager.IsMovementDisabled,
                new Sequence(
                    new DecoratorContinue(ret => StyxWoW.Me.IsFlying,
                        new Sequence(
                            new DecoratorContinue(ret => StyxWoW.Me.IsMoving,
                                new Sequence(
                                    new Action(ret => Logger.WriteDebug("Stopping to descend..." + (!string.IsNullOrEmpty(reason) ? (" Reason: " + reason) : string.Empty))),
                                    new Action(ret => StopMoving.Now()),
                                    new Wait( 1, ret => !StyxWoW.Me.IsMoving, new ActionAlwaysSucceed())
                                    )
                                ),
                            new Action( ret => Logger.WriteDebug( "Descending to land..." + (!string.IsNullOrEmpty(reason) ? (" Reason: " + reason) : string.Empty))),
                            new Action(ret => WoWMovement.Move(WoWMovement.MovementDirection.Descend)),
                            new PrioritySelector(
                                new Wait( 1, ret => StyxWoW.Me.IsMoving, new ActionAlwaysSucceed()),
                                new Action( ret => Logger.WriteDebug( "warning -- tried to descend but IsMoving == false ....!"))
                                ),
                            new WaitContinue(30, ret => !StyxWoW.Me.IsFlying, new ActionAlwaysSucceed()),
                            new DecoratorContinue( 
                                ret => StyxWoW.Me.IsFlying, 
                                new Action( ret => Logger.WriteDebug( "error -- still flying -- descend appears to have failed....!"))
                                ),
                            new Action(ret => WoWMovement.MoveStop(WoWMovement.MovementDirection.Descend))
                            )
                        ), // and finally dismount. 
                    new Action(r => {
                        Logger.WriteDebug( "Dismounting..." + (!string.IsNullOrEmpty(reason) ? (" Reason: " + reason) : string.Empty));
                        ShapeshiftForm shapeshift = StyxWoW.Me.Shapeshift;
                        if (StyxWoW.Me.Class == WoWClass.Druid && (shapeshift == ShapeshiftForm.FlightForm || shapeshift == ShapeshiftForm.EpicFlightForm))
                            Lua.DoString("RunMacroText('/cancelform')");
                        else
                            Lua.DoString("Dismount()");
                        })
                    )
                );
        }

        /// <summary>
        /// Creates a stop and dismount composite. Matches the prior behavior of old CreateDismount()
        /// </summary>
        /// <param name="reason">The reason to dismount</param>
        /// <returns></returns>
        public static Composite CreateStopAndDismount(string reason)
        {
            return new Decorator( 
                ret => !MovementManager.IsMovementDisabled,
                new PrioritySelector(
                    new Decorator(
                        ret => StyxWoW.Me.IsMoving,
                        Movement.CreateEnsureMovementStoppedBehavior( reason: string.IsNullOrEmpty(reason) ? string.Empty : (" StopDismount Reason: " + reason))
                        ),

                    CreateDismount( reason)
                    )
                );
        }
        /// <summary>
        /// This is meant to replace the 'SleepForLagDuration()' method. Should only be used in a Sequence
        /// </summary>
        /// <returns></returns>
        public static Composite CreateWaitForLagDuration()
        {
            // return new WaitContinue(TimeSpan.FromMilliseconds((SingularRoutine.Latency * 2) + 150), ret => false, new ActionAlwaysSucceed());
            return CreateWaitForLagDuration(ret => false);
        }

        /// <summary>
        /// Allows waiting for SleepForLagDuration() but ending sooner if condition is met
        /// </summary>
        /// <param name="orUntil">if true will stop waiting sooner than lag maximum</param>
        /// <returns></returns>
        public static Composite CreateWaitForLagDuration( CanRunDecoratorDelegate orUntil)
        {
            return new DynaWaitContinue(ts => TimeSpan.FromMilliseconds((SingularRoutine.Latency * 2) + 150), orUntil, new ActionAlwaysSucceed());
        }

        #region Wait for Rez Sickness

        public static Composite CreateWaitForRessSickness()
        {
            return new Decorator(
                ret => SingularSettings.Instance.WaitForResSickness && StyxWoW.Me.HasAura("Resurrection Sickness"),
                new PrioritySelector(
                    new Throttle(TimeSpan.FromMinutes(1), new Action(r => Logger.Write("Waiting out Resurrection Sickness (expires in {0:F0} seconds", StyxWoW.Me.GetAuraTimeLeft("Resurrection Sickness", false).TotalSeconds))),
                    new Action(ret => { })
                    )
                );
        }

        #endregion

        public static Composite EnsureReadyToAttackFromMelee()
        {
            PrioritySelector prio = new PrioritySelector(
                Movement.CreatePositionMobsInFront(),
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                new Decorator(
                    req => Me.GotTarget && Me.CurrentTarget.Distance < SingularSettings.Instance.MeleeDismountRange,
                    Helpers.Common.CreateDismount( Dynamics.CompositeBuilder.CurrentBehaviorType.ToString())   // should be Pull or Combat 99% of the time
                    )
                );

            if (Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Pull)
            {
                prio.AddChild(
                    new PrioritySelector(
                        ctx => Me.GotTarget && Me.CurrentTarget.IsAboveTheGround(),
                        new Decorator(
                            req => (bool)req,
                            new PrioritySelector(
                                Movement.CreateMoveToUnitBehavior(on => Me.CurrentTarget, 27, 22),
                                Movement.CreateEnsureMovementStoppedBehavior(22)
                                )
                            ),
                        new Decorator(
                            req => !(bool)req,
                            new PrioritySelector(
                                Movement.CreateMoveToMeleeBehavior(true),
                                Movement.CreateEnsureMovementStoppedWithinMelee()
                                )
                            )
                        )
                    );
            }
            else
            {
                prio.AddChild( Movement.CreateMoveToMeleeBehavior(true));
                prio.AddChild(Movement.CreateEnsureMovementStoppedWithinMelee());
            }

            return prio;
        }

        /// <summary>
        /// Ensures the ready to attack from medium (30yd) range.
        /// </summary>
        /// <returns></returns>
        public static Composite EnsureReadyToAttackFromMediumRange( )
        {
            return new PrioritySelector(
                Movement.CreatePositionMobsInFront(),

                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateDismount(Dynamics.CompositeBuilder.CurrentBehaviorType.ToString()),   // should be Pull or Combat 99% of the time
                Movement.CreateMoveToUnitBehavior(on => Me.CurrentTarget, 30, 25),
                Movement.CreateEnsureMovementStoppedBehavior(25f)
                );
        }

        /// <summary>
        /// Ensures the ready to attack from long (40yd) range.
        /// </summary>
        /// <returns></returns>
        public static Composite EnsureReadyToAttackFromLongRange()
        {
            return new PrioritySelector(
                Movement.CreatePositionMobsInFront(),

                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateDismount(Dynamics.CompositeBuilder.CurrentBehaviorType.ToString()),   // should be Pull or Combat 99% of the time
                Movement.CreateMoveToUnitBehavior(on => Me.CurrentTarget, 40, 36),
                Movement.CreateEnsureMovementStoppedBehavior(36f)
                );
        }

        static DateTime brezStart = DateTime.Now;
        static ulong brezPrevGuid = 0;

        public static Composite CreateCombatRezBehavior(string spellName, SimpleBooleanDelegate unitFilter = null, SimpleBooleanDelegate requirements = null)
        {
            UnitSelectionDelegate onUnit;

            if (unitFilter == null)
                unitFilter = req => true;

            if (requirements == null)
                requirements = req => true;

            switch (SingularSettings.Instance.CombatRezTarget)
            {
                default:
                    onUnit = null;
                    break;
                case CombatRezTarget.Tank:
                    onUnit = on => Group.Tanks.FirstOrDefault(t => !t.IsMe && t.IsDead && unitFilter(t) && t.SpellDistance() < SingularSettings.Instance.MaxHealTargetRange);
                    break;
                case CombatRezTarget.Healer:
                    onUnit = on => Group.Healers.FirstOrDefault(h => !h.IsMe && h.IsDead && unitFilter(h) && h.SpellDistance() < SingularSettings.Instance.MaxHealTargetRange);
                    break;
                case CombatRezTarget.TankOrHealer:
                    onUnit = on => Group.Tanks.FirstOrDefault(t => !t.IsMe && t.IsDead && unitFilter(t) && t.SpellDistance() < SingularSettings.Instance.MaxHealTargetRange)
                        ?? Group.Healers.FirstOrDefault(h => !h.IsMe && h.IsDead && unitFilter(h) && h.SpellDistance() < SingularSettings.Instance.MaxHealTargetRange);
                    break;
                case CombatRezTarget.DPS:
                    onUnit = on => Group.Dps.FirstOrDefault(d => !d.IsMe && d.IsDead && unitFilter(d) && d.SpellDistance() < SingularSettings.Instance.MaxHealTargetRange);
                    break;
                case CombatRezTarget.All:
                    onUnit = on => (Group.Tanks.FirstOrDefault(t => !t.IsMe && t.IsDead && unitFilter(t) && t.SpellDistance() < SingularSettings.Instance.MaxHealTargetRange)
                        ?? Group.Healers.FirstOrDefault(h => !h.IsMe && h.IsDead && unitFilter(h) && h.SpellDistance() < SingularSettings.Instance.MaxHealTargetRange))
                            ?? Group.Dps.FirstOrDefault(d => !d.IsMe && d.IsDead && unitFilter(d) && d.SpellDistance() < SingularSettings.Instance.MaxHealTargetRange);
                    break;
            }

            if (onUnit == null)
            {
                Logger.WriteDebug("CreateRebirthBehavior: error - onUnit == null");
                return new PrioritySelector();
            }

            // throttle to minimize the impact of the list searches to once every interval
            return new ThrottlePasses( 1, 1,
                new Decorator(
                    req => Me.Combat && Spell.GetSpellCooldown(spellName) == TimeSpan.Zero && requirements(req),
                    new PrioritySelector(
                        ctx => onUnit(ctx),
                        new Decorator(
                            ret => onUnit(ret) != null,
                            new PrioritySelector(
                                new Action( on => {
                                    if (((WoWUnit)on).Guid != brezPrevGuid)
                                    {
                                        brezPrevGuid = ((WoWUnit)on).Guid;
                                        brezStart = DateTime.Now + TimeSpan.FromSeconds( SingularSettings.Instance.CombatRezDelay);
                                        Logger.Write(Color.White,"^Combat Ressurrect: {0} @ {1:F1} yds in {2} seconds", ((WoWUnit)on).SafeName(), ((WoWUnit)on).SpellDistance(), SingularSettings.Instance.CombatRezDelay);
                                    }
                                    return RunStatus.Failure;
                                }),
                                Movement.CreateMoveToLosBehavior(on => (WoWUnit) on),
                                Movement.CreateMoveToUnitBehavior(on => (WoWUnit)on, 40f, 40f),
                                new Wait( SingularSettings.Instance.CombatRezDelay, until => brezStart < DateTime.Now, new ActionAlwaysFail()),
                                Spell.Cast(spellName, mov => true, on => (WoWUnit)on, requirements, cancel => ((WoWUnit)cancel).IsAlive)
                                )
                            )
                        )
                    )
                );
        }

        private static DateTime timeMoveToSoulwell = DateTime.MinValue;

        public static Composite CreateUseSoulwellBehavior(SimpleBooleanDelegate requirements = null)
        {
            if (!SingularSettings.Instance.UseSoulwell)
                return new ActionAlwaysFail();

            if (requirements == null)
                requirements = req => true;

            // throttle to minimize the impact of the list searches to once every interval
            return new Decorator(
                req => !MovementManager.IsMovementDisabled && DoWeNeedHealthstones && requirements(req),
                new PrioritySelector(
                    ctx => ClassSpecific.Warlock.Common.Soulwell,
                    new Decorator(
                        req => (req as WoWGameObject) != null && (SingularSettings.Instance.SoulwellDistance == 0 || (req as WoWGameObject).Distance < SingularSettings.Instance.SoulwellDistance),
                        new PrioritySelector(
                            new Throttle(
                                TimeSpan.FromMilliseconds(500), 
                                new Action( r => 
                                { 
                                    Logger.WriteDebug("Soulwell: moving towards @ {0:F1} yds", (r as WoWGameObject).Distance); 
                                    return RunStatus.Failure; 
                                })
                                ),
                            Movement.CreateMoveToLocationBehavior( loc => (loc as WoWGameObject).Location, true, range => (range as WoWGameObject).InteractRange - 0.5f),
                            new Sequence(
                                new Action( r=> 
                                {
                                    Logger.Write( Color.White, "^Interact with Soulwell");
                                    (r as WoWGameObject).Interact();
                                }),
                                new WaitContinue( TimeSpan.FromSeconds(1), until => ClassSpecific.Warlock.Common.HaveHealthStone, new ActionAlwaysSucceed())
                                )
                            )
                        )
                    )
                );
        }

        public static bool DoWeNeedHealthstones
        {
            get
            {
                return !Me.Combat && !Me.Mounted && !Me.BagsFull && !ClassSpecific.Warlock.Common.HaveHealthStone;
            }
        }

        public static bool DoWeNeedBiscuits 
        { 
            get
            {
                if (!SingularSettings.Instance.UseTable)
                    return false;
                int drinksWanted = CharacterSettings.Instance.DrinkAmount;
                int foodWanted = CharacterSettings.Instance.FoodAmount;
                int biscuitsWanted = Math.Max(drinksWanted, foodWanted);

                if (Me.Combat || Me.Mounted)
                    return false;

                if (ClassSpecific.Mage.Common.Gotfood)
                    return false;

                int slotsNeeded = (biscuitsWanted + 19) / 20;
                if (Me.FreeNormalBagSlots < slotsNeeded)
                    return false;

                return true;
            }
        }

       

        private static DateTime timeMoveToTable = DateTime.MinValue;

        public static Composite CreateUseTableBehavior(SimpleBooleanDelegate requirements = null)
        {
            if (!SingularSettings.Instance.UseTable)
                return new ActionAlwaysFail();

            if (requirements == null)
                requirements = req => true;

            // throttle to minimize the impact of the list searches to once every interval
            return new Decorator(
                req => !MovementManager.IsMovementDisabled && !ClassSpecific.Mage.Common.Gotfood && requirements(req),
                new PrioritySelector(
                    ctx => ClassSpecific.Mage.Common.MageTable,

                    new Throttle(
                        TimeSpan.FromSeconds(15),
                        new Decorator(
                            req => Me.FreeNormalBagSlots < 1,
                            new Action( r=> Logger.Write(Color.White, "Refreshment Table:  !! Bags Full - skipping !!"))
                            )
                        ),

                    // check if we have space
                    new Decorator(
                        req =>
                        {
                            int drinksWanted = CharacterSettings.Instance.DrinkAmount;
                            int foodWanted = CharacterSettings.Instance.FoodAmount;
                            int biscuitsWanted = Math.Max(drinksWanted, foodWanted);

                            uint slotsNeeded = (uint) (biscuitsWanted + 19) / 20;
                            uint slotsToFill = Me.FreeNormalBagSlots;

                            if (slotsNeeded > slotsToFill)
                            {
                                Logger.Write(Color.White, "Refreshment Table: only {0} bag slots free - reducing amount we pickup", slotsToFill);
                                slotsNeeded = slotsToFill;
                            }
                            
                            return true;
                        },
                        new PrioritySelector(
                            new Decorator(
                                req => (req as WoWGameObject) != null && (SingularSettings.Instance.TableDistance == 0 || (req as WoWGameObject).Distance < SingularSettings.Instance.TableDistance),
                                new PrioritySelector(
                                    new Throttle(
                                        TimeSpan.FromMilliseconds(500),
                                        new Action(r =>
                                        {
                                            Logger.WriteDebug("Refreshment Table: moving towards @ {0:F1} yds", (r as WoWGameObject).Distance);
                                            return RunStatus.Failure;
                                        })
                                        ),
                                    Movement.CreateMoveToLocationBehavior(loc => (loc as WoWGameObject).Location, true, range => (range as WoWGameObject).InteractRange - 0.5f),
                                    new Sequence(
                                        new Action(r =>
                                        {
                                            Logger.Write(Color.White, "^Interact with Refreshment Table");
                                            (r as WoWGameObject).Interact();
                                        }),
                                        Helpers.Common.CreateWaitForLagDuration()
                                        )
                                    )
                                )
                            )
                        )
                    )
                );
        }

    }
}
