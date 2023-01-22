
using System.Linq;
using System.Text;
using Styx;
using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using System.Collections.Generic;
using Styx.Pathing;
using Singular.Helpers;
using Singular.Settings;

namespace Singular
{
    internal static class Extensions
    {

        public static bool Between(this double distance, double min, double max)
        {
            return distance >= min && distance <= max;
        }

        public static bool Between(this float distance, float min, float max)
        {
            return distance >= min && distance <= max;
        }

        public static bool Between(this int value, int min, int max)
        {
            return value >= min && value <= max;
        }

        public static string AlignLeft(this string s, int width)
        {
            int len = s.Length;
            if (len >= width)
                return s.Substring(0, width);

            return s + ("                                                                                                                          ".Substring(0, width - len));
        }

        public static string AlignRight(this string s, int width)
        {
            int len = s.Length;
            if (len >= width)
                return s.Substring(0, width);

            return ("                                                                                                                          ".Substring(0, width - len)) + s;
        }

        /// <summary>
        ///   A string extension method that turns a Camel-case string into a spaced string. (Example: SomeCamelString -> Some Camel String)
        /// </summary>
        /// <remarks>
        ///   Created 2/7/2011.
        /// </remarks>
        /// <param name = "str">The string to act on.</param>
        /// <returns>.</returns>
        public static string CamelToSpaced(this string str)
        {
            var sb = new StringBuilder();
            foreach (char c in str)
            {
                if (char.IsUpper(c))
                {
                    sb.Append(' ');
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        private static string Right(string s, int c)
        {
            return s.Substring(c > s.Length ? 0 : s.Length - c);
        }
        public static string UnitID(ulong guid)
        {
            return Right(string.Format("{0:X4}", guid), 4);
        }

        public static Styx.StatType GetPrimaryStat(this WoWUnit unit)
        {
            Styx.StatType primaryStat = StatType.Strength;
            if (unit.Agility > unit.Strength)
                primaryStat = StatType.Agility;
            if (unit.Intellect > unit.Agility)
                primaryStat = StatType.Intellect;
            return primaryStat;
        }

        public static bool ShowPlayerNames { get; set; }

        public static string SafeName(this WoWObject obj)
        {
            if (obj.IsMe)
            {
                return "Me";
            }

            string name;
            if (obj is WoWPlayer)
            {
                if (!obj.ToPlayer().IsFriendly)
                {
                    name = "Enemy.";
                }
                else
                {
                    if (RaFHelper.Leader == obj)
                        name = "lead.";
                    else if (Group.Tanks.Any(t => t.Guid == obj.Guid))
                        name = "tank.";
                    else if (Group.Healers.Any(t => t.Guid == obj.Guid))
                        name = "healer.";
                    else
                        name = "dps.";
                }
                name += ShowPlayerNames ? ((WoWPlayer)obj).Name : ((WoWPlayer)obj).Class.ToString();
            }
            else if (obj is WoWUnit && obj.ToUnit().IsPet)
            {
                WoWUnit root = obj.ToUnit().OwnedByRoot;
                name =  (root == null ? "(unknown-owner)" : root.SafeName()) + ":Pet";
            }
            else
            {
                name = obj.Name;
            }

            return name + "." + UnitID(obj.Guid);
        }

        public static bool IsWanding(this LocalPlayer me)
        {
            return StyxWoW.Me.AutoRepeatingSpellId == 5019;
        }

        private static HashSet<int> _AddtlHealSpells = new HashSet<int>()
        {
            33076,  // Prayer of Mending
            120517, // Halo
            73920,  // Healing Rain
            115460, // Healing Sphere

        };

        public static bool IsHeal(this WoWSpell spell)
        {
            return _AddtlHealSpells.Contains(spell.Id)
                || spell.SpellEffects
                    .Any(s => s.EffectType == WoWSpellEffectType.Heal
                        || s.EffectType == WoWSpellEffectType.HealMaxHealth
                        || s.EffectType == WoWSpellEffectType.HealPct
                        || (s.EffectType == WoWSpellEffectType.ApplyAura && (s.AuraType == WoWApplyAuraType.PeriodicHeal || s.AuraType == WoWApplyAuraType.SchoolAbsorb))
                        );
        }

        public static bool IsDamageRedux(this WoWSpell spell)
        {
            return spell.SpellEffects
                .Any(s => s.EffectType == WoWSpellEffectType.ApplyAura
                    && (   s.AuraType == WoWApplyAuraType.ModDamageTaken
                        || s.AuraType == WoWApplyAuraType.ModDamagePercentTaken
                        || s.AuraType == WoWApplyAuraType.DamageImmunity
                        )
                    );
        }

        /// <summary>
        /// determines if a target is off the ground far enough that you can
        /// reach it with melee spells if standing directly under.
        /// </summary>
        /// <param name="u">unit</param>
        /// <returns>true if above melee reach</returns>
        public static bool IsAboveTheGround(this WoWUnit u)
        {
            // temporary change while working out issues with using mesh to check if off ground
            // return !Styx.Pathing.Navigator.CanNavigateFully(StyxWoW.Me.Location, u.Location);

            float height = HeightOffTheGround(u);
            if ( height == float.MaxValue )
                return false;   // make this true if better to assume aerial 

            if (height >= StyxWoW.Me.MeleeDistance(u))
                return true;

            return false;
        }

        /// <summary>
        /// calculate a unit's vertical distance (height) above ground level (mesh).  this is the units position
        /// relative to the ground and is independent of any other character.  note: this isn't actually the ground,
        /// it's the height from the mesh and the mesh is not guarranteed to be flush with the terrain (which is why we add the +2f)
        /// </summary>
        /// <param name="u">unit</param>
        /// <returns>float.MinValue if can't determine, otherwise distance off ground</returns>
        public static float HeightOffTheGround(this WoWUnit u)
        {
            var unitLoc = new WoWPoint( u.Location.X, u.Location.Y, u.Location.Z);         
            var listMeshZ = Navigator.FindHeights( unitLoc.X, unitLoc.Y).Where( h => h <= unitLoc.Z + 2f);
            if (listMeshZ.Any())
                return unitLoc.Z - listMeshZ.Max();
            
            return float.MaxValue;
        }

        private static string _lastGetPredictedError;
        public static float PredictedHealthPercent(this WoWUnit u, bool includeMyHeals = false)
        {
#if true
            return u.GetPredictedHealthPercent(includeMyHeals);
#else
            float ph = u.GetPredictedHealthPercent(includeMyHeals);
            if (ph > 100f || ph < 0f)
            {
                float hp = (float)u.HealthPercent;
                if (SingularSettings.Debug && hp < 99.95f)
                {
                    // note: added some simple caching of last erro to reduce message spam.
                    // .. once this error occurs at a certain health %, it appears to persist 
                    // .. for awhile
                    string msg = string.Format("Error=WoWUnit.GetPredictedHealthPercent({0}) returned {1:F1}% for {2} with HealthPercent={3:F1}%", includeMyHeals, ph, u.SafeName(), hp);
                    if (msg != _lastGetPredictedError)
                    {
                        _lastGetPredictedError = msg;
                        Logger.WriteDebug(System.Drawing.Color.Pink, msg);
                        u.LocalGetPredictedHealth(includeMyHeals);
                    }
                }

                return hp;
            }

            return ph;
#endif
        }

        /// <summary>
        /// converts bool to Y or N string
        /// </summary>
        /// <param name="b">bool to convert</param>
        /// <returns></returns>
        public static string ToYN(this bool b)
        {
            return b ? "Y" : "N";
        }
    }
}