// Behavior originally contributed by mastahg
// 24/9/2013 - Practically rewritten, the old behaviour didn't do anything. - Aevitas
// 24-Sep-2013 - Added logic to get in tank. - Chinajade
//              The profile can't use UtilityBehaviorPS.Interact() for this, because the tank
//              goes invalidimmediately after interacting with it, which causes Interact()
//              to throw exceptions because it is unable to finish the job on an InValid object.
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
// Documentation:
// * Moves to Siege Tank, dismounts, and enters tank
// * Fires at targets as it moves around range
// * Behavior is stop/start friendly
// * Accommodates getting thrown out of a vehicle for any reason
//
// Notes:
// * The only time this behavior misses is when weapons platform pitches or rolls.
//   We've looked at techniques to calculate the angle contributions induced by
//   vehicle pitch and roll, but we've yet to find any place with usable information.
//   The most obvious place to look is WoWUnit.GetWorldMatrix(); however, the matrix
//   returned is devoid of meaningful 'Z contributions' for each of the primary axis.
//   <sigh> The search will have to continue some other day.  For now, the misses
//   aren't significant enough to waste significant effort trying to find the buried
//   information--if its available at all.
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
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.Uldum.Battlezone_24910
{
	[CustomBehaviorFileName(@"SpecificQuests\27990-Uldum-Battlezone")]
	public class Battlezone_27990 : QuestBehaviorBase
	{
		#region Constructor and Argument Processing
		public Battlezone_27990(Dictionary<string, string> args)
			: base(args)
		{
			try
			{
				QuestId = 27990;
				TerminationChecksQuestProgress = false;

				MobId_Objective1_DecrepitWatcher = 47385;   // http://wowhead.com/npc=47385
				MobId_SchnottzSiegeTank = 47732;            // http://wowhead.com/npc=47732
				MobId_SchnottzSiegeTankInstanced = 47743;   // http://wowhead.com/npc=47743
				Location_VehicleStagingArea = new WoWPoint(-10697.07, 1106.809, 23.11283);
				Location_ReturnToSchnottz = new WoWPoint(-10674.97, 933.8754, 26.32263);

				// Weapon allows TAU (i.e., 2*PI) horizontal rotation
				WeaponAzimuthMax = 0.785;               // Use: /script print(VehicleAimGetAngle())
				WeaponAzimuthMin = -0.524;              // Use: /script print(VehicleAimGetAngle())
				WeaponMuzzleVelocity = 65.0;            // (slot 1) http://wowhead.com/npc=75560
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

		// Attributes provided by caller
		private WoWPoint Location_VehicleStagingArea { get; set; }
		private WoWPoint Location_ReturnToSchnottz { get; set; }
		private int MobId_Objective1_DecrepitWatcher { get; set; }
		private int MobId_SchnottzSiegeTank { get; set; }
		private int MobId_SchnottzSiegeTankInstanced { get; set; }
		private double WeaponAzimuthMax { get; set; }
		private double WeaponAzimuthMin { get; set; }
		private double WeaponMuzzleVelocity { get; set; }

		protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
		{
			// empty, for now
		}

		protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
		{
			// empty, for now
		}
		#endregion


		#region Private and Convenience variables
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
		private WoWUnit SelectedTarget { get; set; }
		private WoWUnit Vehicle { get; set; }
		private VehicleWeapon WeaponFireCannon { get; set; }

		private BehaviorStateType _behaviorState;
		private readonly LocalBlacklist _targetBlacklist = new LocalBlacklist(TimeSpan.FromSeconds(30));
		#endregion


		#region Overrides of CustomForcedBehavior
		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Rev$"); } }

		// CreateBehavior supplied by QuestBehaviorBase.
		// Instead, provide CreateMainBehavior definition.

		// Dispose provided by QuestBehaviorBase.

		// IsDone provided by QuestBehaviorBase.
		// Call the QuestBehaviorBase.BehaviorDone() method when you want to indicate your behavior is complete.

		// OnFinished provided by QuestBehaviorBase.

		public override void OnStart()
		{
			// Let QuestBehaviorBase do basic initializaion of the behavior, deal with bad or deprecated attributes,
			// capture configuration state, install BT hooks, etc.  This will also update the goal text.
			var isBehaviorShouldRun = OnStart_QuestBehaviorCore();

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (isBehaviorShouldRun)
			{
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
		protected override Composite CreateBehavior_CombatMain()
		{
			return new PrioritySelector(
				// empty, for now...
				);
		}


		protected override Composite CreateBehavior_CombatOnly()
		{
			return new PrioritySelector(
				// Disable combat routine while we are in the vehicle...
				new Decorator(context => Query.IsInVehicle(),
					new ActionAlwaysSucceed())
				);
		}


		protected override Composite CreateBehavior_DeathMain()
		{
			return new PrioritySelector(
				// empty, for now...
				);
		}


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
		#endregion


		#region Behavior States
		private Composite StateBehaviorPS_MountingVehicle()
		{
			return new PrioritySelector(
				new Decorator(context => Me.IsQuestComplete(QuestId),
					new Action(context => { BehaviorDone(); })),

				// If we're in the vehicle, wait for the ride out to hunting grounds to complete...
				new Decorator(context => IsInTank(),
					new PrioritySelector(
						SubBehaviorPS_InitializeVehicleAbilities(),
						new Action(context => { BehaviorState = BehaviorStateType.RidingOutToHuntingGrounds; })
					)),

				// If vehicle is in "enter vehicle" animation, wait for the animation to complete...
				new Decorator(context => FindVehicle_OwnedByMe(MobId_SchnottzSiegeTankInstanced) != null,
					new PrioritySelector(
						new CompositeThrottle(Throttle.UserUpdate,
							new Action(context =>
							{
								TreeRoot.StatusText =
									string.Format("Waiting for {0} to become ready.",
										Utility.GetObjectNameFromId(MobId_SchnottzSiegeTankInstanced));
							})),
						new ActionAlwaysSucceed()
					)),
					
				// Locate a vehicle to mount...
				new Decorator(context => !Query.IsViable(Vehicle),
					new PrioritySelector(
						new Action(context =>
						{
							Vehicle =
								Query.FindMobsAndFactions(Utility.ToEnumerable(MobId_SchnottzSiegeTank))
								.FirstOrDefault()
								as WoWUnit;

							if (Query.IsViable(Vehicle))
							{
								Utility.Target(Vehicle);
								return RunStatus.Success;
							}

							return RunStatus.Failure;   // fall through
						}),

						// No vehicle found, move to staging area...
						new ActionRunCoroutine(
						    interactUnitContext => UtilityCoroutine.MoveTo(
						        Location_VehicleStagingArea,
						        "Vehicle Staging Area",
                                MovementBy)),

						// Wait for vehicle to respawn...
						new CompositeThrottle(Throttle.UserUpdate,
							new Action(context =>
							{
								TreeRoot.StatusText =
									string.Format("Waiting for {0} to respawn.", Utility.GetObjectNameFromId(MobId_SchnottzSiegeTank));
							}))
					)),

				// Move to vehicle and enter...
				new CompositeThrottle(Throttle.UserUpdate,
                    new Action(context => { TreeRoot.StatusText = string.Format("Moving to {0}", Vehicle.SafeName); })),
				new Decorator(context => !Vehicle.WithinInteractRange,
				    new ActionRunCoroutine(
				        interactUnitContext => UtilityCoroutine.MoveTo(
				            Vehicle.Location,
				            Vehicle.SafeName,
				            MovementBy))),
				new Decorator(context => Me.IsMoving,
					new Action(context => { Navigator.PlayerMover.MoveStop(); })),
				new Decorator(context => Me.Mounted,
                    new ActionRunCoroutine(context => UtilityCoroutine.ExecuteMountStrategy(MountStrategyType.DismountOrCancelShapeshift))),
				new ActionFail(context =>
				{
					// If we got booted out of a vehicle for some reason, reset the weapons...
					WeaponFireCannon = null;

					Utility.Target(Vehicle);
					Vehicle.Interact();
				}),
				new Wait(TimeSpan.FromMilliseconds(10000), context => IsInTank(), new ActionAlwaysSucceed()),
				new ActionAlwaysSucceed()
			);
		}


		private Composite StateBehaviorPS_RidingOutToHuntingGrounds()
		{
			return new PrioritySelector(
				// If for some reason no longer in the vehicle, go fetch another...
				new Decorator(context => !IsInTank(),
					new Action(context =>
					{
						QBCLog.Warning("We've been jettisoned from vehicle unexpectedly--will try again.");
						BehaviorState = BehaviorStateType.MountingVehicle;
					})),

				new Decorator(context => WeaponFireCannon.IsWeaponUsable(),
					new Action(context => { BehaviorState = BehaviorStateType.CompletingObjectives; })),

				new CompositeThrottle(Throttle.UserUpdate,
					new Action(context => { TreeRoot.StatusText = "Riding out to hunting grounds"; }))
			);            
		}


		private Composite StateBehaviorPS_CompletingObjectives()
		{
			return new PrioritySelector(
				// If for some reason no longer in the vehicle, go fetch another...
				new Decorator(context => !IsInTank(),
					new Action(context =>
					{
						QBCLog.Warning("We've been jettisoned from vehicle unexpectedly--will try again.");
						BehaviorState = BehaviorStateType.MountingVehicle;
					})),

				// If quest is complete, then head back...
				new Decorator(context => Me.IsQuestObjectiveComplete(QuestId, 1),
					new Action(context => { BehaviorState = BehaviorStateType.ReturningToBase; })),

				new CompositeThrottle(Throttle.UserUpdate,
					new Action(context => { TreeRoot.StatusText = "Completing Quest Objectives"; })),

				// Select new best target, if our current one is no longer useful...
				new Decorator(context => !IsViableForTargeting(SelectedTarget),
					new ActionFail(context =>
					{
						SelectedTarget = FindBestTarget(MobId_Objective1_DecrepitWatcher);
						// fall through
					})),

				// Aim & Fire at the selected target...
				new Decorator(context => IsViableForTargeting(SelectedTarget),
					new Sequence(
						new Action(context =>
						{
							// If weapon aim cannot address selected target, blacklist target for a few seconds...
							if (!WeaponFireCannon.WeaponAim(SelectedTarget))
							{
								_targetBlacklist.Add(SelectedTarget, TimeSpan.FromSeconds(5));
								return RunStatus.Failure;
							}

							// If weapon could not be fired, wait for it to become ready...
							if (!WeaponFireCannon.WeaponFire())
								{ return RunStatus.Failure; }

							return RunStatus.Success;
						}),
						new WaitContinue(Delay.AfterWeaponFire, context => false, new ActionAlwaysSucceed())
					))
			);            
		}


		private Composite StateBehaviorPS_ReturningToBase()
		{
			// If we are not returning home...
			return new PrioritySelector(
				new Decorator(context => IsInTank(),
					new CompositeThrottle(Throttle.UserUpdate,
						new ActionFail(context => { TreeRoot.StatusText ="Returning to base"; })
					)),

				new Decorator(context => !Navigator.AtLocation(Location_ReturnToSchnottz),
				    new ActionRunCoroutine(
				        interactUnitContext => UtilityCoroutine.MoveTo(
				            Location_ReturnToSchnottz,
				            "Commander Schnottz",
				            MovementBy))),

				new Decorator(context => Me.IsQuestComplete(QuestId),
					new Action(context => BehaviorDone("quest complete")))
				);
		}


		private Composite SubBehaviorPS_InitializeVehicleAbilities()
		{
			return
				new Decorator(context => (WeaponFireCannon == null),
					// Give the WoWclient a few seconds to produce the vehicle action bar...
					// NB: If we try to use the weapon too quickly after entering vehicle,
					// then it will cause the WoWclient to d/c.
					new WaitContinue(TimeSpan.FromSeconds(10),
						context => Query.IsVehicleActionBarShowing(),
						new Action(context =>
						{
							var weaponArticulation = new WeaponArticulation(WeaponAzimuthMin, WeaponAzimuthMax);

							// (slot 1): http://wowhead.com/spell=
							WeaponFireCannon =
								new VehicleWeapon(1, weaponArticulation, WeaponMuzzleVelocity)
								{
									LogAbilityUse = true,
									LogWeaponFiringDetails = false
								};
						})
					));
		}
		#endregion


		#region Helpers
		private WoWUnit FindBestTarget(int targetId)
		{
			using (StyxWoW.Memory.AcquireFrame())
			{
				return
				   (from wowUnit in ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
					where
						(wowUnit.Entry == targetId)
						&& IsViableForTargeting(wowUnit)
					orderby
						wowUnit.DistanceSqr
					select wowUnit)
					.FirstOrDefault();
			}
		}


		private WoWUnit FindVehicle_OwnedByMe(int targetId)
		{
			using (StyxWoW.Memory.AcquireFrame())
			{
				var meGuid = Me.Guid;

				return
				   (from wowUnit in ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
					where
						(wowUnit.Entry == targetId)
						&& (wowUnit.CreatedByUnitGuid == meGuid)
					orderby
						wowUnit.DistanceSqr
					select wowUnit)
					.FirstOrDefault();
			}
		}


		private bool IsInTank()
		{
			return Query.IsInVehicle();
		}


		private bool IsViableForTargeting(WoWUnit wowUnit)
		{
			return
				Query.IsViable(wowUnit)
				&& !_targetBlacklist.Contains(wowUnit.Guid)
				&& wowUnit.IsAlive;
		}
		#endregion
	}
}
