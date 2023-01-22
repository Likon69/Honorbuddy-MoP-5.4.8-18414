// Behavior originally contributed by Natfoth.
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
// WIKI DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_FireFromTheSky
//
// QUICK DOX:
//      Used for the Dwarf Quest SI7: Fire From The Sky
//
//  Notes:
//      * Make sure to Save Gizmo.
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
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.ALessonInBravery
{
	[CustomBehaviorFileName(@"SpecificQuests\29918-FourWinds-ALessonInBravery")]
	public class FourWindsLessonInBravery : CustomForcedBehavior
	{
		public FourWindsLessonInBravery(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				QuestId = GetAttributeAsNullable("QuestId", false, ConstrainAs.QuestId(this), null) ?? 29918;
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
		private int QuestId { get; set; }
		private QuestCompleteRequirement QuestRequirementComplete { get; set; }
		private QuestInLogRequirement QuestRequirementInLog { get; set; }


		// Private variables for internal state
		private bool _isBehaviorDone;
		private Composite _root;

		// Private properties
		private const int AuraId_Mangle = 105373;
		private IEnumerable<int> AuraIds_OccupiedVehicle; 
		private LocalPlayer Me { get { return (StyxWoW.Me); } }

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }

		
		#region Overrides of CustomForcedBehavior
		public Composite DoneYet
		{
			get
			{
				return new Decorator(ret => Me.IsQuestObjectiveComplete(QuestId, 2) ,
					new Action(delegate
					{
						TreeRoot.StatusText = "Finished!";
						_isBehaviorDone = true;
						return RunStatus.Success;
					}));
			}
		}


		public WoWItem Rope
		{
			get { return Me.BagItems.FirstOrDefault(r => r.Entry == 75208); }
		}


		public WoWUnit GiantAssBird
		{         
			get
			{
				if (!Query.IsViable(_giantAssBird))
				{
					_giantAssBird =
					   (from wowObject in Query.FindMobsAndFactions(Utility.ToEnumerable<int>(56171))
						let wowUnit = wowObject as WoWUnit
						where
							Query.IsViable(wowUnit)
							&& wowUnit.IsAlive
							// Eliminate bird vehicles occupied by other players...
							&& !wowUnit.Auras.Values.Any(aura => AuraIds_OccupiedVehicle.Contains(aura.SpellId))
						orderby wowUnit.Distance
						select wowUnit)
						.FirstOrDefault();
				}

				return _giantAssBird;
			}
		}
		private WoWUnit _giantAssBird;


		public Composite GetOnBird
		{
			get
			{
				return
					new Decorator(ret => !Query.IsInVehicle(),
						new PrioritySelector(
							new Decorator(r => Query.IsViable(GiantAssBird),
								new Action(r =>
								{
									Utility.Target(GiantAssBird);
									Navigator.MoveTo(GiantAssBird.Location);
									Rope.Use();
								}))
							));
			}
		}


		public Composite KillBird
		{
			get
			{
				return new Decorator(ret => Query.IsInVehicle(),
					new PrioritySelector(
						// Get back on bid when tossed off...
						new Decorator(context => Me.HasAura(AuraId_Mangle),
							new Sequence(
								// Small variant delay to prevent looking like a bot...
								new WaitContinue(
									Delay.BeforeButtonClick,
									context => false,
									new ActionAlwaysSucceed()),
								new Action(context => { Lua.DoString("RunMacroText('/click ExtraActionButton1')");  })
							)),

						// Make certain bird stays targeted...
						new ActionFail(context => { Utility.Target(GiantAssBird, false, PoiType.Kill); }),

						// Spank bird (use backup MiniCombatRoutine if main CR doesn't attack in vehicles...
						RoutineManager.Current.CombatBuffBehavior,
						RoutineManager.Current.CombatBehavior,
                        new ActionRunCoroutine(context => UtilityCoroutine.MiniCombatRoutine())
					));
			}
		}

		protected Composite CreateBehavior_MainCombat()
		{
			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, GetOnBird,KillBird, new ActionAlwaysSucceed())));
		}


        public override void OnFinished()
        {
            TreeHooks.Instance.RemoveHook("Combat_Main", CreateBehavior_MainCombat());
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
				TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_MainCombat());

				AuraIds_OccupiedVehicle = QuestBehaviorBase.GetOccupiedVehicleAuraIds();

				this.UpdateGoalText(QuestId);
			}
		}

		#endregion
	}
}
