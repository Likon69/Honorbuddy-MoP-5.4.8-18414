// Originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

//==================================================
// COPY WHAT YOU NEED FROM THIS FILE INTO YOUR QUEST BEHAVIOR:
// These are functional code snippets to be used for Quest Behavior (or other) development.
// Please copy whatever snippets you need from this file into TEMPLATE.cs.  We did not place them
// in TEMPLATE.cs to prevent unnecessarily bloating the base file.
// BE CERTAIN, that your namespace is unique, as other behaviors may be using the same snippets. (i.e.,
// Honorbuddy does not provide a location for common methods that are highly reusable in Quest Behaviors).
//==================================================


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
	public abstract partial class QuestBehaviorBase
	{
		public const double TAU = (2 * Math.PI);

		/// <summary>
		/// Returns the normalized ANGLEINRADIANS to the closed interval [-PI..+PI]
		/// </summary>
		/// <param name="angleInRadians"></param>
		/// <returns></returns>
		public static double NormalizeAngleToPi(double angleInRadians)
		{
			while (angleInRadians > Math.PI)  { angleInRadians -= (2 * Math.PI); }
			while (angleInRadians < -Math.PI) { angleInRadians += (2 * Math.PI); }
			return (angleInRadians);
		}
	}
}
