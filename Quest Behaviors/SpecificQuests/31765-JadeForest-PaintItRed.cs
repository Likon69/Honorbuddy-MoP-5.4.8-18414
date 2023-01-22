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


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.PaintItRed
{
	[CustomBehaviorFileName(@"SpecificQuests\31765-JadeForest-PaintItRed")]
	public class PaintItRed : CustomForcedBehavior
	{
		public PaintItRed(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				QuestId = 31765;
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
		private bool _isBehaviorDone;


		private Composite _root;


		public override bool IsDone
		{
			get
			{
				return _isBehaviorDone;
			}
		}
		private LocalPlayer Me
		{
			get { return (StyxWoW.Me); }
		}

		public override void OnStart()
		{
			OnStart_HandleAttributeProblem();
			if (!IsDone)
			{
				TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_MainCombat());

				this.UpdateGoalText(QuestId);
			}
		}


		public WoWUnit Solider
		{
			get
			{
				return 
					ObjectManager.GetObjectsOfType<WoWUnit>()
					.Where(u => u.Entry == 66200 && u.IsAlive)
					.OrderBy(u => u.Distance)
					.FirstOrDefault();
			}
		}

		public WoWUnit Cannon
		{
			get
			{
				return 
					ObjectManager.GetObjectsOfType<WoWUnit>()
					.Where(r => r.IsAlive && r.Entry == 66203)
					.OrderBy(u => u.Distance)
					.FirstOrDefault();
			}
		}

		public Composite DoneYet
		{
			get
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
		}


		private void shoot(WoWUnit who)
		{
			var v = who.Location - StyxWoW.Me.Transport.Location;
			v.Normalize();
			Lua.DoString(string.Format("local pitch = {0}; local delta = pitch - VehicleAimGetAngle(); VehicleAimIncrement(delta);", Math.Asin(v.Z)));

			//If the target is moving, the projectile is not instant
			if (who.IsMoving)
			{
				WoWMovement.ClickToMove(who.Location.RayCast(who.Rotation, 10f));
			}
			else
			{
				WoWMovement.ClickToMove(who.Location);
			}
			//Fire pew pew
			Lua.DoString("CastPetAction({0})", 1);
		}


		public Composite KillSoldier
		{
			get
			{
				return new Decorator(r => !Me.IsQuestObjectiveComplete(QuestId, 1) && Solider != null,
					new Action(r => shoot(Solider)));
			}
		}


		public Composite KillCannon
		{
			get
			{
				return new Decorator(r => !Me.IsQuestObjectiveComplete(QuestId, 2) && Cannon != null,
					new Action(r => shoot(Cannon)));
			}
		}

		public Composite EnsureTarget
		{
			get
			{
				return new Decorator(r => Me.GotTarget && !Me.CurrentTarget.IsHostile,
					new Action(r => Me.ClearTarget()));
			}
		}

		protected Composite CreateBehavior_MainCombat()
		{
			return _root ?? (_root = 
				new Decorator(ret => !_isBehaviorDone,
					new PrioritySelector(DoneYet, EnsureTarget, KillSoldier, KillCannon)));
		}


		#region Cleanup

        public override void OnFinished()
        {
            TreeHooks.Instance.RemoveHook("Combat_Main", CreateBehavior_MainCombat());
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
        }

		#endregion

	}
}



