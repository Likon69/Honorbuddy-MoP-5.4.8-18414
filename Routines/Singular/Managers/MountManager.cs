using Styx;
using Styx.CommonBot;
using Styx.WoWInternals;
using System;
using Singular.Settings;
using Singular.Helpers;
using System.Drawing;

namespace Singular.Managers
{
    // This class is here to deal with Ghost Wolf/Travel Form usage for shamans and druids
    internal static class MountManager
    {
        internal static void Init()
        {
            Mount.OnMountUp += Mount_OnMountUp;
        }

        private static void Mount_OnMountUp(object sender, MountUpEventArgs e)
        {
            if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds && PVP.PrepTimeLeft > 5)
            {
                e.Cancel = true;
                return;
            }

            if (e.Destination == WoWPoint.Zero)
                return;

            if (Spell.GcdActive || StyxWoW.Me.IsCasting || StyxWoW.Me.ChanneledSpell != null )
                return;

            if ((!Battlegrounds.IsInsideBattleground || !PVP.IsPrepPhase) && !Utilities.EventHandlers.IsShapeshiftSuppressed)
            {
                if (e.Destination.Distance(StyxWoW.Me.Location) < Styx.Helpers.CharacterSettings.Instance.MountDistance)
                {
                    if (StyxWoW.Me.Class == WoWClass.Shaman && SpellManager.HasSpell("Ghost Wolf") && SingularSettings.Instance.Shaman().UseGhostWolf)
                    {
                        e.Cancel = true;

                        if (!StyxWoW.Me.HasAura("Ghost Wolf"))
                        {
                            Logger.Write(Color.White, "^Ghost Wolf instead of mounting");
                            Spell.LogCast("Ghost Wolf", StyxWoW.Me);
                            SpellManager.Cast("Ghost Wolf");
                        }
                    }
                    else if (StyxWoW.Me.Class == WoWClass.Druid && SingularSettings.Instance.Druid().UseTravelForm && SpellManager.HasSpell("Travel Form") && StyxWoW.Me.IsOutdoors)
                    {
                        e.Cancel = true;

                        if (!StyxWoW.Me.HasAura("Travel Form"))
                        {
                            Logger.Write(Color.White, "^Travel Form instead of mounting.");
                            Spell.LogCast("Travel Form", StyxWoW.Me);
                            SpellManager.Cast("Travel Form");
                        }
                    }
                }
                else if (StyxWoW.Me.Class == WoWClass.Druid && ClassSpecific.Druid.Common.AllowAquaticForm)
                {
                    e.Cancel = true;

                    if (!StyxWoW.Me.HasAnyAura("Aquatic Form", "Flight Form"))  // check flightform in case we jump cast it at water surface
                    {
                        Logger.Write(Color.White, "^Aquatic Form instead of mounting.");
                        Spell.LogCast("Aquatic Form", StyxWoW.Me);
                        SpellManager.Cast("Aquatic Form");
                    }
                }
            }

            if (StyxWoW.Me.Class == WoWClass.Shaman && SingularRoutine.CurrentWoWContext != WoWContext.Battlegrounds && ClassSpecific.Shaman.Totems.NeedToRecallTotems )
            {
                Logger.WriteDiagnostic("OnMountUp: recalling totems since about to mount");
                ClassSpecific.Shaman.Totems.RecallTotems();
            }
        }
    }
}