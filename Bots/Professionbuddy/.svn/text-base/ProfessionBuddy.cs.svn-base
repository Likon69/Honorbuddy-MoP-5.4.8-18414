//!CompilerOption:Optimize:On
// Professionbuddy botbase by HighVoltz

// SVN http://professionbuddy.googlecode.com/svn/trunk/Professionbuddy/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media;
using System.Xml.Linq;
using CommonBehaviors.Actions;
using HighVoltz.Professionbuddy.Components;
using HighVoltz.Professionbuddy.Dynamic;
using Styx;
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = System.Action;
using Color = System.Drawing.Color;
using Vector3 = Tripper.Tools.Math.Vector3;

namespace HighVoltz.Professionbuddy
{
	public class ProfessionbuddyBot : BotBase
	{
		#region Static Members.

		public static readonly string BotPath = GetProfessionbuddyPath();
		public static readonly Svn Svn = new Svn();
		public static readonly string ProfilePath = Path.Combine(BotPath, "Profiles");
		// static instance
		public static ProfessionbuddyBot Instance { get; private set; }
		#endregion

		#region Fields

		private Version _version;
		private readonly bool _ctorRunOnce;
		private readonly Dictionary<uint, int> _materialList = new Dictionary<uint, int>();
		private readonly PbProfileSettings _profileSettings = new PbProfileSettings();
		private List<TradeSkill> _tradeSkillList;

		// Used as a fix when profile is loaded before Inititialize is called
		private static string _profileToLoad = "";

		#endregion

		#region Constructor

		public ProfessionbuddyBot()
		{
			Instance = this;
			// Initialize is called when bot is started.. we need to hook these events before that.
			if (!_ctorRunOnce)
			{
				BotEvents.Profile.OnNewOuterProfileLoaded += Profile_OnNewOuterProfileLoaded;
				Profile.OnUnknownProfileElement += Profile_OnUnknownProfileElement;
				_ctorRunOnce = true;
				CurrentProfile = PbProfile.EmptyProfile;
			}
		}
		#endregion

		#region Events

		public static event EventHandler<ConfigurationFormCreatedArg> ConfigurationFormCreated;
		public event EventHandler OnTradeSkillsLoaded;

		#endregion

		#region Properties

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		public ProfessionBuddySettings MySettings { get; private set; }

		internal List<uint> TradeskillTools { get; private set; }

		public bool IsRunning { get; internal set; }

		/// <summary>
		/// Gets a value indicating whether [skip branch reset on startup]. This is used when Honorbudy is stopped 
		/// to execute a action that can only be safely executed while not running
		/// </summary>
		public static bool IsExecutingActionWhileHonorbuddyIsStopped { get; internal set; }

		public List<TradeSkill> TradeSkillList
		{
			get
			{
				lock (tradeSkillLocker)
				{
					return _tradeSkillList;
				}
			}
			private set { _tradeSkillList = value; }
		}

		// dictionary that keeps track of material list using item ID for key and number required as value
		public Dictionary<uint, int> MaterialList
		{
			get
			{
				lock (materialLocker)
				{
					return _materialList;
				}
			}
		}

		public Dictionary<string, string> Strings { get; private set; }

		// <itemId,count>
		public DataStore DataStore { get; private set; }
		public bool IsTradeSkillsLoaded { get; private set; }

		// ReSharper disable InconsistentNaming
		// DataStore is an addon for WOW thats stores bag/ah/mail item info and more.
		public bool HasDataStoreAddon
		{
			get { return DataStore != null && DataStore.HasDataStoreAddon; }
		}

		public PbProfileSettings ProfileSettings
		{
			get { return _profileSettings; }
		}

		public Version Version
		{
			get { return _version ?? (_version = new Version(1, Svn.Revision)); }
		}

		#endregion

		#region Overrides

		private MainForm _gui;

		public override string Name
		{
			get { return "ProfessionBuddy"; }
		}

		public override PulseFlags PulseFlags
		{
			get { return PulseFlags.All; }
		}

		public override Form ConfigurationForm
		{
			get
			{
				if (!_init)
					Initialize();
				if (!MainForm.IsValid)
				{
					_gui = new MainForm();
					EventHandler<ConfigurationFormCreatedArg> handler = ConfigurationFormCreated;
					if (handler != null)
						handler(this, new ConfigurationFormCreatedArg(_gui));
				}
				else
				{
					_gui.Activate();
				}
				return _gui;
			}
		}

		public override void Start()
		{
			PBLog.Debug("Start Called");
			IsRunning = true;
			AttachEvents();
			// make sure bank frame is closed on start to ensure Util.IsGBankFrameOpen is synced
			Util.CloseBankFrames();

			if (IsExecutingActionWhileHonorbuddyIsStopped)
			{
				IsExecutingActionWhileHonorbuddyIsStopped = false;
				ResetSecondaryBot();
			}
			else
			{
				// reset all actions 
				Reset();
				if (DynamicCodeCompiler.CodeIsModified)
				{
					DynamicCodeCompiler.GenorateDynamicCode();
				}
			}

			if (MainForm.IsValid)
				MainForm.Instance.UpdateControls();

			if (!SecondaryBot.Initialized)
				SecondaryBot.DoInitialize();
		}

		public override void Stop()
		{
			IsRunning = false;
			DetachEvents();

			PBLog.Debug("Stop Called");
			if (MainForm.IsValid)
				MainForm.Instance.UpdateControls();
			if (SecondaryBot != null)
				SecondaryBot.Stop();
		}

		public override void Pulse()
		{

			if (SecondaryBot != null)
				SecondaryBot.Pulse();
		}

		public override void Initialize()
		{
			try
			{
				if (!_init)
				{
					PBLog.Debug("Initializing ...");
					Util.ScanForOffsets();
					if (!Directory.Exists(BotPath))
						Directory.CreateDirectory(BotPath);
					DynamicCodeCompiler.WipeTempFolder();
					// force Tripper.Tools.dll to load........
					new Vector3(0, 0, 0);

					MySettings =
						new ProfessionBuddySettings(
							Path.Combine(
								Utilities.AssemblyDirectory,
								string.Format(@"Settings\{0}\{0}[{1}-{2}].xml", Name, Me.Name, Lua.GetReturnVal<string>("return GetRealmName()", 0))));

					IsTradeSkillsLoaded = false;
					LoadTradeSkills();
					DataStore = new DataStore();
					DataStore.ImportDataStore();
					LoadTradeskillTools();
					// load localized strings
					LoadStrings();
					
					// load the previous 
					BotBase bot =
						BotManager.Instance.Bots.Values.FirstOrDefault(
							b => b.Name.IndexOf(MySettings.LastBotBase, StringComparison.InvariantCultureIgnoreCase) >= 0);

					if (bot == null)
					{
						// look for combat bot, otherwise select first bot if combat bot is not found
						bot = BotManager.Instance.Bots.Values.FirstOrDefault(b => b.GetType().ToString() == "CombatBot") 
							?? BotManager.Instance.Bots.Values.FirstOrDefault();
						MySettings.LastBotBase = bot.Name;
						MySettings.Save();
					}
					SecondaryBot = bot;

					bot.DoInitialize();

					try
					{
						if (!string.IsNullOrEmpty(_profileToLoad))
						{
							LoadPBProfile(_profileToLoad);
						}
						else if (!string.IsNullOrEmpty(MySettings.LastProfile))
						{
							LoadPBProfile(MySettings.LastProfile);
						}
					}
					catch (Exception ex)
					{
						PBLog.Warn(ex.ToString());
					}

					_init = true;
				}
			}
			catch (Exception ex)
			{
				Logging.Write(Colors.Red, ex.ToString());
			}
		}

		#endregion

		#region Callbacks

		#region OnBagUpdate

		private readonly WaitTimer _onBagUpdateTimer = new WaitTimer(TimeSpan.FromSeconds(1));

		private void OnBagUpdate(object obj, LuaEventArgs args)
		{
			if (_onBagUpdateTimer.IsFinished)
			{
				try
				{
					lock (tradeSkillLocker)
					{
						foreach (TradeSkill ts in TradeSkillList)
						{
							ts.PulseBags();
						}
						UpdateMaterials();
						if (MainForm.IsValid)
						{
							MainForm.Instance.RefreshTradeSkillTabs();
							MainForm.Instance.RefreshActionTree(typeof(CastSpellAction));
						}
					}
				}
				catch (Exception ex)
				{
					PBLog.Warn(ex.ToString());
				}
				_onBagUpdateTimer.Reset();
			}
		}

		#endregion

		#region OnSkillUpdate

		private readonly WaitTimer _onSkillUpdateTimer = new WaitTimer(TimeSpan.FromSeconds(1));

		private void OnSkillUpdate(object obj, LuaEventArgs args)
		{
			if (_onSkillUpdateTimer.IsFinished)
			{
				try
				{
					lock (tradeSkillLocker)
					{
						UpdateMaterials();
						// check if there was any tradeskills added or removed.
						WoWSkill[] skills = SupportedTradeSkills;
						bool changed = skills.Count(s => TradeSkillList.Count(l => l.SkillLine == (SkillLine)s.Id) == 1) !=
										TradeSkillList.Count ||
										skills.Length != TradeSkillList.Count;
						if (changed)
						{
							PBLog.Debug("A profession was added or removed. Reloading Tradeskills (OnSkillUpdateTimerCB)");
							OnTradeSkillsLoaded += Professionbuddy_OnTradeSkillsLoaded;
							LoadTradeSkills();
						}
						else
						{
							PBLog.Debug("Updated tradeskills from OnSkillUpdateTimerCB");
							foreach (TradeSkill ts in TradeSkillList)
							{
								ts.PulseSkill();
							}
							if (MainForm.IsValid)
							{
								MainForm.Instance.RefreshTradeSkillTabs();
								MainForm.Instance.RefreshActionTree(typeof(CastSpellAction));
							}
						}
					}
				}
				catch (Exception ex)
				{
					PBLog.Warn(ex.ToString());
				}
				_onSkillUpdateTimer.Reset();
			}
		}

		#endregion

		public void Professionbuddy_OnTradeSkillsLoaded(object sender, EventArgs e)
		{
			if (MainForm.IsValid)
				MainForm.Instance.InitTradeSkillTab();
			OnTradeSkillsLoaded -= Professionbuddy_OnTradeSkillsLoaded;
		}

		private void Profile_OnUnknownProfileElement(object sender, UnknownProfileElementEventArgs e)
		{
			if (e.Element.Ancestors("Professionbuddy").Any())
			{
				e.Handled = true;
			}
		}

		private static void Profile_OnNewOuterProfileLoaded(BotEvents.Profile.NewProfileLoadedEventArgs args)
		{
			var xmlElement = args.NewProfile.XmlElement;

			if (!Util.IsProfessionbuddyProfile(xmlElement))
				return;

			// prevents HB from reloading current profile when bot is started.
			if (!Instance.IsRunning && ProfileManager.XmlLocation == Instance.CurrentProfile.XmlPath)
				return;

			if (_init)
			{
				var loadProfile = new Action(
					() =>
					{
						LoadPBProfile(ProfileManager.XmlLocation, args.NewProfile.XmlElement);
						if (MainForm.IsValid)
						{
							MainForm.Instance.ActionTree.SuspendLayout();
							if (Instance.ProfileSettings.SettingsDictionary.Count > 0)
								MainForm.Instance.AddProfileSettingsTab();
							else
								MainForm.Instance.RemoveProfileSettingsTab();
							MainForm.Instance.ActionTree.ResumeLayout();
						}
					});

				if (Instance.IsRunning && TreeRoot.IsRunning)
				{
					Util.ExecuteActionWhileBotIsStopped(loadProfile,
						"Changing profiles (Profile_OnNewOuterProfileLoaded)");
				}
				else
				{
					loadProfile();
				}
			}
			else
			{
				_profileToLoad = ProfileManager.XmlLocation;
			}
		}

		#region OnSpellsChanged

		private readonly WaitTimer _onSpellsChangedTimer = new WaitTimer(TimeSpan.FromSeconds(1));

		private void OnSpellsChanged(object obj, LuaEventArgs args)
		{
			if (_onSpellsChangedTimer.IsFinished)
			{
				try
				{
					foreach (TradeSkill ts in TradeSkillList)
					{
						ts.PulseSkill();
					}
					if (MainForm.IsValid)
					{
						MainForm.Instance.InitTradeSkillTab();
						MainForm.Instance.RefreshActionTree(typeof(CastSpellAction));
					}
				}
				catch (Exception ex)
				{
					PBLog.Warn(ex.ToString());
				}
				_onSpellsChangedTimer.Reset();
			}
		}

		#endregion

		#endregion

		#region Behavior Tree

		private Composite _root;
		private BotBase _secondaryBot;
		private bool _calledStart;

		public PbProfile CurrentProfile { get; private set; }

		public override Composite Root
		{
			get { return _root ?? 
				(_root = new ActionRunCoroutine(
					async ctx => await Branch || await ExecuteSecondaryBot())); }
		}


		public PBBranch Branch
		{
			get { return CurrentProfile.Branch; }
		}

		public BotBase SecondaryBot
		{
			get { return _secondaryBot; }
			set
			{
				if (_secondaryBot == value)
					return;

				_secondaryBot = value;
				_calledStart = false;
			}
		}

		public async Task<bool> ExecuteSecondaryBot()
		{
			if (_secondaryBot == null || _secondaryBot.Root == null)
				return false;

			if (!_calledStart)
			{
				try
				{
					_secondaryBot.Start();
				}
				finally
				{
					_calledStart = true;
				}
			}
			return await _secondaryBot.Root.ExecuteCoroutine();
		}

		public void Reset()
		{
			ResetBranch();
			ResetSecondaryBot();
		}

		public void ResetBranch()
		{
			Branch.Reset();
		}

		public void ResetSecondaryBot()
		{
			_calledStart = false;
		}

		#endregion

		#region Misc

		private static bool _init;
		private static readonly object tradeSkillLocker = new object();
		private static readonly object materialLocker = new object();


		private WoWSkill[] SupportedTradeSkills
		{
			get
			{
				IEnumerable<WoWSkill> skillList = from skill in TradeSkill.SupportedSkills
												  where Me.GetSkill(skill).MaxValue > 0
												  select Me.GetSkill(skill);

				return skillList.ToArray();
			}
		}

		private void LoadStrings()
		{
			Strings = new Dictionary<string, string>();
			string directory = Path.Combine(BotPath, "Localization");
			string defaultStringsPath = Path.Combine(directory, "Strings.xml");
			LoadStringsFromXml(defaultStringsPath);
			// file that includes language and country/region
			string langAndCountryFile = Path.Combine(directory, "Strings." + Thread.CurrentThread.CurrentUICulture.Name + ".xml");
			// file that includes language only;
			string langOnlyFile = Path.Combine(
				directory,
				"Strings." + Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName + ".xml");
			if (File.Exists(langAndCountryFile))
			{
				PBLog.Log("Loading strings for language {0}", Thread.CurrentThread.CurrentUICulture.Name);
				LoadStringsFromXml(langAndCountryFile);
			}
			else if (File.Exists(langOnlyFile))
			{
				PBLog.Log("Loading strings for language {0}", Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName);
				LoadStringsFromXml(langOnlyFile);
			}
		}

		private void LoadStringsFromXml(string path)
		{
			XElement root = XElement.Load(path);
			foreach (XElement element in root.Elements())
			{
				Strings[element.Name.ToString()] = element.Value;
			}
		}

		void AttachEvents()
		{
			Lua.Events.AttachEvent("BAG_UPDATE", OnBagUpdate);
			Lua.Events.AttachEvent("SKILL_LINES_CHANGED", OnSkillUpdate);
			Lua.Events.AttachEvent("SPELLS_CHANGED", OnSpellsChanged);

			Lua.Events.AttachEvent("GUILDBANKFRAME_OPENED", Util.OnGBankFrameOpened);
			Lua.Events.AttachEvent("GUILDBANKFRAME_CLOSED", Util.OnGBankFrameClosed);

			Lua.Events.AttachEvent("BANKFRAME_OPENED", Util.OnBankFrameOpened);
			Lua.Events.AttachEvent("BANKFRAME_CLOSED", Util.OnBankFrameClosed);
		}

		void DetachEvents()
		{
			Lua.Events.DetachEvent("BAG_UPDATE", OnBagUpdate);
			Lua.Events.DetachEvent("SKILL_LINES_CHANGED", OnSkillUpdate);
			Lua.Events.DetachEvent("SPELLS_CHANGED", OnSpellsChanged);

			Lua.Events.DetachEvent("GUILDBANKFRAME_OPENED", Util.OnGBankFrameOpened);
			Lua.Events.DetachEvent("GUILDBANKFRAME_CLOSED", Util.OnGBankFrameClosed);

			Lua.Events.DetachEvent("BANKFRAME_OPENED", Util.OnBankFrameOpened);
			Lua.Events.DetachEvent("BANKFRAME_CLOSED", Util.OnBankFrameClosed);
		}

		public void LoadTradeSkills()
		{
			var newTradeSkills = new List<TradeSkill>();
			try
			{
				using (StyxWoW.Memory.AcquireFrame())
				{
					foreach (WoWSkill skill in SupportedTradeSkills)
					{
						PBLog.Log("Adding TradeSkill {0}", skill.Name);
						TradeSkill ts = TradeSkill.GetTradeSkill((SkillLine)skill.Id);
						if (ts != null)
						{
							newTradeSkills.Add(ts);
						}
						else
						{
							IsTradeSkillsLoaded = false;
							PBLog.Log("Unable to load tradeskill {0}", (SkillLine)skill.Id);
							return;
						}
					}
				}

			}
			catch (Exception ex)
			{
				Logging.Write(Colors.Red, ex.ToString());
			}
			finally
			{
				lock (tradeSkillLocker)
				{
					TradeSkillList = newTradeSkills;
				}
				PBLog.Log("Done Loading Tradeskills.");
				IsTradeSkillsLoaded = true;
				if (OnTradeSkillsLoaded != null)
				{
					OnTradeSkillsLoaded(this, null);
				}
			}
		}

		public void UpdateMaterials()
		{
			if (!_init)
				return;
			try
			{
				lock (materialLocker)
				{
					_materialList.Clear();
					List<CastSpellAction> castSpellList = CastSpellAction.GetCastSpellActionList(Branch);
					if (castSpellList != null)
					{
						foreach (CastSpellAction ca in castSpellList)
						{
							if (ca.IsRecipe && ca.RepeatType != CastSpellAction.RepeatCalculationType.Craftable)
							{
								foreach (Ingredient ingred in ca.Recipe.Ingredients)
								{
									_materialList[ingred.ID] = _materialList.ContainsKey(ingred.ID)
										? _materialList[ingred.ID] +
										(ca.CalculatedRepeat > 0 ? (int)ingred.Required * (ca.CalculatedRepeat - ca.Casted) : 0)
										: (ca.CalculatedRepeat > 0 ? (int)ingred.Required * (ca.CalculatedRepeat - ca.Casted) : 0);
								}
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				PBLog.Warn(ex.ToString());
			}
		}

		public static void LoadPBProfile(string path, XElement element = null)
		{
			PbProfile profile = null;
			if (!string.IsNullOrEmpty(path))
			{
				if (File.Exists(path))
				{
					PBLog.Log("Loading profile {0} from file", Path.GetFileName(path));
					profile = PbProfile.LoadFromFile(path);
					Instance.MySettings.LastProfile = path;
				}
				else
				{
					PBLog.Warn("Profile: {0} does not exist", path);
					Instance.MySettings.LastProfile = path;
					return;
				}
			}
			else if (element != null)
			{
				PBLog.Log("Loading profile from Xml element");
				profile = PbProfile.LoadFromXml(element);
			}

			if (profile == null)
				return;
			Instance.CurrentProfile = profile;

			Instance.MySettings.LastProfile = path;
			Instance.ProfileSettings.Load();
			DynamicCodeCompiler.GenorateDynamicCode();
			Instance.UpdateMaterials();
			if (MainForm.IsValid)
			{
				MainForm.Instance.InitActionTree();
				MainForm.Instance.RefreshTradeSkillTabs();
			}


			if (MainForm.IsValid)
				MainForm.Instance.UpdateControls();

			Instance.MySettings.Save();
		}

		// list of tradeskill tools
		private void LoadTradeskillTools()
		{
			List<uint> tempList = null;
			string path = Path.Combine(BotPath, "Tradeskill Tools.xml");
			if (File.Exists(path))
			{
				XElement xml = XElement.Load(path);
				tempList = xml.Elements("Item").Select(
					x =>
					{
						XAttribute xAttribute = x.Attribute("Entry");
						return xAttribute != null ? xAttribute.Value.ToUint() : 0;
					}).Distinct().ToList();
			}
			TradeskillTools = tempList ?? new List<uint>();
		}

		public static void ChangeSecondaryBot(string botName)
		{
			BotBase bot = Util.GetBotByName(botName);
			if (bot != null)
				ChangeSecondaryBot(bot);
			else
				PBLog.Warn("Bot with name: {0} was not found", botName);
		}

		public static void ChangeSecondaryBot(BotBase bot)
		{
			if (bot == null)
				return;

			if (Instance.SecondaryBot != null && Instance.SecondaryBot.Name == bot.Name) 
				return;

			Util.ExecuteActionWhileBotIsStopped(
				() =>
				{
					Instance.SecondaryBot = bot;
					if (!bot.Initialized)
						bot.DoInitialize();
					if (ProfessionBuddySettings.Instance.LastBotBase != bot.Name)
					{
						ProfessionBuddySettings.Instance.LastBotBase = bot.Name;
						ProfessionBuddySettings.Instance.Save();
					}
					if (MainForm.IsValid)
						MainForm.Instance.UpdateBotCombo();
				},"Changing SecondaryBot");
		}

		#endregion

		#region Utilies

		#region Logging

		// ToDO Move these into a PBLog class.

		[Obsolete("Use the PBLog class functions instead")]
		public static void Warn(string format, params object[] args)
		{
			PBLog.Warn(format, args);
		}

		[Obsolete("Use the PBLog class functions instead")]
		public static void Fatal(string format, params object[] args)
		{
			PBLog.Fatal(format, args);
		}

		[Obsolete("Use the PBLog class functions instead")]
		public static void Log(string format, params object[] args)
		{
			PBLog.Log(format, args);
		}

		[Obsolete("Use the PBLog class functions instead")]
		public static void Log(Color headerColor, string header, Color msgColor, string format, params object[] args)
		{
			PBLog.Log(headerColor, header, msgColor,format, args);
		}

		[Obsolete("Use the PBLog class functions instead")]
		public static void Log(
			System.Windows.Media.Color headerColor,
			string header,
			System.Windows.Media.Color msgColor,
			string format,
			params object[] args)
		{
			PBLog.Log(headerColor, header, msgColor, format, args);
		}

		[Obsolete("Use the PBLog class functions instead")]
		public static void Log(
			LogLevel logLevel,
			System.Windows.Media.Color headerColor,
			string header,
			System.Windows.Media.Color msgColor,
			string format,
			params object[] args)
		{
			PBLog.Log(logLevel, headerColor, header, msgColor, format, args);
		}

		[Obsolete("Use the PBLog class functions instead")]
		public static void Debug(string format, params object[] args)
		{
			PBLog.Debug(format, args);
		}		

		#endregion

		private static string GetProfessionbuddyPath()
		{ // taken from Singular.
			// bit of a hack, but location of source code for assembly is only.
			string asmName = Assembly.GetExecutingAssembly().GetName().Name;
			int len = asmName.LastIndexOf("_", StringComparison.Ordinal);
			string folderName = asmName.Substring(0, len);

			string botsPath = GlobalSettings.Instance.BotsPath;
			if (!Path.IsPathRooted(botsPath))
			{
				botsPath = Path.Combine(Utilities.AssemblyDirectory, botsPath);
			}
			return Path.Combine(botsPath, folderName);
		}

		#endregion

		#region Embedded Types

		public class ConfigurationFormCreatedArg : EventArgs
		{
			public ConfigurationFormCreatedArg(Form configurationForm)
			{
				ConfigurationForm = configurationForm;
			}

			public Form ConfigurationForm { get; private set; }
		}

		#endregion
	}
}