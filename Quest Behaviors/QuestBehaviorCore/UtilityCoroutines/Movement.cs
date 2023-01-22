using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Honorbuddy.QuestBehaviorCore.XmlElements;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Profiles.Quest.Order;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.CommonBot.Coroutines;
using Action = Styx.TreeSharp.Action;

namespace Honorbuddy.QuestBehaviorCore
{
	public static partial class UtilityCoroutine
	{
		private static WaitTimer _moveToMessageThrottle = null;

		private static bool IsMoveToMessageThrottled
		{
			get
			{
				if (_moveToMessageThrottle == null)
					_moveToMessageThrottle = new WaitTimer(TimeSpan.FromMilliseconds(1000));

				if (!_moveToMessageThrottle.IsFinished)
					return true;
				_moveToMessageThrottle.Reset();
				return false;
			}
		}

		public static async Task MoveStop()
		{
			Navigator.PlayerMover.MoveStop();
			await Coroutine.Wait((int) Delay.LagDuration.TotalMilliseconds, () => !WoWMovement.ActiveMover.IsMoving);
		}

		public static async Task<bool> MoveTo(
			HuntingGroundsType huntingGrounds,
			MovementByType movementBy = MovementByType.FlightorPreferred)
		{
			Contract.Requires(huntingGrounds != null, context => "huntingGrounds may not be null");
			var destination = huntingGrounds.CurrentWaypoint().Location;
			var destinationName = String.Format("hunting ground waypoint '{0}'", huntingGrounds.CurrentWaypoint().Name);
			return await MoveTo(destination, destinationName, movementBy);
		}

		public static async Task<bool> MoveTo(
			WoWPoint destination,
			string destinationName,
			MovementByType movementBy = MovementByType.FlightorPreferred)
		{
			Contract.Requires(destinationName != null, context => "destinationName may not be null");

			if (movementBy == MovementByType.None)
			{
				return false;
			}

			var activeMover = WoWMovement.ActiveMover;
			if (activeMover == null)
			{
				return false;
			}

			if (!IsMoveToMessageThrottled)
			{
				if (string.IsNullOrEmpty(destinationName))
					destinationName = destination.ToString();
				TreeRoot.StatusText = "Moving to " + destinationName;
			}

			if (!Navigator.AtLocation(destination))
			{
				switch (movementBy)
				{
					case MovementByType.FlightorPreferred:
						if (await TryFlightor(destination))	
							return true;

						if (await TryNavigator(destination, destinationName))
							return true;

						if (await TryClickToMove(destination, NavType.Fly))
							return true;
						break;
					case MovementByType.NavigatorPreferred:
						if (await TryNavigator(destination, destinationName))
							return true;

						if (await TryClickToMove(destination, NavType.Run))
							return true;
						break;
					case MovementByType.NavigatorOnly:
						if (await TryNavigator(destination, destinationName))
							return true;
						break;
					case MovementByType.ClickToMoveOnly:
						var navType = activeMover.MovementInfo.CanFly ? NavType.Fly : NavType.Run;
						if (await TryClickToMove(destination, navType))
							return true;
						break;
					case MovementByType.None:
						break;
					default:
						QBCLog.MaintenanceError("Unhandled MovementByType of {0}", movementBy);
						break;
				}
			}
			return false;
		}

		private static async Task<bool> SetMountState(WoWPoint destination, NavType navType = NavType.Fly)
		{
			// Are we mounted, and not supposed to be?
			if (!Mount.UseMount && Me.IsMounted())
			{
				if (await ExecuteMountStrategy(MountStrategyType.Dismount))
					return true;
			}
			// Are we unmounted, and mount use is permitted?
			// NB: We don't check for IsMounted(), in case the ExecuteMountStrategy decides a mount switch is necessary
			// (based on NavType).
			if (Mount.UseMount && destination.CollectionDistance(WoWMovement.ActiveMover.Location) > CharacterSettings.Instance.MountDistance)
			{
				if (await ExecuteMountStrategy(MountStrategyType.Mount, navType))
					return true;
			}
			return false;
		}


		private static async Task<bool> TryClickToMove(
			WoWPoint destination,
			NavType navType = NavType.Fly)
		{
			var activeMover = WoWMovement.ActiveMover;
			// NB: Do not 'dismount' for CtM.  We may be using it for aerial navigation, also.
			if (await SetMountState(destination, navType))
				return true;

			// If Navigator can generate a parital path for us, take advantage of it...
			var tempDestination =
				Navigator.GeneratePath(activeMover.Location, destination)
					.Where(p => !Navigator.AtLocation(p))
					.DefaultIfEmpty(destination)
					.FirstOrDefault();

			WoWMovement.ClickToMove(tempDestination);
			return true;
		}


		private static async Task<bool> TryFlightor(WoWPoint destination)
		{
			// If a toon can't fly, skip this...
			// NB: Although Flightor will fall back to Navigator, there are side-effects.
			// Flightor will mount even if UseMount is disabled.  So, if we don't want to mount
			// we don't want to even try Flightor; otherwise, unexpected side-effects can ensue.
			var activeMover = WoWMovement.ActiveMover;
			if (Mount.UseMount || activeMover.IsSwimming
				|| !Navigator.CanNavigateWithin(activeMover.Location, destination, 5))
			{
				Flightor.MoveTo(destination, 15.0f, true);
				return true;
			}
			return false;
		}


		private static async Task<bool> TryNavigator(WoWPoint destination, string destinationName = null)
		{
			var activeMover = WoWMovement.ActiveMover;
			// If we can navigate to destination, use navigator...
			if (Navigator.CanNavigateFully(activeMover.Location, destination))
			{
				var moveResult = Navigator.MoveTo(destination);
				if (Navigator.GetRunStatusFromMoveResult(moveResult) == RunStatus.Success)
				{
					return true;
				}
			}
			if (destinationName == null)
				destinationName = destination.ToString();
			QBCLog.DeveloperInfo(
				"Navigator unable to move from {0} to destination({1}, {2}) on ground --try MovementBy=\"FlightorPreferred\".",
				activeMover.Location,
				destinationName,
				destination.ToString());
			return false;
		}


		public class NoMobsAtCurrentWaypoint : CoroutineTask<bool>
		{
			private ThrottleCoroutineTask _messageThrottle;

			public NoMobsAtCurrentWaypoint(
				Func<HuntingGroundsType> huntingGroundsProvider,
				Func<MovementByType> movementByDelegate,
				System.Action terminateBehaviorIfNoTargetsProvider = null,
				Func<string> huntedMobExclusions = null)
			{
				Contract.Requires(huntingGroundsProvider != null, context => "huntingGroundsProvider may not be null");
				Contract.Requires(movementByDelegate != null, context => "movementByDelegate may not be null");

				HuntingGroundsProvider = huntingGroundsProvider;
				MovementByDelegate = movementByDelegate ?? (() => MovementByType.FlightorPreferred);
				TerminateBehaviorIfNoTargetsProvider = terminateBehaviorIfNoTargetsProvider;
				HuntedMobExclusions = huntedMobExclusions ?? (() => String.Empty);
			}

			// BT contruction-time properties...
			private Func<HuntingGroundsType> HuntingGroundsProvider { get; set; }
			private Func<string> HuntedMobExclusions { get; set; }
			private Func<MovementByType> MovementByDelegate { get; set; }
			private System.Action TerminateBehaviorIfNoTargetsProvider { get; set; }

			public override async Task<bool> Run()
			{
				// Move to next hunting ground waypoint...
				if (await MoveTo(HuntingGroundsProvider(), MovementByDelegate()))
					return true;

				// Only one hunting ground waypoint to move to?
				await (_messageThrottle ?? (_messageThrottle = new ThrottleCoroutineTask(TimeSpan.FromSeconds(10), LogMessage)));

				// Terminate of no targets available?
				if (TerminateBehaviorIfNoTargetsProvider != null)
					TerminateBehaviorIfNoTargetsProvider();

				return false;
			}


			private async Task<bool> LogMessage()
			{
				string message = "No viable mobs in area.";
				TreeRoot.StatusText = message;

				// Show excluded units before terminating.  This aids in profile debugging if WaitForNpcs="false"...
				string excludedUnitReasons = HuntedMobExclusions();
				if (!String.IsNullOrEmpty(excludedUnitReasons))
				{
					message += excludedUnitReasons;
					QBCLog.DeveloperInfo("{0}", message);
				}
				return false;
			}
		}
	}
}