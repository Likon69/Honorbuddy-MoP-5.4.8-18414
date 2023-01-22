using Styx;
using Styx.Common;
using Styx.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RaresAndChests
{
    public class RareSettings : Settings
    {
        public static readonly RareSettings myPrefs = new RareSettings();

        public RareSettings()
            : base(
                Path.Combine(Utilities.AssemblyDirectory,
                    string.Format(@"Plugins/Settings/FishingBaits/{0}-RareSettings-{1}.xml", StyxWoW.Me.RealmName, StyxWoW.Me.Name))
                )
        {
        }

        [Setting, DefaultValue(true)]
        public bool CombatLooter { get; set; }

        [Setting, DefaultValue(true)]
        public bool Rarefinder { get; set; }

        [Setting, DefaultValue(true)]
        public bool ChestFinder  { get; set; }

        [Setting, DefaultValue(200)]
        public int radius { get; set; }

        [Setting, DefaultValue(true)]
        public bool NormalRares { get; set; }

        [Setting, DefaultValue(true)]
        public bool EliteRares { get; set; }

    }
}
