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


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.LaosyScouting
{
	[CustomBehaviorFileName(@"SpecificQuests\31758-VOEB-LaosyScouting")]
	public class Blastranaar : CustomForcedBehavior
	{
		public Blastranaar(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				QuestId = 31758;//GetAttributeAsQuestId("QuestId", true, null) ?? 0;
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
		public int MobIdLao = 65868;
		private Composite _root;
		public WoWPoint Location1 = new WoWPoint(1578.794, 1446.312, 512.7374);
		public WoWPoint Location2 = new WoWPoint(1574.712, 1428.84, 484.7786);

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

		public List<WoWUnit> Lao
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>()
					.Where(u => u.Entry == MobIdLao && !u.IsDead && u.Distance < 10000)
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

		public Composite LaoMove
		{
			get
			{
				return new Decorator(ret => !Me.IsQuestObjectiveComplete(QuestId, 1),
					new PrioritySelector(
						new Decorator(ret => Lao.Count > 0,
							new Action(c =>
							{
								TreeRoot.StatusText = "Got Lao, moving to him";
								Lao[0].Target();
								Flightor.MoveTo(Lao[0].Location);

								if(Lao[0].Location.Distance(Me.Location) < 10)
								{
									TreeRoot.StatusText = "Finished!";
									_isBehaviorDone = true;
									return RunStatus.Success;
								}

								return RunStatus.Success;
							})),

						new Decorator(ret => Lao.Count == 0,
							new PrioritySelector(
								new Decorator(ret => Location1.Distance(Me.Location) > 50  && Me.CurrentTarget == null,
									new Action(c =>
									{
									  TreeRoot.StatusText = "Moving to 1st location";
									  Flightor.MoveTo(Location1);
										if(Lao.Count > 0)
										{
											Lao[0].Target();
										}

										return RunStatus.Success;
									 })),

								new Decorator(ret => Location2.Distance(Me.Location) > 50 && Me.CurrentTarget == null,
									new Action(c =>
									{
										TreeRoot.StatusText = "Moving to 2nd location";
										Flightor.MoveTo(Location2);
										Lao[0].Target();
										if(Lao.Count > 0)
										{
											Lao[0].Target();
										}
										return RunStatus.Success;
									  }))
							))
					));
			}
		}
		
		protected override Composite CreateBehavior()
		{
			return _root ?? (_root =
				new Decorator(ret => !_isBehaviorDone,
					new PrioritySelector(DoneYet, LaoMove, new ActionAlwaysSucceed())));
		}
	}
}

