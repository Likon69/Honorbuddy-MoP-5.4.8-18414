using System; 
 using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.Helpers;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;

using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Enums;
using Bots.DungeonBuddy.Helpers;
using Styx.Common;
using Styx.CommonBot.Coroutines;

namespace Bots.DungeonBuddy.Dungeon_Scripts.Cataclysm
{
	
    public class ThroneOfTheTides : Dungeon
    {
        #region Overrides of Dungeon

        /// <summary>
        ///     The Map Id of this dungeon. This is the unique id for dungeons thats used to determine which dungeon, the script belongs to
        /// </summary>
        /// <value> The map identifier. </value>
        public override uint DungeonId
        {
            get { return 302; }
        }

        public override WoWPoint Entrance
        {
            get { return new WoWPoint(-5583.308f, 5400.118f, -1797.845f); }
        }

        public override WoWPoint ExitLocation
        {
            get { return new WoWPoint(-677.3771f, 802.6225f, 249.002f); }
        }

        public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
        {
            foreach (var unit in incomingunits.OfType<WoWUnit>())
            {
                var target = unit.CurrentTarget;
                if (target != null && target.Entry == NeptulonId)
                    outgoingunits.Add(unit);
                else if (unit.Entry == SapperId || unit.Entry == OzumatId || unit.Entry == MindLasherId || unit is WoWPlayer)
                    outgoingunits.Add(unit);

              else if (unit.Entry == NazjarTempestWitchId && StyxWoW.Me.IsDps())
					outgoingunits.Add(unit);

                else if (unit.Entry == UnstableCorruptionId && unit.Combat)
                    outgoingunits.Add(unit);

                else if (unit.Entry == TaintedSentryId && StyxWoW.Me.IsTank())
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
                        // Naz'jar Soldier. Can't be pulled due to LOS, but the bot will still try.
                        if (unit.Entry == 45620)
                            return true;

                        // don't dps ozumat until we have this aura.
                        if (unit.Entry == OzumatId && !StyxWoW.Me.HasAura("Tidal Surge"))
                            return true;

                        // Faceless watcher casting Ground Pound. Melee should stay away
                        if (unit.CastingSpellId == GroundPoundSpellId && !Me.IsTank() && unit.DistanceSqr < 10 * 10)
                            return true;

                        // For Lady Naz'jar encounter. Lady Naz'jar becomes immune while casting Waterspout(40586)
                        if (unit.Entry == LadyNazjarId && unit.HasAura("Waterspout"))
                            return true;

                        // For Mindbender encounter. Mindbender becomes immune to magic and heals itself on damage. We should stop dpsing
                        if (unit.HasAura("Absorb Magic"))
                            return true;

                        if (unit.Entry == UnstableCorruptionId && (!unit.Combat || ShouldMoveForwardInGauntlet))
                            return true;
                    }
                    return false;
                });
        }

        private readonly WaitTimer _gaunletLongTimer = new WaitTimer(TimeSpan.FromSeconds(20));
        private readonly WaitTimer _gaunletShortTimer = new WaitTimer(TimeSpan.FromSeconds(10));

        private bool ShouldMoveForwardInGauntlet
        {
            get
            {
                if (_gaunletLongTimer.IsFinished)
                {
                    _gaunletLongTimer.Reset();
                    _gaunletShortTimer.Reset();
                }
                return !_gaunletShortTimer.IsFinished && StyxWoW.Me.IsTank();
            }
        }

        public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var t in units)
			{
				var prioObject = t.Object;

				//We should prio Faceless Sappers for Ozumat fight
				if ((prioObject.Entry == SapperId || prioObject.Entry == MindLasherId || NazjarSpiritmenderIds.Contains(prioObject.Entry)) && StyxWoW.Me.IsDps()) // 
					t.Score += 500;

				if (prioObject.Entry == OzumatId)
					t.Score += 500;
				if (prioObject.Entry == UnyieldingBehemothId && StyxWoW.Me.IsTank())
					t.Score += 600;

				if (prioObject.Entry == NazjarTempestWitchId && StyxWoW.Me.IsDps())
					t.Score += 500;

				if (prioObject.Entry == TaintedSentryId && StyxWoW.Me.IsTank() && Targeting.Instance.FirstUnit == null)
					t.Score += 200;
			}
		}

        public override void IncludeLootTargetsFilter(List<WoWObject> incomingObjects, HashSet<WoWObject> outgoingunits)
        {
            foreach (var obj in incomingObjects)
            {
                var gObj = obj as WoWGameObject;
                if (gObj != null && (gObj.Entry == GameObjectId_NeptulonsCache || gObj.Entry == GameObjectId_NeptulonsCache_Timewalking) 
                    && gObj.CanUse() && DungeonBuddySettings.Instance.LootMode != LootMode.Off)
                {
                    outgoingunits.Add(gObj);
                }
            }
        }
        #endregion

        #region Encounter Handlers

        private const uint FacelessWatcherId = 40936;
        private const uint GroundPoundSpellId = 76590;
        private const uint UnyieldingBehemothId = 44648;
        private const uint LadyNazjarId = 40586;
        private const uint SapperId = 44752;
        private const uint OzumatId = 44566;
        private const uint MindLasherId = 44715;
        private const uint NazjarTempestWitchId = 44404;
        private readonly uint[] _nazjarSpiritmenderIds = new uint[] { 41096, 41139 };
        private readonly uint[] _nazjarInvaderIds = new uint[] { 39616, 40584 };
		private readonly uint[] NazjarSpiritmenderIds = new uint[] { 41096, 41139 };
		private readonly uint[] NazjarInvaderIds = new uint[] { 39616, 40584 };
        private const uint UnstableCorruptionId = 40923;
        private const uint TaintedSentryId = 40925;
        private const uint NoxiousMireId = 41201;
        private const uint CommanderUlthokId = 40765;
        private const uint DarkFictureSpellId = 76047;

        private readonly uint[] _blightOfOzumatIds = new uint[] { 44801, 44834 };
        private readonly WoWPoint _ozumatDpsLoc = new WoWPoint(-113.4719f, 957.8307f, 230.7365f);
        private readonly WaitTimer _teleportTimer = new WaitTimer(TimeSpan.FromSeconds(10));
        private WoWUnit _erunakStonespeaker;

        private WoWUnit _mindbender;
        private WoWUnit _neptulon;
        private WoWUnit _ozumat;

        private LocalPlayer Me
        {
            get { return StyxWoW.Me; }
        }

        /// <summary>
        ///     Using 0 as BossEntry will make that composite the main logic for the dungeon and it will be called in every tick You can only have one main logic for a dungeon The context of the main composite is all units around as List <WoWUnit />
        /// </summary>
        /// <returns> </returns>
        [EncounterHandler(0)]
        public Composite RootLogic()
        {
            var cancelCinematicTimer = new WaitTimer(TimeSpan.FromMinutes(1));

            AddAvoidObject(ctx => true, 5f, NoxiousMireId);
            // clump of coral in middle of room at 2nd boss that we need to avoid.
            AddAvoidObject(ctx => ScriptHelpers.IsBossAlive("Lady Naz'jar") && StyxWoW.Me.Z > 500, 10, 205542);
            AddAvoidObject(ctx => !Me.IsTank(), 7, u => u.Entry == FacelessWatcherId && u.ToUnit().CastingSpellId == GroundPoundSpellId);


            return new PrioritySelector(
                new Decorator(ctx => Targeting.Instance.IsEmpty() && cancelCinematicTimer.IsFinished && InCinematic,
                    new Action(ctx =>
                                   {
                                       StopCinematic();
                                       cancelCinematicTimer.Reset();
                                   })),
                CreateFollowerElevatorBehavior());
        }

        private bool InCinematic
        {
            get { return Lua.GetReturnVal<bool>("return InCinematic()", 0); }
        }

        private void StopCinematic()
        {
            Lua.DoString("StopCinematic()");
        }

        [EncounterHandler(39959, "Lady Naz'jar", Mode = CallBehaviorMode.Proximity)]
        public Composite LadyNazjarMurlocEncounter()
       {
			WoWUnit boss = null;
			WoWPoint moveTo = WoWPoint.Zero;
			var leashPoint = new WoWPoint(-20.19097, 802.0608, 807.4537);
			var moveForwardTimer = new WaitTimer(TimeSpan.FromSeconds(20));

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				new Decorator(
					ctx => boss.Location.Distance(leashPoint) < 3 && Me.IsTank() && moveForwardTimer.IsFinished,
					new Sequence(
						ctx =>
						moveTo =
						ObjectManager.GetObjectsOfType<WoWUnit>()
									 .Where(u => (NazjarInvaderIds.Contains(u.Entry) || NazjarSpiritmenderIds.Contains(u.Entry)) && u.IsAlive && u.X < -15)
									 .Select(u => u.Location)
									 .OrderByDescending(l => l.DistanceSqr(Me.Location))
									 .FirstOrDefault(),
						ScriptHelpers.CreateMoveToContinue(ctx => moveTo != WoWPoint.Zero ? moveTo : boss.Location),
						new Action(ctx => moveForwardTimer.Reset()))));
		}

        [EncounterHandler(40586, "Lady Naz'jar", Mode = CallBehaviorMode.Proximity)]
        public Composite LadyNazjarEncounter()
        {
            WoWUnit boss = null;
            var fungalSpores = new uint[] { 76001, 91470 };
            const uint geyser = 40597;

            AddAvoidObject(ctx => true, 5, fungalSpores);
            AddAvoidObject(ctx => true, 5, geyser);
            AddAvoidObject(ctx => true, 6, o => o.Entry == LadyNazjarId && o.ToUnit().HasAura("Waterspout"));

            return new PrioritySelector(
                ctx => boss = ctx as WoWUnit,
                        ScriptHelpers.CreateDispelGroup("Fungal Spores", ScriptHelpers.PartyDispelType.Disease));
        }

        [ObjectHandler(203199, "Throne of Tides Defense System")]
        public Composite ThroneofTidesDefenseSystemHandler()
        {
            WoWGameObject defenseSystem = null;
            return new PrioritySelector(
                ctx => defenseSystem = ctx as WoWGameObject, new Decorator(ctx => StyxWoW.Me.IsTank() && defenseSystem.CanUse(), ScriptHelpers.CreateInteractWithObject(ctx => defenseSystem)));
        }

        [EncounterHandler(40765, "Commander Ulthok")]
        public Composite CommanderUlthokEncounter()
        {
            WoWUnit _commanderUlthok = null;
            const uint darkFissureId = 40784;
           AddAvoidObject(
				ctx => true,
                8,
                u => u.Entry == CommanderUlthokId && u.ToUnit().CastingSpellId == DarkFictureSpellId,
                o => WoWMathHelper.GetPointAt(o.Location, 6, o.Rotation, 0));

            AddAvoidObject(ctx => true, ctx => Me.IsTank() ? 15 : 6, darkFissureId);
            return new PrioritySelector(ctx => _commanderUlthok = ctx as WoWUnit);
        }

        private const uint MobId_ErunakStonespeaker = 40825;

        [EncounterHandler(40825, "Erunak Stonespeaker", Mode = CallBehaviorMode.Proximity)]
        public Composite ErunakStonespeakerEncounter()
        {
            const uint earthShardId = 45469;
            const uint mindFogId = 40861;
            AddAvoidObject(ctx => true, 5, earthShardId);
			AddAvoidObject(ctx => true, o => Me.IsTank() ? 18 : 10, mindFogId);
            var insideDoorLoc = new WoWPoint(-202.067f, 637.7326f, 286.3887f);
         //   AddAvoidUnitCone(() => !Me.IsTank(), 0, 20, 100, u => u.Entry == MobId_ErunakStonespeaker && !u.IsMoving && u.CurrentTarget != Me && u.IsHostile && u.Combat);

            return new PrioritySelector(
                ctx =>
                {
                    _mindbender = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == 40788);
                    return _erunakStonespeaker = ctx as WoWUnit;
                },
                // get in the door, 
                new Decorator(ctx => !Me.Combat && StyxWoW.Me.X < -205 && !_erunakStonespeaker.IsFriendly,
                    new Action(ctx => Navigator.MoveTo(insideDoorLoc))),
                // since this guy doesn't die we need to manually mark him as dead.
                new Decorator(
                    ctx => (_mindbender == null || _mindbender.IsDead) && BossManager.CurrentBoss != null && BossManager.CurrentBoss.Entry == 40825,
                    new Action(ctx => BossManager.CurrentBoss.MarkAsDead())),
                new Decorator(ctx => StyxWoW.Me.IsTank() && StyxWoW.Me.CurrentTargetGuid == _erunakStonespeaker.Guid, ScriptHelpers.CreateTankFaceAwayGroupUnit(25)));
        }

        private const uint NeptulonId = 40792;

        [EncounterHandler(40792, "Neptulon", Mode = CallBehaviorMode.Proximity)]
        public Composite OzumatEncounterEncounter()
        {
            var dpsOzmatLoc = new WoWPoint(-113.209f, 957.3337f, 230.738f);
            AddAvoidObject(ctx => true, 10, _blightOfOzumatIds);
            WaitTimer waitForSpawnsTimer = WaitTimer.ThirtySeconds;

            return new PrioritySelector(
                ctx =>
                {
                    _neptulon = (WoWUnit)ctx;
                    if (!Targeting.Instance.IsEmpty() || _neptulon.CanGossip)
                        waitForSpawnsTimer.Reset();
                    _ozumat = ObjectManager.ObjectList.FirstOrDefault(o => o.Entry == 44566) as WoWUnit;
                    return _neptulon;
                },
                new Decorator(
                    ctx => !Targeting.Instance.IsEmpty() && Targeting.Instance.FirstUnit.Entry == OzumatId && !StyxWoW.Me.IsHealer() &&
					StyxWoW.Me.Location.DistanceSqr(dpsOzmatLoc) > 4 * 4,
                    new Action(ctx => Navigator.PlayerMover.MoveTowards(dpsOzmatLoc))),
                new Decorator(
					ctx =>
					(_ozumat == null || _ozumat.IsValid) && Targeting.Instance.IsEmpty()   && _neptulon.CanGossip && StyxWoW.Me.IsTank() &&
					ScriptHelpers.GroupMembers.All(p => p.Location.DistanceSqr(Me.Location) < 40 * 40) ,
					ScriptHelpers.CreateTalkToNpc(ctx => ctx as WoWUnit)),
                // wait for spawns.
                new Decorator(
                    ctx => Targeting.Instance.IsEmpty() && !waitForSpawnsTimer.IsFinished && StyxWoW.Me.IsTank() && ScriptHelpers.IsBossAlive("Ozumat"), new ActionAlwaysSucceed()));
        }

        private const uint GameObjectId_NeptulonsCache = 205216;
        private const uint GameObjectId_NeptulonsCache_Timewalking = 207973;
        private const uint MobId_UnyieldingBehemoth = 44648;

        [EncounterHandler(44648, "Unyielding Behemoth")]
        public Composite UnyieldingBehemothEncounter()
        {
			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(
					ctx => !Me.IsTank() && unit.CurrentTargetGuid != Me.Guid && unit.Distance <= 25 && !unit.IsMoving,
					ctx => _erunakStonespeaker,
					new ScriptHelpers.AngleSpan(0, 100)),
				ScriptHelpers.CreateTankFaceAwayGroupUnit(25));
		}

        #region Elevator

        private const uint ElevatorId = 207209;
        private const float ElevatorBottomZ = 253.7109f;
        private const float ElevatorTopZ = 791.1983f;
        private readonly WoWPoint _elevatorBottomBoardLoc = new WoWPoint(-217.0751f, 805.2832f, 262.3475f);
        private readonly WoWPoint _elevatorBottomExitLoc = new WoWPoint(-252.8892f, 808.4686f, 258.7985f);
        private readonly WoWPoint _elevatorTopBoardLoc = new WoWPoint(-217.0751f, 805.2832f, 799.8349f);
        private readonly WoWPoint _elevatorTopExitLoc = new WoWPoint(-186.2363f, 803.0603f, 796.6603f);

        private WoWPoint GetRandomPointAroundLocation(WoWPoint loc, float radius)
        {
            return loc.RayCast(WoWMathHelper.DegreesToRadians(ScriptHelpers.Rnd.Next(360)), radius);
        }

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
                            if (IsOnLift(tank) && !IsOnLift(Me) && tankLevel == myFloorLevel)
                            {
                                var ele = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(g => g.Entry == ElevatorId);
                                bool elevatorIsReadyToBoard = ele != null && Math.Abs(ele.Z - elevatorRestingZ) <= 0.5f;
                                if (elevatorIsReadyToBoard)
                                {
                                    if (Me.Location.DistanceSqr(elevatorBoardLoc) > 3 * 3)
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

        private bool IsOnLift(WoWPlayer player)
        {
            return player != null && player.TransportGuid > 0 && player.Transport.Entry == ElevatorId;
        }

        private int FloorLevel(WoWPoint loc)
        {
            return loc.Z < 500 ? 1 : 2;
        }

        public bool ElevatorBehavior(WoWPoint destination)
        {
            var myloc = StyxWoW.Me.Location;

            var myFloorLevel = FloorLevel(myloc);
            var destinationFloorLevel = FloorLevel(destination);

            if (myFloorLevel != destinationFloorLevel)
            {
                var ele = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(g => g.Entry == ElevatorId);

                var elevatorRestingZ = myFloorLevel == 1 ? ElevatorBottomZ : ElevatorTopZ;
                var elevatorBoardLoc = destinationFloorLevel == 1 ? _elevatorTopBoardLoc : _elevatorBottomBoardLoc;
                var elevatorWaitLoc = destinationFloorLevel == 1 ? _elevatorTopExitLoc : _elevatorBottomExitLoc;
                bool elevatorIsReadyToBoard = ele != null && Math.Abs(ele.Z - elevatorRestingZ) <= 0.5f;
                // move to the lever loc
              if ((ele == null || myloc.DistanceSqr(elevatorBoardLoc) > 45 * 45 || !elevatorIsReadyToBoard && myloc.DistanceSqr(elevatorWaitLoc) > 4 * 4) &&
					Me.TransportGuid == 0)
                {
                    Logger.Write("[Elevator Manager] Moving To Elevator");
                    var moveResult = Navigator.MoveTo(elevatorWaitLoc);
                    return moveResult != MoveResult.Failed && moveResult != MoveResult.PathGenerationFailed;
                }
                // get onboard of elevator.
                if (elevatorIsReadyToBoard && myloc.DistanceSqr(elevatorBoardLoc) > 3 * 3)
                {
                    Logger.Write("[Elevator Manager] Boarding Elevator");
                    // avoid getting stuck on lever
                    Navigator.PlayerMover.MoveTowards(elevatorBoardLoc);
                }
                else if (elevatorIsReadyToBoard && myloc.DistanceSqr(elevatorBoardLoc) <= 3 * 3 && Me.TransportGuid == 0)
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

        public override async Task<bool> HandleMovement(WoWPoint location)
        {
           // var location = Me.Location;
            var myLoc = StyxWoW.Me.Location;

            // we are on the bottom floor and want to move to the top floor.
            // Teleporters aren't usable while in combat. We're checking Combat here rather than IsActuallyInCombat, because we solely depend on the game's flag for combat.
            if (!StyxWoW.Me.IsOnTransport && myLoc.Z < 500 && location.Z > 500 && !Me.Combat)
            {
                var teleporter = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(o => o.Entry == 51391 && o.HasAura("Teleporter Active Visual"));
                if (teleporter != null && _teleportTimer.IsFinished)
                {
                    if (!teleporter.WithinInteractRange)
                        return (await CommonCoroutines.MoveTo(teleporter.Location)).IsSuccessful();

                    teleporter.Interact();
                    _teleportTimer.Reset();
                    return true;
                }
            }

            // we are on the top floor and want to move to the bottom floor.
            if (!StyxWoW.Me.IsOnTransport && myLoc.Z > 500 && location.Z < 500 && !Me.Combat)
            {
                var teleporter = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(o => o.Entry == 51395 && o.HasAura("Teleporter Active Visual"));
                if (teleporter != null && _teleportTimer.IsFinished)
                {
                    if (!teleporter.WithinInteractRange)
                        return (await CommonCoroutines.MoveTo(teleporter.Location)).IsSuccessful();

                    teleporter.Interact();
                    _teleportTimer.Reset();
                    return true;
                }
            }

            // ozumat move to this spot if fighting Ozumat
            var currentTarget = StyxWoW.Me.CurrentTarget;
            if (currentTarget != null && currentTarget.Entry == 44566 && StyxWoW.Me.IsMelee())
            {
                return (await CommonCoroutines.MoveTo(_ozumatDpsLoc)).IsSuccessful();
            }

            if (ElevatorBehavior(location))
                return true;

            return false;
        }

        #endregion

        #endregion
    }
}