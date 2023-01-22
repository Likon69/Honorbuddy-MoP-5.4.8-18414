using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Frames;
using Styx.Helpers;
using Styx.Patchables;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
using AvoidanceManager = Bots.DungeonBuddy.Avoidance.AvoidanceManager;

namespace Bots.DungeonBuddy.Dungeon_Scripts.Mists_of_Pandaria
{

	#region Normal Difficulty

	public class MogushanPalace : Dungeon
	{
		#region Overrides of Dungeon

		private readonly WoWPoint _gekkanRoomLoc = new WoWPoint(-4398.502, -2607.031, -54.8428);
		private readonly WoWPoint _gekkanShortcutJumpStartLoc = new WoWPoint(-4395.434f, -2557.754f, -28.35415f);
		private readonly WoWPoint _gekkanShortcutActualJumpLoc = new WoWPoint(-4395.556f, -2563.14f, -28.31531f);
		private readonly WaitTimer _ignoreGreenhornTimer = new WaitTimer(TimeSpan.FromSeconds(20));
		private readonly WaitTimer _killGreenhornTimer = new WaitTimer(TimeSpan.FromSeconds(10));
		private bool _takeGekkanShortcut;

		public override uint DungeonId
		{
			get { return 467; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(1400.152, 428.4431, 479.0349); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(-3969.258, -2520.795, 26.80361); }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (unit != null)
					{
						if (unit.Entry == RefereeId)
							return true;
						// remove targets that being kited by group members.
						if (unit.Combat && unit.IsTargetingMyPartyMember)
						{
							var targetOfUnit = unit.CurrentTarget;
							if (targetOfUnit != null && targetOfUnit.Location.DistanceSqr(unit.Location) > 35 * 35)
							{
								return true;
							}
						}
						if (unit.Entry == GlintrokGreenhornId)
						{
							if (_ignoreGreenhornTimer.IsFinished)
							{
								_ignoreGreenhornTimer.Reset();
								_killGreenhornTimer.Reset();
							}
							if (!_killGreenhornTimer.IsFinished && StyxWoW.Me.IsTank())
							{
								return true;
							}
						}
						if (unit.Entry == MuShibaId)
							return true;

						if (unit.Entry == MingTheCunning && unit.CastingSpellId == MagneticFieldId && Me.IsMelee())
							return true;
						// don't pull these NPCs if they're in combat with another NPC.
						if ((unit.Entry == KargeshRibcrusherId || unit.Entry == HarthakStormcallerId) && unit.Combat && Me.Combat &&
							!unit.TaggedByMe)
							return true;
					}
					return false;
				});
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			foreach (var obj in incomingunits)
			{
				var unit = obj as WoWUnit;
				if (unit != null) { }
			}
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var priority in units)
			{
				var unit = priority.Object as WoWUnit;
				if (unit != null)
				{
					if (unit.Entry == GlintrokIronhideId)
						priority.Score += 600;
					if (unit.Entry == GlintrokHexxerId)
						priority.Score += 550;
					if (unit.Entry == GlintrokSkulkerId)
						priority.Score += 500;
					if (unit.Entry == GlintrokOracleId)
						priority.Score += 450;
				}
			}
		}

		bool IsByGekkan(WoWPoint location)
		{
			return location.DistanceSqr(_gekkanRoomLoc) < 50 * 50 && location.Z < -43;
		}

		bool IsByGekkanJumpStart(WoWPoint location)
		{
			return location.Z > -43 && location.DistanceSqr(_gekkanShortcutJumpStartLoc) < 30*30;
		}

		public override MoveResult MoveTo(WoWPoint location)
		{
			if (ElevatorBehavior(location))
				return MoveResult.Moved;

			var combat = Me.IsActuallyInCombat;
			var myLoc = Me.Location;
			if (!_takeGekkanShortcut && IsByGekkan(location) && !IsByGekkan(myLoc) && (!combat ||
				// if in combat and 'to kill' target is at the bottom by Gekkan then take jump instead of running 
				// down the ramp and pulling tons of aggro.
				(!Targeting.Instance.IsEmpty() && IsByGekkan(Targeting.Instance.FirstUnit.Location)))
				// don't jump if there's stuff to kill.
				&& (!Me.IsLeader() || !ScriptHelpers.GetUnfriendlyNpsAtLocation(_gekkanShortcutJumpStartLoc, 20).Any()))
			{
				Logger.Write("Taking shortcut at Gekkan");
				_takeGekkanShortcut = true;
			}
			else if (_takeGekkanShortcut && combat && !Targeting.Instance.IsEmpty() 
				&& !IsByGekkan(Targeting.Instance.FirstUnit.Location))
			{
				Logger.Write("Canceled taking shortcut at Gekkan");
				_takeGekkanShortcut = false;
			}

			return base.MoveTo(location);
		}

		#region Elevator

		private const float ElevatorBottomZ = -40.35011f;
		private const float ElevatorTopZ = 21.66621f;
		private const uint ElevatorId = 212162;
		private readonly WoWPoint _elevatorBottomBoardLoc = new WoWPoint(-4399.304, -2747.819, -39.71338);
		private readonly WoWPoint _elevatorBottomExitLoc = new WoWPoint(-4399.436, -2742.337, -40.023);
		private readonly WoWPoint _elevatorTopBoardLoc = new WoWPoint(-4399.304, -2747.819, 22.30293);
		private readonly WoWPoint _elevatorTopExitLoc = new WoWPoint(-4399.411, -2740.962, 22.32531);

		private Composite CreateFollowerElevatorBehavior()
		{
			return new Decorator(
				ctx => !StyxWoW.Me.IsTank(),
				new Action(
					ctx =>
					{
						var tankI = StyxWoW.Me.GroupInfo.RaidMembers.FirstOrDefault(p => p.HasRole(WoWPartyMember.GroupRole.Tank));

						if (tankI != null)
						{
							var tank = tankI.ToPlayer();
							var myFloorLevel = FloorLevel(Me.Location);
							var tankLoc = tank != null ? tank.Location : tankI.Location3D;
							var tankLevel = FloorLevel(tankLoc);
							var elevatorRestingZ = myFloorLevel == 1 ? ElevatorBottomZ : ElevatorTopZ;
							var elevatorBoardLoc = myFloorLevel == 2 ? _elevatorTopBoardLoc : _elevatorBottomBoardLoc;
							var elevatorWaitLoc = myFloorLevel == 2 ? _elevatorTopExitLoc : _elevatorBottomExitLoc;

							// do we need to get on a lift?
							if ((IsOnLift(tank) || _elevatorBottomBoardLoc.Distance(tankLoc) < 8) && !IsOnLift(Me) && tankLevel == myFloorLevel)
							{
								var ele = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(g => g.Entry == ElevatorId);
								bool elevatorIsReadyToBoard = ele != null && Math.Abs(ele.Z - elevatorRestingZ) <= 0.5f;
								if (elevatorIsReadyToBoard)
								{
									if (Me.Location.DistanceSqr(elevatorBoardLoc) > 1.5 * 1.5)
									{
										Logger.Write("[Elevator Manager] Boarding Elevator");
										Navigator.PlayerMover.MoveTowards(elevatorBoardLoc);
									}
									else
									{
										Logger.Write("[Elevator Manager] Jumping");
										WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
										WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend);
									}
								}
								return RunStatus.Success;
							}
							if (IsOnLift(Me))
							{
								var ele = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(g => g.Entry == ElevatorId);
								bool elevatorIsReadyToBoard = ele != null && Math.Abs(ele.Z - elevatorRestingZ) <= 0.5f;
								// do we need to get off lift?
								if (elevatorIsReadyToBoard && !IsOnLift(tank) && tankLevel == myFloorLevel)
								{
									Logger.Write("[Elevator Manager] Exiting Elevator");
									Navigator.PlayerMover.MoveTowards(elevatorWaitLoc);
								}
								else if (Me.Location.DistanceSqr(elevatorBoardLoc) > 3 * 3)
									Navigator.PlayerMover.MoveTowards(elevatorBoardLoc);
								return RunStatus.Success;
							}
						}
						return RunStatus.Failure;
					}));
		}

		public bool ElevatorBehavior(WoWPoint destination)
		{
			var myloc = StyxWoW.Me.Location;

			var myFloorLevel = FloorLevel(myloc);
			var destinationFloorLevel = FloorLevel(destination);

			if (myFloorLevel == 1 && destinationFloorLevel == 2 && IsLastBossDoorOpen)
			{
				var ele = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(g => g.Entry == ElevatorId);

				var elevatorRestingZ = myFloorLevel == 1 ? ElevatorBottomZ : ElevatorTopZ;
				var elevatorBoardLoc = destinationFloorLevel == 1 ? _elevatorTopBoardLoc : _elevatorBottomBoardLoc;
				var elevatorWaitLoc = destinationFloorLevel == 1 ? _elevatorTopExitLoc : _elevatorBottomExitLoc;
				bool elevatorIsReadyToBoard = ele != null && Math.Abs(ele.Z - elevatorRestingZ) <= 0.5f;
				// move to the lever loc
				if ((ele == null || myloc.DistanceSqr(elevatorBoardLoc) > 20 * 20 ||
					(!elevatorIsReadyToBoard && myloc.DistanceSqr(elevatorWaitLoc) > 4 * 4) &&
					Me.TransportGuid == 0 && !Navigator.AtLocation(elevatorBoardLoc)))
				{
					Logger.Write("[Elevator Manager] Moving To Elevator");
					var moveResult = Navigator.MoveTo(elevatorWaitLoc);
					return moveResult != MoveResult.Failed && moveResult != MoveResult.PathGenerationFailed;
				}
				// get onboard of elevator.
				if (elevatorIsReadyToBoard && myloc.DistanceSqr(elevatorBoardLoc) > 1.5 * 1.5)
				{
					Logger.Write("[Elevator Manager] Boarding Elevator");
					// avoid getting stuck on lever
					Navigator.PlayerMover.MoveTowards(elevatorBoardLoc);
				}
				else if (elevatorIsReadyToBoard && myloc.DistanceSqr(elevatorBoardLoc) <= 1.5 * 1.5 && Me.TransportGuid == 0)
				{
					Logger.Write("[Elevator Manager] Jumping");
					WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
					WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend);
				}
				return true;
			}

			// exit elevator
			var transport = Me.Transport;
			if (transport != null && transport.Entry == ElevatorId)
			{
				Logger.Write("[Elevator Manager] Exiting Elevator");
				var elevatorExitZ = destinationFloorLevel == 1 ? ElevatorBottomZ : ElevatorTopZ;
				var elevatorExitLoc = destinationFloorLevel == 1 ? _elevatorBottomExitLoc : _elevatorTopExitLoc;
				bool elevatorIsReadyToExit = Math.Abs(transport.Z - elevatorExitZ) <= 0.5f;
				if (elevatorIsReadyToExit && myFloorLevel == destinationFloorLevel)
					Navigator.PlayerMover.MoveTowards(elevatorExitLoc);
				return true;
			}
			return false;
		}

		private bool IsOnLift(WoWPlayer player)
		{
			return player != null && player.TransportGuid > 0 && player.Transport.Entry == ElevatorId;
		}

		private int FloorLevel(WoWPoint loc)
		{
			return loc.Z < -10 ? 1 : 2;
		}

		#endregion

		#endregion

		private const uint MuShibaId = 61453;
		private const uint MingTheCunning = 61444;
		private const uint HaiyanTheUnstoppable = 61445;
		private const uint KuaiTheBrute = 61442;
		private const int MagneticFieldId = 120100;
		private const uint GlintrokOracleId = 61339;
		private const uint GlintrokHexxerId = 61340;
		private const uint GlintrokIronhideId = 61337;
		private const uint GlintrokSkulkerId = 61338;
		private const uint GlintrokGreenhornId = 61247;
		private const uint HarthakStormcallerId = 61946;
		private const uint KargeshRibcrusherId = 61947;
		private const uint RefereeId = 61478;

		private const uint LightningStormId = 120562;

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0)]
		public Composite RootEncounter()
		{
			AddAvoidObject(ctx => true, 5, LightningStormId);
			return new PrioritySelector(
				CreateFollowerElevatorBehavior(),
				new ActionRunCoroutine(ctx => JumpAtGekkan()),
				ScriptHelpers.CreateRunToTankIfAggroed());
		}

		private async Task DoGekkanJump()
		{
			Stopwatch timer = Stopwatch.StartNew();
			while (Me.Z > -43 && timer.ElapsedMilliseconds < 20000)
			{
				await ScriptHelpers.ForceJump(_gekkanShortcutJumpStartLoc, _gekkanShortcutActualJumpLoc, false);
				await Coroutine.Sleep(1000);

				Stopwatch innerTimer = Stopwatch.StartNew();
				WoWPoint prevLoc = (WoWMovement.ActiveMover ?? StyxWoW.Me).Location;
				await Coroutine.Wait(10000, () =>
				{
					// Break if we fell
					if (Me.Z <= -43)
						return true;

					if (innerTimer.ElapsedMilliseconds > 500)
					{
						WoWPoint myLoc = (WoWMovement.ActiveMover ?? StyxWoW.Me).Location;

						// Break if we haven't changed position for more than half a second (stuck on rail)
						if (myLoc == prevLoc)
							return true;

						prevLoc = myLoc;
						innerTimer.Restart();
					}

					return false;
				});
			}

			WoWMovement.MoveStop(WoWMovement.MovementDirection.Forward);
		}

		public async Task<bool> JumpAtGekkan()
		{
			if (!_takeGekkanShortcut)
				return false;
			var myLoc = Me.Location;

			if (myLoc.Z <= -43 || 
				// if we get in combat with something up top then stop and kill it
				(Me.IsActuallyInCombat && !Targeting.Instance.IsEmpty() 
				&& !IsByGekkan(Targeting.Instance.FirstUnit.Location)))
			{
				_takeGekkanShortcut = false;
				return false;
			}

			if (Me.Location.DistanceSqr(_gekkanShortcutJumpStartLoc) > 6*6)
			{
				await CommonCoroutines.MoveTo(_gekkanShortcutJumpStartLoc);
				return true;
			}

			// dismiss any pets.
			if (await WoWPetControl.DismissPet())
				return true;

			// jump.
			await DoGekkanJump();
			return true;
		}
	
		[EncounterHandler(64432, "Sinan the Dreamer", Mode = CallBehaviorMode.Proximity)]
		public Composite QuestHandler()
		{
			const int aNewLessonForTheMasterQuestId = 31360;

			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				new Decorator(
					ctx =>
						!Me.Combat && !ScriptHelpers.WillPullAggroAtLocation(unit.Location) &&
						unit.QuestGiverStatus == QuestGiverStatus.Available &&
						Me.QuestLog.GetCompletedQuests().All(q => q != aNewLessonForTheMasterQuestId) &&
						!Me.QuestLog.ContainsQuest(aNewLessonForTheMasterQuestId),
					ScriptHelpers.CreatePickupQuest(ctx => unit, aNewLessonForTheMasterQuestId)),
				new Decorator(
					ctx =>
						!Me.Combat && !ScriptHelpers.WillPullAggroAtLocation(unit.Location) && unit.QuestGiverStatus == QuestGiverStatus.TurnIn,
					ScriptHelpers.CreateTurninQuest(ctx => unit)));
		}

		private const uint XintheWeaponmasterId = 61398;

		[EncounterHandler(61398, "Xin the Weaponmaster", Mode = CallBehaviorMode.Proximity, BossRange = 100)]
		public Composite XintheWeaponmasterEncounter()
		{
			var spearStreamStart1 = new WoWPoint(-4653.144, -2571.559, 27.28992);
			var spearStreamEnd1 = new WoWPoint(-4635.11, -2642.283, 27.75975);

			var spearStreamStart2 = new WoWPoint(-4612.325, -2655.249, 27.23762);
			var spearStreamEnd2 = new WoWPoint(-4594.599, -2584.786, 27.18633);

			var spearStreamStart3 = new WoWPoint(-4613.093, -2655.249, 27.23765);
			var spearStreamEnd3 = new WoWPoint(-4630.541, -2584.572, 27.10564);

			var spearStreamStart4 = new WoWPoint(-4654.006, -2571.327, 27.28995);
			var spearStreamEnd4 = new WoWPoint(-4671.301, -2642.371, 27.81245);

			const uint animatedAxeId = 61451;
			const uint ringOfFireId = 61499;
			const int groundSlamId = 119684;
			WoWUnit boss = null;

			AddAvoidObject(ctx => true, o => o.ToUnit().HasAura("Ring of Fire") ? 2 : 8, ringOfFireId);

			AddAvoidObject(ctx => true, 7, o => o.Entry == XintheWeaponmasterId && o.ToUnit().CastingSpellId == groundSlamId,
				o => o.Location.RayCast(o.Rotation, 7));

			// just ignore these axes.. they don't hurt enough to make it worth running all over the place.
			//AddAvoidObject(
			//	ctx => true,
			//	o => Me.IsRange() && Me.IsMoving ? 12 : 8,
			//	u => u.Entry == animatedAxe && ((WoWUnit) u).HasAura("Whirlwind") && u.DistanceSqr <= 20*20);

			AddAvoidLocation(
				ctx =>
					boss != null && boss.IsValid && boss.HealthPercent <= 65 && boss.IsAlive && !Me.IsMoving,
				4,
				loc => (WoWPoint)loc,
				() =>
					new List<object>
					{
						Me.Location.GetNearestPointOnSegment(spearStreamStart1, spearStreamEnd1),
						Me.Location.GetNearestPointOnSegment(spearStreamStart2, spearStreamEnd2),
						Me.Location.GetNearestPointOnSegment(spearStreamStart3, spearStreamEnd3),
						Me.Location.GetNearestPointOnSegment(spearStreamStart4, spearStreamEnd4)
					});

			var tankLoc = new WoWPoint(-4584.406, -2613.636, 22.33785);

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				new Decorator(
					ctx => boss.Combat,
					new PrioritySelector(
						new Decorator(ctx => Me.IsTank() && Me.HealthPercent > 60, ScriptHelpers.CreateTankUnitAtLocation(ctx => tankLoc, 10)))));
		}

		private const uint DoodadPaRoyalDoor001Id = 213593;

		bool IsLastBossDoorOpen
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWGameObject>()
					.Any(g => g.Entry == DoodadPaRoyalDoor001Id && ((WoWDoor) g.SubObj).IsOpen);
			}
		}

		#region Trial of the King

		[EncounterHandler(61442, "Kuai the Brute", Mode = CallBehaviorMode.Proximity)]
		public Composite KuaitheBruteEncounter()
		{
			const int shockWave = 119922;
			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				new Decorator(
					ctx => boss.IsNeutral && ScriptHelpers.IsBossAlive("Kuai the Brute"),
					new Action(ctx => ScriptHelpers.MarkBossAsDead("Kuai the Brute", "He got his ass whooped"))),
				new Decorator(
					ctx => boss.Combat,
					new PrioritySelector(
						ScriptHelpers.CreateAvoidUnitAnglesBehavior(
							ctx => Me.IsTank() && boss.CastingSpellId == shockWave || !Me.IsTank() && !boss.IsMoving,
							ctx => boss,
							new ScriptHelpers.AngleSpan(0, 180)))));
		}

		[EncounterHandler(61444, "Ming the Cunning", Mode = CallBehaviorMode.Proximity)]
		public Composite MingtheCunningEncounter()
		{
			const uint WhilringDervishId = 61626;


			AddAvoidObject(ctx => true, 20, u => u.Entry == MingTheCunning && ((WoWUnit)u).CastingSpellId == MagneticFieldId);
			AddAvoidObject(ctx => true, 15, WhilringDervishId);

			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				new Decorator(
					ctx => boss.IsNeutral && ScriptHelpers.IsBossAlive("Ming the Cunning"),
					new Action(ctx => ScriptHelpers.MarkBossAsDead("Ming the Cunning", "He got his ass whooped"))),
				new Decorator(ctx => boss.Combat, new PrioritySelector()));
		}

		[EncounterHandler(61445, "Haiyan the Unstoppable", Mode = CallBehaviorMode.Proximity)]
		public Composite HaiyantheUnstoppableEncounter()
		{
			const uint meteorSpellId = 120195;

			AddAvoidObject(ctx => true, 6, u => u is WoWPlayer && ((WoWPlayer)u).HasAura("Conflagrate") && (u).Guid != Me.Guid);

			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				new Decorator(
					ctx => boss.IsNeutral && ScriptHelpers.IsBossAlive("Haiyan the Unstoppable"),
					new Action(ctx => ScriptHelpers.MarkBossAsDead("Haiyan the Unstoppable", "He got his ass whooped"))),
				new Decorator(
					ctx => boss.Combat,
					new PrioritySelector(
						new Decorator(
							ctx => boss.CastingSpellId == meteorSpellId && boss.CurrentTargetGuid != 0,
							new Action(
								ctx =>
								{
									var tank = ScriptHelpers.Tank;
									var bossTarget = boss.CurrentTarget.ToPlayer();
									if (bossTarget != null)
									{ // move to the tank if meteor is being casted on me.
										if (bossTarget == Me && tank != null && !tank.IsMe && tank.Distance > 10)
										{
											return Navigator.GetRunStatusFromMoveResult(Navigator.MoveTo(tank.Location));
										}
										if (bossTarget != Me && bossTarget.Distance > 10)
										{
											return Navigator.GetRunStatusFromMoveResult(Navigator.MoveTo(bossTarget.Location));
										}
									}
									return RunStatus.Failure;
								})))));
		}

		[ObjectHandler(214520, "Legacy of the Clan Leaders", ObjectRange = 100)]
		[ObjectHandler(214521, "Legacy of the Clan Leaders Heroic", ObjectRange = 100)]
		public Composite LegacyoftheClanLeadersHandler()
		{
			WoWGameObject chest = null;
			return new PrioritySelector(ctx => chest = ctx as WoWGameObject, ScriptHelpers.CreateLootChest(ctx => chest));
		}

		#endregion

		#region Gekkan

		[ObjectHandler(214667, "Ancient Mogu Vault")]
		public Composite AncientMoguVaultHandler()
		{
			const uint ancientMoguKey = 87806;

			WoWGameObject chest = null;
			return new PrioritySelector(
				ctx => chest = ctx as WoWGameObject,
				new Decorator(
					ctx => chest.CanLoot && Me.BagItems.Any(i => i.Entry == ancientMoguKey),
					ScriptHelpers.CreateInteractWithObject(ctx => chest)));
		}

		[EncounterHandler(61243, "Gekkan")]
		public Composite GekkanEncounter()
		{
			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit);
		}

		[EncounterHandler(61337, "Glintrok Ironhide")]
		public Composite GlintrokIronhideEncounter()
		{
			const int ironProtector = 118958;
			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit, ScriptHelpers.CreateInterruptCast(ctx => boss, ironProtector));
		}


		[EncounterHandler(61340, "Glintrok Hexxer")]
		public Composite GlintrokHexxerEncounter()
		{
			const int hexOfLethargy = 118903;
			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateInterruptCast(ctx => boss, hexOfLethargy),
				ScriptHelpers.CreateDispelGroup("Hex of Lethargy", ScriptHelpers.PartyDispelType.Magic));
		}

		[EncounterHandler(61339, "Glintrok Oracle")]
		public Composite GlintrokOracleEncounter()
		{
			const int cleansingFlame = 118940;
			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit, ScriptHelpers.CreateInterruptCast(ctx => boss, cleansingFlame));
		}

		[ObjectHandler(214795, "Ancient Mogu Treasure", ObjectRange = 60)]
		[ObjectHandler(214794, "Ancient Mogu Treasure Heroic", ObjectRange = 60)]
		public Composite AncientMoguTreasureHandler()
		{
			WoWGameObject chest = null;
			return new PrioritySelector(ctx => chest = ctx as WoWGameObject, ScriptHelpers.CreateLootChest(ctx => chest));
		}

		#endregion
	}

	#endregion

	#region Heroic Difficulty

	public class MogushanPalaceHeroic : MogushanPalace
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 519; }
		}

		#endregion
	}

	#endregion
}