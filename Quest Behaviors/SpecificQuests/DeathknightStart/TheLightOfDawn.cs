// Behavior originally contributed by Unknown / rework by Chinajade
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
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;

using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.DeathknightStart.TheLightOfDawn
{
	[CustomBehaviorFileName(@"SpecificQuests\DeathknightStart\TheLightOfDawn")]
	public class TheLightOfDawn : CustomForcedBehavior
	{
		public TheLightOfDawn(Dictionary<string, string> args)
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

				DistanceToFollowWarlord = 45.0;
				Location_Battlefield = new Blackspot(new WoWPoint(2266.047, -5300.083, 89.15713), 95.0f, 10.0f);
				Location_WaitForBattleToComplete = new WoWPoint(2282.805, -5207.55, 82.10373).FanOutRandom(20.0);
				Location_WaitToChatWithDarion = new WoWPoint(2431.67, -5137.021, 83.84176).FanOutRandom(20.0);
				AuraId_TheMightOfMograine = 53642;
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
		public int AuraId_TheMightOfMograine { get; private set; }
		public double DistanceToFollowWarlord { get; private set; }
		public Blackspot Location_Battlefield { get; private set; }
		public WoWPoint Location_WaitForBattleToComplete { get; private set; }
		public WoWPoint Location_WaitToChatWithDarion { get; private set; }
		public int QuestId { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }


		// Private properties
		private enum StateType_Behavior
		{
			WaitingForWarlordDarion,
			CheckingBattlefieldForWarlordDarion,
			ChattingToStartBattle,
			WaitingForBattleToStart,
			WaitingForBattleToComplete
		}

		private WoWUnit HighWarlordDarion { get; set; }
		private bool IsBattlefieldCheckedForDarion { get; set; }
		private LocalPlayer Me { get { return (StyxWoW.Me); } }
		private StateType_Behavior State_Behavior
		{
			get { return _state_Behavior; }
			set
			{
				if (_state_Behavior != value)
					{ QBCLog.DeveloperInfo("Behavior State: {0}", value); }

				_state_Behavior = value;
			}
		}


		// Private variables for internal state
		private readonly WaitTimer _afkTimer = new WaitTimer(TimeSpan.FromMinutes(2));
		private Composite _behaviorTreeHook_CombatMain = null;
		private Composite _behaviorTreeHook_CombatOnly = null;
		private Composite _behaviorTreeHook_DeathMain = null;
		private Composite _behaviorTreeHook_Main = null;
		private bool _isBehaviorDone = false;
		public static Random _random = new Random((int)DateTime.Now.Ticks);
		private StateType_Behavior _state_Behavior;

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }


		private void AntiAfk()
		{
			if (!_afkTimer.IsFinished) return;
			WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend, TimeSpan.FromMilliseconds(100));
			_afkTimer.Reset();
		}


		private static TimeSpan VariantTimeSpan(int milliSecondsMin, int milliSecondsMax)
		{
			return TimeSpan.FromMilliseconds(_random.Next(milliSecondsMin, milliSecondsMax));
		}


		#region Overrides of CustomForcedBehavior
		protected override Composite CreateBehavior()
		{
			return _behaviorTreeHook_Main ?? (_behaviorTreeHook_Main = CreateMainBehavior());
		}


        public override void OnFinished()
        {
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
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
        }


		public override bool IsDone
		{
			get
			{
				return _isBehaviorDone
						|| !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete);
			}
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
				State_Behavior = StateType_Behavior.ChattingToStartBattle;
			
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


		#region Main Behaviors
		private Composite CreateBehavior_CombatMain()
		{
			return new Decorator(context => !IsDone,
				new PrioritySelector(
					// Update information for this BT visit...
					new Action(context =>
					{
							HighWarlordDarion = ObjectManager.GetObjectsOfType<WoWUnit>(false, false).FirstOrDefault(u => u.Entry == 29173);

						if ((HighWarlordDarion != null) && (Me.CurrentTarget != HighWarlordDarion))
							{ HighWarlordDarion.Target(); }

						return RunStatus.Failure;
					}),

					new Switch<StateType_Behavior>(context => State_Behavior,
						#region State: DEFAULT
						new Action(context =>   // default case
						{
							QBCLog.MaintenanceError("State_Behavior({0}) is unhandled", State_Behavior);
							TreeRoot.Stop();
							_isBehaviorDone = true;
						}),
						#endregion


						#region State: Wait for Warlord
						new SwitchArgument<StateType_Behavior>(StateType_Behavior.WaitingForWarlordDarion,
							new PrioritySelector(
								// Move into position to await Warlord arrival...
								new Decorator(context => !Navigator.AtLocation(Location_WaitToChatWithDarion),
									new Action(context =>
									{
										TreeRoot.StatusText = "Moving into position to wait on High Warlord Darion to arrive";
										Navigator.MoveTo(Location_WaitToChatWithDarion);
									})),

								// Warlord has arrived, go have a chat..
								new Decorator(context => HighWarlordDarion != null,
									new Action(context => { State_Behavior = StateType_Behavior.ChattingToStartBattle; })),

								// If warlord is not here, go check the battlefield...
								new Decorator(context => !IsBattlefieldCheckedForDarion,
									new Action(context => { State_Behavior = StateType_Behavior.CheckingBattlefieldForWarlordDarion; })),

								new Action(context =>
								{
									TreeRoot.StatusText = "Waiting for High Warlord Darion to arrive";
									AntiAfk();
								})
							)),
						#endregion


						#region State: Check Battlefield for Warlord
						new SwitchArgument<StateType_Behavior>(StateType_Behavior.CheckingBattlefieldForWarlordDarion,
							new PrioritySelector(
								// Move into position to await Warlord arrival...
								new Decorator(context => !Navigator.AtLocation(Location_WaitForBattleToComplete),
									new Action(context =>
									{
										TreeRoot.StatusText = "Checking battlefield for High Warlord Darion";
										Navigator.MoveTo(Location_WaitForBattleToComplete);
									})),

								// If we found Warlord on battlefield, wait for battle to complete...
								new Decorator(context => HighWarlordDarion != null,
									new Action(context => { State_Behavior = StateType_Behavior.WaitingForBattleToComplete; })),

								// If warlord is not here, return to start point and wait...
								new Decorator(context => HighWarlordDarion == null,
									new Action(context =>
									{
										IsBattlefieldCheckedForDarion = true;
										State_Behavior = StateType_Behavior.WaitingForWarlordDarion;
									}))
							)),
						#endregion
							
							
						#region State: Chat with Warlord
						new SwitchArgument<StateType_Behavior>(StateType_Behavior.ChattingToStartBattle,
							new PrioritySelector(
								// If the Warlord disappeared, go find him...
								new Decorator(context => HighWarlordDarion == null,
									new Action(context =>
									{
										IsBattlefieldCheckedForDarion = false;
										State_Behavior = StateType_Behavior.WaitingForWarlordDarion;
									})),

								// Move close enough to chat with Warlord...
								new Decorator(context => !HighWarlordDarion.WithinInteractRange,
									new Action(context =>
									{
                                        TreeRoot.StatusText = "Moving to " + HighWarlordDarion.SafeName;
										Navigator.MoveTo(HighWarlordDarion.Location);
									})),

								// Chat with warlord...
								// When we interact with the warlord, he will present us with either:
								// 1) a Quest frame (with no gossip options)
								// 2) a Gossip frame (with gossip options) that we use to kick off the event
								// Both frames always *exist*, what matters is the one the Warlord presents us with.
								new Decorator(context => HighWarlordDarion.CanGossip,
									new PrioritySelector(
										new Decorator(context => !(GossipFrame.Instance.IsVisible || QuestFrame.Instance.IsVisible),
											new Action(context => { HighWarlordDarion.Interact(); })),

										// Process GossipFrame or QuestFrame, whichever was presented...
										new Sequence(
											// Simulate reading frame text...
											new WaitContinue(VariantTimeSpan(2500, 7000), context => false, new ActionAlwaysSucceed()),
											// If gossip frame showing, choose correct gossip option...
											new DecoratorContinue(context => GossipFrame.Instance.IsVisible
																				&& GossipFrame.Instance.GossipOptionEntries.Count() > 0,
												new Action(context => { GossipFrame.Instance.SelectGossipOption(0); })),
											new WaitContinue(VariantTimeSpan(1250, 3500), context => false, new ActionAlwaysSucceed()),
											// Close gossip frame if that was showing...
											new DecoratorContinue(context => GossipFrame.Instance.IsVisible,
												new Action(context => { GossipFrame.Instance.Close(); })),
											// Close quest frame if that was showing...
											new DecoratorContinue(context => QuestFrame.Instance.IsVisible,
												new Action(context => { QuestFrame.Instance.Close(); })),
											new Action(context => { State_Behavior = StateType_Behavior.WaitingForBattleToStart; })
										)
									)),

								// Warlord doesn't want to chat, wait for battle to start...
								new Decorator(context => !HighWarlordDarion.CanGossip,
									new Action(context => { State_Behavior = StateType_Behavior.WaitingForBattleToStart; }))
							)),
						#endregion
							

						#region State: Wait for Battle to start
						new SwitchArgument<StateType_Behavior>(StateType_Behavior.WaitingForBattleToStart,
							new PrioritySelector(
								// If warlord disappeared, start over...
								new Decorator(context => HighWarlordDarion == null,
									new Action(context => { State_Behavior = StateType_Behavior.WaitingForWarlordDarion; })),

								// If Warlord is already on the battlefield, time to go...
								new Decorator(context => HighWarlordDarion.Location.Distance(Location_Battlefield.Location) <= Location_Battlefield.Radius,
									new Action(context => { State_Behavior = StateType_Behavior.WaitingForBattleToComplete; })),

								// If the warlord is not on the battlefield, follow him at a reasonable distance...
								new Decorator(context => HighWarlordDarion.Distance > DistanceToFollowWarlord,
									new Action(context =>
									{
                                        TreeRoot.StatusText = string.Format("Following {0}", HighWarlordDarion.SafeName);
										Navigator.MoveTo(HighWarlordDarion.Location);
									})),

								// Move into position to await battle start...
								new Decorator(context => !HighWarlordDarion.IsMoving && !Navigator.AtLocation(Location_WaitToChatWithDarion),
									new Action(context =>
									{
										TreeRoot.StatusText = "Waiting for Battle to start";
										Navigator.MoveTo(Location_WaitToChatWithDarion);
									}))
							)),
						#endregion
							
							
						#region State: Wait for Battle to complete
						new SwitchArgument<StateType_Behavior>(StateType_Behavior.WaitingForBattleToComplete,
							new PrioritySelector(
								// If warlord disappeared, start over...
								new Decorator(context => HighWarlordDarion == null,
									new Action(context => { State_Behavior = StateType_Behavior.WaitingForWarlordDarion; })),

								// If the warlord is on the battlefield, wait at our safespot...
								new Decorator(context => HighWarlordDarion.Location.Distance(Location_Battlefield.Location) <= Location_Battlefield.Radius,
									new PrioritySelector(
										// Move to our position to wait for battle to be over...
										new Decorator(context => !Navigator.AtLocation(Location_WaitForBattleToComplete),
											new Action(context =>
											{
												TreeRoot.StatusText = "Moving to safe spot";
												Navigator.MoveTo(Location_WaitForBattleToComplete);
											})),

										// Wait for battle to be over...
										new Action(context =>
										{
											TreeRoot.StatusText = "Waiting for battle to complete.";
											AntiAfk();
										}))),

								// If the warlord has returned to his starting position, then start over...
								new Decorator(context => HighWarlordDarion.Location.Distance(Location_WaitToChatWithDarion) <= DistanceToFollowWarlord,
									new Action(context => { State_Behavior = StateType_Behavior.WaitingForWarlordDarion; }))
							))
						#endregion  
					)
				));
		}


		private Composite CreateBehavior_CombatOnly()
		{
			return new PrioritySelector(
				// We disallow combat for this behavior...
				new ActionAlwaysSucceed()
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

				// If quest is done, behavior is done...
				new Decorator(context => IsDone,
					new Action(context =>
					{
						_isBehaviorDone = true;
						QBCLog.Info("Finished");
					}))
				);
		}
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
