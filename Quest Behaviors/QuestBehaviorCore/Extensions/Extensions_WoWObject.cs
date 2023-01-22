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

using Styx;
using Styx.CommonBot;
using Styx.Pathing;
using Styx.WoWInternals.WoWObjects;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
	public static class Extensions_WoWObject
	{
		public static WoWPoint AnticipatedLocation(this WoWObject wowObject, TimeSpan atTime)
		{
			var wowUnit = wowObject as WoWUnit;

			if (wowUnit == null)
				{ return wowObject.Location; }

			var anticipatedLocation =
				wowUnit.Location.RayCast(
					wowUnit.RenderFacing, 
					(float)(wowUnit.MovementInfo.CurrentSpeed * atTime.TotalSeconds));

			return (anticipatedLocation);
		}


		// 30May2013-03:56UTC chinajade
		public static void BlacklistForCombat(this WoWObject wowObject, TimeSpan duration)
		{
			Blacklist.Add(wowObject.Guid, BlacklistFlags.Pull | BlacklistFlags.Combat, duration);
		}


		// 11Apr2013-03:56UTC chinajade
		public static void BlacklistForInteracting(this WoWObject wowObject, TimeSpan duration)
		{
			Blacklist.Add(wowObject.Guid, BlacklistFlags.Interact, duration);
		}


		public static void BlacklistForPulling(this WoWObject wowObject, TimeSpan duration)
		{
			Blacklist.Add(wowObject.Guid, BlacklistFlags.Pull, duration);
		}


		// 11Apr2013-04:41UTC chinajade
		public static bool IsBlacklistedForCombat(this WoWObject wowObject)
		{
			return Blacklist.Contains(wowObject.Guid, BlacklistFlags.Combat);
		}


		// 11Apr2013-04:41UTC chinajade
		public static bool IsBlacklistedForInteraction(this WoWObject wowObject)
		{
			return Blacklist.Contains(wowObject.Guid, BlacklistFlags.Interact);
		}


		// 4Jun2013-04:41UTC chinajade
		public static bool IsBlacklistedForPulling(this WoWObject wowObject)
		{
			return Blacklist.Contains(wowObject.Guid, BlacklistFlags.Pull);
		}


		// 2Sep2013 chinajade
		public static int SafeGuid(this WoWObject wowObject)
		{
			return (int)(wowObject.Guid & 0x0ffffff);
		}


		public static double SurfacePathDistance(this WoWObject objectTo)
		{
			return StyxWoW.Me.SurfacePathDistance(objectTo);
		}


		public static double SurfacePathDistance(this WoWObject objectFrom, WoWObject objectTo)
		{
			return objectFrom.Location.SurfacePathDistance(objectTo.Location);
		}

		public static double PathTraversalCost(this WoWObject objectTo)
		{
			return StyxWoW.Me.PathTraversalCost(objectTo);
		}


		public static double PathTraversalCost(this WoWObject objectFrom, WoWObject objectTo)
		{
			return objectFrom.Location.PathTraversalCost(objectTo.Location);
		}
	}
}
