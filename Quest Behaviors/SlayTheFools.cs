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
// DOCUMENTATION:
// This behavior makes mastahg cry each time it is used.
// 
#endregion


#region Examples
// <CustomBehavior File="SlayTheFools" QuestId="" MobId="" CollectionDistance="" 
// <CustomBehavior File="SlayTheFools" QuestId="29749" MobId="55489" CollectionDistance="60" X="2930.426" Y="-1644.892" Z="252.8332" />
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


namespace Honorbuddy.Quest_Behaviors.SlayTheFools
{
	[CustomBehaviorFileName(@"SlayTheFools")]
	public class SlayTheFools : CustomForcedBehavior
	{
		public SlayTheFools(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				Location = GetAttributeAsNullable<WoWPoint>("", false, ConstrainAs.WoWPointNonEmpty, null) ?? Me.Location;
				QuestId = GetAttributeAsNullable<int>("QuestId",true, ConstrainAs.QuestId(this), null) ?? 0;
				ObjectiveId = GetAttributeAsNullable<int>("ObjectiveId", false, ConstrainAs.RepeatCount, null) ?? 0;
				MobId = GetAttributeAsNullable<int>("MobId", true, ConstrainAs.MobId, null) ?? 0;
				Distance = GetAttributeAsNullable<int>("CollectionDistance", false, null, null) ?? 1000;
				QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;

				QBCLog.DeveloperInfo("Distance thingy is:" + Distance); 
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
		public int ObjectiveId { get; private set; }
		public int QuestId { get; private set; }
		public int MobId { get; private set; }
		public int Distance { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }
		public WoWPoint Location { get; private set; }


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
					new Decorator(ret => IsComplete(), new Action(delegate
					{

						TreeRoot.StatusText = "Finished!";
						_isBehaviorDone = true;
						return RunStatus.Success;
					}));

			}
		}

		public bool IsComplete()
		{
			return
				(ObjectiveId == 0)
				? Me.IsQuestComplete(QuestId)
				: Me.IsQuestObjectiveComplete(QuestId, ObjectiveId);
		}


		public Composite DoDps
		{
			get
			{
				return
					new PrioritySelector(RoutineManager.Current.CombatBuffBehavior,RoutineManager.Current.CombatBehavior);
			}

		}

		public WoWUnit TargetKind
		{
			get
			{
				return
					ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == MobId && u.IsAlive && u.Location.Distance(Location) <= Distance).OrderBy( u => u.Distance).FirstOrDefault();
			}
		}

		public Composite SelectNew
		{
			get
			{
				return new Decorator(r=> TargetKind != null, new Action(r=>TargetKind.Target()));
			}
		}


		public Composite FindOne
		{
			get
			{
				return
					new Decorator(
						r => Me.CurrentTarget == null || (Me.CurrentTarget.IsFriendly || Me.CurrentTarget.IsDead),
						SelectNew);

			}
		}

		public Composite HandleCombat
		{
			get
			{
				return
					new PrioritySelector(
					new Decorator(r=>Me.Mounted,new Action(r=>Flightor.MountHelper.Dismount())),
						new Decorator(ret => Me.Combat, DoDps));
			}
		}


		public Composite StayInRange
		{
			get
			{
				return new Decorator(ret => Me.Location.Distance(Location) > Distance, new Action(r=>Navigator.MoveTo(Location)));
			}
		}

		protected Composite CreateBehavior_QuestbotMain()
		{

			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, StayInRange,HandleCombat, FindOne,DoDps, new ActionAlwaysSucceed())));
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