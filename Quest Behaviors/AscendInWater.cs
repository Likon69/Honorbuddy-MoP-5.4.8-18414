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
// WIKI DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_BasicMoveTo
//

#region Summary and Documentation
// QUICK DOX:
//      Moves the toon to a desired location in the game world.
//      NOTE: This behavior is deprecated--use the built-in <RunTo> element instead.    
//
//  Parameters (required, then optional--both listed alphabetically):
//      X, Y, Z:  World coordinates to which the toon should move.
//
//      DestName [Default:"<X,Y,Z>"]:   a human-readable name of the location to which the toon is moving.
//
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.AscendInWater
{
	[CustomBehaviorFileName(@"AscendInWater")]
	public class AscendInWater : CustomForcedBehavior
	{
		// Private variables for internal state
		private static readonly WaitTimer MaxAscendTimer = new WaitTimer(TimeSpan.FromSeconds(30));

		private bool _isBehaviorDone;
		private Composite _root;

		#region Overrides of CustomForcedBehavior

		public override bool IsDone
		{
			get { return _isBehaviorDone; }
		}

		public override void OnStart()
		{
			// This reports problems, and stops BT processing if there was a problem with attributes...
			// We had to defer this action, as the 'profile line number' is not available during the element's
			// constructor call.
			OnStart_HandleAttributeProblem();

			MaxAscendTimer.Reset();

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (!IsDone)
			{
				TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_CombatMain());
				this.UpdateGoalText(0);
			}
		}

		#endregion

		public AscendInWater(Dictionary<string, string> args) : base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try {}

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

		// Private properties
		private LocalPlayer Me
		{
			get { return (StyxWoW.Me); }
		}

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId
		{
			get { return ("$Id$"); }
		}

		public override string SubversionRevision
		{
			get { return ("$Revision$"); }
		}

		protected Composite CreateBehavior_CombatMain()
		{
			return _root ?? (_root = new ActionRunCoroutine(ctx => MainCoroutine()));
		}

		async Task<bool> MainCoroutine()
		{
			if (IsDone)
				return false;

			if (MaxAscendTimer.IsFinished || !IsUnderWater)
			{
				// N.B. There were issues getting WoWMovement.MoveStop() calls to always register so using the lua version.
				Lua.DoString("AscendStop()");
				_isBehaviorDone = true;
				return true;
			}
			if (!Me.MovementInfo.IsAscending)
			{
				WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
				return true;
			}
			return false;
		}


		// Note: In some areas the toon can be underwater and IsSwimming reports false. This is only known to happen
		// when character is on some ocean floors and IsSwimming will correctly report 'true' once toon gets off the ocean floor 
		bool IsUnderWater
		{
			get
			{
				return Me.IsSwimming || Lua.GetReturnVal<bool>("return IsSubmerged()", 0);
			}
		}

		public override void OnFinished()
		{
			TreeRoot.GoalText = string.Empty;
			TreeRoot.StatusText = string.Empty;
			base.OnFinished();
		}
	}
}