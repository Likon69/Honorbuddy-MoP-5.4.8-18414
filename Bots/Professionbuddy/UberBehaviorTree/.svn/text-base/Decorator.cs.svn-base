// UberBehaviorTree aka UberTree is a behavior tree with an implementation similar to 
// that of TreeSharp https://code.google.com/p/treesharp/

using System;
using System.Threading.Tasks;

namespace HighVoltz.BehaviorTree
{
	public class Decorator : Composite
	{
		private readonly Func<object, bool> _canRunCondition;

		public Decorator() : this(null, null) { }

		public Decorator(Func<object, bool> canRunCondition, Component component)
			: base(component)
		{
			_canRunCondition = canRunCondition;
		}

		public Component Child
		{
			get { return Children[0]; }
		}

		protected virtual bool CanRun(object context)
		{
			return _canRunCondition == null || _canRunCondition(context);
		}

		public override async Task<bool> Run()
		{
			if (Children.Count != 1)
				throw new ApplicationException("Decorators must have only one child.");

			if (!CanRun(Context))
				return false;
			return await Child;
		}
	}

	internal class Decorator<T> : Decorator
	{
		public Decorator() : this(null, null) { }

		public Decorator(Func<T, bool> canRunCondition, Component component)
			: base(context => canRunCondition((T)context), component) { }

		protected override sealed bool CanRun(object context)
		{
			return context is T && base.CanRun(context);
		}

		protected virtual bool CanRun(T context)
		{
			return base.CanRun(context);
		}
	}

}