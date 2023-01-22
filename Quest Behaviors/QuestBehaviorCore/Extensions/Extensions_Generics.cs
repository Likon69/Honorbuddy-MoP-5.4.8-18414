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
using System.Collections.Generic;

using Styx.Common;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
	public static class Extensions_Generics
	{
		//*****
		// Dictionary...
		public static void CopyFrom<T1, T2>(this Dictionary<T1,T2> destination, Dictionary<T1,T2> source)
		{
			destination.Clear();
			foreach (var entry in source)
				{ destination.Add(entry.Key, entry.Value); }
		}


		//*****
		// DualHashSet...
		public static DualHashSet<T1, T2> Clone<T1, T2>(this DualHashSet<T1, T2> source)
		{
			var result = new DualHashSet<T1, T2>();

			result.CopyFrom(source);
			return (result);
		}


		public static void CopyFrom<T1, T2>(this DualHashSet<T1, T2> destination, DualHashSet<T1, T2> source)
		{
			destination.HashSet1.Clear();
			destination.HashSet2.Clear();

			// Add elements of T1 type...
			foreach (var entry in source.HashSet1)
				{ destination.Add(entry); }

			// Add elements of T2 type...
			foreach (var entry in source.HashSet2)
				{ destination.Add(entry); }
		}


		//*****
		// HashSet...
		public static void CopyFrom<T>(this HashSet<T> destination, IEnumerable<T> source)
		{
			destination.Clear();
			destination.UnionWith(source);
		}


		//*****
		// List...
		public static void CopyFrom<T>(this List<T> destination, IEnumerable<T> source)
		{
			destination.Clear();
			destination.AddRange(source);
		}
	}
}
