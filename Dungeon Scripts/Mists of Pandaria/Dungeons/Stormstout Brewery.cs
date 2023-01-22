


using System;
using System.Collections.Generic;
using System.Linq;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
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
namespace Bots.DungeonBuddy.Dungeon_Scripts.Mists_of_Pandaria
{

	#region Normal Difficulty

	public class Stormstout_Brewery : Dungeon
	{
		#region Overrides of Dungeon

		private readonly WaitTimer _ignoreHoblitesTimer = new WaitTimer(TimeSpan.FromSeconds(20));
		private readonly WaitTimer _killHoblitesTimer = new WaitTimer(TimeSpan.FromSeconds(10));

		public override uint DungeonId
		{
			get { return 465; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(-718.4064, 1262.169, 136.4682); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(-730.7267, 1261.082, 116.6705); }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (unit != null)
					{
						if (PartyAnimals.Contains(unit.Entry) && (!KillPartyAnimals || unit.Combat && unit.CurrentTargetGuid == 0))
							return true;

						if (_hoplites.Contains(unit.Entry) && StyxWoW.Me.Location.DistanceSqr(_hoptallusLoc) > 10 * 10 && !HoptallusEngaged)
						{
							if (_ignoreHoblitesTimer.IsFinished)
							{
								_ignoreHoblitesTimer.Reset();
								_killHoblitesTimer.Reset();
								Logger.Write("Moving forward for 10 seconds");
							}
							if (!_killHoblitesTimer.IsFinished && StyxWoW.Me.IsTank())
							{
								return true;
							}
						}
						if (unit.Entry == HoptallusId && StyxWoW.Me.IsMelee() && unit.CastingSpellId == FurlwindSpellId)
							return true;

						if (_yeastyBrewAlementalIds.Contains(unit.Entry) && ShouldStayInYanZhusMeleeRange)
							return true;
					}
					return false;
				});
		}


		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			var hasAggro = ObjectManager.GetObjectsOfType<WoWUnit>().Any(u => u.Combat && u.IsTargetingMyRaidMember);
			foreach (var obj in incomingunits)
			{
				var unit = obj as WoWUnit;
				if (unit != null)
				{
					if (PartyAnimals.Contains(unit.Entry) && !hasAggro && unit.DistanceSqr <= 40 * 40 && KillPartyAnimals && Me.IsTank())
					{
						var pathDist = Me.Location.PathDistance(unit.Location, 40f);
						if (pathDist.HasValue && pathDist.Value < 40f)
							outgoingunits.Add(unit);
					}
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
					if (unit.Entry == HabaneroBrewId)
						priority.Score += 400;

					if (BopperIds.Contains(unit.Entry) && StyxWoW.Me.IsDps())
						priority.Score += 600;

					if (HopperIds.Contains(unit.Entry) && StyxWoW.Me.IsDps())
						priority.Score += 500;

					if (unit.Entry == HoptallusId)
					{
						// dps adds while Hoptallus is casting Furlwind otherwise dpe him and ignore adds during carret breath.
						if (unit.CastingSpellId == FurlwindSpellId)
							priority.Score -= 1000;
						else if (unit.CastingSpellId == CarretBreathSpellId)
							priority.Score += 1000;
					}
					// ignore these and stay in melee of Yan-Zhu
					if (_yeastyBrewAlementalIds.Contains(unit.Entry))
					{
						if (ShouldStayInYanZhusMeleeRange && unit.Location.Distance(_yanZhu.Location) > _yanZhu.MeleeRange())
							priority.Score -= 500;
						else
							priority.Score = 10000 - unit.HealthPercent;
					}

					if (unit.Entry == BubbleShieldId)
						priority.Score += 1000;
				}
			}
		}

		#endregion

		private const uint RollingBarrelId = 56682;
		private const uint HozenPartyAnimalId = 56927;
		private const uint SleepyHozenBrawlerId = 56863;
		private const uint HabaneroBrewId = 56731;
		private const uint MysteriouslyShakingKegId = 211138;
		private const uint HoptallusId = 56717;
		private const int FurlwindSpellId = 112992;
		private const uint GushingBrewId = 59394;
		private const int CarbonationId = 56746;
		private const uint FizzyBrewAlemental = 56748;
		private const int CarretBreathSpellId = 112944;
		private readonly uint[] _yeastyBrewAlementalIds = new uint[] { 66413, 59494 };

		private const int FermentHealSpellId = 114451;
		private const int FermentSpellId = 106859;
		private const uint WallOfSudsVehicle = 59510;
		private const uint YanZhuTheUncasked = 56717;
		private const uint UncleGaoId = 59074;
		private const uint ChenStormstoutId = 64361;
		private const int ExplosiveBrew = 116027;
		private const uint FizzyBubbleId = 59799;
		private const int FizzyBubblesSpellId = 114459;
		private const int CarbonationSpellId = 114386;
		private const uint BubbleShieldId = 59487;

		private static readonly uint[] BopperIds = new uint[] { 59426, 59551 };

		private static readonly uint[] HoplingIds = new uint[] { 59459, 59460, 59461 };

		private static readonly uint[] HopperIds = new uint[] { 59464, 56718 };
		private static readonly uint[] PartyAnimals = new[] { SleepyHozenBrawlerId, HozenPartyAnimalId, HabaneroBrewId };
		private readonly WoWPoint _exitBarrelLoc = new WoWPoint(-697.1378, 1259.298, 162.7813);

		private readonly uint[] _hoplites = BopperIds.Concat(HoplingIds.Concat(HopperIds)).ToArray();
		private readonly WoWPoint _hoptallusLoc = new WoWPoint(-696.0826, 1259.851, 162.7818);

		private int _hozenPartyAnimalsDistrupted;
		private DateTime _hozenPartyAnimalsDistruptedUpdatedTime;

		private WoWUnit _ookook;
		private WoWUnit _yanZhu;

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		#region Quest

		[EncounterHandler(64361, "Chen Stormstout", Mode = CallBehaviorMode.Proximity, BossRange = 35)]
		[EncounterHandler(59704, "Chen Stormstout", Mode = CallBehaviorMode.Proximity, BossRange = 35)]
		[EncounterHandler(59822, "Auntie Stormstout", Mode = CallBehaviorMode.Proximity, BossRange = 35)]
		[EncounterHandler(59074, "Uncle Gao", Mode = CallBehaviorMode.Proximity, BossRange = 35)]
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

		[ObjectHandler(213795, "Stormstout Secrets", ObjectRange = 20)]
		public Composite StormstoutSecretsHandler()
		{
			const int familySecretsQuestId = 31324;
			return
				new Decorator<WoWGameObject>(
					book =>
					{
						if (!ScriptHelpers.HasQuest(familySecretsQuestId) || ScriptHelpers.IsQuestInLogComplete(familySecretsQuestId))
							return false;

						if (Me.Combat || !Targeting.Instance.IsEmpty() || ScriptHelpers.WillPullAggroAtLocation(book.Location))
							return false;

						var pathDist = Me.Location.PathDistance(book.Location, 20f);
						return pathDist.HasValue && pathDist.Value < 20f;
					},
					new PrioritySelector(
						new Decorator<WoWGameObject>(book => !book.WithinInteractRange, new Helpers.Action<WoWGameObject>(book => Navigator.MoveTo(book.Location))),
						new Decorator(ctx => LootFrame.Instance.IsVisible, new Action(ctx => LootFrame.Instance.LootAll())),
						 new Helpers.Action<WoWGameObject>(book => book.Interact())));
		}

		#endregion


		#region Ook-Ook

		private int HozenPartyAnimalsDistrupted
		{
			get
			{
				// Throttle the Lua call for efficiency
				if (DateTime.Now - _hozenPartyAnimalsDistruptedUpdatedTime > TimeSpan.FromMilliseconds(1000))
				{
					_hozenPartyAnimalsDistrupted = Lua.GetReturnVal<int>("return UnitPower('player',10)", 0);
					_hozenPartyAnimalsDistruptedUpdatedTime = DateTime.Now;
				}
				return _hozenPartyAnimalsDistrupted;
			}
		}

		private bool KillPartyAnimals
		{
			get
			{
				if (_ookook != null && _ookook.IsValid && _ookook.Attackable)
					return false;
				return HozenPartyAnimalsDistrupted < 40;
			}
		}

		[EncounterHandler(56637, "Ook-Ook", Mode = CallBehaviorMode.Proximity)]
		public Composite OokOokEncounter()
		{
			var rightDoorSide = new WoWPoint(-771.9838, 1387.028, 146.7171);
			var leftDoorSide = new WoWPoint(-760.3922, 1390.458, 146.7169);

			WoWUnit rollingBarrel = null;
			const int groundPoundId = 106807;

			return new PrioritySelector(
				ctx => _ookook = ctx as WoWUnit,
				// out of combat behavior.
				new Decorator(
					ctx => !_ookook.Combat && !Me.Combat,
					new PrioritySelector(
						ctx => ScriptHelpers.Tank,
				// tank needs to wait inside room until all followers are in room
						new Decorator<WoWPlayer>(
							tank =>
								tank.IsMe && Me.Location.IsPointLeftOfLine(leftDoorSide, rightDoorSide) && Me.Z > 140 &&
								!ScriptHelpers.GroupMembers.All(g => g.Location.IsPointLeftOfLine(leftDoorSide, rightDoorSide) && g.Location.Z > 140),
						new PrioritySelector(
								new Decorator(ctx => Me.IsMoving, new Action(ctx => WoWMovement.MoveStop())),
								new ActionAlwaysSucceed())),
				// followers need to move in room before encounter start otherwise they'll get locked out when door closes.
						new Decorator<WoWPlayer>(
							tank =>
								!tank.IsMe && (tank.Location.IsPointLeftOfLine(leftDoorSide, rightDoorSide) && tank.Location.Z > 140) &&
								(!Me.Location.IsPointLeftOfLine(leftDoorSide, rightDoorSide) && Me.Location.Z > 140),
							new Helpers.Action<WoWPlayer>(tank => Navigator.GetRunStatusFromMoveResult(Navigator.MoveTo(tank.Location)))))),

				new Decorator(ctx => _ookook.Combat,
				// handle combat behavior
					new PrioritySelector(
						ScriptHelpers.CreateAvoidUnitAnglesBehavior(
							ctx =>
							_ookook.Distance < 15 &&
							(Me.IsTank() && (_ookook.CastingSpellId == groundPoundId || _ookook.HasAura("Ground Pound")) || !Me.IsTank() && _ookook.CurrentTargetGuid != Me.Guid),
							ctx => _ookook,
							new ScriptHelpers.AngleSpan(0, 120)),
				// handle barrels
						new Decorator(
							ctx => StyxWoW.Me.IsDps(),
							new PrioritySelector(
								ctx =>
								rollingBarrel =
								ObjectManager.GetObjectsOfType<WoWUnit>()
											 .Where(u => u.Entry == RollingBarrelId && u.DistanceSqr <= 12 * 12 && u.CharmedByUnitGuid == 0)
											 .OrderBy(u => u.DistanceSqr)
											 .FirstOrDefault(),
				// if I'm riding a barrel then move towards boss.
								new Decorator(ctx => StyxWoW.Me.IsOnTransport, new Action(ctx => Navigator.PlayerMover.MoveTowards(_ookook.Location))),
				// If I'm a Dps then hop on any nearby rolling barrels 
								new Decorator(ctx => rollingBarrel != null, new Action(ctx => rollingBarrel.Interact())))))));
		}

		#endregion

		#region Root

		[EncounterHandler(0, "Root")]
		public Composite RootEncounter()
		{
			AddAvoidObject(
				ctx => !Me.HasAura("Smash!"),
				10,
				u => HopperIds.Contains(u.Entry) && u.ToUnit().CastingSpellId == ExplosiveBrew && u.ToUnit().CurrentCastTimeLeft <= TimeSpan.FromSeconds(3));

			//casted by Fizzy Brew Alemental
			AddAvoidObject(ctx => true, 6, CarbonationId, FizzyBrewAlemental);

			// brew gushing out of barrels. don't stand in it.
			AddAvoidObject(ctx => StyxWoW.Me.Combat && Math.Abs(StyxWoW.Me.Z - 139) <= 10, 4, GushingBrewId);

			// dodge barrels if not boarding them and nobody is riding them
			AddAvoidObject(
				ctx => _ookook == null || !_ookook.IsValid || !_ookook.Combat || _ookook.Combat && !Me.IsDps(),
				4,
				u => u.Entry == RollingBarrelId && u.ZDiff < 4 && u.Distance < 15,
				u =>
				{
					var start = u.Location;
					return Me.Location.GetNearestPointOnSegment(start, start.RayCast(WoWMathHelper.NormalizeRadian(u.Rotation), 20));
				});

			WoWPlayer bloatedPartyMember = null;
			return new PrioritySelector(
				new Decorator(ctx => StyxWoW.Me.HasAura("How Did I Get Here?"), new Action(ctx => Navigator.PlayerMover.MoveTowards(_exitBarrelLoc))),
				// run from bloated party members.
				new PrioritySelector(
					ctx =>
					bloatedPartyMember =
					ScriptHelpers.GroupMembers.Where(p => p.Player != null && !p.Player.IsMe && p.Player.HasAura("Bloat")).Select(p => p.Player).FirstOrDefault(),
					ScriptHelpers.CreateAvoidUnitAnglesBehavior(
						ctx => bloatedPartyMember != null && bloatedPartyMember.Distance <= 12 && !bloatedPartyMember.IsMoving,
						ctx => bloatedPartyMember,
						new ScriptHelpers.AngleSpan(90, 20),
						new ScriptHelpers.AngleSpan(270, 20))),
				// jump to get rid of blackout brew debuf.
				new Decorator(
					ctx =>
					StyxWoW.Me.HasAura("Blackout Brew") && !Me.HasAura(FizzyBubblesSpellId) && !Me.HasAura(CarbonationSpellId) &&
					(!Me.IsHealer() || Me.IsHealer() && Me.Auras["Blackout Brew"].StackCount < 9 && Me.PartyMembers.All(p => p.HealthPercent > 50)),
					new Action(
						ctx =>
						{
							Lua.DoString("JumpOrAscendStart()");
							Lua.DoString("AscendStop()");
							return RunStatus.Failure;
						})),
				CreateHoppletBehavior());
		}

		#endregion

		#region Hoptallus

		[EncounterHandler(59539, "Big Ol' Hammer", Mode = CallBehaviorMode.Proximity, BossRange = 15)]
		public Composite BigOlHammerEncounter()
		{
			WoWUnit hammer = null;
			return new PrioritySelector(
				ctx => hammer = ctx as WoWUnit,
				new Decorator(
				// can have a max of 10 stacks of Smash! debuf. each hammer gives 3
					ctx => (!StyxWoW.Me.HasAura("Smash!") || StyxWoW.Me.Auras["Smash!"].StackCount <= 7) && !StyxWoW.Me.IsHealer(),
					ScriptHelpers.CreateInteractWithObject(ctx => hammer, 0, true)));
		}

		private Composite CreateHoppletBehavior()
		{
			const int explosiveBrewSpellId = 114291;


			// run away before hopper explodes.
			AddAvoidObject(
				ctx => true,
				10,
				u =>
				HopperIds.Contains(u.Entry) && ((WoWUnit)u).CastingSpellId == explosiveBrewSpellId && ((WoWUnit)u).CurrentCastTimeLeft <= TimeSpan.FromMilliseconds(2000));
			return
				new PrioritySelector(
					new Decorator(
						ctx => StyxWoW.Me.HasAura("Smash!") && ScriptHelpers.GetUnfriendlyNpsAtLocation(StyxWoW.Me.Location, 6, u => _hoplites.Contains(u.Entry)).Any(),
						new Action(
							ctx =>
							{
								Lua.DoString("ExtraActionButton1:Click()");
								return RunStatus.Failure; // we can do other stuff while casting this.
							})));
		}

		private bool HoptallusEngaged;
		[EncounterHandler(56717, "Hoptallus", Mode = CallBehaviorMode.CurrentBoss)]
		public Composite HoptallusGauntletBehavior()
		{
			// force the tank to move to Hoptallus 
			return new PrioritySelector(
				new Action(
					ctx =>
					{
						HoptallusEngaged = ctx != null && ((WoWUnit)ctx).Combat;
						return RunStatus.Failure;
					}),
				new Decorator(ctx => Me.IsTank() && Me.Combat && _killHoblitesTimer.IsFinished && !HoptallusEngaged && Me.Location.DistanceSqr(_hoptallusRoomCenterLoc) > 10 * 10,
					new Action(ctx => Navigator.MoveTo(_hoptallusRoomCenterLoc)))
				);
		}

		readonly WoWPoint _hoptallusRoomCenterLoc = new WoWPoint(-696.4132, 1258.904, 162.7954);

		[EncounterHandler(56717, "Hoptallus")]
		public Composite HoptallusEncounter()
		{
			WoWUnit boss = null;

			var insideDoorLoc = new WoWPoint(-700.3579, 1275.827, 162.7954);
			var roomCenterLoc = new WoWPoint(-696.4132, 1258.904, 162.7954);
			// Run away from furlwind
			AddAvoidObject(ctx => !Me.IsCasting, () => roomCenterLoc, 20, 10, u => u.Entry == HoptallusId && ((WoWUnit)u).CastingSpellId == FurlwindSpellId);

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				// make sure we're inside the door!
				new Decorator(ctx => StyxWoW.Me.Y > 1280, new Action(ctx => Navigator.MoveTo(insideDoorLoc))),
				// ScriptHelpers.CreateAvoidUnitAnglesBehavior(
				//   ctx => boss.CastingSpellId == carrotBreathSpellId && !Me.IsCasting && boss.Distance <= 15, ctx => boss, new ScriptHelpers.AngleSpan(40, 120)),
				new Decorator(
					ctx => StyxWoW.Me.CurrentTarget == boss && boss.ChanneledCastingSpellId == 0 && !Targeting.Instance.IsEmpty(),
					ScriptHelpers.CreateTankUnitAtLocation(ctx => roomCenterLoc, 5)),
				new Decorator(ctx => StyxWoW.Me.IsTank() && Targeting.Instance.IsEmpty(), new ActionAlwaysSucceed()));
		}

		#endregion

		#region Yan-Zhu the Uncasked

		// handles waiting for boss to spawn.

		private bool ShouldJumpForWallsOfSuds
		{
			get
			{
				var sudsWalls = ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == WallOfSudsVehicle).OrderBy(u => u.DistanceSqr).ToArray();
				if (sudsWalls.Length == 0) return false;
				var myLoc = StyxWoW.Me.Location;
				return (from sudsWall in sudsWalls
						let start = WoWMathHelper.CalculatePointAtSide(sudsWall.Location, sudsWall.Rotation, 10, true)
						let end = WoWMathHelper.CalculatePointAtSide(sudsWall.Location, sudsWall.Rotation, 10, false)
						where myLoc.GetNearestPointOnLine(start, end).DistanceSqr(myLoc) <= 15 * 15
						select start).Any();
			}
		}

		private bool ShouldStayInYanZhusMeleeRange
		{
			get
			{
				var tank = ScriptHelpers.Tank;
				return _yanZhu != null && _yanZhu.IsValid && _yanZhu.Combat &&
					   (tank == null || tank.IsMe || tank.Location.Distance(_yanZhu.Location) > _yanZhu.MeleeRange() && StyxWoW.Me.IsMelee());
			}
		}

		private WoWPoint FermentInterceptLoc
		{
			get
			{
				if (StyxWoW.Me.HasAura(FermentHealSpellId))
					return WoWPoint.Zero;

				return (from yeasty in ObjectManager.GetObjectsOfType<WoWUnit>()
						where yeasty.HasAura(FermentSpellId)
						let target =
							ObjectManager.GetObjectsOfType<WoWUnit>(false, false)
										 .FirstOrDefault(u => u.GetAllAuras().Any(a => a.SpellId == FermentHealSpellId && a.CreatorGuid == yeasty.Guid))
						where target != null && (ShouldStayInYanZhusMeleeRange && target.Entry == YanZhuTheUncasked || !ShouldStayInYanZhusMeleeRange)
						select StyxWoW.Me.Location.GetNearestPointOnSegment(yeasty.Location, target.Location)).FirstOrDefault();
			}
		}

		[EncounterHandler(59479, "Yan-Zhu the Uncasked", Mode = CallBehaviorMode.CurrentBoss)]
		public Composite YanZhuTheUncaskedSpawnEncounter()
		{
			var roomCenterLoc = new WoWPoint(-703.0988, 1163.54, 166.1415);
			var waitForSpawnTimer = new WaitTimer(TimeSpan.FromSeconds(20));
			int waitCnt = 0;
			return new PrioritySelector(
				ctx => _yanZhu = ctx as WoWUnit,
				// wait for spawns.
				new Decorator(
					ctx =>
					_yanZhu == null && Targeting.Instance.IsEmpty() && Me.IsTank() && StyxWoW.Me.Location.DistanceSqr(roomCenterLoc) <= 20 * 20,
					new PrioritySelector(
						new Decorator(ctx => waitForSpawnTimer.IsFinished && waitCnt <= 3, new Action(
							ctx =>
							{
								waitForSpawnTimer.Reset();
								waitCnt++;
								return RunStatus.Failure;
							})),
						new Decorator(ctx => !waitForSpawnTimer.IsFinished,
							new PrioritySelector(
								new ActionSetActivity("Waiting for Yan-Zhu to come out of hiding"),
								new ActionAlwaysSucceed()))
						)));
		}

		[ObjectHandler(211137, "Sliding Door", ObjectRange = 4)]
		public Composite SlidingDoorHandler()
		{
			return new PrioritySelector(
				new Decorator<WoWGameObject>(
					door => door.State == WoWGameObjectState.Ready,
					new Helpers.Action<WoWGameObject>(
						door =>
						{
							door.Interact();
							return RunStatus.Failure;
						})));
		}

		private const uint BubblingBrewAlementalId = 59521;

		[LocationHandler(-700.8574, 1161.332, 166.1415, 50, "Yan-Zhu the Uncasked Trash")]
		public Composite CreateBehavior_YanZhuTrash()
		{
			var moveTo = new WoWPoint(-712.2311, 1192.942, 167.3986);
			return new PrioritySelector(
				ctx => ScriptHelpers.Tank,
				// check if tank is LOSing trash at a pillar
				new Decorator<WoWPlayer>(
					tank =>
						!tank.IsMe && tank.Location.DistanceSqr(moveTo) < 5 * 5 &&
						Targeting.Instance.TargetList.Any(t => t.Entry == BubblingBrewAlementalId),
					new PrioritySelector(
						new ActionSetActivity("LOSing trash at Yan-Zhu"),
						new Decorator(
							loc => Me.Location.DistanceSqr(moveTo) > 3 * 3,
							new Action(ctx => Navigator.MoveTo(moveTo))),
				// wait for target to get in dps range.
						new Decorator(
							ctx => ScriptHelpers.MovementEnabled,
							new Helpers.Action<WoWPlayer>(
								tank =>
									ScriptHelpers.DisableMovement(
										() =>
											tank.IsValid && tank.IsAlive && tank.Location.DistanceSqr(moveTo) < 5 * 5 &&
											Targeting.Instance.TargetList.Any(t => t.Entry == BubblingBrewAlementalId)))))));
		}

		[EncounterHandler(59479, "Yan-Zhu the Uncasked")]
		public Composite YanZhuTheUncaskedEncounter()
		{
			WoWUnit fizzyBubble = null;
			var jumpTimer = new WaitTimer(TimeSpan.FromSeconds(2));
			return new PrioritySelector(
				ctx =>
				{
					fizzyBubble = (from obj in ObjectManager.GetObjectsOfType<WoWUnit>()
								   where
									   obj.Entry == FizzyBubbleId &&
									   !Me.PartyMembers.Any(
										   p =>
										   p.IsSafelyFacing(obj, 45) && !p.HasAura(FizzyBubblesSpellId) &&
										   p.Location.DistanceSqr(obj.Location) < Me.Location.DistanceSqr(obj.Location))
								   orderby obj.DistanceSqr
								   select obj).FirstOrDefault();
					return _yanZhu = ctx as WoWUnit;
				},
				// Handle carbonation.
				new Decorator(
					ctx => fizzyBubble != null && !Me.HasAura(FizzyBubblesSpellId),
					new PrioritySelector(
						new Decorator(ctx => fizzyBubble.Distance2D <= 4, new Action(ctx => fizzyBubble.Interact())),
						new Decorator(ctx => fizzyBubble.Distance2D > 4, new Action(ctx => Navigator.MoveTo(fizzyBubble.Location))))),
				new Decorator(
					ctx => Me.HasAura(FizzyBubblesSpellId) && jumpTimer.IsFinished,
					new PrioritySelector(
						new Decorator(
							ctx => Me.HasAura(CarbonationSpellId) || !Me.IsFlying,
							new Action(
								ctx =>
								{
									Lua.DoString("JumpOrAscendStart()");
									Lua.DoString("AscendStop()");
									var loc = Me.Location;
									loc.Z += 6;
									Navigator.PlayerMover.MoveTowards(loc);
									jumpTimer.Reset();
								})))),
				// walls of suds.
				new Decorator(
					ctx => ShouldJumpForWallsOfSuds,
					new Sequence(
						new Action(ctx => WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend)),
						new Action(ctx => Logger.Write("Jumping to avoid wall of suds")),
						new Action(ctx => WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend)))),
				new Decorator(
					ctx => ShouldStayInYanZhusMeleeRange && _yanZhu.Distance2DSqr > 4 * 4,
					new Sequence(
						new Action(ctx => Logger.Write("Moving into Yan-Zhu's Melee Range")),
						new DecoratorContinue(
							ctx => _yanZhu.Distance2DSqr < 10 * 10,
							new Action(ctx => Navigator.PlayerMover.MoveTowards(WoWMathHelper.CalculatePointFrom(StyxWoW.Me.Location, _yanZhu.Location, 3)))),
						new DecoratorContinue(
							ctx => _yanZhu.Distance2DSqr >= 10 * 10,
							ScriptHelpers.CreateMoveToContinue(ctx => WoWMathHelper.CalculatePointFrom(StyxWoW.Me.Location, _yanZhu.Location, 3))))),
				new PrioritySelector(
					ctx => FermentInterceptLoc,
					new Decorator(
						ctx => (WoWPoint)ctx != WoWPoint.Zero,
						new Sequence(new Action(ctx => Logger.Write("Intercepting Ferment")), new Action(ctx => Navigator.PlayerMover.MoveTowards((WoWPoint)ctx))))));
		}

		#endregion
	}

	#endregion

	#region Heroic Difficulty

	public class Stormstout_BreweryHeroic : Stormstout_Brewery
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 469; }
		}

		#endregion
	}

	#endregion

}