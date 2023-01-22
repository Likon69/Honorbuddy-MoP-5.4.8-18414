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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using Honorbuddy.QuestBehaviorCore.XmlElements;
using Styx.Helpers;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
	public abstract partial class QuestBehaviorBase
	{
		/// <summary>
		/// <para>Reads the "Quest Behaviors/DATA/AuraIds_OccupiedVehicle.xml" file, and returns an IEnumerable
		/// of all the AuraIds that are represent Vehicles that are occupied.</para>
		/// <para>If the da file has malformed entries (which it should never be), error messages
		/// will be emitted.</para>
		/// </summary>
		/// <returns>the IEnumerable may be empty, but it will never be null.</returns>
		//  7Mar2013-02:28UTC chinajade
		public static IEnumerable<int> GetOccupiedVehicleAuraIds()
		{
			var occupiedVehicleAuraIds = new List<int>();

			string auraDataFileName = GetDataFileFullPath("AuraIds_OccupiedVehicle.xml");

			if (!File.Exists(auraDataFileName))
			{
				QBCLog.Warning("Unable to locate Occupied Vehicle Aura database (in {0}).  Vehicles will be unqualified"
					+ "--this may cause us to follow vehicles occupied by other players.",
					auraDataFileName);
				return occupiedVehicleAuraIds;
			}

			XDocument xDoc = XDocument.Load(auraDataFileName, LoadOptions.SetBaseUri | LoadOptions.SetLineInfo);

			foreach (var childElement in xDoc.Descendants("Aura"))
			{
				var aura = new SpellType(childElement);

				if (!aura.IsAttributeProblem)
					{ occupiedVehicleAuraIds.Add(aura.SpellId); }
			}

			return occupiedVehicleAuraIds;
		}


		// 30Apr2013-09:09UTC chinajade
		public static SwimBreathType GetSwimBreathInfo()
		{
			string swimBreathFileName = GetDataFileFullPath("SwimBreath.xml");

			if (!File.Exists(swimBreathFileName))
			{
				QBCLog.Warning("Unable to locate Swim Breath database (in {0}).  Will only be able to catch breath"
							+ " at water's surface.",
					swimBreathFileName);
				return null;
			}

			XDocument xDoc = XDocument.Load(swimBreathFileName, LoadOptions.SetBaseUri | LoadOptions.SetLineInfo);

			return new SwimBreathType(xDoc.Elements("SwimBreath").FirstOrDefault());
		}


		private static string GetDataFileFullPath(string fileName)
		{
			// NB: We use the absolute path here.  If we don't, then QBs get confused if there are additional
			// QBs supplied in the Honorbuddy/Default Profiles/. directory.
			return Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName),
								GlobalSettings.Instance.QuestBehaviorsPath,
								"QuestBehaviorCore",
								"Data",
								fileName);
		}
	}
}
