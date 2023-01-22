
using System.Collections.Generic;
using Styx.CommonBot;
using Styx.WoWInternals.WoWObjects;
using Styx.WoWInternals;
using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Wrath_of_the_Lich_King
{
	public class HallsOfReflection : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 255; }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(ret => { return false; });
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			foreach (var obj in incomingunits)
			{
				var unit = obj as WoWUnit;
				if (unit != null) { }
			}
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var priority in units)
			{
				var unit = priority.Object as WoWUnit;
				if (unit != null) { }
			}
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