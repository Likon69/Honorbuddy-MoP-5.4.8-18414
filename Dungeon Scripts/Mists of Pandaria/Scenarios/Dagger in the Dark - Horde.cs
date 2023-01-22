
using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
using AvoidanceManager = Bots.DungeonBuddy.Avoidance.AvoidanceManager;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Mists_of_Pandaria
{
	public class DaggerInTheDarkHorde : DaggerInTheDarkAlliance
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 586; }
		}

		#endregion

	}
}