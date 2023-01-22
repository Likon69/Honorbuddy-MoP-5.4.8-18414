// Originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

#region Usings

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Styx.CommonBot.Coroutines;

#endregion

namespace Honorbuddy.QuestBehaviorCore
{
	/// <summary>
	///     Thottles a child coroutine by the provided time, returning <c>true</c> when not throttled; <c>false</c> otherwise.
	///     Use <see cref="ThrottleCoroutineTask&lt;T&gt;" /> if the child needs to return a value.
	/// </summary>
	public class ThrottleCoroutineTask : CoroutineTask<bool>
	{
		private readonly Stopwatch _timer = new Stopwatch();

		public ThrottleCoroutineTask(TimeSpan throttleTime, Func<Task> childProducer)
		{
			ThrottleTime = throttleTime;
			ChildProducer = childProducer;
		}

		public TimeSpan ThrottleTime { get; private set; }
		public Func<Task> ChildProducer { get; private set; }

		public override async Task<bool> Run()
		{
			if (_timer.IsRunning && _timer.Elapsed < ThrottleTime)
			{
				return false;
			}

			await ChildProducer();
			_timer.Restart();
			return true;
		}
	}

	/// <summary>
	///     Thottles a child coroutine by the provided time. A <see cref="ThrottleResult&lt;T&gt;" /> is returned with the
	///     result. Use <see cref="ThrottleCoroutineTask" /> if the child doesn't needs to return a value.
	/// </summary>
	/// <typeparam name="T">The child coroutine return type</typeparam>
	public class ThrottleCoroutineTask<T> : CoroutineTask<ThrottleResult<T>>
	{
		private readonly Stopwatch _timer = new Stopwatch();

		public ThrottleCoroutineTask(TimeSpan throttleTime, Func<Task<T>> childProducer)
		{
			ThrottleTime = throttleTime;
			ChildProducer = childProducer;
		}

		public TimeSpan ThrottleTime { get; private set; }
		public Func<Task<T>> ChildProducer { get; private set; }

		public override async Task<ThrottleResult<T>> Run()
		{
			if (_timer.IsRunning && _timer.Elapsed < ThrottleTime)
			{
				return ThrottleResult<T>.Throttled;
			}

			var result = await ChildProducer();
			_timer.Restart();
			return new ThrottleResult<T>(true, result);
		}
	}

	public struct ThrottleResult<T>
	{
		internal static readonly ThrottleResult<T> Throttled = new ThrottleResult<T> {RanChild = false};

		internal ThrottleResult(bool ranChild, T childResult)
			: this()
		{
			RanChild = ranChild;
			ChildResult = childResult;
		}

		/// <summary>Gets a value indicating whether child was executed.</summary>
		/// <value>
		///     <c>true</c> if child was executed; otherwise, <c>false</c>.
		/// </value>
		public bool RanChild { get; private set; }

		/// <summary>Gets the result of the child.</summary>
		/// <value>The result of child.</value>
		public T ChildResult { get; private set; }

	}
}