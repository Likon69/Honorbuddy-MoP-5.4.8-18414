using System;
using System.Collections.Generic;
using System.Linq;
using CommonBehaviors.Actions;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

namespace Singular.Helpers
{
    /*
     * Apply Aura: Mod Rating (33554432) Value: 5
     * Apply Aura: Mod Stat - % (Strength, Agility, Intellect) Value: 5
     * Apply Aura: 
     */

    /// <summary>
    /// indicates buff category an aura belongs to.  values must be a unique bit to allow creating 
    /// masks to represent a single aura that provides buffs in multiple categories, such as 
    /// Arcane Brilliance being PartyBuff.Spellpower+PartyBuff.Crit
    /// </summary>
    public enum PartyBuffType
    {
        // from http://www.wowhead.com/guide=1100
        None = 0,
        Stats = 1,  // Mark of the Wild, Legacy of the Emperor, Blessing of Kings, Embrace of the Shale Spider
        Stamina= 1 << 1,    // PW:Fortitude, Imp: Blood Pact, Commanding Shout, Qiraji Fortitude
        AttackPower = 1 << 2,    // Horn of Winter, Trueshot Aura, Battle Shout
        SpellPower = 1 << 3, // Arcane Brilliance, Dalaran Brilliance, Burning Wrath, Dark Intent, Still Water
        Haste = 1 << 4,  // Unholy Aura, Swiftblade's Cunning, Unleashed Rage, Crackling Howl, Serpent's Swiftness
        SpellHaste = 1 << 5, // Moonkin Aura, Shadowform, Elemental Oath, Mind Quickening
        Crit = 1 << 6,   // Leader of the Pack, Arcane Brilliance, Dalaran Brilliance, Legacy of the White Tiger, Bellowing Roar, Furious Howl, Terrifying Roar, Fearless Roar, Still Water
        Mastery = 1 << 7 // Blessing of Might, Grace of Air, Roar of Courage, Spirit Beast Blessing
    }


    public static class PartyBuff
    {
        private static Dictionary<string, PartyBuffType> dictBuffs = new Dictionary<string, PartyBuffType>()
        {
            { "Mark of the Wild",                   PartyBuffType.Stats},
            { "Legacy of the Emperor",              PartyBuffType.Stats},
            { "Blessing of Kings",                  PartyBuffType.Stats},
            { "Embrace of the Shale Spider",        PartyBuffType.Stats},

            { "Power Word: Fortitude",              PartyBuffType.Stamina},
            { "Commanding Shout",                   PartyBuffType.Stamina},
            { "Qiraji Fortitude",                   PartyBuffType.Stamina},
            // { "Dark Intent",                        PartyBuffType.SpellPower | PartyBuffType.Stamina },

            { "Horn of Winter",                     PartyBuffType.AttackPower},
            { "Trueshot Aura",                      PartyBuffType.AttackPower},
            { "Battle Shout",                       PartyBuffType.AttackPower},

            { "Arcane Brilliance",                  PartyBuffType.SpellPower | PartyBuffType.Crit},
            { "Dalaran Brilliance",                 PartyBuffType.SpellPower | PartyBuffType.Crit},
            { "Burning Wrath",                      PartyBuffType.SpellPower},
            { "Dark Intent",                        PartyBuffType.SpellPower | PartyBuffType.Stamina },
            { "Still Water",                        PartyBuffType.SpellPower |PartyBuffType.Crit},

            { "Unholy Aura",                        PartyBuffType.Haste},
            { "Swiftblade's Cunning",               PartyBuffType.Haste},
            { "Unleashed Rage",                     PartyBuffType.Haste},
            { "Crackling Howl",                     PartyBuffType.Haste},
            { "Serpent's Swiftness",                PartyBuffType.Haste},

            { "Moonkin Aura",                       PartyBuffType.SpellHaste},
            { "Shadowform",                         PartyBuffType.SpellHaste},
            { "Elemental Oath",                     PartyBuffType.SpellHaste},
            { "Mind Quickening",                    PartyBuffType.SpellHaste},

            { "Leader of the Pack",                 PartyBuffType.Crit},
            // { "Arcane Brilliance",               new BuffCategory[] {PartyBuff.SpellPower | PartyBuff.Crit}, // here for doc only... see SpellPower section
            // { "Dalaran Brilliance",              new BuffCategory[] {PartyBuff.SpellPower | PartyBuff.Crit}, // here for doc only... see SpellPower section
            { "Legacy of the White Tiger",          PartyBuffType.Crit},
            { "Bellowing Roar",                     PartyBuffType.Crit},
            { "Furious Howl",                       PartyBuffType.Crit},
            { "Terrifying Roar",                    PartyBuffType.Crit},
            { "Fearless Roar",                      PartyBuffType.Crit},
            // { "Still Water",                     new BuffCategory[] {PartyBuff.SpellPower | PartyBuff.Crit}, // here for doc only... see SpellPower section

            { "Blessing of Might",                  PartyBuffType.Mastery},
            { "Grace of Air",                       PartyBuffType.Mastery},
            { "Roar of Courage",                    PartyBuffType.Mastery},
            { "Spirit Beast Blessing",              PartyBuffType.Mastery},
        };

        /// <summary>
        /// maps a Spell name to its associated PartyBuff vlaue
        /// </summary>
        /// <param name="name">spell name</param>
        /// <returns>PartyBuff enum mask if exists for spell, otherwise PartyBuff.None</returns>
        public static PartyBuffType GetPartyBuffForSpell(string name)
        {
            PartyBuffType bc;
            if (!dictBuffs.TryGetValue(name, out bc))
                bc = PartyBuffType.None;

            return bc;
        }

        /// <summary>
        /// check a WoWUnit for a particular PartyBuff enum
        /// </summary>
        /// <param name="unit">unit to check for buff</param>
        /// <param name="cat">buff to check for.  may be a mask of multiple buffs if any will do, such as PartyBuff.Stats + PartyBuff.Mastery</param>
        /// <returns>true if any buff matching the mask in 'cat' is found, otherwise false</returns>
        public static bool HasPartyBuff(this WoWUnit unit, PartyBuffType cat)
        {
            foreach (var a in unit.GetAllAuras())
            {
                PartyBuffType bc = GetPartyBuffForSpell(a.Name);
                if ((bc & cat) != PartyBuffType.None)
                    return true;
            }
            return false;
        }

        /// <summary>
        ///  
        /// </summary>
        /// <param name="unit"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static bool HasPartyBuff(this WoWUnit unit, string name)
        {
            PartyBuffType cat = GetPartyBuffForSpell(name);
            foreach (var a in unit.GetAllAuras())
            {
                PartyBuffType bc = GetPartyBuffForSpell(a.Name);
                if ((bc & cat) != PartyBuffType.None)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// gets a PartyBuff mask representing all categories of buffs present
        /// </summary>
        /// <param name="unit">WoWUnit to check for buffs</param>
        /// <returns>PartyBuff mask representing all buffs founds</returns>
        public static PartyBuffType GetPartyBuffs(this WoWUnit unit)
        {
            PartyBuffType buffs = PartyBuffType.None;
            foreach (var a in unit.GetAllAuras())
            {
                PartyBuffType bc = GetPartyBuffForSpell(a.Name);
                buffs |= bc;
            }
            return buffs;
        }

        /// <summary>
        /// gets a PartyBuff mask representing all categories of buffs missing
        /// </summary>
        /// <param name="unit">WoWUnit to check for missing buffs</param>
        /// <returns>PartyBuff mask representing all buffs that are missing</returns>
        public static PartyBuffType GetMissingPartyBuffs(this WoWUnit unit)
        {
            PartyBuffType buffMask = PartyBuffType.Stats | PartyBuffType.Stamina | PartyBuffType.AttackPower | PartyBuffType.SpellPower | PartyBuffType.Haste | PartyBuffType.SpellHaste | PartyBuffType.Crit | PartyBuffType.Mastery;
            PartyBuffType buffs = GetPartyBuffs(unit);
            buffs = (~buffs) & buffMask;
            return buffs;
        }


        /// <summary>
        /// time next PartyBuff attempt allowed
        /// </summary>
        public static DateTime timeAllowBuff = DateTime.MinValue;

        /// <summary>
        /// minimum TimeSpan to wait between PartyBuff casts
        /// </summary>
        public static TimeSpan spanBuffFrequency = new TimeSpan(0,0,20);

        private static int _secsBeforeBattle = 0;

        /// <summary>
        /// # of seconds this character waits prior to start of battleground
        /// to do PartyBuff casts.  if 0 when retrieving value, will initialize
        /// it to a random value between 5 and 12.  to force it to recalc a
        /// new random value, set to 0 perodically
        /// </summary>
        public static int secsBeforeBattle
        {
            get 
            {
                if ( _secsBeforeBattle == 0 )
                    _secsBeforeBattle = new Random().Next(6, 12);

                return _secsBeforeBattle;
            }

            set
            {
                _secsBeforeBattle = value;
            }
        }

        /// <summary>
        /// sets blessings timer to wait 'spanBuffFrequency' before next allowed cast
        /// ... allowed blessing attempt
        /// </summary>
        public static void ResetReadyToPartyBuffTimer()
        {
            timeAllowBuff = DateTime.Now + spanBuffFrequency;
        }

        /// <summary>
        /// returns true if has been atleast 1 minute since last blessing attempt.
        /// .. if in battlegrounds will also check that the battle only has
        /// .. random # seconds (5 to 12) remaining or has already begun
        /// </summary>
        /// <returns></returns>
        public static bool IsItTimeToBuff()
        {
            if (!StyxWoW.Me.IsInGroup())
                return true;

            if (DateTime.Now < timeAllowBuff)
                return false;

            if (!Battlegrounds.IsInsideBattleground)
                return true;

            return PVP.PrepTimeLeft < secsBeforeBattle;
        }

        /// <summary>
        /// private function to buff individual units.  makes assumptions
        /// that certain states have been checked previously. only the 
        /// group interface to PartyBuffs is exposed since it handles the
        /// case of LocalPlayer not in a group as well
        /// </summary>
        /// <param name="name">spell name</param>
        /// <param name="onUnit">unit selection delegate</param>
        /// <param name="requirements">requirements delegate that must be true to cast buff</param>
        /// <param name="myMutexBuffs">list of buffs your mechanics make mutually exclusive for you to cast.  For example, BuffGroup("Blessing of Kings", ret => true, "Blessing of Might") </param>
        /// <returns></returns>
        private static Composite BuffUnit(string name, UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements, params string[] myMutexBuffs)
        {
            return 
                new Decorator(
                    ret => onUnit(ret) != null 
                        && (PartyBuffType.None != (onUnit(ret).GetMissingPartyBuffs() & GetPartyBuffForSpell(name)))
                        && (myMutexBuffs == null || myMutexBuffs.Count() == 0 || !onUnit(ret).GetAllAuras().Any(a => a.CreatorGuid == StyxWoW.Me.Guid && myMutexBuffs.Contains(a.Name))),
                    new Sequence(
                        Spell.Buff( name, onUnit, requirements),
                        new Wait( 1, until => StyxWoW.Me.HasPartyBuff(name), new ActionAlwaysSucceed()),
                        new Action(ret =>
                            {                                   
                            System.Diagnostics.Debug.Assert( PartyBuffType.None != GetPartyBuffForSpell(name));
                            if (PartyBuffType.None != GetPartyBuffForSpell(name))
                                ResetReadyToPartyBuffTimer();
                            else
                                Logger.WriteDebug("Programmer Error: should use Spell.Buff(\"{0}\") instead", name);
                            })
                        )
                    );
        }

        /// <summary>
        /// checks group members in range if they have a buff providing the benefits 
        /// that this spell does, and if not casts the buff upon them.  understands
        /// similar buffs such as Blessing of Kings being same as Mark of the Wild.
        /// if  not in a group, will treat Me as a group of one
        /// Will not buff if Mounted unless during prep phase of a battleground
        /// </summary>
        /// <param name="name">spell name of buff</param>
        /// <param name="myMutexBuffs">list of your buffs which are mutually exclusive to 'name'.  For example, BuffGroup("Blessing of Kings", ret => true, "Blessing of Might") </param>
        /// <returns></returns>
        public static Composite BuffGroup(string name, params string[] myMutexBuffs)
        {
            return BuffGroup(name, ret => true, myMutexBuffs);
        }

        /// <summary>
        /// checks group members in range if they have a buff providing the benefits 
        /// that this spell does, and if not casts the buff upon them.  understands
        /// similar buffs such as Blessing of Kings being same as Mark of the Wild.
        /// if  not in a group, will treat Me as a group of one. 
        /// Will not buff if Mounted unless during prep phase of a battleground
        /// </summary>
        /// <param name="name">spell name of buff</param>
        /// <param name="requirements">requirements delegate that must be true for cast to occur</param>
        /// <param name="myMutexBuffs">list of your buffs which are mutually exclusive to 'name'.  For example, BuffGroup("Blessing of Kings", ret => true, "Blessing of Might") </param>
        /// <returns></returns>
        public static Composite BuffGroup(string name, SimpleBooleanDelegate requirements, params string[] myMutexBuffs)
        {
            return
                new Decorator(ret => IsItTimeToBuff() 
                                    && SpellManager.HasSpell(name)
                                    && (!StyxWoW.Me.Mounted || !PVP.IsPrepPhase),
                    new PrioritySelector(
                        ctx => Unit.GroupMembers.FirstOrDefault(m => m.IsAlive && m.DistanceSqr < 30 * 30 && (PartyBuffType.None != (m.GetMissingPartyBuffs() & GetPartyBuffForSpell(name)))),
                        BuffUnit(name, ctx => (WoWUnit)ctx, requirements, myMutexBuffs)
                        )
                    );
        }

        #region Handle Faction Specific Name for Bloodlust

        /// <summary>
        /// spell name to use for Bloodlust
        /// </summary>
        public static string BloodlustSpellName { get; set; }

        /// <summary>
        /// debuff name to use for Bloodlust cooldown
        /// </summary>
        public static string SatedDebuffName { get; set; }

        /// <summary>
        /// determine the name of the spell to use.  Should be called during Combat Routine initialization. Doesn't change so no need to repeat
        /// </summary>
        public static void SetBloodlustSpellInformation()
        {
            if (StyxWoW.Me.IsHorde)
            {
                BloodlustSpellName = "Bloodlust";
                SatedDebuffName = "Sated";
            }
            else
            {
                BloodlustSpellName = "Heroism";
                SatedDebuffName = "Exhaustion";
            }
        }

        /// <summary>
        /// true: we have a Bloodlust-like buff, typically indicating we should cast 
        /// other long cooldown abilities that we save for such an occassion
        /// </summary>
        public static bool WeHaveBloodlust
        {
            get
            {
                return StyxWoW.Me.HasAnyAura(BloodlustSpellName, "Time Warp", "Ancient Hysteria");
            }
        }

        /// <summary>
        /// true: we have a Sated-like debuff, indicating we are prevented from benefitting
        /// from a Bloodlust buff until it expires
        /// </summary>
        public static bool WeHaveSatedDebuff
        {
            get
            {
                return StyxWoW.Me.HasAnyAura(SatedDebuffName, "Temporal Displacement", "Insanity");
            }
        }

        #endregion

        /// <summary>
        /// one time initialization of PartyBuff info
        /// </summary>
        internal static void Init()
        {
            SetBloodlustSpellInformation();
        }
    }
}
