using System;
using System.Collections.Generic;
using System.Linq;
using Styx;
using Styx.WoWInternals.WoWObjects;
using Bots.DungeonBuddy.Helpers;

namespace Singular.Helpers
{
    internal static class Clusters
    {
        public static IEnumerable<WoWUnit> GetCluster(WoWUnit target, IEnumerable<WoWUnit> otherUnits, ClusterType type, float clusterRange)
        {
            if (target == null)
                return new List<WoWUnit>();

            if (otherUnits == null || !otherUnits.Any() )
                return new List<WoWUnit>();

            switch (type)
            {
                case ClusterType.Radius:
                    return GetRadiusCluster(target, otherUnits, clusterRange);
                case ClusterType.Chained:
                    return GetChainedCluster(target, otherUnits, clusterRange);
                case ClusterType.Cone:
                    return GetConeCluster(target, otherUnits, clusterRange);
                case ClusterType.PathToUnit:
                    return GetPathToUnitCluster(target, otherUnits, clusterRange);
                default:
                    throw new ArgumentOutOfRangeException("type");
            }
        }

        public static int GetClusterCount(WoWUnit target, IEnumerable<WoWUnit> otherUnits, ClusterType type, float clusterRange)
        {
            if (target == null)
                return 0;

            if (otherUnits == null || !otherUnits.Any())
                return 0;

            switch (type)
            {
                case ClusterType.Radius:
                    return GetRadiusClusterCount(target, otherUnits, clusterRange);
                case ClusterType.Chained:
                    return GetChainedClusterCount(target, otherUnits, clusterRange);
                case ClusterType.Cone:
                    return GetConeClusterCount(target, otherUnits, clusterRange);
                case ClusterType.PathToUnit:
                    return GetPathClusterCount(target, otherUnits, clusterRange);
                default:
                    throw new ArgumentOutOfRangeException("type");
            }
        }

        public static WoWUnit GetBestUnitForCluster(IEnumerable<WoWUnit> units, ClusterType type, float clusterRange)
        {
            if (units == null || !units.Any())
                return null;

            switch (type)
            {
                case ClusterType.Radius:
                    return (from u in units
                            select new { Count = GetRadiusClusterCount(u, units, clusterRange), Unit = u }).OrderByDescending(a => a.Count).
                        FirstOrDefault().Unit;
                case ClusterType.Chained:
                    return (from u in units
                            select new { Count = GetChainedClusterCount(u, units, clusterRange), Unit = u }).OrderByDescending(a => a.Count).
                        FirstOrDefault().Unit;
                case ClusterType.PathToUnit:
                    return (from u in units
                            select new { Count = GetPathClusterCount(u, units, clusterRange), Unit = u }).OrderByDescending(a => a.Count).
                        FirstOrDefault().Unit;
                // coned doesn't have a best unit, since we are the source
                default:
                    throw new ArgumentOutOfRangeException("type");
            }
        }

        private static IEnumerable<WoWUnit> GetConeCluster(WoWUnit target, IEnumerable<WoWUnit> otherUnits, float distance)
        {
            var targetLoc = target.Location;
            // most (if not all) player cone spells are 90 degrees.
            return otherUnits.Where(u => target.IsSafelyFacing(u, 90) && u.Location.Distance(targetLoc) <= distance);
        }

        private static int GetConeClusterCount(WoWUnit target, IEnumerable<WoWUnit> otherUnits, float distance)
        {
            return GetConeCluster(target, otherUnits, distance).Count();
        }

        private static IEnumerable<WoWUnit> GetRadiusCluster(WoWUnit target, IEnumerable<WoWUnit> otherUnits, float radius)
        {
            var targetLoc = target.Location;
            return otherUnits.Where(u => u.Location.DistanceSqr(targetLoc) <= radius * radius);
        }

        private static int GetRadiusClusterCount(WoWUnit target, IEnumerable<WoWUnit> otherUnits, float radius)
        {
            return GetRadiusCluster(target, otherUnits, radius).Count();
        }

        private static int GetChainedClusterCount(WoWUnit target, IEnumerable<WoWUnit> otherUnits, float chainRange)
        {
            return GetChainedCluster(target, otherUnits, chainRange).Count();
        }

        static IEnumerable<WoWUnit> GetChainedCluster(WoWUnit target, IEnumerable<WoWUnit> otherUnits, float chainRange)
        {
            var chainRangeSqr = chainRange * chainRange;
            var chainedTargets = new List<WoWUnit> { target };
            WoWUnit chainTarget;
            while ((chainTarget = GetChainTarget(target, otherUnits, chainedTargets, chainRangeSqr)) != null)
            {
                chainedTargets.Add(chainTarget);
                target = chainTarget;
            }
            return chainedTargets;
        }

        private static IEnumerable<WoWUnit> GetPathToUnitCluster(WoWUnit target, IEnumerable<WoWUnit> otherUnits, float distance)
        {
            return GetPathToPointCluster(target.Location, otherUnits, distance);
        }

        public static IEnumerable<WoWUnit> GetPathToPointCluster(WoWPoint destLoc, IEnumerable<WoWUnit> otherUnits, float distance)
        {
            var myLoc = StyxWoW.Me.Location;
            return otherUnits.Where(u => u.Location.GetNearestPointOnSegment(myLoc, destLoc).Distance(u.Location) <= distance);
        }

        private static int GetPathClusterCount(WoWUnit target, IEnumerable<WoWUnit> otherUnits, float distance)
        {
            return GetPathToUnitCluster(target, otherUnits, distance).Count();
        }

        static WoWUnit GetChainTarget(WoWUnit from, IEnumerable<WoWUnit> otherUnits, List<WoWUnit> currentChainTargets, float chainRangeSqr)
        {
            return otherUnits
                .Where(u => !currentChainTargets.Contains(u) && from.Location.DistanceSqr(u.Location) <= chainRangeSqr)
                .OrderBy(u => from.Location.DistanceSqr(u.Location))
                .FirstOrDefault();
        }
    }
}
