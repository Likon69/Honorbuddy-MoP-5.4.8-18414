using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Styx;
using Styx.CommonBot;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;

using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Burning_Crusade
{
	public class TheBotanica : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 173; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint (3415.277, 1482.168, 182.8366);}
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(46.95716, -35.48818, -1.09696); }
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var priority in units)
			{
				var unit = priority.Object as WoWUnit;
				if (unit != null)
				{
					if (unit.Entry == ThornLasherId)
					{
						if (StyxWoW.Me.IsDps())
							priority.Score += 5000;
						else if (StyxWoW.Me.IsTank())
							priority.Score -= 100;
					}
					if (unit.Entry == ThornFlayerId)
					{
						if (StyxWoW.Me.IsDps())
							priority.Score += 5000;
						else if (StyxWoW.Me.IsTank())
							priority.Score -= 100;
					}
					if (unit.Entry == SaplingId && StyxWoW.Me.IsTank())
						priority.Score -= 120;
				}
			}
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(u =>
								{
									var unit = u as WoWUnit;
									if (unit != null)
									{
										if (unit.Entry == ThorngrinTheTenderId && _helfireIds.Contains(unit.CastingSpellId) && Me.IsDps() && Me.IsMelee())
											return true;
										if ((unit.Entry == ThornFlayerId || unit.Entry == ThornLasherId) && Me.IsTank())
											return true;
									}
									return false;
								});
		}
		#endregion

		#region Root

		private const uint ThornLasherId = 19919;
		private const uint ThornFlayerId = 19920;
		private const uint SaplingId = 19949;
		private const int VialOfPoisonId = 34358;
		private const uint SavingTheBotanicaQuestId = 29660;
		private const uint CullingTheHerdQuestId = 29667;
		private const uint AMostSomberTaskQuestId = 29669;

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0, "Root")]
		public Composite RootBehaviorEncounter()
		{
			PlayerQuest quest = null;
			AddAvoidObject(ctx => !Me.IsCasting, 5, VialOfPoisonId);
			return new PrioritySelector(
				new Decorator(ctx => (quest = Me.QuestLog.GetQuestById(SavingTheBotanicaQuestId)) != null && quest.IsCompleted,
					ScriptHelpers.CreateCompletePopupQuest(SavingTheBotanicaQuestId)),

				new Decorator(ctx => (quest = Me.QuestLog.GetQuestById(CullingTheHerdQuestId)) != null && quest.IsCompleted,
					ScriptHelpers.CreateCompletePopupQuest(CullingTheHerdQuestId)),

				new Decorator(ctx => (quest = Me.QuestLog.GetQuestById(AMostSomberTaskQuestId)) != null && quest.IsCompleted,
					ScriptHelpers.CreateCompletePopupQuest(AMostSomberTaskQuestId))
					);

		}

		#endregion


		#region Commander Saranni

		[EncounterHandler(17976, "Commander Sarannis", Mode = CallBehaviorMode.Proximity, BossRange = 60)]
		public Composite CommanderSarannisEncounter()
		{
			WoWUnit trash = null;
			var tankTrashLoc = new WoWPoint(107.7079, 290.2441, -6.796101);
			var trashWaitLoc = new WoWPoint(117.333, 281.3033, -5.778226);
			var trashLoc = new WoWPoint(150.9637, 296.034, -4.57425);
			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				new Decorator(
					ctx => !boss.Combat,
					new PrioritySelector(
						ctx => trash = ScriptHelpers.GetUnfriendlyNpsAtLocation(trashLoc, 25, u => u != boss).FirstOrDefault(),
						ScriptHelpers.CreatePullNpcToLocation(
							ctx => trash != null, ctx => boss.Location.DistanceSqr(trashLoc) > 25 * 25, ctx => trash, ctx => tankTrashLoc, ctx => trashWaitLoc, 10))),
				new Decorator(ctx => boss.Combat, new PrioritySelector()));
		}

		#endregion



		#region High Botanist Freywinn

		[EncounterHandler(17975, "High Botanist Freywinn")]
		public Composite HighBotanistFreywinnEncounter()
		{
			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit);
		}

		#endregion


		#region Thorngrin the Tender
		const uint ThorngrinTheTenderId = 17978;
		readonly int[] _helfireIds = new[] { 34659, 39131 };

		[EncounterHandler(17978, "Thorngrin the Tender")]
		public Composite ThorngrinTheTenderEncounter()
		{
			WoWUnit boss;
			AddAvoidObject(ctx => !Me.IsTank(), 20, o => o.Entry == ThorngrinTheTenderId && _helfireIds.Contains(o.ToUnit().CastingSpellId));

			return new PrioritySelector(ctx => boss = ctx as WoWUnit);
		}

		#endregion


		#region Laj

		[EncounterHandler(17980, "Laj", Mode = CallBehaviorMode.Proximity)]
		public Composite LajEncounter()
		{
			WoWUnit boss = null;
			var centerRoomLoc = new WoWPoint(-165.5613, 393.9581, -17.69983);
			return new PrioritySelector(ctx => boss = ctx as WoWUnit, ScriptHelpers.CreateClearArea(() => centerRoomLoc, 60, u => u != boss));
		}

		#endregion


		[EncounterHandler(17977, "Warp Splinter")]
		public Composite WarpSplinterEncounter()
		{
			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit);
		}
	}
}