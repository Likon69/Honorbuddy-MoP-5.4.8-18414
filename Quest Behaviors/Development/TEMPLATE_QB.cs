// Template originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

#region Summary and Documentation
// QUICK DOX:
// TEMPLATE_QB.cs is a skeleton for creating new quest behaviors.
//
// Quest binding:
//      QuestId [REQUIRED if EscortCompleteWhen=QuestComplete; Default:none]:
//      QuestCompleteRequirement [Default:NotComplete]:
//      QuestInLogRequirement [Default:InLog]:
//              A full discussion of how the Quest* attributes operate is described in
//              http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
//      QuestObjectiveIndex [REQUIRED if EventCompleteWhen=QuestObjectiveComplete]
//          [on the closed interval: [1..5]]
//          This argument is only consulted if EventCompleteWhen is QuestObjectveComplete.
//          The argument specifies the index of the sub-goal of a quest.
//
// Tunables (ideally, the profile would _never_ provide these arguments):
//      CombatMaxEngagementRangeDistance [optional; Default: 23.0]
//          This is a work around for some buggy Combat Routines.  If a targetted mob is
//          "too far away", some Combat Routines refuse to engage it for killing.  This
//          value moves the toon within an appropriate distance to the requested target
//          so the Combat Routine will perform as expected.
//
// THINGS TO KNOW:
//
#endregion


#region Examples
// EXAMPLE:
//     <CustomBehavior File="TEMPLATE_QB" />
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Xml.Linq;


using Honorbuddy.QuestBehaviorCore;

using Styx.CommonBot.Profiles;
using Styx.TreeSharp;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.TEMPLATE_QB
{
	[CustomBehaviorFileName(@"Development\TEMPLATE_QB")]
	public class TEMPLATE_QB : QuestBehaviorBase
	{
		#region Constructor and Argument Processing
		public TEMPLATE_QB(Dictionary<string, string> args)
			: base(args)
		{
			try
			{
				// NB: Core attributes are parsed by QuestBehaviorBase parent (e.g., QuestId, NonCompeteDistance, etc)

				// Behavior-specific attributes...
			}

			catch (Exception except)
			{
				// Maintenance problems occur for a number of reasons.  The primary two are...
				// * Changes were made to the behavior, and boundary conditions weren't properly tested.
				// * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
				// In any case, we pinpoint the source of the problem area here, and hopefully it can be quickly
				// resolved.
				QBCLog.Exception(except);
				IsAttributeProblem = true;
			}
		}


		// Variables for Attributes provided by caller


		protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
		{
			//// EXAMPLE: 
			//UsageCheck_DeprecatedAttribute(xElement,
			//    Args.Keys.Contains("Nav"),
			//    "Nav",
			//    context => string.Format("Automatically converted Nav=\"{0}\" attribute into MovementBy=\"{1}\"."
			//                              + "  Please update profile to use MovementBy, instead.",
			//                              Args["Nav"], MovementBy));
		}

		protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
		{
			//// EXAMPLE:
			//UsageCheck_SemanticCoherency(xElement,
			//    (!MobIds.Any() && !FactionIds.Any()),
			//    context => "You must specify one or more MobIdN, one or more FactionIdN, or both.");
			//
			//const double rangeEpsilon = 3.0;
			//UsageCheck_SemanticCoherency(xElement,
			//    ((RangeMax - RangeMin) < rangeEpsilon),
			//    context => string.Format("Range({0}) must be at least {1} greater than MinRange({2}).",
			//                  RangeMax, rangeEpsilon, RangeMin)); 
		}
		#endregion


		#region Private and Convenience variables
		// Add what you need here...
		#endregion


		#region Overrides of CustomForcedBehavior
		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return "$Id$"; } }
		public override string SubversionRevision { get { return "$Rev$"; } }


		// CreateBehavior supplied by QuestBehaviorBase.
		// Instead, provide CreateMainBehavior definition.


		// Dispose provided by QuestBehaviorBase.


		// IsDone provided by QuestBehaviorBase.
		// Call the QuestBehaviorBase.BehaviorDone() method when you want to indicate your behavior is complete.


		public override void OnStart()
		{
			// Acquisition and checking of any sub-elements go here.
			// A common example:
			//     HuntingGrounds = HuntingGroundsType.GetOrCreate(Element, "HuntingGrounds", HuntingGroundCenter);
			//     IsAttributeProblem |= HuntingGrounds.IsAttributeProblem;
			
			// Let QuestBehaviorBase do basic initialization of the behavior, deal with bad or deprecated attributes,
			// capture configuration state, install BT hooks, etc.  This will also update the goal text.
			var isBehaviorShouldRun = OnStart_QuestBehaviorCore();

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (isBehaviorShouldRun)
			{
				// Setup settings to prevent interference with your behavior --
				// These settings will be automatically restored by QuestBehaviorBase when Dispose is called
				// by Honorbuddy, or the bot is stopped.
				//CharacterSettings.Instance.HarvestHerbs = false;
				//CharacterSettings.Instance.HarvestMinerals = false;
				//CharacterSettings.Instance.LootChests = false;
				//CharacterSettings.Instance.NinjaSkin = false;
				//CharacterSettings.Instance.SkinMobs = false;


				// Setup the BehaviorFlags as needed --
				// These settings will be automatically restored by QuestBehaviorBase when Dispose is called
				// by Honorbuddy, or the bot is stopped.
				//LevelBot.BehaviorFlags &= ~BehaviorFlags.Combat;  // Turns off the Combat Routine, if needed
			}
		}
		#endregion


		#region TargetFilters
		// HBcore runs the TargetFilter_IncludeTargets after the TargetFilter_RemoveTargets.
		//protected override void TargetFilter_IncludeTargets(List<WoWObject> incomingWowObjects, HashSet<WoWObject> outgoingWowObjects)
		//{
		//    // empty--left for child to override
		//}


		// HBcore runs the TargetFilter_RemoveTargets before the TargetFilter_IncludeTargets.
		//protected override void TargetFilter_RemoveTargets(List<WoWObject> wowObjects)
		//{
		//    // empty--left for child to override
		//}


		// When scoring targets, a higher value of TargetPriority.Score makes the target more valuable.
		//protected override void TargetFilter_WeighTargets(List<Targeting.TargetPriority> targetPriorities)
		//{
		//    // empty--left for child to override
		//}
		#endregion


		#region Main Behaviors
		protected override Composite CreateBehavior_CombatMain()
		{
			return new PrioritySelector(
				// empty--left for child to override
				);
		}


		protected override Composite CreateBehavior_CombatOnly()
		{
			return new PrioritySelector(
				// empty--left for child to override
				);
		}


		protected override Composite CreateBehavior_DeathMain()
		{
			return new PrioritySelector(
				// empty--left for child to override
				);
		}


		protected override Composite CreateBehavior_QuestbotMain()
		{
			return new PrioritySelector(
				// empty--left for child to override
				);
		}


		protected override Composite CreateMainBehavior()
		{
			return new PrioritySelector(

				// If quest is done, behavior is done...
				new Decorator(context => IsDone,
					new Action(context => { BehaviorDone(); }))
				);
		}
		#endregion
	}
}