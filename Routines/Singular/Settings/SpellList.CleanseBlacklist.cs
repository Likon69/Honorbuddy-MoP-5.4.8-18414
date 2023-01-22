using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using System.Reflection;

using System.ComponentModel;
using Styx;
using Styx.Helpers;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;
using Singular.Managers;
using Styx.WoWInternals;

namespace Singular.Settings
{
    /// <summary>
    /// class defining list of spells that we should not Purify Spirit, etc
    /// </summary>
    public class CleanseBlacklist
    {
        private static CleanseBlacklist _instance;

        private static int[] Defaults = new int[]
        {
            96328,      // "Toxic Torment (Green Cauldron)"
            96325,      // "Frostburn Formula (Blue Cauldron)"
            96326,      // "Burning Blood (Red Cauldron)"
            92876,      // "Blackout (10man)"
            92878,      // "Blackout (25man)"
            30108,      // "(Warlock) Unstable Affliction"
            8050,       // "(Shaman) Flame Shock"
            3600,       // "(Shaman) Earthbind"
            34914,      // "(Priest) Vampiric Touch"
            104050,     // "Torrent of Frost"
            103962,     // "Torrent of Frost"
            103904,     // "Torrent of Frost" 
            139822,     // Cinders - Flaming Head, Meguera, Forgotten Depths
            149207,     // Corrupted Touch - Lingering Corruption, Siege of Orgrimmar
            144351,     // Mark of Arrogance - Sha of Price, Siege of Orgrimmar
        };

        public SpellList SpellList;

        public CleanseBlacklist() 
        {
            string file = Path.Combine(SingularSettings.GlobalSettingsPath, "Singular.CleanseBlacklist.xml");
            SpellList = new SpellList( file, Defaults);
        }

        public static CleanseBlacklist Instance
        {
            get { return _instance ?? (_instance = new CleanseBlacklist()); }
            set { _instance = value; }
        }
    }

}