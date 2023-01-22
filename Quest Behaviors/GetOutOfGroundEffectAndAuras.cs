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
// GETOUTOFGROUNDEFFECTANDAURAS has the following characteristics:
//  * It can move _away_ from mobs casting certain spells
//  * It can move _away_ from mobs possessing certain auras
//  * It can move _behind_ mobs casting certain spells
//  * It can move out of ground-effect spells
//  * It can switch targets to preferred mobs during battle
//  * It can gossip to start an event
//
// BEHAVIOR ATTRIBUTES:
// Event start:
//      StartNpcIdN [optional]
//          N may be omitted, or any numeric value--multiple mobs are supported.
//          If NPC interaction is required to start the event, the StartNpcIdN identifieds one
//          or more NPCs that may be interacted with to initiate the escort.
//          If the quest fails for some reason, the behavior will return to these NPCs
//          to restart the event.
//      StartEventGossipOptions [optional; Default: a dialog-less chat interaction]
//          This argument is only used of a StartNpcIdN is specified.
//          Specifies the set of interaction choices on each gossip panel to make, in order to
//          start the event.
//          The value of this attribute is a comma-separated list of gossip options (e.g., "1,3,1").
//      EventX/EventY/EventZ [optional]
//          This argument specifies the location where the toon should loiter while waiting
//          for the event to start.  If this value is not specified, the toon will normally loiter
//          at its current location where the behavior was started.  If StartNpcIdN is specified,
//          the toon will loiter near the event-starting NPC.
//
// At least one of the following arguments must be specified:
//      MoveAwayFromMobWithAuraIdN [optional]
//          When a mob in the vicinity has this aura, the toon will avoid this mob
//          by moving towards safe spots.
//          This argument applies to any mob that is aggro'd on the toon--not necessarily
//          the mob the toon is currently fighting.
//      MoveAwayFromMobCastingSpellIdN [optional]
//          When a mob in the vicinity is casting this spell, the toon will avoid this mob
//          by moving towards safe spots.
//          This argument applies to any mob that is aggro'd on the toon--not necessarily
//          the mob the toon is currently fighting.
//      MoveBehindMobCastingSpellIdN [optional]
//          When a mob in the vicinity is casting this spell, the toon will avoid this mob
//          by moving behind the mob.
//          This argument applies to any mob that is aggro'd on the toon--not necessarily
//          the mob the toon is currently fighting.
//      MoveOutOfGroundEffectAuraIdN [optional]
//          When a toon has one of thse ground-effect aura debuffs, the toon will
//          move out of the ground-effect by moving towards safe spots.
//      PreferKillingMobIdN [optional]
//          If a mob appears that is identified as 'preferred', the toon will switch from
//          the mob it is currently fighting to the preferred mob.  This only happens if
//          the toon is not already fighting a preferred mob.
//
// Behavior completion criteria:
//      EventCompleteWhen [optional; Default: QuestComplete]
//          [allowed values: QuestComplete/QuestCompleteOrFails/QuestObjectiveComplete/SpecificMobsDead]
//          Defines the completion criteria for the behavior.
//      EventCompleteDeadMobIds [REQUIRED if EventCompleteWhen=SpecificMobsDead]
//          This argument is only consulted if EventCompleteWhen is SpecificMobsDead.
//          This argument identifies one or more mobs.  The killing of _any_ of the
//          identified mobs results in behavior completion.
//
// Quest binding:
//      QuestId [REQUIRED if EscortCompleteWhen=QuestComplete; Default:none]:
//      QuestCompleteRequirement [Default:NotComplete]:
//      QuestInLogRequirement [Default:InLog]:
//          A full discussion of how the Quest* attributes operate is described in
//          http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
//      QuestObjectiveIndex [REQUIRED if EventCompleteWhen=QuestObjectiveComplete]
//          [on the closed interval: [1..5]]
//          This argument is only consulted if EventCompleteWhen is QuestObjectveComplete.
//          The argument specifies the index of the sub-goal of a quest.
//
// Tunables (ideally, the profile would _never_ provide these arguments):
//      AvoidMobMinRange [optional; Default: 25.0]
//          This argument defines the minimum distance that should be between the toon
//          and the mob when the behavior is avoiding a mob.
//      MovementBy [optional; Default NavigatorPreferred]
//          [allowed values: ClickToMoveOnly/NavigatorOnly/NavigatorPreferred]
//          Allows alternative navigation techniques.  You should provide this argument
//          only when an area is unmeshed, or not meshed well.  If ClickToMoveOnly
//          is specified, the area must be free of obstacles; otherwise, the toon
//          will get hung up.
//
// BEHAVIOR EXTENSION ELEMENTS (goes between <CustomBehavior ...> and </CustomBehavior> tags)
// See the "Examples" section for typical usage.
//      Safespots [REQUIRED]
//          Comprised of a set of Hotspots, this element defines a set of locations
//          to which a toon may move to avoid mobs.
//
// THINGS TO KNOW:
// * All looting and harvesting is turned off while the escort is in progress.
//
// POSSIBLE FUTURE IMPROVEMENTS:
// * Timeouts: 1) While searching for NPCs, 2) for overall quest completion
#endregion


#region FAQs
// * There is an NPC that starts the event, but it doesn't have a dialog box pop up?
//      Specify a StartNpcId, but no StartEventGossipOptions.  StartEventGossipOptions
//      defaults to a dialog-less chat interaction.
//
// * There is no NPC that starts the event--just need to "get into position"?
//      Specify an EventX/EventY/EventZ, but no StartNpcId.  This will move the toon
//      into position, and wait for the event to start.
//
// * What happens if I die?
//      This depends on how the behavior was configured:
//      If EventCompleteWhen=QuestCompleteOrFails, and the death causes a quest failure,
//      then this behavior completes.
//      All other conditions, the toon will move back and rez.  After that, if a StartNpcId
//      was specified, it will interact with the event-start NPC to try again.
//      If an EventX/EventY/EventZ was specified, it will move to that location and wait
//      for the mobs to arrive.
//
// * How should I place safespots?
//      The safespots are just a collection of locations to which to run, and the behavior
//      selects from them according to the battlefield conditions (i.e., toon placement,
//      mob placement, etc).  The safespots do _not_ form any kind of 'path'.
//      Safespots should be placed as far away as reasonably possible.  Six spots making
//      a circle with a 50-yard radius should be sufficient.  Try to place the spots
//      such that there are no interfering obstacles.
//
// * The toon is pathing funny to get to the safespots.  How do I handle this?
//      This happens because the area is unmeshed, or meshed badly.  You can resort to
//      MovementType=ClickToMoveOnly.  But, we would recommend you report the problem
//      to the Navigation thread to have the mesh repaired.  "Click to move" doesn't
//      handle obstacles or other obstructions at all.
//
// * What happens if the Quest fails?
//      Normally, the behavior returns to its starting point to try again.  However,
//      if EventCompleteWhen is set to QuestCompleteOrFails, the behavior will simply
//      terminate.
//
// * The quest failed, how do I arrange to abandon it, and go pick it up again?
//      Set the EventCompleteWhen attribute to QuestCompleteOrFails.  In the profile,
//      check for quest failure, and abandon and pick the quest up again, before
//      re-conducting the behavior.
//

#endregion


#region Examples
// "The Celestial Experience" (http://www.wowhead.com/quest=31394)
// This quest has three consecutive NPCs that must be killed in a large circular room.
// The quest starts by chatting with Xuen (http://www.wowhead.com/npc=64528).  This is a dialog-less
// interaction, so we specify a StartNpcId, but no StartEventGossipOptions.
// The NPCs have a set of spells you must avoid:
// * A high damage ground-effect AoE must be avoided (Sha Corruption, http://www.wowhead.com/spell=126625).
//   To 'get out of this effect' we specify a MoveOutOfGroundEffectAuraId1="126625" argument.
// * An AoE directly in front of the caster (Devastation, http://www.wowhead.com/spell=126631).
//   We choose to get out of this spell while it is being cast by moving behind the caster.
//   This is accomplished with a MoveBehindMobCastingSpellId1="126631" argument.
// * The mob will buff himself with an 8-sec +300 Strength buff  (Untamed Fury, http://www.wowhead.com/spell=23719)
//   We choose to avoid the mob as long as this buff is up by specifying
//   a MoveAwayFromMobWithAuraId1="23719" argument.
// * The mob will buff himself with a extremely damaging attack (Whirlwind of Anger, http://www.wowhead.com/spell=126633)
//   We get a 'head start' on avoiding this mob while he is casting the spell for 3 seconds
//   by specifying MoveAwayFromMobCastingSpellId1="126633".  While the whirlwind is in progress
//   for another 8 seconds, we want to stay away from the mob.  We do this by specifying an additional
//   an additional MoveAwayFromMobWithAuraId2="126633" argument.
//
//      <CustomBehavior File="GetOutOfGroundEffectAndAuras" StartNpcId="64528" QuestId="31395"
//          EventX="3783.071" EventY="535.3829" EventZ="639.0076"
//          MoveOutOfGroundEffectAuraId1="126625"
//          MoveBehindMobCastingSpellId1="126631"
//          MoveAwayFromMobCastingSpellId1="126633"
//          MoveAwayFromMobWithAuraId1="23719"
//          MoveAwayFromMobWithAuraId2="126633">
//          <Safespots>
//              <!-- safe spots in the room are the gold circles that comprise the big circle -->
//              <Hotspot X="3825.954" Y="550.3646" Z="639.8345" />
//              <Hotspot X="3800.181" Y="567.9801" Z="639.8226" />
//              <Hotspot X="3770.128" Y="557.3201" Z="639.8259" />
//              <Hotspot X="3765.737" Y="516.8966" Z="639.8144" />
//              <Hotspot X="3791.67"  Y="499.2032" Z="639.825" />
//              <Hotspot X="3821.244" Y="509.5078" Z="639.8226" />
//          </Safespots>
//      </CustomBehavior>
// 
#endregion


#region Usings
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
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


namespace Honorbuddy.Quest_Behaviors.GetOutOfGroundEffectAndAuras
{
	[CustomBehaviorFileName(@"GetOutOfGroundEffectAndAuras")]
	public class GetOutOfGroundEffectAndAuras : CustomForcedBehavior
	{
		public delegate WoWPoint LocationDelegate(object context);
		public delegate string MessageDelegate(object context);
		public delegate double RangeDelegate(object context);

		#region Consructor and Argument Processing
		public enum EventCompleteWhenType
		{
			QuestComplete,
			QuestCompleteOrFails,
			QuestObjectiveComplete,
			SpecificMobsDead,
		}

		public enum MovementByType
		{
			ClickToMoveOnly,
			NavigatorOnly,
			NavigatorPreferred,
		}

		public GetOutOfGroundEffectAndAuras(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// Parameters dealing with 'starting' the behavior...
				StartEventGossipOptions = GetAttributeAsArray<int>("StartEventGossipOptions", false, new ConstrainTo.Domain<int>(1, 10), null, null);
				StartNpcIds = GetNumberedAttributesAsArray<int>("StartNpcId", 0, ConstrainAs.MobId, null);
				EventLocation = GetAttributeAsNullable<WoWPoint>("Event", false, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;

				// Parameters dealing with avoidance during battle...
				MoveAwayFromMobWithAuraIds = GetNumberedAttributesAsArray<int>("MoveAwayFromMobWithAuraId", 0, ConstrainAs.SpellId, null);
				MoveAwayFromMobCastingSpellIds = GetNumberedAttributesAsArray<int>("MoveAwayFromMobCastingSpellId", 0, ConstrainAs.SpellId, null);
				MoveBehindMobCastingSpellIds = GetNumberedAttributesAsArray<int>("MoveBehindMobCastingSpellId", 0, ConstrainAs.SpellId, null);
				MoveOutOfGroundEffectAuraIds = GetNumberedAttributesAsArray<int>("MoveOutOfGroundEffectAuraId", 0, ConstrainAs.SpellId, null);
				PreferKillingMobIds = GetNumberedAttributesAsArray<int>("PreferKillingMobId", 0, ConstrainAs.MobId, null);

				// Parameters dealing with when the task is 'done'...
				EventCompleteDeadMobIds = GetNumberedAttributesAsArray<int>("EventCompleteDeadMobId", 0, ConstrainAs.MobId, null);
				EventCompleteWhen = GetAttributeAsNullable<EventCompleteWhenType>("EventCompleteWhen", false, null, null) ?? EventCompleteWhenType.QuestComplete;

				// Quest handling...
				QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
				QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;
				QuestObjectiveIndex = GetAttributeAsNullable<int>("QuestObjectiveIndex", false, new ConstrainTo.Domain<int>(1, 5), null) ?? 0;

				// Behavior tunables (profiles shouldn't specify these unless working around bugs)...
				AvoidMobMinRange = GetAttributeAsNullable<double>("AvoidMobMinRange", false, new ConstrainTo.Domain<double>(5.0, 100.0), null) ?? 25.0;
				MovementBy = GetAttributeAsNullable<MovementByType>("MovementBy", false, null, null) ?? MovementByType.NavigatorPreferred;


				// Semantic Coherency checks --
				if ((StartEventGossipOptions.Count() > 0) && (StartNpcIds.Count() <= 0))
				{
					QBCLog.Error("If StartEscortGossipOptions are specified, you must also specify one or more StartNpcIdN");
					IsAttributeProblem = true;
				}

				if ((QuestId == 0)
					&& ((EventCompleteWhen == EventCompleteWhenType.QuestComplete)
						|| (EventCompleteWhen == EventCompleteWhenType.QuestCompleteOrFails)))
				{
					QBCLog.Error("With a EventCompleteWhen argument of QuestComplete, you must specify a QuestId argument");
					IsAttributeProblem = true;
				}

				if ((EventCompleteWhen == EventCompleteWhenType.QuestObjectiveComplete)
					&& ((QuestId == 0) || (QuestObjectiveIndex == 0)))
				{
					QBCLog.Error("With an EventCompleteWhen argument of QuestObjectiveComplete, you must specify both QuestId and QuestObjectiveIndex arguments");
					IsAttributeProblem = true;
				}

				if ((QuestObjectiveIndex != 0) && (EventCompleteWhen != EventCompleteWhenType.QuestObjectiveComplete))
				{
					QBCLog.Error("The QuestObjectiveIndex argument should not be specified unless EventCompleteWhen is QuestObjectiveComplete");
					IsAttributeProblem = true;
				}

				if ((EventCompleteWhen == EventCompleteWhenType.SpecificMobsDead) && (EventCompleteDeadMobIds.Count() <= 0))
				{
					QBCLog.Error("With an EventCompleteWhen argument of SpecificMobsDead, you must specify one or more EventCompleteDeadMobIdN argument");
					IsAttributeProblem = true;
				}

				if ((MoveAwayFromMobWithAuraIds.Count() <= 0)
					&& (MoveAwayFromMobCastingSpellIds.Count() <= 0)
					&& (MoveBehindMobCastingSpellIds.Count() <= 0)
					&& (MoveOutOfGroundEffectAuraIds.Count() <= 0)
					&& (PreferKillingMobIds.Count() <= 0))
				{
					QBCLog.Error("None of MoveAwayFromMobWithAuraIdN, MoveAwayFromMobCastingSpellIdN, MoveBehindMobCastingSpellIdN,"
									+ " MoveOutOfGroundEffectAuraIdN, or PreferKillingMobIdN were specified");
					IsAttributeProblem = true;
				}

				// If no gossip options specified, set up options for 'interaction without a dialog'...
				if (StartEventGossipOptions.Count() == 0)
					{ StartEventGossipOptions = new int[] { 0 }; }

				for (int i = 0; i < StartEventGossipOptions.Length; ++i)
					{ StartEventGossipOptions[i] -= 1; }
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
		private int[] StartEventGossipOptions { get; set; }
		private int[] StartNpcIds { get; set; }
		private WoWPoint EventLocation { get; set; }

		private double AvoidMobMinRange { get; set; }
		private int[] MoveAwayFromMobWithAuraIds { get; set; }
		private int[] MoveAwayFromMobCastingSpellIds { get; set; }
		private int[] MoveBehindMobCastingSpellIds { get; set; }
		private int[] MoveOutOfGroundEffectAuraIds { get; set; }
		private int[] PreferKillingMobIds { get; set; }
		
		private int[] EventCompleteDeadMobIds { get; set; }
		private EventCompleteWhenType EventCompleteWhen { get; set; }

		private int QuestId { get; set; }
		private int QuestObjectiveIndex { get; set; }
		private QuestCompleteRequirement QuestRequirementComplete { get; set; }
		private QuestInLogRequirement QuestRequirementInLog { get; set; }

		// Tunables
		private MovementByType MovementBy { get; set; }

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return "$Id$"; } }
		public override string SubversionRevision { get { return "$Rev$"; } }
		#endregion


		#region Private and Convenience variables
		private enum BehaviorStateType
		{
			InitialState,
			SearchingForStartUnits,
			InteractingToStart,
			MainEvent,
			CheckDone,
		}

		private readonly TimeSpan Delay_GossipDialogThrottle = TimeSpan.FromMilliseconds(1000);
		private readonly TimeSpan Delay_WoWClientMovementThrottle = TimeSpan.FromMilliseconds(100);
		private readonly TimeSpan LagDuration = TimeSpan.FromMilliseconds((StyxWoW.WoWClient.Latency * 2) + 150);
		private LocalPlayer Me { get { return StyxWoW.Me; } }
		private IEnumerable<WoWUnit> MeAsGroup  = new List<WoWUnit>() { StyxWoW.Me };

		private BehaviorStateType _behaviorState = BehaviorStateType.InitialState;
		private Composite _behaviorTreeHook_Combat = null;
		private Composite _behaviorTreeHook_Death = null;
		private ConfigMemento _configMemento = null;
		private int _gossipOptionIndex;
		private bool _isBehaviorDone = false;
		private List<WoWPoint> _safespots = null;
		private WoWPoint _toonStartingPosition = StyxWoW.Me.Location;
		#endregion

		#region Overrides of CustomForcedBehavior

		protected override Composite CreateBehavior()
		{
			return CreateMainBehavior();
		}

        public override void OnFinished()
        {
            // Clean up unmanaged resources (if any) here...
            if (_behaviorTreeHook_Combat != null)
            {
                TreeHooks.Instance.RemoveHook("Combat_Main", _behaviorTreeHook_Combat);
                _behaviorTreeHook_Combat = null;
            }

            if (_behaviorTreeHook_Death != null)
            {
                TreeHooks.Instance.RemoveHook("Death_Main", _behaviorTreeHook_Death);
                _behaviorTreeHook_Death = null;
            }

            if (_configMemento != null)
            {
                _configMemento.Dispose();
                _configMemento = null;
            }

            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
        }

		public override bool IsDone
		{
			get
			{
				return _isBehaviorDone     // normal completion
						|| !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete);
			}
		}


		public override void OnStart()
		{
			_safespots = ParsePath("Safespots");
			if (_safespots.Count() <= 0)
			{
				QBCLog.Error("The <Safespots> element is missing or empty");
				IsAttributeProblem = true;
			}

			// This reports problems, and stops BT processing if there was a problem with attributes...
			// We had to defer this action, as the 'profile line number' is not available during the element's
			// constructor call.
			OnStart_HandleAttributeProblem();

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (!IsDone)
			{
				_configMemento = new ConfigMemento();

				// Disable any settings that may interfere with the escort --
				// When we escort, we don't want to be distracted by other things.
				// NOTE: these settings are restored to their normal values when the behavior completes
				// or the bot is stopped.
				CharacterSettings.Instance.HarvestHerbs = false;
				CharacterSettings.Instance.HarvestMinerals = false;
				CharacterSettings.Instance.LootChests = false;
				ProfileManager.CurrentProfile.LootMobs = false;
				CharacterSettings.Instance.NinjaSkin = false;
				CharacterSettings.Instance.SkinMobs = false;
				CharacterSettings.Instance.PullDistance = 25;

				// If search path not provided, use our current location...
				if (!_safespots.Any())
					{ _safespots.Add(Me.Location); }

				_toonStartingPosition = Me.Location;

				_behaviorState = BehaviorStateType.InitialState;
				_behaviorTreeHook_Death = CreateDeathBehavior();
				TreeHooks.Instance.InsertHook("Death_Main", 0, _behaviorTreeHook_Death);
				_behaviorTreeHook_Combat = CreateCombatBehavior();
				TreeHooks.Instance.InsertHook("Combat_Main", 0, _behaviorTreeHook_Combat);

				this.UpdateGoalText(QuestId, "Looting and Harvesting are disabled while event in progress");
			}
		}
		#endregion


		#region Main Behaviors

		protected Composite CreateCombatBehavior()
		{
			// NB: This behavior is hooked in at a 'higher priority' than Combat_Main.  We need this
			// because it is sometimes more important to avoid things than fight.

			// NB: We might be in combat with nothing to fight immediately after initiating the event.
			// Be aware of this when altering this behavior.
			
			WoWPoint safeSpot = WoWPoint.Zero;

			return new PrioritySelector(targetUnitsContext => FindAllTargets(),

				// Move away from mob with Aura...
				new PrioritySelector(targetUnitsContext => NearestMobWithAura((IEnumerable<WoWUnit>)targetUnitsContext, MoveAwayFromMobWithAuraIds),
					new Decorator(nearestAvoidMobContext => 
						ShouldMoveToSafespot(
							(WoWUnit)nearestAvoidMobContext, 
							t => t.Distance < AvoidMobMinRange,
							t => PreferredSafespot(t), 
							out safeSpot),
						new PrioritySelector(
							// use speed ehancement abilites to get out of dodge. This will always drop down to behavior below
							new ActionRunCoroutine(nearestAvoidMobContext => UseSpeedEnhancements(safeSpot)),
							// move to safespot.
							UtilityBehavior_MoveWithinRange(nearestAvoidMobContext => safeSpot,
								nearestAvoidMobContext => string.Format("away from mob with '{0}' aura",
									UnitAuraFromAuraIds((WoWUnit)nearestAvoidMobContext, MoveAwayFromMobWithAuraIds).Name))
						))),

				// Move away from mob casting particular AoE spells...
				new PrioritySelector(targetUnitsContext => NearestMobCastingSpell((IEnumerable<WoWUnit>)targetUnitsContext, MoveAwayFromMobCastingSpellIds),
					new Decorator(nearestAuraMobContext =>
						ShouldMoveToSafespot(
							(WoWUnit)nearestAuraMobContext,
							t => t.Distance < AvoidMobMinRange,
							t => PreferredSafespot(t),
							out safeSpot),
						new PrioritySelector(
							// use speed ehancement abilites to get out of dodge. This will always drop down to behavior below
							new ActionRunCoroutine(nearestAuraMobContext => UseSpeedEnhancements(safeSpot)),
							// move to safespot.
							UtilityBehavior_MoveWithinRange(nearestAuraMobContext => safeSpot,
								nearestAuraMobContext => string.Format("away from mob casting '{0}'",
									UnitSpellFromCastingIds((WoWUnit)nearestAuraMobContext, MoveAwayFromMobCastingSpellIds).Name)))
					)),

				// Move behind mobs casting particular AoE spells...
				new PrioritySelector(targetUnitsContext => NearestMobCastingSpell((IEnumerable<WoWUnit>)targetUnitsContext, MoveBehindMobCastingSpellIds),
					new Decorator(nearestBehindMobContext =>
						ShouldMoveToSafespot(
							(WoWUnit)nearestBehindMobContext,
							t => !Me.IsSafelyBehind(t) || !Me.IsSafelyFacing(t),
							// We add an additional +3 yards to move behind mob to deal with "large mob" considerations,
							// and the WoWclient lieing to HB in such situations.
							t => SafespotBehindMob(t, t.CombatReach + 3.0),
							out safeSpot),
						new PrioritySelector(
							// use speed ehancement abilites to get out of dodge. This will always drop down to behavior below
							new ActionRunCoroutine(nearestBehindMobContext => UseSpeedEnhancements(safeSpot)),
							// move to safespot.
							UtilityBehavior_MoveWithinRange(nearestBehindMobContext => safeSpot,
								nearestBehindMobContext => string.Format("behind mob casting '{0}'",
									UnitSpellFromCastingIds((WoWUnit)nearestBehindMobContext, MoveBehindMobCastingSpellIds).Name)),
							new Decorator(nearestBehindMobContext => !Me.IsSafelyFacing((WoWUnit)nearestBehindMobContext),
								new Action(nearestBehindMobContext => { ((WoWUnit)nearestBehindMobContext).Face(); }))
						))),

				// Move out of ground effect...
				new PrioritySelector(targetUnitsContext => ((IEnumerable<WoWUnit>)targetUnitsContext).FirstOrDefault(),
					new Decorator(targetUnitContext =>
						ShouldMoveToSafespot(
							(WoWUnit)targetUnitContext,
							t => GroundEffectFromIds(MoveOutOfGroundEffectAuraIds) != null,
							t => PreferredSafespot(t, true),
							out safeSpot),
						new PrioritySelector(
							// use speed ehancement abilites to get out of dodge. This will always drop down to behavior below
							new ActionRunCoroutine(targetUnitContext => UseSpeedEnhancements(safeSpot)),
							// move to safespot.
							UtilityBehavior_MoveWithinRange(targetUnitContext => safeSpot,
								targetUnitContext => string.Format("out of '{0}' ground effect", GroundEffectFromIds(MoveOutOfGroundEffectAuraIds).Name)))
					)),

				// If a preferred target is available and not targeted, switch targets...
				new PrioritySelector(preferredUnitContext => FindUnitsFromIds(PreferKillingMobIds).FirstOrDefault(),
					new Decorator(preferredUnitContext => (preferredUnitContext != null)
											&& ((Me.CurrentTarget == null) || !PreferKillingMobIds.Contains((int)Me.CurrentTarget.Entry))
											&&  !Query.IsPoiMatch( (WoWUnit)preferredUnitContext, PoiType.Kill),
						new Action(preferredUnitContext =>
						{
                            QBCLog.Info("Reprioritizing target to '{0}'", ((WoWUnit)preferredUnitContext).SafeName);
							BotPoi.Current = new BotPoi((WoWUnit)preferredUnitContext, PoiType.Kill);
						}))
					)
				);
		}

		protected Composite CreateDeathBehavior()
		{
			// If toon dies, we need to restart behavior
			return new Decorator(context => (Me.IsDead || Me.IsGhost) && (_behaviorState != BehaviorStateType.CheckDone),
				new Action(context => { _behaviorState = BehaviorStateType.CheckDone; }));
		}


		protected Composite CreateMainBehavior()
		{
			// NB: Due to the complexity, this behavior is 'state' based.  All necessary actions are
			// conducted in the current state.  If the current state is no longer valid, then a state change
			// is effected.  Ths entry state is "InitialState".
			return new PrioritySelector(
					new Decorator(context => _isBehaviorDone,
						new Action(context => { QBCLog.Info("Behavior Finished"); })),

					new Switch<BehaviorStateType>(context => _behaviorState,
						new Action(context =>   // default case
						{
							QBCLog.Error("BEHAVIOR MAINTENANCE PROBLEM: BehaviorState({0}) is unhandled", _behaviorState);
							TreeRoot.Stop();
							_isBehaviorDone = true;
						}),

						new SwitchArgument<BehaviorStateType>(BehaviorStateType.InitialState,
							new PrioritySelector(
								UtilityBehavior_MoveWithinRange(context => _toonStartingPosition,
																context => "to start location"),
								new Action(context => { _behaviorState = BehaviorStateType.SearchingForStartUnits; })
							)),

						new SwitchArgument<BehaviorStateType>(BehaviorStateType.SearchingForStartUnits,
							new PrioritySelector(
								// If no StartNpcs specified, move on to main event...
								new Decorator(context => (StartNpcIds.Count() <= 0),
									new Action(context => _behaviorState = BehaviorStateType.MainEvent)),

								// If Start NPCs specified, move to them when found...
								new PrioritySelector(startUnitsContext => FindUnitsFromIds(StartNpcIds),
									new Decorator(startUnitsContext => ((IEnumerable<WoWUnit>)startUnitsContext).Count() > 0,
										new PrioritySelector(nearestStartUnitContext => ((IEnumerable<WoWUnit>)nearestStartUnitContext).OrderBy(u => u.Distance).FirstOrDefault(),
											UtilityBehavior_MoveWithinRange(nearestStartUnitContext => ((WoWUnit)nearestStartUnitContext).Location,
                                                                            nearestStartUnitContext => string.Format("to {0}", ((WoWUnit)nearestStartUnitContext).SafeName)),
											new Action(startUnitsContext => _behaviorState = BehaviorStateType.InteractingToStart)
										))),

								new CompositeThrottle(TimeSpan.FromSeconds(10),
									new Action(escortedUnitsContext =>
									{
										QBCLog.Warning("Unable to locate start units: {0}",
													Utility_GetNamesOfUnits(StartNpcIds));
									}))
							)),

						// NB:some events depop the interaction NPC and immediately replace with the instanced version
						// after selecting the appropriate gossip options.  Do NOT be tempted to check for presence of
						// correct NPC while in this state--it will hang the behavior tree if the NPC
						// is immediately replaced on gossip.
						new SwitchArgument<BehaviorStateType>(BehaviorStateType.InteractingToStart,
							new PrioritySelector(
								// If no interaction required to start escort, then proceed excorting
								new Decorator(escortedUnitsContext => StartNpcIds.Count() <= 0,
									new Action(escortedUnitsContext => _behaviorState = BehaviorStateType.MainEvent)),

								// If in combat while interacting, restart the conversation
								new Decorator(escortedUnitsContext => IsInCombat(MeAsGroup),
									new Action(escortedUnitsContext =>
									{
										if (GossipFrame.Instance != null)
											{ GossipFrame.Instance.Close(); }
										_behaviorState = BehaviorStateType.SearchingForStartUnits;
									})),

								// Continue with interaction
								UtilityBehavior_GossipToStartEvent(),

								new Action(escortedUnitsContext =>
								{
									if (GossipFrame.Instance != null)
										{ GossipFrame.Instance.Close(); }
									_behaviorState = BehaviorStateType.MainEvent;
								})
							)),

						new SwitchArgument<BehaviorStateType>(BehaviorStateType.MainEvent,
							new PrioritySelector(
								// Main event doesn't have a whole lot to do, just check for complete or failed...
								new Decorator(context => IsEventComplete() || IsEventFailed(),
									new Action(context => { _behaviorState = BehaviorStateType.CheckDone; })),

								new Decorator(context => (EventLocation != WoWPoint.Empty) && !Navigator.AtLocation(EventLocation),
									UtilityBehavior_MoveWithinRange(context => EventLocation, context => "to start location")),

								new ActionAlwaysFail()
							)),

						new SwitchArgument<BehaviorStateType>(BehaviorStateType.CheckDone,
							new PrioritySelector(
								new Decorator(context => IsEventComplete(),
									new Action(delegate { _isBehaviorDone = true; })),
								new Action(delegate
								{
									QBCLog.Info("Looks like we've failed the event, returning to start to re-do");
									_behaviorState = BehaviorStateType.InitialState;
								})
							))
					));
		}
		#endregion


		#region Helper methods
		// Finds all enemies attacking escorted units, or the myself or pet
		private IEnumerable<WoWUnit> FindAllTargets()
		{
			IEnumerable<WoWUnit> targetsQuery = 
				from unit in ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
				where unit.IsValid && IsAttackingMeOrPet(unit) && !Blacklist.Contains(unit, BlacklistFlags.Combat)
				select unit;

			return (targetsQuery.ToList());
		}


		private IEnumerable<WoWUnit> FindUnitsFromIds(IEnumerable<int> unitIds)
		{
			return ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
				.Where(u => unitIds.Contains((int)u.Entry) && u.IsValid && u.IsAlive)
				.ToList();
		}


		/// <summary>
		/// Returns the WoWAura if we are standing in one of the ground effects
		/// identified by GROUNDEFFECTSPELLIDS.
		/// </summary>
		/// <param name="groundEffectSpellIds"></param>
		/// <returns>may return null</returns>
		private WoWAura GroundEffectFromIds(int[] groundEffectSpellIds)
		{
			return Me.Auras.Values.FirstOrDefault(a => groundEffectSpellIds.Contains(a.SpellId));
		}


		/// <summary>
		/// We rolled our own function here, since WoWUnit.Aggro isn't documented as considering the pet
		/// </summary>
		/// <param name="wowUnit"></param>
		/// <returns>Returns true if the WoWUnit is attacking the player or his pet</returns>
		private bool IsAttackingMeOrPet(WoWUnit wowUnit)
		{
			return (wowUnit.Combat
					&& ( (wowUnit.ThreatInfo.RawPercent > 0) || wowUnit.TaggedByMe || (wowUnit.CurrentTarget == Me) || ((Me.Pet != null) && (wowUnit.CurrentTarget == Me.Pet))));
		}


		private bool IsEventComplete()
		{
			switch (EventCompleteWhen)
			{
				case EventCompleteWhenType.QuestComplete:
				{
					PlayerQuest quest = Me.QuestLog.GetQuestById((uint)QuestId);
					if (quest.IsCompleted)
						{ QBCLog.Info("Event done due to Quest(\"{0}\", {1}) complete", quest.Name, quest.Id); }
					return (quest == null) || quest.IsCompleted;
				}

				case EventCompleteWhenType.QuestCompleteOrFails:
				{
					PlayerQuest quest = Me.QuestLog.GetQuestById((uint)QuestId);
					if (quest.IsCompleted)
						{ QBCLog.Info("Event done due to Quest(\"{0}\", {1}) complete", quest.Name, quest.Id); }
					if (quest.IsCompleted)
						{ QBCLog.Info("Event done due to Quest(\"{0}\", {1}) failed", quest.Name, quest.Id); }
					return (quest == null) || quest.IsCompleted || quest.IsFailed;
				}

				case EventCompleteWhenType.QuestObjectiveComplete:
				{
					bool isObjectiveComplete = (Me.IsQuestObjectiveComplete(QuestId, QuestObjectiveIndex));
					PlayerQuest quest = Me.QuestLog.GetQuestById((uint)QuestId);

					if (isObjectiveComplete)
						{ QBCLog.Info("Event done due to Quest(\"{0}\", {1}) objective {2} complete", quest.Name, quest.Id, QuestObjectiveIndex); }

					return (isObjectiveComplete);
				}
				
				case EventCompleteWhenType.SpecificMobsDead:
				{
					WoWUnit ourDeadTarget =
						ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
						.FirstOrDefault(u => EventCompleteDeadMobIds.Contains((int)u.Entry) && u.IsDead && u.TaggedByMe);

					if (ourDeadTarget != null)
                    { QBCLog.Info("Event done due to killing '{0}'({1})", ourDeadTarget.SafeName, ourDeadTarget.Entry); }
					return (ourDeadTarget != null);
				}
			}

			QBCLog.MaintenanceError("EventCompleteWhen({0}) state is unhandled", EventCompleteWhen);
			TreeRoot.Stop();
			return true;
		}


		/// <returns>returns true if the QUESTID exists in our log, and has failed</returns>
		private bool IsEventFailed()
		{
			bool isFailed = false;

			if (QuestId != 0)
			{
				PlayerQuest quest = Me.QuestLog.GetQuestById((uint)QuestId);
				isFailed |= quest.IsFailed;
			}

			return isFailed;
		}


		/// <returns>returns true, if any member of GROUP (or their pets) is in combat</returns>
		private bool IsInCombat(IEnumerable<WoWUnit> group)
		{
			return group.Any(u => u.Combat || ((u.Pet != null) && u.Pet.Combat));
		}


		/// <summary>
		/// Returns the nearest mob in TARGETS casting one of the SPELLIDs
		/// </summary>
		/// <param name="targets"></param>
		/// <param name="spellIds"></param>
		/// <returns>may return null</returns>
		private WoWUnit NearestMobCastingSpell(IEnumerable<WoWUnit> targets, int[] spellIds)
		{
			IEnumerable<WoWUnit> targetQuery =
				from target in targets
				where UnitSpellFromCastingIds(target, spellIds) != null
				orderby target.Distance
				select target;

			return (targetQuery.FirstOrDefault());
		}


		/// <summary>
		/// Returns the nearest mob in TARGETS that has an aura of one of the AURAIDS
		/// </summary>
		/// <param name="targets"></param>
		/// <param name="auraIds"></param>
		/// <returns>may return null</returns>
		private WoWUnit NearestMobWithAura(IEnumerable<WoWUnit> targets, int[] auraIds)
		{
			IEnumerable<WoWUnit> targetQuery =
				from target in targets
				where target.Auras.Values.Any(a => auraIds.Contains(a.SpellId))
				orderby target.Distance
				select target;

			return (targetQuery.FirstOrDefault());
		}


		/// <returns>returns WoWPoint away from the TARGETUNIT</returns>
		private WoWPoint PreferredSafespot(WoWUnit targetunit, bool isGroundEffectProblem = false)
		{
			IEnumerable<WoWPoint> preferredSafespotOrder;

			preferredSafespotOrder =
				from spot in _safespots
				// The +1 term guarantees distances are never less than 1.  This prevents
				// degenerate cases in preference evaluation for distances less than one, or zero.
				let myDistanceToSpot = Me.Location.Distance(spot) +1
				let mobDistanceToSpot = targetunit.Location.Distance(spot) +1
				orderby // preference ordering equation:
					// prefer spots that are close to me, but not mob
					(myDistanceToSpot / mobDistanceToSpot)

					// Note: the line below caused run back n forth behavior (#HB-580) and it's orginal intent is unknown
					// [Original comment] If ground effect problem, avoid any nearby spots
					//  + ((isGroundEffectProblem && (myDistanceToSpot < AvoidMobMinRange)) ? 100 : 0)

					// prefer spots away from mob
					+ ((targetunit.Location.Distance(spot) < AvoidMobMinRange) ? 1000 : 0)
					// prefer spots the toon doesn't have to run through the mob
					+ (WoWMathHelper.IsInPath(targetunit.Location, (float)AvoidMobMinRange, Me.Location, spot) ? 10000 : 0)
				select spot;

			return preferredSafespotOrder.FirstOrDefault();
		}


		/// <returns>Returns a WoWPoint at DISTANCE behind UNIT</returns>
		private WoWPoint SafespotBehindMob(WoWUnit unit, double distance)
		{
			return unit.Location.RayCast(unit.Rotation + (float)Math.PI, (float)distance);
		}
   
   
		/// <returns>Returns the WoWAura if UNIT has one of the AURAIDS; otherwise, returns null</returns>
		private WoWAura UnitAuraFromAuraIds(WoWUnit unit, int[] auraIds)
		{
			return unit.Auras.Values.FirstOrDefault(a => auraIds.Contains(a.SpellId));
		}
		
		
		/// <returns>Returns the WoWSpell if UNIT is casting one of the SPELLIDS; otherwise, returns null</returns>
		private WoWSpell UnitSpellFromCastingIds(WoWUnit unit, int[] spellIds)
		{
			return (unit.IsCasting ? (spellIds.Contains(unit.CastingSpellId) ? unit.CastingSpell : null)
				: unit.IsChanneling ? (spellIds.Contains(unit.ChanneledCastingSpellId) ? unit.ChanneledSpell : null)
				: null);
		}


		/// <returns>Returns RunStatus.Success if gossip in progress; otherwise, RunStatus.Failure if gossip complete or unnecessary</returns>
		private Composite UtilityBehavior_GossipToStartEvent()
		{
			return new PrioritySelector(gossipUnitContext => FindUnitsFromIds(StartNpcIds).OrderBy(u => u.Distance).FirstOrDefault(),
				new Decorator(gossipUnitContext => gossipUnitContext != null,
					new PrioritySelector(
						// Move to closest unit...
						UtilityBehavior_MoveWithinRange(gossipUnitContext => ((WoWUnit)gossipUnitContext).Location,
                                                gossipUnitContext => ((WoWUnit)gossipUnitContext).SafeName),

						// Interact with unit to open the Gossip dialog...
						new Decorator(gossipUnitContext => (GossipFrame.Instance == null) || !GossipFrame.Instance.IsVisible,
							new Sequence(
								new Action(gossipUnitContext => ((WoWUnit)gossipUnitContext).Target()),
                                new Action(gossipUnitContext => QBCLog.Info("Interacting with \"{0}\" to start event.", ((WoWUnit)gossipUnitContext).SafeName)),
								new Action(gossipUnitContext => ((WoWUnit)gossipUnitContext).Interact()),
								new WaitContinue(LagDuration, ret => GossipFrame.Instance.IsVisible, new ActionAlwaysSucceed()),
								new WaitContinue(Delay_GossipDialogThrottle, ret => GossipFrame.Instance.IsVisible, new ActionAlwaysSucceed()),
								new Action(gossipUnitContext =>
								{
									_gossipOptionIndex = 0;

									// If no dialog is expected, we're done...
									if (StartEventGossipOptions[_gossipOptionIndex] < 0)
										{ return RunStatus.Failure; }
									return RunStatus.Success;
								})
							)),

						// Choose appropriate gossip options...
						// NB: If we get attacked while gossiping, and the dialog closes, then it will automatically be retried.
						new Decorator(gossipUnitContext => (_gossipOptionIndex < StartEventGossipOptions.Length)
															&& (GossipFrame.Instance != null) && GossipFrame.Instance.IsVisible,
								new Sequence(
									new Action(gossipUnitContext => GossipFrame.Instance.SelectGossipOption(StartEventGossipOptions[_gossipOptionIndex])),
									new Action(gossipUnitContext => ++_gossipOptionIndex),
									new WaitContinue(Delay_GossipDialogThrottle, ret => false, new ActionAlwaysSucceed())
								))
					)));
		}


		/// <returns>RunStatus.Success while movement is in progress; othwerise, RunStatus.Failure if no movement necessary</returns>
		private Composite UtilityBehavior_MoveWithinRange(LocationDelegate locationDelegate,
															MessageDelegate locationNameDelegate)
		{
			return new Sequence(
				// Done, if we're already at destination...
				new DecoratorContinue(context => Navigator.AtLocation(locationDelegate(context)),
					new Decorator(context => Me.IsMoving,   // This decorator failing indicates the behavior is complete
						new Action(delegate { WoWMovement.MoveStop(); }))),

				// Notify user of progress...
				new CompositeThrottle(TimeSpan.FromSeconds(1),
					new Action(context =>
					{
						double destinationDistance = Me.Location.Distance(locationDelegate(context));
						string locationName = locationNameDelegate(context) ?? locationDelegate(context).ToString();
						QBCLog.Info("Moving {0}", locationName);
					})
					),

				new Action(context =>
				{
					WoWPoint destination = locationDelegate(context);
					MoveResult moveResult = MoveResult.Failed;

					// Try to use Navigator to get there...
					if ((MovementBy == MovementByType.NavigatorOnly) || (MovementBy == MovementByType.NavigatorPreferred))
						{ moveResult = Navigator.MoveTo(destination); }

					// If Navigator fails, fall back to click-to-move...
					if ((moveResult == MoveResult.Failed) || (moveResult == MoveResult.PathGenerationFailed))
					{
						if (MovementBy == MovementByType.NavigatorOnly)
						{
							QBCLog.Warning("Failed to move--is area unmeshed?");
							return RunStatus.Failure;
						}

						WoWMovement.ClickToMove(destination);
					}

					return RunStatus.Success; // fall through
				}),

				new WaitContinue(Delay_WoWClientMovementThrottle, ret => false, new ActionAlwaysSucceed())
				);
		}


		private string Utility_BuildTimeAsString(TimeSpan timeSpan)
		{
			string formatString = string.Empty;

			if (timeSpan.Hours > 0)         { formatString = "{0:D2}h:{1:D2}m:{2:D2}s"; }
			else if (timeSpan.Minutes > 0)  { formatString = "{1:D2}m:{2:D2}s"; }
			else                            { formatString = "{2:D}s"; }

			return string.Format(formatString, timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
		}


		private string Utility_GetNamesOfUnits(int[] unitIds)
		{
			if ((unitIds == null) || (unitIds.Count() <= 0))
				{ return ("No units"); }

			List<string> unitNames = new List<string>();

			foreach (var unitId in unitIds)
			{
				WoWUnit unit = ObjectManager.GetObjectsOfType<WoWUnit>(true, false).FirstOrDefault(u => u.Entry == unitId);

				unitNames.Add((unit != null)
                            ? unit.SafeName
							: string.Format("NpcId({0})", unitId));                    
			}

			return string.Join(", ", unitNames);
		}

		bool ShouldMoveToSafespot<T>(
			T target,
			Func<T, bool> condition,
			Func<T, WoWPoint> safeSpotSelector,
			out WoWPoint safeSpot) where T : WoWObject
		{
			if (target == null || !condition(target))
			{
				safeSpot = WoWPoint.Zero;
				return false;
			}
			safeSpot = safeSpotSelector(target);
			return true;
		}

		private async Task<bool> UseSpeedEnhancements(WoWPoint destination)
		{
			// Return if ActiveMover is not player
			if (WoWMovement.ActiveMover != StyxWoW.Me)
				return false;

			var myLoc = StyxWoW.Me.Location;
			var distToDestSqr = destination.DistanceSqr(myLoc);
			switch (StyxWoW.Me.Class)
			{
				case WoWClass.Druid:
					if (Me.Shapeshift == ShapeshiftForm.Cat && distToDestSqr > 15 * 15 && SpellManager.CanCast("Dash"))
					{
						SpellManager.Cast("Dash");
					}
					break;
				case WoWClass.Hunter:
					// commented out since it's not always jumping in right direction
					//if (distToDestSqr > 15 * 15 && SpellManager.CanCast("Disengage"))
					//{
					//	var destToMe = myLoc - destination;
					//	var radiansToMe = (float)Math.Atan2(destToMe.Y, destToMe.X);
					//	var rnd = new Random();
					//	WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
					//	StyxWoW.Me.SetFacing(radiansToMe);
					//	yield return StyxCoroutine.Sleep(rnd.Next(100, 250));
					//	WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend);
					//	SpellManager.Cast("Disengage");
					//	yield return StyxCoroutine.Wait(4000, () => !SpellManager.CanCast("Disengage"));
					//	StyxWoW.Me.SetFacing(WoWMathHelper.NormalizeRadian(radiansToMe + (float)Math.PI));
					//	yield return StyxCoroutine.Sleep(rnd.Next(300, 500));
					//}
					break;
				case WoWClass.Mage:
					if (distToDestSqr > 15 * 15
						&& WoWMathHelper.IsFacing(myLoc, Me.Rotation, destination, WoWMathHelper.DegreesToRadians(30)) 
						&& SpellManager.CanCast("Blink"))
					{
						SpellManager.Cast("Blink");
					}
					break;
				case WoWClass.Monk:
					if (distToDestSqr > 15 * 15 
						&& WoWMathHelper.IsFacing(Me.Location, Me.Rotation, destination, WoWMathHelper.DegreesToRadians(30))
						&& SpellManager.CanCast("Roll"))
					{
						SpellManager.Cast("Roll");
					}
					break;
				case WoWClass.Rogue:
					if (distToDestSqr > 15 * 15 && SpellManager.CanCast("Sprint"))
					{
						SpellManager.Cast("Sprint");
					}
					break;
				case WoWClass.Warrior:
					if (distToDestSqr > 8 * 8 && distToDestSqr < 40 * 40 && SpellManager.CanCast("Heroic Leap"))
					{
						SpellManager.Cast("Heroic Leap");
						await Coroutine.Sleep((int)Delay.AfterItemUse.TotalMilliseconds);
						SpellManager.ClickRemoteLocation(destination);
						await Coroutine.Sleep((int)Delay.AfterItemUse.TotalMilliseconds);
					}
					break;
			}
			return false;
		}

		#endregion // Behavior helpers


		#region TreeSharp Extensions
		public class CompositeThrottle : DecoratorContinue
		{
			public CompositeThrottle(TimeSpan throttleTime,
									 Composite composite)
				: base(composite)
			{
				_throttle = new Stopwatch();
				_throttleTime = throttleTime;
			}


			protected override bool CanRun(object context)
			{
				if (_throttle.IsRunning && (_throttle.Elapsed < _throttleTime))
					{ return false; }

				_throttle.Restart();
				return true;
			}

			private readonly Stopwatch _throttle;
			private readonly TimeSpan _throttleTime;
		}
		#endregion


		#region Path parsing
		// never returns null, but the returned Queue may be empty
		public List<WoWPoint> ParsePath(string pathElementName)
		{
			var descendants = Element.Descendants(pathElementName).Elements();
			List<WoWPoint> path = new List<WoWPoint>();

			if (descendants.Count() > 0)
			{
				foreach (XElement element in descendants.Where(elem => elem.Name == "Hotspot"))
				{
					string elementAsString = element.ToString();
					bool isAttributeMissing = false;

					XAttribute xAttribute = element.Attribute("X");
					if (xAttribute == null)
					{
						QBCLog.Error("Unable to locate X attribute for {0}", elementAsString);
						isAttributeMissing = true;
					}

					XAttribute yAttribute = element.Attribute("Y");
					if (yAttribute == null)
					{
						QBCLog.Error("Unable to locate Y attribute for {0}", elementAsString);
						isAttributeMissing = true;
					}

					XAttribute zAttribute = element.Attribute("Z");
					if (zAttribute == null)
					{
						QBCLog.Error("Unable to locate Z attribute for {0}", elementAsString);
						isAttributeMissing = true;
					}

					if (isAttributeMissing)
					{
						IsAttributeProblem = true;
						continue;
					}

					bool isParseProblem = false;

					double x = 0.0;
					if (!double.TryParse(xAttribute.Value, out x))
					{
						QBCLog.Error("Unable to parse X attribute for {0}", elementAsString);
						isParseProblem = true;
					}

					double y = 0.0;
					if (!double.TryParse(yAttribute.Value, out y))
					{
						QBCLog.Error("Unable to parse Y attribute for {0}", elementAsString);
						isParseProblem = true;
					}

					double z = 0.0;
					if (!double.TryParse(zAttribute.Value, out z))
					{
						QBCLog.Error("Unable to parse Z attribute for {0}", elementAsString);
						isParseProblem = true;
					}

					if (isParseProblem)
					{
						IsAttributeProblem = true;
						continue;
					}

					path.Add(new WoWPoint(x, y, z));
				}
			}

			return path;
		}
		#endregion
	}
}

