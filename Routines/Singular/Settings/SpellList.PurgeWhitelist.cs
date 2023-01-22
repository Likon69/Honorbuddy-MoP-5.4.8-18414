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
    /// class defining spells on enemies we should Purge, etc
    /// </summary>
    public class PurgeWhitelist
    {
        private static PurgeWhitelist _instance;

        private static int[] Defaults = new int[]
        {
            // outdated but relevant list http://www.wowwiki.com/List_of_magic_effects
            1022,   //  Paladin - Hand of Protection
            1044,   //  Paladin - Hand of Freedom
            6940,   //  Paladin - Hand of Sacrifice
            974,    //  Shaman - Earth Shield
            2825,   //  Shaman - Bloodlust
            32182,  //  Shaman - Heroism
            80353,  //  Mage - Time Warp
            69369,  //  Druid - Predatory Swiftness
            6346,   //  Priest - Fear Ward
            123012, //  Tsulong / Embodied Terror - Terrace of the Endless Springs - Terrorize  
        };

        public SpellList SpellList;

        public PurgeWhitelist() 
        {
            string file = Path.Combine(SingularSettings.GlobalSettingsPath, "Singular.PurgeWhitelist.xml");
            SpellList = new SpellList( file, Defaults);
        }

        public static PurgeWhitelist Instance
        {
            get { return _instance ?? (_instance = new PurgeWhitelist()); }
            set { _instance = value; }
        }
    }

}