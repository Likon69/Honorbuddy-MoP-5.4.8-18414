// Behavior originally contributed by Chinajade.
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
// QUICK DOX:
//  This behavior locates MOBID on the HUNTINGGROUNDS.  Once the mob is located,
//  the behavior will:
//      1) Target the mob,
//      2) Close to within MOVEWITHMAXRANGEOFMOB
//      3) Terminate (with the target still selected)
//
//  If the requested mob(s) could not be locate, the toon's "current target" is cleared.
//  This enables the profile writer to conditionally perform subsequent actions
//  similar to the following:
//      <If Condition="(Me.CurrentTarget != null) &amp;&amp; (Me.CurrentTarget.Entry == 12345)">
//          <!-- whatever -->
//
//  The behavior is necessary because there is no locale-independent method of
//  selecting a target in the WoWclient LUA.
//
// BEHAVIOR ATTRIBUTES:
// *** ALSO see the documentation in QuestBehaviorBase.cs.  All of the attributes it provides
// *** are available here, also.  The documentation on the attributes QuestBehaviorBase provides
// *** is _not_ repeated here, to prevent documentation inconsistencies.
//
// Basic Attributes:
//      MobIdN [at least one FactionIdN or one MobIdN is REQUIRED]
//          Identifies the mobs on which the targeting should take place.
//          The MobIdN can represent either an NPC (WoWUnit).
//          Although a MobIdN can specify a WoWObject, WoWObjects are not
//          targetable by the WoWclient.
//
// Optional Target Qualifiers:
//      TargetOnlyIfHealthPercentAbove [optional; Default: 0.0]
//          This attribute qualifies a target that fulfills the MobIdN selection.
//          The target must have a HealthPercent AT or ABOVE the value specified
//          for this attribute to be considered a qualified target.
//      TargetOnlyIfHealthPercentBelow [optional; Default: 100.0]
//          This attribute qualifies a target that fulfills the MobIdN selection.
//          The target must have a HealthPercent AT or BELOW the value specified
//          for this attribute to be considered a qualified target.
//      TargetOnlyIfMobHasAuraIdN [optional; Default: none]
//          This attribute qualifies a target that fullfills the MobIdN or FactionIdN selection.
//          The target *must* possess an aura that matches one of the defined 
//          TargetOnlyIfMobHasAuraIdN, in order to be considered a qualified target.
//      TargetOnlyIfMobMissingAuraIdN [optional; Default: none]
//          This attribute qualifies a target that fullfills the MobIdN or FactionIdN selection.
//          The target must *not* possess an aura that matches one of the defined 
//          TargetOnlyIfMobMissingAuraIdN, in order to be considered a qualified target.
//
// Tunables:
//      IgnoreLoSToTarget [optional; Default: false]
//          If true, the behavior will not consider Line of Sight when trying to interact
//          with the selected target.
//      MoveWithinMaxRangeOfMob [optional; Default: 30.0]
//          Defines the maximum range at which the toon should be from the selected target
//          before the behavior terminates.
//          If the toon is out of range, the toon will be moved within this distance
//          of the mob.
//      WaitForNpcs [optional; Default: true]
//          This value affects what happens if there are no MobIds in the immediate area.
//          If true, the behavior will move to the next hunting ground waypoint, or if there
//          is only one waypoint, the behavior will stand and wait for MobIdN to respawn.
//          If false, and the behavior cannot locate MobIdN in the immediate area, the behavior
//          considers itself complete.
//      X/Y/Z [optional; Default: toon's current location when behavior is started]
//          This specifies the location where the toon should loiter
//          while waiting to interact with MobIdN.  If you need a large hunting ground
//          you should prefer using the <HuntingGrounds> sub-element, as it allows for
//          multiple locations (waypoints) to visit.
//          This value is automatically converted to a <HuntingGrounds> waypoint.
//
// BEHAVIOR EXTENSION ELEMENTS (goes between <CustomBehavior ...> and </CustomBehavior> tags)
// See the "Examples" section for typical usage.
//      HuntingGrounds [optional; Default: none]
//          The HuntingGrounds contains a set of Waypoints we will visit to seek mobs
//          that fulfill the quest goal.  The <HuntingGrounds> element accepts the following
//          attributes:
//              WaypointVisitStrategy= [optional; Default: Random]
//              [Allowed values: InOrder, PickOneAtRandom, Random]
//              Determines the strategy that should be employed to visit each waypoint.
//              Any mobs encountered while traveling between waypoints will be considered
//              viable.  The Random strategy is highly recommended unless there is a compelling
//              reason to otherwise.  The Random strategy 'spread the toons out', if
//              multiple bos are running the same quest.
//              The PickOneAtRandom strategy will only visit one waypoint on the list
//              and camp the mobs from the single selected waypoint.  This is another good tactic
//              for spreading toons out in heavily populated areas.
//          Each Waypoint is provided by a <Hotspot ... /> element with the following
//          attributes:
//              Name [optional; Default: X/Y/Z location of the waypoint]
//                  The name of the waypoint is presented to the user as it is visited.
//                  This can be useful for debugging purposes, and for making minor adjustments
//                  (you know which waypoint to be fiddling with).
//              X/Y/Z [REQUIRED; Default: none]
//                  The world coordinates of the waypoint.
//              Radius [optional; Default: 10.0]
//                  Once the toon gets within Radius of the waypoint, the next waypoint
//                  will be sought.
//
// THINGS TO KNOW:
// * Profiles do not run while in combat!
//      Thus, this behavior is only useful when you can arrange to run it in out-of-combat
//      situations.
//
// * Watch your PullDistance!
//      Consider the following unexpected scenario with a typical pull distance of 25...
//      1) The behavior runs, locates target 12345, moves to within 30 yards, and terminates
//      2) Honorbuddy decides to 'pull' a mob at 25 yards, and changes the target that
//          was just chosen
//      3) Honorbuddy kills the selected target, and may leave the dead mob selected,
//          or it may clear the target.
//      4) Honorbuddy runs the next statement in the profile, but the mob is not the
//          one that was selected by the behavior, thus the <If> yields unexpected results.
//
#endregion


#region Examples
// EXAMPLE:
//      <CustomBehavior File="TargetAndMoveToMob" MobId="12345" >
//          <HuntingGrounds>
//              <Hotspot X="123.0" Y="234.0" Z="345.0" />
//              <Hotspot X="543.0" Y="432.0" Z="321.0" />
//          </HuntingGrounds>
//      </CustomBehavior>
//      <If Condition="(Me.CurrentTarget != null) &amp;&amp; (Me.CurrentTarget.Entry == 12345)">
//          <!-- ...do something with selected target... -->
//      </If>
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Honorbuddy.QuestBehaviorCore.XmlElements;
using Styx;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.TargetAndMoveToMob
{
	[CustomBehaviorFileName(@"TargetAndMoveToMob")]
	public class TargetAndMoveToMob : QuestBehaviorBase
	{
		#region Constructor and Argument Processing
		public TargetAndMoveToMob(Dictionary<string, string> args)
			: base(args)
		{
			try
			{
				// NB: Core attributes are parsed by QuestBehaviorBase parent (e.g., QuestId, NonCompeteDistance, etc)

				// Primary attributes...
				MobIds = GetNumberedAttributesAsArray<int>("MobId", 1, ConstrainAs.MobId, null);

				// Qualifiers
				TargetOnlyIfHealthPercentAbove = GetAttributeAsNullable<double>("TargetOnlyIfHealthPercentAbove", false, ConstrainAs.Percent, null) ?? 0.0;
				TargetOnlyIfHealthPercentBelow = GetAttributeAsNullable<double>("TargetOnlyIfHealthPercentBelow", false, ConstrainAs.Percent, null) ?? 100.0;
				TargetOnlyIfMobHasAuraId = GetNumberedAttributesAsArray<int>("TargetOnlyIfMobHasAuraId", 0, ConstrainAs.AuraId, null);
				TargetOnlyIfMobMissingAuraId = GetNumberedAttributesAsArray<int>("TargetOnlyIfMobMissingAuraId", 0, ConstrainAs.AuraId, null);

				// Tunables...
				HuntingGroundCenter = GetAttributeAsNullable<WoWPoint>("", false, ConstrainAs.WoWPointNonEmpty, null) ?? Me.Location;
				IgnoreLoSToTarget = GetAttributeAsNullable<bool>("IgnoreLoSToTarget", false, null, null) ?? false;
				MoveWithinMaxRangeOfMob = GetAttributeAsNullable<double>("MoveWithinMaxRangeOfMob", false, null, null) ?? 30.0;
				WaitForNpcs = GetAttributeAsNullable<bool>("WaitForNpcs", false, null, null) ?? true;
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


		// Variables for Attributes provided by caller
		private WoWPoint HuntingGroundCenter { get; set; }
		private bool IgnoreLoSToTarget { get; set; }
		private int[] MobIds { get; set; }
		private double MoveWithinMaxRangeOfMob { get; set; }
		private int[] TargetOnlyIfMobHasAuraId { get; set; }
		private int[] TargetOnlyIfMobMissingAuraId { get; set; }
		private double TargetOnlyIfHealthPercentAbove { get; set; }
		private double TargetOnlyIfHealthPercentBelow { get; set; }
		private bool WaitForNpcs { get; set; }

		protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
		{
			//// EXAMPLE: 
			//UsageCheck_DeprecatedAttribute(xElement,
			//    Args.Keys.Contains("Nav"),
			//    "Nav",
			//    context => string.Format("Automatically converted Nav=\"{0}\" attribute into MovementBy=\"{1}\"."
			//                              + "  Please update profile to use MovementBy, instead.",
			//                              Args["Nav"], MovementBy));
		}

		protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
		{
			UsageCheck_SemanticCoherency(xElement,
				TargetOnlyIfHealthPercentBelow < TargetOnlyIfHealthPercentAbove,
				context => string.Format("TargetOnlyIfHealthPercentBelow({0}) must be greater than or equal to TargetOnlyIfHealthPercentAbove({1}).",
					TargetOnlyIfHealthPercentBelow,
					TargetOnlyIfHealthPercentAbove));
		}
		#endregion


		#region Private and Convenience variables
		private string GoalText
		{
			get
			{
				return
					string.Format("Looking to target mobs: {0}",
						string.Join(", ", MobIds.Select(m => Utility.GetObjectNameFromId(m)).Distinct()));
			}
		}
		private HuntingGroundsType HuntingGrounds { get; set; }
		private WoWUnit SelectedTarget { get; set; }
	    private UtilityCoroutine.NoMobsAtCurrentWaypoint _noMobsAtCurrentWaypoint;

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return "$Id$"; } }
		public override string SubversionRevision { get { return "$Rev$"; } }
		#endregion


		#region Overrides of CustomForcedBehavior

		// CreateBehavior supplied by QuestBehaviorBase.
		// Instead, provide CreateMainBehavior definition.


		// Dispose provided by QuestBehaviorBase.


		// IsDone provided by QuestBehaviorBase.
		// Call the QuestBehaviorBase.BehaviorDone() method when you want to indicate your behavior is complete.


		public override void OnStart()
		{
			// Acquisition and checking of any sub-elements go here.
			// A common example:
			HuntingGrounds =
				HuntingGroundsType.GetOrCreate(Element,
											   "HuntingGrounds",
											   new WaypointType(HuntingGroundCenter, "hunting ground center"));
			IsAttributeProblem |= HuntingGrounds.IsAttributeProblem;

			// Let QuestBehaviorBase do basic initializaion of the behavior, deal with bad or deprecated attributes,
			// capture configuration state, install BT hooks, etc.  This will also update the goal text.
			var isBehaviorShouldRun = OnStart_QuestBehaviorCore(GoalText);

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (isBehaviorShouldRun)
			{
				// Setup settings to prevent interference with your behavior --
				// These settings will be automatically restored by QuestBehaviorBase when Dispose is called
				// by Honorbuddy, or the bot is stopped.
				//CharacterSettings.Instance.HarvestHerbs = false;
				//CharacterSettings.Instance.HarvestMinerals = false;
				//CharacterSettings.Instance.LootChests = false;
				//CharacterSettings.Instance.NinjaSkin = false;
				//CharacterSettings.Instance.SkinMobs = false;
				//CharacterSettings.Instance.PullDistance = 0;
			}
		}
		#endregion


		#region Main Behaviors
		protected override Composite CreateBehavior_CombatMain()
		{
			return new PrioritySelector(
				// empty, for now
				);
		}


		protected override Composite CreateBehavior_CombatOnly()
		{
			return new PrioritySelector(
				// empty, for now
				);
		}


		protected override Composite CreateBehavior_DeathMain()
		{
			return new PrioritySelector(
				// empty, for now
				);
		}


		protected override Composite CreateMainBehavior()
		{
			return new PrioritySelector(
				// If we don't have a selected target, find one...
				new Decorator(context => !IsMobQualified(SelectedTarget),
					new PrioritySelector(
						new ActionFail(context => { SelectedTarget = FindQualifiedMob(); }),

						// No qualifed mobs in immediate vicinity...
						new Decorator(context => !IsMobQualified(SelectedTarget),
							new PrioritySelector(
								new Decorator(context => Me.GotTarget,
									new Action(context => { Me.ClearTarget(); })),

								// NB: if the terminateBehaviorIfNoTargetsProvider argument evaluates to 'true', calling
				// this sub-behavior will terminate the overall behavior.
								new ActionRunCoroutine(context =>
								        _noMobsAtCurrentWaypoint ??
								        (_noMobsAtCurrentWaypoint =
								            new UtilityCoroutine.NoMobsAtCurrentWaypoint(
								                () => HuntingGrounds,
								                () => MovementBy,
								                () => { if (!WaitForNpcs) BehaviorDone("Terminating--\"WaitForNpcs\" is false."); },
								                () => TargetExclusionAnalysis.Analyze(
								                    Element,
								                    () => Query.FindMobsAndFactions(MobIds),
								                    TargetExclusionChecks))))))
					)),

				// If qualified mob was found, move within range, if needed...
				// NB: A mob can lose its 'qualified' status for several reasons.  For instance,
				// another player moves close to or tags the mob while we're on our way to it.
				new Decorator(context => IsMobQualified(SelectedTarget),
					new PrioritySelector(
						new ActionFail(context => { Utility.Target(SelectedTarget); }),
						new Decorator(context => IsDistanceCloseNeeded(SelectedTarget),
						    new ActionRunCoroutine(
                                context => UtilityCoroutine.MoveTo(
                                    SelectedTarget.Location, 
                                    SelectedTarget.SafeName, 
                                    MovementBy))),
                        new ActionRunCoroutine(context => CommonCoroutines.StopMoving()),
						new Action(context =>
						{
							Utility.Target(SelectedTarget, true);
							BehaviorDone();
						})
					))
			);
		}
		#endregion


		#region Helpers
		private WoWUnit FindQualifiedMob()
		{
			return
			   (from wowObject in Query.FindMobsAndFactions(MobIds)
				let wowUnit = wowObject as WoWUnit
				where
					IsMobQualified(wowUnit)
				orderby wowUnit.Distance
				select wowUnit)
				.FirstOrDefault();
		}


		private bool IsDistanceCloseNeeded(WoWObject wowObject)
		{
			double targetDistance = WoWMovement.ActiveMover.Location.Distance(wowObject.Location);

			var isWithinRange =
				(targetDistance <= MoveWithinMaxRangeOfMob)
				&& (IgnoreLoSToTarget || Query.IsInLineOfSight(wowObject));

			return !isWithinRange;
		}


		private bool IsMobQualified(WoWUnit wowUnit)
		{
			return
				Query.IsViable(wowUnit)
				// Unique checks...
					&& (wowUnit.HealthPercent >= TargetOnlyIfHealthPercentAbove)
					&& (wowUnit.HealthPercent <= TargetOnlyIfHealthPercentBelow)
				// 'Core' checks...
					&& !wowUnit.IsBlacklistedForCombat()
					&& Query.IsStateMatch_IgnoreMobsInBlackspots(wowUnit, IgnoreMobsInBlackspots)
					&& !Query.IsInCompetition(wowUnit, NonCompeteDistance)
					&& Query.IsStateMatch_MeshNavigable(wowUnit, MovementBy)
				// 'Aura' checks...
					&& Query.IsStateMatch_AurasWanted(wowUnit, TargetOnlyIfMobHasAuraId)
					&& Query.IsStateMatch_AurasMissing(wowUnit, TargetOnlyIfMobMissingAuraId);
		}


		// 30May2013-08:11UTC chinajade
		private List<string> TargetExclusionChecks(WoWObject wowObject)
		{
			var exclusionReasons = TargetExclusionAnalysis.CheckCore(wowObject, this);

			TargetExclusionAnalysis.CheckAuras(exclusionReasons, wowObject, TargetOnlyIfMobHasAuraId, TargetOnlyIfMobMissingAuraId);

			var wowUnit = wowObject as WoWUnit;
			if (wowUnit != null)
			{
				if (wowUnit.HealthPercent < TargetOnlyIfHealthPercentAbove)
				{
					exclusionReasons.Add(string.Format("Health({0:F1}) < TargetOnlyIfHealthPercentAbove({1:F1})",
						wowUnit.HealthPercent, TargetOnlyIfHealthPercentAbove));
				}

				if (wowUnit.HealthPercent > TargetOnlyIfHealthPercentBelow)
				{
					exclusionReasons.Add(string.Format("Health({0:F1}) > TargetOnlyIfHealthPercentBelow({1:F1})",
						wowUnit.HealthPercent, TargetOnlyIfHealthPercentBelow));
				}
			}

			return exclusionReasons;
		}
		#endregion
	}
}