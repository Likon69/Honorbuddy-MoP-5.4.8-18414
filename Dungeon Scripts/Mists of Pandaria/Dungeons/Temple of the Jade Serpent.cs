
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bots.DungeonBuddy.Profiles.Handlers;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using System.Linq;
using Styx.WoWInternals.World;
using Tripper.Tools.Math;
using Action = Styx.TreeSharp.Action;
using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Mists_of_Pandaria
{

	#region Normal Difficulty

	public class Temple_of_the_Jade_Serpent : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 464; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(957.2355, -2476.059, 180.9084); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(955.8338, -2480.424, 180.4973); }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret as WoWUnit;
					if (unit.Entry == WiseMariId && unit.HasAura("Water Bubble") && !Me.IsHealer())
						return true;

					if ((unit.Entry == LesserSha || unit.Entry == MinionOfDoubt))
					{ 
						// check if these are on otherside of a door.
						return !unit.InLineOfSpellSight;
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
					if (unit.Entry == FragmentOfDoubt && unit.CanSelect && unit.Attackable)
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
					if ((unit.Entry == StrifeId || unit.Entry == PerilId) && unit.HasAura("Intensity") && unit.Auras["Intensity"].StackCount >= 9)
						priority.Score -= 5000;

					if (unit.Entry == HauntingShaId)
						priority.Score += 2000;

					if (unit.Entry == FragmentOfDoubt)
						priority.Score += 2000;
				}
			}
		}

		/*

	public override void OnEnter()
	{
		Alert.Show(
			"Do Not AFK",
			string.Format("Mesh problems exist in {0}. Do you want to continue?", Name),
			30,
			true,
			true,
			null,
			() => Lua.DoString("LeaveParty()"),
			"Continue",
			"Leave");
	}
		 */

		#endregion

		private const uint ShaResidueId = 106653;
		private const uint WiseMariId = 56448;
		private const uint WiseMariFireHoseTargetId = 56574;
		private const uint StrifeId = 59051;
		private const uint PerilId = 59726;
		private const uint HauntingShaId = 59555;
		private const int ShadowsOfDoubt = 110099;
		private const uint DragonWave = 56789;
		private const uint JadeFire = 56893;
		private const uint FragmentOfDoubt = 56792;
		private const uint LesserSha = 58319;
		private const uint MinionOfDoubt = 57109;

		private static readonly WoWPoint[] WiseMariSafeSpots = new[]
															   { // inner area
																   new WoWPoint(1051.405, -2553.585, 174.6977), new WoWPoint(1052.822, -2554.836, 174.6978),
																   new WoWPoint(1054.469, -2557.322, 174.6978), new WoWPoint(1055.248, -2560.337, 174.6978),
																   new WoWPoint(1054.762, -2562.604, 174.6978), new WoWPoint(1053.547, -2565.319, 174.6978),
																   new WoWPoint(1048.494, -2568.502, 174.6978), new WoWPoint(1045.432, -2568.629, 174.6977),
																   new WoWPoint(1042.862, -2567.668, 174.6977), new WoWPoint(1040.639, -2565.951, 174.6978),
																   new WoWPoint(1038.646, -2560.687, 174.6978), new WoWPoint(1039.012, -2557.942, 174.6978),
																   new WoWPoint(1040.285, -2555.325, 174.6978), new WoWPoint(1042.682, -2553.407, 174.6978),
																   new WoWPoint(1045.162, -2552.474, 174.6978), new WoWPoint(1047.432, -2552.278, 174.6977),
																   // outer area
																   new WoWPoint(1047.545, -2546.342, 174.6978), new WoWPoint(1043.129, -2546.999, 174.6978),
																   new WoWPoint(1039.361, -2548.945, 174.6978), new WoWPoint(1035.902, -2551.642, 174.6978),
																   new WoWPoint(1033.8, -2555.948, 174.6978), new WoWPoint(1033.109, -2560.566, 174.6978),
																   new WoWPoint(1029.522, -2554.528, 174.6978), new WoWPoint(1026.834, -2572.702, 174.6978),
																   new WoWPoint(1029.761, -2576.143, 174.6978), new WoWPoint(1031.975, -2579.165, 174.6978),
																   new WoWPoint(1035.268, -2579.458, 174.6978), new WoWPoint(1039.015, -2575.475, 174.6978),
																   new WoWPoint(1036.832, -2569.53, 174.6978), new WoWPoint(1040.178, -2572.773, 174.6978),
																   new WoWPoint(1044.089, -2573.994, 174.6977), new WoWPoint(1048.111, -2573.905, 174.6978),
																   new WoWPoint(1050.307, -2573.528, 174.6978), new WoWPoint(1045.066, -2577.509, 174.6978),
																   new WoWPoint(1040.632, -2579.016, 174.6977), new WoWPoint(1042.359, -2576.134, 174.6977),
																   new WoWPoint(1053.788, -2582.024, 174.6978), new WoWPoint(1048.276, -2583.024, 174.6978),
																   new WoWPoint(1046.994, -2589.204, 174.6979), new WoWPoint(1043.431, -2589.852, 174.6978),
																   new WoWPoint(1040.591, -2593.741, 174.703), new WoWPoint(1040.438, -2585.912, 174.6977),
																   new WoWPoint(1044.145, -2585.725, 174.6978), new WoWPoint(1033.802, -2584.221, 174.6978),
																   new WoWPoint(1034.886, -2589.313, 174.6982), new WoWPoint(1033.678, -2592.558, 174.702),
																   new WoWPoint(1038.613, -2592.392, 174.701), new WoWPoint(1057.845, -2569.239, 174.6978),
																   new WoWPoint(1059.668, -2564.843, 174.6978), new WoWPoint(1061.083, -2560.323, 174.6978),
																   new WoWPoint(1059.867, -2555.553, 174.6978), new WoWPoint(1057.377, -2551.288, 174.6978),
																   new WoWPoint(1054.945, -2548.756, 174.6978), new WoWPoint(1064.203, -2566.805, 174.6984),
																   new WoWPoint(1066.948, -2565.171, 174.6978)
															   };

		private readonly CircularQueue<WoWPoint> _wiseMariCirclePath = new CircularQueue<WoWPoint>
																	   {
																		   new WoWPoint(1051.295, -2553.112, 174.6977),
																		   new WoWPoint(1054.035, -2556.29, 174.6978),
																		   new WoWPoint(1055.2, -2562.44, 174.6978),
																		   new WoWPoint(1053.14, -2566.464, 174.6978),
																		   new WoWPoint(1048.866, -2568.538, 174.6978),
																		   new WoWPoint(1046.027, -2569.12, 174.6977),
																		   new WoWPoint(1042.23, -2567.977, 174.6977),
																		   new WoWPoint(1040.654, -2566.208, 174.6978),
																		   new WoWPoint(1038.396, -2560.746, 174.6978),
																		   new WoWPoint(1039.406, -2556.696, 174.6978),
																		   new WoWPoint(1042.437, -2553.605, 174.6978),
																		   new WoWPoint(1047.244, -2551.961, 174.6977),
																	   };


		private readonly WoWPoint[] _wiseMariCorruptLivingTankSpots = new[] { new WoWPoint(1049.255, -2585.932, 174.6978), new WoWPoint(1032.726, -2583.954, 174.6978) };
		private WoWUnit _liuFlameheart;

		private WoWUnit _wiseMari;

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0)]
		public async Task<bool> Root(WoWUnit unit)
		{
			return false;
		}

		[EncounterHandler(60578, "Priestess Summerpetal", Mode = CallBehaviorMode.Proximity, BossRange = 35)]
		[EncounterHandler(64399, "Master Windstrong", Mode = CallBehaviorMode.Proximity, BossRange = 35)]
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

		[EncounterHandler(0, "Root")]
		public Composite RootHandler()
		{
			// Corrupt Living Water npc drop this after they die.
			AddAvoidObject(ctx => true, 5, o => o.Entry == ShaResidueId, null, true);
			// dropped by minion of doubt
			AddAvoidObject(ctx => true, 3, o => o.Entry == ShadowsOfDoubt, null, true);

			return new PrioritySelector();
		}

		#region Liu Flameheart

		[EncounterHandler(56732, "Liu Flameheart", Mode = CallBehaviorMode.CurrentBoss)]
		public Func<WoWUnit,Task<bool>> LiuFlameheartPreEncounter()
		{
			var roomCenterLoc = new WoWPoint(929.55, -2560.471, 180.0693);
			return async boss =>
			{
				if (boss == null  || !boss.CanSelect || !boss.Attackable)
				{
					await ScriptHelpers.ClearArea(roomCenterLoc, 70);
				}
				return false;
			};
		}

		[EncounterHandler(56732, "Liu Flameheart")]
		public Composite LiuFlameheartEncounter()
		{
			const uint JadeSerpentWaveId = 119508;
			var tankLoc = new WoWPoint(929.6893, -2560.766, 180.0695);

			AddAvoidObject(ctx => true, 3.5f, JadeFire);
			AddAvoidObject(ctx => true, 2, o => o is WoWAreaTrigger && ((WoWAreaTrigger)o).SpellId == JadeSerpentWaveId, null, true);
			AddAvoidObject(
				ctx => true,
				5,
				o => o.Entry == DragonWave,
				o =>
				{
					var loc = o.Location;
					if (_liuFlameheart != null && _liuFlameheart.IsValid)
					{
						return Me.Location.GetNearestPointOnSegment(loc, _liuFlameheart.Location.RayCast(WoWMathHelper.NormalizeRadian(o.ToUnit().Rotation), 80));
					}
					return loc;
				});
			return new PrioritySelector(
				ctx => _liuFlameheart = ctx as WoWUnit,
				new Decorator(
					ctx => Me.IsTank() && Me.CurrentTargetGuid == _liuFlameheart.Guid && _liuFlameheart.CurrentTargetGuid == Me.Guid,
					ScriptHelpers.CreateTankUnitAtLocation(ctx => tankLoc, 8)));
		}

		#endregion

		#region Sha of Doubt

		[EncounterHandler(56439, "Sha of Doubt")]
		public Composite ShaOfDoubtEncounter()
		{
			// run away from other party members if I have touch of nothingness.
			AddAvoidObject(ctx => StyxWoW.Me.HasAura("Touch of Nothingness"), 8, u => u is WoWPlayer && !u.IsMe);
			// run from nearby players.
			AddAvoidObject(
				ctx => StyxWoW.Me.HasAura("Touch of Nothingness"),
				() => ScriptHelpers.Healer.Location,
				35,
				10,
				u => u.Entry == FragmentOfDoubt && ((WoWUnit)u).CurrentTargetGuid == StyxWoW.Me.Guid);

			return new PrioritySelector(
				ScriptHelpers.CreateDispelGroup("Touch of Nothingness", ScriptHelpers.PartyDispelType.Magic),
				new Decorator(
					ctx =>
					{
						var tank = ScriptHelpers.Tank;
						return Me.IsRange() && tank != null && tank.DistanceSqr > 6 * 6 && !StyxWoW.Me.HasAura("Touch of Nothingness");
					},
					new Action(ctx => Navigator.MoveTo(ScriptHelpers.Tank.Location))));
		}

		#endregion

		#region Wise Mari

		[EncounterHandler(56448, "Wise Mari")]
		public Composite WiseMariEncounter()
		{
			List<WoWPoint> shaResidueLocs = null;
			WoWPoint safeLoc = WoWPoint.Zero;
			float relativeSpoutAngle = 0;
			bool cycledToNearestCircularPoint = false;

			AddAvoidObject(ctx => true, 7f, u => u.Entry == WiseMariId && ((WoWUnit)u).HasAura("Water Bubble"));

			return new PrioritySelector(
				ctx => _wiseMari = ctx as WoWUnit,
				// Phase 1
				new Decorator<WoWUnit>(
					boss => boss.HasAura("Water Bubble"),
					new PrioritySelector(
						ctx =>
						{
							if (cycledToNearestCircularPoint)
								cycledToNearestCircularPoint = false;
							shaResidueLocs = ObjectManager.GetObjectsOfType<WoWDynamicObject>().Where(u => u.Entry == ShaResidueId).Select(u => u.Location).ToList();
							return ctx;
						},
				// tank the corrupt Living waters in the back
						new Decorator(
							ctx => Me.IsTank() && (Targeting.Instance.IsEmpty() || Targeting.Instance.TargetList.All(u => u.CurrentTargetGuid == Me.Guid)),
							new PrioritySelector(
								ctx =>
								safeLoc =
								_wiseMariCorruptLivingTankSpots.OrderBy(l => l.DistanceSqr(Me.Location)).FirstOrDefault(l => !shaResidueLocs.Any(s => s.DistanceSqr(l) < 6 * 6)),
								new Decorator(ctx => Me.Location.DistanceSqr(safeLoc) > 4 * 4, new Action(ctx => Navigator.MoveTo(safeLoc))),
								new Decorator(
									ctx => Me.Location.DistanceSqr(safeLoc) <= 4 * 4 && Me.CurrentTargetGuid > 0 && !Me.CurrentTarget.IsWithinMeleeRange,
									new PrioritySelector(
										new Decorator(ctx => !Me.IsSafelyFacing(Me.CurrentTarget), new Action(ctx => Me.CurrentTarget.Face())), new ActionAlwaysSucceed())))),
				// don't stand in the pools.
						new Decorator(
							ctx =>
							Me.HasAura("Corrupted Waters") &&
							(!Me.IsTank() || (Me.IsTank() && (Targeting.Instance.IsEmpty() || Targeting.Instance.TargetList.All(u => u.CurrentTargetGuid == Me.Guid)))),
							new PrioritySelector(
								ctx =>
								safeLoc =
								WiseMariSafeSpots.OrderBy(l => l.DistanceSqr(Me.IsMelee() && Me.CurrentTargetGuid > 0 ? Me.CurrentTarget.Location : Me.Location))
												 .FirstOrDefault(l => !shaResidueLocs.Any(s => s.DistanceSqr(l) < 6 * 6)),
								new Decorator(ctx => Me.Location.Distance(safeLoc) > Navigator.PathPrecision, new Action(ctx => Navigator.MoveTo(safeLoc))),
								new Decorator(ctx => Me.Location.Distance(safeLoc) <= Navigator.PathPrecision, new Action(ctx => Navigator.PlayerMover.MoveTowards(safeLoc))))))),
				// Phase 2
				new Decorator<WoWUnit>(
					boss => !boss.HasAura("Water Bubble") && _wiseMari.HealthPercent > 1,
					new PrioritySelector(
						ctx =>
						{
							if (!cycledToNearestCircularPoint)
							{
								var myLoc = Me.Location;
								var nearestLoc = _wiseMariCirclePath.OrderBy(l => l.DistanceSqr(myLoc)).FirstOrDefault();
								_wiseMariCirclePath.CycleTo(nearestLoc);
								cycledToNearestCircularPoint = true;
							}
							relativeSpoutAngle = GetRelativeSpoutAngle(_wiseMari);
							return ctx;
						},
				// move closer
						new Decorator(
							ctx => _wiseMari.DistanceSqr > 13 * 13, new Action(ctx => Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(Me.Location, _wiseMari.Location, 8)))),
						new Decorator(
							ctx => (relativeSpoutAngle > 0 && relativeSpoutAngle < 90 || Me.Z < 174.5) && Me.Z > 174,
							new Sequence(
								new Action(
									ctx =>
									{
										var point = _wiseMariCirclePath.Peek();
										Navigator.PlayerMover.MoveTowards(point);
										if (Me.Location.Distance(point) < 3)
											_wiseMariCirclePath.Dequeue();
										var ang = GetRelativeSpoutAngle(_wiseMari);
										// keep moving if standing in water.
										if ((ang < 200 || Me.Z < 174.5) && _wiseMari.IsAlive)
											return RunStatus.Running;
										WoWMovement.MoveStop();
										return RunStatus.Success;
									}),
								new WaitContinue(3, ctx => Me.IsMoving, new ActionAlwaysSucceed()),
								new DecoratorContinue(ctx => !Me.IsMoving, new Action(ctx => _wiseMari.Face())))))),
				// Tank does nothing if there are not targets in target list.
				new Decorator(ctx => StyxWoW.Me.IsTank() && Targeting.Instance.IsEmpty(), new ActionAlwaysSucceed()));
		}

		private float GetRelativeSpoutAngle(WoWUnit wiseMari)
		{
			var firehoseTarget = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == WiseMariFireHoseTargetId);
			var ang = 0f;
			if (firehoseTarget != null)
			{
				var mariToTarget = firehoseTarget.Location - wiseMari.Location;
				var mariAng = (float)Math.Atan2(mariToTarget.Y, mariToTarget.X);
				var mariToMe = Me.Location - wiseMari.Location;
				var angToMe = (float)Math.Atan2(mariToMe.Y, mariToMe.X);
				ang = WoWMathHelper.RadiansToDegrees(mariAng - angToMe);
				if (ang < 0)
					ang = 360 + ang;
			}
			return ang;
		}

		#endregion

		#region Lorewalker Stonestep

		[EncounterHandler(56843, "Lorewalker Stonestep", Mode = CallBehaviorMode.Proximity)]
		public Composite LorewalkerStonestepEncounter()
		{
			var loc = new WoWPoint(844.3649, -2460.1, 174.961);

			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				new Decorator(
					ctx => boss.HasAura("Meditation") && ScriptHelpers.IsBossAlive("Lorewalker Stonestep"),
					new Action(ctx => ScriptHelpers.MarkBossAsDead("Lorewalker Stonestep"))),
				new Decorator(ctx => Me.IsTank() && Targeting.Instance.IsEmpty() && !boss.HasAura("Meditation") && Me.Location.Distance(loc) <= 15, new ActionAlwaysSucceed()));
		}

		#endregion
	}

	#endregion


	#region Heroic Difficulty

	public class Temple_of_the_Jade_Serpent_Heroic : Temple_of_the_Jade_Serpent
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 468; }
		}

		#endregion
	}

	#endregion

}