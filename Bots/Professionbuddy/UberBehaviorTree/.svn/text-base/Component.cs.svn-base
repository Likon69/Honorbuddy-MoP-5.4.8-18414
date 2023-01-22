// UberBehaviorTree aka UberTree is a behavior tree with an implementation similar to 
// that of TreeSharp https://code.google.com/p/treesharp/

using System;
using Styx.CommonBot.Coroutines;

namespace HighVoltz.BehaviorTree
{
	public abstract class Component : CoroutineTask<bool>
	{
		public Component Parent { get; protected internal set; }
		protected Func<object, object> ContextChanger { get; set; }
		public static object Context { get; protected set; }
	}
}