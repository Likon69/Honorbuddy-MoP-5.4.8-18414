// UberBehaviorTree aka UberTree is a behavior tree with an implementation similar to 
// that of TreeSharp https://code.google.com/p/treesharp/

using System;
using System.Threading.Tasks;
using Buddy.Coroutines;

namespace HighVoltz.BehaviorTree
{
	public class Action : Component
	{
		private readonly Func<object, Coroutine> _coroutineProducer;
		private readonly Func<object, Task<bool>> _taskProducer;

		public Action() {}

		public Action(Func<object, Task<bool>> taskProducer)
		{
			_taskProducer = taskProducer;
		}

		public Action(Func<object, Coroutine> coroutineProducer)
		{
			_coroutineProducer = coroutineProducer;
		}

		public Action(Func<object, bool> coroutineProducer)
		{
			_coroutineProducer = context => new Coroutine( async () => coroutineProducer(context));
		}

		public Action(System.Action<object> action)
		{
			_coroutineProducer = context => new Coroutine(async () => action(context));
		}

		public async override Task<bool> Run()
		{
			if (_coroutineProducer != null)
			{
				var coroutine = _coroutineProducer(Context);
				while (!coroutine.IsFinished)
				{
					coroutine.Resume();
					await Coroutine.Yield();
				}

				return (bool)coroutine.Result;
			}

			if (_taskProducer != null)
			{
				var task = _taskProducer(Context);
				return await task;
			}

			return await Run(Context);
		}

		protected virtual async Task<bool> Run(object context)
		{
			return false;
		}
	}

	internal class Action<T> : Action
	{
		public Action(Func<T, Task<bool>> taskProducer) : base(context => taskProducer((T)context)) {}

		public Action(Func<T, Coroutine> coroutineProducer) : base(context => coroutineProducer((T)context)) {}

		public Action(Func<T, bool> coroutineProducer) : base(context => coroutineProducer((T)context)) { }

		public Action(System.Action<T> action) : base(context => action((T)context)) { }

		sealed async protected override Task<bool> Run(object context)
		{
			if (!(context is T))
				return false;
			return await Run((T) context);
		}

		protected  virtual async Task<bool> Run(T context)
		{
			return false;
		}
	}
}