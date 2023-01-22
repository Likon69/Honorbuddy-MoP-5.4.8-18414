using System.Collections.Generic;
using System.Threading.Tasks;
using Styx;
using Styx.Helpers;
using Styx.WoWInternals.World;

namespace Bots.FishingBuddy
{
	static partial class Coroutines
	{
		const float PIx2 = 3.14159f * 2f;
		const int TraceStep = 20;

		public async static Task<bool> FaceWater()
		{
			float? bestDirection = GetFaceWaterDirection();
			if (bestDirection.HasValue &&
				!WoWMathHelper.IsFacing(StyxWoW.Me.Location, StyxWoW.Me.Rotation, StyxWoW.Me.Location.RayCast(bestDirection.Value, 10f), WoWMathHelper.DegreesToRadians(15)))
			{
				FishingBuddyBot.Log("auto facing towards water");
				Me.SetFacing(bestDirection.Value);
			}
			return false;
		}

		private static float? GetFaceWaterDirection()
		{
			WoWPoint playerLoc = StyxWoW.Me.Location;
			var sonar = new List<int>(TraceStep);
			var tracelines = new WorldLine[TraceStep * 3];
			bool[] tracelineWaterVals, traceLineTerrainVals;
			WoWPoint[] waterHitPoints, terrainHitpoints;

			for (int i = 0; i < TraceStep; i++)
			{
				// scans 10,15 and 20 yards from player for water at every 18 degress 
				for (int n = 0; n < 3; n++)
				{
					WoWPoint p = (playerLoc.RayCast((i * PIx2) / TraceStep, 10 + (n * 5)));
					WoWPoint highPoint = p;
					highPoint.Z += 5;
					WoWPoint lowPoint = p;
					lowPoint.Z -= 55;
					tracelines[(i * 3) + n].Start = highPoint;
					tracelines[(i * 3) + n].End = lowPoint;
				}
			}

			GameWorld.MassTraceLine(tracelines, TraceLineHitFlags.LiquidAll, out tracelineWaterVals, out waterHitPoints);

            GameWorld.MassTraceLine(tracelines, TraceLineHitFlags.Collision, out traceLineTerrainVals, out terrainHitpoints);

			for (int i = 0; i < TraceStep; i++)
			{
				int scan = 0;
				for (int n = 0; n < 3; n++)
				{
					var idx = i*3 + n;
					if (tracelineWaterVals[idx]
						&& (!traceLineTerrainVals[idx] || terrainHitpoints[idx].Z < waterHitPoints[idx].Z))
					{
						scan++;
					}
				}
				sonar.Add(scan);
			}

			int widest = 0;
			for (int i = 0; i < TraceStep; i++)
			{
				if (sonar[i] > widest)
					widest = sonar[i];
			}
			bool counting = false;
			int startIndex = 0, bigestStartIndex = 0, startLen = 0, endLen = 0, bigestStretch = 0;
			// if we found water find the largest area and face towards the center of it.


			if (widest > 0)
			{
				for (int i = 0; i < TraceStep; i++)
				{
					if (sonar[i] == widest && !counting)
					{
						startIndex = i;
						if (i == 0)
							startLen = 1;
						counting = true;
					}
					if (sonar[i] != widest && counting)
					{
						if ((i) - startIndex > bigestStretch)
						{
							bigestStretch = (i) - startIndex;
							bigestStartIndex = startIndex;
						}
						if (startIndex == 0)
							startLen = i;
						counting = false;
					}
					if (sonar[i] == widest && counting && i == 19)
						endLen = i - startIndex;
				}
				int index;
				if (startLen + endLen > bigestStretch)
				{
					if (startLen >= endLen)
						index = startLen > endLen ? startLen - endLen : endLen - startLen;
					else
						index = (TraceStep - 1) - (endLen - startLen);
				}
				else
					index = bigestStartIndex + (bigestStretch / 2);
				float direction = (index * PIx2) / 20;

				return direction;
			}
			return null;
		}
	}
}
