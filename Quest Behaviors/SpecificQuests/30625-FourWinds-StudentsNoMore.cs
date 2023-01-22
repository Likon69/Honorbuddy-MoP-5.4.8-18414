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
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;

using CommonBehaviors.Actions;
using CommonBehaviors.Decorators;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.StudentsNoMore
{
	[CustomBehaviorFileName(@"SpecificQuests\30625-FourWinds-StudentsNoMore")]
	public class FourWindsStudentsNoMore : CustomForcedBehavior
	{
		public FourWindsStudentsNoMore(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				QuestId = GetAttributeAsNullable("QuestId", false, ConstrainAs.QuestId(this), null) ?? 30625;
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

		public static List<uint> StudentIDs = new List<uint> { 59839, 59840, 59843, 59841, 59842 };

		public static List<WoWPoint> StudentPoints = new List<WoWPoint> { new WoWPoint(-252.1181f, 2333.618f, 136.896f), new WoWPoint(-428.7136f, 2309.584f, 133.6259f) };
		public bool AtFirstPoint = false;


		public List<WoWUnit> Enemys
		{
			get
			{
				return (ObjectManager.GetObjectsOfType<WoWUnit>()
									 .Where(u => u.FactionId == 2550 && !u.Elite && !u.IsDead && u.Distance < 199)
									 .OrderBy(u => u.Distance).ToList());
			}
		}

		public List<WoWUnit> EnemysStudents
		{
			get
			{
				return (ObjectManager.GetObjectsOfType<WoWUnit>()
									 .Where(u => u.FactionId == 2550 && !u.IsDead && u.Distance < 199 && Students.Any(stud => u.CurrentTarget == stud))
									 .OrderBy(u => u.Distance).ToList());
			}
		}

		public List<WoWUnit> Students
		{
			get
			{
				return (ObjectManager.GetObjectsOfType<WoWUnit>()
									 .Where(u => StudentIDs.Contains(u.Entry)).OrderBy(u => u.IsDead)
									 .ThenBy(u => u.Distance).ToList());
			}
		}

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }

		#region Overrides of CustomForcedBehavior

	   

		protected override Composite CreateBehavior()
		{
			return _root ?? (_root =
				new PrioritySelector(
					new Decorator(
						ret => !_isBehaviorDone,
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

								new Decorator(
									ret => Students.Count == 0,
									new PrioritySelector(
										new Decorator(ret => !AtFirstPoint,
											new PrioritySelector(
												new Decorator(ret => Me.Location.Distance(StudentPoints[0]) > 3,
													new Action(ret => Navigator.MoveTo(StudentPoints[0]))),
												new Decorator(ret => Me.Location.Distance(StudentPoints[0]) <= 3,
													new Action(ret => AtFirstPoint = true))
													)),
									   new Decorator(ret => AtFirstPoint,
										   new PrioritySelector(
												new Decorator(ret => Me.Location.Distance(StudentPoints[1]) > 3,
													new Action(ret => Navigator.MoveTo(StudentPoints[1]))),
												new Decorator(ret => Me.Location.Distance(StudentPoints[1]) <= 3,
													new Action(ret => AtFirstPoint = false))
													))

								   )),

								   new Decorator(ret => Students.Count > 0 && Students[0].IsDead,
										new DecoratorIsNotPoiType(PoiType.Kill,
										   new PrioritySelector(
											   new Decorator(ret => Enemys.Any(enem => enem.Location.Distance(Students[0].Location) < 20),
												   new ActionSetPoi(ret => new BotPoi(Enemys.Where(enem => enem.Location.Distance(Students[0].Location) < 20).OrderBy(enem => enem.Distance).FirstOrDefault(), PoiType.Kill))),
											   new Decorator(ret => Students[0].Distance > 3,
												   new Action(ret => Navigator.MoveTo(Students[0].Location)))))),

								  new Decorator(ret => EnemysStudents.Count > 0,
									  new PrioritySelector(
										  new Decorator(ret => EnemysStudents[0].Distance > 10,
											  new Action(ret => Navigator.MoveTo(EnemysStudents[0].Location))),
										  new Decorator(ret => EnemysStudents[0].Distance <= 10 && Me.IsMoving,
											  new Action(ret => WoWMovement.MoveStop())),
										  new Decorator(ret => EnemysStudents[0].Distance <= 10,
											  new PrioritySelector(
											  new Decorator(ret => RoutineManager.Current.CombatBehavior != null,
																RoutineManager.Current.PullBehavior),
													 new Action(ret => EnemysStudents[0].Target()),
													 new Action(c => RoutineManager.Current.Pull()))))),

								  new Decorator(ret => Students.Count > 0 && Students[0].IsAlive,
									  new PrioritySelector(
										  new Decorator(ret => Students.Any(stud => stud.Combat) && EnemysStudents.Count > 0,
											  new ActionSetPoi(ret => new BotPoi(EnemysStudents[0], PoiType.Kill))),
										  new Decorator(ret => Students[0].Distance > 3,
											  new Action(ret => Navigator.MoveTo(Students[0].Location)))))
															

					))));
		}

        public override void OnFinished()
        {
            Targeting.Instance.IncludeTargetsFilter -= IncludeTargetsFilter;
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

			OnStart_HandleAttributeProblem();

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (!IsDone)
			{
				Targeting.Instance.IncludeTargetsFilter += IncludeTargetsFilter;
				this.UpdateGoalText(QuestId);
			}
		}

		void IncludeTargetsFilter(List<WoWObject> incomingUnits, HashSet<WoWObject> outgoingUnits)
		{
			var studs = Students;
			if (!studs.Any()) return;
			foreach (var unit in incomingUnits.OfType<WoWUnit>())
			{
				if (studs.Any(s => s.CurrentTargetGuid == unit.Guid))
					outgoingUnits.Add(unit);
			}
		}

		#endregion
	}
}
