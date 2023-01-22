using Styx.WoWInternals;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Wrath_of_the_Lich_King
{
	public class TrialOfTheChampion : Dungeon
	{
		#region Overrides of Dungeon
		public override uint DungeonId
		{
			get { return 245; }
		}

		public override void OnEnter()
		{
			Alert.Show(
				"Dungeon Not Supported",
				string.Format(
					"The {0} dungeon is not supported. If you wish to stay in group and play manually then press 'Continue'. Otherwise you will automatically leave group.",
					Name),
				30,
				true,
				true,
				null,
				() => Lua.DoString("LeaveParty()"),
				"Continue",
				"Leave");
		}
		#endregion
	}
}
