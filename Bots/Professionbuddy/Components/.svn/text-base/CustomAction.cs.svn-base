using System;
using System.ComponentModel;
using System.Drawing.Design;
using System.Threading;
using System.Threading.Tasks;
using HighVoltz.Professionbuddy.ComponentBase;
using HighVoltz.Professionbuddy.Dynamic;
using HighVoltz.Professionbuddy.PropertyGridUtilities;

namespace HighVoltz.Professionbuddy.Components
{
	[PBXmlElement("Custom", new []{"CustomAction"})]
	public sealed class CustomAction : DynamicallyCompiledCodeAction
	{
		public CustomAction()
			: base(CsharpCodeType.Coroutine)
		{
			
			Action = async c => {};
			Properties["Code"] = new MetaProp(
				"Code",
				typeof (string),
				new EditorAttribute(typeof (MultilineTextEditor), typeof (UITypeEditor)),
				new DisplayNameAttribute(Strings["Action_Common_Code"]));
			Code = "";
			Properties["Code"].PropertyChanged += CustomAction_PropertyChanged;
		}

		public Func<object,Task> Action { get; set; }


		[PBXmlAttribute]
		public override string Code
		{
			get { return Properties.GetValue<string>("Code"); }
			set { Properties["Code"].Value = value; }
		}

		public override string Name
		{
			get { return Strings["Action_CustomAction_Name"]; }
		}

		public override string Title
		{
			get { return string.Format("{0}:({1})", Strings["Action_CustomAction_Name"], Code); }
		}


		public override string Help
		{
			get { return Strings["Action_CustomAction_Help"]; }
		}

		public override Delegate CompiledMethod
		{
			get { return Action; }
			set { Action = (Func<object, Task>)value; }
		}

		private void CustomAction_PropertyChanged(object sender, MetaPropArgs e)
		{
			DynamicCodeCompiler.CodeIsModified = true;
		}

		protected async override Task Run()
		{
			try
			{
				try
				{
				 await Action(this);
				}
				catch (Exception ex)
				{
					if (ex.GetType() != typeof (ThreadAbortException))
						PBLog.Warn("{0}:({1})\n{2}", Strings["Action_CustomAction_Name"], Code, ex);
				}
				IsDone = true;
			}
			catch (Exception ex)
			{
				if (ex.GetType() != typeof (ThreadAbortException))
					PBLog.Warn("There was an exception while executing a CustomAction\n{0}", ex);
			}
		}

		public override IPBComponent DeepCopy()
		{
			return new CustomAction {Code = Code};
		}
	}
}