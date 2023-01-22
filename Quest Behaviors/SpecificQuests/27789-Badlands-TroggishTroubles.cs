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

// This behavior is tailored for the quest http://www.wowhead.com/quest=27789/troggish-troubles 

#region Summary and Documentation
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;

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


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.TroggishTroubles
{
	[CustomBehaviorFileName(@"SpecificQuests\27789-Badlands-TroggishTroubles")]
	public class TroggishTroubles : CustomForcedBehavior
	{
		private const int QuestId = 27789;
		private const uint StonevaultRuffianId = 46711;
		private const uint StonevaultGoonId = 46712;
		private readonly WoWPoint _questLocation = new WoWPoint(-7077.73, -3252.417, 241.7727);
		private Composite _root;

		public TroggishTroubles(Dictionary<string, string> args) : base(args)
		{
			QBCLog.BehaviorLoggingContext = this;
		}

		private LocalPlayer Me
		{
			get { return (StyxWoW.Me); }
		}


		public override bool IsDone
		{
			get
			{
				var quest = Me.QuestLog.GetQuestById(QuestId);
				return quest == null || quest.IsCompleted;
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

		#region Behavior

		protected Composite CreateBehavior_CombatMain()
		{
			WoWUnit turret = null;
			WoWUnit selectedTarget = null;

			return _root ??
				   (_root =
					   new Decorator(
						   ctx => !IsDone,
						   new PrioritySelector(
							   // if not in a turret than move to one and interact with it 
							   new Decorator(ret => !Query.IsInVehicle(),
								   new PrioritySelector(
									   ctx => turret = GetTurret(),
									   new Decorator(ctx => turret == null,
										   new Action(ctx => Navigator.MoveTo(_questLocation))),
									   new Decorator(ctx => !turret.WithinInteractRange,
										   new Action(ctx => Navigator.MoveTo(turret.Location))),
									   new Action(ctx => turret.Interact()))),
							   // use Power Burst ability if 5 or more mobs are within 10 units of bot.
							   new Decorator(ctx => MobCountAtLocation(Me.Location, 10, StonevaultRuffianId, StonevaultGoonId) >= 5,
								   new Sequence(
									   new Action(ctx => CastPowerBurst() ? RunStatus.Success : RunStatus.Failure),
									   new Action(ctx => QBCLog.Info("Used Power Burst")))),
							   // aim and shoot
							   new PrioritySelector(ctx => selectedTarget = GetBestTarget(),
								   new Decorator(ctx => selectedTarget != null,
									   new Sequence(
										   new Action(ctx => WoWMovement.ClickToMove(selectedTarget.Location)),
										   new Action(ctx => Shoot())))))));
		}

		private bool CastPowerBurst()
		{
			return
				Lua.GetReturnVal<bool>(
					"if GetPetActionCooldown(2) == 0 then CastPetAction(2) return 1 else return nil end",
					0);
		}

		private void Shoot()
		{
			Lua.DoString("CastPetAction(1)");
		}

		private WoWUnit GetBestTarget()
		{
			var myLoc = Me.Location;
			return (from unit in ObjectManager.GetObjectsOfType<WoWUnit>()
				where unit.IsAlive && (unit.Entry == StonevaultRuffianId || unit.Entry == StonevaultGoonId)
				let loc = unit.Location
				orderby MobCountAtLocation(loc, 10, StonevaultRuffianId, StonevaultGoonId) descending
				orderby loc.DistanceSqr(myLoc)
				select unit).FirstOrDefault();
		}

		private int MobCountAtLocation(WoWPoint point, float radius, params uint[] mobIds)
		{
			var radiusSqr = radius*radius;
			return
				ObjectManager.GetObjectsOfTypeFast<WoWUnit>()
					.Count(u => u.IsAlive && mobIds.Contains(u.Entry) && u.Location.DistanceSqr(point) <= radiusSqr);
		}

		private WoWUnit GetTurret()
		{
			return
				ObjectManager.GetObjectsOfTypeFast<WoWUnit>()
					.Where(u => (u.CharmedByUnitGuid == 0 || u.CharmedByUnitGuid == Me.Guid) && u.Entry == 46707)
					.OrderBy(u => u.DistanceSqr)
					.FirstOrDefault();
		}

		#endregion

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