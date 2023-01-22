using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using HighVoltz.BehaviorTree;
using HighVoltz.Professionbuddy.ComponentBase;

namespace HighVoltz.Professionbuddy.Components
{
	[PBXmlElement("If")]
	public sealed class IfComposite : FlowControlComposite
	{
		#region IPBComponent Members

		public IfComposite(): this(new Component[0]) {}
		private IfComposite(Component[] children) : base(children){}
		public override async Task<bool> Run()
		{
			if ((!IsRunning || !IgnoreCanRun) && !CanRun())
			{
				IsDone = true;
				return false;
			}
			IsRunning = true;
			foreach (var child in Children.SkipWhile(c => Selection != null && c != Selection))
			{
				var pbComp = child as IPBComponent;
				if (pbComp == null || pbComp.IsDone)
					continue;
				Selection = child;

				var coroutine = new Coroutine(async () => await child.Run());

				try
				{
					while (true)
					{
						coroutine.Resume();
						if (coroutine.Status == CoroutineStatus.RanToCompletion)
							break;
						await Coroutine.Yield();
						if (!IgnoreCanRun && !CanRun())
						{
							IsDone = true;
							return false;
						}
					}
					if ((bool) coroutine.Result)
						return true;
				}
				finally
				{
					coroutine.Dispose();
				}
			}

			IsDone = true;
			return false;
		}


		public override string Name
		{
			get { return Strings["FlowControl_If_LongName"]; }
		}

		public override string Title
		{
			get
			{
				return string.IsNullOrEmpty(Condition)
					? Strings["FlowControl_If_LongName"]
					: (Strings["FlowControl_If_Name"] + " (" + Condition + ")");
			}
		}


		public override string Help
		{
			get { return Strings["FlowControl_If_Help"]; }
		}

		public override IPBComponent DeepCopy()
		{
			return new IfComposite(DeepCopyChildren())
					{
						CanRunDelegate = CanRunDelegate,
						Condition = Condition,
						IgnoreCanRun = IgnoreCanRun,
					};
		}

		#endregion
	}
}