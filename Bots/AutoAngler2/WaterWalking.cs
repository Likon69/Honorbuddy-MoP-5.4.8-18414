using System;
using System.Diagnostics;
using System.Linq;
using Styx;
using Styx.CommonBot;
using Styx.Helpers;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace HighVoltz.AutoAngler
{
    public class WaterWalking
    {
        private static readonly Stopwatch _recastSW = new Stopwatch();

        private static string[] _waterWalkingAbilities = { "Levitate", "Water Walking", "Path of Frost" };

        public static bool CanCast
        {
            get
            {
				return AutoAnglerBot.Instance.MySettings.UseWaterWalking &&
                       (SpellManager.HasSpell("Levitate") || // priest levitate
                        SpellManager.HasSpell("Water Walking") || // shaman water walking
                        SpellManager.HasSpell("Path of Frost") || // Dk Path of frost
                        SpellManager.HasSpell("Soulburn") || // Affliction Warlock
                        StyxWoW.Me.HasAura("Still Water") || // hunter with water strider pet.
                        Utils.IsItemInBag(8827) || //isItemInBag(8827);
                        Utils.IsItemInBag(85500)); // Anglers Fishing Raft
            }
        }


        public static bool IsActive
        {
            get
            {
                // DKs have 2 Path of Frost auras. only one can be stored in WoWAuras at any time. 

                return StyxWoW.Me.Auras.Values.Any(a => (StyxWoW.Me.HasAura("Levitate") || StyxWoW.Me.HasAura("Anglers Fishing Raft") || StyxWoW.Me.HasAura("Water Walking") || StyxWoW.Me.HasAura("Unending Breath")) && a.TimeLeft >= new TimeSpan(0, 0, 20)) ||
                       StyxWoW.Me.HasAura("Path of Frost") || StyxWoW.Me.HasAura("Surface Trot");
            }
        }

        public static bool Cast()
        {
            WoWItem fishingRaft;
            WoWItem waterPot;

            bool casted = false;
            if (!IsActive)
            {
                if (_recastSW.IsRunning && _recastSW.ElapsedMilliseconds < 5000)
                    return false;
                _recastSW.Reset();
                _recastSW.Start();
                int waterwalkingSpellID = 0;
                switch (StyxWoW.Me.Class)
                {
                    case WoWClass.Priest:
                        waterwalkingSpellID = 1706;
                        break;
                    case WoWClass.Shaman:
                        waterwalkingSpellID = 546;
                        break;
                    case WoWClass.DeathKnight:
                        waterwalkingSpellID = 3714;
                        break;
                    case WoWClass.Warlock:
                        waterwalkingSpellID = 5697;
                        break;
                    case WoWClass.Hunter:
                        // cast Surface Trot if Water Strider pet is active.
                        if (StyxWoW.Me.HasAura("Still Water"))
                            waterwalkingSpellID = 126311;
                        break;
                }
                if (waterwalkingSpellID != 0 && (SpellManager.CanCast(waterwalkingSpellID) || StyxWoW.Me.HasAura("Still Water")))
                {
                    if (StyxWoW.Me.Class == WoWClass.Warlock)
                        SpellManager.Cast(74434); //cast Soulburn

                    //SpellManager.Cast(waterwalkingSpellID);
                    // use lua to cast spells because SpellManager.Cast can't handle pet spells.
                    Lua.DoString("CastSpellByID ({0})", waterwalkingSpellID);
                    casted = true;
                }
                else if ((waterPot = Utils.GetIteminBag(8827)) != null && waterPot.Use())
                {
                    casted = true;
                }
                else if ((fishingRaft = Utils.GetIteminBag(85500)) != null && fishingRaft.Use())
                {
                    casted = true;
                }
            }
            if (StyxWoW.Me.IsSwimming)
            {
                using (StyxWoW.Memory.AcquireFrame())
                {
                    KeyboardManager.AntiAfk();
                    WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
                }
            }
            return casted;
        }
    }
}