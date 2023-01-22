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
// 30798-BreakingTheEmporersShield is a point-solution behavior that takes care
// of moving to the Emporer and killing him.
// It prioritizes the spawned mobs when the shield is present.
// 
// EXAMPLE:
//     <CustomBehavior File="30798-BreakingTheEmporersShield" />
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.BreakingTheEmperorsShield
{
	[CustomBehaviorFileName(@"SpecificQuests\30798-KunLai-BreakingTheEmperorsShield")]
	public class BreakingTheEmperorsShield : CustomForcedBehavior
	{
		public delegate WoWPoint LocationDelegate(object context);
		public delegate string MessageDelegate(object context);
		public delegate double RangeDelegate(object context);

		#region Consructor and Argument Processing
		public BreakingTheEmperorsShield(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				AvoidTargetsWithAura = new int[] { 118596 /*Protection of Zian*/ };
				QuestId = 30798;
				StartLocation = new WoWPoint(3463.548, 1527.291, 814.9634);
				TargetIds = new int[] { 60572 /*Nakk'rakas*/ };
				QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = QuestInLogRequirement.InLog;
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
		private int[] AvoidTargetsWithAura { get; set; }
		private WoWPoint StartLocation { get; set; }
		private int[] TargetIds { get; set; }

		private int QuestId { get; set; }
		private QuestCompleteRequirement QuestRequirementComplete { get; set; }
		private QuestInLogRequirement QuestRequirementInLog { get; set; }

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return "$Id$"; } }
		public override string SubversionRevision { get { return "$Rev$"; } }
		#endregion


		#region Private and Convenience variables

		private readonly TimeSpan Delay_WoWClientMovementThrottle = TimeSpan.FromMilliseconds(100);
		private LocalPlayer Me { get { return StyxWoW.Me; } }
		private IEnumerable<WoWUnit> MeAsGroup  = new List<WoWUnit>() { StyxWoW.Me };

		private Composite _behaviorTreeCombatHook = null;
		private Composite _behaviorTreeMainRoot = null;
		private ConfigMemento _configMemento = null;
		private bool _isBehaviorDone = false;
		private WoWUnit _targetPoiUnit = null;
		#endregion


		#region Overrides of CustomForcedBehavior

		protected override Composite CreateBehavior()
		{
			return _behaviorTreeMainRoot ?? (_behaviorTreeMainRoot = CreateMainBehavior());
		}


        public override void OnFinished()
        {
            if (_behaviorTreeCombatHook != null)
                TreeHooks.Instance.RemoveHook("Combat_Only", _behaviorTreeCombatHook);

            if (_configMemento != null)
            {
                _configMemento.Dispose();
                _configMemento = null;
            }

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
				CharacterSettings.Instance.PullDistance = 25;
				GlobalSettings.Instance.KillBetweenHotspots = true;

				_behaviorTreeCombatHook = CreateCombatBehavior();
				TreeHooks.Instance.InsertHook("Combat_Only", 0, _behaviorTreeCombatHook);

				this.UpdateGoalText(QuestId);
			}
		}
		#endregion


		#region Main Behavior
		protected Composite CreateCombatBehavior()
		{
			// NB: This behavior is hooked in at a 'higher priority' than Combat_Only.  We need this
			// for proper target selection.

			return new Decorator(context => Me.Combat && HasAuraToBeAvoided(Me.CurrentTarget),
					new Action(context =>
					{
						TreeRoot.StatusText = "NEW TARGET";
						ChooseBestTarget();
						return RunStatus.Failure;
					}));
		}


		protected Composite CreateMainBehavior()
		{
			// Move to destination...
			return new PrioritySelector(
				UtilityBehavior_MoveWithinRange(preferredUnitsContext => StartLocation,
												preferredUnitsContext => "start location")
				);
		}


		// Get the weakest mob attacking our weakest escorted unit...
		private void ChooseBestTarget()
		{
			// If we're targetting unit with aura to be avoided, find another target...
			if (HasAuraToBeAvoided(Me.CurrentTarget))
			{
				// Since an aura can go up at any time, we need to constantly evaluate it <sigh>...
				_targetPoiUnit =
					ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
					.Where(u => u.IsValid && u.IsHostile && u.Aggro)
					.OrderBy(u => 
						(HasAuraToBeAvoided(u) ? 1000 : 1)  // favor targets without aura
						* u.Distance                        // favor nearby units
						* (u.Elite ? 100 : 1))              // prefer non-elite mobs
					.FirstOrDefault();
			}

			// If target has strayed, reset to what we want...
			if ((_targetPoiUnit != null) && (Me.CurrentTarget != _targetPoiUnit))
			{
                Utility_NotifyUser("Selecting new target: {0}", _targetPoiUnit.SafeName);
				BotPoi.Current = new BotPoi(_targetPoiUnit, PoiType.Kill);
				_targetPoiUnit.Target();
			}
		}


		private bool HasAuraToBeAvoided(WoWUnit wowUnit)
		{
			return (wowUnit == null) ? false : wowUnit.ActiveAuras.Values.Any(a => AvoidTargetsWithAura.Contains(a.SpellId));
		}


		private IEnumerable<WoWUnit> FindUnitsFromIds(IEnumerable<int> unitIds)
		{
			return ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
				.Where(u => unitIds.Contains((int)u.Entry) && u.IsValid && u.IsAlive)
				.ToList();
		}


		// returns true, if any member of GROUP (or their pets) is in combat
		private bool IsInCombat(IEnumerable<WoWUnit> group)
		{
			return group.Any(u => u.Combat || ((u.Pet != null) && u.Pet.Combat));
		}


		private bool IsViableTarget(WoWUnit wowUnit)
		{
			return ((wowUnit != null) && wowUnit.IsValid && wowUnit.IsHostile && !wowUnit.IsDead);
		}

		// Returns: RunStatus.Success while movement is in progress; othwerise, RunStatus.Failure if no movement necessary
		private Composite UtilityBehavior_MoveWithinRange(LocationDelegate locationDelegate,
															MessageDelegate locationNameDelegate)
		{
			return new Sequence(
				// Done, if we're already at destination...
				new DecoratorContinue(context => Navigator.AtLocation(locationDelegate(context)),
					new Decorator(context => Me.IsMoving,   // This decorator failing indicates the behavior is complete
						new Action(delegate { WoWMovement.MoveStop(); }))),

				// Notify user of progress...
				new CompositeThrottle(TimeSpan.FromSeconds(1),
					new Action(context =>
					{
						double destinationDistance = Me.Location.Distance(locationDelegate(context));
						string locationName = locationNameDelegate(context) ?? locationDelegate(context).ToString();
						Utility_NotifyUser(string.Format("Moving to {0} (distance: {1:F1})", locationName, destinationDistance));
					})),

				new Action(context =>
				{
					WoWPoint destination = locationDelegate(context);

					// Try to use Navigator to get there...
					MoveResult moveResult = Navigator.MoveTo(destination);

					// If Navigator fails, fall back to click-to-move...
					if ((moveResult == MoveResult.Failed) || (moveResult == MoveResult.PathGenerationFailed))
						{ WoWMovement.ClickToMove(destination); }

					return RunStatus.Success; // fall through
				}),

				new WaitContinue(Delay_WoWClientMovementThrottle, ret => false, new ActionAlwaysSucceed())
				);
		}


		private void Utility_NotifyUser(string format, params object[] args)
		{
			if (format != null)
			{
				string message = string.Format(format, args);

				if (TreeRoot.StatusText != message)
					{ TreeRoot.StatusText = message; }
			}
		}
		#endregion // Behavior helpers


		#region TreeSharp Extensions
		public class CompositeThrottle : DecoratorContinue
		{
			public CompositeThrottle(TimeSpan throttleTime,
									 Composite composite)
				: base(composite)
			{
				_throttle = new Stopwatch();
				_throttleTime = throttleTime;
			}


			protected override bool CanRun(object context)
			{
				if (_throttle.IsRunning && (_throttle.Elapsed < _throttleTime))
					{ return false; }

				_throttle.Restart();
				return true;
			}

			private readonly Stopwatch _throttle;
			private readonly TimeSpan _throttleTime;
		}
		#endregion
	}
}

