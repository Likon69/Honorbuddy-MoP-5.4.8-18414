using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Styx;
using Styx.WoWInternals.WoWObjects;

namespace Honorbuddy.QuestBehaviorCore
{
	public static partial class UtilityCoroutine
	{
		// Convenience properties...
		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}
	}
}