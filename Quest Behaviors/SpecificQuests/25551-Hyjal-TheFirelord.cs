// Behavior originally contributed by mastahg
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
using System.Threading.Tasks;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
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


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.TheFirelord
{
	[CustomBehaviorFileName(@"SpecificQuests\25551-Hyjal-TheFirelord")]
	public class Rag : CustomForcedBehavior
	{
		/// <summary>
		/// This is only used when you get a quest that Says, Kill anything x times. Or on the chance the wowhead ID is wrong
		/// ##Syntax##
		/// QuestId: Id of the quest.
		/// MobId, MobId2, ...MobIdN: Mob Values that it will kill.
		/// X,Y,Z: The general location where theese objects can be found
		/// </summary>
		/// 
		public Rag(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				Location = GetAttributeAsNullable<WoWPoint>("", false, ConstrainAs.WoWPointNonEmpty, null) ??WoWPoint.Empty;
				//MobIds = GetNumberedAttributesAsArray<int>("MobId", 1, ConstrainAs.MobId, new[] {"NpcID"})
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
		public WoWPoint Location { get; private set; }
		public int QuestId = 25551;// 26581;//25551;
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

		//<Vendor Name="Malfurion Stormrage" Entry="41632" Type="Repair" X="3993.279" Y="-3036.587" Z="575.3904" /> Alliance
		//<Vendor Name="Malfurion Stormrage" Entry="40804" Type="Repair" X="3941.458" Y="-2825.699" Z="618.7477" /> Horde
		private WoWUnit Malfurion

		{
			get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(r => r.Entry == 41632 || r.Entry == 40804); }
		}


		//<Vendor Name="Cenarius" Entry="41631" Type="Repair" X="3954.34" Y="-2826.02" Z="618.7476" /> Alliance
		//<Vendor Name="Cenarius" Entry="40803" Type="Repair" X="3954.5" Y="-2825.82" Z="618.7477" /> Horde
		private WoWUnit Cenarius

		{
			get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(r => r.Entry == 41631 || r.Entry == 40803); }
		}

		private WoWUnit Add
		{
			get
			{//40794 40803 31146
				return
					ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == 40794 && !u.IsDead).OrderBy(
						u => u.Distance).FirstOrDefault();
			}
		}


		//<Vendor Name="Ragnaros" Entry="40793" Type="Repair" X="4027.45" Y="-3054.09" Z="569.141" /> Horde
		private WoWUnit Ragnaros
		{
			get { return (ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == 41634 || u.Entry ==  40793)); }
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
				return new PrioritySelector(RoutineManager.Current.CombatBuffBehavior,
											RoutineManager.Current.CombatBehavior);
			}
		}


		public Composite Story
		{
			get
			{
				return new Decorator(r => (Malfurion == null) && (Cenarius != null),
					new ActionRunCoroutine(ctx => ListenToStory()));
			}
		}

	    private async Task ListenToStory()
	    {
            WoWMovement.MoveStop();
            Cenarius.Interact();
            await Coroutine.Sleep(400);
            Lua.DoString("SelectGossipOption(1,\"gossip\", true)");
	    }

		public Composite KillAdds
		{
			get
			{
				return new Decorator(r=> Add != null,
					new PrioritySelector(
						new Decorator(r=> !Me.GotTarget || (Me.CurrentTarget != Add),
							new Action(r=>Add.Target())),
						DoDps
					));
			}
		}


		public Composite KillBoss
		{
			get
			{
				return new Decorator(r => (Ragnaros != null) && (Add == null),
					new PrioritySelector( // Sanity check for Add so it doesn't go for raggy.
						new Decorator(r => !Me.GotTarget || (Me.CurrentTarget != Ragnaros),
							new Action(r => Ragnaros.Target())),
						DoDps
					));
			}
		}


		protected Composite CreateBehavior_QuestbotMain()
		{
			return _root ?? (_root = 
				new Decorator(ret => !_isBehaviorDone,
					new PrioritySelector(
						DoneYet,
						Story,
						KillAdds,
						KillBoss
					)));
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