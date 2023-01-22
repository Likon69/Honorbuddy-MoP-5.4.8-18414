using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Rest = Singular.Helpers.Rest;
using System.Drawing;
using CommonBehaviors.Actions;

namespace Singular.ClassSpecific.Paladin
{
    public class Retribution
    {

        #region Properties & Fields

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static PaladinSettings PaladinSettings { get { return SingularSettings.Instance.Paladin(); } }

        private const int RET_T13_ITEM_SET_ID = 1064;

        private static int NumTier13Pieces
        {
            get
            {
                return StyxWoW.Me.CarriedItems.Count(i => i.ItemInfo.ItemSetId == RET_T13_ITEM_SET_ID);
            }
        }

        private static bool Has2PieceTier13Bonus { get { return NumTier13Pieces >= 2; } }

        private static int _mobCount;

        #endregion

        #region Heal
        [Behavior(BehaviorType.Heal, WoWClass.Paladin, WoWSpec.PaladinRetribution)]
        public static Composite CreatePaladinRetributionHeal()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Devotion Aura", req => Me.Silenced),
                Spell.Cast("Lay on Hands",
                    mov => false,
                    on => Me,
                    req => Me.PredictedHealthPercent(includeMyHeals: true) <= PaladinSettings.SelfLayOnHandsHealth),
                Common.CreateWordOfGloryBehavior(on => Me),
                Spell.Cast("Flash of Light",
                    mov => false,
                    on => Me,
                    req => Me.PredictedHealthPercent(includeMyHeals: true) <= PaladinSettings.SelfFlashOfLightHealth,
                    cancel => Me.HealthPercent > PaladinSettings.SelfFlashOfLightHealth)
                );
        }

        [Behavior(BehaviorType.Rest, WoWClass.Paladin, WoWSpec.PaladinRetribution)]
        public static Composite CreatePaladinRetributionRest()
        {
            return new PrioritySelector(
                Spell.WaitForCastOrChannel(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Rest.CreateDefaultRestBehaviour( "Flash of Light", "Redemption")
                        )
                    )
                );
        }
        #endregion

        #region Normal Rotation

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Paladin, WoWSpec.PaladinRetribution, WoWContext.Normal | WoWContext.Battlegrounds )]
        public static Composite CreatePaladinRetributionNormalPullAndCombat()
        {
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCastOrChannel(),

                new Decorator( 
                    ret => !Spell.IsGlobalCooldown() && Me.GotTarget,
                    new PrioritySelector(
                        // aoe count
                        ActionAoeCount(),

                        CreateRetDiagnosticOutputBehavior(),

                        Helpers.Common.CreateAutoAttack(true),
                        Helpers.Common.CreateInterruptBehavior(),

                        Common.CreatePaladinPullMore(),

                        // Defensive
                        Spell.BuffSelf("Hand of Freedom",
                            ret => Me.HasAuraWithMechanic(WoWSpellMechanic.Dazed,
                                                                  WoWSpellMechanic.Disoriented,
                                                                  WoWSpellMechanic.Frozen,
                                                                  WoWSpellMechanic.Incapacitated,
                                                                  WoWSpellMechanic.Rooted,
                                                                  WoWSpellMechanic.Slowed,
                                                                  WoWSpellMechanic.Snared)),

                        Spell.BuffSelf("Divine Shield", ret => Me.HealthPercent <= 20 && !Me.HasAura("Forbearance") && (!Me.HasAura("Horde Flag") || !Me.HasAura("Alliance Flag"))),
                        Spell.BuffSelf("Divine Protection", ret => Me.HealthPercent <= PaladinSettings.DivineProtectionHealthProt),

                        Common.CreatePaladinSealBehavior(),

                        Spell.Cast( "Hammer of Justice", ret => PaladinSettings.StunMobsWhileSolo && SingularRoutine.CurrentWoWContext == WoWContext.Normal ),

                        //7	Blow buffs seperatly.  No reason for stacking while grinding.
                        Spell.Cast("Guardian of Ancient Kings", ret => PaladinSettings.RetAvengAndGoatK && (_mobCount >= 4 || Me.GotTarget && Me.CurrentTarget.TimeToDeath() > 30)),
                        Spell.Cast("Holy Avenger", ret => PaladinSettings.RetAvengAndGoatK && _mobCount < 4),
                        Spell.BuffSelf("Avenging Wrath", 
                            ret => PaladinSettings.RetAvengAndGoatK
                                && (_mobCount >= 4 || Me.GotTarget && Me.CurrentTarget.TimeToDeath() > 30 || (!Me.HasAura("Holy Avenger") && Spell.GetSpellCooldown("Holy Avenger").TotalSeconds > 10))),

                        Spell.Cast("Execution Sentence", ret => Me.CurrentTarget.TimeToDeath() > 15),
                        Spell.Cast("Holy Prism", on => Group.Tanks.FirstOrDefault(t => t.IsAlive && t.Distance < 40)),

                        new Decorator(
                            ret => _mobCount >= 2 && Spell.UseAOE && Me.CurrentTarget.IsTrivial(),
                            new PrioritySelector(
                                // Bobby53: Inq > 5HP DS > Exo > HotR > 3-4HP DS
                                Spell.BuffSelf("Inquisition", ret => Me.CurrentHolyPower > 0 && Me.GetAuraTimeLeft("Inquisition", true).TotalSeconds < 4),
                                Spell.Cast("Divine Storm", ret => Me.CurrentHolyPower == 5),
                                Spell.Cast("Exorcism", req => TalentManager.HasGlyph("Mass Exorcism")),
                                Spell.Cast("Hammer of the Righteous"),
                                Spell.Cast("Divine Storm", ret => Me.CurrentHolyPower >= 3)
                                )
                            ),

                        Common.CreatePaladinBlindingLightBehavior(),

                        new Decorator(
                            ret => _mobCount >= 2 && Spell.UseAOE,
                            new PrioritySelector(

                                // was EJ: Inq > 5HP DS > LH > HoW > Exo > HotR > Judge > 3-4HP DS (> SS)
                                // now EJ: Inq > 5HP DS > LH > HoW (> T16 Free DS) > HotR > Judge > Exo > 3-4HP DS (> SS)
                                Spell.BuffSelf("Inquisition", ret => Me.CurrentHolyPower > 0 && Me.GetAuraTimeLeft("Inquisition", true).TotalSeconds < 4),
                                Spell.Cast(SpellManager.HasSpell("Divine Storm") ? "Divine Storm" : "Templar's Verdict", ret => Me.CurrentHolyPower == 5),
                                Spell.CastOnGround("Light's Hammer", on => Me.CurrentTarget, ret => 2 <= Clusters.GetClusterCount(Me.CurrentTarget, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 10f)),
                                Spell.Cast("Hammer of Wrath"),
                                Spell.Cast("Divine Storm", req => Me.HasAura("Divine Crusader")),   // T16 buff
                                Spell.Cast(SpellManager.HasSpell("Hammer of the Righteous") ? "Hammer of the Righteous" : "Crusader Strike"),
                                Spell.Cast("Judgment"),
                                Spell.Cast("Exorcism"),
                                Spell.Cast(SpellManager.HasSpell("Divine Storm") ? "Divine Storm" : "Templar's Verdict", ret => Me.CurrentHolyPower >= 3),
                                Spell.BuffSelf("Sacred Shield"),
                                Movement.CreateMoveToMeleeBehavior(true),
                                new ActionAlwaysSucceed()
                                )
                            ),

                        // was EJ: Inq > 5HP TV > ES > HoW > Exo > CS > Judge > 3-4HP TV (> SS)
                        // was EJ: Inq > 5HP TV > ES > HoW > CS > Judge > Exo > 3-4HP TV (> SS)
                        // now EJ: Inq > ES (> 5HP T16 Free DS) > 5HP TV > HoW (> T16 Free DS) > CS > Judge > Exo > 3-4HP TV (> SS)
                        Spell.BuffSelf("Inquisition", req => Me.CurrentHolyPower > 0 && Me.GetAuraTimeLeft("Inquisition", true).TotalSeconds < 4),
                        Spell.Cast("Execution Sentence"),
                        Spell.Cast("Divine Storm", req => Me.CurrentHolyPower == 5 && Me.HasAura("Divine Crusader")),   // T16 buff
                        Spell.Cast("Templar's Verdict", req => Me.CurrentHolyPower == 5),
                        Spell.Cast("Hammer of Wrath"),
                        Spell.Cast("Divine Storm", req => Me.HasAura("Divine Crusader")),   // T16 buff
                        Spell.Cast("Crusader Strike"),
                        Spell.Cast("Judgment"),
                        Spell.Cast("Exorcism"),
                        Spell.Cast("Templar's Verdict", req => Me.CurrentHolyPower >= 3),
                        Spell.BuffSelf("Sacred Shield")
                        )
                    ),

                // Move to melee is LAST. Period.
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        private static Action ActionAoeCount()
        {
            return new Action(ret =>
            {
                _mobCount = Unit.NearbyUnfriendlyUnits.Count(u => u.Distance < (u.MeleeDistance() + 3));
                return RunStatus.Failure;
            });
        }

        #endregion


        #region Instance Rotation

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Paladin, WoWSpec.PaladinRetribution, WoWContext.Instances)]
        public static Composite CreatePaladinRetributionInstancePullAndCombat()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCastOrChannel(),

                new Decorator( 
                    ret => !Spell.IsGlobalCooldown() && Me.GotTarget,
                    new PrioritySelector(
                        // aoe count
                        new Action(ret =>
                        {
                            _mobCount = Unit.NearbyUnfriendlyUnits.Count(u => u.Distance < (u.MeleeDistance() + 3));
                            return RunStatus.Failure;
                        }),

                        CreateRetDiagnosticOutputBehavior(),

                        Helpers.Common.CreateAutoAttack(true),

                        Helpers.Common.CreateInterruptBehavior(),

                        // Defensive
                        Spell.BuffSelf("Hand of Freedom",
                                       ret =>
                                       !Me.Auras.Values.Any(
                                           a => a.Name.Contains("Hand of") && a.CreatorGuid == Me.Guid) &&
                                       Me.HasAuraWithMechanic(WoWSpellMechanic.Dazed,
                                                                      WoWSpellMechanic.Disoriented,
                                                                      WoWSpellMechanic.Frozen,
                                                                      WoWSpellMechanic.Incapacitated,
                                                                      WoWSpellMechanic.Rooted,
                                                                      WoWSpellMechanic.Slowed,
                                                                      WoWSpellMechanic.Snared)),

                        Spell.BuffSelf("Divine Shield", 
                            ret => Me.HealthPercent <= 20  && !Me.HasAura("Forbearance")),
                        Spell.BuffSelf("Divine Protection",
                            ret => Me.HealthPercent <= PaladinSettings.DivineProtectionHealthProt),

                        Common.CreatePaladinSealBehavior(),

                        new Throttle( 40,
                            new Decorator(
                                ret => PartyBuff.WeHaveBloodlust,
                                new PrioritySelector(
                                    ctx => Item.FindFirstUsableItemBySpell("Golem's Strength", "Potion of Mogu Power"),
                                    new Decorator(
                                        ret => ret != null,
                                        new Action(item => ((WoWItem)item).Use() )
                                        )
                                    )
                                )
                            ),

                        new Decorator(
                            ret => Me.CurrentTarget.IsWithinMeleeRange && PaladinSettings.RetAvengAndGoatK,
                            new PrioritySelector(
                                Spell.Cast("Guardian of Ancient Kings",
                                    ret => Me.CurrentTarget.IsBoss()
                                        && Me.ActiveAuras.ContainsKey("Inquisition")),
                                Spell.BuffSelf("Avenging Wrath", 
                                    ret => Me.ActiveAuras.ContainsKey("Inquisition")
                                        && (Common.HasTalent(PaladinTalents.SanctifiedWrath) || Spell.GetSpellCooldown("Guardian of Ancient Kings").TotalSeconds <= 290)),
                                Spell.Cast("Holy Avenger", ret => Me.HasAura("Avenging Wrath"))
                                )
                            ),

                        // react to Divine Purpose proc
                        new Decorator(
                            ret => Me.GetAuraTimeLeft("Divine Purpose", true).TotalSeconds > 0,
                            new PrioritySelector(
                                Spell.BuffSelf("Inquisition", ret => Me.GetAuraTimeLeft("Inquisition", true).TotalSeconds <= 2),
                                Spell.Cast("Templar's Verdict")
                                )
                            ),

                        Spell.Cast( "Execution Sentence", ret => Me.CurrentTarget.TimeToDeath() > 12 ),
                        Spell.Cast( "Holy Prism", on => Group.Tanks.FirstOrDefault( t => t.IsAlive && t.Distance < 40)),

                        //Use Synapse Springs Engineering thingy if inquisition is up

                        new Decorator(
                            ret => _mobCount >= 2 && Spell.UseAOE,
                            new PrioritySelector(
                                Spell.CastOnGround("Light's Hammer", on => Me.CurrentTarget, ret => true),

                                // EJ Multi Rotation
                                // was EJ: Inq > 5HP TV > ES > HoW > Exo > CS > Judge > 3-4HP TV (> SS)
                                // now EJ: Inq > 5HP DS > LH > HoW (> T16 Free DS) > HotR > Judge > Exo > 3-4HP DS (> SS)
                                Spell.BuffSelf("Inquisition", ret => Me.CurrentHolyPower > 0 && Me.GetAuraTimeLeft("Inquisition", true).TotalSeconds < 4),
                                Spell.Cast(SpellManager.HasSpell("Divine Storm") ? "Divine Storm" : "Templar's Verdict", ret => Me.CurrentHolyPower == 5),
                                Spell.CastOnGround("Light's Hammer", on => Me.CurrentTarget, ret => 2 <= Clusters.GetClusterCount(Me.CurrentTarget, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 10f)),
                                Spell.Cast("Hammer of Wrath"),
                                Spell.Cast("Divine Storm", req => Me.HasAura("Divine Crusader")),   // T16 buff
                                Spell.Cast(SpellManager.HasSpell("Hammer of the Righteous") ? "Hammer of the Righteous" : "Crusader Strike"),
                                Spell.Cast("Judgment"),
                                Spell.Cast("Exorcism"),
                                Spell.Cast(SpellManager.HasSpell("Divine Storm") ? "Divine Storm" : "Templar's Verdict", ret => Me.CurrentHolyPower >= 3),
                                Spell.BuffSelf("Sacred Shield"),
                                Movement.CreateMoveToMeleeBehavior(true)
                                )
                            ),

                        // Single Target Priority
                        // was EJ: Inq > 5HP TV > ES > HoW > Exo > CS > Judge > 3-4HP TV (> SS)
                        // was EJ: Inq > 5HP TV > ES > HoW > CS > Judge > Exo > 3-4HP TV (> SS)
                        // now EJ: Inq > ES (> 5HP T16 Free DS) > 5HP TV > HoW (> T16 Free DS) > CS > Judge > Exo > 3-4HP TV (> SS)
                        Spell.BuffSelf("Inquisition", req => Me.CurrentHolyPower > 0 && Me.GetAuraTimeLeft("Inquisition", true).TotalSeconds < 4),
                        Spell.Cast("Execution Sentence"),
                        Spell.Cast("Divine Storm", req => Me.CurrentHolyPower == 5 && Me.HasAura("Divine Crusader")),   // T16 buff
                        Spell.Cast("Templar's Verdict", req => Me.CurrentHolyPower == 5),
                        Spell.Cast("Hammer of Wrath"),
                        Spell.Cast("Divine Storm", req => Me.HasAura("Divine Crusader")),   // T16 buff
                        Spell.Cast("Crusader Strike"),
                        Spell.Cast("Judgment"),
                        Spell.Cast("Exorcism"),
                        Spell.Cast("Templar's Verdict", req => Me.CurrentHolyPower >= 3),
                        Spell.BuffSelf("Sacred Shield"),

                        // Symbiosis
                        Spell.Cast("Wrath", ret => !Me.CurrentTarget.IsWithinMeleeRange && Me.CurrentTarget.SpellDistance() < 40)
                        )
                    ),


                    // Move to melee is LAST. Period.
                    Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        private static Composite CreateRetDiagnosticOutputBehavior()
        {
            if (!SingularSettings.Debug)
                return new Action( ret => { return RunStatus.Failure; } );

            return new Sequence(
                new Action( r => SingularRoutine.UpdateDiagnosticCastingState() ),
                new Action(r => TMsg.ShowTraceMessages = false),
                new ThrottlePasses(1, 1, 
                    new Action(ret =>
                    {
                        TMsg.ShowTraceMessages = true;

                        string sMsg;
                        sMsg = string.Format(".... h={0:F1}%, m={1:F1}%, moving={2}, mobs={3}",
                            Me.HealthPercent,
                            Me.ManaPercent,
                            Me.IsMoving.ToYN(),
                            _mobCount
                            );

                        WoWUnit target = Me.CurrentTarget;
                        if (target != null)
                        {
                            sMsg += string.Format(
                                ", {0}, {1:F1}%, {2:F1} yds, face={3}, loss={4}",
                                target.SafeName(),
                                target.HealthPercent,
                                target.Distance,
                                Me.IsSafelyFacing(target).ToYN(),
                                target.InLineOfSpellSight.ToYN()
                                );
                        }

                        Logger.WriteDebug(Color.LightYellow, sMsg);
                        return RunStatus.Failure;
                    })
                    )
                );
        }

    }

    public class TMsg : Decorator
    {
        public static bool ShowTraceMessages { get; set; }

        public TMsg(SimpleStringDelegate str)
            : base(ret => ShowTraceMessages, new Action(r => { Logger.WriteDebug(str(r)); return RunStatus.Failure; }))
        {
        }
    }

}
