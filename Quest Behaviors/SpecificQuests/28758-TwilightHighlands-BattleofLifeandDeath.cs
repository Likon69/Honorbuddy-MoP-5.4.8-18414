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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.BattleofLifeandDeath
{
	[CustomBehaviorFileName(@"SpecificQuests\28758-TwilightHighlands-BattleofLifeandDeath")]
	public class LifeAndDeath : CustomForcedBehavior
	{
		public LifeAndDeath(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				QuestId = 28758;
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
		public int QuestId { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }

		// Private variables for internal state
		private bool _isBehaviorDone;
		private Composite _root;
		private const uint TwilightShadowdrakeId = 49873;
		private const uint VermillionDefenderId = 49872;
		private const uint VermillionVanguardId = 49914;
		static readonly WoWPoint QuestLocation = new WoWPoint(-3924.175, -3475.402, 640.4075);

		// Private properties
		private LocalPlayer Me
		{
			get { return (StyxWoW.Me); }
		}

		#region Overrides of CustomForcedBehavior

		protected Composite CreateBehavior_QuestbotMain()
		{
			return new ActionRunCoroutine(ctx => MainCoroutine());
		}

		async Task<bool> MainCoroutine()
		{
			var activeMover = WoWMovement.ActiveMover;
			if (!IsDone && activeMover != null && activeMover.Entry == VermillionVanguardId)
			{
				bool shouldHeal = activeMover.HealthPercent < 50;
				var target =
					ObjectManager.GetObjectsOfType<WoWUnit>()
						.Where(u => u.IsAlive && u.Entry == (shouldHeal ? VermillionDefenderId : TwilightShadowdrakeId))
						.OrderBy(u => u.DistanceSqr).FirstOrDefault();

				// if there are no targets then move to quest area.
				if (target == null)
				{
					if (activeMover.Location.DistanceSqr(QuestLocation) > 10*10)
					{
						Flightor.MoveTo(QuestLocation);
					}
					return true;
				}
				// target the NPC
				if (Me.CurrentTargetGuid != target.Guid)
				{
					target.Target();
					await Coroutine.Sleep((int)Delay.AfterInteraction.TotalMilliseconds);
					return true;
				}

				var targetDistSqr = activeMover.Location.DistanceSqr(target.Location);
				// move to target
				if (targetDistSqr > 60 * 60 || !target.InLineOfSpellSight)
				{
					Flightor.MoveTo(QuestLocation);
					return true;
				}
				// face if necessary
				if (!activeMover.IsSafelyFacing(target))
				{
					target.Face();
					await Coroutine.Sleep((int)Delay.LagDuration.TotalMilliseconds);
					return true;
				}

				// heal if needed
				if (shouldHeal)
				{
					Lua.DoString("CastPetAction(2);");
					await Coroutine.Sleep((int)Delay.AfterWeaponFire.TotalMilliseconds);
				}
				else
				{			
					// fireblast: 60 yd range
					if (targetDistSqr > 30*30 || target.HealthPercent > 30)
					{
						Lua.DoString("CastPetAction(1);");
						await Coroutine.Sleep((int)Delay.AfterWeaponFire.TotalMilliseconds);
					}
					else
					{
						// Finishing Strike: 30 yd range
						Lua.DoString("CastPetAction(3);");
						await Coroutine.Sleep((int)Delay.AfterWeaponFire.TotalMilliseconds);
					}
				}
				return true;
			}
			return false;
		}

		public override bool IsDone
		{
			get
			{
				return (_isBehaviorDone // normal completion
						|| !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
			}
		}

		public override void OnStart()
		{
			// This reports problems, and stops BT processing if there was a problem with attributes...
			// We had to defer this action, as the 'profile line number' is not available during the element's
			// constructor call.
			OnStart_HandleAttributeProblem();

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (!IsDone)
			{
				TreeHooks.Instance.InsertHook("Questbot_Main", 0, CreateBehavior_QuestbotMain());

				this.UpdateGoalText(QuestId);
			}
		}

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