/*
 * NOTE:    DO NOT POST ANY MODIFIED VERSIONS OF THIS TO THE FORUMS.
 * 
 *          DO NOT UTILIZE ANY PORTION OF THIS PLUGIN WITHOUT
 *          THE PRIOR PERMISSION OF AUTHOR.  PERMITTED USE MUST BE
 *          ACCOMPANIED BY CREDIT/ACKNOWLEDGEMENT TO ORIGINAL AUTHOR.
 * 
 * Author:  Bobby53
 * 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Threading;
using System.Diagnostics;

using Levelbot.Actions.Combat;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using CommonBehaviors.Actions;
using Action = Styx.TreeSharp.Action;
using Sequence = Styx.TreeSharp.Sequence;
using Styx.WoWInternals;

using Bobby53;
using Styx.CommonBot;

namespace Styx.Bot.CustomBots
{
    public class Tank
    {
        public static ulong _tankGuid = 0;
        static ObjectInvalidateDelegate invalidDelegate;

        public static ulong Guid
        {
            get { return _tankGuid; }
            set
            {
                WoWPartyMember pm = LazyRaider.GroupMemberInfos.FirstOrDefault(m => m != null && m.Guid == value);
                if (pm == null)
                    _tankGuid = 0;
                else
                    _tankGuid = value;
            }
        }

        public static WoWPartyMember Current
        {

            get
            {
                return LazyRaider.GroupMemberInfos.FirstOrDefault(m => m != null && m.Guid == Tank.Guid);
            }
            set
            {
                Tank.Guid = value == null ? 0 : value.Guid;
            }
        }

        public static WoWPlayer Player
        {
            get
            {
                WoWPlayer p = null;
                WoWPartyMember pm = Current;
                if (pm != null)
                    p = pm.ToPlayer();
                return p;
            }
        }

        public static WoWPoint Location
        {
            get
            {
#if BUG_LOCATION_3D_NOT_UPDATED
                WoWPlayer WoWPartyMember pm = Tank.Current;
                if (pm == null)
                    return new WoWPoint();
                return pm.Location3D;
#else
                WoWPoint pt = new WoWPoint();
                WoWPlayer p = Tank.Current.ToPlayer();
                if (p != null)
                    pt = p.Location;
                else
                    pt = Tank.Current.Location3D;

                return pt;
#endif
            }
        }

        public static double Distance
        {
            get
            {
                return Tank.Location.Distance(StyxWoW.Me.Location);
            }
        }


        public static uint Health
        {
            get
            {
                WoWPartyMember pm = Tank.Current;
                if (pm == null)
                    return 0;

                WoWPlayer p = pm.ToPlayer();
                return p == null ? pm.Health : (uint)p.HealthPercent;
            }
        }

        public static void Clear()
        {
            LazyRaider.Dlog("Tank.Clear:  cleared tank");

            RaFHelper.ClearLeader();
            Tank.Guid = 0;
        }

        public static void SetAsLeader()
        {
            SetAsLeader(Guid);
        }

        public static void SetAsLeader(ulong guidNew)
        {
            WoWPlayer p = null;
            Tank.Guid = guidNew;

            if (Tank.Guid != 0)
                p = Tank.Current.ToPlayer();

            if (p == null)
            {
                LazyRaider.Dlog("Tank.SetAsLeader:  out of range and cannot resolve {0}", LazyRaider.Safe_UnitName(Tank.Current));
                RaFHelper.ClearLeader();
            }
            else
            {
                LazyRaider.Dlog("Tank.SetAsLeader:  setting tank {0}", LazyRaider.Safe_UnitName(p));
                RaFHelper.SetLeader(p);
                invalidDelegate = new ObjectInvalidateDelegate(RaFLeader_OnInvalidate);
                RaFHelper.Leader.OnInvalidate += invalidDelegate;
            }
        }

        public static bool IsLeader()
        {
            return RaFHelper.Leader != null && RaFHelper.Leader.Guid == Tank.Guid;
        }

        public static void RaFLeader_OnInvalidate()
        {
            LazyRaider.Dlog("RaFLeader_OnInvalidate: tank reference now invalid, resetting");
            RaFHelper.ClearLeader();
            invalidDelegate = null;
        }

    }
}
