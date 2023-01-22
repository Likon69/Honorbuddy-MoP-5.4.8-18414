using Styx;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;

namespace HighVoltz.AutoAngler.Composites
{
    public class WaterWalkingAction : Action
    {
        protected override RunStatus Run(object context)
        {
            // refresh water walking if needed
			if (!StyxWoW.Me.Mounted && WaterWalking.CanCast && (!WaterWalking.IsActive || StyxWoW.Me.IsSwimming))
            {
                WaterWalking.Cast();
                return RunStatus.Success;
            }
            return RunStatus.Failure;
        }
    }
}