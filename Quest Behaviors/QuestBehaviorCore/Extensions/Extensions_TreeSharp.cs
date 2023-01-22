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
using System.Collections.Generic;
using System.Diagnostics;
using System.Media;
using System.Threading;

using Styx;
using Styx.TreeSharp;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
	// 15May2013-04:32UTC chinajade
	class ActionFail : Composite
	{
		public ActionFail() { }

		public ActionFail(ActionDelegate action)
		{
			Runner = action;
		}

		public ActionFail(ActionSucceedDelegate action)
		{
			FailRunner = action;
		}

		public ActionDelegate Runner { get; private set; }
		public ActionSucceedDelegate FailRunner { get; private set; }

		protected virtual RunStatus Run(object context)
		{
			return RunStatus.Failure;
		}

		protected RunStatus RunAction(object context)
		{
			if (Runner != null)
				return Runner(context);
			if (FailRunner != null)
			{
				FailRunner(context);
				return RunStatus.Failure;
			}
			RunStatus runStatus = Run(context);

			return runStatus;
		}

		protected override sealed IEnumerable<RunStatus> Execute(object context)
		{
			while ((LastStatus = RunAction(context)) == RunStatus.Running)
				yield return RunStatus.Running;

			yield return LastStatus.Value;
		}
	}


	// 20Apr2013-04:32UTC chinajade
	public class CompositeThrottle : Decorator
	{
		public CompositeThrottle(TimeSpan throttleTime, Composite composite)
			: this(context => true, throttleTime, composite)
		{
			// empty
		}


		public CompositeThrottle(CanRunDecoratorDelegate predicateDelegate, TimeSpan throttleTime, Composite composite)
			: base(composite)
		{
			_predicateDelegate = predicateDelegate;
			_throttleTime = throttleTime;
		}


		protected override bool CanRun(object context)
		{
			if (_predicateDelegate(context))
			{
				bool canRun = IsThrottleFinished;

				if (IsThrottleFinished)
					{ _throttle.Restart(); }

				return canRun;
			}

			if (_throttle.IsRunning)
				{ _throttle.Stop(); }

			return false;
		}

		private bool IsThrottleFinished
		{
			get { return !_throttle.IsRunning || (_throttle.Elapsed > _throttleTime); }
		}

		private readonly CanRunDecoratorDelegate _predicateDelegate;
		private readonly Stopwatch _throttle = new Stopwatch();
		private readonly TimeSpan _throttleTime;
	}


	// 20Apr2013-04:32UTC chinajade
	public class CompositeThrottleContinue : DecoratorContinue
	{
		public CompositeThrottleContinue(TimeSpan throttleTime, Composite composite)
			: this(context => true, throttleTime, composite)
		{
			// empty
		}

		
		public CompositeThrottleContinue(CanRunDecoratorDelegate predicateDelegate, TimeSpan throttleTime, Composite composite)
			: base(composite)
		{
			_predicateDelegate = predicateDelegate;
			_throttleTime = throttleTime;
		}


		protected override bool CanRun(object context)
		{
			if (_predicateDelegate(context))
			{
				bool canRun = IsFinished;

				if (IsFinished)
					{ _throttle.Restart(); }

				return canRun;
			}

			if (_throttle.IsRunning)
				{_throttle.Stop(); }

			return false;
		}

		private bool IsFinished
		{
			get { return !_throttle.IsRunning || (_throttle.Elapsed > _throttleTime); }
		}

		private readonly CanRunDecoratorDelegate _predicateDelegate;
		private readonly Stopwatch _throttle = new Stopwatch();
		private readonly TimeSpan _throttleTime;
	}


	// 20Apr2013-04:32UTC chinajade
	public class ExceptionCatchingWrapper : PrioritySelector
	{
		public ExceptionCatchingWrapper(QuestBehaviorBase questBehaviorBase, Composite unwrappedChild)
			: base(unwrappedChild)
		{
			_questBehaviorBase = questBehaviorBase;
		}

		private readonly QuestBehaviorBase _questBehaviorBase;


		public override RunStatus Tick(object context)
		{
			try
			{
				return base.Tick(context);
			}

			catch (Exception except)
			{
				if (except.GetType() != typeof(ThreadAbortException))
				{
					var message = QBCLog.BuildMessageWithContext(_questBehaviorBase.Element,
						"{0} EXCEPTION CONTEXT ({1}):",
						QBCLog.VersionedBehaviorName,
						except.GetType().Name);

					if (QuestBehaviorCoreSettings.Instance.LogProfileContextOnExceptions)
					{
						QBCLog.Error(message);
						SystemSounds.Asterisk.Play();
					}
					else
					{
						QBCLog.DeveloperInfo(message);
					}
				}

				throw;
			}
		}
	}


	//  8May2013-08:10UTC Mastahg
	class FailLogger : Action
	{
		public FailLogger(object data)
		{
			_data = data;
		}

		private readonly object _data;


		protected override RunStatus Run(object context)
		{
			QBCLog.DeveloperInfo(_data.ToString());
			return RunStatus.Failure;
		}
	}


	//
	// This behavior wraps the child behaviors in a 'frame lock' which can provide
	// a big performance improvement if the child behaviors makes multiple HB API
	// calls that internally run off a frame in WoW in one CC pulse.
	//
	// 20Apr2013-04:32UTC chinajade
	public class FrameLockSelector : PrioritySelector
	{
		public FrameLockSelector(params Composite[] children)
			: base(children)
		{
			// empty
		}


		public FrameLockSelector(ContextChangeHandler contextChange, params Composite[] children)
			: base(contextChange, children)
		{
			// empty
		}


		public override RunStatus Tick(object context)
		{
			using (StyxWoW.Memory.AcquireFrame())
			{
				return base.Tick(context);
			}
		}
	}
}