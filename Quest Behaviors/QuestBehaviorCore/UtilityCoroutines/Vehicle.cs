// Originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

#region Usings

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bots.BGBuddy.Helpers;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Styx;
using Styx.CommonBot;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Styx.CommonBot.Coroutines;
#endregion

namespace Honorbuddy.QuestBehaviorCore
{
	public static partial class UtilityCoroutine
	{
		private static ThrottleCoroutineTask _mountVehicleUserUpdateThrottle;

		/// <summary>
		/// Mounts a vehicle.
		/// </summary>
		/// <param name="vehicleId">The vehicle identifier.</param>
		/// <param name="searchLocation">The search location.</param>
		/// <param name="movementBy">The movement by.</param>
		/// <param name="extraVehicleQualifiers">The extra vehicle qualifiers.</param>
		/// <returns></returns>
		public static async Task<bool> MountVehicle(
			int vehicleId,
			WoWPoint searchLocation,
			MovementByType movementBy = MovementByType.FlightorPreferred,
			Func<WoWUnit, bool> extraVehicleQualifiers = null)
		{
			return await MountVehicle(searchLocation, movementBy, extraVehicleQualifiers, vehicleId);
		}

		/// <summary>
		/// Mounts a vehicle
		/// </summary>
		/// <param name="searchLocation">The search location.</param>
		/// <param name="movementBy">The movement type.</param>
		/// <param name="extraVehicleQualifiers">The extra vehicle qualifiers.</param>
		/// <param name="vehicleIds">The vehicle ids.</param>
		/// <returns>
		///   <c>true</c> if any action was taken; <c>false</c> otherwise
		/// </returns>
		public static async Task<bool> MountVehicle(
			WoWPoint searchLocation,
			MovementByType movementBy = MovementByType.FlightorPreferred,
			Func<WoWUnit, bool> extraVehicleQualifiers = null,
			params int[] vehicleIds)
		{
			if (Query.IsInVehicle())
				return false;

			var vehicle = Query.FindUnoccupiedVehicles(vehicleIds, extraVehicleQualifiers).FirstOrDefault();

			if (vehicle == null)
			{
				if (!Navigator.AtLocation(searchLocation))
					return await MoveTo(searchLocation, "Vehicle search area", movementBy);

				await
					(_mountVehicleUserUpdateThrottle ??
					(_mountVehicleUserUpdateThrottle =
						new ThrottleCoroutineTask(
							TimeSpan.FromSeconds(10),
							async () => QBCLog.Info("Waiting for a vehicle to become available"))));
				return true;
			}

			if (!vehicle.WithinInteractRange)
				return await MoveTo(vehicle.Location, vehicle.SafeName, movementBy);

			if (await CommonCoroutines.Dismount("Getting inside vehicle"))
				await Coroutine.Sleep(Delay.BeforeButtonClick);
			vehicle.Interact();
			await Coroutine.Sleep(Delay.AfterInteraction);
			return true;
		}
	}
}