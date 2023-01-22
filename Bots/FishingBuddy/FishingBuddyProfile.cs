using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Styx;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.WoWInternals.WoWObjects;

namespace Bots.FishingBuddy
{
	class FishingBuddyProfile
	{
		private int _currentIndex;

		public FishingBuddyProfile(Profile profile, PathingType pathType, List<uint> poolsToFish)
		{
			HBProfile = profile;
			PathType = pathType;
			PoolsToFish = poolsToFish.AsReadOnly();
			LoadWayPoints();
			FishAtHotspot = WayPoints.Count == 1;
		}

		public bool FishAtHotspot { get; private set; }

		public List<WoWPoint> WayPoints { get; private set; }

		public Profile HBProfile { get; private set; }
		
		public ReadOnlyCollection<uint> PoolsToFish { get; private set; }

		public PathingType PathType { get; private set; }

		public WoWPoint CurrentPoint
		{
			get
			{
				return WayPoints != null && WayPoints.Count > 0
					? WayPoints[_currentIndex]
					: WoWPoint.Zero;
			}
		}		

		public void LoadWayPoints()
		{
			if (HBProfile != null && HBProfile.GrindArea != null && HBProfile.GrindArea.Hotspots != null)
			{
				var myLoc = StyxWoW.Me.Location;
				WayPoints = HBProfile.GrindArea.Hotspots.ConvertAll(hs => hs.Position);
				WoWPoint closestPoint =
					WayPoints.OrderBy(u => u.DistanceSqr(myLoc)).FirstOrDefault();
				_currentIndex = WayPoints.FindIndex(w => w == closestPoint);
				return;
			}
			WayPoints = new List<WoWPoint>();
		}

		public void CycleToNextPoint()
		{
			if (WayPoints == null || !WayPoints.Any())
				return;

			if (_currentIndex >= WayPoints.Count - 1)
			{
				if (PathType == PathingType.Bounce)
				{
					WayPoints.Reverse();
					_currentIndex = 1;
				}
				else
				{
					_currentIndex = 0;
				}
			}
			else
			{
				_currentIndex++;
			}
		}

		private WoWPoint GetNextWayPoint()
		{
			int i = _currentIndex + 1;
			if (i >= WayPoints.Count)
			{
				if (PathType == PathingType.Bounce)
					i = WayPoints.Count - 2;
				else
					i = 0;
			}
			if (WayPoints != null && i < WayPoints.Count)
				return WayPoints[i];
			return WoWPoint.Zero;
		}

		//if pool is between CurrentPoint and NextPoint then cycle to nextPoint
		public void CycleToNextIfBehind(WoWGameObject pool)
		{
			WoWPoint cp = CurrentPoint;
			WoWPoint point = GetNextWayPoint();
			point = new WoWPoint(point.X - cp.X, point.Y - cp.Y, 0);
			point.Normalize();
			float angle = WoWMathHelper.NormalizeRadian((float)Math.Atan2(point.Y, point.X - 1));
			if (WoWMathHelper.IsFacing(CurrentPoint, angle, pool.Location)
				&& CurrentPoint != WayPoints[WayPoints.Count - 1])
			{
				CycleToNextPoint();
			}
		}
	}
}
