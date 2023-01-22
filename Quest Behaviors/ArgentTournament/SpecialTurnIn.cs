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

using Bots.Quest;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Database;
using Styx.CommonBot.Frames;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Styx.Bot.Quest_Behaviors
{
[CustomBehaviorFileName(@"ArgentTournament\SpecialTurnIn")]
	public class SpecialTurnIn : CustomForcedBehavior
	{
    	public SpecialTurnIn(Dictionary<string, string> args) : base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				//Location = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ?? MountSpot;
				QuestId = GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null) ?? 0;
				QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = QuestInLogRequirement.InLog;

				LootId = GetAttributeAsNullable<int>("LootId", true, ConstrainAs.QuestId(this), null) ?? 0;
				QuestName = GetAttributeAs<string>("QuestName", true, ConstrainAs.StringNonEmpty, null) ?? "";
				TurnInName = GetAttributeAs<string>("TurnInName", true, ConstrainAs.StringNonEmpty, null) ?? "";
				TurnInId = GetAttributeAsNullable<int>("TurnInId", true, ConstrainAs.MobId, null) ?? 0;
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

		private WoWItem HordeLance()
		{
			return StyxWoW.Me.BagItems.FirstOrDefault(x => x.Entry == 46070);
		}

		private WoWItem ArgentLance()
		{
			return StyxWoW.Me.BagItems.FirstOrDefault(x => x.Entry == 46106);
		}

		// Attributes provided by caller
		public int TurnInId { get; private set; }
		public int QuestId { get; private set; }
		public int LootId { get; private set; }
		public string TurnInName { get; private set; }
		public string QuestName { get; private set; }

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

		public Composite GetClose
		{
			get
			{
				return
					new PrioritySelector(
						new Decorator(r=>StyxWoW.Me.QuestLog.GetQuestById((uint) QuestId) == null, new Action(r=>_isBehaviorDone=true)),
						new Decorator(ret => Me.Location.Distance(turin.Location) > turin.InteractRange,
									  new Action(r => Navigator.MoveTo(turin.Location))),
						new Decorator(ret => !GossipFrame.Instance.IsVisible && QuestManager.QuestFrame.IsVisible,
							new Sequence(
								new Sleep(300),
								new Action(ret => QuestManager.QuestFrame.SelectQuestReward(LootId)),
								new Action(ret => QuestManager.QuestFrame.SelectQuestReward(LootId)),
								new Sleep(300),
								new Action(ret => QuestManager.QuestFrame.CompleteQuest()),
								new Sleep(300)
								)),
						new Decorator(ret => GossipFrame.Instance.IsVisible && !QuestFrame.Instance.IsVisible,
									  new Action(r => stol())),
						new Decorator(ret => turin.WithinInteractRange, new Action(r => turin.Interact())));
			}
		}

		protected Composite CreateBehavior_QuestbotMain()
		{
			return _root ??
				   (_root =
					new Decorator(ret => !_isBehaviorDone, new PrioritySelector(GetClose, new ActionAlwaysSucceed())));
		}

		private WoWUnit turin
		{
			get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(x => x.Entry == turninguy.Entry); }
		}

		private NpcResult turninguy
		{
			get { return NpcQueries.GetNpcById((uint) TurnInId); }
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
			get { return (_isBehaviorDone); }
		}

		protected RunStatus stol()
		{
			if (QuestManager.QuestFrame.IsVisible && (QuestManager.QuestFrame.CurrentShownQuestId != 0))
			{
				return RunStatus.Success;
			}
			if (!QuestManager.GossipFrame.IsVisible)
			{
				if (QuestManager.QuestFrame.IsVisible)
				{
					List<uint> quests = QuestManager.GossipFrame.ActiveQuests.Select(r=>(uint)r.Id).ToList();
					if ((QuestId != -1) && !quests.Contains((uint) QuestId))
					{
						QuestManager.QuestFrame.Close();
						return RunStatus.Failure;
					}
					for (int i = 0; i < quests.Count; i++)
					{
						if ((QuestId == -1) || (((ulong) quests[i]) == (ulong) QuestId))
						{
							PlayerQuest questById = StyxWoW.Me.QuestLog.GetQuestById(quests[i]);
							if ((QuestId != -1) || questById.IsCompleted)
							{
								QuestManager.GossipFrame.SelectActiveQuest(i);
								return RunStatus.Success;
							}
						}
					}
				}
			}
			return RunStatus.Failure;
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