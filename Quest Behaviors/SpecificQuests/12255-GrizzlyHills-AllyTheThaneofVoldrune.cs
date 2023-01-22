// Behavior originally contributed by Kickazz006
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
// This behavior is for killing Thane noobface in Grizzly Hills (Horde 12259 and Alliance 12255) 
// Code was taken from Shak
#endregion


#region Examples
#endregion


#region Usings
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
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


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.AllyTheThaneofVoldrune
{
	[CustomBehaviorFileName(@"SpecificQuests\12255-GrizzlyHills-AllyTheThaneofVoldrune")]
	public class q12255 : CustomForcedBehavior
	{
		public q12255(Dictionary<string, string> args) : base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			QuestId =  12255;
			Location = WoWPoint.Empty;
			Endloc = WoWPoint.Empty;
			QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
			QuestRequirementInLog    = QuestInLogRequirement.InLog;
		}
			

		public WoWPoint Location { get; private set; }
		public WoWPoint Endloc { get; private set; }
		public int QuestId { get; set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement    QuestRequirementInLog { get; private set; }
		public static LocalPlayer Me = StyxWoW.Me;
		WoWPoint endloc = new WoWPoint(2798.203, -2510.08, 99.77123);
		WoWPoint startloc = new WoWPoint(2939.488, -2525.839, 127.3586);
		WoWPoint flyloc = new WoWPoint(2788.155, -2508.851, 56.05595);
		
		#region Overrides of CustomForcedBehavior
		public List<WoWUnit> objmob
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>()
									.Where(u => (u.Entry == 27377 && !u.IsDead))
									.OrderBy(u => u.Distance).ToList();
			}
		}
		public List<WoWUnit> flylist
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>()
									.Where(u => (u.Entry == 27292 && !u.IsDead))
									.OrderBy(u => u.Distance).ToList();
			}
		}
		private Composite _root;
		protected override Composite CreateBehavior()
		{
			return _root ?? (_root =
				new PrioritySelector(


					new Decorator(ret => Me.QuestLog.GetQuestById(12255) != null && Me.QuestLog.GetQuestById(12255).IsCompleted,
						new Sequence(
							new Action(ret => TreeRoot.StatusText = "Finished!"),
							new WaitContinue(120,
							new Action(delegate
							{
								_isDone = true;
								return RunStatus.Success;
							})))),

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

							)),
                    new Decorator(ret => Query.IsInVehicle(), new ActionRunCoroutine(ctx => VehicleLogic())),

					new DecoratorContinue(ret => !Me.IsQuestObjectiveComplete(QuestId, 1) && objmob[0].Location.Distance(Me.Location) <= 20,
						new Sequence(
                            new Action(ret => TreeRoot.StatusText = "PWNing " + objmob[0].SafeName),
							new Action(ret => Lua.DoString("VehicleMenuBarActionButton2:Click()")),
							//new Sleep(1500),
							//new Action(ret => Lua.DoString("VehicleMenuBarActionButton3:Click()")),
							new Action(ret => Lua.DoString("VehicleMenuBarActionButton1:Click()")),
							new Action(ret => WoWMovement.Move(WoWMovement.MovementDirection.Backwards)),
                            new SleepForLagDuration(),
							new Action(ret => WoWMovement.MoveStop(WoWMovement.MovementDirection.Backwards)),
                            new SleepForLagDuration(),
							new Action(ret => objmob[0].Face()),
							new Sleep(500)
						)
					)
				)
			);
		}

	    private async Task VehicleLogic()
	    {
            while (Me.IsAlive)
            {
                if (Me.QuestLog.GetQuestById(12255).IsCompleted)
                {
                    if (Me.Location.Distance(endloc) > 15)
                    {
                        WoWMovement.ClickToMove(endloc);
                        await Coroutine.Sleep(5000);
                    }
                    Lua.DoString("VehicleExit()");
                    return;
                }
                if (objmob.Count == 0)
                {
                    WoWMovement.ClickToMove(startloc);
                     await Coroutine.Sleep(1000);
                }
                if (objmob.Count > 0)
                {
                    objmob[0].Target();
                    WoWMovement.ClickToMove(objmob[0].Location);
                    await Coroutine.Sleep(100);
                    Lua.DoString("UseAction(122, 'target', 'LeftButton')");
                    Lua.DoString("UseAction(121, 'target', 'LeftButton')");
                }
                await Coroutine.Yield();
            }
	    }

		private bool _isDone;
		public override bool IsDone
		{
			get { return _isDone; }
		}


		public override void OnStart()
		{
			OnStart_HandleAttributeProblem();
			if (!IsDone)
			{
				this.UpdateGoalText(QuestId);
			}
		}

		#endregion
	}
}