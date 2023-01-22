using Styx;
using Styx.CommonBot.Routines;
using Styx.TreeSharp;

namespace HighVoltz.AutoAngler.Composites
{
    internal class AutoAnglerDecorator : Decorator
    {
        public AutoAnglerDecorator(Composite child) : base(child)
        {
        }

        protected override bool CanRun(object context)
        {
            return StyxWoW.Me.IsAlive && !StyxWoW.Me.Combat &&
                   !RoutineManager.Current.NeedRest;
        }
    }
}