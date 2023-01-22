using System;
using HighVoltz.Professionbuddy.ComponentBase;

namespace HighVoltz.Professionbuddy.Dynamic
{
	public enum CsharpCodeType
	{
		BoolExpression,
		Statements,
		Declaration,
		Expression,
		Coroutine
	}

	public interface IDynamicallyCompiledCode
	{
		int CodeLineNumber { get; set; }
		string CompileError { get; set; }
		CsharpCodeType CodeType { get; }
		string Code { get; }
		Delegate CompiledMethod { get; set; }
		IPBComponent AttachedComponent { get; }
	}
}
