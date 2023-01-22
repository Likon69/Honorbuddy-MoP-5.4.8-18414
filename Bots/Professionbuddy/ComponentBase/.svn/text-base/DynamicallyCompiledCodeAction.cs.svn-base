using System;
using System.ComponentModel;
using System.Drawing;
using HighVoltz.Professionbuddy.Dynamic;
using HighVoltz.Professionbuddy.PropertyGridUtilities;

namespace HighVoltz.Professionbuddy.ComponentBase
{
	//this is a PBAction derived abstract class that adds functionallity for dynamically compiled Csharp expression/statement

	public abstract class DynamicallyCompiledCodeAction : PBAction, IDynamicallyCompiledCode
	{
		private string _lastError = "";

		protected DynamicallyCompiledCodeAction() : this(CsharpCodeType.Statements) {}

		protected DynamicallyCompiledCodeAction(CsharpCodeType codeType)
		{
			CodeType = codeType;
			Properties["CompileError"] = new MetaProp(
				"CompileError",
				typeof (string),
				new ReadOnlyAttribute(true),
				new DisplayNameAttribute(
					Strings["Action_CSharpAction_CompileError"])) {Show = false};
			Properties["CompileError"].PropertyChanged += CompileErrorPropertyChanged;
		}

		public override Color Color
		{
			get { return string.IsNullOrEmpty(CompileError) ? base.Color : Color.Red; }
		}

		#region ICSharpCode Members

		public int CodeLineNumber { get; set; }

		public string CompileError
		{
			get { return (string) Properties["CompileError"].Value; }
			set { Properties["CompileError"].Value = value; }
		}

		public CsharpCodeType CodeType { get; protected set; }

		public virtual string Code { get; set; }

		public virtual Delegate CompiledMethod { get; set; }

		public IPBComponent AttachedComponent
		{
			get { return this; }
		}

		#endregion

		private void CompileErrorPropertyChanged(object sender, MetaPropArgs e)
		{
			if (CompileError != "")
			{
				Properties["CompileError"].Show = true;
				MainForm.Instance.RefreshActionTree(this);
			}
			else
			{
				Properties["CompileError"].Show = false;
			}
			RefreshPropertyGrid();
			_lastError = CompileError;
		}
	}
}