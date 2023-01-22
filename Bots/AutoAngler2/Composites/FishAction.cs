using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.Helpers;

using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;

namespace HighVoltz.AutoAngler.Composites
{
    public class FishAction : Action
    {
	    public static readonly Stopwatch LineRecastSW = new Stopwatch();
        private LocalPlayer Me {get { return StyxWoW.Me; }}

        private readonly Stopwatch _timeAtPoolSW = new Stopwatch();
        private int _castCounter;
        private ulong _lastPoolGuid;
	    private WaitTimer _bobberInteractionTimer;


        protected override RunStatus Run(object context)
        {
            WoWGameObject pool = null;
			if (Me.Mounted)
                Mount.Dismount("Fishing");
			if (Me.IsMoving || Me.IsFalling)
            {
                WoWMovement.MoveStop();
				if (!Me.HasAura("Levitate"))
                    return RunStatus.Success;
            }
            if (BotPoi.Current != null && BotPoi.Current.Type == PoiType.Harvest)
            {
                pool = (WoWGameObject)BotPoi.Current.AsObject;
                if (pool == null || !pool.IsValid)
                {
                    BotPoi.Current = null;
                    return RunStatus.Failure;
                }
                if (pool.Guid != _lastPoolGuid)
                {
                    _lastPoolGuid = pool.Guid;
                    _timeAtPoolSW.Reset();
                    _timeAtPoolSW.Start();
                }
                // safety check. if spending more than 5 mins at pool than black list it.
                if (_timeAtPoolSW.ElapsedMilliseconds >= AutoAnglerBot.Instance.MySettings.MaxTimeAtPool * 60000)
                {
                    Utils.BlacklistPool(pool, TimeSpan.FromMinutes(10), "Spend too much time at pool");
                    return RunStatus.Failure;
                }
                // Blacklist pool if we have too many failed casts
                if (_castCounter >= AutoAnglerBot.Instance.MySettings.MaxFailedCasts)
                {
                    AutoAnglerBot.Instance.Log("Moving to a new fishing location since we have {0} failed casts",
                                            _castCounter);
                    _castCounter = 0;
                    MoveToPoolAction.PoolPoints.RemoveAt(0);
                    return RunStatus.Success;
                }

                // face pool if not facing it already.
				if (!IsFacing2D(Me.Location, Me.Rotation, pool.Location, WoWMathHelper.DegreesToRadians(5)))
                {
                    LineRecastSW.Reset();
                    LineRecastSW.Start();
					Me.SetFacing(pool.Location);
                    // SetFacing doesn't really update my angle in game.. still tries to fish using prev angle. so I need to move to update in-game angle
                    WoWMovement.Move(WoWMovement.MovementDirection.ForwardBackMovement);
                    WoWMovement.MoveStop(WoWMovement.MovementDirection.ForwardBackMovement);
                    return RunStatus.Success;
                }
            }
			if (Me.IsCasting)
            {
                WoWGameObject bobber = null;
                try
                {
                    bobber = ObjectManager.GetObjectsOfType<WoWGameObject>()
                        .FirstOrDefault(o => o.IsValid && o.SubType == WoWGameObjectType.FishingNode &&
											 o.CreatedBy.Guid == Me.Guid);
                }
                catch (Exception)
                {
                }
                if (bobber != null)
                {
                    // recast line if it's not close enough to pool
                    if (AutoAnglerBot.Instance.MySettings.Poolfishing && pool != null &&
                        bobber.Location.Distance(pool.Location) > 3.6f)
                    {
                        CastLine();
                    }
                    // else lets see if there's a bite!
					else if (((WoWFishingBobber)bobber.SubObj).IsBobbing && _bobberInteractionTimer == null)
                    {
						_bobberInteractionTimer = new WaitTimer(DelayAfterBobberTrigger);
						_bobberInteractionTimer.Reset();
                    }
					else if (_bobberInteractionTimer != null && _bobberInteractionTimer.IsFinished)
					{
						_bobberInteractionTimer = null;
						_castCounter = 0;
						bobber.SubObj.Use();
						LootAction.WaitingForLootSW.Restart();
					}
                }
                return RunStatus.Success;
            }						
            CastLine();
            return RunStatus.Success;
        }

        private void CastLine()
        {
            if (LineRecastSW.IsRunning && LineRecastSW.ElapsedMilliseconds < 2000)
                return;
            LineRecastSW.Reset();
            _castCounter++;
            SpellManager.Cast("Fishing");
			StyxWoW.ResetAfk();
			InactivityDetector.Reset();
            LineRecastSW.Start();
        }

        public static bool IsFacing2D(WoWPoint me, float myFacingRadians, WoWPoint target, float arcRadians)
        {
            me.Z = target.Z = 0;
            arcRadians = WoWMathHelper.NormalizeRadian(arcRadians);
            float num = WoWMathHelper.CalculateNeededFacing(me, target);
            float num2 = WoWMathHelper.NormalizeRadian(num - myFacingRadians);
            if (num2 > 3.1415926535897931)
            {
                num2 = (float)(6.2831853071795862 - num2);
            }
            bool result = (num2 <= arcRadians / 2f);
            return result;
        }

		public static TimeSpan DelayAfterBobberTrigger
		{
			get
			{
				return (Utils.Rnd.Next(1, 100) < 85)
					? TimeSpan.FromMilliseconds(Utils.Rnd.Next(300, 700))     // 'normal' delay
					: TimeSpan.FromMilliseconds(Utils.Rnd.Next(600, 2400));   // 'outlier' delay
			}
		}
    }
}