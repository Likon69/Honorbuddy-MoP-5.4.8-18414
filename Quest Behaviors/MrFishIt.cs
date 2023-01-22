// Behavior originally contributed by BarryDurex
// [Quest Behavior] MrFishIt - v1.0.8
// Credits: [Bot]MrFishIt by Nesox | [Bot]PoolFishingBuddy by Iggi66
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//   

#region Summary and Documentation
// DOCUMENTATION: http://www.thebuddyforum.com/honorbuddy-forum/submitted-profiles/neutral/96244-quest-behavior-mrfishit-fishing-some-questitems.html
//  
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.MrFishIt
{
	[CustomBehaviorFileName(@"MrFishIt")]
	class MrFishIt : CustomForcedBehavior
	{
		public MrFishIt(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
				QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;

				CollectItemCount = GetAttributeAsNullable<int>("CollectItemCount", false, ConstrainAs.RepeatCount, null) ?? 1;
				CollectItemId = GetAttributeAsNullable<int>("CollectItemId", true, ConstrainAs.ItemId, null) ?? 0;
				PoolFishingBuddy.MaxCastRange = GetAttributeAsNullable<int>("MaxCastRange", false, ConstrainAs.ItemId, null) ?? 20;
				PoolFishingBuddy.MinCastRange = GetAttributeAsNullable<int>("MinCastRange", false, ConstrainAs.ItemId, null) ?? 15;
				MoveToPool = GetAttributeAsNullable<bool>("MoveToPool", false, null, null) ?? true;
				PoolId = GetAttributeAsNullable<int>("PoolId", false, ConstrainAs.ItemId, null) ?? 0;
				TestFishing = GetAttributeAsNullable<bool>("TestFishing", false, null, null) ?? false;
				WaterPoint = GetAttributeAsNullable<WoWPoint>("", false, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
			}

			catch (Exception except)
			{
				// Maintenance problems occur for a number of reasons.  The primary two are...
				// * Changes were made to the behavior, and boundary conditions weren't properly tested.
				// * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
				// In any case, we pinpoint the source of the problem area here, and hopefully it
				// can be quickly resolved.
				QBCLog.Exception(except);
				IsAttributeProblem = true;
			}
		}


		// Attributes provided by caller
		public int CollectItemCount { get; private set; }
		public int CollectItemId { get; private set; }
		public bool MoveToPool { get; private set; }
		public static int PoolId { get; private set; }
		public int QuestId { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }
		public bool TestFishing { get; private set; }
		public WoWPoint WaterPoint { get; private set; }

		// Private variables for internal state
		private Version _Version { get { return new Version(1, 0, 8); } }
		public static double _PoolGUID;
		private ConfigMemento _configMemento;
		private bool _cancelBehavior;
		private Composite _root;

		#region Overrides of CustomForcedBehavior

        public override void OnFinished()
        {
            TreeHooks.Instance.RemoveHook("Questbot_Main", CreateBehavior_QuestbotMain());
            // Clean up unmanaged resources (if any) here...
            if (_configMemento != null)
            {
                _configMemento.Dispose();
                _configMemento = null;
            }

            _cancelBehavior = true;

            Lua.Events.DetachEvent("LOOT_OPENED", HandleLootOpened);
            Lua.Events.AttachEvent("LOOT_CLOSED", HandleLootClosed);
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;

            if (Fishing.IsFishing)
                SpellManager.StopCasting();

            // Call parent Dispose() (if it exists) here ...                
            _root = null;
            base.OnFinished();
        }
		
		public override bool IsDone
		{
			get
			{
				bool ret = ((StyxWoW.Me.BagItems.FirstOrDefault(i => i.Entry == CollectItemId && i.StackCount == CollectItemCount) != null)     // normal completion
						|| !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete) || _cancelBehavior);
				if (ret)
				{ _root = null; _cancelBehavior = true; }
				return ret;
			}
		}

		static public WoWItem IteminBag(uint entry)
		{
			return StyxWoW.Me.BagItems.FirstOrDefault(i => i.Entry == entry);
		}


		public override void OnStart()
		{
			// This reports problems, and stops BT processing if there was a problem with attributes...
			// We had to defer this action, as the 'profile line number' is not available during the element's
			// constructor call.
			OnStart_HandleAttributeProblem();

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (!IsDone)
			{
				_configMemento = new ConfigMemento();

				Lua.Events.AttachEvent("LOOT_OPENED", HandleLootOpened);
				Lua.Events.AttachEvent("LOOT_CLOSED", HandleLootClosed);
				LootOpen = false;

				// Disable any settings that may cause us to dismount --
				// When we mount for travel via FlyTo, we don't want to be distracted by other things.
				// We also set PullDistance to its minimum value.  If we don't do this, HB will try
				// to dismount and engage a mob if it is within its normal PullDistance.
				// NOTE: these settings are restored to their normal values when the behavior completes
				// or the bot is stopped.
				CharacterSettings.Instance.HarvestHerbs = false;
				CharacterSettings.Instance.HarvestMinerals = false;
				CharacterSettings.Instance.LootChests = false;
				ProfileManager.CurrentProfile.LootMobs = false;
				CharacterSettings.Instance.NinjaSkin = false;
				CharacterSettings.Instance.SkinMobs = false;
				CharacterSettings.Instance.PullDistance = 1;
				
				// Make sure we don't get logged out
				GlobalSettings.Instance.LogoutForInactivity = false;

				TreeHooks.Instance.InsertHook("Questbot_Main", 0, CreateBehavior_QuestbotMain());

				PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
				if (quest == null)
					this.UpdateGoalText(QuestId, "Fishing Item [" + CollectItemId + "]");
				else
					this.UpdateGoalText(QuestId, "Fishing Item for [" + quest.Name + "]");

				QBCLog.DeveloperInfo("Fishing Item: {1} - Quantity: {2} - QuestID: {3}", _Version.ToString(), CollectItemId, CollectItemCount, CollectItemId);
			}
		}

		public static bool hasPoolFound
		{
			get
			{
				ObjectManager.Update();
				WoWGameObject _pool = (from unit in ObjectManager.GetObjectsOfType<WoWGameObject>(true, true)
									   orderby unit.Distance ascending

									   where !Blacklist.Contains(unit, BlacklistFlags.All)
									   where unit.IsValid
									   where unit.Entry == PoolId
									   select unit).FirstOrDefault();

				if (_pool != null)
				{
					//QBCLog.DeveloperInfo(DateTime.Now.ToLongTimeString() + " - hasPoolFound - set " + _pool.Guid.ToString() + " - " + _pool.Name + " - " + _pool.Distance2D);
					if (_PoolGUID != (double)_pool.Guid)
					{
						PoolFishingBuddy.looking4NewLoc = true;
						_PoolGUID = (double)_pool.Guid;
					}
					return true;
				}
				_PoolGUID = -1;
				return false;
			}
		}

		protected Composite CreateBehavior_QuestbotMain()
		{
			return _root ?? (_root =
				new Decorator(ret => !IsDone /*&& !LootOpen*/,
					new PrioritySelector(

						// Have we a facing waterpoint or a PoolId and PoolGUID? No, then cancel this behavior!
						new Decorator(ret => (!TestFishing && WaterPoint == WoWPoint.Empty && (PoolId == 0 || _PoolGUID == -1)) ||
						StyxWoW.Me.Combat || StyxWoW.Me.IsDead || StyxWoW.Me.IsGhost,
							new Action(ret => _cancelBehavior = true)),

						new Decorator(ret => (!Flightor.MountHelper.Mounted || !StyxWoW.Me.IsFlying) && (TestFishing || (WaterPoint != WoWPoint.Empty ||
							(PoolId != 0 && hasPoolFound && PoolFishingBuddy.saveLocation.Count > 0 &&
							StyxWoW.Me.Location.Distance(PoolFishingBuddy.saveLocation[0]) <= 2.5 && !PoolFishingBuddy.looking4NewLoc))),
							CreateFishBehavior()
							),

						PoolFishingBuddy.CreateMoveToPoolBehavior()                            
					)));
		}

		private bool LootOpen { get; set; }
		private void HandleLootOpened(object sender, LuaEventArgs args)
		{
			LootOpen = true;
			QBCLog.DeveloperInfo("looting..");

			object[] arg = args.Args;
			if ((double)arg[0] == 0)
			{
				QBCLog.DeveloperInfo("no autoloot");
				Lua.DoString("for i=1, GetNumLootItems() do LootSlot(i) ConfirmBindOnUse() end CloseLoot()");
			}
			QBCLog.DeveloperInfo("looting done.");
		}
		private void HandleLootClosed(object sender, LuaEventArgs args) { QBCLog.DeveloperInfo("(hook)looting done."); LootOpen = false; }

		private Composite CreateFishBehavior()
		{
			return new PrioritySelector(

				// Don't do anything if the global cooldown timer is running
				new Decorator(ret => SpellManager.GlobalCooldown,
					new ActionIdle()),

				// Do we need to interact with the bobber?
				new Decorator(ret => Fishing.IsBobberBobbing,
					new Sequence(

						// Interact with the bobber
						new Action(delegate
						{
							QBCLog.Info("[MrFishIt] Got a bite!");
							WoWGameObject bobber = Fishing.FishingBobber;

							if (bobber != null)
								bobber.Interact();

							else
								return RunStatus.Failure;

							return RunStatus.Success;
						}),

						// Wait for the lootframe
						new Wait(5, ret => LootFrame.Instance.IsVisible,
							new Sequence(
								new Action(ret => TreeRoot.StatusText = "[MrFishIt] Looting ..."),
								new ActionAlwaysSucceed()
								))
							)),

				// Do we need to recast?
                new Decorator(ret => Fishing.FishingBobber == null || !Fishing.IsFishing || (Fishing.IsFishing && PoolId != 0 && !PoolFishingBuddy.BobberIsInTheHole),
                    new Sequence(
						new Action(ret => { if (Fishing.FishingBobber == null) QBCLog.DeveloperInfo("no FishingBobber found!?"); }),
						new Action(ret => QBCLog.Info("Casting...")),

                        new Decorator(ctx =>WaterPoint != WoWPoint.Empty,
                            new Sequence(
                                new Action(ctx => StyxWoW.Me.SetFacing(WaterPoint)),
                                new Sleep(200))),

                        new Decorator(ctx => PoolId != 0,
                            new Sequence(
                                new Action(ctx => StyxWoW.Me.SetFacing(_PoolGUID.asWoWGameObject())),
                                new Sleep(200))),

						new Action(ret => SpellManager.Cast("Fishing")),
                        new DecoratorContinue(ctx =>PoolId != 0, new Sleep(300)),
						new Wait(2, ret => Fishing.IsFishing, new ActionAlwaysSucceed())
						//CreateWaitForLagDuration()
						)),

				new Sequence(
					new ActionSetActivity("[MrFishIt] Waiting for bobber to splash ..."),
					new ActionIdle()
					));
		}

		#endregion
	}


	static class Fishing
	{
		//static readonly List<int> FishingIds = new List<int> { 131474, 7620, 7731, 7732, 18248, 33095, 51294, 88868 };
		/// <summary>
		/// Returns true if you are fishing
		/// </summary>
		public static bool IsFishing { get { return /*FishingIds.Contains(StyxWoW.Me.ChanneledCastingSpellId);*/ (StyxWoW.Me.IsCasting | StyxWoW.Me.HasAura("Fishing")); } }

		/// <summary>
		/// Returns your fishing pole
		/// </summary>
		public static WoWItem FishingPole
		{ get { return ObjectManager.GetObjectsOfType<WoWItem>().Where(b => b.IsFishingPole()).FirstOrDefault(); } }

		/// <summary>
		/// Returns true if you have a temp-enchantm on your pole
		/// </summary>
		public static bool GotLure { get { return Lua.GetReturnVal<bool>("return GetWeaponEnchantInfo()", 0); } }

		/// <summary>
		/// Returns true if the fishing bobber is bobbing
		/// </summary>
		public static bool IsBobberBobbing { get { return FishingBobber != null && FishingBobber.IsBobbing(); } }

		/// <summary>
		/// Returns the current fishing bobber in use, null otherwise
		/// </summary>
		public static WoWGameObject FishingBobber
		{
			get
			{
				ObjectManager.Update();
				var d = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(o => o != null && o.IsValid && o.CreatedByGuid == StyxWoW.Me.Guid);
				//if (d != null)
				//    QBCLog.DeveloperInfo("[FishingBobber]Name: {0} - AnimationState: {1}", d.Name, d.AnimationState);

				return ObjectManager.GetObjectsOfType<WoWGameObject>()
					.FirstOrDefault(o => o != null && o.IsValid && o.CreatedByGuid == StyxWoW.Me.Guid &&
						o.SubType == WoWGameObjectType.FishingNode);
			}
		}
	}

	static class Extensions
	{
		static readonly List<uint> PoleIds = new List<uint> { 44050, 19970, 45991, 45992, 45858, 19022, 25978, 6367, 12225, 6366, 6256, 6365 };

		public static bool IsFishingPole(this WoWItem value)
		{
			if (value == null)
				return false;

			return PoleIds.Contains(value.Entry);
		}

		public static bool IsBobbing(this WoWGameObject value)
		{
			if (value == null)
				return false;

			return ((WoWFishingBobber)value.SubObj).IsBobbing;
			//return null != Fishing.FishingBobber ? 1 == Fishing.FishingBobber.AnimationState : false;

			//return value.AnimationState == 1;
		}

		public static WoWGameObject asWoWGameObject(this double GUID)
		{
			ObjectManager.Update();
			WoWGameObject _o = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(o => o.Guid == GUID);
			return _o;
		}
	}

	static class PoolFishingBuddy
	{
		private static Stopwatch movetopoolTimer = new Stopwatch();
		public static bool looking4NewLoc;
		private static WoWGameObject Pool { get { if (MrFishIt.hasPoolFound) { } return MrFishIt._PoolGUID.asWoWGameObject(); } }
		static public List<WoWPoint> saveLocation = new List<WoWPoint>(100);
		static public List<WoWPoint> badLocations = new List<WoWPoint>(100);
		static public int newLocAttempts = 0;

		private static int MaxNewLocAttempts = 5;
		public static int MaxCastRange { get; set; }
		public static int MinCastRange { get; set; }

		/// <summary>
		/// fixed by BarryDurex
		/// </summary>
		static public WoWPoint getSaveLocation(WoWPoint Location, int minDist, int maxDist, int traceStep, int traceStep2)
		{
			QBCLog.DeveloperInfo("Navigation: Looking for save Location around {0}.", Location);

			WoWPoint point = WoWPoint.Empty;
			float _PIx2 = 3.14159f * 2f;

			for (int i = 0, x = minDist; i < traceStep && x < maxDist && looking4NewLoc == true; i++)
			{
				WoWPoint p = Location.RayCast((i * _PIx2) / traceStep, x);

				p.Z = getGroundZ(p);
				WoWPoint pLoS = p;
				pLoS.Z = p.Z + 0.5f;

				if (p.Z != float.MinValue && !badLocations.Contains(p) && StyxWoW.Me.Location.Distance(p) > 1)
				{
					if (getHighestSurroundingSlope(p) < 1.2f && GameWorld.IsInLineOfSight(pLoS, Location))
					{
						point = p;
						break;
					}
				}

				if (i == (traceStep - 1))
				{
					i = 0;
					x++;
				}
			}

			for (int i = 0, x = 10; i < traceStep2 && x < maxDist && looking4NewLoc == true; i++)
			{
				WoWPoint p2 = point.RayCast((i * _PIx2) / traceStep2, x);

				p2.Z = getGroundZ(p2);
				WoWPoint pLoS = p2;
				pLoS.Z = p2.Z + 0.5f;

				if (p2.Z != float.MinValue && !badLocations.Contains(p2) && StyxWoW.Me.Location.Distance(p2) > 1)
				{
					if (getHighestSurroundingSlope(p2) < 1.2f && GameWorld.IsInLineOfSight(pLoS, Location) && p2.Distance2D(Location) <= maxDist)
					{
						looking4NewLoc = false;
						QBCLog.DeveloperInfo("Navigation: Moving to {0}. Distance: {1}", p2, Location.Distance(p2));
						return p2;
					}
				}

				if (i == (traceStep2 - 1))
				{
					i = 0;
					x++;
				}
			}

			QBCLog.Warning("{0} - No valid points returned by RayCast, blacklisting for 2 minutes.", DateTime.Now.ToLongTimeString());
			Blacklist.Add(Pool, BlacklistFlags.All, TimeSpan.FromMinutes(2));
			return WoWPoint.Empty;
		}

		static public WoWPoint getSaveLocation(WoWPoint Location, int minDist, int maxDist, int traceStep)
		{
			QBCLog.DeveloperInfo("Navigation: Looking for save Location around {0}.", Location);

			float _PIx2 = 3.14159f * 2f;

			for (int i = 0, x = minDist; i < traceStep && x < maxDist && looking4NewLoc == true; i++)
			{
				WoWPoint p = Location.RayCast((i * _PIx2) / traceStep, x);

				p.Z = getGroundZ(p);
				WoWPoint pLoS = p;
				pLoS.Z = p.Z + 0.5f;

				if (p.Z != float.MinValue && !badLocations.Contains(p) && StyxWoW.Me.Location.Distance(p) > 1)
				{
					if (getHighestSurroundingSlope(p) < 1.2f && GameWorld.IsInLineOfSight(pLoS, Location) /*&& Navigator.CanNavigateFully(StyxWoW.Me.Location, Location)*/)
					{
						looking4NewLoc = false;
						QBCLog.DeveloperInfo("Navigation: Moving to {0}. Distance: {1}", p, Location.Distance(p));
						return p;
					}
				}

				if (i == (traceStep - 1))
				{
					i = 0;
					x++;
				}
			}

			if (Pool != null)
			{
				QBCLog.Warning("No valid points returned by RayCast, blacklisting for 2 minutes.");
				Blacklist.Add(Pool, BlacklistFlags.All, TimeSpan.FromMinutes(2));
				return WoWPoint.Empty;
			}
			else
			{
				QBCLog.Warning("No valid points returned by RayCast, can't navigate without user interaction. Stopping!");
				TreeRoot.Stop();
				return WoWPoint.Empty;
			}
			
		}

		/// <summary>
		/// Credits to funkescott.
		/// </summary>
		/// <returns>Highest slope of surrounding terrain, returns 100 if the slope can't be determined</returns>
		public static float getHighestSurroundingSlope(WoWPoint p)
		{
			QBCLog.DeveloperInfo("Navigation: Sloapcheck on Point: {0}", p);
			float _PIx2 = 3.14159f * 2f;
			float highestSlope = -100;
			float slope = 0;
			int traceStep = 15;
			float range = 0.5f;
			WoWPoint p2;
			for (int i = 0; i < traceStep; i++)
			{
				p2 = p.RayCast((i * _PIx2) / traceStep, range);
				p2.Z = getGroundZ(p2);
				slope = Math.Abs( getSlope(p, p2) );
				if( slope > highestSlope )
				{
					highestSlope = (float)slope;
				}
			}
			QBCLog.DeveloperInfo("Navigation: Highslope {0}", highestSlope);
			return Math.Abs( highestSlope );
		}

		/// <summary>
		/// Credits to funkescott.
		/// </summary>
		/// <param name="p1">from WoWPoint</param>
		/// <param name="p2">to WoWPoint</param>
		/// <returns>Return slope from WoWPoint to WoWPoint.</returns>
		public static float getSlope(WoWPoint p1, WoWPoint p2)
		{
			float rise = p2.Z - p1.Z;
			float run = (float)Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));

			return rise / run;
		}

		/// <summary>
		/// Credits to exemplar.
		/// </summary>
		/// <returns>Z-Coordinates for PoolPoints so we don't jump into the water.</returns>
		public static float getGroundZ(WoWPoint p)
		{
			WoWPoint ground = WoWPoint.Empty;

			GameWorld.TraceLine(new WoWPoint(p.X, p.Y, (p.Z + MaxCastRange)), new WoWPoint(p.X, p.Y, (p.Z - 0.8f)), GameWorld.CGWorldFrameHitFlags.HitTestGroundAndStructures/* | GameWorld.CGWorldFrameHitFlags.HitTestBoundingModels | GameWorld.CGWorldFrameHitFlags.HitTestWMO*/, out ground);

			if (ground != WoWPoint.Empty)
			{
				QBCLog.DeveloperInfo("Ground Z: {0}.", ground.Z);
				return ground.Z;
			}
			QBCLog.DeveloperInfo("Ground Z returned float.MinValue.");
			return float.MinValue;
		}

		public static  Composite CreateMoveToPoolBehavior()
		{
			return new Decorator(ret => Pool != null && !Blacklist.Contains(Pool, BlacklistFlags.All),
				new Sequence(
					new Action(ret => QBCLog.DeveloperInfo("Composit: CreateMoveToPoolBehaviour")),
					new Action(ret => movetopoolTimer.Start()),

					new PrioritySelector(

						// Timer
						new Decorator(ret => movetopoolTimer.ElapsedMilliseconds > 30000,
							new Sequence(
								new Action(ret => QBCLog.Warning("Timer for moving to ground elapsed, blacklisting for 2 minutes.")),
								new Action(ret => Blacklist.Add(MrFishIt._PoolGUID.asWoWGameObject(), BlacklistFlags.All, TimeSpan.FromMinutes(2)))                                
						)),

						//// Blacklist if other Player is detected
						//new Decorator(ret => Helpers.PlayerDetected && !PoolFisherSettings.Instance.NinjaPools,
						//    new Sequence(
						//            new Action(ret => QBCLog.Warning("Detected another player in pool range, blacklisting for 2 minutes.")),
						//            new Action(ret => Helpers.BlackListPool(Pool)),
						//            new Action(delegate { return RunStatus.Success; })
						//)),

						// Get PoolPoint
						new Decorator(ret => looking4NewLoc,
							new Sequence(
								new ActionSetActivity(ret => "[MrFishIt] Looking for valid Location"),
								new Action(ret => WoWMovement.MoveStop()),
								new PrioritySelector(

									// Pool ist Feuerteich 
									new Decorator(ret => Pool.Entry == 207734,
										new Sequence(
										new Action(ret => saveLocation.Add(getSaveLocation(Pool.Location, MinCastRange, 
											MaxCastRange, 50, 60))),
										new Action(ret => QBCLog.DeveloperInfo("Added {0} to saveLocations.", saveLocation[0]))
									)),

									// Pool ist kein Feuerteich 
									new Decorator(ret => Pool.Entry != 207734,
										new Sequence(
										new Action(ret => saveLocation.Add(getSaveLocation(Pool.Location, MinCastRange, 
											MaxCastRange, 50))),
										new Action(ret => QBCLog.DeveloperInfo("Added {0} to saveLocations.", saveLocation[0]))
									))                                
						))),

						// Move to PoolPoint
						new Decorator(pool => Pool != null && saveLocation.Count > 0 && !looking4NewLoc,
							new PrioritySelector(

								// Pool still there?
								new Decorator(ret => MrFishIt._PoolGUID.asWoWGameObject() == null,
									new Sequence(
										new Action(ret => QBCLog.Info("Fishing Pool is gone, moving on."))
								)),

								// reached max attempts for new locations?
								new Decorator(ret => newLocAttempts == MaxNewLocAttempts + 1,
									new Sequence(
									new Action(ret => QBCLog.Warning("Reached max. attempts for new locations, blacklisting for 2 minutes.")),
									new Action(ret => Blacklist.Add(Pool, BlacklistFlags.All, TimeSpan.FromMinutes(2)))
								)),

								// tries++
								new Decorator(ret => StyxWoW.Me.Location.Distance(saveLocation[0]) <= 2 && !Flightor.MountHelper.Mounted && !StyxWoW.Me.IsMoving,
									new Sequence(
										new Wait(2, ret => StyxWoW.Me.IsCasting, new ActionIdle()),
										new Action(ret => newLocAttempts++),
										new Action(ret => QBCLog.Warning("Moving to new Location.. Attempt: {0} of {1}.", newLocAttempts, MaxNewLocAttempts))
								)),
											

								// Dismount
								new Decorator(ret => StyxWoW.Me.Location.Distance(new WoWPoint(saveLocation[0].X, saveLocation[0].Y, saveLocation[0].Z + 2)) <= 1.5 && Flightor.MountHelper.Mounted, //&& !StyxWoW.Me.IsMoving,
									//new PrioritySelector(

										//new Decorator(ret => Helpers.CanWaterWalk && !Helpers.hasWaterWalking,
											new Sequence(
												new Action(ret => StyxWoW.Me.SetFacing(Pool.Location)),
												//new Action(ret => Helpers.WaterWalk()),
												//new SleepForLagDuration(),
												new Action(ret => WoWMovement.Move(WoWMovement.MovementDirection.Descend)),
												new Action(ret => Flightor.MountHelper.Dismount()),
												new Action(ret => WoWMovement.MoveStop()),
												new Action(ret => QBCLog.DeveloperInfo("Navigation: Dismount. Current Location {0}, PoolPoint: {1}, Distance: {2}", StyxWoW.Me.Location, new WoWPoint(saveLocation[0].X, saveLocation[0].Y, saveLocation[0].Z + 2), StyxWoW.Me.Location.Distance(new WoWPoint(saveLocation[0].X, saveLocation[0].Y, saveLocation[0].Z + 2)))),
												new Wait(3, ret => Flightor.MountHelper.Mounted, new ActionIdle()),
												new SleepForLagDuration()

										//new Decorator(ret => !Helpers.CanWaterWalk || (Helpers.CanWaterWalk && Helpers.hasWaterWalking),
										//    new Sequence(
										//        new Action(ret => StyxWoW.Me.SetFacing(Pool.Location)),
										//        new Action(ret => WoWMovement.Move(WoWMovement.MovementDirection.Descend)),
										//        new Action(ret => Mount.Dismount()),
										//        new Action(ret => WoWMovement.MoveStop()),
										//        new Action(ret => QBCLog.DeveloperInfo("Navigation: Dismount. Current Location {1}, PoolPoint: {2}, Distance: {3}", StyxWoW.Me.Location, new WoWPoint(saveLocation[0].X, saveLocation[0].Y, saveLocation[0].Z + 2), StyxWoW.Me.Location.Distance(new WoWPoint(saveLocation[0].X, saveLocation[0].Y, saveLocation[0].Z + 2)))),
										//        new Wait(3, ret => Flightor.MountHelper.Mounted, new ActionIdle()),
                                        //        new SleepForLagDuration()))
										
								)),

								// in Line Line of sight?
								new Decorator(ret => StyxWoW.Me.Location.Distance(saveLocation[0]) <= 2 && !Pool.InLineOfSight && !StyxWoW.Me.IsMoving && !Flightor.MountHelper.Mounted,
									new Sequence(
									new Action(ret => QBCLog.Warning("Pool is not in Line of Sight!")),
									new Action(ret => badLocations.Add(saveLocation[0])),
									new Action(ret => saveLocation.Clear()),
									new Action(ret => newLocAttempts++),
									new Action(ret => QBCLog.Warning("Moving to new Location.. Attempt: {0} of {1}.", newLocAttempts, MaxNewLocAttempts)),
									new Action(ret => looking4NewLoc = true)
								)),

								// Move without Mount
								new Decorator(ret => (StyxWoW.Me.Location.Distance(saveLocation[0]) > 1 && StyxWoW.Me.Location.Distance(saveLocation[0]) <= 10 && !Flightor.MountHelper.Mounted && GameWorld.IsInLineOfSight(StyxWoW.Me.Location, saveLocation[0])) && !StyxWoW.Me.IsSwimming,
									new PrioritySelector(

										// Mount if not mounted and Navigator is not able to generate a path
										new Decorator(ret => !Navigator.CanNavigateFully(StyxWoW.Me.Location, saveLocation[0]),
											new Action(ret => Flightor.MountHelper.MountUp())),

										new Sequence(
											new ActionSetActivity(ret => "Moving to new Location"),
											new Action(ret => QBCLog.DeveloperInfo("Navigation: Moving to Pool: " + saveLocation[0] + ", Location: " + StyxWoW.Me.Location + ", distance: " + saveLocation[0].Distance(StyxWoW.Me.Location) + " (Not Mounted)")),
											//new Action(ret => QBCLog.Info("{0} - Moving to Pool: {1}, Location: {2}, Distance: {3}. (Not Mounted)", Helpers.TimeNow, PoolPoints[0], StyxWoW.Me.Location, PoolPoints[0].Distance(StyxWoW.Me.Location))),
											// Move
											new Action(ret => Navigator.MoveTo(saveLocation[0]))
										)
								)),

								// Move with Mount
								new Decorator(ret => (StyxWoW.Me.Location.Distance(saveLocation[0]) > 10 || Flightor.MountHelper.Mounted || (StyxWoW.Me.Location.Distance(saveLocation[0]) <= 10 && !GameWorld.IsInLineOfSight(StyxWoW.Me.Location, saveLocation[0])) && !StyxWoW.Me.IsSwimming),
									new PrioritySelector(

										// Mount if not mounted
										new Decorator(ret => !Flightor.MountHelper.Mounted && !StyxWoW.Me.Combat,
											new Action(ret => Flightor.MountHelper.MountUp())),

										// Move
										new Sequence(
											new ActionSetActivity(ret => "[MrFishIt] Moving to Ground"),
											new Action(ret => QBCLog.DeveloperInfo("Navigation: Moving to Pool: " + saveLocation[0] + ", Location: " + StyxWoW.Me.Location + ", distance: " + StyxWoW.Me.Location.Distance(new WoWPoint(saveLocation[0].X, saveLocation[0].Y, saveLocation[0].Z + 2)) + " (Mounted)")),
											//new Action(ret => QBCLog.Info("{0} - Moving to Pool: {1}, Location: {2}, Distance: {3}. (Mounted)", Helpers.TimeNow, PoolPoints[0], StyxWoW.Me.Location, PoolPoints[0].Distance(StyxWoW.Me.Location)),
											new Action(ret => Flightor.MoveTo(new WoWPoint(saveLocation[0].X, saveLocation[0].Y, saveLocation[0].Z + 2), true))
										)
								))
							))
				)));
		}

		/// <summary>
		/// Checks if the bobber is in distance of 3.6 to location of the pool.
		/// </summary>
		static public bool BobberIsInTheHole
		{
	        get
            {
	            if (Fishing.FishingBobber != null && Pool != null)
	            {
	                if (Fishing.FishingBobber.Location.Distance2D(Pool.Location) <= 3.6f)
	                {
	                    return true;
	                }
	            }
	            return false;
	        }
		}
	}
}
