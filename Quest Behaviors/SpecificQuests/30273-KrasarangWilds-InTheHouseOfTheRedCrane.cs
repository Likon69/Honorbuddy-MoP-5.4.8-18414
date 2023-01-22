// Behavior originally contributed by Natfoth.
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
// WIKI DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_FireFromTheSky
//
// QUICK DOX:
//      Used for the Dwarf Quest SI7: Fire From The Sky
//
//  Notes:
//      * Make sure to Save Gizmo.
//
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
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.InTheHouseOfTheRedCrane
{
	[CustomBehaviorFileName(@"SpecificQuests\30273-KrasarangWilds-InTheHouseOfTheRedCrane")]
	public class KrasarangWildsHouseOfTheRedCrane : CustomForcedBehavior
	{
		public KrasarangWildsHouseOfTheRedCrane(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				QuestId = GetAttributeAsNullable("QuestId", false, ConstrainAs.QuestId(this), null) ?? 30273;
				QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;
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
		private LocalPlayer Me { get { return (StyxWoW.Me); } }

		public static int[] MobIds = new[] { 59687 };
		public static int RedCraneID = 59653;

		public static WoWPoint ShaLocation = new WoWPoint(-1813.47f, 1052.34f, -31.73f);

		public WoWUnit Echo
		{
			get
			{
				return (ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => (MobIds.Contains((int)u.Entry) || u.SafeName.Contains("Echo")) && u.CanSelect && !u.IsDead));
			}
		}

		public WoWUnit Sha
		{
			get
			{
				return (ObjectManager.GetObjectsOfType<WoWUnit>()
									 .Where(u => u.Entry == 59651 && !u.IsDead && u.CanSelect)
									 .OrderBy(u => u.Distance).FirstOrDefault());
			}
		}

		public WoWUnit Crane
		{
			get
			{
				return (ObjectManager.GetObjectsOfType<WoWUnit>()
									 .Where(u => u.Entry == RedCraneID && !u.IsDead)
									 .OrderBy(u => u.Distance).FirstOrDefault());
			}
		}

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }

	   
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


		public Composite PreCombatStory
		{
			get
			{
				
				return new Decorator(
					ret => Sha == null,
					new PrioritySelector(
						new Decorator(ret => Crane == null,
										  new Action(delegate
														 {
															 TreeRoot.StatusText = "Moving to Start Crane Story";
															 Navigator.MoveTo(ShaLocation);

															 if (Me.IsDead)
															 {
																 Lua.DoString("RetrieveCorpse()");
															 }
														 }

											  )

										  ),

						new Decorator(ret => Crane != null && !Crane.WithinInteractRange && Math.Abs(Crane.Z - -31.73) < .20,
									  new Action(ret => Navigator.MoveTo(Crane.Location))
							),

						new Decorator(ret => Crane != null && Crane.WithinInteractRange && Math.Abs(Crane.Z - -31.73) < .20,
									  new Sequence(
										  new Action(ret => WoWMovement.MoveStop()),
										  new Action(ret => Crane.Interact()),
										  new Sleep(400),
										  new Action(ret => Lua.DoString("SelectGossipOption(1,\"gossip\", true)"))
										  ))));
			}
		}


		public WoWUnit Priority
		{
			get { 

				if (Echo != null)
				{
					return Echo;
				}

				if (Sha != null)
				{
					return Sha;
				}

				return null;

			}
		}


		public Composite CombatStuff
		{
			get
			{
				return new PrioritySelector(
					
					new Decorator(r=> Me.CurrentTarget == null && Priority != null, new Action(r=>Priority.Target())),
					//new Decorator(r=> Echo != null && Sha != null && Me.CurrentTarget != null && Me.CurrentTarget == Sha, new Action(r=>Echo.Target())),

					//LevelBot.CreateCombatBehavior()
					
					new Decorator(r=> !Me.Combat, RoutineManager.Current.PullBehavior),
					RoutineManager.Current.HealBehavior,
					RoutineManager.Current.CombatBuffBehavior,
				   RoutineManager.Current.CombatBehavior
					
					);
			}
		}


		protected Composite CreateBehavior_QuestbotMain()
		{
			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, PreCombatStory, CombatStuff, new ActionAlwaysSucceed())));
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
