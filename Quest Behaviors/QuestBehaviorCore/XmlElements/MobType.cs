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
#endregion


namespace Honorbuddy.QuestBehaviorCore.XmlElements
{
    public class MobType : QuestBehaviorXmlBase
    {
        #region Constructor and Argument Processing
        public MobType(XElement xElement)
            : base(xElement)
        {
            try
            {
                Name = GetAttributeAs<string>("Name", false, ConstrainAs.StringNonEmpty, null) ?? string.Empty;
                Entry = GetAttributeAsNullable<int>("Entry", false, ConstrainAs.MobId, null)
                    ?? GetAttributeAsNullable<int>("Id", false, ConstrainAs.MobId, null)
                    ?? 0;

                if (Entry == 0)
                {
                    QBCLog.Error(QBCLog.BuildMessageWithContext(Element,
                        "Attribute '{1}' is required, but was not provided.",
                        Environment.NewLine,
                        "Entry"));
                    IsAttributeProblem = true;
                }

                HandleAttributeProblem();
            }

            catch (Exception except)
            {
                if (Query.IsExceptionReportingNeeded(except))
                    { QBCLog.Exception(except, "PROFILE PROBLEM with \"{0}\"", xElement.ToString()); }
                IsAttributeProblem = true;
            }
        }

        public string Name { get; set; }
        public int Entry { get; set; }
        #endregion


		#region Concrete class required implementations...
		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return "$Id$"; } }
		public override string SubversionRevision { get { return "$Rev$"; } }

		public override XElement ToXml(string elementName = null)
		{
			if (string.IsNullOrEmpty(elementName))
				elementName = "Mob";

			return
				new XElement(elementName,
					new XAttribute("Name", Name),
					new XAttribute("Id", Entry));
		}
		#endregion


		#region Private and Convenience variables
        #endregion
    }
}
