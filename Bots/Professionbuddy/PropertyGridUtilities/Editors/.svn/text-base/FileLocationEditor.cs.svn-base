using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HighVoltz.Professionbuddy.PropertyGridUtilities.Editors
{
	public class FileLocationEditor : UITypeEditor
	{
		public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
		{
			return UITypeEditorEditStyle.Modal;
		}

		public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
		{
			using (var ofd = new OpenFileDialog())
			{
				string pbPath = Path.GetDirectoryName(ProfessionbuddyBot.Instance.MySettings.LastProfile);
				if (string.IsNullOrEmpty(pbPath))
				{
					MessageBox.Show("Please save your profile 1st");
					return "";
				}
				ofd.Filter = "Xml files|*.xml|All files|*.*";
				ofd.InitialDirectory = pbPath;
				if (ofd.ShowDialog() == DialogResult.OK)
				{
					if (ofd.FileName.Contains(pbPath))
					{
						string relative = ofd.FileName.Substring(pbPath.Length + 1);
						return relative;
					}
					else
					{
						MessageBox.Show(
							"File needs to be in same folder or in a subfolder from your professionbuddy profile");
						return "";
					}
				}
			}
			return value;
		}
	}
}
