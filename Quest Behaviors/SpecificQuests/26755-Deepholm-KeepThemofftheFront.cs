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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Honorbuddy.QuestBehaviorCore;

using Action = Styx.TreeSharp.Action;
#endregion


// ReSharper disable once CheckNamespace
namespace Honorbuddy.Quest_Behaviors.SpecificQuests.KeepThemofftheFront
{
	[CustomBehaviorFileName(@"SpecificQuests\26755-Deepholm-KeepThemofftheFront")]
	public class KeepThemofftheFront : CustomForcedBehavior
	{
		public KeepThemofftheFront(Dictionary<string, string> args) : base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			QuestId = 26755;
			var weaponArticulation = new WeaponArticulation(WeaponAzimuthMin, WeaponAzimuthMax);
			_catapult = new VehicleWeapon(1, weaponArticulation, WeaponMuzzleVelocity, WeaponProjectileGravity);
		}

		private int QuestId { get; set; }


		private const uint StoneTroggReinforcementId = 43960;
		private const uint FungalTerrorId = 43954;

		private const double WeaponAzimuthMax = 0.7853999;
		private const double WeaponAzimuthMin = -0.1745;
		private const double WeaponMuzzleVelocity = 100;
		private const double WeaponProjectileGravity = 30;

		private readonly VehicleWeapon _catapult;
		private readonly uint[] _mobIds = { StoneTroggReinforcementId, FungalTerrorId };
		private readonly Stopwatch _doingQuestTimer = new Stopwatch();

		private bool _isBehaviorDone;
		private bool IsOnFinishedRun { get; set; }
		private Composite _root;

		private static LocalPlayer Me { get { return (StyxWoW.Me); } }


		private static List<WoWUnit> Catapult
		{
			get
			{
				return
					ObjectManager.GetObjectsOfType<WoWUnit>()
					.Where(u => 
						u.IsValid
						&& (u.Entry == 43952))
					.OrderBy(ret => ret.DistanceSqr)
					.ToList();
			}
		}
		private readonly WoWPoint _catapultLoc = new WoWPoint(21023.448, 1888.969, 309.9148);


		private WoWUnit BestTarget
		{
			get
			{
				var activeMover = WoWMovement.ActiveMover;
				var myLoc = activeMover.Location;

				var myTarget = activeMover.CurrentTarget;

				if (myTarget != null && myTarget.IsAlive && _mobIds.Contains(myTarget.Entry) && myTarget.DistanceSqr > 25*25)
					return myTarget;

				return 
				   (from unit in ObjectManager.GetObjectsOfType<WoWUnit>()
					where _mobIds.Contains(unit.Entry) && unit.IsAlive
					let distanceSqr = myLoc.DistanceSqr(unit.Location)
					where distanceSqr > 25*25
					orderby distanceSqr
					select unit)
					.FirstOrDefault();
			}
		}


		private Composite CreateBehavior_CombatMain()
		{
			return _root ??
				   (_root =
					   new Decorator(
						   ret => !_isBehaviorDone,
						   new PrioritySelector(
							   // Leave vehicle if time is pass.
							   new Decorator(ret => _doingQuestTimer.ElapsedMilliseconds >= 180000,
									new Sequence(
										new Action(ret => _doingQuestTimer.Restart()),
										new Action(ret => Lua.DoString("VehicleExit()")),
										new Sleep(4000)
								)),
							   // Get in a vehicle if not in one.
							   new Decorator(ret => !Query.IsInVehicle(),
								   new Sequence(
									   new DecoratorContinue(ret => Catapult.Count == 0,
										   new Sequence(
											   new Action(ret => Navigator.MoveTo(_catapultLoc)),
											   new Sleep(1000)
									   )),
									   new DecoratorContinue(ret => Catapult.Count > 0 && Catapult[0].Location.Distance(Me.Location) > 5,
										   new Sequence(
												new Action(ret => Navigator.MoveTo(Catapult[0].Location)),
												new Sleep(1000)
									   )),
									   new DecoratorContinue(ret => Catapult.Count > 0 && Catapult[0].Location.Distance(Me.Location) <= 5,
										   new Sequence(
											   new Action(ret => WoWMovement.MoveStop()),
											   new Action(ret => Catapult[0].Interact()),
											   new Sleep(1000)
									   ))
							   )),
							   CreateBehavior_CheckQuestCompletion(),
							   CreateBehavior_ShootCatapult()
				   )));
		}


		private Composite CreateBehavior_CheckQuestCompletion()
		{
			return new Decorator(ret => Me.IsQuestComplete(QuestId),
				new Action(ctx =>
					{
						Lua.DoString("VehicleExit()");
						_isBehaviorDone = true;
					}));
		}


		private Composite CreateBehavior_ShootCatapult()
		{
			WoWUnit target = null;
			return new PrioritySelector(
				ctx => target = BestTarget,
				new Decorator(
					ctx => target != null && _catapult.WeaponAim(target),
					new Action(ctx => _catapult.WeaponFire())));
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

			// Clean up resources...
			TreeHooks.Instance.RemoveHook("Combat_Main", CreateBehavior_CombatMain());

			TreeRoot.GoalText = string.Empty;
			TreeRoot.StatusText = string.Empty;

			// QuestBehaviorBase.OnFinished() will set IsOnFinishedRun...
			base.OnFinished();
			IsOnFinishedRun = true;
		}


		public override void OnStart()
		{
			OnStart_HandleAttributeProblem();
			_doingQuestTimer.Start();
			if (!IsDone)
			{
				TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_CombatMain());

				this.UpdateGoalText(QuestId);
			}
		}
		#endregion
	}
}