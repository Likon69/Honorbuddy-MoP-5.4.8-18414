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
using System.Text;

using Styx;
using Styx.CommonBot;
using Styx.WoWInternals.WoWObjects;

#endregion


namespace Honorbuddy.QuestBehaviorCore
{
	public class QBCDebug
	{
		public static string DumpTargetList()
		{
			var buffer = new StringBuilder();
			var newLine = Environment.NewLine;
			const ulong guidMask = 0x0ffff;

			buffer.AppendFormat("Target List ({1} entries):{0}", newLine, Targeting.Instance.TargetList.Count);
			foreach (var target in Targeting.Instance.TargetList)
			{
				buffer.AppendFormat("    {1}.{2:X4}{0}", newLine, target.SafeName, (target.Guid & guidMask));
			}

			return buffer.ToString();
		}


		public static void ShowVehicleArticulationChain(WoWUnit wowUnit)
		{
			var tmp = new StringBuilder();
			var indentLevel = 4;

			do
			{
				var indent = string.Empty.PadLeft(indentLevel);
				var fieldSeparator = string.Format("\n  {0}", indent);

                tmp.Append(wowUnit.SafeName);

				var worldMatrix = wowUnit.GetWorldMatrix();
				var pitch = StyxWoW.Memory.Read<float>(wowUnit.BaseAddress + 0x820 + 0x24);
				tmp.AppendFormat("{0}{1} (pitch: {2}): {3}",
					fieldSeparator,
                    wowUnit.SafeName,
					pitch,
					worldMatrix.ToString_FullInfo(false, indentLevel));

				wowUnit = wowUnit.Transport as WoWUnit;

				if (wowUnit != null)
					{ tmp.AppendFormat("{0} => ", fieldSeparator); }

				indentLevel += 4;
			} while (wowUnit != null);

			QBCLog.Debug("Articulation chain: {0}", tmp.ToString());
		}
	}
}