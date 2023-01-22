using System;

namespace HighVoltz.Professionbuddy.PropertyGridUtilities
{
	public class  MetaProp
	{
		private object _val;

		public MetaProp(string name, Type type, params Attribute[] attributes)
		{
			Name = name;
			Type = type;
			Show = true;
			if (attributes != null)
			{
				Attributes = new Attribute[attributes.Length];
				attributes.CopyTo(Attributes, 0);
			}
		}

		public string Name { get; private set; }
		public Type Type { get; private set; }
		public Attribute[] Attributes { get; private set; }
		public bool Show { get; set; }

		public object Value
		{
			get { return _val; }
			set
			{
				if (_val == value) return;
				var handler = PropertyChanged;
				if (handler != null)
				{
					var args = new MetaPropArgs(value, _val);
					_val = value;
					handler(this, args);
				}
				else
				{
					_val = value;
				}
			}
		}

		public event EventHandler<MetaPropArgs> PropertyChanged;
	}
}
