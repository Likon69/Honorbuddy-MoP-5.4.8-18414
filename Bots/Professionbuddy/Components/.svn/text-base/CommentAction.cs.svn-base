using System.ComponentModel;
using System.Drawing;
using HighVoltz.Professionbuddy.ComponentBase;
using HighVoltz.Professionbuddy.PropertyGridUtilities;

namespace HighVoltz.Professionbuddy.Components
{
	[PBXmlElement("Comment")]
	public sealed class CommentAction : PBAction
	{
		public CommentAction()
		{
			Properties["Text"] = new MetaProp(
				"Text",
				typeof (string),
				new DisplayNameAttribute(ProfessionbuddyBot.Instance.Strings["Action_Comment_Name"]));
		}

		public CommentAction(string comment) : this()
		{
			Text = comment;
		}

		[PBXmlAttribute]
		public string Text
		{
			get { return Properties.GetValue<string>("Text"); }
			set { Properties["Text"].Value = value; }
		}

		public override Color Color
		{
			get { return Color.DarkGreen; }
		}

		public override string Help
		{
			get { return ProfessionbuddyBot.Instance.Strings["Action_Comment_Help"]; }
		}

		public override string Name
		{
			get { return ProfessionbuddyBot.Instance.Strings["Action_Comment_Name"]; }
		}

		public override string Title
		{
			get { return string.Format("// {0}", Text); }
		}

		public override bool IsDone
		{
			get { return true; }
		}

		public override IPBComponent DeepCopy()
		{
			return new CommentAction {Text = Text};
		}
	}
}