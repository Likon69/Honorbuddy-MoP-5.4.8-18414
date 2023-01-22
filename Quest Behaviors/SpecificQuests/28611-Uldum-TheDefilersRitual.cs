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
using Styx.CommonBot.Routines;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.SpecificQuests.TheDefilersRitual
{
	[CustomBehaviorFileName(@"SpecificQuests\28611-Uldum-TheDefilersRitual")]
	public class Zakahn : CustomForcedBehavior
	{
		public Zakahn(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				Location = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
				QuestId = 28611;
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
		private WoWPoint Location { get; set; }

		// Private variables for internal state
		private bool _isBehaviorDone;
		private bool IsOnFinishedRun { get; set; }
		private Composite _root;

		private LocalPlayer Me { get { return (StyxWoW.Me); } }


		private WoWUnit Zah
		{
			get
			{
				return 
					ObjectManager.GetObjectsOfType<WoWUnit>()
					.FirstOrDefault(u => u.IsValid && (u.Entry == 49148));
			}
		}


		private List<WoWUnit> Guards
		{
			get
			{
				return 
					ObjectManager.GetObjectsOfType<WoWUnit>()
					.Where(u =>
						u.IsValid
						&& (u.Entry == 49156) 
						&& !u.IsDead)
						.ToList();
			}
		}


		private static WoWUnit Snake
		{
			get
			{
				return
					ObjectManager.GetObjectsOfType<WoWUnit>()
					.Where(u =>
						u.IsValid
						&& (u.Entry == 49159)
						&& !u.IsDead)
					.OrderBy(u => u.DistanceSqr)
					.FirstOrDefault();
			}
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


		private Composite DoPull
		{
			get
			{
				return
					new PrioritySelector(
						new Decorator(ret => RoutineManager.Current.PullBehavior != null,
							RoutineManager.Current.PullBehavior),
						new Action(c => RoutineManager.Current.Pull()));
			}
		}

		private Composite CheckSpot
		{
			get
			{
				return new Decorator(ret => !Me.Combat && (Me.Location.Distance(Location) > 1),
					new Action(ret => Navigator.MoveTo(Location)));
			}
		}


		private void SetPetMode(string action)
		{

			var spell = StyxWoW.Me.PetSpells.FirstOrDefault(p => p.ToString() == action);
			if (spell == null)
				return;

			QBCLog.Info("[Pet] Casting {0}", action);
			Lua.DoString("CastPetAction({0})", spell.ActionBarIndex + 1);

		}


		private void PullMob()
		{
			string spell = "";

			switch (Me.Class)
			{
				case WoWClass.Mage:
					spell = "Ice Lance";
					break;
				case WoWClass.Druid:
					spell = "Moonfire";
					break;
				case WoWClass.Paladin:
					spell = "Judgment";
					break;
				case WoWClass.Priest:
					spell = "Shadow Word: Pain";
					break;
				case WoWClass.Shaman:
					spell = "Flame Shock";
					break;
				case WoWClass.Warlock:
					if (Me.GotAlivePet)
						SetPetMode("Passive");

					spell = "Corruption";
					break;
				case WoWClass.DeathKnight:
					if (Me.GotAlivePet)
						SetPetMode("Passive");

					spell = "Dark Command";
					break;
				case WoWClass.Hunter:
					if (Me.GotAlivePet)
						SetPetMode("Passive");

					spell = "Arcane Shot";
					break;
				case WoWClass.Warrior:
					if (SpellManager.CanCast("Shoot"))
						spell = "Shoot";
					if (SpellManager.CanCast("Throw"))
						spell = "Throw";
					break;
				case WoWClass.Rogue:
					if (SpellManager.CanCast("Shoot"))
						spell = "Shoot";
					if (SpellManager.CanCast("Throw"))
						spell = "Throw";
					break;
				case WoWClass.Monk:
					spell = "Provoke";
					break;
			}

			if (!String.IsNullOrEmpty(spell))
			{
				SpellManager.Cast(spell);
			}
		}


		private Composite PullOne
		{
			get
			{
				return new Decorator(ret => !Me.Combat,
					new Action(delegate
					{
						Navigator.PlayerMover.MoveStop();
						Guards[0].Target();
						Guards[0].Face();
						PullMob();
					}));
			}
		}


		private Composite KillIt
		{
			get
			{
				return 
					new Decorator(ret => (Me.CurrentTarget != null
											&& Me.CurrentTarget.IsWithinMeleeRange)
										 || (Me.Class == WoWClass.Hunter),
						Bots.Grind.LevelBot.CreateCombatBehavior());
			}
		}


		private Composite KillAdds
		{
			get
			{
				return
					new Decorator(ret => Guards.Count > 0,
						new PrioritySelector(
							CheckSpot, 
							PullOne,
							KillIt,
							new ActionAlwaysSucceed()));
			}
		}



		private Composite TargetHim
		{
			get
			{
				return new Decorator(ret => (Me.CurrentTarget == null) || (Me.CurrentTarget != Zah),
					new Action(delegate
					{
						Zah.Target();
						Zah.Face();
						if (Me.GotAlivePet)
							SetPetMode("Assist");

					}));
			}
		}

		private Composite Pullhim
		{
			get { return new Decorator(ret => !Me.Combat, DoPull); }
		}


		private Composite KillSnakes
		{
			get
			{
				return new Decorator(r => Snake != null,
					new PrioritySelector(
						new Decorator(r => !Me.GotTarget || (Me.CurrentTarget != Snake),
							new Action(r => Snake.Target())),
						RoutineManager.Current.CombatBehavior
					));
			}
		}

		private Composite KillBoss
		{
			get
			{
				return new Decorator(ret => Zah != null && !Zah.IsDead,
					new PrioritySelector(
						TargetHim,
						Pullhim,
						new Decorator(ret => StyxWoW.Me.Combat,
							new PrioritySelector(
								RoutineManager.Current.HealBehavior,
								new Decorator(ret => Me.GotTarget
														&& !Me.CurrentTarget.IsFriendly
														&& !Me.CurrentTarget.IsDead,
									new PrioritySelector(
										RoutineManager.Current.CombatBuffBehavior,
										RoutineManager.Current.CombatBehavior))))

					));
			}
		}


		protected override Composite CreateBehavior()
		{
			return _root ?? (_root =
				new Decorator(ret => !_isBehaviorDone,
					new PrioritySelector(
						DoneYet,
						KillAdds,
						KillSnakes,
						KillBoss,
						new ActionAlwaysSucceed())));
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
			Targeting.Instance.IncludeTargetsFilter -= Instance_IncludeTargetsFilter;
			TreeHooks.Instance.RemoveHook("Questbot_Main", CreateBehavior());

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

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (!IsDone)
			{
				TreeHooks.Instance.InsertHook("Questbot_Main", 0, CreateBehavior());
				Targeting.Instance.IncludeTargetsFilter += Instance_IncludeTargetsFilter;

				this.UpdateGoalText(QuestId);
			}
		}
		#endregion


		#region Target Filters
		void Instance_IncludeTargetsFilter(List<WoWObject> incomingUnits, HashSet<WoWObject> outgoingUnits)
		{
			var targetGuid = StyxWoW.Me.CurrentTargetGuid;
			foreach (var unit in incomingUnits)
			{
				if (unit.Guid == targetGuid)
					outgoingUnits.Add(unit);
			}
		}
		#endregion
	}
}