// UberBehaviorTree aka UberTree is a behavior tree with an implementation similar to 
// that of TreeSharp https://code.google.com/p/treesharp/

using System;
using System.Threading.Tasks;
using Buddy.Coroutines;

namespace HighVoltz.BehaviorTree
{
	public class Wait : Decorator
	{
		private readonly Func<object, TimeSpan> _timeSpanProducer;

		public Wait(int timeoutMs, Component component)
			: this(TimeSpan.FromMilliseconds(timeoutMs), null, component) { }

		public Wait(int timeoutMs, Func<object, bool> canRunCondition, Component component)
			: this(TimeSpan.FromMilliseconds(timeoutMs), canRunCondition, component) { }

		public Wait(TimeSpan timespan, Component component)
			: this(timespan, null, component) { }

		public Wait(TimeSpan timespan, Func<object, bool> canRunCondition, Component component)
			: base(canRunCondition, component)
		{
			Timeout = timespan;
		}

		public Wait(Func<object, TimeSpan> timeSpanProducer, Component component)
			: this(timeSpanProducer, null, component) { }

		public Wait(Func<object, TimeSpan> timeSpanProducer, Func<object, bool> canRunCondition, Component component)
			: base(canRunCondition, component)
		{
			_timeSpanProducer = timeSpanProducer;
		}

		private TimeSpan _timeout;
		public TimeSpan Timeout
		{
			get { return _timeout != default(TimeSpan) ? _timeout : _timeSpanProducer(Context); }
			protected set { _timeout = value; }
		}

		public override async Task<bool> Run()
		{
			if (!await Coroutine.Wait(Timeout, () => CanRun(Context)))
				return false;

			return Child == null || await Child;
		}
	}

	public class Wait<T> : Wait
	{
		public Wait(int timeoutMs, Func<T, bool> canRunCondition, Component component)
			: this(TimeSpan.FromMilliseconds(timeoutMs), canRunCondition, component) { }

		public Wait(TimeSpan timespan, Func<T, bool> canRunCondition, Component component)
			: base(timespan, context => canRunCondition((T)context), component) { }

		public Wait(Func<T, TimeSpan> timeSpanProducer, Component component)
			: this(timeSpanProducer, null, component) { }

		public Wait(Func<T, TimeSpan> timeSpanProducer, Func<T, bool> canRunCondition, Component component)
			: base(context => timeSpanProducer((T)context), context => canRunCondition((T)context), component) { }

	}	
}