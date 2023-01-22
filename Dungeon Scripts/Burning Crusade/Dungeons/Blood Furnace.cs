using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommonBehaviors.Actions;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.POI;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;

using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Burning_Crusade
{
	public class BloodFurnace : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 137; }
		}

		/// <summary>
		///     The entrance of this dungeon as a <see cref="WoWPoint" /> .
		/// </summary>
		/// <value> The entrance. </value>
		public override WoWPoint Entrance
		{
			get { return new WoWPoint(-311.6548, 3175.894, 27.29512); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(-0.5393078, 25.18321, -44.80187); }
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var targetPriority in units)
			{
				switch (targetPriority.Object.Entry)
				{
					case 17395: // Shadowmoon Summoner
						if (StyxWoW.Me.IsDps())
							targetPriority.Score += 120;
						break;
				}
			}
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(unit => unit is WoWPlayer);
		}

		public override void OnEnter()
		{
			// Proximity bombs thrown by trash mobs
			AddAvoidObject(ctx => !Me.IsCasting, 3, ProximityBombId);
			base.OnEnter();
		}

		#endregion

		#region Root

		const uint HeartOfRageQuestId_Horde = 29536;
		const uint MakeThemBleedQuestId_Horde = 29535;
		const uint MindTheGapQuestId = 29537;
		const uint HeartOfRageQuestId_Alliance = 29539;
		const uint MakeThemBleedQuestId_Alliance = 29538;
		const uint TheBreakerQuestId = 29540;
		WoWPoint _kelidanTheBreakerLoc = new WoWPoint(326.5651, -87.207, -24.65471);

		private readonly uint[] _entranceQuestIds =
		{
			HeartOfRageQuestId_Horde, MakeThemBleedQuestId_Horde, MindTheGapQuestId,
			HeartOfRageQuestId_Alliance, MakeThemBleedQuestId_Alliance, TheBreakerQuestId
		};

		private const uint ProximityBombId = 181877;

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(54636, "Caza'rez", Mode = CallBehaviorMode.Proximity, BossRange = 40)]
		[EncounterHandler(54629, "Gunny", Mode = CallBehaviorMode.Proximity, BossRange = 40)]
		public async Task<bool> QuestPickupTurninLogic(WoWUnit unit)
		{
			if (Me.IsActuallyInCombat || ScriptHelpers.WillPullAggroAtLocation(unit.Location))
				return false;
			if (unit.HasQuestAvailable(true))
				return await ScriptHelpers.PickupQuest(unit);
			if (unit.HasQuestTurnin())
				return await ScriptHelpers.TurninQuest(unit);
			return false;
		}


		[EncounterHandler(0, "Root")]
		public async Task<bool> RootLogic(WoWUnit unit)
		{
			// If dungeon is complete and we have some quests to turn in at entrance then port out and back in.
			if (IsComplete && LootTargeting.Instance.IsEmpty()
				&& _entranceQuestIds.Any(ScriptHelpers.IsQuestInLogComplete)
				&& Me.Location.DistanceSqr(_kelidanTheBreakerLoc) < 50 * 50
				&& await ScriptHelpers.PortOutsideAndBackIn())
			{
				return true;
			}
			return false;
		}

		#endregion

		#region Broggok

		private const uint BroggokId = 17380;

		[ObjectHandler(181982, "Cell Door Lever", ObjectRange = 100)]
		public Composite CellDoorLeverHandler()
		{
			var tankPoint = new WoWPoint(455.4293, 95.4829, 9.613952);
			const uint leverId = 181982;
			WoWGameObject entranceDoor = null, lever = null;
			return new PrioritySelector(
				ctx =>
				{
					entranceDoor = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(o => o.Entry == 181822);
					return lever = ctx as WoWGameObject;
				},
				new Decorator(
					ctx => lever.State == WoWGameObjectState.Ready && ScriptHelpers.IsBossAlive("Broggok"),
					new PrioritySelector(
				// clear the area before we start the event
						ScriptHelpers.CreateClearArea(() => tankPoint, 70, u => u.CanSelect),
				// nothing left to clear, lets go pull that lever.
						new Decorator(
							ctx => StyxWoW.Me.IsTank() && Targeting.Instance.IsEmpty() && BotPoi.Current.Type == PoiType.None && lever.CanUse(),
							new PrioritySelector(
								new Decorator(ctx => entranceDoor.State == WoWGameObjectState.Active, ScriptHelpers.CreateInteractWithObject(leverId)),
								new Decorator(ctx => entranceDoor.State == WoWGameObjectState.Ready, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(tankPoint))))))));
		}

		[EncounterHandler(17380, "Broggok")]
		public Composite BroggokHandler()
		{
			WoWUnit boss = null;
			const uint poisonCloudId = 17662;

			AddAvoidObject(ctx => boss != null && boss.IsValid && boss.Combat || !Me.IsMoving, obj => Me.IsRange() && Me.IsMoving ? 10f : obj.Scale + 2, poisonCloudId);

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(
					ctx => !Me.IsTank() && boss.CurrentTargetGuid != Me.Guid && !boss.IsMoving, ctx => boss, new ScriptHelpers.AngleSpan(0, 180)),
				ScriptHelpers.CreateTankFaceAwayGroupUnit(ctx => boss, 20));
		}

		#endregion

		#region Keli'dan the Breaker

		private const uint KelidanTheBreakerId = 17377;

		[EncounterHandler(17377, "Keli'dan the Breaker")]
		public Composite KelidanTheBreakerHandler()
		{
			AddAvoidObject(ctx => true, 20, o => o.Entry == KelidanTheBreakerId && o.ToUnit().HasAura("Burning Nova"));

			return new PrioritySelector();
		}

		#endregion
	}
}