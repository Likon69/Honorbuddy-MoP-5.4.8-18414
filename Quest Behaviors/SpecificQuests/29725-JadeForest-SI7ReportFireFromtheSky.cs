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
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.SI7ReportFireFromtheSky
{
	[CustomBehaviorFileName(@"SpecificQuests\29725-JadeForest-SI7ReportFireFromtheSky")]
	public class JadeForestFireFromTheSky : CustomForcedBehavior
	{
		public JadeForestFireFromTheSky(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				QuestId = GetAttributeAsNullable("QuestId", false, ConstrainAs.QuestId(this), null) ?? 29725;
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

		public static int[] MobIds = new[] { 55550, 55589 };

		public static WoWPoint Shrine1Location = new WoWPoint(789.3542f, -1988.882f, 54.2512f);
		public static WoWPoint Shrine2Location = new WoWPoint(963.9094, -1960.19, 67.762);
		public static WoWPoint Shrine3Location = new WoWPoint(776.9325, -1788.328, 56.5228);
		public static WoWPoint CampLocation = new WoWPoint(714.5405, -2103.443, 65.78586);

		public static WaitTimer AimingTimer = new WaitTimer(TimeSpan.FromSeconds(2));
		public static WaitTimer WaitAtThridTimer = new WaitTimer(TimeSpan.FromSeconds(20));

		private bool _firstExplored;
		private bool _secondExplored;
		private bool _thridExplored;
		private bool _waitForThrid;
		private bool _campExplored;

		public WoWUnit Enemy
		{
			get
			{
				return (ObjectManager.GetObjectsOfType<WoWUnit>()
									 .Where(u => MobIds.Contains((int)u.Entry) && !u.IsDead)
									 .OrderBy(u => u.Distance).FirstOrDefault());
			}
		}

		public WoWUnit Sully
		{
			get
			{
				return (ObjectManager.GetObjectsOfType<WoWUnit>()
									 .Where(u => u.Entry == 55282 && !u.IsDead)
									 .OrderBy(u => u.Distance).FirstOrDefault());
			}
		}

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }

		#region Overrides of CustomForcedBehavior

	   

		protected Composite CreateBehavior_MainCombat()
		{
			return _root ?? (_root =
				new PrioritySelector(
					new Decorator(
						ret => !_isBehaviorDone,
						new PrioritySelector(
							new Decorator(ret => _campExplored || Me.QuestLog.GetQuestById((uint)QuestId) != null && Me.QuestLog.GetQuestById((uint)QuestId).IsCompleted,
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
										new Decorator(ret => Sully == null,
											new Sequence(
												new Action(ret => TreeRoot.StatusText = "Moving to Start Sully(Dwarf) Story"),
												new Action(ret => Navigator.MoveTo(new WoWPoint(-157.5062f, -2659.278f, 1.069468f)))
											 )),

										new Decorator(ret => Sully != null && !Sully.WithinInteractRange,
												new Action(ret => Navigator.MoveTo(Sully.Location))
											 ),

										new Decorator(ret => Sully != null && Sully.WithinInteractRange,
											new Sequence(
												new Action(ret => WoWMovement.MoveStop()),
												new Action(ret => Flightor.MountHelper.Dismount()),
												new Action(ret => Sully.Interact()),
												new Sleep(400),
												new Action(ret => Lua.DoString("SelectGossipOption(1,\"gossip\", true)"))
											 )))),


									new Decorator(ret => Query.IsInVehicle(),
										new PrioritySelector(
											new Decorator(ret => Enemy != null && AimingTimer.IsFinished,
												new Sequence(
													new Action(ret => Enemy.Target()),
													new Sleep(400),
													new Action(ret => Lua.DoString("CastPetAction({0})", 1)),
													new Action(ret => AimingTimer.Reset()))),

											new Decorator(ret => !_firstExplored,
												new PrioritySelector(
													new Decorator(ret => Shrine1Location.Distance(Me.Location) > 3,
														new Action(ret => Navigator.MoveTo(Shrine1Location))),
													new Decorator(ret => Shrine1Location.Distance(Me.Location) <= 3,
														new Action(ret => _firstExplored = true)))),

											new Decorator(ret => !_secondExplored,
												new PrioritySelector(
													new Decorator(ret => Shrine2Location.Distance(Me.Location) > 3,
														new Action(ret => Navigator.MoveTo(Shrine2Location))),
													new Decorator(ret => Shrine2Location.Distance(Me.Location) <= 3,
														new Action(ret => _secondExplored = true)))),

											new Decorator(ret => !_thridExplored,
												new PrioritySelector(
													new Decorator(ret => Shrine3Location.Distance(Me.Location) > 3,
														new Action(ret => Navigator.MoveTo(Shrine3Location))),
													new Decorator(ret => Shrine3Location.Distance(Me.Location) <= 3,
														new Sequence(
															new Action(ret => _thridExplored = true),
															new Action(ret => WaitAtThridTimer.Reset()))))),

											new Decorator(ret => !_waitForThrid,
												new PrioritySelector(
													new Decorator(ret => WaitAtThridTimer.IsFinished,
														new Action(ret => _waitForThrid = true)))),

											new Decorator(ret => !_campExplored && _waitForThrid,
												new PrioritySelector(
													new Decorator(ret => CampLocation.Distance(Me.Location) > 3,
														new Action(ret => Navigator.MoveTo(CampLocation))),
													new Decorator(ret => CampLocation.Distance(Me.Location) <= 3,
															new Action(ret => _campExplored = true))))
											))
					))));
		}

        public override void OnFinished()
        {
            TreeHooks.Instance.RemoveHook("Combat_Main", CreateBehavior_MainCombat());
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
			AimingTimer.Reset();

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (!IsDone)
			{
				TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_MainCombat());

				this.UpdateGoalText(QuestId);
			}
		}

		#endregion
	}
}
