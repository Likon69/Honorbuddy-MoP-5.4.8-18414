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
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.AnAncientEvil
{
	[CustomBehaviorFileName(@"SpecificQuests\29798-PandaStarter-AnAncientEvil")]
	public class AnAncientEvil : CustomForcedBehavior
	{
		public AnAncientEvil(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			QuestId = 29798;//GetAttributeAsQuestId("QuestId", true, null) ?? 0;
		}
		public int QuestId { get; set; }
		private bool _isBehaviorDone;
		

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
				TreeHooks.Instance.InsertHook("Questbot_Main", 0, CreateBehavior_QuestbotMain());

				this.UpdateGoalText(QuestId);
			}
		}

		//<Vendor Name="Vordraka, the Deep Sea Nightmare" Entry="56009" Type="Repair" X="267.6218" Y="4036.052" Z="68.99686" />
		public WoWUnit Vord
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>(true).FirstOrDefault(u => u.Entry == 56009);
			}
		}

		//<Vendor Name="Deepscale Aggressor" Entry="60685" Type="Repair" X="287.0461" Y="4015.281" Z="75.54617" />


		public WoWUnit Add
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>(true).FirstOrDefault(u => u.Entry == 60685 && u.IsAlive);
			}
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

		private static WoWPoint CalculatePointBehindTarget()
		{
			return
				StyxWoW.Me.CurrentTarget.Location.RayCast(
					StyxWoW.Me.CurrentTarget.Rotation + WoWMathHelper.DegreesToRadians(150),10f);
		}
		
		public Composite DpsHim
		{
			get
			{
				return new PrioritySelector(
					new Decorator(r => Me.CurrentTarget == null && Vord != null && Me.CurrentTarget != Vord && Add == null, new Action(r => Vord.Target())),
					new Decorator(r => Me.CurrentTarget == null || Me.CurrentTarget == Vord && Add != null, new Action(r=>Add.Target())),

					new Decorator(r => Vord != null && Vord.IsCasting && !Vord.MeIsSafelyBehind, new Action(r=>Navigator.MoveTo(CalculatePointBehindTarget()))),
					
					RoutineManager.Current.CombatBehavior
					
					);
			}
		}

		protected Composite CreateBehavior_QuestbotMain()
		{
			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, DpsHim, new ActionAlwaysSucceed())));
		}

		 #region Cleanup

        public override void OnFinished()
        {
            TreeHooks.Instance.RemoveHook("Questbot_Main", CreateBehavior_QuestbotMain());
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
        }

		#endregion
	}
}
