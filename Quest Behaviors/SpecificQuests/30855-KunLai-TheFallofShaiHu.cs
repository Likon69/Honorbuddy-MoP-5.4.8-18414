// Behavior originally contributed by LastCoder
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
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.TheFallofShaiHu
{
	[CustomBehaviorFileName(@"SpecificQuests\30855-KunLai-TheFallofShaiHu")]
	[Obsolete("DO NOT USE. THIS QB WILL BE REMOVED IN THE NEAR FUTURE")]
	class KunLaiTheFallofShaiHu : CustomForcedBehavior
	{

		#region Construction
		public KunLaiTheFallofShaiHu(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				QuestId = GetAttributeAsNullable("QuestId", false, ConstrainAs.QuestId(this), null) ?? 30855;
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
		#endregion


		#region Variables

		// Variables filled by construction
		public int QuestId { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }

		// Internal variables
		private bool _isBehaviorDone = false;
		private Composite _root;
		private Composite _behaviorTreeCombatHook;
		private LocalPlayer Me { get { return (StyxWoW.Me); } }

		// Static constant variables
		public static List<uint> ExplosiveHatredIds = new List<uint> { 61070 };
		public static uint ShaiHuId = 61069;
		public static WoWPoint Waypoint = new WoWPoint(2104.215, 314.8302, 475.4525);

		public List<WoWUnit> Enemys
		{
			get
			{
				return (ObjectManager.GetObjectsOfType<WoWUnit>()
									 .Where(u => u.FactionId == 2550 && !u.Elite && !u.IsDead && u.Distance < 199)
									 .OrderBy(u => u.Distance).ToList());
			}
		}

		public List<WoWUnit> ExplosiveHatredEnemies
		{
			get
			{
				return (ObjectManager.GetObjectsOfType<WoWUnit>()
									 .Where(u => ExplosiveHatredIds.Contains(u.Entry) && !u.IsDead)
									 .OrderBy(u => u.Distance).ToList());
			}
		}

		public WoWUnit ExplosiveHatredEnemy
		{
			get
			{
				return ExplosiveHatredEnemies.FirstOrDefault();
			}
		}

		public WoWUnit ShaiHuNPC
		{
			get
			{
				return (ObjectManager.GetObjectsOfType<WoWUnit>()
									 .Where(u => ShaiHuId == u.Entry && !u.IsDead).OrderBy(u => u.Distance)).FirstOrDefault();
			}
		}

		public Composite CreateCombatBehavior()
		{
			return new PrioritySelector(
				  new Decorator(cond => ShaiHuNPC != null && !ShaiHuNPC.HasAura(118633) && Me.CurrentTarget != ShaiHuNPC,
					new Action(r =>
					{
						BotPoi.Current = new BotPoi(ShaiHuNPC, PoiType.Kill);
						ShaiHuNPC.Target();
						return RunStatus.Failure;
					})),
				  new Decorator(cond => ShaiHuNPC != null && ShaiHuNPC.HasAura(118633),
					new PrioritySelector(
								new Decorator(cond => !ExplosiveHatredEnemy.IsTargetingMeOrPet,
									new Action(r =>
									{
										TreeRoot.StatusText = "Pulling explosive hatred using no combat move...";
									    Navigator.MoveTo(ExplosiveHatredEnemy.Location);
										return RunStatus.Failure;
									})),
								new Decorator(cond => ExplosiveHatredEnemy.IsTargetingMeOrPet && ExplosiveHatredEnemy.Location.Distance(ShaiHuNPC.Location) > 10,
									new Action(r =>
									{
                                        Navigator.MoveTo(ShaiHuNPC.Location);
										return RunStatus.Failure;
									})),
							  new Decorator(cond => ExplosiveHatredEnemy.Location.Distance(ShaiHuNPC.Location) < 10,
								  new Action(r =>
								  {
									  BotPoi.Current = new BotPoi(ExplosiveHatredEnemy, PoiType.Kill);
									  ExplosiveHatredEnemy.Target();
									  return RunStatus.Failure;
								  })))),

						new Decorator(cond => ShaiHuNPC != null && !ShaiHuNPC.HasAura(118633),      
							new Action(r =>
							{
								if (BotPoi.Current.Entry != ShaiHuId)
								{
									BotPoi.Current = new BotPoi(ShaiHuNPC, PoiType.Kill);
								}
								Navigator.MoveTo(ShaiHuNPC.Location);
								return RunStatus.Failure;
							})));
		}

		#endregion


		#region CustomForcedBehavior Override

		protected override Composite CreateBehavior()
		{
			return _root ?? (_root = CreateMainBehavior());
		}

		public Composite CreateMainBehavior()
		{
			return new PrioritySelector(
				new PrioritySelector(new Decorator(
				ret => !_isBehaviorDone,
				new PrioritySelector(
					new Decorator(ret => Me.QuestLog.GetQuestById((uint)QuestId) != null && Me.QuestLog.GetQuestById((uint)QuestId).IsCompleted,
						 new Sequence(
							new Action(ret => TreeRoot.StatusText = "Finished!"),
								new WaitContinue(120,
										new Action(delegate
										{
											_isBehaviorDone = true;
											return RunStatus.Success;
										}))
									)))))
 
									 
				);
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
				_behaviorTreeCombatHook = CreateCombatBehavior();
				TreeHooks.Instance.InsertHook("Combat_Only", 0, _behaviorTreeCombatHook);

				this.UpdateGoalText(QuestId);

				WoWMovement.ClickToMove(new WoWPoint(2175.019, 380.8854, 476.0461));
			}
		}

        public override void OnFinished()
        {
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
        }

		#endregion
	}
}
