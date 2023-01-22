using System.IO;
using Styx.Common;
using Styx.Helpers;

namespace HighVoltz.Professionbuddy
{
	public class GlobalPBSettings : Settings
	{

        public static readonly GlobalPBSettings Instance = new GlobalPBSettings();
        public GlobalPBSettings() : base(Path.Combine(Utilities.AssemblyDirectory, string.Format(@"Settings\{0}\{0}.xml", ProfessionbuddyBot.Instance.Name)))
        {
            Load();
        }

		[Setting, DefaultValue(0)]
		public int CurrentRevision { get; set; }

		[Setting, DefaultValue(0u)]
		public uint KnownSpellsPtr { get; set; }

		[Setting, DefaultValue(null)]
		public string DataStoreTable { get; set; }

        [Setting, DefaultValue("")]
        public string WowVersion { get; set; }
    }
}