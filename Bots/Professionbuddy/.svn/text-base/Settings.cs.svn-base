using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml;
using HighVoltz.BehaviorTree;
using Styx;
using Styx.Common;
using Styx.Helpers;
using Styx.WoWInternals;

namespace HighVoltz.Professionbuddy
{
	public class ProfessionBuddySettings : Settings
	{
		public ProfessionBuddySettings(string settingsPath)
			: base(settingsPath)
		{
			Instance = this;
			Load();
		}

		public static ProfessionBuddySettings Instance { get; private set; }

		[Setting, DefaultValue("")]
		public string LastProfile { get; set; }

		[Setting, DefaultValue("")]
		public string LastBotBase { get; set; }
	}

	public class PbProfileSettingEntry
	{
		public object Value { get; set; }
		public string Summary { get; set; }
		public string Category { get; set; }
		public bool Global { get; set; }
		public bool Hidden { get; set; }
	}

	public class PbProfileSettings : IEnumerable
	{
		public PbProfileSettings()
		{
			SettingsDictionary = new Dictionary<string, PbProfileSettingEntry>();
		}

		public Dictionary<string, PbProfileSettingEntry> SettingsDictionary { get;private set; }

        public object this[string name]
        {
            get { return SettingsDictionary.ContainsKey(name) ? SettingsDictionary[name].Value : null; }
            set
            {
                SettingsDictionary[name].Value = value;
                if (ProfessionbuddyBot.Instance.CurrentProfile != null)
                {
                    Save();
                    if (MainForm.IsValid)
                        MainForm.Instance.RefreshSettingsPropertyGrid();
                }
            }
        }

        private string ProfileName
        {
            get
            {
                return ProfessionbuddyBot.Instance.CurrentProfile != null
                           ? Path.GetFileNameWithoutExtension(ProfessionbuddyBot.Instance.CurrentProfile.XmlPath)
                           : "";
            }
        }

		private string CharacterSettingsPath
		{
			get
			{
				return Path.Combine(Utilities.AssemblyDirectory,
									string.Format("Settings\\ProfessionBuddy\\{0}[{1}-{2}].xml", ProfileName,
												  StyxWoW.Me.Name,
												  Lua.GetReturnVal<string>("return GetRealmName()", 0)));
			}
		}

		private string GlobalSettingsPath
		{
			get
			{
				return Path.Combine(Utilities.AssemblyDirectory,
									string.Format("Settings\\ProfessionBuddy\\{0}.xml", ProfileName));
			}
		}

        public void Save()
        {
            if (ProfessionbuddyBot.Instance.CurrentProfile != null)
            {
                bool hasGlobalSettings = SettingsDictionary.Any(setting => setting.Value.Global);
                bool hasCharacterSettings = SettingsDictionary.Any(setting => !setting.Value.Global);
                if (hasGlobalSettings)
                    SaveGlobalSettings();
                if (hasCharacterSettings)
                    SaveCharacterSettings();
            }
        }

		private void SaveCharacterSettings()
		{
			var settings = new XmlWriterSettings {Indent = true};
			using (XmlWriter writer = XmlWriter.Create(CharacterSettingsPath, settings))
			{
				var serializer = new DataContractSerializer(typeof (Dictionary<string, object>));
				Dictionary<string, object> temp =
					SettingsDictionary.Where(setting => !setting.Value.Global).ToDictionary(kv => kv.Key, kv => kv.Value.Value);
				serializer.WriteObject(writer, temp);
			}
		}

		private void SaveGlobalSettings()
		{
			var settings = new XmlWriterSettings {Indent = true};
			using (XmlWriter writer = XmlWriter.Create(GlobalSettingsPath, settings))
			{
				var serializer = new DataContractSerializer(typeof (Dictionary<string, object>));
				Dictionary<string, object> temp =
					SettingsDictionary.Where(setting => setting.Value.Global).ToDictionary(kv => kv.Key, kv => kv.Value.Value);
				serializer.WriteObject(writer, temp);
			}
		}

        public void Load()
        {
            if (ProfessionbuddyBot.Instance.CurrentProfile != null)
            {
                SettingsDictionary = new Dictionary<string, PbProfileSettingEntry>();
                LoadCharacterSettings();
                LoadGlobalSettings();
                LoadDefaultValues();
            }
        }

        private void LoadCharacterSettings()
        {
            if (File.Exists(CharacterSettingsPath))
            {
                using (XmlReader reader = XmlReader.Create(CharacterSettingsPath))
                {
                    try
                    {
                        var serializer = new DataContractSerializer(typeof (Dictionary<string, object>));
                        var temp = (Dictionary<string, object>) serializer.ReadObject(reader);
                        if (temp != null)
                        {
                            foreach (var kv in temp)
                            {
                                SettingsDictionary[kv.Key] = new PbProfileSettingEntry { Value = kv.Value };
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        PBLog.Warn(ex.ToString());
                    }
                }
            }
        }

        private void LoadGlobalSettings()
        {
            if (File.Exists(GlobalSettingsPath))
            {
                using (XmlReader reader = XmlReader.Create(GlobalSettingsPath))
                {
                    try
                    {
                        var serializer = new DataContractSerializer(typeof (Dictionary<string, object>));
                        var temp = (Dictionary<string, object>) serializer.ReadObject(reader);
                        if (temp != null)
                        {
                            foreach (var kv in temp)
                            {
                                SettingsDictionary[kv.Key] = new PbProfileSettingEntry { Value = kv.Value };
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        PBLog.Warn(ex.ToString());
                    }
                }
            }
        }

        public void LoadDefaultValues()
        {
			List<Components.SettingsAction> settingsList = GetDefaultSettings(ProfessionbuddyBot.Instance.Branch);
            foreach (Components.SettingsAction setting in settingsList)
            {
                if (!SettingsDictionary.ContainsKey(setting.SettingName))
                    SettingsDictionary[setting.SettingName] = new PbProfileSettingEntry
                                                        {Value = GetValue(setting.Type, setting.DefaultValue)};
                SettingsDictionary[setting.SettingName].Summary = setting.Summary;
                SettingsDictionary[setting.SettingName].Category = setting.Category;
                SettingsDictionary[setting.SettingName].Global = setting.Global;
                SettingsDictionary[setting.SettingName].Hidden = setting.Hidden;
            }
            // remove unused settings..
            SettingsDictionary = SettingsDictionary.Where(kv => settingsList.Any(s => s.SettingName == kv.Key)).ToDictionary(kv => kv.Key,
                                                                                                         kv => kv.Value);
        }

		private object GetValue(TypeCode code, string value)
		{
			try
			{
				switch (code)
				{
					case TypeCode.Boolean:
						return bool.Parse(value);
					case TypeCode.Byte:
						return byte.Parse(value);
					case TypeCode.Char:
						return char.Parse(value);
					case TypeCode.DateTime:
						return DateTime.Parse(value);
					case TypeCode.Decimal:
						return decimal.Parse(value);
					case TypeCode.Double:
						return double.Parse(value);
					case TypeCode.Int16:
						return short.Parse(value);
					case TypeCode.Int32:
						return int.Parse(value);
					case TypeCode.Int64:
						return long.Parse(value);
					case TypeCode.SByte:
						return sbyte.Parse(value);
					case TypeCode.Single:
						return float.Parse(value);
					case TypeCode.String:
						return value;
					case TypeCode.UInt16:
						return ushort.Parse(value);
					case TypeCode.UInt32:
						return uint.Parse(value);
					case TypeCode.UInt64:
						return ulong.Parse(value);
					default:
						return new object();
				}
			}
			catch (Exception ex)
			{
				Logging.WriteException(ex);
				return null;
			}
		}

        public List<Components.SettingsAction> GetDefaultSettings(Component comp)
        {
            var list = new List<Components.SettingsAction>();
            GetProfileSettings(comp, ref list);
            return list;
        }

        // recursively get all profile settings
        private void GetProfileSettings(Component comp, ref List<Components.SettingsAction> list)
        {
            if (comp is Components.SettingsAction)
                list.Add(comp as Components.SettingsAction);
            if (comp is Composite)
            {
                foreach (var child in ((Composite) comp).Children)
                    GetProfileSettings(child, ref list);
            }
        }

		IEnumerator IEnumerable.GetEnumerator()
		{
			return SettingsDictionary.GetEnumerator();
		}   
	}
}