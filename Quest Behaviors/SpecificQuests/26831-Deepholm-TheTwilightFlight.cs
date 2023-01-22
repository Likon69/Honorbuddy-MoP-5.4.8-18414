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

using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.TheTwilightFlight
{
	[CustomBehaviorFileName(@"SpecificQuests\26831-Deepholm-TheTwilightFlight")]
	public class Zoltrik : CustomForcedBehavior
	{

		public Zoltrik(Dictionary<string, string> args)
			: base(args)
		{
			try
			{
				QBCLog.BehaviorLoggingContext = this;

				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				QuestId = 26831;//GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null) ?? 0;
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


		private WoWUnit zol
		{
			get { return (ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == 42918 || u.Entry == 44135)); }
		}


		public bool isMelee
		{
			get
			{
				return Me.Class == WoWClass.Rogue || Me.Class == WoWClass.DeathKnight || Me.Class == WoWClass.Paladin ||
					   Me.Class == WoWClass.Warrior ||
					   (Me.Class == WoWClass.Shaman && SpellManager.HasSpell("Lava Lash")) ||
					   (Me.Class == WoWClass.Druid && SpellManager.HasSpell("Mangle"));
			}
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

		public Composite DoDps
		{
			get
			{
				return
					 new Sequence(//new Action(r => QBCLog.Info("dps" + new Random().Next().ToString())),
					new PrioritySelector(
						new Decorator(ret => RoutineManager.Current.CombatBehavior != null, RoutineManager.Current.CombatBehavior),
						new Action(c => RoutineManager.Current.Combat())));
			}
		}

		public Composite JumpDrake
		{
			get
			{
				return new Action(delegate
				{
					var spell = Me.PetSpells.FirstOrDefault(p => p.ToString() == "Jump Drakes!");
					if (spell == null)
						return;

					QBCLog.Info("Casting Jump Drakes!");
					jumped = true;
					Lua.DoString("CastPetAction({0})", spell.ActionBarIndex + 1);
				});
			}
		}




		public Composite Scare2
		{
			get
			{
				return new Decorator(ret => zol.HealthPercent > 75, DoDps);
			}
		}



		public Composite JumporNot
		{
			get
			{
				return new PrioritySelector(
					new Decorator(ret => jumped || !isMelee, DoDps),
					new Decorator(ret => isMelee, JumpDrake)

					);
			}
		}


		public Composite TargetHim
		{
			get
			{
				return new Decorator(ret => Me.CurrentTarget != zol, new Action(ret => zol.Target()));
			}
		}

		private bool jumped = false;
		protected Composite CreateBehavior_QuestbotMain()
		{
			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet,TargetHim, Scare2, JumporNot)));
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