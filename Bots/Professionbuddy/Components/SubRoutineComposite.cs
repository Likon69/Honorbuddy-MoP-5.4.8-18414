using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using HighVoltz.BehaviorTree;
using HighVoltz.Professionbuddy.ComponentBase;
using HighVoltz.Professionbuddy.PropertyGridUtilities;
using Component = HighVoltz.BehaviorTree.Component;

namespace HighVoltz.Professionbuddy.Components
{
	[PBXmlElement("SubRoutine")]
    public sealed class SubRoutineComposite : PBComposite
	{
		public SubRoutineComposite() : this(new Component[0]) { }
        public SubRoutineComposite(params Component[] children): base(children)
        {		
            Properties["SubRoutineName"] = new MetaProp("SubRoutineName", typeof (string),
                                                        new DisplayNameAttribute(
                                                            ProfessionbuddyBot.Instance.Strings[
                                                                "Action_SubRoutine_SubroutineName"]));
            SubRoutineName = "";
        }

        [PBXmlAttribute]
        public string SubRoutineName
        {
            get { return Properties.GetValue<string>("SubRoutineName"); }
            set { Properties["SubRoutineName"].Value = value; }
        }

		#region PBComposite Members

		override public Color Color
        {
            get { return Color.Blue; }
        }

		override public string Name
        {
            get { return ProfessionbuddyBot.Instance.Strings["Action_SubRoutine_Name"]; }
        }

		override public string Title
        {
            get { return string.Format("Sub {0}", SubRoutineName); }
        }

        override public string Help
        {
            get { return ProfessionbuddyBot.Instance.Strings["Action_SubRoutine_Help"]; }
        }

		public override IPBComponent DeepCopy()
		{
			return new SubRoutineComposite(DeepCopyChildren())
			{
				SubRoutineName = SubRoutineName,
			};
		}

		public async override Task<bool> Run()
		{
			if (!IsActive)
				return false;

			foreach (var child in Children.SkipWhile(c => Selection != null && c != Selection))
			{
				var pbComp = child as IPBComponent;
				if (pbComp == null || pbComp.IsDone)
					continue;
				Selection = child;				
				if (await child)
					return true;
			}

			base.IsDone = true;
			return false;
		}

		// Subroutines should only be executed via an action such as 'CallSubroutine' action so in-order to force 
		// it to be skipped by the behavior tree we need to have IsDone always return true whenever subroutine is not active.
		public override bool IsDone { get { return !IsActive || base.IsDone; } }

		public override void Reset()
		{
			if (!IsActive)
				return;
			base.Reset();
		}

		#endregion

		private bool IsActive { get; set; }
		/// <summary>
		/// Activates the specified sub routine. A subroutine needs to be activated before it can be executed
		/// </summary>
		/// <param name="subRoutine">The sub routine.</param>
		public static IDisposable Activate(SubRoutineComposite subRoutine)
		{
			return new SubRoutineExecutor(subRoutine);
		}

		public static bool GetSubRoutineMyName(string subRoutineName, out SubRoutineComposite subRoutine)
		{
			subRoutine = GetSubRoutineMyNameInternal(subRoutineName, ProfessionbuddyBot.Instance.Branch);
			return subRoutine != null;
		}

		private static SubRoutineComposite GetSubRoutineMyNameInternal(string subRoutineName, Component comp)
		{
			if (comp is SubRoutineComposite && ((SubRoutineComposite)comp).SubRoutineName == subRoutineName)
				return (SubRoutineComposite)comp;
			var composite = comp as Composite;
			if (composite != null)
			{
				return composite.Children.Select(c => GetSubRoutineMyNameInternal(subRoutineName, c)).
					FirstOrDefault(temp => temp != null);
			}
			return null;
		}

		private class SubRoutineExecutor : IDisposable
		{
			private readonly SubRoutineComposite _subroutine;
			public SubRoutineExecutor(SubRoutineComposite subroutine)
			{
				_subroutine = subroutine;
				subroutine.IsActive = true;
			}
			public void Dispose()
			{
				_subroutine.IsActive = false;
			}
		}
	}
}