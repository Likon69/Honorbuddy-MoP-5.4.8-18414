// Behavior originally contributed by mastahg.
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

using Styx.CommonBot.Coroutines;

#region Usings

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


// ReSharper disable once CheckNamespace
namespace Honorbuddy.Quest_Behaviors.SpecificQuests.Gnomebliteration
{
	[CustomBehaviorFileName(@"SpecificQuests\27779-Uldum-Gnomebliteration")]
	public class KillGnomes : CustomForcedBehavior
	{
		public KillGnomes(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				QuestId = 27779;
				QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = QuestInLogRequirement.InLog;
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


		// Attributes provided by caller
		private int QuestId { get; set; }
		private QuestCompleteRequirement QuestRequirementComplete { get; set; }
		private QuestInLogRequirement QuestRequirementInLog { get; set; }

		// Private variables for internal state
		private readonly WaitTimer _exitTimer = new WaitTimer(TimeSpan.FromSeconds(10));
		private bool _isBehaviorDone;
		private Composite _root;
		private readonly Stopwatch _doingQuestTimer = new Stopwatch();

		private bool IsOnFinishedRun { get; set; }
		private LocalPlayer Me { get { return (StyxWoW.Me); } }


		private Composite DoDps
		{
			get
			{
				return new PrioritySelector(
					new Decorator(ret => RoutineManager.Current.CombatBehavior != null,
						RoutineManager.Current.CombatBehavior),
					new Action(c => RoutineManager.Current.Combat()));
			}
		}


		private Composite DoneYet
		{
			get
			{
				return
					new Decorator(ret => Me.IsQuestComplete(QuestId),
						new Action(delegate
						{
							TreeRoot.StatusText = "Finished!";
							_isBehaviorDone = true;
							return RunStatus.Success;
						}));
			}
		}


		private List<WoWUnit> Enemies
		{
			get
			{
				var myLoc = Me.Location;
				return
				   (from unit in ObjectManager.GetObjectsOfType<WoWUnit>()
					where 
						unit.IsValid
						&& (unit.Entry == 46384)
						&& unit.IsAlive
						&& (unit.HealthPercent >= 100)
						&& !Blacklist.Contains(unit, BlacklistFlags.Interact)
					let loc = unit.Location
					where unit.Location.DistanceSqr(_badBomb) > 60*60
					orderby loc.DistanceSqr(myLoc)
					select unit)
					.ToList();
			}
		}


		private Composite Combat
		{
			get
			{
				return
					new Decorator(ret => Me.Combat,
						DoDps);
			}
		}


		private Composite RunEmOver
		{
			get
			{
				return 
					new PrioritySelector(ctx => Enemies,
						new Decorator(
							ret => WoWMovement.ActiveMover.MovementInfo.IsFalling,
							new ActionAlwaysSucceed()),
						new Decorator(ret => _exitTimer.IsFinished && !((List<WoWUnit>)ret).Any(),
							new Action(ret => Lua.DoString("VehicleExit()"))),
						// Leave vehicle if time is pass.
						new Decorator(ret => _doingQuestTimer.ElapsedMilliseconds >= 180000,
							new Sequence(
								new Action(ret => _doingQuestTimer.Restart()),
								new Action(ret => Lua.DoString("VehicleExit()")),
								new Sleep(4000)
						)),
						new ActionFail(ret =>
							{
								var closeBys = ((List<WoWUnit>) ret).Where(u => u.DistanceSqr < 10f*10f);

								foreach (var unit in closeBys)
								{
									Blacklist.Add(unit, BlacklistFlags.Interact, TimeSpan.FromMinutes(10));
								}
							}),
						new Decorator(ret => Me.IsOnTransport && ((List<WoWUnit>)ret).Any(),
							new Action(r => Navigator.MoveTo(((List<WoWUnit>)r)[0].Location))));
			}
		}

		//<Vendor Name="Fusion Core" Entry="46750" Type="Repair" X="" />

		readonly WoWPoint _orbLoc = new WoWPoint(-10641.33,-2344.599,144.8416);
		//<Vendor Name="Crazed Gnome" Entry="46384" Type="Repair" X="-10542.87" Y="-2411.554" Z="88.44117" />
		readonly WoWPoint _badBomb = new WoWPoint(-10561.68, -2429.371, 91.56037);


		private WoWUnit Orb
		{
			get
			{
				return 
					ObjectManager.GetObjectsOfType<WoWUnit>()
					.FirstOrDefault(u => u.IsValid && (u.Entry == 46750));
			}
		}


		private Composite FlyClose
		{
			get
			{
				return
					new Decorator(ret => !Me.IsOnTransport && (Orb == null || Orb.DistanceSqr > 5 * 5),
						new Action(r => Flightor.MoveTo(_orbLoc)));
			}
		}


		private Composite Interact
		{
			get
			{
				return new PrioritySelector(
					ctx => Orb,
					new Decorator(
						ret => !Me.IsOnTransport && ret != null && ((WoWUnit)ret).Distance <= 5,
						new Sequence(
							new DecoratorContinue(ctx => Me.Mounted, 
								new Sequence(
                                    new ActionRunCoroutine(context => CommonCoroutines.Dismount("interacting with orb")),
									new WaitContinue(2, ctx => !Me.Mounted, new ActionAlwaysSucceed()))),
						new Action(ctx => ((WoWUnit)ctx).Interact()),                                    
						new WaitContinue(2, ctx => GossipFrame.Instance.IsVisible, new ActionAlwaysSucceed()),
						new Action(
							delegate
							{
								_exitTimer.Reset();
								Lua.DoString("SelectGossipOption(1)");
							}))));
			}
		}

  
		private Composite CreateBehavior_CombatMain()
		{

			return _root ?? (_root =
				new Decorator(ret => !_isBehaviorDone,
					new PrioritySelector(
						DoneYet,
						Combat,
						FlyClose,
						Interact,
						RunEmOver)));
		}

		

		#region Overrides of CustomForcedBehavior

		public override bool IsDone
		{
			get
			{
				return (_isBehaviorDone     // normal completion
						|| !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
			}
		}


		public override void OnFinished()
		{
			// Defend against being called multiple times (just in case)...
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
			// This reports problems, and stops BT processing if there was a problem with attributes...
			// We had to defer this action, as the 'profile line number' is not available during the element's
			// constructor call.
			OnStart_HandleAttributeProblem();
			_doingQuestTimer.Start();
			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (!IsDone)
			{
				TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_CombatMain());

				this.UpdateGoalText(QuestId);
			}
		}
		#endregion
	}
}