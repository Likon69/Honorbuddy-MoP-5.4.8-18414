using System;
using System.ComponentModel;
using HighVoltz.Professionbuddy.Dynamic;
using HighVoltz.Professionbuddy.PropertyGridUtilities;
using Component = HighVoltz.BehaviorTree.Component;

namespace HighVoltz.Professionbuddy.ComponentBase
{
    abstract public class DynamicallyCompiledCodeComposite : PBComposite, IDynamicallyCompiledCode
    {
        private string _lastError = "";

	    protected DynamicallyCompiledCodeComposite() : this(CsharpCodeType.BoolExpression) {}

		protected DynamicallyCompiledCodeComposite(CsharpCodeType codeType, params Component[] children)
			:base(children)
	    {
		    CodeType = codeType;
            Properties["CompileError"] = new MetaProp("CompileError", typeof (string), new ReadOnlyAttribute(true),
                                                      new DisplayNameAttribute(
                                                          ProfessionbuddyBot.Instance.Strings[
                                                              "Action_CSharpAction_CompileError"])) {Show = false};

		    Properties["CompileError"].PropertyChanged += CompileErrorPropertyChanged;
        }

		public abstract Delegate CompiledMethod { get; set; }

	    public IPBComponent AttachedComponent { get { return this; } }

		public abstract string Code { get; }

        public int CodeLineNumber { get; set; }

        public string CompileError
        {
            get { return (string) Properties["CompileError"].Value; }
            set { Properties["CompileError"].Value = value; }
        }

		public CsharpCodeType CodeType { get; private set; }

		private void CompileErrorPropertyChanged(object sender, EventArgs e)
		{
			if (CompileError != "" || (CompileError == "" && _lastError != ""))
				MainForm.Instance.RefreshActionTree(this);
			Properties["CompileError"].Show = CompileError != "";
			RefreshPropertyGrid();
			_lastError = CompileError;
		}
    }
}