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


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.FortuneAndGlory
{
	[CustomBehaviorFileName(@"SpecificQuests\27748-Uldum-FortuneAndGlory")]
	public class FortuneAndGlory : CustomForcedBehavior
	{

		public FortuneAndGlory(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				QuestId = 27748;
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


		// Private properties
		private LocalPlayer Me
		{
			get { return (StyxWoW.Me); }
		}


		#region Overrides of CustomForcedBehavior

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

		private const uint ObsidianColossusId = 46646;

		public WoWUnit Enemey
		{
			get
			{
				return
					ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.IsAlive && u.Entry == ObsidianColossusId);
			}
		}

		public Composite TargetHim
		{
			get
			{
				return new PrioritySelector(ctx => Enemey, 
					new Decorator(ctx => Me.CurrentTarget != (WoWUnit)ctx, 
						new Action(ctx => ((WoWUnit)ctx).Target())));
			}
		}


		public Composite WaitAround
		{
			get
			{
				return new PrioritySelector(ctx => Enemey,
					new Decorator(ctx => ctx != null && ((WoWUnit)ctx).HealthPercent > 50, new ActionAlwaysSucceed()));
			}
		}

		public Composite Kick
		{
			get
			{
				return new PrioritySelector( ctx => Me.CurrentTarget,
					new Decorator(ctx => ctx != null && ((WoWUnit)ctx).IsCasting && ((WoWUnit)ctx).CastingSpellId == 87990 && SpellManager.CanCast(InteruptSpellName),
					new Action(ctx => SpellManager.Cast(InteruptSpellName))));
			}
		}

		protected override Composite CreateBehavior()
		{
			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, WaitAround,TargetHim, Kick)));
		}


        public override void OnFinished()
        {
            TreeHooks.Instance.RemoveHook("Combat_Main", CreateBehavior());
            Targeting.Instance.IncludeTargetsFilter -= Instance_IncludeTargetsFilter;
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
        }


		public override bool IsDone
		{
			get
			{
				return (_isBehaviorDone // normal completion
						|| !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
			}
		}


		public string InteruptSpellName
		{
			get
			{
				switch (Me.Class)
				{
					case WoWClass.Mage:
						return "Counterspell";
					case WoWClass.Paladin:
						return "Rebuke";
					case WoWClass.Shaman:
						return "Wind Shear";
					case WoWClass.DeathKnight:
						return "Mind Freeze";
					case WoWClass.Hunter:
						return "Silencing Shot";
					case WoWClass.Warrior:
						return "Pummel";
					case WoWClass.Rogue:
						return "Kick";
				}
				return String.Empty;
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
				TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior());
				Targeting.Instance.IncludeTargetsFilter += Instance_IncludeTargetsFilter;

				this.UpdateGoalText(QuestId);
			}
		}


		void Instance_IncludeTargetsFilter(List<WoWObject> incomingUnits, HashSet<WoWObject> outgoingUnits)
		{
			foreach (var unit in incomingUnits.OfType<WoWUnit>())
			{
				if (unit.Entry == ObsidianColossusId && unit.HealthPercent <= 50)
					outgoingUnits.Add(unit);
			}
		}

		#endregion
	}
}