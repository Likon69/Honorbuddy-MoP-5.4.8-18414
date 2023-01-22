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
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.DriveByPiracy
{
	[CustomBehaviorFileName(@"SpecificQuests\26649-Stranglethorn-DriveByPiracy")]
	public class q26649 : CustomForcedBehavior
	{
		private const double WeaponAzimuthMax = 1.134464;
		private const double WeaponAzimuthMin = -0.348367;
		private const double CannonballMuzzleVelocity = 80;
		private const double CannonballGravity = 19.29;
		private const uint VentureCoOilWorkerId = 43596;

		private readonly WeaponArticulation _weaponArticulation = new WeaponArticulation(WeaponAzimuthMin, WeaponAzimuthMax);

		private int QuestId = 26649;
		private bool _isBehaviorDone;
		private Composite _root;
		public q26649(Dictionary<string, string> args) : base(args)
		{
			QBCLog.BehaviorLoggingContext = this;
		}


		public override bool IsDone
		{
			get { return _isBehaviorDone; }
		}

		private LocalPlayer Me
		{
			get { return (StyxWoW.Me); }
		}

		private WoWUnit BestTarget
		{
			get
			{
				var myLoc = Me.Location;
				return
					ObjectManager.GetObjectsOfTypeFast<WoWUnit>()
						.Where(u => u.Entry == VentureCoOilWorkerId && u.IsAlive)
						.OrderBy(u => u.Location.DistanceSqr(myLoc))
						.FirstOrDefault();
			}
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
			return _root ??
				   (_root =
					   new Decorator(
						   ctx => !IsDone,
						   new PrioritySelector(CreateBehavior_CheckCompletion(), CreateBehavior_ShootPirates())));
		}

		public Composite CreateBehavior_ShootPirates()
		{
			var cannon = new VehicleWeapon(3, _weaponArticulation, CannonballMuzzleVelocity, CannonballGravity);
			const int readyAuraId = 81513;
			const int aimAuraId = 81514;
			WoWUnit charmedUnit = null;

			WoWUnit selectedTarget = null;
			return new PrioritySelector(
				ctx => selectedTarget = BestTarget,
				new Decorator(
					r => selectedTarget != null,
					new PrioritySelector(
						ctx => charmedUnit = Me.CharmedUnit,
						new Decorator(
							ctx => !charmedUnit.HasAura(readyAuraId),
							new Action(ctx => Lua.DoString("CastPetAction({0})", 1))),
						// aim weapon and fire.
						new Decorator(
							ctx => cannon.WeaponAim(selectedTarget),
							new Sequence(
								new Action(ctx => Lua.DoString("CastPetAction({0})", 2)),
								new WaitContinue(2, ctx => charmedUnit.HasAura(aimAuraId), new ActionAlwaysSucceed()),
								new Action(ctx => cannon.WeaponFire()))))));
		}

		public Composite CreateBehavior_CheckCompletion()
		{
			return new Decorator(ret => !_isBehaviorDone && Me.IsQuestComplete(QuestId),
				new Sequence(
					new ActionSetActivity("Finished!"),
					new Action(ctx => Lua.DoString("CastPetAction({0})", 5)),
					new Action(ctx => _isBehaviorDone = true)));
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