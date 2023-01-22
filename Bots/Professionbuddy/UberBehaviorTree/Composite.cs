// UberBehaviorTree aka UberTree is a behavior tree with an implementation similar to 
// that of TreeSharp https://code.google.com/p/treesharp/

using System.Collections.Generic;

namespace HighVoltz.BehaviorTree
{
	public abstract class Composite : Component
	{
		protected Composite(params Component[] children)
		{
			Children = new List<Component>(children);
			Children.ForEach(c => c.Parent = this);
		}

		public List<Component> Children { get; private set; }
		protected Component Selection { get; set; }

		public void AddChild(Component child)
		{
			if (child != null)
			{
				child.Parent = this;
				Children.Add(child);
			}
		}

		public void InsertChild(int index, Component child)
		{
			if (child != null)
			{
				child.Parent = this;
				Children.Insert(index, child);
			}
		}
	}
}