using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bots.DungeonBuddy.Enums;
using Bots.DungeonBuddy.Profiles.Handlers;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;

using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Burning_Crusade
{
	public class HellfireRamparts : Dungeon
	{
		#region Overrides of Dungeon

		/// <summary>
		///   The mapid of this dungeon.
		/// </summary>
		/// <value> The map identifier. </value>
		public override uint DungeonId
		{
			get { return 136; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(-365.0169, 3093.254, -14.35639); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(-1361.804, 1629.332, 68.38041); }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			// remove Nazan from targeting if he is flying and bot is a melee.
			units.RemoveAll(u => u.Entry == NazanId && Me.IsMelee() && u.Z > 90);
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var p in units)
			{
				var unit = p.Object as WoWUnit;
				if (unit == null) continue;
				if ((unit.Entry == FiendishHoundId || unit.Entry == HellfireWatcherId) && Me.IsDps())
				{
					p.Score += 1000;
				}
			}
		}

		public override void IncludeLootTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			foreach (var obj in incomingunits)
			{
				var gObj = obj as WoWGameObject;
				if (gObj != null)
				{
					// chest at last (optional) boss.
					if (gObj.Entry == ReinforcedFelIronChestId
						&& DungeonBuddySettings.Instance.LootMode != LootMode.Off
						&& gObj.CanUse())
					{
						outgoingunits.Add(gObj);
					}
					
					// loot crates for the quest 'Hitting Them Where It Hurts'
					if (_hellfireSuppliesIds.Contains(gObj.Entry)
						&& !ScriptHelpers.WillPullAggroAtLocation(gObj.Location)
						&& gObj.DistanceSqr < 30*30
						&& !gObj.InUse && gObj.CanUse())
					{
						var pathDist = Me.Location.PathDistance(gObj.Location, 30f);
						if (!pathDist.HasValue || pathDist.Value >= 30f)
							continue;
						outgoingunits.Add(gObj);
					}
					continue;
				}
				var unit = obj as WoWUnit;
				if (unit != null)
				{
					// make sure quest object gets looted regardless of loot settings.
					if (unit.Entry == OmorTheUnscarredId && unit.IsDead && unit.CanLoot
						&& unit.DistanceSqr < 50 * 50
						&& DemonsInTheCitadelQuestIds.Any(id => ScriptHelpers.HasQuest(id) && !ScriptHelpers.IsQuestInLogComplete(id)))
					{
						outgoingunits.Add(unit);
					}

					if ((unit.Entry == VazrudenTheHeraldId || unit.Entry == VazrudenTheHeraldId)
						&& unit.IsDead && unit.CanLoot && unit.DistanceSqr < 50 * 50
						&& WarOnTheRampartsQuestIds.Any(id => ScriptHelpers.HasQuest(id) && !ScriptHelpers.IsQuestInLogComplete(id)))
					{
						outgoingunits.Add(unit);
					}
				}
			}
		}

		#endregion

		#region Root

		private const uint FiendishHoundId = 17540;
		private const uint HellfireWatcherId = 17309;

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0, "Root")]
		public async Task<bool> RootLogic(WoWUnit supplies)
		{
			// port outside and back in to hand in quests once dungeon is complete.
			// QuestPickupTurninHandler will handle the turnin
			return IsComplete && !Me.Combat
					&& QuestsAtEntrance.Any(ScriptHelpers.IsQuestInLogComplete)
					&& LootTargeting.Instance.IsEmpty()
					&& Me.Location.DistanceSqr(_lastBossLocation) < 50*50
					&& await ScriptHelpers.PortOutsideAndBackIn();
		}

		[EncounterHandler(54606, "Stone Guard Stok'ton", Mode = CallBehaviorMode.Proximity, BossRange = 30)]
		[EncounterHandler(54603, "Advance Scout Chadwick", Mode = CallBehaviorMode.Proximity, BossRange = 30)]
		public async Task<bool> QuestPickupTurninHandler(WoWUnit npc)
		{
			if (Me.Combat || ScriptHelpers.WillPullAggroAtLocation(npc.Location) || npc.ZDiff > 10)
				return false;
			// pickup or turnin quests if any are available.
			return npc.HasQuestAvailable(true)
				? await ScriptHelpers.PickupQuest(npc)
				: npc.HasQuestTurnin() && await ScriptHelpers.TurninQuest(npc);
		}

		private static readonly uint[] HittingThemWhereItHurtsQuestIds = {29593, 29594};
		private static readonly uint[] DemonsInTheCitadelQuestIds = { 29529, 29530 };
		private static readonly uint[] WarOnTheRampartsQuestIds = { 29527, 29528 };

		private static readonly List<uint> QuestsAtEntrance =
			new List<uint>(
				HittingThemWhereItHurtsQuestIds
					.Concat(DemonsInTheCitadelQuestIds)
					.Concat(WarOnTheRampartsQuestIds));


		private readonly uint[] _hellfireSuppliesIds =  { 209347, 209348 };

		#endregion


		#region Nazan

		private const uint NazanId = 17536;
		private const uint VazrudenTheHeraldId = 17307;

		readonly WoWPoint _lastBossLocation = new WoWPoint(-1405.533, 1735.979, 81.2851);

		[EncounterHandler(17536, "Nazan")]
		public Func<WoWUnit, Task<bool>> NazanEncounter()
		{
			const uint liquidFireId = 181890;

			AddAvoidObject(ctx => !Me.IsCasting, 3f, liquidFireId);
			var frontArc = new ScriptHelpers.AngleSpan(0, 180);

			return async npc =>
			{
				// face dragon away from group to get them hit by flame breath.
				if (await ScriptHelpers.TankFaceUnitAwayFromGroup(15))
					return true;
				// avoid standing in front of dragon because of flame breath if not tanking
				return !Me.IsTank() && npc.CurrentTargetGuid != Me.Guid
						&& await ScriptHelpers.AvoidUnitAngles(npc, frontArc);
			};
		}

		private const uint ReinforcedFelIronChestId = 185168;

		#endregion

		#region Omor the Unscarred

		private const uint OmorTheUnscarredId = 17536;

		[EncounterHandler(17536, "Omor the Unscarred")]
		public Func<WoWUnit, Task<bool>> OmorTheUnscarredEncounter()
		{
			AddAvoidObject(ctx => !Me.IsTank() && Me.HasAura("Bane of Treachery"), 15, o => o is WoWPlayer && !o.IsMe);
			return async boss => false;
		}

		#endregion

		#region Watchkeeper Gargolmar

		private const uint WatchkeeperGargolmarId = 17306;

		[EncounterHandler(17306, "Watchkeeper Gargolmar")]
		public async Task<bool> WatchkeeperGargolmarEncounter(WoWUnit boss)
		{
			return await ScriptHelpers.DispelEnemy("Renew", ScriptHelpers.EnemyDispelType.Magic, boss);
		}

		[EncounterHandler(17309, "Hellfire Watcher")]
		public Func<WoWUnit, Task<bool>> HellfireWatcherEncounter()
		{
			var healIds = new[] { 12039, 30643 };

			return async npc => await ScriptHelpers.DispelEnemy("Renew", ScriptHelpers.EnemyDispelType.Magic, npc)
							 || await ScriptHelpers.InterruptCast(npc, healIds);
		}

		#endregion
	}
}