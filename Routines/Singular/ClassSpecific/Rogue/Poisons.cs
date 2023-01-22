using System.Collections.Generic;
using System.Linq;
using Singular.Settings;
using Singular.Helpers;
using Styx;
using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using System;
using Singular.Managers;

namespace Singular.ClassSpecific.Rogue
{
    public enum LethalPoisonType
    {
        None    = 0,
        Auto,
        Deadly  = 2823,
        Wound   = 8679
    }

    public enum NonLethalPoisonType
    {
        None            = 0,
        Auto,
        Crippling       = 3408,
        MindNumbing     = 5761,
        Leeching        = 108211,
        Paralytic       = 108215
    }

    public static class Poisons
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static RogueSettings RogueSettings { get { return SingularSettings.Instance.Rogue(); } }
        private static bool HasTalent(RogueTalents tal) { return TalentManager.IsSelected((int)tal); } 

        private const int RefreshAtMinutesLeft = 30;

        static Poisons()
        {
            //Lua.Events.AttachEvent("END_BOUND_TRADEABLE", HandleEndBoundTradeable);
        }

        private static void HandleEndBoundTradeable(object sender, LuaEventArgs args)
        {
            //Lua.DoString("EndBoundTradeable(" + args.Args[0] + ")");
        }

        public static LethalPoisonType NeedLethalPosion()
        {
            return PoisonCheck(RogueSettings.LethalPoison);
        }

        public static NonLethalPoisonType NeedNonLethalPosion()
        {
            return PoisonCheck(RogueSettings.NonLethalPoison);
        }

        private static LethalPoisonType PoisonCheck(LethalPoisonType poison)
        {
            if (poison > LethalPoisonType.Auto && !SpellManager.HasSpell((int)poison))
                poison = LethalPoisonType.Auto;

            if (poison == LethalPoisonType.Auto)
            {
                if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds && SpellManager.HasSpell((int)LethalPoisonType.Wound))
                    poison = LethalPoisonType.Wound;
                else if (SpellManager.HasSpell((int)LethalPoisonType.Deadly))
                    poison = LethalPoisonType.Deadly;
                else
                    poison = LethalPoisonType.None;
            }

            if ( poison != LethalPoisonType.None && Me.GetAuraTimeLeft((int)poison, true).TotalMinutes < RefreshAtMinutesLeft)
                return poison;

            return LethalPoisonType.None;
        }

        private static NonLethalPoisonType PoisonCheck(NonLethalPoisonType poison)
        {
            if (poison > NonLethalPoisonType.Auto && !SpellManager.HasSpell((int)poison))
                poison = NonLethalPoisonType.Auto;

            if (poison == NonLethalPoisonType.Auto)
            {
                if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds && SpellManager.HasSpell((int)NonLethalPoisonType.Paralytic ))
                    poison = NonLethalPoisonType.Paralytic;
                else if (SpellManager.HasSpell((int)NonLethalPoisonType.Leeching ))
                    poison = NonLethalPoisonType.Leeching;
                else if (SpellManager.HasSpell((int)NonLethalPoisonType.Crippling))
                    poison = NonLethalPoisonType.Crippling;
                else
                    poison = NonLethalPoisonType.None;
            }


            if ( poison != NonLethalPoisonType.None && Me.GetAuraTimeLeft((int)poison, true) < TimeSpan.FromMinutes(RefreshAtMinutesLeft))
                return poison;

            return NonLethalPoisonType.None;
        }

    }
}
