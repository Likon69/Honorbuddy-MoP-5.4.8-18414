using System.ComponentModel;
using System.IO;
using Styx.Helpers;

namespace Bots.FishingBuddy
{
	public class FishingBuddySettings : Settings
	{
		public static FishingBuddySettings Instance { get; private set; }

		static FishingBuddySettings()
		{
			Instance = new FishingBuddySettings(Path.Combine(CharacterSettingsDirectory, "FishingBuddy.xml"));
		}

		private FishingBuddySettings(string path)
			: base(path)
		{
			Instance = this;
			Load();
		}


		[Setting, Styx.Helpers.DefaultValue(""), Browsable(false)]
		public string LastLoadedProfile { get; set; }

		[Setting, Styx.Helpers.DefaultValue(false), Description("If set to true bot will attempt to loot any dead lootable NPCs")]
		public bool LootNPCs { get; set; }

		#region Gear

		[Setting, Styx.Helpers.DefaultValue(0u),
		Description("Wowhead Id of the hat to switch to when not fishing"), Category("Gear")]
		public uint Hat { get; set; }

		[Setting, Styx.Helpers.DefaultValue(0u), Description("Wowhead Id of the mainhand weapon to switch to when in combat"),
		Category("Gear")]
		public uint MainHand { get; set; }

		[Setting, Styx.Helpers.DefaultValue(0u), Description("Wowhead Id of the offhand weapon to switch to when in combat"),
		Category("Gear")]
		public uint OffHand { get; set; }

		#endregion

		#region Fishing

		[Setting, Styx.Helpers.DefaultValue(false),
		Description("Set this to true if you want to fish from pools, otherwise set to false."), Category("Fishing")]
		public bool Poolfishing { get; set; }

		[Setting, Styx.Helpers.DefaultValue(true),
		Description("Set to true to enable flying, false to use ground based navigation"), Category("Fishing")]
		public bool Fly { get; set; }

		[Setting, Styx.Helpers.DefaultValue(true),
		Description("If set to true bot will use water walking, either class abilities or pots"), Category("Fishing")]
		public bool UseWaterWalking { get; set; }

		[Setting, Styx.Helpers.DefaultValue(false),
		Description(
			"If set to true, bot will try to avoid landing in lava. Some pools by floating objects such as ice floes will get blacklisted if this is set to true"
			), Category("Fishing")]
		public bool AvoidLava { get; set; }

		[Setting, Styx.Helpers.DefaultValue(false), Description("If set to true bot will 'ninja' nodes from other players."),
		Category("Fishing")]
		public bool NinjaNodes { get; set; }

		[Setting, Styx.Helpers.DefaultValue(true), Description("If set to true bot will automatically apply fishing baits"),
		Category("Fishing")]
		public bool UseBait { get; set; }

        // Credits to the user 'anything'. https://www.thebuddyforum.com/archives/131036-hb-archives-fishingbuddy-delete-post1915079.html#post1915079
        [Setting, Styx.Helpers.DefaultValue((uint)0), Description("Which bait to prefer (item id). If not found, other baits will be used."),
        Category("Fishing")]
        public uint UseBaitPreference { get; set; }

		[Setting, Styx.Helpers.DefaultValue(true), Description("If set to true bot will automatically fillet fish"), Category("Fishing")]
		public bool FilletFish { get; set; }
		#endregion

		#region Advanced

		[Setting, Styx.Helpers.DefaultValue(5),
		Description("The maximum time in minutes to spend at a pool before it gets blacklisted"), Category("Advanced")]
		public int MaxTimeAtPool { get; set; }

		[Setting, Styx.Helpers.DefaultValue(15),
		Description("The maximum number of failed casts at a pool before moving to a new location at pool"), Category("Advanced")]
		public int MaxFailedCasts { get; set; }


		[Setting, Styx.Helpers.DefaultValue(15f),
		Description("When bot is within this distance from current hotspot then it cycles to next hotspot. flymode only"),
		Category("Advanced")]
		public float PathPrecision { get; set; }

		[Setting, Styx.Helpers.DefaultValue(40),
		Description(
			"Number of tracelines to do in a 360 deg area. the higher the more likely to find a landing spot.recomended to set at a multiple of 20"
			),
		Category("Advanced")]
		public int TraceStep { get; set; }

		[Setting, Styx.Helpers.DefaultValue(0.5f),
		Description(
			"Each time bot fails to find a landing spot it adds this number to the range and tries again until it hits MaxPoolRange. Can use decimals."
			), Category("Advanced")]
		public float PoolRangeStep { get; set; }

		#endregion
	}
}