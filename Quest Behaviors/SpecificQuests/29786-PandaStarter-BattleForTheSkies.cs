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
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.BattleForTheSkies
{
	[CustomBehaviorFileName(@"SpecificQuests\29786-PandaStarter-BattleForTheSkies")]
	public class BattlefortheSkies : CustomForcedBehavior
	{
		public BattlefortheSkies(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			QuestId = 29786;
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


		public WoWUnit Dargon
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>(true).FirstOrDefault(u => u.Entry == 55786);
			}
		}


		public WoWUnit Launcher
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>(true).Where(u => u.Entry == 64507 && u.Flags == 295680).OrderBy(r=>r.Location.Distance(Dargon.Location.RayCast(Dargon.MovementInfo.Heading,20f))).FirstOrDefault();
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

		
		public Composite DpsHim
		{
			get
			{
				return new PrioritySelector(
					new Decorator(r=> Me.CurrentTarget == null && Dargon != null && Me.CurrentTarget != Dargon, new Action(r=>Dargon.Target())),
					new Decorator(r => Dargon != null && Dargon.HasAura(125992), RoutineManager.Current.CombatBehavior)
					
					);
			}
		}


		public Composite Boom
		{
			get
			{
				return new Decorator(r => Dargon != null && Launcher != null, new Action(r =>
																		{
																			//Navigator.MoveTo(Launcher.Location);
																			Launcher.Interact(true);
																		}));
			}
		}


		protected Composite CreateBehavior_QuestbotMain()
		{
			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, DpsHim, Boom, new ActionAlwaysSucceed())));
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
