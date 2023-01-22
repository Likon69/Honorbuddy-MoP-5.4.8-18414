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
using System.Collections.Generic;
using System.Linq;

using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.WarchiefsRevenge
{
	[CustomBehaviorFileName(@"SpecificQuests\14243-LostIsles-WarchiefsRevenge")]
	public class q14243 : CustomForcedBehavior
	{

		public q14243(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;
		}


		public static LocalPlayer me = StyxWoW.Me;
		
		
		#region Overrides of CustomForcedBehavior
		public List<WoWUnit> mobList
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>()
									.Where(u => (u.Entry == 36176 && !u.IsDead))
									.OrderBy(u => u.Distance).ToList();
			}
		}
		
		private Composite _root;
		protected override Composite CreateBehavior()
		{
			return _root ?? (_root =
				new PrioritySelector(
					
					new Decorator(ret => (me.QuestLog.GetQuestById(14243) != null && me.QuestLog.GetQuestById(14243).IsCompleted),
						new Sequence(
							new Action(ret => TreeRoot.StatusText = "Finished!"),
							new Action(ret => Lua.DoString("VehicleExit()")),
							new Sleep(15000),
							new WaitContinue(120,
							new Action(delegate
							{
								_isDone = true;
								return RunStatus.Success;
								}))
							)),
							
					new Decorator(ret => mobList.Count > 0,
						new Sequence(
                            new Action(ret => TreeRoot.StatusText = "Bombing - " + mobList[0].SafeName),
							new Action(ret => Lua.DoString("RunMacroText('/click VehicleMenuBarActionButton1','0')")),
							new Action(ret => SpellManager.ClickRemoteLocation(mobList[0].Location)),
							new Sleep(2000)
						)
					)
				)
			);
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
				this.UpdateGoalText(0);
			}
		}

		#endregion
	}
}

