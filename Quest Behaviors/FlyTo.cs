// Behavior originally contributed by Unknown / rework by chinajade
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
// FLYTO moves to the specified destination using a flying mount, if possible.  If we
// cannot fly in the area, this behavior will use ground travel to move to the destination.
// You may specify a single destination, or have FLYTO pick from one of several possible destinations.
//
// BEHAVIOR ATTRIBUTES:
// *** ALSO see the documentation in QuestBehaviorBase.cs.  All of the attributes it provides
// *** are available here, also.  The documentation on the attributes QuestBehaviorBase provides
// *** is _not_ repeated here, to prevent documentation inconsistencies.
//
// Basic Attributes:
//      X/Y/Z [required, if <DestinationChoices> sub-element not specified; Default: none]
//          This specifies the location to which the toon should travel.
//          This value is automatically converted to a <DestinationChoices> waypoint.
//
// Tunables:
//      AllowedVariance [optional; Default: 0.0; RECOMMENDED: 7.0]
//          ***It is HIGHLY recommended you make this value somewhere around 7.0 - 10.0.  The default value
//          of zero is to maintain backward compatibility for existing profiles.***
//			This value is used to:
//			* Prevent toons running the same profile from 'stacking up' on each other once they arrive
//			* Defeat WoWserver-side LCP detection
//			This value represents a radius.  A fractional percentage of this radius will be added
//			to the specified X/Y/Z in a random direction, and that new point used for the final destination.
//			The effect is that X/Y/Z no longer defines a 'landing point', but instead, a 'landing zone'.
//			The final destination is always selected in a sane fashion, so boundary cases like boat
//			docks and blimp towers should not be a concern.
//          By default, this value will move to the exact X/Y/Z specified.  It is HIGHLY recommended you
//          allow a more 'fuzzy' destination by setting this value from 7.0 - 10.0.  This will help
//          abate automated WoWserver-side detection, and make the toons look more 'human like' when
//          they are waiting for boats and whatnot.
//          N.B.: This AllowedVariance is only associated with the X/Y/Z specified in the FlyTo proper.
//          If you use the <DestinationChoices> sub-element-form of FlyTo, you specify an AllowedVariance
//          with each <Hotspot> in the destination choices.
//      ArrivalTolerance [optional;  Default: 1.5]
//			The distance to X/Y/Z at which we can declare we have 'arrived'.  Once we are within ArrivalTolerance
//			of the destination, landing procedures will be conducted if the caller has specified.  Otherwise,
//			the behavior simply terminates.
//          N.B.: This ArrivalTolerance is only associated with the X/Y/Z specified in the FlyTo proper.
//          If you use the <DestinationChoices> sub-element-form of FlyTo, you specify an ArrivalTolerance
//          with each <Hotspot> in the destination choices.
//		DestName [optional; Default:  X/Y/Z location of the waypoint]
//			A human-readable name that should be associated with the provided X/Y/Z.
//      IgnoreIndoors [optional; Default: false]
//			If set to true, the behavior will employ alternate heuristics in an attempt
//			to navigate.
//			It is best to leave this set to false.
//      Land [optional; Default: false]
//			If set to true, FLYTO will land upon reaching the destination.
//			The toon will look for a suitable landing site (on rough terrain), so
//			the final location may not be the same as that specified in X/Y/Z.
//
// BEHAVIOR EXTENSION ELEMENTS (goes between <CustomBehavior ...> and </CustomBehavior> tags)
// See the "Examples" section for typical usage.
//      DestinationChoices [required, if X/Y/Z is not specified; Default: none]
//          The DestinationChoices contains a set of Waypoints.  ONE OF these waypoints will be randomly 
//			selected as the FLYTO destination.  This is useful for the following purposes:
//				* Entering a large grinding area from multiple points
//					This helps toons 'fan out' if there's competition in the area. It also prevents
//					users from noticing bots landing at the same spot to start their grind.
//				* Fanning out into human-congested areas
//          Each Waypoint is provided by a <Hotspot ... /> element with the following
//          attributes:
//              Name [optional; Default: X/Y/Z location of the waypoint]
//                  The name of the waypoint is presented to the user as it is visited.
//                  This can be useful for debugging purposes, and for making minor adjustments
//                  (you know which waypoint to be fiddling with).
//              X/Y/Z [REQUIRED; Default: none]
//                  The world coordinates of the waypoint.
//				AllowedVariance [optional; Default: 7.0]
//					This value is used to:
//					* Prevent toons running the same profile from 'stacking up' on each other once they arrive
//					* Defeat WoWserver-side LCP detection
//					This value represents a radius.  A fractional percentage of this radius will be added
//					to the specified X/Y/Z in a random direction, and that new point used for the final destination.
//					The effect is that X/Y/Z no longer defines a 'point', but instead, a 'landing zone'.
//					The final destination is always selected in a sane fashion, so boundary cases like boat
//					docks and blimp towers should not be a concern.
//					We recommend you allow this value to default.  However, there may be the occasion that you need to
//					land on an exact point to prevent drawing aggro from a particular mob.  In this case, it is
//					appropriated to set the AllowedVariance to zero.
//              ArrivalTolerance [optional; Default: 1.5]
//					The distance to X/Y/Z at which we can declare we have 'arrived'.  Once we are 
//					within ArrivalTolerance of the destination, landing procedures will be conducted
//					if the caller has specified.  Otherwise, the behavior simply terminates.
//
// THiNGS TO KNOW:
// * LCP article: http://iseclab.org/papers/botdetection-article.pdf
//	
#endregion


#region Examples
// SIMPLE FLYTO:
//		<CustomBehavior File="FlyTo" DestName="Cathedral Square mailbox"
//                      X="-8657.595" Y="775.6388" Z="96.99747" AllowedVariance="5.0" />
//
// PRECISE LANDING DESTINATION:
// When stealing the Thunderbluff Flame for "A Thief's Reward", it is important to land in a precise location
// to prevent unnecessarily aggroing guards:
//		<CustomBehavior File="FlyTo" DestName="Thunderbluff flame" Land="true" AllowedVariance="0.0"
//						X="-1053.131" Y="284.7893" Z="133.8197" />
//		
//
// "FANNING OUT" INTO A HUMAN-CONGESTED AREA:
// Entering a human-congested area makes it very easy to spot bots, if they all land at the same exact location.
// This problem is *especially* problematical for seasonal-type profiles where large chunks of the Community are
// all running the same profile through heavily-congested areas.  This technique allows the Community members
// running such profiles to 'scatter' upon arrival to congested areas, such that we do not draw attention.
//		<CustomBehavior File="FlyTo" Land="true" >
//			<DestinationChoices>
//				<Hotspot DestName="Stormwind: Backgate Bank" X="-8360.063" Y="620.2231" Z="95.35557" AllowedVariance="7.0" />
//				<Hotspot DestName="Stormwind: Canal mailbox" X="-8752.236" Y="561.497" Z="97.43406" AllowedVariance="7.0" /> 
//				<Hotspot DestName="Stormwind: Cathedral Square mailbox" X="-8657.595" Y="775.6388" Z="96.99747" AllowedVariance="3.0" />
//				<Hotspot DestName="Stormwind: Elder's mailbox" X="-8859.798" Y="640.8622" Z="96.28608" AllowedVariance="5.0" />
//				<Hotspot DestName="Stormwind: Fishing pier mailbox"  X="-8826.954" Y="729.8922" Z="98.42244" AllowedVariance="7.0" />
//			</DestinationChoices>
//		</CustomBehavior>
//
// PICKING A GRIND AREA START POINT:
// Here we want to enter a grind area from several possible starting points.  Once we arrive, we choose to remain
// on foot while we grind.  By picking one of several possible starting points, we are less obvious to bot watchers
// and other players that may be in the same area.
//		<CustomBehavior File="FlyTo" Land="true" AllowedVariance="7.0" >
//			<DestinationChoices>
//				<Hotspot Name="Warmaul Hill: main path up" X="-1076.62" Y="8726.684" Z="78.98088" AllowedVariance="7.0" />
//				<Hotspot Name="Warmaul Hill: cauldren on lower plateau" X="-1002.597" Y="8981.075" Z="94.9998" AllowedVariance="7.0" /> 
//				<Hotspot Name="Warmaul Hill: mid-plateau fire banner" X="-753.0932" Y="8774.961" Z="183.0739" AllowedVariance="7.0" />
//				<Hotspot Name="Warmaul Hill: mid-plateau path down" X="-769.6554" Y="8864.765" Z="182.0117" AllowedVariance="7.0" />
//			</DestinationChoices>
//		</CustomBehavior>
//
//		<SetGrindArea>
//			<GrindArea>
//				<TargetMinLevel>60</TargetMinLevel>
//				<TargetMaxLevel>76</TargetMaxLevel>
//				<!-- Use Factions OR MobIds, usually not both -->
//				<Factions>1693</Factions>
//				<!-- <MobIds></MobIds> -->
//				<MaxDistance>150</MaxDistance>
//				<RandomizeHotspots>true</RandomizeHotspots>
//				<Hotspots>
//					<Hotspot Name="Warmaul Hill: main path up" X="-1076.62" Y="8726.684" Z="78.98088" />
//					<Hotspot Name="lower-plateau small cave" X="-1129.587" Y="8986.811" Z="103.3183" />
//					<Hotspot Name="Warmaul Hill: cauldren on lower plateau" X="-1002.597" Y="8981.075" Z="94.9998" />
//					<Hotspot Name="path to upper plateau" X="-1003.514" Y="8870.865" Z="137.3745" />
//					<Hotspot Name="Warmaul Hill: mid-plateau path down" X="-769.6554" Y="8864.765" Z="182.0117" />
//					<Hotspot Name="mid-plateau cave: U turn" X="-804.4118" Y="8728.973" Z="179.524" />
//					<Hotspot Name="mid-plasteu cave: forked room" X="-836.8087" Y="8684.212" Z="181.179" />
//					<Hotspot Name="mid-plateau cave: back room" X="-911.4927" Y="8671.196" Z="171.3158" />
//					<Hotspot Name="mid-plateau cave: dias area" X="-876.7471" Y="8748.412" Z="174.8344" />
//					<Hotspot Name="Warmaul Hill: mid-plateau fire banner" X="-753.0932" Y="8774.961" Z="183.0739" />
//					<Hotspot Name="mid-plateau main cave entrance" X="-615.0732" Y="8805.139" Z="201.9375" />
//					<Hotspot Name="mid-plateau main cave backside" X="-452.9572" Y="8722.715" Z="182.6988" />
//					<Hotspot Name="mid-plateau back entrance" X="-396.6419" Y="8803.345" Z="216.8268" />
//					<Hotspot Name="mid-plateau back circle" X="-536.6176" Y="8882.902" Z="230.6543" />
//					<Hotspot Name="Cho'war the Pillager area" X="-474.5793" Y="8854.279" Z="239.5755" />
//				</Hotspots>
//			</GrindArea>
//		</SetGrindArea>
//		<GrindTo Nav="Run" Condition="..."/>
//
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;

using Bots.Grind;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Honorbuddy.QuestBehaviorCore.XmlElements;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;

#endregion


namespace Honorbuddy.Quest_Behaviors.FlyTo
{
	[CustomBehaviorFileName(@"FlyTo")]
	class FlyTo : QuestBehaviorBase
	{
		#region Constructor and Argument Processing
		public FlyTo(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				DefaultAllowedVariance = GetAttributeAsNullable<double>("AllowedVariance", false, new ConstrainTo.Domain<double>(0.0, 50.0), null)
					?? 0.0;
                DefaultArrivalTolerance = GetAttributeAsNullable<double>("ArrivalTolerance", false, new ConstrainTo.Domain<double>(1.5, 30.0), new string[] { "Distance" })
					?? 1.5;
				DefaultDestination = GetAttributeAsNullable<WoWPoint>("", false, ConstrainAs.WoWPointNonEmpty, null);
				DefaultDestinationName = GetAttributeAs<string>("DestName", false, ConstrainAs.StringNonEmpty, new[] { "Name" })
										 ?? ((DefaultDestination != null) ? DefaultDestination.ToString() : string.Empty);
				IgnoreIndoors = GetAttributeAsNullable<bool>("IgnoreIndoors", false, null, null) ?? false;
				Land = GetAttributeAsNullable<bool>("Land", false, null, null) ?? false;

				// Make certain ArrivalTolerance is coherent with Navigator.PathPrecision...
				if (DefaultArrivalTolerance < Navigator.PathPrecision)
				{
					QBCLog.DeveloperInfo("ArrivalTolerance({0:F1}) is less than PathPrecision({1:F1})."
										 + "  Setting ArrivalTolerance to be PathPrecision to prevent navigational issues.",
										 DefaultArrivalTolerance,
										 Navigator.PathPrecision);
					DefaultArrivalTolerance = Navigator.PathPrecision;
				}
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
			// For examples, see Development/TEMPLATE_QB.cs
		}


		protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
		{
			// For examples, see Development/TEMPLATE_QB.cs
		}


		// Attributes provided by caller
		private double DefaultArrivalTolerance { get; set; }
		private WoWPoint? DefaultDestination { get; set; }
		private string DefaultDestinationName { get; set; }
		private double DefaultAllowedVariance { get; set; }
		private bool Land { get; set; }
		private bool IgnoreIndoors { get; set; }
		#endregion


		#region Private and Convenience variables
		private WoWPoint? FinalDestination { get; set; }
		private WaypointType RoughDestination { get; set; }
		private HuntingGroundsType PotentialDestinations { get; set; }
		#endregion


		#region Overrides of CustomForcedBehavior
		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }

		// CreateBehavior supplied by QuestBehaviorBase.
		// Instead, provide CreateMainBehavior definition.

		// Dispose provided by QuestBehaviorBase.

		// IsDone provided by QuestBehaviorBase.
		// Call the QuestBehaviorBase.BehaviorDone() method when you want to indicate your behavior is complete.

		// OnFinished provided by QuestBehaviorBase.

		public override void OnStart()
		{
			// 'Destination choices' processing...
			// NB: We had to defer this processing from the constructor, because XElement isn't available
			// to parse child XML nodes until OnStart() is called.
			PotentialDestinations =
				HuntingGroundsType.GetOrCreate(Element,
											   "DestinationChoices",
											   (DefaultDestination.HasValue
												? new WaypointType(DefaultDestination.Value, DefaultDestinationName, DefaultAllowedVariance, DefaultArrivalTolerance)
												: null));
			IsAttributeProblem |= PotentialDestinations.IsAttributeProblem;

			// Let QuestBehaviorBase do basic initializaion of the behavior, deal with bad or deprecated attributes,
			// capture configuration state, install BT hooks, etc.  This will also update the goal text.
			var isBehaviorShouldRun = OnStart_QuestBehaviorCore();

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (isBehaviorShouldRun)
			{
				// Disable any settings that may cause us to dismount --
				// When we mount for travel via FlyTo, we don't want to be distracted by other things.
				// NOTE: the ConfigMemento in QuestBehaviorBase restores these settings to their
				// normal values when OnFinished() is called.
				LevelBot.BehaviorFlags &= ~(BehaviorFlags.Loot | BehaviorFlags.Pull);

				// Clear any existing POI (after we've disabled Pull/Loot behaviors)...
				// Otherwise, FlyTo can get stuck trying to pursue the previous POI, if one was
				// set immediately before the behavior was launched.  This is a boundary condition,
				// and it happens frequently enough to be really annoying.
				BotPoi.Clear();

				// Pick our destination proper...
				PotentialDestinations.WaypointVisitStrategy = HuntingGroundsType.WaypointVisitStrategyType.PickOneAtRandom;
				RoughDestination = PotentialDestinations.CurrentWaypoint();

				var actionDescription = string.Format("Flying to '{0}' ({1})", RoughDestination.Name, RoughDestination.Location);
				this.UpdateGoalText(QuestId, actionDescription);
				TreeRoot.StatusText = actionDescription;
			}
		}
		#endregion


		#region Main Behaviors
		protected override Composite CreateMainBehavior()
		{
			return new ActionRunCoroutine(ctx => MainCoroutine());
		}


		private async Task<bool> MainCoroutine()
		{
			var activeMover = WoWMovement.ActiveMover;
			if (activeMover == null)
				return false;

			var immediateDestination = FindImmediateDestination();

			// If we've no way to reach destination, inform user and quit...
            if (!Flightor.CanFly && !Navigator.CanNavigateWithin(Me.Location, immediateDestination, (float)DefaultArrivalTolerance))
			{
				QBCLog.Fatal("Toon doesn't have flying capability in this area, and there is no ground path to the destination."
							 + " Please learn the flying skill appropriate for this area.");
				BehaviorDone();
				return false;
			}

			// Arrived at destination?
			if (AtLocation(activeMover.Location, immediateDestination))
			{
				var completionMessage = string.Format("Arrived at destination '{0}'", RoughDestination.Name);

				// Land if we need to...
				// NB: The act of landing may cause us to exceed the ArrivalTolerance specified.
				if (Land && Me.Mounted)
				{
					await UtilityCoroutine.LandAndDismount(string.Format("Landing at destination '{0}'", RoughDestination.Name));
					BehaviorDone(completionMessage);
					return true;
				}

				// Done...
				BehaviorDone(completionMessage);
				return false;
			}

			// Do not run FlyTo when there is a PoI set...
			if (BotPoi.Current.Type != PoiType.None)
			{
				await Coroutine.Sleep(TimeSpan.FromSeconds(10));
				QBCLog.DeveloperInfo("FlyTo temporarily suspended due to {0}", BotPoi.Current);
				return true;
			}

			// Move closer to destination...
			Flightor.MoveTo(immediateDestination, !IgnoreIndoors);
			return true;
		}
		#endregion


		#region Helpers
		// NB: We cannot calculate our final (variant) destination early on, because we may be traveling
		// large distances.  FanOutRandom() needs to be able to 'see' terrain features, WMOs, etc
		// that need to be considered when calculating the final (variant) point.
		// WMOs in particular need to be 'visible' for FanOutRandom() to take them into proper consideration.
		// This means we must be reasonably near the destination before the FanOutRandom() is called.
		private WoWPoint FindImmediateDestination()
		{
			// If we have our final destination, use it...
			if (FinalDestination.HasValue)
			{
				return FinalDestination.Value;
			}

			var distanceToPickVariantDestinationSqr = Math.Max(50.0, RoughDestination.ArrivalTolerance + 10.0);
			distanceToPickVariantDestinationSqr *= distanceToPickVariantDestinationSqr;

			// If we're close enough to 'see' final destination...
			// Pick the final destination, and use it...
			if (WoWMovement.ActiveMover.Location.DistanceSqr(RoughDestination.Location) <= distanceToPickVariantDestinationSqr)
			{
				FinalDestination = RoughDestination.Location.FanOutRandom(RoughDestination.AllowedVariance);
				return FinalDestination.Value;
			}

			// Otherwise, maintain our pursuit of rough destination...
			return RoughDestination.Location;
		}

		/// <summary>Determines if <paramref name="myPos"/> is at <paramref name="otherPos"/></summary>
		private bool AtLocation(WoWPoint myPos, WoWPoint otherPos)
		{
			// We are using cylinder distance comparison because often times we want faily high precision 
			// but need an increased tolerance in the z coord due to 'otherPos' sometimes being below terrain.
			if (myPos.Distance2DSqr(otherPos) > RoughDestination.ArrivalTolerance * RoughDestination.ArrivalTolerance)
				return false;

			var zTolerance = Math.Max(4.5f, RoughDestination.ArrivalTolerance);
			return Math.Abs(otherPos.Z - otherPos.Z) < zTolerance;
		}
		#endregion
	}
}
