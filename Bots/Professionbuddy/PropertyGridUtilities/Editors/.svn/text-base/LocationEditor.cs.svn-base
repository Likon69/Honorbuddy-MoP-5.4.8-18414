using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Styx;
using Styx.CommonBot;
using Styx.WoWInternals;

namespace HighVoltz.Professionbuddy.PropertyGridUtilities.Editors
{
	public class LocationEditor : UITypeEditor
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
				WoWPoint loc = StyxWoW.Me.GotTarget
								   ? StyxWoW.Me.CurrentTarget.Location
								   : StyxWoW.Me.Location;
				return string.Format(CultureInfo.InvariantCulture, "{0}, {1}, {2}", loc.X, loc.Y, loc.Z);
			}
			return value;
		}
	}
}
