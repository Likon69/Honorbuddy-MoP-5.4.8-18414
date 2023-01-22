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
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.DarkSkies
{
	[CustomBehaviorFileName(@"SpecificQuests\31216-DreadWastes-DarkSkies")]
	public class DarkSkies : CustomForcedBehavior
	{
		public DarkSkies(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				QuestId = 31216;
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

		public uint[] Mobs = new uint[] { 63635, 63613, 63615, 63636, 65455 };
		public uint[] Mobs2 = new uint[] { 63625, 63637 };

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

		public WoWUnit Mantid
		{
			get
			{
				return
					ObjectManager.GetObjectsOfType<WoWUnit>(true).Where(
						u => Mobs.Contains(u.Entry) && !u.IsDead).OrderBy(u => u.Distance).
						FirstOrDefault();
			}
		}
		public WoWUnit Kunchong
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>(true).Where(u => Mobs2.Contains(u.Entry) && !u.IsDead).OrderBy(u => u.Distance).FirstOrDefault();
			}
		}


		public Composite DoneYet
		{
			get
			{
				return new Decorator(ret => Me.IsQuestComplete(QuestId),
					new Action(delegate
					{
						Lua.DoString("CastPetAction(6)");
						TreeRoot.StatusText = "Finished!";
						_isBehaviorDone = true;
						return RunStatus.Success;
					}));
			}
		}


		public Composite KillOne
		{
			get
			{
				return new Decorator(r => !Me.IsQuestObjectiveComplete(QuestId, 3) && Kunchong != null,
					new Action(r =>
					{
						Lua.DoString("CastPetAction(2)");
						SpellManager.ClickRemoteLocation(Kunchong.Location);
						Lua.DoString("CastPetAction(1)");
						SpellManager.ClickRemoteLocation(Kunchong.Location);
					}));
			}
		}


		public Composite KillTwo
		{
			get
			{
				return new Decorator(r => !Me.IsQuestObjectiveComplete(QuestId, 2) && Mantid != null,
					new Action(r =>
					{
						Lua.DoString("CastPetAction(1)");
						SpellManager.ClickRemoteLocation(Mantid.Location);
					}));
			}
		}

		protected Composite CreateBehavior_QuestbotMain()
		{
			return _root ?? (_root = 
				new Decorator(ret => !_isBehaviorDone,
					new PrioritySelector(DoneYet, KillOne, KillTwo, new ActionAlwaysSucceed())));
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
