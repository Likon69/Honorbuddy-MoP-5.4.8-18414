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


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.ANewFriend
{
	[CustomBehaviorFileName(@"SpecificQuests\29679-PandaStarter-ANewFriend")]
	public class aNewFriend : CustomForcedBehavior
	{
		public aNewFriend(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			QuestId = 29679;
		}
		public int QuestId { get; set; }
		private bool _isBehaviorDone;
		private Composite _root;
		
		public override bool IsDone
		{
			get
			{
				return _isBehaviorDone;
			}
		}


		private LocalPlayer Me
		{
			get { return (StyxWoW.Me); }
		}


		public override void OnStart()
		{
			OnStart_HandleAttributeProblem();
			if (!IsDone)
			{
				TreeHooks.Instance.InsertHook("Questbot_Main", 0, CreateBehavior_QuestbotMain());

				this.UpdateGoalText(QuestId);
			}
		}


		public WoWUnit Spout
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>(true).Where(u => u.Entry == 60488).OrderBy(u => u.Distance).FirstOrDefault();
			}
		}

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


		public Composite SpoutWalk
		{
			get
			{
				return new Decorator(r => Spout != null, new Action(r =>
																		{
																			Navigator.MoveTo(Spout.Location);
																		}));
			}
		}



		protected Composite CreateBehavior_QuestbotMain()
		{
			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, SpoutWalk, new ActionAlwaysSucceed())));
		}

		#region Cleanup

        public override void OnFinished()
        {
            TreeHooks.Instance.RemoveHook("Questbot_Main", CreateBehavior_QuestbotMain());
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
        }

		#endregion
	}
}
