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
// BearsUpThere by Bobby53 
// 
// Completes the vehicle quest http://www.wowhead.com/quest=25462
// 
// To use, you must use the Ladder at <RunTo  X="5254.562" Y="-1536.917" Z="1361.341" />
// Due to how the coordinate system is relative to the vehicle once you enter, it
// is setup to only support this specific ladder.  
// 
// ##Syntax##
// QuestId: Id of the quest (default is 0)
// [Optional] QuestName: optional quest name (documentation only)
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
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.MountHyjal.BearsUpThere
{
	[CustomBehaviorFileName(@"SpecificQuests\MountHyjal\BearsUpThere")]
	public class BearsUpThere : CustomForcedBehavior
	{
		public BearsUpThere(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				QuestId = GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null) ?? 0;
				QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;
				/* */
				GetAttributeAs<string>("QuestName", false, ConstrainAs.StringNonEmpty, null);      // (doc only - not used)

                // Make certain quest is one of the ones we know how to do...
			    if (QuestId == QuestId_BearsUpThere)
			        _mobId_bearTargets = MobId_Bear;
                else if (QuestId == QuestId_ThoseBearsUpThere)
                    _mobId_bearTargets = MobId_DailyBear;
                else
                {
                    QBCLog.Fatal("This behavior can only do QuestId({0}) or QuestId({1}).  (QuestId({2}) was seen.)",
                        QuestId_BearsUpThere, QuestId_ThoseBearsUpThere, QuestId);
                    IsAttributeProblem = true;
                }
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
		public bool RunningBehavior = true;

		// Private variables for internal state


		private bool _isBehaviorDone;
	    private readonly int _mobId_bearTargets;
		private Composite _root;

		// Private properties
		private LocalPlayer Me { get { return (StyxWoW.Me); } }

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }


		//  LEVEL: -1=unknown, 0=tree top, 1=highest, 2=middle, 3=lowest
		const int LEVEL_BOTTOM = 1;
		const int LEVEL_TOP = 5;
		const int LEVEL_UNKNOWN = 0;
		int _lvlCurrent = LEVEL_UNKNOWN;

		const int AURA_CLIMBING_TREE = 74920;
		const int AURA_IN_TREE = 46598;
		const int CLIMB_UP = 74922;
		const int CLIMB_DOWN_AT_TOP = 75070;
		const int CLIMB_DOWN = 74974;
		const int CHUCK_A_BEAR = 75139;

        private const int MobId_Bear = 40240;                   // Bear mobs for main-line quest
        private const int MobId_DailyBear = 52688;              // Bear mobs for daily quest
        private const int QuestId_BearsUpThere = 25462;         // Main-line quest
        private const int QuestId_ThoseBearsUpThere = 29161;    // Daily

		/*
			RIGHT SIDE:  isontransport:True, rotation:1.356836,  degrees:77.741
			LEFT SIDE:  isontransport:True, rotation:1.612091,  degrees:92.366
			ENTRY:  isontransport:True, rotation:0.1570796,  degrees:9
		 */
		// these are values recorded from tree @ 14:33
		//  ..  when taking the right ladder (while facing tree)
		//  ..  angle while on tree level other than top is always 9
		//  ..  if you are on correct tree and correct side

		const double AIM_ANGLE = -0.97389394044876;
		const double TRAMP_RIGHT_SIDE = 77.741;
		const double TRAMP_LEFT_SIDE = 92.366;


		private async Task WaitForCurrentSpell()
		{
            await Coroutine.Wait(2000, () => !SpellManager.GlobalCooldown);
            await Coroutine.Wait(12000, () => !StyxWoW.Me.IsCasting);
		}


		private async Task ClimbUp()
		{
			// bool canCast = CanCastNow(CLIMB_UP);
			WoWPoint lastPos = Me.Location;
			// Lua.DoString("CastSpellByID({0})", CLIMB_UP);
			Lua.DoString("RunMacroText(\"/click OverrideActionBarButton1\")");
			await WaitForCurrentSpell();
			await Coroutine.Sleep(2000);

			if (Me.Location.Distance(lastPos) != 0)
			{
				QBCLog.DeveloperInfo("(Climb Up) moved +{0:F1} yds, pos: {1}", Me.Location.Distance(lastPos), Me.Location);
				if (!IsClimbingTheTree)
					_lvlCurrent = LEVEL_TOP;
				else
					_lvlCurrent++;
			}
			else
				QBCLog.DeveloperInfo("(Climb Up) no movement UP occurred");

		}

		private async Task ClimbDown()
		{
			int spellId;

			// spell id to move down is different if you are at top of tree
			if (IsClimbingTheTree)
				spellId = CLIMB_DOWN;
			else
				spellId = CLIMB_DOWN_AT_TOP;

			WoWPoint lastPos = Me.Location;
			Lua.DoString("RunMacroText(\"/click OverrideActionBarButton2\")");
			await WaitForCurrentSpell();

			// wait longer if at top due to UI skin change
			await Coroutine.Sleep(spellId == CLIMB_DOWN_AT_TOP ? 3000 : 2000);

			if (Me.Location.Distance(lastPos) != 0)
			{
				_lvlCurrent--;
				QBCLog.DeveloperInfo("(Climb Down) moved -{0:F1} yds, pos: {1}", Me.Location.Distance(lastPos), Me.Location);
			}
			else
				QBCLog.DeveloperInfo("(Climb Down) no movement DOWN occurred");
		}

		private double GetAimAngle()
		{
			return Lua.GetReturnVal<double>("return VehicleAimGetAngle()", 0);
		}

		private double GetAimAdjustment()
		{
			return GetAimAngle() - AIM_ANGLE;
		}

	    private bool NeedAimAngle { get { return Math.Abs(GetAimAdjustment()) > 0.0001; } }

	    private async Task AimAngle()
		{
			double angleAdjust = GetAimAdjustment();
			QBCLog.DeveloperInfo("(Aim Angle) adjusting current angle {0} by {1} to {2}", GetAimAngle(), angleAdjust, AIM_ANGLE);

			Lua.DoString("VehicleAimDecrement({0})", angleAdjust);

			await CommonCoroutines.SleepForLagDuration();
		}

	    private bool NeedAimDirection
	    {
	        get
	        {
	            double normRotation = TRAMP_LEFT_SIDE > TRAMP_RIGHT_SIDE ? 0 : 360;
	            if (Me.Transport.RotationDegrees < TRAMP_RIGHT_SIDE)
	                return true;

	            if ((Me.Transport.RotationDegrees + normRotation) > (TRAMP_LEFT_SIDE + normRotation))
	                return true;

	            return false;
	        }
	    }

	    private async Task<bool> AimDirection()
		{
			const double normRotation = TRAMP_LEFT_SIDE > TRAMP_RIGHT_SIDE ? 0 : 360;
			QBCLog.DeveloperInfo("(AimRotation) Trampoline Boundary - Left Edge: {0}  Right Edge: {1}", TRAMP_LEFT_SIDE, TRAMP_RIGHT_SIDE);

			WoWMovement.MovementDirection whichWay;
			string dirCmd;

			// left/right - get current direction and turn until on trampoline
			if (Me.Transport.RotationDegrees < TRAMP_RIGHT_SIDE)
			{
				whichWay = WoWMovement.MovementDirection.TurnLeft;
				dirCmd = "TurnLeft";
			}
			else if ((Me.Transport.RotationDegrees + normRotation) > (TRAMP_LEFT_SIDE + normRotation))
			{
				whichWay = WoWMovement.MovementDirection.TurnRight;
				dirCmd = "TurnRight";
			}
			else // if (whichWay == WoWMovement.MovementDirection.None)
			{
				QBCLog.DeveloperInfo("(AimRotation) Done, Ending Rotation: {0}", Me.Transport.RotationDegrees);
				return false;
			}

			QBCLog.DeveloperInfo("(AimRotation) Current Rotation: {0} - {1}", Me.Transport.RotationDegrees, whichWay.ToString().ToUpper());
#if WOWMOVEMENT_TIMED_TURNS_STOPFAILING
			WoWMovement.Move(whichWay, TimeSpan.FromMilliseconds( 10));
			WoWMovement.MoveStop(whichWay);
			// loop until we actually move
			while ( 0.001 > (currRotation - Me.Transport.RotationDegrees ))
			   await CommonCoroutines.SleepForLagDuration();
#elif WOWMOVEMENT_TURNS_STOPFAILING
			WoWMovement.Move(whichWay);
			await Coroutine.Sleep(10);
			WoWMovement.MoveStop(whichWay);
			// loop until we actually move
			while ( 0.001 > (currRotation - Me.Transport.RotationDegrees ))
			   await CommonCoroutines.SleepForLagDuration();
#else
            // doing LUA calls these because WoWMovement API doesn't stop turning quickly enough
			Lua.DoString(dirCmd + "Start()");
		    await Coroutine.Sleep(10);
			Lua.DoString(dirCmd + "Stop()");
#endif
		    return true;
		}

		private async Task ChuckBear()
		{
			QBCLog.DeveloperInfo("(Chuck-A-Bear) threw bear at trampoline");
			// bool canCast = CanCastNow(CHUCK_A_BEAR);
			// Lua.DoString("CastSpellByID({0})", CHUCK_A_BEAR);
			Lua.DoString("RunMacroText(\"/click OverrideActionBarButton4\")");
			await WaitForCurrentSpell();
			await Coroutine.Sleep(4000);
		}

	    private bool IsBearCubInBags
	    {
	        get
	        {
#if USE_OM
			    WoWItem item = ObjectManager.GetObjectsOfType<WoWItem>().Find(unit => unit.Entry == 54439);
#else
	            WoWItem item = Me.BagItems.Find(unit => unit.Entry == 54439);
#endif
	            return item != null;
	        }
	    }

	    private async Task<bool> LootClosestBear()
		{
			List<WoWUnit> bears =
				   (from o in ObjectManager.ObjectList
					where o is WoWUnit
					let unit = o.ToUnit()
					where
                        unit.Entry == _mobId_bearTargets
						&& (15 < unit.WorldLocation.Distance(Me.Transport.WorldLocation))
					orderby
						unit.WorldLocation.Distance(Me.Transport.WorldLocation) ascending
					select unit
						).ToList();

			foreach (WoWUnit bear in bears)
			{
				StyxWoW.SleepForLagDuration();

				bear.Target();  // target so we can use LUA func
				bool bChkLua = Lua.GetReturnVal<bool>("return CheckInteractDistance(\"target\", 1)", 0);

				bool bChkInt = bear.WithinInteractRange;
				if (!bChkLua && !bChkInt)
					continue;

				bear.Interact();
				await WaitForCurrentSpell();
				await CommonCoroutines.SleepForLagDuration();

				if (IsBearCubInBags)
				{
					QBCLog.Info("(Loot Bear) grabbed a bear to throw");
					return true;
				}
			    await Coroutine.Yield();
			}

			QBCLog.DeveloperInfo("(Loot Bear) no bear at level {0}", _lvlCurrent);
			return false;
		}

	    public bool InTree
	    {
	        get
	        {
	            RunningBehavior = Me.Transport != null;
	            return Me.Transport != null || IsClimbingTheTree;
	        }
	    }

        public bool IsClimbingTheTree { get { return Me.HasAura(AURA_CLIMBING_TREE); } }

	    public bool DoWeHaveQuest
	    {
	        get
	        {
	            PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint) QuestId);
	            return quest != null;
	        }
	    }

		#region Overrides of CustomForcedBehavior

		protected override Composite CreateBehavior()
		{
            return _root ?? (_root = new ActionRunCoroutine(ctx => MainCoroutine()));
		}

	    private async Task<bool> MainCoroutine()
	    {

	        if (Me.IsCasting || SpellManager.GlobalCooldown)
	            return true;

	        // check if we left tree/vehicle
	        if (!InTree)
	        {
	            _isBehaviorDone = true;
	            return true;
	        }

	        // is quest abandoned or complete?
	        //  ..  move down until we auto-exit vehicle
	        if (!DoWeHaveQuest || Me.IsQuestComplete(QuestId))
	        {
	            await ClimbDown();
	            return true;
	        }

	        // level unknown and already at top?  set to top then
	        if (_lvlCurrent == LEVEL_UNKNOWN && !IsClimbingTheTree)
	        {
	            _lvlCurrent = LEVEL_TOP;
	            return true;
	        }

	        // level unknown?
	        //  ..  move to top and establish known level
	        if (_lvlCurrent == LEVEL_UNKNOWN)
	        {
	            await ClimbUp();
	            return true;
	        }

	        // have a bear in inventory?
	        if (IsBearCubInBags)
	        {
	            //  ..  below top?  move up
	            if (_lvlCurrent != LEVEL_TOP)
	            {
	                await ClimbUp();
	                return true;
	            }
	            //  ..  aim trajectory angle
	            if (NeedAimAngle)
	            {
	                await AimAngle();
	                return true;
	            }
	            //  ..  aim direction (left/right)
	            if (NeedAimDirection && await AimDirection())
	                return true;
	            //  ..  throw                           
	            await ChuckBear();
	            return true;
	        }

	        // at top with no bear?
	        //  ..  move down
	        if (_lvlCurrent == LEVEL_TOP)
	        {
	            await ClimbDown();
	            return true;
	        }

	        // lootable bears here?
	        //  ..  loot a bear
	        if (!IsBearCubInBags && await LootClosestBear())
	            return true;

	        // can we move down without leaving vehicle?
	        if (_lvlCurrent > LEVEL_BOTTOM)
	        {
	            await ClimbDown();
	            return true;
	        }

	        // move up
	        if (_lvlCurrent < LEVEL_TOP)
	        {
	            await ClimbUp();
	            return true;
	        }
	        return false;
	    }

		public override bool IsDone
		{
			get
			{
				return (!RunningBehavior && (_isBehaviorDone     // normal completion
						|| !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete)));
			}
		}


        public override void OnFinished()
        {
            // Clean up unmanaged resources (if any) here...
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
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
				if (DoWeHaveQuest && !Me.IsQuestComplete(QuestId) && !InTree)
				{
					QBCLog.Fatal("==================================================================\n"
								+ "NOT IN TREE!!!  ENTER TREE TO USE CUSTOM BEHAVIOR\n"
								+ "==================================================================");
				}

				else
				{
					this.UpdateGoalText(QuestId);
				}
			}
		}

		#endregion
	}
}

