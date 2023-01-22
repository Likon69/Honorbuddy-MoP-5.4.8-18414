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
// WIKI DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_BasicVehicleBehavior
//
// QUICK DOX:
//      Allows you to Interact with (e.g., 'right-click') mobs that are nearby.
//
//  Parameters (required, then optional--both listed alphabetically):
//      MountX, MountY, MountZ:     world-coordinates where the toon should be standing to enter the vehicle
//      VehicleId:  Id of the Vehicle we seek to mount
//      X, Y, Z:    world-coordinates for the vehicle's destination.
//
//      QuestId [Default:none]:
//      QuestCompleteRequirement [Default:NotComplete]:
//      QuestInLogRequirement [Default:InLog]:
//              A full discussion of how the Quest* attributes operate is described in
//              http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
//      SpellId [Default:none]: Spell to cast (if any) once the vehicle arrives at its destination
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
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.BasicVehicleBehaviour
{
	[CustomBehaviorFileName(@"Vehicles\BasicVehicleBehaviour")]
	[CustomBehaviorFileName(@"BasicVehicleBehaviour")]  // Deprecated location--do not use
	public class BasicVehicleBehaviour : CustomForcedBehavior
	{
		public BasicVehicleBehaviour(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				QBCLog.Warning("*****\n"
							+ "* THIS BEHAVIOR IS DEPRECATED, and will be retired on July 31th 2012.\n"
							+ "*\n"
							+ "* BasicVehicleBehavior adds _no_ _additonal_ _value_ over the VehicleMover behavior.\n"
							+ "* Please update the profile to use the VehicleMover behavior."
							+ "*****");

				LocationDest = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, new[] { "Dest" }) ?? WoWPoint.Empty;
				LocationMount = GetAttributeAsNullable<WoWPoint>("Mount", true, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
				QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
				QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;
				SpellCastId = GetAttributeAsNullable<int>("SpellId", false, ConstrainAs.SpellId, null) ?? 0;
				VehicleId = GetAttributeAsNullable<int>("VehicleId", true, ConstrainAs.VehicleId, null) ?? 0;

				MountedPoint = WoWPoint.Empty;
			}

			catch (Exception except)
			{
				// Maintenance problems occur for a number of reasons.  The primary two are...
				// * Changes were made to the behavior, and boundary conditions weren't properly tested.
				// * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
				// In any case, we pinpoint the source of the problem area here, and hopefully it
				// can be quickly resolved.
				QBCLog.Exception(except); ;
				IsAttributeProblem = true;
			}
		}


		// Attributes provided by caller
		public WoWPoint LocationDest { get; private set; }
		public WoWPoint LocationMount { get; private set; }
		public int QuestId { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }
		public int SpellCastId { get; private set; }
		public int VehicleId { get; private set; }

		// Private variables for internal state
		private bool _isBehaviorDone;
		private Composite _root;
		private List<WoWUnit> _vehicleList;

		// Private properties
		private int Counter { get; set; }
		public bool IsMounted { get; set; }
		private LocalPlayer Me { get { return (StyxWoW.Me); } }
		public WoWPoint MountedPoint { get; private set; }

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }


		#region Overrides of CustomForcedBehavior

		/// <summary>
		/// A Queue for npc's we need to talk to
		/// </summary>
		//private WoWUnit CurrentUnit { get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(unit => unit.Entry == VehicleId); } }

		protected override Composite CreateBehavior()
		{
			return _root ?? (_root =
				new PrioritySelector(

					new Decorator(ret => (QuestId != 0 && Me.QuestLog.GetQuestById((uint)QuestId) != null &&
						 Me.QuestLog.GetQuestById((uint)QuestId).IsCompleted),
						new Action(ret => _isBehaviorDone = true)),

					new Decorator(ret => Counter >= 1,
						new Action(ret => _isBehaviorDone = true)),

						new PrioritySelector(

							new Decorator(ret => IsMounted != true && _vehicleList == null,
                                new ActionRunCoroutine(ctx => MoveToMountLocation())
								),

							new Decorator(ret => _vehicleList[0] != null && !_vehicleList[0].WithinInteractRange && IsMounted != true,
								new Action(ret => Navigator.MoveTo(_vehicleList[0].Location))
								),

							new Decorator(ret => StyxWoW.Me.IsMoving,
                                new ActionRunCoroutine(ctx => CommonCoroutines.StopMoving())),

							new Decorator(ret => IsMounted != true,
								new Sequence(
                                    new Action(ctx => MountedPoint = Me.Location),
                                    new Action(ctx => _vehicleList[0].Interact()),
                                    new SleepForLagDuration(),
                                    new Action(ctx => IsMounted = true),
                                    new Action(ctx => _vehicleList = ObjectManager.GetObjectsOfType<WoWUnit>()
										  .Where(ret => (ret.Entry == VehicleId) && !ret.IsDead).OrderBy(ret => ret.Location.Distance(MountedPoint)).ToList()),
									new Sleep(3000))
								),

							new Decorator(ret => IsMounted = true,
                                new ActionRunCoroutine(ctx => MoveToDestination())
								),

							new Action(ret => QBCLog.DeveloperInfo(string.Empty))
						)
					));
		}

	    private async Task MoveToMountLocation()
	    {
	        while (Me.IsAlive && Me.Location.DistanceSqr(LocationMount) > 3*3)
	        {
	            Navigator.MoveTo(LocationMount);
	            await Coroutine.Yield();
	        }
            _vehicleList = ObjectManager.GetObjectsOfType<WoWUnit>()
              .Where(ret => (ret.Entry == VehicleId) && !ret.IsDead).OrderBy(ret => ret.Location.Distance(Me.Location)).ToList();
	    }

	    private async Task MoveToDestination()
	    {
            while (Me.IsAlive && Me.Location.DistanceSqr(LocationDest) > 3 * 3)
            {
                Navigator.MoveTo(LocationDest);
                await Coroutine.Yield();
            }
            Lua.DoString("CastSpellByID(" + SpellCastId + ")");
            Counter++;
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
