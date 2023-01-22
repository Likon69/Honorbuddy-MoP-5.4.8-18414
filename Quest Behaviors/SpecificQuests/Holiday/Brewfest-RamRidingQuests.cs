// Behavior originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

#region Summary and Documentation
// QUICK DOX:
// This behavior is capable of doing the objectives for the Brewfest "Ram Riding" quest.
// These include:
//  * Bark For Drohn's Distillery // (Horde: http://www.wowhead.com/quest=11407)
//  * Bark For Tchali's Voodoo Brewery // (Horde: http://www.wowhead.com/quest=11408)
//  * Bark for the Barleybrews (Alliance: http://wowhead.com/quest=11293)
//  * Bark for the Thunderbrews (Alliance: http://wowhead.com/quest=11294)
//  * Brew for Brewfest (Alliance: http://wowhead.com/quest=29394;  Horde: http://wowhead.com/quest=29393)
//  * Now This Is Ram Racing... Almost (Alliance: http://wowhead.com/quest=11318;  Horde: http://wowhead.com/quest=11409)
//  * There and Back Again (Alliance: http://wowhead.com/quest=11122;  Horde: http://wowhead.com/quest=11412)
//
// You are expected to pick up the quest, which will automatically mount you on the Ram.
// If for some reason, you get dismounted from the Ram, this behavior will automatically go back and
// fetch another one, if it is possible to do so.  Things like this can happen during the
// Dark Iron Dwarf invasion.
//
// Quest binding:
//      QuestId [REQUIRED; Default:none]:
//          One of the four supported quest ids.  Any other quest id causes the behavior to
//          terminate immediately.
//
// BEHAVIOR EXTENSION ELEMENTS (goes between <CustomBehavior ...> and </CustomBehavior> tags)
// See the "Examples" section for typical usage.
//      RidingPath [REQUIRED; Default: none]
//          The RidingPath contains a set of Waypoints we will visit to execute the riding circuit.
//          The <RidingPath> element accepts the following attributes:
//          Each Waypoint is provided by a <Hotspot ... /> element with the following
//          attributes:
//              Name [optional; Default: X/Y/Z location of the waypoint]
//                  The name of the waypoint is presented to the user as it is visited.
//                  This can be useful for debugging purposes, and for making minor adjustments
//                  (you know which waypoint to be fiddling with).
//              X/Y/Z [REQUIRED; Default: none]
//                  The world coordinates of the waypoint.
//              ArrivalTolerance [optional; Default: 1.5]
//                  Once the toon gets within ArrivalTolerance of the waypoint, the next waypoint
//                  will be sought.
//              ArrivalVariance [optional: Default: 0.0]
//			        This value is used to:
//			        * Prevent toons running the same profile from 'stacking up' on each other once they arrive
//			        * Defeat WoWserver-side LCP detection
//			        This value represents a radius.  A fractional percentage of this radius will be added
//			        to the specified X/Y/Z in a random direction, and that new point used for the final destination.
//			        The effect is that X/Y/Z no longer defines a 'landing point', but instead, a 'landing zone'.
//			        The final destination is always selected in a sane fashion, so boundary cases like boat
//			        docks and blimp towers should not be a concern.
//                  By default, this value will move to the exact X/Y/Z specified.  It is HIGHLY recommended you
//                  allow a more 'fuzzy' destination by setting this value from 7.0 - 10.0.  This will help
//                  abate automated WoWserver-side detection, and make the toons look more 'human like' when
//                  they are waiting for boats and whatnot.
//
// THINGS TO KNOW:
//    n/a
//
#endregion


#region Examples
// EXAMPLE:
//      <CustomBehavior File="SpecificQuests\Brewfest_RamRidingQuests" QuestId="11318" >
//          <RidingPath>
//              <Hotspot Name="Apple Bucket 1" X="-5357.132" Y="-536.7656" Z="391.7714" />
//              <Hotspot Name="Apple Bucket 2" X="-5492.018" Y="-534.7864" Z="398.1151" />
//              <!-- <Hotspot Name="Apple Bucket 3" X="-5633.462" Y="-492.3576" Z="396.6727" /> -->
//              <Hotspot Name="Kegs by Flynn Firebrew" X="-5599.494" Y="-469.1557" Z="398.0992" AllowedVariance="4" />
//              <Hotspot Name="Apple Bucket 2" X="-5492.018" Y="-534.7864" Z="398.1151" />
//              <Hotspot Name="Apple Bucket 1" X="-5357.132" Y="-536.7656" Z="391.7714" />
//              <Hotspot Name="Brew drop-off location" X="-5203.017" Y="-496.6536" Z="388.3303" />
//          </RidingPath>
//      </CustomBehavior>
//
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

using Bots.DungeonBuddy.Helpers;
using Bots.Grind;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Honorbuddy.QuestBehaviorCore.XmlElements;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
#endregion


namespace Honorbuddy.Quest_Behaviors.Brewfest_RamRidingQuests
{
    [CustomBehaviorFileName(@"SpecificQuests\Holiday\Brewfest-RamRidingQuests")]
	public class Brewfest_RamRidingQuests : QuestBehaviorBase
	{
		#region Constructor and Argument Processing
		public Brewfest_RamRidingQuests(Dictionary<string, string> args)
			: base(args)
		{
			try
			{
				// NB: Core attributes are parsed by QuestBehaviorBase parent (e.g., QuestId, NonCompeteDistance, etc)

                // QuestBehaviorBase will handle QuestId, but we re-acquire here because it is 'required'...
			    GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null);

			    TerminationChecksQuestProgress = false;
			}

			catch (Exception except)
			{
				// Maintenance problems occur for a number of reasons.  The primary two are...
				// * Changes were made to the behavior, and boundary conditions weren't properly tested.
				// * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
				// In any case, we pinpoint the source of the problem area here, and hopefully it can be quickly
				// resolved.
				QBCLog.Exception(except);
				IsAttributeProblem = true;
			}
		}


		// Variables for Attributes provided by caller
        private HuntingGroundsType RidingPath { get; set; }


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
			//// EXAMPLE:
			//UsageCheck_SemanticCoherency(xElement,
			//    (!MobIds.Any() && !FactionIds.Any()),
			//    context => "You must specify one or more MobIdN, one or more FactionIdN, or both.");
			//
			//const double rangeEpsilon = 3.0;
			//UsageCheck_SemanticCoherency(xElement,
			//    ((RangeMax - RangeMin) < rangeEpsilon),
			//    context => string.Format("Range({0}) must be at least {1} greater than MinRange({2}).",
			//                  RangeMax, rangeEpsilon, RangeMin)); 
		}
		#endregion


		#region Private and Convenience variables

	    public enum RamGaitType
	    {
            None,
	        Trot,
	        Canter,
	        Gallop,
	    };

        public static readonly int[] AuraId_RamList =
            {
                42146,      // Brewfest Racing Ram (http://wowhead.com/npc=42146)
                43880,      // Ramstein's Swift Work Ram (http://wowhead.com/npc=43880)
                43883,      // Rental Racing Ram (http://wowhead.com/npc=43883)
            };

        private const int AuraId_ExhaustedRam = 43332; // http://wowhead.com/spell=43332
        // private const int AuraId_Giddyup = 42924; // http://wowhead.com/spell=42924
        // private const int AuraId_HasPortableKeg = 44066; http://wowhead.com/spell=44066
        public const int AuraId_RamTrot = 42992; // http://wowhead.com/spell=42992
        public const int AuraId_RamCanter = 42993; // http://wowhead.com/spell=42993
        public const int AuraId_RamFatigue = 43052; // http://wowhead.com/spell=43052
        public const int AuraId_RamGallop = 42994; // http://wowhead.com/spell=42994
        public const int MobId_BecanBarleybrew = 23627; // http://wowhead.com/npc=23627
        public const int MobId_CortGorestein = 24498; // http://www.wowhead.com/npc=24498
        public const int MobId_DaranThunderbrew = 23628; // http://wowhead.com/npc=23628
        public const int MobId_Jaron = 24499; // http://www.wowhead.com/npc=24499
        public const int MobId_NeillRamstein = 23558; // http://wowhead.com/npc=23558
        public const int MobId_RamMasterRay = 24497; // http://wowhead.com/npc=24497
        public static readonly WoWPoint RoughLocation_BecanBarleybrew = new WoWPoint(-5184.127, -611.7972, 397.1764);
        public static readonly WoWPoint RoughLocation_CortGorestein = new WoWPoint(1167.21, -4107.191, 19.2142);
        public static readonly WoWPoint RoughLocation_DaranThunderbrew = new WoWPoint(-5146.556, -633.5544, 397.1813);
        public static readonly WoWPoint RoughLocation_Jaron = new WoWPoint(1206.252, -4088.099, 20.75311);
        public static readonly WoWPoint RoughLocation_NeilRamstein = new WoWPoint(-5200.164, -491.5487, 388.4774);
        public static readonly WoWPoint RoughLocation_RamMasterRay = new WoWPoint(1179.486, -4216.002, 23.81173);
        public const int QuestId_BarkForDrohnsDistillery = 11407; // http://www.wowhead.com/quest=11407
        public const int QuestId_BarkForTchalisVoodooBrewery = 11408; // http://www.wowhead.com/quest=11408
        public const int QuestId_BarkForTheBarleybrews = 11293; // http://wowhead.com/quest=11293
        public const int QuestId_BarkForTheThunderbrews = 11294; // http://wowhead.com/quest=11294
        public const int QuestId_BrewForBrewfest_Alliance = 29394; // http://wowhead.com/quest=29394
        public const int QuestId_BrewForBrewfest_Horde = 29393; // http://wowhead.com/quest=29393
        public const int QuestId_NowThisIsRamRacingAlmost_Alliance = 11318; // http://wowhead.com/quest=11318
        public const int QuestId_NowThisIsRamRacingAlmost_Horde = 11409; // http://wowhead.com/quest=11409
        public const int QuestId_ThereAndBackAgain_Alliance = 11122; // http://wowhead.com/quest=11122
        public const int QuestId_ThereAndBackAgain_Horde = 11412; // http://wowhead.com/quest=11412

        private readonly List<IRamReacquireStrategy> _ramReacquireStrategies = new List<IRamReacquireStrategy>()
            {
                new RamReacquireStrategy_DropQuestAndReacquire(
                    QuestId_BarkForDrohnsDistillery, MobId_CortGorestein, RoughLocation_CortGorestein),
                new RamReacquireStrategy_DropQuestAndReacquire(
                    QuestId_BarkForTchalisVoodooBrewery, MobId_Jaron, RoughLocation_Jaron),
                new RamReacquireStrategy_DropQuestAndReacquire(
                    QuestId_BarkForTheBarleybrews, MobId_BecanBarleybrew, RoughLocation_BecanBarleybrew),
                new RamReacquireStrategy_DropQuestAndReacquire(
                    QuestId_BarkForTheThunderbrews, MobId_DaranThunderbrew, RoughLocation_DaranThunderbrew),
                new RamReacquireStrategy_GossipWithNpc(
                    QuestId_NowThisIsRamRacingAlmost_Alliance, MobId_NeillRamstein, RoughLocation_NeilRamstein, new int[] {1}),
                new RamReacquireStrategy_GossipWithNpc(
                    QuestId_NowThisIsRamRacingAlmost_Horde, MobId_RamMasterRay, RoughLocation_RamMasterRay, new int[] {1}),
                new RamReacquireStrategy_GossipWithNpc(
                    QuestId_ThereAndBackAgain_Alliance, MobId_NeillRamstein, RoughLocation_NeilRamstein, new int[] {1}),
                new RamReacquireStrategy_GossipWithNpc(
                    QuestId_ThereAndBackAgain_Horde, MobId_RamMasterRay, RoughLocation_RamMasterRay, new int[] {1}),
            };
        private IRamReacquireStrategy _ramReacquireStrategy = null;

        private readonly List<IRidingStrategy> _ridingStrategies = new List<IRidingStrategy>()
            {
                new RidingStrategy_PreventExhaustion(QuestId_BarkForDrohnsDistillery, RamGaitType.Canter),
                new RidingStrategy_PreventExhaustion(QuestId_BarkForTchalisVoodooBrewery, RamGaitType.Canter),
                new RidingStrategy_PreventExhaustion(QuestId_BarkForTheBarleybrews, RamGaitType.Canter),
                new RidingStrategy_PreventExhaustion(QuestId_BarkForTheThunderbrews, RamGaitType.Canter),
                new RidingStrategy_FixedSpeed(QuestId_BrewForBrewfest_Alliance, RamGaitType.Gallop),
                new RidingStrategy_FixedSpeed(QuestId_BrewForBrewfest_Horde, RamGaitType.Gallop),
                new RidingStrategy_NowThisIsRamRacingAlmost(QuestId_NowThisIsRamRacingAlmost_Alliance),
                new RidingStrategy_NowThisIsRamRacingAlmost(QuestId_NowThisIsRamRacingAlmost_Horde),
                new RidingStrategy_FixedSpeed(QuestId_ThereAndBackAgain_Alliance, RamGaitType.Gallop),
                new RidingStrategy_FixedSpeed(QuestId_ThereAndBackAgain_Horde, RamGaitType.Gallop),
            };
        private IRidingStrategy _ridingStrategy = null;

        // Alliance:
        // <WoWUnit Name="Apple Bucket" Entry="73919" X="-5357.132" Y="-536.7656" Z="391.7714" />
        // <WoWUnit Name="Apple Bucket" Entry="73919" X="-5492.018" Y="-534.7864" Z="398.1151" />
        // <WoWUnit Name="Apple Bucket" Entry="73919" X="-5633.462" Y="-492.3576" Z="396.6727" />
        // <WoWUnit Name="Neill Ramstein" Entry="23558" X="-5200.164" Y="-491.5487" Z="388.4774" />
        // <WoWUnit Name="Flynn Firebrew" Entry="24364" X="-5609.832" Y="-459.0559" Z="404.5505" />
        //
		#endregion


		#region Overrides of CustomForcedBehavior
		// DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return "$Id: Brewfest_RamRidingQuests.cs 1581 2014-06-27 02:34:30Z chinajade $"; } }
		public override string SubversionRevision { get { return "$Rev: 1581 $"; } }


		// CreateBehavior supplied by QuestBehaviorBase.
		// Instead, provide CreateMainBehavior definition.


		// Dispose provided by QuestBehaviorBase.


		// IsDone provided by QuestBehaviorBase.
		// Call the QuestBehaviorBase.BehaviorDone() method when you want to indicate your behavior is complete.
        public override void OnFinished()
        {
            // If we're still on the Ram, then dismount...
            // Since we're no longer steering the ram once the behavior terminates, we don't want
            // to get 'stuck' somewhere awkward in a busy area.
            var ramAura = Me.GetAllAuras().FirstOrDefault(a => AuraId_RamList.Contains(a.SpellId));

            if (ramAura != null)
                ramAura.TryCancelAura();

            // Stop moving...
            // Double-condom approach, since we are no longer running as Coroutine.
            WoWMovement.MoveStop();
            WoWMovement.ClickToMove(Me.Location);
            
            base.OnFinished();
        }


		public override void OnStart()
		{
            // RidingPath processing...
            // NB: We had to defer this processing from the constructor, because XElement isn't available
            // to parse child XML nodes until OnStart() is called.
            RidingPath = HuntingGroundsType.GetOrCreate(Element, "RidingPath");
            IsAttributeProblem |= RidingPath.IsAttributeProblem;

            if (RidingPath.Waypoints.Count <= 0)
            {
                QBCLog.Fatal("<RidingPath> sub-element is not defined, or has no waypoints.");
                IsAttributeProblem = true;
            }

            if (!IsBrewfestInProgress())
            {
                var message = "Brewfest is not in progress--cannot run behavior.";
                QBCLog.Fatal(message);
                BehaviorDone(message);
                return;
            }

            // Locate strategies for identified quest?
		    _ramReacquireStrategy = _ramReacquireStrategies.FirstOrDefault(s => s.QuestId == QuestId);
		    _ridingStrategy = _ridingStrategies.FirstOrDefault(s => s.QuestId == QuestId);
            if (_ridingStrategy == null)
            {
                var message = string.Format("QuestId {0} is not supported by this behavior", QuestId);
                QBCLog.Fatal(message);
                BehaviorDone(message);
                return;
            }

            // HACK for "Brew for Brewfest"...
            // "Brew for Brewfest" is not a 'normal' quest.  You can pick it up, but it does not go in the toon's
            // log, and there is nowhere to turn it in.  Once you have the quest, if you mess up, you cannot get it
            // again on that day.  Once you have the quest, you just "keep going" until you no longer have a ram mount.
            // To make this work, we need to select the appropriate riding strategy (above), yet set the QuestId
            // to zero, so the 'quest complete' logic works as expected (I.e., we're not doing the behavior
            // in a context of a quest).
		    if ((QuestId == QuestId_BrewForBrewfest_Alliance) | (QuestId == QuestId_BrewForBrewfest_Horde))
		        QuestId = 0;

            // Let QuestBehaviorBase do basic initialization of the behavior, deal with bad or deprecated attributes,
            // capture configuration state, install BT hooks, etc.  This will also update the goal text.
            TerminationChecksQuestProgress = false;
            var isBehaviorShouldRun = OnStart_QuestBehaviorCore();
            if (!isBehaviorShouldRun)
            {
                BehaviorDone(string.Format("Behavior for Quest({0}) appears complete.", QuestId));
                return;
            }

            // Setup the BehaviorFlags as needed --
			// These settings will be automatically restored by QuestBehaviorBase when Dispose is called
			// by Honorbuddy, or the bot is stopped.
			LevelBot.BehaviorFlags &= ~(BehaviorFlags.Combat | BehaviorFlags.Loot);

            RidingPath.WaypointVisitStrategy = HuntingGroundsType.WaypointVisitStrategyType.InOrder;
        }
		#endregion


		#region Main Behaviors
        protected override Composite CreateMainBehavior()
        {
            return new ActionRunCoroutine(ctx => MainCoroutine());
        }

        private async Task<bool> MainCoroutine()
        {
            var isQuestComplete = Me.IsQuestComplete(QuestId);

            // If quest is complete, we're done...
            if (HasQuest(QuestId) && isQuestComplete)
            {
                BehaviorDone(string.Format("Quest {0} complete.", QuestId));
                return false;
            }

            var isMountedOnRam = IsMountedOnRam();

            // If we need to fetch another Ram, give it a try...
            if (!isMountedOnRam && !isQuestComplete)
            {
                if (_ramReacquireStrategy == null)
                {
                    BehaviorDone(string.Format("Terminating: No ram re-acquire for Quest({0}) available.", QuestId));
                    return false;
                }

                if (await _ramReacquireStrategy.Execute())
                    return true;

                // Toggle visit strategy so we start at beginning of "InOrder" path, again...
                // If we lose our mount, and have to "go back" for another (via gossip or drop quest and reacquire),
                // then when we get a new mount we need to start over at the beginning of the "InOrder" path.
                // If we don't reset the path, thenwe get hung up on all kinds of scenery decorations as we try
                // to return to where we left off in the InOrder path.
                // 'Toggling' the visit strategy will cause the InOrder path to be reset to the beginning.
                // The problem is HuntingGroundsType is missing a method to make this happen, at the moment.
                RidingPath.WaypointVisitStrategy = HuntingGroundsType.WaypointVisitStrategyType.Random;
                RidingPath.WaypointVisitStrategy = HuntingGroundsType.WaypointVisitStrategyType.InOrder;
                return true;
            }

            // Execute riding strategy...
            await _ridingStrategy.Execute(RidingPath);
            return true;
        }
		#endregion


        #region Helpers
        public static bool HasQuest(int questId)
        {
            var quest = Me.QuestLog.GetQuestById((uint)questId);

            return (quest != null);
        }
        
        
        private bool IsBrewfestInProgress()
        {
            // TODO:
            return true;
        }

        
        protected static bool IsMountedOnRam()
        {
            return Me.GetAllAuras().Any(a => AuraId_RamList.Contains(a.SpellId));
        }
        #endregion


        #region Ram Re-acquire strategies...
        public abstract class IRamReacquireStrategy
        {
            protected IRamReacquireStrategy(int questId)
            {
                QuestId = questId;
            }

            // Attributes provided by caller...
            public int QuestId { get; private set; }

            public abstract Task<bool> Execute();
        }


        public class RamReacquireStrategy_GossipWithNpc : IRamReacquireStrategy
        {
            public RamReacquireStrategy_GossipWithNpc(int questId, int gossipMobId, WoWPoint gossipMobRoughLocation, int[] gossipOptions)
                : base(questId)
            {
                Contract.Requires(gossipMobId != 0, context => "gossipMobId != 0");
                Contract.Requires((gossipMobRoughLocation != WoWPoint.Empty) && (gossipMobRoughLocation != WoWPoint.Zero),
                                  context => "(gossipMobRoughLocation != WoWPoint.Empty) && (gossipMobRoughLocation != WoWPoint.Zero)");
                Contract.Requires(gossipOptions.Length > 0, context => "gossipOptions.Length > 0");

                GossipOptions = (int[])gossipOptions.Clone();  // clone needed, because we will modify below...
                GossipMobId = gossipMobId;
                GossipMobRoughLocation = gossipMobRoughLocation;

                // Rebase gossip option indices to something we can use internally...
                for (int i = 0;  i < GossipOptions.Length;  ++i)
                    GossipOptions[i] -= 1;
            }

            // Attributes provided by caller...
            public int[] GossipOptions { get; private set; }
            public int GossipMobId { get; private set; }
            public WoWPoint GossipMobRoughLocation { get; private set; }

            public override async Task<bool> Execute()
            {
                // If we cannot 'see' the mob yet, move to the rough location...
                var gossipMob = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == GossipMobId);
                if (gossipMob == null)
                {
                    await UtilityCoroutine.MoveTo(GossipMobRoughLocation, "Gossip mob");
                    return true;
                }

                // Move into interact range of mob...
                if (Me.Location.Distance(gossipMob.Location) > gossipMob.InteractRange)
                {
                    if (await UtilityCoroutine.MoveTo(gossipMob.Location, gossipMob.Name))
                        return true;
                }
                WoWMovement.MoveStop();

                // Pull up gossip frame, if not visible
                if (!IsGossipFrameVisible())
                {
                    await UtilityCoroutine.Interact(gossipMob);
                    await Coroutine.Sleep(Delay.AfterInteraction);
                    return true;
                }

                TreeRoot.StatusText = string.Format("Gossiping with {0}", gossipMob.Name);
                var gossipPageIndex = 0;
                while (gossipPageIndex < GossipOptions.Length)
                {
                    GossipEntry gossipEntry;
                    if (!TryGetGossipEntry(GossipOptions[gossipPageIndex], out gossipEntry))
                    {
                        QBCLog.Fatal(
                            "{0} is not offering gossip option {1} on page {2}."
                            + "  Did competing player alter NPC state?"
                            + "  Did you stop/start Honorbuddy?"
                            + "  Terminating behavior.",
                            gossipMob.Name,
                            GossipOptions[gossipPageIndex] + 1,
                            gossipPageIndex + 1);
                        Utility.CloseAllNpcFrames();
                        Me.ClearTarget();
                        return false;
                    }

                    // Log the gossip option we're about to take...
                    QBCLog.DeveloperInfo(
                        "Selecting Gossip Option({0}) on page {1}: \"{2}\"",
                        gossipEntry.Index + 1,
                        gossipPageIndex + 1,
                        gossipEntry.Text);

                    GossipFrame.Instance.SelectGossipOption(GossipOptions[gossipPageIndex]);
                    ++gossipPageIndex;
                    await Coroutine.Wait((int)Delay.AfterInteraction.TotalMilliseconds, () => !IsGossipFrameVisible());
                }

                // Gossip is complete, claim credit...
                Utility.CloseAllNpcFrames();
                var message = string.Format("Gossip with {0} complete.", gossipMob.Name);
                QBCLog.DeveloperInfo(message);
                TreeRoot.StatusText = message;
                return true;
            }

            #region Helpers
            private bool IsGossipFrameVisible()
            {
                return GossipFrame.Instance.IsVisible;
            }

            private bool TryGetGossipEntry(int gossipOptionWanted, out GossipEntry gossipEntry)
            {
                try
                {
                    // NB: This clumsiness is because HB defines the default GossipEntry with an
                    // an Index of 0.  Since this is a valid gossip option index, this leaves us with
                    // no way to determine the difference between the 'default' value, and a valid
                    // value. So, we try to get the gossip entry using First() (vs. FirstOrDefault()),
                    // and if an exception gets thrown, we know the entry is not present.
                    if (GossipFrame.Instance.GossipOptionEntries == null)
                    {
                        throw new InvalidOperationException();
                    }

                    gossipEntry = GossipFrame.Instance.GossipOptionEntries
                        .First(o => o.Index == gossipOptionWanted);
                }
                catch (InvalidOperationException)
                {
                    gossipEntry = new GossipEntry();
                    return false;
                }
                return true;
            }
            #endregion
        }


        public class RamReacquireStrategy_DropQuestAndReacquire : IRamReacquireStrategy
        {
            public RamReacquireStrategy_DropQuestAndReacquire(int questId, int questGiverId, WoWPoint questGiverRoughLocation)
                : base(questId)
            {
                Contract.Requires(questGiverId != 0, context => "questGiverId != 0");
                Contract.Requires((questGiverRoughLocation != WoWPoint.Empty) && (questGiverRoughLocation != WoWPoint.Zero),
                                  context => "(questGiverRoughLocation != WoWPoint.Empty) && (questGiverRoughLocation != WoWPoint.Zero)");

                QuestGiverId = questGiverId;
                QuestGiverRoughLocation = questGiverRoughLocation;
            }

            public int QuestGiverId { get; private set; }
            public WoWPoint QuestGiverRoughLocation { get; private set; }

            public override async Task<bool> Execute()
            {
                // If we have the quest, drop it...
                if (HasQuest(QuestId))
                {
                    Me.QuestLog.AbandonQuestById((uint)QuestId);
                    await Coroutine.Wait(Delay.LagDuration, () => !HasQuest(QuestId));
                    return true;
                }

                // If we cannot 'see' the mob yet, move to the rough location...
                var questGiver = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == QuestGiverId);
                if (questGiver == null)
                {
                    await UtilityCoroutine.MoveTo(QuestGiverRoughLocation, "Quest Giver");
                    return true;
                }

                if (await UtilityCoroutine.MoveTo(questGiver.Location, questGiver.Name))
                    return true;

                await ScriptHelpers.PickupQuest(questGiver, (uint) QuestId);
                await Coroutine.Wait(Delay.LagDuration, () => HasQuest(QuestId));
                return false;
            }
        }
        #endregion



        #region Riding strategies...
        public abstract class IRidingStrategy
        {
            protected IRidingStrategy(int questId)
            {
                QuestId = questId;
            }

            // Attributes provided by caller...
            public int QuestId { get; private set; }


            // Methods concrete classes must implement...
            public abstract Task<bool> Execute(HuntingGroundsType ridingPath);


            #region Private and Convenience variables...
            private TimeSpan Time_GaitMaintenance
            {
                get
                {
                    // Variable timing to simulate 'human'...
                    return (_random.Next(1, 100) < 65)
                            ? TimeSpan.FromMilliseconds(_random.Next(1700, 2100))
                            : TimeSpan.FromMilliseconds(_random.Next(1900, 2300));
                }
            }

            private TimeSpan Time_GiddyupThrottle
            {
                get
                {
                    // Variable timing to simulate 'human'...
                    return (_random.Next(1, 100) < 65)
                            ? TimeSpan.FromMilliseconds(_random.Next(300, 550))
                            : TimeSpan.FromMilliseconds(_random.Next(450, 750));
                }
            }

            private readonly Stopwatch _timer_GaitMaintenance = new Stopwatch();
            private readonly Stopwatch _timer_GiddyupThrottle = new Stopwatch();
            #endregion


            #region Helpers
            protected bool AdjustRamSpeed(RamGaitType gaitWanted)
            {
                if (!IsMountedOnRam())
                    return false;

                // Ram exhausted?
                var isRamExhausted = Me.HasAura(AuraId_ExhaustedRam);
                if (isRamExhausted)
                {
                    _timer_GaitMaintenance.Reset();
                    _timer_GiddyupThrottle.Reset();
                    return false;
                }

                var isCantering = Me.HasAura(AuraId_RamCanter);
                var isGalloping = Me.HasAura(AuraId_RamGallop);
                var isTrotting = Me.HasAura(AuraId_RamTrot);

                // Need to slow down?
                var isGaitTooFast = (((gaitWanted == RamGaitType.None) && (isTrotting || isCantering || isGalloping))
                                     || ((gaitWanted == RamGaitType.Trot) && (isCantering || isGalloping))
                                     || ((gaitWanted == RamGaitType.Canter) && isGalloping));
                if (isGaitTooFast)
                {
                    _timer_GaitMaintenance.Restart();
                    _timer_GiddyupThrottle.Restart();
                    return true; // "True", because we are waiting for gait to slow
                }

                // Need to pick up the pace?
                var isGaitTooSlow = (((gaitWanted == RamGaitType.Trot) && !(isTrotting || isCantering || isGalloping))
                                     || ((gaitWanted == RamGaitType.Canter) && !(isCantering || isGalloping))
                                     || ((gaitWanted == RamGaitType.Gallop) && !isGalloping));
                if (isGaitTooSlow)
                {
                    var isGiddyupThrottleFinished = (!_timer_GiddyupThrottle.IsRunning
                                                     || (_timer_GiddyupThrottle.Elapsed > Time_GiddyupThrottle));
                    if (isGiddyupThrottleFinished)
                    {
                        // N.B.: To find the correct button, type "/framestack" into WoWclient and hover over the button...
                        Lua.DoString("ExtraActionButton1:Click()");
                        _timer_GiddyupThrottle.Restart();
                    }

                    _timer_GaitMaintenance.Restart();
                    return true;
                }

                // Maintain what we've got...
                var isGaitMaintenanceTimerFinished = (!_timer_GaitMaintenance.IsRunning
                                                      || (_timer_GaitMaintenance.Elapsed > Time_GaitMaintenance));
                if (isGaitMaintenanceTimerFinished)
                {
                    // N.B.: To find the correct button, type "/framestack" into WoWclient and hover over the button...
                    Lua.DoString("ExtraActionButton1:Click()");
                    _timer_GaitMaintenance.Restart();
                    return true;
                }

                return false;
            }


            protected bool MoveToNextGoal(HuntingGroundsType ridingPath)
            {
                if (!IsMountedOnRam())
                    return false;

                var activeMover = WoWMovement.ActiveMover;
                var currentWaypoint = ridingPath.CurrentWaypoint();

                var moveResult = Navigator.MoveTo(currentWaypoint.Location);
                if (Navigator.GetRunStatusFromMoveResult(moveResult) == RunStatus.Success)
                    return true;

                QBCLog.DeveloperInfo(
                    "Navigator unable to move from {0} to destination({1}, {2}) on ground.",
                    activeMover.Location,
                    currentWaypoint.Name,
                    currentWaypoint.Location.ToString());
                return false;
            }
            #endregion
        }


        public class RidingStrategy_FixedSpeed : IRidingStrategy
        {
            public RidingStrategy_FixedSpeed(int questId, RamGaitType speedWanted)
                : base(questId)
            {
                _speedWanted = speedWanted;
            }

            private readonly RamGaitType _speedWanted;

            public override async Task<bool> Execute(HuntingGroundsType ridingPath)
            {
                AdjustRamSpeed(_speedWanted);
                MoveToNextGoal(ridingPath);
                return true;              
            }
        }


        public class RidingStrategy_PreventExhaustion : IRidingStrategy
        {
            public RidingStrategy_PreventExhaustion(int questId, RamGaitType maxSpeedWanted)
                : base(questId)
            {
                _maxSpeedWanted = maxSpeedWanted;
                _inFatigueRecovery = false;
            }

            private bool _inFatigueRecovery;
            private readonly RamGaitType _maxSpeedWanted;

            // Variable stack counts to simulate 'humans'...
            private readonly int _stackCountFatigued = _random.Next(70, 77);
            private readonly int _stackCountFatigueRecovered = _random.Next(50, 60);

            public override async Task<bool> Execute(HuntingGroundsType ridingPath)
            {
                // Calculate fatigue, and apply hysteresis recovery...
                var fatigueAura = Me.GetAuraById(AuraId_RamFatigue);

                // Ram fatigued?
                if ((fatigueAura != null) && (fatigueAura.StackCount > _stackCountFatigued))
                    _inFatigueRecovery = true;

                // Ram fatigue recovered?
                if ((fatigueAura == null) || (fatigueAura.StackCount < _stackCountFatigueRecovered))
                    _inFatigueRecovery = false;

                // Stay in "Trot" until fatigue recovered...
                AdjustRamSpeed(_inFatigueRecovery ? RamGaitType.Trot : _maxSpeedWanted);
                MoveToNextGoal(ridingPath);
                return true;                              
            }
        }

        public class RidingStrategy_NowThisIsRamRacingAlmost : IRidingStrategy
        {
            public RidingStrategy_NowThisIsRamRacingAlmost(int questId)
                : base(questId)
            {
                Contract.Requires(((questId == QuestId_NowThisIsRamRacingAlmost_Alliance)
                                   || (questId == QuestId_NowThisIsRamRacingAlmost_Horde)),
                                  context => string.Format("questId is not {0} or {1}",
                                                           QuestId_NowThisIsRamRacingAlmost_Alliance,
                                                           QuestId_NowThisIsRamRacingAlmost_Horde));
            }

            public override async Task<bool> Execute(HuntingGroundsType ridingPath)
            {
                // Trot gait (for 8 seconds)...
                if (!Me.IsQuestObjectiveComplete(QuestId, 1))
                {
                    AdjustRamSpeed(RamGaitType.Trot);
                    MoveToNextGoal(ridingPath);
                    return true;
                }

                // Canter gait (for 8 seconds)...
                if (!Me.IsQuestObjectiveComplete(QuestId, 2))
                {
                    AdjustRamSpeed(RamGaitType.Canter);
                    MoveToNextGoal(ridingPath);
                    return true;
                }

                // Gallop gate (for 8 seconds)...
                if (!Me.IsQuestObjectiveComplete(QuestId, 3))
                {
                    AdjustRamSpeed(RamGaitType.Gallop);
                    MoveToNextGoal(ridingPath);
                    return true;
                }

                return false;
            }            
        }
        #endregion
    }
}