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
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.DroppingTheHammer
{
	[CustomBehaviorFileName(@"SpecificQuests\27817-AllyTwilightHighlands-DroppingTheHammer")]
	public class DroppingTheHammer : CustomForcedBehavior
	{

		public DroppingTheHammer(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				QuestId = 27817;
				QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = QuestInLogRequirement.InLog;
				MobIds = new uint[] { 50635, 50638, 50643, 50636 };
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
		public uint[] MobIds { get; private set; }
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
				return new Decorator(ret => Me.IsQuestComplete(QuestId),
					new Action(delegate
					{
						TreeRoot.StatusText = "Finished!";
						_isBehaviorDone = true;
						return RunStatus.Success;
					}));
			}
		}

		public WoWUnit Normal
		{
			get { return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == 47199 && u.IsAlive).OrderBy(u => u.Distance).FirstOrDefault(); }
		}

		public WoWUnit Boss
		{
			get { return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == 46839 && u.IsAlive).OrderBy(u => u.Distance).FirstOrDefault(); }
		}

		 public WoWUnit Mount
		{
			get { return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == 47315 && u.IsAlive && u.CharmedByUnit == Me).OrderBy(u => u.Distance).FirstOrDefault(); }
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


		public Composite PartOne
		{
			get
			{
				return new Decorator(r=>!Me.IsQuestObjectiveComplete(QuestId, 1),
					new PrioritySelector(
						new Decorator(r => Me.CurrentTarget == null || Me.CurrentTarget.Distance > 55,
							new Action(r=>Normal.Target())),
						new Decorator(r => Me.CurrentTarget != null && Me.CurrentTarget.Distance <= 55,
							new Action(r=>Hammer()))));
			}
		}

		public Composite PartTwo
		{
			get
			{
				return new Decorator(r => !Me.IsQuestObjectiveComplete(QuestId, 2),
					new PrioritySelector(
					new Decorator(r => Me.IsOnTransport && Me.CurrentTarget == null || Me.CurrentTarget.Distance > 55,
						new Action(r => Boss.Target())),
					new Decorator(r => Me.IsOnTransport && Me.CurrentTarget != null && Me.CurrentTarget.Distance <= 55,
						new Action(r => Hammer())),
					new Decorator(r => !Me.IsOnTransport && Me.CurrentTarget != null && (Me.CurrentTarget.IsCasting) && Me.CurrentTarget.CastingSpellId == 88207 && Me.CurrentTarget.Distance < 10,
						new Action(r =>
						{
							var moveTo = WoWMathHelper.CalculatePointFrom(StyxWoW.Me.Location, StyxWoW.Me.CurrentTarget.Location, 10f);

							if (Navigator.CanNavigateFully(StyxWoW.Me.Location, moveTo))
							{
								Navigator.MoveTo(moveTo);
								return RunStatus.Success;
							}

							return RunStatus.Failure;
						})),
						new Decorator(r => !Me.IsOnTransport && Me.Combat,
							DoDps)));
			}
		}

		public void Hammer()
		{
			Lua.DoString("CastPetAction(1);");
		}

		public void Shield()
		{
			Lua.DoString("CastPetAction(2);");
		}

		protected Composite CreateBehavior_QuestbotMain()
		{
			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new Sequence(new DecoratorContinue(r=> Mount != null  && (Mount.GetAllAuras().FirstOrDefault(x=>x.SpellId == 88043 || x.SpellId ==88189) != null),new Action(r=>Shield())),new PrioritySelector(DoneYet, PartOne, PartTwo, new ActionAlwaysSucceed()))));
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