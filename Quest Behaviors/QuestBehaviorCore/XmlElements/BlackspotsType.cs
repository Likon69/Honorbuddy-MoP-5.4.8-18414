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
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using Styx.CommonBot.Profiles;


namespace Honorbuddy.QuestBehaviorCore.XmlElements
{
    public class BlackspotsType : QuestBehaviorXmlBase
    {        
        #region Constructor and Argument Processing
        // 30Jun2013-11:49UTC chinajade
        public BlackspotsType(XElement xElement)
            : base(xElement)
        {
            try
            {
                Blackspots = new List<BlackspotType>();

                if (xElement != null)
                {
                    var blackspotElementsQuery =
                        from element in xElement.Elements()
                        where
                            (element.Name == "Blackspot")
                            || (element.Name == "BlackspotType")
                        select element;

                    foreach (XElement childElement in blackspotElementsQuery)
                    {
                        var blackspot = new BlackspotType(childElement);

                        if (!blackspot.IsAttributeProblem)
                            Blackspots.Add(blackspot);

                        IsAttributeProblem |= blackspot.IsAttributeProblem;
                    }
                }

                HandleAttributeProblem();
            }

            catch (Exception except)
            {
                if (Query.IsExceptionReportingNeeded(except))
                    QBCLog.Exception(except, "PROFILE PROBLEM with \"{0}\"", xElement.ToString());
                IsAttributeProblem = true;
            }
        }

        
        public List<BlackspotType> Blackspots { get; set; }
        #endregion


		#region Concrete class required implementations...
		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return "$Id$"; } }
		public override string SubversionRevision { get { return "$Rev$"; } }

		public override XElement ToXml(string elementName = null)
		{
			if (string.IsNullOrEmpty(elementName))
				elementName = "Blackspots";

			var root = new XElement(elementName);

			foreach (var blackspot in Blackspots.OrderBy(b => b.AsBlackspot().Name))
				root.Add(blackspot.ToXml());

			return root;
		}
		#endregion
		
		
		#region Private and Convenience variables
        #endregion


        public List<Blackspot> GetBlackspots()
        {
            return
               (from blackspot in Blackspots
                select blackspot.AsBlackspot())
                .ToList();
        }


        // 11Apr2013-04:42UTC chinajade
        public static BlackspotsType GetOrCreate(XElement parentElement, string elementName)
        {
            var blackspotsType = new BlackspotsType(parentElement
                                                .Elements()
                                                .DefaultIfEmpty(new XElement(elementName))
                                                .FirstOrDefault(elem => (elem.Name == elementName)));

            return blackspotsType;
        }
	}
}
