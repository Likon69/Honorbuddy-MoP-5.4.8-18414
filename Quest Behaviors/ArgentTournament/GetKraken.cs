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
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Styx.Bot.Quest_Behaviors
{
[CustomBehaviorFileName(@"ArgentTournament\GetKraken")]
	public class GetKraken : CustomForcedBehavior
	{
		public GetKraken(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				QuestId = 14108;//GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null) ?? 0;
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



		WoWItem Spear()
		{
			return StyxWoW.Me.BagItems.FirstOrDefault(x => x.Entry == 46954);
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
				return new Decorator(r => Me.IsQuestComplete(QuestId),
					new Action(delegate
					{
						TreeRoot.StatusText = "Finished!";
						_isBehaviorDone = true;
						return RunStatus.Success;
					}));
			}
		}

		WoWUnit Deepcaller
		{
			get
			{
				return
					ObjectManager.GetObjectsOfType<WoWUnit>().Where(x => x.Entry == 35092 && x.IsAlive).OrderBy(u => u.Distance).FirstOrDefault();
			}
		}

		WoWUnit Kraken
		{
			get
			{
				return
					ObjectManager.GetObjectsOfType<WoWUnit>().Where(x => x.Entry == 34925 && x.IsAlive).OrderBy(u => u.Distance).FirstOrDefault();
			}
		}


		public Composite Part1
		{
			get
			{
				return new Decorator(r => !Me.IsQuestObjectiveComplete(QuestId, 2) && Deepcaller != null, new Action(r =>
																							 {
																								 Deepcaller.Target();
																								 Spear().Use();
																							 }));
			}
		}

		public Composite Part2
		{
			get
			{
				return new Decorator(r => !Me.IsQuestObjectiveComplete(QuestId, 1) && Kraken != null, new Action(r =>
																								 {
																									 Kraken.Target();
																									 Spear().Use();
																								 }));
			}
		}

		protected Composite CreateBehavior_QuestbotMain()
		{
			return _root ??
				   (_root =
					new Decorator(ret => !_isBehaviorDone,
								  new PrioritySelector(DoneYet, Part1, Part2, new ActionAlwaysSucceed())));
		}

        public override void OnFinished()
        {
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            TreeHooks.Instance.RemoveHook("Questbot_Main", CreateBehavior_QuestbotMain());
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
				this.UpdateGoalText(QuestId);

				TreeHooks.Instance.InsertHook("Questbot_Main", 0, CreateBehavior_QuestbotMain());

				_isBehaviorDone = true;
			}
		}
		#endregion
	}
}