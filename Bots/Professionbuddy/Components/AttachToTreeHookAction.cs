using System;
using System.ComponentModel;
using System.Threading.Tasks;
using CommonBehaviors.Actions;
using HighVoltz.Professionbuddy.ComponentBase;
using HighVoltz.Professionbuddy.PropertyGridUtilities;
using Styx.Common;
using Styx.CommonBot;

namespace HighVoltz.Professionbuddy.Components
{
	[PBXmlElement("AttachToTreeHook")]
    public sealed class AttachToTreeHookAction : PBAction
    {
        private bool _ranonce;
        private SubRoutineComposite _sub;
		private ActionRunCoroutine _treeHookStub;

		public AttachToTreeHookAction()
	    {
			Properties["SubRoutineName"] = new MetaProp(
				"SubRoutineName",
				typeof (string),
				new DisplayNameAttribute(
					ProfessionbuddyBot.Instance.Strings["Action_Common_SubRoutineName"]));

			Properties["TreeHookName"] = new MetaProp(
				"TreeHookName",
				typeof (string),
				new DisplayNameAttribute(
					ProfessionbuddyBot.Instance.Strings["Action_AttachToTreehook_TreeHookName"]));

			Properties["AttachOnStart"] = new MetaProp(
			"AttachOnStart",
				typeof(bool),
				new DisplayNameAttribute(
					ProfessionbuddyBot.Instance.Strings["Action_AttachToTreehook_AttachOnStart"]));

			SubRoutineName = TreeHookName = "";
			AttachOnStart = false;
	    }

		[PBXmlAttribute]
		public string SubRoutineName
		{
			get { return Properties.GetValue<string>("SubRoutineName"); }
			set { Properties["SubRoutineName"].Value = value; }
		}

		[PBXmlAttribute]
		public string TreeHookName
		{
			get { return Properties.GetValue<string>("TreeHookName"); }
			set { Properties["TreeHookName"].Value = value; }
		}

		[PBXmlAttribute]
		public bool AttachOnStart
		{
			get { return Properties.GetValue<bool>("AttachOnStart"); }
			set { Properties["AttachOnStart"].Value = value; }
		}

        public override string Name
        {
			get { return ProfessionbuddyBot.Instance.Strings["Action_AttachToTreehook_Name"]; }
        }

        public override string Title
        {
            get { return string.Format("{0}: {1}", Name, TreeHookName); }
        }

        public override string Help
        {
			get { return ProfessionbuddyBot.Instance.Strings["Action_AttachToTreehook_Help"]; }
        }

		public override bool IsDone { get { return _ranonce; } }

		protected async override Task Run()
		{
			try
			{
				if (!SubRoutineComposite.GetSubRoutineMyName(SubRoutineName, out _sub))
				{
					PBLog.Warn("{0}: {1}.", ProfessionbuddyBot.Instance.Strings["Error_SubroutineNotFound"], SubRoutineName);
					return;
				}

				_treeHookStub = new ActionRunCoroutine(ctx => SubRoutineExecutor());
				TreeHooks.Instance.InsertHook(TreeHookName, 0, _treeHookStub);
				BotEvents.Profile.OnNewOuterProfileLoaded += ProfileOnOnNewOuterProfileLoaded;
				BotEvents.OnBotStopped += BotEvents_OnBotStopped;
				PBLog.Debug("Attached the '{0}' SubRoutine to the {1} TreeHook", SubRoutineName, TreeHookName);
			}
			finally
			{
				_ranonce = true;
			}
		}

		private async Task<bool> SubRoutineExecutor()
		{
			using (SubRoutineComposite.Activate(_sub))
			{
				if (_sub.IsDone)
					_sub.Reset();
				return await _sub;
			}
		}

		private void ProfileOnOnNewOuterProfileLoaded(BotEvents.Profile.NewProfileLoadedEventArgs args)
		{
			if (Util.IsProfessionbuddyProfile(args.NewProfile.XmlElement))
				DoCleanup();
		}

		void BotEvents_OnBotStopped(EventArgs args)
		{
			if (!ProfessionbuddyBot.IsExecutingActionWhileHonorbuddyIsStopped)
				DoCleanup();
		}

		private void DoCleanup()
		{
			TreeHooks.Instance.RemoveHook(TreeHookName, _treeHookStub);
			BotEvents.Profile.OnNewOuterProfileLoaded -= ProfileOnOnNewOuterProfileLoaded;
			BotEvents.OnBotStopped -= BotEvents_OnBotStopped;
			PBLog.Debug("Detached the '{0}' SubRoutine from the {1} TreeHook", SubRoutineName, TreeHookName);
			_ranonce = false;
		}

	    public override IPBComponent DeepCopy()
	    {
			return new CallSubRoutineAction { SubRoutineName = SubRoutineName };
	    }

    }
}