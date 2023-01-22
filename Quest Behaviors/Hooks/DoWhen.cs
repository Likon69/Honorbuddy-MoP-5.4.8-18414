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
// DOWHEN either uses an item, or casts a spell, when certain trigger criteria have been met.
// DOWHEN is a 'hook' behavior. Unlike normal quest behaviors, it runs constantly in the background
// once the criteria for performing a 'use item' or 'cast spell' activity have been established.
//
// Basic Attributes:
//      ActivityName [at least one is REQUIRED if Command is "Update": ItemId, SpellId, ActivityName]
//          Identifies a custom activity that will be executed when the trigger condition defined in USEWHEN
//          has been met.
//			A custom activity allows the profile writer to define what happens when activity is executed.
//          "ActivityName", "ItemId", and "SpellId" attributes are mutually exclusive.
//      ItemId  [at least one is REQUIRED if Command is "Update": ItemId, SpellId, ActivityName]
//          Identifies the item to be used when the trigger condition defined in USEWHEN
//          has been met.
//          "ActivityName", "ItemId", and "SpellId" attributes are mutually exclusive.
//      SpellId [at least one is REQUIRED if Command is "Update": ItemId, SpellId, ActivityName]
//          Identifies the spell to be cast when the trigger condition defined in USEWHEN
//          has been met.
//          "ActivityName", "ItemId", and "SpellId" attributes are mutually exclusive.
//      UseAtInterval [UseAtInterval OR UseWhen REQUIRED if Command is "Update"]
//          Defines an interval (in milliseconds) at which the Activity should be conducted.
//          "UseAtInterval" and "UseWhen" attributes are mutually exclusive.
//      UseWhen [UseAtInterval OR UseWhen REQUIRED if Command is "Update"]
//          Defines a predicate that must return a boolean value.  When the predicate
//          evaluates to 'true', the item is used, or spell is cast.  Once the activity
//          succeeds, the predicate must evaluate to 'false'; otherwise, the behavior will
//          flag an error with the predicate.
//          "UseAtInterval" and "UseWhen" attributes are mutually exclusive.
//
// Tunables:
//      AllowUseDuringCombat [optional; Default: false]
//          If true, then using the item or casting the spell is acceptable to try during combat.
//      AllowUseInVehicle [optional; Default: false]
//          If true, then using the item or casting the spell is acceptable to try while in a vehicle.
//      AllowUseWhileFlying [optional; Default: false]
//          If true, then using the item or casting the spell is acceptable to try while flying.
//      AllowUseWhileMounted [optional; Default: false]
//          If true, then using the item or casting the spell is acceptable to try while mounted.
//      Command [optional; ONE OF: Disable, Enable, Remove, ShowActivities, Update; Default: Update]
//          Determines the disposition of the activity that evaluates the item use, spell cast or a custom activity
//          Please see the examples below, and the purpose will become clear.
//      StopMovingToConductActivity [optional; Default: false]
//          Many items and spells do not require the toon to be motionless when performing the actions;
//          however, some do.
//
// THINGS TO KNOW:
// * We refer to the item id (or spell cast, or custom activity) and its trigger condition
//   as an DoWhen 'activity'.
//
// * Once a UseWhen predicate evaluates to 'true', after the associated action is taken
//   (e.g., item is used, or spell is cast), the predicate must immediately return to
//   evaluating as 'false'.  If the predicate remains true after a successful action,
//   then the behavior will flag an error with the predicate.  With bad predicates,
//   erratic Honorbuddy behavior may ensue.
//
// * Attempting to use an Item that is not in the backpack will cause the behavior to
//   emit errors warning of a problem with the profile.  There are two resolutions to
//   this situation:
//     + (Preferred) Remove the corresponding DoWhen in the profile until the item is in the backpack.
//     + Include a "HasItem(XXX) term as part of the UseWhen predicate expression.
//   An isomorphic situation exists when trying to use a spell that is not known.
//
// * Any time the bot is stopped, or a new profile is loaded, DoWhen will clear the list
//   of activities (e.g., item usage, or spell casts).  When the bot is started, the
//   profile will re-load whatever is appropriate.  Similarly, the new profile has a clean
//   slate with which to start working without concern for DoWhen remnants left by
//   a previous profile.
//
// * There is only one activity entry for any given ActivityName, ItemId, or SpellId.
//   If you think you are creating more than one activity for the same Id, you are actually replacing
//   the activity already present, instead.
//
#endregion


#region Examples
// EXAMPLE:
// This trivial example arranges for the toon to drink all the Refreshing Spring Water
// in his backpack:
//      <CustomBehavior File="Hooks\DoWhen" ItemId="159" UseWhen="!Me.HasAura(&quot;Drink&quot;)" />
//
// The problem with the above example is the toon will continue to try to drink the Refreshing Spring
// Water when none remains in the backpack.  The DoWhen will flag this as an error since the item
// is not in the backpack.  To avoid this situation, we must augment the UseWhen term to take the
// supply of Refreshing Spring Water into consideration:
//      <CustomBehavior File="Hooks\DoWhen" ItemId="159" UseWhen="HasItem(159) &amp;&amp; !Me.HasAura(&quot;Drink&quot;)" />
// (Recall that the DoWhen "Command" attribute defaults to "Update".)
//
// A more realistic example... deploys the Lashtail Hatchling when in Northern Stranglethorn Vale:
//      <CustomBehavior File="Hooks\DoWhen" ItemId="58165" UseWhen="(Me.ZoneId == 33) &amp;&amp; !Me.Minions.Any(m => m.Entry == 42736)" />
// (Recall that Command defaults to "Update".)
// Note that our predicate did not check for the existence of the Lashtail Raptor Egg in the backpack.
// Instead, we chose the better solution of configuring the DoWhen only when the profile already knows the egg
// must already be in our backpack.  This helps a profile writer to locate hard-to-find errors.
// 
// If there's a need to perform a custom tailored activity then you can do so by placing 
// any valid quest profile elements inside the <DoWhen> element like so. 
//		<CustomBehavior File="Hooks\DoWhen" UseWhen="HasItem(1234)" ActivityName="UseSomeItem" AllowUseWhileFlying="True" AllowUseWhileMounted="True">
//			<If Condition="Me.Mounted">
//				<CustomBehavior File="ForcedDismount" />
//			</If>
//			<If Condition="Me.Shapeshift != ShapeshiftForm.Normal">
//				<CustomBehavior File="Misc\RunLua" Lua="CancelShapeshiftForm()" /> 
//			</If>
//			<CustomBehavior File="Misc\RunLua" Lua="UseItemByName(1234)" WaitTime="1500" />          
//		</CustomBehavior>
//
// At any time, to see the list of current DoWhen activities, use the "ShowActivities" Command:
//      <CustomBehavior File="Hooks\DoWhen" Command="ShowActivities" />
// Output will be generated to the log that looks like the following:
//      [DoWhen-v$Rev$(info)] DoWhenActivities in use (count:2):
//          SpellId(159)
//              Used when: "Me.GotTarget && (Me.CurrentTarget.Entry == 43034)"
//              Enabled=True
//              AllowUseDuringCombat=False, AllowUseInVehicle=False, AllowUseWhileFlying=False, AllowUseWhileMounted=False
//          SpellId(4540)
//              Used when: "Me.GotTarget && (Me.CurrentTarget.Entry == 22435)"
//              Enabled=True
//              AllowUseDuringCombat=False, AllowUseInVehicle=False, AllowUseWhileFlying=False, AllowUseWhileMounted=False
//
// If you need to temporarily disable an activity:
//     <CustomBehavior File="DoWhen" ItemId="159" Command="Disable" />
// and conversely to restore it:
//     <CustomBehavior File="DoWhen" ItemId="159" Command="Enable" />
//
// And lastly, when you are done with an activity, you should remove it:
//     <CustomBehavior File="DoWhen" ItemId="159" Command="Remove" />
//
#endregion


#region Usings
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Bots.Quest.Actions;
using Bots.Quest.QuestOrder;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Profiles.Quest.Order;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

#endregion


namespace Honorbuddy.Quest_Behaviors.DoWhen
{
	[CustomBehaviorFileName(@"Hooks\DoWhen")]
	public class DoWhen : QuestBehaviorBase
	{
		#region Constructor and Argument Processing
		private enum CommandType
		{
			Disable,
			Enable,
			Remove,
			ShowActivities,
			Update,
		};


		public DoWhen(Dictionary<string, string> args)
			: base(args)
		{
			try
			{
				// NB: Core attributes are parsed by QuestBehaviorBase parent (e.g., QuestId, NonCompeteDistance, etc)

				// Behavior-specific attributes...
				// NB: We must parse the Command first, as this helps determine whether certain attributes are
				// mandator or optional.
				Command = GetAttributeAsNullable<CommandType>("Command", false, null, null) ?? CommandType.Update;

				// Primary attributes...
				ActivityKey_ItemId = GetAttributeAsNullable<int>("ItemId", false, ConstrainAs.ItemId, null) ?? 0;
				ActivityKey_SpellId = GetAttributeAsNullable<int>("SpellId", false, ConstrainAs.SpellId, null) ?? 0;
				ActivityKey_Name = GetAttributeAs<string>("ActivityName", false, ConstrainAs.StringNonEmpty, null) ?? "";

                var useAtInterval = GetAttributeAsNullable<int>("UseAtInterval", false, ConstrainAs.Milliseconds, null) ?? 0;
                UseAtInterval = TimeSpan.FromMilliseconds(useAtInterval);

				// We test compile the "UseWhen" expression to look for problems.
				// If there is a problem, an exception will be thrown (and handled here).
				UseWhenExpression = GetAttributeAs<string>("UseWhen", false, ConstrainAs.StringNonEmpty, null) ?? "false";
				if (CompileAttributePredicateExpression("UseWhen", UseWhenExpression) == null)
				    IsAttributeProblem = true;

				// Tunables...
				AllowUseDuringCombat = GetAttributeAsNullable<bool>("AllowUseDuringCombat", false, null, null) ?? false;
				AllowUseInVehicle = GetAttributeAsNullable<bool>("AllowUseInVehicle", false, null, null) ?? false;
				AllowUseWhileFlying = GetAttributeAsNullable<bool>("AllowUseWhileFlying", false, null, null) ?? false;
				AllowUseWhileMounted = GetAttributeAsNullable<bool>("AllowUseWhileMounted", false, null, null) ?? false;
                StopMovingToConductActivity = GetAttributeAsNullable<bool>("StopMovingToConductActivity", false, null, null) ?? false;

				CfbContextForHook = this;
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
        private int ActivityKey_ItemId { get; set; }
        private string ActivityKey_Name { get; set; }
        private int ActivityKey_SpellId { get; set; }
        private bool AllowUseDuringCombat { get; set; }
		private bool AllowUseInVehicle { get; set; }
		private bool AllowUseWhileFlying { get; set; }
		private bool AllowUseWhileMounted { get; set; }
		private CommandType Command { get; set; }
        private bool StopMovingToConductActivity { get; set; }
        private TimeSpan UseAtInterval { get; set; }
		private string UseWhenExpression { get; set; }

		private static CustomForcedBehavior CfbContextForHook { get; set; }


		protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
		{
			// empty, for now...
		}

		protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
		{
			var activityKeyUsage = (Args.ContainsKey("ItemId") ? 1 : 0)
				+ (Args.ContainsKey("SpellId") ? 1 : 0)
				+ (Args.ContainsKey("ActivityName") ? 1 : 0);

			// Activity Identifier coherency...
			var isIdRequired = (Command != CommandType.ShowActivities);
			UsageCheck_SemanticCoherency(xElement,
				isIdRequired && (activityKeyUsage == 0),
				context => "ItemId, SpellId, or ActivityName attribute is required.");

            UsageCheck_SemanticCoherency(xElement,
				isIdRequired && (activityKeyUsage > 1),
				context => "ItemId, SpellId, and ActivityName attributes are mutually exclusive.");

			// If Id is not allowed, then neither ItemId, SpellId nor ActivityName can be specified...
			var isIdAllowed = (Command != CommandType.ShowActivities);
			UsageCheck_SemanticCoherency(xElement,
                !isIdAllowed && (activityKeyUsage > 0),
				context => "ItemId, SpellId, and ActivityName attributes are not allowed when Command is \"ShowActivities\".");


            // Predicate coherency...
            var predicateUsage = (Args.ContainsKey("UseAtInterval") ? 1 : 0)
                                 + (Args.ContainsKey("UseWhen") ? 1 : 0);

            var isPredicateRequired = (Command == CommandType.Update);
            UsageCheck_SemanticCoherency(xElement,
                isPredicateRequired && (predicateUsage == 0),
                context => "UseAtInterval or UseWhen attribute is required.");

            UsageCheck_SemanticCoherency(xElement,
                isPredicateRequired && (predicateUsage > 1),
                context => "UseAtInterval or UseWhen attributes are mutually exclusive.");

		    var isPredicateAllowed = (Command == CommandType.Update);
            UsageCheck_SemanticCoherency(xElement,
                !isPredicateAllowed && (predicateUsage > 0),
                context => string.Format("UseAtInterval and UseWhen attributes are only allowed if Command is \"Update\"."));


			// Tunable coherency...
			var isAllowUseAllowed = (Command == CommandType.Update);
			UsageCheck_SemanticCoherency(xElement,
				Args.ContainsKey("AllowUseDuringCombat") && !isAllowUseAllowed,
				context => "AllowUseDuringCombat attribute is only allowed when Command is \"Update\".");

			UsageCheck_SemanticCoherency(xElement,
				Args.ContainsKey("AllowUseInVehicle") && !isAllowUseAllowed,
				context => "AllowUseInVehicle attribute is only allowed when Command is \"Update\".");

			UsageCheck_SemanticCoherency(xElement,
				Args.ContainsKey("AllowUseWhileFlying") && !isAllowUseAllowed,
				context => "AllowUseWhileFlying attribute is only allowed when Command is \"Update\".");

			UsageCheck_SemanticCoherency(xElement,
				Args.ContainsKey("AllowUseWhileMounted") && !isAllowUseAllowed,
				context => "AllowUseWhileMounted attribute is only allowed when Command is \"Update\".");

            UsageCheck_SemanticCoherency(xElement,
				Args.ContainsKey("StopMovingToConductActivity") && !isAllowUseAllowed,
				context => "StopMovingToConductActivity attribute is only allowed when Command is \"Update\".");
		}
		#endregion


		#region Private and Convenience variables
		private readonly TimeSpan _activityThrottleDelay = TimeSpan.FromMilliseconds(750);
		private static Composite _persistedDoWhenHookBehavior = null;
		private static readonly List<IDoWhenActivity> PersistedActivities = new List<IDoWhenActivity>();
		private static WaitTimer _persistedActivityThrottle = null;
		private static bool _persistedIsOnBotStopHooked = false;
		private static bool _persistedIsOnNewProfileLoadedHooked = false;
		#endregion


		#region Overrides of CustomForcedBehavior
		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return "$Id$"; } }
		public override string SubversionRevision { get { return "$Rev$"; } }


		// CreateBehavior supplied by QuestBehaviorBase.
		// Instead, provide CreateMainBehavior definition.

		// Dispose provided by QuestBehaviorBase.

		// IsDone provided by QuestBehaviorBase.
		// Call the QuestBehaviorBase.BehaviorDone() method when you want to indicate your behavior is complete.

        // OnFinished provided by QuestBehaviorBase.

		public override void OnStart()
		{
			// Acquisition and checking of any sub-elements go here.
			// A common example:
			//     HuntingGrounds = HuntingGroundsType.GetOrCreate(Element, "HuntingGrounds", HuntingGroundCenter);
			//     IsAttributeProblem |= HuntingGrounds.IsAttributeProblem;

			// Let QuestBehaviorBase do basic initialization of the behavior, deal with bad or deprecated attributes,
			// capture configuration state, install BT hooks, etc.  This will also update the goal text.
			var isBehaviorShouldRun = OnStart_QuestBehaviorCore();

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (isBehaviorShouldRun)
			{
				// The BotStop handler will remove the "use when" activities...
				// Note, we only want to hook BotStopped once for this behavior.
				if (!_persistedIsOnBotStopHooked)
				{
					BotEvents.OnBotStopped += BotEvents_OnBotStopped;
					_persistedIsOnBotStopHooked = true;
				}

				if (!_persistedIsOnNewProfileLoadedHooked)
				{
					BotEvents.Profile.OnNewProfileLoaded += BotEvents_OnNewProfileLoaded;
					_persistedIsOnNewProfileLoadedHooked = true;
				}

				switch (Command)
				{
					case CommandType.Disable:
						ActionSetEnableState(false);
						break;

					case CommandType.Enable:
						ActionSetEnableState(true);
						break;

					case CommandType.ShowActivities:
						ActionShowActivities();
						break;

					case CommandType.Remove:
						ActionRemove();
						break;

					case CommandType.Update:
				        IUseWhenPredicate useWhenPredicate =
                            (UseAtInterval > TimeSpan.Zero)
                            ? (IUseWhenPredicate)new UseWhenPredicate_TimeElapse(UseAtInterval,
                                                                                AllowUseDuringCombat,
                                                                                AllowUseInVehicle,
                                                                                AllowUseWhileFlying,
                                                                                AllowUseWhileMounted)
                            : (IUseWhenPredicate)new UseWhenPredicate_FuncEval(UseWhenExpression,
                                                                                AllowUseDuringCombat,
                                                                                AllowUseInVehicle,
                                                                                AllowUseWhileFlying,
                                                                                AllowUseWhileMounted);

						ActionUpdate(useWhenPredicate, StopMovingToConductActivity);
						break;

					default:
						QBCLog.MaintenanceError("Unhandled action type of '{0}'.", Command);
						TreeRoot.Stop();
						return;
						break;
				}

				// Install or remove behavior as needed...
				// We need to install the hook when its not present, AND there is something to execute.
				// We remove the hook if there is nothing left to execute.  This betters the user's experience,
				// by maximizing performance.
				// NB: We cannot simply override the methods provided by QuestBehaviorBase, because this behavior
				// is unusual.  We want these hooks to remain after this behavior terminates.  If we use the
				// QuestBehaviorBase-provide facilities (e.g., override the methods), then the hooks would be
				// cleaned up (e.g., removed) when this behavior exits.
				if (PersistedActivities.Count > 0)
				    DoWhenHookInstall();

				if (PersistedActivities.Count <= 0)
				    DoWhenHookRemove();

				BehaviorDone();
			}
		}
		#endregion


		#region Main Behaviors
		private Composite CreateDoWhenHook()
		{
			return new ActionRunCoroutine(context => MainCoroutine(context));
		}


		private async Task<bool> MainCoroutine(object context)
		{
			// Ignore, while in non-actionable condition...
			if (Me.IsDead)
			    return false;

			// Ignore if eating or drinking...
			if (IsDrinkingOrEating())
			    return false;

			// If throttle is not running, get it started...
			if (_persistedActivityThrottle == null)
			{
				_persistedActivityThrottle = new WaitTimer(_activityThrottleDelay);
				_persistedActivityThrottle.Reset();
			}

			// If throttling in progress, return immediately...
			if (!_persistedActivityThrottle.IsFinished)
			    return false;

			// Process each of the activites in play...
			var oldLoggingContext = QBCLog.BehaviorLoggingContext;
			QBCLog.BehaviorLoggingContext = this;
			try
			{
				if (await ExecuteActivities())
					return true;
			}
			finally
			{
				// ensure the QBCLog.BehaviorLoggingContext is reverted back to it's previous value in the event an exception was thown
				QBCLog.BehaviorLoggingContext = oldLoggingContext;
			}

			// Done for this visit...
			_persistedActivityThrottle.Reset();
			return false;
		}

		async Task<bool> ExecuteActivities()
		{
			foreach (var activity in PersistedActivities)
			{
				if (await activity.Execute())
					return true;
			}
			return false;
		}

		#endregion


		#region Helpers
		private void ActionSetEnableState(bool wantEnabled)
		{
			var activityIdentifier = FindActivityIdentifier();
			var activity = FindActivity(activityIdentifier);

			// If activity does not exist, that's a problem...
			if (activity == null)
			{
				QBCLog.Error("DoWhenActivity '{0}' is not in use.", activityIdentifier);
				return;
			}

			// Update activity's enabled status...
			var previousState = activity.UseWhenPredicate.IsEnabled;
			activity.UseWhenPredicate.IsEnabled = wantEnabled;
			QBCLog.DeveloperInfo("DoWhenActivity '{0}' {1} (was {2}).",
				activity.ActivityIdentifier,
				(wantEnabled ? "enabled" : "disabled"),
				(previousState ? "enabled" : "disabled"));
		}


		private void ActionRemove()
		{
			var activityIdentifier = FindActivityIdentifier();
			var activity = FindActivity(activityIdentifier);

			// If activity does not exist, that's a problem...
			if (activity == null)
			{
				QBCLog.Error("DoWhenActivity '{0}' is not in use.", activityIdentifier);
				return;
			}

			// Remove the activity...
			PersistedActivities.Remove(activity);
			QBCLog.DeveloperInfo("DoWhenActivity '{0}' removed.", activity.ActivityIdentifier);
		}


		private void ActionShowActivities()
		{
			var builder = new StringBuilder();

			if (PersistedActivities.Count <= 0)
			    builder.AppendFormat("No DoWhenActivities in use.");

			else
			{
				builder.AppendFormat("DoWhenActivities in use (count:{0}):", PersistedActivities.Count);

				foreach (var activity in PersistedActivities.OrderBy(e => e.ActivityIdentifier))
				    builder.Append(activity.BuildDebugInfo("    "));
			}

			QBCLog.Info(builder.ToString());
		}


		private void ActionUpdate(IUseWhenPredicate useWhenPredicate, bool isMovementStopRequired)
		{
			var activityIdentifier = FindActivityIdentifier();
			var existingActivity = FindActivity(activityIdentifier);

			// If activity already exists, remove it...
			if (existingActivity != null)
			    PersistedActivities.Remove(existingActivity);

			// Install new activity...
			IDoWhenActivity doWhenActivity = null;
			if (ActivityKey_ItemId > 0)
			    doWhenActivity = new DoWhenUseItemActivity(ActivityKey_ItemId, useWhenPredicate, isMovementStopRequired);

			else if (ActivityKey_SpellId > 0)
			    doWhenActivity = new DoWhenCastSpellActivity(ActivityKey_SpellId, useWhenPredicate, isMovementStopRequired);

			else if (!string.IsNullOrEmpty(ActivityKey_Name))
                doWhenActivity = new DoWhenNamedActivity(ActivityKey_Name, useWhenPredicate, Element, isMovementStopRequired);

			if (doWhenActivity != null)
			{
				PersistedActivities.Add(doWhenActivity);
				QBCLog.DeveloperInfo("DoWhenActivity '{0}' created:{1}",
					doWhenActivity.ActivityIdentifier,
					doWhenActivity.BuildDebugInfo("    "));
			}
		}


		private void BotEvents_OnBotStopped(EventArgs args)
		{
			QBCLog.DeveloperInfo(CfbContextForHook, "OnBotStop cleanup...");
			// Uninstall the behavior from the tree...
			DoWhenHookRemove();

			// Remove our OnNewProfileLoaded handler....
			BotEvents.Profile.OnNewProfileLoaded -= BotEvents_OnNewProfileLoaded;
			_persistedIsOnNewProfileLoadedHooked = false;

			// Remove our OnBotStop handler
			BotEvents.OnBotStopped -= BotEvents_OnBotStopped;
			_persistedIsOnBotStopHooked = false;
		}


		private void BotEvents_OnNewProfileLoaded(EventArgs args)
		{
			QBCLog.DeveloperInfo(CfbContextForHook, "OnNewProfileLoaded cleanup...");
			// Uninstall the behavior from the tree...
			DoWhenHookRemove();
		}


		private void DoWhenHookInstall()
		{
			if (_persistedDoWhenHookBehavior == null)
			    _persistedDoWhenHookBehavior = BehaviorHookInstall("Questbot_Main", CreateDoWhenHook());
		}


		private void DoWhenHookRemove()
		{
		    if (_persistedDoWhenHookBehavior == null)
		        return;

			// Log the item activity that are being removed...
			var builder = new StringBuilder();

			if (PersistedActivities.Count <= 0)
			    builder.AppendFormat("DoWhen hook removed--no DoWhenActivities to clean up.");

			else
			{
				builder.AppendFormat("Removing DoWhenActivities (count:{0}):", PersistedActivities.Count);

				// Show each activity to be removed...
				foreach (var activity in PersistedActivities.OrderBy(e => e.ActivityIdentifier).ToList())
				    builder.Append(activity.BuildDebugInfo("    "));
			}

			QBCLog.DeveloperInfo(CfbContextForHook, builder.ToString());
			PersistedActivities.Clear();

			BehaviorHookRemove("Questbot_Main", ref _persistedDoWhenHookBehavior);
			_persistedDoWhenHookBehavior = null;
			_persistedActivityThrottle = null;
		}


		private IDoWhenActivity FindActivity(string activityIdentifierToFind)
		{
			return PersistedActivities.FirstOrDefault(e => e.ActivityIdentifier == activityIdentifierToFind);
		}


		private string FindActivityIdentifier()
		{
			if (ActivityKey_ItemId > 0)
			    return DoWhenUseItemActivity.CreateActivityIdentifier(ActivityKey_ItemId);

			if (ActivityKey_SpellId > 0)
			    return DoWhenCastSpellActivity.CreateActivityIdentifier(ActivityKey_SpellId);

            if (!string.IsNullOrEmpty(ActivityKey_Name))
                return DoWhenNamedActivity.CreateActivityIdentifier(ActivityKey_Name);

            QBCLog.MaintenanceError("Unable to find ActivityIdentifier for ItemId, SpellId, or ActivityName--none were specified.");
			return string.Empty;
		}


		private bool IsDrinkingOrEating()
		{
			// NB: We must look up auras "by name" here, instead of "by spell id"...
			// The spell id of a "drinking aura" changes with the drink's level.  However,
			// the name remains the same--"Drink".  Ditto for "Food" auras.
			return Me.GetAllAuras().Any(aura => (aura.Name == "Drink") || (aura.Name == "Food"));
		}
		#endregion


		#region Helper Classes - IDoWhenActivity
		private abstract class IDoWhenActivity
		{
            protected enum ActivityResult
            {
                Failed,
                Indeterminate,
                Succeeded,
            }

            protected IDoWhenActivity(string activityIdentifier,
                                      IUseWhenPredicate useWhenPredicate,
                                      bool isMovementStopRequired)
			{
			    Contract.Requires(!string.IsNullOrEmpty(activityIdentifier), (context) => "activityIdentifier may not be null or empty");
			    Contract.Requires(useWhenPredicate != null, (context) => "useWhenPredicate != null");

				ActivityIdentifier = activityIdentifier;
			    IsMovementStopRequired = isMovementStopRequired;
                UseWhenPredicate = useWhenPredicate;
			}

            public string ActivityIdentifier { get; private set; }
            public IUseWhenPredicate UseWhenPredicate { get; private set; }
            public bool IsMovementStopRequired { get; private set; }


			// Utility methods...
			public string BuildDebugInfo(string linePrefix)
			{
				var builder = new StringBuilder();

				builder.AppendFormat("{0}{1}{2}", Environment.NewLine, linePrefix, ActivityIdentifier);
				builder.AppendFormat("{0}{1}    Used when: \"{2}\"",
					Environment.NewLine,
					linePrefix,
					UseWhenPredicate.ExpressionAsString);
                builder.AppendFormat("{0}{1}    Enabled={2}", Environment.NewLine, linePrefix, UseWhenPredicate.IsEnabled);
				builder.AppendFormat("{0}{1}    AllowUseDuringCombat={2}", Environment.NewLine, linePrefix, UseWhenPredicate.AllowUseDuringCombat);
                builder.AppendFormat(", AllowUseInVehicle={0}", UseWhenPredicate.AllowUseInVehicle);
                builder.AppendFormat(", AllowUseWhileFlying={0}", UseWhenPredicate.AllowUseWhileFlying);
                builder.AppendFormat(", AllowUseWhileMounted={0}", UseWhenPredicate.AllowUseWhileMounted);

				return builder.ToString();
			}


            public async Task<bool> Execute()
            {
                if (!IsSpecificExecutionNeeded())
                    return false;

                if (IsMovementStopRequired && Me.IsMoving)
                    await UtilityCoroutine.MoveStop();

                var activityResult = await ExecuteSpecificActivity();
                if (activityResult == ActivityResult.Indeterminate)
                    return true;
                if (activityResult == ActivityResult.Failed)
                    return false;

                // If we get here, we got a ActivityResult.Succeeded
                UseWhenPredicate.Reset();

                // If predicate did not clear, then predicate is bad...
                // We allow for a server round-trip time to allow enough time for a potential
                // aura to be applied.
                await CommonCoroutines.SleepForLagDuration();
                if (UseWhenPredicate.IsReady())
                {
                    QBCLog.Error(
                        "For DoWhenActivity {1}, predicate ({2}) was not reset by execution.{0}"
                        + "  This is a profile problem, and can result in erratic Honorbuddy behavior.{0}"
                        + "  The predicate must return to 'false' after the action has been successfully executed.",
                        Environment.NewLine, ActivityIdentifier, UseWhenPredicate.ExpressionAsString);
                }

                return true;
            }

            // Methods requiring override...
            protected abstract Task<ActivityResult> ExecuteSpecificActivity();

            protected virtual bool IsSpecificExecutionNeeded()
            {
                return UseWhenPredicate.IsReady();
            }
		}


		/// <summary>
		/// Subclass of DoWhenActivity that knows how to cast spells...
		/// </summary>
		private class DoWhenCastSpellActivity : IDoWhenActivity
		{
			public DoWhenCastSpellActivity(int spellId, IUseWhenPredicate useWhenPredicate, bool isMovementStopRequired)
				: base(CreateActivityIdentifier(spellId), useWhenPredicate, isMovementStopRequired)
			{
				Contract.Requires(spellId > 0, context => "spellId > 0");

                _errorMessage_UnknownSpell = string.Format(
                        "For DoWhenActivity {1}, SpellId({2}) is not known.{0}"
                        + "  This is a profile problem.  To squelch this message there are two options:{0}"
                        + "  * (Preferred) Remove the corresponding DoWhen in the profile until the spell is learned.{0}"
                        + "  * Include a \"HasSpell({2})\" term as part of the UseWhen predicate expression.",
                        Environment.NewLine, ActivityIdentifier, spellId);

                _wowSpell = WoWSpell.FromId(spellId);
			}

		    private readonly string _errorMessage_UnknownSpell;
		    private readonly WoWSpell _wowSpell;


			public static string CreateActivityIdentifier(int spellId)
			{
				// Note: we can't use Utility.GetSpellNameFromId() for this, because its value
				// will change based on whether or not the spell is known or not.
				return string.Format("SpellId({0})", spellId);
			}


            protected override async Task<ActivityResult> ExecuteSpecificActivity()
			{
				// If spell is not known at the moment, this is a problem...
                if ((_wowSpell == null) || !_wowSpell.IsValid)
                {
                    QBCLog.Error(_errorMessage_UnknownSpell);
                    return ActivityResult.Failed;
				}

                return
                    await UtilityCoroutine.CastSpell(_wowSpell.Id) == SpellCastResult.Succeeded
                        ? ActivityResult.Succeeded
                        : ActivityResult.Failed;
			}
		}


		/// <summary>
		/// Subclass of DoWhenActivity that knows how to use items...
		/// </summary>
		private class DoWhenUseItemActivity : IDoWhenActivity
		{
			public DoWhenUseItemActivity(int itemId, IUseWhenPredicate useWhenPredicate, bool isMovementStopRequired)
				: base(CreateActivityIdentifier(itemId), useWhenPredicate, isMovementStopRequired)
			{
				Contract.Requires(itemId > 0, context => "itemId > 0");

                _errorMessage_ItemNotInInventory = string.Format(
						"For DoWhenActivity {1}, ItemId({2}) is not in our inventory.{0}"
						+ "  This is a profile problem.  To squelch this message there are two options:{0}"
						+ "  * (Preferred) Remove the corresponding DoWhen in the profile until the item is in our backpack.{0}"
						+ "  * Include a \"HasItem({2})\" term as part of the UseWhen predicate expression.",
                        Environment.NewLine, ActivityIdentifier, itemId);

                _itemToUse = Me.CarriedItems.FirstOrDefault(i => (i.Entry == itemId));
			}

		    private readonly string _errorMessage_ItemNotInInventory;
		    private readonly WoWItem _itemToUse;


			public static string CreateActivityIdentifier(int itemId)
			{
				// Note: we can't use Utility.FindItemNameFromId() for this, because its value
				// will change based on whether or not the item is in our inventory currently.
				return string.Format("ItemId({0})", itemId);
			}


            protected override async Task<ActivityResult> ExecuteSpecificActivity()
			{
				// If item is not in inventory at the moment, we consider that a problem...
                if (!Query.IsViable(_itemToUse))
				{
                    QBCLog.Error(_errorMessage_ItemNotInInventory);
                    return ActivityResult.Failed;
				}

                var activityResult = ActivityResult.Failed;
				await UtilityCoroutine.UseItem((int)_itemToUse.Entry,
                                                null, /*missing item is non-fatal*/
                                                null, /*notification on fail*/
                                                () => { activityResult = ActivityResult.Succeeded; });
                return activityResult;
			}
		}

		/// <summary>
		/// Subclass of DoWhenActivity that knows how to use items...
		/// </summary>
		private class DoWhenNamedActivity : IDoWhenActivity
		{
            public DoWhenNamedActivity(string customActivityName,
                                        IUseWhenPredicate useWhenPredicate, 
									    XElement element,
                                        bool isMovementStopRequired)
                : base(CreateActivityIdentifier(customActivityName), useWhenPredicate, isMovementStopRequired)
			{
                Contract.Requires(!string.IsNullOrEmpty(customActivityName), context => "!string.IsNullOrEmpty(customActivityName)");
				Contract.Requires(element != null, context => "element != null");

				Element = element;
			}

            private XElement Element { get; set; }
			private ForcedBehaviorExecutor BehaviorExecutor { get; set; }


            public static string CreateActivityIdentifier(string customActivityName)
            {
                return string.Format("ActivityName({0})", customActivityName);
            }


            protected override async Task<ActivityResult> ExecuteSpecificActivity()
			{
                if (BehaviorExecutor == null)
				{
					var questOrder = new QuestOrder(OrderNodeCollection.FromXml(Element));
					questOrder.UpdateNodes();
					BehaviorExecutor = new ForcedBehaviorExecutor(questOrder);
				}

				if (BehaviorExecutor.Order.Nodes.Any())
				{
					await BehaviorExecutor.ExecuteCoroutine();
					// return now if we have any nodes left to execute
					if (BehaviorExecutor.Order.Nodes.Any())
                        return ActivityResult.Indeterminate;
				}

				BehaviorExecutor = null;
                return ActivityResult.Succeeded;
			}

            protected override bool IsSpecificExecutionNeeded()
            {
                // UseWhenPredicate.IsReady() might return 'false' after we started executing this activity.
                // If this happens, we want to continue executing until this activity is complete.
                // BehaviorExecutor will not be null when the activity is executing.
                return UseWhenPredicate.IsReady() || (BehaviorExecutor != null);
            }
		}
		#endregion

        #region Helper Classes - IUseWhenPredicate
        private abstract class IUseWhenPredicate
        {
            protected IUseWhenPredicate(string useWhenExpression,
                                    bool allowUseDuringCombat,
                                    bool allowUseInVehicle,
                                    bool allowUseWhileFlying,
                                    bool allowUseWhileMounted)
			{
				AllowUseDuringCombat = allowUseDuringCombat;
				AllowUseInVehicle = allowUseInVehicle;
				AllowUseWhileFlying = allowUseWhileFlying;
				AllowUseWhileMounted = allowUseWhileMounted;
				IsEnabled = true;

                // We keep the string representation of the expression for use in logging messages, etc...
                ExpressionAsString = useWhenExpression;
			}

            public readonly bool AllowUseDuringCombat;
            public readonly bool AllowUseInVehicle;
            public readonly bool AllowUseWhileFlying;
            public readonly bool AllowUseWhileMounted;
            public readonly string ExpressionAsString;
            public bool IsEnabled { get; set; }

            public abstract bool IsReady();
            public abstract void Reset();

            protected bool IsReady_Common()
            {
                return
                    IsEnabled
                    && (AllowUseDuringCombat || !Me.Combat)
                    && (AllowUseInVehicle || !Query.IsInVehicle())
                    && (AllowUseWhileFlying || !Me.IsFlying)
                    && (AllowUseWhileMounted || !Me.IsMounted());
            }
        }

        private class UseWhenPredicate_FuncEval : IUseWhenPredicate
        {
            public UseWhenPredicate_FuncEval(string useWhenExpression,
                                                bool allowUseDuringCombat,
                                                bool allowUseInVehicle,
                                                bool allowUseWhileFlying,
                                                bool allowUseWhileMounted)
                :base(useWhenExpression,
                      allowUseDuringCombat, allowUseInVehicle, allowUseWhileFlying, allowUseWhileMounted)
            {
                // We keep the string representation of the expression for use in logging messages, etc...
                _predicate = CompileAttributePredicateExpression("UseWhen", useWhenExpression);
                if (_predicate == null)
                    throw new ArgumentException("Predicate ({0}) expression doesn't compile.", useWhenExpression);
            }

            private readonly Func<bool> _predicate; 

            public override bool IsReady()
            {
                return IsReady_Common() && _predicate();
            }

            public override void Reset()
            {
                // empty on purpose--predicate should be written to reset when action is taken
            }
        }

        private class UseWhenPredicate_TimeElapse : IUseWhenPredicate
        {
            public UseWhenPredicate_TimeElapse(TimeSpan delayInterval,
                                                bool allowUseDuringCombat,
                                                bool allowUseInVehicle,
                                                bool allowUseWhileFlying,
                                                bool allowUseWhileMounted)
                :base(string.Format("TimeSpan({0})", delayInterval.ToString()),
                      allowUseDuringCombat, allowUseInVehicle, allowUseWhileFlying, allowUseWhileMounted)
            {
                _delayInterval = delayInterval;
            }

            private readonly TimeSpan _delayInterval;
            private readonly Stopwatch _timer = new Stopwatch();

            public override bool IsReady()
            {
                // Boundary condition...
                // We want the predicate to be 'is ready' immediately after timer creation.
                // So, we don't do an elapsed time check if the timer is not running.  (The
                // timer is not running until Reset() is called.)
                // When the caller calls Reset(), the timer will start running, and we'll
                // start taking the elapsed time into consideration.
                return
                    _timer.IsRunning
                    ? (IsReady_Common() && (_timer.Elapsed > _delayInterval))
                    : IsReady_Common();
            }

            public override void Reset()
            {
                _timer.Restart();
            }
        }
        #endregion
    }
}