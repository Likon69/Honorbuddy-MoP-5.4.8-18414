using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx.CommonBot;
using Styx.Helpers;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using Styx;
using Styx.Common;
using System;
using System.Collections.Generic;
using System.Drawing;
using Styx.CommonBot.Routines;

namespace Singular.ClassSpecific.Mage
{
    public static class Common
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static MageSettings MageSettings { get { return SingularSettings.Instance.Mage(); } }

        private static DateTime _cancelIceBlockForCauterize = DateTime.MinValue;
        private static WoWPoint locLastFrostArmor = WoWPoint.Empty;
        private static WoWPoint locLastIceBarrier = WoWPoint.Empty;

        public static bool TreatAsFrozen(this WoWUnit unit)
        {
            return Me.HasAura("Brain Freeze") || unit.IsFrozen();
        }

        public static bool IsFrozen(this WoWUnit unit)
        {
            return unit.GetAllAuras().Any(a => a.Spell.Mechanic == WoWSpellMechanic.Frozen || (a.Spell.School == WoWSpellSchool.Frost && a.Spell.SpellEffects.Any(e => e.AuraType == WoWApplyAuraType.ModRoot)));
        }

        [Behavior(BehaviorType.Initialize, WoWClass.Mage)]
        public static Composite CreateMageInitialize()
        {
            if (SingularRoutine.CurrentWoWContext == WoWContext.Normal || SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                Kite.CreateKitingBehavior(null, null, null);

            return null;
        }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Mage)]
        public static Composite CreateMagePreCombatBuffs()
        {
            return new PrioritySelector(
                Spell.WaitForCastOrChannel(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        // Defensive 
                        Spell.BuffSelf("Slow Fall", req => MageSettings.UseSlowFall && Me.IsFalling),

                        PartyBuff.BuffGroup("Dalaran Brilliance", "Arcane Brilliance"),
                        PartyBuff.BuffGroup("Arcane Brilliance", "Dalaran Brilliance"),

                        // Additional armors/barriers for BGs. These should be kept up at all times to ensure we're as survivable as possible.
                        /*
                        new Decorator(
                            ret => SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds,
                            new PrioritySelector(
                                // Don't put up mana shield if we're arcane. Since our mastery works off of how much mana we have!
                                Spell.BuffSelf("Mana Shield", ret => TalentManager.CurrentSpec != WoWSpec.MageArcane)
                                )
                            ),
                        */
                        CreateMageArmorBehavior(),
/*
                        new PrioritySelector(
                            ctx => MageTable,
                            new Decorator(
                                ctx => ctx != null && CarriedMageFoodCount < 60 && StyxWoW.Me.FreeNormalBagSlots > 1,
                                new Sequence(
                                    new Action(ctx => Logger.Write(Color.White, "^Getting Mage food")),
                // Move to the Mage table
                                    new DecoratorContinue(
                                        ctx => ((WoWGameObject)ctx).DistanceSqr > 5 * 5,
                                        new Action(ctx => Navigator.GetRunStatusFromMoveResult(Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(StyxWoW.Me.Location, ((WoWGameObject)ctx).Location, 5))))
                                        ),
                // interact with the mage table
                                    new Action(ctx => ((WoWGameObject)ctx).Interact()),
                                    new WaitContinue(2, ctx => false, new ActionAlwaysSucceed())
                                    )
                                )
                            ),
*/
                        new ThrottlePasses(
                            1, TimeSpan.FromSeconds(10),
                            RunStatus.Failure,
                            new Decorator(
                                ctx => ShouldSummonTable && NeedTableForBattleground,
                                new Sequence(
                                    new Action(ctx => Logger.Write(Color.White, "^Conjure Refreshment Table")),
                                    Spell.Cast("Conjure Refreshment Table", mov => true, on => Me, req => true, cancel => false, LagTolerance.No ),
                                    // new Action(ctx => SpellManager.Cast("Conjure Refreshment Table")),
                                    new WaitContinue(4, ctx => !StyxWoW.Me.IsCasting, new ActionAlwaysSucceed())
                                    )
                                )
                            ),

                        Spell.BuffSelf("Conjure Refreshment", ret => !Gotfood && !ShouldSummonTable),
                        Spell.BuffSelf("Conjure Mana Gem", ret => !HaveManaGem )
/*
                        new Throttle( 1,
                            new Decorator(ret => !HaveManaGem && Spell.CanCastHack("Conjure Mana Gem"),
                                new Sequence(
                                    new Action(ret => Logger.Write("*Conjure Mana Gem")),
                                    new Action(ret => SpellManager.Cast(759))
                                    )
                                )
                            )
*/ 
                        )
                    )
                );
        }

        [Behavior(BehaviorType.LossOfControl, WoWClass.Mage)]
        public static Composite CreateMageLossOfControlBehavior()
        {
            return new PrioritySelector(

                // deal with Ice Block here (a stun of our own doing)
                new Decorator(
                    ret => Me.ActiveAuras.ContainsKey("Ice Block"),
                    new PrioritySelector(
                        new Throttle(10, new Action(r => Logger.Write(Color.DodgerBlue, "^Ice Block for 10 secs"))),
                        new Decorator(
                            ret => DateTime.Now < _cancelIceBlockForCauterize && !Me.ActiveAuras.ContainsKey("Cauterize"),
                            new Action(ret => {
                                Logger.Write(Color.White, "/cancel Ice Block since Cauterize has expired");
                                _cancelIceBlockForCauterize = DateTime.MinValue ;
                                // Me.GetAuraByName("Ice Block").TryCancelAura();
                                Me.CancelAura("Ice Block");
                                return RunStatus.Success;
                                })
                            ),
                        new ActionIdle()
                        )
                    ),

                // Spell.BuffSelf("Blink", ret => MovementManager.IsClassMovementAllowed && Me.Stunned && !TalentManager.HasGlyph("Rapid Displacement")),
                Spell.BuffSelf("Temporal Shield", ret => Me.Stunned)
                );
        }

        /// <summary>
        /// PullBuffs that must be called only when in Pull and in range of target
        /// </summary>
        /// <returns></returns>
        public static Composite CreateMagePullBuffs()
        {
            return new Decorator(
                req => Me.GotTarget && Me.CurrentTarget.SpellDistance() < 40,
                new PrioritySelector(
                    CreateMageRuneOfPowerBehavior(),
                    CreateMageInvocationBehavior()
                    )
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Mage)]
        public static Composite CreateMageCombatBuffs()
        {
            return new Decorator(
                req => !Unit.IsTrivial(Me.CurrentTarget),
                new PrioritySelector(
                    // Defensive 
                    Spell.BuffSelf( "Slow Fall", req => MageSettings.UseSlowFall && Me.IsFalling ),
                        
                    // handle Cauterize debuff if we took talent and get it
                    new Decorator(
                        ret => Me.ActiveAuras.ContainsKey("Cauterize"),
                        new PrioritySelector(
                            Spell.BuffSelf("Ice Block",
                                ret => { 
                                    _cancelIceBlockForCauterize = DateTime.Now.AddSeconds(10);
                                    return true;
                                }),

                            Spell.BuffSelf("Temporal Shield"),
                            Spell.BuffSelf("Ice Barrier"),
                            Spell.BuffSelf("Incanter's Ward"),

                            new Throttle( 8, Item.CreateUsePotionAndHealthstone(100, 0))
                            )
                        ),

                    // Ice Block cast if we didn't take Cauterize
                    Spell.BuffSelf("Ice Block",
                        ret => SingularRoutine.CurrentWoWContext != WoWContext.Instances
                            && !SpellManager.HasSpell("Cauterize")
                            && StyxWoW.Me.HealthPercent < 20
                            && !StyxWoW.Me.ActiveAuras.ContainsKey("Hypothermia")),

                    Spell.BuffSelf("Incanter's Ward", req => Unit.NearbyUnitsInCombatWithMeOrMyStuff.Any()),
                    Spell.BuffSelf("Ice Ward"),

                    // cast Evocation for Buff
                    CreateMagePullBuffs(),

                    // cast Evocation for Heal or Mana
                    new Throttle( 3, Spell.Cast("Evocation", mov => true, on => Me, ret => NeedEvocation, cancel => false) ),
                    // new Wait( 1, until => !HasTalent(MageTalents.Invocation) || Me.HasAura("Invoker's Energy"), new ActionAlwaysSucceed())

                    Dispelling.CreatePurgeEnemyBehavior("Spellsteal"),
                    // CreateMageSpellstealBehavior(),

                    Spell.Cast("Ice Barrier", on => Me, ret => Me.HasAuraExpired("Ice Barrier", 2)),

                    Spell.Buff("Nether Tempest", true, on => Me.CurrentTarget, req => true, 1),
                    Spell.Buff("Living Bomb", true, on => Me.CurrentTarget, req => true, 0),
                    Spell.Buff("Frost Bomb", true, on => Me.CurrentTarget, req => true, 0),

                    // Spell.Cast("Alter Time", ret => StyxWoW.Me.HasAura("Icy Veins") && StyxWoW.Me.HasAura("Brain Freeze") && StyxWoW.Me.HasAura("Fingers of Frost") && StyxWoW.Me.HasAura("Invoker's Energy")),

                    Spell.Cast("Mirror Image", 
                         req => Me.GotTarget &&  (Me.CurrentTarget.IsBoss() || (Me.CurrentTarget.Elite && SingularRoutine.CurrentWoWContext != WoWContext.Instances) || Me.CurrentTarget.IsPlayer || Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() >= 3)),

                    Spell.BuffSelf("Time Warp", ret => MageSettings.UseTimeWarp && NeedToTimeWarp),

                    Common.CreateUseManaGemBehavior(ret => Me.ManaPercent < (SingularRoutine.CurrentWoWContext == WoWContext.Instances ? 20 : 80)),
                
                    // , Spell.BuffSelf( "Ice Floes", req => Me.IsMoving)

                    CreateHealWaterElemental()
                    )
                );
        }

        public static Composite CreateHealWaterElemental()
        {
            return Spell.Cast("Frostbolt", mov => true, on => Me.Pet,
                req => 
                {
                    if (Me.Pet != null && Me.Pet.IsAlive)
                    {
                        if (Me.Pet.PredictedHealthPercent(true) < MageSettings.HealWaterElementalPct)
                        {
                            if (Spell.CanCastHack("Frostbolt", Me.Pet, false))
                            {
                                Logger.Write(Color.White, "^Heal Water Elemental: currently at {0:F1}%", Me.Pet.HealthPercent);
                                return true;
                            }
                        }
                    }
                    return false;
                },
                cancel => Me.Pet == null || !Me.Pet.IsAlive || Me.Pet.PredictedHealthPercent(false) >= 100
                )
            ;
        }

        private static readonly uint[] MageFoodIds = new uint[]
                                                         {
                                                             65500,
                                                             65515,
                                                             65516,
                                                             65517,
                                                             43518,
                                                             43523,
                                                             65499, //Conjured Mana Cake - Pre Cata Level 85
                                                             80610, //Conjured Mana Pudding - MoP Lvl 85+
                                                             80618  //Conjured Mana Buns 
                                                             //This is where i made a change.
                                                         };

        private const uint ArcanePowder = 17020;

        /// <summary>
        /// True if config allows conjuring tables, we have the spell, are not moving, group members
        /// are within 15 yds, and no table within 40 yds
        /// </summary>
        private static bool ShouldSummonTable
        {
            get
            {
                return MageSettings.SummonTableIfInParty 
                    && SpellManager.HasSpell("Conjure Refreshment Table") 
                    && !StyxWoW.Me.IsMoving
                    && MageTable == null
                    && Unit.GroupMembers.Any(p => !p.IsMe && p.DistanceSqr < 15 * 15);
            }
        }

       static readonly Dictionary<uint, uint> RefreshmentTableIds = new Dictionary<uint,uint>() 
                                         {
                                             { 186812, 70 }, //Level 70
                                             { 207386, 80 }, //Level 80
                                             { 207387, 85 }, //Level 85
                                             { 211363, 90 }, //Level 90
                                         };

        /// <summary>
        /// finds a level appropriate Mage Table if one exists.
        /// </summary>
        static public WoWGameObject MageTable
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWGameObject>()
                        .Where(
                            i => RefreshmentTableIds.ContainsKey(i.Entry) 
                                && RefreshmentTableIds[i.Entry] <= Me.Level 
                                && (StyxWoW.Me.PartyMembers.Any(p => p.Guid == i.CreatedByGuid) || StyxWoW.Me.Guid == i.CreatedByGuid)
                                && i.Distance <= SingularSettings.Instance.TableDistance
                            )
                        .OrderByDescending( t => t.Level )
                        .FirstOrDefault();
            }
        }

        public static int CarriedMageFoodCount
        {
            get
            {

                return (int)StyxWoW.Me.CarriedItems.Sum(i => i != null
                                                      && i.ItemInfo != null
                                                      && i.ItemInfo.ItemClass == WoWItemClass.Consumable
                                                      && i.ItemSpells != null
                                                      && i.ItemSpells.Count > 0
                                                      && i.ItemSpells[0].ActualSpell.Name.Contains("Refreshment")
                                                          ? i.StackCount
                                                          : 0);
            }
        }
        
   
        public static bool Gotfood { get { return StyxWoW.Me.BagItems.Any(item => MageFoodIds.Contains(item.Entry)); } }

        private static bool HaveManaGem { get { return StyxWoW.Me.BagItems.Any(i => i.Entry == 36799 || i.Entry == 81901); } }

        public static Composite CreateUseManaGemBehavior() { return CreateUseManaGemBehavior(ret => true); }

        public static Composite CreateUseManaGemBehavior(SimpleBooleanDelegate requirements)
        {
            return new Throttle( 2, 
                new PrioritySelector(
                    ctx => StyxWoW.Me.BagItems.FirstOrDefault(i => i.Entry == 36799 || i.Entry == 81901),
                    new Decorator(
                        ret => ret != null && StyxWoW.Me.ManaPercent < 100 && ((WoWItem)ret).Cooldown == 0 && requirements(ret),
                        new Sequence(
                            new Action(ret => Logger.Write("Using {0}", ((WoWItem)ret).Name)),
                            new Action(ret => ((WoWItem)ret).Use())
                            )
                        )
                    )
                );
        }

        public static Composite CreateStayAwayFromFrozenTargetsBehavior()
        {
#if NOPE
            return new PrioritySelector(
                ctx => Unit.NearbyUnfriendlyUnits
                           .Where( u => u.IsFrozen() && Me.SpellDistance(u) < 8)
                           .OrderBy(u => u.DistanceSqr).FirstOrDefault(),
                new Decorator(
                    ret => ret != null && MovementManager.IsClassMovementAllowed,
                    new PrioritySelector(
                        new Decorator(
                            ret => Spell.GetSpellCooldown("Blink").TotalSeconds > 0
                                && Spell.GetSpellCooldown("Rocket Jump").TotalSeconds > 0,
                            new Action(
                                ret =>
                                {
                                    if (Me.IsMoving && StopMoving.Type == StopMoving.StopType.Location)
                                    {
                                        Logger.WriteDebug(Color.LightBlue, "StayAwayFromFrozen:  looks like we are already moving away");
                                        return RunStatus.Success;
                                    }

                                    WoWPoint moveTo = WoWMathHelper.CalculatePointBehind(
                                        ((WoWUnit)ret).Location,
                                        ((WoWUnit)ret).Rotation,
                                        -Me.SpellRange(12f, (WoWUnit) ret)
                                        );

                                    if (!Navigator.CanNavigateFully(StyxWoW.Me.Location, moveTo))
                                    {
                                        Logger.WriteDebug(Color.LightBlue, "StayAwayFromFrozen:  unable to navigate to point behind me {0:F1} yds away", StyxWoW.Me.Location.Distance(moveTo));
                                        return RunStatus.Failure;
                                    }

                                    Logger.Write( Color.LightBlue, "Getting away from frozen target {0}", ((WoWUnit)ret).SafeName());
                                    Navigator.MoveTo(moveTo);
                                    StopMoving.AtLocation(moveTo);
                                    return RunStatus.Success;
                                })
                            ),

                        new Decorator(
                            ret => !Me.IsMoving,
                            new PrioritySelector(
                                Disengage.CreateDisengageBehavior("Blink", Disengage.Direction.Frontwards, 20, null),
                                Disengage.CreateDisengageBehavior("Rocket Jump", Disengage.Direction.Frontwards, 20, null)
                                )
                            )
                        )
                    )
                );
             */
#else
            Composite slowBehave = CreateSlowMeleeBehavior();
            return Avoidance.CreateAvoidanceBehavior(
                "Blink", 
                TalentManager.HasGlyph("Blink") ? 28 : 20, 
                Disengage.Direction.Frontwards, 
                slowBehave
                );
#endif
        }

        /*
        public static Composite CreateMageSpellstealBehavior()
        {
            return Spell.Cast("Spellsteal", 
                mov => false, 
                on => {
                    WoWUnit unit = GetSpellstealTarget();
                    if (unit != null)
                        Logger.WriteDebug("Spellsteal:  found {0} with a triggering aura, cancast={1}", unit.SafeName(), Spell.CanCastHack("Spellsteal", unit));
                    return unit;
                    },
                ret => SingularSettings.Instance.DispelTargets != CheckTargets.None 
                );                   
        }

        public static WoWUnit GetSpellstealTarget()
        {
            if (SingularSettings.Instance.DispelTargets == CheckTargets.Current)
            {
                if ( Me.GotTarget && null != GetSpellstealAura( Me.CurrentTarget))
                {
                    return Me.CurrentTarget;
                }
            }
            else if (SingularSettings.Instance.DispelTargets != CheckTargets.None)
            {
                WoWUnit target = Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => Me.IsSafelyFacing(u) && null != GetSpellstealAura(u));
                return target;
            }

            return null;
        }

        public static WoWAura GetSpellstealAura(WoWUnit target)
        {
            return target.GetAllAuras().FirstOrDefault(a => a.TimeLeft.TotalSeconds > 5 && a.Spell.DispelType == WoWDispelType.Magic && PurgeWhitelist.Instance.SpellList.Contains(a.SpellId) && !Me.HasAura(a.SpellId));
        }
        */

        public static Composite CreateMagePolymorphOnAddBehavior()
        {
            return new Decorator(
                req => !Unit.NearbyUnfriendlyUnits.Any(u => u.HasMyAura("Polymorph")),
                Spell.Buff("Polymorph", on => Unit.NearbyUnfriendlyUnits.Where(IsViableForPolymorph).OrderByDescending(u => u.CurrentHealth).FirstOrDefault())
                );
        }

        private static bool IsViableForPolymorph(WoWUnit unit)
        {
            if (StyxWoW.Me.CurrentTargetGuid == unit.Guid)
                return false;

            if (!unit.Combat)
                return false;

            if (unit.CreatureType != WoWCreatureType.Beast && unit.CreatureType != WoWCreatureType.Humanoid)
                return false;

            if (unit.IsCrowdControlled())
                return false;

            if (!unit.IsTargetingMeOrPet && !unit.IsTargetingMyPartyMember)
                return false;

            if (StyxWoW.Me.RaidMembers.Any(m => m.CurrentTargetGuid == unit.Guid && m.IsAlive))
                return false;

            return true;
        }

        public static bool NeedToTimeWarp
        {
            get
            {
                if ( !MageSettings.UseTimeWarp || !MovementManager.IsClassMovementAllowed)
                    return false;

                if (Battlegrounds.IsInsideBattleground && Shaman.Common.IsPvpFightWorthLusting) 
                    return true;

                if (!Me.GroupInfo.IsInRaid && Me.GotTarget)
                {
                    if (Me.CurrentTarget.IsBoss() || Me.CurrentTarget.TimeToDeath() > 45 || (Me.CurrentTarget.IsPlayer && Me.CurrentTarget.ToPlayer().IsHostile))
                    {
                        return !Me.HasAnyAura("Temporal Displacement", PartyBuff.SatedDebuffName);
                    }
                }

                return false;
            }
        }

        public static bool HasTalent( MageTalents tal)
        {
            return TalentManager.IsSelected((int)tal);
        }

        private static int _secsBeforeBattle = 0;

        public static int secsBeforeBattle
        {
            get
            {
                if (_secsBeforeBattle == 0)
                    _secsBeforeBattle = new Random().Next(30, 60);

                return _secsBeforeBattle;
            }

            set
            {
                _secsBeforeBattle = value;
            }
        }

        public static bool NeedTableForBattleground 
        {
            get
            {
                return SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds && PVP.PrepTimeLeft < secsBeforeBattle && Me.HasAnyAura("Preparation", "Arena Preparation");
            }
        }

        /// <summary>
        /// behavior to cast appropriate Armor 
        /// </summary>
        /// <returns></returns>
        public static Composite CreateMageArmorBehavior()
        {
            return new Throttle(TimeSpan.FromMilliseconds(500),
                new Sequence(
                    new Action(ret => _Armor = GetBestArmor()),
                    new Decorator(
                        ret => _Armor != MageArmor.None
                            && !Me.HasMyAura(ArmorSpell(_Armor))
                            && Spell.CanCastHack(ArmorSpell(_Armor), Me),
                        Spell.BuffSelf(s => ArmorSpell(_Armor), ret => !Me.HasAura(ArmorSpell(_Armor)))
                        )
                    )
                );
        }

        static MageArmor _Armor;

        static string ArmorSpell(MageArmor s)
        {
            return s.ToString() + " Armor";
        }

        /// <summary>
        /// determines the best MageArmor value to use.  Attempts to use 
        /// user setting first, but defaults to something reasonable otherwise
        /// </summary>
        /// <returns>MageArmor to use</returns>
        public static MageArmor GetBestArmor()
        {
            if (MageSettings.Armor == MageArmor.None)
                return MageArmor.None;

            if (TalentManager.CurrentSpec == WoWSpec.None)
                return MageArmor.None;

            MageArmor bestArmor;
            if (MageSettings.Armor != Settings.MageArmor.Auto)
                bestArmor = MageSettings.Armor;
            else if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                bestArmor = MageArmor.Frost;
            else
            {
                if (TalentManager.CurrentSpec == WoWSpec.MageArcane)
                    bestArmor = MageArmor.Mage;
                else if (TalentManager.CurrentSpec == WoWSpec.MageFrost)
                    bestArmor = MageArmor.Frost;
                else
                    bestArmor = MageArmor.Molten;
            }

            if (bestArmor == MageArmor.Mage && Me.Level < 80)
                bestArmor = MageArmor.Frost;

            if (bestArmor == MageArmor.Frost && Me.Level < 54)
                bestArmor = MageArmor.Molten;

            if (bestArmor == MageArmor.Molten && Me.Level < 34)
                bestArmor = MageArmor.None;

            return bestArmor;
        }

        public static Composite CreateSpellstealEnemyBehavior()
        {
            return Dispelling.CreatePurgeEnemyBehavior("Spellsteal");
        }

        #region Avoidance and Disengage

        /// <summary>
        /// creates a Mage specific avoidance behavior based upon settings.  will check for safe landing
        /// zones before using Blink or Rocket Jump.  will additionally do a running away or jump turn
        /// attack while moving away from attacking mob if behaviors provided
        /// </summary>
        /// <param name="nonfacingAttack">behavior while running away (back to target - instants only)</param>
        /// <param name="jumpturnAttack">behavior while facing target during jump turn (instants only)</param>
        /// <returns></returns>
        public static Composite CreateMageAvoidanceBehavior()
        {
            int distBlink = TalentManager.HasGlyph("Blink") ? 28 : 20;
            return Avoidance.CreateAvoidanceBehavior("Blink", distBlink, Disengage.Direction.Frontwards, new ActionAlwaysSucceed());
        }

        /*
        private static Composite CreateSlowMeleeBehavior()
        {
            return new Decorator(
                ret => Unit.NearbyUnfriendlyUnits.Any(u => u.SpellDistance() <= 8 && !u.Stunned && !u.Rooted && !u.IsSlowed()),
                new PrioritySelector(
                    new Decorator(
                        ret => TalentManager.CurrentSpec == WoWSpec.MageFrost,
                        Mage.Frost.CastFreeze(on => Clusters.GetBestUnitForCluster(Unit.NearbyUnfriendlyUnits.Where(u => u.SpellDistance() < 8), ClusterType.Radius, 8))
                        ),
                    Spell.Buff("Frost Nova"),
                    Spell.Buff("Frostjaw"),
                    // Spell.CastOnGround("Ring of Frost", loc => Me.Location, req => true, false),
                    Spell.Buff("Cone of Cold")
                    )
                );
        }
        */

        private static Composite CreateSlowMeleeBehavior()
        {
            return new PrioritySelector(
                ctx => SafeArea.NearestEnemyMobAttackingMe,
                new Action( ret => {
                    if (ret == null)
                        Logger.WriteDebug("SlowMelee: no nearest mob found");
                    else
                        Logger.WriteDebug("SlowMelee: crowdcontrolled: {0}, slowed: {1}", ((WoWUnit)ret).IsCrowdControlled(), ((WoWUnit)ret).IsSlowed());
                    return RunStatus.Failure;
                    }),
                new Decorator(
                    // ret => ret != null && !((WoWUnit)ret).Stunned && !((WoWUnit)ret).Rooted && !((WoWUnit)ret).IsSlowed(),
                    ret => ret != null,
                    new PrioritySelector(
                        new Throttle(2,
                            new PrioritySelector(
                                new Decorator(
                                    req => ((WoWUnit)req).IsCrowdControlled(),
                                    new Action(r => Logger.WriteDebug("SlowMelee: target already crowd controlled"))
                                    ),
                                new Decorator(
                                    req => ((WoWUnit)req).IsSlowed(60),
                                    new Action(r => Logger.WriteDebug("SlowMelee: target already slowed at least 50%"))
                                    ),
                                new Decorator(
                                    ret => TalentManager.CurrentSpec == WoWSpec.MageFrost,
                                    Mage.Frost.CastFreeze(on => Clusters.GetBestUnitForCluster(Unit.NearbyUnfriendlyUnits.Where(u => u.SpellDistance() < 8), ClusterType.Radius, 8))
                                    ),
                                Spell.CastOnGround("Ring of Frost", onUnit => (WoWUnit)onUnit, req => ((WoWUnit)req).SpellDistance() < 30, true),
                                Spell.Cast("Frost Nova", mov => true, onUnit => (WoWUnit)onUnit, req => ((WoWUnit)req).SpellDistance() < 12, cancel => false),
                                Spell.Cast("Frostjaw", mov => true, onUnit => (WoWUnit)onUnit, req => true, cancel => false),
                                Spell.Cast("Cone of Cold", mov => true, onUnit => (WoWUnit)onUnit, req => true, cancel => false)
                                )
                            )
                        )
                    )
                );
        }

        #endregion

        public static bool NeedEvocation 
        { 
            get 
            {
                // never cast Evocation if we talent rune of power
                if (HasTalent(MageTalents.RuneOfPower))
                    return false;

                if (!Spell.CanCastHack("Evoation"))
                    return false;

                // always evocate if low mana
                if (Me.ManaPercent < 30)
                {
                    Logger.Write(Color.White, "^Evocation: casting due to low Mana");
                    return true;
                }

                // if low health, return true if we are glyphed (made sure no invocation or rune of power talented chars reach here already)
                if (Me.HealthPercent < 40)
                {
                    bool needHeal = TalentManager.HasGlyph("Evocation");
                    if (needHeal)
                        Logger.Write(Color.White, "^Evocation: casting for glyphed heal");
                    return needHeal;
                }

                return false;
            }
        }

        private static Composite _runeOfPower;

        public static Composite CreateMageRuneOfPowerBehavior()
        {
            if (!Common.HasTalent(MageTalents.RuneOfPower))
                return new ActionAlwaysFail();

            if (_runeOfPower == null)
            {
                _runeOfPower = new ThrottlePasses(
                    1,
                    TimeSpan.FromSeconds(6),
                    RunStatus.Failure,
                    Spell.CastOnGround("Rune of Power", on => Me, req => !Me.IsMoving && !Me.InVehicle && !Me.HasAura("Rune of Power") && Singular.Utilities.EventHandlers.LastNoPathFailure.AddSeconds(15) < DateTime.Now, false)
                    );
            }

            return _runeOfPower;
        }

        public static Composite CreateMageInvocationBehavior()
        {
            if (!Common.HasTalent(MageTalents.Invocation))
                return new ActionAlwaysFail();

            return new Decorator(
                req => !Me.HasAura("Invoker's Energy") && Spell.CanCastHack("Evocation"),
                new Sequence(
                    new Action(r => Logger.Write(Color.White, "^Invocation: buffing Invoker's Energy")),
                    Spell.Cast("Evocation", on => Me, req => true, cancel => false),
                    Helpers.Common.CreateWaitForLagDuration(),
                    new Wait(TimeSpan.FromMilliseconds(500), until => Me.HasAura("Invoker's Energy"), new ActionAlwaysSucceed())
                    )
                );
        }

    }

    public enum MageTalents
    {
        None = 0,
        PresenceOfMind,
        BlazingSpeed,
        IceFloes,
        TemporalShield,
        Flameglow,
        IceBarrier,
        RingOfFrost,
        IceWard,
        Frostjaw,
        GreaterInivisibility,
        Cauterize,
        ColdSnap,
        NetherTempest,
        LivingBomb,
        FrostBomb,
        Invocation,
        RuneOfPower,
        IncantersWard
    }
}