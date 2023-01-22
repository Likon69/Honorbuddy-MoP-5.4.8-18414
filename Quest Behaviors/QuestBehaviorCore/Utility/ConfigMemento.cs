// Originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Bots.Grind;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Profiles.Quest;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;

#endregion


namespace Honorbuddy.QuestBehaviorCore
{
	public class ConfigMemento : IDisposable
	{
		/// <summary>
		/// The ConfigMemento() class captures the user's existing configuration.
		/// After its captured, we can change the configuration however needed.
		/// When the memento is dispose'd, the user's original configuration is restored.
		/// More info about how the ConfigMemento applies to saving and restoring user configuration
		/// can be found here...
		///     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_Saving_and_Restoring_User_Configuration
		/// </summary>
		public ConfigMemento()
		{
			_subMementos = new List<ISubMemento>()
				{
					new CharacterSettingsSubMemento(),
					new GlobalSettingsSubMemento(),
					new LevelBotSubMemento(),
					new NavigatorSubMemento(),
					new ProfileSettingsSubMemento(),
					new RoutineManagerSubMemento()
				};
		}


		#region Private and Convenience variables
		private bool _isDisposed = false;
		private readonly List<ISubMemento> _subMementos;
		#endregion


		/// <summary>
		/// Disposing of a memento restores the Honorbuddy configuration that existed when the memento
		/// was created.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}


		/// <summary>
		/// Disposing of a memento restores the Honorbuddy configuration that existed when the memento
		/// was created.
		/// </summary>
		///
		/// <param name="isExplicitlyInitiatedDispose"> true if this object is explicitly initiated
		///                                             dispose. </param>
		private void Dispose(bool isExplicitlyInitiatedDispose)
		{
			if (!_isDisposed)
			{
				// NOTE: we should call any Dispose() method for any managed or unmanaged
				// resource, if that resource provides a Dispose() method.

				// Clean up managed resources, if explicit disposal...
				if (isExplicitlyInitiatedDispose)
				{
					// empty for now
				}

				// Clean up unmanaged resources (if any) here...

				// Clean up sub-mementos
				foreach (var memento in _subMementos)
				{
					memento.Restore();
				}
				_subMementos.Clear();

				// Call parent Dispose() (if it exists) here ...
				// base.Dispose();
			}

			_isDisposed = true;
		}


		/// <summary>
		/// Returns a <see cref="T:System.String" /> that represents the current
		/// <see cref="T:System.Object" />.
		/// </summary>
		///
		/// <exception cref="ObjectDisposedException">  Thrown when a supplied object has been disposed. </exception>
		///
		/// <returns>
		/// A <see cref="T:System.String" /> that represents the current <see cref="T:System.Object" />.
		/// </returns>
		public override string ToString()
		{
			if (_isDisposed)
				{ throw (new ObjectDisposedException(this.GetType().Name)); }

			var root = new XElement("ConfigMemento");
			foreach (var memento in _subMementos)
			{
				memento.AddXml(root);
			}

			return (SortedChildren(root).ToString());
		}


		// Sorts the child elements, and eliminates comments...
		private static XElement SortedChildren(XElement element)
		{
			return
				new XElement(element.Name,
					// include attributes of current node...
					element.Attributes(),

					// include all the non-Element children that are not comments...
					from child in element.Nodes()
					where
						!((child.NodeType == XmlNodeType.Element)
						  || (child.NodeType == XmlNodeType.Comment))
					select child,

					// include child XElements in a sorted fashion...
					from child in element.Elements()
					where
						// Do not emit any children that may cause security concerns...
						!((child.Name == "MailRecipient")
						  || (child.Name == "Username"))
					orderby child.Name.ToString()
					select SortedChildren(child)
					);
		}


		// Sub-mementos don't have to handle 'dispose' considerations.  We leave this for
		// the parent (ConfigMemento).  This keeps sub-momentos focused on their immediate
		// task without cluttering up code with unnecessary distractions.  The interface
		// is designed accordingly.
		private interface ISubMemento
		{
			void AddXml(XElement parent);
			void Restore();
		}


		private class CharacterSettingsSubMemento : ISubMemento
		{
			private readonly XElement _settings;

			public CharacterSettingsSubMemento()
			{
				_settings = CharacterSettings.Instance.GetXML();
			}


			public void Restore()
			{
				CharacterSettings.Instance.LoadFromXML(_settings);
				CharacterSettings.Instance.Save();
			}


			public void AddXml(XElement parent)
			{
				parent.Add(_settings);
			}
		}


		private class GlobalSettingsSubMemento : ISubMemento
		{
			private readonly XElement _settings;

			public GlobalSettingsSubMemento()
			{
				_settings = GlobalSettings.Instance.GetXML();
			}


			public void Restore()
			{
				GlobalSettings.Instance.LoadFromXML(_settings);
				GlobalSettings.Instance.Save();
			}


			public void AddXml(XElement parent)
			{
				parent.Add(_settings);
			}
		}


		// This memento captures both the settings embedded in the LevelBot proper,
		// and the settings in LevelbotSettings.
		private class LevelBotSubMemento : ISubMemento
		{
			private readonly BehaviorFlags _behaviorFlags;
			private readonly XElement _settings;
			private readonly bool _shouldUseSpiritHealer;

			public LevelBotSubMemento()
			{
				// NB: Not all LevelBot settings are in LevelBotSettings--some are in the
				// LevelBot itself.  
				_behaviorFlags = LevelBot.BehaviorFlags;
				_shouldUseSpiritHealer = LevelBot.ShouldUseSpiritHealer;

				// Settings associated with Levelbot...
				_settings = LevelbotSettings.Instance.GetXML();
			}


			public void Restore()
			{
				// Settings from Levelbot proper...
				LevelBot.BehaviorFlags = _behaviorFlags;
				LevelBot.ShouldUseSpiritHealer = _shouldUseSpiritHealer;

				// Settings associated with Levelbot...
				LevelbotSettings.Instance.LoadFromXML(_settings);
				LevelbotSettings.Instance.Save();
			}


			public void AddXml(XElement parent)
			{
				// Levelbot proper...
				parent.Add(
					new XElement("LevelBot",
						new XElement("BehaviorFlags", _behaviorFlags),
						new XElement("ShouldUseSpiritHealer", _shouldUseSpiritHealer)
						)
					);

				// LevelbotSettings...
				parent.Add(_settings);
			}
		}


		// This memento captures the settings embedded in the Navigator
		// that a quest behavior is interested in altering.
		private class NavigatorSubMemento : ISubMemento
		{
			private readonly float _pathPrecision;

			public NavigatorSubMemento()
			{
				_pathPrecision = Navigator.PathPrecision;
			}


			public void Restore()
			{
				Navigator.PathPrecision = _pathPrecision;
			}


			public void AddXml(XElement parent)
			{
				parent.Add(
					new XElement("Navigator",
						new XElement("PathPrecision", _pathPrecision)
						)
					);
			}
		}
		
		
		// This class captures any profile settings that can be directly modified (i.e.,
		// that have a 'setter' operation defined for them), or indirectly modified
		// (i.e., they directly expose their internal representation).
		private class ProfileSettingsSubMemento : ISubMemento
		{
			private readonly Dictionary<Tuple<uint, WoWFactionGroup>, List<Vector2[]>> _aerialBlackspots;
			private readonly DualHashSet<uint, string> _avoidMobs;
			private readonly Dictionary<uint, BlacklistFlags> _blacklist;
			private readonly DualHashSet<string, uint> _blacklistQuestgivers;
			private readonly DualHashSet<string, uint> _blacklistedQuests;
			private readonly List<Blackspot> _blackspots;
			// IMMUTABLE: private readonly int _continentId;
			private readonly HashSet<uint> _factions;
			private readonly DualHashSet<uint, string> _forceMail;
			// IMMUTABLE: private readonly Styx.CommonBot.AreaManagement.GrindArea _grindArea;
			// NON-CONFIGURATION: private readonly Styx.CommonBot.AreaManagement.HotspotManager _hotspotManager;
			private readonly bool? _lootMobs;
			private readonly double? _lootRadius;
			// IMMUTABLE: private readonly bool _mailBlue;
			// NON-CONFIGURATION: private readonly MailboxManager _mailboxManager;
			// IMMUTABLE: private readonly bool _mailGreen;
			// IMMUTABLE: private readonly bool _mailGrey;
			// IMMUTABLE: private readonly bool _mailPurple;
			private readonly List<WoWItemQuality> _mailQualities;
			// IMMUTABLE: private readonly bool _mailWhite;
			// IMMUTABLE: private readonly int _maxLevel;
			// IMMUTABLE: private readonly float _minDurability;
			// IMMUTABLE: private readonly int _minFreeBagSlots;
			// IMMUTABLE: private readonly int _minLevel;
			// IMMUTABLE: private readonly int _minMailLevel;
			private readonly HashSet<uint> _mobIds;
			// IMMUTABLE: private readonly string _name;
			// NON-CONFIGURATION: private readonly Styx.CommonBot.Profiles.Profile _parent;
			private readonly DualHashSet<uint, string> _protectedItems;
			// IMMUTABLE: private readonly Styx.CommonBot.Profiles.Quest.Order.OrderNodeCollection _questOrder;
			private readonly List<QuestInfo> _quests;
			// IMMUTABLE: private readonly bool _sellBlue;
			// IMMUTABLE: private readonly bool _sellGreen;
			// IMMUTABLE: private readonly bool _sellGrey;
			// IMMUTABLE: private readonly bool _sellPurple;
			// IMMUTABLE: private readonly bool _sellWhite;
			private readonly List<Profile> _subProfiles;
			// IMMUTABLE: private readonly bool _targetElites;
			private readonly double? _targetingDistance;
			// IMMUTABLE: private readonly int _targetMaxLevel;
			// IMMUTABLE: private readonly int _targetMinLevel;
			private readonly bool? _useMount;
			// NON-CONFIGURATION: private readonly VendorManager _vendorManager;
			// NON-CONFIGURATION: private readonly XmlElement _xmlElement;


			public ProfileSettingsSubMemento()
			{
				var currentProfile = ProfileManager.CurrentProfile;

				if (currentProfile != null)
				{
					// NB: We can't use reflection, because not all data members are 'configuration items'.
					// Of the configuration items, not all of them are mutable.  And of the mutable ones,
					// some are directly mutable, and others are indirectly mutable. (So, different techniques
					// must be used).
					_aerialBlackspots = new Dictionary<Tuple<uint, WoWFactionGroup>, List<Vector2[]>>(currentProfile.AerialBlackspots);
					_avoidMobs = currentProfile.AvoidMobs.Clone();
					_blacklist = new Dictionary<uint, BlacklistFlags>(currentProfile.Blacklist);
					_blacklistQuestgivers = currentProfile.BlacklistedQuestgivers.Clone();
					_blacklistedQuests = currentProfile.BlacklistedQuests.Clone();
					_blackspots = new List<Blackspot>(currentProfile.Blackspots);
					_factions = new HashSet<uint>(currentProfile.Factions);
					_forceMail = currentProfile.ForceMail.Clone();
					_lootMobs = currentProfile.LootMobs;
					_lootRadius = currentProfile.LootRadius;
					_mailQualities = new List<WoWItemQuality>(currentProfile.MailQualities);
					_mobIds = new HashSet<uint>(currentProfile.MobIDs);
					_protectedItems = currentProfile.ProtectedItems.Clone();
					_quests = new List<QuestInfo>(currentProfile.Quests);
					_subProfiles = new List<Profile>(currentProfile.SubProfiles);
					_targetingDistance = currentProfile.TargetingDistance;
					_useMount = currentProfile.UseMount;
				}
			}


			public void Restore()
			{
				var currentProfile = ProfileManager.CurrentProfile;

				if (currentProfile != null)
				{
					// NB: We can't use reflection, because not all data members are 'configuration items'.
					// Of the configuration items, not all of them are mutable.  And of the mutable ones,
					// some are directly mutable, and others are indirectly mutable. (So, different techniques
					// must be used).
					// NB: We depend on HBcore to suppress the logging of any setting for which the 'new'
					// value is the same as the 'current' value.
					currentProfile.AerialBlackspots.CopyFrom(_aerialBlackspots);
					currentProfile.AvoidMobs.CopyFrom(_avoidMobs);
					currentProfile.Blacklist.CopyFrom(_blacklist);
					currentProfile.BlacklistedQuestgivers.CopyFrom(_blacklistQuestgivers);
					currentProfile.BlacklistedQuests.CopyFrom(_blacklistedQuests);
					currentProfile.Blackspots.CopyFrom(_blackspots);
					currentProfile.Factions.CopyFrom(_factions);
					currentProfile.ForceMail.CopyFrom(_forceMail);
					currentProfile.LootMobs = _lootMobs;
					currentProfile.LootRadius = _lootRadius;
					currentProfile.MailQualities.CopyFrom(_mailQualities);
					currentProfile.MobIDs.CopyFrom(_mobIds);
					currentProfile.ProtectedItems.CopyFrom(_protectedItems);
					currentProfile.Quests.CopyFrom(_quests);
					currentProfile.SubProfiles.CopyFrom(_subProfiles);
					currentProfile.TargetingDistance = _targetingDistance;
					currentProfile.UseMount = _useMount;
				}
			}


			public void AddXml(XElement parent)
			{
				// The 'vector' sub-elements are largely uninteresting and 'noise', so we do not
				// emit them.
				parent.Add(
					new XElement("ProfileSettings",
							// currentProfile.AerialBlackspots.CopyFrom(_aerialBlackspots);
							// currentProfile.AvoidMobs.CopyFrom(_avoidMobs);
							// currentProfile.Blacklist.CopyFrom(_blacklist);
							// currentProfile.BlacklistedQuestgivers.CopyFrom(_blacklistQuestgivers);
							// currentProfile.BlacklistedQuests.CopyFrom(_blacklistedQuests);
							// currentProfile.Blackspots.CopyFrom(_blackspots);
							// currentProfile.Factions.CopyFrom(_factions);
							// currentProfile.ForceMail.CopyFrom(_forceMail);
							new XElement("LootMobs",
								new XAttribute("Value", (object)_lootMobs ?? "null")
								),
							new XElement("LootRadius",
								new XAttribute("Value", (object)_lootRadius ?? "null")
								),
							// currentProfile.MailQualities.CopyFrom(_mailQualities);
							// currentProfile.MobIDs.CopyFrom(_mobIds);
							// currentProfile.ProtectedItems.CopyFrom(_protectedItems);
							// currentProfile.Quests.CopyFrom(_quests);
							// currentProfile.SubProfiles.CopyFrom(_subProfiles);
							new XElement("TargetingDistance",
								new XAttribute("Value", (object)_targetingDistance ?? "null")
								),
							new XElement("UseMount",
								new XAttribute("Value", (object)_useMount ?? "null")
								)
						)
					);
			}
		}


		private class RoutineManagerSubMemento : ISubMemento
		{
			private readonly Dictionary<CapabilityFlags, CapabilityState> _capabilityStates = new Dictionary<CapabilityFlags, CapabilityState>();

			public RoutineManagerSubMemento()
			{
				// We explicitly enumerate the CapabilityFlags...
				// This eliminates the possiblity of wrongly snagging "composite flags" (like .All).
				for (var i = 0; i < 32; i++)
				{
					var capabilityFlag = (CapabilityFlags)(1u << i);
					if (!Enum.IsDefined(typeof(CapabilityFlags), capabilityFlag))
						continue;

					_capabilityStates.Add(capabilityFlag, RoutineManager.GetCapabilityState(capabilityFlag));
				}
			}


			public void Restore()
			{
				foreach (var capabilityState in _capabilityStates)
				{
					RoutineManager.SetCapabilityState(capabilityState.Key, capabilityState.Value);
				}
			}


			public void AddXml(XElement parent)
			{
				var routineManager = new XElement("RoutineManager");
				var capabilities = new XElement("Capabilities");

				foreach (var capabilityState in _capabilityStates.OrderBy(kvp => kvp.Key.ToString()))
				{
					capabilities.Add(new XElement(capabilityState.Key.ToString(), capabilityState.Value));
				}

				routineManager.Add(capabilities);
				parent.Add(routineManager);
			}
		}
	}
}
