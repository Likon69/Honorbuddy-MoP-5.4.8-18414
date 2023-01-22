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


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.HealingofShenZinSu
{
	[CustomBehaviorFileName(@"SpecificQuests\29799-PandaStarter-HealingofShenZinSu")]
	public class HealingofShenZinSu : CustomForcedBehavior
	{

		public HealingofShenZinSu(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				QuestId = 29799;
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
				return new Decorator(ret => Me.IsQuestComplete(QuestId),
					new Action(delegate
					{
						TreeRoot.StatusText = "Finished!";
						_isBehaviorDone = true;
						return RunStatus.Success;
					}));

			}
		}

		//<Vendor Name="Alliance Priest" Entry="60877" Type="Repair" X="208.6163" Y="3913.861" Z="61.57914" />
		//<Vendor Name="Horde Druid" Entry="60770" Type="Repair" X="318.3125" Y="3896.318" Z="78.3259" />
		//16777216

		//209691 - sniper rifle
		public WoWUnit PriestsClickable
		{
			get
			{//559104
				return
					ObjectManager.GetObjectsOfType<WoWUnit>().Where(r => (r.Entry == 60877 || r.Entry == 60770) && r.Flags == 32768).OrderBy(r => r.Distance).FirstOrDefault();
			}

		}

		public WoWUnit Wreckage
		{
			get
			{//559104
				return
					ObjectManager.GetObjectsOfType<WoWUnit>().Where(r => r.Entry == 60848).OrderBy(r => r.Distance).FirstOrDefault();
			}

		}


		
		public WoWUnit InCombat
		{
			get
			{
				return
					ObjectManager.GetObjectsOfType<WoWUnit>().Where(r => (r.Entry == 60877 || r.Entry == 60770) && r.Flags == 559104).OrderBy(r=>r.Distance).FirstOrDefault();
			}

		}

	
		private Composite GetonPole
		{
			get
			{
				return new PrioritySelector(
					new Decorator(r => Me.Combat, RoutineManager.Current.CombatBehavior),

					//Sometimes wreckage would popup and make us stray, if theres a nearby healer click that first.
					new Decorator(r => PriestsClickable != null && PriestsClickable.Distance < 5,
						new Action(r=>PriestsClickable.Interact(true))),
					new Decorator(r => Wreckage != null,
						new Action(r =>
						{
							Navigator.MoveTo(Wreckage.Location);
							Wreckage.Interact();
						} )),
					new Decorator(r => PriestsClickable != null,
						new Action(r =>
						{

							Navigator.MoveTo(PriestsClickable.Location);

							if (PriestsClickable.WithinInteractRange)
							{
								PriestsClickable.Interact(true);
							}
																					
						})),
					new Decorator(r => Me.CurrentTarget == null && InCombat != null && InCombat.GotTarget,
						new Action(r =>
						{
							QBCLog.Info("Attacking nearby enemy");
							InCombat.CurrentTarget.Target();

							InCombat.CurrentTarget.Interact();
							Navigator.MoveTo(InCombat.CurrentTarget.Location);
						}
						))
					);
			}
		}


		protected Composite CreateBehavior_QuestbotMain()
		{
			return _root ?? (_root =
				new Decorator(ret => !_isBehaviorDone,
					new PrioritySelector(DoneYet, GetonPole,new ActionAlwaysSucceed())));
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
				TreeHooks.Instance.InsertHook("Questbot_Main", 0, CreateBehavior_QuestbotMain());

				this.UpdateGoalText(QuestId);
			}
		}
		#endregion
	}
}