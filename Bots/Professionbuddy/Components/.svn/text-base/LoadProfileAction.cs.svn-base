using System;
using System.ComponentModel;
using System.Drawing.Design;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using HighVoltz.Professionbuddy.ComponentBase;
using HighVoltz.Professionbuddy.PropertyGridUtilities;
using HighVoltz.Professionbuddy.PropertyGridUtilities.Editors;
using Styx.CommonBot.Profiles;

namespace HighVoltz.Professionbuddy.Components
{
	public enum LoadProfileType
	{
		Honorbuddy,
		Professionbuddy
	}

	[PBXmlElement("LoadProfile", new []{"LoadProfileAction"})]
	public sealed class LoadProfileAction : PBAction
	{
		private volatile bool _loadingProfile;

		public LoadProfileAction()
		{
			Properties["Path"] = new MetaProp(
				"Path",
				typeof (string),
				new EditorAttribute(
					typeof (FileLocationEditor),
					typeof (UITypeEditor)),
				new DisplayNameAttribute(ProfessionbuddyBot.Instance.Strings["Action_Common_Path"]));

			Properties["ProfileType"] = new MetaProp(
				"ProfileType",
				typeof (LoadProfileType),
				new DisplayNameAttribute(ProfessionbuddyBot.Instance.Strings["Action_LoadProfileAction_ProfileType"]));

			Properties["IsLocal"] = new MetaProp(
				"IsLocal",
				typeof (bool),
				new DisplayNameAttribute(ProfessionbuddyBot.Instance.Strings["Action_LoadProfileAction_IsLocal"]));

			Path = "";
			ProfileType = LoadProfileType.Honorbuddy;
			IsLocal = true;
		}

		[PBXmlAttribute]
		public LoadProfileType ProfileType
		{
			get { return Properties.GetValue<LoadProfileType>("ProfileType"); }
			set { Properties["ProfileType"].Value = value; }
		}

		[PBXmlAttribute]
		public string Path
		{
			get { return Properties.GetValue<string>("Path"); }
			set { Properties["Path"].Value = value; }
		}

		[PBXmlAttribute]
		public bool IsLocal
		{
			get { return Properties.GetValue<bool>("IsLocal"); }
			set { Properties["IsLocal"].Value = value; }
		}

		public override string Name
		{
			get { return ProfessionbuddyBot.Instance.Strings["Action_LoadProfileAction_Name"]; }
		}

		public override string Title
		{
			get { return string.Format("{0}: {1}", Name, Path); }
		}

		public override string Help
		{
			get { return ProfessionbuddyBot.Instance.Strings["Action_LoadProfileAction_Help"]; }
		}

		protected async override Task Run()
		{
			if (_loadingProfile)
				return;

			var absPath = GetAbsolutePath();

			bool emptyProfile = string.IsNullOrEmpty(Path);

			if (IsLocal)
			{
				// check if profile is already loaded.
				if (!string.IsNullOrEmpty(ProfileManager.XmlLocation)
					&& ProfileManager.XmlLocation.Equals(absPath, StringComparison.CurrentCultureIgnoreCase))
				{
					IsDone = true;
					return;
				}
				// check if profile exists
				if (!emptyProfile && !File.Exists(absPath))
				{
					PBLog.Warn("{0}: {1}", ProfessionbuddyBot.Instance.Strings["Error_UnableToFindProfile"], Path);
					IsDone = true;
					return;
				}
			}

			ProfessionbuddyBot.Debug(
				"Loading Profile: {0}, previous profile was {1}",
				emptyProfile ? "(Empty Profile)" : Path,
				ProfileManager.XmlLocation ?? "[No Profile]");

			var path = emptyProfile || !IsLocal ? Path : absPath;

			Util.ExecuteActionWhileBotIsStopped(
				() =>
				{
					try
					{
						if (string.IsNullOrEmpty(path))
						{
							ProfileManager.LoadEmpty();
						}
						else if (!IsLocal)
						{
							var req = WebRequest.Create(path);
							req.Proxy = null;
							using (WebResponse res = req.GetResponse())
							{
								using (var stream = res.GetResponseStream())
								{
									ProfileManager.LoadNew(stream);
								}
							}
						}
						else if (File.Exists(path))
						{
							ProfileManager.LoadNew(path);
						}
						IsDone = true;
						ProfessionbuddyBot.Debug("Successfully loaded profile:{0}", emptyProfile ? "(Empty Profile)" : Path);
					}
					catch (Exception ex)
					{
						// if ex is not null there was a problem loading profile.
						ProfessionbuddyBot.Fatal("Failed to load profile.\n{0}", ex);
					}
				},
				"Loading a new profile");
			_loadingProfile = true;
		}

		public override IPBComponent DeepCopy()
		{
			return new LoadProfileAction {Path = Path, ProfileType = ProfileType, IsLocal = IsLocal};
		}

		public override void Reset()
		{
			_loadingProfile = false;
			base.Reset();
		}
		
		public string GetAbsolutePath()
		{
			if (!IsLocal)
				return Path;

			return string.IsNullOrEmpty(ProfessionbuddyBot.Instance.CurrentProfile.XmlPath)
				? string.Empty
				: System.IO.Path.Combine(System.IO.Path.GetDirectoryName(ProfessionbuddyBot.Instance.CurrentProfile.XmlPath), Path);
		}
	}
}