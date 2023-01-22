using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using HighVoltz.BehaviorTree;
using HighVoltz.Professionbuddy.PropertyGridUtilities;
using Styx;
using Styx.Common;
using Styx.WoWInternals.WoWObjects;

namespace HighVoltz.Professionbuddy.ComponentBase
{
	public abstract class PBComposite : Composite, IPBComponent
	{
		protected PBComposite(params Component[] children)
			: base(children)
		{
			Properties = new PropertyBag();
		}

		#region IPBComponent Implementation

		public abstract string Help { get; }

		public abstract string Title { get; }

		public abstract string Name { get; }

		virtual public Color Color { get { return Color.Black; }}

		public bool HasErrors { get; set; }

		virtual public bool IsDone { get; protected set; }

		public PropertyBag Properties { get; private set; }

		public virtual void OnProfileLoad(XElement element) {}

		public virtual void OnProfileSave(XElement element) {}


		public virtual void Reset()
		{
			Children.OfType<IPBComponent>().ForEach(c => c.Reset());
			Selection = null;
			IsDone = false;
		}

		public abstract IPBComponent DeepCopy();

		#endregion

		#region Utility

		protected Dictionary<string, string> Strings { get { return ProfessionbuddyBot.Instance.Strings; } }

		protected ProfessionbuddyBot PB { get { return ProfessionbuddyBot.Instance; } }

		protected PropertyGrid PropertyGrid
		{
			get { return MainForm.IsValid ? MainForm.Instance.ActionGrid : null; }
		}

		protected void RefreshPropertyGrid()
		{
			if (PropertyGrid != null)
			{
				PropertyGrid.Refresh();
			}
		}

		protected LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		protected Component[] DeepCopyChildren()
		{
			return Children.Select(
				c =>
				{
					var pbComp = c as IPBComponent;
					return pbComp != null ? (Component)pbComp.DeepCopy() : c;
				}).ToArray();
		}	
		
		#endregion
	}
}