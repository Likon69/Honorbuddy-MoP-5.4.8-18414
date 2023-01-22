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


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.ThePrideofKezan
{
	[CustomBehaviorFileName(@"SpecificQuests\25066-LostIsles-ThePrideofKezan")]
	public class q25066 : CustomForcedBehavior
	{
		public q25066(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;
		}

		private Composite _behaviorTreeHook_CombatMain;
		public static LocalPlayer Me = StyxWoW.Me;
		private Composite _root;
		private bool _isBehaviorDone;
		WoWPoint endloc = new WoWPoint(1662.314, 2717.742, 189.7396);
		WoWPoint startloc = new WoWPoint(1782.963, 2884.958, 157.274);
		WoWPoint flyloc = new WoWPoint(1782.963, 2884.958, 157.274);
		
		public List<WoWUnit> objmob
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>()
									.Where(u => (u.Entry == 39039 && !u.IsDead))
									.OrderBy(u => u.Distance).ToList();
			}
		}
		public List<WoWUnit> flylist
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>()
									.Where(u => (u.Entry == 38387 && !u.IsDead))
									.OrderBy(u => u.Distance).ToList();
			}
		}
		
		public override void OnStart()
		{
			OnStart_HandleAttributeProblem();
			if (!IsDone)
			{
				_behaviorTreeHook_CombatMain = CreateBehavior_CombatMain();
				TreeHooks.Instance.InsertHook("Combat_Main", 0, _behaviorTreeHook_CombatMain);

				this.UpdateGoalText(0);
			}
		}
		
		public override bool IsDone
		{
			get
			{
				return _isBehaviorDone;
			}
		}
		
		public Composite DoneYet
		{
			get
			{
				return
					new Decorator(ret => Me.QuestLog.GetQuestById(25066) != null && Me.QuestLog.GetQuestById(25066).IsCompleted,
						new Sequence(
							new DecoratorContinue(ret => Query.IsInVehicle(),
								new Sequence(
									new DecoratorContinue(ret => Me.Location.Distance(endloc) > 10,
										new Sequence(
											new Action(ret => WoWMovement.ClickToMove(endloc)),
											new WaitContinue(TimeSpan.FromSeconds(10),
												context => Me.Location.Distance(endloc) <= 10,
												new ActionAlwaysSucceed()))),
									new DecoratorContinue(ret => Me.Location.Distance(endloc) <= 10,
										new Sequence(
											new Action(ret => Lua.DoString("VehicleExit()")),
											new ActionAlwaysSucceed())))),
							new Action(ret => TreeRoot.StatusText = "Finished!"),
							new WaitContinue(120,
								new Action(delegate
								{
									_isBehaviorDone = true;
									return RunStatus.Success;
								}))));
			}
		}
		
		protected virtual Composite CreateBehavior_CombatMain()
		{
			return new Decorator(context => !IsDone,
				new PrioritySelector(
					// Disable the CombatRoutine
					DoneYet, NotInPlane, InPlane, new ActionAlwaysSucceed()
			));
		}
		
		public Composite NotInPlane
		{
			get
			{
				return
					new Decorator(ret => !Query.IsInVehicle(),
						new Sequence(
							new DecoratorContinue(ret => flylist.Count == 0,
								new Sequence(
									new Action(ret => Navigator.MoveTo(flyloc)),
									new Sleep(1000))),
							new DecoratorContinue(ret => flylist.Count > 0 && flylist[0].Location.Distance(Me.Location) > 5,
								new Sequence(
									new Action(ret => Navigator.MoveTo(flylist[0].Location)),
									new Sleep(1000))),
							new DecoratorContinue(ret => flylist.Count > 0 && flylist[0].Location.Distance(Me.Location) <= 5,
								new Sequence(
									new Action(ret => WoWMovement.MoveStop()),
									new Action(ret => flylist[0].Interact()),
									new Sleep(1000),
									new Action(ret => Lua.DoString("SelectGossipOption(1)")),
									new Sleep(1000)))
					));
			}
		}
		
		public Composite InPlane
		{
			get
			{
				return
					new Decorator(ret => Query.IsInVehicle(),
						new Sequence(
							new DecoratorContinue(ret => !Query.IsInVehicle(),
								new ActionAlwaysSucceed()),
							new DecoratorContinue(ret => objmob.Count == 0,
								new Sequence(
									new Action(ret => WoWMovement.ClickToMove(startloc)),
									new WaitContinue(TimeSpan.FromSeconds(10),
										ret => Me.Location.Distance(startloc) <= 10,
										new ActionAlwaysSucceed()))),
							new DecoratorContinue(ret => objmob.Count > 0,
								new Sequence(
									new Action(ret => objmob[0].Target()),
									new DecoratorContinue(ret => Me.CurrentTarget.Location.Distance(Me.Location) > 10,
										new Action(ret => WoWMovement.ClickToMove(Me.CurrentTarget.Location))),
									new DecoratorContinue(ret => Me.CurrentTarget.Location.Distance(Me.Location) <= 20,
										new Sequence(
											new Action(ret => Lua.DoString("CastPetAction(2)")),
											new Action(ret => Lua.DoString("CastPetAction(1)"))))))
					));
			}
		}

		protected override Composite CreateBehavior()
		{
			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(new ActionAlwaysSucceed())));
		}
	}
}

