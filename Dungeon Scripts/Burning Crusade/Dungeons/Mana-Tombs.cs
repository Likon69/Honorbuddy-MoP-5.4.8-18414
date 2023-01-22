using System.Linq;
using System.Threading.Tasks;
using CommonBehaviors.Actions;
using Styx;
using Styx.CommonBot;
using Styx.Pathing;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;

using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Burning_Crusade
{
	public class ManaTombs : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId { get { return 148; } }

		public override WoWPoint Entrance { get { return new WoWPoint(-3074.542, 4943.176, -101.0476); } }

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(6.456772, 0.9883103, -0.9543309); }
		}

		#endregion

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		private readonly uint[] _entranceQuestIds = { 29574, 29573, 29575 };
		readonly WoWPoint _lastBossLocation = new WoWPoint(-184.3657, 9.333467, 16.73412);

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

		[EncounterHandler(54694, "Mamdy the Ologist", Mode = CallBehaviorMode.Proximity, BossRange = 30)]
		[EncounterHandler(54692, "Artificer Morphalius", Mode = CallBehaviorMode.Proximity, BossRange = 30)]
		public async Task<bool> QuestPickupTurninHandler(WoWUnit npc)
		{
			if (Me.Combat || ScriptHelpers.WillPullAggroAtLocation(npc.Location))
				return false;
			return npc.HasQuestAvailable()
				? await ScriptHelpers.PickupQuest(npc)
				: npc.HasQuestTurnin() && await ScriptHelpers.TurninQuest(npc);
		}

		[EncounterHandler(18341, "Pandemonius", Mode = CallBehaviorMode.Proximity, BossRange = 80)]
		public Composite PandemoniusEncounter()
		{
			WoWUnit boss = null;
			var pandemoniusRoomAddsloc1 = new WoWPoint(-44.84757, -80.27489, -2.326236);
			var pandemoniusRoomAddsloc2 = new WoWPoint(-94.44795, -85.13178, -2.020432);
			var pandemoniusTankSpot = new WoWPoint(-61.92603, -95.23901, -0.4504707);

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				new Decorator(
					ctx => ScriptHelpers.GetUnfriendlyNpsAtLocation(pandemoniusRoomAddsloc1, 25, u => u != boss).Any(),
					ScriptHelpers.CreateClearArea(() => pandemoniusRoomAddsloc1, 25, u => u != boss)),
				new Decorator(
					ctx => !ScriptHelpers.GetUnfriendlyNpsAtLocation(pandemoniusRoomAddsloc1, 25, u => u != boss).Any(),
					ScriptHelpers.CreateClearArea(() => pandemoniusRoomAddsloc2, 25, u => u != boss)),
				// stop attacking if boss has Dark Shell
				new Decorator(
					ctx => boss.HasAura("Dark Shell") && StyxWoW.Me.IsRange() && StyxWoW.Me.IsDps() && StyxWoW.Me.PowerType == WoWPowerType.Mana,
					new PrioritySelector(
						new Decorator(
							ctx => StyxWoW.Me.IsCasting,
							new Action(ctx => SpellManager.StopCasting())),
						new ActionAlwaysSucceed())),
				new Decorator(
					ctx => Targeting.Instance.FirstUnit == boss && boss.CurrentTargetGuid == Me.Guid,
					ScriptHelpers.CreateTankUnitAtLocation(ctx => pandemoniusTankSpot, 3))
				);
		}

		[EncounterHandler(18343, "Tavarok")]
		public Composite TavarokEncounter()
		{
			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(ctx => !Me.IsTank() && boss.Distance < 15 && boss.CurrentTargetGuid != Me.Guid, ctx => boss, new ScriptHelpers.AngleSpan(0, 180)),
				ScriptHelpers.CreateTankFaceAwayGroupUnit(15)
				);
		}

		[EncounterHandler(18344, "Nexus-Prince Shaffar", Mode = CallBehaviorMode.Proximity)]
		public Composite NexusPrinceShaffarEncounter()
		{
			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateClearArea(() => boss.Location, 70, u => u != boss)
				);
		}
	}
}