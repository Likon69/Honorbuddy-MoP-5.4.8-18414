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
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.TheMarinersRevenge
{
	[CustomBehaviorFileName(@"SpecificQuests\31190-DreadWastes-TheMarinersRevenge")]
	public class squid : CustomForcedBehavior
	{
		public squid(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				QuestId = 31190;
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
						CharacterSettings.Instance.UseMount = true;
						_isBehaviorDone = true;
						return RunStatus.Success;
					}));
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


		WoWUnit enemy(int stage)
		{
			return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(r => r.Entry == parts[stage]); 
		}

		uint[] parts = new uint[] { 0, 64222, 64228, 64229, 64230 };

		//bow = front - 1 - 64222
		//port = right - 2 - 64228
		//Starboard = left -3 - 64229
		//stern = back - 4 - 64230
		public Composite Obj1
		{
			get
			{
				var x = 1;
				return new Decorator(r => enemy(x) != null && !Me.IsQuestObjectiveComplete(QuestId, x),
					new Action(r =>
					{
						QBCLog.Info("bow");
						WoWMovement.ClickToMove(enemy(x).Location);
						CastSpell("Harpoon Cannon");
					}
				));
			}
		}


		private void adjustangle()
		{
			var angle = 0.22;
			var CurentAngle = Lua.GetReturnVal<double>("return VehicleAimGetAngle()", 0);
			if (CurentAngle < angle)
			{
				Lua.DoString(string.Format("VehicleAimIncrement(\"{0}\")", (angle - CurentAngle)));
			}
			if (CurentAngle > angle)
			{
				Lua.DoString(string.Format("VehicleAimDecrement(\"{0}\")", (CurentAngle - angle)));
			}
		}

		public Composite Obj2
		{
			get
			{
				var x = 2;
				return new Decorator(r => enemy(x) != null && !Me.IsQuestObjectiveComplete(QuestId, x),
					new Action(r =>
					{
						adjustangle();
						QBCLog.Info("port");
						WoWMovement.ClickToMove(enemy(x).Location);
						CastSpell("Harpoon Cannon");
					}
				));
			}
		}

		public Composite Obj3
		{
			get
			{
				var x = 3;
				return new Decorator(r => enemy(x) != null && !Me.IsQuestObjectiveComplete(QuestId, x),
					new Action(r =>
				   {
					   adjustangle();
					   QBCLog.Info("Starboard");
					   WoWMovement.ClickToMove(enemy(x).Location);
					   CastSpell("Harpoon Cannon");
				   }
				));
			}
		}

		public Composite Obj4
		{
			get
			{
				var x = 4;
				return new Decorator(r => enemy(x) != null && !Me.IsQuestObjectiveComplete(QuestId, x),
					new Action(r =>
					{
						adjustangle();
						QBCLog.Info("stern");
						WoWMovement.ClickToMove(enemy(x).Location);
						CastSpell("Harpoon Cannon");
					}
				));
			}
		}

		protected Composite CreateBehavior_QuestbotMain()
		{
			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone,
				new PrioritySelector(DoneYet, Obj1, Obj2, Obj3, Obj4, new ActionAlwaysSucceed())));
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