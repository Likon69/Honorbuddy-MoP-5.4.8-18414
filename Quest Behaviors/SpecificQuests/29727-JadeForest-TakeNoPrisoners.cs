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


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.TakeNoPrisoners
{
	[CustomBehaviorFileName(@"SpecificQuests\29727-JadeForest-TakeNoPrisoners")]
	public class JadeForestTakeNoPrisoners : CustomForcedBehavior
	{
		public JadeForestTakeNoPrisoners(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				QuestId = GetAttributeAsNullable("QuestId", false, ConstrainAs.QuestId(this), null) ?? 29727;
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

		private bool _usedTurret;

		public static int[] MobIds = new[] { 55411, 55484, 55410, 55473, 55505, 55485 };
		public static int[] OrcIds = new[] { 55498, 55501, 55499 };

		public static WoWPoint TurretLocation = new WoWPoint(1116.968f, -544.0963f, 413.5516f);
		public static WoWPoint UsingTurretLocation = new WoWPoint(1296.96f, -430.156f, 314.718f);

		public WoWUnit HozenEnemy
		{
			get
			{
				return (ObjectManager.GetObjectsOfType<WoWUnit>()
									 .Where(u => MobIds.Contains((int)u.Entry) && !u.IsDead)
									 .OrderBy(u => u.Distance).FirstOrDefault());
			}
		}

		public WoWUnit OrcEnemy
		{
			get
			{
				return (ObjectManager.GetObjectsOfType<WoWUnit>()
									 .Where(u => OrcIds.Contains((int)u.Entry) && !u.IsDead)
									 .OrderBy(u => u.Distance).FirstOrDefault());
			}
		}

		public WoWGameObject Turret
		{
			get
			{
				return (ObjectManager.GetObjectsOfType<WoWGameObject>()
									 .Where(u => u.Entry == 209621)
									 .OrderBy(u => u.Distance).FirstOrDefault());
			}
		}

		public WoWUnit Amber
		{
			get
			{
				return (ObjectManager.GetObjectsOfType<WoWUnit>()
									 .Where(u => u.Entry == 55283 && !u.IsDead)
									 .OrderBy(u => u.Distance).FirstOrDefault());
			}
		}

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }


		#region Overrides of CustomForcedBehavior



		protected Composite CreateBehavior_QuestbotMain()
		{
				
			return _root ?? (_root =
				new PrioritySelector(
					new Decorator(ret => !_isBehaviorDone,
						new PrioritySelector(
							new Decorator(ret => Me.QuestLog.GetQuestById((uint)QuestId) != null && Me.QuestLog.GetQuestById((uint)QuestId).IsCompleted,
								new Sequence(
									new Action(ret => TreeRoot.StatusText = "Finished!"),
									new WaitContinue(120,
										new Action(delegate
										{
											_isBehaviorDone = true;
											return RunStatus.Success;
										}))
									)),

								new Decorator(ret => !Query.IsInVehicle(),
									new PrioritySelector(
										new Decorator(ret => Amber == null,
											new Sequence(
												new Action(ret => TreeRoot.StatusText = "Moving to Start Amber(Human) Story"),
												new Action(ret => Navigator.MoveTo(new WoWPoint(-157.5062f, -2659.278f, 1.069468f))),
												new Action(ret => WoWMovement.MoveStop()),
												new Sleep(1000)
											 )),

										new Decorator(ret => Amber != null && !Amber.WithinInteractRange,
												new Sequence(
													new Action(ret => Navigator.MoveTo(Amber.Location)),
													new Action(ret => WoWMovement.MoveStop()),
													new Sleep(1000)
													)
											 ),

										new Decorator(ret => Amber != null && Amber.WithinInteractRange,
											new Sequence(
												new Action(ret => WoWMovement.MoveStop()),
												new Action(ret => Amber.Interact()),
												new Sleep(400),
												new Action(ret => Lua.DoString("SelectGossipOption(1,\"gossip\", true)"))
											 )))),


									new Decorator(ret => Query.IsInVehicle(),
										new PrioritySelector(
												new Decorator(ret => HozenEnemy != null,
												new Sequence(
													new Action(ret => HozenEnemy.Target()), 
													new Sleep(400),
													new Action(ret => HozenEnemy.Interact()))),

											new Decorator(ret => OrcEnemy != null, // Orc Has to be Seperate or we will Die
												new Sequence(
													new Action(ret => OrcEnemy.Target()),
													new Sleep(400),
													new Action(ret => OrcEnemy.Interact()))),

											new Decorator(ret => UsingTurretLocation.Distance(StyxWoW.Me.Location) > 30 && !_usedTurret,
												new PrioritySelector(
													new Decorator(ret => TurretLocation.Distance(Me.Location) > 3,
														new Action(ret => Navigator.MoveTo(TurretLocation))),
													new Decorator(r => Turret.WithinInteractRange, 
														new Sequence(
															new Sleep(450),
															new Action(r =>
															{
																Navigator.PlayerMover.MoveStop();
																Turret.Interact();

																_usedTurret = true;
															})))))
											))
					))));
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
