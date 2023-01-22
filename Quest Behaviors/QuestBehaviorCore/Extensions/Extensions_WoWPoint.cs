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
using System.Collections.Generic;
using System.Linq;

using Styx;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
	public static class Extensions_WoWPoint
	{
		public static Random _random = new Random((int)DateTime.Now.Ticks);

		private static LocalPlayer Me { get { return (StyxWoW.Me); } }
		public const double TAU = (2 * Math.PI);    // See http://tauday.com/


		/// <summary>
		/// <para>Adds the provided X, Y, and Z offsets to WOWPOINT yielding a new WoWPoint.</para>
		/// <para>---</para>
		/// <para>The HBcore only provides a version of this that accepts 'float' values.
		/// This version accepts 'doubles', because it is inefficient to keep truncating
		/// data types (to float) that are provided by the Math and other libraries.</para>
		/// <para>'Double' performance is just as fast as 'Float'.  Internally, modern
		/// computer architectures calculate using maximum precision (i.e., many bits
		/// bigger than double), then truncate the result to fit.  The only benefit
		/// 'float' has over 'double' is storage space, which is negligible unless
		/// you've a database using billions of them.</para>
		/// </summary>
		/// <param name="wowPoint"></param>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="z"></param>
		/// <returns>new WoWPoint with adjusted coordinates</returns>
		/// <remarks>17Apr2011-12:16UTC chinajade</remarks> 
		public static WoWPoint Add(this WoWPoint wowPoint,
									double x,
									double y,
									double z)
		{
			return (new WoWPoint((wowPoint.X + x), (wowPoint.Y + y), (wowPoint.Z + z)));
		}


		public static WoWPoint AddPolarXY(this WoWPoint wowPoint,
										   double xyHeadingInRadians,
										   double distance,
										   double zModifier)
		{
			return (wowPoint.Add((Math.Cos(xyHeadingInRadians) * distance),
								 (Math.Sin(xyHeadingInRadians) * distance),
								 zModifier));
		}

		public static double CollectionDistance(this WoWPoint wowPointDest, WoWPoint? wowPointSrc = null)
		{
			var wowPointRef = wowPointSrc ?? StyxWoW.Me.Location;

			// NB: we use the 'surface path' to calculate distance to mobs.
			// This is important in tunnels/caves where mobs may be within X feet of us,
			// but they are below or above us, and we have to traverse much tunnel to get to them.
			// NB: If either the player or the mob is 'off the mesh', then a SurfacePath distance
			// calculation will be absurdly large.  In these situations, we resort to direct line-of-sight
			// distances.
			var pathDistance = wowPointRef.SurfacePathDistance(wowPointDest);
			return !float.IsNaN(pathDistance)
					? pathDistance
					: wowPointRef.Distance(wowPointDest);
		}


		/// <summary>
		/// <para>Finds another point near the destination.  Useful when toon is 'waiting' for something
		/// (e.g., boat, mob repops, etc). This allows multiple people running
		/// the same profile to not stand on top of each other while waiting for
		/// something.</para>
		/// <para>Notes:<list type="bullet">
		/// <item><description><para> * The returned WoWPoint is carefully chosen.  The returned WoWPoint
		/// will not cause you to fall off a boat dock or Zeppelin landing.</para></description></item>
		/// </list></para>
		/// </summary>
		/// <param name="location"></param>
		/// <param name="maxRadius"></param>
		/// <returns></returns>
		/// <remarks>17Apr2011-12:16UTC chinajade</remarks> 
		public static WoWPoint FanOutRandom(this WoWPoint location, double maxRadius)
		{
			Contract.Requires(maxRadius >= 0.0, context => "maxRadius >= 0.0");

			// Optimize situations where we want the exact point...
			if (maxRadius <= 0.0)
				return location;

			const int CYLINDER_LINE_COUNT = 12;
			const int MAX_TRIES = 50;
			const double SAFE_DISTANCE_BUFFER = 1.75;

			WoWPoint candidateDestination = location;
			int tryCount;

			// ActiveMover is null in some cases where player is not in control of movement,
			// such as when on a taxi like the one for the 
			// 'Mission: The Murketh and Shaadraz Gateways' quest (http://www.wowhead.com/quest=10146)
			var me = WoWMovement.ActiveMover ?? StyxWoW.Me;
			Contract.Requires(me != null, context => "me != null");
			var myLoc = me.Location;

			// Most of the time we'll find a viable spot in less than 2 tries...
			// However, if you're standing on a pier, or small platform a
			// viable alternative may take 10-15 tries--its all up to the
			// random number generator.
			for (tryCount = MAX_TRIES; tryCount > 0; --tryCount)
			{
				bool[] hitResults;
				WoWPoint[] hitPoints;
				Func<double, double> weightedRandomRadius =
					(radiusMaximum) =>
						{
							return
								(_random.Next(101) < 80)
									// We want a large number of the candidate magnitudes to be near the max range.
									// This encourages toons to 'spread out'.
									? ((radiusMaximum * 0.70) + (radiusMaximum * 0.30 * _random.NextDouble()))
									: (radiusMaximum*_random.NextDouble());
						};
				var traceLines = new WorldLine[CYLINDER_LINE_COUNT + 1];

				candidateDestination = location.AddPolarXY((TAU*_random.NextDouble()), weightedRandomRadius(maxRadius), 0.0);

				// If destination is in the air...
				if (!IsOverGround(candidateDestination, 3.0))
				{
					// If we don't have clear LoS between the specified and candidate destinations, the candidate is unsuitable...
					if (GameWorld.TraceLine(location, candidateDestination, TraceLineHitFlags.Collision))
						continue;

					// Otherwise, we have our candidate destination...
					break;
				}


				// Ground-based destinations...
				// Build set of tracelines that can evaluate the candidate destination --
				// We build a cone of lines with the cone's base at the destination's 'feet',
				// and the cone's point at maxRadius over the destination's 'head'.  We also
				// include the cone 'normal' as the first entry.

				// 'Normal' vector
				var index = 0;
				traceLines[index].Start = candidateDestination.Add(0.0, 0.0, maxRadius);
				traceLines[index].End = candidateDestination.Add(0.0, 0.0, -maxRadius);

				// Cylinder vectors
				for (double turnFraction = 0.0; turnFraction < TAU; turnFraction += (TAU / CYLINDER_LINE_COUNT))
				{
					++index;
					var circlePoint = candidateDestination.AddPolarXY(turnFraction, SAFE_DISTANCE_BUFFER, 0.0);
					traceLines[index].Start = circlePoint.Add(0.0, 0.0, maxRadius);
					traceLines[index].End = circlePoint.Add(0.0, 0.0, -maxRadius);
				}


				// Evaluate the cylinder...
				// The result for the 'normal' vector (first one) will be the location where the
				// destination meets the ground.  Before this MassTrace, only the candidateDestination's
				// X/Y values were valid.
				GameWorld.MassTraceLine(traceLines.ToArray(), 
									TraceLineHitFlags.Collision,
									out hitResults,
									out hitPoints);

				candidateDestination = hitPoints[0];    // From 'normal', Destination with valid Z coordinate


				// Sanity check...
				// We don't want to be standing right on the edge of a drop-off (say we'e on
				// a plaform or pier).  If there is not solid ground all around us, we reject
				// the candidate.  Our test for validity is that the walking distance must
				// not be more than 20% greater than the straight-line distance to the point.
				int viableVectorCount = hitPoints.Sum(point => ((location.PathTraversalCost(point) < (location.Distance(point) * 1.20))
																? 1
																: 0));

				if (viableVectorCount < (CYLINDER_LINE_COUNT * 0.8))
					continue;

				// If new destination is 'too close' to our current position, try again...
				if (myLoc.Distance(candidateDestination) <= SAFE_DISTANCE_BUFFER)
					continue;

				break;
			}

			// If we exhausted our tries, just go with simple destination --
			if (tryCount <= 0)
				candidateDestination = location;

			return (candidateDestination);
		}


		// 29Apr2013-09:51UTC chinajade
		public static double HeightOverGroundOrWater(this WoWPoint location, double probeDistance = 300.0)
		{
			WoWPoint hitLocation;
			WoWPoint destination = location.Add(0.0, 0.0, -probeDistance);

			var isObstructed = GameWorld.TraceLine(location, destination,
												   GameWorld.CGWorldFrameHitFlags.HitTestGroundAndStructures
												   | GameWorld.CGWorldFrameHitFlags.HitTestLiquid
												   | GameWorld.CGWorldFrameHitFlags.HitTestLiquid2,
												   out hitLocation);

			return isObstructed
				? location.Distance(hitLocation)
				: double.MaxValue;
		}


		/// <summary>
		/// Returns true, if ground is within DISTANCE _below_ you.
		/// </summary>
		/// <param name="location"></param>
		/// <param name="probeDistance"></param>
		/// <returns>true, if ground is within DISTANCE _below_ you.</returns>
		/// <remarks>17Apr2011-12:16UTC chinajade</remarks> 
		public static bool IsOverGround(this WoWPoint location, double probeDistance)
		{
			Contract.Requires(probeDistance >= 0.0, context => "distance >= 0.0");

			return (GameWorld.TraceLine(location.Add(0.0, 0.0, 1.0),
										location.Add(0.0, 0.0, -probeDistance),
										GameWorld.CGWorldFrameHitFlags.HitTestGroundAndStructures));
		}


		/// <summary>
		/// Returns true, if ground or water is withing DISTANCE _below_ you.
		/// </summary>
		/// <param name="location">TracelinePos should be passed as location</param>
		/// <param name="probeDistance"></param>
		/// <returns>true, if ground or water is withing DISTANCE _below_ you.</returns>
		/// <remarks>raphus 18/12/2012</remarks>
		public static bool IsOverGroundOrWater(this WoWPoint location, double probeDistance)
		{
			Contract.Requires(probeDistance >= 0.0, context => "distance >= 0.0");

			return GameWorld.TraceLine(location.Add(0.0, 0.0, 1.0),
									   location.Add(0.0, 0.0, -probeDistance),
									   GameWorld.CGWorldFrameHitFlags.HitTestGroundAndStructures
									   | GameWorld.CGWorldFrameHitFlags.HitTestLiquid
									   | GameWorld.CGWorldFrameHitFlags.HitTestLiquid2);
		}


		/// <summary>
		/// Returns true, if water is within DISTANCE _below_ you.
		/// </summary>
		/// <param name="location"></param>
		/// <param name="probeDistance"></param>
		/// <returns>true, if water is within DISTANCE _below_ you.</returns>
		/// <remarks>17Apr2011-12:16UTC chinajade</remarks> 
		public static bool IsOverWater(this WoWPoint location, double probeDistance)
		{
			Contract.Requires(probeDistance >= 0.0, context => "distance >= 0.0");

			return GameWorld.TraceLine(location.Add(0.0, 0.0, 1.0),
									   location.Add(0.0, 0.0, -probeDistance),
									   GameWorld.CGWorldFrameHitFlags.HitTestLiquid
									   | GameWorld.CGWorldFrameHitFlags.HitTestLiquid2);
		}


		/// <summary>
		/// Returns the time it takes to traverse to the DESTINATION.  The caller
		/// can supply a FACTOROFSAFETY that acts as a multiplier on the calculated time.
		/// The caller can provide a LOWERLIMITOFMAXTIME to place a minimum bound on the
		/// traversal time returned.
		/// The caller can provide UPPERLIMITONMAXTIME to place an upper bound on the
		/// traversal time calculated.
		/// <para>Notes:<list type="bullet">
		/// <item><description><para> * If we are on the ground, the traversal time is calculated
		/// based on the ground path to the destination.  This may require navigating around obstacles,
		/// or via a particular path to the destination.  If we are swimming or flying, the the
		/// travesal time is calculated as straight line-of-sight to the destination.</para></description></item>
		/// <item><description><para> * The FACTOROFSAFETY defaults to 1.0.  The 1.0 value calculates
		/// the precise time needed to arrive at the destination if everything goes perfect.
		/// The factor of safety should be increased to accomodate 'stuck' situations, mounting
		/// time, and other factors.  In most situations, a good value for factor of safety
		/// is about 2.5.</para></description></item>
		/// <item><description><para> * The LOWERLIMITOFMAXTIME places a lower bound on the
		/// traversal time.  This lower limit is imposed after the factor of safety has
		/// been applied.</para></description></item>
		/// <item><description><para> * The UPPERLIMITONMAXTIME places an upper bound on the
		/// traversal time.  This upper limit is imposed after the factor of safety has
		/// been applied.  We can get times that are effectively 'infinite' in situations 
		/// where the Navigator was unable to calculate a path to the target.  This puts
		/// an upper limit on such bogus values.</para></description></item>
		/// </list></para>
		/// </summary>
		/// <param name="destination"></param>
		/// <param name="factorOfSafety"></param>
		/// <param name="lowerLimitOnMaxTime"></param>
		/// <param name="upperLimitOnMaxTime"></param>
		/// <returns></returns>
		public static TimeSpan MaximumTraversalTime(this WoWPoint destination,
													double factorOfSafety = 1.0,
													TimeSpan? lowerLimitOnMaxTime = null,
													TimeSpan? upperLimitOnMaxTime = null)
		{
			lowerLimitOnMaxTime = lowerLimitOnMaxTime ?? TimeSpan.Zero;

			var isFlying = WoWMovement.ActiveMover.IsFlying;
			var isSwimming = WoWMovement.ActiveMover.IsSwimming;

			var pathDistance = Me.Location.SurfacePathDistance(destination);

			float distanceToCover =
				!float.IsNaN(pathDistance)
				? pathDistance
				: WoWMovement.ActiveMover.Location.Distance(destination);

			// these speeds have been verified.
			double myMovementSpeed =
				isSwimming ? Me.MovementInfo.SwimmingForwardSpeed
				: isFlying ? Me.MovementInfo.FlyingForwardSpeed
				: Me.MovementInfo.ForwardSpeed;

			double timeToDestination = distanceToCover / myMovementSpeed;

			timeToDestination *= factorOfSafety;

			// Impose hard lower limit...
			timeToDestination = Math.Max(timeToDestination, lowerLimitOnMaxTime.Value.TotalSeconds);

			// Impose upper limit on the maximum time to reach the destination...
			// NB: We can get times that are effectively 'infinite' in situations where the Navigator
			// was unable to calculate a path to the target.  This puts an upper limit on such
			// bogus values.
			if (upperLimitOnMaxTime.HasValue)
				timeToDestination = Math.Min(timeToDestination, upperLimitOnMaxTime.Value.TotalSeconds);

			return (TimeSpan.FromSeconds(timeToDestination));
		}

		/// <summary>
		/// Returns the cost to travel between 2 point. 
		/// Points that are not on mesh have a much higher cost.
		/// </summary>
		/// <param name="start">The start.</param>
		/// <param name="destination">The destination.</param>
		/// <returns></returns>
		public static float PathTraversalCost(this WoWPoint start, WoWPoint destination)
		{
			float pathDistance = SurfacePathDistance(start, destination);
			if (!float.IsNaN(pathDistance))
				return pathDistance;

			// For targets in the air, we will be unable to calculate the
			// surface path to them.  If we're flying, we still want
			// a gauging of the distance, so we use half the max float range,
			// and tack on the line-of-site distance to the unit.
			// This allows sane ordering evaluations in LINQ queries, yet
			// still returns something close enough to 'infinite' to make
			// using the path highly undesirable.
			return (float.MaxValue / 2) + start.Distance(destination);
		}

		/// <summary>
		/// Calculates the distance between START and DESTINATION if the travel must be conducted
		/// over a surface (i.e., instead of flying).  This is most helpful in tunnels where a mob
		/// can be within X feet of you, but above or below you.  For such mobs, the direct distance
		/// is X feet, but the path you must take through the tunnels may be much much longer.
		/// </summary>
		/// <param name="start"></param>
		/// <param name="destination"></param>
		/// <returns></returns>
		/// <remarks>17Apr2011-12:16UTC chinajade</remarks> 
		public static float SurfacePathDistance(this WoWPoint start, WoWPoint destination)
		{
			float pathDistance;
			if (SurfacePathDistanceCache.TryGet(start, destination, out pathDistance))
				return pathDistance;

			var groundPath = new WoWPoint[] { };

			bool canFullyNavigate;

			// Note: Use the Navigate.GeneratePath that outs a 'isPartial' boolean once it's available.
			var meshNavigator = Navigator.NavigationProvider as MeshNavigator;
			if (meshNavigator != null)
			{
				var pathResult = meshNavigator.Nav.FindPath(start, destination);
				canFullyNavigate = pathResult.Succeeded && !pathResult.IsPartialPath;

				if (canFullyNavigate)
					groundPath = pathResult.Points.Select(v => (WoWPoint)v).ToArray();
			}
			else
			{
				groundPath = Navigator.GeneratePath(start, destination) ?? new WoWPoint[0];
				canFullyNavigate = groundPath.Length > 0;
			}

			if (!canFullyNavigate || groundPath.Length <= 0)
			{
				SurfacePathDistanceCache.Add(start, destination, float.NaN);
				return float.NaN;
			}


			// Include distance it takes us to get from start point to first point in path...
			pathDistance = start.Distance(groundPath[0]);

			// Include distance it takes us to get from last point in path to destination...
			pathDistance += groundPath[groundPath.Length - 1].Distance(destination);

			// Include distance for each point in path...
			for (int i = 0; i < (groundPath.Length - 1); ++i)
				pathDistance += groundPath[i].Distance(groundPath[i + 1]);

			// Sanity check...
			Contract.Provides(
				pathDistance >= start.Distance(destination),
				context => "Surface path distance must be equal to or greater than straight-line distance.");

			SurfacePathDistanceCache.Add(start, destination, pathDistance);
			return (pathDistance);
		}

		// Returns WoWPoint.Empty if unable to locate water's surface
		public static WoWPoint WaterSurface(this WoWPoint location)
		{
			WoWPoint hitLocation;
			WoWPoint locationUpper = location.Add(0.0, 0.0, 2000.0);
			WoWPoint locationLower = location.Add(0.0, 0.0, -2000.0);

			var hitResult = (GameWorld.TraceLine(locationUpper,
											 locationLower,
											 (GameWorld.CGWorldFrameHitFlags.HitTestLiquid
											  | GameWorld.CGWorldFrameHitFlags.HitTestLiquid2),
											 out hitLocation));

			return (hitResult ? hitLocation : WoWPoint.Empty);
		}

		#region Embedded Type: SurfacePathDistanceCache

		private class SurfacePathDistanceCache
		{
			private static readonly TimeSpan MaxCacheTimeSpan = TimeSpan.FromMilliseconds(2500);

			private static DateTime _lastCleanupTime = DateTime.MinValue;

			// a list is probably the best collection to use if avg collection size is small (less than 20 to 30 items)
			private static readonly List<SurfacePathDistanceCache> PathDistanceCache = new List<SurfacePathDistanceCache>();

			private SurfacePathDistanceCache(WoWPoint start, WoWPoint destination, float distance)
			{
				Start = start;
				Destination = destination;
				Distance = distance;
				TimeStamp = DateTime.Now;
			}

			private WoWPoint Start { get; set; }

			private WoWPoint Destination { get; set; }

			private float Distance { get; set; }

			private DateTime TimeStamp { get; set; }

			/// <summary>
			/// Tries to find a cached surface path distance between the start/destination pair and returns <c>true</c> if successful
			/// </summary>
			/// <param name="start">The start.</param>
			/// <param name="destination">The destination.</param>
			/// <param name="distance">The surface path distance, zero if no cache found or NaN if no path could be fully generated</param>
			/// <returns><c>true</c> if a cache was found, <c>false</c> otherwise.</returns>
			internal static bool TryGet(WoWPoint start, WoWPoint destination, out float distance)
			{
				SurfacePathDistanceCache match = null;
				var now = DateTime.Now;

				// do we need to cleanup old caches?
				var doCleanup = now - _lastCleanupTime > MaxCacheTimeSpan;

				// iterate the path cache in revere so we can remove entries safely
				for (int idx = PathDistanceCache.Count - 1; idx >= 0; idx--)
				{
					var entry = PathDistanceCache[idx];
					// check if we need the entry
					if (doCleanup && now - entry.TimeStamp > MaxCacheTimeSpan)
					{
						PathDistanceCache.RemoveAt(idx);
						continue;
					}
					// check if we have a match
					if (match == null && Navigator.AtLocation(start, entry.Start) && Navigator.AtLocation(destination, entry.Destination))
					{
						match = entry;
						// exit for loop now if not doing a cleanup pass
						if (!doCleanup)
							break;
					}
				}


				if (doCleanup)
					_lastCleanupTime = now;

				if (match == null)
				{
					distance = 0;
					return false;
				}

				distance = match.Distance;
				return true;
			}

			/// <summary>
			/// Adds the specified surface distance for the start/destination pair. 
			/// This assumes the caller has already checked if the cache exists before adding to prevent duplicates.
			/// </summary>
			/// <param name="start">The start.</param>
			/// <param name="destination">The destination.</param>
			/// <param name="distance">The distance.</param>
			internal static void Add(WoWPoint start, WoWPoint destination, float distance)
			{
				PathDistanceCache.Add(new SurfacePathDistanceCache(start, destination, distance));
			}
		}

		#endregion
	}
}
