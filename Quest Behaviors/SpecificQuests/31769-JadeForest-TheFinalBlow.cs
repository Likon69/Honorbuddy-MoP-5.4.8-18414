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

using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.TheFinalBlow
{
	[CustomBehaviorFileName(@"SpecificQuests\31769-JadeForest-TheFinalBlow")]
	public class TheFinalBlow : CustomForcedBehavior
	{
		private bool _isBehaviorDone;

		private Composite _root;
		private WoWPoint bounce = new WoWPoint(3158.702, -934.0057, 324.6955);
		private WoWPoint spot = new WoWPoint(3157.633, -894.3948, 324.696);
		private int stage = 0;

		public TheFinalBlow(Dictionary<string, string> args) : base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				QuestId = 31769;
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


		public override bool IsDone
		{
			get { return _isBehaviorDone; }
		}

		private LocalPlayer Me
		{
			get { return (StyxWoW.Me); }
		}


		public WoWGameObject Barricade
		{
			get { return ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(u => u.Entry == 215650 && u.Distance < 10); }
		}

		public WoWItem Gun
		{
			get { return Me.BagItems.FirstOrDefault(r => r.Entry == 89769); }
		}

		public Composite DoneYet
		{
			get
			{
				return new Decorator(ret => Me.IsQuestComplete(QuestId),
					new Action(delegate
					{
						TreeRoot.StatusText = "Finished!";
						_isBehaviorDone = true;
						return RunStatus.Success;
					}));
			}
		}


		private Composite HandleCombat
		{
			get
			{
				return new PrioritySelector(
					new Decorator(ret => !StyxWoW.Me.Combat, RoutineManager.Current.PreCombatBuffBehavior),
					new Decorator(
						ret => StyxWoW.Me.Combat,
						new PrioritySelector(
							RoutineManager.Current.HealBehavior,
							new Decorator(
								ret => StyxWoW.Me.GotTarget && !StyxWoW.Me.CurrentTarget.IsFriendly && !StyxWoW.Me.CurrentTarget.IsDead,
								new PrioritySelector(RoutineManager.Current.CombatBuffBehavior, RoutineManager.Current.CombatBehavior)))));
			}
		}


		public Composite BlowUp
		{
			get
			{
				return new Decorator(r => Barricade != null,
					new Action(delegate
					{
						Gun.Use();
						return RunStatus.Failure;
					}));
			}
		}


		public Composite Move
		{
			get
			{
				return new PrioritySelector(
					new Decorator(r => spot.Distance(Me.Location) > 10 && stage <= 0, new Action(r => Navigator.MoveTo(spot))),
					new Decorator(r => spot.Distance(Me.Location) < 10 && stage == 0, new Action(r => stage = 1)));
			}
		}


		public Composite bouncez
		{
			get
			{
				return new PrioritySelector(
					new Decorator(r => bounce.Distance(Me.Location) > 3 && stage == 1, new Action(r => Navigator.MoveTo(bounce))),
					new Decorator(r => bounce.Distance(Me.Location) <= 3 && stage == 1, new Action(r => stage = -1)));
			}
		}

		private bool _useMount;
		public override void OnStart()
		{
			OnStart_HandleAttributeProblem();
			if (!IsDone)
			{
				TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_MainCombat());
				_useMount = CharacterSettings.Instance.UseMount;
				CharacterSettings.Instance.UseMount = false;

				this.UpdateGoalText(QuestId);
			}
		}


		protected Composite CreateBehavior_MainCombat()
		{
			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, BlowUp, HandleCombat, bouncez, Move)));
		}


		#region Cleanup

        public override void OnFinished()
        {
            CharacterSettings.Instance.UseMount = _useMount;
            TreeHooks.Instance.RemoveHook("Combat_Main", CreateBehavior_MainCombat());
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
        }

		#endregion
	}
}