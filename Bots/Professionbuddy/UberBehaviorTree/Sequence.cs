// UberBehaviorTree aka UberTree is a behavior tree with an implementation similar to 
// that of TreeSharp https://code.google.com/p/treesharp/

using System;
using System.Threading.Tasks;

namespace HighVoltz.BehaviorTree
{
	public class Sequence : Composite
	{
		public Sequence(params Component[] children) : base(children) { }
		public Sequence(Func<object, object> contextChanger, params Component[] children)
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
					if (!await child)
						return false;
				}
			}
			finally
			{
				Context = orginalContext;
			}
			return true;
		}
	}
}
