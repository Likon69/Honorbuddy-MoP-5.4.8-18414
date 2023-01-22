// Originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

using System;
using System.Xml.Linq;

using Styx;
using Styx.CommonBot.Profiles;


namespace Honorbuddy.QuestBehaviorCore.XmlElements
{
    public class BlackspotType : QuestBehaviorXmlBase
    {
        #region Constructor and Argument Processing
        public BlackspotType(XElement xElement)
            : base(xElement)
        {
            try
            {
                var height = GetAttributeAsNullable<double>("Height", false, new ConstrainTo.Domain<double>(0.0, 10000.0), null) ?? 1.0;
                var location = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
                var name = GetAttributeAs<string>("Name", false, ConstrainAs.StringNonEmpty, null) ?? string.Empty;
                var radius = GetAttributeAsNullable<double>("Radius", false, ConstrainAs.Range, null) ?? 10.0;

                _blackspot = new Blackspot(location, (float)radius, (float)height, CreateBlackspotName(name, location));

                HandleAttributeProblem();
            }

            catch (Exception except)
            {
                if (Query.IsExceptionReportingNeeded(except))
                    QBCLog.Exception(except, "PROFILE PROBLEM with \"{0}\"", xElement.ToString());
                IsAttributeProblem = true;
            }
        }

        public BlackspotType(WoWPoint location, string name = "", double radius = 10.0, double height = 1.0)
        {
            _blackspot = new Blackspot(location, (float) radius, (float) height, CreateBlackspotName(name, location));
        }
        #endregion


		#region Concrete class required implementations...
		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return "$Id$"; } }
		public override string SubversionRevision { get { return "$Rev$"; } }

		public override XElement ToXml(string elementName = null)
		{
			if (string.IsNullOrEmpty(elementName))
				elementName = "Blackspot";

			return
				new XElement(elementName,
				             new XAttribute("Name", _blackspot.Name),
				             new XAttribute("X", _blackspot.Location.X),
				             new XAttribute("Y", _blackspot.Location.Y),
				             new XAttribute("Z", _blackspot.Location.Z),
				             new XAttribute("Radius", _blackspot.Radius),
				             new XAttribute("Height", _blackspot.Height));
		}
		#endregion


		#region Private and Convenience variables
        private const string QbcoreNamePrefix = "QBcore: ";

        private readonly Blackspot _blackspot;
        #endregion


        public Blackspot AsBlackspot()
        {
            return _blackspot;
        }


        /// <summary>
        /// <p>Returns 'true', if BLACKSPOT was defined as part of a QBcore-based behavior; otherwise, 'false'.</p>
        /// 
        /// <p>Not all blackspots are created equal.  Please see the comments in Query.IsTargetInBlackspot() for
        /// a better understanding.</p>
        /// </summary>
        /// <param name="blackspot"></param>
        /// <returns></returns>
        public static bool IsQbcoreDefined(Blackspot blackspot)
        {
            return blackspot.Name.StartsWith(QbcoreNamePrefix);
        }


        private string CreateBlackspotName(string preferredName, WoWPoint wowPoint)
        {
            if (string.IsNullOrEmpty(preferredName))
                preferredName = string.Format("Blackspot({0})", wowPoint.ToString());

            return QbcoreNamePrefix + preferredName;
        }
    }
}
