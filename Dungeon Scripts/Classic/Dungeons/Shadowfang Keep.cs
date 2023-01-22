
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.Helpers;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using System.Linq;
using Action = Styx.TreeSharp.Action;

using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
using Bots.DungeonBuddy.Profiles.Handlers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Classic
{
	public class ShadowfangKeep : Dungeon
	{
		#region Overrides of Dungeon

		private readonly List<TeleporterLocation> _teleporterLocations = new List<TeleporterLocation>
		{
			new TeleporterLocation(new WoWPoint(-274.603, 2297.12, 76.1534), "Baron Silverlaine"),
			new TeleporterLocation(new WoWPoint(-223.041, 2258.44, 102.756), "Commander Springvale"),
			new TeleporterLocation(new WoWPoint(-160.636, 2178.7, 138.698), "Lord Walden")
		};

		public override uint DungeonId
		{
			get { return 8; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(-233.2333, 1571, 76.88484); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(-231.207, 2104.399, 76.89213); }
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var priority in units)
			{
				var unit = priority.Object as WoWUnit;
				if (unit != null)
				{
					if (unit.Entry == BloodthirstyGhoulId)
						priority.Score += 500;
				}
			}
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			base.IncludeTargetsFilter(incomingunits, outgoingunits);

			foreach (var unit in incomingunits.Select(obj => obj.ToUnit()))
			{
				if ((unit.Entry == ShadowChargerId || unit.Entry == FelSteedId) && unit.Combat)
					outgoingunits.Add(unit);
			}
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				o =>
				{
					var unit = o as WoWUnit;
					if (unit != null)
					{
						if ((unit.Entry == ShadowChargerId || unit.Entry == FelSteedId) && !unit.Combat)
							return true;
					}
					return false;
				});
		}

		public override void OnEnter()
		{
			BossManager.OnBossKill += BossManager_OnBossKill;
		}

		public override void OnExit()
		{
			BossManager.OnBossKill -= BossManager_OnBossKill;
		}

		private void BossManager_OnBossKill(Boss boss)
		{
			if (boss.Entry == CommanderSpringvaleId || boss.Entry == LordWaldenId || boss.Entry == LordGodfreyId)
			{
				_waitForQuestTimer.Reset();
			}
		}

		public override MoveResult MoveTo(WoWPoint location)
		{
			var myLoc = Me.Location;
			var meToLocationDistance = myLoc.Distance(location);
			if (meToLocationDistance > 100 && !ScriptHelpers.IsBossAlive("Baron Silverlaine"))
			{
				var nearbyStablemaster = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(g => g.Entry == HauntedStableHandId && !Blacklist.Contains(g, BlacklistFlags.All) && g.Distance <= 50);
				if (nearbyStablemaster != null)
				{
					var pathDistToDestination = myLoc.PathDistance(location);
					if (pathDistToDestination.HasValue)
					{
						var portToLoc = (from port in _teleporterLocations
							let portPathDist = port.Location.PathDistance(location)
							where portPathDist.HasValue && pathDistToDestination.Value - portPathDist.Value > 100f && !ScriptHelpers.IsBossAlive(port.RequiredBoss)
							orderby portPathDist
							select port).FirstOrDefault();
						if (portToLoc != null)
						{
							if (!nearbyStablemaster.WithinInteractRange)
								return Navigator.MoveTo(nearbyStablemaster.Location);
							if (!GossipFrame.Instance.IsVisible)
								nearbyStablemaster.Interact();
							else
							{
								GossipFrame.Instance.SelectGossipOption(_teleporterLocations.IndexOf(portToLoc));
								Blacklist.Add(nearbyStablemaster, BlacklistFlags.All, TimeSpan.FromMinutes(1));
							}
							return MoveResult.Moved;
						}
					}
				}
			}
			return base.MoveTo(location);
		}

		#endregion

		#region Root

		private const uint FelSteedId = 3864;
		private const int SweetMercilessRevengeQuestId = 27998;
		private const int PlaguePlagueEverywhereId = 27988;
		private const uint HauntedStableHandId = 51400;
		private const uint ShadowChargerId = 3865;

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0)]
		public Composite RootLogic()
		{
			return new PrioritySelector();
		}

		[EncounterHandler(47293, "Deathstalker Commander Belmont", Mode = CallBehaviorMode.Proximity, BossRange = 40)]
		public Composite QuestGiversBehavior()
		{
			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				new Decorator(ctx => !Me.Combat && unit.QuestGiverStatus == QuestGiverStatus.Available, ScriptHelpers.CreatePickupQuest(ctx => unit)),
				new Decorator(ctx => !Me.Combat && unit.QuestGiverStatus == QuestGiverStatus.TurnIn, ScriptHelpers.CreateTurninQuest(ctx => unit)));
		}

		#endregion

		[LocationHandler(-241.613, 2156.265, 90.62401, 10, "Wait for door to open")]
		public Composite BaronAshburyArea()
		{
			const uint courtyardDoorId = 18895;

			WoWGameObject door = null;
			return new PrioritySelector(
				ctx => door = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(o => o.Entry == courtyardDoorId),
				// wait for door to open..
				new Decorator(
					ctx => !ScriptHelpers.IsBossAlive("Baron Ashbury") && door != null && door.State == WoWGameObjectState.Ready && Me.IsTank() && !Me.Combat,
					new ActionAlwaysSucceed()));
		}

		[EncounterHandler(46962, "Baron Ashbury")]
		public Composite BaronAshburyFight()
		{
			WoWPlayer tank = null;
			return new PrioritySelector(
				ScriptHelpers.CreateDispelGroup("Pain and Suffering", ScriptHelpers.PartyDispelType.Magic),
				// because of LOS issues the healer should move close to tank.
				new Decorator(
					ctx => Me.IsHealer() && (tank = ScriptHelpers.Tank) != null && tank.IsAlive && tank.Distance > 10, new Action(ctx => Navigator.MoveTo(tank.Location))));
		}

		[EncounterHandler(3887, "Baron Silverlaine")]
		public Composite BaronSilverlaineFight()
		{
			return new PrioritySelector(ScriptHelpers.CreateDispelGroup("Veil of Shadow", ScriptHelpers.PartyDispelType.Curse));
		}

		#region Commander Springvale

		private const uint CommanderSpringvaleId = 4278;
		private readonly WaitTimer _waitForQuestTimer = new WaitTimer(TimeSpan.FromSeconds(15));

		[LocationHandler(-246.2209, 2252.282, 100.8934, 35, "Clear Commander Springvale's room.")]
		public Composite ClearCommanderRoom()
		{
			PlayerQuest quest;
			WoWUnit trash = null;
			var roomCenterLoc = new WoWPoint(-246.2209, 2252.282, 100.8934);
			var pullToLoc = new WoWPoint(-249.3336, 2261.913, 100.8891);
			return
				new PrioritySelector(
					ctx => trash = ScriptHelpers.GetUnfriendlyNpsAtLocation(roomCenterLoc, 35, u => u.Entry != CommanderSpringvaleId && u.ZDiff < 10).FirstOrDefault(),
				// pull trash to doorway of room.
					ScriptHelpers.CreatePullNpcToLocation(ctx => trash != null, ctx => trash, ctx => pullToLoc, 3),
				// wait for questgiver to show up and shutup
					new Decorator(
						ctx => !Me.Combat && !_waitForQuestTimer.IsFinished && (quest = Me.QuestLog.GetQuestById(PlaguePlagueEverywhereId)) != null && quest.IsCompleted,
						new ActionAlwaysSucceed()));
		}

		[EncounterHandler(4278, "Commander Springvale")]
		public Composite CommanderSpringvaleFight()
		{
			WoWUnit boss = null;
			const int shieldOfThePerfidiousId = 93693;

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(
					ctx => !Me.IsTank() || boss.CastingSpellId == shieldOfThePerfidiousId, ctx => boss, new ScriptHelpers.AngleSpan(0, 40)));
		}

		#endregion

		#region Lord Walden

		private const uint LordWaldenId = 46963;

		[LocationHandler(-146.3959, 2172.847, 127.9531, 30, "Wait for quest giver at Lord Walden")]
		public Composite LordWaldenArea()
		{
			const uint sorcerersGateId = 18972;
			const int ordersAreForTheLivingQuestId = 27996;

			PlayerQuest quest = null;
			WoWGameObject door = null;
			return new PrioritySelector(
				ctx => door = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(o => o.Entry == sorcerersGateId),
				// wait for door to open..
				new Decorator(
					ctx => !ScriptHelpers.IsBossAlive("Lord Walden") && door != null && door.State == WoWGameObjectState.Ready && Me.IsTank() && !Me.Combat,
					new ActionAlwaysSucceed()),
				// wait for questgiver to spawn and finish talking..
				new Decorator(
					ctx => !Me.Combat && !_waitForQuestTimer.IsFinished && (quest = Me.QuestLog.GetQuestById(ordersAreForTheLivingQuestId)) != null && quest.IsCompleted,
					new ActionAlwaysSucceed()));
		}

		[EncounterHandler(46963, "Lord Walden")]
		public Composite LordWaldenFight()
		{
			const int conjurePoisonousMixtureId = 93697;

			var iceShardIds = new[] { 93532, 93542 };
			var centerOfRoom = new WoWPoint(-146.3959, 2172.847, 127.9531);

			AddAvoidLocation(
				ctx => !(Me.IsCasting && Me.CurrentCastTimeLeft <= TimeSpan.FromSeconds(1)),
				2,
				o => ((WoWMissile)o).ImpactPosition,
				() => WoWMissile.InFlightMissiles.Where(m => iceShardIds.Contains(m.SpellId)));
			AddAvoidLocation(
				ctx => true,
				() => centerOfRoom,
				15,
				4,
				o => ((WoWMissile)o).ImpactPosition,
				() => WoWMissile.InFlightMissiles.Where(m => m.SpellId == conjurePoisonousMixtureId));
			return new PrioritySelector();
		}

		#endregion

		#region Lord Godfrey

		private const uint BloodthirstyGhoulId = 50561;
		private const uint LordGodfreyId = 46964;
		

		[EncounterHandler(46964, "Lord Godfrey")]
		public Func<WoWUnit, Task<bool>> LordGodfreyLogic()
		{
			const int cursedBulletsId = 93629;
			const int desecrationStalkerId = 50503;
			const int pistolBarrageSpellId = 93520;

			AddAvoidObject(ctx => true, 6, o => o.Entry == desecrationStalkerId && o.ToUnit().HasAura("Desecration"));
			var pistolBarrageAvoidArc = new ScriptHelpers.AngleSpan(0, 120);

			return async boss => (boss.CastingSpellId == pistolBarrageSpellId 
								&& await ScriptHelpers.AvoidUnitAngles(boss, pistolBarrageAvoidArc))
								|| await ScriptHelpers.InterruptCast(boss, cursedBulletsId)
								|| await ScriptHelpers.DispelGroup("Cursed Bullets", ScriptHelpers.PartyDispelType.Curse);
		}

		[LocationHandler(-98.43058, 2141.098, 144.9211, 40, "Wait for quest giver to show up!")]
		public Composite WaitAtLordGodfreyQuestArea()
		{
			PlayerQuest quest = null;
			// wait for questgiver to spawn and finish talking..
			return
				new Decorator(
					ctx => !Me.Combat && !_waitForQuestTimer.IsFinished && (quest = Me.QuestLog.GetQuestById(SweetMercilessRevengeQuestId)) != null && quest.IsCompleted,
					new ActionAlwaysSucceed());
		}

		#endregion

		#region Nested type: TeleporterLocation

		private class TeleporterLocation
		{
			public readonly WoWPoint Location;
			public readonly string RequiredBoss;

			public TeleporterLocation(WoWPoint loc, string requiredBoss)
			{
				Location = loc;
				RequiredBoss = requiredBoss;
			}
		}

		#endregion
	}
}