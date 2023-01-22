// Behavior originally contributed by Bobby53 / rework by chinajade
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
// GOTHRUPORTAL walks through portals in a way that does not result in
// WoW "red error" messages, or error messages in the HB log/debug files.
//
// You provide an XYZ coordinate as close as possible to the portal entrance
// without entering (within a couple of yards).  GOTHRUPORTAL then calculates
// a vector from your current position that passes through the provided XYZ.  The endpoint
// of this vector is 15 yards away from your current position.  The toon should
// intercept the portal and zone, before it gets to the calculated endpoint.
//
// If portal entry fails, this behavior will try a few times before giving up.
// Portal entry failures are most often caused by hiccups in the destination WoWserver
// instance.  The number of times, and the delay between attempts is adjustable
// with tunable parameters.
// 
// BEHAVIOR ATTRIBUTES:
// *** ALSO see the documentation in QuestBehaviorBase.cs.  All of the attributes it provides
// *** are available here, also.  The documentation on the attributes QuestBehaviorBase provides
// *** is _not_ repeated here, to prevent documentation inconsistencies.
//
// Basic Attributes:
//      X/Y/Z [REQUIRED]:
//          Defines a point immediately in front of the portal.  A vector will be created
//          that originates from the toon's current position, and passes through this
//          X/Y/Z value.  The portal entrance should lie along this vector.
//          The length of the created vector is 15 yards.
//
// Tunables:
//      InitialX/InitialY/InitialZ [optional; Default: toon's current position]
//          Represents the location the toon should be standing when it uses the X/Y/Z
//          to calculate a vector through the portal.
//      MaxRetryCount [optional; Default: 3]
//          The number of attempts the behavior will make to enter the portal.
//          After this number of retries is exhausted, the behavior gives up and
//          terminates the profile.
//      RetryDelay [optional; Default: 90 (seconds)]:
//          The amount of time the behavior should wait before attempting to re-enter
//          the portal from a failed attempt. If the destination instance server is having
//          issues, we don't want the retries to be too close together.
//      Timeout [optional; Default: 10000 (milliseconds)]:
//          Represents the maximum number of milliseconds the caller expects entering
//          the portal to take.  If the portal is not entered in this amount of time,
//          the retry process is engaged.
//
#endregion


#region Examples
//
// Use MoveTo to get start position, then GoThruPortal to run through XYZ vector
// on way through portal.
//     <MoveTo X="4646.201" Y="-3685.043" Z="954.2496" />
//     <CustomBehavior File="GoThruPortal" X="4656.928" Y="-3685.472" Z="957.185" />
// 
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Xml.Linq;

using Bots.Grind;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.GoThruPortal
{
	[CustomBehaviorFileName(@"GoThruPortal")]
	public class GoThruPortal : QuestBehaviorBase
	{
		#region Constructor and Argument Processing
		public GoThruPortal(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// NB: Core attributes are parsed by QuestBehaviorBase parent (e.g., QuestId, NonCompeteDistance, etc)

				// Behavior-specific attributes...
				MovePoint = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;

				// Tunables...
				StartingPoint = GetAttributeAsNullable<WoWPoint>("Initial", false, ConstrainAs.WoWPointNonEmpty, null) ?? Me.Location;
				MaxRetryCount = GetAttributeAsNullable<int>("MaxRetryCount", false, new ConstrainTo.Domain<int>(1, 10), null) ?? 3;
				int retryDelay = GetAttributeAsNullable<int>("RetryDelay", false, new ConstrainTo.Domain<int>(0, 300000), null) ?? 90000;
				int zoningMaxWaitTime = GetAttributeAsNullable<int>("Timeout", false, new ConstrainTo.Domain<int>(1, 60000), null) ?? 10000;

				MovePoint = WoWMathHelper.CalculatePointFrom(StartingPoint, MovePoint, -15.0f);
				RetryDelay = TimeSpan.FromMilliseconds(retryDelay);
				MaxTimeToPortalEntry = TimeSpan.FromMilliseconds(zoningMaxWaitTime);
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
		private WoWPoint MovePoint { get; set; }
		private int MaxRetryCount { get; set; }
		private TimeSpan RetryDelay { get; set; }
		private WoWPoint StartingPoint { get; set; }
		private TimeSpan MaxTimeToPortalEntry { get; set; }

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
		#endregion


		#region Private and Convenience variables
		private bool SawLoadingScreen { get; set; }

		private bool _tookPortal;
		// Private properties
		private bool TookPortal
		{
			get
			{
				_tookPortal |= BigChangeInPosition | SawLoadingScreen;
				return _tookPortal;
			}
		}

		private PerFrameCachedValue<bool> _bigChangeInPosition;

		private readonly TimeSpan PostZoningDelay = TimeSpan.FromMilliseconds(1250);

		private Stopwatch RetryDelayTimer { get; set; }
		private WoWPoint LastLocation { get; set; }
		private float LastForwardSpeed { get; set; }
		private Stopwatch PulseTimer { get; set; }
		private int _retryCount = 1;
		private Composite _behaviorTreeHook_InGameCheck;

		#endregion


		#region Overrides of CustomForcedBehavior
		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }


		public override void OnFinished()
		{
			// Defend against being called multiple times (just in case)...
			if (!IsOnFinishedRun)
			{
				TreeHooks.Instance.RemoveHook("InGame_Check", CreateBehavior_InGameCheck());
				// QuestBehaviorBase.OnFinished() will set IsOnFinishedRun...
				base.OnFinished();
			}
		}


		public override void OnStart()
		{
			// Let QuestBehaviorBase do basic initialization of the behavior, deal with bad or deprecated attributes,
			// capture configuration state, install BT hooks, etc.  This will also update the goal text.
			var isBehaviorShouldRun = OnStart_QuestBehaviorCore("Moving through Portal");

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (isBehaviorShouldRun)
			{
				TreeHooks.Instance.InsertHook("InGame_Check", 0, CreateBehavior_InGameCheck());
				LastLocation = Me.Location;
				PulseTimer = Stopwatch.StartNew();
				LastForwardSpeed = GetFowardSpeed();
			}
		}
		#endregion


		#region Main Behaviors

		private Composite CreateBehavior_InGameCheck()
		{
			return _behaviorTreeHook_InGameCheck ?? (_behaviorTreeHook_InGameCheck = new Action(
				context =>
				{
					if (!SawLoadingScreen && !StyxWoW.IsInWorld)
					{
						QBCLog.DeveloperInfo("Detected a loading screen.");
						SawLoadingScreen = true;
					}
					return RunStatus.Failure;
				}));
		}

		protected override Composite CreateMainBehavior()
		{
			return new ActionRunCoroutine(ctx => MainCoroutine());
		}

		private async Task<bool> MainCoroutine()
		{
			if (IsDone)
				return false;

			if (TookPortal)
			{
				if (Me.IsMoving)
					await CommonCoroutines.StopMoving();
				await Coroutine.Sleep(PostZoningDelay);
				BehaviorDone("Zoned into portal");
				return true;
			}

			if (Navigator.AtLocation(StartingPoint) && await WaitToRetry())
				return true;

			// Move to portal starting position...
			if (await UtilityCoroutine.MoveTo(StartingPoint, "Portal", MovementBy))
				return true;

			// If we're not at StartingPoint then something seriously went wrong.
			if (!Navigator.AtLocation(StartingPoint))
			{
				QBCLog.Fatal("Unable to Navigate to StartingPoint");
				return true;
			}

			if (await EnterPortal())
				return true;

			// Zoning failed, do we have any retries left?
			_retryCount += 1;
			if (_retryCount > MaxRetryCount)
			{
				var message = string.Format("Unable to go through portal in {0} attempts.", MaxRetryCount);

				// NB: Posting a 'fatal' message will stop the bot--which is what we want.
				QBCLog.Fatal(message);
				BehaviorDone(message);
				return true;
			}

			RetryDelayTimer = new Stopwatch();
			return true;
		}

		private async Task<bool> EnterPortal()
		{
			var portalEntryTimer = new WaitTimer(MaxTimeToPortalEntry);
			portalEntryTimer.Reset();
			QBCLog.DeveloperInfo("Portal Entry Timer Started");

			while (true)
			{
				if (TookPortal)
					return true;

				// If portal entry timer expired, deal with it...
				if (portalEntryTimer.IsFinished)
				{
					QBCLog.Warning(
						"Unable to enter portal within allotted time of {0}",
						Utility.PrettyTime(MaxTimeToPortalEntry));
					break;
				}

				// If we are within 2 yards of calculated end point we should never reach...
				if (Me.Location.Distance(MovePoint) < 2)
				{
					QBCLog.Warning("Seems we missed the portal. Is Portal activated? Profile needs to pick better alignment?");
					break;
				}

				// If we're not moving toward portal, get busy...
				if (!StyxWoW.Me.IsMoving || Navigator.AtLocation(StartingPoint))
				{
					QBCLog.DeveloperInfo("Entering portal via {0}", MovePoint);
					WoWMovement.ClickToMove(MovePoint);
				}
				await Coroutine.Yield();
			}
			return false;
		}

		private async Task<bool> WaitToRetry()
		{
			if (RetryDelayTimer == null) 
				return false;

			if (!RetryDelayTimer.IsRunning)
			{
				QBCLog.Info(
					"Last portal entry attempt failed.  Will try re-entering portal again in {0} (try #{1}).",
					Utility.PrettyTime(RetryDelay),
					_retryCount);

				RetryDelayTimer.Start();
			}

			// if the retry timer is running wait for it to expire.
			if (RetryDelayTimer.Elapsed < RetryDelay)
			{
				TreeRoot.StatusText =
					string.Format(
						"Retrying portal entry in {0} of {1}.",
						Utility.PrettyTime(RetryDelay - RetryDelayTimer.Elapsed),
						Utility.PrettyTime(RetryDelay));
				return true;
			}
			RetryDelayTimer = null;
			return false;
		}

		#endregion

		#region Helpers

		private static float GetFowardSpeed()
		{
			if (Me.IsFlying)
				return Me.MovementInfo.FlyingForwardSpeed;
			if (Me.IsSwimming)
				return Me.MovementInfo.SwimmingForwardSpeed;
			return Me.MovementInfo.ForwardSpeed;
		}

		private bool BigChangeInPosition
		{
			get
			{
				return _bigChangeInPosition ?? (_bigChangeInPosition = new PerFrameCachedValue<bool>(
				() =>
				{
					var myLoc = Me.Location;
					var distToPrevLoc = myLoc.Distance(LastLocation);
					var secondsSinceLastPulse = PulseTimer.ElapsedMilliseconds / 1000f;
					LastLocation = myLoc;
					PulseTimer.Restart();
					// ignore small changes in distance.
					if (distToPrevLoc < 50)
						return false;

					var distPerSecond = distToPrevLoc / secondsSinceLastPulse;
					// The fastest travel speed is about 34.44 with highest riding skill level and guild bonuses.
					// Check if player moved further then the speed would have allowed him/her to travel, indicating that player 
					// was ported.
					var result = distPerSecond * 1.5 > LastForwardSpeed;
					PulseTimer.Reset();
					LastForwardSpeed = GetFowardSpeed();

					if (result)
						QBCLog.DeveloperInfo("Detected a big change in position");

					return result;
				}));
			}
		}

		#endregion

	}
}

