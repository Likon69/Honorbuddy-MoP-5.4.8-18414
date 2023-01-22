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
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.ADisarmingDistraction
{
	[CustomBehaviorFileName(@"SpecificQuests\27761-Uldum-ADisarmingDistraction")]
	public class BomberMan : CustomForcedBehavior // A Disarming Distraction
	{

		public BomberMan(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				QuestId = 27761;
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
				return
					new Decorator(ret => Me.IsQuestComplete(QuestId) && Me.Mounted,
						new Action(delegate
						{
							TreeRoot.StatusText = "Finished!";
							_isBehaviorDone = true;
							return RunStatus.Success;
						}));

			}
		}







		public List<WoWUnit> Enemies
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.FactionId == 2334 && u.IsAlive).OrderBy(u => u.Distance).ToList();
			}
		}


		public WoWUnit ClosestBomb()
		{
			return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == 46888 && u.IsAlive && u.Location.Distance(BadBomb) > 10).OrderBy(u => u.Distance).FirstOrDefault();
		}


		public int Hostiles
		{
			get
			{
				return
					ObjectManager.GetObjectsOfType<WoWUnit>().Count(
						u => u.IsHostile && u.Location.Distance(ClosestBomb().Location) <= 10);
			}

		}

		public Composite DeployHologram
		{
			get
			{
				return new Decorator(ret => Hostiles > 0,
					new Action(r => Hologram().Use()));
			}
		}


		public Composite FindBomb
		{
			get
			{
				return new Decorator(ret => Me.Location.Distance(ClosestBomb().Location) > 12,
					new Action(delegate
					{
						var x = ClosestBomb().Location;
						x.Z += 10;
						Flightor.MoveTo(x);
					}));
			}
		}


		public Composite BreakCombat
		{
			get
			{
				return new Decorator(ret => Me.Combat,
					new Action(r => Hologram().Use()));
			}
		}

		public Composite Mount
		{
			get
			{
				return new Decorator(ret => !Me.Mounted && ClosestBomb().Distance > 10,
					new Action(r => Flightor.MountHelper.MountUp()));
			}
		}

		public Composite UseAndGo
		{
			get
			{
				return new Action(delegate { 
					ClosestBomb().Interact();
					Flightor.MountHelper.MountUp();
				});
			}
		}

		public WoWItem Hologram()
		{
			return Me.BagItems.FirstOrDefault(x => x.Entry == 62398);
		}

		protected Composite CreateBehavior_QuestbotMain()
		{

			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(BreakCombat,Mount, DoneYet,FindBomb, DeployHologram, UseAndGo)));
		}

		WoWPoint BadBomb = new WoWPoint(-10561.68, -2429.371, 91.56037);

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