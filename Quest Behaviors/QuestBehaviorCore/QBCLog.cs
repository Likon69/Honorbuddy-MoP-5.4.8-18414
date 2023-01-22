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
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;

using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
	public class QBCLog
	{
		public static CustomForcedBehavior BehaviorLoggingContext
		{
			get { return _behaviorLoggingContext; }
			set
			{
				if (_behaviorLoggingContext != value)
				{
					_behaviorLoggingContext = value;

					VersionedBehaviorName =
						(_behaviorLoggingContext != null)
						? BuildVersionedBehaviorName(_behaviorLoggingContext)
						: string.Empty;
				}
			}
		}
		private static CustomForcedBehavior _behaviorLoggingContext;

		public static string VersionedBehaviorName { get; private set; }


		// 30Apr2013-06:20UTC chinajade
		public static string BuildLogMessage(string messageType, string format, params object[] args)
		{                           
			return string.Format("[{0}({1})] {2}",
				VersionedBehaviorName,
				messageType,
				BuildMessageWithContext(null, format, args));           
		}


		// 30Apr2013-06:20UTC chinajade
		public static string BuildMessageWithContext(XElement xElement, string format, params object[] args)
		{
			var context =
				(xElement != null)
				? string.Format("{0}    {1}", Environment.NewLine, GetXmlFileReference(xElement))
				: string.Empty;
									
			return string.Format("{0}{1}", string.Format(format, args), context);           
		}


		// 25Apr2013-11:42UTC chinajade
		private static string BuildVersionedBehaviorName(CustomForcedBehavior cfb)
		{
			Func<string, string> utilStripSubversionDecorations =
				(subversionRevision) =>
				{
					var regexSvnDecoration = new Regex("^\\$[^:]+:[:]?[ \t]*([^$]+)[ \t]*\\$$");

					return regexSvnDecoration.Replace(subversionRevision, "$1").Trim();
				};

			var behaviorName = (cfb != null) ? cfb.GetType().Name : "UnknownBehavior";
			var versionNumber = (cfb != null) ? utilStripSubversionDecorations(cfb.SubversionRevision) : "0";

			VersionedBehaviorName = string.Format("{0}-v{1}", behaviorName, versionNumber);

			return VersionedBehaviorName;
		}


		/// <summary>
		/// <para>Returns the name of the method that calls this function. If SHOWDECLARINGTYPE is true,
		/// the scoped method name is returned; otherwise, the undecorated name is returned.</para>
		/// <para>This is useful when emitting log messages.</para>
		/// </summary>
		/// <para>Notes:<list type="bullet">
		/// <item><description><para> * This method uses reflection--making it relatively 'expensive' to call.
		/// Use it with caution.</para></description></item>
		/// </list></para>
		/// <returns></returns>
		///  7Jul2012-20:26UTC chinajade
		public static string GetMyMethodName(bool showDeclaringType = false)
		{
			var method = (new StackTrace(1)).GetFrame(0).GetMethod();

			if (showDeclaringType)
			{ return (method.DeclaringType + "." + method.Name); }

			return (method.Name);
		}


		/// <summary>
		/// <para>For DEBUG USE ONLY--don't use in production code!</para>
		/// </summary>
		/// <param name="format"></param>
		/// <param name="args"></param>
		public static void Debug(string format, params object[] args)
		{
			Logging.Write(Colors.Fuchsia, BuildLogMessage("debug", format, args));
		}

		public static void Debug(CustomForcedBehavior cfbContext, string format, params object[] args)
		{
			CustomForcedBehavior originalCfbContext = BehaviorLoggingContext;
			BehaviorLoggingContext = cfbContext;

			Debug(format, args);

			BehaviorLoggingContext = originalCfbContext;
		}

		
		/// <summary>
		/// <para>For chasing longer-term (i.e., sporadic) issues.  These messages are only emitted to the log--not the scrolly window,
		/// and are acceptable to leave in production code.</para>
		/// </summary>
		/// <param name="format"></param>
		/// <param name="args"></param>
		public static void DeveloperInfo(string format, params object[] args)
		{
			Logging.WriteDiagnostic(Colors.LimeGreen, BuildLogMessage("debug", format, args));
		}

		public static void DeveloperInfo(CustomForcedBehavior cfbContext, string format, params object[] args)
		{
			CustomForcedBehavior originalCfbContext = BehaviorLoggingContext;
			BehaviorLoggingContext = cfbContext;

			DeveloperInfo(format, args);

			BehaviorLoggingContext = originalCfbContext;
		}
		
		
		/// <summary>
		/// <para>Error situations occur when bad data/input is provided, and no corrective actions can be taken.</para>
		/// </summary>
		/// <param name="format"></param>
		/// <param name="args"></param>
		public static void Error(string format, params object[] args)
		{
			Logging.Write(Colors.Red, BuildLogMessage("error", format, args));
		}

		public static void Error(CustomForcedBehavior cfbContext, string format, params object[] args)
		{
			CustomForcedBehavior originalCfbContext = BehaviorLoggingContext;
			BehaviorLoggingContext = cfbContext;

			Error(format, args);

			BehaviorLoggingContext = originalCfbContext;
		}


		/// <summary>
		/// <para>Exception situations occur when bad data/input is provided, and no corrective actions can be taken.</para>
		/// </summary>
		/// <param name="except"></param>
		/// <param name="formatForPrefix"></param>
		/// <param name="argsForPrefix"></param>
		public static void Exception(Exception except, string formatForPrefix = null, params object[] argsForPrefix)
		{
			var messagePrefix =
				(formatForPrefix == null)
				? "MAINTENANCE PROBLEM"
				: string.Format(formatForPrefix, argsForPrefix);

			QBCLog.Error("[{0}]: {1}\nFROM HERE ({2}):\n{3}\n",
				messagePrefix,
				except.Message,
				except.GetType().Name,
				except.StackTrace);
		}

		public static void Exception(CustomForcedBehavior cfbContext, Exception except, string format, params object[] args)
		{
			CustomForcedBehavior originalCfbContext = BehaviorLoggingContext;
			BehaviorLoggingContext = cfbContext;

			Exception(except, format, args);

			BehaviorLoggingContext = originalCfbContext;
		}
		   
		
		/// <summary>
		/// <para>Error situations occur when bad data/input is provided, and no corrective actions can be taken.</para>
		/// </summary>
		/// <param name="format"></param>
		/// <param name="args"></param>
		public static void Fatal(string format, params object[] args)
		{
			Logging.Write(Colors.Red, BuildLogMessage("fatal", format, args));
			TreeRoot.Stop("Fatal error in quest behavior, or profile.");
		}

		public static void Fatal(CustomForcedBehavior cfbContext, string format, params object[] args)
		{
			CustomForcedBehavior originalCfbContext = BehaviorLoggingContext;
			BehaviorLoggingContext = cfbContext;

			Fatal(format, args);

			BehaviorLoggingContext = originalCfbContext;
		}
		
		
		/// <summary>
		/// <para>Normal information to keep user informed.</para>
		/// </summary>
		/// <param name="format"></param>
		/// <param name="args"></param>
		public static void Info(string format, params object[] args)
		{
			Logging.Write(Colors.CornflowerBlue, BuildLogMessage("info", format, args));
		}

		public static void Info(CustomForcedBehavior cfbContext, string format, params object[] args)
		{
			CustomForcedBehavior originalCfbContext = BehaviorLoggingContext;
			BehaviorLoggingContext = cfbContext;

			Info(format, args);

			BehaviorLoggingContext = originalCfbContext;
		}
		
		
		/// <summary>
		/// MaintenanceErrors occur as a result of incorrect code maintenance.  There is usually no corrective
		/// action a user can perform in the field for these types of errors.
		/// </summary>
		/// <param name="format"></param>
		/// <param name="args"></param>
		///  30Jun2012-15:58UTC chinajade
		public static void MaintenanceError(string format, params object[] args)
		{
			string formattedMessage = string.Format(format, args);
			var trace = new StackTrace(1);

			Error("[MAINTENANCE ERROR] {0}\nFROM HERE:\n{1}", formattedMessage, trace.ToString());
		}

		public static void MaintenanceError(CustomForcedBehavior cfbContext, string format, params object[] args)
		{
			CustomForcedBehavior originalCfbContext = BehaviorLoggingContext;
			BehaviorLoggingContext = cfbContext;

			MaintenanceError(format, args);

			BehaviorLoggingContext = originalCfbContext;
		}


		//  5May2013-09:04UTC chinajade
		public static Composite MarkerPS(ProvideStringDelegate messageDelegate)
		{
			return new Action(context =>
			{
				Logging.Write(Colors.Fuchsia, BuildLogMessage("marker", messageDelegate(context)));
				return RunStatus.Failure;
			});
		}


		//  26May2013-09:04UTC chinajade
		public static Composite MarkerSeq(ProvideStringDelegate messageDelegate)
		{
			return new Action(context =>
			{
				Logging.Write(Colors.Fuchsia, BuildLogMessage("marker", messageDelegate(context)));
			});
		}


		/// <summary>
		/// ProfileErrors occur as a result of the profile attempting to use a behavior in a manner for which
		/// it was not intended.  Such errors also occur because behavior entry criteria are not met.
		/// For instance, the behavior was asked to use an item in the backpack on a mob, but the item is not
		/// present in the backpack.
		/// There is no corrective action a user or the behavior can perform to work around these types
		/// of errors; thus, they are always considered fatal.
		/// </summary>
		/// <param name="format"></param>
		/// <param name="args"></param>
		///  30Jun2012-15:58UTC chinajade
		public static void ProfileError(string format, params object[] args)
		{
			Fatal("[PROFILE ERROR] {0}", string.Format(format, args));
		}

		public static void ProfileError(CustomForcedBehavior cfbContext, string format, params object[] args)
		{
			CustomForcedBehavior originalCfbContext = BehaviorLoggingContext;
			BehaviorLoggingContext = cfbContext;

			ProfileError(format, args);

			BehaviorLoggingContext = originalCfbContext;
		}
		
		
		/// <summary>
		/// <para>Used to notify of problems where corrective (fallback) actions are possible.</para>
		/// </summary>
		/// <param name="format"></param>
		/// <param name="args"></param>
		public static void Warning(string format, params object[] args)
		{
			Logging.Write(Colors.DarkOrange, BuildLogMessage("warning", format, args));
		}

		public static void Warning(CustomForcedBehavior cfbContext, string format, params object[] args)
		{
			CustomForcedBehavior originalCfbContext = BehaviorLoggingContext;
			BehaviorLoggingContext = cfbContext;

			Warning(format, args);

			BehaviorLoggingContext = originalCfbContext;
		}

		
		//  1May2013-07:49UTC chinajade
		public static string GetXmlFileReference(XElement xElement)
		{
			string fileLocation =
				((xElement == null) || string.IsNullOrEmpty(xElement.BaseUri))
					? Utility.GetProfileName()
					: xElement.BaseUri;

			var lineLocation =
				((xElement != null) && ((IXmlLineInfo)xElement).HasLineInfo())
				? ("@line " + ((IXmlLineInfo)xElement).LineNumber.ToString())
				: "@unknown line";

			return string.Format("[Ref: \"{0}\" {1}]", fileLocation, lineLocation);
		}
	}
}