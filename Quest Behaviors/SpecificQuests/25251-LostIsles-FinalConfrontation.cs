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

#endregion

#region Examples

#endregion

#region Usings

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Honorbuddy.QuestBehaviorCore;
using Honorbuddy.Quest_Behaviors.WaitTimerBehavior;
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

namespace Honorbuddy.Quest_Behaviors.SpecificQuests.FinalConfrontation
{
	[CustomBehaviorFileName(@"SpecificQuests\25251-LostIsles-FinalConfrontation")]
	public class q25251 : CustomForcedBehavior
	{

		private const int QuestId = 25251;
		private readonly WoWPoint _flyloc = new WoWPoint(2120.643, 2402.012, 49.6927);
		private readonly WoWPoint _wallyLocation = new WoWPoint(2277.934, 2420.762, 22.582);

		private CircularQueue<WoWPoint> _gallywixPath = new CircularQueue<WoWPoint>()
														{
															new WoWPoint(2400.707, 2532.421, 4.890985),
															new WoWPoint(2298.823, 2433.5, 26.45126)
														};

		private bool _isBehaviorDone = false;
		private Composite _root;

		public q25251(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;
		}


		public static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		public WoWUnit Gallywix
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>()
					.FirstOrDefault(u => (u.Entry == 39582 && !u.IsDead));
			}
		}

		public WoWUnit Robot
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>()
					.Where(u => (u.Entry == 39592 && !u.IsDead))
					.OrderBy(u => u.DistanceSqr).FirstOrDefault();
			}
		}

		public override bool IsDone
		{
			get
			{
				return _isBehaviorDone
					   ||
					   !UtilIsProgressRequirementsMet(
						   QuestId,
						   QuestInLogRequirement.InLog,
						   QuestCompleteRequirement.NotComplete);
			}
		}

		protected Composite CreateBehavior_QuestbotMain()
		{
			return _root ?? (_root =
				new Decorator(
					ctx => !_isBehaviorDone && Me.IsAlive,
					new PrioritySelector(
						// If quest is complete then move to a safe place and exit vehicle.
						new Decorator(
							ctx => Me.QuestLog.GetQuestById(QuestId).IsCompleted,
							new PrioritySelector(
								new Decorator(
									ctx => Me.Location.DistanceSqr(_flyloc) > 10*10,
									CreateBehavior_VehicleMoveTo(ctx => _flyloc, 5)),
								new Sequence(
									new Action(ctx => Lua.DoString("VehicleExit()")),
									new Action(ctx => _isBehaviorDone = true)))),
						// Get in a vehicle if not in one.
						new Decorator(
							ret => !Query.IsInVehicle(),
							new PrioritySelector(
								ctx => Robot,
								new Decorator(ctx => ctx == null, new Action(ctx => Navigator.MoveTo(_flyloc))),
								new Decorator(
									ctx => ((WoWUnit) ctx).DistanceSqr > 5*5,
									new Action(ctx => Navigator.MoveTo(((WoWUnit) ctx).Location))),
								new Decorator(ctx => Me.IsMoving, new Action(ctx => WoWMovement.MoveStop())),
								new Action(ctx => ((WoWUnit) ctx).Interact()))),
						new PrioritySelector(
							ctx => Gallywix,
							// search for gallywix
							new Decorator(
								ctx => ctx == null,
								new PrioritySelector(
									new Decorator(
										ctx => Me.Location.Distance2DSqr(_gallywixPath.Peek()) < 5*5,
										new Action(ctx => _gallywixPath.Dequeue())),
									CreateBehavior_VehicleMoveTo(ctx => _gallywixPath.Peek(), 5))),
							// target
							new Decorator(
								ctx => Me.CurrentTarget != (WoWUnit) ctx,
								new Action(ctx => ((WoWUnit) ctx).Target())),
							// Move within range and LOS of Gallywix
							new Decorator(
								ctx => ((WoWUnit) ctx).DistanceSqr > 40*40
									   || !((WoWUnit) ctx).InLineOfSpellSight,
								CreateBehavior_VehicleMoveTo(ctx => ((WoWUnit) ctx).Location, 5)),
							// stop moving after getting within range and los
							new Decorator(ctx => WoWMovement.ActiveMover.IsMoving, new Action(ctx => WoWMovement.MoveStop())),
							// face
							new Decorator(
								ctx => !WoWMovement.ActiveMover.IsSafelyFacing((WoWUnit) ctx),
								new Action(ctx => ((WoWUnit) ctx).Face())),
							// fire away.
							new Decorator(ctx => CanUsePetButton(3), new Action(ctx => UsePetButton(3))),
							new Decorator(ctx => CanUsePetButton(2), new Action(ctx => UsePetButton(2))),
							new Decorator(ctx => CanUsePetButton(1), new Action(ctx => UsePetButton(1))))))
				);
		}

		private Composite CreateBehavior_VehicleMoveTo(Func<object, WoWPoint> locationSelector, float? pathPrecision = null)
		{
			var stuckCheckSw = new Stopwatch();

			var lastPoint = WoWPoint.Zero;

			float originalPrecision = Navigator.PathPrecision;

			// normal stuck handler doesn't trigger when in a vehicle.
			var IsStuck = new Func<bool>(
				() =>
				{
					var result = false;
					if (stuckCheckSw.ElapsedMilliseconds > 2000 || !stuckCheckSw.IsRunning)
					{
						var activeMover = WoWMovement.ActiveMover;
						if (activeMover == null)
							return false;
						var loc = activeMover.Location;
						if (lastPoint != WoWPoint.Zero)
						{

							double myMovementSpeed = activeMover.IsSwimming
								? activeMover.MovementInfo.SwimmingForwardSpeed
								: activeMover.MovementInfo.ForwardSpeed;
							var expectedDist = myMovementSpeed/stuckCheckSw.Elapsed.TotalSeconds * 0.5;
							if (loc.DistanceSqr(lastPoint) < expectedDist*expectedDist)
							{
								result = true;
							}
						}

						lastPoint = loc;
						stuckCheckSw.Restart();
					}
					return result;
				});

			return new Action(
				ctx =>
				{
					var result = MoveResult.Failed;
					if (IsStuck())
					{
						Navigator.NavigationProvider.StuckHandler.Unstick();
					}
					if (pathPrecision.HasValue)
					{
						originalPrecision = Navigator.PathPrecision;
						Navigator.PathPrecision = pathPrecision.Value;
					}
					try
					{
					   result= Navigator.MoveTo(locationSelector(ctx));
					}
					finally
					{
						if (pathPrecision.HasValue)
							Navigator.PathPrecision = originalPrecision;
					}
					return Navigator.GetRunStatusFromMoveResult(result);
				});
		}

		private bool CanUsePetButton(int index)
		{
			string lua = string.Format("if GetPetActionCooldown({0}) == 0 then return 1 end return 0", index);
			return Lua.GetReturnVal<int>(lua, 0) == 1;
		}

		private void UsePetButton(int index)
		{
			Lua.DoString("CastPetAction({0})", index);
		}

		public override void OnStart()
		{
			OnStart_HandleAttributeProblem();
			if (!IsDone)
			{
				TreeHooks.Instance.InsertHook("Questbot_Main", 0, CreateBehavior_QuestbotMain());
				this.UpdateGoalText(0);
			}
		}

		public override void OnFinished()
		{
			TreeHooks.Instance.RemoveHook("Questbot_Main", CreateBehavior_QuestbotMain());
			base.OnFinished();
		}
	}
}