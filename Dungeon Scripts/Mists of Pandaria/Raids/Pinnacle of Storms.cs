using System;
using System.Collections.Generic;
using System.Linq;
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
	public class PinnacleOfStorms : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 613; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(7262.557, 5018.036, 76.17107); }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			bool superchargeCircuitsPhase = _leiShen != null && _leiShen.IsValid && _leiShen.HasAura("Supercharge Conduits");

			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (unit != null)
					{
						if (unit.Entry == LulinId && unit.HasAura("Tidal Force") && Me.IsDps())
							return true;
						if (unit.Entry == LeiShenId && unit.HasAura("Supercharge Conduits"))
							return true;
						if (superchargeCircuitsPhase)
						{
							if (!IsUnitWithinDpsRange(unit, QuadrantLocation) || Me.IsMelee() && QuadrantLocation.Distance(unit.Location) > 20)
								return true;
						}
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
				if (unit != null) { }
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

		#endregion

		private const uint DisplacementPadId = 218417;
		private readonly WoWPoint _displacementPadLoc = new WoWPoint(5898.408, 4098.049, 202.5646);

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		public override MoveResult MoveTo(WoWPoint location)
		{
			var myLoc = Me.Location;
			if (location.DistanceSqr(_leiShenRoomCenterLoc) <= 120 * 120 && myLoc.DistanceSqr(_leiShenRoomCenterLoc) > 120 * 120)
			{
				var displacementPad = (WoWGameObject)ObjectManager.ObjectList.FirstOrDefault(o => o.Entry == DisplacementPadId);
				if (displacementPad != null)
				{
					if (!displacementPad.CanUseNow())
						return Navigator.MoveTo(displacementPad.Location);

					if (Me.IsMoving)
						WoWMovement.MoveStop();

					if (!GossipFrame.Instance.IsVisible)
					{
						displacementPad.Interact();
					}
					else
					{
						GossipFrame.Instance.SelectGossipOption(0);
					}
					return MoveResult.Moved;
				}

				return Navigator.MoveTo(_displacementPadLoc);
			}
			return base.MoveTo(location);
		}

		public override bool CanNavigateFully(WoWPoint from, WoWPoint to)
		{
			if (to.DistanceSqr(_leiShenRoomCenterLoc) <= 120 * 120 && from.DistanceSqr(_leiShenRoomCenterLoc) > 120 * 120)
				return true;
			return base.CanNavigateFully(from, to);
		}

		#region Root

		[EncounterHandler(0)]
		public Composite RootEncounter()
		{
			return new PrioritySelector();
		}

		#endregion

		#region  Iron Qon

		private const uint RushingWindsId = 68852;
		private const uint IronQonId = 68078;
		private const uint RushingWindsIgniteCycloneId = 69703;

		private const uint BurningCindersSpellId = 134758;
		private const uint FrozenBloodSpellId = 136451;
		private const uint StormCloudId = 136421;

		private const uint RoshakId = 68079;

		[EncounterHandler(68078, "Iron Qon")]
		public Composite IronQonEncounter()
		{
			WoWUnit boss = null;
			AddAvoidObject(
				ctx => !Me.IsMoving,
				2,
				o =>
					((o is WoWAreaTrigger && (((WoWAreaTrigger)o).SpellId == BurningCindersSpellId || ((WoWAreaTrigger)o).SpellId == FrozenBloodSpellId)) ||
					 o.Entry == StormCloudId) && o.DistanceSqr <= 30 * 30,
				null,
				true);

			AddAvoidObject(ctx => true, 6, RushingWindsId);
			AddAvoidObject(ctx => true, 8, RushingWindsIgniteCycloneId);

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				// stay near boss after Ro'shak dies.
				new Decorator(
					ctx => ((!boss.IsOnTransport || boss.Transport.Entry != RoshakId) && boss.ZDiff < 10) && boss.Distance > 25,
					new Action(ctx => Navigator.MoveTo(boss.Location))));
		}

		#endregion

		#region Twin Consorts

		private const uint SlumberSporesId = 136722;
		private const int CosmicBarrageSpellId = 136752;
		private const uint LulinId = 68905;
		private const int FlamesOfPassionSpellId = 137416;
		private const uint IceCometId = 69596;
		private readonly int[] _lightOfDayIds = new[] { 137401, 138803, 137493 };

		[EncounterHandler(68904, "Suen", Mode = CallBehaviorMode.Proximity, BossRange = 100)]
		[EncounterHandler(68905, "Lu'lin", Mode = CallBehaviorMode.Proximity, BossRange = 100)]
		public Composite TwinConsortsEncounter()
		{
			WoWUnit boss = null;
			var rightDoorCorner = new WoWPoint(6124.383, 4271.612, 146.4858); // right
			var leftDoorCorner = new WoWPoint(6141.024, 4289.231, 146.826); // left
			var moveToLoc = new WoWPoint(6136.579, 4276.69, 146.5802);

			AddAvoidObject(ctx => true, 4, SlumberSporesId);
			// move away from Lu'lin's target when she is casting Cosmic Barrage.
			AddAvoidObject(
				ctx => true,
				8,
				o => o.Entry == LulinId && o.ToUnit().CastingSpellId == CosmicBarrageSpellId && o.ToUnit().CurrentTargetGuid != 0,
				o => o.ToUnit().CurrentTarget.Location);
			// run away from the light of day target.
			//AddAvoidLocation(ctx => true, 8, o => ((WoWMissile)o).ImpactPosition, () => WoWMissile.InFlightMissiles.Where(m => _lightOfDayIds.Contains(m.SpellId)));
			AddAvoidObject(ctx => true, 5, FlamesOfPassionSpellId);
			AddAvoidObject(ctx => true, 6, IceCometId);

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				// move onto the platform to not get locked out when encounter starts
				new Decorator(
					ctx => !boss.Combat && !Me.Combat && ScriptHelpers.Tank != null && !ScriptHelpers.Tank.Location.IsPointLeftOfLine(rightDoorCorner, leftDoorCorner),
					new PrioritySelector(
						new ActionSetActivity("Moving inside room"),
						new Decorator(ctx => Me.Location.IsPointLeftOfLine(rightDoorCorner, leftDoorCorner), new Action(ctx => Navigator.MoveTo(moveToLoc))),
				// disable movement to prevent raid following while waiting inside room
						new Decorator(ctx => !Me.Location.IsPointLeftOfLine(rightDoorCorner, leftDoorCorner) && ScriptHelpers.MovementEnabled,
							new Action(ctx => ScriptHelpers.DisableMovement(() => boss.IsAlive && !boss.Combat))))));
		}

		#endregion

		#region Lei Shen

		private const uint LeiShenId = 68397;
		private const uint ThunderstruckId = 135095;
		private const uint ThunderKingDiscCollisionId = 218488;
		private const uint CrashingThunderSpellId = 135150;
		private const uint LightningWhipId = 136850;

		private const int ViolentGaleWindsNorthWestId = 136879;
		private const int ViolentGaleWindsSouthWestId = 136876;
		private const int ViolentGaleWindsNorthEastId = 136877;
		private const int ViolentGaleWindsSouthEastId = 136867;
		const uint OverloadedCircuitsSpellVisualId = 30235;

		private const float NorthEastRadians = (float)Math.PI * 1.75f;
		private const float SouthEastRadians = (float)Math.PI * 1.25f;
		private const float SouthWestRadians = (float)Math.PI * 0.75f;
		private const float NorthWestRadians = (float)Math.PI * 0.25f;
		private const int StaticShockId = 135695;

		private readonly WoWPoint _leiShenPlatformEastCornerLoc = new WoWPoint(5711.908, 4039.707, 156.4626);
		private readonly WoWPoint _leiShenPlatformNorthCornerLoc = new WoWPoint(5764.473, 4093.515, 156.463);
		private readonly WoWPoint _leiShenPlatformSouthCornerLoc = new WoWPoint(5655.858, 4093.599, 156.4639);
		private readonly WoWPoint _leiShenPlatformWestCornerLoc = new WoWPoint(5709.55, 4148.847, 156.463);
		private readonly WoWPoint _leiShenRoomCenterLoc = new WoWPoint(5710.253, 4094.104, 156.464);
		private WoWUnit _leiShen;
		readonly WoWPoint[] _quadrantCenterLocs =
		{ // south 
			new WoWPoint(5670.284, 4094.13, 156.4627),
			// west
			new WoWPoint(5710.777, 4136.031, 156.4627),
			// north
			new WoWPoint(5750.242, 4094.285, 156.4627),
			// east
			new WoWPoint(5710.151, 4054.058, 156.4627),
		};


		[EncounterHandler(68397, "Lei Shen")]
		public Composite LeiShenEncounter()
		{
			const uint thunderousThrowId = 68574;

			WoWPlayer overchargedAndStaticShockTarget = null;

			// this game object spawns in center during encounter.
			AddAvoidObject(ctx => true, 12, ThunderKingDiscCollisionId);
			AddAvoidObject(ctx => true, 6, o => o is WoWAreaTrigger && ((WoWAreaTrigger)o).SpellId == CrashingThunderSpellId);
			AddAvoidObject(ctx => true, 10, thunderousThrowId);


			return new PrioritySelector(
				ctx =>
				{
					_leiShen = ctx as WoWUnit;
					overchargedAndStaticShockTarget =
						ObjectManager.GetObjectsOfType<WoWPlayer>()
							.Where(p => (p.HasAura("Overcharged") || p.HasAura(StaticShockId)) && !p.IsMe && p.Distance <= 30)
							.OrderBy(p => p.Distance2DSqr)
							.FirstOrDefault();
					return _leiShen;
				},
				// don't stand in front of boss while he's casting Lightning Whip.
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(ctx => _leiShen.CastingSpellId == LightningWhipId, ctx => _leiShen, new ScriptHelpers.AngleSpan(0, 95)),
				// move to boss if in a quadrant that has been overloaded. (depends on tank moving boss out)

				new Decorator(
					ctx => Me.HasAura("Overloaded Circuits"),
					new Action(
						ctx =>
						{
							Logger.Write("Moving out of overloaded quadrant");
							Navigator.MoveTo(_leiShen.Location);
						})),
				new Decorator(
					ctx => overchargedAndStaticShockTarget != null,
				// move to overcharge target
					new PrioritySelector(
						new Decorator(
							ctx => overchargedAndStaticShockTarget.Distance >= 4,
							new PrioritySelector(
								new ActionSetActivity("Moving to overcharge target"),
								new Action(ctx => Navigator.MoveTo(overchargedAndStaticShockTarget.Location)))),
						new Decorator(
							ctx => Me.IsHealer() && ScriptHelpers.MovementEnabled,
							new Action(
								ctx =>
									ScriptHelpers.DisableMovement(
										() =>
											overchargedAndStaticShockTarget.IsValid && overchargedAndStaticShockTarget.IsAlive &&
											overchargedAndStaticShockTarget.HasAura("Overcharged") && overchargedAndStaticShockTarget.Distance < 4))),
						new Decorator(ctx => !Me.IsHealer(), IdleIfTargetIsOutOfRangeBehavior()))),
				new Decorator(
					ctx => _leiShen.HasAura("Supercharge Conduits") && Targeting.Instance.IsEmpty() && overchargedAndStaticShockTarget == null,
					new Action(
						ctx =>
						{
							if (Me.Location.DistanceSqr(QuadrantLocation) > 8 * 8)
							{
								Navigator.MoveTo(QuadrantLocation);
								return RunStatus.Success;
							}
							if (Me.IsHealer() && ScriptHelpers.MovementEnabled)
								ScriptHelpers.DisableMovement(
									() =>
										_leiShen.HasAura("Supercharge Conduits") && !AvoidanceManager.IsRunningOutOfAvoid && Me.Location.DistanceSqr(QuadrantLocation) <= 8 * 8 &&
										overchargedAndStaticShockTarget == null);

							return Targeting.Instance.IsEmpty() ? RunStatus.Success : RunStatus.Failure;
						})),
				// North West
				new Decorator(
					ctx => _leiShen.HasAura(ViolentGaleWindsNorthWestId) && Me.Location.IsPointLeftOfLine(_leiShenPlatformEastCornerLoc, _leiShenPlatformSouthCornerLoc),
					new Action(ctx => Navigator.MoveTo(Me.Location.RayCast(NorthWestRadians, 30)))),
				// North East
				new Decorator(
					ctx => _leiShen.HasAura(ViolentGaleWindsNorthEastId) && Me.Location.IsPointLeftOfLine(_leiShenPlatformSouthCornerLoc, _leiShenPlatformWestCornerLoc),
					new Action(ctx => Navigator.MoveTo(Me.Location.RayCast(NorthEastRadians, 30)))),
				// South East
				new Decorator(
					ctx => _leiShen.HasAura(ViolentGaleWindsSouthEastId) && Me.Location.IsPointLeftOfLine(_leiShenPlatformWestCornerLoc, _leiShenPlatformNorthCornerLoc),
					new Action(ctx => Navigator.MoveTo(Me.Location.RayCast(SouthEastRadians, 30)))),
				// South West
				new Decorator(
					ctx => _leiShen.HasAura(ViolentGaleWindsSouthWestId) && Me.Location.IsPointLeftOfLine(_leiShenPlatformNorthCornerLoc, _leiShenPlatformEastCornerLoc),
					new Action(ctx => Navigator.MoveTo(Me.Location.RayCast(SouthWestRadians, 30)))),
				// melee will spam ctm while wind is up.
				new Decorator(
					ctx => _leiShen.HasAura("Violent Gale Winds") && _leiShen.CastingSpellId != LightningWhipId && Me.IsMelee(),
					new Action(
						ctx =>
						{
							Navigator.PlayerMover.MoveTowards(_leiShen.Location);
							return RunStatus.Failure;
						})));
		}

		private TimeCachedValue<WoWPoint> _quadrantLocation;
		
		WoWPoint QuadrantLocation
		{
			get
			{
				return _quadrantLocation ?? (_quadrantLocation =
					new TimeCachedValue<WoWPoint>(
						TimeSpan.FromSeconds(4),
						() =>
						{
							var overchargedCircuitLocations =
								ObjectManager.GetObjectsOfTypeFast<WoWAreaTrigger>()
									.Where(a => a.SpellVisualId == OverloadedCircuitsSpellVisualId)
									.Select(a => a.Location)
									.ToArray();
							var goodQuadrants =
								_quadrantCenterLocs.Where(q => !overchargedCircuitLocations.Any(o => o.DistanceSqr(q) <= 20*20)).ToArray();

							// only use my location if i'm in a 1 man party.
							WoWPoint[] locs = Me.GroupInfo.PartySize > 1
								? Me.PartyMembers.Where(p => !p.IsMe).Select(p => p.Location).ToArray()
								: new[] {Me.Location};
							// return the quadrant that my party members are nearest to
							return goodQuadrants.OrderBy(l => locs.Sum(p => p.Distance(l))/locs.Length).FirstOrDefault();
						}));
			}
		}

		private Composite IdleIfTargetIsOutOfRangeBehavior()
		{
			return new Decorator(ctx => !Me.IsHealer() && !IsUnitWithinDpsRange(Targeting.Instance.FirstUnit, Me.Location), new ActionAlwaysSucceed());
		}

		private bool IsUnitWithinDpsRange(WoWUnit unit, WoWPoint location)
		{
			if (unit == null)
				return false;
			var dpsRange = Me.IsMelee() ? unit.MeleeRange() : unit.CombatReach + 30;
			return unit.Location.Distance(location) <= dpsRange;
		}

		#endregion
	}
}