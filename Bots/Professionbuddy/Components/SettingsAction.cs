using System;
using System.ComponentModel;
using HighVoltz.Professionbuddy.ComponentBase;
using HighVoltz.Professionbuddy.PropertyGridUtilities;

namespace HighVoltz.Professionbuddy.Components
{
	[PBXmlElement("Setting", new[] { "Settings" })]
    public sealed class SettingsAction : PBAction
    {
	    public SettingsAction()
	    {
		    Properties["DefaultValue"] = new MetaProp(
			    "DefaultValue",
				    typeof (string),
				    new DisplayNameAttribute(ProfessionbuddyBot.Instance.Strings["Action_Common_DefaultValue"]));

		    Properties["Type"] = new MetaProp(
			    "Type",
				    typeof (TypeCode),
				    new DisplayNameAttribute(ProfessionbuddyBot.Instance.Strings["Action_Common_Type"]));

		    Properties["Name"] = new MetaProp(
			    "Name",
				    typeof (string),
				    new DisplayNameAttribute(ProfessionbuddyBot.Instance.Strings["Action_Common_Name"]));

		    Properties["Summary"] = new MetaProp(
			    "Summary",
				    typeof (string),
				    new DisplayNameAttribute(ProfessionbuddyBot.Instance.Strings["Action_Common_Summary"]));

		    Properties["Category"] = new MetaProp(
			    "Category",
				    typeof (string),
				    new DisplayNameAttribute(ProfessionbuddyBot.Instance.Strings["Action_Common_Category"]));

		    Properties["Global"] = new MetaProp(
			    "Global",
				    typeof (bool),
				    new DisplayNameAttribute(ProfessionbuddyBot.Instance.Strings["Action_Common_Global"]));

		    Properties["Hidden"] = new MetaProp(
			    "Hidden",
				    typeof (bool),
				    new DisplayNameAttribute(ProfessionbuddyBot.Instance.Strings["Action_Common_Hidden"]));

		    DefaultValue = "true";
		    Type = TypeCode.Boolean;
		    SettingName = ProfessionbuddyBot.Instance.Strings["Action_Common_SettingName"];
		    Summary = ProfessionbuddyBot.Instance.Strings["Action_Common_SummaryExample"];
		    Category = "Misc";
		    Global = false;
		    Hidden = false;
	    }

        [PBXmlAttribute]
        public string DefaultValue
        {
            get { return  Properties.GetValue<string>("DefaultValue"); }
            set { Properties["DefaultValue"].Value = value; }
        }

        [PBXmlAttribute]
        public TypeCode Type
        {
			get { return Properties.GetValue<TypeCode>("Type"); }
            set { Properties["Type"].Value = value; }
        }

        [PBXmlAttribute("Name")]
        public string SettingName
        {
            get { return Properties.GetValue<string>("Name"); }
            set { Properties["Name"].Value = value; }
        }

        [PBXmlAttribute]
        public string Summary
        {
            get { return Properties.GetValue<string>("Summary"); }
            set { Properties["Summary"].Value = value; }
        }

        [PBXmlAttribute]
        public string Category
        {
			get { return Properties.GetValue<string>("Category"); }
            set { Properties["Category"].Value = value; }
        }

        [PBXmlAttribute]
        public bool Global
        {
            get { return Properties.GetValue<bool>("Global"); }
            set { Properties["Global"].Value = value; }
        }

        [PBXmlAttribute]
        public bool Hidden
        {
            get { return (bool) Properties["Hidden"].Value; }
            set { Properties["Hidden"].Value = value; }
        }

        public override string Help
        {
			get { return ProfessionbuddyBot.Instance.Strings["Action_Settings_Help"]; }
        }

        public override string Name
        {
            get { return ProfessionbuddyBot.Instance.Strings["Action_Settings_Name"]; }
        }

        public override string Title
        {
            get { return string.Format("{0}: {1} {2}={3}", Name, Type, SettingName, DefaultValue); }
        }

        public override bool IsDone
        {
            get { return true; }
        }

	    public override IPBComponent DeepCopy()
	    {
			return new SettingsAction
			{
				DefaultValue = DefaultValue,
				SettingName = SettingName,
				Type = Type,
				Summary = Summary,
				Category = Category,
				Global = Global,
				Hidden = Hidden
			};
	    }

    }
}