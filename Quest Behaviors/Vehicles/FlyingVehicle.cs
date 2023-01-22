// Behavior originally contributed by HighVoltz.
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
// Moves to along a path in a vehicle using the specific actionbar butons until quest is complete
// ##Syntax##
//		VehicleId 
//			ID of the vehicle
//		Buttons
//			A series of numbers that represent the buttons to press in order of importance, 
//			separated by comma, for example Buttons ="2,1"     
//		NpcList
//			A comma separated list of Npcs IDs to kill for this quest. example: NpcList ="2323,4231,4324"
//		ItemId [optional; Default 0] 
//			Id of item that summons Vehicle
//		HealButton [optional; Default 0] 
//			the button number that's used to heal: 1-20
//		HealPercent [optional; Default 35] 
//			The HealButton ability is used when vehicle's health drops below this threshold
//		Path
//			The Path to follow while completing the quests objectives, This Path should loop..
//			format is x,y,z|x,y,z. example: Path = "2331.773,-5752.029,153.9199 | 2310.267,-5742.212,161.2074"
//		EndX/EndY/EndZ [alias: DropOffX/DropOffY/DropOffZ]
//			The location to move to when quest completes or drop off point for rescued NPCs.  
//			This is usually by the quest turnin NPC
//		PickUpPassengerButton [optional; Default 0; Range(1,10)] 
//			Button used to pickup NPCs durring search and rescue operations
//		DropPassengerButton: [optional; Default 0; Range(1,10)]  
//			Button used to drop NPCs durring search and rescue operations
//		SpeedButton: [optional; Default 0; Range(1,10)]  
//			Button presses a speed boost ability if specified
//		NpcScanRange: [optional; Default 10000.0] 
//			Maximum range from player to scan for NPCs
//		Precision: [optional; Default 4.0] 
//			This behavior moves on to the next waypoint when less than this distance to current waypoint.    
//
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms.VisualStyles;
using System.Xml.Linq;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Honorbuddy.Quest_Behaviors.WaitTimerBehavior;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
using WaitTimer = Styx.Common.Helpers.WaitTimer;

#endregion


namespace Honorbuddy.Quest_Behaviors.Vehicles.FlyingVehicle
{
	[CustomBehaviorFileName(@"Vehicles\FlyingVehicle")]
	public class FlyingVehicle : QuestBehaviorBase
	{
		#region Constructor and Argument Processing
		public FlyingVehicle(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				Buttons = GetAttributeAsArray<int>("Buttons", false, ConstrainAs.HotbarButton, null, null);
				DropPassengerButton = GetAttributeAsNullable<int>("DropPassengerButton", false, ConstrainAs.HotbarButton, null) ?? 0;
				var end = GetAttributeAsArray<WoWPoint>("EndPath", true, ConstrainAs.WoWPointNonEmpty, null, null);

				if (end != null && end.Any())
					EndLocation = end.Last();
				else
					EndLocation = GetAttributeAsNullable<WoWPoint>("End", true, ConstrainAs.WoWPointNonEmpty, new[] { "DropOff" }) ?? WoWPoint.Empty;

				HealButton = GetAttributeAsNullable<int>("HealButton", false, ConstrainAs.HotbarButton, null) ?? 0;
				HealPercent = GetAttributeAsNullable<double>("HealPercent", false, ConstrainAs.Percent, null) ?? 35.0;
				ItemId = GetAttributeAsNullable<int>("ItemId", false, ConstrainAs.ItemId, null) ?? 0;
				NpcList = GetAttributeAsArray<int>("NpcList", true, ConstrainAs.MobId, null, null);
				NpcScanRange = GetAttributeAsNullable<double>("NpcScanRange", false, ConstrainAs.Range, null) ?? 10000.0;
				var path = GetAttributeAsArray<WoWPoint>("Path", true, ConstrainAs.WoWPointNonEmpty, null, null);
				PickUpPassengerButton = GetAttributeAsNullable<int>("PickUpPassengerButton", false, ConstrainAs.HotbarButton, null) ??
										0;
				Precision = GetAttributeAsNullable<double>("Precision", false, new ConstrainTo.Domain<double>(2.0, 100.0), null) ??
							4.0;
				PrecisionSqr = Precision * Precision;
				SpeedButton = GetAttributeAsNullable<int>("SpeedButton", false, ConstrainAs.HotbarButton, null) ?? 0;
				VehicleId = GetAttributeAsNullable<int>("VehicleId", true, ConstrainAs.VehicleId, null) ?? 0;

				Path = new CircularQueue<WoWPoint>();
				path.ForEach(p => Path.Enqueue(p));
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

		protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
		{
			UsageCheck_DeprecatedAttribute(
				xElement,
				Args.Keys.Contains("StartPath"),
				"StartPath",
				context => "StartPath is no-longer used. You can safely remove it from the profile call to FlyingVehicle.");

			UsageCheck_DeprecatedAttribute(
				xElement,
				Args.Keys.Contains("EndPath"),
				"EndPath",
				context => "EndPath should no-longer be used. " +
							"Instead use (EndX, EndY, EndZ) or (DropOffX, DropOffY, DropOffZ)");
		}

		protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
		{
			UsageCheck_SemanticCoherency(xElement, EndLocation == WoWPoint.Zero,
				context => "You must specify a End location (EndX/EndY/EndZ) or DropOff location (DropOffX/DropOffY/DropOffZ).");
		}

		#endregion

		#region Private and Convenience variables

		// Attributes provided by caller
		private int[] Buttons { get; set; }
		private int DropPassengerButton { get; set; }
		private WoWPoint EndLocation { get; set; }
		private double HealPercent { get; set; }
		private int HealButton { get; set; }
		private int ItemId { get; set; }
		private int[] NpcList { get; set; }
		private double NpcScanRange { get; set; }
		private int PickUpPassengerButton { get; set; }
		private double Precision { get; set; }
		private int SpeedButton { get; set; }
		private int VehicleId { get; set; }

		// Private variables for internal state
		// after like 15 minutes the dragon auto dies, so we need to resummon before this
		private readonly WaitTimer _flightTimer = new WaitTimer(TimeSpan.FromMinutes(13));
		private CircularQueue<WoWPoint> Path { get; set; }
		private Composite _root;
		private double PrecisionSqr { get; set; }

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }

		#endregion

		#region Overrides of CustomForcedBehavior

		protected override Composite CreateBehavior_QuestbotMain()
		{
			return _root ?? (_root = new ActionRunCoroutine(ctx => MainLogic()));
		}

		public override void OnStart()
		{
			// Let QuestBehaviorBase do basic initializaion of the behavior, deal with bad or deprecated attributes,
			// capture configuration state, install BT hooks, etc.  This will also update the goal text.
			var isBehaviorShouldRun = OnStart_QuestBehaviorCore();

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (isBehaviorShouldRun)
			{
				_flightTimer.Reset();
				this.UpdateGoalText(QuestId);
			}
		}

		#endregion

		#region Logic

		async Task<bool> MainLogic()
		{
			if (IsDone)
				return false;

			if (!Query.IsInVehicle())
				return await GetInVehicleLogic();

			var vehicleLoc = Vehicle.Location;
			var target = FindTarget(vehicleLoc);
			// are we done with the quest or will the vehicle automatically despawn soon?
			var exitVehicle = Quest.IsCompleted || _flightTimer.IsFinished;
			// check if there's a passenger 
			var dropOffPassenger = DropPassengerButton != 0 && target != null && UnitIsRidingMyVehicle(target);

			if (exitVehicle || dropOffPassenger)
			{
				if (await MoveToEnd(exitVehicle, target))
					return true;

				if (Quest.IsCompleted)
				{
					BehaviorDone();
					return true;
				}
			}

			if (target != null && Me.CurrentTargetGuid != target.Guid)
			{
				target.Target();
				return true;
			}

			// handle target logic.
			await TargetLogic(target);
			
			if (vehicleLoc.DistanceSqr(Path.Peek()) < PrecisionSqr)
				Path.Dequeue();

			await UseSpeedBuff();

			var wayPoint = Path.Peek();		 
			Flightor.MoveTo(wayPoint);
			return true;
		}

		private async Task TargetLogic(WoWUnit target)
		{
			if (!Query.IsViable(target))
				return;

			var targetDistSqr = target.Location.DistanceSqr(Vehicle.Location);

			if (PickUpPassengerButton == 0)
			{
				TreeRoot.StatusText = string.Format("Blowing stuff up. {0} mins before resummon is required", 
					_flightTimer.TimeLeft.TotalMinutes);

				if (HealButton > 0  && targetDistSqr < 60 * 60 
					&& (Vehicle.HealthPercent <= HealPercent || Vehicle.ManaPercent <= HealPercent)
					&& UseVehicleButton(HealButton))
				{
					QBCLog.Info("Used heal button {0} on NPC:{1}", HealButton, target.SafeName);
					return;
				}

				// return when a button is used. 
				foreach (var button in Buttons)
				{
					if (UseVehicleButton(button))
						return;
				}
				return;
			}

			TreeRoot.StatusText = string.Format("Rescuing {0}", target.SafeName);
			var pickTimer = new WaitTimer(TimeSpan.FromSeconds(20));
			pickTimer.Reset();
			while (target.IsValid && target.IsAlive && !UnitIsRidingMyVehicle(target) && Query.IsInVehicle() && !pickTimer.IsFinished)
			{
				WoWPoint clickLocation = target.Location.RayCast(target.Rotation, 6);
				clickLocation.Z += 3;
				if (Vehicle.Location.DistanceSqr(clickLocation) > 3 * 3 )
				{
					Flightor.MoveTo(clickLocation);
				}
				else
				{
					if (Vehicle.IsMoving)
						await CommonCoroutines.StopMoving(string.Format("Picking up {0}", target.SafeName));
					UseVehicleButton(PickUpPassengerButton);
					if (await Coroutine.Wait(4000, () => UnitIsRidingMyVehicle(target)))
					{
						QBCLog.Info("Successfully picked up passenger {0}", target.SafeName);
						return;
					}
					QBCLog.Info("Failed to picked up passenger {0}", target.SafeName);
				}
				await Coroutine.Yield();
			}
		}

		private async Task<bool> GetInVehicleLogic()
		{
			// currently we only support using an item to get into vehicle
			if (ItemId > 0)
			{
				if (Me.IsFalling)
					return false;
				var item = Me.BagItems.FirstOrDefault(i => i.Entry == ItemId);
				if (item == null)
				{
					QBCLog.Fatal("No Item with ID {0} was found in bags", ItemId);
					return false;
				}

				item.Use();

				if (!await Coroutine.Wait(6000, Query.IsInVehicle))
				{
					QBCLog.Warning("Could not get into vehicle by using {0}.", item.SafeName);
					return false;
				}
				CycleToNearestPointInPath();
				_flightTimer.Reset();
			}
			else
			{
				QBCLog.Fatal("Not in a vehicle");
			}
			await Ascend(3000);
			return true;
		}

		private async Task<bool> MoveToEnd(bool exitVehicle, WoWUnit passenger = null)
		{
			if (!Query.IsInVehicle())
				return false;

			if (Vehicle.Location.DistanceSqr(EndLocation) >= PrecisionSqr)
			{
				await UseSpeedBuff();
				Flightor.MoveTo(EndLocation);
				return true;
			}

			if (exitVehicle)
			{
				Lua.DoString("VehicleExit()");
				await Coroutine.Sleep(2000);
				await Coroutine.Wait(20000, () => !Me.IsFalling);
				if (Me.Combat)
				{
					QBCLog.Info("Getting in vehicle to drop combat");
					return await GetInVehicleLogic();
				}
				return true;
			}

			if (!Query.IsViable(passenger))
				return false;

			if (Vehicle.IsMoving)
			{
				await CommonCoroutines.StopMoving("Dropping off passenger.");
				await CommonCoroutines.SleepForLagDuration();
			}

			UseVehicleButton(DropPassengerButton);

			await CommonCoroutines.SleepForLagDuration();
			if (!await Coroutine.Wait(6000, () => !Query.IsViable(passenger) || !UnitIsRidingMyVehicle(passenger)))
			{
				QBCLog.Warning("Failed to drop passenger off");
				return false;
			}
			if (Query.IsViable(passenger))
				Blacklist.Add(passenger, BlacklistFlags.Interact, TimeSpan.FromMinutes(10), "Rescued");
			
			// pause a sec to see if quest completes.
			if (await Coroutine.Wait(2000, () => Quest.IsCompleted))
				return true;
			
			CycleToNearestPointInPath();			
			return true;
		}

		private async Task UseSpeedBuff()
		{
			if (UseVehicleButton(SpeedButton))
				await CommonCoroutines.SleepForLagDuration();
		}

		void CycleToNearestPointInPath()
		{
			var loc = Vehicle.Location;
			Path.CycleTo(Path.OrderBy(loc.DistanceSqr).FirstOrDefault());
		}

		private PerFrameCachedValue<PlayerQuest> _quest;

		PlayerQuest Quest
		{
			get { return _quest ?? (_quest = new PerFrameCachedValue<PlayerQuest>(() => Me.QuestLog.GetQuestById((uint) QuestId))); }
		}

		private PerFrameCachedValue<WoWUnit> _vehicle;
		WoWUnit Vehicle 
		{
			get
			{
				return _vehicle ??
						(_vehicle = new PerFrameCachedValue<WoWUnit>(
							() =>
							{
								var vehicle = Me.Transport as WoWUnit ;
								if (vehicle != null && vehicle.Entry == VehicleId)
									return vehicle;
								return ObjectManager.GetObjectsOfType<WoWUnit>()
									.Where(u => u.Entry == VehicleId)
									.OrderBy(u => u.DistanceSqr)
									.FirstOrDefault();
							}));
			}
		}

		private ulong _targetGuid;
		private WoWUnit FindTarget(WoWPoint location)
		{
			WoWUnit target = null;
			if (_targetGuid != 0)
				target = ObjectManager.GetAnyObjectByGuid<WoWUnit>(_targetGuid);

			if (target != null && target.IsAlive 
				// if rescuing NPCs then we don't want to include those rescued by other players.
				&& (DropPassengerButton == 0 || !IsRescuedByOtherPlayer(target))
				&& !Blacklist.Contains(target, BlacklistFlags.Combat | BlacklistFlags.Interact))
			{
				return target;
			}

			target = ObjectManager.GetObjectsOfType<WoWUnit>()
				.Where(u => !u.IsDead && NpcList.Contains((int)u.Entry) 
					&& !Blacklist.Contains(u, BlacklistFlags.Combat | BlacklistFlags.Interact)
					// if rescuing NPCs then we don't want to include those rescued by other players.
					&& (DropPassengerButton == 0 || !IsRescuedByOtherPlayer(target))
					&& u.Location.Distance2D(location) < NpcScanRange)
				.OrderBy(u => location.Distance2DSqr(u.Location) )
				.FirstOrDefault();

			if (target != null)
			{
				_targetGuid = target.Guid;
			}

			return target;
		}

		private bool UnitIsRidingMyVehicle(WoWUnit unit)
		{
			if (!Query.IsViable(unit))
				return false;
			var myTransportGuid = Me.TransportGuid;
			if (myTransportGuid == 0)
				return false;
			return myTransportGuid == unit.TransportGuid;
		}

		#endregion

		#region Helpers

		private async Task Ascend(int timeMs)
		{
			try
			{
				WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
				await Coroutine.Sleep(timeMs);
			}
			finally
			{
				WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend);
			}
		}

		bool UseVehicleButton(int buttonIdx)
		{
			if (SpellManager.GlobalCooldown)
				return false;
			var lua = string.Format("if GetPetActionCooldown({0}) == 0 then CastPetAction({0}) return 1 end return nil", buttonIdx);
			return Lua.GetReturnVal<bool>(lua, 0);
		}

		bool IsRescuedByOtherPlayer(WoWUnit unit)
		{
			if (!Query.IsViable(unit))
				return false;
			var transportGuid = unit.TransportGuid;
			return transportGuid != 0 && transportGuid != Me.TransportGuid 
				&& ObjectManager.GetObjectsOfType<WoWPlayer>(false, false).Any(p => p.TransportGuid == transportGuid);
		}
		#endregion

	}
}
