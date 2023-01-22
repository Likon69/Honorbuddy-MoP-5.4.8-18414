using System;
using System.Linq;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace HighVoltz.AutoAngler.Composites
{
    internal class HarvestPoolDecorator : Decorator
    {
        private ulong _lastPoolGuid;

        public HarvestPoolDecorator(Composite child) : base(child) { }

        protected override bool CanRun(object context)
        {
			if (!AutoAnglerBot.Instance.MySettings.Poolfishing || (AutoAnglerBot.FishAtHotspot && StyxWoW.Me.Location.Distance(AutoAnglerBot.CurrentPoint) <= 3))
                return true;
            WoWGameObject pool = ObjectManager.GetObjectsOfType<WoWGameObject>()
                .OrderBy(o => o.Distance)
                .FirstOrDefault(o => o.SubType == WoWGameObjectType.FishingHole && !Blacklist.Contains(o.Guid) &&
                    // Check if we're fishing from specific pools
									 ((AutoAnglerBot.PoolsToFish.Count > 0 && AutoAnglerBot.PoolsToFish.Contains(o.Entry))
									  || AutoAnglerBot.PoolsToFish.Count == 0) &&
                    // chaeck if pool is in a blackspot
                                     !IsInBlackspot(o) &&
                    // check if player is near pool
                                     NinjaCheck(o));

            WoWGameObject poiObj = BotPoi.Current != null && BotPoi.Current.Type == PoiType.Harvest
                                       ? (WoWGameObject)BotPoi.Current.AsObject
                                       : null;
            if (pool != null)
            {
                if (poiObj == null || !poiObj.IsValid)
                {
                    BotPoi.Current = new BotPoi(pool, PoiType.Harvest);
					AutoAnglerBot.CycleToNextIfBehind(pool);
                }
                return true;
            }
            return false;
        }

        private bool NinjaCheck(WoWGameObject pool)
        {
            if (pool.Guid == _lastPoolGuid || (pool.Distance2D <= 22 && !StyxWoW.Me.Mounted))
                return true;
            _lastPoolGuid = pool.Guid;
            bool nearbyPlayers = ObjectManager.GetObjectsOfType<WoWPlayer>(false, false).
                Any(p => !p.IsFlying && p.Location.Distance2D(pool.Location) < 20);
			bool fishDaPool = !(!AutoAnglerBot.Instance.MySettings.NinjaNodes && nearbyPlayers);
            if (!fishDaPool)
                Utils.BlacklistPool(pool, TimeSpan.FromMinutes(1), "Another player fishing that pool");
            return fishDaPool;
        }


        private bool IsInBlackspot(WoWGameObject pool)
        {
            if (ProfileManager.CurrentProfile != null && ProfileManager.CurrentProfile.Blackspots != null)
            {
                if (
                    ProfileManager.CurrentProfile.Blackspots.Any(
                        blackSpot => blackSpot.Location.Distance2D(pool.Location) <= blackSpot.Radius))
                {
					AutoAnglerBot.Instance.Log("Ignoring {0} at {1} since it's in a BlackSpot", pool.Name, pool.Location);
                    return true;
                }
            }
            return false;
        }
    }
}