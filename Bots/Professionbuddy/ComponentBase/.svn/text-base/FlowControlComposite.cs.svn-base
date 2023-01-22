using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.Threading;
using HighVoltz.Professionbuddy.Dynamic;
using HighVoltz.Professionbuddy.PropertyGridUtilities;
using Component = HighVoltz.BehaviorTree.Component;

namespace HighVoltz.Professionbuddy.ComponentBase
{
	public abstract class FlowControlComposite : DynamicallyCompiledCodeComposite
	{
		protected FlowControlComposite(params Component[] children)
			: base(CsharpCodeType.BoolExpression, children)
		{
			Properties["IgnoreCanRun"] = new MetaProp(
				"IgnoreCanRun",
				typeof (bool),
				new DisplayNameAttribute(
					Strings["FlowControl_IgnoreCanRun"
						]));

			Properties["Condition"] = new MetaProp(
				"Condition",
				typeof (string),
				new EditorAttribute(
					typeof (MultilineTextEditor),
					typeof (UITypeEditor)),
				new DisplayNameAttribute(
					Strings["FlowControl_Condition"]));

			Condition = "";
			CanRunDelegate = ctx => false;
			Properties["Condition"].PropertyChanged += Condition_PropertyChanged;
			IgnoreCanRun = true;
		}

		protected Func<object,bool> CanRunDelegate { get; set; }

		[PBXmlAttribute]
		public string Condition
		{
			get { return Properties.GetValue<string>("Condition"); }
			set { Properties["Condition"].Value = value; }
		}

		[PBXmlAttribute]
		public bool IgnoreCanRun
		{
			get { return Properties.GetValue<bool>("IgnoreCanRun"); }
			set { Properties["IgnoreCanRun"].Value = value; }
		}

		protected bool IsRunning { get; set; }

		#region ICSharpCode Members

		public override Delegate CompiledMethod
		{
			get { return CanRunDelegate; }
			set { CanRunDelegate = (Func<object,bool>) value; }
		}


		public override string Code
		{
			get { return Condition; }
		}

		#endregion

		#region IPBComponent Members

		public override Color Color
		{
			get { return string.IsNullOrEmpty(CompileError) ? Color.Blue : Color.Red; }
		}

		public override void Reset()
		{
			IsRunning = false;
			base.Reset();
		}

		#endregion

		private void Condition_PropertyChanged(object sender, EventArgs e)
		{
			DynamicCodeCompiler.CodeIsModified = true;
		}

		protected bool CanRun()
		{
			try
			{
				return CanRunDelegate(this);
			}
			catch (Exception ex)
			{
				if (ex.GetType() != typeof (ThreadAbortException))
					PBLog.Warn("{0}\nErr:{1}", Title, ex);
				return false;
			}
		}

	}
}