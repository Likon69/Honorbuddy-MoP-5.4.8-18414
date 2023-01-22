using Styx;
using Styx.CommonBot;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

namespace HighVoltz.AutoAngler.Composites
{
    public class FollowPathAction : Action
    {
		private readonly AutoAnglerSettings _settings = AutoAnglerBot.Instance.MySettings;

        protected override RunStatus Run(object context)
        {
            if (LootAction.GetLoot())
                return RunStatus.Success;
            //  dks can refresh water walking while flying around.
			if (AutoAnglerBot.Instance.MySettings.UseWaterWalking &&
                StyxWoW.Me.Class == WoWClass.DeathKnight && !WaterWalking.IsActive)
            {
                WaterWalking.Cast();
            }
			if (AutoAnglerBot.CurrentPoint == WoWPoint.Zero)
                return RunStatus.Failure;
			if (AutoAnglerBot.FishAtHotspot && StyxWoW.Me.Location.Distance(AutoAnglerBot.CurrentPoint) <= 3)
            {
                return RunStatus.Failure;
            }
            //float speed = StyxWoW.Me.MovementInfo.CurrentSpeed;
            //float modifier = _settings.Fly ? 5f : 2f;
            //float precision = speed > 7 ? (modifier*speed)/7f : modifier;
            float precision = StyxWoW.Me.IsFlying ? AutoAnglerSettings.Instance.PathPrecision : 3;
			if (StyxWoW.Me.Location.Distance(AutoAnglerBot.CurrentPoint) <= precision)
				AutoAnglerBot.CycleToNextPoint();
            if (_settings.Fly)
            {
				if (StyxWoW.Me.IsSwimming)
                {
					if (StyxWoW.Me.GetMirrorTimerInfo(MirrorTimerType.Breath).CurrentTime > 0)
                        WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
					else if (StyxWoW.Me.MovementInfo.IsAscending || StyxWoW.Me.MovementInfo.JumpingOrShortFalling)
                        WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend);
                }
                if (!StyxWoW.Me.Mounted)
                    Flightor.MountHelper.MountUp();
				Flightor.MoveTo(AutoAnglerBot.CurrentPoint);
            }
            else
            {
				if (!StyxWoW.Me.Mounted && Mount.ShouldMount(AutoAnglerBot.CurrentPoint) && Mount.CanMount())
					Mount.MountUp(() => AutoAnglerBot.CurrentPoint);
				var result = Navigator.MoveTo(AutoAnglerBot.CurrentPoint);
	            if (result != MoveResult.Failed && result != MoveResult.PathGenerationFailed)
	            {
					InactivityDetector.Reset();
	            }
            }
            return RunStatus.Success;
        }

 
    }
}