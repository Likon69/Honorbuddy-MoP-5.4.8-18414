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

using Styx.CommonBot.Coroutines;

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
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


// ReSharper disable once CheckNamespace
namespace Honorbuddy.Quest_Behaviors.SpecificQuests.ScentOfBattle
{
	[CustomBehaviorFileName(@"SpecificQuests\27811-AllyTwilightHighlands-ScentOfBattle")]
	public class ScentOfBattle : CustomForcedBehavior
	{
		public ScentOfBattle(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				QuestId = 27811;
				QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = QuestInLogRequirement.InLog;
				MobIds = new uint[] { 50635, 50638, 50643, 50636 };
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
		private uint[] MobIds { get; set; }
		private int QuestId { get; set; }
		private QuestCompleteRequirement QuestRequirementComplete { get; set; }
		private QuestInLogRequirement QuestRequirementInLog { get; set; }


		// Private variables for internal state
		private bool _isBehaviorDone;
		private bool IsOnFinishedRun { get; set; }
		private Composite _root;
		private readonly Stopwatch _doingQuestTimer = new Stopwatch();

		private LocalPlayer Me { get { return (StyxWoW.Me); }  }


		private Composite DoneYet
		{
			get
			{
				return new Decorator(ret => Me.IsQuestComplete(QuestId) || _doingQuestTimer.ElapsedMilliseconds >= 30000,
					new Action(delegate
					{                                                       
						TreeRoot.StatusText = "Finished! or time is Over";
						_isBehaviorDone = true;
						return RunStatus.Success;
					}));
			}
		}


		private WoWUnit Normal
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>()
				.Where(u =>
					u.IsValid
					&& (u.Entry == 46968)
					&& u.IsAlive
					&& !u.IsMoving)
				.OrderBy(u => u.DistanceSqr)
				.FirstOrDefault();
			}
		}


		private WoWUnit Pinned
		{
			get
			{
				return 
					ObjectManager.GetObjectsOfType<WoWUnit>()
					.Where(u =>
						u.IsValid
						&& (u.Entry == 46969)
						&& u.IsAlive
						&& u.HasAura(87490))
					.OrderBy(u => u.DistanceSqr)
					.FirstOrDefault();
			}
		}


		private WoWUnit Pin
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>()
					.FirstOrDefault(p =>
						p.IsValid
						&& p.IsAlive
						&& (p.Entry == 46975)
						&& (p.Location.Distance(Pinned.Location) <= (20)));

			}
		}


		private Composite TryCast(int spellId, ProvideBoolDelegate requirements = null)
		{
			requirements = requirements ?? (context => true);

			return new Decorator(context => SpellManager.CanCast(spellId) && requirements(context),
				new Action(context =>
				{
					QBCLog.DeveloperInfo("MiniCombatRoutine used {0}", Utility.GetSpellNameFromId(spellId));
					SpellManager.Cast(spellId);
				}));
		}


		private Composite DoDps
		{
			get
			{
				return
					new PrioritySelector(
						new Decorator(ret => Me.Combat && Me.Mounted,
							new Sequence(
								new Action(ret =>WoWMovement.MoveStop()),
								new Action(ret =>Lua.DoString("Dismount()")),
								new Decorator(ret => Me.Class == WoWClass.Druid,
									new Action(ret => Lua.DoString("RunMacroText('/cancelaura Flight Form')"))),
                                new ActionRunCoroutine(context => CommonCoroutines.Dismount())
						)),
						//new Decorator(ret => RoutineManager.Current.CombatBehavior != null, RoutineManager.Current.CombatBehavior),
						//new Action(c => RoutineManager.Current.Combat()),
						new Decorator(ret => Me.CurrentTarget != null && Me.CurrentTarget.IsDead,
							new Sequence(
								new Action(ret => Blacklist.Add(Me.CurrentTarget, BlacklistFlags.Combat, TimeSpan.FromSeconds(180000))),
								new Action(ret => Me.ClearTarget())
						)),
						new Decorator(ret => Me.CurrentTarget != null && Me.CurrentTarget.Location.Distance(Me.Location) > 4,
							new Action(c => Navigator.MoveTo(Me.CurrentTarget.Location))),
						new Decorator(ret => !Me.IsAutoAttacking,
							new Action(c => Lua.DoString("StartAttack()"))),
						new Decorator(context => !WoWMovement.ActiveMover.IsSafelyFacing(Me.CurrentTarget, 30),
							new ActionFail(context => Me.SetFacing(Me.CurrentTarget.Location))),
						new Decorator(ret => Me.CurrentTarget.Location.Distance(Me.Location) <= 4,
							new Switch<WoWClass>(context => Me.Class,
								new SwitchArgument<WoWClass>(WoWClass.DeathKnight,
									new PrioritySelector(
										TryCast(49998)      // Death Strike: http://wowhead.com/spell=49998
								)),
								new SwitchArgument<WoWClass>(WoWClass.Druid,
									new PrioritySelector(
										TryCast(5176, context => !Me.HasAura(768)),      // Wrath: http://wowhead.com/spell=5176
										TryCast(768, context => !Me.HasAura(768)),       // Cat Form: http://wowhead.com/spell=768
										TryCast(1822),      // Rake: http://wowhead.com/spell=1822
										TryCast(22568),     // Ferocious Bite: http://wowhead.com/spell=22568
										TryCast(33917)      // Mangle: http://wowhead.com/spell=33917
								)),
								new SwitchArgument<WoWClass>(WoWClass.Hunter,
									new PrioritySelector(
										TryCast(3044),      // Arcane Shot: http://wowhead.com/spell=3044
										TryCast(56641)      // Steady Shot: http://wowhead.com/spell=56641
								)),
								new SwitchArgument<WoWClass>(WoWClass.Mage,
									new PrioritySelector(
										TryCast(44614),     // Frostfire Bolt: http://wowhead.com/spell=44614
										TryCast(126201),    // Frostbolt: http://wowhead.com/spell=126201
										TryCast(2136)       // Fire Blast: http://wowhead.com/spell=2136
								)),
								new SwitchArgument<WoWClass>(WoWClass.Monk,
									new PrioritySelector(
										TryCast(100780),    // Jab: http://wowhead.com/spell=100780
										TryCast(100787)     // Tiger Palm: http://wowhead.com/spell=100787
								)),
								new SwitchArgument<WoWClass>(WoWClass.Paladin,
									new PrioritySelector(
										TryCast(35395),     // Crusader Strike: http://wowhead.com/spell=35395
										TryCast(20271)      // Judgment: http://wowhead.com/spell=20271
								)),
								new SwitchArgument<WoWClass>(WoWClass.Priest,
									new PrioritySelector(
										TryCast(589, context => !Me.CurrentTarget.HasAura(589)),      // Shadow Word: Pain: http://wowhead.com/spell=589
										TryCast(15407),     // Mind Flay: http://wowhead.com/spell=15407
										TryCast(585)        // Smite: http://wowhead.com/spell=585
								)),
								new SwitchArgument<WoWClass>(WoWClass.Rogue,
									new PrioritySelector(
										TryCast(2098),      // Eviscerate: http://wowhead.com/spell=2098
										TryCast(1752)       // Sinster Strike: http://wowhead.com/spell=1752
								)),
								new SwitchArgument<WoWClass>(WoWClass.Shaman,
									new PrioritySelector(
										TryCast(17364),     // Stormstrike: http://wowhead.com/spell=17364
										TryCast(403),       // Lightning Bolt: http://wowhead.com/spell=403
										TryCast(73899)      // Primal Strike: http://wowhead.com/spell=73899
								)),
								new SwitchArgument<WoWClass>(WoWClass.Warlock,
									new PrioritySelector(
										TryCast(686)        // Shadow Bolt: http://wowhead.com/spell=686
								)),
								new SwitchArgument<WoWClass>(WoWClass.Warrior,
									new PrioritySelector(
										TryCast(78),        // Heroic Strike: http://wowhead.com/spell=78
										TryCast(34428),     // Victory Rush: http://wowhead.com/spell=34428
										TryCast(23922),     // Shield Slam: http://wowhead.com/spell=23922
										TryCast(20243)      // Devastate: http://wowhead.com/spell=20243
						))))
				);
			}
		}


		private Composite GetInRange
		{
			get
			{
				return
					new Decorator(r => Normal.Distance > 1,
						new Action(r=>Flightor.MoveTo(Normal.Location)));
			}
		}


		private Composite GetInRangep
		{
			get
			{
				return
					new Decorator(r => Pinned.Distance > 1,
						new Action(r => Flightor.MoveTo(Pinned.Location)));
			}
		}


		private Composite Interact
		{
			get
			{
				return
					new Decorator(r => Normal.Distance <= 1,
						new Action(delegate
						{
							Normal.Interact();
							Lua.DoString("SelectGossipOption(1);");
						}));
			}
		}


		private Composite NormalGryphon
		{
			get 
			{
				return 
					new Decorator(r => Normal != null && !Me.Combat,
						new PrioritySelector(
							GetInRange,
							Interact));
			}
		}


		private Composite KillPegs
		{
			get
			{
				return 
					new Decorator(r => Pin != null && !Me.Combat,
						new Action(delegate
						{
							Pin.Target();
							Pin.Interact();
						}));
			}
		}


		private Composite PinnedGryphon
		{
			get
			{
				return
					new Decorator(r => Pinned != null && !Me.Combat,
						new PrioritySelector(GetInRangep, KillPegs));
			}
		}


		private Composite Combat
		{
			get
			{
				return new Decorator(r => Me.Combat,DoDps);
			}
		}


		private Composite CreateBehavior_QuestbotMain()
		{
			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone,
				new PrioritySelector(
					DoneYet,
					Combat,
					NormalGryphon,
					PinnedGryphon,
					new ActionAlwaysSucceed()
			)));
		}


		#region Overrides of CustomForcedBehavior

		public override bool IsDone
		{
			get
			{
				return (_isBehaviorDone // normal completion
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