// Behavior originally contributed by Kickazz006
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
// This behavior is for killing Thane noobface in Grizzly Hills (Horde 12259 and Alliance 12255) 
// Code was taken from Shak
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Frames;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.AllyTheThaneofVoldrune
{
	[CustomBehaviorFileName(@"SpecificQuests\12259-GrizzlyHills-HordeTheThaneofVoldrune")]
	public class q12259 : CustomForcedBehavior
	{
		private WoWPoint endloc = new WoWPoint(2805.055, -2488.745, 47.76864);

		private readonly WoWPoint _flamebringerLocation = new WoWPoint(2793.088, -2506.125, 47.61626);
		private bool _isDone;
		private Composite _root;


		#region Overrides of CustomForcedBehavior

		public override bool IsDone
		{
			get { return _isDone; }
		}

		public override void OnStart()
		{
			OnStart_HandleAttributeProblem();
			if (!IsDone)
			{
				TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_CombatMain());
				this.UpdateGoalText(QuestId);
			}
		}

		#endregion


		protected Composite CreateBehavior_CombatMain()
		{
			return _root ??
				   (_root =new Decorator(ctx => !IsDone,
					   new PrioritySelector(
						   new Decorator(ret => Me.IsQuestComplete(QuestId),
							   new PrioritySelector(
								   new Decorator(
									   ctx => Query.IsInVehicle(),
									   new PrioritySelector(
										   new Decorator(
											   ctx => WoWMovement.ActiveMover.Location.DistanceSqr(endloc) > 10*10,
											   new Action(ctx => Flightor.MoveTo(endloc))),
										   new Action(ctx => Lua.DoString("VehicleExit()")))),
								   new Sequence(
									   new Action(ret => TreeRoot.StatusText = "Finished!"),
									   new Action(ctx => _isDone = true)))),
						   new Decorator(ctx => !Me.IsActuallyInCombat && !Query.IsInVehicle(), CreateBehavior_GetInVehicle()),
						   new Decorator(ctx => Query.IsInVehicle(), CreateBehavior_Kill()))));
		}

		private Composite CreateBehavior_GetInVehicle()
		{
			const uint flamebringerId = 27292;

			WoWUnit flamebringer = null;

			return
				new PrioritySelector(
					ctx =>
						flamebringer =
							ObjectManager.GetObjectsOfTypeFast<WoWUnit>().FirstOrDefault(u => u.Entry == flamebringerId),
					// move out to framebringers location
					new Decorator(ctx => flamebringer == null,CreateBehavior_MoveTo(ctx =>_flamebringerLocation)),
					new Decorator(ctx => flamebringer.Distance > 5, CreateBehavior_MoveTo(ctx => flamebringer.Location)),
					// dismount and cancel shapeshift
					new Decorator(ctx => Me.IsMounted(), new ActionRunCoroutine(context => CommonCoroutines.Dismount("Getting on Flamebringger"))),
					new Decorator(ctx => Me.IsShapeshifted(), new Action(ctx => Lua.DoString("CancelShapeshiftForm()"))),
					new Decorator(ctx => Me.IsMoving, new Action(ctx => WoWMovement.MoveStop())),
					// interact with and talk to flamebringer
					new Decorator(ctx => !GossipFrame.Instance.IsVisible, new Action(ctx => flamebringer.Interact())),
					new Decorator(
						ctx => GossipFrame.Instance.IsVisible,
						new Action(ctx => GossipFrame.Instance.SelectGossipOption(0))));
		}

		private Composite CreateBehavior_MoveTo(Func<object, WoWPoint> locationSelector)
		{
			return
				new PrioritySelector(
					new Decorator(
						ctx => WoWMovement.ActiveMover.MovementInfo.CanFly,
						new Action(ctx => Flightor.MoveTo(locationSelector(ctx)))),
					new Action(ctx => Navigator.MoveTo(locationSelector(ctx))));
		}

		private Composite CreateBehavior_Kill()
		{
			const uint torvaldErikssonId = 27377;

			var movetoLocation = new WoWPoint(2939.321, -2536.72, 123.3394);
			WoWUnit torvaldEriksson = null;

			return
				new PrioritySelector(
					ctx =>
						torvaldEriksson =
							ObjectManager.GetObjectsOfTypeFast<WoWUnit>().FirstOrDefault(u => u.Entry == torvaldErikssonId && u.IsAlive),
					// move in position
					new Decorator(
						ctx => WoWMovement.ActiveMover.Location.DistanceSqr(movetoLocation) > 5*5,
						new Action(ctx => Flightor.MoveTo(movetoLocation))),
					new Decorator(ctx => WoWMovement.ActiveMover.IsMoving, new Action(ctx => WoWMovement.MoveStop())),
					new Decorator(
						ctx => torvaldEriksson != null ,
						new PrioritySelector(
							// target
							new Decorator(
								ctx => WoWMovement.ActiveMover.CurrentTargetGuid != torvaldEriksson.Guid,
								new Action(ctx => torvaldEriksson.Target())),
							// face 
							new Decorator(
								ctx => !WoWMovement.ActiveMover.IsSafelyFacing(torvaldEriksson, 30),
								new Action(ctx => torvaldEriksson.Face())),
							new Action(ctx => AimAndFire(torvaldEriksson)))));
		}

		private void AimAndFire(WoWUnit target)
		{
			var v = target.Location - StyxWoW.Me.Location;
			v.Normalize();
			Lua.DoString(
				string.Format(
					"local pitch = {0}; local delta = pitch - VehicleAimGetAngle(); VehicleAimIncrement(delta);",
					Math.Asin(v.Z).ToString(CultureInfo.InvariantCulture)));
			Lua.DoString("CastPetAction(3)");
			Lua.DoString("CastPetAction(2)");
			Lua.DoString("CastPetAction(1)");
		}

		public q12259(Dictionary<string, string> args) : base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			QuestId = 12259;
			Location = WoWPoint.Empty;
			Endloc = WoWPoint.Empty;
			QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
			QuestRequirementInLog = QuestInLogRequirement.InLog;
		}

		public WoWPoint Location { get; private set; }
		public WoWPoint Endloc { get; private set; }
		public int QuestId { get; set; }

		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		#region Cleanup

        public override void OnFinished()
        {
            TreeHooks.Instance.RemoveHook("Combat_Main", CreateBehavior_CombatMain());
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
        }

		#endregion
	}
}