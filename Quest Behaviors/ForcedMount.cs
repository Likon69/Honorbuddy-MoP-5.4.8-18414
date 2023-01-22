// Behavior originally contributed by Unknown.
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
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_ForcedMount
//
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.ForcedMount
{
	[CustomBehaviorFileName(@"ForcedMount")]
	public class ForcedMount : CustomForcedBehavior
	{
		public enum ForcedMountType
		{
			Ground,
			Flying,
			Water,
		}


		public ForcedMount(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				MountType = GetAttributeAsNullable<ForcedMountType>("MountType", false, null, null) ?? ForcedMountType.Ground;
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
		public ForcedMountType MountType { get; private set; }
		public int QuestId { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }

		// Private variables for internal state
		private bool _isBehaviorDone;
		private Composite _root;

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }

		private async Task<bool> MountForFlying()
		{
		    if (!Flightor.CanFly)
		        return false;

			if (StyxWoW.Me.Class == WoWClass.Druid && (SpellManager.HasSpell("Flight Form") || SpellManager.HasSpell("Swift Flight Form")))
			{
				if (SpellManager.CanCast("Swift Flight Form"))
				{ SpellManager.Cast("Swift Flight Form"); }

				else if (SpellManager.CanCast("Flight Form"))
				{ SpellManager.Cast("Flight Form"); }
			}

			else
			{
				Mount.FlyingMounts.First().CreatureSpell.Cast();
			    await Coroutine.Wait(3000, () => StyxWoW.Me.Mounted);
			}

			// Hop off the ground. Kthx
			await Coroutine.Sleep(2500);
		    try
		    {
		        Navigator.PlayerMover.Move(WoWMovement.MovementDirection.JumpAscend);
		        await Coroutine.Sleep(250);
		    }
		    finally
		    {
                Navigator.PlayerMover.MoveStop();
		    }
		    return true;
		}


		private Composite CreateActualBehavior()
		{
		    return new PrioritySelector(
		        new Decorator(
		            ret => MountType == ForcedMountType.Ground,
		            new Action(ret => Mount.MountUp(() => WoWPoint.Zero))),

		        new Decorator(
		            ret => MountType == ForcedMountType.Water && Mount.UnderwaterMounts.Count != 0 && StyxWoW.Me.IsSwimming,
		            new Action(ret => Mount.UnderwaterMounts.First().CreatureSpell.Cast())),

		        new Decorator(
		            ret => MountType == ForcedMountType.Flying,
                    new ActionRunCoroutine(ctx => MountForFlying()))
				);
		}


		#region Overrides of CustomForcedBehavior

		protected override Composite CreateBehavior()
		{
			if (_root == null)
			{
				_root = new Sequence(
					CreateActualBehavior(),
					new Action(ret => _isBehaviorDone = true));
			}
			return _root;
		}

		public override bool IsDone
		{
			get
			{
				return (_isBehaviorDone     // normal completion
						|| !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete) || StyxWoW.Me.Mounted);
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
				this.UpdateGoalText(QuestId, "Mounting for " + MountType.ToString() + " travel");
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
