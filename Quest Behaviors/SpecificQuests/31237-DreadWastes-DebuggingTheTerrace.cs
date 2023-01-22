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
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.DebuggingTheTerrace
{
	[CustomBehaviorFileName(@"SpecificQuests\31237-DreadWastes-DebuggingTheTerrace")]
	public class Blastranaar : CustomForcedBehavior
	{
		public Blastranaar(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				QuestId = 31237;
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
		public int QuestId { get; set; }
		private bool _isBehaviorDone;
		public int MobIdEyeGas = 63786;
		public int MobIdEyeEmpress = 63783;
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
				this.UpdateGoalText(QuestId);
			}
		}

		public List<WoWUnit> Eye
		{
			get
			{
				return
					ObjectManager.GetObjectsOfType<WoWUnit>()
					.Where(u => u.Entry == MobIdEyeGas && !u.IsDead && u.Distance < 10000)
					.OrderBy(u => u.Distance)
					.ToList();
			}
		}

		public List<WoWUnit> Eye1
		{
			get
			{
				return 
					ObjectManager.GetObjectsOfType<WoWUnit>()
					.Where(u => u.Entry == MobIdEyeEmpress && !u.IsDead && u.Distance < 10000)
					.OrderBy(u => u.Distance)
					.ToList();
			}
		}


		public Composite DoneYet
		{
			get
			{
				return new Decorator(ret => Me.IsQuestObjectiveComplete(QuestId, 1),
					new Action(delegate
					{
						TreeRoot.StatusText = "Finished!";
						_isBehaviorDone = true;
						return RunStatus.Success;
					}));

			}
		}







		
		protected override Composite CreateBehavior()
		{
			return _root ?? (_root =
				new Decorator(ret => !_isBehaviorDone,
					new PrioritySelector(
						DoneYet,
						new DecoratorContinue(ret => !Me.IsQuestObjectiveComplete(QuestId, 1),
							new Sequence(  
								new DecoratorContinue(ret => Eye[0].Location.Distance(Me.Location) > 5,
									new Sequence(
										new Action(ret => Navigator.MoveTo(Eye[0].Location)),
										new Action(r => Eye[0].Face())
									)),
								new DecoratorContinue(ret => Eye[0].Location.Distance(Me.Location) <= 3,
									new Sequence(
										new Action(r => WoWMovement.MoveStop()),
										new Action(r => Eye[0].Face()),
										new Action(r => Eye1[0].Interact())
									)))),
								new ActionAlwaysSucceed())));
		}
	}
}
