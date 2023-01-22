using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HighVoltz.Professionbuddy.PropertyGridUtilities.Editors
{
	public class GoldEditor
	{
		private uint copper;
		private uint gold;

		private uint silver;

		public GoldEditor()
		{
			Gold = 0;
			Silver = 0;
			Copper = 0;
		}

		public GoldEditor(string gold)
			: this()
		{
			SetValues(gold);
		}

		[RefreshProperties(RefreshProperties.Repaint)]
		public uint Gold
		{
			get { return gold; }
			set
			{
				gold = value;
				if (OnChanged != null)
					OnChanged(this, null);
			}
		}

		[RefreshProperties(RefreshProperties.Repaint)]
		public uint Silver
		{
			get { return silver; }
			set
			{
				silver = value > 99 ? 99 : value;
				if (OnChanged != null)
					OnChanged(this, null);
			}
		}

		[RefreshProperties(RefreshProperties.Repaint)]
		public uint Copper
		{
			get { return copper; }
			set
			{
				copper = value > 99 ? 99 : value;
				if (OnChanged != null)
					OnChanged(this, null);
			}
		}

		[Browsable(false)]
		public uint TotalCopper
		{
			get { return Copper + (Silver * 100) + (Gold * 10000); }
		}

		public event EventHandler OnChanged;

		public bool SetValues(string values)
		{
			try
			{
				int gI = values.IndexOf('g');
				int sI = values.IndexOf('s');
				int cI = values.IndexOf('c');
				string g = values.Substring(0, gI);
				string s = values.Substring(gI + 1, (sI - gI) - 1);
				string c = values.Substring(sI + 1, (cI - sI) - 1);
				uint gold, silver, copper;
				uint.TryParse(g, out gold);
				uint.TryParse(s, out silver);
				uint.TryParse(c, out copper);
				Gold = gold;
				Silver = silver > 99 ? 99 : silver;
				Copper = copper > 99 ? 99 : copper;
				return true;
			}
			catch
			{
				return false;
			}
		}

		public override string ToString()
		{
			return string.Format("{0}g{1}s{2}c", Gold, Silver, Copper);
		}
	}
}
