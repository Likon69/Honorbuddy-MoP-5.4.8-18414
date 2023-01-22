
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bots.DungeonBuddy.Profiles.Handlers;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.Helpers;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Wrath_of_the_Lich_King
{
	public class UtgardeKeep : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId { get { return 202; } }

		public override WoWPoint Entrance { get { return new WoWPoint(1235.027, -4860.007, 41.24839); } }

		public override WoWPoint ExitLocation { get { return new WoWPoint(144.4507, -88.97738, 12.55168); } }

		private readonly WoWPoint _ignoreDragonflayerPackLoc = new WoWPoint(331.5634, 0.776747, 22.7549);

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					// fix for HB trying to run through a flaming wall of fire to get to this pack
					if ((ret.Entry == 24078 || ret.Entry == 24079 || ret.Entry == 24080) && !ret.ToUnit().Combat && ret.Location.DistanceSqr(_ignoreDragonflayerPackLoc) <= 20 * 20)
						return true;
					return false;
				});
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			foreach (var obj in incomingunits)
			{
				var unit = obj as WoWUnit;
				if (unit != null)
				{
					if (unit.Entry == FrostTombId) // Frost Tomb
						outgoingunits.Add(unit);
				}
			}
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var priority in units)
			{
				var unit = priority.Object as WoWUnit;
				if (unit != null)
				{
					if (unit.Entry == FrostTombId && Me.IsDps())
						priority.Score += 1000;

					if ((unit.Entry == KarvaldTheConstructorGhostId || unit.Entry == DalronnTheControllerId) && Me.IsDps())
						priority.Score -= 500;

					if (unit.Entry == DrakeId && (!Me.IsTank() || !Me.GroupInfo.IsInParty))
						priority.Score += 1000;
				}
			}
		}

		public override void IncludeLootTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			var armamentQuestId = Me.IsAlliance ? DisarmamentAllianceQuestId : DisarmamentHordeQuestId;
			if (!ScriptHelpers.HasQuest(armamentQuestId) || ScriptHelpers.IsQuestInLogComplete(armamentQuestId))
				return;

			foreach (var obj in incomingunits)
			{
				var gObj = obj as WoWGameObject;
				if (gObj != null)
				{
					// weapon racks for the quest 'Disarmament'
					if (gObj.Entry == VrykulWeaponsId
						&& gObj.DistanceSqr < 30 * 30
						&& !gObj.InUse && gObj.CanUse()
						&& !ScriptHelpers.WillPullAggroAtLocation(gObj.Location))
					{
						var pathDist = Me.Location.PathDistance(gObj.Location, 30f);
						if (!pathDist.HasValue || pathDist.Value >= 30f)
							continue;
						outgoingunits.Add(gObj);
					}
				}
			}
		}

		public override void OnEnter()
		{
			BossManager.OnBossKill += BossManager_OnBossKill;
			_shortcutBlackspot =
			new DynamicBlackspot(
				() =>
					_applyShortcutDoorBlackspot ??
					(_applyShortcutDoorBlackspot = new TimeCachedValue<bool>(TimeSpan.FromSeconds(4),
						ShouldApplyBlackspotAtDoorByShortcut)),
				() => _shortcutLoc,
				LfgDungeon.MapId,
				35,
				100,
				"shortcut");

			DynamicBlackspotManager.AddBlackspot(_shortcutBlackspot);
		}

		public override void OnExit()
		{
			BossManager.OnBossKill -= BossManager_OnBossKill;
			DynamicBlackspotManager.RemoveBlackspot(_shortcutBlackspot);
			_shortcutBlackspot = null;
		}

		#endregion

		#region Quest

		private static readonly uint[] EntranceQuestIds =
		{
			// Alliance
			29764, // Disarmament
			29803, // Ears of the Lich King
			29763, // Stealing Their Thunder
			// Horde
			30112, // A Score to Settle
			13206, // Disarmament
			11262, // Ingvar Must Die!
		};

		private const uint DisarmamentHordeQuestId = 30112;
		private const uint DisarmamentAllianceQuestId = 29764;

		private const uint VrykulWeaponsId = 193059;

		[EncounterHandler(24137, "Dark Ranger Marrah", Mode = CallBehaviorMode.Proximity, BossRange = 30)]
		[EncounterHandler(24111, "Defender Mordun", Mode = CallBehaviorMode.Proximity, BossRange = 30)]
		public async Task<bool> QuestPickupTurninHandler(WoWUnit npc)
		{
			if (Me.Combat || npc.ZDiff > 15 || ScriptHelpers.WillPullAggroAtLocation(npc.Location))
				return false;
			// pickup or turnin quests if any are available.
			return npc.HasQuestAvailable(true)
				? await ScriptHelpers.PickupQuest(npc)
				: npc.HasQuestTurnin() && await ScriptHelpers.TurninQuest(npc);
		}



		#endregion

		#region Root

		private const uint KarvaldTheConstructorGhostId = 27389;
		private const uint DalronnTheControllerId = 27390;
		private const uint DrakeId = 24083;
		private const uint FrostTombId = 23965;
		const uint ShortcutDoorId = 186694;
		private TimeCachedValue<bool> _applyShortcutDoorBlackspot;
		private DynamicBlackspot _shortcutBlackspot;
		private readonly WoWPoint _shortcutLoc = new WoWPoint(147.2509, -141.6872, 92.32478);
		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}


		readonly WaitTimer _waitForBossToRespawn = new WaitTimer(TimeSpan.FromSeconds(20));
		public override bool IsComplete
		{
			get
			{
				return !ScriptHelpers.IsBossAlive("Ingvar the Plunderer") && _waitForBossToRespawn.IsFinished && !Me.Combat;
			}
		}

		void BossManager_OnBossKill(Boss boss)
		{
			if (boss.Entry == IngvarThePlundererId)
			{
				_waitForBossToRespawn.Reset();
				Logger.Write("We killed Ingvar once, lets do it again.");
			}
		}

		private static bool ShouldApplyBlackspotAtDoorByShortcut()
		{
			var door = ObjectManager.GetObjectsOfType<WoWGameObject>()
				.FirstOrDefault(g => g.Entry == ShortcutDoorId);
			return door == null || ((WoWDoor)door.SubObj).IsClosed;
		}

		#endregion

		#region Ingvar the Plunderer

		private const uint IngvarThePlundererId = 23954;
		// Note: WoWObject.Entry is cached so be aware.
		const uint IngvarThePlundererUndeadId = 23980;
		private const uint DarkSmashSpellId = 42723;
		const uint IngvarThrowTargetId = 23996;

		class PillarInfo
		{
			public PillarInfo(WoWPoint location, float radius)
			{
				Location = location;
				Radius = radius;
			}
			internal WoWPoint Location { get; private set; }
			internal float Radius { get; private set; }
		}

		static readonly PillarInfo[] IngvarPillars =
		{
			new PillarInfo(new WoWPoint(245.0084, -314.9051, 180.4893), 3.104043f ),
			new PillarInfo(new WoWPoint(225.6272, -325.0349, 180.4895), 3.120694f ),
			new PillarInfo(new WoWPoint(213.4978, -316.1436, 180.4903), 4.318369f ),
			new PillarInfo(new WoWPoint(244.8809, -300.0176, 180.4905), 4.271772f )
		};

		[LocationHandler(228.7146, -306.7076, 180.4915, 60, "Ingvar the Plunderer Area")]
		public Func<WoWPoint, Task<bool>> IngvarThePlundererAreaHandler()
		{
			AddAvoidObject(ctx => true, 8, IngvarThrowTargetId);
			var bossFrontalAvoidArc = new ScriptHelpers.AngleSpan(0, 150);
			WoWUnit boss = null;
			var losTimer = new WaitTimer(TimeSpan.FromSeconds(6));
			Func<bool> losCondition = () => 
			{
				if (!ScriptHelpers.IsViable(boss))
					return false;
				if (boss.HasAura("Ingvar Feign Death") || boss.HasAura("Scourge Resurrection"))
				{
					losTimer.Reset();
					return true;
				}
				return !losTimer.IsFinished;
			};

			return async loc =>
			{
				boss = ObjectManager.GetObjectsOfType<WoWUnit>()
				   .FirstOrDefault(u => u.Entry == IngvarThePlundererId
					   || u.Entry == IngvarThePlundererUndeadId);

				if (boss == null)
					return false;
				var myLoc = Me.Location;
				// LOS Dreadful Roar ability
				if (losCondition())
				{
					var nearestPillar = IngvarPillars.OrderBy(p => p.Location.DistanceSqr(myLoc)).First();
					if (await ScriptHelpers.MoveOutOfLos(
							losCondition,
							boss.Location,
							nearestPillar.Location,
							nearestPillar.Radius))
					{
						return true;
					}
					// return false if healer to allow CR to use abilities while movement is disabled.
					// Movement should be disabled by the ScriptHelpers.LosLocation coroutine
					// when its not moving.
					return !Me.IsHealer();
				}

				// tank should face boss away from group to because of cleave
				if (await ScriptHelpers.TankFaceUnitAwayFromGroup(8))
					return true;

				// followers shouldn't stand in front of boss because of his cleave
				if (!Me.IsTank() && boss.Location.DistanceSqr(myLoc) < 8 * 8 && boss.CurrentTargetGuid != Me.Guid && !boss.IsMoving
					&& await ScriptHelpers.AvoidUnitAngles(boss, bossFrontalAvoidArc))
				{
					return true;
				}

				// port to entrance if we have quest to turnin
				return IsComplete && !Me.Combat
					&& EntranceQuestIds.Any(ScriptHelpers.IsQuestInLogComplete)
					&& LootTargeting.Instance.IsEmpty()
					&& await ScriptHelpers.PortOutsideAndBackIn();
			};
		}

		#endregion

	}
}