/*
 * file no longer used 
 * 
using Styx;
using Styx.CommonBot;
using Styx.WoWInternals;
using System;
using System.Linq;
using Singular.Settings;
using System.Drawing;
using Styx.Common.Helpers;
using System.Collections.Generic;
using Singular.Helpers;
using Styx.CommonBot.Routines;

namespace Singular.Managers
{
    internal static class SoulstoneManager
    {
        internal static void Init()
        {
            Lua.Events.AttachEvent("PLAYER_DEAD", HandlePlayerDead);
        }

        private static void HandlePlayerDead(object sender, LuaEventArgs args)
        {
            // Since we hooked this in ctor, make sure we are the selected CC
            if (RoutineManager.Current.Name != SingularRoutine.Instance.Name)
                return;

            if (StyxWoW.Me.IsAlive || StyxWoW.Me.IsGhost)
                return;

            List<string> hasSoulstone = Lua.GetReturnValues("return HasSoulstone()", "hawker.lua");
            if (hasSoulstone != null && hasSoulstone.Count > 0 && !String.IsNullOrEmpty(hasSoulstone[0]) && hasSoulstone[0].ToLower() != "nil")
            {
                if (MovementManager.IsMovementDisabled )
                {
                    Logger.Write(Color.Aquamarine, "Suppressing {0} behavior since movement disabled...", hasSoulstone[0]);
                    return;
                }

                const int RezMaxMobsNear = 0;
                const int RezWaitTime = 10;
                const int RezWaitDist = 20;
                WaitTimer waitClearArea = new WaitTimer(TimeSpan.FromSeconds(RezWaitTime ));
                waitClearArea.Reset();
                Logger.Write(Color.Aquamarine, "Waiting up to {0} seconds for clear area to use {1}...", RezWaitTime , hasSoulstone[0]);
                int countMobs;
                do
                {
                    countMobs = (from u in Unit.NearbyUnfriendlyUnits where u.Distance < RezWaitDist select u).Count();
                } while (countMobs > RezMaxMobsNear && !waitClearArea.IsFinished && !StyxWoW.Me.IsAlive && !StyxWoW.Me.IsGhost);

                if (StyxWoW.Me.IsGhost)
                {
                    Logger.Write(Color.Aquamarine, "Insignia taken or something else released the corpse");
                    return;
                }

                if (StyxWoW.Me.IsAlive)
                {
                    Logger.Write(Color.Aquamarine, "Ressurected by something other than Singular...");
                    return;
                }

                if (countMobs > RezMaxMobsNear )
                {
                    Logger.Write(Color.Aquamarine, "Still {0} enemies within {1} yds, skipping {2}", countMobs, RezWaitDist, hasSoulstone[0]);
                    return;
                }

                Lua.DoString("UseSoulstone()");
                StyxWoW.SleepForLagDuration();
            }
            else
            {

            }
        }
    }
}
*/
