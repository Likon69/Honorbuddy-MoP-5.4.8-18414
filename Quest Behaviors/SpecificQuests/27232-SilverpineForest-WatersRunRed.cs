// Behavior originally contributed by Cava
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//

// TODO: Will want to deprecate this when a generic Ballistic Vehicle behavior becomes available

#region Usings
using System.Collections.Generic;
using System.Linq;

using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


// ReSharper disable once CheckNamespace
namespace Honorbuddy.Quest_Behaviors.SpecificQuests.WatersRunRed
{
	[CustomBehaviorFileName(@"SpecificQuests\27232-SilverpineForest-WatersRunRed")]
	public class Q27232 : CustomForcedBehavior
	{
		public Q27232(Dictionary<string, string> args)
			: base(args)
		{
			// empty for now
		}


		private readonly WoWPoint _turretLoc = new WoWPoint(710.7488, 947.981, 34.75594);
		private readonly WoWPoint _firstshot = new WoWPoint(719.7801, 826.9634, 31.05201);
		public double Angle = 0;
		public double CurentAngle = 0;
		private bool _isBehaviorDone;
		private bool IsOnFinishedRun { get; set; }
		private const int QuestId = 27232;
		private Composite _root;

		public static LocalPlayer Me { get { return StyxWoW.Me; } }
		

		static public bool InVehicle
		{
			get
			{
				return Lua.GetReturnVal<int>("if IsPossessBarVisible() or UnitInVehicle('player') then return 1 else return 0 end", 0) == 1;
			}
		}

		
		public List<WoWUnit> MobWorgenList
		{
			get
			{
				return
					ObjectManager.GetObjectsOfType<WoWUnit>()
					.Where(u =>
						u.IsValid
						&& (u.Entry == 45270)
						&& !u.IsDead
						&& (u.Y <= 875)
						&& (u.Distance < 150))
					.OrderBy(u => u.DistanceSqr)
					.ToList();
			}
		}


		public List<WoWUnit> Turret
		{
			get
			{
				return
					ObjectManager.GetObjectsOfType<WoWUnit>()
					.Where(u => 
						u.IsValid
						&& (u.Entry == 45263))
					.OrderBy(u => u.DistanceSqr)
					.ToList();
			}
		}


		protected override Composite CreateBehavior()
		{
			return _root ?? (_root =
				new PrioritySelector(
					// exit vehicle if quest is done.
					new Decorator(ret => Me.QuestLog.GetQuestById(27232) !=null && Me.QuestLog.GetQuestById(27232).IsCompleted,
						new Sequence(
							new Action(ret => TreeRoot.StatusText = "Finished!"),
							new Action(ret => Lua.DoString("VehicleExit()")),
							new WaitContinue(120,
								new Action(delegate
								{
									_isBehaviorDone = true;
									return RunStatus.Success;
								})))),

					// Get in a vehicle if not in one.
					new Decorator(ret => !Query.IsInVehicle(),
						new Sequence(
							new DecoratorContinue(ret => Turret.Count == 0,
								new Sequence(
									new Action(ret => Navigator.MoveTo(_turretLoc)),
									new Sleep(1000)
							)),
							new DecoratorContinue(ret => Turret.Count > 0 && Turret[0].Location.Distance(Me.Location) > 5,
								new Sequence(
									new Action(ret => Navigator.MoveTo(Turret[0].Location)),
									new Sleep(1000)
							)),
							new DecoratorContinue(ret => Turret.Count > 0 && Turret[0].Location.Distance(Me.Location) <= 5,
								new Sequence(
									new Action(ret => WoWMovement.MoveStop()),
									new Action(ret => Turret[0].Interact()),
									new Sleep(1000)
					)))),

					new Decorator(ret => MobWorgenList.Count == 0,
						new Sequence(
							new Action(ret => WoWMovement.ClickToMove(_firstshot)),
							new DecoratorContinue(ret => Me.CurrentTarget == null,
								new Sequence(
									new Action(ret => WoWMovement.ConstantFace(Me.CurrentTarget.Guid)),
									new Action(ret => Angle = (_firstshot.Z - Me.Z) / (Me.Location.Distance(_firstshot))),
									new Action(ret => CurentAngle = Lua.GetReturnVal<double>("return VehicleAimGetAngle()", 0)),
									new Decorator(ret => CurentAngle < Angle,
										new Action(ret => Lua.DoString(string.Format("VehicleAimIncrement(\"{0}\")", (Angle - CurentAngle))))),
									new Decorator(ret => CurentAngle > Angle,
										new Action(ret => Lua.DoString(string.Format("VehicleAimDecrement(\"{0}\")", (CurentAngle - Angle))))),
									new Sleep(1000)
					)))),

					new Decorator(ret => MobWorgenList.Count > 0,
						new Sequence(
							new Action(ret => MobWorgenList[0].Target()),
							new DecoratorContinue(ret => Me.CurrentTarget != null && Me.CurrentTarget.IsAlive,
								new Sequence(
									new Action(ret => WoWMovement.ConstantFace(Me.CurrentTarget.Guid)),
									new Action(ret => Angle = (Me.CurrentTarget.Z - Me.Z) / (Me.CurrentTarget.Location.Distance(Me.Location))),
									new Action(ret => CurentAngle = Lua.GetReturnVal<double>("return VehicleAimGetAngle()", 0)),
									new Decorator(ret => CurentAngle < Angle,
										new Action(ret => Lua.DoString(string.Format("VehicleAimIncrement(\"{0}\")", (Angle - CurentAngle))))),
									new Decorator(ret => CurentAngle > Angle,
										new Action(ret => Lua.DoString(string.Format("VehicleAimDecrement(\"{0}\")", (CurentAngle - Angle))))),
									new Sleep(1000)
					)))),

					new Action(ret => Lua.DoString("RunMacroText('/click OverrideActionBarButton1','0')"))
				)
			);
		}


		#region Overrides of CustomForcedBehavior

		public override bool IsDone
		{
			get { return _isBehaviorDone; }
		}


		public override void OnFinished()
		{
			if (IsOnFinishedRun)
				{ return; }

			TreeRoot.GoalText = string.Empty;
			TreeRoot.StatusText = string.Empty;

			// QuestBehaviorBase.OnFinished() will set IsOnFinishedRun...
			base.OnFinished();
			IsOnFinishedRun = true;
		}


		public override void OnStart()
		{
			OnStart_HandleAttributeProblem();
			if (!IsDone)
			{
				this.UpdateGoalText(QuestId);
			}
		}
		#endregion
	}
}

