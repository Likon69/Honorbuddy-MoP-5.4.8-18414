
using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
using AvoidanceManager = Bots.DungeonBuddy.Avoidance.AvoidanceManager;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Mists_of_Pandaria
{
	public class ALittlePatienceHorde : ALittlePatienceAlliance
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 619; }
		}

		#endregion

	}
}