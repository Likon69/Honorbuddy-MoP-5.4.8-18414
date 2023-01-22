using System.Collections.Generic;
using Styx.Common;

namespace Bots.FishingBuddy
{
	static class Extensions
	{
		public static CircularQueue<T> ToCircularQueue<T>(this IEnumerable<T> source)
		{
			var result = new CircularQueue<T>();
			source.ForEach(result.Add);
			return result;
		}
	}
}
