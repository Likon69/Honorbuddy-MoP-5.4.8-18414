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
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
#endregion


namespace Honorbuddy.QuestBehaviorCore.XmlElements
{
    public class SwimBreathType : QuestBehaviorXmlBase
    {        
        public SwimBreathType(XElement xElement)
            : base(xElement)
        {
            try
            {
                AirSources = new List<AirSourceType>();
                foreach (var childElement in xElement.Descendants().Where(elem => (elem.Name == "AirSource")))
                {
                    var airSource = new AirSourceType(childElement);

                    if (!airSource.IsAttributeProblem)
                        { AirSources.Add(airSource); }

                    IsAttributeProblem |= airSource.IsAttributeProblem;
                }

                Consumables = new List<ConsumablesType>();
                foreach (var childElement in xElement.Descendants().Where(elem => (elem.Name == "Consumable")))
                {
                    var consumable = new ConsumablesType(childElement);

                    if (!consumable.IsAttributeProblem)
                        { Consumables.Add(consumable); }

                    IsAttributeProblem |= consumable.IsAttributeProblem;
                }


                Equipments = new List<EquipmentType>();
                foreach (var childElement in xElement.Descendants().Where(elem => (elem.Name == "Equipment")))
                {
                    var equipment = new EquipmentType(childElement);

                    if (!equipment.IsAttributeProblem)
                        { Equipments.Add(equipment); }

                    IsAttributeProblem |= equipment.IsAttributeProblem;
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

        public SwimBreathType()
        {
            AirSources = new List<AirSourceType>();
            Consumables = new List<ConsumablesType>();
            Equipments = new List<EquipmentType>();
        }

        public List<AirSourceType>       AirSources { get; private set; }
        public List<ConsumablesType>     Consumables { get; private set; }
        public List<EquipmentType>       Equipments { get; private set; }


		#region Concrete class required implementations...
		// DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return "$Id$"; } }
        public override string SubversionRevision { get { return "$Rev$"; } }

		public override XElement ToXml(string elementName = null)
		{
			if (string.IsNullOrEmpty(elementName))
				elementName = "SwimBreath";

			var root = new XElement(elementName);

			var airSourcesRoot = new XElement("AirSources");
			foreach (var airSource in AirSources.OrderBy(a => a.Name))
				airSourcesRoot.Add(airSource.ToXml());
			root.Add(airSourcesRoot);

			var consumablesRoot = new XElement("Consumables");
			foreach (var consumable in Consumables.OrderBy(c => c.Name))
				consumablesRoot.Add(consumable.ToXml());
			root.Add(consumablesRoot);

			var equipmentsRoot = new XElement("Equipments");
			foreach (var equipment in Equipments)
				equipmentsRoot.Add(equipment.ToXml());
			root.Add(equipmentsRoot);

			return root;
		}
		#endregion


		public class AirSourceType : QuestBehaviorXmlBase
        {        
            public AirSourceType(XElement xElement)
                : base(xElement)
            {
                try
                {
                    ObjectId = GetAttributeAsNullable<int>("ObjectId", true, ConstrainAs.ObjectId, null) ?? 0;
                    Name = GetAttributeAs<string>("Name", false, ConstrainAs.StringNonEmpty, null) ?? string.Empty;

                    if (string.IsNullOrEmpty(Name))
                        { Name = GetDefaultName(ObjectId); }

                    HandleAttributeProblem();
                }

                catch (Exception except)
                {
                    if (Query.IsExceptionReportingNeeded(except))
                    {
                        QBCLog.Error("[PROFILE PROBLEM with \"{0}\"]: {1}\nFROM HERE ({2}):\n{3}\n",
                            xElement.ToString(), except.Message, except.GetType().Name,
                            except.StackTrace);
                    }
                    IsAttributeProblem = true;
                }
            }

            public AirSourceType(int objectId, string name = null)
            {
                ObjectId = objectId;
                Name = name ?? GetDefaultName(objectId);
            }

            public int ObjectId { get; set; }
            public string Name { get; set; }

			#region Concrete class required implementations...
			// DON'T EDIT THESE--they are auto-populated by Subversion
            public override string SubversionId { get { return "$Id$"; } }
            public override string SubversionRevision { get { return "$Rev$"; } }

			public override XElement ToXml(string elementName = null)
			{
				if (string.IsNullOrEmpty(elementName))
					elementName = "AirSource";

				return
					new XElement(elementName,
					             new XAttribute("Name", Name),
					             new XAttribute("ObjectId", ObjectId));
			}
			#endregion


			private string GetDefaultName(int itemId)
            {
                return string.Format("ObjectId({0})", itemId);
            }
        }

    
        public class ConsumablesType : QuestBehaviorXmlBase
        {        
            public ConsumablesType(XElement xElement)
                : base(xElement)
            {
                try
                {
                    ItemId = GetAttributeAsNullable<int>("ItemId", true, ConstrainAs.ItemId, null) ?? 0;
                    Name = GetAttributeAs<string>("Name", false, ConstrainAs.StringNonEmpty, null) ?? string.Empty;

                    if (string.IsNullOrEmpty(Name))
                        { Name = GetDefaultName(ItemId); }

                    HandleAttributeProblem();
                }

                catch (Exception except)
                {
                    if (Query.IsExceptionReportingNeeded(except))
                    {
                        QBCLog.Error("[PROFILE PROBLEM with \"{0}\"]: {1}\nFROM HERE ({2}):\n{3}\n",
                            xElement.ToString(), except.Message, except.GetType().Name,
                            except.StackTrace);
                    }
                    IsAttributeProblem = true;
                }
            }

            public ConsumablesType(int itemId, string name = null)
            {
                ItemId = itemId;
                Name = name ?? GetDefaultName(itemId);
            }

            public int ItemId { get; set; }
            public string Name { get; set; }


			#region Concrete class required implementations...
			// DON'T EDIT THESE--they are auto-populated by Subversion
            public override string SubversionId { get { return "$Id$"; } }
            public override string SubversionRevision { get { return "$Rev$"; } }

			public override XElement ToXml(string elementName = null)
			{
				if (string.IsNullOrEmpty(elementName))
					elementName = "Consumable";

				return new XElement(elementName,
				                    new XAttribute("Name", Name),
				                    new XAttribute("ItemId", ItemId));
			}
			#endregion


			private string GetDefaultName(int itemId)
            {
                return string.Format("ItemId({0})", itemId);
            }
        }


        public class EquipmentType : QuestBehaviorXmlBase
        {        
            public EquipmentType(XElement xElement)
                : base(xElement)
            {
                try
                {
                    ItemId = GetAttributeAsNullable<int>("ItemId", true, ConstrainAs.ItemId, null) ?? 0;
                    Name = GetAttributeAs<string>("Name", false, ConstrainAs.StringNonEmpty, null) ?? string.Empty;

                    if (string.IsNullOrEmpty(Name))
                        { Name = GetDefaultName(ItemId); }

                    HandleAttributeProblem();
                }

                catch (Exception except)
                {
                    if (Query.IsExceptionReportingNeeded(except))
                    {
                        QBCLog.Error("[PROFILE PROBLEM with \"{0}\"]: {1}\nFROM HERE ({2}):\n{3}\n",
                            xElement.ToString(), except.Message, except.GetType().Name,
                            except.StackTrace);
                    }
                    IsAttributeProblem = true;
                }
            }

            public EquipmentType(int itemId, string name = null)
            {
                ItemId = itemId;
                Name = name ?? GetDefaultName(itemId);
            }

            public int ItemId { get; set; }
            public string Name { get; set; }


			#region Concrete class required implementations...
			// DON'T EDIT THESE--they are auto-populated by Subversion
            public override string SubversionId { get { return "$Id$"; } }
            public override string SubversionRevision { get { return "$Rev$"; } }

			public override XElement ToXml(string elementName = null)
			{
				if (string.IsNullOrEmpty(elementName))
					elementName = "Equipment";

				return new XElement(elementName,
				                    new XAttribute("Name", Name),
				                    new XAttribute("ItemId", ItemId));
			}
			#endregion


			private string GetDefaultName(int itemId)
            {
                return string.Format("ItemId({0})", itemId);
            }
        }
    }
}
