using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using HighVoltz.BehaviorTree;
using HighVoltz.Professionbuddy.PropertyGridUtilities;
using Styx;
using Styx.WoWInternals.WoWObjects;

namespace HighVoltz.Professionbuddy.ComponentBase
{
	/// <summary>Base class for all Profesionbuddy actions</summary>
	public abstract class PBAction : Action, IPBComponent
	{
		protected PBAction()
		{
			Properties = new PropertyBag();
		}

		#region IPBComponent Implementation

		public abstract string Help { get; }

		public abstract string Title { get; }

		public abstract string Name { get; }

		public virtual Color Color
		{
			get { return Color.Black; }
		}

		public bool HasErrors { get; set; }

		public virtual bool IsDone { get; protected set; }

		public PropertyBag Properties { get; private set; }

		public virtual void OnProfileLoad(XElement element) {}

		public virtual void OnProfileSave(XElement element) {}


		/// <summary>
		/// Resets this instance. Derived classes NEED to call base if overriding
		/// </summary>
		public virtual void Reset()
		{
			IsDone = false;
		}

		public abstract IPBComponent DeepCopy();

		sealed async protected override Task<bool> Run(object context)
		{
			if (!IsDone)
				await Run();
			return !IsDone;
		}

		protected new virtual async Task Run() { }

		#endregion

		#region Utility

		protected Dictionary<string, string> Strings
		{
			get { return ProfessionbuddyBot.Instance.Strings; }
		}

		protected ProfessionbuddyBot PB
		{
			get { return ProfessionbuddyBot.Instance; }
		}

		protected PropertyGrid PropertyGrid
		{
			get { return MainForm.IsValid ? MainForm.Instance.ActionGrid : null; }
		}

		protected LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		protected void RefreshPropertyGrid()
		{
			if (PropertyGrid != null)
			{
				PropertyGrid.Refresh();
			}
		}

		#endregion
	}
}