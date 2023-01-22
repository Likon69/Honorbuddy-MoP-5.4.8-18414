using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HighVoltz.Professionbuddy.PropertyGridUtilities.Editors;

namespace HighVoltz.Professionbuddy.PropertyGridUtilities.Converters
{
	[TypeConverter(typeof(GoldEditorConverter))]
	public class GoldEditorConverter : ExpandableObjectConverter
	{
		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
		{
			if (destinationType == typeof(GoldEditorConverter))
				return true;
			return base.CanConvertTo(context, destinationType);
		}

		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value,
										 Type destinationType)
		{
			if (destinationType == typeof(String) && value is GoldEditor)
			{
				var ge = (GoldEditor)value;
				return ge.ToString();
			}
			return base.ConvertTo(context, culture, value, destinationType);
		}

		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			if (sourceType == typeof(string))
				return true;
			return base.CanConvertFrom(context, sourceType);
		}

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			if (value is string)
			{
				var ge = new GoldEditor();
				if (!ge.SetValues((string)value))
					throw new ArgumentException("Can not convert '" + (string)value + "' to type GoldEditor");
				return ge;
			}
			return base.ConvertFrom(context, culture, value);
		}
	}
}
