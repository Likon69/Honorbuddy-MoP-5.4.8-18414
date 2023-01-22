using System;
using System.Collections.Generic;
using System.Linq;
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
using Tripper.MeshMisc;
using Tripper.Navigation;
using Tripper.RecastManaged.Detour;
using Action = Styx.TreeSharp.Action;
using Vector3 = Tripper.Tools.Math.Vector3;

using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
using AvoidanceManager = Bots.DungeonBuddy.Avoidance.AvoidanceManager;

namespace Bots.DungeonBuddy.Dungeon_Scripts.Mists_of_Pandaria
{
	public class LastStandOfTheZandalari : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 610; }
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
					if (unit != null)
					{
						// don't kill these while they're on bridge..
						if (unit.Entry == TormentedSpiritId && unit.Z <= 114.5f)
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
					if (unit.Entry == HorridonId)
						priority.Score -= 10000;
					else if (unit.Entry == ZandalariDinomancerId)
						priority.Score += 10000;
					else if (_tier1KillpriorityIds.Contains(unit.Entry))
						priority.Score += 9000;
					else if (_shadowedLoaSpiritIds.Contains(unit.Entry))
						priority.Score += 100000;
					else if (unit.Entry == BlessedLoaSpiritId)
						priority.Score += 90000;
					else if (unit.HasAura("Possessed"))
						priority.Score += 400;
				}
			}
		}

		public override void OnEnter()
		{
			_entranceTrash = new List<DynamicBlackspot>
							{
								new DynamicBlackspot(ShouldAvoidLeftSide, () => leftsideLoc1, LfgDungeon.MapId, 7, 20, "Left Entrance Steps"),
								new DynamicBlackspot(ShouldAvoidLeftSide, () => leftsideLoc2, LfgDungeon.MapId, 7, 20 ),
								new DynamicBlackspot(ShouldAvoidLeftSide, () => leftsideLoc3, LfgDungeon.MapId, 10, 20),

								new DynamicBlackspot(ShouldAvoidRightSide, () => rightsideLoc1, LfgDungeon.MapId, 7, 20, "Right Entrance Steps"),
								new DynamicBlackspot(ShouldAvoidRightSide, () => rightsideLoc2, LfgDungeon.MapId, 7, 20),
								new DynamicBlackspot(ShouldAvoidRightSide, () => rightsideLoc3, LfgDungeon.MapId, 10, 20)
							};

			DynamicBlackspotManager.AddBlackspots(_entranceTrash);

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

		public override void OnExit()
		{
			DynamicBlackspotManager.RemoveBlackspots(_entranceTrash);
			_entranceTrash = null;
		}

		#region Bridge

		private readonly WoWPoint _northSouthBridgeEnd = new WoWPoint(5477.297, 6263.105, 112.1032);
		private readonly WoWPoint _northSouthBridgeStart = new WoWPoint(5640.991, 6263.309, 112.0668);
		readonly WoWPoint _eastWestBridgeStart = new WoWPoint(5431.373, 6217.634, 112.0471);
		readonly WoWPoint _eastWestBridgeEnd = new WoWPoint(5431.382, 6053.903, 112.053);

		private WaitTimer bridgeCtmTimer = new WaitTimer(TimeSpan.FromSeconds(1));

		public override MoveResult MoveTo(WoWPoint location)
		{
			var myLoc = Me.Location;
			if (Me.HasAura("Dark Winds") && myLoc.X >= 5475 && myLoc.X <= 5645)
			{
				var nearestPointOnPath = myLoc.GetNearestPointOnSegment(_northSouthBridgeStart, _northSouthBridgeEnd);
				if (nearestPointOnPath.Distance(myLoc) > 3)
				{
					if (bridgeCtmTimer.IsFinished)
					{
						var endloc = location.X < 5641 ? _northSouthBridgeEnd : _northSouthBridgeStart;
						var clickPoint = WoWMathHelper.CalculatePointFrom(endloc, nearestPointOnPath, 10);
						Navigator.PlayerMover.MoveTowards(clickPoint);
						bridgeCtmTimer.Reset();
					}
					return MoveResult.Moved;
				}
				else
				{

				}
			}
			return base.MoveTo(location);
		}

		#endregion

		#endregion

		private const uint TormentedSpiritId = 70341;
		private const uint SpiritFlayerId = 70246;

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		#region Root

		private List<DynamicBlackspot> _entranceTrash;
		private readonly WoWPoint leftsideLoc1 = new WoWPoint(5914.103, 6497.358, 100);
		private readonly WoWPoint leftsideLoc2 = new WoWPoint(5915.649, 6567.077, 110);
		private readonly WoWPoint leftsideLoc3 = new WoWPoint(5924.17, 6465.354, 118.1056);

		private readonly WoWPoint rightsideLoc1 = new WoWPoint(5868.801, 6570.834, 100);
		private readonly WoWPoint rightsideLoc2 = new WoWPoint(5868.532, 6503.984, 110);
		readonly WoWPoint rightsideLoc3 = new WoWPoint(5863.477, 6461.133, 118.1055);

		WoWPoint leftSideMobLoc = new WoWPoint(5933.514, 6539.982, 112.2605);
		WoWPoint rightSideMobLoc = new WoWPoint(5867.311, 6539.257, 112.2603);

		WaitTimer LeftSideTimer = new WaitTimer(TimeSpan.FromSeconds(5));
		WaitTimer RightSideTimer = new WaitTimer(TimeSpan.FromSeconds(5));

		[EncounterHandler(0)]
		public Composite RootEncounter()
		{
			AddAvoidObject(ctx => Me.Z >= 114, 8, SpiritFlayerId);

			return new PrioritySelector(
				new Action(
					ctx =>
					{
						if (!Me.HasAura("Dark Winds"))
						{
							Styx.CommonBot.Profiles.ProfileManager.CurrentProfile.UseMount = true;
							return RunStatus.Failure;
						}

						Styx.CommonBot.Profiles.ProfileManager.CurrentProfile.UseMount = false;
						var myLoc = Me.Location;
						var atNorthSouthBridge = AtNorthSouthBridge(myLoc);
						var atEastWestBridge = AtEastWestBridge(myLoc);


						if (atNorthSouthBridge || atEastWestBridge)
							Navigator.NavigationProvider.StuckHandler.Reset();

						if (atNorthSouthBridge)
						{
							var nearestPointOnPath = myLoc.GetNearestPointOnSegment(_northSouthBridgeStart, _northSouthBridgeEnd);
							// don't get blown off bridge.
							if (!WoWMovement.ClickToMoveInfo.IsClickMoving && nearestPointOnPath.Distance(myLoc) > 3 && bridgeCtmTimer.IsFinished)
							{
								Navigator.PlayerMover.MoveTowards(nearestPointOnPath);
								bridgeCtmTimer.Reset();
								return RunStatus.Success;
							}
						}
						return RunStatus.Failure;
					}));
		}

		bool AtNorthSouthBridge(WoWPoint point)
		{
			return point.X >= 5475 && point.X <= 5645 && point.Y >= 6252.218 && point.Y <= 6273.957;
		}

		bool AtEastWestBridge(WoWPoint point)
		{
			return point.Y >= 6248 && point.Y <= 6041.935 && point.X <= 5443 && point.X >= 5419;
		}

		private bool ShouldAvoidLeftSide()
		{
			if (!LeftSideTimer.IsFinished) return true;
			var aliveTrash = ScriptHelpers.GetUnfriendlyNpsAtLocation(leftSideMobLoc, 20, unit => unit.IsHostile).Any();
			if (aliveTrash)
				LeftSideTimer.Reset();
			var aliveTrashOnRight = ScriptHelpers.GetUnfriendlyNpsAtLocation(rightSideMobLoc, 20, unit => unit.IsHostile).Any();
			return aliveTrash && !aliveTrashOnRight;
		}

		private bool ShouldAvoidRightSide()
		{
			if (!RightSideTimer.IsFinished) return true;
			var aliveTrash = ScriptHelpers.GetUnfriendlyNpsAtLocation(rightSideMobLoc, 20, unit => unit.IsHostile).Any();
			if (aliveTrash)
				RightSideTimer.Reset();
			var aliveTrashOnLeft = ScriptHelpers.GetUnfriendlyNpsAtLocation(leftSideMobLoc, 20, unit => unit.IsHostile).Any();
			return aliveTrash && !aliveTrashOnLeft;
		}

		#endregion

		[LocationHandler(5891.591, 6562.026, 115.2828, 100, "Entrance Trash Skip Behavior")]
		public Composite EntranceTrashSkipHandler()
		{
			var centerLoc = new WoWPoint(5891.591, 6562.026, 115.2828);
			return new PrioritySelector();
		}

		#region Jin'rokh the Breaker

		private const uint ConductiveWaterId = 69469;
		private const int StormSpellId = 137313;
		private bool ignoreConductiveWater;

		[EncounterHandler(69465, "Jin'rokh the Breaker")]
		public Composite JinrokhTheBreakeBehavior()
		{
			WoWUnit boss = null;
			var roomCenterLoc = new WoWPoint(5892.104, 6263.264, 124.0337);
			const uint focusedLightningId = 69593;
			const uint lightningFissureId = 69609;
			//const float roomRadius = 49;
			const int electrifiedAuraId = 137978;

			WoWUnit nonElectrifiedConductiveWater = null;

			AddAvoidObject(ctx => true, 5, focusedLightningId);
			AddAvoidObject(
				ctx => !ignoreConductiveWater,
				() => roomCenterLoc,
				20,
				34,
				o => o.Entry == ConductiveWaterId && (o.ToUnit().HasAura(electrifiedAuraId) || boss != null && boss.IsValid && boss.CastingSpellId == StormSpellId));
			AddAvoidObject(ctx => true, 5, lightningFissureId);
			return new PrioritySelector(
				ctx =>
				{
					var conductiveWater = ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == ConductiveWaterId).ToArray();
					nonElectrifiedConductiveWater = conductiveWater.FirstOrDefault(u => !u.HasAura(electrifiedAuraId));
					ignoreConductiveWater = conductiveWater.Count(u => u.HasAura(electrifiedAuraId)) == 4;
					return boss = ctx as WoWUnit;
				},
				// drop focuses lightning away from group and condive water.
				new Decorator(
					ctx => Me.HasAura("Focused Lightning"),
					new PrioritySelector(
						ctx => // find a location at the ouside prerimeter of the boss area that has no wild fires 
						(from point in GetPointsAroundCircle(roomCenterLoc, 60, 20)
						 let myLoc = Me.Location
						 where !AvoidanceManager.Avoids.Any(a => a.IsPointInAvoid(point)) && // steer clear of any water pools.
							   Navigator.CanNavigateFully(myLoc, point)
						 orderby point.DistanceSqr(myLoc)
						 select point).FirstOrDefault(),
						new Decorator(ctx => (WoWPoint)ctx != WoWPoint.Zero, new Action(ctx => Navigator.MoveTo((WoWPoint)ctx))))),
				new Decorator(
					ctx => !Me.HasAura("Fluidity") && nonElectrifiedConductiveWater != null,
					new PrioritySelector(
						new Decorator(
							ctx => Me.IsMelee(),
							new PrioritySelector(
								ctx => WoWMathHelper.CalculatePointFrom(nonElectrifiedConductiveWater.Location, boss.Location, boss.MeleeRange()),
								new Decorator(ctx => Me.Location.Distance((WoWPoint)ctx) > Navigator.PathPrecision, new Action(ctx => Navigator.MoveTo((WoWPoint)ctx))))),
						new Decorator(ctx => Me.IsRange(), new Action(ctx => Navigator.MoveTo(nonElectrifiedConductiveWater.Location))))),
				// move to room center if all 4 water pools are electrified.
				new Decorator(ctx => ignoreConductiveWater && Me.Location.Distance(roomCenterLoc) > 5, new Action(ctx => Navigator.MoveTo(roomCenterLoc))));
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

		#region Horridon

		private const uint ZandalariDinomancerId = 69221;
		private const uint FarrakiWastewalkerId = 69175;
		private const uint GurubashiVenomPriestId = 69164;
		private const uint DrakkariFrozenWarlordId = 69178;
		private const uint AmanishiBeastShamanId = 69176;

		private const uint HorridonId = 68476;
		private readonly uint[] _tier1KillpriorityIds = new[] { FarrakiWastewalkerId, GurubashiVenomPriestId, DrakkariFrozenWarlordId, AmanishiBeastShamanId };

		[EncounterHandler(68476, "Horridon")]
		public Composite HorridonBehavior()
		{
			WoWUnit boss = null;
			const uint sandTrapId = 69346;
			const uint frozenOrbId = 69268;
			const uint lightningNovaTotemId = 69215;
			const uint livingPoisonId = 69313;

			AddAvoidObject(ctx => true, 11f, lightningNovaTotemId, sandTrapId, HorridonId);

			AddAvoidObject(ctx => true, 6, livingPoisonId, frozenOrbId);
			// todo, move to next gate.
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(
					ctx => boss.Distance <= 30 && !Me.IsMoving, ctx => boss, new ScriptHelpers.AngleSpan(0, 40), new ScriptHelpers.AngleSpan(180, 40)));
		}

		#endregion

		#region Council of the Elders

		private uint[] _shadowedLoaSpiritIds = new uint[] { 69548, 69553 };
		private const uint BlessedLoaSpiritId = 69480;
		[EncounterHandler(69078, "Sul the Sandcrawler")]
		[EncounterHandler(69134, "Kazra'jin")]
		[EncounterHandler(69132, "High Priestess Mar'li")]
		[EncounterHandler(69131, "Frost King Malakk")]
		public Composite CouncilOfTheEldersBehavior()
		{
			WoWUnit boss = null;
			var roomCenterLoc = new WoWPoint(6045.837, 5403.019, 136.0888);
			const uint livingSandId = 69153;
			const uint recklessChargeId = 69453;
			const uint recklessChargeMissileVisualId = 30216;

			AddAvoidObject(ctx => true, 5, recklessChargeId);
			AddAvoidObject(ctx => true, 7, o => o.Entry == livingSandId && o.ToUnit().HasAura("Quicksand"));
			// run from the Shadowed Loa Spirit.
			AddAvoidObject(
				ctx => true,
				() => roomCenterLoc,
				40,
				o => Me.IsRange() && Me.IsMoving ? 23 : 15,
				o => _shadowedLoaSpiritIds.Contains(o.Entry) && o.ToUnit().CurrentTargetGuid == Me.Guid);

			AddAvoidLocation(
				ctx => true, 3, o => ((WoWMissile)o).ImpactPosition, () => WoWMissile.InFlightMissiles.Where(m => m.SpellVisualId == recklessChargeMissileVisualId));

			return new PrioritySelector(ctx => boss = ctx as WoWUnit);
		}

		#endregion

	}
}