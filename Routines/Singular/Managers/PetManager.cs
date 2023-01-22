
using System;
using System.Collections.Generic;
using System.Linq;
using Styx;

using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWCache;
using Styx.WoWInternals.WoWObjects;
using Singular.Helpers;
using Styx.TreeSharp;

using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using System.Drawing;

namespace Singular.Managers
{

    internal class PetManager
    {
        public static readonly WaitTimer CallPetTimer = WaitTimer.OneSecond;

        private static ulong _petGuid;
        private static readonly List<WoWPetSpell> PetSpells = new List<WoWPetSpell>();
        public static readonly WaitTimer PetSummonAfterDismountTimer = new WaitTimer(TimeSpan.FromSeconds(2));

        private static bool _wasMounted;

        static PetManager()
        {
            // NOTE: This is a bit hackish. This fires VERY OFTEN in major cities. But should prevent us from summoning right after dismounting.
            // Lua.Events.AttachEvent("COMPANION_UPDATE", (s, e) => CallPetTimer.Reset());
            // Note: To be changed to OnDismount with new release
            Mount.OnDismount += (s, e) =>
                {
                    if (StyxWoW.Me.Class == WoWClass.Hunter || StyxWoW.Me.Class == WoWClass.Warlock || 
                        StyxWoW.Me.PetNumber > 0)
                    {
                        PetSummonAfterDismountTimer.Reset();
                    }
                };

            // force us to check initially upon load
            NeedToCheckPetTauntAutoCast = true;

            // Lets hook this event so we can disable growl
            SingularRoutine.OnWoWContextChanged += PetManager_OnWoWContextChanged;
        }

        public static bool HavePet { get { return StyxWoW.Me.GotAlivePet; } }

        public static string GetPetTalentTree()
        {
            return Lua.GetReturnVal<string>("return GetPetTalentTree()", 0);
        }

        public static string WantedPet { get; set; }

        internal static void Pulse()
        {
            if (StyxWoW.Me.Mounted)
            {
                _wasMounted = true;
            }

            if (_wasMounted && !StyxWoW.Me.Mounted)
            {
                _wasMounted = false;
                PetSummonAfterDismountTimer.Reset();
            }

            if (StyxWoW.Me.Pet != null)
            {
                if (_petGuid != StyxWoW.Me.Pet.Guid)
                {
                    // clear any existing spells
                    PetSpells.Clear();

                    // only load spells if we have one that is non-null
                    // .. as initial load happens before Me.PetSpells is initialized and we were saving 'null' spells
                    if (StyxWoW.Me.PetSpells.Any(s => s.Spell != null))
                    {
                        NeedToCheckPetTauntAutoCast = true;

                        // Cache the list. yea yea, we should just copy it, but I'd rather have shallow copies of each object, rather than a copy of the list.
                        PetSpells.AddRange(StyxWoW.Me.PetSpells);
                        PetSummonAfterDismountTimer.Reset();
                        _petGuid = StyxWoW.Me.Pet.Guid;

                        Logger.WriteDebug("---PetSpells Loaded for: {0} Pet ---", PetManager.GetPetTalentTree());
                        foreach (var sp in PetSpells)
                        {
                            if (sp.Spell == null)
                                Logger.WriteDebug("   {0} spell={1}  Action={0}", sp.ActionBarIndex, sp.ToString(), sp.Action.ToString());
                            else
                                Logger.WriteDebug("   {0} spell={1} #{2}", sp.ActionBarIndex, sp.ToString(), sp.Spell.Id);
                        }
                        Logger.WriteDebug(" ");
                    }
                }

                HandleAutoCast();
            }

            if (!StyxWoW.Me.GotAlivePet)
            {
                PetSpells.Clear();
            }
        }

        public static bool Attack(WoWUnit unit)
        {
            if ( unit == null || StyxWoW.Me.Pet == null || StyxWoW.Me.Pet.CurrentTargetGuid == unit.Guid)
                return false;

            Logger.Write(Color.White, "/petattack on {0} @ {1:F1} yds", unit.SafeName(), unit.SpellDistance());
            PetManager.CastPetAction("Attack", unit);
            return true;
        }

        public static bool CanCastPetAction(string action)
        {
            if (StyxWoW.Me.Level < 10)
                return false;

            WoWPetSpell petAction = PetSpells.FirstOrDefault(p => p.ToString() == action);
            if (petAction == null)
                return false;

            if (petAction.Cooldown)
                return false;

            if (petAction.Spell != null)
                return petAction.Spell.CanCast;

            return Lua.GetReturnVal<bool>(string.Format("return GetPetActionSlotUsable({0})", petAction.ActionBarIndex + 1), 0);
        }

        public static void CastPetAction(string action)
        {
            WoWPetSpell spell = PetSpells.FirstOrDefault(p => p.ToString() == action);
            if (spell == null)
                return;

            Logger.Write(Color.DeepSkyBlue, "[Pet] Casting {0}", action);
            Lua.DoString("CastPetAction({0})", spell.ActionBarIndex + 1);
        }

        public static void CastPetAction(string action, WoWUnit on)
        {
            WoWPetSpell spell = PetSpells.FirstOrDefault(p => p.ToString() == action);
            if (spell == null)
                return;

            Logger.Write(Color.DeepSkyBlue, "[Pet] Casting {0} on {1} @ {2:F1} yds", action, on.SafeName(), on.SpellDistance());
            if (on.Guid == StyxWoW.Me.CurrentTargetGuid)
            {
                Logger.WriteDebug("CastPetAction: cast [{0}] specifying CurrentTarget", action);
                Lua.DoString("CastPetAction({0}, 'target')", spell.ActionBarIndex + 1);
            }
            else
            {
                WoWUnit save = StyxWoW.Me.FocusedUnit;
                StyxWoW.Me.SetFocus(on);
                Logger.WriteDebug("CastPetAction: cast [{0}] specifying FocusTarget {1}", action, on.SafeName());
                Lua.DoString("CastPetAction({0}, 'focus')", spell.ActionBarIndex + 1);
                StyxWoW.Me.SetFocus(save == null ? 0 : save.Guid);
            }
        }

        /// <summary>
        /// behavior form of CastPetAction().  note that this Composite will return RunStatus.Success
        /// if it appears the ability was cast.  this is to trip the Throttle wrapping it internally
        /// -and- to allow cascaded sequences of Pet Abilities.  Note: Pet Abilities are not on the
        /// GCD, so you can safely allow execution to continue even on Success
        /// </summary>
        /// <param name="action">pet ability</param>
        /// <param name="onUnit">unit deleg to cast on (null if current target)</param>
        /// <returns></returns>
        public static Composite CastAction(string action, UnitSelectionDelegate onUnit = null)
        {
            return new Throttle( TimeSpan.FromMilliseconds(750), 
                new Action(ret =>
                {
                    if ( !CanCastPetAction(action))
                        return RunStatus.Failure;

                    WoWUnit target;
                    if (onUnit == null)
                        target = StyxWoW.Me.CurrentTarget;
                    else
                        target = onUnit(ret);

                    if (target == null)
                        return RunStatus.Failure;

                    if (target.Guid == StyxWoW.Me.CurrentTargetGuid)
                        CastPetAction(action);
                    else
                        CastPetAction(action, target);

                    return RunStatus.Success;
                }));
        }

        //public static void EnableActionAutocast(string action)
        //{
        //    var spell = PetSpells.FirstOrDefault(p => p.ToString() == action);
        //    if (spell == null)
        //        return;

        //    var index = spell.ActionBarIndex + 1;
        //    Logger.Write("[Pet] Enabling autocast for {0}", action, index);
        //    Lua.DoString("local index = " + index + " if not select(6, GetPetActionInfo(index)) then TogglePetAutocast(index) end");
        //}

        /// <summary>
        ///   Calls a pet by name, if applicable.
        /// </summary>
        /// <remarks>
        ///   Created 2/7/2011.
        /// </remarks>
        /// <param name = "petName">Name of the pet. This parameter is ignored for mages. Warlocks should pass only the name of the pet. Hunters should pass which pet (1, 2, etc)</param>
        /// <returns>true if it succeeds, false if it fails.</returns>
        public static bool CallPet(string petName)
        {
            if (!CallPetTimer.IsFinished)
            {
                return false;
            }

            switch (StyxWoW.Me.Class)
            {
                case WoWClass.Warlock:
                    if (Spell.CanCastHack("Summon " + petName))
                    {
                        Logger.Write(Color.DeepSkyBlue, "[Pet] Calling out my {0}", petName);
                        bool result = SpellManager.Cast("Summon " + petName);
                        return result;
                    }
                    break;

                case WoWClass.Mage:
                    if (Spell.CanCastHack("Summon Water Elemental"))
                    {
                        Logger.Write(Color.DeepSkyBlue, "[Pet] Calling out Water Elemental");
                        bool result = SpellManager.Cast("Summon Water Elemental");
                        return result;
                    }
                    break;

                case WoWClass.Hunter:
                    if (Spell.CanCastHack("Call Pet " + petName))
                    {
                        if (!StyxWoW.Me.GotAlivePet)
                        {
                            Logger.Write(Color.DeepSkyBlue, "[Pet] Calling out pet #{0}", petName);
                            bool result = SpellManager.Cast("Call Pet " + petName);
                            return result;
                        }
                    }
                    break;
            }
            return false;
        }

        public static bool IsAutoCast(int id, out bool allowed)
        {
            WoWPetSpell ps = StyxWoW.Me.PetSpells.FirstOrDefault(s => s.Spell != null && s.Spell.Id == id);
            return IsAutoCast(ps, out allowed);
        }

        public static bool IsAutoCast(string action, out bool allowed)
        {
            WoWPetSpell ps = StyxWoW.Me.PetSpells.FirstOrDefault(s => s.ToString() == action);
            return IsAutoCast(ps, out allowed);
        }

        public static bool IsAutoCast(WoWPetSpell ps, out bool allowed)
        {
            allowed = false;
            if (ps != null)
            {
                // action bar index base 0 in HB but base 1 in LUA, so adjust
                List<string> svals = Lua.GetReturnValues("return GetPetActionInfo(" + (ps.ActionBarIndex+1) + ");");
                if (svals != null && svals.Count >= 7)
                {                  
                    allowed = ("1" == svals[5]);
                    bool active = ("1" == svals[6]);
                    return active;
                }
            }

            return false;
        }

        #region Manage Growl for Instances

        // flag used to indicate need to check; set anywhere but handled within Pulse()
        private static bool NeedToCheckPetTauntAutoCast { get; set; }
        private static bool PetSpellsAvailableAfterNeedToCheck { get; set; }

        // set needtocheck flag anytime context changes
        static void PetManager_OnWoWContextChanged(object sender, WoWContextEventArg e)
        {
            NeedToCheckPetTauntAutoCast = true;
            PetSpellsAvailableAfterNeedToCheck = false;
        }

        public static void HandleAutoCast()
        {
            if ( NeedToCheckPetTauntAutoCast)
            {
                if (StyxWoW.Me.GotAlivePet)
                {
                    if (!IsAnySpellOnPetToolbar("Growl", "Taunt", "Thunderstomp", "Suffering"))
                    {
                        NeedToCheckPetTauntAutoCast = false;
                    }
                    else if (CanWeCheckAutoCastForAnyOfThese("Growl", "Taunt", "Thunderstomp", "Suffering"))
                    {
                        if (StyxWoW.Me.Class == WoWClass.Hunter)
                        {
                            NeedToCheckPetTauntAutoCast = !(HandleAutoCastForSpell("Growl") || HandleAutoCastForSpell("Taunt") || HandleAutoCastForSpell("Thunderstomp"));
                        }
                        else if (StyxWoW.Me.Class == WoWClass.Warlock && Singular.ClassSpecific.Warlock.Common.GetCurrentPet() == Settings.WarlockPet.Voidwalker)
                        {
                            NeedToCheckPetTauntAutoCast = !HandleAutoCastForSpell("Suffering");
                        }
                    }
                }
            }
        }

        public static bool IsAnySpellOnPetToolbar(params string[] spells)
        {
            if (!StyxWoW.Me.GotAlivePet)
                return false;

            if (spells == null || !spells.Any())
                return false;

            if (StyxWoW.Me.PetSpells == null)
                return false;

            HashSet<string> taunt = new HashSet<string>(spells);
            WoWPetSpell ps = StyxWoW.Me.PetSpells.FirstOrDefault(s => s.Spell != null && taunt.Contains(s.Spell.Name));

            return ps != null;
        }

        public static bool CanWeCheckAutoCastForAnyOfThese(params string[] spells)
        {
            if (!StyxWoW.Me.GotAlivePet)
                return false;

            if (spells == null || !spells.Any())
                return false;

            if (StyxWoW.Me.PetSpells == null)
                return false;

            HashSet<string> taunt = new HashSet<string>(spells);
            WoWPetSpell ps = StyxWoW.Me.PetSpells.FirstOrDefault(s => s.Spell != null && taunt.Contains(s.Spell.Name));

            if (ps == null)
                return false;

            bool allowed;
            bool active = PetManager.IsAutoCast(ps, out allowed);
            if (!allowed)
                return false;

            return true;
        }

        private static bool HandleAutoCastForSpell(string spellName)
        {
            WoWPetSpell ps = StyxWoW.Me.PetSpells.FirstOrDefault(s => s.ToString() == spellName);

            // Disable pet growl in instances but enable it outside.
            if (ps == null)
                Logger.WriteDebug("PetManager: '{0}' is NOT an ability known by this Pet", spellName);
            else
            {
                bool allowed;
                bool active = PetManager.IsAutoCast(ps, out allowed);
                if (!allowed)
                    Logger.Write(Color.White, "PetManager: '{0}' is NOT an auto-cast ability for this Pet", spellName);
                else
                {
                    if (SingularRoutine.CurrentWoWContext == WoWContext.Instances)
                    {
                        if (!active)
                            Logger.Write(Color.White, "PetManager: '{0}' Auto-Cast Already Disabled", spellName);
                        else
                        {
                            Logger.Write(Color.White, "PetManager: Disabling '{0}' Auto-Cast", spellName);
                            Lua.DoString("DisableSpellAutocast(GetSpellInfo(" + ps.Spell.Id + "))");
                        }
                    }
                    else
                    {
                        if (active)
                            Logger.Write(Color.White, "PetManager: '{0}' Auto-Cast Already Enabled", spellName);
                        else
                        {
                            Logger.Write(Color.White, "PetManager: Enabling '{0}' Auto-Cast", spellName);
                            Lua.DoString("EnableSpellAutocast(GetSpellInfo(" + ps.Spell.Id + "))");
                        }
                    }

                    return true;
                }
            }

            return false;
        }

        #endregion

    }

}