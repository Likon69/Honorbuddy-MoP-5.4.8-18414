// Behavior originally contributed by Bobby53.
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
// Completes the quest http://www.wowhead.com/quest=25310
// by using the item to enter a vehicle then casting
// its attack and shield abilities as needed to defeat the target
// 
// Note: you must already be within 100 yds of MobId when starting
// 
// ##Syntax##
// QuestId: Id of the quest (default is 0)
// MobId:  Id of the mob to kill
// [Optional] QuestName: optional quest name (documentation only)
// 
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.MountHyjal.GreaterOfTwoEvils
{
	[CustomBehaviorFileName(@"SpecificQuests\MountHyjal\GreaterOfTwoEvils")]
	public class GreaterOfTwoEvils : CustomForcedBehavior
	{
		public GreaterOfTwoEvils(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				QuestId = GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null) ?? 0;
				/* */
				GetAttributeAs<string>("QuestName", false, ConstrainAs.StringNonEmpty, null);      //  (doc only - not used)
				MobId = GetAttributeAsNullable<int>("MobId", true, ConstrainAs.MobId, new[] { "NpcId" }) ?? 0;
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
		public int MobId { get; private set; }
		public int QuestId { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }

		// Private variables for internal state
		private bool _isBehaviorDone;
		private static RunStatus _lastStateReturn = RunStatus.Success;
		private static int _lineCount = 0;
		private Composite _root;

		// Private properties
		private LocalPlayer Me { get { return (StyxWoW.Me); } }

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }


		public void Log(string format, params object[] args)
		{
			// following linecount hack is to stop dup suppression of Log window
			QBCLog.Info(format + (++_lineCount % 2 == 0 ? "" : " "), args);
		}

		public void DLog(string format, params object[] args)
		{
			// following linecount hack is to stop dup suppression of Log window
			QBCLog.DeveloperInfo(format + (++_lineCount % 2 == 0 ? "" : " "), args);
		}

		private WoWUnit Target
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>()
									   .Where(u => u.Entry == MobId && !u.IsDead)
									   .OrderBy(u => u.Distance).FirstOrDefault();
			}
		}


		#region Overrides of CustomForcedBehavior

		protected Composite CreateBehavior_QuestbotMain()
		{
			return _root ?? (_root =
				new Decorator(ret => !_isBehaviorDone,
					new PrioritySelector(
						new Decorator(ret => Me.IsQuestComplete(QuestId),
							new PrioritySelector(
								new Decorator(ret => Me.HasAura("Flame Ascendancy"),
									new Sequence( 
										new Action( ret => DLog("Quest complete - cancelling Flame Ascendancy")),
										new Action( ret => Lua.DoString("RunMacroText(\"/cancelaura Flame Ascendancy\")")),
										new SleepForLagDuration()
										)
									),
								new Sequence(
									new Action( ret => _isBehaviorDone = true ),
                                    new SleepForLagDuration()
									)
								)
							),

						// loop waiting for target only if no buff
						new Decorator(ret => Target == null,
                            // Using sequence since it'll return 'Success' after SleepForLagDuration ends
                            new Sequence(new SleepForLagDuration())
						),

						// loop waiting for CurrentTarget only if no buff
						new Decorator(ret => Target != Me.CurrentTarget,
                            new Sequence(
                                new Action(ctx => Target.Target()),
                                new SleepForLagDuration())
						),

						// use item to get buff (enter vehicle)
						new Decorator(ret => !Me.HasAura("Flame Ascendancy"),
                            new ActionRunCoroutine(ctx => UseTalismanOfFlameAscendancy())
						),

						new Decorator(ret => Target.Distance > 5,
							new Action(delegate
							{
								DLog("Moving towards target");
								Navigator.MoveTo(Target.Location);
								return RunStatus.Success;
							})
						),

						new Decorator(ret => Target.Distance <= 5 && Me.IsMoving,
							new Action(delegate
							{
								DLog("At target, so stopping");
								WoWMovement.MoveStop();
								return RunStatus.Success;
							})
						),

						new Decorator(ret => !SpellManager.GlobalCooldown && !Blacklist.Contains(2, BlacklistFlags.Combat) && !Me.Auras.ContainsKey("Flame Shield"),
							new Action(delegate
							{
								Log("Cast Flame Shield");
								Lua.DoString("RunMacroText(\"/click OverrideActionBarButton2\")");
								Blacklist.Add(2, BlacklistFlags.Combat, TimeSpan.FromMilliseconds(6000));
								return RunStatus.Success;
							})
						),

						new Decorator(ret => !SpellManager.GlobalCooldown && !Blacklist.Contains(1, BlacklistFlags.Combat),
							new Action(delegate
							{
								Log("Cast Attack");
								Lua.DoString("RunMacroText(\"/click OverrideActionBarButton1\")");
								Blacklist.Add(1, BlacklistFlags.Combat, TimeSpan.FromMilliseconds(1500));
								return RunStatus.Success;
							})
						),

						new Action(delegate
						{
							DLog("Waiting for Cooldown");
							return _lastStateReturn;
						})
					)
				)
			);
			
		}

	    async Task UseTalismanOfFlameAscendancy()
	    {
            WoWItem item = ObjectManager.GetObjectsOfType<WoWItem>().FirstOrDefault(i => i != null && i.Entry == 54814);
            if (item == null)
            {
                QBCLog.Fatal("Quest item \"Talisman of Flame Ascendancy\" not in inventory.");
                TreeRoot.Stop();
                return;
            }

            Log("Use: {0}", item.SafeName);
            item.Use(true);
	        await CommonCoroutines.SleepForLagDuration();
	    }

        public override void OnFinished()
        {
            TreeHooks.Instance.RemoveHook("Questbot_Main", CreateBehavior_QuestbotMain());
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
        }


		public override bool IsDone
		{
			get
			{
				return (_isBehaviorDone     // normal completion
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

		/// <summary>
		/// This is meant to replace the 'SleepForLagDuration()' method. Should only be used in a Sequence
		/// </summary>
		/// <returns></returns>
		public static Composite CreateWaitForLagDuration()
		{
			return new WaitContinue(TimeSpan.FromMilliseconds((StyxWoW.WoWClient.Latency * 2) + 150), ret => false, new ActionAlwaysSucceed());
		}

		#endregion
	}
}
