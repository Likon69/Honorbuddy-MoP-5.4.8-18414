using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace HighVoltz.Professionbuddy.PropertyGridUtilities
{
    public class PropertyBag :  ICustomTypeDescriptor
    {

	    private readonly Dictionary<string, MetaProp> _metaPropList = new Dictionary<string, MetaProp>();

		public MetaProp this[string key]
		{
			get
			{
				MetaProp value;
				return _metaPropList.TryGetValue(key, out value) ? value : null;
			}
			set
			{
				if (value == null)
					_metaPropList.Remove(key);
				else _metaPropList[key] = value;
			}
		}

        public T GetValue<T>(string name)
        {
            return (T)this[name].Value;
        }

        #region Nested type: PropertyBagDescriptor

        public class PropertyBagDescriptor : PropertyDescriptor
        {
            private readonly Type type;

            public PropertyBagDescriptor(string name, Type type, Attribute[] attributes)
                : base(name, attributes)
            {
                this.type = type;
            }

            public override Type PropertyType
            {
                get { return type; }
            }

            public override bool IsReadOnly
            {
                get
                {
                    foreach (Attribute att in Attributes)
                    {
                        if (att is ReadOnlyAttribute)
                        {
                            var ro = att as ReadOnlyAttribute;
                            return ro.IsReadOnly;
                        }
                    }
                    return false;
                }
            }

            public override Type ComponentType
            {
                get { return typeof(PropertyBag); }
            }

            public override bool SupportsChangeEvents
            {
                get { return true; }
            }

            public override TypeConverter Converter
            {
                get
                {
                    foreach (Attribute att in Attributes)
                    {
                        if (att is TypeConverterAttribute)
                        {
                            var tc = att as TypeConverterAttribute;
                            return (TypeConverter)Activator.CreateInstance(Type.GetType(tc.ConverterTypeName));
                        }
                    }
                    return base.Converter;
                }
            }

            public override object GetValue(object component)
            {
                return ((PropertyBag)component)[Name].Value;
            }

            public override void SetValue(object component, object value)
            {
                ((PropertyBag)component)[Name].Value = value;
            }

            public override bool ShouldSerializeValue(object component)
            {
                return GetValue(component) != null;
            }

            public override bool CanResetValue(object component)
            {
                return true;
            }

            public override void ResetValue(object component)
            {
                SetValue(component, null);
            }
        }

        #endregion

        #region ICustomTypeDescriptor definitions

        AttributeCollection ICustomTypeDescriptor.GetAttributes()
        {
            return TypeDescriptor.GetAttributes(this, true);
        }

        string ICustomTypeDescriptor.GetClassName()
        {
            return TypeDescriptor.GetClassName(this, true);
        }

        string ICustomTypeDescriptor.GetComponentName()
        {
            return TypeDescriptor.GetComponentName(this, true);
        }

        TypeConverter ICustomTypeDescriptor.GetConverter()
        {
            return TypeDescriptor.GetConverter(this, true);
        }

        EventDescriptor ICustomTypeDescriptor.GetDefaultEvent()
        {
            return TypeDescriptor.GetDefaultEvent(this, true);
        }

        PropertyDescriptor ICustomTypeDescriptor.GetDefaultProperty()
        {
            return null;
        }

        object ICustomTypeDescriptor.GetEditor(Type editorBaseType)
        {
            return TypeDescriptor.GetEditor(this, editorBaseType, true);
        }

        EventDescriptorCollection ICustomTypeDescriptor.GetEvents()
        {
            return TypeDescriptor.GetEvents(this, true);
        }

        EventDescriptorCollection ICustomTypeDescriptor.GetEvents(Attribute[] attributes)
        {
            return TypeDescriptor.GetEvents(this, attributes, true);
        }

        PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties()
        {
            return ((ICustomTypeDescriptor)this).GetProperties(new Attribute[0]);
        }

        PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties(Attribute[] attributes)
        {
            PropertyDescriptor[] metaProps = (from prop in _metaPropList.Values
                                              where prop.Show
                                              select new PropertyBagDescriptor(prop.Name, prop.Type, prop.Attributes)).
                ToArray();
            return new PropertyDescriptorCollection(metaProps);
        }

        object ICustomTypeDescriptor.GetPropertyOwner(PropertyDescriptor pd)
        {
            return this;
        }

        #endregion

    }

}