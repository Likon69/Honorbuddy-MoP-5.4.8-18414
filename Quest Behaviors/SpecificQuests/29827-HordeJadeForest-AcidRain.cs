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


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.AcidRain
{
	[CustomBehaviorFileName(@"SpecificQuests\29827-HordeJadeForest-AcidRain")]
	public class AcidRain : CustomForcedBehavior
	{
		private bool _isBehaviorDone;
		private Composite _root;

		public AcidRain(Dictionary<string, string> args) : base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				QuestId = 29827;
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


		public int Underneath
		{
			get
			{
				return
					ObjectManager.GetObjectsOfType<WoWUnit>()
						.Count(
							r => (r.Entry == 55707 || r.Entry == 55701) && StyxWoW.Me.CharmedUnit != null && r.Location.Distance(ModifiedLocation(StyxWoW.Me.CharmedUnit)) < 20);
			}
		}

		public WoWUnit Gutripper
		{
			get { return ObjectManager.GetObjectsOfType<WoWUnit>().Where(x => x.Entry == 55707).OrderBy(x => x.Distance).FirstOrDefault(); }
		}


		public WoWUnit Nibstabber
		{
			get { return ObjectManager.GetObjectsOfType<WoWUnit>().Where(x => x.Entry == 55701).OrderBy(x => x.Distance).FirstOrDefault(); }
		}


		public Composite Obj2
		{
			get
			{
				return new Decorator(r => Nibstabber != null && !Me.IsQuestObjectiveComplete(QuestId, 2),
					new Action(r =>
					{
						CastSpell("Throw Star");
						SpellManager.ClickRemoteLocation(Nibstabber.Location);
					}));
			}
		}


		public Composite Obj1
		{
			get
			{
				return new Decorator( r => Gutripper != null && !Me.IsQuestObjectiveComplete(QuestId, 1),
					new Action(r =>
					{
						CastSpell("Throw Star");
						SpellManager.ClickRemoteLocation(Gutripper.Location);
					}));
			}
		}


		public Composite Aoe
		{
			get { return new Decorator(r => Underneath > 5 && CanCast("Poison Blossom"), new Action(r => CastSpell("Poison Blossom"))); }
		}

		public override bool IsDone
		{
			get
			{
				return (_isBehaviorDone // normal completion
						|| !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
			}
		}

		public static WoWPoint ModifiedLocation(WoWUnit u)
		{
			return u.Location.Add(0f, 0f, -15f);
		}

		public bool CanCast(string spells)
		{
			var spell = StyxWoW.Me.PetSpells.FirstOrDefault(p => p.ToString() == spells);
			if (spell == null || spell.Cooldown)
				return false;

			return true;
		}


		public void CastSpell(string action)
		{
			var spell = StyxWoW.Me.PetSpells.FirstOrDefault(p => p.ToString() == action);
			if (spell == null)
				return;

			QBCLog.Info("[Pet] Casting {0}", action);
			Lua.DoString("CastPetAction({0})", spell.ActionBarIndex + 1);
		}


		protected Composite CreateBehavior_MainCombat()
		{
			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, Aoe, Obj1, Obj2, new ActionAlwaysSucceed())));
		}


        public override void OnFinished()
        {
            TreeHooks.Instance.RemoveHook("Combat_Main", CreateBehavior_MainCombat());
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
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
				TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_MainCombat());

				this.UpdateGoalText(QuestId);
			}
		}

		#endregion
	}
}