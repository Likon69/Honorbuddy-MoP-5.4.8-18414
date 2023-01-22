using System;
using System.Collections;

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



namespace KBA
{
    public enum BuffWhere
    {
        NoWhere = 0,
        Everywhere,
        TimelessIsle,
        Dungeon,
        Party,
        Raid,
        PartyOrRaid
    }

    public enum BuffWhen
    {
        Never = 0,
        Everytime,
        HeroismOrBloodlust,
        LowHp,
        LowMana
    }
}
