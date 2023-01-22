using System.Diagnostics;
using System.Threading;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;

namespace HighVoltz.AutoAngler.Composites
{
    public class LogoutAction : Action
    {
        protected override RunStatus Run(object context)
        {
            if (StyxWoW.Me.Mounted)
                Mount.Dismount();
            Utils.UseItemByID(6948);
            var hearthSW = new Stopwatch();
            hearthSW.Start();
            // since I'm logging out lets just abuse sleep anyways :D
            while (hearthSW.ElapsedMilliseconds < 20000)
            {
                // damn.. we got something beating on us... 
                if (StyxWoW.Me.Combat)
                    return RunStatus.Success;
                Thread.Sleep(100); // I feel so teribad... not!
            }
			AutoAnglerBot.Instance.Log("Logging out");
            Lua.DoString("Logout()");
            TreeRoot.Stop();
            return RunStatus.Success;
        }
    }
}