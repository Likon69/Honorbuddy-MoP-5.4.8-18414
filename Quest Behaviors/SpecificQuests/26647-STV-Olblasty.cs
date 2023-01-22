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
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.Olblasty
{
	[CustomBehaviorFileName(@"SpecificQuests\26647-STV-Olblasty")]
	public class Olblasty : CustomForcedBehavior
	{
		private const double WeaponAzimuthMax = 1.134464;
		private const double WeaponAzimuthMin = -0.348367;
		private const double CannonballMuzzleVelocity = 80;
		private const double CannonballGravity = 19.29;

		private readonly WeaponArticulation _weaponArticulation = new WeaponArticulation(WeaponAzimuthMin, WeaponAzimuthMax);

		private bool _isBehaviorDone;

		private Composite _root;

		public Olblasty(Dictionary<string, string> args) : base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				QuestId = 26647; //GetAttributeAsQuestId("QuestId", true, null) ?? 0;
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

		public int QuestId { get; set; }


		public override bool IsDone
		{
			get { return _isBehaviorDone; }
		}

		private LocalPlayer Me
		{
			get { return (StyxWoW.Me); }
		}

		public Composite CreateBehavior_CheckCompletion()
		{
			return new Decorator(ret => !_isBehaviorDone && Me.IsQuestComplete(QuestId),
				new Action(delegate
				{
					TreeRoot.StatusText = "Finished!";
					Lua.DoString("VehicleExit()");
					_isBehaviorDone = true;
					return RunStatus.Success;
				}));
		}

		public Composite CreateBehavior_Shoot()
		{
			var cannon = new VehicleWeapon(3, _weaponArticulation, CannonballMuzzleVelocity, CannonballGravity);
			const int readyAuraId = 81513;
			const int aimAuraId = 81514;
			WoWUnit charmedUnit = null;

			const uint boatId = 43561;
			WoWUnit boat = null;
			return new PrioritySelector(
				ctx => boat = ObjectManager.GetObjectsOfTypeFast<WoWUnit>().FirstOrDefault(u => u.Entry == boatId),
				new Decorator(
					r => boat != null,
					new PrioritySelector(
						ctx => charmedUnit = Me.CharmedUnit,
						new Decorator(ctx => !charmedUnit.HasAura(readyAuraId), new Action(ctx => Lua.DoString("CastPetAction({0})", 1))),
						new Decorator(
							ctx => cannon.WeaponAim(boat),
							new Sequence(
								new Action(ctx => Lua.DoString("CastPetAction({0})", 2)),
								new WaitContinue(2, ctx => charmedUnit.HasAura(aimAuraId), new ActionAlwaysSucceed()),
								new Action(ctx => cannon.WeaponFire()))))));
		}

		private Composite CreateBehavior_GetInVehicle()
		{
			const uint oldBlastyId = 43562;

			WoWUnit vehicle = null;

			return new Decorator(ctx => !Query.IsInVehicle(),
				new PrioritySelector(
					ctx => vehicle = ObjectManager.GetObjectsOfTypeFast<WoWUnit>().FirstOrDefault(u => u.Entry == oldBlastyId),
					new Decorator(ctx => vehicle != null && !vehicle.WithinInteractRange, new Action(ctx => Navigator.MoveTo(vehicle.Location))),
					new Decorator(
						ctx => vehicle != null && vehicle.WithinInteractRange,
						new PrioritySelector(
							new Decorator(ctx => Me.Mounted,
                                new ActionRunCoroutine(context => CommonCoroutines.Dismount("Getting in vehicle"))),
							new Decorator(ctx => Me.IsShapeshifted(),
								new Action(ctx => Lua.DoString("CancelShapeshiftForm()"))),
							new Action(ctx => vehicle.Interact())))));
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


		protected Composite CreateBehavior_CombatMain()
		{
			return _root ?? (_root =
				new Decorator(ret => !IsDone,
					new PrioritySelector(CreateBehavior_CheckCompletion(), CreateBehavior_GetInVehicle(), CreateBehavior_Shoot())));
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