// Behavior originally contributed by Nesox / completely reworked by Chinajade and HighVoltz
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
// INTERACTWITH interacts with mobs or objects in various fashions, including:
//  * Gossiping with the mob through a set of dialogs
//  * "Right-clicking" on the mob to complete a goal
//  * Buying particular items off of vendors
//  * Using an item on a mob or object.  The item can be one-click use, or two-click
//      use (two-click: clicks once to get a 'placement cursor', and clicks the
//      second time to drop the placement cursor on the mob or object).
//  * Looting or harvesting (through interaction) an item off a mob or object
// The behavior initiates interaction by "right clicking" on the mob of interest.
// The subsequent actions taken by the behavior depend on the attributes provided.
//
// BEHAVIOR ATTRIBUTES:
// *** ALSO see the documentation in QuestBehaviorBase.cs.  All of the attributes it provides
// *** are available here, also.  The documentation on the attributes QuestBehaviorBase provides
// *** is _not_ repeated here, to prevent documentation inconsistencies.
//
// Basic Attributes:
//      FactionIdN [at least one is REQUIRED: FactionIdN, MobIdN, MobIdIncludesSelf]
//          Identifies the faction of the mobs on which the interaction should take place.
//          If you specify both MobIdN and FactionIdN, the two sets will be combined,
//          and any target that fulfillseither MobIdN or FactionIdN will be selected for interaction.
//      MobIdN [at least one is REQUIRED: FactionIdN, MobIdN, MobIdIncludesSelf]
//          Identifies the mobs on which the interaction should take place.
//          The MobIdN can represent either an NPC (WoWUnit) or an Object (WoWObject).
//          The two types can freely be mixed.
//          If you specify both MobIdN and FactionIdN, the two sets will be combined,
//          and any target that fulfillseither MobIdN or FactionIdN will be selected for interaction.
//      MobIdIncludesSelf [Default: false; at least one is REQUIRED: FactionIdN, MobIdN, MobIdIncludesSelf]
//          Indentifies 'self' as one of the mobs on which the interaction should take place.
//          If you specify other qualifiers, such as required auras, missing auras, mob state,
//          not moving, etc, 'self' must meet the qualifers just like any other mob.
//      NumOfTimes [optional; Default: 1]
//          This is the number of times the behavior should interact with MobIdN.
//          Once this value is achieved, the behavior considers itself done.
//          If the Quest or QuestObjectiveIndex completes prior to reaching this
//          count, the behavior also terminates.
//
// Optional Target Qualifiers (applies to all targets, including 'self'):
// These attributes further qualify a target that fullfills the MobIdN/FactionIdN/Self selection.
//      AuraIdOnMobN [optional; Default: none]
//          The target *must* possess an aura that matches one of the defined 
//          AuraIdMissingFromMobN, in order to be considered a target for interaction.
//      AuraIdMissingFromMob [optional; Default: none]
//          The target must *not* possess an aura that matches one of the defined 
//          AuraIdMissingFromMobN, in order to be considered a target for interaction.
//      MobHpPercentLeft [optional; Default: 100.0]
//          The target's health must be at or below this value to be considered a qualfiied target.
//          This value is only considered if MobState is "BelowHp".
//      MobState [optional; Default: DontCare]
//          [Allowed values for NPC targets: Alive, AliveNotInCombat, BelowHp, Dead, DontCare]
//          This attribute qualifies the state the MobIdN or FactionIdN must be in,
//          when selecting targets for interaction.
//      NotMoving [optional; Default: false]
//          If true, the behavior will only consider targets that are not moving.
//
// Interaction by Buying Items:
//      BuyItemCount [optional; Default: 1]
//          This is the number of items (specified by BuyItemId) that should be
//          purchased from the Vendor (specified by MobId).
//      InteractByBuyingItemId [optional; Default: none]
//          This is the ItemId of the item that should be purchased from the
//          Vendor (specified by MobId).
//
// Interact by Casting Spell:
//      InteractByCastingSpellId [optional; Default: none]
//          Specifies an SpellId to use on the specified target.
//          The spell may be a normal 'one-click-to-use' spell, or it may be
//          a (two-click) spell that needs to be placed on the ground at
//          the target's location.
//
// Interaction by Gossiping:
//      InteractByGossipOptions [optional; Default: none]
//          Defines a comma-separated list of (1-based) numbers that specifies
//          which Gossip option to select in each dialog frame when chatting with an NPC.
//          This value should be separated with commas. ie. InteractByGossipOptions="1,1,4,2".
//
// Interaction by Looting:
//      InteractByLooting [optional; Default: false]
//          If true, the behavior will pick up loot from any loot frame
//          offered by the MobIdN.
//          This feature is largely unused since the WoW game mechanics
//          have changed.
//
// Interaction by Quest frames:
//      InteractByQuestFrameDisposition [optional; Default: TerminateProfile]
//          [Allowed values: Accept/Complete/Continue/Ignore/TerminateBehavior/TerminateProfile]
//          This attribute determines the behavior's response should the NPC
//          with which we've interacted offer us a quest frame.
//          Accept/Complete/Continue
//              clicks on the appropriate button in the offered quest frame.
//              The behavior continues after the appropriate button is clicked.
//          Ignore
//              closes the offered quest frame, and continues with the behavior.
//          TerminateBehavior
//              closes the offered quest frame, and terminates the behavior.
//              This is useful in situations where the Quest frame was unexpected,
//              but tells us what 'state' an NPC may be in.  See the "By Any Means Necessary"
//              in the Examples section below.
//
// Interact by Using Item:
//      InteractByUsingItemId [optional; Default: none]
//          Specifies an ItemId to use on the specified MobInN.
//          The item may be a normal 'one-click-to-use' item, or it may be
//          a (two-click) item that needs to be placed on the ground at
//          the MobIdN's location.
//
// Tunables:
//      AttemptCountMax [optional; Default: 7]
//          Specifies the maximum number of attempts at interacting with a target before moving on.
//      CollectionDistance [optional; Default: 100.0]
//          Measured from the toon's current location, this value specifies
//          the maximum distance that should be searched when looking for
//          a viable MobId with which to interact.
//      IgnoreCombat [optional; Default: false]
//          If true, this behavior will not defend itself if attacked, and
//          will carry on with its main task.
//      IgnoreLoSToTarget [optional; Default: false]
//          If true, the behavior will not consider Line of Sight when trying to interact
//          with the selected target.
//      InteractBlacklistTimeInSeconds [optional: Default: 180]
//          Specifies the number of seconds for which a target with which we've interacted
//          is to remain blacklisted.
//      KeepTargetSelected [optional; Default: false]
//          If true, the behavior will not clear the toon's target after the interaction
//          is complete.  Instead, the target will remain on the last interacted
//          mob until a new mob is ready for interaction.
//          If false, the behavior clears the toon's target immediately after
//          it considers the interaction complete.
//      MinRange [optional; Default: 0.0]
//          Defines the minimum range at which the interaction with MobIdN should take place.
//          If the toon is too close to the mob, the toon will move to acquire this minimum
//          distance to the mob.
//      PreInteractMountStrategy [optional; Default: None]
//          [allowed values: CancelShapeshift, Dismount, DismountOrCancelShapeshift, Mount, None]
//          Provides the opportunity to alter the mounting state of a toon immediately prior
//          to interacting, or using an object.  The options are defined as follows:
//              CancelShapeshift
//                  Cancels *any* shapeshifted form, whether it represents a 'mounted form'
//                  or not.  Examples of non-mounted forms include a Druid's Bear or Cat Form.
//              Dismount
//                  Unmounts from an explicit mount, or cancels a 'mounted form'.  Examples
//                  of mounted forms are Druid Flight Forms, Druid Travel Form, Shaman Ghost Wolf,
//                  and Worgen Running Wild.
//              DismountOrCancelShapeshift
//                  Dismounts or cancel's a shapeshifted form--whichever is appropriate.
//              Mount
//                  Mounts the mount defined in the user's preference settings.
//              None
//                  Does not alter the existing mounting state prior to interaction.
//      ProactiveCombatStrategy [optional; Default: ClearAll]
//          [allowed values: NoClear/ClearMobsTargetingUs/ClearMobsThatWillAggro/ClearAll]
//              NoClear
//                  Will not proactively clear around the selected target.  This means that
//                  an attempt to interact with the target could be interrupted by a mob.
//              ClearMobsTargetingUs
//                  If a mob targets us we immediately move to engage it, rather than wait
//                  for the mob to actually close the range to us, put us in combat, then
//                  we deal with it.  This option prevents dragging a large number of mobs
//                  to our selected interact target only to have to fight them.
//              ClearMobsThatWillAggro
//                  If mobs within 'aggro range' of our selected interact target are cleared
//                  before we attempt to interact with the selected target.  This prevents us
//                  from being interrupted when we attempt to interact with our selected target.
//              ClearAll
//                  Combines ClearMobsThatWillAggro and ClearMobsTargetingUs.
//      Range [optional; Default: interaction range defined by the object with which we're trying to interact]
//          Defines the maximum range at which the interaction with MobIdN should take place.
//          If the toon is out of range, the toon will be moved within this distance
//          of the mob.
//          It is highly recommended you let this attribute default, unless there is
//          a compelling reason not to.
//      WaitForNpcs [optional; Default: true]
//          This value affects what happens if there are no MobIds in the immediate area.
//          If true, the behavior will move to the next hunting ground waypoint, or if there
//          is only one waypoint, the behavior will stand and wait for MobIdN to respawn.
//          If false, and the behavior cannot locate MobIdN in the immediate area, the behavior
//          considers itself complete.
//          Please see "Things to know", below.
//      WaitTime [optional; Default: 0ms]
//          Defines the number of milliseconds to wait after the interaction is successfully
//          conducted before carrying on with the behavior on other mobs.
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
//              ArrivalTolerance [optional; Default: 1.5]
//                  Once the toon gets within ArrivalTolerance of the waypoint, the next waypoint
//                  will be sought.
//
// THiNGS TO KNOW:
// * As a design choice, this behavior waits a variant amount of time when 'clicking on things'.
//      We try to simulate an attentive human as much as possible.
//
// * The InteractByGossipOptions and InteractByBuyingItemId attributes can be combined to get to
//      Innkeeper goods.  Innkeepers require you to select one of their gossip options before they
//      will show you their wares for sale.
//
// * If the InteractByGossipOptions selects a "bind to this location" option, then InteractWith
//      will automatically confirm the request to bind at the location.  After the binding is complete
//      InteractWith displays a confirmation message of the new bind location.
//
// * If you specify MobState="Dead", but only alive mobs exist in the area, InteractWith
//      will kill the alive mobs, and then try to interact with them once they are dead.
//
// * The behavior pro-actively clears mobs within aggro range of the selected interact target.
//      This prevents the mobs from interfering with the interaction.  If IgnoreCombat="true",
//      then this pro-active clearing is also turned off.
//
// * If you are trying to gossip with NPCs that are in combat, this behavior will help the NPC
//      kill the mob so the NPC will leave combat.  This is necessary because many NPCs will not
//      gossip when they are in combat.
//
// * Be careful when specifying the WaitForNpcs="false".
//      The 'interact blacklist' is internal to the behavior.  The means the blacklist is destroyed
//      any time the behavior exits, and a fresh one is created upon re-entry to the behavior.
//      This means InteractWith does not know which mobs have already been interacted and which
//      have not, when WaitForNpcs="true".  The can cause slow progress as the failure detection
//      mechnaisms built into the behavior kick in and re-exclude non-viable mobs as they are
//      rediscovered.
//
// * Deprecated attributes:
//      + BuySlot
//          This attribute is deprecated, and BuySlot presents a number of problems.
//          If a vendor presents 'seasonal' or limited-quantity wares, the slot number
//          for the desired item can change.
//          OLD DOX: Buys the item from the slot. Slots are:    0 1
//                                                              2 3
//                                                              4 5
//                                                              6 7
//                                                              page2
//                                                              8 9 etc.
//      + ObjectType
//          This option should no longer be specified--the behavior just 'figures it out'
//      + Nav--prefer MovementBy, instead.
//          For support reasons, here is the old documentation:
//              Nav [optional; Default: not any]
//              [Allowed values: CTM, Mesh, None]
//              This attribute is no longer used--use MovementBy, instead.
//              (See documentation in QuestBehaviorBase for details).
//

#endregion

#region Examples

// BUYING AN ITEM:
// From a simple vendor that immediately presents you with a frame of their wares:
//      <CustomBehavior File="InteractWith" MobId="44236" InteractByBuyingItemId="2723"
//			X="-8365.76" Y="594.658" Z="97.00068" />
//
// From an Innkeeper that requires you to gossip before showing you their wares.
// Gossip entry 2 is the "Let me browse your goods." option from Thaegra Tillstone.  (The InteractBy* options
// are not order dependent.)
//      <CustomBehavior File="InteractWith" MobId="44235" InteractByGossipOptions="2" InteractByBuyingItemId="4540"
//			X="-8365.76" Y="594.658" Z="97.00068" />
//
//
// BINDING AT AN INN:
// Gossip entry 1 is the "Make this inn your home." option in Thaegra Tillstone's Inn in the Stormwind
// Dwarven District:
//      <If Condition="Me.HearthstoneAreaId != 5150">
//          <CustomBehavior File="InteractWith" MobId="44235" InteractByGossipOptions="1" />
//		        X="-8365.76" Y="594.658" Z="97.00068" />
//      </If>
// The only way to obtain the "area id" for the Condition is to actually set your hearthstone,
// then use the following command with HBConsole (or Developer Tools):
//      Logging.Write("AreaID: {0}", StyxWoW.Me.HearthstoneAreaId);
//
//
// QUEST EXAMPLES:  
// "By Any Means Necessary" (http://wowhead.com/quest=9978)
// This is a difficult quest for a number of reasons. First, a bug in the WoWclient indicates the quest
// is always completed, whether it is or not.  Thus we can not use an "IsCompleted" test to make decisions.
// Because of this bug, we cannot associate a QuestId attribute with our invocation of InteractWith.
// Second, Empoor may be in one of two states.  In one state, he'll offer us a gossip frame, and we must gossip
// and finish the ensuing fight before we can turn in the quest.  In the other state, he allows us to turn in
// the quest immediately (this happens when someone else has just fought him, and he stays in the second state
// for a while).
// To solve the problem, we arrange for InteractWith to gossip with the NPC.  If he offers the Quest frame,
// we terminate the 'gossip' behavior (via providing the InteractByQuestFrameDisposition="TerminateBehavior"
// attribute.
//      <!-- A couple of states for Empoor:
//      	1. We may need to gossip, do the fight, then turn in quest
//  	   	2. Someone else may have started and finished the fight already, and we can
//      		turn in quest immediately.
//      	NB: The WoWclient makes this quest appears complete, even though its not.
//      	So, we can't use the "IsQuestCompleted()" qualifier as a valid check.
//      	-->
//      <CustomBehavior File="InteractWith" MobId="18482" GossipOptions="1"
//      	InteractByQuestFrameDisposition="TerminateBehavior" >
//      	<HuntingGrounds>
//      		<Hotspot Name="Tuurem" X="-2037.871" Y="4377.199" Z="1.805441" />
//      		<Hotspot Name="Shattrath upper ring" X="-1955.325" Y="5029.867" Z="31.30444" />
//      		<Hotspot Name="Shattrath tunnel entrance" X="-1548.137" Y="5079.232" Z="-17.91318" />
//      	</HuntingGrounds>
//      </CustomBehavior>
//      <TurnIn QuestName="By Any Means Necessary" QuestId="9978" TurnInName="Empoor" TurnInId="18482" />
//
// "Fear No Evil" (http://wowhead.com/quest=28809)
// Revive four injured soldiers (by interacting with them) using Paxton's Prayer Book (http://wowhead.com/item=65733).
//      <CustomBehavior File="InteractWith" QuestId="28809" MobId="50047" NumOfTimes="4"
//          CollectionDistance="1" >
//          <HuntingGrounds WaypointVisitStrategy="Random" >
//              <Hotspot Name="Eastern Tent and Campfire" X="-8789.213" Y="-253.3615" Z="82.46034" />
//              <Hotspot Name="North Campfire" X="-8757.012" Y="-188.6659" Z="85.05094" />
//              <Hotspot Name="Mine entrance" X="-8716.521" Y="-105.2505" Z="87.57959" />
//              <Hotspot Name="NW LeanTo and Campfire" X="-8770.273" Y="-111.1501" Z="84.09385" />
//          </HuntingGrounds>
//      </CustomBehavior>
//

#endregion

#region Usings

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Bots.DungeonBuddy.Helpers;
using JetBrains.Annotations;

using Bots.Grind;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Honorbuddy.QuestBehaviorCore.XmlElements;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion

namespace Honorbuddy.Quest_Behaviors.InteractWith
{
	[CustomBehaviorFileName(@"InteractWith")]
	public class InteractWith : QuestBehaviorBase
	{
		#region Constructor and argument processing

		public InteractWith(Dictionary<string, string> args)
			: base(args)
		{
			try
			{
				// NB: Core attributes are parsed by QuestBehaviorBase parent (e.g., QuestId, NonCompeteDistance, etc)

				// Primary attributes...
				MobIds = GetAttributeAsArray<int>("MobIds", false, ConstrainAs.MobId, new[] { "NpcIds" }, null);

				if (MobIds != null && MobIds.Count() == 0)
					MobIds = GetNumberedAttributesAsArray<int>("MobId", 0, ConstrainAs.MobId, new[] { "NpcId" });
				MobIdIncludesSelf = GetAttributeAsNullable<bool>("MobIdIncludesSelf", false, null, null) ?? false;
				FactionIds = GetNumberedAttributesAsArray<int>("FactionId", 0, ConstrainAs.MobId, null);
				NumOfTimes = GetAttributeAsNullable<int>("NumOfTimes", false, ConstrainAs.RepeatCount, null) ?? 1;

				// Additional target qualifiers...
				AuraIdsOnMob = GetNumberedAttributesAsArray<int>("AuraIdOnMob", 0, ConstrainAs.AuraId, null);
				AuraIdsMissingFromMob = GetNumberedAttributesAsArray<int>("AuraIdMissingFromMob", 0, ConstrainAs.AuraId, null);
				MobState = GetAttributeAsNullable<MobStateType>("MobState", false, null, new[] { "NpcState" }) ?? MobStateType.DontCare;
				NotMoving = GetAttributeAsNullable<bool>("NotMoving", false, null, null) ?? false;

				// InteractionBy attributes...
				InteractByBuyingItemId = GetAttributeAsNullable("InteractByBuyingItemId", false, ConstrainAs.ItemId, null)
										?? GetAttributeAsNullable<int>("BuyItemId", false, ConstrainAs.ItemId, null) /*Legacy name--don't use */
										?? 0;
				InteractByCastingSpellId = GetAttributeAsNullable("InteractByCastingSpellId", false, ConstrainAs.SpellId, null) ?? 0;
				InteractByGossipOptions = GetAttributeAsArray<int>(
					"InteractByGossipOptions",
					false,
					new ConstrainTo.Domain<int>(-1, 10),
					null,
					null);
				if (InteractByGossipOptions.Length <= 0)
				{
					InteractByGossipOptions = GetAttributeAsArray<int>(
						"GossipOptions",
						false,
						new ConstrainTo.Domain<int>(-1, 10),
						new[] { "GossipOption" },
						null);
				} /*Legacy name--don't use */
				InteractByLooting = GetAttributeAsNullable<bool>("InteractByLooting", false, null, null)
									?? GetAttributeAsNullable<bool>("Loot", false, null, null) /* Legacy name--don't use*/
									?? false;
				InteractByQuestFrameAction = GetAttributeAsNullable<QuestFrameDisposition>(
					"InteractByQuestFrameDisposition",
					false,
					null,
					null)
											?? QuestFrameDisposition.TerminateProfile;
				InteractByUsingItemId = GetAttributeAsNullable<int>("InteractByUsingItemId", false, ConstrainAs.ItemId, null) ?? 0;

				// Tunables...
                AttemptCountMax = GetAttributeAsNullable<int>("AttemptCountMax", false, new ConstrainTo.Domain<int>(1,100), null) ?? 7;
				BuyItemCount = GetAttributeAsNullable<int>("BuyItemCount", false, ConstrainAs.CollectionCount, null) ?? 1;
				CollectionDistance = GetAttributeAsNullable<double>("CollectionDistance", false, ConstrainAs.Range, null) ?? 100;
				HuntingGroundCenter = GetAttributeAsNullable<WoWPoint>("", false, ConstrainAs.WoWPointNonEmpty, null) ?? Me.Location;
				IgnoreCombat = GetAttributeAsNullable<bool>("IgnoreCombat", false, null, null) ?? false;
				IgnoreLoSToTarget = GetAttributeAsNullable<bool>("IgnoreLoSToTarget", false, null, null) ?? false;
				InteractBlacklistTimeInSeconds =
					GetAttributeAsNullable<int>("InteractBlacklistTimeInSeconds", false, ConstrainAs.CollectionCount, null) ?? 180;
				KeepTargetSelected = GetAttributeAsNullable<bool>("KeepTargetSelected", false, null, null) ?? false;
				MobHpPercentLeft =
					GetAttributeAsNullable<double>("MobHpPercentLeft", false, ConstrainAs.Percent, new[] { "HpLeftAmount" }) ?? 100.0;
				PreInteractMountStrategy = GetAttributeAsNullable<MountStrategyType>("PreInteractMountStrategy", false, null, null)
											?? MountStrategyType.None;
				ProactiveCombatStrategy = GetAttributeAsNullable<ProactiveCombatStrategyType>(
					"ProactiveCombatStrategy",
					false,
					null,
					null)
										?? ProactiveCombatStrategyType.ClearAll;
				RangeMax = GetAttributeAsNullable<double>("Range", false, ConstrainAs.Range, null);
				RangeMin = GetAttributeAsNullable<double>("MinRange", false, ConstrainAs.Range, null);
				WaitForNpcs = GetAttributeAsNullable<bool>("WaitForNpcs", false, null, null) ?? true;
				WaitTime = GetAttributeAsNullable<int>("WaitTime", false, ConstrainAs.Milliseconds, null) ?? 0;

				// Deprecated attributes...
				InteractByBuyingItemInSlotNum =
					GetAttributeAsNullable<int>(
						"InteractByBuyingItemInSlotNum",
						false,
						new ConstrainTo.Domain<int>(-1, 100),
						new[] { "BuySlot" }) ?? -1;
				GetAttributeAsNullable<Deprecated_MobType>("ObjectType", false, null, new[] { "MobType" }); // Deprecated--no longer used
				var navigationMode = GetAttributeAsNullable<NavigationModeType>("Nav", false, null, new[] { "Navigation" });
				if (navigationMode != null)
				{
					MovementBy =
						(navigationMode == NavigationModeType.CTM)
							? MovementByType.ClickToMoveOnly
							: (navigationMode == NavigationModeType.Mesh)
								? MovementByType.NavigatorPreferred
								: MovementByType.None;
				}

				// Pre-processing into a form we can use directly...
				for (int i = 0; i < InteractByGossipOptions.Length; ++i)
				{
					InteractByGossipOptions[i] -= 1;
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


		// Attributes provided by caller
		private int AttemptCountMax  { get; set; }
		private int[] AuraIdsOnMob { get; set; }
		private int[] AuraIdsMissingFromMob { get; set; }
		private int BuyItemCount { get; set; }
		private double CollectionDistance { get; set; }
		private int[] FactionIds { get; set; }
		private WoWPoint HuntingGroundCenter { get; set; }
		private bool IgnoreCombat { get; set; }
		private bool IgnoreLoSToTarget { get; set; }
		private int InteractBlacklistTimeInSeconds { get; set; }
		private int InteractByBuyingItemId { get; set; }
		private int InteractByBuyingItemInSlotNum { get; set; }
		private int InteractByCastingSpellId { get; set; }
		private int[] InteractByGossipOptions { get; set; }
		private int InteractByUsingItemId { get; set; }
		private bool InteractByLooting { get; set; }
		private bool KeepTargetSelected { get; set; }
		private double MobHpPercentLeft { get; set; }
		private int[] MobIds { get; set; }
		private bool MobIdIncludesSelf { get; set; }
		private MobStateType MobState { get; set; }
		private bool NotMoving { get; set; }
		private int NumOfTimes { get; set; }
		private QuestFrameDisposition InteractByQuestFrameAction { get; set; }
		private MountStrategyType PreInteractMountStrategy { get; set; }
		private ProactiveCombatStrategyType ProactiveCombatStrategy { get; set; }
		private double? RangeMax { get; set; }
		private double? RangeMin { get; set; }
		private bool WaitForNpcs { get; set; }
		private int WaitTime { get; set; }

		protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
		{
			UsageCheck_DeprecatedAttribute(
				xElement,
				(InteractByBuyingItemInSlotNum != -1),
				"InteractByBuyingItemInSlotNum/BuySlot",
				context => "The InteractByBuyingItemInSlotNum/BuySlot attributes have been deprecated.\n"
							+ "Please replace them with InteractByBuyingItemId attribute."
							+ "Your InteractByBuyingItemInSlotNum/BuySlot attribute will still be honored, but it may yield unexpected surprises,"
							+ " if the vendor is offering seasonal or other such items.");

			UsageCheck_DeprecatedAttribute(
				xElement,
				Args.Keys.Contains("Nav"),
				"Nav",
				context => string.Format(
					"Automatically converted Nav=\"{0}\" attribute into MovementBy=\"{1}\"."
					+ "  Please update profile to use MovementBy, instead.",
					Args["Nav"],
					MovementBy));

			UsageCheck_DeprecatedAttribute(
				xElement,
				Args.Keys.Contains("ObjectType"),
				"ObjectType",
				context => "The ObjectType attribute is no longer used by InteractWith."
							+ "  You may safely remove it from the profile call to the InteractWith behavior.");

			UsageCheck_DeprecatedAttribute(
				xElement,
				MobState == MobStateType.BelowHp,
				"MobState=\"BelowHp\"",
				context => "Please remove the 'MobState=\"BelowHp\"' attribute."
							+ "  The \"BelowHp\" value is no longer used, and has been deprecated."
							+ "  The \"MobHpPercentLeft\" attribute alone is sufficient to capture intent.");
		}


		protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
		{
			UsageCheck_SemanticCoherency(
				xElement,
				!(MobIdIncludesSelf || MobIds.Any() || FactionIds.Any()),
				context => "You must specify one or more: MobIdIncludesSelf, MobIdN, FactionIdN");

			UsageCheck_SemanticCoherency(
				xElement,
				RangeMax.HasValue && RangeMin.HasValue && ((RangeMax.Value - RangeMin.Value) < RangeMinMaxEpsilon),
				context => string.Format(
					"Range({0}) must be at least {1} greater than MinRange({2}).",
					RangeMax.Value,
					RangeMinMaxEpsilon,
					RangeMin.Value));

			UsageCheck_SemanticCoherency(
				xElement,
				MobIdIncludesSelf && !((InteractByCastingSpellId > 0) || (InteractByUsingItemId > 0)),
				context => "When \"MobIdIncludesSelf\" is specified, one of the following attributes must also be specified:"
							+ "InteractByCastingSpellId, InteractByUsingItemId");

		    var interactSpell = GetInteractSpell();

            UsageCheck_SemanticCoherency(
                xElement,
                interactSpell != null && interactSpell.HasRange && RangeMin.HasValue && RangeMin.Value < interactSpell.MinRange,
                context => string.Format("RangeMin({0}) must be equal to or greater than the spell's minimum cast range({1})", 
                    RangeMin.Value, interactSpell.MinRange));

            UsageCheck_SemanticCoherency(
                xElement,
                interactSpell != null && interactSpell.HasRange && RangeMax.HasValue && RangeMax.Value > interactSpell.MaxRange,
                context => string.Format("RangeMax({0}) must be equal to or less than the spell's maximum cast range({1})",
                    RangeMax.Value, interactSpell.MaxRange));

		}

		private enum NavigationModeType
		{
			Mesh,
			CTM,
			[UsedImplicitly]
			None,
		}

		private enum QuestFrameDisposition
		{
			Accept,
			Complete,
			Continue,
			Ignore,
			TerminateBehavior,
			TerminateProfile
		}

		#endregion

		#region Private and Convenience variables

		private const double RangeMinMaxEpsilon = 3.0;
		private UtilityCoroutine.NoMobsAtCurrentWaypoint _noMobsAtCurrentWaypoint;
		private UtilityCoroutine.WaitForInventoryItem _waitForInventoryItem;
		private WaitTimer _waitTimerAfterInteracting = null;
		private WaitTimer _watchdogTimerToReachDestination = null;
		private readonly WaitTimer _moveCloserTimerAfterSpellLosFailed = new WaitTimer(TimeSpan.FromSeconds(1));
		private BindingEventStateType BindingEventState { get; set; }
		private int Counter { get; set; }
		private int GossipPageIndex { get; set; }
		private HuntingGroundsType HuntingGrounds { get; set; }
		private int InteractAttemptCount { get; set; }

		private bool InteractByRightClick
		{
			get { return (InteractByUsingItemId == 0) && (InteractByCastingSpellId == 0); }
		}

		private WoWItem ItemToUse { get; set; }
		private WoWUnit SelectedAliveTarget { get; set; }
		private WoWObject SelectedTarget { get; set; }

		public override string SubversionId
		{
			get { return ("$Id$"); }
		}

		public override string SubversionRevision
		{
			get { return ("$Revision$"); }
		}

		private enum BindingEventStateType
		{
			BindingEventUnhooked,
			BindingEventHooked,
			BindingEventFired,
		}

		#endregion

		#region Cleanup

		public override void OnFinished()
		{
			Targeting.Instance.IncludeTargetsFilter -= Targeting_IncludeTargetsFilter;
			Targeting.Instance.RemoveTargetsFilter -= Targeting_RemoveTargetsFilter;
			Targeting.Instance.WeighTargetsFilter -= Targeting_OnWeighTargetsFilter;
			base.OnFinished();
		}

		#endregion

		#region Overrides of CustomForcedBehavior

		// CreateBehavior supplied by QuestBehaviorBase.
		// Instead, provide CreateMainBehavior definition.

		// Dispose supplied by QuestBehaviorBase.
		// Instead, provide CreateMainBehavior definition.

		// IsDone provided by QuestBehaviorBase.
		// Call the QuestBehaviorBase.BehaviorDone() method when you want to indicate your behavior is complete.

		// OnFinished provided by QuestBehaviorBase.

		public override void OnStart()
		{
			// Hunting ground processing...
			// NB: We had to defer this processing from the constructor, because XElement isn't available
			// to parse child XML nodes until OnStart() is called.
			HuntingGrounds = HuntingGroundsType.GetOrCreate(Element,
															"HuntingGrounds",
															new WaypointType(HuntingGroundCenter, "hunting ground center"));
			IsAttributeProblem |= HuntingGrounds.IsAttributeProblem;

			// Let QuestBehaviorBase do basic initializaion of the behavior, deal with bad or deprecated attributes,
			// capture configuration state, install BT hooks, etc.  This will also update the goal text.
			var isBehaviorShouldRun = OnStart_QuestBehaviorCore(GetGoalText());

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (isBehaviorShouldRun)
			{
				// Setup settings to prevent interference with your behavior --
				// These settings will be automatically restored by QuestBehaviorBase when Dispose is called
				// by Honorbuddy, or the bot is stopped.

				// We need to disable 'pull distance' while this behavior in progress.
				// If we don't, HBcore tends to retarget things and pull them while we are in the middle of
				// an activity.

				if (IgnoreCombat)
				{
					LevelBot.BehaviorFlags &= ~BehaviorFlags.Combat;
				}

				_waitTimerAfterInteracting = new WaitTimer(TimeSpan.FromMilliseconds(WaitTime));

				Targeting.Instance.IncludeTargetsFilter += Targeting_IncludeTargetsFilter;

				Targeting.Instance.RemoveTargetsFilter += Targeting_RemoveTargetsFilter;

				Targeting.Instance.WeighTargetsFilter += Targeting_OnWeighTargetsFilter;

				// NB: With the post-.557 HB releases, Honorbuddy will keep the NPC dialog boxes up
				// after a <PickUp> directive.  This looks more human like, and is a good thing.
				// Unfortunately, a <PickUp> immediate followed by an <InteractWith> will cause InteractWith
				// to see an unexpected quest dialog frame.  To prevent problems, we close all dialogs
				// when InteractWith is started, here.
				CloseOpenFrames(true);

				// If toon doesn't know any of the prescribed spells, we're done...
				if ((InteractByCastingSpellId > 0) && !SpellManager.HasSpell(InteractByCastingSpellId))
				{
					var message = string.Format("Toon doesn't know: {0}", Utility.GetSpellNameFromId(InteractByCastingSpellId));
					QBCLog.ProfileError(message);
					BehaviorDone(message);
				}
			}
		}

		#endregion

		#region Main Behaviors

		//protected override Composite CreateBehavior_CombatMain()
		//{
		//	return new Decorator(context => !IsDone, new PrioritySelector());
		//}

		protected override Composite CreateBehavior_CombatOnly()
		{
			return new PrioritySelector(
				new Action(
					context =>
					{
						// Force recalculation of time to reach destination after combat completes...
						_watchdogTimerToReachDestination = null;

						// If current target is not attackable, blacklist it...
						if ((Me.CurrentTarget != null) && !Me.CurrentTarget.Attackable)
						{
							Me.CurrentTarget.BlacklistForCombat(TimeSpan.FromSeconds(120));
							Me.ClearTarget();
						}

						return RunStatus.Failure;
					})
				);
		}


		protected override Composite CreateBehavior_DeathMain()
		{
			return new Action(
				ctx =>
				{
					if (Me.IsDead)
						SelectedTarget = null;
					return RunStatus.Failure;
				});
		}

		protected override Composite CreateMainBehavior()
		{
			return new ActionRunCoroutine(ctx => MainCoroutine());
		}

		private async Task<bool> MainCoroutine()
		{
			// return if behavior is considered done or if targeting is not empty meaning we have something to kill and killing takes priority over any interaction
			bool shouldFight = Targeting.Instance.FirstUnit != null
				&& LevelBot.BehaviorFlags.HasFlag(BehaviorFlags.Combat)
				&& (Me.Combat || LevelBot.BehaviorFlags.HasFlag(BehaviorFlags.Pull));

			if (IsDone || shouldFight)
				return false;

			// Delay, if necessary...
			// NB: We must do this prior to checking for 'behavior done'.  Otherwise, profiles
			// that don't have an associated quest, and put the behavior in a <While> loop will not behave
			// as the profile writer expects.  They expect the delay to be executed if the interaction
			// succeeded.
			if (!_waitTimerAfterInteracting.IsFinished)
			{
				TreeRoot.StatusText = string.Format(
					"Completing {0} wait of {1}",
					Utility.PrettyTime(TimeSpan.FromSeconds((int)_waitTimerAfterInteracting.TimeLeft.TotalSeconds)),
					Utility.PrettyTime(_waitTimerAfterInteracting.WaitTime));
				return true;
			}

			// Counter is used to determine 'done'...
			// NB: If QuestObjectiveIndex was specified, we don't care what counter is.  Instead,
			// we want the objective to complete.
			if (QuestObjectiveIndex == 0 && Counter >= NumOfTimes)
			{
				BehaviorDone(string.Format("Reached our required count of {0}.", NumOfTimes));
				return true;
			}

			// If WoWclient has not placed items in our bag, wait for it...
			// NB: This clumsiness is because Honorbuddy can launch and start using the behavior before the pokey
			// WoWclient manages to put the item into our bag after accepting a quest.  This delay waits
			// for the item to show up, if its going to.
			if (InteractByUsingItemId > 0 && !Query.IsViable(ItemToUse))
			{
				if (_waitForInventoryItem == null)
					_waitForInventoryItem = new UtilityCoroutine.WaitForInventoryItem(() => InteractByUsingItemId, () => BehaviorDone());
				// return if waiting for item to show up in bags.
				if (await _waitForInventoryItem)
					return true;
				ItemToUse = Me.CarriedItems.FirstOrDefault(i => (i.Entry == InteractByUsingItemId));
			}

			// If interact target no longer meets qualifications, try to find another...
			if (!IsInteractNeeded(SelectedTarget, MobState))
			{
				SelectedTarget = FindViableTargets(MobState).FirstOrDefault();

				if (SelectedTarget != null)
				{
					CloseOpenFrames();
					Me.ClearTarget();

					this.UpdateGoalText(QuestId, GetGoalText());
					InteractAttemptCount = 0;
					_watchdogTimerToReachDestination = null;
				}

				// If we're looking for 'dead' targets, and there are none...
				// But, there are 'alive' targets that fit the bill, go convert the 'alive' ones to 'dead'.
				if ((SelectedTarget == null) && (!Query.IsViable(SelectedAliveTarget) || !SelectedAliveTarget.IsAlive)
					&& (MobState == MobStateType.Dead))
				{
					SelectedAliveTarget = FindViableTargets(MobStateType.Alive).FirstOrDefault() as WoWUnit;
					if (SelectedAliveTarget != null)
						return true;
				}
			}

			// break if we have something else to do.
			if (!Query.IsPoiIdle(BotPoi.Current))
				return false;

			// No mobs in immediate vicinity...
			// NB: if the terminateBehaviorIfNoTargetsProvider argument evaluates to 'true', calling
			// this sub-behavior will terminate the overall behavior.
			if (!Query.IsViable(SelectedTarget))
			{
				if (_noMobsAtCurrentWaypoint == null)
				{
					_noMobsAtCurrentWaypoint =
						new UtilityCoroutine.NoMobsAtCurrentWaypoint(
							() => HuntingGrounds,
							() => MovementBy,
							() => { if (!WaitForNpcs) BehaviorDone("Terminating--\"WaitForNpcs\" is false."); },
							() =>
								TargetExclusionAnalysis.Analyze(
									Element,
									() => Query.FindMobsAndFactions(MobIds, MobIdIncludesSelf, FactionIds),
									TargetExclusionChecks));
				}

				if (await _noMobsAtCurrentWaypoint)
					return true;
			}

			#region Deal with mob we've selected for interaction...

			if (Query.IsViableForInteracting(SelectedTarget, IgnoreMobsInBlackspots, NonCompeteDistance))
			{
				if (await SubCoroutine_HandleFrame_Loot())
					return true;

				if (await SubCoroutine_HandleFrame_Gossip())
					return true;

				if (await SubCoroutine_HandleFrame_Merchant())
					return true;

				if (await SubCoroutine_HandleFrame_Quest())
					return true;

				if (await SubCoroutine_HandleFramesComplete())
					return true;

				// If anything in the frame handling made the target no longer viable for interacting,
				// go find a new target...
				// NB: This mostly happens when NPCs close the gossip dialog on their end and walk away
				// or despawn, or outright go 'non viable' after the chat.
				if (!Query.IsViableForInteracting(SelectedTarget, IgnoreMobsInBlackspots, NonCompeteDistance))
				{
					return true;
				}

				#region Interact with, or use item on, selected target...

				// Show user the target that's interesting to us...
				// NB: If we've been in combat, our current target may remain on a dead body or other object.
				// This will confuse HB as we try to interact with an object.  So, we either guarantee that
				// a target is selected if it is a WoWUnit, or clear the target if its an object.
				var myTarget = Me.CurrentTarget;
				if (myTarget != SelectedTarget)
				{
					var wowUnit = SelectedTarget as WoWUnit;
					if ((Query.IsViable(wowUnit)) && (wowUnit.CanSelect) && (IsWithinInteractDistance(wowUnit) || Query.IsInLineOfSight(wowUnit)))
					{
						Utility.Target(wowUnit);
					}
					else if (myTarget != null)
					{
						Me.ClearTarget();
					}
				}

				if (await SubCoroutine_DoMoveToTarget())
					return true;

				// NB: We do the move before waiting for the cooldown.  The hope is that for most items, the
				// cooldown will have elapsed by the time we get within range of the next target.
				if (ItemToUse != null && ItemToUse.CooldownTimeLeft > TimeSpan.Zero)
				{
					TreeRoot.StatusText = string.Format(
						"Waiting for {0} cooldown ({1} remaining)",
                        ItemToUse.SafeName,
						Utility.PrettyTime(TimeSpan.FromSeconds((int)ItemToUse.CooldownTimeLeft.TotalSeconds)));
					return true;
				}

				// If we've exceeded our maximum allowed attempts to interact, blacklist mob for a while...
				if (++InteractAttemptCount > AttemptCountMax)
				{
					var blacklistTime = BlacklistInteractTarget(SelectedTarget);

					QBCLog.Warning(
						"Exceeded our maximum count({0}) at attempted interactions--blacklisting {1} for {2}",
						AttemptCountMax,
						SelectedTarget.SafeName,
						Utility.PrettyTime(blacklistTime));
					return false;
				}

				// Interact by casting spell...
				if (InteractByCastingSpellId > 0)
				{
					var castResult = await UtilityCoroutine.CastSpell(InteractByCastingSpellId, SelectedTarget);
					if (castResult != SpellCastResult.Succeeded)
					{
						// The WoWUnit.InLineOfSpellSight is not always correct so if we get a LOS error we need
						// to move closer while the _moveCloserTimerAfterSpellLosFailed timer is running.
						if (castResult == SpellCastResult.LineOfSight)
						{
							_moveCloserTimerAfterSpellLosFailed.Reset();
						}
						return false;
					}
				}

				// Interact by casting spell...
				if (InteractByUsingItemId > 0)
				{
					await UtilityCoroutine.UseItemOnTarget(
							InteractByUsingItemId,
							SelectedTarget,
							() =>
								BehaviorDone(
									string.Format("Terminating behavior due to missing {0}", Utility.GetItemNameFromId(InteractByUsingItemId))));
				}

				// Interact by right-click..
				if (InteractByRightClick)
				{
					if (!await UtilityCoroutine.Interact(SelectedTarget))
						return false;
				}

				// Peg tally, if follow-up actions not expected...
				if (!IsFrameExpectedFromInteraction)
				{
					// NB: Some targets go invalid immediately after interacting with them.
					// So we must make certain that we don't intend to use such invalid targets
					// here.
					BlacklistInteractTarget(SelectedTarget);
					_waitTimerAfterInteracting.Reset();
					++Counter;

					if (IsClearTargetNeeded(SelectedTarget))
					{
						Me.ClearTarget();
					}

					SelectedTarget = null;
				}

				#endregion
			}

			#endregion

			return false;
		}

		#endregion

		#region Sub-Behaviors

		private async Task<bool> SubCoroutine_DoMoveToTarget()
		{
			var isViableTarget = Query.IsViable(SelectedTarget);

			if (!isViableTarget)
			{
				_watchdogTimerToReachDestination = null;
			}
			// Watchdog only runs when not in combat...
			else if (!Me.Combat)
			{
				// If watchdog timer isn't running, start it...
				// NB: This places upper bound on time allowed to reach destination.  If a mob is bugged
				// or unreachable for some other reason, this timer will blacklist the mob when it expires.
				// Also, this timer will be terminated when we get in combat, so we may need to re-establish
				// it when combat completes.
				if (_watchdogTimerToReachDestination == null)
				{
					_watchdogTimerToReachDestination =
						new WaitTimer(
							SelectedTarget.Location.MaximumTraversalTime(2.5, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(180)));
					_watchdogTimerToReachDestination.Reset();
				}

				if (_watchdogTimerToReachDestination.IsFinished)
				{
					var blacklistTime = BlacklistInteractTarget(SelectedTarget);
					QBCLog.Warning(
						"Taking too long to reach {0}--blacklisting for {1}",
						SelectedTarget.SafeName,
						Utility.PrettyTime(blacklistTime));
					_watchdogTimerToReachDestination = null;
					isViableTarget = false;
				}
			}

			if (!isViableTarget)
				return false;

		    double rangeMin;
            if (IsDistanceGainNeeded(SelectedTarget, out rangeMin))
			{
				var destinationName = string.Format(
					"gain distance from {0} (id:{1}, dist:{2:F1}/{3:F1})",
					GetName(SelectedTarget),
					SelectedTarget.Entry,
					SelectedTarget.Distance,
                    rangeMin);

				if (await UtilityCoroutine.MoveTo(
                    Utility.GetPointToGainDistance(SelectedTarget, rangeMin),
					destinationName,
					MovementBy))
				{
					return true;
				}
			}
			if (IsDistanceCloseNeeded(SelectedTarget))
			{
				if (MovementBy == MovementByType.NavigatorOnly
					&& !Navigator.CanNavigateFully(StyxWoW.Me.Location, SelectedTarget.Location)
					&& (!Me.IsFlying || !Me.IsOnTransport))
				{
					TimeSpan blacklistDuration = BlacklistInteractTarget(SelectedTarget);
					TreeRoot.StatusText = string.Format(
						"Unable to navigate to {0} (dist: {1:F1})--blacklisting for {2}.",
						GetName(SelectedTarget),
						SelectedTarget.Distance,
						blacklistDuration);
					return true;
				}
				if (MovementBy == MovementByType.None)
				{
					TimeSpan blacklistDuration = BlacklistInteractTarget(SelectedTarget);
					TreeRoot.StatusText = string.Format(
						"{0} is out of range (dist: {1:F1})--blacklisting for {2}.",
						GetName(SelectedTarget),
						SelectedTarget.Distance,
						blacklistDuration);
					return true;
				}

				var destinationName = string.Format(
					"interact with {0} (id: {1}, dist: {2:F1}{3}, TtB: {4})",
					GetName(SelectedTarget),
					SelectedTarget.Entry,
					SelectedTarget.Distance,
					(Query.IsInLineOfSight(SelectedTarget) ? "" : ", noLoS"),
					// Time-to-Blacklist
					((_watchdogTimerToReachDestination != null)
						? Utility.PrettyTime(_watchdogTimerToReachDestination.TimeLeft)
						: "\u8734"));

				if (await UtilityCoroutine.MoveTo(SelectedTarget.Location, destinationName, MovementBy))
					return true;
			}

			// Prep to interact...
			await UtilityCoroutine.MoveStop();
			if (await UtilityCoroutine.ExecuteMountStrategy(PreInteractMountStrategy))
				return true;
			_watchdogTimerToReachDestination = null;
			// Face target...
			Utility.Target(SelectedTarget, true);
			return false;
		}


		private async Task<bool> SubCoroutine_HandleFramesComplete()
		{
			if (IsExpectedFrameOpen)
			{
				TreeRoot.StatusText = string.Format("Interaction with {0} complete.", GetName(SelectedTarget));
				CloseOpenFrames();
				_waitTimerAfterInteracting.Reset();

				// Some mobs go non-viable immediately after interacting with them...
				if (Query.IsViable(SelectedTarget))
				{
					BlacklistInteractTarget(SelectedTarget);
					if (IsClearTargetNeeded(SelectedTarget))
					{
						Me.ClearTarget();
					}
				}

				SelectedTarget = null;
				return true;
			}

			// Some mobs go non-viable immediately after interacting with them...
			// For instance, interacting with mobs that give you automatic taxi rides.
			// We must guard against trying to interact with them further.
			if (!Query.IsViable(SelectedTarget))
			{
				SelectedTarget = null;
				return true;
			}
			return false;
		}


		private async Task<bool> SubCoroutine_HandleFrame_Gossip()
		{
			if (IsGossipFrameVisible)
			{
				if (InteractByGossipOptions.Length > 0)
				{
					TreeRoot.StatusText = string.Format("Gossiping with {0}", GetName(SelectedTarget));
					BindingEventState = BindingEventStateType.BindingEventUnhooked;
					GossipPageIndex = 0;
					while (!IsDone && GossipPageIndex < InteractByGossipOptions.Length)
					{
						GossipEntry gossipEntry;
						if (!TryGetGossipEntry(out gossipEntry))
						{
							QBCLog.Error(
								"{0} is not offering gossip option {1} on page {2}."
								+ "  Did competing player alter NPC state?"
								+ "  Did you stop/start Honorbuddy?"
								+ "  Terminating behavior.",
								GetName(SelectedTarget),
								InteractByGossipOptions[GossipPageIndex] + 1,
								GossipPageIndex + 1);
							CloseOpenFrames();
							Me.ClearTarget();
							BehaviorDone();
							return false;
						}

						// Log the gossip option we're about to take...
						QBCLog.DeveloperInfo(
							"Selecting Gossip Option({0}) on page {1}: \"{2}\"",
							gossipEntry.Index + 1,
							GossipPageIndex + 1,
							gossipEntry.Text);

						// If Innkeeper 'binding option', arrange to confirm ensuing popup...
						if (gossipEntry.Type == GossipEntry.GossipEntryType.Binder)
						{
							BindingEventState = BindingEventStateType.BindingEventHooked;
							Lua.Events.AttachEvent("CONFIRM_BINDER", HandleConfirmForBindingAtInn);
						}

						GossipFrame.Instance.SelectGossipOption(InteractByGossipOptions[GossipPageIndex]);
						++GossipPageIndex;

						// If gossip is complete, claim credit...
						// Frequently, the last gossip option in a chain will start a fight.  If this happens,
						// and we don't claim credit, the behavior will hang trying to re-try a gossip with the NPC,
						// and the NPC doesn't want to gossip any more.
						if (GossipPageIndex >= InteractByGossipOptions.Length)
						{
							QBCLog.DeveloperInfo("Gossip with {0} complete.", GetName(SelectedTarget));

							// NB: Some merchants require that we gossip with them before purchase.
							// If the caller has also specified a "buy item", then we're not done yet.
							if ((InteractByBuyingItemId <= 0) && (InteractByBuyingItemInSlotNum <= 0))
							{
								BlacklistInteractTarget(SelectedTarget);
								_waitTimerAfterInteracting.Reset();
								++Counter;
							}
						}
						await Coroutine.Wait((int)Delay.AfterInteraction.TotalMilliseconds, () => !IsGossipFrameVisible);
					}
					// If the NPC pops down the dialog for us, or goes non-viable after gossip...
					// Go ahead and blacklist it, so we don't try to interact again.

					if (!IsGossipFrameVisible || !Query.IsViable(SelectedTarget))
					{
						TreeRoot.StatusText = string.Format("Gossip with {0} complete.", GetName(SelectedTarget));
						_waitTimerAfterInteracting.Reset();
						++Counter;

						BlacklistInteractTarget(SelectedTarget);
						if (IsClearTargetNeeded(SelectedTarget))
						{
							Me.ClearTarget();
						}
					}
				}
				// Only a problem if Gossip frame, and not also another frame type...
				if ((InteractByGossipOptions.Length <= 0) && IsGossipFrameVisible && !IsMultipleFramesVisible())
				{
					QBCLog.Warning("[PROFILE ERROR]: Gossip frame not expected--ignoring.");
				}
				await Coroutine.Sleep((int)Delay.AfterInteraction.TotalMilliseconds);
			}

			// Tell user if he is now bound to a new location...
			if (BindingEventState != BindingEventStateType.BindingEventUnhooked)
			{
				if (await Coroutine.Wait(10000, () => BindingEventState == BindingEventStateType.BindingEventFired))
				{
					await Coroutine.Sleep((int)Delay.AfterInteraction.TotalMilliseconds);
					Lua.DoString("ConfirmBinder(); StaticPopup_Hide('CONFIRM_BINDER')");
					// NB: We give the WoWclient a little time to register our new location
					// before asking it "where is our hearthstone set?"
					await Coroutine.Sleep(1000);
					var boundLocation = Lua.GetReturnVal<string>("return GetBindLocation()", 0);

					QBCLog.Info(
						"You are now bound at {0} Inn in {1}({2})",
						(Query.IsViable(SelectedTarget) ? GetName(SelectedTarget) : "the"),
						boundLocation,
						Me.HearthstoneAreaId);

					BindingEventState = BindingEventStateType.BindingEventUnhooked;
					Lua.Events.DetachEvent("CONFIRM_BINDER", HandleConfirmForBindingAtInn);
				}
			}
			return false;
		}

		private async Task<bool> SubCoroutine_HandleFrame_Loot()
		{
			// Nothing really special for us to do here.  HBcore will take care of 'normal' looting.
			// And looting objects through "interaction" is usually nothing more than right-clicking
			// on the object and a loot frame is not even produced.  But this is here, just in case
			// a loot frame is produced, and HBcore doesn't deal with it.
			if (LootFrame.Instance.IsVisible)
			{
				TreeRoot.StatusText = string.Format("Looting {0}", GetName(SelectedTarget));
				LootFrame.Instance.LootAll();
				await Coroutine.Sleep((int)Delay.AfterInteraction.TotalMilliseconds);
			}
			// we want to 'fall through'
			return false;
		}

		private async Task<bool> SubCoroutine_HandleFrame_Merchant()
		{
			if (!IsMerchantFrameVisible)
				return false;

			if ((InteractByBuyingItemId > 0) || (InteractByBuyingItemInSlotNum >= 0))
			{
				MerchantItem item = (InteractByBuyingItemId > 0)
					? MerchantFrame.Instance.GetAllMerchantItems().FirstOrDefault(i => i.ItemId == InteractByBuyingItemId)
					: (InteractByBuyingItemInSlotNum >= 0)
						? MerchantFrame.Instance.GetMerchantItemByIndex(InteractByBuyingItemInSlotNum)
						: null;

				if (item == null)
				{
					if (InteractByBuyingItemId > 0)
					{
						QBCLog.ProfileError(
							"{0} does not appear to carry ItemId({1})--abandoning transaction.",
							GetName(SelectedTarget),
							InteractByBuyingItemId);
					}
					else
					{
						QBCLog.ProfileError(
							"{0} does not have an item to sell in slot #{1}--abandoning transaction.",
							GetName(SelectedTarget),
							InteractByBuyingItemInSlotNum);
					}
				}
				else if ((item.BuyPrice * (ulong)BuyItemCount) > Me.Copper)
				{
					QBCLog.ProfileError(
						"Toon does not have enough money to purchase {0} (qty: {1})"
						+ "--(requires: {2}, have: {3})--abandoning transaction.",
						item.Name,
						BuyItemCount,
						Utility.PrettyMoney(item.BuyPrice * (ulong)BuyItemCount),
						Utility.PrettyMoney(Me.Copper));
				}
				else if ((item.NumAvailable != /*unlimited*/ -1) && (item.NumAvailable < BuyItemCount))
				{
					QBCLog.ProfileError(
						"{0} only has {1} units of {2} (we need {3})--abandoning transaction.",
						GetName(SelectedTarget),
						item.NumAvailable,
						item.Name,
						BuyItemCount);
				}
				else
				{
					QBCLog.Info("Buying {0} (qty: {1}) from {2}", item.Name, BuyItemCount, GetName(SelectedTarget));
					MerchantFrame.Instance.BuyItem(item.Index, BuyItemCount);
				}
				// NB: We do not blacklist merchants.
				++Counter;
			}

			else if (!IsMultipleFramesVisible())
			{
				QBCLog.Warning("[PROFILE ERROR] Merchant frame not expected--ignoring.");
			}

			await Coroutine.Sleep((int)Delay.AfterInteraction.TotalMilliseconds);
			// we want to 'fall through'
			return false;
		}

		private async Task<bool> SubCoroutine_HandleFrame_Quest()
		{
			if (!IsQuestFrameVisible)
				return false;

			if (InteractByQuestFrameAction == QuestFrameDisposition.Accept)
				QuestFrame.Instance.AcceptQuest();
			else if (InteractByQuestFrameAction == QuestFrameDisposition.Complete)
				QuestFrame.Instance.CompleteQuest();
			else if (InteractByQuestFrameAction == QuestFrameDisposition.Continue)
				QuestFrame.Instance.ClickContinue();

			// If the NPC pops down the dialog for us, or goes non-viable after gossip...
			// Go ahead and blacklist it, so we don't try to interact again.
			if (!IsQuestFrameVisible || !Query.IsViable(SelectedTarget))
			{
				TreeRoot.StatusText = string.Format("Quest accept from {0} complete.", GetName(SelectedTarget));
				_waitTimerAfterInteracting.Reset();
				++Counter;

				BlacklistInteractTarget(SelectedTarget);
				if (IsClearTargetNeeded(SelectedTarget))
				{
					Me.ClearTarget();
				}
			}
			if (InteractByQuestFrameAction == QuestFrameDisposition.Ignore)
				QuestFrame.Instance.Close();

			if (!IsMultipleFramesVisible())
			{
				if (InteractByQuestFrameAction == QuestFrameDisposition.TerminateBehavior)
				{
					QBCLog.DeveloperInfo(
						"Behavior Done--due to {0} providing a quest frame, and InteractByQuestFrameDisposition=TerminateBehavior",
						GetName(SelectedTarget));
					CloseOpenFrames(true);
					BehaviorDone();
				}

				if (InteractByQuestFrameAction == QuestFrameDisposition.TerminateProfile)
				{

					QBCLog.ProfileError(
						"{0} provided an unexpected Quest frame--terminating profile."
						+ "  Please provide an appropriate InteractByQuestFrameDisposition attribute to instruct"
						+ " the behavior how to handle this situation. \n\n{1}",
						GetName(SelectedTarget), GetOpenQuestFrameInfo());

					CloseOpenFrames(true);
					BehaviorDone();
				}
			}
			await Coroutine.Sleep((int)Delay.AfterInteraction.TotalMilliseconds);
			// we want to 'fall through'
			return false;
		}

		#endregion

		#region Targeting

		private void Targeting_IncludeTargetsFilter(List<WoWObject> incomingUnits, HashSet<WoWObject> outgoingUnits)
		{
			var isActuallyInCombat = Me.IsActuallyInCombat;

			var clearMobsThatWillAggro = ProactiveCombatStrategy == ProactiveCombatStrategyType.ClearAll
										|| ProactiveCombatStrategy == ProactiveCombatStrategyType.ClearMobsThatWillAggro;

			var selectedUnit = SelectedTarget as WoWUnit;
			// If we expect to gossip, and mob in combat and offers no gossip, help mob...
			// NB: Mobs frequently will not offer their gossip options while they are in combat.
			if (InteractByGossipOptions.Length > 0 && Query.IsViable(selectedUnit) && selectedUnit.Combat)
			{
				var selectedTarget = selectedUnit.CurrentTarget;
				if (selectedTarget != null && selectedTarget.Attackable && selectedTarget.IsHostile)
					outgoingUnits.Add(selectedTarget);
			}

			// hard to gain distance on a mob that aggroed so kill it.
			if (RangeMin.HasValue && Query.IsViable(selectedUnit) && selectedUnit.Aggro)
				outgoingUnits.Add(selectedUnit);

			// if we have a target that needs to be killed before interaction can take place then make it so.
			if (Query.IsViable(SelectedAliveTarget) && SelectedAliveTarget.IsAlive)
				outgoingUnits.Add(SelectedAliveTarget);

			// we only need to include NPCs while out of combat, clearing NPCs in way and when SelectedTarget is set.
			if (isActuallyInCombat || !clearMobsThatWillAggro || SelectedTarget == null)
				return;

			foreach (var unit in incomingUnits.OfType<WoWUnit>())
			{
				if (!unit.IsHostile || !unit.Attackable)
					continue;

				if (!unit.IsUntagged())
					continue;

				if (unit.PlayerControlled)
					continue;

				if (unit.Location.PathTraversalCost(unit.Location) > (unit.MyAggroRange + unit.InteractRange))
					continue;

				outgoingUnits.Add(unit);
			}
		}

		private void Targeting_RemoveTargetsFilter(List<WoWObject> units)
		{
			var removeViableTargets = (InteractAttemptCount == 0) || (Query.FindMobsAttackingMe().Count() <= 1);

			var clearMobsThatWillAggro = ProactiveCombatStrategy == ProactiveCombatStrategyType.ClearAll
										|| ProactiveCombatStrategy == ProactiveCombatStrategyType.ClearMobsThatWillAggro;

			var isActuallyInCombat = Me.IsActuallyInCombat;

			units.RemoveAll(
				o =>
				{
					if (Me.IsFlying)
						return true;

					var unit = (WoWUnit)o;

					if (!isActuallyInCombat)
					{
						if (unit == SelectedAliveTarget)
							return false;

						if (!Query.IsStateMatch_IgnoreMobsInBlackspots(unit, IgnoreMobsInBlackspots))
							return true;

						if (Query.IsInCompetition(unit, NonCompeteDistance))
							return true;

						if (!clearMobsThatWillAggro)
							return true;

						// because linear distance check is cheaper than path distance check we eliminate all that fall outside of linear aggro range distance first
						var aggroRange = unit.MyAggroRange + unit.InteractRange;
						if (unit.Distance > aggroRange)
							return true;

						if (unit.Location.PathTraversalCost(Me.Location) > aggroRange)
							return true;
					}

					// exclude any units that are candidates for interacting unless we are attacked by multiple units.
					if (removeViableTargets && IsViableInteractTarget(unit))
						return true;

					return false;
				});
		}


		private void Targeting_OnWeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			var isActuallyInCombat = Me.IsActuallyInCombat;
			foreach (var targetPriority in units)
			{
				// give viable interact targets lower targeting score while in combat so bot kills non-viable interact targets first
				if (isActuallyInCombat && MobState != MobStateType.Dead && IsViableInteractTarget(targetPriority.Object))
				{
					targetPriority.Score -= 1000;
				}
			}
		}

		#endregion

		#region Helpers

		private PerFrameCachedValue<bool> _isGossipFrameVisible;

		private PerFrameCachedValue<bool> _isLootFrameVisible;

		private PerFrameCachedValue<bool> _isMerchantFrameVisible;

		private PerFrameCachedValue<bool> _isQuestFrameVisible;

		private PerFrameCachedValue<bool> _isTaxiFrameVisible;

		private PerFrameCachedValue<bool> _isTrainerFrameVisible;

		// cache the 'IsVisible' results for one frame to help increase performance.
		private bool IsLootFrameVisible
		{
			get
			{
				return _isLootFrameVisible ??
						(_isLootFrameVisible = new PerFrameCachedValue<bool>(() => LootFrame.Instance.IsVisible));
			}
		}

		private bool IsGossipFrameVisible
		{
			get
			{
				return _isGossipFrameVisible ??
						(_isGossipFrameVisible = new PerFrameCachedValue<bool>(() => GossipFrame.Instance.IsVisible));
			}
		}

		private bool IsMerchantFrameVisible
		{
			get
			{
				return _isMerchantFrameVisible ??
						(_isMerchantFrameVisible =
							new PerFrameCachedValue<bool>(() => MerchantFrame.Instance.IsVisible));
			}
		}

		private bool IsQuestFrameVisible
		{
			get
			{
				return _isQuestFrameVisible ??
						(_isQuestFrameVisible = new PerFrameCachedValue<bool>(() => QuestFrame.Instance.IsVisible));
			}
		}

		private bool IsTaxiFrameVisible
		{
			get
			{
				return _isTaxiFrameVisible ??
						(_isTaxiFrameVisible = new PerFrameCachedValue<bool>(() => TaxiFrame.Instance.IsVisible));
			}
		}

		private bool IsTrainerFrameVisible
		{
			get
			{
				return _isTrainerFrameVisible ??
						(_isTrainerFrameVisible =
							new PerFrameCachedValue<bool>(() => TrainerFrame.Instance.IsVisible));
			}
		}

		private TimeSpan BlacklistInteractTarget(WoWObject selectedTarget)
		{
			// NB: The selectedTarget can sometimes go "non viable" immediately upon interaction.
			// An example: We gossip with an NPC that results in a forced taxi ride.  Honorbuddy suspends
			// this behavior while the taxi ride is in progress, and when we land, the selectedTarget
			// is no longer viable to blacklist.
			if (!Query.IsViable(selectedTarget))
			{
				return TimeSpan.Zero;
			}

			var wowUnit = selectedTarget as WoWUnit;
			bool isShortBlacklist = (wowUnit != null) && ((wowUnit == Me) || Query.IsSharedWorldResource(wowUnit));
			TimeSpan blacklistDuration = TimeSpan.FromSeconds(isShortBlacklist ? 30 : InteractBlacklistTimeInSeconds);

			selectedTarget.BlacklistForInteracting(blacklistDuration);
			return blacklistDuration;
		}


		private void CloseOpenFrames(bool forceClose = false)
		{
			if (forceClose || IsClearTargetNeeded(SelectedTarget))
			{
				Utility.CloseAllNpcFrames();
			}
		}


		/// <summary> Current object we should interact with.</summary>
		/// <value> The object.</value>
		private IEnumerable<WoWObject> FindViableTargets(MobStateType mobState)
		{
			const double minionWeighting = 1000;

			using (StyxWoW.Memory.AcquireFrame())
			{
				var entities =
					from wowObject in Query.FindMobsAndFactions(MobIds, MobIdIncludesSelf, FactionIds)
					where
						Query.IsViable(wowObject)
						&& IsInteractNeeded(wowObject, mobState)
						&& Query.IsStateMatch_MeshNavigable(wowObject, MovementBy)
					let objectCollectionDistance = wowObject.Location.CollectionDistance()
					where
						objectCollectionDistance <= CollectionDistance
					orderby
						objectCollectionDistance
						// Fix for undead-quest (and maybe some more), where the targets can be minions...
						+ (Me.Minions.Contains(wowObject) ? minionWeighting : 1)
						// Make sure 'self' is always last on the list, otherwise we'll ignore all other Mobs...
						+ (wowObject.IsMe ? 1000 : 0)
					select wowObject;

				return entities.ToList();
			}
		}


		private string GetGoalText()
		{
			var action =
				(InteractByUsingItemId > 0)
					? string.Format("by using {0} on", Utility.GetItemNameFromId(InteractByUsingItemId))
					: (InteractByCastingSpellId > 0)
						? string.Format("by casting {0} on", Utility.GetSpellNameFromId(InteractByCastingSpellId))
						: "with";

			var targetNames = string.Join(", ", MobIds.Select(m => Utility.GetObjectNameFromId(m)).Distinct());
			if (MobIdIncludesSelf)
			{
				targetNames = string.Join(", ", "Self", targetNames);
			}

			return string.Format("Interacting {0} {1}", action, targetNames);
		}


		/// <summary>
		///     The SelectedInteractTarget may no longer be viable after interacting with it.
		///     For instance, the NPC may disappear, or if the toon was forced on a taxi ride
		///     as a result of the gossip, the SelectedInteractTarget will no longer be viable
		///     once we land.
		/// </summary>
		private string GetName(WoWObject target)
		{
			return Query.IsViable(target)
                ? target.SafeName
				: (target == SelectedTarget)
					? "selected target"
					: (target.ToUnit() != null)
						? "unit"
						: "object";
		}


		private void HandleConfirmForBindingAtInn(object sender, LuaEventArgs args)
		{
			BindingEventState = BindingEventStateType.BindingEventFired;
		}


		private bool IsClearTargetNeeded(WoWObject wowObject)
		{
			if (!Query.IsViable(wowObject))
			{
				return false;
			}

			var wowUnit = wowObject as WoWUnit;

			return
				(wowUnit != null)
				&& (Me.CurrentTargetGuid == wowUnit.Guid)
				&& !KeepTargetSelected;
		}


		private bool IsDistanceCloseNeeded(WoWObject wowObject)
		{
			bool canInteract = IsWithinInteractDistance(wowObject);

			// Item usage and spell casts must be additionally qualified by LoS constraints...
			if (ItemToUse != null || InteractByCastingSpellId > 0)
			{
				canInteract &= (IgnoreLoSToTarget 
					|| (_moveCloserTimerAfterSpellLosFailed.IsFinished && Query.IsInLineOfSight(wowObject)));
			}

			return !canInteract;
		}


		private bool IsDistanceGainNeeded(WoWObject wowObject, out double rangeMin)
		{
            rangeMin = GetMinRange(wowObject);
            var rangeMax = GetMaxRange(wowObject);

            var unit = wowObject as WoWUnit;
            // can't easily gain distance on a mob that aggroed
            if (unit != null && unit.Aggro)
            {
                return false;
            }

            var distToMob = WoWMovement.ActiveMover.Location.Distance(wowObject.Location);

            // Ignore minRange if its > maxRange
            if (rangeMin > rangeMax)
            {
                QBCLog.Error("RangeMin ({0}) is higher than MaxRange ({1}) of {2}",
                    rangeMin, rangeMax, wowObject.SafeName);
                return  false;
            }

            return distToMob < rangeMin;
		}


		private bool IsFrameExpectedFromInteraction 
		{
			get
			{
				// NB: InteractByLoot is nothing more than a normal "right click" activity
				// on something. If something is normally 'lootable', HBcore will deal with it.
				return
					(InteractByBuyingItemId > 0)
					|| (InteractByBuyingItemInSlotNum > -1)
					|| (InteractByGossipOptions.Length > 0)
					|| (InteractByQuestFrameAction == QuestFrameDisposition.Accept)
					|| (InteractByQuestFrameAction == QuestFrameDisposition.Complete)
					|| (InteractByQuestFrameAction == QuestFrameDisposition.Continue);
			}
		}


		private bool IsExpectedFrameOpen
		{
			get 
			{
				if (InteractByBuyingItemId > 0 || InteractByBuyingItemInSlotNum > -1)
					return IsGossipFrameVisible || IsMerchantFrameVisible;

				if (InteractByGossipOptions.Length > 0)
					return IsGossipFrameVisible;

				if (InteractByQuestFrameAction != QuestFrameDisposition.Ignore)
					return IsGossipFrameVisible || IsQuestFrameVisible;

				return false;
			}
		}


		// 24Feb2013-08:11UTC chinajade
		private bool IsInteractNeeded(WoWObject wowObject, MobStateType mobState)
		{
			if (wowObject == null)
			{
				return false;
			}

			bool isViableForInteracting = Query.IsViableForInteracting(wowObject, IgnoreMobsInBlackspots, NonCompeteDistance);
			WoWUnit wowUnit = wowObject as WoWUnit;

			// We're done, if not a WoWUnit...
			if (wowUnit == null)
			{
				return isViableForInteracting;
			}

			// Additional qualifiers for WoWUnits...        
			return
				isViableForInteracting
				&& Query.IsStateMatch_NotMoving(wowUnit, NotMoving)
				// Many times, units can't gossip unti they're out of combat.  So, assume they can gossip if they are in combat...
				// Once out of combat, we can re-evaluate whether this was a good choice or not.w
				&& ((InteractByGossipOptions.Length <= 0) || wowUnit.Combat || wowUnit.CanGossip)
				&& Query.IsStateMatch_AurasWanted(wowUnit, AuraIdsOnMob)
				&& Query.IsStateMatch_AurasMissing(wowUnit, AuraIdsMissingFromMob)
				&& Query.IsStateMatch_MobState(wowObject, mobState, MobHpPercentLeft);
		}


		private bool IsMultipleFramesVisible()
		{
			int score =
				(IsGossipFrameVisible ? 1 : 0)
				+ (IsMerchantFrameVisible ? 1 : 0)
				+ (IsQuestFrameVisible ? 1 : 0)
				+ (IsTaxiFrameVisible ? 1 : 0)
				+ (IsTrainerFrameVisible ? 1 : 0);

			return score > 1;
		}

		private bool IsViableInteractTarget(WoWObject wowObject)
		{
			if (!Query.IsMobOrFaction(wowObject, MobIds, MobIdIncludesSelf, FactionIds))
				return false;

			if (!IsInteractNeeded(wowObject, MobState))
				return false;

			if (!Query.IsStateMatch_MeshNavigable(wowObject, MovementBy))
				return false;

			var unit = wowObject as WoWUnit;
			if (RangeMin.HasValue && unit != null && unit.Aggro)
				return false;

			return wowObject.Location.CollectionDistance() <= CollectionDistance;
		}


		private bool IsWithinInteractDistance(WoWObject wowObject)
		{
		    if (!Query.IsViable(wowObject))
		        return false;
		    var maxRange = GetMaxRange(wowObject);

		    var distToMob = WoWMovement.ActiveMover.Location.Distance(wowObject.Location);

            return distToMob <= maxRange;
		}

        private double GetMaxRange(WoWObject wowObject)
	    {
            if (!Query.IsViable(wowObject))
                return double.MaxValue;

            if (RangeMax.HasValue)
                return RangeMax.Value;

            var spell = GetInteractSpell();

            if (spell != null && spell.HasRange)
            {
                var wowUnit = wowObject as WoWUnit;
                return wowUnit != null ? spell.MaxRange + wowUnit.CombatReach + 4/3f : spell.MaxRange;
            }

            return wowObject.InteractRange;
	    }

        private double GetMinRange(WoWObject wowObject)
        {
            if (!Query.IsViable(wowObject))
                return 0;

            if (RangeMin.HasValue)
                return RangeMin.Value;

            var spell = GetInteractSpell();

            if (spell != null && spell.HasRange)
            {
                var wowUnit = wowObject as WoWUnit;
                return wowUnit != null ? spell.MinRange + wowUnit.CombatReach + 4 / 3f : spell.MinRange;
            }

            return 0;
        }

	    private WoWSpell GetInteractSpell()
	    {
            return Query.IsViable(ItemToUse)
                ? ItemToUse.ItemSpells.Select(i => i.ActualSpell).FirstOrDefault()
                : (InteractByCastingSpellId > 0 ? WoWSpell.FromId(InteractByCastingSpellId) : null);
	    }

		// 4JUn2013-08:11UTC chinajade
		private List<string> TargetExclusionChecks(WoWObject wowObject)
		{
			var exclusionReasons = TargetExclusionAnalysis.CheckCore(wowObject, this);

			TargetExclusionAnalysis.CheckAuras(exclusionReasons, wowObject, AuraIdsOnMob, AuraIdsMissingFromMob);
			TargetExclusionAnalysis.CheckMobState(exclusionReasons, wowObject, MobState, MobHpPercentLeft);

			var objectCollectionDistance = wowObject.Location.CollectionDistance();
			if (objectCollectionDistance > CollectionDistance)
			{
				exclusionReasons.Add(
					string.Format("ExceedsCollectionDistance({0:F1}, saw {1:F1})", CollectionDistance, objectCollectionDistance));
			}

			var wowUnit = wowObject as WoWUnit;
			if (wowUnit != null)
			{
				if (!Query.IsStateMatch_NotMoving(wowUnit, NotMoving))
				{
					exclusionReasons.Add("Moving");
				}

				if ((InteractByGossipOptions.Length > 0) && !wowUnit.CanGossip)
				{
					exclusionReasons.Add("NoGossip");
				}
			}

			return exclusionReasons;
		}

		private bool TryGetGossipEntry(out GossipEntry gossipEntry)
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
					.First(o => o.Index == InteractByGossipOptions[GossipPageIndex]);
			}
			catch (InvalidOperationException)
			{
				gossipEntry = new GossipEntry();
				return false;
			}
			return true;
		}

		string GetOpenQuestFrameInfo()
		{
			if (!IsQuestFrameVisible)
				return string.Empty;

			var shownQuestId = QuestFrame.Instance.CurrentShownQuestId;
			if (shownQuestId == 0)
				return "No quest is shown in quest frame";

			var quest = Quest.FromId(shownQuestId);
			return string.Format("The quest: {0} (Id: {1}) is shown in quest frame", quest.Name, quest.Id);
		}

		#endregion
	}
}