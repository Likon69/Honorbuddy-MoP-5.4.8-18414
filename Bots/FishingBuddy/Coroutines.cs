using System;
using System.Linq;
using System.Threading.Tasks;
using Bots.Grind;
using Buddy.Coroutines;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Frames;
using Styx.CommonBot.POI;
using Styx.CommonBot.Routines;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Bots.FishingBuddy
{
    partial class Coroutines
	{
		private static Composite _deathBehavior;
		private static Composite _lootBehavior;
		private static DateTime _pulseTimestamp;
		static readonly WaitTimer AntiAfkTimer = new WaitTimer(TimeSpan.FromMinutes(2));
		private static readonly WaitTimer LootTimer = WaitTimer.FiveSeconds;

		static LocalPlayer Me { get { return StyxWoW.Me; }}

		static Composite LootBehavior
		{
			get { return _lootBehavior ?? (_lootBehavior = LevelBot.CreateLootBehavior()); }
		}

		static Composite DeathBehavior
		{
			get { return _deathBehavior ?? (_deathBehavior = LevelBot.CreateDeathBehavior()); }
		}


	    internal static void OnStart()
	    {
		    Gear_OnStart();
	    }

		internal static void OnStop()
		{
			Gear_OnStop();
		}

		public async static Task<bool> RootLogic()
		{
			CheckPulseTime();
			AnitAfk();
			// Is bot dead? if so, release and run back to corpse
			if (await DeathBehavior.ExecuteCoroutine())
				return true;

			if (await HandleCombat())
			{
				// reset the autoBlacklist timer 
				MoveToPoolTimer.Reset();
				return true;
			}

			if (await HandleVendoring())
				return true;

			if (!StyxWoW.Me.IsAlive
				|| (StyxWoW.Me.IsActuallyInCombat && Targeting.Instance.FirstUnit != null)
				|| RoutineManager.Current.NeedRest)
			{
				return false;
			}

			var poiGameObject = BotPoi.Current.AsObject as WoWGameObject;
			// FishingBuddy uses PoiType.Harvest for fishing pools so we only run LevelBot.LootBehavior when it's not set to a pool
			if (!StyxWoW.Me.IsFlying
				&& (BotPoi.Current.Type != PoiType.Harvest
				|| (poiGameObject != null && poiGameObject.SubType != WoWGameObjectType.FishingHole))
				&& await LootBehavior.ExecuteCoroutine())
			{
				return true;
			}
			// Fishing Logic

			if (await DoFishing())
				return true;

			return await FindNewPool() || await FollowPath();
		}

		private static async Task<bool> FindNewPool()
		{
			if (BotPoi.Current.Type != PoiType.None || !FishingBuddySettings.Instance.Poolfishing)
				return false;

			var lootTarget = LootTargeting.Instance.FirstObject as WoWGameObject;
			if (lootTarget == null || lootTarget.SubType != WoWGameObjectType.FishingHole
				|| Blacklist.Contains(lootTarget, BlacklistFlags.Node))
			{
				return false;
			}

			BotPoi.Current = new BotPoi(lootTarget, PoiType.Harvest);
			return true;
		}

		private static WoWPoint _lastMoveTo;
	    private static readonly WaitTimer MoveToLogTimer = WaitTimer.OneSecond;

	    public async static Task<bool> MoveTo(WoWPoint destination, string destinationName = null)
	    {
		    if (destination.DistanceSqr(_lastMoveTo) > 5*5)
		    {
			    if (MoveToLogTimer.IsFinished)
			    {
				    if (string.IsNullOrEmpty(destinationName))
					    destinationName = destination.ToString();
					FishingBuddyBot.Log("Moving to {0}", destinationName);
					MoveToLogTimer.Reset();
			    }
			    _lastMoveTo = destination;
		    }
		    var moveResult = Navigator.MoveTo(destination);
		    return moveResult != MoveResult.Failed && moveResult != MoveResult.PathGenerationFailed;
	    }

		public async static Task<bool> FlyTo(WoWPoint destination, string destinationName = null)
		{
			if (destination.DistanceSqr(_lastMoveTo) > 5 * 5)
			{
				if (MoveToLogTimer.IsFinished)
				{
					if (string.IsNullOrEmpty(destinationName))
						destinationName = destination.ToString();
					FishingBuddyBot.Log("Flying to {0}", destinationName);
					MoveToLogTimer.Reset();
				}
				_lastMoveTo = destination;
			}
			Flightor.MoveTo(destination);
			return true;
		}

		public async static Task<bool> Logout()
	    {
		    var activeMover = WoWMovement.ActiveMover;
		    if (activeMover == null)
			    return false;

		    var hearthStone =
			    Me.BagItems.FirstOrDefault(
				    h => h != null && h.IsValid && h.Entry == 6948 
						&& h.CooldownTimeLeft == TimeSpan.FromMilliseconds(0));
		    if (hearthStone == null)
		    {
			    FishingBuddyBot.Log("Unable to find a hearthstone");
				return false;
		    }

		    if (activeMover.IsMoving)
		    {
			    WoWMovement.MoveStop();
			    if (!await Coroutine.Wait(4000, () => !activeMover.IsMoving))
				    return false;
		    }

			hearthStone.UseContainerItem();
		    if (await Coroutine.Wait(15000, () => Me.Combat))
			    return false;

			FishingBuddyBot.Log("Logging out");
			Lua.DoString("Logout()");
			TreeRoot.Stop();
		    return true;
	    }

		static private void AnitAfk()
		{
			// keep the bot from going afk.
			if (AntiAfkTimer.IsFinished)
			{
				StyxWoW.ResetAfk();
				AntiAfkTimer.Reset();
			}
		}

		static void CheckPulseTime()
		{
			if (_pulseTimestamp == DateTime.MinValue)
			{
				_pulseTimestamp = DateTime.Now;
				return;
			} 

			var pulseTime = DateTime.Now - _pulseTimestamp;
			if (pulseTime >= TimeSpan.FromSeconds(3))
			{
				FishingBuddyBot.Err(
					"Warning: It took {0} seconds to pulse.\nThis can cause missed bites. To fix try disabling all plugins",
					pulseTime.TotalSeconds);
			}
			_pulseTimestamp = DateTime.Now;
		}

		private static async Task<bool> CheckLootFrame()
		{
			if (!LootTimer.IsFinished)
			{
				// loot everything.
				if (FishingBuddyBot.Instance.LootFrameIsOpen)
				{
					for (int i = 0; i < LootFrame.Instance.LootItems; i++)
					{
						LootSlotInfo lootInfo = LootFrame.Instance.LootInfo(i);
						if (FishingBuddyBot.Instance.FishCaught.ContainsKey(lootInfo.LootName))
							FishingBuddyBot.Instance.FishCaught[lootInfo.LootName] += (uint)lootInfo.LootQuantity;
						else
							FishingBuddyBot.Instance.FishCaught.Add(lootInfo.LootName, (uint)lootInfo.LootQuantity);
					}
					LootFrame.Instance.LootAll();
					LootTimer.Stop();
					await CommonCoroutines.SleepForLagDuration();
				}
				return true;
			}
			return false;
		}
	}
}
