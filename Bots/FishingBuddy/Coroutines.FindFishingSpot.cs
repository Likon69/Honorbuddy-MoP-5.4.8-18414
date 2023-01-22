using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;

namespace Bots.FishingBuddy
{
	partial class Coroutines
	{
		static List<WoWPoint> _poolPoints;

		// used to auto blacklist a pool if it takes too long to get to a point.
		public static readonly WaitTimer MoveToPoolTimer = new WaitTimer(TimeSpan.FromSeconds(25));
		private static ulong _lastPoolGuid;

		public async static Task<bool> MoveToPool(WoWGameObject pool) {
			if (!FishingBuddySettings.Instance.Poolfishing || FishingBuddyBot.Instance.Profile.FishAtHotspot)
				return false;

			if (pool == null || !pool.IsValid)
				return false;

			if (_lastPoolGuid != pool.Guid)
			{
				MoveToPoolTimer.Reset();
				_lastPoolGuid = pool.Guid;
				if (!FindPoolPoint(pool, out _poolPoints) || !_poolPoints.Any())
				{
					Utility.BlacklistPool(pool, TimeSpan.FromDays(1), "Found no landing spots");
					return false;
				}
			}

			// should never be true.. but being safe..
			if (!_poolPoints.Any())
			{
				Utility.BlacklistPool(pool, TimeSpan.FromDays(1), "Pool landing points mysteriously disapear...");
				return false;
			}

			var myLoc = Me.Location;			
			var moveto = _poolPoints[0];

			TreeRoot.StatusText = "Moving to " + pool.Name;
			if (myLoc.DistanceSqr(moveto) > 4 * 4)
			{
				MoveResult moveResult;
				if (FishingBuddySettings.Instance.Fly)
				{
					// don't bother mounting up if we can use navigator to walk over if it's less than 25 units away
					if (myLoc.DistanceSqr(moveto) < 25 * 25 && !Me.Mounted)
					{
						moveResult = Navigator.MoveTo(moveto);
						if (moveResult != MoveResult.Failed && moveResult != MoveResult.PathGenerationFailed)
							return true;
					}
					
					//if (!Me.Mounted && !SpellManager.GlobalCooldown)
					//{
					//	Flightor.MountHelper.MountUp();
					//	return true;
					//}
					Flightor.MoveTo(WoWMathHelper.CalculatePointFrom(myLoc, moveto, -1f));
					return true;
				}

				moveResult = Navigator.MoveTo(moveto);
				if (moveResult == MoveResult.UnstuckAttempt 
					|| moveResult == MoveResult.PathGenerationFailed 
					|| moveResult == MoveResult.Failed)
				{
					if (!RemovePointAtTop(pool))
						return true;
					FishingBuddyBot.Debug("Unable to path to pool point, switching to a new point");
					_poolPoints.Sort((a, b) => a.DistanceSqr(myLoc).CompareTo(b.DistanceSqr(myLoc)));
				}

				// if it takes more than 25 seconds to get to a point remove that point and try another.
				if (MoveToPoolTimer.IsFinished)
				{
					if (!RemovePointAtTop(pool))
						return false;
				}
				return true;
			}
			// wait for bot to reach destination before dismounting
			await Coroutine.Wait(2000, () => !Me.IsMoving);

			if (Me.Mounted)
				await CommonCoroutines.Dismount("Fishing");

			// can't fish while swimming..
			if (Me.IsSwimming && !WaterWalking.IsActive && !WaterWalking.CanCast)
			{
				FishingBuddyBot.Debug("Moving to new PoolPoint since I'm swimming at current PoolPoint");
				if (!RemovePointAtTop(pool))
					return false;
				return true;
			}
			return false;
		}

		private static bool FindPoolPoint(WoWGameObject pool, out List<WoWPoint> poolPoints )
		{
			int traceStep = FishingBuddySettings.Instance.TraceStep;
			const float pIx2 = 3.14159f * 2f;
			var traceLine = new WorldLine[traceStep];
			poolPoints = new List<WoWPoint>();

			// scans starting at OptimumPoolDistance2D from player for water at every 18 degress 
			float range = OptimumPoolDistance2D;
			var poolRadius = GetPoolRadius(pool);
			var min = MinCastDistance2D - poolRadius + PoolDistTolerance;
			var max = MaxCastDistance2D + poolRadius - PoolDistTolerance;


			float step = FishingBuddySettings.Instance.PoolRangeStep;
			float delta = step;
			float avg = (min + max) / 2f;
			while (true)
			{
				for (int i = 0; i < traceStep; i++)
				{
					WoWPoint p = pool.Location.RayCast((i * pIx2) / traceStep, range);
					WoWPoint hPoint = p;
					hPoint.Z += 45;
					WoWPoint lPoint = p;
					lPoint.Z -= 1;
					traceLine[i].Start = hPoint;
					traceLine[i].End = lPoint;
				}
				WoWPoint[] hitPoints;
				bool[] tracelineRetVals;
                GameWorld.MassTraceLine(traceLine, TraceLineHitFlags.Collision, out tracelineRetVals, out hitPoints);
	
				// what I'm doing here is compare the elevation of 4 corners around a point with 
				// that point's elevation to determine if that point is too steep to stand on.
				var slopetraces = new List<WorldLine>();
				var testPoints = new List<WoWPoint>();
				for (int i = 0; i < traceStep; i++)
				{
					if (tracelineRetVals[i])
					{
						slopetraces.AddRange(GetQuadSloopTraceLines(hitPoints[i]));
						testPoints.Add(hitPoints[i]);
					}
					else if (WaterWalking.IsActive || WaterWalking.CanCast)
					{
						traceLine[i].End.Z = pool.Z + 1;
						poolPoints.Add(traceLine[i].End);
					}
				}
				// fire tracelines.. 
				bool[] lavaRetVals = null;
				WoWPoint[] slopeHits;
				using (StyxWoW.Memory.AcquireFrame())
				{
					bool[] slopelinesRetVals;
					GameWorld.MassTraceLine(slopetraces.ToArray(),
											TraceLineHitFlags.Collision,
											out slopelinesRetVals, out slopeHits);
					if (FishingBuddySettings.Instance.AvoidLava)
					{
						GameWorld.MassTraceLine(slopetraces.ToArray(), TraceLineHitFlags.LiquidAll,
												out lavaRetVals);
					}
				}

				// process results
				poolPoints.AddRange(ProcessSlopeAndLavaResults(testPoints, slopeHits, lavaRetVals));
				// perform LOS checks
				if (poolPoints.Any())
				{
					var losLine = new WorldLine[poolPoints.Count];
					for (int i2 = 0; i2 < poolPoints.Count; i2++)
					{
						WoWPoint point = poolPoints[i2];
						point.Z += 2;
						losLine[i2].Start = point;
						losLine[i2].End = pool.Location;
					}
					GameWorld.MassTraceLine(losLine, TraceLineHitFlags.Collision, out tracelineRetVals);
					for (int i2 = poolPoints.Count - 1; i2 >= 0; i2--)
					{
						if (tracelineRetVals[i2])
							poolPoints.RemoveAt(i2);
					}
				}
				// sort pools by distance to player                
				poolPoints.Sort((p1, p2) => p1.Distance(StyxWoW.Me.Location).CompareTo(p2.Distance(StyxWoW.Me.Location)));
				if (!StyxWoW.Me.IsFlying)
				{
					// if we are not flying check if we can genorate a path to points.
					for (int i = 0; i < poolPoints.Count; )
					{
						WoWPoint[] testP = Navigator.GeneratePath(StyxWoW.Me.Location, poolPoints[i]);
						if (testP.Length > 0)
						{
							return true;
						}
						poolPoints.RemoveAt(i);
						poolPoints.Sort((a, b) => a.Distance(StyxWoW.Me.Location).CompareTo(b.Distance(StyxWoW.Me.Location)));
					}
				}

				if (poolPoints.Any())
					return true;

				bool minCaped = (OptimumPoolDistance2D - delta) < min;
				bool maxCaped = (OptimumPoolDistance2D + delta) > max;
				if (minCaped && maxCaped)
					break;

				if ((range <= OptimumPoolDistance2D && (OptimumPoolDistance2D + delta) <= max) || minCaped)
				{
					range = OptimumPoolDistance2D + delta;
					if (avg < OptimumPoolDistance2D || minCaped)
						delta += step;
					continue;
				}

				if ((range > OptimumPoolDistance2D && (OptimumPoolDistance2D - delta) >= min) || maxCaped)
				{
					range = OptimumPoolDistance2D - delta;
					if (avg >= OptimumPoolDistance2D || maxCaped)
						delta += step;
				}
			}
			return false;
		}

		public static WorldLine GetSlopeTraceLine(WoWPoint point, float xDelta, float yDelta)
		{
			WoWPoint topP = point;
			topP.X += xDelta;
			topP.Y += yDelta;
			topP.Z += 6;
			WoWPoint botP = topP;
			botP.Z -= 12;
			return new WorldLine(topP, botP);
		}

		public static List<WorldLine> GetQuadSloopTraceLines(WoWPoint point)
		{
			const float delta = 0.5f;
			var wl = new List<WorldLine>
                         {
                             // north west
                             GetSlopeTraceLine(point, delta, -delta),
                             // north east
                             GetSlopeTraceLine(point, delta, delta),
                             // south east
                             GetSlopeTraceLine(point, -delta, delta),
                             // south west
                             GetSlopeTraceLine(point, -delta, -delta)
                         };
			return wl;
		}

		public static List<WoWPoint> ProcessSlopeAndLavaResults(List<WoWPoint> testPoints, WoWPoint[] slopePoints,
																bool[] lavaHits)
		{
			const float slopeRise = 0.60f;
			var retList = new List<WoWPoint>();
			for (int i = 0; i < testPoints.Count; i++)
			{
				if (slopePoints[i * 4] != WoWPoint.Zero &&
					slopePoints[i * 4 + 1] != WoWPoint.Zero &&
					slopePoints[i * 4 + 2] != WoWPoint.Zero &&
					slopePoints[i * 4 + 3] != WoWPoint.Zero &&
					// check for lava hits
					(lavaHits == null ||
					 (!lavaHits[i * 4] &&
					  !lavaHits[i * 4 + 1] &&
					  !lavaHits[i * 4 + 2] &&
					  !lavaHits[i * 4 + 3]))
					)
				{
					if (ElevationDifference(testPoints[i], slopePoints[(i * 4)]) <= slopeRise &&
						ElevationDifference(testPoints[i], slopePoints[(i * 4) + 1]) <= slopeRise &&
						ElevationDifference(testPoints[i], slopePoints[(i * 4) + 2]) <= slopeRise &&
						ElevationDifference(testPoints[i], slopePoints[(i * 4) + 3]) <= slopeRise)
					{
						retList.Add(testPoints[i]);
					}
				}
			}
			return retList;
		}

		public static float ElevationDifference(WoWPoint p1, WoWPoint p2)
		{
			if (p1.Z > p2.Z)
				return p1.Z - p2.Z;
			return p2.Z - p1.Z;
		}


		internal static bool RemovePointAtTop(WoWGameObject pool)
		{
			_poolPoints.RemoveAt(0);
			if (!_poolPoints.Any())
			{
				Utility.BlacklistPool(pool, TimeSpan.FromMinutes(10), "No Landing spot found");
				return false;
			}
			return true;
		}
	}
}
