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
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
using WaitTimer = Styx.Common.Helpers.WaitTimer;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.TheBurlapGrind
{
	[CustomBehaviorFileName(@"SpecificQuests\30747-KunLai-TheBurlapGrind")]
	public class TheBurlapGrind : CustomForcedBehavior
	{
		public uint[] MobIds = new uint[] {60749, 60746, 60752, 60753, 60743,};
		private WoWPoint StartPoint = new WoWPoint(2726.94, 2179.04, 590.6934);
		private WoWPoint WaitPoint = new WoWPoint(2793.078, 1773.688, 638.9326);
		private bool _isBehaviorDone;


		private Composite _root;
		private bool _useMount;
		private WoWPoint point1 = new WoWPoint(2794.707, 1695.803, 642.4144);

		#region Cleanup

        public override void OnFinished()
        {
            CharacterSettings.Instance.UseMount = _useMount;
            TreeHooks.Instance.RemoveHook("Combat_Main", CreateBehavior_MainCombat());
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
        }

		#endregion

		public TheBurlapGrind(Dictionary<string, string> args) : base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				QuestId = 30747; //GetAttributeAsQuestId("QuestId", true, null) ?? 0;
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


		public List<WoWUnit> Monkies
		{
			get
			{
				var myLoc = Me.Location;
				return (from u in ObjectManager.GetObjectsOfType<WoWUnit>()
					where MobIds.Contains(u.Entry) && !u.IsDead
					let loc = u.Location
					orderby loc.DistanceSqr(myLoc)
					select u).ToList();
			}
		}


		public Composite CreateBehavior_CheckCompletion()
		{
			return new Decorator(ret => Me.IsQuestComplete(QuestId),
				new Action(delegate
				{
					TreeRoot.StatusText = "Finished!";
					Lua.DoString("VehicleExit()");
					_isBehaviorDone = true;
					return RunStatus.Success;
				}));
		}


		public WoWUnit Yeti
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>()
					.FirstOrDefault(r => r.NpcFlags == 1 && r.Entry == 60587 && r.Location.DistanceSqr(StartPoint) < 30*30);
			}
		}


		public Composite CreateBehavior_KillMantid()
		{
			WoWUnit attackTarget = null;
			WoWUnit yeti = null;
			WaitTimer leapSmashTimer = WaitTimer.TenSeconds;

			return new Decorator(r => !Me.IsQuestComplete(QuestId) && Query.IsInVehicle() && (yeti = Me.CharmedUnit) != null,
				new PrioritySelector(ctx => attackTarget = GetAttackTarget(),
					new Decorator(ctx => attackTarget != null,
						new PrioritySelector(
							new ActionFail(ctx => _stuckTimer.Reset()),
							new ActionSetActivity("Moving to Attack"),
							new Decorator(ctx => Me.CurrentTargetGuid != attackTarget.Guid,
								new ActionFail(ctx => attackTarget.Target())),
							new Decorator(ctx => !Me.IsSafelyFacing(attackTarget) || !yeti.IsSafelyFacing(attackTarget),
								new ActionFail(ctx => attackTarget.Face())),
							// cast 'Hozen Snack' ability to heal up.
							new Decorator(ctx => yeti.HealthPercent <= 70,
								new ActionFail(ctx => Lua.DoString("CastPetAction(4)"))),
							// cast 'Leap Smash' ability on targets outside of melee
							new Decorator(
								ctx =>
									yeti.Location.DistanceSqr(attackTarget.Location) > 30*30 && yeti.Location.DistanceSqr(attackTarget.Location) < 90 * 90 && leapSmashTimer.IsFinished,
								new Sequence(
									new Action(ctx => Lua.DoString("CastPetAction(1)")),
									new WaitContinue(2, ctx => StyxWoW.Me.CurrentPendingCursorSpell != null, new ActionAlwaysSucceed()),
									new Action(ctx => SpellManager.ClickRemoteLocation(attackTarget.Location)),
									new Action(ctx => leapSmashTimer.Reset()))),
							// cast 'Headbutt' ability on melee range target.
							new Decorator(
								ctx => yeti.Location.DistanceSqr(attackTarget.Location) <= 25*25,
								new PrioritySelector(
									new Decorator(
										ctx => yeti.Location.DistanceSqr(attackTarget.Location) <= 25* 25 && (Me.IsMoving || Me.CharmedUnit.IsMoving),
										new ActionFail(ctx => WoWMovement.ClickToMove(Me.CharmedUnit.Location))),
									new Action(ctx => Lua.DoString("CastPetAction(2)")))),
							new Decorator(ctx => yeti.Location.DistanceSqr(attackTarget.Location) > 25 * 25,
								new Action(ctx => Navigator.MoveTo(attackTarget.Location))))),
					new Decorator(
						ctx => attackTarget == null,
						new PrioritySelector(
							new Decorator(
								ctx => yeti.Location.DistanceSqr(WaitPoint) > 10 * 10,
								new PrioritySelector(
									// can't set path precision so I'll just handle it directly...
									// the yeti takes wide turns so needs a higher path precision than normal
									new Decorator(
										ctx =>
										{
											var nav = Navigator.NavigationProvider as MeshNavigator;
											if (nav == null)
												return false;
											if (nav.CurrentMovePath == null || nav.CurrentMovePath.Index >= nav.CurrentMovePath.Path.Points.Length)
												return false;
											WoWPoint point = nav.CurrentMovePath.Path.Points[nav.CurrentMovePath.Index];
											return point.DistanceSqr(yeti.Location) < 6 * 6;
										},
										new Action(ctx => ((MeshNavigator) Navigator.NavigationProvider).CurrentMovePath.Index++)),
									
									CreateBehavior_Antistuck(),

									new Action(ctx => Navigator.MoveTo(WaitPoint)))),
							new ActionSetActivity("No viable targets, waiting."))),
					new ActionAlwaysSucceed()));
		}

		public Composite CreateBehavior_GetIn()
		{
			return new Decorator(r => !Query.IsInVehicle() && !Me.IsActuallyInCombat,
				new PrioritySelector(
					new Decorator(r => Yeti != null,
						new Action(r =>
						{
							Yeti.Interact();
							Lua.DoString("SelectGossipOption(1,\"gossip\", true)");
						})),
					new Decorator(r => Yeti == null, new Action(r => Navigator.MoveTo(StartPoint)))));
		}

		private WoWUnit GetAttackTarget()
		{
			var target = Me.CurrentTarget;
			if (target != null && target.IsHostile && target.Attackable && target.IsAlive && target.DistanceSqr < 25 * 25)
			{
				return target;
			}

			var charmedUnit = Me.CharmedUnit;

			var nearestHostileAttackingPlayer =
				ObjectManager.GetObjectsOfType<WoWUnit>()
					.Where(r => r.Entry != 60754 && r.GotTarget && r.CurrentTarget == charmedUnit)
					.OrderBy(r => r.DistanceSqr)
					.FirstOrDefault();

			if (nearestHostileAttackingPlayer != null)
			{
				return nearestHostileAttackingPlayer;
			}
			return Monkies.FirstOrDefault();
		}

		public override void OnStart()
		{
			OnStart_HandleAttributeProblem();
			if (!IsDone)
			{
				TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_MainCombat());
				_useMount = CharacterSettings.Instance.UseMount;
				CharacterSettings.Instance.UseMount = false;

				this.UpdateGoalText(QuestId);
			}
		}


		protected Composite CreateBehavior_MainCombat()
		{
			return _root ?? (_root = 
				new Decorator(ret => !_isBehaviorDone,
					new PrioritySelector(
						CreateBehavior_CheckCompletion(), 
						CreateBehavior_GetIn(), 
						CreateBehavior_KillMantid())));
		}

		#region StuckHandler

		readonly WaitTimer _stuckTimer = new WaitTimer(TimeSpan.FromSeconds(2));
		private static readonly Random _rnd = new Random();

		protected Composite CreateBehavior_Antistuck()
		{
			var prevPosition = WoWPoint.Empty;
			WoWPoint myLoc = WoWPoint.Empty;
			var moveDirection = WoWMovement.MovementDirection.None;

			return new PrioritySelector(
				new Decorator(
					ctx => _stuckTimer.IsFinished,
					new Sequence(
						ctx => myLoc = WoWMovement.ActiveMover.Location,
						// checks if stuck
						new DecoratorContinue(
							ctx => myLoc.DistanceSqr(prevPosition) < 3 * 3,
							new Sequence(         
										ctx => moveDirection = GetRandomMovementDirection(),
										new Action(ctx => QBCLog.Debug("Stuck. Movement Directions: {0}", moveDirection)),
										new Action(ctx => WoWMovement.Move(moveDirection)),
										new WaitContinue(2, ctx => false, new ActionAlwaysSucceed()),
										new Action(ctx => WoWMovement.MoveStop(moveDirection)))),

						new Action(ctx => prevPosition = myLoc),
						new Action(ctx => _stuckTimer.Reset()))));
		}

		private WoWMovement.MovementDirection GetRandomMovementDirection()
		{
			// randomly move left or ritht
			WoWMovement.MovementDirection ret = _rnd.Next(2) == 0
				? WoWMovement.MovementDirection.StrafeLeft
				: WoWMovement.MovementDirection.StrafeRight;

			// randomly choose to go diagonal backwords + left or right
			if (_rnd.Next(2) == 0)
				ret |= WoWMovement.MovementDirection.Backwards;

			// randomly choose to jump (or descend if flying or swimming)
			if (_rnd.Next(2) == 0)
			{
				var activeMover = WoWMovement.ActiveMover;
				if (activeMover.IsFlying || activeMover.IsSwimming)
				{
					ret |= _rnd.Next(2) == 0
						? WoWMovement.MovementDirection.JumpAscend
						: WoWMovement.MovementDirection.Descend;
				}
				else
				{
					ret |= WoWMovement.MovementDirection.JumpAscend;
				}
			}
			return ret;
		}
		#endregion

	}
}