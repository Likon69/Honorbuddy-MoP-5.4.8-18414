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
// DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_ForcedDismount
//
// QUICK DOX:
//      Dismounts a toon from a mount (or Druid flying form).  If flying, the behavior will
//      descend straight down to ground/water level before conducting the dismount.
//
//  Parameters (required, then optional--both listed alphabetically):
//      MaxDismountHeight [Default: 3.0]: The maximum height above ground/water at which
//          a toon is allowed to dismount.  If the toon is higher above the ground/water
//          than this, then the behavior will descend to this level before attempting
//          dismount.
//      QuestId [Default:none]:
//      QuestCompleteRequirement [Default:NotComplete]:
//      QuestInLogRequirement [Default:InLog]:
//              A full discussion of how the Quest* attributes operate is described in
//              http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors.
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.ForcedDismount
{
	[CustomBehaviorFileName(@"ForcedDismount")]
	public class ForcedDismount : QuestBehaviorBase
	{

		#region Constructor and Argument Processing
		public ForcedDismount(Dictionary<string, string> args)
			: base(args)
		{
			try
			{
				// NB: Core attributes are parsed by QuestBehaviorBase parent (e.g., QuestId, NonCompeteDistance, etc)

				GetAttributeAs<string>("QuestName", false, ConstrainAs.StringNonEmpty, null);     // (doc only - not used)
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
		// empty, for now...

		protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
		{
			// empty, for now...
		}

		protected override void EvaluateUsage_SemanticCoherency(XElement xElment)
		{
			// empty, for now... 
		}
		#endregion


		#region Private and Convenience variables

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Rev$"); } }
		#endregion


		#region Overrides of CustomForcedBehavior
		// CreateBehavior supplied by QuestBehaviorBase.
		// Instead, provide CreateMainBehavior definition.

		// Dispose provided by QuestBehaviorBase.

		// IsDone provided by QuestBehaviorBase.
		// Call the QuestBehaviorBase.BehaviorDone() method when you want to indicate your behavior is complete.

		// OnFinished provided by QuestBehaviorBase.

		public override void OnStart()
		{
			// Let QuestBehaviorBase do basic initializaion of the behavior, deal with bad or deprecated attributes,
			// capture configuration state, install BT hooks, etc.  This will also update the goal text.
			var isBehaviorShouldRun = OnStart_QuestBehaviorCore("Dismounting");

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (isBehaviorShouldRun)
			{
				// empty, for now
			}
		}

		#endregion


		#region Main Behaviors
		protected override Composite CreateBehavior_CombatMain()
		{
			return new PrioritySelector(
				// empty, for now
				);
		}


		protected override Composite CreateBehavior_CombatOnly()
		{
			return new PrioritySelector(
				// empty, for now
				);
		}


		protected override Composite CreateBehavior_DeathMain()
		{
			return new PrioritySelector(
				// empty, for now
				);
		}


		protected override Composite CreateMainBehavior()
		{
			return new PrioritySelector(
				// If we're not mounted, nothing to do...
				new Decorator(ret => !Me.IsMounted() && !Me.IsShapeshifted(),
					new Action(delegate { BehaviorDone(); })),

                new ActionRunCoroutine(context => UtilityCoroutine.ExecuteMountStrategy(MountStrategyType.DismountOrCancelShapeshift))
			);
		}
		#endregion
	}
}
