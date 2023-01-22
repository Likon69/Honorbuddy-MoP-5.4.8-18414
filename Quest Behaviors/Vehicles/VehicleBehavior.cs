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
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_VehicleBehavior
//
// Will control a vehicle and fire on locations/Mobs
// ##Syntax##
// QuestId: Id of the quest.
// NpcMountID: MobId of the vehicle before it is mounted.
// VehicleID: Mob of the actual Vehicle, sometimes it will be the some but sometimes it will not be.
// SpellIndex: Button bar Number starting from 1
// FireHeight: Between 0 - 99 The lower the number the closer to the ground it will be
// FireTillFinish: This is used for a few quests that the mob is flying but respawns fast, So the bot can fire in the same spot over and over.
// FireLocation Coords: Where you want to be at when you fire.
// TargetLocation Coords: Where you want to aim.
// PreviousFireLocation Coords: This should only be used if you are already inside of the vehicle when you call the behaviors again, and
//                                 should be the same coords as FireLocation on the call before it, Check the Wiki for more info or examples.
// 
#endregion

#region Examples
#endregion

#region Usings
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Buddy.Coroutines;
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

namespace Honorbuddy.Quest_Behaviors.VehicleBehavior
{
	[CustomBehaviorFileName(@"Vehicles\VehicleBehavior")]
	[CustomBehaviorFileName(@"VehicleBehavior")]  // Deprecated location--do not use
	public class VehicleBehavior : CustomForcedBehavior
	{
		public VehicleBehavior(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				QuestId = GetAttributeAsNullable("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
				QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;

				AttackButton = GetAttributeAsNullable("AttackButton", true, ConstrainAs.HotbarButton, new[] { "SpellIndex" }) ?? 0;
				FirePoint = GetAttributeAsNullable("FireLocation", false, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
				FireHeight = GetAttributeAsNullable("FireHeight", false, new ConstrainTo.Domain<int>(1, 999), null) ?? 1;
				FireUntilFinished = GetAttributeAsNullable<bool>("FireUntilFinished", false, null, new[] { "FireTillFinish" }) ?? false;
				PreviousLocation = GetAttributeAsNullable("PreviousFireLocation", false, ConstrainAs.WoWPointNonEmpty, null);
				TargetPoint = GetAttributeAsNullable("TargetLocation", false, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
				VehicleId = GetAttributeAsNullable("VehicleId", true, ConstrainAs.VehicleId, new[] { "VehicleID" }) ?? 0;
				VehicleMountId = GetAttributeAsNullable("VehicleMountId", true, ConstrainAs.VehicleId, new[] { "NpcMountId", "NpcMountID" }) ?? 0;

				StartObjectivePoint = GetAttributeAsNullable("StartObjectivePoint", false, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
				NPCIds = GetNumberedAttributesAsArray("MobId", 0, ConstrainAs.MobId, new[] { "NpcID" });
				EndPoint = GetAttributeAsNullable("EndPoint", false, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
				StartPoint = GetAttributeAsNullable("StartPoint", false, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;

				VehicleType = GetAttributeAsNullable("VehicleType", false, new ConstrainTo.Domain<int>(0, 4), null) ?? 0;
				Counter = 0;
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
		public int AttackButton { get; set; }
		public int FireHeight { get; private set; }
		public WoWPoint FirePoint { get; private set; }
		public bool FireUntilFinished { get; set; }
		public WoWPoint? PreviousLocation { get; private set; }
		public int QuestId { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }
		public WoWPoint TargetPoint { get; private set; }
		public int VehicleId { get; set; }
		public int[] NPCIds { get; set; }
		public int VehicleMountId { get; private set; }
		public WoWPoint StartObjectivePoint { get; private set; }
		public int VehicleType { get; set; }

		// Private variables for internal state
		private bool _isBehaviorDone;
		private bool _isInitialized;
		private int _pathIndex;
		private Composite _root;

		// Private properties
		private int Counter { get; set; }
		private LocalPlayer Me { get { return (StyxWoW.Me); } }

		private List<WoWUnit> NpcAttackList
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>()
									 .Where(ret => (NPCIds.Contains((int)ret.Entry)) && !ret.IsDead)
									 .OrderBy(u => u.Distance)
									 .ToList();
			}
		}

		private List<WoWUnit> NpcVehicleList
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>()
									 .Where(ret => (ret.Entry == VehicleMountId) && !ret.IsDead)
									 .OrderBy(u => u.Distance)
									 .ToList();
			}
		}
		
		private WoWPoint[] Path { get; set; }
		private CircularQueue<WoWPoint> PathCircle { get; set; }
		private WoWPoint StartPoint { get; set; }    // Start point Where Mount Is
		private WoWPoint EndPoint { get; set; }
		private WoWUnit _vehicle;
		
		private List<WoWUnit> VehicleList
		{
			get
			{
				if (PreviousLocation.HasValue)
				{
					return ObjectManager.GetObjectsOfType<WoWUnit>()
										.Where(ret => (ret.Entry == VehicleId) && !ret.IsDead)
										.OrderBy(u => u.Location.Distance(PreviousLocation.Value))
										.ToList();
				}
				return ObjectManager.GetObjectsOfType<WoWUnit>()
					.Where(ret => (ret.Entry == VehicleId) && !ret.IsDead)
					.OrderBy(u => u.Distance)
					.ToList();
			}
		}

		// Styx.Logic.Profiles.Quest.ProfileHelperFunctionsBase

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }

		WoWPoint MoveToLocation
		{
			get
			{

				Path = Navigator.GeneratePath(_vehicle.Location, FirePoint);
				_pathIndex = 0;

				while (Path[_pathIndex].Distance(_vehicle.Location) <= 3 && _pathIndex < Path.Length - 1)
					_pathIndex++;
				return Path[_pathIndex];

			}
		}

		#region Overrides of CustomForcedBehavior

		protected override Composite CreateBehavior()
		{
			return _root ?? (_root =
				new PrioritySelector(

							new Decorator(ret => (Counter > 0 && !FireUntilFinished) || (Me.QuestLog.GetQuestById((uint)QuestId) != null && Me.QuestLog.GetQuestById((uint)QuestId).IsCompleted),
								new Sequence(
									new Action(ret => TreeRoot.StatusText = "Finished!"),
									new WaitContinue(120,
										new Action(delegate
										{
											_isBehaviorDone = true;
											return RunStatus.Success;
										}))
									)),

							new Decorator(ret => !_isInitialized && VehicleType == 2,
							new Action(ret => ParsePaths())),

						new Decorator(c => !Query.IsInVehicle() && NpcVehicleList.Count == 0,
							new Action(c =>
							{

								Navigator.MoveTo(StartPoint);
								TreeRoot.StatusText = "Moving To Vehicle Location - " + " Yards Away: " + StartPoint.Distance(Me.Location);
								return RunStatus.Success;


							})
						),

						   new Decorator(c => !Query.IsInVehicle() && NpcVehicleList.Count > 0,
							new Action(c =>
							{
								if (!NpcVehicleList[0].WithinInteractRange)
								{
									Navigator.MoveTo(NpcVehicleList[0].Location);
                                    TreeRoot.StatusText = "Moving To Vehicle - " + NpcVehicleList[0].SafeName + " Yards Away: " + NpcVehicleList[0].Location.Distance(Me.Location);
								}
								else
								{
									Flightor.MountHelper.Dismount();

									NpcVehicleList[0].Interact();
									PreviousLocation = Me.Location;

								}

								return RunStatus.Success;

							})
						),
						new Decorator(c => Query.IsInVehicle() && VehicleType == 0, new ActionRunCoroutine(ctx => TypeZeroVehicleBehavior())),
						new Decorator(c => Query.IsInVehicle() && VehicleType == 1, new ActionRunCoroutine(ctx => TypeOneVehicleBehavior())),
						new Decorator(c => Query.IsInVehicle() && VehicleType == 2, new ActionRunCoroutine(ctx => TypeTwoVehicleBehavior()))
					));
		}

        private async Task<bool> TypeZeroVehicleBehavior()
        {
            while (Me.IsAlive)
            {
                if (_vehicle == null || !_vehicle.IsValid)
                {
                    _vehicle = VehicleList.FirstOrDefault();
                }

                if (_vehicle.Location.Distance(FirePoint) <= 5)
                {
                    TreeRoot.StatusText = "Firing Vehicle - " + _vehicle.SafeName + " Using Spell Index: " + AttackButton + " Height: " + FireHeight;
                    WoWMovement.ClickToMove(TargetPoint);
                    await Coroutine.Sleep(500);
                    WoWMovement.MoveStop();

                    Lua.DoString("VehicleAimRequestNormAngle(0.{0})", FireHeight);
                    Lua.DoString("CastPetAction({0})", AttackButton);
                    Counter++;
                    return true;
                }
                TreeRoot.StatusText = "Moving To FireLocation - Yards Away: " + FirePoint.Distance(_vehicle.Location);
                WoWMovement.ClickToMove(MoveToLocation);
                _vehicle.Target();
                await Coroutine.Yield();
            }
            return false;
        }

        private async Task<bool> TypeOneVehicleBehavior()
        {
            while (Me.IsAlive)
            {
                if (_vehicle == null || !_vehicle.IsValid)
                {
                    _vehicle = VehicleList.FirstOrDefault();
                }

                if (NpcAttackList.Count > 1)
                {
                    TreeRoot.StatusText = "Moving to Assault - " + NpcAttackList[0].SafeName + " Using Spell Index: " + AttackButton;
                    if (_vehicle.Location.Distance(NpcAttackList[0].Location) > 20)
                    {
                        var testfly = Flightor.CanFly;

                        if (testfly)
                        {
                            Flightor.MoveTo(NpcAttackList[0].Location);
                        }
                        else
                        {
                            TreeRoot.StatusText = "CAUTION - USING CTM!!!";
                            WoWMovement.ClickToMove(NpcAttackList[0].Location);
                        }

                        if (Me.CurrentTarget != NpcAttackList[0])
                            NpcAttackList[0].Target();

                        return true;
                    }

                    Lua.DoString("VehicleAimRequestNormAngle(0.{0})", FireHeight);
                    Lua.DoString("CastPetAction({0})", AttackButton);
                    Counter++;
                    await Coroutine.Sleep(1000);
                    return true;
                }

                if (_vehicle.Location.Distance(StartObjectivePoint) > 5)
                {
                    TreeRoot.StatusText = "Moving To Start Location - Yards Away: " + StartObjectivePoint.Distance(Me.Location);

                    var testfly = Flightor.CanFly;

                    if (testfly)
                    {
                        Flightor.MoveTo(StartObjectivePoint);
                    }
                    else
                    {
                        TreeRoot.StatusText = "CAUTION - USING CTM!!!";
                        WoWMovement.ClickToMove(StartObjectivePoint);
                    }

                    if (StyxWoW.Me.CurrentTarget != _vehicle)
                        _vehicle.Target();
                }
                await Coroutine.Yield();
            }
            return false;
        }

        private async Task<bool> TypeTwoVehicleBehavior()
        {
            while (Me.IsAlive)
            {
                if (_vehicle == null || !_vehicle.IsValid)
                {
                    _vehicle = VehicleList.FirstOrDefault();
                }

                if ((Counter > 0 && !FireUntilFinished) || (Me.QuestLog.GetQuestById((uint)QuestId) != null && Me.QuestLog.GetQuestById((uint)QuestId).IsCompleted))
                {
                    if (EndPoint.Distance(Me.Location) > 20)
                    {
                        var testfly = Flightor.CanFly;

                        if (testfly)
                        {
                            Flightor.MoveTo(EndPoint);
                        }
                        else
                        {
                            TreeRoot.StatusText = "CAUTION - USING CTM!!!";
                            WoWMovement.ClickToMove(EndPoint);
                        }

                        await Coroutine.Yield();
                        continue;
                    }

                    return true;
                }

                if (PathCircle.Count == 0)
                {
                    //Counter++;
                    ParsePaths();
                    await Coroutine.Yield();
                    continue;
                }

                if (PathCircle.Peek().Distance(Me.Location) > 5)
                {
                    var testfly = Flightor.CanFly;

                    if (testfly)
                    {
                        Flightor.MoveTo(PathCircle.Peek());
                    }
                    else
                    {
                        TreeRoot.StatusText = "CAUTION - USING CTM!!!";
                        WoWMovement.ClickToMove(PathCircle.Peek());
                    }
                    await Coroutine.Yield();
                    continue;
                }
                WoWMovement.MoveStop();
                await Coroutine.Sleep(400);

                if (NpcAttackList[0] != null)
                {
                    var testfly = Flightor.CanFly;

                    if (testfly)
                    {
                        Flightor.MoveTo(NpcAttackList[0].Location);
                    }
                    else
                    {
                        TreeRoot.StatusText = "CAUTION - USING CTM!!!";
                        WoWMovement.ClickToMove(NpcAttackList[0].Location);
                    }
                }

                WoWMovement.MoveStop();
                await Coroutine.Sleep(400);
                Lua.DoString("CastPetAction({0})", AttackButton);
                WoWMovement.MoveStop();

                PathCircle.Dequeue();

                await Coroutine.Yield();
            }
            return false;
        }

		public IEnumerable<WoWPoint> ParseWoWPoints(IEnumerable<XElement> elements)
		{
			var temp = new List<WoWPoint>();

			foreach (XElement element in elements)
			{
				XAttribute xAttribute = element.Attribute("X");
				XAttribute yAttribute = element.Attribute("Y");
				XAttribute zAttribute = element.Attribute("Z");

				float x, y, z;
				float.TryParse(xAttribute.Value, out x);
				float.TryParse(yAttribute.Value, out y);
				float.TryParse(zAttribute.Value, out z);
				temp.Add(new WoWPoint(x, y, z));
			}

			return temp;
		}

		private void ParsePaths()
		{
			var startPoint = WoWPoint.Empty;
			var path = new CircularQueue<WoWPoint>();

			foreach (WoWPoint point in ParseWoWPoints(Element.Elements().Where(elem => elem.Name == "Start")))
				startPoint = point;

			foreach (WoWPoint point in ParseWoWPoints(Element.Elements().Where(elem => elem.Name == "Path")))
				path.Enqueue(point);

			StartPoint = startPoint;
			PathCircle = path;
			_isInitialized = true;
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
