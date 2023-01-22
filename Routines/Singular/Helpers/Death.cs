using System.Linq;
using CommonBehaviors.Actions;

using Singular.Settings;
using Styx;
using Singular.Helpers;

using Styx.CommonBot;
using Styx.CommonBot.Inventory;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Styx.TreeSharp;
using System;

using Action = Styx.TreeSharp.Action;
using Singular.Dynamics;
using Singular.Managers;
using System.Collections.Generic;
using System.Drawing;

namespace Singular.Helpers
{
    internal static class Death
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        const int RezMaxMobsNear = 0;
        const int RezWaitTime = 10;
        const int RezWaitDist = 20;

        private static string SelfRezSpell { get; set; }
        private static int MobsNearby { get; set; }
        private static DateTime NextSuppressMessage = DateTime.MinValue;

        [Behavior(BehaviorType.Death)]
        public static Composite CreateDefaultDeathBehavior()
        {
            return new Throttle( 60,
                new Decorator(
                    req => {
                        if (Me.IsAlive || Me.IsGhost)
                            return false;

                        if (SingularSettings.Instance.SelfRessurect == Singular.Settings.SelfRessurectStyle.None)
                            return false;

                        List<string> hasSoulstone = Lua.GetReturnValues("return HasSoulstone()", "hawker.lua");
                        if (hasSoulstone == null || hasSoulstone.Count == 0 || String.IsNullOrEmpty(hasSoulstone[0]) || hasSoulstone[0].ToLower() == "nil")
                            return false;

                        if (SingularSettings.Instance.SelfRessurect == Singular.Settings.SelfRessurectStyle.Auto && MovementManager.IsMovementDisabled)
                        {
                            if (NextSuppressMessage < DateTime.Now)
                            {
                                NextSuppressMessage = DateTime.Now.AddSeconds(RezWaitTime);
                                Logger.Write(Color.Aquamarine, "Suppressing {0} behavior since movement disabled...", hasSoulstone[0]);
                            }
                            return false;
                        }

                        SelfRezSpell = hasSoulstone[0];
                        return true;
                        },
                    new Sequence(
                        new Action( r => Logger.Write(Color.Aquamarine, "Waiting up to {0} seconds for clear area to use {1}...", RezWaitTime, SelfRezSpell)),
                        new Wait( 
                            RezWaitTime, 
                            until => {
                                MobsNearby = Unit.UnfriendlyUnits(RezWaitDist).Count();
                                return MobsNearby <= RezMaxMobsNear || Me.IsAlive || Me.IsGhost;
                                },
                            new Action( r => {
                                if ( Me.IsGhost )
                                {
                                    Logger.Write(Color.Aquamarine, "Insignia taken or corpse release by something other than Singular...");
                                    return RunStatus.Failure;
                                }

                                if ( Me.IsAlive)
                                {
                                    Logger.Write(Color.Aquamarine, "Ressurected by something other than Singular...");
                                    return RunStatus.Failure;
                                }

                                return RunStatus.Success;
                                })
                            ),
                        new DecoratorContinue(
                            req => MobsNearby > RezMaxMobsNear,
                            new Action( r => {
                                Logger.Write(Color.Aquamarine, "Still {0} enemies within {1} yds, skipping {2}", MobsNearby, RezWaitDist, SelfRezSpell);
                                return RunStatus.Failure;
                                })
                            ),

                        new Action(r => Logger.Write("Ressurrecting Singular by invoking {0}...", SelfRezSpell)),

                        new Action(r => Lua.DoString("UseSoulstone()")),

                        new WaitContinue( 1, until => Me.IsAlive || Me.IsGhost, new ActionAlwaysSucceed())
                        )
                    )
                );
        }

    }
}
