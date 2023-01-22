using System;
using System.ComponentModel;
using System.Globalization;
using HighVoltz.Professionbuddy.ComponentBase;
using HighVoltz.Professionbuddy.Components;
using HighVoltz.Professionbuddy.PropertyGridUtilities;

namespace HighVoltz.Professionbuddy.Dynamic
{
	public class DynamicProperty<T> : IDynamicProperty
	{
		private Func<object, T> _expressionMethod;
		private string _code;

		public DynamicProperty() : this("", null, "")
		{
		}

		public DynamicProperty(string code) : this("",null, code)
		{
		}

		public DynamicProperty(string name, PBAction parent, string code)
		{
			Code = code;
			_expressionMethod = context => default(T);
			AttachedComposite = parent;
			Name = name;
			if (parent != null && !string.IsNullOrEmpty(name))
			{
				parent.Properties[name].PropertyChanged -= DynamicPropertyChanged;
				parent.Properties[name].PropertyChanged += DynamicPropertyChanged;
			}
			CompileError = "";
		}

		static private void DynamicPropertyChanged(object sender, MetaPropArgs e)
		{
			var dynamicProperty = e.Value as DynamicProperty<T>;
			if (dynamicProperty == null) return;
			var prevDynamicProperty = e.PreviousValue as DynamicProperty<T>;
			if (prevDynamicProperty == null) return;
			if (prevDynamicProperty.AttachedComposite != null)
				dynamicProperty.AttachedComposite = prevDynamicProperty.AttachedComposite;
			if (!string.IsNullOrEmpty(prevDynamicProperty.Name))
				dynamicProperty.Name = prevDynamicProperty.Name;
			if (!string.IsNullOrEmpty(prevDynamicProperty.CompileError))
				dynamicProperty.CompileError = prevDynamicProperty.CompileError;
		}

		public T Value
		{
			get { return _expressionMethod(AttachedComposite); }
		}

		#region IDynamicProperty Members

		public string Name { get;  private set; }

		public int CodeLineNumber { get; set; }

		public string CompileError { get; set; }

		public CsharpCodeType CodeType
		{
			get { return CsharpCodeType.Expression; }
		}

		public virtual Delegate CompiledMethod
		{
			get { return _expressionMethod; }
			set { _expressionMethod = (Func<object, T>) value; }
		}

		public PBAction AttachedComposite { get; set; }

		IPBComponent IDynamicallyCompiledCode.AttachedComponent { get { return AttachedComposite; } }

		object IDynamicProperty.Value { get { return AttachedComposite; } }

		public string Code
		{
			get { return _code; }
			set
			{
				if (value == _code) return;
				_code = value;
				DynamicCodeCompiler.CodeIsModified = true;
			}
		}

		public Type ReturnType
		{
			get { return typeof (T); }
		}

		#endregion

		public override string ToString()
		{
			return Code;
		}

		public static implicit operator T(DynamicProperty<T> exp)
		{
			return exp.Value;
		}

		#region Nested type: DynamivExpressionConverter

		public class DynamivExpressionConverter : TypeConverter
		{
			public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
			{
				if (destinationType == typeof (DynamivExpressionConverter))
					return true;
				return base.CanConvertTo(context, destinationType);
			}

			public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value,
											 Type destinationType)
			{
				if (destinationType == typeof (String) && value is DynamicProperty<T>)
				{
					return ((DynamicProperty<T>) value).Code;
				}
				return base.ConvertTo(context, culture, value, destinationType);
			}

			public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
			{
				if (sourceType == typeof (string))
					return true;
				return base.CanConvertFrom(context, sourceType);
			}

			public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
			{
				if (value is string)
				{
					var ge = new DynamicProperty<T> {Code = (string) value};
					return ge;
				}
				return base.ConvertFrom(context, culture, value);
			}
		}

		#endregion
	}
}