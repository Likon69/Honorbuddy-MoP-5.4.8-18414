using System;
using System.Collections.Generic;
using System.Linq;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.Helpers;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Burning_Crusade
{
	public class ShadowLabyrinth : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 151; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(-3650.999, 4942.84, -101.0476); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(8.25, 0.0, -1.13); }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				u =>
				{
					var unit = u as WoWUnit;
					if (unit != null)
					{
						if (unit is WoWPlayer)
							return true;
						if (unit.Entry == AmbassadorHellmawId && unit.HasAura("Banish"))
							return true;
						if (unit.Entry == CabalExectionerId && Me.IsDps() && Me.IsMelee() && unit.HasAura("Whirlwind"))
							return true;
						if (unit.Entry == MurmurId && _sonicBoomIds.Contains(unit.CastingSpellId))
							return true;
					}
					return false;
				});
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var priority in units)
			{
				var unit = priority.Object as WoWUnit;
				if (unit != null)
				{
					if (StyxWoW.Me.IsDps())
					{
						if (unit.Entry == FelOverseerId)
							priority.Score += 210;

						if (unit.Entry == CabalSummonerId)
							priority.Score += 210;

						if (unit.Entry == VoidTravelerId)
							priority.Score += 400;
					}
				}
			}
		}

		public override void OnEnter()
		{
			_blackHeartRoomCleared = false;
		}

		#endregion

		private const uint AmbassadorHellmawId = 18731;
		private const uint FelOverseerId = 18796;
		private const uint CabalSummonerId = 18634;
		private const uint VoidTravelerId = 19226;
		private const uint CabalExectionerId = 18632;

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}


		[EncounterHandler(54890, "Field Commander Mahfuun", Mode = CallBehaviorMode.Proximity, BossRange = 35)]
		public Composite QuestPickupHandler()
		{
			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				new Decorator(
					ctx => !Me.Combat && !ScriptHelpers.WillPullAggroAtLocation(unit.Location) && unit.QuestGiverStatus == QuestGiverStatus.Available,
					ScriptHelpers.CreatePickupQuest(ctx => unit)),
				new Decorator(
					ctx => !Me.Combat && !ScriptHelpers.WillPullAggroAtLocation(unit.Location) && unit.QuestGiverStatus == QuestGiverStatus.TurnIn,
					ScriptHelpers.CreateTurninQuest(ctx => unit)));
		}

		[EncounterHandler(0, "Root behavior")]
		public Composite RootHandler()
		{
			AddAvoidObject(ctx => !Me.IsTank(), 8, o => o.Entry == CabalExectionerId && o.ToUnit().HasAura("Whirlwind"));
			return new PrioritySelector();
		}

		#region Ambassador Hellmaw

		[EncounterHandler(18796, "Fel Overseer")]
		public Composite FelOverseerHandler()
		{
			WoWUnit unit = null;
			// stay away from overseer to prevent getting feared if ranged.
			AddAvoidObject(
				ctx => StyxWoW.Me.IsRange() && !Me.IsCasting,
				10,
				o => o.Entry == FelOverseerId && !o.ToUnit().IsMoving && o.ToUnit().CurrentTargetGuid != Me.Guid && o.ToUnit().IsAlive);

			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				// avoid getting cleaved if not tank
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(
					ctx => !Me.IsTank() && unit.CurrentTargetGuid != Me.Guid && !unit.IsMoving && !Me.IsCasting && unit.Distance < 8,
					ctx => unit,
					new ScriptHelpers.AngleSpan(0, 180)),
				ScriptHelpers.CreateTankFaceAwayGroupUnit(8));
		}

		[EncounterHandler(18731, "Ambassador Hellmaw", 150, CallBehaviorMode.Proximity)]
		public Composite AmbassadorHellmawEncounter()
		{
			List<WoWUnit> trashGroup1 = null;
			List<WoWUnit> overSeers = null;
			WoWUnit boss = null;

			var trashGroup1PullSpot = new WoWPoint(-152.6901, -121.3326, 6.650446);
			var trashGroup1TankSpot = new WoWPoint(-130.4145, -129.5648, 4.572331);
			var seerAtEntranceLoc = new WoWPoint(-156.8657, -93.51019, 8.07323);
			var trashGroup1Loc = new WoWPoint(-156.003, -74.48317, 8.073184);
			var overSeerCenterLoc = new WoWPoint(-158.9956, -39.65439, 8.073073);
			var overSeerTankLocation = new WoWPoint(-157.2833, -85.43105, 8.073076);
			return new PrioritySelector(
				ctx =>
				{
					trashGroup1 = ScriptHelpers.GetUnfriendlyNpsAtLocation(trashGroup1Loc, 25, u => u.Entry == 18794);
					overSeers = ScriptHelpers.GetUnfriendlyNpsAtLocation(overSeerCenterLoc, 100, u => u.Entry == 18796);
					return boss = ctx as WoWUnit;
				},
				new Decorator(
					ctx => boss.Combat,
					new PrioritySelector(
						ScriptHelpers.CreateAvoidUnitAnglesBehavior(
							ctx => !Me.IsTank() && boss.Distance < 20 && !boss.IsMoving && boss.CurrentTargetGuid != Me.Guid, ctx => boss, new ScriptHelpers.AngleSpan(0, 180)),
						ScriptHelpers.CreateTankFaceAwayGroupUnit(20))),
				new Decorator(
					ctx => !boss.Combat && StyxWoW.Me.Z > 3f,
					new PrioritySelector(
				// pull seer if it's close enough.
						ScriptHelpers.CreatePullNpcToLocation(
							ctx => overSeers.Any() && overSeers[0].Location.DistanceSqr(seerAtEntranceLoc) <= 10 * 10,
							ctx => true,
							ctx => overSeers[0],
							ctx => trashGroup1TankSpot,
							ctx => trashGroup1PullSpot),
				// clear the 1st group of trash while avoiding overseers.
						ScriptHelpers.CreatePullNpcToLocation(
							ctx => trashGroup1.Any(),
							ctx => !overSeers.Any(u => u.Location.DistanceSqr(trashGroup1[0].Location) <= 40 * 40),
							ctx => trashGroup1[0],
							ctx => trashGroup1TankSpot,
							ctx => trashGroup1PullSpot),
				// kill the overseers.
						ScriptHelpers.CreatePullNpcToLocation(
							ctx => overSeers.Any(),
							ctx => !ScriptHelpers.GetUnfriendlyNpsAtLocation(overSeers[0].Location, 30, u => u != overSeers[0]).Any(),
							ctx => overSeers[0],
							ctx => overSeers.Count > 1 ? trashGroup1PullSpot : overSeerTankLocation,
							ctx => overSeers.Count > 1 ? trashGroup1PullSpot : overSeerTankLocation))));
		}

		#endregion

		#region Blackheart the Inciter

		private readonly CircularQueue<WoWPoint> _clearRoomPath = new CircularQueue<WoWPoint>
																  {
																	  new WoWPoint(-233.7071, -76.19012, 8.073038),
																	  new WoWPoint(-243.252, 4.626983, 8.073118)
																  };


		private bool _blackHeartRoomCleared;
		private WoWUnit _blackheartTheInciter;

		[EncounterHandler(18667, "Blackheart the Inciter", BossRange = 120, Mode = CallBehaviorMode.Proximity)]
		public Composite BlackheartTheInciterEncounter()
		{
			var pillarsByBlackHeart = new List<KeyValuePair<WoWPoint, float>>
									  {
										  new KeyValuePair<WoWPoint, float>(new WoWPoint(-305.4713, -24.4433, 8.072822), 3),
										  new KeyValuePair<WoWPoint, float>(new WoWPoint(-305.386, -54.1253, 8.072817), 3),
										  new KeyValuePair<WoWPoint, float>(new WoWPoint(-284.017, -51.09766, 8.072872), 6),
										  new KeyValuePair<WoWPoint, float>(new WoWPoint(-284.1259, -26.82462, 8.167638), 6),
									  };

			var blackheartRoom = new WoWPoint(-261.3178, -38.62257, 8.072852);
			var nearestPillar = new KeyValuePair<WoWPoint, float>();

			return new Decorator(
				ctx => !ScriptHelpers.IsBossAlive("Ambassador Hellmaw"),
				new PrioritySelector(
					ctx =>
					{
						nearestPillar = pillarsByBlackHeart.OrderBy(kv => StyxWoW.Me.Location.DistanceSqr(kv.Key)).FirstOrDefault();
						return _blackheartTheInciter = ctx as WoWUnit;
					},
					new Decorator(
						ctx => !_blackheartTheInciter.Combat,
						new PrioritySelector(
							new Decorator(
								ctx => ScriptHelpers.GetUnfriendlyNpsAtLocation(blackheartRoom, 100, u => u != _blackheartTheInciter).Any(),
								ScriptHelpers.CreateClearArea(() => blackheartRoom, 100, u => u != _blackheartTheInciter)),
				// make sure the room is completely cleared before we continue.
							new Decorator(
								ctx => !ScriptHelpers.GetUnfriendlyNpsAtLocation(blackheartRoom, 100, u => u != _blackheartTheInciter).Any() && !_blackHeartRoomCleared,
								new PrioritySelector(
									ctx => _clearRoomPath.Peek(),
									new Decorator(
										ctx => StyxWoW.Me.Location.Distance2DSqr((WoWPoint)ctx) < 5 * 5,
				// we have reached the last point.
										new Action(
											ctx =>
											{
												_clearRoomPath.Dequeue();
												if (_clearRoomPath.Peek() == _clearRoomPath.First)
													_blackHeartRoomCleared = true;
											})),
									new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS((WoWPoint)ctx)))))),
					new Decorator(
						ctx => _blackheartTheInciter.Combat,
						new PrioritySelector(
							ScriptHelpers.CreateTankAgainstObject(
								ctx => _blackheartTheInciter.Combat && _blackheartTheInciter.CurrentTarget == StyxWoW.Me, ctx => nearestPillar.Key, ctx => nearestPillar.Value)))));
		}

		#endregion

		#region Grandmaster Vorpil

		[EncounterHandler(18732, "Grandmaster Vorpil", BossRange = 120, Mode = CallBehaviorMode.Proximity)]
		public Composite GrandmasterVorpilEncounter()
		{
			WoWUnit boss = null;
			List<WoWUnit> patCirclingBoss = null;
			List<WoWUnit> patInfrontOfBoss = null;
			List<WoWUnit> skellies = null;
			var waitForPatLoc = new WoWPoint(-312.7558, -262.3074, 12.6841);
			var patInfrontOfBossLoc = new WoWPoint(-271.8994, -263.7166, 12.68043);
			var kiteCasterToLoc = new WoWPoint(-356.7253, -264.3843, 12.68629);
			AddAvoidObject(ctx => StyxWoW.Me.IsTank(), () => patInfrontOfBossLoc, 45, 30, VoidTravelerId);

			return new PrioritySelector(
				ctx =>
				{
					boss = ctx as WoWUnit;
					if (boss != null)
					{
						patCirclingBoss = ScriptHelpers.GetUnfriendlyNpsAtLocation(boss.Location, 35, u => u != boss && u.Entry != 18797);
						patInfrontOfBoss = ScriptHelpers.GetUnfriendlyNpsAtLocation(patInfrontOfBossLoc, 25, u => u != boss && u.Entry != 18797);
						// GetUnfriendlyNpcs disgards any npcs that can't be selected..
						skellies =
							ObjectManager.GetObjectsOfType<WoWUnit>()
										 .Where(u => u.Entry == 18797 && u.IsAlive && u.Location.DistanceSqr(waitForPatLoc) < 25 * 25)
										 .OrderBy(u => u.DistanceSqr)
										 .ToList();
					}
					return boss;
				},
				// Trash Behavior
				new Decorator(
					ctx => !boss.Combat && boss.DistanceSqr <= 80 * 80,
					new PrioritySelector(
				// kite the casters away from boss to not agrro boss.
						new Decorator(ctx => patCirclingBoss.Any(u => u.Entry == 18638 && u.Combat), new Action(ctx => Navigator.MoveTo(kiteCasterToLoc))),
				// kill Skillies
						new Decorator(
							ctx => StyxWoW.Me.IsTank() && !StyxWoW.Me.Combat && skellies.Any() && patInfrontOfBoss.Count <= 4,
							new Action(ctx => Navigator.MoveTo(skellies[0].Location))),
						ScriptHelpers.CreatePullNpcToLocation(
							ctx => patCirclingBoss.Any(),
							ctx => patInfrontOfBoss.Any() && patInfrontOfBoss.Count <= 4,
							ctx => patInfrontOfBoss[0],
							ctx => waitForPatLoc,
							ctx => StyxWoW.Me.Location))),
				// Boss Behavior
				new Decorator(ctx => boss.Combat, new PrioritySelector()));
		}

		[ObjectHandler(182947, "The Codex of Blood")]
		public Composite TheCodexofBloodHandler()
		{
			WoWGameObject chest = null;
			PlayerQuest quest = null;
			const uint theCodexOfBloodQuestId = 29643;
			var interactTimer = new WaitTimer(TimeSpan.FromSeconds(1));

			return new PrioritySelector(
				ctx => chest = ctx as WoWGameObject,
				new Decorator(
					ctx => !ScriptHelpers.WillPullAggroAtLocation(chest.Location) && !Me.Combat && (quest = Me.QuestLog.GetQuestById(theCodexOfBloodQuestId)) != null,
					new PrioritySelector(
						new Decorator(ctx => !chest.WithinInteractRange, new Action(ctx => Navigator.MoveTo(chest.Location))),
						new Decorator(
							ctx => chest.WithinInteractRange,
							new PrioritySelector(
								new Decorator(
									ctx => !GossipFrame.Instance.IsVisible && !chest.InUse && interactTimer.IsFinished,
									new Action(
										ctx =>
										{
											chest.Interact();
											interactTimer.Reset();
										})),
								new Decorator(ctx => GossipFrame.Instance.IsVisible, new Action(ctx => GossipFrame.Instance.SelectActiveQuest(0))),
								new Decorator(
									ctx => QuestFrame.Instance.IsVisible,
									new Sequence(
										new Action(ctx => QuestFrame.Instance.CompleteQuest()),
										new WaitContinue(2, ctx => false, new ActionAlwaysSucceed()),
										new Action(ctx => QuestFrame.Instance.AcceptQuest()))))),
						new ActionAlwaysSucceed())));
		}

		#endregion

		#region Murmur

		private const uint MurmurId = 18708;

		private readonly int[] _sonicBoomIds = new[] { 33923, 38796 };

		[EncounterHandler(18708, "Murmur")]
		public Composite MurmurEncounter()
		{
			WoWUnit boss = null;
			// big bang
			AddAvoidObject(ctx => _sonicBoomIds.Contains(boss.CastingSpellId), 40, 18708);
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit, ScriptHelpers.CreateSpreadOutLogic(ctx => StyxWoW.Me.HasAura("Murmur's Touch"), ctx => boss.Location, 15, 35));
		}

		#endregion
	}
}