using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bots.DungeonBuddy.Profiles.Handlers;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.Helpers;
using Styx.Patchables;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
using Action = Styx.TreeSharp.Action;

namespace Bots.DungeonBuddy.Dungeon_Scripts.Burning_Crusade
{
	public class TheEscapeFromDurnholde : Dungeon
	{

		#region Overrides of Dungeon

		private const uint ThrallId = 17876;

		public override uint DungeonId
		{
			get { return 170; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(-8332.569, -4057.429, -207.7462); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(2762.708, 1294.983, 13.77185); }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			WoWUnit thrall = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == ThrallId);
			units.RemoveAll(
				u =>
				{ // remove low level units.
					var unit = u as WoWUnit;
					if (unit == null)
						return false;

					if (StyxWoW.Me.Level - unit.Level > 5 && !unit.Combat)
						return true;

					if (thrall != null && unit.DistanceSqr > 25 * 25 && !unit.Combat)
						return true;
					return false;
				});
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			foreach (var u in incomingunits)
			{
				var unit = u as WoWUnit;
				var currentTarget = unit.CurrentTarget;
				if (currentTarget != null && currentTarget.Entry == ThrallId)
					outgoingunits.Add(unit);
			}
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var priority in units)
			{
				var unit = priority.Object as WoWUnit;
				if (unit == null) continue;
				var currentTarget = unit.CurrentTarget;
				if (currentTarget != null && currentTarget.Entry == ThrallId)
					priority.Score += 250;
				else if (unit.Entry == DurnholdeRiflemanId && StyxWoW.Me.IsRange())
					priority.Score += 200;

				else if (unit.Entry == DurnholdeWardenId && Me.IsDps())
					priority.Score += 210;
			}
		}

		public override void OnEnter()
		{
			_epochHunterStep1Done = _epochHunterStep2Done = _epochHunterStep3Done = _villageOnFire = false;
		}

		public override bool IsComplete
		{
			// wait for quest to be handed in before considering dungeon complete.
			get { return base.IsComplete && !ScriptHelpers.HasQuest(EscapeFromDurnholdeQuestId); }
		}

		#endregion

		private const uint DurnholdeRiflemanId = 17820;
		private const uint DurnholdeWardenId = 17833;

		private readonly CircularQueue<WoWPoint> _barrelLocations = new CircularQueue<WoWPoint>
																	{
																		new WoWPoint(2102.327, 113.4871, 53.25289),
																		new WoWPoint(2166.457, 219.0035, 52.66649),
																	};

		private static readonly WoWPoint TunnelEndPoint = new WoWPoint(2378.912, 1174.472, 65.89436);

		private readonly WoWPoint _villageLocation = new WoWPoint(2127.658, 180.1258, 69.28806);

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		private bool OnFire
		{
			get { return ObjectManager.GetObjectsOfType<WoWGameObject>().Any(o => o.IsValid && o.Entry == 182592); }
		}

		[EncounterHandler(0)]
		public Composite RootBehavior()
		{
			return new PrioritySelector();
		}

		const int DrakeId = 18725;
		const uint ErozionId = 18723;

		[EncounterHandler(18723, "Erozion", Mode = CallBehaviorMode.Proximity)]
		public async Task<bool> ErozionEncounter(WoWUnit npc)
		{
			if (Me.Combat || ScriptHelpers.WillPullAggroAtLocation(npc.Location))
				return false;

			// pickup quests
			if (npc.HasQuestAvailable(true) && await ScriptHelpers.PickupQuest(npc))
				return true;
			// turnin quests.
			if (npc.HasQuestTurnin() && await ScriptHelpers.TurninQuest(npc))
				return true;


			// Don't interact with NPC when running into tunnel e.g. when selling or reseting in Farm Mode
			if (!IsInTunnel(_destination))
			{
				// get some bombs.
				if (!Me.BagItems.Any(i => i.IsValid && i.Entry == BombItemId))
					return await ScriptHelpers.TalkToNpc(npc);
				// talk to the drake for a ride
				if (Me.Mounted)
					return await CommonCoroutines.Dismount();

				if (_shapeshiftFormsPreventingTaxiRide.Contains(Me.Shapeshift))
					Lua.DoString("CancelShapeshiftForm()");
				var drake = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == DrakeId) as WoWUnit;
				return await ScriptHelpers.TalkToNpc(drake);
			}
			return false;
		}

		private readonly ShapeshiftForm[] _shapeshiftFormsPreventingTaxiRide =
		{
			ShapeshiftForm.Bear, ShapeshiftForm.Cat, ShapeshiftForm.DireBear, ShapeshiftForm.Travel,
			ShapeshiftForm.TreeOfLife, ShapeshiftForm.GhostWolf
		};

		private WoWPoint _destination;
		public override MoveResult MoveTo(WoWPoint location)
		{
			_destination = location;
			return base.MoveTo(location);
		}

		bool IsInTunnel(WoWPoint point)
		{
			return point.X > 2390 && point.Y > 1155 && point.Z < 67;
		}

		[ObjectHandler(184393, "Prison Door")]
		public Composite PrisonDoorHandler()
		{
			return new PrioritySelector();
		}

		[EncounterHandler(17862, "Captain Skarloc", Mode = CallBehaviorMode.CurrentBoss)]
		public Func<WoWUnit, Task<bool>> CaptainSkarlocEncounter()
		{
			var thrallLoc = new WoWPoint(2229.994, 117.7617, 82.29035);
			var thrallEscortEndLoc = new WoWPoint(2063.042, 229.1765, 64.49027);

			bool waitedForBeforeTalkingToThrall = false;
			return async boss =>
			{
				var thrall = Thrall;
				if (thrall == null && !Me.Combat && Me.Location.DistanceSqr(thrallLoc) < 10*10)
				{
					ScriptHelpers.MarkBossAsDead("Captain Skarloc", "Thrall wasn't found in his cage");
					return false;
				}

				var hasTurnin = thrall != null && thrall.HasQuestTurnin();
				var hasPickup = thrall != null && thrall.HasQuestAvailable(true);
				if ((hasTurnin || hasPickup) && thrall.DistanceSqr < 25 * 25 && !Me.IsActuallyInCombat)
				{
					// Thrall is on another floor level so we need to check path distance
					var pathDist = Navigator.PathDistance(Me.Location, thrall.Location);
					if (pathDist.HasValue && pathDist < 25)
					{
						if (hasTurnin && await ScriptHelpers.TurninQuest(thrall)
							|| hasPickup && await ScriptHelpers.PickupQuest(thrall))
						{
							return true;
						}
					}
				}

				if (!waitedForBeforeTalkingToThrall )
				{
					try
					{
						TreeRoot.StatusText = "Giving group members a chance to turnin quest and get the followup quest";
						if (await Coroutine.Wait(12000, () => ScriptHelpers.GroupMembers.Any(g => g.Player != null && g.Player.Combat)))
							return false;
						waitedForBeforeTalkingToThrall = true;
					}
					finally
					{
						TreeRoot.StatusText = null;
					}
				}

				// wait a few mins for everyone to turn in a quest..
				if (Me.PartyMembers.All(g => g.IsAlive)
					&& (thrall == null || thrall.Location.Distance(thrallEscortEndLoc) > 10*10))
				{
					return await ScriptHelpers.TankTalkToAndEscortNpc(thrall, thrallLoc);
				}
				return false;
			};
		}

		#region Epoch Hunter

		private const uint EscapeFromDurnholdeQuestId = 29599;

		private bool _epochHunterStep1Done;
		private bool _epochHunterStep2Done;
		private bool _epochHunterStep3Done;

		private WoWUnit _taretha;
		private WoWUnit _thrall;

		private WoWUnit Thrall
		{
			get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == ThrallId); }
		}

		[EncounterHandler(18096, "Epoch Hunter", Mode = CallBehaviorMode.CurrentBoss)]
		public Func<WoWUnit, Task<bool>> EpochHunterSpawnBehavior()
		{
			var thrallStartLoc1 = new WoWPoint(2063.042, 229.1765, 64.49027);
			var thrallStartLoc2 = new WoWPoint(2486.878, 624.3096, 57.90615);
			var thrallStartLoc3 = new WoWPoint(2660.485, 659.4092, 61.9358);
			var thrallEndLoc = new WoWPoint(2635.069, 673.2795, 54.46173);

			return async epochHunter =>
			{
				if (!Me.IsLeader())
					return false;

				_thrall = Thrall;
				_taretha = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == 18887);
				if (!_epochHunterStep1Done)
				{
					_epochHunterStep1Done = ((_thrall != null && _thrall.Location.DistanceSqr(thrallStartLoc2) < 5 * 5)
											|| _thrall == null && Me.Location.DistanceSqr(thrallStartLoc1) < 10 * 10)
											|| _epochHunterStep2Done;
				}
				if (!_epochHunterStep2Done)
				{
					_epochHunterStep2Done = (_thrall == null && Me.Location.DistanceSqr(thrallStartLoc2) < 10 * 10)
						|| (_thrall != null && _thrall.Location.DistanceSqr(thrallStartLoc3) < 5 * 5
							&& _taretha != null && _taretha.CanGossip);
				}
				else if (_thrall != null && _thrall.IsDead)
				{
					_epochHunterStep2Done = false;
				}
				if (!_epochHunterStep3Done)
				{
					_epochHunterStep3Done = _epochHunterStep2Done
											&& ((_thrall != null && _thrall.Location.DistanceSqr(thrallEndLoc) < 5 * 5)
												|| (_thrall == null && Me.Location.DistanceSqr(thrallStartLoc3) < 10 * 10)
												|| (epochHunter != null && epochHunter.Combat));
				}

				if (!_epochHunterStep1Done)
				{
					if (Me.Location.DistanceSqr(thrallStartLoc1) < 5*5 && _thrall == null)
					{
						_epochHunterStep1Done = true;
						return true;
					}
				
					if (_thrall == null || _thrall.Location.Distance2D(thrallStartLoc2) > 1 * 1)
						return await ScriptHelpers.TankTalkToAndEscortNpc(_thrall, thrallStartLoc1, 10, 1, 1, 1);							
				}

				if (!_epochHunterStep2Done && _epochHunterStep1Done)
				{
					return (_taretha == null || !_taretha.CanGossip)
								&& await ScriptHelpers.TankTalkToAndEscortNpc(_thrall, thrallStartLoc2, 3);
				}

				if (!_epochHunterStep3Done && _epochHunterStep2Done && _epochHunterStep1Done)
				{
					if (Me.Location.DistanceSqr(thrallStartLoc3) < 5*5 && _thrall == null)
					{
						_epochHunterStep2Done = false;
						return true;
					}
					if (_taretha != null)
					{
						if (_taretha.CanGossip)
							return await ScriptHelpers.TalkToNpc(_taretha, 1, 1);

						if (epochHunter == null)
						{
							await ScriptHelpers.TankTalkToAndEscortNpc(_thrall, thrallStartLoc3, 3);
						}
						else if (!epochHunter.Attackable && Targeting.Instance.IsEmpty() && Me.IsTank())
						{
							if (Me.Location.DistanceSqr(thrallEndLoc) > 6*6)
								ScriptHelpers.SetLeaderMoveToPoiPS(thrallEndLoc);
							else // wait for the waves to start
								return true;
						}
					}
				}
				return false;
			};
		}

		[EncounterHandler(18096, "Epoch Hunter")]
		public Composite EpochHunterEncounter()
		{
			WoWUnit epochHunter = null;
			return new PrioritySelector(
				ctx => epochHunter = ctx as WoWUnit,
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(
					ctx => !Me.IsTank() && epochHunter.CurrentTargetGuid != Me.Guid && epochHunter.Distance < 20, ctx => epochHunter, new ScriptHelpers.AngleSpan(0, 180)),
				new Decorator(ctx => Me.IsTank() && Targeting.Instance.TargetList.All(t => t.CurrentTargetGuid == Me.Guid), ScriptHelpers.CreateTankFaceAwayGroupUnit(20)));
		}

		#endregion

		#region Lieutenant Drake

		private const uint BombItemId = 25853;
		private const uint LieutenantDrakeId = 17848;
		private bool _villageOnFire;

		[EncounterHandler(17848, "Lieutenant Drake", Mode = CallBehaviorMode.CurrentBoss)]
		public Composite LieutenantDrakeSpawnEncounter()
		{
			WoWUnit boss = null;
			WoWGameObject barrel = null;
			return new PrioritySelector(
				ctx =>
				{
					if (!_villageOnFire)
						_villageOnFire = OnFire;
					return boss = ctx as WoWUnit;
				},
				// go to each barrel
				new Decorator(
					ctx => Targeting.Instance.IsEmpty() && !_villageOnFire && Me.Location.Distance(_villageLocation) <= 200,
					new PrioritySelector(
						ctx =>
						{
							barrel =
								ObjectManager.GetObjectsOfType<WoWGameObject>().Where(o => o.Entry == 182589 && o.CanUse()).OrderBy(o => o.DistanceSqr).FirstOrDefault();
							return _barrelLocations.Peek();
						},
						new ActionSetActivity("Blowing up barrels"),
						new Decorator(ctx => barrel != null && barrel.Distance > 10, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(barrel.Location))),
						new Decorator(
							ctx => barrel != null && barrel.Distance <= 10 && !barrel.InUse && !Me.Combat && StyxWoW.Me.BagItems.Any(i => i.IsValid && i.Entry == BombItemId),
							ScriptHelpers.CreateInteractWithObject(ctx => barrel, 6)),
						new Decorator(
							ctx => barrel == null,
							new PrioritySelector(
								new Decorator(ctx => StyxWoW.Me.Location.Distance2DSqr((WoWPoint)ctx) <= 3 * 3, new Action(ctx => _barrelLocations.Dequeue())),
								new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS((WoWPoint)ctx)))))));
		}

		[EncounterHandler(17848, "Lieutenant Drake")]
		public Composite LieutenantDrakeEncounter()
		{
			AddAvoidObject(ctx => !Me.IsTank(), 8, o => o.Entry == LieutenantDrakeId && o.ToUnit().HasAura("Whirlwind"));
			return new PrioritySelector();
		}

		#endregion
	}
}