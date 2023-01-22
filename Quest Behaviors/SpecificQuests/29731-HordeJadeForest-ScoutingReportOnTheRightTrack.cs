// Behavior originally contributed by mastahg.
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
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.ScoutingReportOnTheRightTrack
{
	[CustomBehaviorFileName(@"SpecificQuests\29731-HordeJadeForest-ScoutingReportOnTheRightTrack")]
	public class OnTheRightTrack : CustomForcedBehavior
	{
		private bool _isBehaviorDone;
		private Composite _root;

		public OnTheRightTrack(Dictionary<string, string> args) : base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				QuestId = 29731;
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


		// Attributes provided by caller
		public int QuestId { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }

		// Private variables for internal state


		// Private properties
		private LocalPlayer Me
		{
			get { return (StyxWoW.Me); }
		}

		#region Overrides of CustomForcedBehavior

		private static readonly WoWPoint spot = new WoWPoint(853.4566, -1950.396, 61.24099);
		private static readonly WoWPoint bounce = new WoWPoint(884.9406, -1863.337, 62.95205);

		private CircularQueue<WoWPoint> _circularQueue = new CircularQueue<WoWPoint>() {spot, bounce};

		private bool _mount;

		public Composite DoneYet
		{
			get
			{
				return new Decorator(ret => Me.IsQuestComplete(QuestId) || !Query.IsInVehicle(),
					new Action(delegate
					{
						TreeRoot.StatusText = "Finished!";
						CharacterSettings.Instance.UseMount = true;
						_isBehaviorDone = true;
						return RunStatus.Success;
					}));
			}
		}

		public WoWUnit Enemy
		{
			get
			{
				return
					ObjectManager.GetObjectsOfType<WoWUnit>()
						.Where(x => x.Entry == 55550 && !x.HasAura("Smoke Bomb") && x.GotTarget && x.CurrentTarget == Me.CharmedUnit)
						.OrderBy(x => x.Distance)
						.FirstOrDefault();
			}
		}

		public Composite Move
		{
			get
			{
				return new PrioritySelector(
					new Decorator(r => _circularQueue.Peek().Distance(Me.Location) < 10, new Action(r => _circularQueue.Dequeue())),
					new Action(r => Navigator.MoveTo(_circularQueue.Peek())));
			}
		}

		public Composite Obj1
		{
			get
			{
				return new Decorator(
					r => Enemy != null,
					new Action(
						r =>

						{
							Enemy.Target();
							CastSpell("Smoke Bomb");
						}));
			}
		}

		public override bool IsDone
		{
			get
			{
				return (_isBehaviorDone // normal completion
						|| !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
			}
		}

		public void CastSpell(string action)
		{
			var spell = StyxWoW.Me.PetSpells.FirstOrDefault(p => p.ToString() == action);
			if (spell == null)
				return;

			QBCLog.Info("[Pet] Casting {0}", action);
			Lua.DoString("CastPetAction({0})", spell.ActionBarIndex + 1);
		}


		protected Composite CreateBehavior_MainCombat()
		{
			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, Obj1, Move, new ActionAlwaysSucceed())));
		}


        public override void OnFinished()
        {
            CharacterSettings.Instance.UseMount = _mount;
            TreeHooks.Instance.RemoveHook("Combat_Main", CreateBehavior_MainCombat());
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
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
				_mount = CharacterSettings.Instance.UseMount;
				CharacterSettings.Instance.UseMount = false;
				TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_MainCombat());
				Navigator.Clear();

				this.UpdateGoalText(QuestId);
			}
		}

		#endregion
	}
}