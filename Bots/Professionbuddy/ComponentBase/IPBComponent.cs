using System.Drawing;
using System.Xml.Linq;
using HighVoltz.Professionbuddy.PropertyGridUtilities;

namespace HighVoltz.Professionbuddy.ComponentBase
{
	public interface IPBComponent : IDeepCopy<IPBComponent>
	{
		string Help { get; }

		string Name { get; }

		string Title { get; }
		
		Color Color { get; }

		bool HasErrors { get; set; }

		bool IsDone { get; }

		PropertyBag Properties { get; }

		void Reset();

		void OnProfileLoad(XElement element);

		void OnProfileSave(XElement element);

	}
}
