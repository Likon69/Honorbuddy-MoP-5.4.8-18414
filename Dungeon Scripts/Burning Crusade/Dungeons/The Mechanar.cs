using System;
using System.Collections.Generic;
using System.Linq;
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
	public class TheMechanar : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 172; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(2860.672, 1544.55, 252.1591); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(-39.46347, 0.2592911, -1.812383); }
		}

		public override bool IsFlyingCorpseRun
		{
			get { return true; }
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
						if (unit.Entry == RagingFlamesId)
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
						if (unit.Entry == NetherWraithId)
							priority.Score += 500;

						if (StyxWoW.Me.IsRange() && unit.Entry == MechanarTinkererId)
							priority.Score += 500;
					}
				}
			}
		}

		#endregion

		#region Root

		private const uint RagingFlamesId = 20481;
		private const uint NetherWraithId = 21062;
		private const uint MechanarTinkererId = 19716;
		private const uint BloodwarderSlayerId = 19167;
		private const uint LostTreasureQuestId = 29659;
		private const uint WithGreatPowerComesGreatResponsibilityQuestId = 29657;
		private const uint TheCalculatorQuestId = 29658;

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0, "Root behavior")]
		public Composite RootBehavior()
		{
			PlayerQuest quest = null;
			return
				new PrioritySelector(
					new Decorator(
						ctx => (quest = Me.QuestLog.GetQuestById(WithGreatPowerComesGreatResponsibilityQuestId)) != null && quest.IsCompleted,
						ScriptHelpers.CreateCompletePopupQuest(WithGreatPowerComesGreatResponsibilityQuestId)),
					new Decorator(
						ctx => (quest = Me.QuestLog.GetQuestById(LostTreasureQuestId)) != null && quest.IsCompleted,
						ScriptHelpers.CreateCompletePopupQuest(LostTreasureQuestId)),
					new Decorator(
						ctx => (quest = Me.QuestLog.GetQuestById(TheCalculatorQuestId)) != null && quest.IsCompleted,
						ScriptHelpers.CreateCompletePopupQuest(TheCalculatorQuestId)),
					CreateFollowerElevatorBehavior());
		}

		[ObjectHandler(184228, "Instance_Portal_Difficulty_1", ObjectRange = 100)]
		public Composite EntranceTrashHandler()
		{
			var patroller1Loc = new WoWPoint(30.21965, 2.915361, -0.0006945329);
			var trash1Loc = new WoWPoint(22.4355, -20.95673, 5.289912);
			var trash2Loc = new WoWPoint(22.14023, 20.44575, -0.0001794659);

			var tankLoc1 = new WoWPoint(-14.75135, 0.7233489, -1.8124);
			var tankLoc2 = new WoWPoint(-27.63513, 0.1582904, -1.812397);
			WoWUnit patroller1 = null, trash1 = null, trash2 = null;
			AddAvoidObject(ctx => Me.IsFollower(), 8, o => o.Entry == BloodwarderSlayerId && o.ToUnit().HasAura("Whirlwind"));

			return new PrioritySelector(
				ctx =>
				{
					patroller1 = ScriptHelpers.GetUnfriendlyNpsAtLocation(patroller1Loc, 2, u => u.Entry == 19166).FirstOrDefault();
					trash1 = ScriptHelpers.GetUnfriendlyNpsAtLocation(trash1Loc, 10, u => u.Entry != 19166).FirstOrDefault();
					trash2 = ScriptHelpers.GetUnfriendlyNpsAtLocation(trash2Loc, 10, u => u.Entry != 19166).FirstOrDefault();
					return ctx;
				},
				// pull the 1st patroller when he's alone.
				ScriptHelpers.CreatePullNpcToLocation(
					ctx => patroller1 != null,
					ctx => ScriptHelpers.GetUnfriendlyNpsAtLocation(patroller1Loc, 25, u => u.Entry == 19166).Count() == 1,
					ctx => patroller1,
					ctx => tankLoc2,
					ctx => tankLoc1,
					10),
				// pull trash group1 if no pats are close.
				ScriptHelpers.CreatePullNpcToLocation(
					ctx => trash1 != null && !ScriptHelpers.GetUnfriendlyNpsAtLocation(trash1Loc, 20, u => u.Entry == 19166).Any(),
					ctx => !ScriptHelpers.GetUnfriendlyNpsAtLocation(trash1Loc, 20, u => u.Entry == 19166).Any(),
					ctx => trash1,
					ctx => tankLoc1,
					4),
				// pull trash group2 if no pats are close.
				ScriptHelpers.CreatePullNpcToLocation(
					ctx => trash2 != null, ctx => !ScriptHelpers.GetUnfriendlyNpsAtLocation(trash2Loc, 20, u => u.Entry == 19166).Any(), ctx => trash2, ctx => tankLoc1, 4));
		}

		#endregion

		#region Mechano-Lord Capacitus

		private const uint NetherChargeId = 20405;

		[EncounterHandler(19219, "Mechano-Lord Capacitus")]
		public Composite MechanoLordCapacitusEncounter()
		{
			WoWUnit boss = null;
			AddAvoidObject(ctx => Me.IsFollower() && !Me.IsCasting, 8, NetherChargeId);
			AddAvoidObject(
				ctx => Me.IsFollower(),
				() => ScriptHelpers.Tank.Location,
				35,
				12,
				o =>
				o is WoWPlayer &&
				(o.ToPlayer().HasAura("Positive Charge") && Me.HasAura("Negative Charge") || o.ToPlayer().HasAura("Negative Charge") && Me.HasAura("Positive Charge")));

			return new PrioritySelector(ctx => boss = ctx as WoWUnit);
		}

		#endregion

		[EncounterHandler(19218, "Gatewatcher Gyro-Kill")]
		[EncounterHandler(19710, "Gatewatcher Iron-Hand")]
		public Composite GatewatcherIronHandEncounter()
		{
			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(
					ctx => boss.Distance < 10 && !boss.IsMoving && !Me.IsCasting && boss.CurrentTargetGuid != Me.Guid, ctx => boss, new ScriptHelpers.AngleSpan(0, 180)),
				ScriptHelpers.CreateTankFaceAwayGroupUnit(10));
		}


		[EncounterHandler(19221, "Nethermancer Sepethrea", Mode = CallBehaviorMode.Proximity, BossRange = 60)]
		public Composite NethermancerSepethreaCapacitusEncounter()
		{
			WoWUnit boss = null;
			const uint RagingFlamesId = 20481;
			const uint RagingFlameSpellId = 35278;
			var InfernoIds = new[] { 35268, 39346 };

			AddAvoidObject(ctx => !Me.IsCasting, o => InfernoIds.Contains(o.ToUnit().CastingSpellId) || Me.IsRange() && Me.IsMoving ? 10 : 6, o => o.Entry == RagingFlamesId && o.ToUnit().IsAlive);
			AddAvoidObject(ctx => true, 2, RagingFlameSpellId);

			WoWUnit destroyer1 = null, destroyer2 = null, trash1 = null, trash2 = null;
			var destroyer1Loc = new WoWPoint(291.155, 34.63794, 25.38616);
			var destroyer2Loc = new WoWPoint(296.6584, -17.13713, 25.3822);
			var trash1Loc = new WoWPoint(309.3312, 15.13393, 25.38623);
			var trash2Loc = new WoWPoint(272.1549, -24.6583, 26.3284);

			var tankloc1 = new WoWPoint(276.8195, 49.4281, 25.38621);
			var tankloc2 = new WoWPoint(275.7698, 26.60911, 25.38618);

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				new Decorator(
					ctx => !boss.Combat,
					new PrioritySelector(
						ctx =>
						{
							destroyer1 = ScriptHelpers.GetUnfriendlyNpsAtLocation(destroyer1Loc, 20, u => u.Entry == 19735).FirstOrDefault();
							destroyer2 = ScriptHelpers.GetUnfriendlyNpsAtLocation(destroyer2Loc, 20, u => u.Entry == 19735).FirstOrDefault();
							trash1 = ScriptHelpers.GetUnfriendlyNpsAtLocation(trash1Loc, 8, u => true).FirstOrDefault();
							trash2 = ScriptHelpers.GetUnfriendlyNpsAtLocation(trash2Loc, 8, u => true).FirstOrDefault();
							return ctx;
						},
						ScriptHelpers.CreatePullNpcToLocation(ctx => destroyer1 != null, ctx => destroyer1, ctx => tankloc1, 10),
						ScriptHelpers.CreatePullNpcToLocation(ctx => trash1 != null, ctx => trash1, ctx => tankloc1, 3),
						ScriptHelpers.CreatePullNpcToLocation(ctx => destroyer2 != null, ctx => destroyer2, ctx => tankloc2, 10),
						ScriptHelpers.CreatePullNpcToLocation(ctx => trash2 != null, ctx => trash2, ctx => tankloc2, 3))),
				new Decorator(
					ctx => boss.Combat,
					new PrioritySelector()));
		}

		[EncounterHandler(19220, "Pathaleon the Calculator")]
		public Composite PathaleonTheCalculatorEncounter()
		{
			const uint pathaleonTheCalculatorId = 19220;
			WoWUnit boss = null;
			AddAvoidObject(
				ctx => Me.IsRange() && !Me.IsCasting,
				25,
				o => o.Entry == pathaleonTheCalculatorId && o.ToUnit().IsAlive && o.ToUnit().CurrentTargetGuid != Me.Guid && !o.ToUnit().IsMoving);
			return new PrioritySelector(ctx => boss = ctx as WoWUnit);
		}


		[ObjectHandler(184465, "Cache of the Legion", ObjectRange = 20)]
		public Composite CacheoftheLegionHandler()
		{
			WoWGameObject chest = null;
			return new PrioritySelector(
				ctx => chest = ctx as WoWGameObject,
				new Decorator(ctx => !chest.InUse && !Blacklist.Contains(chest, BlacklistFlags.Loot) && !chest.Locked && !Me.Combat,
					new PrioritySelector(
						new Decorator(ctx => !chest.WithinInteractRange, new Action(ctx => Navigator.MoveTo(chest.Location))),
						new Sequence(
							new DecoratorContinue(ctx => Me.IsMoving, new Action(ctx => WoWMovement.MoveStop())),
							new WaitContinue(2, ctx => !Me.IsMoving, new ActionAlwaysSucceed()),
							new Action(ctx => chest.Interact()),
							new WaitContinue(3, ctx => false, new ActionAlwaysSucceed()),
							new Action(ctx => LootFrame.Instance.LootAll()),
							new Action(ctx => Blacklist.Add(chest, BlacklistFlags.Loot, TimeSpan.FromMinutes(10)))))));
		}

				#region Elevator

		private const float ElevatorBottomZ = 0;
		private const float ElevatorTopZ = 25.43577f;
		private const uint ElevatorId = 183788;
		private readonly WoWPoint _elevatorBottomBoardLoc = new WoWPoint(257.019, 52.3995, 0.2461777);
		private readonly WoWPoint _elevatorBottomExitLoc = new WoWPoint(248.6114, 52.36416, 0.2290135);

		private readonly WoWPoint _elevatorTopBoardLoc = new WoWPoint(257.019, 52.39949, 25.67912);
		private readonly WoWPoint _elevatorTopExitLoc = new WoWPoint(265.7578, 52.01247, 25.64683);

		private Composite CreateFollowerElevatorBehavior()
		{
			return new Decorator(
				ctx => !StyxWoW.Me.IsTank(),
				new Action(
					ctx =>
					{
						var tank = ScriptHelpers.GroupMembers.FirstOrDefault(g => g.IsLeader());

						if (tank != null && tank.Player != null)
						{
							var myFloorLevel = FloorLevel(Me.Location);
							var tankLoc = tank.Location;
							var tankLevel = FloorLevel(tankLoc);
							var elevatorRestingZ = myFloorLevel == 1 ? ElevatorBottomZ : ElevatorTopZ;
							var elevatorBoardLoc = myFloorLevel == 2 ? _elevatorTopBoardLoc : _elevatorBottomBoardLoc;
							var elevatorWaitLoc = myFloorLevel == 2 ? _elevatorTopExitLoc : _elevatorBottomExitLoc;

							// do we need to get on a lift?
							if ((IsOnLift(tank.Player) || _elevatorBottomBoardLoc.Distance(tankLoc) < 8) && !IsOnLift(Me) && tankLevel == myFloorLevel)
							{
								var ele = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(g => g.Entry == ElevatorId);
								bool elevatorIsReadyToBoard = ele != null && Math.Abs(ele.Z - elevatorRestingZ) <= 0.5f;
								if (elevatorIsReadyToBoard)
								{
									if (Me.Location.DistanceSqr(elevatorBoardLoc) > 3*3)
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
								if (elevatorIsReadyToBoard && !IsOnLift(tank.Player) && tankLevel == myFloorLevel)
								{
									Logger.Write("[Elevator Manager] Exiting Elevator");
									Navigator.PlayerMover.MoveTowards(elevatorWaitLoc);
								}
								else if (Me.Location.DistanceSqr(elevatorBoardLoc) > 3*3)
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

			if (myFloorLevel != destinationFloorLevel )
			{
				var ele = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(g => g.Entry == ElevatorId);

				var elevatorRestingZ = myFloorLevel == 1 ? ElevatorBottomZ : ElevatorTopZ;
				var elevatorBoardLoc = destinationFloorLevel == 1 ? _elevatorTopBoardLoc : _elevatorBottomBoardLoc;
				var elevatorWaitLoc = destinationFloorLevel == 1 ? _elevatorTopExitLoc : _elevatorBottomExitLoc;
				bool elevatorIsReadyToBoard = ele != null && Math.Abs(ele.Z - elevatorRestingZ) <= 0.5f;

				// move to the
				if ((ele == null || myloc.DistanceSqr(elevatorBoardLoc) > 20*20 ||
					!elevatorIsReadyToBoard && myloc.DistanceSqr(elevatorWaitLoc) > 4*4) &&
					Me.TransportGuid == 0)
				{
					//  If we fall off elevator we can't navagate back to the 'elevatorWaitLoc' because of a too high of a step so we just wait
					if (Me.Location.Distance(elevatorBoardLoc) < 8 && Me.Z < -1)
						return true;
					Logger.Write("[Elevator Manager] Moving To Elevator");
					var moveResult = Navigator.MoveTo(elevatorWaitLoc);					
					return moveResult != MoveResult.Failed && moveResult != MoveResult.PathGenerationFailed;
				}
				// get onboard of elevator.
				if (elevatorIsReadyToBoard && myloc.DistanceSqr(elevatorBoardLoc) > 3*3)
				{
					Logger.Write("[Elevator Manager] Boarding Elevator");
					// avoid getting stuck on lever
					Navigator.PlayerMover.MoveTowards(elevatorBoardLoc);
				}
				else if (elevatorIsReadyToBoard && myloc.DistanceSqr(elevatorBoardLoc) <= 3*3 && Me.TransportGuid == 0)
				{
					Logger.Write("[Elevator Manager] Jumping");
					WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
					WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend);
				}
				return true;
			}

			// exit elevator
			var transport = Me.TransportGuid > 0 ? Me.Transport : null;
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
			return loc.Z < 20 ? 1 : 2;
		}

		public override MoveResult MoveTo(WoWPoint location)
		{
			return ElevatorBehavior(location) ? MoveResult.Moved : MoveResult.Failed;
		}

		#endregion

		/*
		 Transport Type: Elevator
		 Tile: TempestKeepFactory
		 GameObject Id: 183788
		 Location when resting at the bottom: <0,0,0>
		 Location when resting at the top: <8.742277E-13, -1E-05, 25.43577>
		 Player wait/exit spot at bottom: <248.6114, 52.36416, 0.2290135>
		 Player wait/exit spot at top: <265.7578, 52.01247, 25.64683>
		 Player board spot at bottom. <257.019, 52.3995, 0.2461777>
		 Player board spot at top. <257.019, 52.39949, 25.67912>
		 */
	}
}