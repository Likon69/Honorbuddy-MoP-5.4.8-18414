// Behavior originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

#region Summary and Documentation
// QUICK DOX:
// 14382-Gilneas-TwoBySea is a point-solution behavior.
// The behavior:
//  1) Kills the Forsaken Machinists and takes their Catapult
//  2) Uses the Catapult to board the boat
//  3) Locates and kills the boat's Captain
//  4) Exits the boat by 'jumping down'
//  5) Repeats the above steps for both Captain Anson and Captain Morris
//  6) Profit!
// 
// THINGS TO KNOW:
// * If for any reason, we lose the Catapult, another one will be acquired
// * If for any reason we miss boarding the boat with the Catapult,
//      the behavior will try again (and keep trying).
// * If the behavior gets attacked while driving the Catapult,
//      the Catapult will be exited and the mob dealt with.
//      Another Catapult will be acquired once this process is complete.
// * Avoids competing with other players in appropriating a Catapult.
// * Will wait for the Captain to respawn if someone else has killed him
//      recentlhy.
// * The behavior can be started immediately after picking up the quest.
//      I.e., there is no need to pre-position to any particular location.
// * The behavior will move back to the Catapult area near the bridge
//      before terminating.  This allows proceeding with the profile from
//      a sane and manageable location.
//
// EXAMPLE:
//     <CustomBehavior File="14382-Gilneas-TwoBySea" />
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;

using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.TwoBySea
{
	[CustomBehaviorFileName(@"SpecificQuests\14382-Gilneas-TwoBySea")]
	public class TwoBySea : CustomForcedBehavior
	{
		#region Consructor and Argument Processing
		public TwoBySea(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// Quest handling...
				QuestId = 14382; // http://wowhead.com/quest=14382
				QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = QuestInLogRequirement.InLog;

				Task_CaptainAnson = new TaskDetail(
					"Captain Anson",
					36397,  // Captain Anson:  http://wowhead.com/npc=36397
					new WoWPoint(-2073.466, 2632.036, 2.717113),    // Launch Position
					new WoWPoint(-2124.181, 2662.547, 8.256202),    // Target Position
					0.22,                                           // Needed Azimuth (in radians)
					new WoWPoint(-2105.5, 2655.504, 0.5987438),     // Jump down off boat point
					c => IsQuestObjectiveComplete(QuestId,  1)
					);

				Task_CaptainMorris = new TaskDetail(
					"Captain Morris",
					36399, // Captain Morris:  http://wowhead/npc=36399
					new WoWPoint(-2182.197, 2549.495, 2.720596),    // Launch Position
					new WoWPoint(-2225.435, 2565.901, 8.664543),    // Target Position
					0.18,                                           // Needed Azimuth (in radians)
					new WoWPoint(-2207.448, 2558.94, 0.950241),     // Jump down off boat point
					c => IsQuestObjectiveComplete(QuestId, 2));

				MobId_ForsakenMachinist = 36292; // http://wowhead.com/npc=36292
				VehicleId_ForsakenCatapult = 36283; // http://www.wowhead.com/npc=36283

				Location_CatapultFarm = new WoWPoint(-2052.313, 2577.324, 1.39316).FanOutRandom(20.0);

				Lua_LaunchCommand = "if GetPetActionCooldown(1) == 0 then CastPetAction(1) end"; // http://www.wowhead.com/spell=66251

				// Tunables...
				CombatMaxEngagementRangeDistance = 23.0;
				NonCompeteDistanceForCatapults = 25.0;
				VehicleLocationPathPrecision = 3.5;

				// Blackspots...
				Blackspots = new List<Blackspot>()
				{
					new Blackspot(new WoWPoint(-2126.297, 2536.12, 7.228605), 12.0f, 1.0f)
				};

				// Semantic coherency / covariant dependency checks --
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


		// Variables for Attributes provided by caller
		public double CombatMaxEngagementRangeDistance { get; private set; }
		public int QuestId { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }

		public IEnumerable<Blackspot> Blackspots { get; private set; }
		public WoWPoint Location_CatapultFarm { get; private set; }

		public TaskDetail Task_CaptainAnson { get; private set; }
		public TaskDetail Task_CaptainMorris { get; private set; }

		public int MobId_ForsakenMachinist { get; private set; }

		public string Lua_LaunchCommand { get; private set; }

		public double NonCompeteDistanceForCatapults { get; private set; }

		public int VehicleId_ForsakenCatapult { get; private set; }
		public double VehicleLocationPathPrecision { get; private set; }

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return "$Id$"; } }
		public override string SubversionRevision { get { return "$Rev$"; } }
		#endregion


		#region Private and Convenience variable
		private enum StateType_MainBehavior
		{
			AssigningTask,  // Initial state
			AcquiringCatapult,
			UsingCatapultToBoardBoat,
			KillingCaptain,
			ExitingBoat,
			ExitBoatJumpDown,
		}

		public delegate WoWPoint LocationDelegate(object context);
		public delegate WoWUnit WoWUnitDelegate(object context);

		private TaskDetail CurrentTask { get; set; }
		private WoWUnit SelectedTarget { get; set; }
		private LocalPlayer Me { get { return StyxWoW.Me; } }
		private WoWUnit SelectedMachinist { get; set; }
		private WoWUnit SelectedCatapult { get; set; }
		private StateType_MainBehavior State_MainBehavior
		{
			get { return _state_MainBehavior; }
			set
			{
				// For DEBUGGING...
				//if (_state_MainBehavior != value)
				//    { QBCLog.Info("CurrentState: {0}", value); }

				_state_MainBehavior = value;
			}
		}

		private Composite _behaviorTreeHook_CombatMain = null;
		private Composite _behaviorTreeHook_CombatOnly = null;
		private Composite _behaviorTreeHook_DeathMain = null;
		private Composite _behaviorTreeHook_Main = null;
		private ConfigMemento _configMemento = null;
		private bool _isBehaviorDone = false;
		private StateType_MainBehavior _state_MainBehavior;
		#endregion


		#region Overrides of CustomForcedBehavior

		protected override Composite CreateBehavior()
		{
			return _behaviorTreeHook_Main ?? (_behaviorTreeHook_Main = CreateMainBehavior());
		}


        public override void OnFinished()
        {
            // NB: we don't unhook _behaviorTreeHook_Main
            // This was installed when HB created the behavior, and its up to HB to unhook it

            if (_behaviorTreeHook_CombatMain != null)
            {
                TreeHooks.Instance.RemoveHook("Combat_Main", _behaviorTreeHook_CombatMain);
                _behaviorTreeHook_CombatMain = null;
            }

            if (_behaviorTreeHook_CombatOnly != null)
            {
                TreeHooks.Instance.RemoveHook("Combat_Only", _behaviorTreeHook_CombatOnly);
                _behaviorTreeHook_CombatOnly = null;
            }

            if (_behaviorTreeHook_DeathMain != null)
            {
                TreeHooks.Instance.RemoveHook("Death_Main", _behaviorTreeHook_DeathMain);
                _behaviorTreeHook_DeathMain = null;
            }

            if (_configMemento != null)
            {
                _configMemento.Dispose();
                _configMemento = null;
            }

            BlackspotManager.RemoveBlackspots(Blackspots);
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
        }


		public override bool IsDone
		{
			get
			{
				// We need to move off boat after quest is complete, so we can't use "quest complete"
				// as part of the normal IsDone criteria for this behavior.  We've included a special
				// check in OnStart for starting in a "quest complete" state.
				return _isBehaviorDone;
			}
		}


		public override void OnStart()
		{
			PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

			if ((QuestId != 0) && (quest == null))
			{
				QBCLog.Error("This behavior has been associated with QuestId({0}), but the quest is not in our log", QuestId);
				IsAttributeProblem = true;
			}

			// This reports problems, and stops BT processing if there was a problem with attributes...
			// We had to defer this action, as the 'profile line number' is not available during the element's
			// constructor call.
			OnStart_HandleAttributeProblem();

			// We need to move off boat after quest is complete, so we can't use "quest complete"
			// as part of the normal IsDone criteria for this behavior.  So, we explicitly check for
			// quest complete here, and set IsDone appropriately.
			if (!UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete))
				{ _isBehaviorDone = true; }

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (!IsDone)
			{
				_configMemento = new ConfigMemento();

				// Disable any settings that may interfere with the escort --
				// When we escort, we don't want to be distracted by other things.
				// NOTE: these settings are restored to their normal values when the behavior completes
				// or the bot is stopped.
				CharacterSettings.Instance.HarvestHerbs = false;
				CharacterSettings.Instance.HarvestMinerals = false;
				CharacterSettings.Instance.LootChests = false;
				CharacterSettings.Instance.NinjaSkin = false;
				CharacterSettings.Instance.SkinMobs = false;

				BlackspotManager.AddBlackspots(Blackspots);

				State_MainBehavior = StateType_MainBehavior.AssigningTask;

				_behaviorTreeHook_CombatMain = CreateBehavior_CombatMain();
				TreeHooks.Instance.InsertHook("Combat_Main", 0, _behaviorTreeHook_CombatMain);
				_behaviorTreeHook_CombatOnly = CreateBehavior_CombatOnly();
				TreeHooks.Instance.InsertHook("Combat_Only", 0, _behaviorTreeHook_CombatOnly);
				_behaviorTreeHook_DeathMain = CreateBehavior_DeathMain();
				TreeHooks.Instance.InsertHook("Death_Main", 0, _behaviorTreeHook_DeathMain);

				this.UpdateGoalText(QuestId);
			}
		}
		#endregion


		#region Main Behavior
		private Composite CreateBehavior_CombatMain()
		{
			return new PrioritySelector(
				// empty, for now
				);
		}


		private Composite CreateBehavior_CombatOnly()
		{
			return new PrioritySelector(
				// If we're in combat while in the vehicle, then exit the vehicle and eliminate the problem...
				new Decorator(context => Query.IsInVehicle(),
					new Action(context =>
					{
						QBCLog.Info("Exiting vehicle to take care of hostile mob");
						Lua.DoString("VehicleExit()");
					}))
				);
		}


		private Composite CreateBehavior_DeathMain()
		{
			return new PrioritySelector(
				// empty, for now
				);
		}


		private Composite CreateMainBehavior()
		{
			return new PrioritySelector(

				// If a mob targets us, kill it...
				// We don't want to blindly move to destination and drag a bunch of mobs behind us...
				new Decorator(context => (SelectedTarget = FindMobTargetingMeOrPet()) != null,
					UtilityBehavior_SpankMob(context => SelectedTarget)),


				// Stateful Operation:
				new Switch<StateType_MainBehavior>(context => State_MainBehavior,
					#region State: DEFAULT
					new Action(context =>   // default case
					{
						QBCLog.Error("BEHAVIOR MAINTENANCE PROBLEM: StateType_MainBehavior({0}) is unhandled", State_MainBehavior);
						TreeRoot.Stop();
						_isBehaviorDone = true;
					}),
					#endregion


					#region State: Assigning Task
					new SwitchArgument<StateType_MainBehavior>(StateType_MainBehavior.AssigningTask,
						new PrioritySelector(
							// Captain Anson...
							new Decorator(context => !Task_CaptainAnson.IsTaskComplete(context),
								new Action(context =>
								{
									CurrentTask = Task_CaptainAnson;
									State_MainBehavior = StateType_MainBehavior.AcquiringCatapult;
								})),

							// Captain Morris...
							new Decorator(context => !Task_CaptainMorris.IsTaskComplete(context),
								new Action(context =>
								{
									CurrentTask = Task_CaptainMorris;
									State_MainBehavior = StateType_MainBehavior.AcquiringCatapult;
								})),

							// Done with all tasks, move back to sane position to continue profile...
							new Decorator(context => !Navigator.AtLocation(Location_CatapultFarm),
								new Action(context => { Navigator.MoveTo(Location_CatapultFarm); })),

							new Action(context =>
							{
								QBCLog.Info("Finished");
								_isBehaviorDone = true;
							})
						)),
					#endregion


					#region State: Acquiring Catapult
					new SwitchArgument<StateType_MainBehavior>(StateType_MainBehavior.AcquiringCatapult,
						new PrioritySelector(
							// If task complete, go get next task...
							new Decorator(context => CurrentTask.IsTaskComplete(context),
								new Action(context => { State_MainBehavior = StateType_MainBehavior.AssigningTask; })),

							// If we're in the catapult, start using it...
							new Decorator(context => Query.IsInVehicle(),
								new Action(context => { State_MainBehavior = StateType_MainBehavior.UsingCatapultToBoardBoat; })),

							// Notify user...
							new Action(context =>
							{
								QBCLog.Info("Appropriating a Catapult");
								return RunStatus.Failure;
							}),

							// If available catapult, take advantage of it...
							new Decorator(context => IsViable(SelectedCatapult)
													&& (FindPlayersNearby(SelectedCatapult.Location, NonCompeteDistanceForCatapults).Count() <= 0),
								UtilityBehavior_InteractWithMob(context => SelectedCatapult)),

							// Otherwise, spank machinist and take his catapult...
							new Decorator(context => IsViable(SelectedMachinist)
													&& (FindPlayersNearby(SelectedMachinist.Location, NonCompeteDistanceForCatapults).Count() <= 0),
								UtilityBehavior_SpankMob(context => SelectedMachinist)),

							// Find next catapult or machinist...
							// NB: Since it takes a couple of seconds for the catapult to appear after
							// we kill the machinist, we want to wait briefly.   Without this delay,
							// the toon will run off to another machinist, and come back when the Catapult
							// spawns from the machinist we just killed.  This makes us look very bottish,
							// and the delay prevents that.
							new Wait(TimeSpan.FromSeconds(3),
								context => ((SelectedCatapult = FindCatapult()) != null),
								new ActionAlwaysSucceed()),

							new Decorator(context => (SelectedMachinist = FindMachinist()) != null,
								new ActionAlwaysSucceed()),

							// No catapults to be had, move to center of catapult farm and wait for respawns...
							new Decorator(context => !Navigator.AtLocation(Location_CatapultFarm),
								new Action(context => { Navigator.MoveTo(Location_CatapultFarm); })),
							new Action(context => { QBCLog.Info("Waiting on more Catapults to respawn"); })
						)),
					#endregion


					#region State: Using Catapult to Board Boat
					new SwitchArgument<StateType_MainBehavior>(StateType_MainBehavior.UsingCatapultToBoardBoat,
						new PrioritySelector(
							// If task complete, go fetch another...
							new Decorator(context => CurrentTask.IsTaskComplete(context),
								new Action(context => { State_MainBehavior = StateType_MainBehavior.AssigningTask; })),

							// If we're no longer in catapult, either launch succeeded or we need to fetch another Catapult...
							new Decorator(context => !Query.IsInVehicle(),
								new PrioritySelector(
									// Allow time for Launch completion, and toon to land on boat...
									new Wait(TimeSpan.FromSeconds(5),
										context => Navigator.CanNavigateFully(Me.Location, CurrentTask.PositionToLand),
										new ActionAlwaysFail()),

									new Action(context =>
									{
										// If we can navigate to intended landing spot, we successfully boarded boat...
										if (Navigator.CanNavigateFully(Me.Location, CurrentTask.PositionToLand))
										{
											State_MainBehavior = StateType_MainBehavior.KillingCaptain;
											return;
										}

										// Otherwise, we missed boarding boat, and need to try again...
										QBCLog.Warning("Failed in boarding {0}'s boat--trying again", CurrentTask.MobName);
										State_MainBehavior = StateType_MainBehavior.AcquiringCatapult;
									})
								)),

							// If Catapult no longer viable, find a new one...
							new Decorator(context => !IsViable(SelectedCatapult),
								new Decorator(context => (SelectedCatapult = FindCatapult()) == null,
									new Action(context => { State_MainBehavior = StateType_MainBehavior.AcquiringCatapult; }))),

							// Try to board boat...
							UtilityBehavior_MoveAndUseCatapult()
						)),
					#endregion
					
	
					#region State: Kill the Captain
					new SwitchArgument<StateType_MainBehavior>(StateType_MainBehavior.KillingCaptain,
						new PrioritySelector(
							// If task complete, exit boat...
							new Decorator(context => CurrentTask.IsTaskComplete(context),
								new Action(context => { State_MainBehavior = StateType_MainBehavior.ExitingBoat; })),

							// Kill the Captain...
							new PrioritySelector(captainContext => FindUnitsFromIds(CurrentTask.MobId).FirstOrDefault(),
								new Decorator(captainContext => captainContext != null,
									UtilityBehavior_SpankMob(captainContext => (WoWUnit)captainContext)),
								new Decorator(captainContext => captainContext == null,
									new Action(captainContext => { QBCLog.Info("Waiting for {0} to respawn", CurrentTask.MobName); }))
								)
						)),
					#endregion


					#region State: Exiting Boat
					new SwitchArgument<StateType_MainBehavior>(StateType_MainBehavior.ExitingBoat,
						new PrioritySelector(
							new Action(context =>
							{
								QBCLog.Info("Exiting {0}'s boat", CurrentTask.MobName);
								return RunStatus.Failure;
							}),
							new Decorator(context => !Navigator.AtLocation(CurrentTask.PositionToLand),
								new Action(context => { Navigator.MoveTo(CurrentTask.PositionToLand); })),
							
							new Action(context => { State_MainBehavior = StateType_MainBehavior.ExitBoatJumpDown; })
						)),
					#endregion
						

					#region State: Exit Boat Jump Down
					new SwitchArgument<StateType_MainBehavior>(StateType_MainBehavior.ExitBoatJumpDown,
						new PrioritySelector(
							new Action(context =>
							{
								QBCLog.Info("Jumping down off of {0}'s boat", CurrentTask.MobName);
								return RunStatus.Failure;
							}),
							// NB: There appear to be no mesh "jump links" in the mesh to get off boat.
							// So, we're left with using ClickToMove to jump down from boat decks.
							new Decorator(context => !Navigator.AtLocation(CurrentTask.PositionToJumpDownOffBoat),
								new Action(context => { WoWMovement.ClickToMove(CurrentTask.PositionToJumpDownOffBoat); })),
							
							new Action(context => { State_MainBehavior = StateType_MainBehavior.AssigningTask; })
						))
					#endregion
				));
		}
		#endregion


		#region Helper Class: TaskDetail
		public class TaskDetail
		{
			public TaskDetail(string mobName, int mobId, WoWPoint positionForLaunch, WoWPoint positionToTarget,
								double neededAzimuth,
								WoWPoint positionToJumpDownOffBoat,
								CanRunDecoratorDelegate isTaskComplete)
			{
				IsTaskComplete = isTaskComplete;
				MobId = mobId;
				MobName = mobName;
				NeededAzimuth = neededAzimuth;
				PositionForLaunch = positionForLaunch;
				PositionToJumpDownOffBoat = positionToJumpDownOffBoat;
				PositionToLand = positionToTarget;
			}

			public CanRunDecoratorDelegate IsTaskComplete { get; private set; }
			public string MobName { get; private set; }
			public int MobId { get; private set; }
			public double NeededAzimuth { get; private set; }
			public WoWPoint PositionForLaunch { get; private set; }
			public WoWPoint PositionToJumpDownOffBoat { get; private set; }
			public WoWPoint PositionToLand { get; private set; }
		}
		#endregion


		#region Helpers

		private WoWUnit FindCatapult()
		{
			IEnumerable<WoWUnit> catapults =
				from unit in FindUnitsFromIds(VehicleId_ForsakenCatapult)
				where
					!unit.IsHostile
					&& (unit.Distance < 50)
					&& (FindPlayersNearby(unit.Location, NonCompeteDistanceForCatapults).Count() <= 0)
				orderby
					unit.Distance
				select unit;

			return catapults.FirstOrDefault();
		}


		private WoWUnit FindMachinist()
		{
			IEnumerable<WoWUnit> machinists =
				from unit in FindUnitsFromIds(MobId_ForsakenMachinist)
				where
					(FindPlayersNearby(unit.Location, NonCompeteDistanceForCatapults).Count() <= 0)
				orderby
					unit.Distance
				select unit;

			return machinists.FirstOrDefault();
		}


		private WoWUnit FindMobTargetingMeOrPet()
		{
			return
			   (from unit in ObjectManager.GetObjectsOfType<WoWUnit>()
				where
					IsViable(unit)
					&& !unit.IsFriendly
					&& ((unit.CurrentTarget == Me)
						|| (Me.GotAlivePet && unit.CurrentTarget == Me.Pet))
				select unit)
				.FirstOrDefault();
		}


		private IEnumerable<WoWPlayer> FindPlayersNearby(WoWPoint location, double radius)
		{
			return
				from player in ObjectManager.GetObjectsOfType<WoWPlayer>()
				where
					player.IsAlive
					&& player.Location.Distance(location) < radius
				select player;
		}


		private IEnumerable<WoWUnit> FindUnitsFromIds(params int[] unitIds)
		{
			if (unitIds == null)
			{
				string message = "BEHAVIOR MAINTENANCE ERROR: unitIds argument may not be null";

				QBCLog.Error(message);
				throw new ArgumentException(message);
			}

			return
				from unit in ObjectManager.GetObjectsOfType<WoWUnit>()
				where
					unit.IsValid
					&& unit.IsAlive
					&& unitIds.Contains((int)unit.Entry)
					&& (unit.TappedByAllThreatLists || !unit.TaggedByOther)
				select unit;
		}


		// The HB API always returns 0 for our facing when we're in a vehicle.
		// To get the actualy value, we must ask the WoWclient directly.
		private float GetVehicleFacing()
		{
			return
				Query.IsInVehicle()
				? WoWMathHelper.NormalizeRadian(Lua.GetReturnVal<float>("return GetPlayerFacing();", 0))
				: Me.RenderFacing;
		}


		private bool IsQuestObjectiveComplete(int questId, int objectiveId)
		{
			if (Me.QuestLog.GetQuestById((uint)questId) == null)
				{ return false; }

			int questLogIndex = Lua.GetReturnVal<int>(string.Format("return GetQuestLogIndexByID({0})", questId), 0);

			return
				Lua.GetReturnVal<bool>(string.Format("return GetQuestLogLeaderBoard({0},{1})", objectiveId, questLogIndex), 2);
		}


		private bool IsViable(WoWUnit wowUnit)
		{
			return
				(wowUnit != null)
				&& wowUnit.IsValid
				&& wowUnit.IsAlive
				&& !Blacklist.Contains(wowUnit, BlacklistFlags.Combat);
		}

		
		/// <summary>
		/// Returns the normalized ANGLEINRADIANS to the closed interval [-PI..+PI]
		/// </summary>
		/// <param name="radians"></param>
		/// <returns></returns>
		public double NormalizeAngleToPi(double angleInRadians)
		{
			while (angleInRadians > Math.PI)  { angleInRadians -= (2 * Math.PI); }
			while (angleInRadians < -Math.PI) { angleInRadians += (2 * Math.PI); }
			return (angleInRadians);
		}
		#endregion


		#region Utility Behaviors

		/// <summary>
		/// This behavior quits attacking the mob, once the mob is targeting us.
		/// </summary>
		private Composite UtilityBehavior_GetMobsAttention(WoWUnitDelegate selectedTargetDelegate)
		{
			return new PrioritySelector(targetContext => selectedTargetDelegate(targetContext),
				new Decorator(targetContext => IsViable((WoWUnit)targetContext),
					new PrioritySelector(
						new Decorator(targetContext => !((((WoWUnit)targetContext).CurrentTarget == Me)
														|| (Me.GotAlivePet && ((WoWUnit)targetContext).CurrentTarget == Me.Pet)),
							new PrioritySelector(
								new Action(targetContext =>
								{
                                    QBCLog.Info("Getting attention of {0}", ((WoWUnit)targetContext).SafeName);
									return RunStatus.Failure;
								}),
								UtilityBehavior_SpankMob(selectedTargetDelegate)))
					)));
		}


		private Composite UtilityBehavior_InteractWithMob(WoWUnitDelegate unitToInteract)
		{
			return new PrioritySelector(interactUnitContext => unitToInteract(interactUnitContext),
				new Decorator(interactUnitContext => IsViable((WoWUnit)interactUnitContext),
					new PrioritySelector(
						// Show user which unit we're going after...
						new Decorator(interactUnitContext => Me.CurrentTarget != (WoWUnit)interactUnitContext,
							new Action(interactUnitContext => { ((WoWUnit)interactUnitContext).Target(); })),

						// If not within interact range, move closer...
						new Decorator(interactUnitContext => !((WoWUnit)interactUnitContext).WithinInteractRange,
							new Action(interactUnitContext =>
							{
                                QBCLog.DeveloperInfo("Moving to interact with {0}", ((WoWUnit)interactUnitContext).SafeName);
								Navigator.MoveTo(((WoWUnit)interactUnitContext).Location);
							})),

						new Decorator(interactUnitContext => Me.IsMoving,
							new Action(interactUnitContext => { WoWMovement.MoveStop(); })),
						new Decorator(interactUnitContext => !Me.IsFacing((WoWUnit)interactUnitContext),
							new Action(interactUnitContext => { Me.SetFacing((WoWUnit)interactUnitContext); })),

						// Blindly interact...
						// Ideally, we would blacklist the unit if the interact failed.  However, the HB API
						// provides no CanInteract() method (or equivalent) to make this determination.
						new Action(interactUnitContext =>
						{
                            QBCLog.DeveloperInfo("Interacting with {0}", ((WoWUnit)interactUnitContext).SafeName);
							((WoWUnit)interactUnitContext).Interact();
							return RunStatus.Failure;
						}),
						new Wait(TimeSpan.FromMilliseconds(1000), context => false, new ActionAlwaysSucceed())
					)));
		}


		private Composite UtilityBehavior_MoveAndUseCatapult()
		{
			return new Decorator(context => Query.IsInVehicle() && IsViable(SelectedCatapult),
				new PrioritySelector(

					// Move vehicle into position...
					// NB: We must use ClickToMove since we're in a vehicle
					new Decorator(context => SelectedCatapult.Location.Distance2D(CurrentTask.PositionForLaunch) > Navigator.PathPrecision,
						new Action(context =>
						{
							WoWPoint destination = CurrentTask.PositionForLaunch;
							WoWPoint interimDestination = destination;

							Queue<WoWPoint> path = new Queue<WoWPoint>(Navigator.GeneratePath(SelectedCatapult.Location, destination));

							interimDestination = path.Dequeue();
							while (SelectedCatapult.Location.Distance2D(interimDestination) <= Navigator.PathPrecision)
								{ interimDestination = (path.Count() > 0) ? path.Dequeue() : destination; }

							QBCLog.Info("Moving catapult into position for {0}'s boat", CurrentTask.MobName);
							WoWMovement.ClickToMove(interimDestination);
						})),

					// Adjust heading...
					new Decorator(context => !WoWMathHelper.IsFacing(Me.Location, GetVehicleFacing(), CurrentTask.PositionToLand, (float)Math.PI/360),
						new Action(context =>
						{
							// Handle heading...
							double neededHeading = WoWMathHelper.CalculateNeededFacing(Me.Location, CurrentTask.PositionToLand);
							neededHeading = WoWMathHelper.NormalizeRadian((float)neededHeading);
							QBCLog.Info("Adjusting firing heading");
							Me.SetFacing((float)neededHeading);
						})),

					// Adjust azimuth...
					new Action(context =>
					{
						// Handle Azimuth...
						double currentAzimuth = WoWMathHelper.NormalizeRadian(Lua.GetReturnVal<float>("return VehicleAimGetAngle();", 0));
						double neededAzimuth = NormalizeAngleToPi(CurrentTask.NeededAzimuth);

						double azimuthChangeRequired = neededAzimuth - currentAzimuth;
						if (Math.Abs(azimuthChangeRequired) >= 0.01)
						{
							QBCLog.Info("Adjusting firing azimuth");
							// NB: VehicleAimIncrement() handles negative values of 'increment' correctly...
							Lua.DoString("VehicleAimIncrement({0})", azimuthChangeRequired);
							return RunStatus.Success;
						}

						return RunStatus.Failure;
					}),

					// Fire...
					new Decorator(context => Query.IsInVehicle(),
						new Sequence(
							new Action(context =>
							{
								QBCLog.Info("Firing Catapult");
								Lua.DoString(Lua_LaunchCommand);
							}),
							new WaitContinue(TimeSpan.FromSeconds(3), context => !Query.IsInVehicle(), new ActionAlwaysSucceed())
						))
				));
		}


		/// <summary>
		/// Unequivocally engages mob in combat.
		/// </summary>
		private Composite UtilityBehavior_SpankMob(WoWUnitDelegate selectedTargetDelegate)
		{
			return new PrioritySelector(targetContext => selectedTargetDelegate(targetContext),
				new Decorator(targetContext => IsViable((WoWUnit)targetContext),
					new PrioritySelector(               
						new Decorator(targetContext => ((WoWUnit)targetContext).Distance > CombatMaxEngagementRangeDistance,
							new Action(targetContext => { Navigator.MoveTo(((WoWUnit)targetContext).Location); })),
						new Decorator(targetContext => Me.CurrentTarget != (WoWUnit)targetContext,
							new Action(targetContext =>
							{
								BotPoi.Current = new BotPoi((WoWUnit)targetContext, PoiType.Kill);
								((WoWUnit)targetContext).Target();
							})),
						new Decorator(targetContext => !((WoWUnit)targetContext).IsTargetingMeOrPet,
							new PrioritySelector(
								new Decorator(targetContext => RoutineManager.Current.CombatBehavior != null,
									RoutineManager.Current.CombatBehavior),
								new Action(targetContext => { RoutineManager.Current.Combat(); })
							))
					)));
		}
		#endregion // Behavior helpers
	}


	#region WoWPoint_Extensions
	public static class WoWPoint_Extensions
	{
		public static Random _random = new Random((int)DateTime.Now.Ticks);

		private static LocalPlayer Me { get { return (StyxWoW.Me); } }
		public const double TAU = (2 * Math.PI);    // See http://tauday.com/


		public static WoWPoint Add(this WoWPoint wowPoint,
									double x,
									double y,
									double z)
		{
			return (new WoWPoint((wowPoint.X + x), (wowPoint.Y + y), (wowPoint.Z + z)));
		}


		public static WoWPoint AddPolarXY(this WoWPoint wowPoint,
										   double xyHeadingInRadians,
										   double distance,
										   double zModifier)
		{
			return (wowPoint.Add((Math.Cos(xyHeadingInRadians) * distance),
								 (Math.Sin(xyHeadingInRadians) * distance),
								 zModifier));
		}


		// Finds another point near the destination.  Useful when toon is 'waiting' for something
		// (e.g., boat, mob repops, etc). This allows multiple people running
		// the same profile to not stand on top of each other while waiting for
		// something.
		public static WoWPoint FanOutRandom(this WoWPoint location,
												double maxRadius)
		{
			const int CYLINDER_LINE_COUNT = 12;
			const int MAX_TRIES = 50;
			const double SAFE_DISTANCE_BUFFER = 1.75;

			WoWPoint candidateDestination = location;
			int tryCount;

			// Most of the time we'll find a viable spot in less than 2 tries...
			// However, if you're standing on a pier, or small platform a
			// viable alternative may take 10-15 tries--its all up to the
			// random number generator.
			for (tryCount = MAX_TRIES; tryCount > 0; --tryCount)
			{
				WoWPoint circlePoint;
				bool[] hitResults;
				WoWPoint[] hitPoints;
				int index;
				WorldLine[] traceLines = new WorldLine[CYLINDER_LINE_COUNT + 1];

				candidateDestination = location.AddPolarXY((TAU * _random.NextDouble()), (maxRadius * _random.NextDouble()), 0.0);

				// Build set of tracelines that can evaluate the candidate destination --
				// We build a cone of lines with the cone's base at the destination's 'feet',
				// and the cone's point at maxRadius over the destination's 'head'.  We also
				// include the cone 'normal' as the first entry.

				// 'Normal' vector
				index = 0;
				traceLines[index].Start = candidateDestination.Add(0.0, 0.0, maxRadius);
				traceLines[index].End = candidateDestination.Add(0.0, 0.0, -maxRadius);

				// Cylinder vectors
				for (double turnFraction = 0.0; turnFraction < TAU; turnFraction += (TAU / CYLINDER_LINE_COUNT))
				{
					++index;
					circlePoint = candidateDestination.AddPolarXY(turnFraction, SAFE_DISTANCE_BUFFER, 0.0);
					traceLines[index].Start = circlePoint.Add(0.0, 0.0, maxRadius);
					traceLines[index].End = circlePoint.Add(0.0, 0.0, -maxRadius);
				}


				// Evaluate the cylinder...
				// The result for the 'normal' vector (first one) will be the location where the
				// destination meets the ground.  Before this MassTrace, only the candidateDestination's
				// X/Y values were valid.
				GameWorld.MassTraceLine(traceLines.ToArray(),
										GameWorld.CGWorldFrameHitFlags.HitTestGroundAndStructures,
										out hitResults,
										out hitPoints);

				candidateDestination = hitPoints[0];    // From 'normal', Destination with valid Z coordinate


				// Sanity check...
				// We don't want to be standing right on the edge of a drop-off (say we'e on
				// a plaform or pier).  If there is not solid ground all around us, we reject
				// the candidate.  Our test for validity is that the walking distance must
				// not be more than 20% greater than the straight-line distance to the point.
				int viableVectorCount = hitPoints.Sum(point => ((Me.Location.SurfacePathDistance(point) < (Me.Location.Distance(point) * 1.20))
																	  ? 1
																	  : 0));

				if (viableVectorCount < (CYLINDER_LINE_COUNT + 1))
				{ continue; }

				// If new destination is 'too close' to our current position, try again...
				if (Me.Location.Distance(candidateDestination) <= SAFE_DISTANCE_BUFFER)
				{ continue; }

				break;
			}

			// If we exhausted our tries, just go with simple destination --
			if (tryCount <= 0)
			{ candidateDestination = location; }

			return (candidateDestination);
		}


		public static double SurfacePathDistance(this WoWPoint start,
													WoWPoint destination)
		{
			WoWPoint[] groundPath = Navigator.GeneratePath(start, destination) ?? new WoWPoint[0];

			// We define an invalid path to be of 'infinite' length
			if (groundPath.Length <= 0)
			{ return (double.MaxValue); }


			double pathDistance = start.Distance(groundPath[0]);

			for (int i = 0; i < (groundPath.Length - 1); ++i)
			{ pathDistance += groundPath[i].Distance(groundPath[i + 1]); }

			return (pathDistance);
		}


		// Returns WoWPoint.Empty if unable to locate water's surface
		public static WoWPoint WaterSurface(this WoWPoint location)
		{
			WoWPoint hitLocation;
			bool hitResult;
			WoWPoint locationUpper = location.Add(0.0, 0.0, 2000.0);
			WoWPoint locationLower = location.Add(0.0, 0.0, -2000.0);

			hitResult = (GameWorld.TraceLine(locationUpper,
											 locationLower,
											 GameWorld.CGWorldFrameHitFlags.HitTestLiquid,
											 out hitLocation)
						 || GameWorld.TraceLine(locationUpper,
												locationLower,
												GameWorld.CGWorldFrameHitFlags.HitTestLiquid2,
												out hitLocation));

			return (hitResult ? hitLocation : WoWPoint.Empty);
		}
	}
	#endregion
}