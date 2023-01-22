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
// DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_NoControlVehicle
//
// For Vehicles you do not have to move, such as Cannons, Horses, Bombings, and even ground targeting cannons.
// ##Syntax##
// QuestId: Id of the quest.
// NpcMountID: MobId of the vehicle before it is mounted.
// VehicleId: Between 0 - 99 The lower the number the closer to the ground it will be
// TargetId, TargetId2, ...TargetIdN: Mob of the actual Vehicle, sometimes it will be the some but sometimes it will not be.
// SpellIndex: Button bar Number starting from 1 
// OftenToUse: This is used for a few quests that the mob is flying but respawns fast, So the bot can fire in the same spot over and over.
// TimesToUse: Where you want to be at when you fire.
// TypeId: Where you want to aim.
// PreviousFireLocation Coords: This should only be used if you are already inside of the vehicle when you call the behaviors again, and
//                                 should be the same coords as FireLocation on the call before it, Check the Wiki for more info or examples.
//
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Globalization;
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
using Styx.WoWInternals.WoWObjects;
using Tripper.Tools.Math;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.NoControlVehicle
{
	[CustomBehaviorFileName(@"Vehicles\NoControlVehicle")]
	[CustomBehaviorFileName(@"NoControlVehicle")]  // Deprecated location--do not use
	public class NoControlVehicle : CustomForcedBehavior
	{
		public NoControlVehicle(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				AttackButton = GetAttributeAsNullable<int>("AttackButton", true, ConstrainAs.HotbarButton, new[] { "AttackIndex", "SpellIndex" }) ?? 0;
				AttackButton2 = GetAttributeAsNullable<int>("AttackButtonSecondary", false, ConstrainAs.HotbarButton, new[] { "AttackIndexSecondary", "SpellIndexSecondary" }) ?? 0;
				GoHomeButton = GetAttributeAsNullable<int>("GoHomeButton", false, ConstrainAs.HotbarButton, new[] { "HomeIndex" }) ?? 0;
				MaxRange = GetAttributeAsNullable<double>("MaxRange", false, ConstrainAs.Range, null) ?? 1;
				MountedPoint = WoWPoint.Empty;
				NumOfTimes = GetAttributeAsNullable<int>("NumOfTimes", false, ConstrainAs.RepeatCount, new[] { "TimesToUse" }) ?? 1;
				OftenToUse = GetAttributeAsNullable<int>("OftenToUse", false, ConstrainAs.Milliseconds, null) ?? 1000;
				QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
				SpellType = GetAttributeAsNullable<int>("TypeId", false, new ConstrainTo.Domain<int>(0, 5), null) ?? 2;
				TargetIds = GetNumberedAttributesAsArray<int>("TargetId", 1, ConstrainAs.MobId, new[] { "MobId", "NpcId" });
				TargetIdsSecondary = GetNumberedAttributesAsArray<int>("TargetIdSecondary", 0, ConstrainAs.MobId, new[] { "MobIdSecondary", "NpcIdSecondary" });
				VehicleId = GetAttributeAsNullable<int>("VehicleId", false, ConstrainAs.VehicleId, null) ?? 0;
				VehicleMountId = GetAttributeAsNullable<int>("VehicleMountId", false, ConstrainAs.VehicleId, new[] { "NpcMountId", "NpcMountID" }) ?? 1;
				WaitTime = GetAttributeAsNullable<int>("WaitTime", false, ConstrainAs.Milliseconds, null) ?? 0;

				Counter = 1;
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
		public int AttackButton { get; private set; }
		public int AttackButton2 { get; private set; }
		public int GoHomeButton { get; private set; }
		public double MaxRange { get; private set; }
		public WoWPoint MountedPoint { get; private set; }
		public int OftenToUse { get; private set; }
		public int QuestId { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }
		public int SpellType { get; private set; }
		public int[] TargetIds { get; private set; }
		public int[] TargetIdsSecondary { get; private set; }
		public int NumOfTimes { get; private set; }
		public int WaitTime { get; private set; }
		public int VehicleId { get; private set; }
		public int VehicleMountId { get; private set; }

		// Private variables for internal state
		private bool _isBehaviorDone;
		private Composite _root;

		// Private properties
		private int Counter { get; set; }
		
		private LocalPlayer Me { get { return (StyxWoW.Me); } }
		private List<WoWUnit> NpcList
		{
			get
			{
				if (VehicleList.Count > 0)
				{
					return (ObjectManager.GetObjectsOfType<WoWUnit>()
											.Where(u => TargetIds.Contains((int)u.Entry)
														&& (VehicleList[0].Location.Distance(u.Location) <= MaxRange) && !u.IsDead)
											.OrderBy(u => u.Distance)
											.ToList());
				}
				return (ObjectManager.GetObjectsOfType<WoWUnit>()
										.Where(u => TargetIds.Contains((int)u.Entry) && !u.IsDead)
										.OrderBy(u => u.Distance)
										.ToList());
			}
		}
		private List<WoWUnit> NpcListSecondary
		{
			get
			{
				if (VehicleList.Count > 0)
				{
					return (ObjectManager.GetObjectsOfType<WoWUnit>()
											.Where(u => TargetIdsSecondary.Contains((int)u.Entry)
														&& (VehicleList[0].Location.Distance(u.Location) <= MaxRange) && !u.IsDead)
											.OrderBy(u => u.Distance)
											.ToList());
				}
				return (ObjectManager.GetObjectsOfType<WoWUnit>()
										.Where(u => TargetIdsSecondary.Contains((int)u.Entry) && !u.IsDead)
										.OrderBy(u => u.Distance)
										.ToList());
			}
		}
		private List<WoWUnit> NpcVehicleList
		{
			get
			{
				return (ObjectManager.GetObjectsOfType<WoWUnit>()
									  .Where(ret => (ret.Entry == VehicleMountId) && !ret.IsDead)
									  .OrderBy(u => u.Distance)
									  .ToList());
			}
		}
		private List<WoWUnit> VehicleList
		{
			get
			{
				return (ObjectManager.GetObjectsOfType<WoWUnit>()
									 .Where(ret => (ret.Entry == VehicleId) && !ret.IsDead)
									 .ToList());
			}
		}

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }

		#region Overrides of CustomForcedBehavior

		protected override Composite CreateBehavior()
		{
			return _root ??
				(_root = new PrioritySelector(
					new Decorator(c => (Counter > NumOfTimes) || Me.IsQuestComplete(QuestId),
						new Action(c =>
						{
							TreeRoot.StatusText = "Finished!";
							if (GoHomeButton > 0)
							{
								Lua.DoString("CastPetAction({0})", GoHomeButton);
							}
							_isBehaviorDone = true;
							return RunStatus.Success;
						})
					),

					new Decorator(c => NpcVehicleList.Any() && !Query.IsInVehicle(),
						new Action(c =>
						{
							if (!NpcVehicleList[0].WithinInteractRange)
							{
								Navigator.MoveTo(NpcVehicleList[0].Location);
                                TreeRoot.StatusText = "Moving To Vehicle - " + NpcVehicleList[0].SafeName + " Yards Away: " + NpcVehicleList[0].Location.Distance(Me.Location);
							}
							else
							{
								NpcVehicleList[0].Interact();
								MountedPoint = Me.Location;
							}

						})
					),
					new Decorator(c => SpellType == 1, new ActionRunCoroutine(ctx => TypeOneVehicleBehavior())),

					new Decorator(c => SpellType == 2, new ActionRunCoroutine(ctx => TypeTwoVehicleBehavior())),
					
                    new Decorator(c => Query.IsInVehicle() && SpellType == 3,
					    new PrioritySelector(
					        ret => NpcList.OrderBy(u => u.DistanceSqr).FirstOrDefault(u => Me.Transport.IsSafelyFacing(u)),
					        new Decorator(
					            ret => ret != null,
					            new PrioritySelector(
					                new Decorator(
					                    ret => Me.CurrentTarget == null || Me.CurrentTarget != (WoWUnit) ret,
					                    new Action(ret => ((WoWUnit) ret).Target())),
					                new Decorator(
					                    ret => !Me.Transport.IsSafelyFacing(((WoWUnit) ret), 10),
					                    new Action(ret => Me.CurrentTarget.Face())),
					                new Sequence(
					                    new Action(
					                        ctx =>
					                        {
					                            Vector3 v = Me.CurrentTarget.Location - StyxWoW.Me.Location;
					                            v.Normalize();
					                            Lua.DoString(
					                                string.Format(
					                                    "local pitch = {0}; local delta = pitch - VehicleAimGetAngle(); VehicleAimIncrement(delta); CastPetAction({1});",
					                                    Math.Asin(v.Z).ToString(CultureInfo.InvariantCulture),
					                                    AttackButton));
					                        }),
					                    new Sleep(WaitTime),
					                    new Action(ctx => Counter++)))))),

					new Decorator(c => SpellType == 4,
						new Action(c =>
						{
							if (!Query.IsInVehicle())
								return RunStatus.Failure;

							if (Counter > NumOfTimes && QuestId == 0 || Me.IsQuestComplete(QuestId))
							{
								Lua.DoString("VehicleExit()");
								_isBehaviorDone = true;
								return RunStatus.Success;
							}

							var target = NpcList.FirstOrDefault();

							if (target != null)
							{
								if (Me.CurrentTargetGuid != target.Guid)
									target.Target();
								WoWMovement.ClickToMove(target.Location);
								Lua.DoString("CastPetAction({0})", AttackButton);
								SpellManager.ClickRemoteLocation(target.Location);
								Counter++;
							}
							return RunStatus.Running;
						})),

				   new Decorator(c => SpellType == 5,
						new Action(c =>
						{
							if (!Query.IsInVehicle())
								return RunStatus.Failure;

							if (Counter > NumOfTimes && QuestId == 0 || Me.IsQuestComplete(QuestId))
							{
								Lua.DoString("VehicleExit()");
								_isBehaviorDone = true;
								return RunStatus.Success;
							}
							var target = NpcList.FirstOrDefault();

							if (target != null)
							{
								if (Me.CurrentTargetGuid != target.Guid)
									target.Target();
								WoWMovement.ConstantFace(Me.CurrentTargetGuid);
								Lua.DoString("CastPetAction({0})", AttackButton);
								if (QuestId == 0)
									Counter++;
							}

							var target2 = NpcListSecondary.FirstOrDefault();
							if (target2 != null)
							{
								if (Me.CurrentTargetGuid != target2.Guid)
									target2.Target();
								WoWMovement.ConstantFace(Me.CurrentTargetGuid);
								Lua.DoString("CastPetAction({0})", AttackButton);
								if (QuestId == 0)
									Counter++;
							}

							Lua.DoString("CastPetAction({0})", AttackButton2);

							return RunStatus.Running;
						}))
				));
		}

	    private async Task<bool> TypeOneVehicleBehavior()
	    {
            if (!Query.IsInVehicle())
                return false;

            while (Me.IsAlive)
            {
	            var target = NpcList.FirstOrDefault();
	            var vehicle = VehicleList.FirstOrDefault();
	            if (target == null || vehicle != null && target.Location.Distance(vehicle.Location) > 15)
	            {
	                TreeRoot.StatusText = "Waiting for Mob to Come Into Range or Appear.";
	                await Coroutine.Yield();
                    continue;
	            }
	            if (vehicle != null && target.Location.Distance(vehicle.Location) <= 15)
	            {
	                TreeRoot.StatusText = "Attacking: " + target.SafeName + ", AttackButton: " + AttackButton;
	                if (Me.CurrentTargetGuid != target.Guid)
	                    target.Target();
	                Lua.DoString("CastPetAction({0})", AttackButton);
	                await Coroutine.Sleep(WaitTime);
	                Counter++;
	                return true;
	            }
                await Coroutine.Yield();
	        }
            return false;
	    }

        private async Task<bool> TypeTwoVehicleBehavior()
        {
            if (!Query.IsInVehicle())
                return false;

            while (Me.IsAlive)
            {
                if (Counter > NumOfTimes && QuestId == 0 || Me.IsQuestComplete(QuestId))
                {
                    Lua.DoString("VehicleExit()");
                    _isBehaviorDone = true;
                    return true;
                }
                var target = NpcList.FirstOrDefault();
                if (target != null)
                {
                    await Coroutine.Sleep(OftenToUse);
                    TreeRoot.StatusText = "Attacking: " + target.SafeName + ", AttackButton: " + AttackButton + ", Times Used: " + Counter;
                    target.Target();
                    Lua.DoString("CastPetAction({0})", AttackButton);
                    SpellManager.ClickRemoteLocation(target.Location);
                    await Coroutine.Sleep(WaitTime);
                    Counter++;
                }
                await Coroutine.Yield();
            }
            return false;
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
