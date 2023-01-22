// UberBehaviorTree aka UberTree is a behavior tree with an implementation similar to 
// that of TreeSharp https://code.google.com/p/treesharp/

using System;
using System.Threading.Tasks;

namespace HighVoltz.BehaviorTree
{
	public class PrioritySelector : Composite
	{
		public PrioritySelector(params Component[] children ): base(children){}
		public PrioritySelector(Func<object, object> contextChanger, params Component[] children)
			: base(children)
		{
			ContextChanger = contextChanger;
		}

		public async override Task<bool> Run()
		{
			var orginalContext = Context;
			try
			{
				if (ContextChanger != null)
					Context = ContextChanger(Context);
				foreach (var child in Children)
				{
					if (await child)
						return true;
				}
			}
			finally
			{
				Context = orginalContext;
			}
			return false;
		}
	}
}
