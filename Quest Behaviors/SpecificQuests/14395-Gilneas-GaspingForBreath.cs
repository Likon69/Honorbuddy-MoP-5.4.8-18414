// Template originally contributed by Chinajade.
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
// TEMPLATE.cs is a skeleton for creating new quest behaviors.
//
// Quest binding:
//      QuestId [REQUIRED if EscortCompleteWhen=QuestComplete; Default:none]:
//      QuestCompleteRequirement [Default:NotComplete]:
//      QuestInLogRequirement [Default:InLog]:
//              A full discussion of how the Quest* attributes operate is described in
//              http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
//      QuestObjectiveIndex [REQUIRED if EventCompleteWhen=QuestObjectiveComplete]
//          [on the closed interval: [1..5]]
//          This argument is only consulted if EventCompleteWhen is QuestObjectveComplete.
//          The argument specifies the index of the sub-goal of a quest.
//
// Tunables (ideally, the profile would _never_ provide these arguments):
//      CombatMaxEngagementRangeDistance [optional; Default: 23.0]
//          This is a work around for some buggy Combat Routines.  If a targetted mob is
//          "too far away", some Combat Routines refuse to engage it for killing.  This
//          value moves the toon within an appropriate distance to the requested target
//          so the Combat Routine will perform as expected.
//
// THINGS TO KNOW:
//
// EXAMPLE:
//     <CustomBehavior File="TEMPLATE" />
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
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.GaspingForBreath
{
	[CustomBehaviorFileName(@"SpecificQuests\14395-Gilneas-GaspingForBreath")]
	public partial class GaspingForBreath : CustomForcedBehavior
	{
		#region Consructor and Argument Processing
		public GaspingForBreath(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// Quest handling...
				QuestId = 14395; // http://wowhead.com/quest=14395
				QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = QuestInLogRequirement.InLog;

				AuraId_Drowning = 68730; // http://wowhead.com/spell=68730
				AuraId_RescueDrowningWatchman = 68735; // http://wowhead.com/spell=68735
				MobId_DrowningWatchman = 36440; // http://wowhead.com/npc=36440

				// Tunables...
				CombatMaxEngagementRangeDistance = 23.0;
				NonCompeteDistance = 25.0;
				PositionToMakeLandfall = new WoWPoint(-1897.101, 2520.762, 1.735498).FanOutRandom(5.0);

				Blackspots = new List<Blackspot>()
				{
					// empty, for now
				};

				IgnoreDrowningWatchmenInTheseAreas = new List<Blackspot>()
				{
					new Blackspot(new WoWPoint(-1916.865, 2559.767, -14.21182), 5.0f, 1.0f),
					new Blackspot(new WoWPoint(-1931.899, 2562.926, -19.15102), 5.0f, 1.0f)
				};

				// A collection of paths that will get us from shore near a Watchman in the water...
				// We need these paths because there are mesh issues in this area/phase, and there is
				// much flotsam and jetsam in the water.
				// NB: Paths are directional--the first point should be on shore, and the last point
				// should be somewhere in the water.  Our algorithm will reverse the path when necessary.
				Paths = new List<List<WoWPoint>>()
				{
					// Path straight out
					new List<WoWPoint>()
					{
						new WoWPoint(-1898.565, 2526.652, 1.035018),
						new WoWPoint(-1905.135, 2532.711, -1.765509),
						new WoWPoint(-1920.694, 2534.667, -1.693366),
						new WoWPoint(-1936.677, 2536.928, -1.693366),
						new WoWPoint(-1951.854, 2541.426, -1.693366),
						new WoWPoint(-1974.511, 2548.637, -1.693366),
						new WoWPoint(-2001.689, 2551.984, -1.655865),
						new WoWPoint(-2022.086, 2549.928, -1.655865),
						new WoWPoint(-2042.27, 2546.034, -1.655865),
						new WoWPoint(-2059.579, 2542.695, -1.655865),
						new WoWPoint(-2074.083, 2539.897, -1.655865),
						new WoWPoint(-2086.656, 2535.151, -1.655865),
						new WoWPoint(-2100.172, 2529.053, -1.655865),
						new WoWPoint(-2120.214, 2520.011, -1.655865)
					},

					// Path to left
					new List<WoWPoint>()
					{
						new WoWPoint(-1917.676, 2512.804, 0.9624463),
						new WoWPoint(-1921.708, 2518.946, -1.56824),
						new WoWPoint(-1933.636, 2519.966, -1.708278),
						new WoWPoint(-1950.844, 2535.389, -1.633051),
						new WoWPoint(-1976.363, 2520.421, -1.633051),
						new WoWPoint(-1983.294, 2512.472, -1.633051),
						new WoWPoint(-1967.469, 2497.509, -1.633051)
					},

					// Path to right
					new List<WoWPoint>()
					{
						new WoWPoint(-1889.763, 2523.739, 1.452872),
						new WoWPoint(-1886.251, 2533.122, 1.710165),
						new WoWPoint(-1894.029, 2546.086, 1.341531),
						new WoWPoint(-1887.969, 2560.070, 1.207472),
						new WoWPoint(-1885.753, 2571.156, 1.337856),
						new WoWPoint(-1891.537, 2574.381, 1.792468),
						new WoWPoint(-1905.131, 2587.235, -1.833358),
						new WoWPoint(-1918.267, 2604.448, -1.675449),
						new WoWPoint(-1931.461, 2614.436, -1.675449),
						new WoWPoint(-1943.776, 2623.013, -1.675449),
						new WoWPoint(-1955.846, 2631.421, -1.675449),
						new WoWPoint(-1968.597, 2638.606, -1.675449),
						new WoWPoint(-1980.798, 2646.535, -1.675449),
						new WoWPoint(-1992.115, 2650.767, -1.675449),
						new WoWPoint(-2003.118, 2665.267, -1.675449),
						new WoWPoint(-2008.434, 2681.922, -1.675449),
					}
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

		public IEnumerable<Blackspot> IgnoreDrowningWatchmenInTheseAreas { get; private set; }
		public int AuraId_Drowning { get; private set; }
		public int AuraId_RescueDrowningWatchman { get; private set; }
		public IEnumerable<Blackspot> Blackspots { get; private set; }
		public int MobId_DrowningWatchman { get; private set; }
		public double NonCompeteDistance { get; private set; }
		public List<List<WoWPoint>> Paths { get; private set; }
		public WoWPoint PositionToMakeLandfall { get; private set; }


		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return "$Id$"; } }
		public override string SubversionRevision { get { return "$Rev$"; } }
		#endregion


		#region Private and Convenience variables
		public delegate WoWPoint LocationDelegate(object context);
		public delegate string MessageDelegate(object context);
		public delegate double RangeDelegate(object context);
		public delegate string StringDelegate(object context);
		public delegate WoWUnit WoWUnitDelegate(object context);

		
		private enum StateType_MainBehavior
		{
			DroppingOffVictim,  // Initial state
			PathingOutToVictim,
			Rescuing,
			PathingIntoShore,
		};


		private Queue<WoWPoint> CurrentPath = null;
		private readonly TimeSpan Delay_WoWClientMovementThrottle = TimeSpan.FromMilliseconds(100);
		private LocalPlayer Me { get { return StyxWoW.Me; } }
		private WoWUnit SelectedTarget { get; set; }
		private StateType_MainBehavior State_MainBehavior
		{
			get { return _state_MainBehavior; }
			set
			{
				// For DEBUGGING...
				//if (_state_MainBehavior != value)
				//    { QBCLog.Info("State_MainBehavior: {0}", value); }

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
				return _isBehaviorDone     // normal completion
						|| !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete);
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
				// CharacterSettings.Instance.PullDistance = 1;    // don't pull anything unless we absolutely must

				BlackspotManager.AddBlackspots(Blackspots);

				State_MainBehavior = StateType_MainBehavior.DroppingOffVictim;

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
				);
		}


		private Composite CreateBehavior_CombatOnly()
		{
			return new PrioritySelector(
				// If we got in Combat, force a path recalculation when done...
				new Action(context =>
				{
					CurrentPath = null;
					return RunStatus.Failure;
				})
				);
		}


		private Composite CreateBehavior_DeathMain()
		{
			return new PrioritySelector(
				);
		}


		private Composite CreateMainBehavior()
		{
			return new PrioritySelector(

				// If quest is done, behavior is done...
				new Decorator(context => IsDone,
					new Action(context =>
					{
						_isBehaviorDone = true;
						QBCLog.Info("Finished");
					})),


				// Stateful Operation:
				new Switch<StateType_MainBehavior>(context => State_MainBehavior,
					#region State: DEFAULT
					new Action(context =>   // default case
					{
						QBCLog.MaintenanceError("StateType_MainBehavior({0}) is unhandled", State_MainBehavior);
						TreeRoot.Stop();
						_isBehaviorDone = true;
					}),
					#endregion


					#region State: Dropping Off Victim
					new SwitchArgument<StateType_MainBehavior>(StateType_MainBehavior.DroppingOffVictim,
						new PrioritySelector(
							// If Watchman dropped off, go get another...
							new Decorator(context => !Me.HasAura(AuraId_RescueDrowningWatchman),
								new Action(context =>
								{
									WoWMovement.MoveStop();
									CurrentPath = null;
									SelectedTarget = null;
									State_MainBehavior = StateType_MainBehavior.PathingOutToVictim;
								})),

							// Move to drop off spot...
							new Decorator(context => Me.Location.Distance(PositionToMakeLandfall) > Navigator.PathPrecision,
							    new ActionRunCoroutine(
							        context => UtilityCoroutine.MoveTo(
							            PositionToMakeLandfall,
							            "back to shore")))
						)),
					#endregion 


					#region State: Pathing Out to Victim
					new SwitchArgument<StateType_MainBehavior>(StateType_MainBehavior.PathingOutToVictim,
						new PrioritySelector(
							// If our selected target is no good, find another...
							new Decorator(context => !IsViableDrowningWatchman(SelectedTarget),
								new Action(context =>
								{
									QBCLog.Info("Finding new Drowning Watchman to save");
									CurrentPath = null;
									SelectedTarget = FindDrowningWatchman();
								})),

							// Show user which target we're after...
							new Decorator(context => Me.CurrentTarget != SelectedTarget,
								new Action(context => { SelectedTarget.Target(); })),

							// If we don't have a path to victim, find one...
							new Decorator(context => CurrentPath == null,
								new Action(context => { CurrentPath = FindPath(Me.Location, SelectedTarget.Location); })),

							// If path completely consumed, we're done...
							new Decorator(context => CurrentPath.Count() <= 0,
								new Action(context => { State_MainBehavior = StateType_MainBehavior.Rescuing; })),

							// If we've arrived at the current waypoint, dequeue it...
							new Decorator(context => Navigator.AtLocation(CurrentPath.Peek()),
								new Action(context => { CurrentPath.Dequeue(); })),

							// Follow the prescribed path...
							new ActionRunCoroutine(
							    context => UtilityCoroutine.MoveTo(
							        CurrentPath.Peek(),
							        "out to Drowned Watcman"))
						)),
					#endregion


					#region State: Rescuing
					new SwitchArgument<StateType_MainBehavior>(StateType_MainBehavior.Rescuing,
						new PrioritySelector(
							// If we've got the Watchman, start heading in...
							new Decorator(context => Me.HasAura(AuraId_RescueDrowningWatchman),
								new Action(context =>
								{
									CurrentPath = null;
									State_MainBehavior = StateType_MainBehavior.PathingIntoShore;
								})),

							// If our selected target is no good, find another...
							new Decorator(context => !IsViableDrowningWatchman(SelectedTarget),
								new Action(context =>
								{
									CurrentPath = null;
									SelectedTarget = null;
									State_MainBehavior = StateType_MainBehavior.PathingOutToVictim;
								})),

							// Go get a fresh Drowning Watchman...
							UtilityBehavior_InteractWithMob(context => SelectedTarget)
						)),
					#endregion


					#region State: Pathing Into Shore
					new SwitchArgument<StateType_MainBehavior>(StateType_MainBehavior.PathingIntoShore,
						new PrioritySelector(
							// If we don't have a path, find the correct one...
							new Decorator(context => CurrentPath == null,
								new Action(context => { CurrentPath = FindPath(Me.Location, PositionToMakeLandfall); })),

							// If path completely consumed, we're done...
							new Decorator(context => CurrentPath.Count() <= 0,
								new Action(context => { State_MainBehavior = StateType_MainBehavior.DroppingOffVictim; })),

							// If we've lost the Watchman we rescued, go fetch another...
							new Decorator(context => !Me.HasAura(AuraId_RescueDrowningWatchman),
								new Action(context =>
								{
									CurrentPath = null;
									SelectedTarget = null;
									State_MainBehavior = StateType_MainBehavior.PathingOutToVictim;
								})),
								
							// If we've arrived at the current waypoint, dequeue it...
							new Decorator(context => Navigator.AtLocation(CurrentPath.Peek()),
								new Action(context => { CurrentPath.Dequeue(); })),

							// Follow the prescribed path...
							new ActionRunCoroutine(
							    context => UtilityCoroutine.MoveTo(
							        CurrentPath.Peek(),
							        "in to drop off Drowned Watchman"))
						))
					#endregion
				));
		}
		#endregion


		#region Helpers

		private double CalculatePathCost(WoWPoint source, WoWPoint destination, IList<WoWPoint> path)
		{
			double pathCost = 0.0;
			int pointCount = path.Count();

			for (int i = 0;  i < pointCount -1;  ++i)
				{ pathCost = path[i].Distance(path[i+1]); }

			return source.Distance(path[0]) + pathCost + destination.Distance(path[pointCount -1]);
		}


		private List<WoWPoint> ExtractPathSegment(WoWPoint source, WoWPoint destination, IList<WoWPoint> path)
		{
			List<WoWPoint> pathSegment = new List<WoWPoint>(path);

			WoWPoint pointNearestSource = pathSegment.OrderBy(p => p.Distance(source)).FirstOrDefault();
			WoWPoint pointNearestDestination = pathSegment.OrderBy(p => p.Distance(destination)).FirstOrDefault();

			if (pathSegment.IndexOf(pointNearestSource) > pathSegment.IndexOf(pointNearestDestination))
				{ pathSegment.Reverse(); }

			while (pathSegment[0] != pointNearestSource)
				{ pathSegment.RemoveAt(0); }

			while (pathSegment[pathSegment.Count() -1] != pointNearestDestination)
				{ pathSegment.RemoveAt(pathSegment.Count() -1); }

			return pathSegment;
		}


		private WoWUnit FindDrowningWatchman()
		{
			// Blacklist any watchmen in troubling area...
			IEnumerable<WoWUnit> drowningWatchmenInNeedOfBlacklisting =
				from unit in FindUnitsFromIds(MobId_DrowningWatchman)
				where
					IgnoreDrowningWatchmenInTheseAreas.Any(b => b.Location.Distance(unit.Location) <= b.Radius)
					&& !Blacklist.Contains(unit.Guid, BlacklistFlags.Combat)
				select unit;

			foreach (var drowningWatchman in drowningWatchmenInNeedOfBlacklisting)
				{ Blacklist.Add(drowningWatchman.Guid, BlacklistFlags.Combat, TimeSpan.FromMinutes(5)); }

			// Choose the nearest viable watchman...
			return
			   (from unit in FindUnitsFromIds(MobId_DrowningWatchman)
				where
					IsViableDrowningWatchman(unit)
					&& unit.HasAura(AuraId_Drowning)
				orderby
				   (from path in Paths
					let pathSegment = ExtractPathSegment(Me.Location, unit.Location, path)
					let pathCost = CalculatePathCost(Me.Location, unit.Location, pathSegment)
					orderby pathCost
					select pathCost)
					.FirstOrDefault()
				select unit)
				.FirstOrDefault();
		}


		private Queue<WoWPoint> FindPath(WoWPoint source, WoWPoint destination)
		{
			return new Queue<WoWPoint>(
			   (from path in Paths
				let pathSegment = ExtractPathSegment(source, destination, path)
				orderby CalculatePathCost(source, destination, pathSegment)
				select pathSegment)
				.FirstOrDefault()
			);
		}


		// 25Feb2013-12:50UTC chinajade
		private IEnumerable<WoWPlayer> FindPlayersNearby(WoWPoint location, double radius)
		{
			return
				from player in ObjectManager.GetObjectsOfType<WoWPlayer>()
				where
					player.IsAlive
					&& player.Location.Distance(location) < radius
				select player;
		}


		// 24Feb2013-08:11UTC chinajade
		private IEnumerable<WoWUnit> FindUnitsFromIds(params int[] unitIds)
		{
			Contract.Requires(unitIds != null, context => "unitIds argument may not be null");

			return
				from unit in ObjectManager.GetObjectsOfType<WoWUnit>()
				where
					unit.IsValid
					&& unit.IsAlive
					&& unitIds.Contains((int)unit.Entry)
					&& (unit.TappedByAllThreatLists || !unit.TaggedByOther)
				select unit;
		}


		// 24Feb2013-08:11UTC chinajade
		private bool IsViable(WoWUnit wowUnit)
		{
			return
				(wowUnit != null)
				&& wowUnit.IsValid
				&& wowUnit.IsAlive
				&& !Blacklist.Contains(wowUnit, BlacklistFlags.Combat);
		}


		private bool IsViableDrowningWatchman(WoWUnit wowUnit)
		{
			return IsViable(wowUnit)
				&& (FindPlayersNearby(wowUnit.Location, NonCompeteDistance).Count() <= 0);
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
						    new ActionRunCoroutine(
						        interactUnitContext => UtilityCoroutine.MoveTo(
						            ((WoWUnit) interactUnitContext).Location,
						            "to interact with " + ((WoWUnit) interactUnitContext).SafeName))),

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
		#endregion // Behavior helpers


		#region Pet Helpers
		// Cut-n-paste any PetControl helper methods you need, here...
		#endregion
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