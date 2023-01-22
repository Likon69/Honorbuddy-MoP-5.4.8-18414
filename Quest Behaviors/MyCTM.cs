// Behavior originally contributed by Natfoth.
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
// QUICK DOX:
// The MYCTM behavior is used in those situations for which mesh navigation is not possible, or
// you are flying and need to cross certain world boundaries that the flying navigator (Flightor)
// needs to honor.
//
// BEHAVIOR ATTRIBUTES:
// Basic Attributes:
//      X/Y/Z [REQUIRED;  Default: none]
//          This specifies the destination to which the behavior should move.
//
// Tunables:
//      DestName [optional;  Default: coordinates of destination]
//          Specifies the name of the destination being obtained.
//          Profile writers should provide this value whenever possible.  It is much easier for
//          a user to understand "Cosmowrench" rather than "<2988.93, 1836.109, 275.2884>".
//      UpperLimitOnMovementTime [optional;  Default: 300000 (five mins)]
//          Places an upper limit on the amount of time tat can elapse before the MyCTM action is considered
//          problematical and the profile should be stopped.  Since toons travel at various speeds (foot,
//          ground mount, flying mount, swimming, etc), it may become necessary to allow additional time
//          in some environments.
//      UseRelative [optional;  Default: false]
//          Relative coordinates are used, rather than absolute coordinates.
//          Relative coordinates must be used for 'transport' type platorms, such as the 'flying boats'
//          found in Icecrown and Deepholm.  Such platforms are disconnected from the main mesh,
//          and their absolute coordinates constantly change even when the toon is not moving
//          while standing on the platform.  Thus, the need for relative coordinates which are
//          based relative to the platform origin, not the zone's origin.
//
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Xml.Linq;

using Bots.Grind;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;

using Action = Styx.TreeSharp.Action;
using Vector3 = Tripper.Tools.Math.Vector3;
#endregion


namespace Honorbuddy.Quest_Behaviors.MyCTM
{
	[CustomBehaviorFileName(@"MyCTM")]
	public class MyCTM : QuestBehaviorBase
	{
		#region Constructor and Argument Processing
		public MyCTM(Dictionary<string, string> args) : base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				DestinationName = GetAttributeAs<string>("DestName", false, ConstrainAs.StringNonEmpty, new[] {"Name"}) ?? "";
				Destination = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
				UseRelativeLocation = GetAttributeAsNullable<bool>("UseRelative", false, null, new[] {"useRelative"}) ?? false;

				var upperLimitOnMovementTime = GetAttributeAsNullable<int>("UpperLimitOnMovementTime", false, ConstrainAs.Milliseconds, null)
					?? (5 * 60 * 1000); // five mins
				UpperLimitOnMovementTime = TimeSpan.FromMilliseconds(upperLimitOnMovementTime);

				OrigDestination = Destination;

				if (string.IsNullOrEmpty(DestinationName))
				{
					DestinationName = Destination.ToString();
				}
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
			//// EXAMPLE: 
			//UsageCheck_DeprecatedAttribute(xElement,
			//    Args.Keys.Contains("Nav"),
			//    "Nav",
			//    context => string.Format("Automatically converted Nav=\"{0}\" attribute into MovementBy=\"{1}\"."
			//                              + "  Please update profile to use MovementBy, instead.",
			//                              Args["Nav"], MovementBy));
		}

		protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
		{
			//// EXAMPLE:
			//UsageCheck_SemanticCoherency(xElement,
			//    (!MobIds.Any() && !FactionIds.Any()),
			//    context => "You must specify one or more MobIdN, one or more FactionIdN, or both.");
			//
			//const double rangeEpsilon = 3.0;
			//UsageCheck_SemanticCoherency(xElement,
			//    ((RangeMax - RangeMin) < rangeEpsilon),
			//    context => string.Format("Range({0}) must be at least {1} greater than MinRange({2}).",
			//                  RangeMax, rangeEpsilon, RangeMin)); 
		}


		// Attributes provided by caller
		private WoWPoint Destination { get; set; }
		private string DestinationName { get; set; }
		private WoWPoint OrigDestination { get; set; }
		private bool UseRelativeLocation { get; set; }
		private TimeSpan UpperLimitOnMovementTime { get; set; }
		#endregion


		#region Private and Convenience variables
		private WoWMovement.MovementDirection _antiStuckMoveDirection = WoWMovement.MovementDirection.None;
		private WoWPoint _antiStuckMyLoc = WoWPoint.Empty;
		private bool _antiStuckPerformSimpleSequence = false;
		private WoWPoint _antiStuckPrevPosition = WoWPoint.Empty;
		private readonly WaitTimer _antiStuckStuckSucceedTimer = new WaitTimer(TimeSpan.FromSeconds(6));
		private WaitTimer _runTimer;
		readonly WaitTimer _stuckTimer = new WaitTimer(TimeSpan.FromSeconds(2));
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
			// Let QuestBehaviorBase do basic initializaion of the behavior, deal with bad or deprecated attributes,
			// capture configuration state, install BT hooks, etc.  This will also update the goal text.
			var isBehaviorShouldRun = OnStart_QuestBehaviorCore("CTMoving to " + DestinationName);

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (isBehaviorShouldRun)
			{
				// Disable any settings that may cause us to dismount --
				// When we mount for travel via FlyTo, we don't want to be distracted by other things.
				// NOTE: the ConfigMemento in QuestBehaviorBase restores these settings to their
				// normal values when OnFinished() is called.
				LevelBot.BehaviorFlags &= ~(BehaviorFlags.Loot | BehaviorFlags.Pull);

				Navigator.NavigationProvider.StuckHandler.Reset();
			}
		}
		#endregion


		#region Main Behaviors
		protected override Composite CreateBehavior_CombatMain()
		{
			return new PrioritySelector(
				new Decorator(context => !IsDone && !Me.IsActuallyInCombat,
					new PrioritySelector(

						// Update Location if relative coord is used...
						// N.B. Relative locations are only used while on transports and because
						// transports are usually moving around 'Location' needs to be updated on every frame.
						new Decorator(context => UseRelativeLocation,
							new Action(context => Destination = CalculateRelativeLocation(OrigDestination))),

						// Initialize the timer...
						new Decorator(context => _runTimer == null,
							new Action(context =>
							{
								_runTimer = new WaitTimer(Destination.MaximumTraversalTime(2.5, TimeSpan.FromSeconds(20), UpperLimitOnMovementTime));
								QBCLog.DeveloperInfo("Maximum allowed time to reach destination: {0} seconds",
									_runTimer.WaitTime.TotalSeconds);
								_runTimer.Reset();
								return RunStatus.Failure;
							})),

						// Stop HB if _runTimer finishes...
						new Decorator(context => _runTimer.IsFinished,
							new Action(context =>
								{
									WoWMovement.MoveStop();

									// N.B. set the runtimer to null so if player manually correct
									// problem and starts bot up it restarts the timer.
									_runTimer = null;

									QBCLog.Fatal("MyCTM is not able to reach {0} from {1}",
										DestinationName,
										WoWMovement.ActiveMover.Location);
								})),

						// Run stuckhandler
						CreateBehavior_Antistuck(),
						// Default anti-stuck has issues.
						// new Decorator(context => Navigator.NavigationProvider.StuckHandler.IsStuck(),
						//     new Action(context => Navigator.NavigationProvider.StuckHandler.Unstick())),

						// check if bot has reached the destination.
						new Decorator(context => Destination.DistanceSqr(Me.Location) <= (3 * 3),
							new Action(context =>
								{
									BehaviorDone(string.Format("Finished moving to {0}", DestinationName));

									// Drop down to 'CreateBehavior_PerformCTM' to ensure ctm is performed
									// at least once if start and destination locations are very close on start 
									return RunStatus.Failure;
								})),
						CreateBehavior_PerformCTM()
				)),

				// _runTimer needs to be recalculated after combat is over and stuck timer needs to rest.
				new Decorator(context => Me.IsActuallyInCombat,
					new Action(context =>
						{
							_runTimer = null;
							_stuckTimer.Reset();
							return RunStatus.Failure;
						}))
			);
		}
		#endregion


		#region Helpers
		private Composite CreateBehavior_Antistuck()
		{
			return new PrioritySelector(
				new Decorator(context => _stuckTimer.IsFinished,
					new Sequence(context => _antiStuckMyLoc = WoWMovement.ActiveMover.Location,

						// Check if stuck...
						new DecoratorContinue(context => _antiStuckMyLoc.DistanceSqr(_antiStuckPrevPosition) < (3 * 3),
							new Sequence(context => _antiStuckPerformSimpleSequence = _antiStuckStuckSucceedTimer.IsFinished,
								new DecoratorContinue(context => Me.IsMounted() && !Me.IsFlying,
                                    new ActionRunCoroutine(context => CommonCoroutines.Dismount("Stuck"))),

								// Perform simple unstuck proceedure...
								new DecoratorContinue(context => _antiStuckPerformSimpleSequence,
									new Sequence(
										new Action(context => QBCLog.Debug("Stuck. Trying to jump")),
										new Action(context =>
										{
											// ensure bot is moving forward when jumping (Wow will sometimes automatically
											// stop moving if running against a wall)
											if (ShouldPerformCTM)
												WoWMovement.ClickToMove(Destination);
											WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
										}),
										new Sleep(1000),
										new Action(context => WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend))
									)),

								// perform less simple unstuck proceedure
								new DecoratorContinue(context => !_antiStuckPerformSimpleSequence,
									new Sequence(context => _antiStuckMoveDirection = GetRandomMovementDirection(),
										new Action(context => QBCLog.Debug("Stuck. Movement Directions: {0}", _antiStuckMoveDirection)),
										new Action(context => WoWMovement.Move(_antiStuckMoveDirection)),
										new Sleep(2000),
										new Action(context => WoWMovement.MoveStop(_antiStuckMoveDirection)))),

								new Action(context => _antiStuckStuckSucceedTimer.Reset()))),

						new Action(context => _antiStuckPrevPosition = _antiStuckMyLoc),
						new Action(context => _stuckTimer.Reset())
					)));
		}


		private Composite CreateBehavior_PerformCTM()
		{
			// CTM only if not moving or current ctm position isn't our destination.
			return new Decorator(context => ShouldPerformCTM,
				new Sequence(
					new Action(ctx => WoWMovement.ClickToMove(Destination)),
					new Sleep(100)));
		}

		
		private WoWMovement.MovementDirection GetRandomMovementDirection()
		{
			// randomly move left or ritht
			WoWMovement.MovementDirection randomDirection = 
				(_random.Next(2) == 0)
				? WoWMovement.MovementDirection.StrafeLeft
				: WoWMovement.MovementDirection.StrafeRight;

			// randomly choose to go diagonal backwords + left or right
			if (_random.Next(2) == 0)
				randomDirection |= WoWMovement.MovementDirection.Backwards;

			// randomly choose to jump (or descend if flying or swimming)
			if (_random.Next(2) == 0)
			{
				var activeMover = WoWMovement.ActiveMover;
				if (activeMover.IsFlying || activeMover.IsSwimming)
				{
					randomDirection |= 
						(_random.Next(2) == 0)
						? WoWMovement.MovementDirection.JumpAscend
						: WoWMovement.MovementDirection.Descend;
				}
				else
				{
					randomDirection |= WoWMovement.MovementDirection.JumpAscend;
				}
			}
			return randomDirection;
		}


		private WoWPoint CalculateRelativeLocation(WoWPoint origDestination)
		{
			return Vector3.Transform(origDestination, Me.Transport.GetWorldMatrix());
		}


		private bool ShouldPerformCTM
		{
			get
			{
				return !WoWMovement.ActiveMover.IsMoving ||
					   WoWMovement.ClickToMoveInfo.ClickPos.DistanceSqr(Destination) > (0.5 * 0.5);
			}
		}
		#endregion
	}
}