using System;
using System.Collections.Generic;
using System.Diagnostics;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

namespace HighVoltz.AutoAngler.Composites
{
    public class MoveToPoolAction : Action
    {
        public static readonly List<WoWPoint> PoolPoints = new List<WoWPoint>();

        public static readonly Stopwatch MoveToPoolSW = new Stopwatch();
        // used to auto blacklist a pool if it takes too long to get to a point.

        private readonly Stopwatch _movetoConcludingSW = new Stopwatch();
        private ulong _lastPoolGuid;

        protected override RunStatus Run(object context)
        {
			if (AutoAnglerSettings.Instance.Poolfishing && !AutoAnglerBot.FishAtHotspot && BotPoi.Current != null && BotPoi.Current.Type == PoiType.Harvest)
            {
                var pool = (WoWGameObject) BotPoi.Current.AsObject;
                if (pool != null && pool.IsValid)
                {
                    return GotoPool(pool); 
                }
                BotPoi.Current = null;
            }

            return RunStatus.Failure;
        }

        private bool FindPoolPoint(WoWGameObject pool)
        {
			int traceStep = AutoAnglerBot.Instance.MySettings.TraceStep;
            const float pIx2 = 3.14159f*2f;
            var traceLine = new WorldLine[traceStep];
            PoolPoints.Clear();

            // scans starting at 15 yards from player for water at every 18 degress 

            float range = 15;
			int min = AutoAnglerBot.Instance.MySettings.MinPoolRange;
			int max = AutoAnglerBot.Instance.MySettings.MaxPoolRange;
			float step = AutoAnglerBot.Instance.MySettings.PoolRangeStep;
            float delta = step;
            float avg = (min + max)/2;
            while (true)
            {
                for (int i = 0; i < traceStep; i++)
                {
                    WoWPoint p = pool.Location.RayCast((i*pIx2)/traceStep, range);
                    WoWPoint hPoint = p;
                    hPoint.Z += 45;
                    WoWPoint lPoint = p;
                    lPoint.Z -= 1;
                    traceLine[i].Start = hPoint;
                    traceLine[i].End = lPoint;
                }
                WoWPoint[] hitPoints;
                bool[] tracelineRetVals;
                GameWorld.MassTraceLine(traceLine, GameWorld.CGWorldFrameHitFlags.HitTestGroundAndStructures,
                                        out tracelineRetVals, out hitPoints);
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
                    else if (WaterWalking.CanCast)
                    {
                        traceLine[i].End.Z = pool.Z + 1;
                        PoolPoints.Add(traceLine[i].End);
                    }
                }
                // fire tracelines.. 
                bool[] lavaRetVals = null;
                WoWPoint[] slopeHits;
                using (StyxWoW.Memory.AcquireFrame())
                {
                    bool[] slopelinesRetVals;
                    GameWorld.MassTraceLine(slopetraces.ToArray(),
                                            GameWorld.CGWorldFrameHitFlags.HitTestGroundAndStructures,
                                            out slopelinesRetVals, out slopeHits);
					if (AutoAnglerBot.Instance.MySettings.AvoidLava)
                    {
                        GameWorld.MassTraceLine(slopetraces.ToArray(), GameWorld.CGWorldFrameHitFlags.HitTestLiquid2,
                                                out lavaRetVals);
                    }
                }

                // process results
                PoolPoints.AddRange(ProcessSlopeAndLavaResults(testPoints, slopeHits, lavaRetVals));
                // perform LOS checks
                if (PoolPoints.Count > 0)
                {
                    var losLine = new WorldLine[PoolPoints.Count];
                    for (int i2 = 0; i2 < PoolPoints.Count; i2++)
                    {
                        WoWPoint point = PoolPoints[i2];
                        point.Z += 2;
                        losLine[i2].Start = point;
                        losLine[i2].End = pool.Location;
                    }
                    GameWorld.MassTraceLine(losLine, GameWorld.CGWorldFrameHitFlags.HitTestGroundAndStructures,
                                            out tracelineRetVals);
                    for (int i2 = PoolPoints.Count - 1; i2 >= 0; i2--)
                    {
                        if (tracelineRetVals[i2])
                            PoolPoints.RemoveAt(i2);
                    }
                }
                // sort pools by distance to player                
				PoolPoints.Sort((p1, p2) => p1.Distance(StyxWoW.Me.Location).CompareTo(p2.Distance(StyxWoW.Me.Location)));
				if (!StyxWoW.Me.IsFlying)
                {
                    // if we are not flying check if we can genorate a path to points.
                    for (int i = 0; i < PoolPoints.Count;)
                    {
						WoWPoint[] testP = Navigator.GeneratePath(StyxWoW.Me.Location, PoolPoints[i]);
                        if (testP.Length > 0)
                        {
                            return true;
                        }
                        PoolPoints.RemoveAt(i);
						PoolPoints.Sort((a, b) => a.Distance(StyxWoW.Me.Location).CompareTo(b.Distance(StyxWoW.Me.Location)));
                    }
                }
                if (PoolPoints.Count > 0)
                    return true;
                bool minCaped = (15 - delta) < min;
                bool maxCaped = (15 + delta) > max;
                if (minCaped && maxCaped)
                    break;

                if ((range <= 15 && (15 + delta) <= max) || minCaped)
                {
                    range = 15 + delta;
                    if (avg < 15 || minCaped)
                        delta += step;
                    continue;
                }

                if ((range > 15 && (15 - delta) >= min) || maxCaped)
                {
                    range = 15 - delta;
                    if (avg >= 15 || maxCaped)
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
            //float delta = AutoAngler2.Instance.MySettings.LandingSpotWidth / 2;
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
            //float slopeRise = AutoAngler2.Instance.MySettings.LandingSpotSlope / 2;
            const float slopeRise = 0.60f;
            var retList = new List<WoWPoint>();
            for (int i = 0; i < testPoints.Count; i++)
            {
                if (slopePoints[i*4] != WoWPoint.Zero &&
                    slopePoints[i*4 + 1] != WoWPoint.Zero &&
                    slopePoints[i*4 + 2] != WoWPoint.Zero &&
                    slopePoints[i*4 + 3] != WoWPoint.Zero &&
                    // check for lava hits
                    (lavaHits == null ||
                     (lavaHits != null &&
                      !lavaHits[i*4] &&
                      !lavaHits[i*4 + 1] &&
                      !lavaHits[i*4 + 2] &&
                      !lavaHits[i*4 + 3]))
                    )
                {
                    if (ElevationDifference(testPoints[i], slopePoints[(i*4)]) <= slopeRise &&
                        ElevationDifference(testPoints[i], slopePoints[(i*4) + 1]) <= slopeRise &&
                        ElevationDifference(testPoints[i], slopePoints[(i*4) + 2]) <= slopeRise &&
                        ElevationDifference(testPoints[i], slopePoints[(i*4) + 3]) <= slopeRise)
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

        private RunStatus GotoPool(WoWGameObject pool)
        {
            if (_lastPoolGuid != pool.Guid)
            {
                MoveToPoolSW.Reset();
                MoveToPoolSW.Start();
                _lastPoolGuid = pool.Guid;
                if (!FindPoolPoint(pool) || PoolPoints.Count == 0)
                {
                    Utils.BlacklistPool(pool, TimeSpan.FromDays(1), "Found no landing spots");
                    return RunStatus.Success;// return sucess so Behavior stops and starts at begining on next tick.
                }
            }
            // should never be true.. but being safe..
            if (PoolPoints.Count == 0)
            {
                Utils.BlacklistPool(pool, TimeSpan.FromDays(1), "Pool landing points mysteriously disapear...");
                return RunStatus.Success;// return sucess so Behavior stops and starts at begining on next tick.
            }
            TreeRoot.StatusText = "Moving to " + pool.Name;
			if (StyxWoW.Me.Location.Distance(PoolPoints[0]) > 3)
            {
                _movetoConcludingSW.Reset();
	            if (!MoveToPoolSW.IsRunning)
	            {
		            MoveToPoolSW.Start();
	            }
				if (StyxWoW.Me.IsSwimming)
                {
					if (StyxWoW.Me.GetMirrorTimerInfo(MirrorTimerType.Breath).CurrentTime > 0)
                        WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
					else if (StyxWoW.Me.MovementInfo.IsAscending || StyxWoW.Me.MovementInfo.JumpingOrShortFalling)
                        WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend);
                }
				if (AutoAnglerBot.Instance.MySettings.Fly)
                {
                    // don't bother mounting up if we can use navigator to walk over if it's less than 25 units away
					if (StyxWoW.Me.Location.Distance(PoolPoints[0]) < 25 && !StyxWoW.Me.Mounted)
                    {
                        MoveResult moveResult = Navigator.MoveTo(PoolPoints[0]);
                        if (moveResult == MoveResult.Failed || moveResult == MoveResult.PathGenerationFailed)
                        {
                            Flightor.MountHelper.MountUp();
                        }
                        else
                            return RunStatus.Success;
                    }
					else if (!StyxWoW.Me.Mounted && !SpellManager.GlobalCooldown)
                        Flightor.MountHelper.MountUp();
					Flightor.MoveTo(WoWMathHelper.CalculatePointFrom(StyxWoW.Me.Location, PoolPoints[0], -1f));
                }
                else
                {
                    if (!StyxWoW.Me.Mounted && Mount.ShouldMount(PoolPoints[0]) && Mount.CanMount())
                        Mount.MountUp(() => PoolPoints[0]);
                    MoveResult moveResult = Navigator.MoveTo(PoolPoints[0]);
                    if (moveResult == MoveResult.UnstuckAttempt ||
                        moveResult == MoveResult.PathGenerationFailed || moveResult == MoveResult.Failed)
                    {
                        if (!RemovePointAtTop(pool))
                            return RunStatus.Success;
						AutoAnglerBot.Instance.Debug("Unable to path to pool point, switching to a new point");
						PoolPoints.Sort((a, b) => a.Distance(StyxWoW.Me.Location).CompareTo(b.Distance(StyxWoW.Me.Location)));
                    }
                }
                // if it takes more than 25 seconds to get to a point remove that point and try another.
                if (MoveToPoolSW.ElapsedMilliseconds > 25000)
                {
                    if (!RemovePointAtTop(pool))
                        return RunStatus.Failure;
                    MoveToPoolSW.Reset();
                    MoveToPoolSW.Start();
                }
                return RunStatus.Success;
            }
            // allow small delay so clickToMove can run its course before dismounting. better landing precision..
            if (!_movetoConcludingSW.IsRunning)
                _movetoConcludingSW.Start();
            if (_movetoConcludingSW.ElapsedMilliseconds < 1500)
            {
				if (StyxWoW.Me.Location.Distance2D(PoolPoints[0]) > 0.5)
                    WoWMovement.ClickToMove(PoolPoints[0]);
                return RunStatus.Success;
            }
			if (StyxWoW.Me.Mounted)
            {
				if (StyxWoW.Me.Class == WoWClass.Druid &&
					(StyxWoW.Me.Shapeshift == ShapeshiftForm.FlightForm || StyxWoW.Me.Shapeshift == ShapeshiftForm.EpicFlightForm))
                {
                    Lua.DoString("CancelShapeshiftForm()");
                }
                else
                    Lua.DoString("Dismount()");
            }
            // can't fish while swimming..
			if (StyxWoW.Me.IsSwimming && !WaterWalking.CanCast)
            {
				AutoAnglerBot.Instance.Debug("Moving to new PoolPoint since I'm swimming at current PoolPoint");
                RemovePointAtTop(pool);
                return RunStatus.Success;
            }
            return RunStatus.Failure;
        }

        private bool RemovePointAtTop(WoWGameObject pool)
        {
            PoolPoints.RemoveAt(0);
            _movetoConcludingSW.Reset();
            if (PoolPoints.Count == 0)
            {
                Utils.BlacklistPool(pool, TimeSpan.FromMinutes(10), "No Landing spot found");
                return false;
            }
            return true;
        }
    }
}
// QmUgY29vbCBhbmQganVzdCBidXkgdGhlIGJvdA==
// !CompilerOption:AddRef:Remoting.dll
