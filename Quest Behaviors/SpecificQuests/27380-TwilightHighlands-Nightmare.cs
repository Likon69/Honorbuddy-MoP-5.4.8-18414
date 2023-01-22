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
using Styx.CommonBot.Routines;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.Nightmare
{
	[CustomBehaviorFileName(@"SpecificQuests\27380-TwilightHighlands-Nightmare")]
	public class Nightmare : CustomForcedBehavior
	{
		public Nightmare(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				QuestId = 27380;
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
		private bool _isBehaviorDone;
		private Composite _root;


		// Private properties
		private LocalPlayer Me
		{
			get { return (StyxWoW.Me); }
		}

		#region Overrides of CustomForcedBehavior




		public Composite DoneYet
		{
			get
			{
				return
					new Decorator(ret => Me.IsQuestComplete(QuestId),
						new Action(delegate
						{
							TreeRoot.StatusText = "Finished!";
							_isBehaviorDone = true;
							return RunStatus.Success;
						}));
			}
		}


		public WoWUnit Blobs
		{
			get
			{
				return
					ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == 48816 && u.IsAlive && (u.CurrentTarget == Me || (u.CurrentTarget != null && u.CurrentTarget.ControllingPlayer == Me))).OrderBy(
						u => u.Distance).FirstOrDefault();
			}
		}

		private readonly WoWPoint spot = new WoWPoint(-2739.832,-5000.674,-127.1305);

		public WoWUnit Brain
		{
			get
			{
				return
					ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == 47960 && u.IsAlive).OrderBy(
						u => u.Distance).FirstOrDefault();
			}
		}


		public Composite LowMeter
		{
			get
			{
				return  new Decorator(r => Me.CurrentHealth < 50 || power > 200,MoveBack);
			}
		}

		public Composite DoDps
		{
			get
			{
				return
					new PrioritySelector(
						new Decorator(ret => RoutineManager.Current.CombatBehavior != null, RoutineManager.Current.CombatBehavior),
						new Action(c => RoutineManager.Current.Combat()));
			}
		}

		public Composite MoveBack
		{
			get
			{
				return new Decorator(r => Me.Location.Distance(spot) > 5, new Action(r=> Navigator.MoveTo(spot)));
			}
		}

		public Composite KillBlobs
		{
			get
			{
				return new Decorator(r => Blobs != null, new Sequence(new Action(z => Blobs.Target()), DoDps));
			}
		}

		public Composite KillBrain
		{
			get
			{
				return new Decorator(r => Brain != null, new Sequence(new Action(z => Brain.Target()), DoDps));
			}
		}

		public Composite EmptyMeter
		{
			get
			{
				return new Decorator(r => Me.Location.Distance(spot) <= 5 && power > 0, new ActionAlwaysSucceed());
			}
		}


		protected Composite CreateBehavior_QuestbotMain()
		{
			//return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(ShootArrows,Lazor, BunchUp, new ActionAlwaysSucceed())));
			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet,EmptyMeter,LowMeter,KillBlobs,KillBrain)));
		}


        public override void OnFinished()
        {
            TreeHooks.Instance.RemoveHook("Questbot_Main", CreateBehavior_QuestbotMain());
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
        }


		public override bool IsDone
		{
			get
			{
				return (_isBehaviorDone // normal completion
						|| !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
			}
		}


		public int power
		{
			get
			{
				return Lua.GetReturnVal<int>("return UnitPower(\"player\",ALTERNATE_POWER_INDEX)", 0);     }
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
				TreeHooks.Instance.InsertHook("Questbot_Main", 0, CreateBehavior_QuestbotMain());

				this.UpdateGoalText(QuestId);
			}
		}

		#endregion
	}
}