using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Styx;
using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using Singular.Settings;
using Styx.Common;
using System.Diagnostics;
using Styx.Common.Helpers;
using System.Drawing;
using Styx.WoWInternals.DBC;
using Styx.CommonBot.POI;
using System.Text;
using Singular.Managers;

namespace Singular.Helpers
{
    internal static class Unit
    {
        public static HashSet<uint> IgnoreMobs = new HashSet<uint>
            {
                52288, // Venomous Effusion (NPC near the snake boss in ZG. Its the green lines on the ground. We want to ignore them.)
                52302, // Venomous Effusion Stalker (Same as above. A dummy unit)
                52320, // Pool of Acid
                52525, // Bloodvenom

                52387, // Cave in stalker - Kilnara
            };

        public static bool IsUndeadOrDemon(this WoWUnit unit)
        {
            return unit.CreatureType == WoWCreatureType.Undead
                    || unit.CreatureType == WoWCreatureType.Demon;
        }

        public static bool IsMelee(this WoWUnit unit)
        {
            if (unit.Class == WoWClass.DeathKnight
                || unit.Class == WoWClass.Paladin
                || unit.Class == WoWClass.Rogue
                || unit.Class == WoWClass.Warrior)
                return true;

            if (!unit.IsMe)
            {
                if (unit.Class == WoWClass.Hunter
                    || unit.Class == WoWClass.Mage
                    || unit.Class == WoWClass.Priest
                    || unit.Class == WoWClass.Warlock)
                    return false;

                if (unit.Class == WoWClass.Monk)    // treat all enemy Monks as melee
                    return true;

                if (unit.Class == WoWClass.Druid && unit.HasAnyAura("Cat Form", "Bear Form"))
                    return true;

                if (unit.Class == WoWClass.Shaman && unit.GetAllAuras().Any(a => a.Name == "Unleashed Rage" && a.CreatorGuid == unit.Guid))
                    return true;

                return false;
            }

            switch (TalentManager.CurrentSpec)
            {
                case WoWSpec.DruidFeral:
                case WoWSpec.DruidGuardian:
                case WoWSpec.MonkBrewmaster:
                case WoWSpec.MonkWindwalker:
                case WoWSpec.ShamanEnhancement:
                    return true;
            }

            return false;
        }


        /// <summary>
        /// List of WoWPlayer in your Group. Deals with Party / Raid in a list independent manner and does not restrict distance
        /// </summary>
        public static IEnumerable<WoWPlayer> GroupMembers
        {
            get
            {
                ulong[] guids = StyxWoW.Me.GroupInfo.RaidMemberGuids
                    .Union(StyxWoW.Me.GroupInfo.PartyMemberGuids)
                    .Union(new[] { StyxWoW.Me.Guid })
                    .Distinct()
                    .ToArray();

                return (  // must check inheritance in getobj because of LocalPlayer
                    from p in ObjectManager.GetObjectsOfType<WoWPlayer>(true, true)
                    where p.IsFriendly && guids.Any(g => g == p.Guid)
                    select p).ToList();
            }
        }

        public static IEnumerable<WoWUnit> GroupMembersAndPets
        {
            get
            {
                ulong[] guids = StyxWoW.Me.GroupInfo.RaidMemberGuids
                    .Union(StyxWoW.Me.GroupInfo.PartyMemberGuids)
                    .Union(new[] { StyxWoW.Me.Guid })
                    .Distinct()
                    .ToArray();

                guids = guids
                    .Union(ObjectManager.GetObjectsOfType<WoWPlayer>(true, true)
                        .Where(p => p.GotAlivePet && p.IsInMyPartyOrRaid )
                        .Select(p => p.Pet.Guid))
                    .ToArray();

                return (  // must check inheritance in getobj because of LocalPlayer
                    from p in ObjectManager.GetObjectsOfType<WoWUnit>(true, true)
                    where p.IsFriendly && guids.Any(g => g == p.Guid)
                    select p).ToList();
            }
        }


        /// <summary>
        /// List of WoWPartyMember in your Group. Deals with Party / Raid in a list independent manner and does not restrict distance
        /// </summary>
        public static IEnumerable<WoWPartyMember> GroupMemberInfos
        {
            get { return StyxWoW.Me.GroupInfo.RaidMembers.Union(StyxWoW.Me.GroupInfo.PartyMembers).Distinct(); }
        }

        public static IEnumerable<WoWPlayer> NearbyGroupMembers
        {
            get
            {
                return GroupMembers.Where(p => p.DistanceSqr <= 40 * 40).ToList();
            }
        }

        public static IEnumerable<WoWUnit> NearbyGroupMembersAndPets
        {
            get
            {
                return GroupMembersAndPets.Where(p => p.DistanceSqr <= 40 * 40).ToList();
            }
        }

        /// <summary>
        ///   Gets the nearby friendly players that can be seen
        /// </summary>
        /// <value>The nearby friendly players.</value>
        public static IEnumerable<WoWPlayer> FriendlyPlayers(double range = 100.0 )
        {
            if ( range >= 100.0)
                return ObjectManager.GetObjectsOfType<WoWPlayer>(false, true).Where(p => p.IsFriendly).ToList();

            range *= range;
            return ObjectManager.GetObjectsOfType<WoWPlayer>(false, true).Where(p => p.IsFriendly && p.DistanceSqr < range).ToList();
        }

        /// <summary>
        ///   Gets the nearby friendly players within 40 yards.
        /// </summary>
        /// <value>The nearby friendly players.</value>
        public static IEnumerable<WoWPlayer> NearbyFriendlyPlayers
        {
            get
            {
                return FriendlyPlayers(40);
            }
        }

        /// <summary>
        ///   Gets the nearby unfriendly units within specified range.  if no range specified, 
        ///   includes all unfriendly units
        /// </summary>
        /// <value>The nearby unfriendly units.</value>
        public static IEnumerable<WoWUnit> UnfriendlyUnits(int maxSpellDist = -1)
        {
            bool useTargeting = (SingularRoutine.IsDungeonBuddyActive || (SingularRoutine.IsQuestBotActive && StyxWoW.Me.IsInInstance));

            if (useTargeting)
            {
                if ( maxSpellDist == -1)
                    return Targeting.Instance.TargetList.Where(u => u != null && ValidUnit(u));
                return Targeting.Instance.TargetList.Where(u => u != null && ValidUnit(u) && u.SpellDistance() < maxSpellDist);
            }

            Type typeWoWUnit = typeof(WoWUnit);
            Type typeWoWPlayer = typeof(WoWPlayer);
            List<WoWUnit> list = new List<WoWUnit>();
            List<WoWObject> objectList = ObjectManager.ObjectList;
            for (int i = 0; i < objectList.Count; i++)
            {
                Type type = objectList[i].GetType();
                if (type == typeWoWUnit || type == typeWoWPlayer)
                {
                    WoWUnit t = objectList[i] as WoWUnit;
                    if (t != null && ValidUnit(t) && (maxSpellDist == -1 || t.SpellDistance() < maxSpellDist ))
                    {
                        list.Add(t);
                    }
                }
            }
            return list;
        }

        /// <summary>
        ///   Gets the nearby unfriendly units within 40 yards.
        /// </summary>
        /// <value>The nearby unfriendly units.</value>
        public static IEnumerable<WoWUnit> NearbyUnfriendlyUnits
        {
            get
            {
                return UnfriendlyUnits(40);
            }
        }

        public static IEnumerable<WoWUnit> NearbyUnitsInCombatWithMe
        {
            get { return NearbyUnfriendlyUnits.Where(p => p.Aggro || (p.Combat && p.CurrentTargetGuid == StyxWoW.Me.Guid)); }
        }

        public static IEnumerable<WoWUnit> NearbyUnitsInCombatWithMeOrMyStuff
        {
            get { return NearbyUnfriendlyUnits.Where(p => p.Aggro || (p.Combat && (p.TaggedByMe || (p.GotTarget && p.IsTargetingMyStuff())))); }
        }

        public static IEnumerable<WoWUnit> UnitsInCombatWithUsOrOurStuff(int maxSpellDist)
        {
            return UnfriendlyUnits(maxSpellDist).Where(p => p.Aggro || (p.Combat && (p.TaggedByMe || (p.GotTarget && p.IsTargetingUs())))); 
        }

        public static IEnumerable<WoWUnit> NearbyUnitsInCombatWithUsOrOurStuff
        {
            get { return UnitsInCombatWithUsOrOurStuff(40); }
        }


        private static Color invalidColor = Color.LightCoral;

        public static bool ValidUnit(WoWUnit p, bool showReason = false)
        {
            if (p == null || !p.IsValid)
                return false;

            if (StyxWoW.Me.IsInInstance && IgnoreMobs.Contains(p.Entry))
            {
                if (showReason) Logger.Write(invalidColor, "invalid attack unit {0} is an Instance Ignore Mob", p.SafeName());
                return false;
            }

            // Ignore shit we can't select
            if (!p.CanSelect )
            {
                if (showReason) Logger.Write(invalidColor, "invalid attack unit {0} cannot be Selected", p.SafeName());
                return false;
            }

            // Ignore shit we can't attack
            if (!p.Attackable)
            {
                if (showReason) Logger.Write(invalidColor, "invalid attack unit {0} cannot be Attacked", p.SafeName());
                return false;
            }

            // Duh
            if (p.IsDead)
            {
                if (showReason) Logger.Write(invalidColor, "invalid attack unit {0} is already Dead", p.SafeName());
                return false;
            }

            // check for enemy players here as friendly only seems to work on npc's
            if (p.IsPlayer)
            {
                WoWPlayer pp = p.ToPlayer();
                if (pp.IsHorde == StyxWoW.Me.IsHorde && !pp.IsHostile)
                {
                    if (showReason)
                        Logger.Write(invalidColor, "invalid attack player {0} not a hostile enemy", p.SafeName());
                    return false;
                }

                if (!pp.CanWeAttack())
                {
                    if (showReason)
                        Logger.Write(invalidColor, "invalid attack player {0} cannot be Attacked currently", p.SafeName());
                    return false;
                }

                return true;
            }

            // Ignore friendlies!
            if (p.IsFriendly)
            {
                if (showReason) Logger.Write(invalidColor, "invalid attack unit {0} is Friendly", p.SafeName());
                return false;
            }

            // Dummies/bosses are valid by default. Period.
            if (p.IsTrainingDummy() || p.IsBoss())
                return true;

            // If it is a pet/minion/totem, lets find the root of ownership chain
            WoWPlayer pOwner = GetPlayerParent(p);

            // ignore if owner is player, alive, and not blacklisted then ignore (since killing owner kills it)
            // .. following .IsMe check to prevent treating quest mob summoned by us that we need to kill as invalid 
            if (pOwner != null && pOwner.IsAlive && !pOwner.IsMe)
            {
                if (!ValidUnit(pOwner))
                {
                    if (showReason)
                        Logger.Write(invalidColor, "invalid attack unit {0} - pets parent not an attackable Player", p.SafeName());
                    return false;
                }
                if (!StyxWoW.Me.IsPvPFlagged)
                {
                    if (showReason)
                        Logger.Write(invalidColor, "valid attackable player {0} but I am not PvP Flagged", p.SafeName());
                    return false;
                }
                if (Blacklist.Contains(pOwner, BlacklistFlags.Combat))
                {
                    if (showReason)
                        Logger.Write(invalidColor, "invalid attack unit {0} - Parent blacklisted for combat", p.SafeName());
                    return false;
                }

                return true;
            }

            // And ignore non-combat pets
            if (p.IsNonCombatPet)
            {
                if (showReason) Logger.Write(invalidColor, "{0} is a Noncombat Pet", p.SafeName());
                return false;
            }

            // And ignore critters (except for those ferocious ones or if set as BotPoi)
            if (IsIgnorableCritter(p))
            {
                if (showReason) Logger.Write(invalidColor, "{0} is a Critter", p.SafeName());
                return false;
            }

            /*
                        if (p.CreatedByUnitGuid != 0 || p.SummonedByUnitGuid != 0)
                            return false;
            */
            return true;
        }

        /// <summary>
        /// Checks if target is a Critter that can safely be ignored
        /// </summary>
        /// <param name="u"></param>
        /// WoWUnit to check
        /// <returns>true: can ignore safely, false: treat as attackable enemy</returns>
        public static bool IsIgnorableCritter(WoWUnit u)
        {
            if (!u.IsCritter)
                return false;

            // good enemy if BotPoi
            if (Styx.CommonBot.POI.BotPoi.Current.Guid == u.Guid && Styx.CommonBot.POI.BotPoi.Current.Type == Styx.CommonBot.POI.PoiType.Kill)
                return false;

            // good enemy if Targeting
            if (Targeting.Instance.TargetList.Contains(u))
                return false;

            // good enemy if Threat towards us
            if (u.ThreatInfo.ThreatValue != 0 && u.IsTargetingMyRaidMember)
                return false;

            // Nah, just a harmless critter
            return true;
        }

        public static uint TrivialHealth()
        {
            float trivialHealth = (0.01f * SingularSettings.Instance.TrivialMaxHealthPcnt) * StyxWoW.Me.MaxHealth;
            return (uint)trivialHealth;
        }

        public static bool IsTrivial(this WoWUnit unit)
        {
            if (SingularRoutine.CurrentWoWContext != WoWContext.Normal)
                return false;

            if (unit == null)
                return false;

            uint maxh = unit.MaxHealth;
            return maxh <= TrivialHealth();
        }

        public static WoWPlayer GetPlayerParent(WoWUnit unit)
        {
            // If it is a pet/minion/totem, lets find the root of ownership chain
            WoWUnit pOwner = unit;
            while (true)
            {
                if (pOwner.OwnedByUnit != null)
                    pOwner = pOwner.OwnedByRoot;
                else if (pOwner.CreatedByUnit != null)
                    pOwner = pOwner.CreatedByUnit;
                else if (pOwner.SummonedByUnit != null)
                    pOwner = pOwner.SummonedByUnit;
                else
                    break;
            }

            if (unit != pOwner && pOwner.IsPlayer)
                return pOwner as WoWPlayer;

            return null;
        }

        /// <summary>
        ///   Gets unfriendly units within *distance* yards of CurrentTarget.
        /// </summary>
        /// <param name="distance"> The distance to check from CurrentTarget</param>
        /// <returns>IEnumerable of WoWUnit in range including CurrentTarget</returns>
        public static IEnumerable<WoWUnit> UnfriendlyUnitsNearTarget(float distance)
        {
            return UnfriendlyUnitsNearTarget(StyxWoW.Me.CurrentTarget, distance);
        }

        /// <summary>
        /// Gets unfriendly units within *distance* yards of *unit*
        /// </summary>
        /// <param name="unit">WoWUnit to find targets in range</param>
        /// <param name="distance">range within WoWUnit of other units</param>
        /// <returns>IEnumerable of WoWUnit in range including *unit*</returns>
        public static IEnumerable<WoWUnit> UnfriendlyUnitsNearTarget(WoWUnit unit, float distance)
        {
            if (unit == null)
                return new List<WoWUnit>();

            var distFromTargetSqr = distance * distance;
            int distFromMe = 40 + (int) distance;

            var curTarLocation = unit.Location;
            return Unit.UnfriendlyUnits(distFromMe).Where(p => p.Location.DistanceSqr(curTarLocation) <= distFromTargetSqr).ToList();
        }

        /// <summary>
        ///  Checks the aura by the name on specified unit.
        /// </summary>
        /// <param name="unit"> The unit to check auras for. </param>
        /// <param name="aura"> The name of the aura in English. </param>
        /// <returns></returns>
        public static bool HasAura(this WoWUnit unit, string aura)
        {
            return HasAura(unit, aura, 0);
        }

        /// <summary>
        ///  Checks the aura count by the name on specified unit.
        /// </summary>
        /// <param name="unit"> The unit to check auras for. </param>
        /// <param name="aura"> The name of the aura in English. </param>
        /// <param name="stacks"> The stack count of the aura to return true. </param>
        /// <returns></returns>
        public static bool HasAura(this WoWUnit unit, string aura, int stacks)
        {
            return HasAura(unit, aura, stacks, null);
        }


        public static bool HasAllMyAuras(this WoWUnit unit, params string[] auras)
        {
            return auras.All(unit.HasMyAura);
        }

        /// <summary>
        ///  Check the aura count thats created by yourself by the name on specified unit
        /// </summary>
        /// <param name="aura"> The name of the aura in English. </param>
        /// <param name="unit"> The unit to check auras for. </param>
        /// <returns></returns>
        public static bool HasMyAura(this WoWUnit unit, string aura)
        {
            return HasMyAura(unit, aura, 0);
        }

        /// <summary>
        ///  Check the aura count thats created by yourself by the name on specified unit
        /// </summary>
        /// <param name="aura"> The name of the aura in English. </param>
        /// <param name="unit"> The unit to check auras for. </param>
        /// <param name="stacks"> The stack count of the aura to return true. </param>
        /// <returns></returns>
        public static bool HasMyAura(this WoWUnit unit, string aura, int stacks)
        {
            return HasAura(unit, aura, stacks, StyxWoW.Me);
        }

        private static bool HasAura(this WoWUnit unit, string aura, int stacks, WoWUnit creator)
        {
            if (unit == null)
                return false;
            return unit.GetAllAuras().Any(a => a.Name == aura && a.StackCount >= stacks && (creator == null || a.CreatorGuid == creator.Guid));
        }

        /// <summary>
        ///  Check the aura count thats created by yourself by the name on specified unit
        /// </summary>
        /// <param name="aura"> The name of the aura in English. </param>
        /// <param name="unit"> The unit to check auras for. </param>
        /// <returns></returns>
        public static bool HasMyAura(this WoWUnit unit, int id)
        {
            return HasMyAura(unit, id, 0);
        }

        /// <summary>
        ///  Check the aura count thats created by yourself by the name on specified unit
        /// </summary>
        /// <param name="aura"> The name of the aura in English. </param>
        /// <param name="unit"> The unit to check auras for. </param>
        /// <param name="stacks"> The stack count of the aura to return true. </param>
        /// <returns></returns>
        public static bool HasMyAura(this WoWUnit unit, int id, int stacks)
        {
            return HasAura(unit, id, stacks, StyxWoW.Me);
        }

        private static bool HasAura(this WoWUnit unit, int id, int stacks, WoWUnit creator)
        {
            return unit.GetAllAuras().Any(a => a.SpellId == id && a.StackCount >= stacks && (creator == null || a.CreatorGuid == creator.Guid));
        }

        /// <summary>
        ///  Checks for the auras on a specified unit. Returns true if the unit has any aura in the auraNames list.
        /// </summary>
        /// <param name="unit"> The unit to check auras for. </param>
        /// <param name="auraNames"> Aura names to be checked. </param>
        /// <returns></returns>
        public static bool HasAnyAura(this WoWUnit unit, params string[] auraNames)
        {
            var auras = unit.GetAllAuras();
            var hashes = new HashSet<string>(auraNames);
            return auras.Any(a => hashes.Contains(a.Name));
        }


        public static bool HasAnyAura(this WoWUnit unit, params int[] ids)
        {
            var auras = unit.GetAllAuras();
            var hashes = new HashSet<int>(ids);
            return auras.Any(a => hashes.Contains(a.SpellId));
        }


        /// <summary>
        ///  Checks for my auras on a specified unit. Returns true if the unit has any aura in the auraNames list applied by player.
        /// </summary>
        /// <param name="unit"> The unit to check auras for. </param>
        /// <param name="auraNames"> Aura names to be checked. </param>
        /// <returns></returns>
        public static bool HasAnyOfMyAuras(this WoWUnit unit, params string[] auraNames)
        {
            var auras = unit.GetAllAuras();
            var hashes = new HashSet<string>(auraNames);
            return auras.Any(a => a.CreatorGuid == StyxWoW.Me.Guid && hashes.Contains(a.Name));
        }


        /// <summary>
        /// aura considered expired if spell of same name as aura is known and aura not present or has less than specified time remaining
        /// </summary>
        /// <param name="u">unit</param>
        /// <param name="aura">name of aura with spell of same name that applies</param>
        /// <returns>true if spell known and aura missing or less than 'secs' time left, otherwise false</returns>
        public static bool HasAuraExpired(this WoWUnit u, string aura, int secs = 3, bool myAura = true)
        {
            return u.HasAuraExpired(aura, aura, secs, myAura);
        }


        /// <summary>
        /// aura considered expired if spell of same name as aura is known and aura not present or has less than specified time remaining
        /// </summary>
        /// <param name="u">unit</param>
        /// <param name="aura">name of aura with spell of same name that applies</param>
        /// <returns>true if spell known and aura missing or less than 'secs' time left, otherwise false</returns>
        public static bool HasAuraExpired(this WoWUnit u, string aura, TimeSpan tm, bool myAura = true)
        {
            return u.HasAuraExpired(aura, aura, tm, myAura);
        }


        /// <summary>
        /// aura considered expired if spell is known and aura not present or has less than specified time remaining
        /// </summary>
        /// <param name="u">unit</param>
        /// <param name="spell">spell that applies aura</param>
        /// <param name="aura">aura</param>
        /// <returns>true if spell known and aura missing or less than 'secs' time left, otherwise false</returns>
        public static bool HasAuraExpired(this WoWUnit u, string spell, string aura, int secs = 3, bool myAura = true)
        {
            return HasAuraExpired( u, spell, aura, TimeSpan.FromSeconds(3), myAura);
        }


        /// <summary>
        /// aura considered expired if spell is known and aura not present or has less than specified time remaining
        /// </summary>
        /// <param name="u">unit</param>
        /// <param name="spell">spell that applies aura</param>
        /// <param name="aura">aura</param>
        /// <returns>true if spell known and aura missing or less than 'secs' time left, otherwise false</returns>
        public static bool HasAuraExpired(this WoWUnit u, string spell, string aura, TimeSpan tm, bool myAura = true)
        {
            // need to compare millisecs even though seconds are provided.  otherwise see it as expired 999 ms early because
            // .. of loss of precision
            if (!SpellManager.HasSpell(spell))
                return false;

            TimeSpan timeLeft = u.GetAuraTimeLeft(aura, myAura);
            if (timeLeft.TotalSeconds <= tm.TotalSeconds)
                return true;

            return false;
        }


        /// <summary>
        /// aura considered expired if aura not present or less than specified time remaining.  
        /// differs from HasAuraExpired since it assumes you have learned the spell which applies it already
        /// </summary>
        /// <param name="u">unit</param>
        /// <param name="aura">aura</param>
        /// <returns>true aura missing or less than 'secs' time left, otherwise false</returns>
        public static bool HasKnownAuraExpired(this WoWUnit u, string aura, int secs = 3, bool myAura = true)
        {
            return u.GetAuraTimeLeft(aura, myAura).TotalSeconds < (double)secs;
        }


        /// <summary>
        ///  Checks for the auras on a specified unit. Returns true if the unit has any aura with any of the mechanics in the mechanics list.
        /// </summary>
        /// <param name="unit"> The unit to check auras for. </param>
        /// <param name="mechanics"> Mechanics to be checked. </param>
        /// <returns></returns>
        public static bool HasAuraWithMechanic(this WoWUnit unit, params WoWSpellMechanic[] mechanics)
        {
            if (unit == null)
                return false;

            var auras = unit.GetAllAuras();
            return auras.Any(a => mechanics.Contains(a.Spell.Mechanic));
        }

        /// <summary>
        ///  Checks for the auras on a specified unit. Returns true if the unit has any aura with any apply aura type in the list.
        /// </summary>
        /// <param name="unit"> The unit to check auras for. </param>
        /// <param name="mechanics"> Mechanics to be checked. </param>
        /// <returns></returns>
        public static bool HasAuraWithMechanic(this WoWUnit unit, params WoWApplyAuraType [] applyType)
        {
            if (unit == null)
                return false;

            var auras = unit.GetAllAuras();
            return auras.Any(a => a.Spell.SpellEffects.Any( se => applyType.Contains(se.AuraType)));
        }

        /// <summary>
        ///  Returns the timeleft of an aura by TimeSpan. Return TimeSpan.Zero if the aura doesn't exist.
        /// </summary>
        /// <param name="auraName"> The name of the aura in English. </param>
        /// <param name="onUnit"> The unit to check the aura for. </param>
        /// <param name="fromMyAura"> Check for only self or all buffs</param>
        /// <returns></returns>
        public static TimeSpan GetAuraTimeLeft(this WoWUnit onUnit, string auraName, bool fromMyAura = true)
        {
            if (onUnit == null)
                return TimeSpan.Zero;

            WoWAura wantedAura =
                onUnit.GetAllAuras().Where(a => a != null && a.Name == auraName && a.TimeLeft > TimeSpan.Zero && (!fromMyAura || a.CreatorGuid == StyxWoW.Me.Guid)).FirstOrDefault();

            return wantedAura != null ? wantedAura.TimeLeft : TimeSpan.Zero;
        }

        public static TimeSpan GetAuraTimeLeft(this WoWUnit onUnit, int auraID, bool fromMyAura = true)
        {
            if (onUnit == null)
                return TimeSpan.Zero;

            WoWAura wantedAura = onUnit.GetAllAuras()
                .Where(a => a.SpellId == auraID && a.TimeLeft > TimeSpan.Zero && (!fromMyAura || a.CreatorGuid == StyxWoW.Me.Guid)).FirstOrDefault();

            return wantedAura != null ? wantedAura.TimeLeft : TimeSpan.Zero;
        }

        public static uint GetAuraStacks(this WoWUnit onUnit, string auraName, bool fromMyAura = true)
        {
            if (onUnit == null)
                return 0;

            WoWAura wantedAura =
                onUnit.GetAllAuras().Where(a => a.Name == auraName && a.TimeLeft > TimeSpan.Zero && (!fromMyAura || a.CreatorGuid == StyxWoW.Me.Guid)).FirstOrDefault();

            if (wantedAura == null)
                return 0;

            return wantedAura.StackCount == 0 ? 1 : wantedAura.StackCount;
        }

        public static void CancelAura(this WoWUnit unit, string aura)
        {
            WoWAura a = unit.GetAuraByName( aura );
            if (a != null && a.Cancellable)
            {
                a.TryCancelAura();
                Logger.Write( Color.White, "/cancelaura: {0} #{1}", a.Name, a.SpellId);
            }
        }

        public static bool HasAnyShapeshift(this WoWUnit unit, params ShapeshiftForm[] forms)
        {
            ShapeshiftForm currentForm = StyxWoW.Me.Shapeshift;
            return forms.Any( f => f == currentForm);
        }


        public static bool IsNeutral(this WoWUnit unit)
        {
            return unit.GetReactionTowards(StyxWoW.Me) == WoWUnitReaction.Neutral;
        }

        /// <summary>
        /// Returns a list of resurrectable players in a 40 yard radius
        /// </summary>
        public static List<WoWPlayer> ResurrectablePlayers
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWPlayer>(false,false).Where(
                    p => !p.IsMe && p.IsDead && p.IsFriendly && p.IsInMyPartyOrRaid &&
                         p.DistanceSqr < 40 * 40 && !Blacklist.Contains(p.Guid, BlacklistFlags.Combat)).ToList();
            }
        }

        public static bool IsCrowdControlled(this WoWUnit unit)
        {
            Dictionary<string, WoWAura>.ValueCollection auras = unit.Auras.Values;

#if AURAS_HAVE_MECHANICS
            return auras.Any(
                a => a.Spell.Mechanic == WoWSpellMechanic.Banished ||
                     a.Spell.Mechanic == WoWSpellMechanic.Charmed ||
                     a.Spell.Mechanic == WoWSpellMechanic.Horrified ||
                     a.Spell.Mechanic == WoWSpellMechanic.Incapacitated ||
                     a.Spell.Mechanic == WoWSpellMechanic.Polymorphed ||
                     a.Spell.Mechanic == WoWSpellMechanic.Sapped ||
                     a.Spell.Mechanic == WoWSpellMechanic.Shackled ||
                     a.Spell.Mechanic == WoWSpellMechanic.Asleep ||
                     a.Spell.Mechanic == WoWSpellMechanic.Frozen ||
                     a.Spell.Mechanic == WoWSpellMechanic.Invulnerable ||
                     a.Spell.Mechanic == WoWSpellMechanic.Invulnerable2 ||
                     a.Spell.Mechanic == WoWSpellMechanic.Turned ||

                     // Really want to ignore hexed mobs.
                     a.Spell.Name == "Hex"

                     );
#else
            return unit.Stunned
                || unit.Rooted
                || unit.Fleeing 
                || unit.HasAuraWithEffect(
                        WoWApplyAuraType.ModConfuse, 
                        WoWApplyAuraType.ModCharm, 
                        WoWApplyAuraType.ModFear, 
                        // WoWApplyAuraType.ModDecreaseSpeed, 
                        WoWApplyAuraType.ModPacify, 
                        WoWApplyAuraType.ModPacifySilence, 
                        WoWApplyAuraType.ModPossess, 
                        WoWApplyAuraType.ModRoot, 
                        WoWApplyAuraType.ModStun );
#endif
        }

        // this one optimized for single applytype lookup
        public static bool HasAuraWithEffect(this WoWUnit unit, WoWApplyAuraType applyType)
        {
            return unit.Auras.Values.Any(a => a.Spell != null && a.Spell.SpellEffects.Any(se => applyType == se.AuraType));
        }

        public static bool HasAuraWithEffect(this WoWUnit unit, params WoWApplyAuraType[] applyType)
        {
            var hashes = new HashSet<WoWApplyAuraType>(applyType);
            return unit.Auras.Values.Any( a => a.Spell != null && a.Spell.SpellEffects.Any(se => hashes.Contains(se.AuraType)));
        }

        /// <summary>
        /// IsBoss() checks usually appear in a sequence testing same target repeatedly.  
        /// Cache the values for a fast return in that circumstanc
        /// </summary>
        private static ulong _lastIsBossGuid = 0;
        private static bool _lastIsBossResult = false;
   
        /// <summary>
        /// checks if unit is in list of bosses. cache of prior check kept for faster return in 
        /// instance behaviors which repeatedly test this as part of criteria for different
        /// cooldown casts
        /// </summary>
        /// <param name="unit">unit to test if they are a known boss</param>
        /// <returns>true: if boss</returns>
        public static bool IsBoss(this WoWUnit unit)
        {
            ulong guid = unit == null ? 0 : unit.Guid;
            if ( guid == _lastIsBossGuid )
                return _lastIsBossResult;

            _lastIsBossGuid = guid;
            _lastIsBossResult = unit != null && (Lists.BossList.CurrentMapBosses.Contains(unit.Name) || Lists.BossList.BossIds.Contains(unit.Entry));
            if (!_lastIsBossResult)
                _lastIsBossResult = IsTrainingDummy(unit);
            return _lastIsBossResult;
        }

        private const int BannerOfTheAlliance = 61573;
        private const int BannerOfTheHorde = 61574;
        
        public static bool IsTrainingDummy(this WoWUnit unit)
        {
            // return Lists.BossList.TrainingDummies.Contains(unit.Entry);
            int bannerId = StyxWoW.Me.IsHorde ? BannerOfTheAlliance : BannerOfTheHorde;
            return unit != null && unit.Level > 1 
                && ((unit.CurrentHealth == 1 && unit.MaxHealth < unit.Level) || unit.HasAura(bannerId));
        }

        /// <summary>
        /// checks if unit is targeting you, your minions, a group member, or group pets
        /// </summary>
        /// <param name="u">unit</param>
        /// <returns>true if targeting your guys, false if not</returns>
        public static bool IsTargetingMyStuff(this WoWUnit u)
        {
            return u.IsTargetingMeOrPet 
                || u.IsTargetingAnyMinion 
                || (u.GotTarget && u.CurrentTarget.IsCompanion());
        }

        public static bool IsCompanion(this WoWUnit u)
        {
            return u.CreatedByUnitGuid == StyxWoW.Me.Guid;
        }

        /// <summary>
        /// checks if unit is targeting you, your minions, a group member, or group pets
        /// </summary>
        /// <param name="u">unit</param>
        /// <returns>true if targeting your guys, false if not</returns>
        public static bool IsTargetingUs(this WoWUnit u)
        {
            return u.IsTargetingMyStuff() || Unit.GroupMemberInfos.Any(m => m.Guid == u.CurrentTargetGuid);
        }

        public static bool IsSensitiveDamage(this WoWUnit u,  int range = 0)
        {
            if (u.Guid == StyxWoW.Me.CurrentTargetGuid)
                return false;

            if (!u.Combat && !u.IsPlayer && u.IsNeutral())
                return true;

            if (range > 0 && u.SpellDistance() > range)
                return false;

            return u.IsCrowdControlled();
        }

        public static bool IsShredBoss(this WoWUnit unit)
        {
            return Lists.BossList.CanShredBoss.Contains(unit.Entry);
        }

        public static bool HasAuraWithEffect(this WoWUnit unit, WoWApplyAuraType auraType, int miscValue, int basePointsMin, int basePointsMax)
        {
            var auras = unit.GetAllAuras();
            return (from a in auras
                    where a.Spell != null
                    let spell = a.Spell
                    from e in spell.GetSpellEffects()
                    // First check: Ensure the effect is... well... valid
                    where e != null &&
                        // Ensure the aura type is correct.
                    e.AuraType == auraType &&
                        // Check for a misc value. (Resistance types, etc)
                    (miscValue == -1 || e.MiscValueA == miscValue) &&
                        // Check for the base points value. (Usually %s for most debuffs)
                    e.BasePoints >= basePointsMin && e.BasePoints <= basePointsMax
                    select a).Any();
        }
        public static bool HasSunders(this WoWUnit unit)
        {
            // Remember; this is negative values [debuff]. So min is -12, max is -4. Duh.
            return unit.HasAuraWithEffect(WoWApplyAuraType.ModResistancePct, 1, -12, -4);

            //var auras = unit.GetAllAuras();
            //var tmp = (from a in auras
            //           where a.Spell != null
            //           from e in a.Spell.SpellEffects
            //           // Sunder, Faerie Fire, and another have -4% armor per-stack.
            //           // Expose Armor, and others, have a flat -12%
            //           // Ensure we check MiscValueA for 1, as thats the resistance index for physical (aka; armor)
            //           where
            //               e != null && e.AuraType == WoWApplyAuraType.ModResistancePct && e.MiscValueA == 1 &&
            //               (e.BasePoints == -4 || e.BasePoints == -12)
            //           select a).Any();

            //return tmp;
        }
        public static bool HasDemoralizing(this WoWUnit unit)
        {
            // don't try if the unit is out of range.
            if (unit.DistanceSqr > 25)
                return true;

            // Plain and simple, any effect with -damage is good. Ensure at least -1. Since 0 may be a buggy spell entry or something.
            var tmp = unit.HasAuraWithEffect(WoWApplyAuraType.ModDamagePercentDone, -1, int.MinValue, -1);

            return tmp;

            //var auras = unit.GetAllAuras();
            //var tmp = (from a in auras
            //           where a.Spell != null && a.Spell.SpellEffect1 != null
            //           let effect = a.Spell.SpellEffect1
            //           // Basically, all spells are -10% damage done that are demoralizing shout/roar/etc.
            //           // The aura type is damage % done. Just chekc for anything < 0. (There may be some I'm forgetting that aren't -10%, but stacks of like 2% or something
            //           where effect.AuraType == WoWApplyAuraType.ModDamagePercentDone && effect.BasePoints < 0
            //           select a).Any();
            //if (!tmp)
            //    Logger.Write(unit.Name + " does not have demoralizing!");
            //return tmp;
        }

        public static bool HasBleedDebuff(this WoWUnit unit)
        {
            return unit.HasAuraWithEffect(WoWApplyAuraType.ModMechanicDamageTakenPercent, 15, 0, int.MaxValue);
        }

        /// <summary>A temporary fix until the next build of HB.</summary>
        static SpellEffect[] GetSpellEffects(this WoWSpell spell)
        {
            SpellEffect[] effects = new SpellEffect[3];
            effects[0] = spell.GetSpellEffect(0);
            effects[1] = spell.GetSpellEffect(1);
            effects[2] = spell.GetSpellEffect(2);
            return effects;
        }

        public static bool IsInGroup(this LocalPlayer me)
        {
            return me.GroupInfo.IsInParty || me.GroupInfo.IsInRaid;
        }

        private static string _lastGetPredictedError;
        public static float GetVerifiedGetPredictedHealthPercent(this WoWUnit unit, bool includeMyHeals = false)
        {
            float hbhp = unit.GetPredictedHealthPercent(includeMyHeals);
#if false
            Styx.Patchables.IncomingHeal[] heals = unit.IncomingHealsArray().ToArray();
            uint myhealth = unit.CurrentHealth;

            uint myincoming = TotalIncomingHeals(heals, includeMyHeals);
            float mypredict = (100f * (myhealth + myincoming)) / unit.MaxHealth;

            if (Math.Abs(mypredict - hbhp) < 2f)
                return hbhp;

            string msg = string.Format("Predict Error=WoWUnit.GetPredictedHealthPercent({0}) returned {1:F1}% for {2} with MyPredict={3:F1}% and HealthPercent={4:F1}%", includeMyHeals, hbhp, unit.SafeName(), mypredict, myhealth);
            if (msg != _lastGetPredictedError)
            {
                _lastGetPredictedError = msg;
                Logger.WriteDebug(System.Drawing.Color.Pink, msg);
            }

            hbhp = Math.Min(hbhp, mypredict);
#endif
            return hbhp;
        }

        private static uint TotalIncomingHeals( Styx.Patchables.IncomingHeal[] heals, bool includeMyHeals = false)
        {
            uint aggcheck = heals
                .Where(heal => includeMyHeals || heal.OwnerGuid != StyxWoW.Me.Guid)
                .Aggregate(0u, (current, heal) => current + heal.HealAmount);
#if false
            uint myincoming = 0;
            foreach (var heal in heals)
            {
                if (includeMyHeals || heal.OwnerGuid != StyxWoW.Me.Guid)
                    myincoming += heal.HealAmount;
            }

            uint sumcheck = (uint) heals
                .Where(heal => includeMyHeals || heal.OwnerGuid != StyxWoW.Me.Guid)
                .Sum( heal => (long) heal.HealAmount);                

            if ( myincoming != aggcheck || aggcheck != sumcheck)
            {
                Logger.WriteDiagnostic(Color.HotPink, "Accuracy Error= my={0}  agg={1}  sum={2}", myincoming, aggcheck, sumcheck);
            }
#endif
            return aggcheck;
        }


        public static IncomingHeal[] LocalIncomingHeals(this WoWUnit unit)
        {
            // Reversing note: CGUnit_C::GetPredictedHeals
            const int PredictedHealsCount = 0x1374;
            const int PredictedHealsArray = 0x1378;

            Debug.Assert(unit != null);
            uint health = unit.CurrentHealth;
            var incomingHealsCnt = StyxWoW.Memory.Read<int>(unit.BaseAddress + PredictedHealsCount);
            if (incomingHealsCnt == 0)
                return new IncomingHeal[0];

            var incomingHealsListPtr = StyxWoW.Memory.Read<IntPtr>(unit.BaseAddress + PredictedHealsArray);

            var heals = StyxWoW.Memory.ReadArray<IncomingHeal>(incomingHealsListPtr, incomingHealsCnt);
            return heals;
        }


        public static uint LocalGetPredictedHealthDebug(this WoWUnit unit, bool includeMyHeals = false)
        {
            // Reversing note: CGUnit_C::GetPredictedHeals
            const int PredictedHealsCount = 0x1494;
            const int PredictedHealsArray = 0x1498;
            uint maxHealth = unit.MaxHealth;

            Debug.Assert(unit != null);
            uint health = unit.CurrentHealth;
            var incomingHealsCnt = StyxWoW.Memory.Read<int>(unit.BaseAddress + PredictedHealsCount);
            if (incomingHealsCnt == 0)
            {
                Logger.WriteDiagnostic( "  0 incoming heals");
                return health;
            }

            var incomingHealsListPtr = StyxWoW.Memory.Read<IntPtr>(unit.BaseAddress + PredictedHealsArray);
            var heals = StyxWoW.Memory.ReadArray<IncomingHeal>(incomingHealsListPtr, incomingHealsCnt);

            StringBuilder sb = new StringBuilder();
            sb.Append( "\n");

            uint inchealth = 0;
            foreach ( var heal in heals)
            {
                if (includeMyHeals && heal.OwnerGuid == StyxWoW.Me.Guid)
                    continue;
                WoWUnit owner = ObjectManager.GetObjectByGuid<WoWUnit>(heal.OwnerGuid);
                WoWSpell spell = WoWSpell.FromId(heal.spellId);
                uint HealPct = (heal.HealAmount * 100) / maxHealth;

                sb.Append(
                    string.Format("  {0} {1}% {2} {3} {4} {5}\r\n", 
                        heal.IsHealOverTime.ToYN(),
                        HealPct.ToString().PadLeft(3),
                        heal.HealAmount.ToString().PadLeft(6),
                        spell.Name.PadLeft(15).Substring(0, 15),
                        spell.Id,
                        owner.Name
                        )
                    );

                inchealth += heal.HealAmount;
            }

            sb.Append( "   Total Incoming Heals = ");
            sb.Append( (inchealth * 100 / maxHealth).ToString().PadLeft(3));
            sb.Append( "% ");
            sb.Append( inchealth.ToString().PadLeft(6));
            sb.Append("  Predicted Health Pct = ");
            sb.Append( (((float) health + inchealth) * 100 / unit.MaxHealth).ToString("F1"));
            sb.Append( "%\r\n");

            Logger.WriteDiagnostic( sb.ToString());

            return health + inchealth;
        }

        public static float LocalGetPredictedHealthPercentDebug(this WoWUnit unit, bool includeMyHeals = false)
        {
             return (float)unit.LocalGetPredictedHealth(includeMyHeals) * 100 / unit.MaxHealth;
        }

        public static uint LocalGetPredictedHealth(this WoWUnit unit, bool includeMyHeals = false)
        {
            // Reversing note: CGUnit_C::GetPredictedHeals
            const int PredictedHealsCount = 0x1374;
            const int PredictedHealsArray = 0x1378;

            Debug.Assert(unit != null);
            uint health = unit.CurrentHealth;
            var incomingHealsCnt = StyxWoW.Memory.Read<int>(unit.BaseAddress + PredictedHealsCount);
            if (incomingHealsCnt == 0)
                return health;

            var incomingHealsListPtr = StyxWoW.Memory.Read<IntPtr>(unit.BaseAddress + PredictedHealsArray);

            var heals = StyxWoW.Memory.ReadArray<IncomingHeal>(incomingHealsListPtr, incomingHealsCnt);
            return heals.Where(heal => includeMyHeals || heal.OwnerGuid != StyxWoW.Me.Guid)
                .Aggregate(health, (current, heal) => current + heal.HealAmount);
        }

        public static float LocalGetPredictedHealthPercent(this WoWUnit unit, bool includeMyHeals = false)
        {
            return (float)unit.LocalGetPredictedHealth(includeMyHeals) * 100 / unit.MaxHealth;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct IncomingHeal
        {
            public ulong OwnerGuid;
            public int spellId;
            private int _dword_C;
            public uint HealAmount;
            private byte _isHealOverTime; // includes chaneled spells.
            private byte _byte_15; // unknown value
            private byte _byte_16; // unused
            private byte _byte_17; // unused

            public bool IsHealOverTime { get { return _isHealOverTime == 1; } }
        }

        private static bool lastMovingAwayAnswer = false;
        private static ulong guidLastMovingAwayCheck = 0;
        private static double distLastMovingAwayCheck = 0f;
        private static readonly WaitTimer MovingAwayTimer = new WaitTimer(TimeSpan.FromMilliseconds(500));

        public static bool CurrentTargetIsMovingAwayFromMe
        {
            get
            {
                if (guidLastMovingAwayCheck != StyxWoW.Me.CurrentTargetGuid || StyxWoW.Me.CurrentTargetGuid == 0)
                {
                    lastMovingAwayAnswer = false;
                    if (StyxWoW.Me.CurrentTarget == null)
                        guidLastMovingAwayCheck = 0;
                    else
                    {
                        guidLastMovingAwayCheck = StyxWoW.Me.CurrentTargetGuid;
                        distLastMovingAwayCheck = StyxWoW.Me.CurrentTarget.Distance;
                        MovingAwayTimer.Reset();
                    }
                }
                else if ( MovingAwayTimer.IsFinished )
                {
                    double currentDistance = StyxWoW.Me.CurrentTarget.Distance;
                    double changeInDistance = currentDistance - distLastMovingAwayCheck;
                    lastMovingAwayAnswer = changeInDistance > 0;
                    distLastMovingAwayCheck = currentDistance;
                    MovingAwayTimer.Reset();
                }

                return lastMovingAwayAnswer ;
            }
        }

        public static IEnumerable<WoWUnit> MobsAttackingTank()
        {
            return Unit.NearbyUnfriendlyUnits.Where(u => Group.Tanks.Any( t => t.IsAlive && t.Guid == u.CurrentTargetGuid));
        }

        public static WoWUnit LowestHealthMobAttackingTank()
        {
            return MobsAttackingTank().OrderBy(u => u.HealthPercent).FirstOrDefault();
        }

        public static WoWUnit HighestHealthMobAttackingTank()
        {
            return MobsAttackingTank().OrderByDescending(u => u.HealthPercent).FirstOrDefault();
        }

        /// <summary>
        /// Calls the UnitCanAttack LUA to check if current target is attackable. This is
        /// necessary because the WoWUnit.Attackable property returns 'true' when targeting
        /// any enemy player including in Sanctuary, not PVP flagged, etc where a player
        /// is not attackable
        /// </summary>
        public static bool CanWeAttack(this WoWUnit unit)
        {
            if (unit == null)
                return false;

            bool canAttack = false;

            if (unit.Guid == StyxWoW.Me.CurrentTargetGuid)
                canAttack = Lua.GetReturnVal<bool>("return UnitCanAttack(\"player\",\"target\")", 0);
            else
            {
                WoWUnit focusSave = StyxWoW.Me.FocusedUnit;
                StyxWoW.Me.SetFocus(unit);
                canAttack = Lua.GetReturnVal<bool>("return UnitCanAttack(\"player\",\"focus\")", 0);
                if (focusSave == null || !focusSave.IsValid)
                    StyxWoW.Me.SetFocus(0);
                else
                    StyxWoW.Me.SetFocus(focusSave);
            }

            return canAttack;
        }
    }

    // following class should probably be in Unit, but made a separate 
    // .. extension class to separate the private property names.
    // --
    // credit to Handnavi.  the following is a wrapping of his code
    public static class TimeToDeathExtension
    {
        public static ulong guid { get; set; }  // guid of mob

        private static uint _firstLife;         // life of mob when first seen
        private static uint _firstLifeMax;      // max life of mob when first seen
        private static int _firstTime;          // time mob was first seen
        private static uint _currentLife;       // life of mob now
        private static int _currentTime;        // time now

        /// <summary>
        /// seconds until the target dies.  first call initializes values. subsequent
        /// return estimate or indeterminateValue if death can't be calculated
        /// </summary>
        /// <param name="target">unit to monitor</param>
        /// <param name="indeterminateValue">return value if death cannot be calculated ( -1 or int.MaxValue are common)</param>
        /// <returns>number of seconds </returns>
        public static long TimeToDeath(this WoWUnit target, long indeterminateValue = -1)
        {
            if (target == null || !target.IsValid || !target.IsAlive)
            {
                //Logging.Write("TimeToDeath: {0} (GUID: {1}, Entry: {2}) is dead!", target.SafeName(), target.Guid, target.Entry);
                return 0;
            }

            if (StyxWoW.Me.CurrentTarget.IsTrainingDummy())
            {
                return 111;     // pick a magic number since training dummies dont die
            }

            //Fill variables on new target or on target switch, this will loose all calculations from last target
            if (guid != target.Guid || (guid == target.Guid && target.CurrentHealth == _firstLifeMax))
            {
                guid = target.Guid;
                _firstLife = target.CurrentHealth;
                _firstLifeMax = target.MaxHealth;
                _firstTime = ConvDate2Timestam(DateTime.Now);
                //Lets do a little trick and calculate with seconds / u know Timestamp from unix? we'll do so too
            }
            _currentLife = target.CurrentHealth;
            _currentTime = ConvDate2Timestam(DateTime.Now);
            int timeDiff = _currentTime - _firstTime;
            uint hpDiff = _firstLife - _currentLife;
            if (hpDiff > 0)
            {
                /*
                * Rule of three (Dreisatz):
                * If in a given timespan a certain value of damage is done, what timespan is needed to do 100% damage?
                * The longer the timespan the more precise the prediction
                * time_diff/hp_diff = x/first_life_max
                * x = time_diff*first_life_max/hp_diff
                * 
                * For those that forgot, http://mathforum.org/library/drmath/view/60822.html
                */
                long fullTime = timeDiff * _firstLifeMax / hpDiff;
                long pastFirstTime = (_firstLifeMax - _firstLife) * timeDiff / hpDiff;
                long calcTime = _firstTime - pastFirstTime + fullTime - _currentTime;
                if (calcTime < 1) calcTime = 1;
                //calc_time is a int value for time to die (seconds) so there's no need to do SecondsToTime(calc_time)
                long timeToDie = calcTime;
                //Logging.Write("TimeToDeath: {0} (GUID: {1}, Entry: {2}) dies in {3}, you are dpsing with {4} dps", target.SafeName(), target.Guid, target.Entry, timeToDie, dps);
                return timeToDie;
            }
            if (hpDiff <= 0)
            {
                //unit was healed,resetting the initial values
                guid = target.Guid;
                _firstLife = target.CurrentHealth;
                _firstLifeMax = target.MaxHealth;
                _firstTime = ConvDate2Timestam(DateTime.Now);
                //Lets do a little trick and calculate with seconds / u know Timestamp from unix? we'll do so too
                //Logging.Write("TimeToDeath: {0} (GUID: {1}, Entry: {2}) was healed, resetting data.", target.SafeName(), target.Guid, target.Entry);
                return indeterminateValue;
            }
            if (_currentLife == _firstLifeMax)
            {
                //Logging.Write("TimeToDeath: {0} (GUID: {1}, Entry: {2}) is at full health.", target.SafeName(), target.Guid, target.Entry);
                return indeterminateValue;
            }
            //Logging.Write("TimeToDeath: {0} (GUID: {1}, Entry: {2}) no damage done, nothing to calculate.", target.SafeName(), target.Guid, target.Entry);
            return indeterminateValue;
        }


        private static readonly DateTime timeOrigin = new DateTime(2012, 1, 1); // Refernzdatum (festgelegt)

        private static int ConvDate2Timestam(DateTime time)
        {
#if PREV
                DateTime baseLine = new DateTime(1970, 1, 1); // Refernzdatum (festgelegt)
                DateTime date2 = time; // jetztiges Datum / Uhrzeit
                var ts = new TimeSpan(date2.Ticks - baseLine.Ticks); // das Delta ermitteln
                // Das Delta als gesammtzahl der sekunden ist der Timestamp
                return (Convert.ToInt32(ts.TotalSeconds));
#else
            return (int)(time - timeOrigin).TotalSeconds;
#endif
        }

        /// <summary>
        /// creates behavior to write timetodeath values to debug log.  only
        /// evaluated if Singular Debug setting is enabled
        /// </summary>
        /// <returns></returns>
        public static Composite CreateWriteDebugTimeToDeath()
        {
            return new Action(
                ret =>
                {
                    if (SingularSettings.Debug && StyxWoW.Me.CurrentTarget != null)
                    {
                        long timeNow = StyxWoW.Me.CurrentTarget.TimeToDeath();
                        if (timeNow != lastReportedTime || guid != StyxWoW.Me.CurrentTargetGuid )
                        {
                            lastReportedTime = timeNow;
                            Logger.WriteFile( "TimeToDeath: {0} (GUID: {1}, Entry: {2}) dies in {3}", 
                                StyxWoW.Me.CurrentTarget.SafeName(),
                                StyxWoW.Me.CurrentTarget.Guid,
                                StyxWoW.Me.CurrentTarget.Entry, 
                                lastReportedTime);
                        }
                    }

                    return RunStatus.Failure;
                });

        }

        private static long lastReportedTime = -111;

    }

}
