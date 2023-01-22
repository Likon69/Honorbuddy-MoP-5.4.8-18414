using System;

namespace HighVoltz.Professionbuddy.PropertyGridUtilities
{
	public class MetaPropArgs : EventArgs
	{
		public MetaPropArgs(object val, object previousValue)
		{
			Value = val;
			PreviousValue = previousValue;
		}

		public object PreviousValue { get; private set; }
		public object Value { get; private set; }

		public T GetPreviousValue<T>()
		{
			return (T)PreviousValue;
		}

		public T GetValue<T>()
		{
			return (T)Value;
		}
	}

}
