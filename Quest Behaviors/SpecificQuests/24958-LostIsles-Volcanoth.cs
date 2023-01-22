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
using System.Threading.Tasks;
using Buddy.Coroutines;
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


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.Volcanoth
{
	[CustomBehaviorFileName(@"SpecificQuests\24958-LostIsles-Volcanoth")]
	public class Volcanoth : CustomForcedBehavior
	{
		public Volcanoth(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				QuestId = 24958;
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
		private Composite _behaviorTreeHook_CombatMain;
		public int QuestId { get; set; }
		private bool _isBehaviorDone;
		public int TurtleMobId = 38855;
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
				_behaviorTreeHook_CombatMain = CreateBehavior_CombatMain();
				TreeHooks.Instance.InsertHook("Combat_Main", 0, _behaviorTreeHook_CombatMain);

				this.UpdateGoalText(QuestId);
			}
		}
		
		protected virtual Composite CreateBehavior_CombatMain()
		{
			return new Decorator(context => !IsDone,
				new PrioritySelector(
					// Disable the CombatRoutine
					DoneYet, TurtleCast, TurtlePull, new ActionAlwaysSucceed()
			));
		}

		public WoWUnit Turtle
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == TurtleMobId && !u.IsDead && u.Distance < 500).OrderBy(u => u.Distance).FirstOrDefault();
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
						_isBehaviorDone = true;
						return RunStatus.Success;
					}));
			}
		}
		
		public Composite TurtleCast
		{
			get
			{
				return 
					new Decorator(c => Turtle != null && Turtle.IsCasting, 
						new ActionRunCoroutine(c => BarryDurex.QuestHelper.AvoidEnemyCast(Turtle, 80, 100))
					);
			}
		}
		
		public Composite TurtlePull
		{
			get
			{
				return
					new Decorator(c => Turtle != null && !Turtle.IsCasting, 
						new Sequence(
							new Action(c =>	Turtle.Target()),
							new DecoratorContinue(c => Turtle.Location.Distance(Me.Location) > 20,
								new Action(c =>	Navigator.MoveTo(Turtle.Location))),
							new DecoratorContinue(c => Turtle.Location.Distance(Me.Location) < 20,
								new Sequence(
									new Action(c =>	Turtle.Face()),
									new Action(c =>	WoWMovement.MoveStop()),
									new Action(c =>	Lua.DoString("UseItemByName(52043)")),
									new WaitContinue(TimeSpan.FromSeconds(1),
										context => Me.IsCasting || Turtle.IsCasting,
										new ActionAlwaysSucceed()),
									new WaitContinue(TimeSpan.FromSeconds(10),
										context => !Me.IsCasting || Turtle.IsCasting,
										new ActionAlwaysSucceed())
					))));
			}
		}
		
		protected override Composite CreateBehavior()
		{
			return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(new ActionAlwaysSucceed())));
		}
	}
}

namespace BarryDurex
{
	static class QuestHelper
	{
		public static async Task AvoidEnemyCast(WoWUnit unit, float enemyAttackRadius, float saveDistance)
		{
		    if (!StyxWoW.Me.IsFacing(unit))
		    {
		        unit.Face(); 
                await Coroutine.Sleep(300);
		    }

			float behemothRotation = getPositive(unit.RotationDegrees);
			float invertEnemyRotation = getInvert(behemothRotation);

		    WoWMovement.MovementDirection move = getPositive(StyxWoW.Me.RotationDegrees) > invertEnemyRotation 
		        ? WoWMovement.MovementDirection.StrafeRight 
		        : WoWMovement.MovementDirection.StrafeLeft;

		    try
		    {
		        while (unit.Distance2D <= saveDistance && unit.IsCasting && ((enemyAttackRadius == 0 && !StyxWoW.Me.IsSafelyBehind(unit)) 
                    ||(enemyAttackRadius != 0 && unit.IsSafelyFacing(StyxWoW.Me, enemyAttackRadius)) || unit.Distance2D <= 2 ))
		        {
		            WoWMovement.Move(move);
		            unit.Face();
		            await Coroutine.Yield();
		        }
		    }
		    finally
		    {
                WoWMovement.MoveStop();
		    }
		}

		private static float getInvert(float f)
		{
			if (f < 180)
				return (f + 180);
			//else if (f >= 180)
			return (f - 180);
		}

		private static float getPositive(float f)
		{
			if (f < 0)
				return (f + 360);
			return f;
		}
	}
}

