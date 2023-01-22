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
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.Hooks
{
	[CustomBehaviorFileName(@"Hooks\BlackrockMaskHook")]
	public class BlackrockMaskHook : CustomForcedBehavior
	{
		public BlackrockMaskHook(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			//True = hook running, false = hook stopped
			_state = GetAttributeAsNullable<bool>("state", false, null, null) ?? false;
		}


		private bool _inserted = false;
		private bool _state;

		public override bool IsDone { get { return true; } }

		public static WoWItem Disguise
		{
			get
			{
				return StyxWoW.Me.BagItems.FirstOrDefault(r => r.Entry == 63357);
			}
		}

		public int[] Auras = new int[] { 89259, 89260, 89254, 89253, 89256, 89255, 89258, 89257 };


		public static bool Disguised
		{
			get { return StyxWoW.Me.HasAura(89261); }
		}

		private static Composite _myHook;
		private static Composite CreateHook()
		{
			return 
				new Decorator(r => Disguise != null && StyxWoW.Me.IsAlive && !StyxWoW.Me.Combat && StyxWoW.Me.ZoneId == 46 && !Disguised,
					new Action(r =>
					{
						Navigator.PlayerMover.MoveStop();
						Disguise.Use();
					}));
		}

		public override void OnStart()
		{
			OnStart_HandleAttributeProblem();

			if (_state == true)
			{
				if (_myHook == null)
				{
					QBCLog.Info("Inserting hook");
					_myHook = CreateHook();
					TreeHooks.Instance.InsertHook("Questbot_Main", 0, _myHook);
				}
				else
				{
					QBCLog.Info("Insert was requested, but was already present");
				}

				_inserted = true;
			}

			else
			{
				if (_myHook != null)
				{
					QBCLog.Info("Removing hook");
					TreeHooks.Instance.RemoveHook("Questbot_Main", _myHook);
					_myHook = null;
				}
				else
				{
					QBCLog.Info("Remove was requested, but hook was not present");
				}

				_inserted = false;
			}
		}
	}
}
