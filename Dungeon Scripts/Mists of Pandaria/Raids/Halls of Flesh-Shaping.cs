using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Media;
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
using Styx.WoWInternals.World;
using Action = Styx.TreeSharp.Action;
using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
using AvoidanceManager = Bots.DungeonBuddy.Avoidance.AvoidanceManager;

namespace Bots.DungeonBuddy.Dungeon_Scripts.Mists_of_Pandaria
{
	public class HallsOfFleshShaping : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 612; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(7262.557, 5018.036, 76.17107); }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (unit != null) { }
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
					if (unit.Entry == AzureFogId || unit.Entry == AmberFogId || unit.Entry == CrossEyeId ||
						unit.Entry == LivingFluidId && unit.CanSelect && unit.Attackable && !Me.HasAura("Fully Mutated"))
					{
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
					if (unit.Entry == AzureFogId || unit.Entry == AmberFogId || unit.Entry == CrossEyeId)
						priority.Score += 10000;
					if (unit.Entry == LivingFluidId)
					{
						if (Me.IsDps() && !Me.HasAura("Fully Mutated"))
							priority.Score = 100000 - unit.DistanceSqr;
						else priority.Score -= 10000;
					}
					else if (unit.Entry == PrimordiusId && Me.HasAura("Fully Mutated"))
						priority.Score += 10000;
				}
			}
		}

		public override void OnEnter()
		{
			if (Me.IsTank())
			{
				Alert.Show(
					"Tanking Not Supported",
					string.Format(
						"Tanking is not supported in the {0} script. If you wish to stay in raid and play manually then press 'Continue'. Otherwise you will automatically leave raid.",
						Name),
					30,
					true,
					true,
					null,
					() => Lua.DoString("LeaveParty()"),
					"Continue",
					"Leave");
			}
			else
			{
				Alert.Show(
					"Do Not AFK",
					"It is highly recommended you do not afk while in a raid and be prepared to intervene if needed in the event something goes wrong or you're asked to perform a certain task.",
					20,
					true,
					false,
					null,
					null,
					"Ok");
			}
		}

		public override MoveResult MoveTo(WoWPoint location)
		{
			// prevent CR or any other behavior from moving when doing maze ebent at Duruma The Forgotten.
			if (_durumu != null && _durumu.IsValid && _durumu.IsAlive && _doingMazeWalk && location != _mazeMoveto)
			{
				return MoveResult.Moved;
			}
			return base.MoveTo(location);
		}

		#endregion

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		#region Root

		[EncounterHandler(0)]
		public Composite RootEncounter()
		{
			return new PrioritySelector();
		}

		#endregion

		#region Durumu the Forgotten

		// Start of Attackable NPCs
		private const uint DurumuTheForgottenId = 68036;
		private const uint CrimsonFogId = 69050;
		private const uint AmberFogId = 69051;
		private const uint AzureFogId = 69052;

		// End of Attackable NPCs

		private const uint CrossEyeId = 67857;

		//  Start of Area Triggers
		private const uint LingeringGazeId = 133793;

		private const uint WholeRoomSlice1Id = 136232;
		private const uint WholeRoomSlice13Id = 136244;


		private const uint AcidicSplashId = 137214;
		private const uint AcidicSplash2Id = 137216;

		private const int LingeringGazePrecastAuraId = 134626;

		private const uint DisintegrationBeamTarget_Clockwise_Id = 69097;
		private const uint DisintegrationEyebeamTarget_CounterClockwise_Id = 67882;

		private const uint RedEyebeamTargetId = 67851;
		private const uint BlueEyebeamTargetId = 67829;
		private const uint YellowBeamTargetId = 67852;
		/*
		 Bright Light Distance: 26.2281551361084, Type:Object, AreaTrigger, const uint BrightLightId = 133740;
		Bright Light Distance: 26.2281551361084, Type:Object, AreaTrigger, const uint BrightLightId = 139129;
		Infrared Light Distance: 26.483081817627, Type:Object, AreaTrigger, const uint InfraredLightId = 139128;
		Infrared Light Distance: 26.483081817627, Type:Object, AreaTrigger, const uint InfraredLightId = 133734;
		Blue Rays Distance: 26.7508659362793, Type:Object, AreaTrigger, const uint BlueRaysId = 139126;
		Blue Rays Distance: 26.7508659362793, Type:Object, AreaTrigger, const uint BlueRaysId = 133672;
		 */

		//  End of Area Triggers

		private const float DurumuRoomRadius = 64;
		private const float DurumuPieRadius = 66;
		private const float DurumuPiePieceWidth = DurumuPieRadius / 13;

		private readonly WoWPoint _durumuRoomCenter = new WoWPoint(5895.542, 4512.609, -2.516495);
		bool _doingMazeWalk;
		WoWPoint _mazeMoveto = WoWPoint.Zero;
		WoWUnit _durumu;

		[EncounterHandler(68036, "Durumu the Forgotten")]
		public Composite DurumuTheForgottenEncounter()
		{
			WoWUnit redEyeTarget = null;
			WoWUnit blueEyeTarget = null;
			WoWUnit yellowEyeTarget = null;
			WoWUnit DisintegrationBeamTarget = null;

			WoWPoint hiddenAmberFogLoc = WoWPoint.Zero;
			WoWPoint hiddenAzureFogLoc = WoWPoint.Zero;
			WoWPoint hiddenCrimsonFogLoc = WoWPoint.Zero;
			WoWPoint beamMoveto = WoWPoint.Zero;


			var lingeringGazeDropTimer = new WaitTimer(TimeSpan.FromSeconds(1));
			var clockwiseStartLoc = new WoWPoint(5895.542, 4488.609, -2.516495);
			var counterClockwiseStartLoc = new WoWPoint(5895.542, 4536.609, -2.516495);

			// Closures FTW.
			Func<WoWPoint> getMyBeamTargetLoc = () =>
													{
														if (Me.HasAura("Blue Beam Precast") || Me.HasAura("Blue Ray Tracking"))
															return hiddenAzureFogLoc;
														if (Me.HasAura("Red Beam Precast") || Me.HasAura("Infrared Tracking"))
															return hiddenCrimsonFogLoc;
														if (Me.HasAura("Yellow Beam Precast"))
															return hiddenAmberFogLoc;
														return WoWPoint.Zero;
													};

			Func<WoWPoint> getColoredBeamMoveToLocation = () =>
															  {
																  var beamTarget = getMyBeamTargetLoc();
																  if (beamTarget == WoWPoint.Zero)
																	  return WoWPoint.Zero;
																  var startLoc = WoWMathHelper.CalculatePointFrom(beamTarget, _durumu.Location, 10);
																  var endLoc = WoWMathHelper.CalculatePointFrom(beamTarget, _durumu.Location, 40);
																  // Note: possible improvement would be to assign a beam for bot to stand in to split damange. 
																  // This is not high prior and not needed in LFR though.
																  return Me.Location.GetNearestPointOnSegment(startLoc, endLoc);
															  };

			Func<WoWPoint> getMazeMoveToLocation = () =>
													   {
														   var bestLoc = WoWPoint.Zero;
														   var beamTarget = DisintegrationBeamTarget;

														   if (beamTarget == null)
														   {
															   if (_doingMazeWalk)
																   _doingMazeWalk = false;
															   return WoWPoint.Zero;
														   }

														   if (!_doingMazeWalk)
															   _doingMazeWalk = true;

														   var clockwise = beamTarget.Entry == DisintegrationBeamTarget_Clockwise_Id;

														   var areaTriggers =
															   ObjectManager.GetObjectsOfTypeFast<WoWAreaTrigger>()
																			.Where(a => a.SpellId >= WholeRoomSlice1Id && a.SpellId <= WholeRoomSlice13Id)
																			.ToArray();

														   if (areaTriggers.Any())
														   {
															   // angle from beam to moveTo spot.
															   var bestDegree = GetBestPieAngle(areaTriggers, _durumu, clockwise);

															   Logger.Write("BestDegree: {0}", bestDegree);
															   var pieSlicePieces =
																   areaTriggers.Where(a => GetAreaTriggerDegree(a) == bestDegree)
																			   .OrderBy(a => a.SpellId)
																			   .Select(a => (uint)a.SpellId - WholeRoomSlice1Id)
																			   .ToList();

															   var gapLocations = GetPieSliceGapLocations(pieSlicePieces, _durumuRoomCenter, bestDegree);
															   var myLoc = Me.Location;
															   bestLoc = gapLocations.OrderBy(l => myLoc.DistanceSqr(l)).FirstOrDefault();
														   }
														   return bestLoc == WoWPoint.Zero ? (clockwise ? clockwiseStartLoc : counterClockwiseStartLoc) : bestLoc;
													   };

			Func<bool> shouldStandIdleAtMyBeam = () =>
													 {
														 if (Targeting.Instance.IsEmpty() || Me.IsHealer())
															 return false;
														 var dpsTarget = Targeting.Instance.FirstUnit;
														 var maxDpsRange = MyMaxRangeForUnit(dpsTarget);
														 if (Me.IsRange() && dpsTarget.Distance > maxDpsRange)
															 return true;
														 // if melee and dps target is near my beam then go dps it instead of idling.
														 if (Me.IsMelee())
														 {
															 var beamTarget = getMyBeamTargetLoc();
															 var startLoc = WoWMathHelper.CalculatePointFrom(beamTarget, _durumu.Location, 10);
															 var endLoc = WoWMathHelper.CalculatePointFrom(beamTarget, _durumu.Location, 40);
															 return dpsTarget.Location.GetNearestPointOnSegment(startLoc, endLoc).Distance(dpsTarget.Location) > maxDpsRange;
														 }
														 return false;
													 };

			AddAvoidObject(ctx => !_doingMazeWalk, 4, o => o is WoWAreaTrigger && ((WoWAreaTrigger)o).SpellId == LingeringGazeId);
			return new PrioritySelector(
				ctx =>
				{
					_durumu = ctx as WoWUnit;
					var units = ObjectManager.GetObjectsOfType<WoWUnit>();
					WoWUnit amberFog = units.FirstOrDefault(u => u.Entry == AmberFogId);
					WoWUnit azureFog = units.FirstOrDefault(u => u.Entry == AzureFogId);
					WoWUnit crimsonFog = units.FirstOrDefault(u => u.Entry == CrimsonFogId);

					redEyeTarget = units.FirstOrDefault(u => u.Entry == RedEyebeamTargetId);
					blueEyeTarget = units.FirstOrDefault(u => u.Entry == BlueEyebeamTargetId);
					yellowEyeTarget = units.FirstOrDefault(u => u.Entry == YellowBeamTargetId);

					DisintegrationBeamTarget =
						units.FirstOrDefault(u => u.Entry == DisintegrationBeamTarget_Clockwise_Id || u.Entry == DisintegrationEyebeamTarget_CounterClockwise_Id);

					// cache the fog locations while they are visible..
					if (amberFog != null) //&& amberFog.HasAura("Yellow Fog Beast Invisibility"))
						hiddenAmberFogLoc = amberFog.Location;
					if (azureFog != null) // && azureFog.HasAura("Blue Fog Beast Invisibility"))
						hiddenAzureFogLoc = azureFog.Location;
					if (crimsonFog != null) // && crimsonFog.HasAura("Red Fog Beast Invisibility"))
						hiddenCrimsonFogLoc = crimsonFog.Location;

					_mazeMoveto = getMazeMoveToLocation();
					beamMoveto = getColoredBeamMoveToLocation();

					return _durumu;
				},
				// handle maze movement.
				new Decorator(
					ctx => _mazeMoveto != WoWPoint.Zero,
					new PrioritySelector(new Decorator(ctx => Me.Location.Distance(_mazeMoveto) > 4, new Action(
						ctx =>
						{
							Navigator.Clear();
							Navigator.MoveTo(_mazeMoveto);
						})))),
				// Handle beam movement
				new Decorator(
					ctx => beamMoveto != WoWPoint.Zero,
					new PrioritySelector(
						new Decorator(ctx => Me.Location.Distance(beamMoveto) > 4, new Action(ctx => Navigator.MoveTo(beamMoveto))),
				// return succeed if my to-kill target is out of dps range to prevent CR from moving 
						new Decorator(ctx => shouldStandIdleAtMyBeam(), new ActionAlwaysSucceed()))),
				// drop lingering gaze on the outside edge. 
				// the debuf drops before the spell hits the ground area so bot needs to hang around a little longer. 
				new Decorator(
					ctx => Me.HasAura(LingeringGazePrecastAuraId) || !lingeringGazeDropTimer.IsFinished,
					new PrioritySelector(
						ctx => GetLingeringGazeDropPoint(),
						new Helpers.Action<WoWPoint>(
							dropPoint =>
							{
								if (Me.HasAura(LingeringGazePrecastAuraId))
									lingeringGazeDropTimer.Reset();
								Navigator.MoveTo(dropPoint);
							}))));
		}

		private float GetBestPieAngle(IEnumerable<WoWAreaTrigger> areaTriggers, WoWUnit boss, bool clockwise)
		{
			var bossAngleDegree = WoWMathHelper.RadiansToDegrees(WoWMathHelper.NormalizeRadian(boss.Rotation));

			// angle from beam to moveTo spot.
			var bestAngle = (from areaTrigger in areaTriggers
							 let ang = GetAreaTriggerDegree(areaTrigger)
							 // get angle difference from boss to areaTrigger. Handles amgle wrapping.
							 let angleToBossDiff = GetBossPieAngleDifference(bossAngleDegree, ang, clockwise)
							 where angleToBossDiff < 180
							 orderby angleToBossDiff descending
							 select ang).FirstOrDefault();
			return bestAngle;
		}

		private float GetBossPieAngleDifference(float bossAngle, float pieAngle, bool clockwise)
		{
			if (clockwise)
				return bossAngle < pieAngle ? 360 + bossAngle - pieAngle : bossAngle - pieAngle;
			return pieAngle < bossAngle ? 360 + pieAngle - bossAngle : pieAngle - bossAngle;
		}

		private List<WoWPoint> GetPieSliceGapLocations(List<uint> pieSlicePieces, WoWPoint centerPoint, float degree)
		{
			var locations = new List<WoWPoint>();
			var radians = WoWMathHelper.DegreesToRadians(degree);

			bool counting = false;
			uint startIndex = 0;
			for (uint i = 0; i <= 13; i++)
			{
				bool containsPiece = pieSlicePieces.Contains(i);
				if (!containsPiece && !counting)
				{
					counting = true;
					startIndex = i;
				}
				else if (counting && (containsPiece || i == 13))
				{
					float missingCnt = i - startIndex;
					float distToGapMiddle = (startIndex + (missingCnt / 2f)) * DurumuPiePieceWidth;
					var loc = centerPoint.RayCast(radians, distToGapMiddle);
					locations.Add(loc);
					counting = false;
				}
			}
			return locations;
		}

		private float GetAreaTriggerDegree(WoWAreaTrigger areaTrigger)
		{
			var rawRad = StyxWoW.Memory.Read<float>(areaTrigger.BaseAddress + 0x108);
			float rad = WoWMathHelper.NormalizeRadian(rawRad);
			return WoWMathHelper.RadiansToDegrees(rad);
		}

		private float MyMaxRangeForUnit(WoWUnit unit)
		{
			if (Me.IsMelee())
				return unit.MeleeRange();
			return unit.CombatReach + 35;
		}

		private WoWPoint GetLingeringGazeDropPoint()
		{
			var myLoc = Me.Location;
			return
				GetPointsAroundCircle(_durumuRoomCenter, DurumuRoomRadius, 10)
					.OrderBy(l => l.DistanceSqr(myLoc))
					.FirstOrDefault(l => !AvoidanceManager.Avoids.Any(a => a.IsPointInAvoid(l)));
		}

		private IEnumerable<WoWPoint> GetPointsAroundCircle(WoWPoint centerLoc, float radius, float stepDegree)
		{
			const float pix2 = (float)(Math.PI * 2);
			var stepRadian = WoWMathHelper.DegreesToRadians(stepDegree);
			for (float ang = 0; ang < pix2; ang += stepRadian)
			{
				yield return centerLoc.RayCast(ang, radius);
			}
		}

		#endregion

		#region Primordius

		private const uint PrimordiusId = 69017;
		private const uint MutagenicPoolId = 136049;
		private const uint LivingFluidId = 69069;
		private const uint VolatilePoolId = 140506;

		[EncounterHandler(69017, "Primordius")]
		public Composite PrimordiusEncounter()
		{
			WoWUnit boss = null;
			WoWAreaTrigger pool = null;
			AddAvoidObject(ctx => true, 4, o => o.Entry == LivingFluidId && o.ToUnit().HasAura("Volatile Pool"));
			return new PrioritySelector(
				ctx =>
				{
					pool =
						ObjectManager.GetObjectsOfType<WoWAreaTrigger>()
									 .Where(a => a.SpellId == MutagenicPoolId && a.Distance < 30)
									 .OrderBy(a => a.DistanceSqr)
									 .FirstOrDefault();
					return boss = ctx as WoWUnit;
				},
				new Decorator(
					ctx => Me.IsDps() && !Me.HasAura("Fully Mutated") && pool != null && pool.Distance > Navigator.PathPrecision,
					new Action(ctx => Navigator.MoveTo(pool.Location))));
		}

		#endregion

		#region Dark Animus

		private const uint LargeAnimaGolemId = 69700;
		private const uint CrimsonWakeId = 69951;

		[EncounterHandler(69427, "Dark Animus", Mode = CallBehaviorMode.Proximity, BossRange = 100)]
		public Composite DarkAnimusEncounter()
		{
			WoWUnit boss = null;
			AddAvoidObject(
				ctx => true,
				() => ScriptHelpers.Tank.Location,
				50,
				o =>
				{
					var range = 8;
					if (Me.HasAura("Fixate") && Me.Auras["Fixate"].CreatorGuid == o.Guid)
						range += 7;
					if (Me.IsRange() && Me.IsMoving)
						range += 5;
					return range;
				},
				CrimsonWakeId);
			return new PrioritySelector(ctx => boss = ctx as WoWUnit);
		}

		#endregion
	}
}