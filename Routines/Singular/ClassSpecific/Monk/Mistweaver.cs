using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Lists;
using Singular.Managers;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using Singular.Settings;
using Styx.WoWInternals;
using Styx.Common.Helpers;


namespace Singular.ClassSpecific.Monk
{
    // wowraids.org/mistweaver
    public class Mistweaver
    {
        private const int SOOTHING_MIST = 115175;

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static MonkSettings MonkSettings { get { return SingularSettings.Instance.Monk(); } }
        public static bool HasTalent(MonkTalents tal) { return TalentManager.IsSelected((int)tal); }


        private static bool RangedAttacks { get; set; }

        public static WoWUnit MyHealTarget
        {
            get
            {
                return HealerManager.SavingHealUnit ?? HealerManager.FindHighestPriorityTarget();
            }
        }

        #region INITIALIZE

        private static string SpinningCraneKick { get; set; }
        private static int minDistRollAllowed { get; set; }

        private static bool glyphedRenewingMist { get; set; }
        private static bool glyphedManaTea { get; set; }

        [Behavior(BehaviorType.Initialize, WoWClass.Monk, WoWSpec.MonkMistweaver)]
        public static Composite MonkMistweaverInitialize()
        {
            SpinningCraneKick = HasTalent(MonkTalents.RushingJadeWind) ? "Rushing Jade Wind" : "Spinning Crane Kick";
            if (HasTalent(MonkTalents.RushingJadeWind))
                Logger.Write("[spinning crane kick] Using Rushing Jade Wind");

            RangedAttacks = SpellManager.HasSpell("Crackling Jade Lightning");
            Logger.Write("[dps distance] Will DPS from {0}", RangedAttacks ? "Range" : "Melee");

            minDistRollAllowed = RangedAttacks ? 45 : 12;
            Logger.Write("[roll distance] Must be atleast {0} yds away for Roll", minDistRollAllowed);

            glyphedManaTea = TalentManager.HasGlyph("Mana Tea");
            if (glyphedManaTea)
                Logger.Write("[glyph of mana tea] Recognize as Instant Cast");

            glyphedRenewingMist = TalentManager.HasGlyph("Renewing Mist");
            if (glyphedRenewingMist)
                Logger.Write("[glyph of renewing mist] Using 40 yd hop distance");
            
            return null;
        }

        #endregion  

        #region REST

        [Behavior(BehaviorType.Rest, WoWClass.Monk, WoWSpec.MonkMistweaver)]
        public static Composite CreateMistweaverRest()
        {
            return new PrioritySelector(

                CreateMistweaverWaitForCast(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),

                    new PrioritySelector(
                        CreateMistweaverMonkHealing(true),

                        CreateManaTeaBehavior(),

                        Rest.CreateDefaultRestBehaviour(null, "Resuscitate"),

                        CreateMistweaverMonkHealing(false)

                        )
                    ),

                Spell.WaitForCastOrChannel(),

                // HealerManager.CreateStayNearTankBehavior(),

                Spell.WaitForGlobalCooldown()
                );
        }

        #endregion

        #region BUFFS

        [Behavior(BehaviorType.PullBuffs | BehaviorType.CombatBuffs, WoWClass.Monk, WoWSpec.MonkMistweaver)]
        public static Composite CreateMistweaverCombatBuffs()
        {
            return new PrioritySelector(

                // stance
                Spell.BuffSelf("Stance of the Wise Serpent"), // ret => Me.IsInGroup()),

                // common Monk group buffs applied in Common.CreateMonkPreCombatBuffs
                // common Monk personal buffs applied in Common.CreateMonkCombatBuffs

                // cast Mana Tea if low on Mana
                CreateManaTeaBehavior()
                );
        }

        private static WoWUnit _statue;

        private static Composite CreateSummonJadeSerpentStatueBehavior()
        {
            if (!SpellManager.HasSpell("Summon Jade Serpent Statue"))
                return new ActionAlwaysFail();

            return new Throttle(
                8,
                new Decorator(
                    req => !Spell.IsSpellOnCooldown("Summon Jade Serpent Statue") ,

                    new PrioritySelector(
                        ctx => _statue = FindStatue(),

                        new Decorator(

                            req => (_statue == null || (Me.GotTarget && Me.CurrentTarget.Location.Distance(_statue.Location) > 35)),

                            new PrioritySelector(
                                ctx => Clusters.GetBestUnitForCluster( Unit.GroupMembers.Where(g => g.IsAlive && g.Combat && g.IsMelee()), ClusterType.Radius, 30f),

                                new Decorator(
                                    req =>
                                    {
                                        if (req == null)
                                            return false;

                                        if (_statue == null)
                                            Logger.WriteDebug("JadeStatue:  my statue does not exist");
                                        else
                                        {
                                            float dist = _statue.Location.Distance((req as WoWUnit).Location);
                                            if (dist > 40)
                                                Logger.WriteDebug("JadeStatue:  my statue is {0:F1} yds away from {1} (max 40 yds)", dist, (req as WoWUnit).SafeName());
                                            else if (_statue.Distance > 40)
                                                Logger.WriteDebug("JadeStatue:  my statue is {0:F1} yds away from {1} (max 40 yds)", dist, Me.SafeName());
                                            else
                                                return false;
                                        }

                                        // yep we need to cast
                                        return true;
                                    },

                                    Spell.CastOnGround(
                                        "Summon Jade Serpent Statue",
                                        loc => {
                                            WoWPoint locTank = (loc as WoWUnit).Location;
                                            WoWPoint locMe = Me.Location;
                                            float dist = (float) locMe.Distance(locTank) * 2f / 3f;
                                            dist = Math.Min(dist, 35f);
                                            if (dist < 10)
                                                dist = -10f;    // plant past tank if he is close to us
                                            return Styx.Helpers.WoWMathHelper.CalculatePointFrom( locMe, locTank, (float)dist);
                                            },
                                        req => true,
                                        false
                                        )
                                    )
                                )
                            )
                        )
                    )
                );
        }

        private static int GetBattlegroundRoleWeight(WoWPartyMember o, int p)
        {
            if (o.HasRole(WoWPartyMember.GroupRole.Tank))
                return p * 2;
            if (o.HasRole(WoWPartyMember.GroupRole.Healer))
                return p;
            return 0;
        }

        public static WoWUnit FindStatue()
        {
            const uint JADE_SERPENT_STATUE = 60849;
            ulong guidMe = Me.Guid;
            return ObjectManager.GetObjectsOfType<WoWUnit>()
                .FirstOrDefault(u => u.Entry == JADE_SERPENT_STATUE && u.CreatedByUnitGuid == guidMe);
        }

        #endregion

        #region HEAL

        [Behavior(BehaviorType.Heal, WoWClass.Monk, WoWSpec.MonkMistweaver, WoWContext.Normal)]
        public static Composite CreateMistweaverHealBehaviorNormal()
        {
            return CreateMistweaverMonkHealing(false);
        }

        #endregion

        #region NORMAL

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkMistweaver, WoWContext.Normal)]
        public static Composite CreateMistweaverCombatBehaviorSolo()
        {
            return new Decorator(
                ret => !Unit.NearbyGroupMembers.Any(m => m.IsAlive && !m.IsMe),

                new PrioritySelector(

                    CreateMistweaverWaitForCast(),

                    CreateMistweaverMoveToEnemyTarget(),

                    new Decorator(
                        req => Me.GotTarget && Me.CurrentTarget.IsWithinMeleeRange,
                        Helpers.Common.CreateAutoAttack(true)
                        ),

                    new Decorator(
                        ret => !Spell.IsGlobalCooldown(),

                        new PrioritySelector(
                            CreateMistWeaverDiagnosticOutputBehavior(on => HealerManager.Instance.FirstUnit),

                            Helpers.Common.CreateInterruptBehavior(),

                            Spell.Cast("Touch of Death", ret => Me.CurrentChi >= 3 && Me.HasAura("Death Note")),

                            new Decorator(
                                req =>
                                {
                                    if (!Me.GotTarget)
                                        return false;

                                    if (SingularRoutine.CurrentWoWContext == WoWContext.Instances)
                                        return Me.CurrentTarget.IsBoss();

                                    // grapple weapon if target is within melee range of its target
                                    WoWUnit tt = Me.CurrentTarget.CurrentTarget;
                                    return !Me.CurrentTarget.IsStunned() && tt != null && tt.IsWithinMeleeRangeOf(Me.CurrentTarget);
                                },

                                Common.CreateGrappleWeaponBehavior()
                                ),

                            Spell.Cast("Paralysis",
                                onu => Unit.NearbyUnfriendlyUnits
                                    .FirstOrDefault(u => u.IsCasting && u.Distance.Between(9, 20) && Me.IsSafelyFacing(u))),

                            Spell.Cast("Leg Sweep", ret => Spell.UseAOE && MonkSettings.StunMobsWhileSolo && SingularRoutine.CurrentWoWContext == WoWContext.Normal && Me.CurrentTarget.IsWithinMeleeRange),

                            // high priority on tiger power
                            Spell.Cast("Tiger Palm", ret => Me.HasKnownAuraExpired("Tiger Power")),

                            // high priority on muscle memory 
                            new Decorator(
                                req => Me.HasAura("Muscle Memory"),
                                new PrioritySelector(
                                    Spell.Cast("Blackout Kick"),
                                    Spell.Cast("Tiger Palm")
                                    )
                                ),

                            Spell.Cast(SpinningCraneKick, ret => Spell.UseAOE && Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) >= MonkSettings.SpinningCraneKickCnt),

                            // chi dump
                            Spell.Cast("Tiger Palm", ret => Me.CurrentChi > 0 && Me.HasKnownAuraExpired("Tiger Power")),
                            Spell.Cast("Blackout Kick", ret => Me.CurrentChi == Me.MaxChi),

                            Spell.Cast("Expel Harm", on =>
                            {
                                if (Spell.IsSpellOnCooldown("Expel Harm"))
                                    return null;

                                WoWUnit target = null;
                                WoWUnit targetOffheal = null;

                                if (Me.HealthPercent < MonkSettings.ExpelHarmHealth)
                                    target = Me;
                                else if (TalentManager.HasGlyph("Targeted Explusion"))
                                {
                                    targetOffheal = Unit.GroupMembers
                                        .Where(p => p.IsAlive && p.DistanceSqr < 40 * 40)
                                        .OrderBy(p => p.PredictedHealthPercent())
                                        .FirstOrDefault();
                                    target = targetOffheal;
                                }

                                if (target != null)
                                    Logger.WriteDebug("Expel Harm:  Heal {0} @ actual:{1:F1}% predict:{2:F1}% and Me moving:{3}", target.SafeName(), target.HealthPercent, target.PredictedHealthPercent(includeMyHeals: true), Me.IsMoving.ToYN());
                                else if (Me.CurrentChi <= (Me.MaxChi - 2))
                                {
                                    target = targetOffheal ?? Me;
                                    Logger.WriteDebug("Expel Harm:  Chi Build (Chi={0}) {1} @ actual:{2:F1}% predict:{3:F1}% and Me moving:{4}", Me.CurrentChi, target.SafeName(), target.HealthPercent, target.PredictedHealthPercent(includeMyHeals: true), Me.IsMoving.ToYN());
                                }

                                return target;
                            }),

                            Spell.Cast(sp => "Crackling Jade Lightning", mov => true, on => Me.CurrentTarget, req => Me.CurrentTarget.SpellDistance() < 40, cancel => false),
                            Spell.Cast("Jab", ret => Me.CurrentChi < Me.MaxChi)
                            )
                        ),

                    // Spell.Cast("Roll", ret => MovementManager.IsClassMovementAllowed && !MonkSettings.DisableRoll && Me.CurrentTarget.Distance > minDistRollAllowed)
                    Common.CreateMonkCloseDistanceBehavior()
                    )
                );
        }

        #endregion

        #region BATTLEGROUNDS

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkMistweaver, WoWContext.Battlegrounds)]
        public static Composite CreateMistweaverCombatBehaviorBattlegrounds()
        {
            return new Decorator(
                ret => !Unit.NearbyGroupMembers.Any(m => m.IsAlive && !m.IsMe),

                new PrioritySelector(

                    CreateMistWeaverDiagnosticOutputBehavior(on => MyHealTarget ),

                    CreateMistweaverWaitForCast(),

                    HealerManager.CreateMeleeHealerMovementBehavior(),

                    new Decorator(
                        req => HealerManager.AllowHealerDPS(),

                        new PrioritySelector(

                            CreateMistweaverMoveToEnemyTarget(),

                            new Decorator(
                                ret => !Spell.IsGlobalCooldown(),
                                new PrioritySelector(

                                    new Decorator(
                                        req => Me.GotTarget && Me.CurrentTarget.IsWithinMeleeRange,
                                        Helpers.Common.CreateAutoAttack(true)
                                        ),

                                    Helpers.Common.CreateInterruptBehavior(),
                                    Dispelling.CreatePurgeEnemyBehavior("Detox"),

                                    Spell.Cast("Touch of Death", ret => Me.CurrentChi >= 3 && Me.HasAura("Death Note")),

                                    new Decorator(
                                        req =>
                                        {
                                            if (!Me.GotTarget)
                                                return false;

                                            // grapple weapon if target is within melee range of its target
                                            WoWUnit tt = Me.CurrentTarget.CurrentTarget;
                                            return !Me.CurrentTarget.IsStunned() && tt != null && tt.IsWithinMeleeRangeOf(Me.CurrentTarget);
                                        },

                                        Common.CreateGrappleWeaponBehavior()
                                        ),

                                    Spell.Cast("Paralysis",
                                        onu => Unit.NearbyUnfriendlyUnits
                                            .FirstOrDefault(u => u.IsCasting && u.Distance.Between(9, 20) && Me.IsSafelyFacing(u))),

                                    Spell.Cast("Leg Sweep", ret => Spell.UseAOE && SingularRoutine.CurrentWoWContext == WoWContext.Normal && Me.CurrentTarget.IsWithinMeleeRange),

                                    // high priority on tiger power
                                    Spell.Cast("Tiger Palm", ret => Me.HasKnownAuraExpired("Tiger Power")),

                                    // high priority on muscle memory 
                                    new Decorator(
                                        req => Me.HasAura("Muscle Memory"),
                                        new PrioritySelector(
                                            Spell.Cast("Blackout Kick"),
                                            Spell.Cast("Tiger Palm")
                                            )
                                        ),

                                    Spell.Cast(SpinningCraneKick, ret => Spell.UseAOE && Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) >= MonkSettings.SpinningCraneKickCnt),

                                    // chi dump
                                    Spell.Cast("Tiger Palm", ret => Me.CurrentChi > 0 && Me.HasKnownAuraExpired("Tiger Power")),
                                    Spell.Cast("Blackout Kick", ret => Me.CurrentChi == Me.MaxChi),

                                    Spell.Cast("Expel Harm", ret => Me.CurrentChi < (Me.MaxChi - 2) || Me.HealthPercent < 80),

                                    Spell.Cast(
                                        "Crackling Jade Lightning",
                                        mov => true,
                                        on => Unit.NearbyUnitsInCombatWithUsOrOurStuff
                                            .Where(u => u.IsAlive && u.SpellDistance() < 40 && Me.IsSafelyFacing(u))
                                            .OrderByDescending(u => u.HealthPercent)
                                            .FirstOrDefault(),
                                        req => !HealerManager.CancelHealerDPS(),
                                        cancel => HealerManager.CancelHealerDPS()
                                        ),

                                    Spell.Cast("Jab", ret => Me.CurrentChi < Me.MaxChi)
                                    )
                                ),

                            Spell.Cast("Roll", ret => MovementManager.IsClassMovementAllowed && !MonkSettings.DisableRoll && Me.CurrentTarget.Distance > minDistRollAllowed)
                            )
                        ),

                    new Decorator(
                        ret => Unit.NearbyGroupMembers.Any(m => m.IsAlive && !m.IsMe),
                        new PrioritySelector(
                            CreateMistweaverMonkHealing(selfOnly: false),
                            Helpers.Common.CreateInterruptBehavior()
                            )
                        )
                    )
                );

        }

        #endregion

        #region INSTANCES

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkMistweaver, WoWContext.Instances)]
        public static Composite CreateMistweaverCombatBehaviorInstances()
        {
            return new PrioritySelector(

                CreateMistweaverWaitForCast(),
                CreateMistWeaverDiagnosticOutputBehavior(on => MyHealTarget),


                HealerManager.CreateMeleeHealerMovementBehavior( Common.CreateMonkCloseDistanceBehavior( min => 10, on => (WoWUnit) on)),
/*
                    new Decorator(
                        unit => MovementManager.IsClassMovementAllowed
                            && !MonkSettings.DisableRoll
                            && (unit as WoWUnit).SpellDistance() > 10
                            && Me.IsSafelyFacing(unit as WoWUnit, 5f),
                        Spell.Cast("Roll")
                        )
                    ),
*/
                new Decorator(
                    ret => Me.Combat && HealerManager.AllowHealerDPS(),
                    new PrioritySelector(

                        CreateMistweaverMoveToEnemyTarget(),
                        new Decorator(
                            req => Me.GotTarget && Me.CurrentTarget.IsAlive && !IsChannelingSoothingMist(),
                            Movement.CreateFaceTargetBehavior()
                            ),

                        Spell.WaitForCastOrChannel(),

            #region Spinning Crane Kick progress handler

                        new Decorator(
                            req => Me.HasAura("Spinning Crane Kick"),   // don't wait for Rushing Jade Wind since we can cast
                            new PrioritySelector(
                                new Action(r =>
                                {
                                    Logger.WriteFile( SpinningCraneKick + ": in progress with {0} ms left", (long)Me.GetAuraTimeLeft(SpinningCraneKick).TotalMilliseconds);
                                    return RunStatus.Failure;
                                }),
                                new Decorator(
                                    req =>
                                    {
                                        if (Me.GetAuraTimeLeft(SpinningCraneKick).TotalMilliseconds < 333)
                                            return false;

                                        int countFriendly = Unit.NearbyGroupMembersAndPets.Count(u => u.SpellDistance() <= 8);
                                        if (countFriendly >= 3)
                                            return false;

                                        if (HealerManager.CancelHealerDPS())
                                        {
                                            Logger.Write(Color.Orange, "/cancel {0} since only {1} friendly targets hit and cannot DPS", SpinningCraneKick, countFriendly);
                                            return true;
                                        }

                                        int countEnemy = Unit.NearbyUnfriendlyUnits.Count(u => u.SpellDistance() <= 8);
                                        if ((countFriendly + countEnemy) < 3)
                                        {
                                            Logger.Write(Color.Orange, "/cancel {0} since only {1} friendly and {2} enemy targets hit", SpinningCraneKick, countFriendly, countEnemy);
                                            return true;
                                        }
                                        return false;
                                    },
                                    new Sequence(
                                        new Action(r => Me.CancelAura(SpinningCraneKick)),
                                        new Wait( 1, until => !Me.HasAura(SpinningCraneKick), new ActionAlwaysFail())
                                        )
                                    ),

                                // dont go past here if SCK active
                                new ActionAlwaysSucceed()
                                )
                            ),
            #endregion

                        new Decorator(
                            ret => !Spell.IsGlobalCooldown(),
                            new PrioritySelector(

                                new Decorator(
                                    req => Me.GotTarget && Me.CurrentTarget.IsWithinMeleeRange,
                                    Helpers.Common.CreateAutoAttack(true)
                                    ),

                                // I WANT CHI!!!
                                Spell.Cast("Expel Harm", ret => Me.CurrentChi < Me.MaxChi),

                                // deal with buffs as highest priority
                                new Decorator(
                                    req => Me.HasAura("Teachings of the Monastery"),
                                    new PrioritySelector(

                                        // lets keep up Muscle Memory
                                        new Decorator(
                                            req => !Me.HasAura("Muscle Memory"),
                                            new Sequence(
                                                Spell.Cast("Jab"),
                                                Helpers.Common.CreateWaitForLagDuration(until => Me.HasAura("Muscle Memory"))
                                                )
                                            ),

                                        // lets keep up Tiger Power and Serpent Zeal
                                        new Decorator(
                                            req => !Me.HasAura("Serpent Zeal") || !Me.HasAura("Tiger Power"),
                                            new PrioritySelector(
                                                // let's buff Muscle Memory if we need a Tiger Palm or Blackout Kick
                                                new Decorator(
                                                    req => !Me.HasAura("Muscle Memory"),
                                                    new Sequence(
                                                        Spell.Cast("Jab"),
                                                        Helpers.Common.CreateWaitForLagDuration( until => Me.HasAura("Muscle Memory"))
                                                        )
                                                    ),
                                                Spell.Cast("Tiger Palm", req => Me.HasKnownAuraExpired("Tiger Power") || Me.HasKnownAuraExpired("Vital Mists")),
                                                Spell.Cast("Blackout Kick", req => Me.HasKnownAuraExpired("Serpent Zeal"))
                                                )
                                            ),

                                        // if multiple targets, lets Blackout Kick
                                        new Decorator(
                                            req => HealerManager.Instance.FirstUnit != null
                                                && Unit.UnfriendlyUnitsNearTarget(8f).Count() >= 3,
                                            new PrioritySelector(                                                
                                                new Decorator(
                                                    req => !Me.HasAura("Muscle Memory"),
                                                    new Sequence(
                                                        Spell.Cast("Jab"),
                                                        Helpers.Common.CreateWaitForLagDuration(until => Me.HasAura("Muscle Memory"))
                                                        )
                                                    ),

                                                Spell.Cast("Blackout Kick", req => Me.HasAura("Muscle Memory") )
                                                )
                                            )
                                        )
                                    ),
                                        

                                //        
                                Helpers.Common.CreateInterruptBehavior(),
                                Spell.Cast("Touch of Death", ret => Me.CurrentChi >= 3 && Me.HasAura("Death Note")),

                                // grapple weapon if target is within melee range of its target
                                new Decorator(
                                    req =>
                                    {
                                        if (!Me.GotTarget)
                                            return false;
                                        WoWUnit tt = Me.CurrentTarget.CurrentTarget;
                                        return !Me.CurrentTarget.IsStunned() && tt != null && tt.IsWithinMeleeRangeOf(Me.CurrentTarget);
                                    },

                                    Common.CreateGrappleWeaponBehavior()
                                    ),

                                Spell.Cast("Leg Sweep", ret => Spell.UseAOE && SingularRoutine.CurrentWoWContext == WoWContext.Normal && Me.CurrentTarget.IsWithinMeleeRange),

                                Spell.Cast(
                                    SpinningCraneKick,
                                    ret => Spell.UseAOE && HealerManager.AllowHealerDPS() && Unit.NearbyUnfriendlyUnits.Count(u => u.SpellDistance() <= 8) >= MonkSettings.SpinningCraneKickCnt
                                    ),

                                // chi dump
                                Spell.Cast("Tiger Palm", ret => Me.CurrentChi > 0 && Me.HasKnownAuraExpired("Tiger Power")),
                                Spell.Cast("Blackout Kick", ret => Me.CurrentChi >= 2),

                                Spell.Cast(
                                    "Crackling Jade Lightning",
                                    mov => true,
                                    on => Unit.NearbyUnitsInCombatWithUsOrOurStuff
                                        .Where(u => u.IsAlive && u.SpellDistance() < 40 && Me.IsSafelyFacing(u))
                                        .OrderByDescending(u => u.HealthPercent)
                                        .FirstOrDefault(),
                                    req => Me.GotTarget && !Me.CurrentTarget.IsWithinMeleeRange && HealerManager.AllowHealerDPS(),
                                    cancel => HealerManager.CancelHealerDPS()
                                    ),

                                Spell.Cast("Jab", ret => Me.CurrentChi < Me.MaxChi)
                                )
                            ),

                        Spell.Cast("Roll", ret => MovementManager.IsClassMovementAllowed && !MonkSettings.DisableRoll && Me.CurrentTarget.Distance > minDistRollAllowed)
                        )
                    ),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown() && Unit.NearbyGroupMembers.Any(m => m.IsAlive && !m.IsMe),
                    new PrioritySelector(
                        CreateMistweaverMonkHealing(selfOnly: false),
                        Helpers.Common.CreateInterruptBehavior()
                        )
                    )
                );

        }

        #endregion

        private static WoWUnit _moveToHealUnit = null;

        public static Composite CreateMistweaverMonkHealing(bool selfOnly = false)
        {
            HealerManager.NeedHealTargeting = true;
            PrioritizedBehaviorList behavs = new PrioritizedBehaviorList();

            int cancelHeal = SingularSettings.Instance.IgnoreHealTargetsAboveHealth;
            cancelHeal = (int)Math.Max(cancelHeal, MonkSettings.MistHealSettings.RenewingMist);
            cancelHeal = (int)Math.Max(cancelHeal, MonkSettings.MistHealSettings.SoothingMist);
            cancelHeal = (int)Math.Max(cancelHeal, MonkSettings.MistHealSettings.EnvelopingMist);
            cancelHeal = (int)Math.Max(cancelHeal, MonkSettings.MistHealSettings.SurgingMist);

            bool moveInRange = false;
            if (!selfOnly)
                moveInRange = (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds);

            Logger.WriteDebugInBehaviorCreate("Monk Healing: will cancel cast of direct heal if health reaches {0:F1}%", cancelHeal);

            int dispelPriority = (SingularSettings.Instance.DispelDebuffs == RelativePriority.HighPriority) ? 999 : -999;
            if (SingularSettings.Instance.DispelDebuffs != RelativePriority.None)
                behavs.AddBehavior(dispelPriority, "Detox", null, Dispelling.CreateDispelBehavior());


            CreateMistweaverHealingRotation(selfOnly, behavs);

            behavs.OrderBehaviors();

            if (selfOnly == false && Singular.Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Combat)
                behavs.ListBehaviors();


            return new PrioritySelector(
                // ctx => selfOnly ? StyxWoW.Me : HealerManager.FindLowestHealthTarget(),
                // ctx => selfOnly ? StyxWoW.Me : HealerManager.Instance.FirstUnit,
                ctx => selfOnly ? StyxWoW.Me : MyHealTarget,

                CreateMistWeaverDiagnosticOutputBehavior(ret => (WoWUnit)ret),

                new Decorator(
                    ret => ret != null 
                        && (Me.Combat || ((WoWUnit)ret).Combat || ((WoWUnit)ret).PredictedHealthPercent() <= 99),
                        // && HealerManager.SavingHealUnit == null
                        // && (selfOnly || !MonkSettings.MistHealSettings.HealFromMelee || !Me.GotTarget || Me.CurrentTarget.IsWithinMeleeRange),

                    new PrioritySelector(
                        new Decorator(
                            ret => !Spell.IsGlobalCooldown(),
                            new PrioritySelector(

                                behavs.GenerateBehaviorTree(),

#if false                  
                            ,
                            new Sequence(
                                new Action(ret => Logger.WriteDebug(Color.LightGreen, "No Action - stunned:{0} silenced:{1}"
                                    , Me.Stunned || Me.IsStunned() 
                                    , Me.Silenced 
                                    )),
                                new Action(ret => { return RunStatus.Failure; })
                                )
                            ,

                            new Decorator(
                                ret => StyxWoW.Me.Combat && StyxWoW.Me.GotTarget && !Unit.NearbyFriendlyPlayers.Any(u => u.IsInMyPartyOrRaid),
                                new PrioritySelector(
                                    Movement.CreateMoveToLosBehavior(),
                                    Movement.CreateFaceTargetBehavior(),
                                    Helpers.Common.CreateInterruptBehavior(),

                                    Spell.Cast("Earth Shock"),
                                    Spell.Cast("Lightning Bolt"),
                                    Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 35f, 30f)
                                    )
                                )
#endif
                                new Decorator(
                                    ret => moveInRange,
                                    new Sequence(
                                        new Action(r => _moveToHealUnit = (WoWUnit)r),
                                        new PrioritySelector(
                                            Movement.CreateMoveToLosBehavior(on => _moveToHealUnit),
                                            Movement.CreateMoveToUnitBehavior(on => _moveToHealUnit, 40f, 34f)
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    )
                );
        }

        private static void CreateMistweaverHealingRotation(bool selfOnly, PrioritizedBehaviorList behavs)
        {
            CreateMistweaverHealingWowhead(selfOnly, behavs);
        }

        private static void CreateMistweaverHealingCustom(bool selfOnly, PrioritizedBehaviorList behavs)
        {
            #region Cast On Cooldown

            if (!selfOnly)
            {
                behavs.AddBehavior(
                    HealthToPriority(MonkSettings.MistHealSettings.RollRenewingMistCount) + 700,
                    String.Format("Roll Renewing Mist on at least {0} targets", MonkSettings.MistHealSettings.RollRenewingMistCount),
                    "Renewing Mist",
                    CreateMistweaverRollRenewingMist()
                    );
            }

            #endregion

            #region Save the Group
            /*
            behavs.AddBehavior(HealthToPriority(MonkSettings.MistHealSettings.SpiritLinkTotem) + 600, "Spirit Link Totem", "Spirit Link Totem",
                new Decorator(
                    ret => Me.Combat && (StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid),
                    Spell.Cast(
                        "Spirit Link Totem", ret => (WoWUnit)ret,
                        ret => HealerManager.Instance.TargetList.Count(
                            p => p.PredictedHealthPercent() < MonkSettings.MistHealSettings.SpiritLinkTotem && p.Distance <= Totems.GetTotemRange(WoWTotem.SpiritLink)) >= MonkSettings.MistHealSettings.MinSpiritLinkCount
                        )
                    )
                );
*/
            if (!selfOnly)
            {
                behavs.AddBehavior(HealthToPriority(MonkSettings.MistHealSettings.ThunderFocusHealSingle) + 600,
                    String.Format("Thunder Focus Heal Single Target @ {0}%", MonkSettings.MistHealSettings.ThunderFocusHealSingle),
                    "Thunder Focus Tea",
                    new Decorator(
                        ret => (Me.Combat || ((WoWUnit)ret).Combat) && ((WoWUnit)ret).PredictedHealthPercent() < MonkSettings.MistHealSettings.ThunderFocusHealSingle,
                        new Sequence(
                            EnsureSoothingMistOnTarget(on => (WoWUnit)on),
                            Spell.OffGCD(Spell.BuffSelf("Thunder Focus Tea")),
                            new PrioritySelector(
                                Spell.Cast("Surging Mist", on => (WoWUnit)on),
                                Spell.Cast("Enveloping Mist", on => (WoWUnit)on)
                                )
                            )
                        )
                    );
            }

            #endregion

            #region High Priority Buff

            behavs.AddBehavior(
                899,
                "Summon Jade Serpent Statue",
                "Summon Jade Serpent Statue",
                new Decorator(
                    req => Group.Tanks.Any(t => t.Combat && t.GotTarget && t.IsWithinMeleeRangeOf(t.CurrentTarget)),
                    CreateSummonJadeSerpentStatueBehavior()
                    )
                );

            #endregion

            #region AoE Heals   400

            if (!selfOnly)
            {
                behavs.AddBehavior(
                    HealthToPriority(MonkSettings.MistHealSettings.ThunderFocusHealGroup) + 400,
                    String.Format("Thunder Focus Uplift on {0} targets @ {1}%", MonkSettings.MistHealSettings.CountThunderFocusHealGroup, MonkSettings.MistHealSettings.ThunderFocusHealGroup),
                    "Thunder Focus Tea",
                    new Decorator(
                        ret =>
                        {
                            if (!Me.Combat || !(StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid))
                                return false;

                            if (Me.CurrentChi < 2)
                                return false;

                            if (!Spell.CanCastHack("Thunder Focus Tea"))
                                return false;

                            int count = HealerManager.Instance.TargetList.Count(p => !p.HasAuraExpired("Renewing Mist", TimeSpan.FromMilliseconds(150)) && p.PredictedHealthPercent() <= MonkSettings.MistHealSettings.ThunderFocusHealGroup);
                            if (count >= MonkSettings.MistHealSettings.CountThunderFocusHealGroup)
                            {
                                Logger.WriteDebug("ThunderFocus: found {0} below {1}%", MonkSettings.MistHealSettings.CountThunderFocusHealGroup, MonkSettings.MistHealSettings.ThunderFocusHealGroup);
                                return true;
                            }

                            return false;
                        },
                        new Sequence(
                            Spell.Cast("Thunder Focus Tea"),
                            Spell.Cast(
                                "Uplift",
                                on => on as WoWUnit
                                )
                            )
                        )
                    );

                behavs.AddBehavior(
                    HealthToPriority(MonkSettings.MistHealSettings.UpliftGroup) + 400,
                    String.Format("Uplift on {0} targets @ {1}%", MonkSettings.MistHealSettings.CountUpliftGroup, MonkSettings.MistHealSettings.UpliftGroup),
                    "Uplift",
                    new Decorator(
                        ret => (Me.Combat || ((WoWUnit)ret).Combat)
                            && (StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid)
                            && Me.CurrentChi >= 2,
                        Spell.Cast(
                            "Uplift",
                            on => on as WoWUnit,
                            req =>
                            {
                                int count = HealerManager.Instance.TargetList.Count(p => !p.HasAuraExpired("Renewing Mist", TimeSpan.FromMilliseconds(150)) && p.PredictedHealthPercent() <= MonkSettings.MistHealSettings.UpliftGroup);
                                if (count >= MonkSettings.MistHealSettings.CountUpliftGroup)
                                {
                                    Logger.WriteDebug("Uplift: found {0} with Renewing Mist (needed {1})", count, MonkSettings.MistHealSettings.CountUpliftGroup);
                                    return true;
                                }

                                return false;
                            }
                            )
                        )
                    );

                behavs.AddBehavior(
                    HealthToPriority(MonkSettings.MistHealSettings.ChiWave) + 400,
                    String.Format("Chi Wave on {0} targets @ {1}%", MonkSettings.MistHealSettings.CountChiWave, MonkSettings.MistHealSettings.ChiWave),
                    "Chi Wave",
                    new Decorator(
                        req => (req as WoWUnit).PredictedHealthPercent() < MonkSettings.MistHealSettings.ChiWave,
                        Spell.Cast("Chi Wave", on => GetBestChiWaveTarget())
                        )
                    );
            }

            #endregion


            #region Healing Cooldowns

            // save a player
            behavs.AddBehavior(
                HealthToPriority(MonkSettings.MistHealSettings.LifeCocoon) + 100,
                String.Format("Life Cocoon @ {0}%", MonkSettings.MistHealSettings.LifeCocoon),
                "Life Cocoon",
                Spell.Buff("Life Cocoon", on => (WoWUnit)on, req => (Me.Combat || (req as WoWUnit).Combat) && (req as WoWUnit).PredictedHealthPercent(includeMyHeals: true) < MonkSettings.MistHealSettings.LifeCocoon)
                );

            #endregion

            #region Single Target Heals

            // healing sphere
            behavs.AddBehavior(
                HealthToPriority(MonkSettings.MistHealSettings.HealingSphere),
                String.Format("Healing Sphere @ {0}%", MonkSettings.MistHealSettings.HealingSphere),
                "Healing Sphere",
                new Decorator(
                    req => (req as WoWUnit).PredictedHealthPercent(includeMyHeals: true) < MonkSettings.MistHealSettings.HealingSphere,
                    new Sequence(
                        Spell.CastOnGround("Healing Sphere", on => (WoWUnit)on, req => true, false),
                        new Wait(TimeSpan.FromMilliseconds(350), until => Spell.GetPendingCursorSpell != null, new ActionAlwaysSucceed()),
                        new Action(r =>
                        {
                            Logger.WriteDebug("HealingSphere: /cancel Pending Spell {0}", Spell.GetPendingCursorSpell.Name);
                            Lua.DoString("SpellStopTargeting()");
                        })
                        )
                    )
                );

            behavs.AddBehavior(
                HealthToPriority(MonkSettings.MistHealSettings.SoothingMist),
                String.Format("Soothing Mist @ {0}%", MonkSettings.MistHealSettings.SoothingMist),
                "Soothing Mist",
                Spell.Cast(
                    "Soothing Mist",
                    mov => true,
                    on => (WoWUnit)on,
                    req => !Me.IsMoving && !IsChannelingSoothingMist((WoWUnit)req) && ((WoWUnit)req).PredictedHealthPercent() < MonkSettings.MistHealSettings.SoothingMist
                    )
                );

            behavs.AddBehavior(
                HealthToPriority(MonkSettings.MistHealSettings.EnvelopingMist),
                String.Format("Enveloping Mist @ {0}%", MonkSettings.MistHealSettings.EnvelopingMist),
                "Enveloping Mist",
                new Decorator(
                    req => ((WoWUnit)req).PredictedHealthPercent() < MonkSettings.MistHealSettings.EnvelopingMist,
                    new Sequence(
                        EnsureSoothingMistOnTarget(on => on as WoWUnit),
                        new PrioritySelector(
                            Spell.Cast("Enveloping Mist", on => (WoWUnit)on),
                            Spell.Cast("Surging Mist", on => (WoWUnit)on, req => {
                                if (Spell.IsSpellOnCooldown("Enveloping Mist"))
                                    Logger.WriteDebug("EnvelopingMist: on cooldown, so casting Surging Mist", Me.CurrentChi);
                                else if (Me.CurrentChi < 3)
                                    Logger.WriteDebug("EnvelopingMist: casting Surging Mist since only {0} Chi", Me.CurrentChi);
                                else
                                {
                                    Logger.WriteDebug("EnvelopingMist: no idea why I cannot cast");
                                    return false;
                                }
                                return true;
                                })
                            )
                        )
                    )
                );

            behavs.AddBehavior(
                HealthToPriority(MonkSettings.MistHealSettings.SurgingMist),
                String.Format("Surging Mist @ {0}%", MonkSettings.MistHealSettings.SurgingMist),
                "Surging Mist",
                new Decorator(
                    req => ((WoWUnit)req).PredictedHealthPercent() < MonkSettings.MistHealSettings.SurgingMist,
                    new Sequence(
                        EnsureSoothingMistOnTarget(on => on as WoWUnit),
                        Spell.Cast("Surging Mist", on => (WoWUnit)on)
                        )
                    )
                );

            #endregion
        }

        private static void CreateMistweaverHealingWowhead(bool selfOnly, PrioritizedBehaviorList behavs)
        {
            // save a group
            if (!selfOnly)
            {
                behavs.AddBehavior(
                    HealthToPriority(MonkSettings.MistHealSettings.Revival) + 900,
                    String.Format("Revival on {0} targets @ {1}%", MonkSettings.MistHealSettings.CountRevival, MonkSettings.MistHealSettings.Revival),
                    "Revival",
                    new Decorator(
                        req => (req as WoWUnit).HealthPercent < MonkSettings.MistHealSettings.Revival,
                        Spell.Cast("Revival", on =>
                        {
                            if (Spell.IsSpellOnCooldown("Revival"))
                                return null;

                            List<WoWUnit> revlist = HealerManager.Instance.TargetList
                                .Where(u => u.HealthPercent < MonkSettings.MistHealSettings.Revival && u.DistanceSqr < 100 * 100 && u.InLineOfSpellSight)
                                .ToList();
                            if (revlist.Count() < MonkSettings.MistHealSettings.CountRevival)
                                return null;

                            Logger.Write(Color.White, "Revival: found {0} heal targets below {1}%", revlist.Count(), MonkSettings.MistHealSettings.Revival);
                            return revlist.FirstOrDefault(u => u != null && u.IsValid);
                        })
                        )
                    );
            }

            // save a player
            behavs.AddBehavior(
                HealthToPriority(MonkSettings.MistHealSettings.LifeCocoon) + 800,
                String.Format("Life Cocoon @ {0}%", MonkSettings.MistHealSettings.LifeCocoon),
                "Life Cocoon",
                Spell.Buff("Life Cocoon", on => (WoWUnit)on, req => (Me.Combat || (req as WoWUnit).Combat) && (req as WoWUnit).PredictedHealthPercent(includeMyHeals: true) < MonkSettings.MistHealSettings.LifeCocoon)
                );

            if (!selfOnly)
            {
                behavs.AddBehavior(
                    HealthToPriority(1) + 700,
                    "Summon Jade Serpent Statue",
                    "Summon Jade Serpent Statue",
                    new Decorator(
                        req => Group.Tanks.Any(t => t.Combat && !t.IsMoving && t.GotTarget && t.CurrentTarget.IsHostile && t.SpellDistance(t.CurrentTarget) < 10),
                        CreateSummonJadeSerpentStatueBehavior()
                        )
                    );
            }

            if (!selfOnly)
            {
                behavs.AddBehavior(
                    HealthToPriority(99) + 700,
                    String.Format("Thunder Focus Uplift on {0} targets", MonkSettings.MistHealSettings.CountThunderFocusHealGroup),
                    "Thunder Focus Tea",
                    new Decorator(
                        ret => {
                            if (!Me.Combat || !(StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid))
                                return false;

                            if (Me.CurrentChi < 3 || Spell.IsSpellOnCooldown("Thunder Focus Tea") || Spell.IsSpellOnCooldown("Uplift"))
                                return false;

                            List<WoWUnit> renewlist = Unit.GroupMembersAndPets
                                .Where(p => p.GetAuraTimeLeft("Renewing Mist").TotalMilliseconds > 250)
                                .ToList();
                            int countAlmostExpired = renewlist.Count(p => p.GetAuraTimeLeft("Renewing Mist").TotalMilliseconds < 3000);

                            if (SingularSettings.DebugSpellCasting)
                                Logger.WriteDebug("ThunderFocusTea: found {0} with renew mist, need {1} ({2} about with less than 3 secs)", renewlist.Count(), MonkSettings.MistHealSettings.CountThunderFocusHealGroup, countAlmostExpired);

                            return countAlmostExpired > 0 && renewlist.Count() >= MonkSettings.MistHealSettings.CountThunderFocusHealGroup;
                            },
                        new Sequence(
                            Spell.Cast("Thunder Focus Tea", on => Me),
                            Spell.Cast("Uplift")
                            )
                        )
                    );

                behavs.AddBehavior(
                    HealthToPriority(100) + 700,
                    String.Format("Roll Renewing Mist on at least {0} targets", MonkSettings.MistHealSettings.RollRenewingMistCount),
                    "Renewing Mist",
                    CreateMistweaverRollRenewingMist()
                    );
            }

            behavs.AddBehavior(
                HealthToPriority(MonkSettings.MistHealSettings.ChiWave) + 600,
                String.Format("Chi Wave on {0} targets @ {1}%", MonkSettings.MistHealSettings.CountChiWave, MonkSettings.MistHealSettings.ChiWave),
                "Chi Wave",
                new Decorator(
                    req => (req as WoWUnit).PredictedHealthPercent() < MonkSettings.MistHealSettings.ChiWave,
                    Spell.Cast("Chi Wave", on => GetBestChiWaveTarget())
                    )
                );

            behavs.AddBehavior(
                HealthToPriority(1) + 600,
                String.Format("Instant Surging Mist (Vital Mists=5)"),
                "Surging Mist",
                new Decorator(
                    req => Me.GetAuraStacks("Vital Mists") == 5, // && !IsChannelingSoothingMist(), // not sure we care about channel here
                    Spell.Cast("Surging Mist", on => (WoWUnit)on)
                    )
                );

            behavs.AddBehavior(
                HealthToPriority(1) + 500,
                "Expel Harm in Combat for Chi",
                "Expel Harm",
                Spell.BuffSelf("Expel Harm", req => Me.Combat && Me.CurrentChi < Me.MaxChi )
                );

            if (!selfOnly)
            {
                behavs.AddBehavior(
                    HealthToPriority(MonkSettings.MistHealSettings.UpliftGroup) + 400,
                    String.Format("Uplift on {0} targets @ {1}%", MonkSettings.MistHealSettings.CountUpliftGroup, MonkSettings.MistHealSettings.UpliftGroup),
                    "Uplift",
                    new Decorator(
                        ret => (Me.Combat || ((WoWUnit)ret).Combat)
                            && (StyxWoW.Me.GroupInfo.IsInParty || StyxWoW.Me.GroupInfo.IsInRaid)
                            && Me.CurrentChi >= 2,
                        Spell.Cast(
                            "Uplift",
                            on => on as WoWUnit,
                            req =>
                            {
                                int count = HealerManager.Instance.TargetList.Count(p => !p.HasAuraExpired("Renewing Mist", TimeSpan.FromMilliseconds(150)) && p.PredictedHealthPercent() < MonkSettings.MistHealSettings.UpliftGroup);
                                if (count >= MonkSettings.MistHealSettings.CountUpliftGroup)
                                {
                                    Logger.WriteDebug("Uplift: found {0} with Renewing Mist (needed {1})", count, MonkSettings.MistHealSettings.CountUpliftGroup);
                                    return true;
                                }

                                return false;
                            }
                            )
                        )
                    );
            }

            if (!selfOnly)
            {
                behavs.AddBehavior(
                    HealthToPriority(MonkSettings.MistHealSettings.SpinningCraneKickGroup),
                    String.Format(SpinningCraneKick + " on {0} targets @ {1}%", MonkSettings.MistHealSettings.CountSpinningCraneKickGroup, MonkSettings.MistHealSettings.SpinningCraneKickGroup),
                    SpinningCraneKick,
                    Spell.Cast(
                        SpinningCraneKick,
                        on => on as WoWUnit,
                        ret =>
                        {
                            if (Spell.IsSpellOnCooldown(SpinningCraneKick))
                                return false;

                            if (!Me.Combat)
                                return false;

                            if (!Spell.UseAOE)
                                return false;

                            if (Me.CurrentChi >= Me.MaxChi)
                                return false;

                            // count heal targets
                            int countHeal = HealerManager.Instance.TargetList
                                .Count(u => u.HealthPercent < MonkSettings.MistHealSettings.SpinningCraneKickGroup && u.SpellDistance() <= 8);
                            if (countHeal >= MonkSettings.MistHealSettings.CountSpinningCraneKickGroup)
                            {
                                Logger.WriteDiagnostic("SpinningCraneKick: found {0} group members to heal", countHeal);
                                return true;
                            }

                            if (HealerManager.AllowHealerDPS())
                            {
                                int countAttack = Unit.NearbyUnfriendlyUnits.Count(u => u.SpellDistance() <= 8);
                                if (countAttack >= MonkSettings.SpinningCraneKickCnt)
                                {
                                    Logger.WriteDiagnostic("SpinningCraneKick: found enemy {0} targets to attack", countAttack);
                                    return true;
                                }

                                float countMerge = countHeal + countAttack;
                                float avgCountNeeded = (0f + MonkSettings.SpinningCraneKickCnt + MonkSettings.MistHealSettings.CountSpinningCraneKickGroup) / 2f;
                                if (countMerge > avgCountNeeded)
                                {
                                    Logger.WriteDiagnostic("SpinningCraneKick: found combination of {0:F1} heal+attack targets, needed {1:F1}", countMerge, avgCountNeeded);
                                    return true;
                                }
                            }

                            return false;
                        }
                        )
                    );
            }

            behavs.AddBehavior(
                HealthToPriority(MonkSettings.MistHealSettings.SoothingMist),
                String.Format("Soothing Mist @ {0}%", MonkSettings.MistHealSettings.SoothingMist),
                "Soothing Mist",
                Spell.Cast(
                    "Soothing Mist",
                    mov => true,
                    on => (WoWUnit)on,
                    req => !Me.IsMoving && !IsChannelingSoothingMist((WoWUnit)req) && ((WoWUnit)req).PredictedHealthPercent() < MonkSettings.MistHealSettings.SoothingMist
                    )
                );

            behavs.AddBehavior(
                HealthToPriority(MonkSettings.MistHealSettings.EnvelopingMist),
                String.Format("Enveloping Mist @ {0}%", MonkSettings.MistHealSettings.EnvelopingMist),
                "Enveloping Mist",
                new Decorator(
                    req => Me.CurrentChi >= 3 && ((WoWUnit)req).HasAuraExpired("Enveloping Mist") && ((WoWUnit)req).PredictedHealthPercent() < MonkSettings.MistHealSettings.EnvelopingMist,
                    new Sequence(
                        EnsureSoothingMistOnTarget(on => on as WoWUnit),
                        Spell.Cast("Enveloping Mist", on => (WoWUnit)on)
                        )
                    )
                );

            // healing sphere
            behavs.AddBehavior(
                HealthToPriority(MonkSettings.MistHealSettings.HealingSphere),
                String.Format("Healing Sphere @ {0}%", MonkSettings.MistHealSettings.HealingSphere),
                "Healing Sphere",
                new Decorator(
                    req => Me.IsMoving && (req as WoWUnit).PredictedHealthPercent(includeMyHeals: true) < MonkSettings.MistHealSettings.HealingSphere,
                    new Sequence(
                        Spell.CastOnGround("Healing Sphere", on => (WoWUnit)on, req => true, false),
                        new Wait(TimeSpan.FromMilliseconds(350), until => Spell.GetPendingCursorSpell != null, new ActionAlwaysSucceed()),
                        new Action(r =>
                        {
                            Logger.WriteDebug("HealingSphere: /cancel Pending Spell {0}", Spell.GetPendingCursorSpell.Name);
                            Lua.DoString("SpellStopTargeting()");
                        })
                        )
                    )
                );

        }


        public static WoWUnit GetBestChiWaveTarget()
        {
            const int ChiWaveHopRange = 20;

            if (!Me.IsInGroup())
                return null;

            if (!Spell.CanCastHack("Chi Wave", Me, skipWowCheck: true))
            {
                Logger.WriteDebug("GetBestChiWaveTarget: CanCastHack says NO to Chi Wave");
                return null;
            }

            var targetInfo = HealerManager.Instance.TargetList
                .Select(p => new { Unit = p, Count = Clusters.GetClusterCount(p, ChiWavePlayers, ClusterType.Chained, ChiWaveHopRange) })
                .OrderByDescending(v => v.Count)
                .ThenByDescending(v => Group.Tanks.Any(t => t.Guid == v.Unit.Guid))
                .DefaultIfEmpty(null)
                .FirstOrDefault();

            WoWUnit target = targetInfo == null ? null : targetInfo.Unit;
            int count = targetInfo == null ? 0 : targetInfo.Count;

            // too few hops? then search any group member
            if (count < MonkSettings.MistHealSettings.CountChiWave)
            {
                target = Clusters.GetBestUnitForCluster(ChiWavePlayers, ClusterType.Chained, ChiWaveHopRange);
                if (target != null)
                {
                    count = Clusters.GetClusterCount(target, ChiWavePlayers, ClusterType.Chained, ChiWaveHopRange);
                    if (count < MonkSettings.MistHealSettings.CountChiWave)
                        target = null;
                }
            }

            if (target != null)
                Logger.WriteDebug("Chi Wave Target:  found {0} with {1} nearby under {2}%", target.SafeName(), count, MonkSettings.MistHealSettings.ChiWave);

            return target;
        }

        private static IEnumerable<WoWUnit> ChiWavePlayers
        {
            get
            {
                // TODO: Decide if we want to do this differently to ensure we take into account the T12 4pc bonus. (Not removing RT when using CH)
                return HealerManager.Instance.TargetList
                    .Where(u => u.IsAlive && u.DistanceSqr < 40 * 40 && u.PredictedHealthPercent() < MonkSettings.MistHealSettings.ChiWave)
                    .Select(u => (WoWUnit)u);
            }
        }

        private static Composite EnsureSoothingMistOnTarget(UnitSelectionDelegate onUnit)
        {
            return new PrioritySelector(
                ctx => onUnit(ctx),

                new Decorator(
                    req => IsChannelingSoothingMist(req as WoWUnit),
                    new Action( r => Logger.WriteDiagnostic("EnsureSmoothingMist: already being cast on {0}", (r as WoWUnit).SafeName()))
                    ),

                new Sequence(
                    new Action( r => Logger.WriteDebug("EnsureSmoothingMist: about to cast Soothing Mist on {0}", (r as WoWUnit).SafeName())),
                    Spell.Cast("Soothing Mist", mov => true, on => on as WoWUnit, req => !Me.IsMoving),
                    new Action(r => Logger.WriteDebug("EnsureSmoothingMist: wait until channeling Soothing Mist active")),
                    new Wait(TimeSpan.FromMilliseconds(500), until => IsChannelingSoothingMist(), new ActionAlwaysSucceed()),
                    new Action(r => Logger.WriteDebug("EnsureSmoothingMist: wait until GCD completes")),
                    new Wait(TimeSpan.FromMilliseconds(1500), until => !Spell.IsGlobalCooldown(), new ActionAlwaysSucceed()),
                    new Action(r => Logger.WriteDebug("EnsureSmoothingMist: is now on {0}", (r as WoWUnit).SafeName()))
                    )
                );
        }

        private static Composite CreateMistweaverRollRenewingMist()
        {
            return new Decorator(
                req => !Spell.IsSpellOnCooldown("Renewing Mist"),
                new PrioritySelector(
                    ctx => GetBestRenewingMistTankTarget(),
                    new Decorator(
                        req =>
                        {
                            if (req != null)
                            {
                                Logger.WriteDebug("RenewingMistTarget:  tank {0} needs Renewing Mist", (req as WoWUnit).SafeName());
                                return true;
                            }

                            int rollCount = HealerManager.Instance.TargetList.Count(u => u.IsAlive && u.HasMyAura("Renewing Mist") && u.SpellDistance() < 40);
                            if (rollCount < MonkSettings.MistHealSettings.RollRenewingMistCount)
                            {
                                Logger.WriteDebug("RenewingMistTarget:  currently {0} have my Renewing Mist, need {1}", rollCount, MonkSettings.MistHealSettings.RollRenewingMistCount);
                                return true;
                            }
                            return false;
                        },
                        Spell.Cast(
                            "Renewing Mist",
                            on =>
                            {
                                WoWUnit unit = (on as WoWUnit) ?? GetBestRenewingMistTarget();
                                if (unit != null)
                                    Logger.WriteDebug(Color.White, "ROLLING Renewing Mist on: {0}", unit.SafeName());
                                return unit;
                            }
                            )
                        )
                    )
                );
        }

        private static object CreateMistweaverTankRenewingMist()
        {
            return new Decorator(
                ret =>
                {
                    int rollCount = HealerManager.Instance.TargetList.Count(u => u.IsAlive && u.HasMyAura("Renewing Mist"));
                    Logger.WriteDebug("GetBestRiptideTarget:  currently {0} have my Renewing Mist, need {1}", rollCount, MonkSettings.MistHealSettings.RollRenewingMistCount);
                    return rollCount < MonkSettings.MistHealSettings.RollRenewingMistCount;
                },
                Spell.Cast("Riptide", on =>
                {
                    // if tank needs Riptide, bail out on Rolling as they have priority
                    WoWUnit unit = GetBestRenewingMistTankTarget();
                    if (unit != null)
                        Logger.WriteDebug(Color.White, "ROLLING Renewing Mist on: {0}", unit.SafeName());

                    return unit;
                })
                );
        }

        private static WoWUnit GetBestRenewingMistTankTarget()
        {
            WoWUnit rewnewTarget = null;
            rewnewTarget = Group.Tanks
                .Where(u => u.IsAlive && u.Combat && u.DistanceSqr < 40 * 40 && !u.HasMyAura("Renewing Mist") && u.InLineOfSpellSight)
                .OrderBy(u => u.HealthPercent)
                .FirstOrDefault();

            if (rewnewTarget != null)
                Logger.WriteDebug("GetBestRenewingMistTank: found tank {0}, hasmyaura={1} with {2} ms left", rewnewTarget.SafeName(), rewnewTarget.HasMyAura("Riptide"), (int)rewnewTarget.GetAuraTimeLeft("Riptide").TotalMilliseconds);
            return rewnewTarget;
        }

        private static WoWUnit GetBestRenewingMistTarget()
        {
            int distHop = glyphedRenewingMist ? 40 : 20;
            WoWUnit ripTarget = Clusters.GetBestUnitForCluster(NonRenewingMistPlayers, ClusterType.Chained, distHop);
            if (ripTarget != null)
                Logger.WriteDebug("GetBestRenewingMistTarget: found optimal target {0}, hasmyaura={1} with {2} ms left", ripTarget.SafeName(), ripTarget.HasMyAura("Riptide"), (int)ripTarget.GetAuraTimeLeft("Riptide").TotalMilliseconds);

            return ripTarget;
        }

        private static IEnumerable<WoWUnit> NonRenewingMistPlayers
        {
            get
            {
                return HealerManager.Instance.TargetList
                    .Where(u => u.IsAlive && !u.HasMyAura("Renewing Mist") && u.DistanceSqr < 40 * 40 && u.PredictedHealthPercent() < MonkSettings.MistHealSettings.RenewingMist)
                    .Select(u => (WoWUnit)u);
            }
        }


        public static int HealthToPriority(int nHealth)
        {
            return nHealth == 0 ? 0 : 200 - nHealth;
        }


        #region Mistweaver Helpers

        private static Composite CreateMistweaverWaitForCast()
        {
            return new Sequence(

                // fall out of sequence if not casting or channelling
                new DecoratorContinue(
                    req => !Spell.IsCastingOrChannelling(),
                    new ActionAlwaysFail()
                    ),

                // cancel channel if Soothing Mist and its our heal target and they are healed up
                new DecoratorContinue(
                    req => Me.ChanneledCastingSpellId == SOOTHING_MIST,
                    new Sequence(
                        ctx => Me.ChannelObject,

                        new DecoratorContinue(
                            req => req != null,
                            new Sequence(

                                // output message at most once per second
                                new Decorator(
                                    req => SingularSettings.Debug,
                                    new ThrottlePasses(1, TimeSpan.FromSeconds(1), RunStatus.Success,
                                        new Action(u => Logger.WriteDebug(System.Drawing.Color.White, "MonkWaitForCast: {0} on {1} @ {2:F1}", "Soothing Mist", (u as WoWUnit).SafeName(), (u as WoWUnit).HealthPercent))
                                        )
                                    ),

                                // check if target healed and we need to cancel
                                new DecoratorContinue(
                                    req => (req as WoWUnit).HealthPercent > 99,
                                    new Sequence(
                                        new Action(u => Logger.Write(System.Drawing.Color.OrangeRed, "/cancel: cancel {0} on {1} @ {2:F1}", Me.ChanneledSpell.Name, (u as WoWUnit).SafeName(), (u as WoWUnit).HealthPercent)),
                                        new Action(r => SpellManager.StopCasting()),

                                        // wait for channel to actual stop
                                        new Wait(
                                            TimeSpan.FromMilliseconds(500),
                                            until => !Spell.IsCastingOrChannelling(),
                                            new ActionAlwaysFail()
                                            )
                                        )
                                    ),

                                // check if higher priority target with lower health needs us, so we cancel
                                // NOTE: all health checks here should be HealthPercent, not GetPredictedHealthPercent()
                                new DecoratorContinue(
                                    req => {
                                        bool surgmistTargetNotPriority = false;
                                        string surgmistTargetMessage = "";
                                        // if channel target out of danger and not a healer/tank, check if a healer/tank needs saving heal
                                        WoWPartyMember pm = Me.GroupInfo.RaidMembers.FirstOrDefault( p => p.Guid == (req as WoWUnit).Guid);
                                        if ( pm == null)
                                        {
                                            surgmistTargetNotPriority = true;
                                            surgmistTargetMessage = "MistweaverWaitForCast: surging mist target {0} not a Raid Member";
                                        }
                                        else if ( !pm.HasRole(WoWPartyMember.GroupRole.Tank) && !pm.HasRole(WoWPartyMember.GroupRole.Healer))
                                        {
                                            surgmistTargetNotPriority = true;
                                            surgmistTargetMessage = "MistweaverWaitForCast: surging mist target {0} not a Tank or Healer";
                                        }
                                        else if ( (req as WoWUnit).HealthPercent > HealerManager.EmergencyHealOutOfDanger)
                                        {
                                            surgmistTargetNotPriority = true;
                                            surgmistTargetMessage = "MistweaverWaitForCast: surging mist target {0} @ {1:F1}%";
                                        }
                                        
                                        if (surgmistTargetNotPriority)
                                        {
                                            WoWUnit cancelFor = Group.Tanks.FirstOrDefault(t => t.IsAlive && t.HealthPercent < HealerManager.EmergencyHealPercent && t.SpellDistance() < 40 && t.InLineOfSpellSight);
                                            if (cancelFor == null)
                                                cancelFor = Group.Healers.FirstOrDefault(h => h.IsAlive && h.HealthPercent < HealerManager.EmergencyHealPercent && h.SpellDistance() < 40 && h.InLineOfSpellSight);
                                            if (cancelFor != null)
                                            {
                                                Logger.WriteDiagnostic(surgmistTargetMessage, (req as WoWUnit).SafeName(), (req as WoWUnit).HealthPercent);
                                                if (req == null)
                                                    Logger.Write(System.Drawing.Color.OrangeRed, "/cancel: {0} because {1} @ {2:F1}% needs saving heal", Me.ChanneledSpell.Name, cancelFor.SafeName(), cancelFor.HealthPercent);
                                                else
                                                    Logger.Write(System.Drawing.Color.OrangeRed, "/cancel: {0} on {1} @ {2:F1}% because {3} @ {4:F1}% needs saving heal", Me.ChanneledSpell.Name, (req as WoWUnit).SafeName(), (req as WoWUnit).HealthPercent, cancelFor.SafeName(), cancelFor.HealthPercent);

                                                SpellManager.StopCasting();
                                                HealerManager.SavingHealUnit = cancelFor;
                                                return true;
                                            }
                                        }

                                        return false;
                                        },
                                    new Wait(
                                        TimeSpan.FromMilliseconds(350),
                                        until => !Spell.IsCastingOrChannelling(),
                                        new ActionAlwaysFail()
                                        )
                                    )
                                )
                            )
                        )
                    ),

                // otherwise allow success here
                new ActionAlwaysSucceed()
                );
        }

        private static bool IsChannelingSoothingMist(WoWUnit target = null)
        {
            if (Me.ChanneledCastingSpellId == SOOTHING_MIST)
            {
                if (!Spell.IsChannelling())
                    return false;

                if (target == null)
                    return true;

                return Me.ChannelObjectGuid == target.Guid;
            }

            return false;
        }

        public static Composite CreateMistweaverMoveToEnemyTarget()
        {
            return new PrioritySelector(
                new Decorator(
                    req => RangedAttacks,
                    Helpers.Common.EnsureReadyToAttackFromLongRange()
                    ),
                new Decorator(
                    req => !RangedAttacks,
                    Helpers.Common.EnsureReadyToAttackFromMelee()
                    )
                );
        }



        private static Composite CreateManaTeaBehavior()
        {

            return new PrioritySelector(

                ctx => MyHealTarget,
                
                Spell.Cast(
                    "Mana Tea",
                    mov => !glyphedManaTea,
                    on => Me,
                    req =>  {
                        if (!Me.HasAura("Stance of the Wise Serpent"))
                            return false;

                        uint stacks = Me.GetAuraStacks("Mana Tea");
                        if (stacks == 0)
                            return false;

                        if (glyphedManaTea)
                            return stacks >= 2 && Me.ManaPercent < MonkSettings.ManaTeaPercentInstant;

                        return Me.ManaPercent < MonkSettings.ManaTeaPercent;
                        },
                    cancel => Me.ManaPercent > 90
                        || (Me.ManaPercent > 7 && cancel != null && (cancel as WoWUnit).HealthPercent >= HealerManager.EmergencyHealOutOfDanger)
                    )
                );
        }


        /// <summary>
        /// selects best Life Cocoon target
        /// </summary>
        /// <returns></returns>
        private static WoWUnit GetBestLifeCocoonTargetBattlegrounds()
        {
#if SUPPORT_MOST_IN_NEED_LifeCocoon_LOGIC
            var target = Unit.NearbyGroupMembers
                .Where(u => IsValidLifeCocoonTarget(u.ToUnit()))
                .Select(u => new { Unit = u, Health = u.HealthPercent, EnemyCount = Unit.NearbyUnfriendlyUnits.Count(e => e.CurrentTargetGuid == u.Guid)})
                .OrderByDescending(v => v.EnemyCount)
                .ThenBy(v => v.Health )
                .FirstOrDefault();

            if (target == null)
            {
                guidLastLifeCocoon = Me.Guid;
            }
            else if (guidLastLifeCocoon != target.Unit.Guid)
            {
                guidLastLifeCocoon = target.Unit.Guid;
                Logger.WriteDebug("Best Life Cocoon Target appears to be: {0} @ {1:F0}% with {2} attackers", target.Unit.SafeName(), target.Health, target.EnemyCount);
            }

            return target == null ? Me : target.Unit;
#else
            return Me;
#endif
        }

        private static bool IsValidLifeCocoonTarget(WoWUnit unit)
        {
            if (unit == null || !unit.IsValid || !unit.IsAlive || !Unit.GroupMembers.Any(g => g.Guid == unit.Guid) || unit.Distance > 99)
                return false;

            return unit.HasMyAura("Life Cocoon") || !unit.HasAnyAura("Life Cocoon", "Water Shield", "Lightning Shield");
        }

        private static WoWUnit _targetHeal;

        public static Composite CreateMistweaverMonkHealingOriginal(bool selfOnly)
        {
#if OLD
            HealerManager.NeedHealTargeting = true;

            return new PrioritySelector(

                ctx => _targetHeal = ChooseBestMonkHealTarget(selfOnly ? Me : HealerManager.Instance.FirstUnit),

                // if channeling soothing mist and they aren't best target, cancel the cast
                CreateMonkWaitForCast(),

                new Decorator(ret => !Spell.GcdActive,

                    new PrioritySelector(

                        new Decorator(ret => _targetHeal != null && _targetHeal.PredictedHealthPercent() <= SingularSettings.Instance.IgnoreHealTargetsAboveHealth,
                
                            new PrioritySelector(

                                CreateMistWeaverDiagnosticOutputBehavior(on => _targetHeal),

                                ShowHealTarget(ret => _targetHeal),

                                Spell.BuffSelf("Stance of the Wise Serpent"),

                                Helpers.Common.CreateInterruptBehavior(),

                                Common.CastLikeMonk("Fortifying Brew", ret => Me, ret => Me.HealthPercent < 40),

                                Common.CastLikeMonk("Mana Tea", ret => Me, ret => Me.ManaPercent < 60 && Me.HasAura("Mana Tea", 2)),

                                Common.CastLikeMonk("Surging Mist", ret => _targetHeal,
                                    ret => _targetHeal.HealthPercent < 30 && Me.HasAura("Vital Mists", 5)),

                                Common.CastLikeMonk("Soothing Mist", ret => _targetHeal,
                                    ret => _targetHeal.HealthPercent < 90 && Me.ChanneledSpell == null),

                                Common.CastLikeMonk("Surging Mist", ret => _targetHeal,
                                    ret => _targetHeal.HealthPercent < 60),

                                Common.CastLikeMonk("Enveloping Mist", ret => _targetHeal,
                                    ret => _targetHeal.HealthPercent < 89 && Me.CurrentChi >= 3)
                                )
                            )
                        )
                    ),

                new Decorator(
                    ret => !Spell.IsCastingOrChannelling(),
                    Movement.CreateMoveToUnitBehavior( ret => _targetHeal, 38f)
                    )
                );
#else
            return CreateMistweaverMonkHealing(selfOnly);
#endif
        }

        private static ulong guidLastHealTarget = 0;
        private static Composite ShowHealTarget(UnitSelectionDelegate onUnit)
        {
            return
                new Sequence(
                    new DecoratorContinue(
                        ret => onUnit(ret) == null && guidLastHealTarget != 0,
                        new Action(ret =>
                        {
                            guidLastHealTarget = 0;
                            Logger.WriteDebug(Color.LightGreen, "Heal Target - none");
                            return RunStatus.Failure;
                        })
                        ),

                    new DecoratorContinue(
                        ret => onUnit(ret) != null && guidLastHealTarget != onUnit(ret).Guid,
                        new Action(ret =>
                        {
                            guidLastHealTarget = onUnit(ret).Guid;
                            Logger.WriteDebug(Color.LightGreen, "Heal Target - {0} {1:F1}% @ {2:F1} yds", onUnit(ret).SafeName(), onUnit(ret).HealthPercent, onUnit(ret).Distance);
                            return RunStatus.Failure;
                        })),

                    new Action(ret => { return RunStatus.Failure; })
                    );
        }

        private static WoWUnit ChooseBestMonkHealTarget(WoWUnit unit)
        {
            if (Me.ChanneledCastingSpellId == SOOTHING_MIST)
            {
                WoWObject channelObj = Me.ChannelObject;
                if (channelObj != null)
                {
                    WoWUnit channelUnit = Me.ChannelObject.ToUnit();
                    if (channelUnit.HealthPercent <= SingularSettings.Instance.IgnoreHealTargetsAboveHealth)
                        return channelUnit;
                }
            }

            if (unit != null && unit.HealthPercent > SingularSettings.Instance.IgnoreHealTargetsAboveHealth)
                unit = null;

            return unit;
        }

        #endregion

        #region Diagnostics

        private static Composite compret;

        private static Composite CreateMistWeaverDiagnosticOutputBehavior(UnitSelectionDelegate onUnit)
        {
            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            if (compret == null)
            {
                compret = new ThrottlePasses(
                    1,
                    TimeSpan.FromSeconds(1),
                    RunStatus.Failure,
                    new Action(ret =>
                    {
                        WoWUnit target = Me.CurrentTarget;

                        string line = "...";
                        line += string.Format(" h={0:F1}%/m={1:F1}%/c={2},move={3},combat={4},tigerpwr={5},mtea={6},muscle={7},statue={8:F1} yds",
                            Me.HealthPercent,
                            Me.ManaPercent,
                            Me.CurrentChi,
                            Me.IsMoving.ToYN(),
                            Me.Combat.ToYN(),
                            (long)target.GetAuraTimeLeft("Tiger Power").TotalMilliseconds,
                            Me.GetAuraStacks("Mana Tea"),
                            (long)target.GetAuraTimeLeft("Muscle Memory").TotalMilliseconds,
                            (FindStatue() ?? Me).Distance
                            );

                        WoWUnit healTarg = onUnit(ret);
                        if (Me.IsInGroup() || (Me.FocusedUnitGuid != 0 && healTarg == Me.FocusedUnit))
                        {
                            if (healTarg == null)
                                line += ",heal=(null)";
                            else if (!healTarg.IsValid)
                                line += ",heal=(invalid)";
                            else
                            {
                                float hh = (float) healTarg.HealthPercent;
                                float hph= healTarg.PredictedHealthPercent();
                                line += string.Format(",vmist={0},zeal={1},heal={2} {3:F1}% @ {4:F1} yds,hph={5:F1}%,hcombat={6},tloss={7}",
                                    Me.GetAuraStacks("Vital Mists"),
                                    (long)Me.GetAuraTimeLeft("Serpent's Zeal").TotalMilliseconds,
                                    healTarg.SafeName(),
                                    hh,
                                    healTarg.SpellDistance(),
                                    hph,
                                    healTarg.Combat.ToYN(),
                                    healTarg.InLineOfSpellSight.ToYN()
                                    );
                                if (hph > 100)
                                    line += ",Error=GetPredictedHealth > 100";
                                else if (hph < 0)
                                    line += ",Error=GetPredictedHealth < 0";
                            }

                            if (SingularSettings.Instance.StayNearTank)
                            {
                                WoWUnit tank = HealerManager.TankToStayNear;
                                if (tank == null)
                                    line += ",tank=(null)";
                                else if (!tank.IsAlive)
                                    line += ",tank=(dead)";
                                else
                                {
                                    float hh = (float)tank.HealthPercent;
                                    float hph = tank.PredictedHealthPercent();
                                    line += string.Format(",tank={0} {1:F1}% @ {2:F1} yds,tph={3:F1}%,tcombat={4},tmove={5},tloss={6},tstatue={7:F1} yds",
                                        tank.SafeName(),
                                        hh,
                                        tank.SpellDistance(),
                                        hph,
                                        tank.Combat.ToYN(),
                                        tank.IsMoving.ToYN(),
                                        tank.InLineOfSpellSight.ToYN(),
                                        tank.Location.Distance((FindStatue() ?? tank).Location)
                                        );
                                }
                            }
                        }

                        if (target == null)
                            line += ", target=(null)";
                        else if (!target.IsValid)
                            line += ", target=(invalid)";
                        else
                            line += string.Format(", target={0} {1:F1}% @ {2:F1} yds, face={3} tloss={4}, gw={5}",
                                target.SafeName(),
                                target.HealthPercent,
                                target.Distance,
                                Me.IsSafelyFacing(target).ToYN(),
                                target.InLineOfSpellSight.ToYN(),
                                (long)target.GetAuraTimeLeft("Grapple Weapon").TotalMilliseconds
                                );

                        Logger.WriteDebug(Color.Yellow, line);
                        return RunStatus.Failure;
                    }));
            }

            return compret;
        }

        #endregion


    }

}