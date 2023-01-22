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
// Completes the quest http://www.wowhead.com/quest=25299
// This behavior completes the quest by correctly responding to
// 10 yes/no questions by checking the toons question aura.
// 
// Requires you to already be in position at the quest give Instructor Mylva X="4524.021" Y="-4731.176" Z="887.9406"
// 
// ##Syntax##
// QuestId: Id of the quest (default is 0)
// [Optional] QuestName: optional quest name (documentation only)
// 
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
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.MountHyjal.MentalTraining
{
	[CustomBehaviorFileName(@"SpecificQuests\MountHyjal\MentalTraining")]
	public class MentalTraining : CustomForcedBehavior
	{
		public MentalTraining(Dictionary<string, string> args)
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
				GetAttributeAs<string>("QuestName", false, ConstrainAs.StringNonEmpty, null);            // (doc only - not used)
				QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;
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

		// Private properties
		private LocalPlayer Me { get { return (StyxWoW.Me); } }

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }


		public bool HasAura(WoWUnit unit, int auraId)
		{
			WoWAura aura = (from a in unit.Auras
							where a.Value.SpellId == auraId
							select a.Value).FirstOrDefault();
			return aura != null;
		}


		#region Overrides of CustomForcedBehavior

		protected override Composite CreateBehavior()
		{
			return _root ?? (_root =
				new PrioritySelector(

					// check if we have finished 10 questions (marked complete)
					new Decorator(ret => Me.IsQuestComplete(QuestId),
						new PrioritySelector(
							new Decorator(ret => Me.HasAura("Mental Training"),
								new Sequence(
									new Action( ret => QBCLog.Info("Mental Training complete - exiting Orb")),
									new Action( ret => Lua.DoString("RunMacroText(\"/click OverrideActionBarButton4\")")),
									CreateWaitForLagDuration()
									)
								),
							new Action(ret => _isBehaviorDone = true)
						)
					),

					// if we don't have vehicle buff, use Orb of Ascension
					new Decorator(ret => !Me.HasAura("Mental Training"),
						new Sequence( 
							new Action( delegate {
								QBCLog.Info("Using Orb of Ascension");
								WoWItem orb = ObjectManager.GetObjectsOfType<WoWItem>().Where(u => u.Entry == 52828).FirstOrDefault();
								if (orb == null)
									{ QBCLog.Fatal("Quest item \"Orb of Ascension\" not in inventory."); }

								orb.Use(true);
								return RunStatus.Success;
								}),
							new WaitContinue( 1, ret => Me.HasAura("Mental Training"), new ActionAlwaysSucceed())
							)
						),

					// if we have YES aura 74008, then click yes
					new Decorator(ret => HasAura(Me, 74008),
						new Sequence(
							new Action(ret => QBCLog.Info("Answering YES")),
							new WaitContinue( TimeSpan.FromMilliseconds(500), ret => false, new ActionAlwaysSucceed()),
							new Action(ret => Lua.DoString("RunMacroText(\"/click OverrideActionBarButton1\")")),
							new WaitContinue( 1, ret => !HasAura(Me, 74008), new ActionAlwaysSucceed())
							)
						),

					// if we have NO aura 74009, then click no
					new Decorator(ret => HasAura(Me, 74009),
						new Sequence(
							new Action(ret => QBCLog.Info("Answering NO")),
							new WaitContinue(TimeSpan.FromMilliseconds(500), ret => false, new ActionAlwaysSucceed()),
							new Action(ret => Lua.DoString("RunMacroText(\"/click OverrideActionBarButton2\")")),
							new WaitContinue(1, ret => !HasAura(Me, 74009), new ActionAlwaysSucceed())
							)
						),

					new Action(delegate
					{
						return RunStatus.Success;
					})
				)
			);
		}


        public override void OnFinished()
        {
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
				this.UpdateGoalText(QuestId);
			}
		}

		#endregion

		/// <summary>
		/// This is meant to replace the 'SleepForLagDuration()' method. Should only be used in a Sequence
		/// </summary>
		/// <returns></returns>
		public static Composite CreateWaitForLagDuration()
		{
			return new WaitContinue(TimeSpan.FromMilliseconds((StyxWoW.WoWClient.Latency * 2) + 150), ret => false, new ActionAlwaysSucceed());
		}

	}
}
