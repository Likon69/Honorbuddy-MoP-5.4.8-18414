// Behavior originally contributed by mastahg.
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
using System.Diagnostics;
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


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.SalhetTheTactician
{
	[CustomBehaviorFileName(@"SpecificQuests\28277-Uldum-SalhetTheTactician")]
	public class Lions : CustomForcedBehavior
	{
		public Lions(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				QuestId = 28277;
				QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = QuestInLogRequirement.InLog;
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


		// Attributes provided by caller
		private int QuestId { get; set; }
		private QuestCompleteRequirement QuestRequirementComplete { get; set; }
		private QuestInLogRequirement QuestRequirementInLog { get; set; }

		// Private variables for internal state
		private bool _isBehaviorDone;
		private bool IsOnFinishedRun { get; set; }
		private Composite _root;
		private readonly Stopwatch _doingQuestTimer = new Stopwatch();

		private LocalPlayer Me { get { return (StyxWoW.Me); } }


		private List<WoWUnit> lions
		{
			get
			{
				return
					ObjectManager.GetObjectsOfType<WoWUnit>()
					.Where(u =>
						u.IsValid
						&& (u.Entry == 48169)
						&& !u.IsDead)
					.ToList();
			}
		}


		private static List<WoWUnit> MobSalhet
		{
			get
			{
				return
					ObjectManager.GetObjectsOfType<WoWUnit>()
					.Where(u =>
						u.IsValid
						&& (u.Entry == 48237))
					.OrderBy(ret => ret.DistanceSqr)
					.ToList();
			}
		}


		private List<WoWUnit> Enemies
		{
			get
			{
				return 
					ObjectManager.GetObjectsOfType<WoWUnit>()
					.Where(u =>
						u.IsValid
						&& (u.Entry == 48199 || u.Entry == 48209)
						&& !u.IsDead)
					.OrderBy(u => u.DistanceSqr)
					.ToList();
			}
		}


		private int MobCountAtLocation(WoWPoint point, float radius, params uint[] mobIds)
		{
			return ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
				.Count(u =>
					u.IsValid
					&& u.IsAlive
					&& mobIds.Contains(u.Entry)
					&& (u.DistanceSqr <= (radius * radius)));
		}


		private Composite DoneYet
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


		private Composite MoveEm
		{
			get
			{
				return new Action(ret => UsePetAbility("Move"));
			}
		}


		private void UsePetAbility(string action)
		{

			var spell = StyxWoW.Me.PetSpells.FirstOrDefault(p => p.ToString() == action);
			if (spell == null)
				return;

			QBCLog.Info("[Pet] Casting {0}", action);
			Lua.DoString("CastPetAction({0})", spell.ActionBarIndex + 1);
			if (action == "Move")
				SpellManager.ClickRemoteLocation(Enemies[0].Location);
		}


		private Composite KeepClose
		{
			get
			{
				return
					new Decorator(ret => (lions.Count > 0)
										&& (Enemies.Count > 0)
										&& (MobCountAtLocation(lions[0].Location, 15, 48199, 48209) < 1)
										&& (Me.PetSpells[0].Cooldown == false),
						MoveEm);
			}
		}


		private Composite Swipe
		{
			get
			{
				return
					new Decorator(ret => (lions.Count > 0)
										&& (MobCountAtLocation(lions[0].Location, 20, 48199,48209) >= 4)
										&& (Me.PetSpells[2].Cooldown == false),
						new Action(ret => UsePetAbility("Claw Flurry")));
			}
		}

		private Composite Fear
		{
			get
			{
				return new Decorator(ret => (lions.Count > 0)
											&& (MobCountAtLocation(lions[0].Location, 20, 48199, 48209) >= 3)
											&& (Me.PetSpells[1].Cooldown == false),
						new Action(ret => UsePetAbility("Fierce Roar")));
			}
		}

		private Composite CreateBehavior_QuestbotMain()
		{
			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone,
				new PrioritySelector(
					new Decorator(ret => _doingQuestTimer.ElapsedMilliseconds >= 180000,
						new Sequence(
							new Action(ret => _doingQuestTimer.Restart()),
							new Action(ret => Lua.DoString("VehicleExit()")),
							new Sleep(4000),
							new Action(ret => MobSalhet[0].Interact()),
							new Sleep(500),
							new Action(ret => Lua.DoString("SelectGossipOption(1)"))
					)),
					DoneYet,
					KeepClose,
					Swipe,
					Fear,
					new ActionAlwaysSucceed()
			)));
		}


		#region Overrides of CustomForcedBehavior

		public override bool IsDone
		{
			get
			{
				return (_isBehaviorDone     // normal completion
						|| !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
			}
		}


		public override void OnFinished()
		{
			// Defend against being called multiple times (just in case)...
			if (IsOnFinishedRun)
				{ return; }

			// Clean up resources...
			TreeHooks.Instance.RemoveHook("Questbot_Main", CreateBehavior_QuestbotMain());

			TreeRoot.GoalText = string.Empty;
			TreeRoot.StatusText = string.Empty;

			// QuestBehaviorBase.OnFinished() will set IsOnFinishedRun...
			base.OnFinished();
			IsOnFinishedRun = true;
		}
		
		
		public override void OnStart()
		{
			// This reports problems, and stops BT processing if there was a problem with attributes...
			// We had to defer this action, as the 'profile line number' is not available during the element's
			// constructor call.
			OnStart_HandleAttributeProblem();
			_doingQuestTimer.Start();

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (!IsDone)
			{
				TreeHooks.Instance.InsertHook("Questbot_Main", 0, CreateBehavior_QuestbotMain());

				this.UpdateGoalText(QuestId);
			}
		}
		#endregion
	}
}