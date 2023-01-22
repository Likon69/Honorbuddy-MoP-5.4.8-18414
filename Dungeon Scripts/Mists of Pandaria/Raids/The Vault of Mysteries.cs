
using System;
using System.Collections.Generic;
using System.Linq;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
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
	public class TheVaultOfMysteries : Dungeon
	{
		#region Overrides of Dungeon

		private const float ElegonInnerCircleRadius = 43;// 46 old..
		private const float ElegonOuterCircleRadius = 63;
		private readonly WoWPoint _elegonRoomCenterLoc = new WoWPoint(4023.042, 1907.786, 358.7884); // inner radius 46. outer radius 63
		private readonly WoWPoint _elegonRoomEntranceLoc = new WoWPoint(4035.819, 1861.859, 358.7884);

		public override uint DungeonId
		{
			get { return 528; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(3978.82, 1115.574, 497.136); }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (unit != null)
					{
						if (unit.Entry == ElegonId && !IsPlatformWalkable)
							return true;
						if (_elegon != null && _elegon.IsValid && _elegon.Combat && Me.IsMelee() && _elegon.IsSafelyFacing(unit, 180) && !_elegon.HasAura("Draw Power") &&
							IsPlatformWalkable)
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
					if (unit.Entry == PinningArrowId && Me.IsRange())
						outgoingunits.Add(unit);

					else if (unit is WoWPlayer && unit.HasAura("Maddening Shout"))
						outgoingunits.Add(unit);

					else if (unit.Entry == UndyingShadowsId && Me.IsRange())
						outgoingunits.Add(unit);

					else if (unit.Entry == EmpyrealFocusId && unit.CanSelect)
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
					if (unit.Entry == PinningArrowId && Me.IsRange())
						priority.Score += 1000;

					else if (unit.Entry == UndyingShadowsId && Me.IsRange())
						priority.Score += 1000;

					else if (unit is WoWPlayer)
						priority.Score += 1000;

					else if (unit.Entry == EnergyChargeId)
						priority.Score = 2000 - unit.Distance;

					else if (unit.Entry == EmpyrealFocusId)
					{
						priority.Score = 10000 - unit.Distance;
					}

					else if (unit.Entry == CelestialProtectorId)
						priority.Score += 1000;

					else if (unit.Entry == EmperorsCourageId && (unit.Distance <= 50 || !Me.IsMelee()))
						priority.Score += 50000;

					else if (_emperorsStrengthIds.Contains(unit.Entry) && (Me.IsMelee() && unit.Distance <= 50 || !Me.IsMelee()))
						priority.Score += 4000;

					else if (unit.Entry == EmperorsRageId && (Me.IsMelee() && unit.Distance <= 50 || !Me.IsMelee()))
						priority.Score += 3000;
					else if (unit.Entry == QinxiId || unit.Entry == JanxiId)
						priority.Score = 800 - unit.Distance;
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

		private WoWPoint _lastLoc;
		WaitTimer _stuckTimer = new WaitTimer(TimeSpan.FromSeconds(2));
		public override MoveResult MoveTo(WoWPoint location)
		{
			var myLoc = Me.Location;
			var destDistFromElegonCenter = location.Distance(_elegonRoomCenterLoc);
			var myDistFromElegonCenter = myLoc.Distance(_elegonRoomCenterLoc);
			// check if we want to move onto the inner circle area.
			if (destDistFromElegonCenter <= ElegonInnerCircleRadius)
			{
				if (myDistFromElegonCenter > ElegonOuterCircleRadius)
					return Navigator.MoveTo(_elegonRoomEntranceLoc);
				AntiStuck(myLoc);
				if (myDistFromElegonCenter <= ElegonInnerCircleRadius + 2)
				{
					Navigator.PlayerMover.MoveTowards(location);
					return MoveResult.Moved;
				}
				var moveTo = GetElegonInnerCircleMovetoPoints(ElegonInnerCircleRadius).OrderBy(l => l.DistanceSqr(myLoc)).FirstOrDefault();
				return Navigator.MoveTo(moveTo);
			}
			if (myDistFromElegonCenter <= ElegonInnerCircleRadius)
			{
				var moveTo = GetElegonInnerCircleMovetoPoints(ElegonInnerCircleRadius + 2).OrderBy(l => l.DistanceSqr(location)).FirstOrDefault();
				Navigator.PlayerMover.MoveTowards(moveTo);
				AntiStuck(myLoc);
				return MoveResult.Moved;
			}
			return base.MoveTo(location);
		}

		void AntiStuck(WoWPoint myLocation)
		{
			if (_stuckTimer.IsFinished)
			{
				if (myLocation.Distance(_lastLoc) <= 2)
				{
					WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
					WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend);
				}
				_lastLoc = myLocation;
				_stuckTimer.Reset();
			}
		}

		public override bool CanNavigateFully(WoWPoint from, WoWPoint to)
		{
			var myLoc = Me.Location;
			return ((from.Distance(myLoc) <= ElegonInnerCircleRadius + 2 || to.Distance(myLoc) <= ElegonInnerCircleRadius + 2) && IsPlatformWalkable) ||
				   base.CanNavigateFully(from, to);
		}

		private IEnumerable<WoWPoint> GetElegonInnerCircleMovetoPoints(float dist)
		{
			//const float pix2 = (float)(Math.PI * 2);
			for (float ang = 0; ang < 360; ang += 40)
			{
				var radians = WoWMathHelper.DegreesToRadians(ang);
				yield return _elegonRoomCenterLoc.RayCast(radians, dist);
			}
		}

		#endregion

		private const uint FlankingMoguId = 60847;
		private const uint PinningArrowId = 60958;

		private const uint MengTheDementedId = 60708;
		private const uint QiangTheMerciless = 60709;
		private const uint SubetaiTheSwift = 60710;
		private const uint ZianOfTheEndlessShadow = 60701;


		private const uint EmperorsRageId = 60396;
		private const uint EmperorsCourageId = 60398;

		private const uint UndyingShadowsId = 60731;
		private const uint EmpyrealFocusId = 60776; // kill these in stage 3.
		private const uint CelestialProtectorId = 60793;
		private const uint EnergyChargeId = 60913; // kill these before they leave the Energy Vortext.
		private const uint ElegonId = 60410;
		private const uint QinxiId = 60399;
		private const uint JanxiId = 60400;
		private readonly uint[] _emperorsStrengthIds = new uint[] { 60397, 60768 };

		private readonly uint[] _spiritKingIds = new[] { MengTheDementedId, QiangTheMerciless, SubetaiTheSwift, ZianOfTheEndlessShadow };
		private readonly int[] _volleyIds = new[] { 118094, 118105, 118106 };

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0)]
		public Composite RootEncounter()
		{
			return new PrioritySelector();
		}

		// 3rd boss 
		//new WoWPoint (3874.398, 1549.394, 362.1991); // radius 50.
		[LocationHandler(3874.398, 1549.394, 362.1991, 100, "Will Of The Emperor")]
		public Composite WillOfTheEmperorEncounter()
		{
			var rangeGroupCenter = WoWPoint.Zero;
			var roomCenterLoc = new WoWPoint(3874.398, 1549.394, 362.1991);

			AddAvoidObject(
				ctx => true, () => roomCenterLoc, 50, o => Me.IsMoving && Me.IsRange() ? 30 : 20, u => u.Entry == EmperorsRageId && u.ToUnit().CurrentTargetGuid == Me.Guid);
			AddAvoidObject(ctx => Me.IsRange() && !Me.IsCasting, () => roomCenterLoc, 50, 25, o => (o.Entry == QinxiId || o.Entry == JanxiId) && o.ToUnit().Combat && !o.ToUnit().IsMoving);
			return new PrioritySelector(

				// Commented out because stacking up on raid doesn't work well when rest of raid isn't stacking...
				//ctx => boss = ctx as WoWUnit,
				//new Decorator(
				//    ctx =>
				//    {
				//        if (!Me.HasAura("Titan Gas") || !Me.IsRange())
				//            return false;
				//        rangeGroupCenter = ScriptHelpers.GetGroupCenterLocation(m => m.IsRange, 15);
				//        return rangeGroupCenter.Distance(Me.Location) > 5;
				//    },
				//    new Action(ctx => Navigator.MoveTo(rangeGroupCenter)))
				);
		}

		#region The Spirit Kings

		[LocationHandler(4227.072, 1595.776, 438.8339, 40, "The Spirit Kings")]
		public Composite TheSpiritKingsEncounter()
		{
			var roomCenterLoc = new WoWPoint(4227.072, 1595.776, 438.8339);
			const int annihilateId = 117948;
			var leftDoorSideloc = new WoWPoint(4190.638, 1580.247, 438.804);
			var rightDoorSideloc = new WoWPoint(4195.618, 1568.984, 441.4225);
			var insideGateLoc = new WoWPoint(4197.687, 1576.985, 438.8339);
			WoWPoint flankingMoguCenterLoc = WoWPoint.Zero;

			// dodge flank.
			AddAvoidObject(
				ctx => flankingMoguCenterLoc != WoWPoint.Zero && flankingMoguCenterLoc.Distance(Me.Location) <= 50,
				() => roomCenterLoc,
				31,
				16,
				// 14.25f,
				o => o.Entry == FlankingMoguId && o.ToUnit().HasAura("Overhand Strike"),
				o =>
				{
					var mogu = (WoWUnit)o;
					var rotation = WoWMathHelper.NormalizeRadian(mogu.Rotation);
					return Me.Location.GetNearestPointOnSegment(flankingMoguCenterLoc, flankingMoguCenterLoc.RayCast(rotation, 60));
				});

			AddAvoidObject(ctx => true, () => roomCenterLoc, 31, o => o.ToUnit().CurrentTargetGuid == Me.Guid ? ( Me.IsRange() && Me.IsMoving ? 17 : 12) : 9, o => o.Entry == UndyingShadowsId);

			WoWUnit activeBoss = null;

			return new PrioritySelector(
				new Decorator(
					ctx => Me.Combat,
					new PrioritySelector(
						ctx =>
						{
							var flankingMogusLocation =
								ObjectManager.GetObjectsOfType<WoWUnit>()
											 .Where(u => u.Entry == FlankingMoguId && u.HasAura("Overhand Strike"))
											 .Select(u => u.Location).ToArray();

							// get the average location. 
							if (flankingMogusLocation.Any())
								flankingMoguCenterLoc = flankingMogusLocation.Aggregate((a, b) => a + b) / flankingMogusLocation.Length;
							else
								flankingMoguCenterLoc = WoWPoint.Zero;

							return activeBoss = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => _spiritKingIds.Contains(u.Entry) && u.Attackable && u.CanSelect);
						},
				// move inside the room to avoid getting locked out when encounter starts.
						new Decorator(
							ctx =>
							{
								var tank = ScriptHelpers.Tank;
								if (Me.Combat || tank == null) return false;
								return tank.Z > 430 && tank.Location.IsPointLeftOfLine(leftDoorSideloc, rightDoorSideloc) &&
									   !Me.Location.IsPointLeftOfLine(leftDoorSideloc, rightDoorSideloc);
							},
							new Action(ctx => Navigator.MoveTo(insideGateLoc))),
						new Decorator(
							ctx => activeBoss != null && activeBoss.Entry == QiangTheMerciless,
							new PrioritySelector(
				// stack on tank when Qiang is active to split damge from Massive Attacks
								new Decorator(
									ctx =>
									activeBoss.CastingSpellId != annihilateId && activeBoss.CurrentTargetGuid > 0 && activeBoss.CurrentTarget.Distance > 10 &&
									!AvoidanceManager.IsRunningOutOfAvoid,
									new Action(ctx => Navigator.MoveTo(activeBoss.CurrentTarget.Location))),
				// avoid getting hit by annihilate 
								ScriptHelpers.CreateAvoidUnitAnglesBehavior(
									ctx => activeBoss.CastingSpellId == annihilateId && activeBoss.Distance <= 20, ctx => activeBoss, new ScriptHelpers.AngleSpan(0, 180)))),
				// Avoid getting hit by volley.
						new Decorator(
							ctx => activeBoss != null && activeBoss.Entry == SubetaiTheSwift,
							ScriptHelpers.CreateAvoidUnitAnglesBehavior(
								ctx => _volleyIds.Contains(activeBoss.CastingSpellId), ctx => activeBoss, new ScriptHelpers.AngleSpan(0, 100))))));
		}

		#endregion

		#region Elegon

		private const uint EnergyPlatformId = 213526;
		private readonly WaitTimer _platformDespawningTimer = new WaitTimer(TimeSpan.FromSeconds(30));
		private readonly WaitTimer _slatformSolidifyTimer = new WaitTimer(TimeSpan.FromSeconds(5));
		private readonly WaitTimer _updatePlatformStatusTimer = new WaitTimer(TimeSpan.FromSeconds(1));
		private WoWUnit _elegon;

		private bool _isPlatformWalkable;

		private bool IsPlatformWalkable
		{
			get
			{
				if (_updatePlatformStatusTimer.IsFinished)
				{
					WoWGameObject platform;
					if (!_platformDespawningTimer.IsFinished)
					{
						_isPlatformWalkable = false;
					}
					else if (_elegon != null && _elegon.IsValid && _elegon.IsAlive && _elegon.HasAura("Restart Engine"))
					{
						_platformDespawningTimer.Reset();
						_isPlatformWalkable = false;
					}
					else if ((platform = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(g => g.Entry == EnergyPlatformId)) != null &&
							 platform.State == WoWGameObjectState.Active)
					{
						_isPlatformWalkable = false;
					}
					else
					{
						_isPlatformWalkable = platform != null;
					}
					_updatePlatformStatusTimer.Reset();
				}
				return _isPlatformWalkable;
			}
		}

		[EncounterHandler(60410, "Elegon")]
		public Composite ElegonEncounter()
		{
			// 2nd boss
			const int energyCascadeId = 122199; // Avoid this missile, does 5 yard aoe damge at impact location.
			const uint empyrealFocusId = 60776; // kill these in stage 3.

			var roomCenterLoc = new WoWPoint(4023.042, 1907.786, 358.7884);

			AddAvoidLocation(ctx => true, 8, o => ((WoWMissile)o).ImpactPosition, () => WoWMissile.InFlightMissiles.Where(m => m.SpellId == energyCascadeId));
			AddAvoidLocation(ctx => !IsPlatformWalkable, ElegonInnerCircleRadius + 2, o => _elegonRoomCenterLoc);
			AddAvoidObject(
				ctx => true,
				2,
				o => o.Entry == empyrealFocusId && o.ToUnit().CanSelect,
				o =>
				{
					var loc = o.Location;
					return Me.Location.GetNearestPointOnSegment(loc, loc.RayCast(WoWMathHelper.NormalizeRadian(o.Rotation), 15));
				});
			return new PrioritySelector(
				ctx => _elegon = ctx as WoWUnit,
				// move off the platform when in phase 3.
				new Decorator(
					ctx => !IsPlatformWalkable && _elegonRoomCenterLoc.Distance(Me.Location) < ElegonInnerCircleRadius && Me.TransportGuid > 0,
					new Action(
						ctx => Navigator.MoveTo(GetElegonInnerCircleMovetoPoints(ElegonInnerCircleRadius + 2).OrderBy(l => l.DistanceSqr(Me.Location)).FirstOrDefault()))),
				// move on the platform if in phase 1 and 2.
				new Decorator(
					ctx =>
					IsPlatformWalkable && Targeting.Instance.FirstUnit == _elegon && _elegon.Distance2D > (_elegon.HealthPercent < 50 ? 10 : ElegonInnerCircleRadius - 10),
					new Action(ctx => Navigator.MoveTo(_elegonRoomCenterLoc))),
				// stay behind boss during phase 1 to aboid getting hit by Celestial Breath.
				new Decorator(
					ctx => IsPlatformWalkable && _elegon.IsSafelyFacing(Me, 180) && !_elegon.HasAura("Draw Power"),
					new Action(ctx => Navigator.MoveTo(WoWMathHelper.CalculatePointBehind(_elegon.Location, WoWMathHelper.NormalizeRadian(_elegon.Rotation), 6)))),
				//new Decorator(ctx => !Me.IsMoving && Me.CurrentTargetGuid > 0 && !Me.IsSafelyFacing(Me.CurrentTarget), new Action(ctx => Me.CurrentTarget.Face())),
				new Decorator(ctx => !Me.IsHealer() && Targeting.Instance.IsEmpty(), new ActionAlwaysSucceed()));
		}

		#endregion
	}
}