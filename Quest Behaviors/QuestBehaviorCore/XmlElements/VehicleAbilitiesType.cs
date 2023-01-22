// Originally contributed by HighVoltz.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml.Linq;

using Styx;
using Styx.CommonBot.Profiles;


namespace Honorbuddy.QuestBehaviorCore.XmlElements
{
    public class VehicleAbilitiesType : QuestBehaviorXmlBase
    {
        #region Constructor and Argument Processing
        public VehicleAbilitiesType(XElement xElement)
            : base(xElement)
        {
			try
			{
				// Acquire the abilities...
				var abilityList = new List<VehicleAbilityType>();
				if (xElement != null)
				{
					var abilityElementsQuery = xElement.Elements()
						.Where(e => e.Name == "VehicleAbility");

					foreach (XElement childElement in abilityElementsQuery)
					{
						var abilityType = new VehicleAbilityType(childElement);

						if (!abilityType.IsAttributeProblem)
							abilityList.Add(abilityType);

						IsAttributeProblem |= abilityType.IsAttributeProblem;
					}
				}
				VehicleAbilities = abilityList.AsReadOnly();
				HandleAttributeProblem();
			}

			catch (Exception except)
			{
				if (Query.IsExceptionReportingNeeded(except))
					QBCLog.Exception(except, "PROFILE PROBLEM with \"{0}\"", xElement.ToString());
				IsAttributeProblem = true;
			}
        }

		public VehicleAbilitiesType(IEnumerable<VehicleAbilityType> vehicleAbilities)
		{
			VehicleAbilities = vehicleAbilities.ToList().AsReadOnly();
		}

        #endregion


		#region Concrete class required implementations...
		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return "$Id$"; } }
		public override string SubversionRevision { get { return "$Rev$"; } }

		public override XElement ToXml(string elementName = null)
		{
			if (string.IsNullOrEmpty(elementName))
				elementName = "VehicleAbilities";

			var root = new XElement(elementName);

			foreach (var ability in VehicleAbilities)
				root.Add(ability.ToXml());

			return root;
		}

		public ReadOnlyCollection<VehicleAbilityType> VehicleAbilities { get; private set; }

		#endregion


		#region Private and Convenience variables
        #endregion

		public static VehicleAbilitiesType GetOrCreate(XElement parentElement, string elementName)
		{
			var vehicleAbilitiesType = new VehicleAbilitiesType(parentElement
												.Elements()
												.DefaultIfEmpty(new XElement(elementName))
												.FirstOrDefault(elem => (elem.Name == elementName)));

			return vehicleAbilitiesType;
		}

    }
}
