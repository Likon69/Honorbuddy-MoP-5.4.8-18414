using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommonBehaviors.Actions;
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Frames;
using Styx.Helpers;
using Styx.CommonBot.POI;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.TreeSharp;
using Styx;
using Styx.WoWInternals.WoWObjects;
using System.Collections.Generic;
using Action = Styx.TreeSharp.Action;

using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Profiles.Handlers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Classic
{
	public class TheDeadmines : Dungeon
	{
		#region Overrides of Dungeon

		private readonly WaitTimer _teleportUseTimer = new WaitTimer(TimeSpan.FromSeconds(10));

		private readonly List<TeleporterLocation> _teleporterLocations = new List<TeleporterLocation>
																		 {
																			 new TeleporterLocation(
																				 new WoWPoint(-305.349, -491.311, 49.8738), "Helix Gearbreaker"),
																			 new TeleporterLocation(
																				 new WoWPoint(-201.04, -606.064, 19.8946), "Foe Reaper 5000"),
																			 new TeleporterLocation(
																				 new WoWPoint(-129.379, -789.0528, 18.11771), "Admiral Ripsnarl")
																		 };

		/// <summary>
		///     The mapid of this dungeon.
		/// </summary>
		/// <value> The map identifier. </value>
		public override uint DungeonId
		{
			get { return 6; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(-11208.21, 1680.011, 23.94507); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(-15.09515, -389.5024, 63.35484); }
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
			// reset Captain Cookie's spawn timer on  Admiral Ripsnarl's death
			if (boss.Entry == AdmiralRipsnarlId)
				_captainCookieSpawnTimer.Reset();
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (unit != null)
					{
						if (unit.Entry == FoeReaper5000Id && (unit.CastingSpellId == OverdriveId || unit.HasAura("Harvest")) && Me.IsMelee())
							return true;
						if (_parrotIds.Contains(unit.Entry) && !unit.Combat)
							return true;
					}
					return false;
				});
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			foreach (var unit in incomingunits.Cast<WoWUnit>())
			{
				if (unit.Entry == HelixGearbreakerId || unit.Entry == CaptainCookieId)
					outgoingunits.Add(unit);
			}
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var priority in units)
			{
				var unit = priority.Object as WoWUnit;
				if (unit == null)
					continue;
			}
		}

		public override async Task<bool> HandleMovement(WoWPoint location)
		{
			var myLoc = Me.Location;
			var meToLocationDistance = myLoc.Distance(location);
			if (meToLocationDistance > 100 && !ScriptHelpers.IsBossAlive("Helix Gearbreaker"))
			{
				var nearbyTeleporter = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(g => g.Entry == GoblinTeleporterId && g.Distance <= 50);
				if (nearbyTeleporter != null)
				{
					var portToLoc =
						_teleporterLocations.Where(port => meToLocationDistance - port.Location.Distance(location) > 100 && !ScriptHelpers.IsBossAlive(port.RequiredBoss))
											.OrderBy(port => port.Location.DistanceSqr(location))
											.FirstOrDefault();
					if (portToLoc != null)
					{
						// throttle teleport usage to prevent Tank from going back to followers immediately
						if (!_teleportUseTimer.IsFinished)
							return true;

						if (!nearbyTeleporter.WithinInteractRange)
						{
                            return (await CommonCoroutines.MoveTo(nearbyTeleporter.Location)).IsSuccessful();
						}
						if (!GossipFrame.Instance.IsVisible)
							nearbyTeleporter.Interact();
						else
						{
							_teleportUseTimer.Reset();
							GossipFrame.Instance.SelectGossipOption(_teleporterLocations.IndexOf(portToLoc));
						}
						return true;
					}
				}
			}
			return false;
		}

		#endregion

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		#region Root

		private const uint GoblinTeleporterId = 208002;

		private const uint OnlyTheBeginningQuestId = 27842;
		private const uint GoodIntentionsPoorExecutionQuestId = 27848;

		private const uint DefiasCannonId = 16398;
		private readonly int[] _cannonballSpellIds = new[] { 89697, 89757 };
		private readonly WoWPoint _deadlyDropoffPoint = new WoWPoint(-40.37247, -733.9104, 9.485084);

		private readonly uint[] _parrotIds = new uint[] { 48447, 48448, 48450 };

		[EncounterHandler(0)]
		public Composite RootHandler()
		{
			// cannon fire 
			AddAvoidLocation(
				ctx => true,
				3.5f,
				o => ((WoWMissile)o).ImpactPosition,
				() => WoWMissile.InFlightMissiles.Where(m => _cannonballSpellIds.Contains(m.SpellId) && m.ImpactPosition.Distance(Me.Location) < 20));

			// avoid this cannon because bot gets stucks at it. 
			AddAvoidObject(ctx => true, 3, o => o.Entry == DefiasCannonId && !o.ToGameObject().CanUse());

			AddAvoidLocation(ctx => Me.Location.Distance(_deadlyDropoffPoint) < 10, 3, o => _deadlyDropoffPoint);
			PlayerQuest quest = null;

			return
				new PrioritySelector(
					new Decorator(
						ctx => (quest = Me.QuestLog.GetQuestById(OnlyTheBeginningQuestId)) != null && quest.IsCompleted,
						ScriptHelpers.CreateCompletePopupQuest(OnlyTheBeginningQuestId)),
					new Decorator(
						ctx => (quest = Me.QuestLog.GetQuestById(GoodIntentionsPoorExecutionQuestId)) != null && quest.IsCompleted,
						ScriptHelpers.CreateCompletePopupQuest(GoodIntentionsPoorExecutionQuestId)));
		}


		[EncounterHandler(46902, "Miss Mayhem", Mode = CallBehaviorMode.Proximity, BossRange = 40)]
		[EncounterHandler(46906, "Slinky Sharpshiv", Mode = CallBehaviorMode.Proximity, BossRange = 40)]
		[EncounterHandler(46889, "Kagtha", Mode = CallBehaviorMode.Proximity, BossRange = 40)]
		public Composite QuestGiversBehavior()
		{
			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				new Decorator(ctx => !Me.Combat && unit.QuestGiverStatus == QuestGiverStatus.Available, ScriptHelpers.CreatePickupQuest(ctx => unit)),
				new Decorator(ctx => !Me.Combat && unit.QuestGiverStatus == QuestGiverStatus.TurnIn, ScriptHelpers.CreateTurninQuest(ctx => unit)));
		}

		[ObjectHandler(16398, "Defias Cannon", ObjectRange = 20)]
		public Composite DefiasCannonHandler()
		{
			WoWGameObject cannon = null;
			return new PrioritySelector(
				ctx => cannon = ctx as WoWGameObject,
				new Decorator(
					ctx => cannon.CanUse() && !Me.Combat,
					new PrioritySelector(
						new Decorator(ctx => cannon.WithinInteractRange, new Action(ctx => cannon.Interact())),
						new Decorator(ctx => !cannon.WithinInteractRange, new Action(ctx => Navigator.MoveTo(cannon.Location))))));
		}

		#endregion

		#region Glubtok

		private const uint GlubtokId = 47162;
		private const int FrostBlossomSpellId = 88169;
		private const int FireBlossomSpellId = 88129;
		private const uint FireBlossomBunnyId = 47282;
		private const uint FrostBlossomBunnyId = 47284;

		[EncounterHandler(47162, "Glubtok")]
		public Composite GlubtokEncounter()
		{
			WoWUnit boss = null;
			// stay 5 units from Glubtoks current targt location because of the 'Fists of Flame' and 'Fists of Frost' abilities

			// Avoid the frost/fire blossom missiles 
			AddAvoidLocation(
				ctx => true,
				5,
				o => ((WoWMissile)o).ImpactPosition,
				() => WoWMissile.InFlightMissiles.Where(m => m.SpellId == FireBlossomSpellId || m.SpellId == FrostBlossomSpellId));
			AddAvoidObject(ctx => true, 5, u => (u.Entry == FireBlossomBunnyId || u.Entry == FrostBlossomBunnyId) && u.ToUnit().HasAura("Blossom Targetting"));
			return new PrioritySelector(ctx => boss = ctx as WoWUnit);
		}

		#endregion

		#region Helix Gearbreaker

		private const uint StickyBombId = 47314;
		private const uint HelixGearbreakerId = 47296;

		[EncounterHandler(47296, "Helix Gearbreaker")]
		public Composite HelixGearbreakerEncounter()
		{
			WoWUnit boss = null;

			AddAvoidObject(ctx => true, o => o.ToUnit().HasAura("Sticky Bomb Periodic Trigger") ? 3 : 1, StickyBombId);

			return new PrioritySelector(ctx => boss = ctx as WoWUnit);
		}

		#endregion

		#region Foe Reaper 5000

		private const int OverdriveId = 88481;
		private const uint FoeReaper5000Id = 43778;
		private const uint FoeReaperTargetingBunnyId = 47468;
		private const int HarvestSpellId = 88495;

		[EncounterHandler(43778, "Foe Reaper 5000", Mode = CallBehaviorMode.Proximity)]
		public Composite FoeReaper5000Encounter()
		{
			WoWUnit boss = null;

			var roomCenterLoc = new WoWPoint(-205.9237, -572.6401, 20.97683);

			AddAvoidObject(ctx => !Me.IsCasting, () => roomCenterLoc, 25, 10, u => u.Entry == FoeReaper5000Id && u.ToUnit().CastingSpellId == OverdriveId);

			// avoid boss when using the Harvest ability.
			AddAvoidObject(ctx => true, 10, o => o.Entry == FoeReaper5000Id && o.ToUnit().HasAura("Harvest"));

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				// clear room to cause the boss to activate.
				new Decorator(ctx => boss.HasAura("Off-line"), ScriptHelpers.CreateClearArea(() => boss.Location, 50)),
				new Decorator(
					ctx => boss.Combat,
					new PrioritySelector(
				// only tank should be in front 
						ScriptHelpers.CreateAvoidUnitAnglesBehavior(ctx => !Me.IsTank() && boss.Distance <= 10, ctx => boss, new ScriptHelpers.AngleSpan(0, 80)),
				// do nothing if we have no target e.g. bot is melee and is waiting for boss to stop whirlwind
						new Decorator(ctx => Targeting.Instance.IsEmpty() && Me.IsMelee(), new ActionAlwaysSucceed()))));
		}

		#endregion

		#region Admiral Ripsnarl

		private const uint AdmiralRipsnarlId = 47626;
		private readonly WaitTimer _ripSnarlcombatTimer = new WaitTimer(TimeSpan.FromSeconds(15));

		[EncounterHandler(47626, "Admiral Ripsnarl", Mode = CallBehaviorMode.CurrentBoss)]
		public Composite AdmiralRipsnarlVanishEncounter()
		{
			WoWUnit boss = null;

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				new Decorator(
					ctx => Me.IsTank() && boss == null && Targeting.Instance.IsEmpty() && !_ripSnarlcombatTimer.IsFinished,
					new PrioritySelector(new ActionSetActivity("Waiting for Admiral Ripsnarl to reappear"), new ActionAlwaysSucceed())));
		}

		[EncounterHandler(47626, "Admiral Ripsnarl")]
		public Composite AdmiralRipsnarlEncounter()
		{
			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				// reset the vanish timer as long as boss is active.
				new Action(
					ctx =>
					{
						_ripSnarlcombatTimer.Reset();
						return RunStatus.Failure;
					}),
				// avoid the Swipe attach
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(
					ctx => !Me.IsTank() && boss.Distance < 8 && Me.CurrentTargetGuid == boss.Guid && boss.CurrentTargetGuid != Me.Guid,
					ctx => boss,
					new ScriptHelpers.AngleSpan(0, 180)));
		}

		#endregion

		#region Captain Cookie

		private const uint CaptainCookieId = 47739;

		private readonly uint[] _badFoodIds = new uint[] { 48276, 48295, 48299, 48293, 48298, 48302 };
		private readonly WaitTimer _captainCookieSpawnTimer = new WaitTimer(TimeSpan.FromSeconds(30));

		private readonly uint[] _goodFoodIds = new uint[] { 48006, 48296, 48300, 48294, 48297, 48301 };

		[EncounterHandler(47739, "Captain Cookie", Mode = CallBehaviorMode.CurrentBoss)]
		public Composite CaptainCookieWaitForSpawnEncounter()
		{
			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				new Decorator(
					ctx => Me.IsTank() && boss == null && Targeting.Instance.IsEmpty() && !_captainCookieSpawnTimer.IsFinished,
					new PrioritySelector(new ActionSetActivity("Waiting for Captain Cookie to spawn"), new ActionAlwaysSucceed())));
		}


		[EncounterHandler(47739, "Captain Cookie")]
		public Composite CaptainCookieEncounter()
		{
			WoWUnit boss = null;

			WoWUnit goodFood = null;
			AddAvoidObject(ctx => !Me.IsCasting, 2, _badFoodIds);

			return new PrioritySelector(
				ctx =>
				{
					goodFood = !Me.HasAura("Satiated")
								   ? (from food in ObjectManager.GetObjectsOfType<WoWUnit>().Where(o => _goodFoodIds.Contains(o.Entry))
									  let foodLoc = food.Location
									  let foodDistSqr = Me.Location.DistanceSqr(foodLoc)
									  where !Me.PartyMembers.Any(p => !p.HasAura("Satiated") && p.Location.DistanceSqr(foodLoc) < foodDistSqr)
									  orderby foodDistSqr
									  select food).FirstOrDefault()
								   : null;
					return boss = ctx as WoWUnit;
				},
				new Decorator(
					ctx => goodFood != null,
					new PrioritySelector(
						new Decorator(ctx => goodFood.WithinInteractRange, new Action(ctx => goodFood.Interact())),
						new Decorator(ctx => !goodFood.WithinInteractRange, new Action(ctx => Navigator.MoveTo(goodFood.Location))))));
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