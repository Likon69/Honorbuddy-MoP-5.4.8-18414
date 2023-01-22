// Behavior created by Hawker largely based on code originally contributed by Natfoth and Raphus.
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
// Not much to say - very specific qb for a very unusual quest
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
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.ALessonInBravery
{
	[CustomBehaviorFileName(@"SpecificQuests\31061-TownlongSteppes-RidingTheStorm")]
	public class RidingTheStorm : CustomForcedBehavior
	{
		public RidingTheStorm(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				QuestId = GetAttributeAsNullable("QuestId", false, ConstrainAs.QuestId(this), null) ?? 31061;
				QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;
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
		private int QuestId { get; set; }
		private QuestCompleteRequirement QuestRequirementComplete { get; set; }
		private QuestInLogRequirement QuestRequirementInLog { get; set; }


		// Private variables for internal state
		private bool _isBehaviorDone;
		private Composite _root;

		// Private properties
		private LocalPlayer Me { get { return (StyxWoW.Me); } }


		#region Overrides of CustomForcedBehavior
		public Composite DoneYet
		{
			get
			{
				return new Decorator(ret => Me.IsQuestComplete(QuestId),
					new Action(delegate
					{
						TreeRoot.StatusText = "Finished!";
						_isBehaviorDone = true;
						return RunStatus.Success;
					}));
			}
		}


		public WoWItem BronzeClaw
		{
			get { return Me.BagItems.FirstOrDefault(r => r.Entry == 83134); }
		}

		private IEnumerable<int> dragonIds = new[] { 62584, 62585, 62586, 62311 };

		private readonly WoWPoint StartingSpot = new WoWPoint(3888.184, 5373.759, 311.3385);

		public WoWUnit ShanzeCloudrunner
		{
			get
			{
				if (!Query.IsViable(_shanzeCloudrunner) ||
					(_shanzeCloudrunner != null && _shanzeCloudrunner.IsFriendly))
				{
					_shanzeCloudrunner =
						(from wowObject in Query.FindMobsAndFactions(dragonIds)
						 let wowUnit = wowObject as WoWUnit
						 where
							 Query.IsViable(wowUnit)
							 && wowUnit.IsAlive
							 // Eliminate Dragons that that have come to their senses
							 && !wowUnit.IsFriendly
							 && wowUnit.DistanceSqr < 90 * 90
							 && !wowUnit.HasAura(122157)
						 orderby wowUnit.Distance
						 select wowUnit)
						.FirstOrDefault();
				}

				if (!Query.IsViable(_shanzeCloudrunner))
				{
					TreeRoot.StatusText = "Ejecting from vehicle";
					Lua.DoString("VehicleExit()");
				}

				return _shanzeCloudrunner;
			}
		}
		private WoWUnit _shanzeCloudrunner;

		private static bool SelectNewDragon
		{
			get
			{
				if (!StyxWoW.Me.GotTarget)
				{
					// Logging.Write("Select something!");
					return true;
				}
				if (StyxWoW.Me.GotTarget && StyxWoW.Me.CurrentTarget.IsFriendly)
				{
					Logging.Write("Seeking a hostile dragon.");
					return true;
				}

				if (StyxWoW.Me.GotTarget && !StyxWoW.Me.CurrentTarget.HasAura(122157)) // Steel Claws debuff
				{
					Logging.Write("Seeking a dragon to use Bronze Claws on.");
					return true;
				}

				if (!Query.IsInVehicle())
				{
					Logging.Write("Seeking a dragon.");
					return true;
				}

				return false;
			}
		}


		public Composite GetOnDragon
		{
			get
			{
				return
					new Decorator(ret => SelectNewDragon,
						new PrioritySelector(
							new Decorator(r => Query.IsViable(ShanzeCloudrunner),
								new Action(r =>
								{
									Utility.Target(ShanzeCloudrunner);
									Logging.Write("Using Bronze Claws.");
									TreeRoot.StatusText = "Using Bronze Claws.";
									BronzeClaw.Use();
								})),
								 new Sleep(3000)
							));
			}
		}


		public Composite KillDragon
		{
			get
			{
				return new Decorator(ret => !SelectNewDragon,
					new PrioritySelector(
					// Get off when dragon sees sense...
					/* new Decorator(context => StyxWoW.Me.GotTarget && (StyxWoW.Me.CurrentTarget.IsFriendly || ,
					 new Sequence(
					 new Action(ctx => Me.ClearTarget()),
					 new ActionAlwaysSucceed())),
					
					 new Sequence(
							
						 // Small variant delay to prevent looking like a bot...
						 new WaitContinue(
							 Delay.BeforeButtonClick,
							 context => false,
							 new ActionAlwaysSucceed()),
						 new Action(delegate
					 {
						 TreeRoot.StatusText = "Ejecting from vehicle";
						 Lua.DoString("VehicleExit()");
					 })
					 )), */

						// Make certain Dragon stays targeted...
						new ActionFail(context => Utility.Target(ShanzeCloudrunner, false, PoiType.Kill)),

						// Spank Dragon (use backup MiniCombatRoutine if main CR doesn't attack in vehicles...
						new ActionFail(context => TreeRoot.StatusText = "Fighting the dragon."),
                        new ActionRunCoroutine(context => UtilityCoroutine.MiniCombatRoutine())
					));
			}
		}

		protected Composite CreateBehavior_MainCombat()
		{
			return _root ?? (_root = new Decorator(ret => !IsDone, new PrioritySelector(DoneYet, MoveToStart(), GetOnDragon, KillDragon, new ActionAlwaysSucceed())));
		}


		public override bool IsDone
		{
			get
			{
				return (_isBehaviorDone     // normal completion
						|| !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
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
				TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_MainCombat());

				this.UpdateGoalText(QuestId);
			}
		}

		public override void OnFinished()
		{
			// get off the dragon
			if (Query.IsInVehicle())
			{ Utility.ExitVehicle(); }

            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            TreeHooks.Instance.RemoveHook("Combat_Main", CreateBehavior_MainCombat());
		}

		private Composite MoveToStart()
		{
			return new Decorator(r => !Query.IsInVehicle() && Me.Location.Distance(StartingSpot) > 10,
					new Action(r => Flightor.MoveTo(StartingSpot)));
		}
		#endregion
	}
}
