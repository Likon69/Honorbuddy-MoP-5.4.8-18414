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
using System.Xml.Linq;

using Styx;
#endregion


namespace Honorbuddy.QuestBehaviorCore.XmlElements
{
    public class WaypointType : QuestBehaviorXmlBase
    {
		public const double DefaultAllowedVariance = 0.0;
		public const double DefaultArrivalTolerance = 1.5;

        public WaypointType(XElement xElement)
            : base(xElement)
        {
            try
            {
                Location = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
				AllowedVariance = GetAttributeAsNullable<double>("AllowedVariance", false, new ConstrainTo.Domain<double>(0.0, 50.0), null) ?? DefaultAllowedVariance;
				ArrivalTolerance = GetAttributeAsNullable<double>("ArrivalTolerance", false, ConstrainAs.Range, null) ?? DefaultArrivalTolerance;
				Name = GetAttributeAs<string>("Name", false, ConstrainAs.StringNonEmpty, null) ?? string.Empty;

                if (string.IsNullOrEmpty(Name))
                    { Name = GetDefaultName(Location); }

                HandleAttributeProblem();
            }

            catch (Exception except)
            {
                if (Query.IsExceptionReportingNeeded(except))
                    { QBCLog.Exception(except, "PROFILE PROBLEM with \"{0}\"", xElement.ToString()); }
                IsAttributeProblem = true;
            }
        }


        public WaypointType(WoWPoint location,
			string name = "", 
			double allowedVariance = DefaultAllowedVariance,
			double arrivalTolerance = DefaultArrivalTolerance)
        {
            Location = location;
            Name = name ?? GetDefaultName(location);
	        AllowedVariance = allowedVariance;
            ArrivalTolerance = arrivalTolerance;
        }


		public double ArrivalTolerance { get; set; }
		public double AllowedVariance { get; set; }
		public WoWPoint Location { get; set; }
        public string Name { get; set; }


		#region Concrete class required implementations...
		// DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return "$Id$"; } }
        public override string SubversionRevision { get { return "$Rev$"; } }

		public override XElement ToXml(string elementName = null)
		{
			if (string.IsNullOrEmpty(elementName))
				elementName = "Waypoint";

			var root = new XElement(elementName,
			                        new XAttribute("Name", Name),
			                        new XAttribute("X", Location.X),
			                        new XAttribute("Y", Location.Y),
			                        new XAttribute("Z", Location.Z));

			if (AllowedVariance != DefaultAllowedVariance)
				root.Add(new XAttribute("AllowedVariance", AllowedVariance));

			if (ArrivalTolerance != DefaultArrivalTolerance)
				root.Add(new XAttribute("ArrivalTolerance", ArrivalTolerance));

			return root;
		}
		#endregion


        private string GetDefaultName(WoWPoint wowPoint)
        {
            return string.Format("Waypoint({0})", wowPoint.ToString());
        }
    }
}
