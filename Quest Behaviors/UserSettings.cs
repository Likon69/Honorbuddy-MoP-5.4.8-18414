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
// DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_UserSettings
//
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
#endregion


namespace Honorbuddy.Quest_Behaviors.UserSettings
{
	[CustomBehaviorFileName(@"UserSettings")]
	class UserSettings : QuestBehaviorBase
	{
		#region Constructor and Argument Processing
		public UserSettings(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// Build the 'presets'...
				// Note that the "UserOriginal" configuration will also be captured as a preset.
				_presetChangeSets = BuildPresets();

				// Behavior-specific attributes...
				PersistedDebugShowChangesApplied = GetAttributeAsNullable<bool>("DebugShowChangesApplied", false, null, null)
					?? PersistedDebugShowChangesApplied;
				DebugShowDetails = GetAttributeAsNullable<bool>("DebugShowDetails", false, null, null) ?? false;
				DebugShowDiff = GetAttributeAsNullable<bool>("DebugShowDiff", false, null, null) ?? false;
				PresetName = GetAttributeAs<string>("Preset", false, new ConstrainTo.SpecificValues<string>(_presetChangeSets.Keys.ToArray()), null) ?? "";
				IsStopBot = GetAttributeAsNullable<bool>("StopBot", false, null, null) ?? false;

				// Attempted to read the 'recognized attributes', so they won't be marked as "not recognized" by the argument processor...
				foreach (var recognizedAttribute in ChangeSet.RecognizedSettings)
				{
					GetAttributeAs<object>(recognizedAttribute.Name, false, null, null);
				}

				_userChangeRequest = ChangeSet.FromXmlAttributes(args);

				// If we were unable to create an (even empty) changeset, then we ran into a problem...
				if (_userChangeRequest == null)
				{ IsAttributeProblem = true; }
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
		private bool DebugShowDetails { get; set; }
		private bool DebugShowDiff { get; set; }
		private bool IsStopBot { get; set; }
		private string PresetName { get; set; }


		protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
		{
			var explicitlyHandled = new List<string>();

			UsageCheck_DeprecatedAttribute(xElement,
				Args.Keys.Contains("LootMobs"),
				"LootMobs",
				context => string.Format("Please update profile to use <LootMobs Value=\"{1}\" />, instead.",
					Environment.NewLine,
					Args["LootMobs"]));
			explicitlyHandled.Add("LootMobs");

			UsageCheck_DeprecatedAttribute(xElement,
				Args.Keys.Contains("PullDistance"),
				"PullDistance",
				context => string.Format("Please update profile to use <TargetingDistance Value=\"{1}\" />, instead.{0}"
					+ "  To restore the original value when done, <TargetingDistance Value=\"null\" />.{0}"
					+ "  Please do not fiddle with TargetingDistance unless _absolutely_ necessary.",
					Environment.NewLine,
					Args["PullDistance"]));
			explicitlyHandled.Add("PullDistance");

			UsageCheck_DeprecatedAttribute(xElement,
				Args.Keys.Contains("UseMount"),
				"UseMount",
				context => string.Format("Please update profile to use <UseMount Value=\"{1}\" />, instead.",
					Environment.NewLine,
					Args["UseMount"]));
			explicitlyHandled.Add("UseMount");


			foreach (var attributeName in Args.Keys.Where(attrName => !explicitlyHandled.Contains(attrName)))
			{
				var recognizedSetting = ChangeSet.RecognizedSettings.FirstOrDefault(s => s.Name == attributeName);

				// If setting not recognized, skip it...
				if (recognizedSetting == null)
				{ continue; }

				if (recognizedSetting.IsObsolete)
				{
					var message = string.Format("Honorbuddy has marked attribute '{1}' as 'Obsolete'.{0}"
						+ "  The attribute may no longer work as expected.  The attribute will be removed in a future release."
						+ "  Please update the profile to remove usage of the '{1}' attribute.",
						Environment.NewLine,
						attributeName);

					if (!string.IsNullOrEmpty(recognizedSetting.ObsoleteMessage))
					{
						message += string.Format("{0}  HB API Info: {1}", Environment.NewLine, recognizedSetting.ObsoleteMessage);
					}

					UsageCheck_DeprecatedAttribute(xElement, true, attributeName, context => message);
				}
			}
		}


		protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
		{
			foreach (var attributeName in Args.Keys)
			{
				var isAttributeAccessDisallowed = ChangeSet.RecognizedSettings.Any(s => (s.Name == attributeName) && s.IsAccessDisallowed);

				if (isAttributeAccessDisallowed)
				{
					var message = string.Format("UserSettings does not allow access to the '{1}' attribute.{0}"
						+ "  Please modify the profile to refrain from accessing the '{1}' attribute.",
						Environment.NewLine,
						attributeName);

					UsageCheck_SemanticCoherency(xElement, true, context => message);
				}
			}
		}
		#endregion


		#region Private and Convenience variables
		private readonly Dictionary<string, ChangeSet> _presetChangeSets;
		private readonly ChangeSet _userChangeRequest;

		// Persisted Data...
		private static bool PersistedDebugShowChangesApplied = false;
		private static bool PersistedIsBotStopHooked = false;
		#endregion


		#region Overrides of QuestBehaviorBase
		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id: UserSettings.cs 1581 2014-06-27 02:34:30Z Mainhaxor $"); } }
		public override string SubversionRevision { get { return ("$Revision: 1581 $"); } }


		protected override ConfigMemento CreateConfigMemento()
		{
			// Suppress the creation of a ConfigMemento...
			// We do NOT want the user settings restored after we have altered them with this behavior.
			return null;
		}


		public override void OnFinished()
		{
			// Defend against being called multiple times (just in case)...
			if (!IsOnFinishedRun)
			{
				// Call parent Dispose() (if it exists) here ...
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
				var logInfo = new StringBuilder();
				var logDeveloperInfo = new StringBuilder();

				// The BotStop handler will put the original configuration settings back in place...
				// Note, we only want to hook it once for this behavior.
				if (!PersistedIsBotStopHooked)
				{
					BotEvents.OnBotStopped += BotEvents_OnBotStopped;
					PersistedIsBotStopHooked = true;
				}

				// First, process Preset request, if any...
				if (!string.IsNullOrEmpty(PresetName))
				{
					var presetChangeSet =
					   (from preset in _presetChangeSets
						where preset.Key == PresetName
						select preset.Value)
						.FirstOrDefault();

					if (presetChangeSet == null)
					{
						QBCLog.Error("Unable to locate any preset named '{0}'", PresetName);
						TreeRoot.Stop();
						BehaviorDone();
						return;
					}

					var appliedChanges = presetChangeSet.Apply("    ");

					var appliedChangesBuilder = PersistedDebugShowChangesApplied ? logInfo : logDeveloperInfo;
					appliedChangesBuilder.AppendFormat("Using preset '{0}'...{1}", PresetName, appliedChanges);
					appliedChangesBuilder.Append(Environment.NewLine);
				}

				// Second, apply any change requests...
				if (_userChangeRequest.Count > 0)
				{
					string appliedChanges = _userChangeRequest.Apply("    ");

					var appliedChangesBuilder = PersistedDebugShowChangesApplied ? logInfo : logDeveloperInfo;
					appliedChangesBuilder.AppendFormat("Applied changes...{0}", appliedChanges);
					appliedChangesBuilder.Append(Environment.NewLine);
				}

				// Third, show state, if requested...                
				if (DebugShowDetails)
				{
					var currentConfiguration = ChangeSet.FromCurrentConfiguration();

					logInfo.AppendFormat("Details...{0}", currentConfiguration.BuildDetails("    "));
					logInfo.Append(Environment.NewLine);
				}

				var diffBuilder = DebugShowDiff ? logInfo : logDeveloperInfo;
				diffBuilder.AppendFormat("Difference from user's original settings...{0}",
					ChangeSet.BuildDifferencesFromOriginalSettings("    "));
				diffBuilder.Append(Environment.NewLine);

				// Forth, stop the bot, if requested...
				if (IsStopBot)
				{
					const string message = "Stopping the bot per profile request.";
					logInfo.AppendFormat(message);
					logInfo.Append(Environment.NewLine);

					var logInfoString = logInfo.ToString();
					if (!string.IsNullOrEmpty(logInfoString))
					{ QBCLog.Info(logInfoString); }

					var logDeveloperString = logDeveloperInfo.ToString();
					if (!string.IsNullOrEmpty(logDeveloperString))
					{ QBCLog.DeveloperInfo(logDeveloperString); }

					TreeRoot.Stop(message);
					BehaviorDone();
					return;
				}

				else
				{
					var logInfoString = logInfo.ToString();
					if (!string.IsNullOrEmpty(logInfoString))
					{ QBCLog.Info(logInfoString); }

					var logDeveloperString = logDeveloperInfo.ToString();
					if (!string.IsNullOrEmpty(logDeveloperString))
					{ QBCLog.DeveloperInfo(logDeveloperString); }
				}

				BehaviorDone();
			}
		}
		#endregion


		private void BotEvents_OnBotStopped(EventArgs args)
		{
			// Restore the user's original configuration, since the bot is stopping...
			if (ChangeSet.OriginalConfiguration != null)
			{
				var changesApplied = ChangeSet.OriginalConfiguration.Apply("    ", true);
				var logMessage =
					string.Format("Bot stopping.  Original user settings restored as follows...{0}",
						(!string.IsNullOrEmpty(changesApplied)
						? changesApplied
						: (Environment.NewLine + "    Original Settings intact--no changes to restore.")));

				if (PersistedDebugShowChangesApplied)
				{ QBCLog.Info(this, logMessage); }
				else
				{ QBCLog.DeveloperInfo(this, logMessage); }
			}

			// Remove our OnBotStop handler
			BotEvents.OnBotStopped -= BotEvents_OnBotStopped;

			// Reset persistent data...
			PersistedIsBotStopHooked = false;
			PersistedDebugShowChangesApplied = false;
			ChangeSet.OriginalConfiguration = null;
			ChangeSet.RecognizedSettings = null;
		}


		#region Preset ChangeSets
		// To add, adjust, or remove presets, this is the only method that needs to be modified...
		// All other code in this class uses the information contained in the returned presetChangeRequests.
		// Note: If you make a spelling error while maintaining this code, by design an exception will be thrown
		// at runtime pointing you directly to the problem.
		private Dictionary<string, ChangeSet> BuildPresets()
		{
			var herbalismSkill = Me.GetSkill(SkillLine.Herbalism);
			var hasHerbalism = herbalismSkill != null ? herbalismSkill.MaxValue > 0 : false;

			var miningSkill = Me.GetSkill(SkillLine.Mining);
			var hasMining = miningSkill != null ? miningSkill.MaxValue > 0 : false;

			var skinningSkill = Me.GetSkill(SkillLine.Skinning);
			var hasSkinning = skinningSkill != null ? skinningSkill.MaxValue > 0 : false;

			var presets = new Dictionary<string, ChangeSet>
			{
				{
					"Grind",
					new ChangeSet(new Dictionary<string, object>()
					{
						{ "GroundMountFarmingMode", false },
						{ "KillBetweenHotspots", true },
					})
				},
				{
					"HarvestsOff",
					new ChangeSet(new Dictionary<string, object>()
					{
						{ "HarvestHerbs", false },
						{ "HarvestMinerals", false },
						{ "LootMobs", false },
						{ "NinjaSkin", false },
						{ "SkinMobs", false },
					})
				},
				{
					"HarvestsOn",
					new ChangeSet(new Dictionary<string, object>()
					{
						{ "HarvestHerbs", hasHerbalism },
						{ "HarvestMinerals", hasMining },
						{ "LootMobs", true },
						{ "LootRadius", 45 },
						{ "NinjaSkin", hasSkinning },
						{ "SkinMobs", hasSkinning },
					})
				},
				{
					"NoDistractions",
					new ChangeSet(new Dictionary<string, object>()
					{
						{ "GroundMountFarmingMode", true },
						{ "HarvestHerbs", false },
						{ "HarvestMinerals", false },
						{ "KillBetweenHotspots", false },
						{ "LootMobs", false },
						{ "NinjaSkin", false },
						{ "SkinMobs", false },
					})
				},
				{
					"NoTrain",
					new ChangeSet(new Dictionary<string, object>())
				},
				{
					"NormalQuesting",
					new ChangeSet(new Dictionary<string, object>()
					{
						{ "GroundMountFarmingMode", false },
						{ "HarvestHerbs", hasHerbalism },
						{ "HarvestMinerals", hasMining },
						{ "KillBetweenHotspots", false },
						{ "LootMobs", true },
						{ "LootRadius", 45 },
						{ "NinjaSkin", hasSkinning },
						{ "RessAtSpiritHealers", false },
						{ "SkinMobs", hasSkinning },
						{ "UseRandomMount", true },
					})
				},
				{
					"UserOriginal",
					ChangeSet.OriginalConfiguration
				}
			};

			return presets;
		}
		#endregion
	}


	//==================================================
	// All classes below this point are support for getting the work done
	//

	public class ChangeSet
	{
		public ChangeSet(Dictionary<string, object> changes)
		{
			var changeSet = new List<Tuple<SettingDescriptor, object>>();
			var isProblemAttribute = false;

			foreach (var change in changes)
			{
				try
				{
					var name = change.Key;
					var value = change.Value;

					// Setting name cannot be null or empty...
					if (string.IsNullOrEmpty(name))
					{
						QBCLog.Error("Name may not be null or empty");
						isProblemAttribute = true;
						continue;
					}

					// Check that setting exists...
					var settingDescriptor = RecognizedSettings.FirstOrDefault(s => s.Name == name);
					if (settingDescriptor == null)
					{
						QBCLog.Error("Unable to locate setting for '{0}'.", name);
						isProblemAttribute = true;
						continue;
					}

					// Is changing attribute allowed?
					if (settingDescriptor.IsAccessDisallowed)
					{
						QBCLog.Error("Accessing attribute '{0}' is not allowed.", name);
						isProblemAttribute = true;
						continue;
					}

					// Check that setting doesn't already exist in the changeset...
					if (changeSet.Any(t => t.Item1.Name == name))
					{
						QBCLog.Error("Setting '{0}' already exists in the changeset.", name);
						isProblemAttribute = true;
						continue;
					}

					// If user specified 'original' value, go look it up and substitute it for 'value'...
					if ((value is string) && ((string)value == "original"))
					{
						object originalValue;

						if (!OriginalConfiguration.TryGetValue(settingDescriptor.Name, out originalValue))
						{
							// A missing 'original configuration' is a maintenance issue, not a user error...
							QBCLog.MaintenanceError("For setting '{0}', there is no original configuration value.",
								settingDescriptor.Name);
							isProblemAttribute = true;
							continue;
						}

						value = originalValue;
					}

					// Check that setting is an appropriate type...
					var newValue = settingDescriptor.ToCongruentObject(value);

					if (!settingDescriptor.ConstraintChecker.IsWithinConstraints(newValue))
					{
						QBCLog.Error("For setting '{0}', the provided value '{1}' is not within the required constraints of {2}.",
							name, value, settingDescriptor.ConstraintChecker.Description);
						isProblemAttribute = true;
						continue;
					}

					// Setting change is acceptable...
					changeSet.Add(Tuple.Create(settingDescriptor, value));
				}

				catch (Exception ex)
				{
					QBCLog.Exception(ex, "MAINTENANCE ERROR: Error processing attribute '{0}.'", change.Key);
					isProblemAttribute = true;
				}
			}

			// If problem encountered with any change, we're unable to build the ChangeSet...
			if (isProblemAttribute)
			{
				_changeSet = null;
				throw new ArgumentException("Problems encountered with provided argument");
			}

			Count = changeSet.Count;
			_changeSet = new ReadOnlyCollection<Tuple<SettingDescriptor, object>>(changeSet);
		}

		private readonly ReadOnlyCollection<Tuple<SettingDescriptor, object>> _changeSet;
		public int Count { get; private set; }


		public static ReadOnlyCollection<SettingDescriptor> RecognizedSettings
		{
			get { return _recognizedSettings ?? (_recognizedSettings = BuildRecognizedSettings()); }

			// Any attempts to set the value, will 'uninitialize' it...
			set { _recognizedSettings = null; }
		}
		private static ReadOnlyCollection<SettingDescriptor> _recognizedSettings;


		public static ChangeSet OriginalConfiguration
		{
			get { return _originalConfiguration ?? (_originalConfiguration = ChangeSet.FromCurrentConfiguration()); }

			// Any attempts to set the value, will 'uninitialize' it...
			set { _originalConfiguration = null; }
		}
		private static ChangeSet _originalConfiguration;


		public string Apply(string linePrefix = "", bool onlyApplyChangesIfDifferent = false)
		{
			var changesApplied = new StringBuilder();

			foreach (var change in _changeSet.OrderBy(t => t.Item1.Name))
			{
				var settingDescriptor = change.Item1;
				var valueWanted = change.Item2;
				var previousValue = settingDescriptor.GetValue();
				object originalValue;

				OriginalConfiguration.TryGetValue(settingDescriptor.Name, out originalValue);


				if (!onlyApplyChangesIfDifferent || !object.Equals(valueWanted, previousValue))
				{
					settingDescriptor.SetValue(valueWanted);

					// Note, we read back the value rather than just assumed the 'set' worked...
					// For instance, for Obsolete attributes, the value may be hard-coded and the set
					// did not take effect.  We should report the real value, so the user knows of the problem.
					var valueObtained = settingDescriptor.GetValue();
					changesApplied.AppendFormat("{0}{1}{2} = {3} (previous: {4};  original: {5})",
						Environment.NewLine, linePrefix, settingDescriptor.Name, valueObtained, previousValue, originalValue);

					if (settingDescriptor.IsObsolete)
					{ changesApplied.Append(" [OBSOLETE]"); }

					if (settingDescriptor.IsAccessDisallowed)
					{ changesApplied.Append(" [ACCESS DISALLOWED]"); }
				}
			}

			return changesApplied.ToString();
		}


		private bool TryGetValue(string name, out object outValue)
		{
			Contract.Requires(!string.IsNullOrEmpty(name), context => "Name cannot be null or empty");

			var changeEntry = _changeSet.FirstOrDefault(v => v.Item1.Name == name);
			if (changeEntry == null)
			{
				outValue = null;
				return false;
			}

			outValue = changeEntry.Item2;
			return true;
		}


		public string BuildDetails(string linePrefix)
		{
			var builder = new StringBuilder();

			foreach (var setting in _changeSet.OrderBy(t => t.Item1.Name))
			{
				object originalValue = null;
				OriginalConfiguration.TryGetValue(setting.Item1.Name, out originalValue);

				if (setting.Item2.Equals(originalValue))
				{
					builder.AppendFormat("{0}{1}{2}.{3} = {4}",
						Environment.NewLine, linePrefix, setting.Item1.InstanceName, setting.Item1.Name, setting.Item2);
				}
				else
				{
					builder.AppendFormat("{0}{1}{2}.{3} = {4} (original: {5})",
						Environment.NewLine, linePrefix, setting.Item1.InstanceName, setting.Item1.Name, setting.Item2, originalValue);
				}

				if (setting.Item1.IsObsolete)
				{ builder.Append(" [OBSOLETE]"); }

				if (setting.Item1.IsAccessDisallowed)
				{ builder.Append(" [ACCESS DISALLOWED]"); }

			}

			return builder.ToString();
		}


		public static string BuildDifferencesFromOriginalSettings(string linePrefix)
		{
			var builder = new StringBuilder();

			foreach (var setting in OriginalConfiguration._changeSet.OrderBy(t => t.Item1.Name))
			{
				var currentValue = setting.Item1.GetValue();

				if (object.Equals(currentValue, setting.Item2))
				{ continue; }

				builder.AppendFormat("{0}{1}{2} = {3} (originally: {4})",
					Environment.NewLine, linePrefix, setting.Item1.Name, currentValue, setting.Item2);

				if (setting.Item1.IsObsolete)
				{ builder.Append(" [OBSOLETE]"); }

				if (setting.Item1.IsAccessDisallowed)
				{ builder.Append(" [ACCESS DISALLOWED]"); }
			}

			if (builder.Length <= 0)
			{
				builder.AppendFormat("{0}{1}No changes from original settings",
					Environment.NewLine, linePrefix);
			}

			return builder.ToString();
		}


		// Note: The RecognizedAttribute's Dictionary value field was left open for user data, by design.
		// We take advantage of that here by storing ConfigurationDescriptors to help us further process
		// the data by moving it into and out of the appropriate properties.
		// Note: If you make a spelling error while maintaining this code, by design an exception will be thrown
		// at runtime pointing you directly to the problem.
		private static ReadOnlyCollection<SettingDescriptor> BuildRecognizedSettings()
		{
			// Attach constraints to particular elements --
			var constraints = new Dictionary<string, ConstraintChecker>()
			{
				{ "DrinkAmount",            new ConstrainInteger(0, 100) },
				{ "FoodAmount",             new ConstrainInteger(0, 100) },
				{ "LogoutInactivityTimer",  new ConstrainInteger(1, int.MaxValue) },
				{ "LootRadius",             new ConstrainInteger(0, 100) },
				{ "MountDistance",          new ConstrainInteger(0, 200) },
				{ "TicksPerSecond",         new ConstrainInteger(5, 100) }
			};
			var noConstraintCheck = new NoConstraint();
			var recognizedSettings = new List<SettingDescriptor>();
			var settingsInstances = new Settings[]
				{   // ordering is significant--earlier setting names mask later setting names in this list
					CharacterSettings.Instance,
					LevelbotSettings.Instance,
					GlobalSettings.Instance
				};

			// Allowed 'Configuration' attributes--
			foreach (var settingsInstance in settingsInstances)
			{
				recognizedSettings.AddRange(
					from propertyInfo in settingsInstance.GetType().GetProperties()
					let customAttributes = propertyInfo.GetCustomAttributes(false)
					let propertyName = propertyInfo.Name
					let setter = propertyInfo.GetSetMethod()
					where
						(customAttributes.OfType<SettingAttribute>().Any()
						 || customAttributes.OfType<ObsoleteAttribute>().Any())
						&& setter != null && setter.IsPublic
					let constraintChecker = constraints.Keys.Contains(propertyName) ? constraints[propertyName] : noConstraintCheck
					select new SettingDescriptor(settingsInstance, propertyName, constraintChecker)
				);
			}

			return (new ReadOnlyCollection<SettingDescriptor>(recognizedSettings));
		}


		// Factories...
		public static ChangeSet FromCurrentConfiguration()
		{
			return new ChangeSet(
				RecognizedSettings
				.Where(setting => !setting.IsAccessDisallowed)
				.ToDictionary(setting => setting.Name, setting => setting.GetValue())
				);
		}


		// If 'null' return, then error was encountered, and offending messages already logged...
		// Otherwise, a (possibly empty) ChangeSet is returned.
		public static ChangeSet FromXmlAttributes(Dictionary<string, string> attributes)
		{
			try
			{
				var attributesToProcess =
				   (from attribute in attributes
					where
						RecognizedSettings.Any(setting => (setting.Name == attribute.Key) && !setting.IsAccessDisallowed)
					select attribute)
					.ToDictionary(attribute => attribute.Key, attribute => (object)attribute.Value);

				return new ChangeSet(attributesToProcess);
			}

			catch (Exception)
			{
				// empty
			}

			return null;
		}
	}


	#region ConstraintChecker classes
	public abstract class ConstraintChecker
	{
		protected ConstraintChecker(string description = null)
		{
			Description = description ?? string.Empty;
		}

		public string Description { get; private set; }

		public abstract bool IsWithinConstraints(object value);
	}


	public class ConstrainInteger : ConstraintChecker
	{
		public ConstrainInteger(int minValue, int maxValue)
			: base(string.Format("[{0}..{1}]", minValue, maxValue))
		{
			_maxValue = maxValue;
			_minValue = minValue;
		}

		private readonly int _maxValue;
		private readonly int _minValue;


		public override bool IsWithinConstraints(object value)
		{
			try
			{
				var newValue = (int)Convert.ChangeType(value, typeof(int));

				return (newValue >= _minValue) && (newValue <= _maxValue);
			}
			catch (Exception)
			{
				// empty
			}

			return false;
		}
	}


	public class NoConstraint : ConstraintChecker
	{
		public NoConstraint()
			: base(string.Empty)
		{
		}

		public override bool IsWithinConstraints(object value)
		{
			return true;
		}
	}
	#endregion


	/// <summary>
	/// Captures the details of a property that the user may alter.
	/// It provides generic Get/Set mechanics without regard of 'type'.
	/// </summary>
	public class SettingDescriptor
	{
		public SettingDescriptor(Settings settingsInstance,
								string name,
								ConstraintChecker constraintCheck)
		{
			// We are a bit aggressive in our error checking here--
			// The most likely source of errors will be people that maintain the code in the future,
			// and we want to weed out as many newbie mistakes as possible.
			Contract.Requires(!string.IsNullOrEmpty(name),
				context => "name cannot be null or empty.");
			Contract.Requires(settingsInstance != null,
				context => String.Format("Null settingsInstance now allowed for {0}", name));
			Contract.Requires(settingsInstance.GetType().GetProperties().Any(s => s.Name == name),
				context => string.Format("The settingsInstance does not contain a \"{0}\" property", name));

			ConstraintChecker = constraintCheck;
			Name = name;
			SettingsInstance = settingsInstance;

			InstanceName = settingsInstance.GetType().Name;
			PropInfo = settingsInstance.GetType().GetProperty(name);

			IsAccessDisallowed = DisallowedPropertyNames.Contains(name);

			var obsoleteAttribute = PropInfo.GetCustomAttributes(true).OfType<ObsoleteAttribute>().FirstOrDefault();
			IsObsolete = (obsoleteAttribute != null);

			ObsoleteMessage = IsObsolete ? obsoleteAttribute.Message : string.Empty;
		}

		public ConstraintChecker ConstraintChecker { get; private set; }
		public string InstanceName { get; private set; }
		public bool IsAccessDisallowed { get; private set; }
		public bool IsObsolete { get; private set; }
		public string Name { get; private set; }
		public string ObsoleteMessage { get; private set; }
		public Settings SettingsInstance { get; private set; }

		private PropertyInfo PropInfo { get; set; }

		public static IEnumerable<string> DisallowedPropertyNames
		{
			get
			{
				return _disallowedPropertyNames ?? (_disallowedPropertyNames = new List<string>()
					{
						// Disallowed CharacterSettings...
						"EnabledPlugins",
						"LastUsedPath",
						"MailRecipient",
						"RecentProfiles",
						"SelectedBotIndex",

						// Disallowed GlobalSettings...
						"AdvancedSettingsMode",
						"BotsPath",
						"CharacterSettingsDirectory",
						"CombatRoutinesPath",
						"MeshesFolderPath",
						"PluginsPath",
						"ProfileDebuggingMode",
						"QuestBehaviorsPath",
						"ReloadBotsOnFileChange",
						"ReloadPluginsOnFileChange",
						"ReloadRoutinesOnFileChange",
						"SeperatedLogFolders",
						"SettingsDirectory",
						"TicksPerSecond",
						"UICulture",
						"UseFrameLock",

						// Disallowed LevelbotSettings...
						// None for now.
					});
			}
		}
		private static IEnumerable<string> _disallowedPropertyNames;


		public object GetValue()
		{
			return
				IsAccessDisallowed
				? string.Format("<PROPERTY ACCESS OF '{0}' IS DISALLOWED>", Name)
				: PropInfo.GetValue(SettingsInstance, null);
		}


		public void SetValue(object newValueAsObject)
		{
			if (IsAccessDisallowed)
			{ return; }

			var newValue = ToCongruentObject(newValueAsObject);

			if (!ConstraintChecker.IsWithinConstraints(newValue))
			{
				var message = string.Format("For '{0}', provided value ('{1}') is not within required constraints {2}.",
											Name, newValue, ConstraintChecker.Description);
				QBCLog.Error(message);
				throw new ArgumentException(message);
			}

			PropInfo.SetValue(SettingsInstance, newValue, null);
		}


		// Largely used to convert 'string' representation of a value into the value's type...
		public object ToCongruentObject(object value)
		{
			var backingType = PropInfo.PropertyType;
			var providedType = value != null ? value.GetType() : backingType;

			try
			{
				Type underlyingType = Nullable.GetUnderlyingType(backingType);
				if (underlyingType != null)
				{
					// Nullables are treated specially as they can be assigned from a null reference or just an underlying value
					return value != null ? Convert.ChangeType(value, underlyingType) : null;
				}
				
				// Disallow int => bool conversions...
				// These are almost _always_ mistakes on the profile writer's part.
				if ((providedType == typeof(int) && (backingType == typeof(bool))))
				{ throw new ArgumentException(); }

				return Convert.ChangeType(value, backingType);
			}
			catch (Exception)
			{
				var message = string.Format("For setting '{0}', the provided value '{1}' ({2})"
											+ " cannot be converted to the backing type ({3}).",
											Name, value ?? "(NULL)", providedType.Name, backingType.Name);
				QBCLog.Error(message);
				throw new ArgumentException(message);
			}
		}
	}
}
