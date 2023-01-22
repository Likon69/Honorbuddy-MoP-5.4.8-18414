using System;

using Styx.Common;
using Styx.Plugins;
using Styx.WoWInternals;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;

namespace Styx
{
	public class AntiDrown : HBPlugin
	{
		public override string Name { get { return "Anti Drown"; } }
		public override string Author { get { return "Nesox"; } }
		public override Version Version { get { return _version; } }
		private readonly Version _version = new Version(1, 0, 0, 0);

		public override void Pulse()
		{
			var me = StyxWoW.Me;
			var value = me.GetMirrorTimerInfo(MirrorTimerType.Breath).CurrentTime;
			if (value < 60000 && me.IsAlive && me.IsSwimming && (value != 0) || (value > 900001))
			{
				DoCheck();
			}
		}

		private static void DoCheck()
		{
			if (!StyxWoW.Me.IsSwimming)
				return;

			Logging.Write("[Anti Drown]: Going for a nibble of air!");
			WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend, TimeSpan.FromMilliseconds(5000));
		}
	}
}
