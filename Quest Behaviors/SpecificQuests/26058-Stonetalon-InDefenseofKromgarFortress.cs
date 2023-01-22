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
using System.Linq;

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


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.InDefenseofKromgarFortress
{
	[CustomBehaviorFileName(@"SpecificQuests\26058-Stonetalon-InDefenseofKromgarFortress")]
	public class q26058 : CustomForcedBehavior
	{
		private const uint TurretId = 41895;

		private const double WeaponAzimuthMax = 0.4363;
		private const double WeaponAzimuthMin = -0.03491;
		private const double WeaponMuzzleVelocity = 1000;
		private const int QuestId = 26058;
		private readonly WoWPoint _questLocation = new WoWPoint(923.4741, -0.8692548, 92.59513);
		private readonly LocalBlacklist _targetBlacklist = new LocalBlacklist(TimeSpan.FromSeconds(30));

		private bool _isDone;
		private Composite _root;

		public q26058(Dictionary<string, string> args) : base(args)
		{
			QBCLog.BehaviorLoggingContext = this;
		}

		private VehicleWeapon WeaponFireCannon { get; set; }

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		public WoWUnit BestTarget
		{
			get
			{
				var myCurrentTargat = Me.CurrentTarget;
				var myLoc = StyxWoW.Me.Location;
				if (IsValidTarget(myCurrentTargat))
				{
					var targetLoc = myCurrentTargat.Location;
					if (IsInPosition(targetLoc, myLoc))
						return myCurrentTargat;
				}

				return (from unit in ObjectManager.GetObjectsOfType<WoWUnit>()
					where IsValidTarget(unit)
					let loc = unit.Location
					where IsInPosition(loc, myLoc)
					orderby myLoc.DistanceSqr(loc)
					select unit).FirstOrDefault();
			}
		}

		#region Overrides

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

				var weaponArticulation = new WeaponArticulation(WeaponAzimuthMin, WeaponAzimuthMax);
				WeaponFireCannon = new VehicleWeapon(1, weaponArticulation, WeaponMuzzleVelocity);

				this.UpdateGoalText(QuestId);
			}
		}

		private Composite CreateBehavior_CombatMain()
		{
			WoWUnit turret = null;
			WoWUnit selectedTarget = null;

			return _root ??
				   (_root = new Decorator(ctx => !IsDone,
					   new PrioritySelector(
						   new Decorator(
							   ret => Me.IsQuestComplete(QuestId),
							   new Sequence(
								   new Action(ret => TreeRoot.StatusText = "Finished!"),
								   new Action(ret => Lua.DoString("VehicleExit()")),
								   new Action(ctx => _isDone = true))),
						   new Decorator(
							   ret => !Query.IsInVehicle(),
							   new PrioritySelector(
								   ctx =>
									   turret =
										   ObjectManager.GetObjectsOfTypeFast<WoWUnit>()
											   .FirstOrDefault(u => u.Entry == TurretId),
								   new Decorator(ctx => turret == null, new Action(ctx => Navigator.MoveTo(_questLocation))),
								   new Decorator(
									   ctx => !turret.WithinInteractRange,
									   new Action(ctx => Navigator.MoveTo(turret.Location))),
								   new Decorator(
									   ctx => turret.WithinInteractRange,
									   new PrioritySelector(
										   new Decorator(
											   ctx => StyxWoW.Me.IsMoving,
											   new Action(ctx => WoWMovement.MoveStop())),
										   new Action(ctx => turret.Interact()))))),
						   new PrioritySelector(
							   ctx => selectedTarget = BestTarget,
							   new Decorator(
								   ctx => selectedTarget != null,
								   new PrioritySelector(
									   new Decorator(
										   ctx => selectedTarget.Guid != Me.CurrentTargetGuid,
										   new Action(ctx => selectedTarget.Target())),
									   // Aim & Fire at the selected target...
									   new Sequence(
										   new Action(
											   context =>
											   {
												   // If weapon aim cannot address selected target, blacklist target for a few seconds...
												   if (!WeaponFireCannon.WeaponAim(selectedTarget))
												   {
													   _targetBlacklist.Add(selectedTarget, TimeSpan.FromSeconds(5));
													   return RunStatus.Failure;
												   }

												   // If weapon could not be fired, wait for it to become ready...
												   if (!WeaponFireCannon.WeaponFire())
												   {
													   return RunStatus.Failure;
												   }

												   return RunStatus.Success;
											   }),
										   new WaitContinue(
											   Delay.AfterWeaponFire,
											   context => false,
											   new ActionAlwaysSucceed()))))))));
		}

		#endregion

		private bool IsValidTarget(WoWUnit unit)
		{
			return unit != null && !_targetBlacklist.Contains(unit) && !unit.IsDead &&
				   (unit.Entry == 42017 || unit.Entry == 42016 || unit.Entry == 42015);
		}

		private bool IsInPosition(WoWPoint unitLoc, WoWPoint myLoc)
		{
			return unitLoc.X > 935 && unitLoc.Y > 5 && unitLoc.Z >= myLoc.Z;
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