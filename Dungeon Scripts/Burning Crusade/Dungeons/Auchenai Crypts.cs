using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Styx;
using Styx.CommonBot;
using Styx.Pathing;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Burning_Crusade
{
	public class AuchenaiCrypts : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId { get { return 149; } }

		public override WoWPoint Entrance { get { return new WoWPoint(-3362.34, 5230.694, -101.0485); } }

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(-31.32874, 0.5065811, -0.1205378); }
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var p in units)
			{
				var unit = p.Object.ToUnit();
				if (unit == null) continue;
				if (unit.Entry == StolenSoulId)
					p.Score += 200;
			}
		}

		#endregion

		private readonly uint[] _entranceQuestIds = {29590, 29596};
		readonly WoWPoint _lastBossLocation = new WoWPoint(66.76575, -388.3454, 26.59005);
		[EncounterHandler(0)]
		public async Task<bool> RootBehavior()
		{
			// port outside and back in to hand in quests once dungeon is complete.
			// QuestPickupTurninHandler will handle the turnin
			return IsComplete && !Me.Combat
					&& _entranceQuestIds.Any(ScriptHelpers.IsQuestInLogComplete)
					&& LootTargeting.Instance.IsEmpty()
					&& Me.Location.DistanceSqr(_lastBossLocation) < 50 * 50
					&& await ScriptHelpers.PortOutsideAndBackIn();
		}

		private const uint StolenSoulId = 18441;
		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(54725, "Draenei Spirit", Mode = CallBehaviorMode.Proximity, BossRange = 30)]
		[EncounterHandler(54698, "Tormented Soulpriest", Mode = CallBehaviorMode.Proximity, BossRange = 30)]
		public async Task<bool> QuestPickupTurninHandler(WoWUnit npc)
		{
			if (Me.Combat || ScriptHelpers.WillPullAggroAtLocation(npc.Location))
				return false;
			return npc.HasQuestAvailable()
				? await ScriptHelpers.PickupQuest(npc)
				: npc.HasQuestTurnin() && await ScriptHelpers.TurninQuest(npc);
		}


		[EncounterHandler(18371, "Shirrak the Dead Watcher")]
		public Composite ShirrakEncounter()
		{
			WoWUnit shirrak = null;
			const uint focusFireId = 18374;
			AddAvoidObject(ctx => true, 12, focusFireId);
			return new PrioritySelector(
				ctx => shirrak = ctx as WoWUnit);
		}

		[EncounterHandler(18373, "Exarch Maladaar")]
		public Composite ExarchMaladaarEncounter()
		{
			const uint exarchMaladaarId = 18373;
			// avoid Soul Scream
			AddAvoidObject(ctx => Me.IsRange() && !Me.IsCasting, 10, o => o.Entry == exarchMaladaarId && o.ToUnit().CurrentTargetGuid != Me.Guid && !o.ToUnit().IsMoving);
			//WoWUnit boss = null;
			return new PrioritySelector(
				//   ctx => boss = ctx as WoWUnit,
				);
		}
	}
}