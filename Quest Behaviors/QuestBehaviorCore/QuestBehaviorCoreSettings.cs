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
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

using Styx.Helpers;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
	public class QuestBehaviorCoreSettings : Settings
	{
		public QuestBehaviorCoreSettings()
			: base(FullPathToSettingsFile())
		{
			if (MonitoredBehaviors == null)
			{
				MonitoredBehaviors = new string[] { "BEHAVIOR_CLASS_NAME" };
				Save();
			}
		}

		private static QuestBehaviorCoreSettings _instance;


		public static QuestBehaviorCoreSettings Instance 
		{ 
			get { return _instance ?? (_instance = new QuestBehaviorCoreSettings()); }
			set { _instance = value; }
		}


		private static string FullPathToSettingsFile()
		{
			return Path.Combine(SettingsDirectory, "QuestBehaviorCoreSettings.xml");
		}


		#region Category: Audible Notifications
		[Setting]
		[DefaultValue(false)]
		[Category("Debug")]
		[DisplayName("Audible Notify on Deprecated Attribute Use")]
		[Description("Emits the system 'asterisk' sound, when a profile uses a deprecated attribute on a behavior."
					 + "  This value is only consulted if LogNotifyOn_OnDeprecatedAttributeUse is enabled."
					 + "  Profile writers will want this value to be 'true'; Profile users will ant this value to be 'false'.")]
		public bool AudibleNotify_OnDeprecatedAttributeUse { get; set; }

		[Setting]
		[DefaultValue(false)]
		[Category("Debug")]
		[DisplayName("Audible Notify on Deprecated Behavior Use")]
		[Description("Emits the system 'asterisk' sound, when a profile uses a deprecated behavior."
					+ "  This value is only consulted if LogNotifyOn_OnDeprecatedBehaviorUse is enabled."
					+ "  Profile writers will want this value to be 'true'; Profile users will ant this value to be 'false'.")]
		public bool AudibleNotify_OnDeprecatedBehaviorUse { get; set; }

		[Setting]
		[DefaultValue(true)]
		[Category("Debug")]
		[DisplayName("Audible Notify on Semantic Incoherency")]
		[Description("Emits the system 'asterisk' sound, when a profile provides semantically incoherent attributes to a behavior.")]
		public bool AudibleNotify_OnSemanticIncoherency { get; set; }
		#endregion


		#region Category: Debug
		[Setting]
		[DefaultValue(false)]
		[Category("Debug")]
		[DisplayName("Log profile context when exception is thrown.")]
		[Description("When a behavior tree exception occurs, enabling this will emit the profile context to the log."
						+ "  This allows the profile/quest behavior writer to quickly locate which line in the profile is associated with"
						+ " the thrown exception")]
		public bool LogProfileContextOnExceptions { get; set; }

		[Setting]
		[Category("Debug")]
		[DisplayName("List of Monitored Behaviors")]
		[Description("Monitored Behaviors will cause an audible alert and a warning message to be emitting to the log"
					+ " when the behavior is used.")]
		public string[] MonitoredBehaviors { get; set; }
		#endregion

		
		#region Category: Log Notification Warnings
		[Setting]
		[DefaultValue(false)]
		[Category("Warnings")]
		[DisplayName("Log Notify on Deprecated Attribute Use")]
		[Description("Emits a message to the Log, when a profile uses a deprecated attribute on a behavior."
					+ "  Profile writers will want this value to be true.")]
		public bool LogNotifyOn_OnDeprecatedAttributeUse { get; set; }

		[Setting]
		[DefaultValue(true)]
		[Category("Warnings")]
		[DisplayName("Log Notify on Deprecated Behavior Use")]
		[Description("Emits a message to the Log, when a profile uses a deprecated behavior.")]
		public bool LogNotifyOn_OnDeprecatedBehaviorUse { get; set; }

		[Setting]
		[DefaultValue(false)]
		[Category("Warnings")]
		[DisplayName("Log and Audible Notification on behaviors planned for deprecation")]
		[Description("Emits a message to the Log and chimes, when a profile uses a behavior scheduled for deprecation.")]
		public bool LogNotifyOn_OnScheduledForDeprecation { get; set; }
		#endregion
	}
}