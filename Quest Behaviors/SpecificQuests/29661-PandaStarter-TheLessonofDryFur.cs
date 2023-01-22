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
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.TheLessonofDryFur
{
	[CustomBehaviorFileName(@"SpecificQuests\29661-PandaStarter-TheLessonofDryFur")]
	public class TheLessonofDryFur : CustomForcedBehavior
	{
		public TheLessonofDryFur(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				QuestId = 29661;
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


		//<Vendor Name="Pearlfin Poolwatcher" Entry="55709" Type="Repair" X="-100.9809" Y="-2631.66" Z="2.150823" />
		//<Vendor Name="Pearlfin Poolwatcher" Entry="55711" Type="Repair" X="-130.8297" Y="-2636.422" Z="1.639656" />

		//209691 - sniper rifle
		public WoWGameObject Bell
		{
			get
			{
				return
					ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(r=>r.Entry == 209608);
			}
		}

	  
		public IEnumerable<WoWUnit> Poles
		{
			get
			{
				return
					ObjectManager.GetObjectsOfType<WoWUnit>().Where(r =>r.NpcFlags == 16777216);
			}

		}


		WoWPoint spot = new WoWPoint(966.1218,3284.928,126.7932);

	
		private Composite GetonPole
		{
			get
			{
				return new PrioritySelector(
					new Decorator(r => Bell.Distance > 10 && !Query.IsInVehicle() && Me.Location.Distance(spot) > 10,
						new Action(r=>Navigator.MoveTo(spot))),
					new Decorator(r => Bell.Distance > 10 && !Query.IsInVehicle(), 
						new Action(r => Poles.OrderBy(z => z.Distance).FirstOrDefault().Interact(true)))        
					);
			}
		}


		private Composite PoleCombat
		{
			get
			{
				return new PrioritySelector(
					new Decorator(r => Bell.Distance > 10, new Action(delegate
																		 {

																			 var Pole =
																				 Poles.Where(r => r.WithinInteractRange)
																					 .
																					 OrderBy(
																						 r =>
																						 r.Location.Distance2D(Bell.Location)).
																					 FirstOrDefault();
																			 Pole.Interact(true);


																		 })),
					new Decorator(r => Bell.Distance <= 5, new Action(delegate
																		  {
																			  Bell.Interact();
																		  }
															   )));
			}
		}


		protected Composite CreateBehavior_QuestbotMain()
		{
			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, GetonPole,PoleCombat,new ActionAlwaysSucceed())));
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