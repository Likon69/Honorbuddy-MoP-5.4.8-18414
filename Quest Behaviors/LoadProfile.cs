// Behavior originally contributed by Natfoth.
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
// <summary>
// Allows you to load a profile, it needs to be in the same folder as your current profile.
// ##Syntax##
// ProfileName: 
//     The name of the profile with or without the ".xml" extension.
// 
// RememberProfile [optional; Default: False] 
//     Set this to True if Honorbuddy should remember and load the profile the next time it's started. Default (False)
// </summary>
// 
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.IO;

using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.LoadProfile
{
	[CustomBehaviorFileName(@"LoadProfile")]
	public class LoadProfile : CustomForcedBehavior
	{
		public LoadProfile(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{                
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				ProfileName = GetAttributeAs<string>("ProfileName", true, ConstrainAs.StringNonEmpty, new[] { "Profile" }) ?? "";
				RememberProfile = GetAttributeAsNullable<bool>("RememberProfile", false, null, null) ?? false;

				if (!ProfileName.ToLower().EndsWith(".xml"))
					{ ProfileName += ".xml"; }
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
		public String ProfileName { get; private set; }
		public bool RememberProfile { get; private set; }

		// Private variables for internal state
		private bool _isBehaviorDone;
		// private Composite           _root;

		// Private properties
		private String CurrentProfile { get { return (ProfileManager.XmlLocation); } }
		private bool IsStoreProfile { get { return CurrentProfile.StartsWith("store://"); } }
		private String NewProfilePath { get; set; }

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }


		#region Overrides of CustomForcedBehavior

		protected override Composite CreateBehavior()
		{
			return new PrioritySelector(
				// If behavior is complete, nothing to do, so bail...
				new Decorator(ret => _isBehaviorDone,
					new Action(delegate { QBCLog.Info("Behavior complete"); })),

				// If file does not exist, notify of problem...
				// Support for store profiles. They are handled by HB when there is no profile with given path
				new Decorator(ret => !IsStoreProfile && !File.Exists(NewProfilePath),
					new Action(delegate
					{
						QBCLog.Fatal("Profile '{0}' does not exist.  Download or unpack problem with profile?", NewProfilePath);
						_isBehaviorDone = true;
					})),

				// Load the specified profile...
				new Sequence(
					new Action(delegate
					{
						TreeRoot.StatusText = "Loading profile '" + NewProfilePath + "'";
						QBCLog.Info("Loading profile '{0}'", ProfileName);
						ProfileManager.LoadNew(NewProfilePath, RememberProfile);
					}),
					new WaitContinue(TimeSpan.FromMilliseconds(300), ret => false, new ActionAlwaysSucceed()),
					new Action(delegate { _isBehaviorDone = true; })
					)
				);
		}

        public override void OnFinished()
        {
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
        }


		public override bool IsDone
		{
			get
			{
				return (_isBehaviorDone);
			}
		}


		public override void OnStart()
		{
			// This reports problems, and stops BT processing if there was a problem with attributes...
			// We had to defer this action, as the 'profile line number' is not available during the element's
			// constructor call.
			OnStart_HandleAttributeProblem();

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (!IsDone)
			{
				this.UpdateGoalText(0);

				// Support for store profiles
				if (IsStoreProfile)
				{
					NewProfilePath = CurrentProfile + "/../" + ProfileName;
					return;
				}

				// Convert path name to absolute, and canonicalize it...
				var absolutePath = Path.Combine(Path.GetDirectoryName(CurrentProfile), ProfileName);
				absolutePath = Path.GetFullPath(absolutePath);
				var canonicalPath = new Uri(absolutePath).LocalPath;
				NewProfilePath = canonicalPath;
			}
		}

		#endregion
	}
}
