
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Styx.WoWInternals;

using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Wrath_of_the_Lich_King
{
	public class PitOfSaron : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 253; }
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