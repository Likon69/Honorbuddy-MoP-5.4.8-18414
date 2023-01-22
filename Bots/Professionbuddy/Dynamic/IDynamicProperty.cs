using System;
using HighVoltz.Professionbuddy.ComponentBase;
using HighVoltz.Professionbuddy.Components;

namespace HighVoltz.Professionbuddy.Dynamic
{
    internal interface IDynamicProperty : IDynamicallyCompiledCode
    {
        /// <summary>
        /// This is the IPBComposite that this propery belongs to. It's set at compile time. 
        /// This version guarantees a public setter
        /// </summary>
        new PBAction AttachedComposite { get; set; }

		string Name { get;  }

		Type ReturnType { get; }

		object Value { get; }
	}
}