// UberBehaviorTree aka UberTree is a behavior tree with an implementation similar to 
// that of TreeSharp https://code.google.com/p/treesharp/

using System;
using System.Threading.Tasks;
using Buddy.Coroutines;

namespace HighVoltz.BehaviorTree
{
	public class WaitContinue : Wait
	{
		public WaitContinue(int timeoutMs, Component component)
			: this(TimeSpan.FromMilliseconds(timeoutMs), null, component) { }

		public WaitContinue(int timeoutMs, Func<object, bool> canRunCondition, Component component)
			: this(TimeSpan.FromMilliseconds(timeoutMs), canRunCondition, component) { }

		public WaitContinue(TimeSpan timespan, Component component)
			: this(timespan, null, component) { }

		public WaitContinue(TimeSpan timespan, Func<object, bool> canRunCondition, Component component)
			: base(timespan, canRunCondition, component) { }

		public WaitContinue(Func<object, TimeSpan> timeSpanProducer, Component component)
			: this(timeSpanProducer, null, component) { }

		public WaitContinue(Func<object, TimeSpan> timeSpanProducer, Func<object, bool> canRunCondition, Component component)
			: base(timeSpanProducer, canRunCondition, component){}

		public override async Task<bool> Run()
		{
			await Coroutine.Wait(Timeout, () => CanRun(Context));
			return Child == null || await Child;
		}
	}

	public class WaitContinue<T> : WaitContinue
	{
		public WaitContinue(int timeoutMs, Func<T, bool> canRunCondition, Component component)
			: this(TimeSpan.FromMilliseconds(timeoutMs), canRunCondition, component) { }

		public WaitContinue(TimeSpan timespan, Func<T, bool> canRunCondition, Component component)
			: base(timespan, context => canRunCondition((T)context), component) { }

		public WaitContinue(Func<T, TimeSpan> timeSpanProducer, Component component)
			: this(timeSpanProducer, null, component) { }

		public WaitContinue(Func<T, TimeSpan> timeSpanProducer, Func<T, bool> canRunCondition, Component component)
			: base(context => timeSpanProducer((T)context), context => canRunCondition((T)context), component) { }

	}	
}