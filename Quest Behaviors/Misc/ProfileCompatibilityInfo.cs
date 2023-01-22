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
// The sole purpose of this behavior is to report compatibility issues when the
// user attempts to use the Honorbuddy for questing.  The addon does checks
// for the following:
//    * Looks for incompatible WoWclient addons, and reports them as errors
//    * Looks for incompatible Honorbuddy plugsin, and reports them as errors
//    * Makes certain the questing bot isn't running as MixedMode
//    * Makes certain WoWclient auto-loot is turned on
//    * Makes certain the WoWclient is running in Windowed mode
//    * Makes certain the WoWclient FPS is suitable for questing
//    * Makes certain the WoWclient Latency is suitable for questing
//    * etc.
// If the addon encounters any of these situations, it emits error messages and
// terminates the profile.
//
// BEHAVIOR ATTRIBUTES:
//      AllowBrokenAddons [optional; Default: false]
//          When set to true and incompatible addons are encountered,
//          the addon errors are converted to warnings, and the profile
//          will not be terminated due to this condition.
//      AllowBrokenPlugins [optional; Default: false]
//          When set to true and incompatible plugins are encountered,
//          the plugin errors are converted to warnings, and the profile
//          will not be terminated due to this condition.
//      AllowMixedModeBot [optional; Default: false]
//          When set to true and the user has selected Mixed Mode,
//          the MixedMode error is converted to warnings, and the profile
//          will not be terminated due to this condition.
//
// THINGS TO KNOW:
//
#endregion


#region Examples
// EXAMPLE:
//     <CustomBehavior File="ProfileCompatibilityInfo" />
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Plugins;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
#endregion


namespace Honorbuddy.Quest_Behaviors.ProfileCompatibilityInfo
{
	[CustomBehaviorFileName(@"Misc\ProfileCompatibilityInfo")]
	public class ProfileCompatibilityInfo : CustomForcedBehavior
	{
		#region Constructor and Argument Processing
		public ProfileCompatibilityInfo(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// NB: Core attributes are parsed by QuestBehaviorBase parent (e.g., QuestId, NonCompeteDistance, etc)

				// Behavior-specific attributes...
				AllowBrokenAddOns = GetAttributeAsNullable<bool>("AllowBrokenAddOns", false, null, null) ?? false;
				AllowBrokenPlugIns = GetAttributeAsNullable<bool>("AllowBrokenPlugIns", false, null, null) ?? false;
				AllowMixedModeBot = GetAttributeAsNullable<bool>("AllowMixedModeBot", false, null, null) ?? false;
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
		private bool AllowBrokenAddOns { get; set; }
		private bool AllowBrokenPlugIns { get; set; }
		private bool AllowMixedModeBot { get; set; }
		#endregion


		#region Private and Convenience variables
		private static readonly string[] KnownHonorbuddyShippedPluginNames =
			{
				"Anti Drown",
				"AutoEquip2",
				"BuddyMonitor",
				"DrinkPotions",
				"PartyBot Leader",
				"Questhelper - ItemForAura",
				"Refreshment Detection",
				"Talented2",
			};


		// Match is case-insensitive StartsWith.
		// NB: *Please* keep alphabetized.  Any dependent addons will also
		// be ignored if the base addon is disabled.
		private static readonly string[] KnownProblemGameClientAddOnNames =
			{
				// Replaces UI components & events...
				"Bartender4",           // http://www.curse.com/addons/wow/bartender4/
				"ElvUI",                // http://www.tukui.org/dl.php
				"Tukui",                // http://www.tukui.org/dl.php
				"Zygor Guides Viewer",  // http://www.zygorguides.com/

				// Deploys Minipets...
				"CollectMe",            // http://www.curse.com/addons/wow/collect_me
				"Critter Caller",       // http://www.curse.com/addons/wow/crittercaller
				"GoGoPet",              // http://www.curse.com/addons/wow/gogopet
				"Kennel",               // http://www.curse.com/addons/wow/kennel
				"Menagerie",            // http://www.curse.com/addons/wow/menagerie
				"PermanentPet",         // http://www.curse.com/addons/wow/permanentpet
				"PetLeash",             // http://www.curse.com/addons/wow/petleash
				"Tiffy's Pet Summoner", // http://www.curse.com/addons/wow/tiffys-pet-summoner
			};

		// Match is case-insensitive StartsWith.
		// NB: *Please* keep alphabetized
		private static readonly string[] KnownProblemPlugInNames =
			{
				// empty, for now
			};

		private static LocalPlayer Me { get { return StyxWoW.Me; } }
		private const int MaximumLatencyWarningThreshold = 850;
		private const int MinimumFpsWarningThreshold = 7;

		private static bool _isBotStopHooked; 


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


		public override bool IsDone
		{
			get { return true; }
		}


		public override void OnStart()
		{
			QBCLog.BehaviorLoggingContext = this;

			var isFatalErrorsEncountered = EmitStateInfo();

			if (isFatalErrorsEncountered)
				{ TreeRoot.Stop("Game client state is incompatible with profile.  Please repair errors."); }

			// Install bot stop handler only once...
			if (!_isBotStopHooked)
			{
				BotEvents.OnBotStopped += BotEvents_OnBotStopped;
				_isBotStopHooked = true;
			}
		}
		#endregion


		#region Helpers
		private void BotEvents_OnBotStopped(EventArgs args)
		{
			EmitStateInfo();

			// Unhook the bot stop handler...
			BotEvents.OnBotStopped -= BotEvents_OnBotStopped;
			_isBotStopHooked = false;
		}


		private void BuildBagList(StringBuilder builder, string linePrefix)
		{
			int freeSlotsNormal = 0;
			int freeSlotsSpeciality = 0;
			int totalSlots = 0;
			StringBuilder tmpBuilder = new StringBuilder();

			Action<WoWContainer> buildBagInfo =
				(wowContainer) =>
				{
					if ((wowContainer == null) || !wowContainer.IsValid)
					{
						tmpBuilder.AppendFormat("{0}    NO BAG", linePrefix);
						tmpBuilder.Append(Environment.NewLine);
						return;
					}

					tmpBuilder.AppendFormat("{0}    {1} [{2}.  {3} free / {4} slots] (http://wowhead.com/item={5})",
						linePrefix,
                        wowContainer.SafeName,
						wowContainer.BagType,
						wowContainer.FreeSlots,
						wowContainer.Slots,
						wowContainer.Entry);
					tmpBuilder.Append(Environment.NewLine);

					if (wowContainer.BagType == BagType.NormalBag)
						{ freeSlotsNormal += (int)wowContainer.FreeSlots; }
					else
						{ freeSlotsSpeciality += (int)wowContainer.FreeSlots; }
					totalSlots += (int)wowContainer.Slots;
				};

			// Backpack requires special handling, grrrr...
			var backpack = Me.Inventory.Backpack;
			tmpBuilder.AppendFormat("{0}    Backpack [{1}: {2} free / {3} slots]",
						linePrefix,
						BagType.NormalBag,
						backpack.FreeSlots,
						backpack.Slots);
			tmpBuilder.Append(Environment.NewLine);

			freeSlotsNormal += (int)backpack.FreeSlots;
			totalSlots += (int)backpack.Slots;

			// Non-backpack bags...
			buildBagInfo(Me.GetBag(WoWBagSlot.Bag1));
			buildBagInfo(Me.GetBag(WoWBagSlot.Bag2));
			buildBagInfo(Me.GetBag(WoWBagSlot.Bag3));
			buildBagInfo(Me.GetBag(WoWBagSlot.Bag4));

			builder.AppendFormat("{0}Bags [TOTALS: ({1} normal + {2} speciality) = {3} free / {4} total slots]:",
				linePrefix,
				freeSlotsNormal,
				freeSlotsSpeciality,
				(freeSlotsNormal + freeSlotsSpeciality),
				totalSlots);
			builder.Append(Environment.NewLine);
			builder.Append(tmpBuilder);
		}


		private void BuildEquipmentList(StringBuilder builder, string linePrefix)
		{
			Func<WoWItem, string>   buildDescription =
				(wowItem) =>
				{
					if ((wowItem != null) && wowItem.IsValid)
					{
						return string.Format("{0} (http://wowhead.com/item={1}){2}",
                            wowItem.SafeName,
							wowItem.Entry,
							(Query.IsQuestItem(wowItem) ? " ***QUEST ITEM***" : ""));
					}
					return "";
				};
			var paperDoll = Me.Inventory.Equipped;

			builder.AppendFormat("{0}Equipped Items:", linePrefix);
			builder.Append(Environment.NewLine);

			builder.AppendFormat("{0}        Head: {1}", linePrefix, buildDescription(paperDoll.Head));
			builder.Append(Environment.NewLine);
			builder.AppendFormat("{0}        Neck: {1}", linePrefix, buildDescription(paperDoll.Neck));
			builder.Append(Environment.NewLine);
			builder.AppendFormat("{0}    Shoulder: {1}", linePrefix, buildDescription(paperDoll.Shoulder));
			builder.Append(Environment.NewLine);
			builder.AppendFormat("{0}        Back: {1}", linePrefix, buildDescription(paperDoll.Back));
			builder.Append(Environment.NewLine);
			builder.AppendFormat("{0}       Chest: {1}", linePrefix, buildDescription(paperDoll.Chest));
			builder.Append(Environment.NewLine);
			builder.AppendFormat("{0}       Shirt: {1}", linePrefix, buildDescription(paperDoll.Shirt));
			builder.Append(Environment.NewLine);
			builder.AppendFormat("{0}      Tabard: {1}", linePrefix, buildDescription(paperDoll.Tabard));
			builder.Append(Environment.NewLine);
			builder.AppendFormat("{0}       Wrist: {1}", linePrefix, buildDescription(paperDoll.Wrist));
			builder.Append(Environment.NewLine);
			builder.AppendFormat("{0}       Hands: {1}", linePrefix, buildDescription(paperDoll.Hands));
			builder.Append(Environment.NewLine);
			builder.AppendFormat("{0}     Finger1: {1}", linePrefix, buildDescription(paperDoll.Finger1));
			builder.Append(Environment.NewLine);
			builder.AppendFormat("{0}     Finger2: {1}", linePrefix, buildDescription(paperDoll.Finger2));
			builder.Append(Environment.NewLine);
			builder.Append(Environment.NewLine);

			builder.AppendFormat("{0}    MainHand: {1}", linePrefix, buildDescription(paperDoll.MainHand));
			builder.Append(Environment.NewLine);
			builder.AppendFormat("{0}     OffHand: {1}", linePrefix, buildDescription(paperDoll.OffHand));
			builder.Append(Environment.NewLine);
			builder.AppendFormat("{0}      Ranged: {1}", linePrefix, buildDescription(paperDoll.Ranged));
			builder.Append(Environment.NewLine);
			builder.Append(Environment.NewLine);

			builder.AppendFormat("{0}       Waist: {1}", linePrefix, buildDescription(paperDoll.Waist));
			builder.Append(Environment.NewLine);
			builder.AppendFormat("{0}    Trinket1: {1}", linePrefix, buildDescription(paperDoll.Trinket1));
			builder.Append(Environment.NewLine);
			builder.AppendFormat("{0}    Trinket2: {1}", linePrefix, buildDescription(paperDoll.Trinket2));
			builder.Append(Environment.NewLine);
			builder.AppendFormat("{0}        Legs: {1}", linePrefix, buildDescription(paperDoll.Legs));
			builder.Append(Environment.NewLine);
			builder.AppendFormat("{0}        Feet: {1}", linePrefix, buildDescription(paperDoll.Feet));
			builder.Append(Environment.NewLine);
		}


		private void BuildGameClientAddOnList(StringBuilder builder, string linePrefix, out List<string> problemAddOnList)
		{
			problemAddOnList = new List<string>();

			var enabledAddOns = new Dictionary<string, bool>();
			var numAddOns = Lua.GetReturnVal<int>("return GetNumAddOns()", 0);

			// Build list of game client addon names & their enabled/disabled state...
			for (int i = 1; i <= numAddOns; ++i)
			{
				// NB: We check 'enabled' and 'loadable' for addons...
				// If an addon cannot be loaded because it is out-of-date, or missing 
				// a dependency, then it is indirectly disabled.
				var addOnInfoQuery = string.Format("return GetAddOnInfo({0})", i);
				var addOnTitle = StripUiEscapeSequences(Lua.GetReturnVal<string>(addOnInfoQuery, 1));
				addOnTitle = string.IsNullOrEmpty(addOnTitle) ? "UnnamedAddon" : addOnTitle;

				var addOnEnabled =
					Lua.GetReturnVal<bool>(addOnInfoQuery, 3) /*enabled*/
					&& Lua.GetReturnVal<bool>(addOnInfoQuery, 4) /*loadable*/;

				// We're only interested in enabled addons...
				if (!addOnEnabled)
					{ continue; }

				// Make certain addon name is unique...
				var addOnName = addOnTitle;
				for (var sameNameIndex = 2; enabledAddOns.ContainsKey(addOnName); ++sameNameIndex)
				{
					addOnName = string.Format("{0}_{1}", addOnTitle, sameNameIndex);
				}

				enabledAddOns.Add(addOnName, addOnEnabled);
			}


			// Analyze addons for known problem ones...
			builder.AppendFormat("{0}Game client addons [Showing {1} enabled / {2} total]:",
				linePrefix, enabledAddOns.Count, numAddOns);
			builder.Append(Environment.NewLine);
			if (!enabledAddOns.Any())
			{
				builder.AppendFormat("{0}    NONE", linePrefix);
				builder.Append(Environment.NewLine);
			}
			else
			{
				foreach (var addOn in enabledAddOns.OrderBy(a => a.Key))
				{
					var addOnName = addOn.Key;
					var addOnEnabled = addOn.Value;
					var attentionPrefix = string.Empty;
					var enabledMessage = addOnEnabled ? "enabled" : "disabled";
					var isProblemAddOn = IsKnownProblemName(KnownProblemGameClientAddOnNames, addOnName) && addOnEnabled;

					if (isProblemAddOn)
					{
						problemAddOnList.Add(addOnName);
						attentionPrefix = "*** ";
						enabledMessage = "ENABLED ***PROBLEMATICAL*** ADDON";
					}

					builder.AppendFormat("{0}    {1}{2}: {3}",
						linePrefix,
						attentionPrefix,
						(string.IsNullOrEmpty(addOnName) ? "UnnamedAddOn" : addOnName),
						enabledMessage);
					builder.Append(Environment.NewLine);
				}
			}
		}


		// Stolen from Talented2
		private static IEnumerable<TalentPlacement> BuildLearnedTalents()
		{
			var talents = new List<TalentPlacement>();

			for (int tierIndex = 0; tierIndex < 6; tierIndex++)
			{
				for (int talentIndex = 1; talentIndex <= 3; talentIndex++)
				{
					var index = tierIndex * 3 + talentIndex;
					var vals = Lua.GetReturnValues("return GetTalentInfo(" + index + ")");
					var name = vals[0];
					var learned = int.Parse(vals[4]) != 0;

					if (learned)
					{
						talents.Add(new TalentPlacement(tierIndex + 1, talentIndex, name));
					}
				}
			}

			return talents;
		}


		private void BuildMountInfo(StringBuilder builder, string linePrefix, out string problemMountWarnings)
		{
			problemMountWarnings = "";

			var flyingMountNameOrId =
				(CharacterSettings.Instance.FlyingMountName != null)
				? CharacterSettings.Instance.FlyingMountName.Trim()
				: "";
			var groundMountNameOrId = 
				(CharacterSettings.Instance.MountName != null)
				? CharacterSettings.Instance.MountName.Trim()
				: "";

			var attentionPrefix = "";
			var problemText = "";
			if (!string.IsNullOrEmpty(groundMountNameOrId))
			{
				if (!Query.IsMountKnown(groundMountNameOrId))
				{
					attentionPrefix = "*** ";
					problemText = " (*** Problem: Ground mount is not known to Honorbuddy ***)";
					problemMountWarnings +=
						string.Format(
							"{0}* Ground mount ({1}) is not known to Honorbuddy."
							+ "  Spelling error?  ItemId instead of SpellId?"
							+ "  Mount not available on this account?  No skill to use mount?"
							+ " Please configure ground mount correctly.{2}",
							linePrefix,
							groundMountNameOrId,
							Environment.NewLine);
				}

				else if (Query.IsMountFlying(groundMountNameOrId))
				{
					attentionPrefix = "*** ";
					problemText = " (*** Problem: Ground mount is a flying mount ***)";
					problemMountWarnings +=
						string.Format("{0}* Ground mount ({1}) is a flying mount."
							+ "  Please configure a ground-only mount.{2}",
							linePrefix,
							groundMountNameOrId,
							Environment.NewLine);
				}
			}
			builder.AppendFormat("{0}{1}Ground Mount: {2} {3}",
				linePrefix,
				attentionPrefix,
				(string.IsNullOrEmpty(groundMountNameOrId) ? "UNSPECIFIED" : ("'" + groundMountNameOrId + "'")),
				problemText);
			builder.Append(Environment.NewLine);

			attentionPrefix = "";
			problemText = "";
			if (!string.IsNullOrEmpty(flyingMountNameOrId))
			{
				if (!Query.IsMountKnown(flyingMountNameOrId))
				{
					attentionPrefix = "*** ";
					problemText = " (*** Problem: Flying mount is not known to Honorbuddy ***)";
					problemMountWarnings +=
						string.Format(
							"{0} Flying mount ({1}) is not known to Honorbuddy."
							+ "  Spelling error?  ItemId instead of SpellId?"
							+ "  Mount not available on this account?  No skill to use mount?"
							+ "Please configure flying mount correctly.{2}",
							linePrefix,
							flyingMountNameOrId,
							Environment.NewLine);
				}

				else if (!Query.IsMountFlying(flyingMountNameOrId))
				{
					attentionPrefix = "*** ";
					problemText = " (*** Problem: Flying mount is not a flying mount ***)";
					problemMountWarnings +=
						string.Format("{0}* Flying mount ({1}) is NOT a flying mount."
							+ "  Please configure a mount capable of flying.{2}",
							linePrefix,
							groundMountNameOrId,
							Environment.NewLine);
				}
			}
			builder.AppendFormat("{0}{1}Flying Mount: {2} {3}",
				linePrefix,
				attentionPrefix,
				(string.IsNullOrEmpty(flyingMountNameOrId) ? "UNSPECIFIED" : ("'" + flyingMountNameOrId + "'")),
				problemText);
			builder.Append(Environment.NewLine);
		}


		private void BuildPluginList(StringBuilder builder, string linePrefix, out List<string> problemPlugInList)
		{
			problemPlugInList = new List<string>();

			var plugIns = PluginManager.Plugins;

			// Analyze plugins for known problem ones...
			builder.AppendFormat("{0}PlugIns:", linePrefix);
			builder.Append(Environment.NewLine);
			if (!plugIns.Any())
			{
				builder.AppendFormat("{0}    NONE", linePrefix);
				builder.Append(Environment.NewLine);
			}
			else
			{
				foreach (var plugIn in plugIns.OrderBy(plugin => plugin.Name))
				{
					var enabledMessage = plugIn.Enabled ? "enabled" : "disabled";

					// Make certain plugin name is unique...
					var plugInName = string.IsNullOrEmpty(plugIn.Name) ? "UnnamedPlugin" : plugIn.Name;
					for (var sameNameIndex = 2; problemPlugInList.Contains(plugInName); ++sameNameIndex)
					{
						plugInName = string.Format("{0}_{1}", plugIn.Name, sameNameIndex);
					}

					var isNonBosslandPlugInEnabled = !KnownHonorbuddyShippedPluginNames.Contains(plugInName) && plugIn.Enabled;
					var isProblemPlugInEnabled = IsKnownProblemName(KnownProblemPlugInNames, plugInName) && plugIn.Enabled;

					if (isProblemPlugInEnabled)
					{
						problemPlugInList.Add(plugInName);
						enabledMessage = "ENABLED (***PROBLEMATICAL PLUGIN***)";
					}
					else if (isNonBosslandPlugInEnabled)
					{
						enabledMessage = "enabled (NON-BosslandGmbH-SHIPPED PLUGIN)";
					}
					
					builder.AppendFormat("{0}    {1}{2} v{3}: {4}",
						linePrefix,
						((isProblemPlugInEnabled || isNonBosslandPlugInEnabled) ? "*** " : ""),
						plugInName,
						plugIn.Version,
						enabledMessage);
					builder.Append(Environment.NewLine);
				}
			}
		}


		class WoWItemIdComparer : IEqualityComparer<WoWItem>
		{
			public bool Equals(WoWItem x, WoWItem y)
			{
				// Comparison to same object always succeed...
				if (Object.ReferenceEquals(x, y))
					{ return true; }

				// Comparison to null always fails...
				if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
					{ return false; }

				// Compare the item's id...
				return x.Entry == y.Entry;
			}

			// If Equals() returns true for a pair of objects  
			// then GetHashCode() must return the same value for these objects. 
			public int GetHashCode(WoWItem wowItem)
			{
				// No hash code for null object...
				if (Object.ReferenceEquals(wowItem, null))
					{ return 0; }

				// Use hash code for the Entry field if WoWItem is not null...
				return wowItem.Entry.GetHashCode();
			}
		}


		private void BuildQuestItemList(StringBuilder builder, string linePrefix)
		{
			var questItems =
				(from item in Me.BagItems
				where Query.IsQuestItem(item)
				select item)
				.Distinct(new WoWItemIdComparer())
				.ToList();

			// Analyze plugins for known problem ones...
			builder.AppendFormat("{0}Quest Items in backpack:", linePrefix);
			builder.Append(Environment.NewLine);
			if (!questItems.Any())
			{
				builder.AppendFormat("{0}    NONE", linePrefix);
				builder.Append(Environment.NewLine);
			}
			else
			{
                foreach (var questItem in questItems.OrderBy(item => item.ItemInfo.InternalInfo.QuestId).ThenBy(item => item.SafeName))
				{
					var questId = questItem.ItemInfo.InternalInfo.QuestId;
					var quest = Quest.FromId((uint)questId);
					var stackCount =
						Me.CarriedItems
						.Where(i => (i.Entry == questItem.Entry))
						.Sum(i => i.StackCount);

					builder.AppendFormat("{0}    {1}{2} (http://wowhead.com/item={3})",
						linePrefix,
                        questItem.SafeName,
						((stackCount <= 1) ? "" : string.Format(" x{0}", stackCount)),
						questItem.Entry);
					builder.Append(Environment.NewLine);
					if (questItem.ItemInfo.BeginQuestId != 0)
					{
						builder.AppendFormat("{0}        => starts quest \"{1}\" (http://wowhead.com/quest={2})",
							linePrefix,
							((quest != null) ? quest.Name : "UnknownQuest"),
							questId);
						builder.Append(Environment.NewLine);
					}
				}
			}
		}


		private void BuildQuestState(StringBuilder builder, string linePrefix)
		{
			var questCount = Me.QuestLog.GetAllQuests().Count;

			// Analyze plugins for known problem ones...
			builder.AppendFormat("{0}Quest Log ({1} total):", linePrefix, questCount);
			builder.Append(Environment.NewLine);
			if (questCount <= 0)
			{
				builder.AppendFormat("{0}    None", linePrefix);
				builder.Append(Environment.NewLine);
				return;
			}

			// Present the quest in the same oder shown in user's quest log...
			var questsQuery =
				Me.QuestLog.GetAllQuests()
				.Select(q => new { Quest = q, Index = Lua.GetReturnVal<int>(string.Format("return GetQuestLogIndexByID({0})", q.Id), 0) })
				.OrderBy(val => val.Index);

			foreach (var quest in questsQuery)
			{
				var questState =
					quest.Quest.IsCompleted ? "COMPLETED"
					: quest.Quest.IsFailed ? "FAILED"
					: "incomplete";

				builder.AppendFormat("{0}    \"{1}\"(http://wowhead.com/quest={2}) {3}{4}",
					linePrefix,
					quest.Quest.Name,
					quest.Quest.Id,
					questState,
					(quest.Quest.IsDaily ? ", Daily" : ""));
				builder.Append(Environment.NewLine);

				foreach (var objective in quest.Quest.GetObjectives().OrderBy(o => o.Index))
				{
					var objectiveIndex = objective.Index + 1;   // HB is zero-based, but LUA is one-based
					var objectiveQuery = string.Format("return GetQuestLogLeaderBoard({0},{1})", objectiveIndex, quest.Index);
					var objectiveText = Lua.GetReturnVal<string>(objectiveQuery, 0);
					var objectiveIsFinished = Lua.GetReturnVal<bool>(objectiveQuery, 2);

					builder.AppendFormat("{0}        {1} (type: {2}) {3}",
						linePrefix,
						objectiveText,
						objective.Type,
						(objectiveIsFinished ? "OBJECTIVE_COMPLETE" : ""));
					builder.Append(Environment.NewLine);
				}
			}
		}


		private void BuildSkillsInfo(StringBuilder builder, string linePrefix)
		{
			Action<WoWSkill> buildSkillInfo =
				(wowSkill) =>
				{
					if ((wowSkill != null) && wowSkill.IsValid && (wowSkill.MaxValue > 0))
					{
						builder.AppendFormat("{0}    {1}: {2}/{3}",
							linePrefix,
							wowSkill.Name,
							wowSkill.CurrentValue,
							wowSkill.MaxValue);

						if (wowSkill.Bonus > 0)
							{ builder.AppendFormat(" [Bonus: +{0}]", wowSkill.Bonus); }

						builder.Append(Environment.NewLine);
					}
				};

			
			builder.AppendFormat("{0}Skills:", linePrefix);
			builder.Append(Environment.NewLine);

			buildSkillInfo(Me.GetSkill(SkillLine.Alchemy));
			buildSkillInfo(Me.GetSkill(SkillLine.Archaeology));
			buildSkillInfo(Me.GetSkill(SkillLine.Blacksmithing));
			buildSkillInfo(Me.GetSkill(SkillLine.Cooking));
			buildSkillInfo(Me.GetSkill(SkillLine.Enchanting));
			buildSkillInfo(Me.GetSkill(SkillLine.Engineering));
			buildSkillInfo(Me.GetSkill(SkillLine.FirstAid));
			buildSkillInfo(Me.GetSkill(SkillLine.Fishing));
			buildSkillInfo(Me.GetSkill(SkillLine.Herbalism));
			buildSkillInfo(Me.GetSkill(SkillLine.Jewelcrafting));
			buildSkillInfo(Me.GetSkill(SkillLine.Leatherworking));
			buildSkillInfo(Me.GetSkill(SkillLine.Mining));
			buildSkillInfo(Me.GetSkill(SkillLine.Riding));
			buildSkillInfo(Me.GetSkill(SkillLine.Skinning));
			buildSkillInfo(Me.GetSkill(SkillLine.Tailoring));
		}


		private bool EmitStateInfo()
		{
			using (StyxWoW.Memory.AcquireFrame(true))
			{
				var fps = GetFPS();
				var latency = StyxWoW.WoWClient.Latency;
				var linePrefix = new String(' ', 4);

				// Build compatibility info...
				var builderInfo = new StringBuilder();

				builderInfo.Append("---------- BEGIN: Profile Compatibility Info ----------");

				// Toon info...
				builderInfo.Append(Environment.NewLine);
				builderInfo.AppendFormat("{0}Toon: {1:F1} {2} {3} ({4}, {5})",
					linePrefix,
					Me.LevelFraction,
					Me.Race,
					Me.Class,
					Me.Gender,
					GetPlayerFaction(Me)
					);
				builderInfo.Append(Environment.NewLine);
				builderInfo.AppendFormat("{0}Specialization: {1}", linePrefix, Me.Specialization);
				builderInfo.Append(Environment.NewLine);
				foreach (var talent in BuildLearnedTalents().OrderBy(t => t.Tier))
				{
					builderInfo.AppendFormat("{0}    Tier {1}: {2}({3})",
						linePrefix,
						talent.Tier,
						talent.Name,
						talent.Index);
					builderInfo.Append(Environment.NewLine);
				}

				builderInfo.AppendFormat("{0}Location: {1} => {2} => {3} => {4}",
					linePrefix,
					GetCurrentMapContinentName(),
					(Me.ZoneText ?? "noZone"),
					(Me.SubZoneText ?? "noSubZone"),
					Me.Location);
				builderInfo.Append(Environment.NewLine);

				builderInfo.AppendFormat("{0}Profile: {1}", linePrefix, GetCurrentProfileName());
				builderInfo.Append(Environment.NewLine);
				builderInfo.AppendFormat("{0}Combat Routine: {1}", linePrefix, GetCombatRoutineName());
				builderInfo.Append(Environment.NewLine);

				// Skills...
				builderInfo.Append(Environment.NewLine);
				BuildSkillsInfo(builderInfo, linePrefix);

				// Quest state...
				builderInfo.Append(Environment.NewLine);
				BuildQuestState(builderInfo, linePrefix);

				// Quest Items in backpack...
				builderInfo.Append(Environment.NewLine);
				BuildQuestItemList(builderInfo, linePrefix);

				// Bag List...
				builderInfo.Append(Environment.NewLine);
				BuildBagList(builderInfo, linePrefix);

				// Equipment List...
				builderInfo.Append(Environment.NewLine);
				BuildEquipmentList(builderInfo, linePrefix);


				// Executable environment & configuration info...
				builderInfo.Append(Environment.NewLine);
				builderInfo.AppendFormat("{0}Honorbuddy: v{1}", linePrefix, Assembly.GetEntryAssembly().GetName().Version);
				builderInfo.Append(Environment.NewLine);
				builderInfo.AppendFormat("{0}Game client: v{1} ({2} FPS, {3}ms latency)",
					linePrefix,
					StyxWoW.GameVersion,
					fps,
					latency);
				builderInfo.Append(Environment.NewLine);
				builderInfo.AppendFormat("{0}    Auto Loot? {1}", linePrefix, (IsGameClientAutoLootEnabled() ? "enabled" : "DISABLED"));
				builderInfo.Append(Environment.NewLine);
				builderInfo.AppendFormat("{0}    Windowed mode? {1}", linePrefix, (IsGameClientWindowedModeEnabled() ? "enabled" : "DISABLED"));
				builderInfo.Append(Environment.NewLine);
				
				// Mount checks...
				string mountWarnings;
				BuildMountInfo(builderInfo, linePrefix + "    ", out mountWarnings);
				builderInfo.Append(Environment.NewLine);


				// Configuration info...
				List<string> problemAddOnList;
				BuildGameClientAddOnList(builderInfo, linePrefix, out problemAddOnList);

				List<string> problemPlugInList;
				builderInfo.Append(Environment.NewLine);
				BuildPluginList(builderInfo, linePrefix, out problemPlugInList);


				// Warnings & Errors...
				var builderErrors = new StringBuilder();
				var builderWarnings = new StringBuilder();

				// Does Combat Routine match toon's class?
				// NB: This can happen if the user logs out and back in to the game client
				// without restarting HB.
				if (Me.Class != RoutineManager.Current.Class)
				{
					builderErrors.AppendFormat("{0}* Combat Routine class({1}) does not match Toon class({2})"
						+ "--please restart Honorbuddy.",
						linePrefix,
						RoutineManager.Current.Class,
						Me.Class);
					builderErrors.Append(Environment.NewLine);
				}

				// Is AutoLoot turned off?
				if (!IsGameClientAutoLootEnabled())
				{
					builderErrors.AppendFormat("{0}* AutoLoot is not enabled in game client"
						+ "--please turn it on.",
						linePrefix);
					builderErrors.Append(Environment.NewLine);
				}


				// Is Windowed mode?
				if (!IsGameClientWindowedModeEnabled())
				{
					builderErrors.AppendFormat("{0}* The game client is running in 'full screen' mode"
						+ "--please set it to 'windowed' mode.",
						linePrefix);
					builderErrors.Append(Environment.NewLine);
				}


				// Mixed mode?
				if (IsMixedModeBot())
				{
					var builder = AllowMixedModeBot ? builderWarnings : builderErrors;

					builder.AppendFormat("{0}* MixedMode bot is not compatible with this profile"
						+ "--please select >Questing< bot.",
						linePrefix);
					builder.Append(Environment.NewLine);
				}

				// Problematic AddOns?
				if (problemAddOnList.Any())
				{
					var builder = AllowBrokenAddOns ? builderWarnings : builderErrors;

					builder.AppendFormat("{0}* Problematic game client addons were encountered"
						+ "--please disable these addons:{1}",
						linePrefix,
						string.Join(", ", problemAddOnList.Select(addOnName => string.Format("{0}{1}    {2}", Environment.NewLine, linePrefix, addOnName))));
					builder.Append(Environment.NewLine);
				}

				// Problematic PlugIns?
				if (problemPlugInList.Any())
				{
					var builder = AllowBrokenPlugIns ? builderWarnings : builderErrors;

					builder.AppendFormat("{0}* Problematic plugins were encountered"
						+ "--please disable these plugins:{1}",
						linePrefix,
						string.Join(", ", problemPlugInList.Select(plugInName => string.Format("{0}{1}    {2}", Environment.NewLine, linePrefix, plugInName))));
					builder.Append(Environment.NewLine);
				}

				// Absurdly low FPS?
				if (fps < MinimumFpsWarningThreshold)
				{
					builderWarnings.AppendFormat("{0}* FPS ({1}) is absurdly low (expected {2} FPS, minimum).",
						linePrefix,
						fps,
						MinimumFpsWarningThreshold);
					builderWarnings.Append(Environment.NewLine);
				}

				// Absurdly high latency?
				if (latency > MaximumLatencyWarningThreshold)
				{
					builderWarnings.AppendFormat("{0}* Latency ({1}ms) is absurdly high (expected {2}ms latency, maximum).",
						linePrefix,
						latency,
						MaximumLatencyWarningThreshold);
					builderWarnings.Append(Environment.NewLine);
				}

				// Issues with ground or flying mount?
				if (!string.IsNullOrEmpty(mountWarnings))
				{
					builderWarnings.Append(mountWarnings);
					builderWarnings.Append(Environment.NewLine);
				}


				// Emit compatibility info...
				QBCLog.DeveloperInfo(this, builderInfo.ToString());

				// Emit warnings...
				if (builderWarnings.Length > 0)
				{
					QBCLog.Warning(this, "PROFILE COMPATIBILITY WARNINGS:{0}{1}",
						Environment.NewLine,
						builderWarnings.ToString());
				}

				// Emit errors...
				if (builderErrors.Length > 0)
				{
					QBCLog.Error(this, "PROFILE COMPATIBILITY ERRORS:{0}{1}",
						Environment.NewLine,
						builderErrors.ToString());
				}

				// Emit end demark...
				QBCLog.DeveloperInfo(this, "---------- END: Profile Compatibility Info ----------");

				// Return value indicating whether or not fatal errors encountered...
				return builderErrors.Length > 0;
			}
		}

		
		private string GetCombatRoutineName()
		{
			if (RoutineManager.Current == null)
				{ return "UnknownCombatRoutine";  }

			return string.IsNullOrEmpty(RoutineManager.Current.Name)
				? "UnnamedCombatRoutine"
				: RoutineManager.Current.ToString();
		}


		private string GetCurrentMapContinentName()
		{
			var instanceName = Lua.GetReturnVal<string>("return GetInstanceInfo()", 0);

			return instanceName;
		}


		private string GetCurrentProfileName()
		{
			var currentProfile = ProfileManager.CurrentProfile;
	
			if (currentProfile == null)
				{ return "No profile"; }

			return currentProfile.Name ?? "noProfileName";
		}


		private int GetFPS()
		{ // swiped from Singular
			try
			{
				return (int)Lua.GetReturnVal<float>("return GetFramerate()", 0);
			}
			catch
			{
				// empty
			}

			return 0;
		}


		private string GetPlayerFaction(LocalPlayer localPlayer)
		{
			if (localPlayer.IsAlliance)
				{ return "Alliance"; }

			if (localPlayer.IsHorde)
				{ return "Horde"; }

			// Pandarans will be unaligned until level 10 or so...
			return "Unaligned";
		}


		private bool IsGameClientAutoLootEnabled()
		{
			var autoLootEnabled = Lua.GetReturnVal<bool>("return GetCVar('AutoLootDefault')", 0);

			return autoLootEnabled;
		}


		private bool IsGameClientWindowedModeEnabled()
		{
			var windowedModeEnabled = Lua.GetReturnVal<bool>("return GetCVar('gxWindow')", 0);

			return windowedModeEnabled; 
		}


		private bool IsKnownProblemName(IEnumerable<string> knownProblemNameList, string name)
		{
			return knownProblemNameList.Any(o => name.StartsWith(o, true, CultureInfo.InvariantCulture));
			// return knownProblemNameList.Any(o => name.Equals(o, StringComparison.InvariantCultureIgnoreCase));
		}


		private bool IsMixedModeBot()
		{
			if (BotManager.Current == null)
				{ return false; }

			var botName = BotManager.Current.Name ?? string.Empty;

			return botName.StartsWith("mixed", true, CultureInfo.InvariantCulture);
		}


		// NB: The WoWclient allows embeddable sequences containing color info in the addon title.
		// See: http://www.wowwiki.com/TOC_format and http://www.wowwiki.com/UI_escape_sequences
		// We must strip away these sequences to create a comparable name.
		// ElvUI, TukUI, and Zygor are examples of addons that make use of this feature.
		private string StripUiEscapeSequences(string possiblyDecoratedName)
		{
			string name = possiblyDecoratedName ?? string.Empty;

			for (MatchCollection matches = _regexUiEscape.Matches(name);
				matches.Count > 0;
				matches = _regexUiEscape.Matches(name))
			{
				name = _regexUiEscape.Replace(name, "$1");
			}

			return name;
		}
		private readonly Regex _regexUiEscape = new Regex(@"\|c[0-9A-Fa-f]{1,8}([^|]*)(\s*\|r)?", RegexOptions.IgnoreCase);



		private class TalentPlacement
		{
			public readonly int Tier;
			public readonly int Index;
			public readonly string Name;

			public TalentPlacement(int tier, int index, string name)
			{
				Tier = tier;
				Index = index;
				Name = name;
			}
		}
		#endregion
	}
}