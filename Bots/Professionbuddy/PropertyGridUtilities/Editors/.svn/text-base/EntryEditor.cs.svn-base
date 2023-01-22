using System;
using System.ComponentModel;
using System.Drawing.Design;
using Styx;
using Styx.CommonBot;
using Styx.WoWInternals;

namespace HighVoltz.Professionbuddy.PropertyGridUtilities.Editors
{
	public class EntryEditor : UITypeEditor
	{
		public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
		{
			return UITypeEditorEditStyle.Modal;
		}

		public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
		{
			if (StyxWoW.IsInGame)
			{
				if (!TreeRoot.IsRunning)
					ObjectManager.Update();
				return StyxWoW.Me.GotTarget ? StyxWoW.Me.CurrentTarget.Entry : 0;
			}
			return value;
		}
	}

}
