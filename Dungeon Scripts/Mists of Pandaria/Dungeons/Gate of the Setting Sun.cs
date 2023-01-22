
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bots.DungeonBuddy.Behaviors;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Bars;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Frames;
using Styx.Helpers;
using Styx.Patchables;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.WoWInternals.World;
using Tripper.Tools.Math;
using Action = Styx.TreeSharp.Action;
using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
using AvoidanceManager = Bots.DungeonBuddy.Avoidance.AvoidanceManager;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Mists_of_Pandaria
{

	#region Normal Difficulty

	public class GateOfTheSettingSun : Dungeon
	{
		#region Overrides of Dungeon

		private const float ElevatorBottomZ = 388.1946f;
		private const float ElevatorTopZ = 430.2219f;
		private const float SouthOfGreatDoorX = 917;
		private const float NorthOfGreatDoorX = 1000;
		private const float LevelDividerZ = 350;
		private const uint RopeStalkerId = 58109;
		private const uint DoodadGreatWallDoor001Id = 212982;

		private readonly WoWPoint _elevatorBottomBoardLoc = new WoWPoint(1195.308f, 2304.462f, 388.8313f);
		//private readonly WoWPoint _elevatorBottomWaitLoc = new WoWPoint(1192.767, 2296.851, 388.1232);
		private readonly WoWPoint _elevatorBottomWaitLoc = new WoWPoint(1193.303, 2297.308, 388.1255);
		private readonly WoWPoint _elevatorTopBoardLoc = new WoWPoint(1195.167, 2304.742, 430.8589);
		private readonly WoWPoint _elevatorTopWaitLoc = new WoWPoint(1203.566, 2304.513, 430.8623);
		private readonly WoWPoint _gadokRoomLoc = new WoWPoint(1195.002, 2305.316, 430.8587);
		private readonly WaitTimer _ropeInteractTimer = new WaitTimer(TimeSpan.FromSeconds(2));

		private readonly CircularQueue<WoWPoint> _ropeLocations = new CircularQueue<WoWPoint>
																  {
																	  new WoWPoint(842.0536, 2315.725, 381.3216),
																	  new WoWPoint(842.3929, 2299.741, 381.3215)
																  };

		private WaitTimer _ropeTimer;
		private uint _ropeUseCounter;

		public override bool IsFlyingCorpseRun
		{
			get { return true; }
		}

		public override uint DungeonId
		{
			get { return 631; }
		}

		//public override WoWPoint Entrance
		//{
		//	get { return new WoWPoint(699.101, 2080.291, 374.6979); }
		//}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(721.3207, 2098.361, 403.9803); }
		}

		private readonly CircularQueue<WoWPoint> _corpseRunBreadCrumb = new CircularQueue<WoWPoint>()
																	{
																		new WoWPoint(639.6627f, 2082.097f, 384.1649f),
																		new WoWPoint(699.101, 2080.291, 374.6979)
																	};
		public override CircularQueue<WoWPoint> CorpseRunBreadCrumb { get { return _corpseRunBreadCrumb; } }

		private bool GreatDoorBroken
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWGameObject>().All(g => g.Entry != DoodadGreatWallDoor001Id);
			}
		}

		public override void OnEnter()
		{
			_wallScalingPackTimer = null;
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			var tank = ScriptHelpers.Tank;
			var tankTransport = tank != null ? tank.Transport : null;

			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (unit != null)
					{
						// remove targets that being kited by group members.
						if (unit.Combat && _kitedMobs.Contains(unit.Entry) && unit.IsTargetingMyPartyMember
							&& (tank == null || !tank.IsMe) )
						{
							if (tank == null)
								return true;
							if (!tank.IsMe)
							{
								var meleeRange = unit.MeleeRange();
								if (tank.Location.DistanceSqr(unit.Location) > meleeRange * meleeRange)
									return true;
								
								if (tankTransport != null && tankTransport.Entry == ElevatorId)
									return true;
							}						
						}
						if (_inCombatMobs.Contains(unit.Entry) && unit.Combat && Me.Combat && !unit.IsTargetingMeOrPet && !unit.IsTargetingMyPartyMember && !unit.TaggedByMe)
							return true;
						if (unit.Entry == KrikthikEngulferId && (Me.IsMelee() || unit.Distance > 35 || Me.InVehicle))
							return true;
						// ignore Striker Gadok if he's on a strafing run  (unless you're healer since some healer routines won't heal if theres nothing it Targeting)
						if (unit.Entry == StrikerGadokId && unit.Z > StrikerGadokStrafeZ && !Me.IsHealer())
							return true;
						// force tank to get off elevator before calling CR pull behavior and getting stuck due to CR blacklisting because boss can't be navigated to from elevator.
						if (unit.Entry == StrikerGadokId && Me.IsTank() && Me.TransportGuid != 0 && !Me.Combat)
							return true;

						if (unit.Entry == KrikthikSwarmerId && (!Me.IsTank() || !_pickupSwarmers))
							return true;

						if (unit.Entry == RaigonnId && unit.Combat && unit.HasAura("Impervious Carapace") && Me.IsDps())
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
				if (unit != null)
				{
					// I have to add these units manually because they apear as non-hostile if using WoWUnit.IsHostile.
					if (_inCombatMobs.Contains(unit.Entry) && !Me.Combat && unit.Distance <= 40)
						outgoingunits.Add(unit);
					if (unit.Entry == RaigonnId && !unit.Combat && Me.IsTank() && unit.Distance < 40)
						outgoingunits.Add(unit);
					if (unit.Entry == WeakSpotId && Me.HasAura(FactionOverrideId))
						outgoingunits.Add(unit);

                    // ranged DPS should kill engulfers but ignore the ones that are > 20 Zdiff unless they're within casting 
                    // range since they probably can't be navigated to. Navigator.CanNavigateFully is too expensive to call here.
					if (unit.Entry == KrikthikEngulferId && Me.IsRangeDps() && (unit.DistanceSqr < 35*35 || unit.ZDiff < 20))
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
					if (unit.Entry == WeakSpotId)
						priority.Score += 5000;

					// ignore the adds on commander Rimok if I'm dps.
					if (unit.Entry == CommanderRimokId && Me.IsDps())
						priority.Score += 5000;

					if (unit.Entry == CommanderRimokId && Me.IsTank() && unit.CurrentTargetGuid != Me.Guid)
						priority.Score += 5000;

					if (unit.Entry == KrikthikEngulferId && Me.IsRange())
						priority.Score += 3000;
				}
			}
		}

		public override async Task<bool> HandleMovement(WoWPoint location)
		{
			if (Me.IsGhost)
			{
				// sometime bot gets dismounted while flying to entrance and false down off wall where it can't get to entrance
				// so we need to port back to spirit healer.
				if (!Me.MovementInfo.CanFly)
				{
					Lua.DoString("PortGraveyard()");
					Logger.Write("Porting back to graveyard");
					await Coroutine.Sleep(2000);
					return true;
				}
				return false;
			}

			if (await ElevatorBehavior(location))
				return true;

			var myLoc = Me.Location;
			var iAmNorthOfGreatDoor = myLoc.X > NorthOfGreatDoorX;
			var iAmSouthOfGreatDoor = myLoc.X < SouthOfGreatDoorX;
			var destinationIsNorthOfGreatDoor = location.X > NorthOfGreatDoorX;
			var destinationIsSouthOfGreatDoor = location.X < SouthOfGreatDoorX;
			var iAmOnBottomLevel = myLoc.Z < LevelDividerZ;
			var destinationIsOnBottomLevel = location.Z < LevelDividerZ;

			var movingNorthAcrossGreatDoor = (iAmSouthOfGreatDoor || iAmOnBottomLevel) && destinationIsNorthOfGreatDoor && !destinationIsOnBottomLevel;
			var movingSouthAcrossGreatDoor = (iAmNorthOfGreatDoor || iAmOnBottomLevel) && destinationIsSouthOfGreatDoor && !destinationIsOnBottomLevel;
			var movingToBottomLevel = !iAmOnBottomLevel && destinationIsOnBottomLevel;
			var movingToTopLevel = iAmOnBottomLevel && !destinationIsOnBottomLevel;

			// are we moving to last boss or do we need to get to other side of broken door?
			if ((movingToBottomLevel || ((movingNorthAcrossGreatDoor || movingSouthAcrossGreatDoor) && !iAmOnBottomLevel && GreatDoorBroken)) && Me.TransportGuid != 0)
			{
				// use the ropes to get down
				if (iAmSouthOfGreatDoor)
				{
					var ropeLoc = _ropeLocations.Peek();

					if (myLoc.Distance(ropeLoc) > 20 && _ropeUseCounter > 0)
						_ropeUseCounter = 0;

					if (myLoc.Distance(ropeLoc) > 4)
						return (await CommonCoroutines.MoveTo(ropeLoc)).IsSuccessful();
					if (myLoc.DistanceSqr(ropeLoc) > 2)
					{
						Navigator.PlayerMover.MoveTowards(ropeLoc);
					}
					else
					{
						if (Me.Mounted)
							Lua.DoString("if IsMounted() then Dismount() else CancelShapeshiftForm() end");
						WoWUnit rope = ObjectManager.GetObjectsOfType<WoWUnit>()
							.Where(u => u.Entry == RopeId)
							.OrderBy(r => r.Location.DistanceSqr(ropeLoc))
							.FirstOrDefault();
						if (rope != null && rope.IsValid)
						{
							if (_ropeTimer == null)
							{
								_ropeTimer = new WaitTimer(TimeSpan.FromMilliseconds(ScriptHelpers.Rnd.Next(10, 6000)));
								_ropeTimer.Reset();
							}
							var otherGroupMemberIsUsingRope =
								ScriptHelpers.GroupMembers.Any(
									g => g.Guid != Me.Guid && g.Player != null && g.Player.TransportGuid != 0 && g.Player.Transport.Entry == RopeStalkerId);
							var yieldToOther =
								ScriptHelpers.GroupMembers.Any(g => g.Guid != Me.Guid && g.Location.Distance(rope.Location) < Me.Location.Distance(rope.Location)) &&
								!_ropeTimer.IsFinished;
							if (!otherGroupMemberIsUsingRope && !yieldToOther && _ropeInteractTimer.IsFinished)
							{
								if (_ropeUseCounter < 20)
								{
									rope.Interact();
									_ropeInteractTimer.Reset();
									_ropeUseCounter++;
									_ropeTimer = null;
								}
								else
								{
									Lua.DoString("LeaveParty()");
									Logger.WriteDebug("Ropes are bugged. Leaving group");
								}
								_ropeLocations.Dequeue();
							}
						}
					}
				}
				// jump down a hole through floor to get down from north side.
				else
				{
					if (myLoc.Z > 375)
					{
						if (myLoc.Distance(_northTopToBottomStepOnePoint) > 15)
							return (await CommonCoroutines.MoveTo(_northTopToBottomStepOnePoint)).IsSuccessful();
						Navigator.PlayerMover.MoveTowards(_northTopToBottomStepTwoPoint);
					}
					else if (myLoc.Z > 350)
					{
						Navigator.PlayerMover.MoveTowards(_northTopToBottomStepThreePoint);
					}
				}
				return true;
			}
			// are we at last boss and we want to move to the walls?
			if (movingToTopLevel)
			{
				var artilleryLoc = movingNorthAcrossGreatDoor ? _artilleryNorthLoc : _artillerySouthLoc;
				if (myLoc.Distance(artilleryLoc) > 4)
					return (await CommonCoroutines.MoveTo(artilleryLoc)).IsSuccessful();
				if (Me.Mounted)
					Lua.DoString("if IsMounted() then Dismount() else CancelShapeshiftForm() end");
				var artillery = ObjectManager.GetObjectsOfType<WoWUnit>().OrderBy(u => u.DistanceSqr).FirstOrDefault(u => u.Entry == ArtilleryId);
				if (artillery != null)
					artillery.Interact();
				return true;
			}
			return false;
		}


		[EncounterHandler(64710, "Rope", Mode = CallBehaviorMode.Proximity, BossRange = 10000)]
		public Composite RopeEncounter()
		{
			return
				new PrioritySelector(new Decorator(ctx => ScriptHelpers.IsBossAlive("Commander Ri'mok"), new Action(ctx => ScriptHelpers.MarkBossAsDead("Commander Ri'mok"))));
		}

		#endregion

		private const uint KrikthikInfiltratorId = 56890;
		private const uint KrikthikInfiltrator2Id = 58108;
		private const uint KrikthikWindShaperId = 59801;
		private const uint KrikthikRagerId = 59800;
		private const uint VolatileMunitionsId = 56896;
		private const uint StableMunitionsId = 56917;
		private const uint LeverId = 211284;
		private const int NorthSouthStrafingRunId = 107342;
		private const int EastWeastStrafingRunId = 107284;
		private const uint StrikerGadokId = 56589;
		private const float StrikerGadokStrafeZ = 432;
		private const uint KrikthikBombardierId = 56706;
		private const int FrenziedAssaultId = 107120;
		private const uint CommanderRimokId = 56636;
		private const uint KrikthikSwarmerId = 59835;
		private const uint RopeId = 64710;
		private const uint ArtilleryId = 66904;
		private const int ImperviousCarapaceId = 107118;
		private const uint RaigonnId = 56877;
		private const uint WeakSpotId = 56895;
		private const int FactionOverrideId = 107132;
		private const uint KrikthikEngulferId = 56912;

		private uint[] _kitedMobs =
		{
			KrikthikInfiltratorId,
			KrikthikInfiltrator2Id,
			KrikthikWindShaperId,
			KrikthikRagerId
		};

		private const uint ElevatorId = 211013;
		private readonly WoWPoint _artillerySouthLoc = new WoWPoint(866.2046, 2225.777, 311.2762);

		private readonly uint[] _inCombatMobs = new[] { KrikthikInfiltratorId, KrikthikInfiltrator2Id, KrikthikWindShaperId, KrikthikRagerId };

		private readonly uint[] _mantidMunitionsIds = new uint[] { 56911, 56918, 56919, 56920, 59205, 59206, 59207, 59208 };
		private readonly WoWPoint _northTopToBottomStepOnePoint = new WoWPoint(1096.306, 2309.375, 381.5589);
		private readonly WoWPoint _northTopToBottomStepThreePoint = new WoWPoint(1060.086, 2297.707, 344.1955);
		private readonly WoWPoint _northTopToBottomStepTwoPoint = new WoWPoint(1081.378, 2299.337, 364.1641);

		private WoWPoint _artilleryNorthLoc = new WoWPoint(1051.559, 2224.952, 311.2657);
		private bool _pickupSwarmers;
		private WaitTimer _wallScalingPackTimer = new WaitTimer(TimeSpan.FromSeconds(10));

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		#region Root

		[EncounterHandler(64467, "Bowmistress Li", Mode = CallBehaviorMode.Proximity)]
		public Composite BowmistressLiEncounter()
		{
			return new PrioritySelector(
				new Decorator<WoWUnit>(u => u.HasQuestAvailable(), ScriptHelpers.CreatePickupQuest(64467)),
				new Decorator<WoWUnit>(u => u.HasQuestTurnin(), ScriptHelpers.CreateTurninQuest(64467)));
		}

		[EncounterHandler(0)]
		public Composite RootEncounter()
		{
			AddAvoidObject(ctx => true, 5, VolatileMunitionsId);
			// AddAvoidObject(ctx => Me.TransportGuid == 0, 2f, LeverId);
			// run from the Munitations explosions.
			AddAvoidObject(
				ctx => true,
				3,
				u => _mantidMunitionsIds.Contains(u.Entry),
				u =>
				{
					var start = u.Location;
					return Me.Location.GetNearestPointOnSegment(start, start.RayCast(WoWMathHelper.NormalizeRadian(u.Rotation), 20));
				});
			return new PrioritySelector(
				new Decorator(ctx => Me.IsFalling, new ActionAlwaysSucceed()),
				CreateFollowerElevatorBehavior(),
				ScriptHelpers.CreateRunToTankIfAggroed(),
				ScriptHelpers.CreateCancelCinematicIfPlaying()
				);
		}

		[LocationHandler(830.1323f, 2312.181f, 381.2352f, 20, "Wait For wall scaling pack")]
		public Composite WaitForWallScalingPack()
		{
			return new PrioritySelector(
				new Decorator(
					ctx => _wallScalingPackTimer == null,
					new Action(
						ctx =>
						{
							_wallScalingPackTimer = new WaitTimer(TimeSpan.FromSeconds(10));
							_wallScalingPackTimer.Reset();
						})),
				new Decorator(
					ctx => !_wallScalingPackTimer.IsFinished && Me.IsTank() && Targeting.Instance.IsEmpty(),
					new PrioritySelector(
						new Decorator(ctx => Me.IsMoving, new Action(ctx => WoWMovement.MoveStop())),
						new ActionAlwaysSucceed())));
		}

		[ObjectHandler(211129, "Signal Flame", ObjectRange = 250)]
		public async Task<bool> SignalFlameHandler(WoWGameObject flame)
		{
			if (flame.State != WoWGameObjectState.Active  || !flame.CanUse())
				return false;
			// The following bosses are dead when the flame can be interacted with
			// so if the following bosses are listed as alive we should mark them as 'dead'.
			// Normally they are marked as dead after being killed but when coming back from 
			// a DC the bot forgets the list of killed bosses.
			if (ScriptHelpers.IsBossAlive("Saboteur Kip'tilak"))
				ScriptHelpers.MarkBossAsDead("Saboteur Kip'tilak");

			if (ScriptHelpers.IsBossAlive("Striker Ga'dok"))
				ScriptHelpers.MarkBossAsDead("Striker Ga'dok");
			
			// Tell the leader (tank if in a LFG, otherwise person with tank spec or highest HP in group if farming) to
			// go interact with the flame. 
			if (Me.IsLeader())
				ScriptHelpers.SetInteractPoi(flame);
			return false;
		}

		#endregion


		#region Saboteur Kip'tilak

		readonly WoWPoint _saboteurKiptilakLoc = new WoWPoint(722.2803, 2318.485, 391.0808);
		private const uint SaboteurKiptilakExitGateId = 212983;

		[EncounterHandler(56906, "Saboteur Kip'tilak", Mode = CallBehaviorMode.CurrentBoss)]
		public Composite SaboteurKiptilakSpawnBehavior()
		{
			return new Action(
				ctx =>
				{
					var boss = ctx as WoWUnit;
					if (boss == null && Me.IsTank()
						&& !IsSaboteurKiptilakExitGateOpen 
						&& Targeting.Instance.IsEmpty() 
						&& Me.Location.DistanceSqr(_saboteurKiptilakLoc) < 20*20)
					{
						return RunStatus.Success;
					}
					return RunStatus.Failure;
				});
		}

		private bool IsSaboteurKiptilakExitGateOpen
		{
			get
			{
				return ObjectManager.GetObjectsOfTypeFast<WoWGameObject>()
					.Any(g => g.Entry == SaboteurKiptilakExitGateId && g.State == WoWGameObjectState.Active);
			}
		}

		[EncounterHandler(56906, "Saboteur Kip'tilak", Mode = CallBehaviorMode.Proximity, BossRange = 100)]
		public Composite SaboteurKiptilakEncounter()
		{
			WoWUnit boss = null;
			var insideDoorLoc = new WoWPoint(722.3629, 2281.56, 387.9894);
			AddAvoidObject(
				ctx => Me.RaidMembers.Any(u => u.HasAura("Sabotage")),
				8,
				u =>
					u.Entry == StableMunitionsId &&
					Me.RaidMembers.Any(
						p =>
							p.HasAura("Sabotage") && (Math.Abs(u.X - p.X) < 4 || Math.Abs(u.Y - p.Y) < 4) && u.Location.Distance(p.Location) < 20));
			AddAvoidObject(ctx => true, 8, u => u is WoWPlayer && u != Me && u.ToPlayer().HasAura("Sabotage"));
			// run away from any stable munitions that are aligned north/south or east/west with a group member with Sabotage debuf.
			AddAvoidObject(ctx => Me.HasAura("Sabotage"), 8, u => u is WoWPlayer && u != Me);
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				new Decorator(
					ctx => Me.IsTank() && !Me.Combat && Me.Y >= 2270 && !ScriptHelpers.GroupMembers.All(g => g.Location.Y >= 2270),
					new PrioritySelector(new ActionSetActivity("Waiting for group members to get inside room"), new ActionAlwaysSucceed())),
				// get foot inside room before door closes
				new PrioritySelector(
					ctx => ScriptHelpers.Tank,
					new Decorator<WoWUnit>(
						tank => Me.Y < 2281 && Targeting.Instance.IsEmpty() && (tank.IsMe || tank.Y >= 2270),
						new Action(ctx => Navigator.MoveTo(insideDoorLoc)))));
		}

		#endregion

		#region Striker Ga'dok

		[EncounterHandler(56589, "Striker Ga'dok", BossRange = 300)]
		public Func<WoWUnit, Task<bool>> StrikerGadokEncounter()
		{
            var eastPoint = new WoWPoint(1195.388, 2275.083, 430.8744);
            var southPoint = new WoWPoint(1166.775, 2304.836, 430.874);
            var westPoint = new WoWPoint(1194.746, 2333.279, 430.874);
		    var northPoint = new WoWPoint(1223.854, 2304.638, 430.874);

			const uint acidBombId = 59813;

			AddAvoidLocation(
				ctx => true,
				5,
				m => ((WoWMissile) m).ImpactPosition,
				() => WoWMissile.InFlightMissiles.Where(m => m.SpellId == acidBombId));
			AddAvoidObject(ctx => true, 5, acidBombId);


		    Func<WoWUnit, WoWPoint> getNearestAvoidStrafePoint = boss =>
		    {
		        var leader = ScriptHelpers.Tank;
		        var myLoc = leader != null && leader != Me
		            ? leader.Location
		            : Me.Location;
		        var bossLoc = boss.Location;

		        var bossToNorthSouthLineDistanceSqr =
		            bossLoc.GetNearestPointOnLine(northPoint, southPoint)
		                .DistanceSqr(bossLoc);
		        var bossToEastWestLineDistanceSqr =
		            bossLoc.GetNearestPointOnLine(eastPoint, westPoint)
		                .DistanceSqr(bossLoc);

		        if (bossToEastWestLineDistanceSqr <
		            bossToNorthSouthLineDistanceSqr)
		        {
		            TreeRoot.StatusText = "Avoiding East/West Strafing run";
		            return myLoc.DistanceSqr(northPoint) <
		                myLoc.DistanceSqr(southPoint)
		                ? northPoint
		                : southPoint;
		        }
		        TreeRoot.StatusText = "Avoiding North/South Strafing run";
		        return myLoc.DistanceSqr(eastPoint) <
		            myLoc.DistanceSqr(westPoint)
		            ? eastPoint
		            : westPoint;
		    };

		    var shouldHandleStrafe =
		        new Func<WoWUnit, bool>(
		            unit =>
		                ScriptHelpers.IsViable(unit) && unit.Z > StrikerGadokStrafeZ &&
		                (Me.IsTank() && Targeting.Instance.TargetList.All(t => t.IsTargetingMeOrPet) || !Me.IsTank()));

		    return async boss =>
		    {
		        // strafe run.
		        if (await ScriptHelpers.StayAtLocationWhile(
		                    () => shouldHandleStrafe(boss),
		                    getNearestAvoidStrafePoint(boss),
		                    precision: 10))
		        {
		            return true;
		        }

		        if (Targeting.Instance.IsEmpty() && Me.IsTank())
		            return true;
		        return false;
		    };
		}


		[EncounterHandler(60415, "Flak Cannon", Mode = CallBehaviorMode.Proximity, BossRange = 100)]
		public Composite FlakCannonEncounter()
		{
			WoWUnit boss = null;
			const int lootSparkleId = 92406;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				new Decorator(
					ctx =>
						boss.HasAura(lootSparkleId) &&
						ObjectManager.GetObjectsOfType<WoWUnit>().Any(u => u.Entry == KrikthikBombardierId && u.IsAlive),
					ScriptHelpers.CreateInteractWithObject(ctx => boss)));
		}

		#endregion

		#region Commander Ri'mok

		[EncounterHandler(56636, "Commander Ri'mok")]
		public Composite CommanderRimokEncounter()
		{
			const uint viscosFluidStalkerId = 56883;
			const int frenziedAssaultId = 107120;
			var kiteCenterLoc = new WoWPoint(1297.094, 2301.952, 388.9373);
			WoWUnit boss = null;
			WoWPoint tankStepOutOfMeleeRangePoint = WoWPoint.Zero;

			AddAvoidObject(
				ctx => StyxWoW.Me.IsTank() && !_pickupSwarmers && boss != null && boss.IsValid && boss.CurrentTargetGuid == Me.Guid,
				() => kiteCenterLoc,
				46,
				17,
				u =>
					u.Entry == viscosFluidStalkerId && boss != null && boss.IsValid && boss.Location.DistanceSqr(u.Location) < 4 &&
					boss.HasAura("Viscous Fluid") &&
					boss.Auras["Viscous Fluid"].StackCount >= 3);

			AddAvoidObject(ctx => Me.IsRangeDps(), 7, viscosFluidStalkerId);

			// ranged should stay away from the boss to avoid the frenzy attack.. 
			// note I'm making avoid radius higher while moving so range don't take tiny baby steps everytime boss moves a little whenever standing just outside the avoidance radius
			AddAvoidObject(ctx => Me.IsRange(), o => Me.IsMoving ? 14 : 12, o => o.Entry == CommanderRimokId && o.ToUnit().IsAlive);


			return new PrioritySelector(
				ctx =>
				{
					_pickupSwarmers =
						ObjectManager.GetObjectsOfType<WoWUnit>().Count(u => u.Combat && u != boss && u.IsTargetingMyPartyMember) >= 5;
					return boss = ctx as WoWUnit;
				},
				new Decorator(
					ctx => Me.IsTank() && boss.CastingSpellId == frenziedAssaultId,
					new PrioritySelector(
						// back away from boss so he kills the adds with his frontal attack.
						new Decorator(
							ctx => (tankStepOutOfMeleeRangePoint = GetPointInFrontOfUnitAndOusideMeleeRange(boss, 1)) != WoWPoint.Zero,
							new PrioritySelector(
								new Decorator(
									ctx => Me.Location.Distance(tankStepOutOfMeleeRangePoint) <= Navigator.PathPrecision,
									new Action(ctx => Navigator.PlayerMover.MoveTowards(tankStepOutOfMeleeRangePoint))),
								new Decorator(
									ctx => Me.Location.Distance(tankStepOutOfMeleeRangePoint) > Navigator.PathPrecision,
									new Action(ctx => Navigator.GetRunStatusFromMoveResult(Navigator.MoveTo(tankStepOutOfMeleeRangePoint)))))),
						// bot can't move to the front we just sidestep.
						ScriptHelpers.CreateAvoidUnitAnglesBehavior(
							ctx => tankStepOutOfMeleeRangePoint == WoWPoint.Zero && boss.Distance < 12,
							ctx => boss,
							new ScriptHelpers.AngleSpan(0, 180)))),
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(
					ctx => Me.IsMeleeDps() && boss.Distance < 12,
					ctx => boss,
					new ScriptHelpers.AngleSpan(0, 180)));
		}

		private WoWPoint GetPointInFrontOfUnitAndOusideMeleeRange(WoWUnit unit, float distancePastMeleeRange)
		{
			var facing = WoWMathHelper.NormalizeRadian(unit.Rotation);
			var point = unit.Location.RayCast(facing, 12 + distancePastMeleeRange);
			// set the Z coord to mesh level.
			var zList = Navigator.FindHeights(point.X, point.Y);
			if (zList != null && zList.Count > 0)
			{
				point.Z = zList.OrderBy(z => Math.Abs(z - point.Z)).FirstOrDefault();
				if (Navigator.CanNavigateFully(Me.Location, point))
					return point;
			}
			return WoWPoint.Zero;
		}

		#endregion

		#region Raigonn

		private const float RightSeatCorrectFacing = 0.8418016f;
		private const float LeftSeatCorrectFacing = 2.11022f;

		private const float WeakSpotFacingErrorAmount = 3f;
		private readonly WoWPoint _rightSeatLoc = new WoWPoint(1.5, -3, 0.25);
		private readonly WoWPoint _leftSeatLoc = new WoWPoint(1.5, 3, 0.25);
		private WoWPoint _raigonnDropLocation = new WoWPoint(1098.698, 2305.023, 381.2352);

		[EncounterHandler(56877, "Raigonn", BossRange = 200)]
        public Func<WoWUnit, Task<bool>> RaigonnEncounter()
		{
			var isMovingTimer = new WaitTimer(TimeSpan.FromSeconds(1));
			const uint artilleryId = 59819;
			const uint engulfingWindsId = 56928;
			const int vulnerabilityId = 111682;

			var roomCenterLoc = new WoWPoint(956.2532, 2275.753, 296.1056);
			var gateLoc = new WoWPoint(959.0933, 2227.044, 296.1056);

			AddAvoidObject(ctx => Me.HasAura("Fixate"), () => roomCenterLoc, 80, 40, RaigonnId);
			AddAvoidObject(ctx => Me.TransportGuid == 0,
				o =>
				{
					if (Me.IsMoving)
						isMovingTimer.Reset();
					return Me.IsRange() && !isMovingTimer.IsFinished ? 10 : 5;
				}, engulfingWindsId);

		    return async boss =>
		    {
		        var shouldAttackWeakSpot = boss.HasAura("Impervious Carapace") && Me.IsDps()
                    && Me.TransportGuid != boss.Guid && Me.PartyMembers.Count(p => p.TransportGuid == boss.Guid) < 2 
                    && (boss.Location.DistanceSqr(gateLoc) <= 50*50 || Targeting.Instance.IsEmpty()) 
                    && ObjectManager.GetObjectsOfType<WoWUnit>().Any(u => u.Entry == WeakSpotId && u.HasAura(vulnerabilityId));

		        if (shouldAttackWeakSpot)
		        {
		            var artillery = ObjectManager.GetObjectsOfType<WoWUnit>()
                        .Where(u => u.Entry == artilleryId )
		                .OrderBy(u => u.DistanceSqr)
                        // skip artillery that have tornados nearby
		                .FirstOrDefault(u => !ObjectManager.GetObjectsOfType<WoWUnit>()
                            .Any(w => w.Entry == engulfingWindsId && u.Location.DistanceSqr(w.Location) < 12*12));
                    if (artillery != null)
                    {
                        if (Me.Mounted && artillery.WithinInteractRange)
                            await CommonCoroutines.Dismount();

                        return await ScriptHelpers.InteractWithObject(artillery, 0, true);
                    }
		        }

                // if stuck in an artillery then exit 
		        if (Me.TransportGuid != 0 && Me.Transport.Entry == artilleryId)
		        {
		            Lua.DoString("VehicleExit()");
		            return true;
		        }

		        if (await ScriptHelpers.DispelGroup("Screeching Swarm", ScriptHelpers.PartyDispelType.Magic))
		            return true;

                // make sure we're facing the weak spot.
		        var currentTarget = Me.CurrentTarget;
                // WoW does not face in the correct direction when the WoWUnit.Face() command is issued so 
                // the following is a hackish fix.
		        if (currentTarget != null && currentTarget.Entry == WeakSpotId && !Me.IsSafelyFacing(currentTarget, 60))
		        {
		            Logging.Write("Facing the WeakSpot");
                    FaceWeakSpot();
		        }

		        return false;
		    };
		}

		// hackish method of getting bot to face weakspot 
		private void FaceWeakSpot()
		{
			var myRelativeLoc = Me.RelativeLocation;
			if (myRelativeLoc == _leftSeatLoc)
			{
				Me.SetFacing(WoWMathHelper.NormalizeRadian(LeftSeatCorrectFacing + WeakSpotFacingErrorAmount));
			}
			else if (myRelativeLoc == _rightSeatLoc)
			{
				Me.SetFacing(WoWMathHelper.NormalizeRadian(RightSeatCorrectFacing + WeakSpotFacingErrorAmount));
			}
		}

		#endregion

		#region Elevator

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
							var elevatorWaitLoc = myFloorLevel == 2 ? _elevatorTopWaitLoc : _elevatorBottomWaitLoc;

							// do we need to get on a lift?
							if (IsOnLift(tank) && (!IsOnLift(Me) || Me.Location.DistanceSqr(elevatorBoardLoc) > 3 * 3) && tankLevel == myFloorLevel)
							{
								var ele = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(g => g.Entry == ElevatorId);
								bool elevatorIsReadyToBoard = ele != null && Math.Abs(ele.Z - elevatorRestingZ) <= 0.5f;
								if (elevatorIsReadyToBoard)
								{
									if (Me.Location.DistanceSqr(elevatorBoardLoc) > 3 * 3)
									{
										Logger.Write("[Elevator Manager] Boarding Elevator");
										Navigator.MoveTo(elevatorBoardLoc);
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

							// do we need to get off lift?
							if (IsOnLift(Me))
							{
								var ele = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(g => g.Entry == ElevatorId);
								bool elevatorInUse = ele != null && Math.Abs(ele.Z - ElevatorBottomZ) > 0.5 && Math.Abs(ele.Z - ElevatorTopZ) > 0.5;
								if (elevatorInUse)
									return RunStatus.Success;
							}
						}
						return RunStatus.Failure;
					}));
		}

		public async Task<bool> ElevatorBehavior(WoWPoint destination)
		{
			if (AvoidanceManager.IsRunningOutOfAvoid)
				return false;
			var myloc = StyxWoW.Me.Location;

			var myFloorLevel = FloorLevel(myloc);
			var destinationFloorLevel = FloorLevel(destination);

			if (myFloorLevel != destinationFloorLevel)
			{
				var ele = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(g => g.Entry == ElevatorId);

				var elevatorRestingZ = myFloorLevel == 1 ? ElevatorBottomZ : ElevatorTopZ;
				var elevatorBoardLoc = destinationFloorLevel == 1 ? _elevatorTopBoardLoc : _elevatorBottomBoardLoc;
				var elevatorWaitLoc = destinationFloorLevel == 1 ? _elevatorTopWaitLoc : _elevatorBottomWaitLoc;
				var lever = ObjectManager.GetObjectsOfType<WoWGameObject>().Where(o => o.Entry == 211284).OrderBy(o => o.DistanceSqr).FirstOrDefault();
				bool elevatorIsReadyToBoard = ele != null && Math.Abs(ele.Z - elevatorRestingZ) <= 0.5f;
				bool elevatorInUse = ele != null && Math.Abs(ele.Z - ElevatorBottomZ) > 0.5 && Math.Abs(ele.Z - ElevatorTopZ) > 0.5;

				// do nothing when taking elevator up/down.
				if (ele != null && Me.TransportGuid == ele.Guid && elevatorInUse)
					return true;

				// move to the lever loc
				if ((ele == null || (myloc.DistanceSqr(elevatorBoardLoc) > 20 * 20 || !elevatorIsReadyToBoard && myloc.DistanceSqr(elevatorWaitLoc) > 4 * 4)) &&
					Me.TransportGuid != 0)
				{
					Logger.Write("[Elevator Manager] Moving To Elevator");
					var moveResult = Navigator.MoveTo(elevatorWaitLoc);
					return moveResult != MoveResult.Failed && moveResult != MoveResult.PathGenerationFailed;
				}
				// use lever to bring elevator to our floor level.
				if (!elevatorIsReadyToBoard && !elevatorInUse && myloc.DistanceSqr(elevatorWaitLoc) <= 20 * 20 && Me.TransportGuid != 0 && lever != null && lever.CanUse())
				{
					if (myloc.DistanceSqr(elevatorWaitLoc) > 3 * 3)
					{
						Navigator.MoveTo(elevatorWaitLoc);
						return true;
					}
					Logger.Write("[Elevator Manager] Bringing Elevator to my level");
					lever.Interact();
				}
				// get onboard of elevator.
				if (elevatorIsReadyToBoard && myloc.DistanceSqr(elevatorBoardLoc) > 2 * 2 && Me.TransportGuid != 0)
				{
					Logger.Write("[Elevator Manager] Boarding Elevator");
					// avoid getting stuck on lever
					Navigator.MoveTo(elevatorBoardLoc);
				}
				else if (elevatorIsReadyToBoard && myloc.DistanceSqr(elevatorBoardLoc) <= 2 * 2 && Me.TransportGuid != 0)
				{
					Logger.Write("[Elevator Manager] Jumping");
					WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
					WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend);
				}
				if (elevatorIsReadyToBoard && Me.TransportGuid == ele.Guid 
					&& Me.RaidMembers.Where(r => FloorLevel(r.Location) == myFloorLevel).All(r => r.TransportGuid == ele.Guid) 
					&& lever != null && lever.CanUse())
				{
					if (!lever.CanUseNow())
						Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(Me.Location, lever.Location, lever.InteractRange - 1));
					else if (Me.IsMoving)
						WoWMovement.MoveStop();
					Logger.Write("[Elevator Manager] Using Lever");
					lever.Interact();
				}
				return true;
			}

			// exit elevator
			var transport = Me.TransportGuid != 0 ? Me.Transport : null;
			if (transport != null && transport.Entry == ElevatorId)
			{
				var elevatorExitZ = destinationFloorLevel == 1 ? ElevatorBottomZ : ElevatorTopZ;
				var elevatorExitLoc = destinationFloorLevel == 1 ? _elevatorBottomWaitLoc : _elevatorTopWaitLoc;
				bool elevatorIsReadyToExit = Math.Abs(transport.Z - elevatorExitZ) <= 0.5f;

				if (elevatorIsReadyToExit)
				{
					if (Navigator.CanNavigateFully(myloc, destination))
						Navigator.MoveTo(destination);
					else
						Navigator.MoveTo(elevatorExitLoc);
				}
				return true;
			}

			return false;
		}


		private bool IsOnLift(WoWPlayer player)
		{
			return player != null && player.TransportGuid != 0 && player.Transport.Entry == ElevatorId;
		}

		private int FloorLevel(WoWPoint loc)
		{
			return loc.Z > 425 && loc.DistanceSqr(_gadokRoomLoc) <= 75 * 75 ? 2 : 1;
		}
     
		#endregion
	}

	#endregion

	#region Heroic Difficulty

	public class GateOfTheSettingSunHeroic : GateOfTheSettingSun
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 471; }
		}

		#endregion
	}

	#endregion
}