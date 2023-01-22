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
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.UndyingTwilight
{
	[CustomBehaviorFileName(@"SpecificQuests\26875-Deepholm-UndyingTwilight")]
	public class UndyingTwilight : CustomForcedBehavior
	{

		public UndyingTwilight(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				QuestId = 26875; //GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null) ?? 0;
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
				return new Decorator(ret => Me.IsQuestComplete(QuestId) && !Me.Combat,
					new Action(delegate
					{
						TreeRoot.StatusText = "Finished!";
						_isBehaviorDone = true;
						return RunStatus.Success;
					}));
			}
		}

		public void PullMob()
		{
			string spell = "";

			switch (Me.Class)
			{
				case WoWClass.Mage:
					if (Me.GotAlivePet)
						SetPetMode("Passive");

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
					if (Me.GotAlivePet)
						SetPetMode("Passive");
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
					 spell = "Icy Touch";
					 if (!SpellManager.CanCast(spell) && SpellManager.CanCast("Death Coil"))
						 spell = "Death Coil";

				   
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

			}

			if (!String.IsNullOrEmpty(spell) && SpellManager.CanCast(spell))
			{
				SpellManager.Cast(spell);
			}


		}

		public void SetPetMode(string action)
		{

			var spell = StyxWoW.Me.PetSpells.FirstOrDefault(p => p.ToString() == action);
			if (spell == null)
				return;

			QBCLog.Info("[Pet] Casting {0}", action);
			Lua.DoString("CastPetAction({0})", spell.ActionBarIndex + 1);
			
		}

		public List<WoWUnit> Tagged
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.IsHostile && u.IsAlive && u.TaggedByMe).OrderBy(u => u.Distance).ToList();
			}
		}

		public WoWUnit Rager
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == 44683 && u.IsAlive && u.HealthPercent < 50).OrderBy(u => u.Distance).FirstOrDefault();
			}
		}

		public WoWUnit RagerTagged
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == 44683 && u.IsAlive && u.TaggedByMe).OrderBy(u => u.Distance).FirstOrDefault();
			}
		}

		public WoWUnit AttackingMe
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.CurrentTarget == Me).OrderBy(u => u.Distance).FirstOrDefault();
			}
		}

		public WoWUnit NotTagged
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry != 44683 && u.IsHostile && u.IsAlive && !u.TaggedByMe && u.CurrentTarget != null && u.CurrentTarget != Me && u.HealthPercent < 50).OrderBy(u => u.Distance).FirstOrDefault();
			}
		}


		public Composite PullOne
		{
			get
			{
				return new Action(delegate
				{
					Navigator.PlayerMover.MoveStop();
				   Rager.Target();
				   Rager.Face();
					PullMob();
				});
			}
		}

		public Composite PullOther
		{
			get
			{
				return new Action(delegate
				{
					Navigator.PlayerMover.MoveStop();
					NotTagged.Target();
					NotTagged.Face();
					PullMob();
				});
			}
		}



		public Composite KillAttacker
		{
			get
			{
				return new Action(delegate
				{
					Navigator.PlayerMover.MoveStop();
					AttackingMe.Target();
					AttackingMe.Face();
					PullMob();
				});
			}
		}


		public Composite RagerStuff
		{
			get
			{
				return new Decorator(ret => !Me.IsQuestObjectiveComplete(QuestId, 2) && RagerTagged == null && Rager != null,
					PullOne);
			}
		}



		public Composite KillAttackers
		{
			get
			{
				return new Decorator(r => Me.Combat && AttackingMe != null, KillAttacker);
			}
		}


		public Composite OtherStuff
		{
			get
			{
				return new Decorator(r => !Me.IsQuestObjectiveComplete(QuestId, 1) && Tagged.Count < 3 && NotTagged != null,
					PullOther);
			}
		}


		
		 //WoWPoint endspot = new WoWPoint(1076.7,455.7638,-44.20478);
	   // WoWPoint spot = new WoWPoint(1109.848,462.9017,-45.03053);
		WoWPoint spot = new WoWPoint(1104.14,467.4733,-44.5488);
		
		public Composite StayClose
		{
			get
			{
				return new Sequence( new ActionDebugString("Too far, moving back to location"),new Decorator(r => Me.Location.Distance(spot) > 5, new Action(r=>WoWMovement.ClickToMove(spot))));
			}
		}


		protected Composite CreateBehavior_QuestbotMain()
		{
			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet,KillAttackers, StayClose,RagerStuff, OtherStuff, new ActionAlwaysSucceed())));
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
		#endregion
	}
}