// Behavior originally contributed by mastahg / rework by chinajade
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
using System.Xml.Linq;

using Bots.Grind;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.TheLessonoftheBalancedRock
{
	[CustomBehaviorFileName(@"SpecificQuests\29663-PandaStarter-TheLessonoftheBalancedRock")]
	public class TheLessonoftheBalancedRock : QuestBehaviorBase
	{
		#region Constructor and Argument Processing
		public TheLessonoftheBalancedRock(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				QuestId = 29663;
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

		protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
		{
			// empty, for now (see TEMPLATE_QB.cs for example)...
		}

		protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
		{
			// empty, for now (see TEMPLATE_QB.cs for example)...
		}
		#endregion

		
		#region Private and Convenience variables
		private const int AuraId_RideVehicle = 103030;
		private const int MobId_BalancePole = 54993;
		private const int MobId_ExitPole = 57626;
		private const int MobId_TushuiMonk = 55019;
		private const int MobId_TushuiMonk2 = 65468;
		private readonly WoWPoint StartingSpot = new WoWPoint(966.1218, 3284.928, 126.7932);

		private WoWUnit SelectedMonk { get; set; }
		#endregion


		#region Overrides of CustomForcedBehavior
		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return "$Id$"; } }
		public override string SubversionRevision { get { return "$Rev$"; } }

		
		public override void OnFinished()
		{
			// Defend against being called multiple times (just in case)...
			if (!IsOnFinishedRun)
			{
				// QuestBehaviorBase.OnFinished() will set IsOnFinishedRun...
				base.OnFinished();
			}
		}


		public override void OnStart()
		{
			// Let QuestBehaviorBase do basic initialization of the behavior, deal with bad or deprecated attributes,
			// capture configuration state, install BT hooks, etc.  This will also update the goal text.
			var isBehaviorShouldRun = OnStart_QuestBehaviorCore();

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (isBehaviorShouldRun)
			{
				// The Poles for this quest are "Vehicles"...
				// Combat Routines simply get confused while in vehicles,
				// so we turn off the Combat Routine while we run this behavior.
				// The QuestBehaviorBase.OnFinished() action will restore this change.
				LevelBot.BehaviorFlags &=
					~(BehaviorFlags.Combat
						| BehaviorFlags.Loot
						| BehaviorFlags.Pull
						| BehaviorFlags.Vendor);
			}
		}
		#endregion


		#region Main Behaviors
		protected override Composite CreateBehavior_QuestbotMain()
		{
			return new Decorator(context => !IsDone && !Me.IsDead,
				new PrioritySelector(
					DoneYet(),
					MoveToStart(),
					RestAndHeal(),
					GetOnPole(),
					PoleCombat(),
					new ActionAlwaysSucceed()
				));
		}
		#endregion


		#region Helpers
		private Composite DoneYet()
		{
			return new Decorator(context => Me.IsQuestComplete(QuestId),
				new PrioritySelector(
					// Get off pole...
					new Decorator(context => Query.IsInVehicle(),
						new Action(context => { Utility.ExitVehicle(); })),

					// Mark as complete...
					new Action(context =>
					{
						BehaviorDone("Finished!");
						return RunStatus.Success;
					})
				));
		}


		private WoWUnit FindMonk()
		{
			using (StyxWoW.Memory.AcquireFrame())
			{
				return
				   (from wowUnit in ObjectManager.GetObjectsOfType<WoWUnit>()
					where
						IsMonkPersuable(wowUnit)
					orderby wowUnit.DistanceSqr
					select wowUnit)
					.FirstOrDefault();
			}
		}


		private IEnumerable<WoWUnit> FindPoles()
		{
			return
			   (from wowUnit in ObjectManager.GetObjectsOfType<WoWUnit>()
				where
					(wowUnit.Entry == MobId_BalancePole || wowUnit.Entry == MobId_ExitPole)
					&& wowUnit.NpcFlags == 16777216
				select wowUnit);
		}


		private bool IsMonkPersuable(WoWUnit wowUnit)
		{
			return
				Query.IsViable(wowUnit)
				&& (wowUnit.Entry == MobId_TushuiMonk || wowUnit.Entry == MobId_TushuiMonk2)
				&& wowUnit.HasAura(AuraId_RideVehicle)
				&& Query.IsViableForFighting(wowUnit)
				&& !Query.IsInCompetition(wowUnit, 10);   // Don't go after another player's monk
		}


		private Composite GetOnPole()
		{
			return new PrioritySelector(
				new Decorator(r => !Query.IsInVehicle(),
					new CompositeThrottle(TimeSpan.FromMilliseconds(500),
						new Action(r =>
						{
							var firstPole =
							   (from pole in FindPoles()
								orderby pole.DistanceSqr
								select pole)
								.FirstOrDefault();
								
							if (firstPole != null)
							{
								firstPole.Interact(true);
								// Disable combat routine while on pole...
								LevelBot.BehaviorFlags &=
									~(BehaviorFlags.Combat
										| BehaviorFlags.Loot
										| BehaviorFlags.Pull
										| BehaviorFlags.Vendor);
							}
						})
					))
				);
		}


		private Composite MoveToStart()
		{
			return new Decorator(r => !Query.IsInVehicle() && Me.Location.Distance(StartingSpot) > 10,
					new Action(r => Navigator.MoveTo(StartingSpot)));
		}


		private Composite PoleCombat()
		{
			return new PrioritySelector(
				// Pick a target, if we don't have one...
				new Decorator(context => !IsMonkPersuable(SelectedMonk),
					new Action(context => { SelectedMonk = FindMonk(); })),

				// Make certain target stays selected...
				new Decorator(context => Me.CurrentTarget != SelectedMonk,
					new ActionFail(context => { SelectedMonk.Target(); })),

				// If we are within melee range of target, spank it...
				new Decorator(r => SelectedMonk.IsWithinMeleeRange,
                    new ActionRunCoroutine(context => UtilityCoroutine.MiniCombatRoutine())),

				// If we are out of range of target, move closer...
				new Decorator(r => !SelectedMonk.IsWithinMeleeRange,
					new CompositeThrottle(TimeSpan.FromMilliseconds(500),
						new Action(delegate
						{
							var bestPole =
							   (from pole in FindPoles()
								where
									pole.WithinInteractRange
								orderby pole.Location.DistanceSqr(SelectedMonk.Location)
								select pole)
								.FirstOrDefault();

							// If we "can't get there from here", then jump own and start over...
							if (bestPole == null)
								{ Utility.ExitVehicle(); }

							// Otherwise, move to the next best pole...
							else
							{
								bestPole.Interact(true);
								// Reset the stuck handler so it doesn't false positive...
								Navigator.NavigationProvider.StuckHandler.Reset();
							}
						})))
			);
		}


		private Composite RestAndHeal()
		{
			return new PrioritySelector(
				// Get off pole and rest, if we are between fights, and don't have enough health to start next fight...
				new Decorator(context => !IsMonkPersuable(SelectedMonk) && Query.IsInVehicle() && (Me.HealthPercent < 50),
					new Action(context => { Utility.ExitVehicle(); })),

				// Rest until health is safe...
				new Decorator(context => !Query.IsInVehicle() && (Me.HealthPercent < 95),
					new Action(context =>
						{
							// Allow Combat Routine to run while resting...
							// With this, we can defend ourself, and use any healing resources the class may have
							LevelBot.BehaviorFlags |= (BehaviorFlags.Combat | BehaviorFlags.Rest);
						}))
				);
		}
		#endregion
	}
}
