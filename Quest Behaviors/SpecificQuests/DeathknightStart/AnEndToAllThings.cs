// Behavior originally contributed by Nesox / complete rework by Chinajade
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//


#region Summary and Documentation
//
// * Summons the dragon, and mounts it
// * Fires at targets as it moves around range
// * Behavior is stop/start friendly
//
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using Bots.Grind;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.DeathknightStart.AnEndToAllThings
{
	/// <summary>
	/// Moves along a path in a vehicle using spells to inflict damage and to heals itself until the quest is completed.
	/// ##Syntax##
	/// VehicleId: Id of the vehicle
	/// ItemId: Id of the item that summons the vehicle.
	/// AttackSpell: Id of the attackspell, can be enumerated using, 'GetPetActionInfo(index)'
	/// HealSpell: Id of the healspell, can be enumerated using, 'GetPetActionInfo(index)'
	/// NpcIds: a comma separated list with id's of npc's to kill for this quest. example. NpcIds="143,2,643,1337" 
	/// </summary>
	[CustomBehaviorFileName(@"SpecificQuests\DeathknightStart\AnEndToAllThings")]
	public class AnEndToAllThings : QuestBehaviorBase
	{
		#region Constructor and Argument Processing
		public AnEndToAllThings(Dictionary<string, string> args)
			: base(args)
		{
			try
			{
				QuestId = 12779;
				TerminationChecksQuestProgress = false;

				ActionBarIndex_Attack = GetAttributeAsNullable<int>("AttackSpellIndex", true, ConstrainAs.SpellId, null) ?? 0;
				ActionBarIndex_Heal = GetAttributeAsNullable<int>("HealSpellIndex", false, ConstrainAs.SpellId, null) ?? 0;
				HealNpcId = GetAttributeAsNullable<int>("HealNpcId", true, ConstrainAs.MobId, null) ?? 0;
				ItemIdToSummonVehicle = GetAttributeAsNullable<int>("ItemId", false, ConstrainAs.ItemId, null) ?? 0;
				KillNpcId = GetAttributeAsNullable<int>("KillNpcId", true, ConstrainAs.MobId, null) ?? 0;
				VehicleId = GetAttributeAsNullable<int>("VehicleId", true, ConstrainAs.VehicleId, null) ?? 0;

				AuraId_RideVehicleHardcoded = 46598;    // http://wowhead.com/spell=46598
				FlightorMinHeight = 25.0;
				MobId_FrostbroodVanquisher = 28670;     // http://wowhead.com/npc=28670
				MobId_ScarletBallista = 29104;          // http://wowhead.com/npc=29104
				MobId_TirisfalCrusader = 29103;         // http://wowhead.com/npc=29102
				WeaponAzimuthMin = -1.27;      // Use: /script print(VehicleAimGetAngle())
				WeaponAzimuthMax = -0.30;      // Use: /script print(VehicleAimGetAngle())
				WeaponMuzzleVelocity = 140;
			}

			catch (Exception except)
			{
				// Maintenance problems occur for a number of reasons.  The primary two are...
				// * Changes were made to the behavior, and boundary conditions weren't properly tested.
				// * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
				// In any case, we pinpoint the source of the problem area here, and hopefully it
				// can be quickly resolved.
				QBCLog.Exception(except);
				IsAttributeProblem = true;
			}
		}


		protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
		{
			// empty
		}


		protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
		{
			// empty
		}


		// Attributes provided by caller
		private int ActionBarIndex_Attack { get; set; }
		private int HealNpcId { get; set; }
		private int ActionBarIndex_Heal { get; set; }
		private int ItemIdToSummonVehicle { get; set; }
		private int KillNpcId { get; set; }
		private int VehicleId { get; set; }
		#endregion


		#region Private data
		private enum BehaviorStateType
		{
			MountingVehicle,        // initial state
			RidingOutToHuntingGrounds,
			CompletingObjectives,
			ReturningToBase,
		}

		private BehaviorStateType BehaviorState
		{
			get { return _behaviorState; }
			set
			{
				// For DEBUGGING...
				if (_behaviorState != value)
				{ QBCLog.DeveloperInfo("BehaviorStateType: {0}", value); }

				_behaviorState = value;
			}
		}

		private WoWUnit DragonVehicle
		{
			get
			{
				if (!Query.IsViable(_dragonVehicle))
				{
					_dragonVehicle = (Me.TransportGuid != 0)
						? ObjectManager.GetObjectByGuid<WoWUnit>(Me.TransportGuid)
						: null;
				}

				return _dragonVehicle;
			}
		}

		private const double FlyingPathPrecision = 15.0;
		private const double MissileImpactClearanceDistance = 25.0;
		private const double TargetDistance2DMax = 80.0;
		private const double TargetDistance2DMin = 60.0;
		private const double TargetHeightMinimum = 50.0;
		private double TargetHeightVariance { get { return _random.Next(15); } }

		private int AuraId_RideVehicleHardcoded { get; set; }
		private WoWPoint? StationPoint { get; set; }
		private double FlightorMinHeight { get; set; }
		private WoWItem ItemToSummonVehicle { get; set; }
		private int MobId_FrostbroodVanquisher { get; set; }
		private int MobId_ScarletBallista { get; set; }
		private int MobId_TirisfalCrusader { get; set; }
		private WoWPoint PathEnd { get; set; }
		private CircularQueue<WoWPoint> PathPatrol { get; set; }
		private WoWPoint PathStart { get; set; }
		private WoWUnit SelectedTarget { get; set; }
		private WoWUnit SelectedTargetToDevour { get; set; }
		private VehicleAbility Weapon_DevourHuman { get; set; }
		private VehicleWeapon Weapon_FrozenDeathbolt { get; set; }
		private double WeaponAzimuthMax { get; set; }
		private double WeaponAzimuthMin { get; set; }
		private double WeaponMuzzleVelocity { get; set; }
		private WoWUnit _dragonVehicle;

		private BehaviorStateType _behaviorState;
		private readonly LocalBlacklist _targetBlacklist = new LocalBlacklist(TimeSpan.FromSeconds(30));
		#endregion


		#region Overrides of CustomForcedBehavior
		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }

		// CreateBehavior supplied by QuestBehaviorBase.
		// Instead, provide CreateMainBehavior definition.

		// Dispose provided by QuestBehaviorBase.

		// IsDone provided by QuestBehaviorBase.
		// Call the QuestBehaviorBase.BehaviorDone() method when you want to indicate your behavior is complete.

		// OnFinished provided by QuestBehaviorBase.

		public override void OnStart()
		{
			ParsePaths();

			// Let QuestBehaviorBase do basic initializaion of the behavior, deal with bad or deprecated attributes,
			// capture configuration state, install BT hooks, etc.  This will also update the goal text.
			var isBehaviorShouldRun = OnStart_QuestBehaviorCore();

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (isBehaviorShouldRun)
			{
				LevelBot.ShouldUseSpiritHealer = true;

				// Turn off LevelBot behaviors that will interfere...
				// NB: These will be restored by our parent class when we're done.
				// NB: We need to disable the Roam behavior to prevent the StuckHandler from kicking in.
				LevelBot.BehaviorFlags &=
					~(BehaviorFlags.Combat | BehaviorFlags.Loot | BehaviorFlags.Roam | BehaviorFlags.Vendor);

				BehaviorState = BehaviorStateType.MountingVehicle;
			}
		}
		#endregion


		#region Main Behaviors
		protected override Composite CreateMainBehavior()
		{
			return new PrioritySelector(
				new Switch<BehaviorStateType>(context => BehaviorState,
					new Action(context =>   // default case
					{
						var message = string.Format("BehaviorStateType({0}) is unhandled", BehaviorState);
						QBCLog.MaintenanceError(message);
						TreeRoot.Stop();
						BehaviorDone(message);
					}),

					new SwitchArgument<BehaviorStateType>(BehaviorStateType.MountingVehicle,
						StateBehaviorPS_MountingVehicle()),
					new SwitchArgument<BehaviorStateType>(BehaviorStateType.RidingOutToHuntingGrounds,
						StateBehaviorPS_RidingOutToHuntingGrounds()),
					new SwitchArgument<BehaviorStateType>(BehaviorStateType.CompletingObjectives,
						StateBehaviorPS_CompletingObjectives()),
					new SwitchArgument<BehaviorStateType>(BehaviorStateType.ReturningToBase,
						StateBehaviorPS_ReturningToBase())
				));
		}


		private Composite StateBehaviorPS_MountingVehicle()
		{
			return new PrioritySelector(
				new Decorator(context => Me.IsQuestComplete(QuestId),
					new Action(context => { BehaviorDone(string.Format("quest complete")); })),

				// If we're mounted on something other than the dragon, then dismount...
				new Decorator(context => Me.Mounted && !Query.IsViable(DragonVehicle),
                    new ActionRunCoroutine(context => UtilityCoroutine.ExecuteMountStrategy(MountStrategyType.DismountOrCancelShapeshift))),

				// If we're on the dragon, get moving...
				new Decorator(context => Query.IsViable(DragonVehicle),
					new PrioritySelector(
						SubBehaviorPS_InitializeVehicleAbilities(),
						new Action(context => { BehaviorState = BehaviorStateType.RidingOutToHuntingGrounds; })
					)),

				// If we don't posssess item to summon the dragon, that's fatal...
				new Decorator(context =>
					{
						ItemToSummonVehicle = Me.CarriedItems.FirstOrDefault(i => i.Entry == ItemIdToSummonVehicle);
						return !Query.IsViable(ItemToSummonVehicle);
					},
					new Action(context => { QBCLog.Fatal("Unable to locate ItemId({0}) in inventory.", ItemIdToSummonVehicle); })),

				// Wait for item to come off cooldown...
				new Decorator(context => ItemToSummonVehicle.Cooldown > 0,
					new Action(context =>
					{
						TreeRoot.StatusText =
							string.Format("Waiting for {0} cooldown ({1} remaining)",
                                ItemToSummonVehicle.SafeName,
								Utility.PrettyTime(ItemToSummonVehicle.CooldownTimeLeft));
						return RunStatus.Success;
					})),

				// Use the item
				new Decorator(context => !Me.IsCasting,
					new ActionFail(context =>
					{
						// If we got booted out of a vehicle for some reason, reset the weapons...
						Weapon_DevourHuman = null;
						Weapon_FrozenDeathbolt = null;

						ItemToSummonVehicle.UseContainerItem();
					}))
			);
		}


		private Composite StateBehaviorPS_RidingOutToHuntingGrounds()
		{
			return new PrioritySelector(
				// If for some reason no longer in the vehicle, go fetch another...
				new Decorator(context => !Query.IsViable(DragonVehicle),
					new Action(context =>
					{
						QBCLog.Warning("We've been jettisoned from vehicle unexpectedly--will try again.");
						BehaviorState = BehaviorStateType.MountingVehicle;
					})),

				new Decorator(context => Weapon_FrozenDeathbolt.IsWeaponUsable(),
					new Action(context => { BehaviorState = BehaviorStateType.CompletingObjectives; })),

				new CompositeThrottle(Throttle.UserUpdate,
					new Action(context => { TreeRoot.StatusText = "Riding out to hunting grounds"; })),

				new ActionAlwaysSucceed()
			);
		}


		private Composite StateBehaviorPS_CompletingObjectives()
		{
			return new PrioritySelector(
				// If for some reason no longer in the vehicle, go fetch another...
				new Decorator(context => !Query.IsViable(DragonVehicle),
					new Action(context =>
					{
						QBCLog.Warning("We've been jettisoned from vehicle unexpectedly--will try again.");
						BehaviorState = BehaviorStateType.MountingVehicle;
					})),

				// If quest is complete, then head back...
				new Decorator(context => Me.IsQuestComplete(QuestId),
					new Action(context => { BehaviorState = BehaviorStateType.ReturningToBase; })),

				new CompositeThrottle(Throttle.UserUpdate,
					new ActionFail(context => { TreeRoot.StatusText = "Completing Quest Objectives"; })),

				SubBehaviorPS_UpdatePathWaypoint(),
				SubBehaviorPS_Heal(),

				// We go after the Ballistas first...
				// NB: Soldiers will be collateral damage of pursing Ballistas.
				// If the soldiers don't complete after doing Ballistas, we'll clean up
				// the Soldiers next.
				new Decorator(context => !IsViableTarget(SelectedTarget),
					new PrioritySelector(
				// Try to find target...
						new ActionFail(context =>
						{
							SelectedTarget = FindMobToKill(!Me.IsQuestObjectiveComplete(QuestId, 2)
															? MobId_ScarletBallista
															: MobId_TirisfalCrusader);
						}),
				// If no target found, move toward next waypoint...
						new Decorator(context => !IsViableTarget(SelectedTarget),
							new Action(context => { Flightor.MoveTo(PathPatrol.Peek(), (float)FlightorMinHeight); }))
					)),

				new Action(context =>
				{
					// NB: We would've preferred to strafe in this algorithm; however,
					// strafing triggers Flightor's unstuck handler too much.  Probably because
					// this is a vehicle/transport, instead of a 'flying mount'.

					// Show the target we're pursuing...
					Utility.Target(SelectedTarget);

					var myLocation = WoWMovement.ActiveMover.Location;
					var selectedTargetLocation = SelectedTarget.Location;
					var distance2DSqrToTarget = myLocation.Distance2DSqr(selectedTargetLocation);

					// Deal with needed evasion...
					if (StationPoint.HasValue)
					{
						if (myLocation.Distance(StationPoint.Value) > FlyingPathPrecision)
						{
							Flightor.MoveTo(StationPoint.Value);
							return RunStatus.Success;
						}

						StationPoint = null;
					}

					// See if we need a new 'on station' location...
					if (!StationPoint.HasValue)
					{
						// If our weapon is not ready or we can't see target, move around station until it is...
						if (!Weapon_FrozenDeathbolt.IsWeaponReady()
							|| (IsViableTarget(SelectedTarget) && !SelectedTarget.InLineOfSight)
							|| IsIncomingMissile())
						{
							StationPoint = FindNewStationPoint(SelectedTarget);
							return RunStatus.Success;
						}
					}

					// If we are too far from selected target, close the distance...
					if (distance2DSqrToTarget > (TargetDistance2DMax * TargetDistance2DMax))
					{
						Flightor.MoveTo(FindDistanceClosePoint(SelectedTarget, PathPatrol.Peek().Z));
						return RunStatus.Success;
					}

					// If we are too close to selected target, put some distance between us...
					if (distance2DSqrToTarget < (TargetDistance2DMin * TargetDistance2DMin))
					{
						Flightor.MoveTo(FindDistanceGainPoint(SelectedTarget, TargetDistance2DMin));
						return RunStatus.Success;
					}

					// If weapon is not ready, just keep on station/evading...
					if (!Weapon_FrozenDeathbolt.IsWeaponReady())
					{ return RunStatus.Success; }

					// If the weapon cannot address the target, blacklist target and find another...
					if (!Weapon_FrozenDeathbolt.WeaponAim(SelectedTarget))
					{ _targetBlacklist.Add(SelectedTarget, TimeSpan.FromSeconds(5)); }

					// If weapon cannot fire for some reason, try again...
					if (!Weapon_FrozenDeathbolt.WeaponFire())
					{ return RunStatus.Success; }

					return RunStatus.Failure;   // fall through
				}),
				// NB: Need to delay a bit for the weapon to actually launch.  Otherwise
				// it screws the aim up if we move again before projectile is fired.
				new Sleep(ctx => Delay.LagDuration + Delay.AfterWeaponFire)
				//new Wait(Delay.LagDuration, context => Weapon_FrozenDeathbolt.IsWeaponReady(), new ActionAlwaysSucceed())
			);
		}


		private Composite StateBehaviorPS_ReturningToBase()
		{
			return new PrioritySelector(
				// Start over...
				new Decorator(context => !Query.IsInVehicle(),
					new Action(context => { BehaviorState = BehaviorStateType.MountingVehicle; })),

				// Exit vehicle...
				new Decorator(context => Navigator.AtLocation(PathEnd),
					new Action(context => { Lua.DoString("VehicleExit()"); })),

				// Move back to 'safe mounting' area...
				new Action(context =>
				{
					TreeRoot.StatusText = "Moving back to safe area";
					Flightor.MoveTo(PathEnd, (float)FlightorMinHeight);
				})
			);
		}


		private Composite SubBehaviorPS_Heal()
		{
			return
				new Decorator(context =>
					{
						var vehicle = DragonVehicle;

						return
							Weapon_DevourHuman.IsAbilityReady()
							&& ((vehicle.HealthPercent <= 70) || (vehicle.ManaPercent <= 35));
					},

					new Action(context =>
					{
						if (!IsViableTarget(SelectedTargetToDevour))
						{
							SelectedTargetToDevour = FindSoldierForHeal();

							// We want exit point to be different than point we entered to get the devour target...
							// This should minimize the damage we take by not returning through areas
							// that are currently under fire.
							StationPoint = FindNewStationPoint(SelectedTargetToDevour, TAU / 2);
							return RunStatus.Success;
						}

						var selectedTargetLocation = SelectedTargetToDevour.Location;

						Utility.Target(SelectedTargetToDevour);
						if (selectedTargetLocation.Distance(DragonVehicle.Location) > (Weapon_DevourHuman.MaxRange / 2))
						{
							Flightor.MoveTo(selectedTargetLocation);
							return RunStatus.Success;
						}

						WoWMovement.MoveStop();
						Weapon_DevourHuman.UseAbility();
						_targetBlacklist.Add(SelectedTargetToDevour, TimeSpan.FromSeconds(60));

						WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend, TimeSpan.FromMilliseconds(1500));
						return RunStatus.Failure;   // fall through
					}));
		}


		private Composite SubBehaviorPS_InitializeVehicleAbilities()
		{
			return
				new Decorator(context => (Weapon_DevourHuman == null)
											|| (Weapon_FrozenDeathbolt == null),
				// Give the WoWclient a few seconds to produce the vehicle action bar...
				// NB: If we try to use the weapon too quickly after entering vehicle,
				// then it will cause the WoWclient to d/c.
					new WaitContinue(TimeSpan.FromSeconds(10),
						context => Query.IsVehicleActionBarShowing(),
						new Action(context =>
						{
							var weaponArticulation = new WeaponArticulation(WeaponAzimuthMin, WeaponAzimuthMax);

							// (slot 1): http://www.wowhead.com/spell=53114
							Weapon_FrozenDeathbolt =
								new VehicleWeapon(ActionBarIndex_Attack, weaponArticulation, WeaponMuzzleVelocity)
								{
									LogAbilityUse = true,
									LogWeaponFiringDetails = false
								};

							// (slot 3): http://www.wowhead.com/spell=53110
							Weapon_DevourHuman =
								new VehicleAbility(ActionBarIndex_Heal)
								{
									LogAbilityUse = true
								};

							WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend, TimeSpan.FromMilliseconds(1500));
						})
					));
		}


		private Composite SubBehaviorPS_UpdatePathWaypoint()
		{
			return
				new Decorator(ret => PathPatrol.Peek().Distance2DSqr(DragonVehicle.Location) <= (30 * 30),
					new ActionFail(ret => PathPatrol.Dequeue()));
		}
		#endregion


		#region Target filtering
		protected override void TargetFilter_RemoveTargets(List<WoWObject> wowObjects)
		{
			wowObjects.Clear();
		}
		#endregion


		#region Helpers
		private WoWPoint FindDistanceClosePoint(WoWUnit wowUnit, double altitudeNeeded)
		{
			var wowUnitLocation = wowUnit.Location;

			return new WoWPoint(wowUnitLocation.X, wowUnit.Location.Y, altitudeNeeded);
		}


		private WoWPoint FindDistanceGainPoint(WoWUnit wowUnit, double minDistanceNeeded)
		{
			var minDistanceNeededSqr = (minDistanceNeeded * minDistanceNeeded);
			var wowUnitLocation = wowUnit.Location;

			return
				(from wowPoint in PathPatrol
				 let distanceSqr = wowUnitLocation.Distance(wowPoint)
				 where distanceSqr > minDistanceNeededSqr
				 orderby distanceSqr
				 select wowPoint)
				 .FirstOrDefault();
		}


		private WoWUnit FindMobToKill(int mobId)
		{
			using (StyxWoW.Memory.AcquireFrame())
			{
				return
					(from wowUnit in ObjectManager.GetObjectsOfType<WoWUnit>()
					 where
						Query.IsViable(wowUnit)
						&& (wowUnit.Entry == mobId)
						&& IsViableTarget(wowUnit)
					 select wowUnit)
					.FirstOrDefault();
			}
		}


		private WoWPoint FindNewStationPoint(WoWUnit desiredTarget, double offsetRadians = 0.0)
		{
			if (!Query.IsViable(desiredTarget))
			{ desiredTarget = FindMobToKill(MobId_ScarletBallista); }

			var myLocation = WoWMovement.ActiveMover.Location;
			var preferredDistance = _random.Next((int)TargetDistance2DMin + 1, (int)TargetDistance2DMax);
			var targetLocation = desiredTarget.Location;

			// To evade, we just rotate in a (counter-clockwise) circle around our selected target...
			var escapeHeading = WoWMathHelper.CalculateNeededFacing(targetLocation, myLocation);
			escapeHeading = WoWMathHelper.NormalizeRadian((float)(escapeHeading + offsetRadians + (TAU / 14)));

			var escapePoint = targetLocation.RayCast(escapeHeading, (float)preferredDistance);
			return new WoWPoint(escapePoint.X, escapePoint.Y, targetLocation.Z).Add(0.0, 0.0, TargetHeightMinimum + TargetHeightVariance);
		}


		private WoWUnit FindSoldierForHeal()
		{
			using (StyxWoW.Memory.AcquireFrame())
			{
				var selectedTargetLocation = Query.IsViable(SelectedTarget)
					? SelectedTarget.Location
					: WoWMovement.ActiveMover.Location;
				const double mobsSurroundingTargetDistanceSqr = (20.0 * 20.0);

				return
				   (from wowUnit in ObjectManager.GetObjectsOfType<WoWUnit>()
					where
						Query.IsViable(wowUnit)
						&& (wowUnit.Entry == HealNpcId)
						&& wowUnit.HealthPercent > 95
						&& IsViableTarget(wowUnit)
						&& (wowUnit.Location.DistanceSqr(selectedTargetLocation) > mobsSurroundingTargetDistanceSqr)
						// Don't try to fetch a soldier from inside a building...
						&& wowUnit.InLineOfSight
					orderby wowUnit.DistanceSqr
					select wowUnit)
					.FirstOrDefault();
			}
		}


		private bool IsIncomingMissile()
		{
			const double impactDistanceSqrConsideration = (MissileImpactClearanceDistance * MissileImpactClearanceDistance);
			const double missileDistanceSqrConsideration = (70.0 * 70.0);
			var myLocation = WoWMovement.ActiveMover.Location;

			using (StyxWoW.Memory.AcquireFrame())
			{
				var isIncomingMissile =
					WoWMissile.InFlightMissiles
					.Any(m =>
						(m.Position.DistanceSqr(myLocation) < missileDistanceSqrConsideration)
						&& (m.ImpactPosition.DistanceSqr(myLocation) < impactDistanceSqrConsideration));

				return isIncomingMissile;
			}
		}

		private bool IsViableTarget(WoWUnit wowUnit)
		{
			// A note about not using wowUnit.IsAlive...
			// There are 'dead' mobs scattered about the battlefield that will
			// have their IsAlive flag set.  This will confuse target selection.
			// So we look for a target with minimal health, instead.
			return
				Query.IsViable(wowUnit)
				&& !_targetBlacklist.Contains(wowUnit.Guid)
				&& wowUnit.IsUntagged()
				&& !wowUnit.HasAura(AuraId_RideVehicleHardcoded)
				&& wowUnit.HealthPercent > 1;
		}


		private IEnumerable<WoWPoint> ParseWoWPoints(IEnumerable<XElement> elements)
		{
			var temp = new List<WoWPoint>();

			foreach (var element in elements)
			{
				var xAttribute = element.Attribute("X");
				var yAttribute = element.Attribute("Y");
				var zAttribute = element.Attribute("Z");

				float x, y, z;
				float.TryParse(xAttribute.Value, out x);
				float.TryParse(yAttribute.Value, out y);
				float.TryParse(zAttribute.Value, out z);
				temp.Add(new WoWPoint(x, y, z).Add(0.0, 0.0, _random.Next(10)));
			}

			return temp;
		}


		private void ParsePaths()
		{
			var endPoint = WoWPoint.Empty;
			var startPoint = WoWPoint.Empty;
			var path = new CircularQueue<WoWPoint>();

			foreach (var point in ParseWoWPoints(Element.Elements().Where(elem => elem.Name == "Start")))
			{ startPoint = point; }

			foreach (var point in ParseWoWPoints(Element.Elements().Where(elem => elem.Name == "End")))
			{ endPoint = point; }

			foreach (var point in ParseWoWPoints(Element.Elements().Where(elem => elem.Name == "Hop")))
			{ path.Enqueue(point); }

			PathStart = startPoint;
			PathEnd = endPoint;
			PathPatrol = path;
		}
		#endregion
	}
}
