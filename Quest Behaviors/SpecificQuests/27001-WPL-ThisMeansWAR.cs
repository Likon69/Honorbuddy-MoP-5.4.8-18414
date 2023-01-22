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
//

#region Summary and Documentation
// This behavior is tailored for the quest http://www.wowhead.com/quest=27789/troggish-troubles 
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
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.ThisMeansWAR
{
	[CustomBehaviorFileName(@"SpecificQuests\27001-WPL-ThisMeansWAR")]
	public class ThisMeansWAR : CustomForcedBehavior
	{
		public ThisMeansWAR(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;
		}


		private LocalPlayer Me { get { return (StyxWoW.Me); } }
		readonly WoWPoint _lumberMillLocation = new WoWPoint(2427.133, -1649.115, 104.0841);
		readonly WoWPoint _spiderLocation = new WoWPoint(2332.387, -1694.623, 104.5099);
		private WoWPoint _movetoPoint;
		private WoWUnit _currentTarget;

		readonly List<ulong> _blackList = new List<ulong>();
		private DateTime _stuckTimeStamp = DateTime.Now;
		private WoWPoint _lastMovetoPoint;

		public override bool IsDone
		{
			get
			{
				var quest = Me.QuestLog.GetQuestById(27001);

				var done = (quest != null && quest.IsCompleted);
				return done;
			}
		}

		public override void OnStart()
		{
			if (!IsDone)
			{
				Targeting.Instance.RemoveTargetsFilter += Instance_RemoveTargetsFilter;

				this.UpdateGoalText(0);
			}
		}

        public override void OnFinished()
        {
            Targeting.Instance.RemoveTargetsFilter -= Instance_RemoveTargetsFilter;
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
        }

		private static void Instance_RemoveTargetsFilter(List<WoWObject> units)
		{
			units.Clear();
		}

		private Composite _root;
		protected override Composite CreateBehavior()
		{
			return _root ?? (_root = new PrioritySelector(
				// if not in a turret than move to one and interact with it
				new Decorator(ret => !Query.IsInVehicle(),
					new Sequence(ctx => GetMustang(), // set Turret as context
						new DecoratorContinue(ctx => ctx != null && ((WoWUnit)ctx).DistanceSqr > 5 * 5,
							new Action(ctx =>
										   {
											   Navigator.MoveTo(((WoWUnit)ctx).Location);
											   TreeRoot.StatusText = "Moving To Mustang";
										   })),
						new DecoratorContinue(ctx => ctx != null && ((WoWUnit)ctx).DistanceSqr <= 5 * 5,
							new Sequence(
								new Mount.ActionLandAndDismount(),
								new Action(ctx =>
								{
									QBCLog.Info("Interacting with Mustang");
									((WoWUnit)ctx).Interact();
								}))))),
				// Find the nearest spider and if none exist then move to thier spawn location
					new Decorator(ret => _currentTarget == null || !_currentTarget.IsValid || !_currentTarget.IsAlive,
							new Action(ctx =>
										   {
											   _currentTarget = ObjectManager.GetObjectsOfType<WoWUnit>()
												   .Where(
													   u =>
													   u.IsAlive && !_blackList.Contains(u.Guid) && u.Entry == 44284).
												   OrderBy(u => u.DistanceSqr).FirstOrDefault();
											   if (_currentTarget == null)
											   {
												   Navigator.MoveTo(_spiderLocation);
												   QBCLog.Info("No spiders found. Moving to spawn point");
											   }
											   else
											   {
												   _movetoPoint = WoWMathHelper.CalculatePointFrom(_lumberMillLocation,
																								   _currentTarget.
																									   Location, -5);
												   QBCLog.Info("Locked on a new target. Distance {0}", _currentTarget.Distance);
											   }
										   })),


							new Sequence(
								new Action(ctx => TreeRoot.StatusText = "Scaring spider towards lumber mill"),
								new Action(ctx =>
											   { // blacklist spider if it doesn't move
												   if (DateTime.Now - _stuckTimeStamp > TimeSpan.FromSeconds(6))
												   {
													   _stuckTimeStamp = DateTime.Now;
													   if (_movetoPoint.DistanceSqr(_lastMovetoPoint) < 3 * 3)
													   {
														   QBCLog.Info("Blacklisting spider");
														   _blackList.Add(_currentTarget.Guid);
														   _currentTarget = null;
														   return RunStatus.Failure;
													   }
													   _lastMovetoPoint = _movetoPoint;
												   }
												   return RunStatus.Success;
											   }),
								new Action(ctx =>
												{
													// update movepoint
													_movetoPoint =
														WoWMathHelper.CalculatePointFrom(_lumberMillLocation,
																						_currentTarget.
																							Location, -6);
													if (_movetoPoint.DistanceSqr(Me.Location) >4 * 4)
													{
														Navigator.MoveTo(_movetoPoint);
														return RunStatus.Running;
													}
													return RunStatus.Success;
												}),
								new WaitContinue(2, ret => !Me.IsMoving, new ActionAlwaysSucceed()),
								new Action(ctx =>
											   {
												   
													   Me.SetFacing(_lumberMillLocation);
													   WoWMovement.Move(WoWMovement.MovementDirection.ForwardBackMovement);
													   WoWMovement.MoveStop(WoWMovement.MovementDirection.ForwardBackMovement);
													   //Lua.DoString("CastSpellByID(83605)");
												   
											   }),
							new WaitContinue(TimeSpan.FromMilliseconds(200), ret => false, new ActionAlwaysSucceed()),
							 new Action(ctx => Lua.DoString("CastSpellByID(83605)"))
								 )));
		}


		WoWUnit GetMustang()
		{
			return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => (u.CharmedByUnitGuid == 0 || u.CharmedByUnitGuid == Me.Guid) && u.Entry == 44836)
				.OrderBy(u => u.DistanceSqr).
				FirstOrDefault();
		}
	}
}
