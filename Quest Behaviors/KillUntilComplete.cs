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
// This is only used when you get a quest that Says, Kill anything x times. Or on the chance the wowhead ID is wrong
// ##Syntax##
// QuestId: Id of the quest.
// MobId, MobId2, ...MobIdN: Mob Values that it will kill.
// X,Y,Z: The general location where theese objects can be found
// 
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;

using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.KillUntilComplete
{
	[CustomBehaviorFileName(@"KillUntilComplete")]
	public class KillUntilComplete : CustomForcedBehavior
	{
		public KillUntilComplete(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				Location = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
				MobIds = GetNumberedAttributesAsArray<int>("MobId", 1, ConstrainAs.MobId, new[] { "NpcID" });
				QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
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
		public WoWPoint Location { get; private set; }
		public int[] MobIds { get; private set; }
		public int QuestId { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }

		// Private variables for internal state
		private bool _isBehaviorDone;
		private Composite _root;

		// Private properties
		private LocalPlayer Me { get { return (StyxWoW.Me); } }
		private List<WoWUnit> MobList
		{
			get
			{
				return (ObjectManager.GetObjectsOfType<WoWUnit>()
							   .Where(u => MobIds.Contains((int)u.Entry) && !u.IsDead)
							   .OrderBy(u => u.Distance).ToList());
			}
		}

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }


		WoWSpell RangeSpell
		{
			get
			{
				switch (Me.Class)
				{
					case Styx.WoWClass.Druid:
						return SpellManager.Spells["Wrath"];
					case Styx.WoWClass.Hunter:
						return SpellManager.Spells["Arcane Shot"];
					case Styx.WoWClass.Mage:
						return SpellManager.Spells["Frostfire Bolt"];
					case Styx.WoWClass.Priest:
						return SpellManager.Spells["Smite"];
					case Styx.WoWClass.Shaman:
						return SpellManager.Spells["Lightning Bolt"];
					case Styx.WoWClass.Warlock:
						return SpellManager.Spells["Shadow Bolt"];
					default: // should never get to here but adding this since the compiler complains
						return SpellManager.Spells["Auto Attack"]; ;
				}
			}
		}

		private bool IsRanged
		{
			get
			{
				return (((Me.Class == WoWClass.Druid) &&
						 (SpellManager.HasSpell("BalanceSpell") || SpellManager.HasSpell("RestoSpell")))
						||
						((Me.Class == WoWClass.Shaman) &&
						 (SpellManager.HasSpell("ElementalSpell") || SpellManager.HasSpell("RestoSpell")))
						||
						(Me.Class == WoWClass.Hunter) ||
						(Me.Class == WoWClass.Mage) ||
						(Me.Class == WoWClass.Priest) ||
						(Me.Class == WoWClass.Warlock));
			}
		}

		private int Range
		{
			get
			{
				return (IsRanged ? 29 : 3);
			}
		}


		#region Overrides of CustomForcedBehavior

		protected override Composite CreateBehavior()
		{
			return _root ?? (_root =
				new PrioritySelector(

							new Decorator(ret => (Me.QuestLog.GetQuestById((uint)QuestId) != null && Me.QuestLog.GetQuestById((uint)QuestId).IsCompleted),
								new Sequence(
									new Action(ret => TreeRoot.StatusText = "Finished!"),
									new WaitContinue(120,
										new Action(delegate
										{
											_isBehaviorDone = true;
											return RunStatus.Success;
										}))
									)),

						   new Decorator(ret => MobList.Count == 0,
								new Sequence(
										new Action(ret => TreeRoot.StatusText = "Moving To Location - X: " + Location.X + " Y: " + Location.Y),
										new Action(ret => Navigator.MoveTo(Location)),
										new Sleep(300)
									)
								),

						   new Decorator(ret => MobList.Count > 0 && !Me.IsCasting,
								new Sequence(
									new DecoratorContinue(ret => MobList[0].Location.Distance(Me.Location) > Range || !MobList[0].InLineOfSight,
										new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Moving to Mob - " + MobList[0].SafeName + " Yards Away " + MobList[0].Location.Distance(Me.Location)),
											new Action(ret => Navigator.MoveTo(MobList[0].Location)),
											new Sleep(300)
											)
									),
									new DecoratorContinue(ret => MobList[0].Location.Distance(Me.Location) <= Range && MobList[0].InLineOfSight,
										new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Attacking Mob - " + MobList[0].SafeName + " With Spell: " + RangeSpell.Name),
										new Action(ret => WoWMovement.MoveStop()),
										new Action(ret => MobList[0].Target()),
										new Action(ret => MobList[0].Face()),
										new Sleep(200),
										new Action(ret => SpellManager.Cast(RangeSpell)),
										new Sleep(300)
											))
									))
					));
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
	}
}

