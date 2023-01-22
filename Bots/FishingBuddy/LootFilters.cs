using System;
using System.Collections.Generic;
using System.Linq;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Bots.FishingBuddy
{
	static class LootFilters
    {
		internal static void IncludeTargetsFilter(List<WoWObject> incomingUnits, HashSet<WoWObject> outgoingUnits)
		{
			if (StyxWoW.Me.Combat)
				return;

			var lootRadiusSqr = LootTargeting.LootRadius * LootTargeting.LootRadius;

			var myLoc = StyxWoW.Me.Location;

			List<WoWPoint> playerLocations = null;

			foreach (var obj in incomingUnits)
			{
				var gObj = obj as WoWGameObject;
				if (gObj != null)
				{
					if (gObj.SubType != WoWGameObjectType.FishingHole)
						continue;

					if (FishingBuddyBot.Instance.Profile.PoolsToFish.Any() 
						&& !FishingBuddyBot.Instance.Profile.PoolsToFish.Contains(gObj.Entry))
					{
						continue;
					}

					var gObjLoc = gObj.Location;

					if (ProfileManager.CurrentProfile != null
						&& ProfileManager.CurrentProfile.Blackspots != null
						&& Targeting.IsTooNearBlackspot(ProfileManager.CurrentOuterProfile.Blackspots, gObjLoc))
					{
						continue;
					}

					// ninja checks
					if (!FishingBuddySettings.Instance.NinjaNodes 
						&& (StyxWoW.Me.Mounted || gObjLoc.Distance2D(myLoc) > 22))
					{
						if (playerLocations == null)
						{
							playerLocations = ObjectManager.GetObjectsOfType<WoWPlayer>(false, false)
								.Where(p => !p.IsFlying)
								.Select(p => p.Location)
								.ToList();
						}

						if (playerLocations.Any(l => l.Distance2DSqr(gObjLoc) < 22 * 22))
						{
							Utility.BlacklistPool(
								gObj, 
								TimeSpan.FromMinutes(1),
								string.Format("Another player is fishing at {0}", gObj.SafeName));

							continue;
						}
					}
					outgoingUnits.Add(obj);
					continue;
				}

				if (!LootTargeting.LootMobs)
					continue;

				var unit = obj as WoWUnit;
				if (unit == null) 
					continue;

				if (!unit.IsDead)
		            continue;

				if (Blacklist.Contains(unit, BlacklistFlags.Loot | BlacklistFlags.Node))
					continue;

				if (myLoc.DistanceSqr(unit.Location) > lootRadiusSqr)
					continue;

				outgoingUnits.Add(unit);						
			}
		}
    }
}