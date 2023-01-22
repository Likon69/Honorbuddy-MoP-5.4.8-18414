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
using Styx.Common;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.Hooks
{
	[CustomBehaviorFileName(@"BarrelHook")]
	[CustomBehaviorFileName(@"Hooks\BarrelHook")]
	public class BarrelHook : CustomForcedBehavior
	{
		private static Composite _myHook;

		public BarrelHook(Dictionary<string, string> args) : base(args)
		{
			QBCLog.BehaviorLoggingContext = this;
		}


		private Composite CreateHook()
		{
			WoWUnit ookOok = null;

			return new Decorator(r => StyxWoW.Me.HasAura("Chuck Barrel"),
				new PrioritySelector(ctx => ookOok = ObjectManager.GetObjectsOfTypeFast<WoWUnit>().FirstOrDefault(u => u.Entry == 57628),
					// only click the 'Break Barrel' button if ook is further than 20 units to - 
					// prevent getting barrel thrown at character again right after removing it.
					new Decorator(
						ctx => ookOok == null || ookOok.Distance > 20,
						new Action(r => Lua.DoString("RunMacroText('/click ExtraActionButton1')")))));
		}

		public override bool IsDone { get { return true; } }

		public override void OnStart()
		{
			OnStart_HandleAttributeProblem();

			// 1st call will install hook and 2nd call will remove it.
			if (_myHook == null)
			{
				 QBCLog.DeveloperInfo("Installing BarrelHook");
				_myHook = CreateHook();
				TreeHooks.Instance.InsertHook("Questbot_Main", 0, _myHook);
			}
			else
			{
				QBCLog.DeveloperInfo("Removing BarrelHook");
				TreeHooks.Instance.RemoveHook("Questbot_Main", _myHook);
				_myHook = null;
			}
		}
	}
}