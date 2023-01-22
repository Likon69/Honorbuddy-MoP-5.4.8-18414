
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Styx.Bot.CustomBots
{
    internal class CombatLogEventArgs : LuaEventArgs
    {
        public CombatLogEventArgs(string eventName, uint fireTimeStamp, object[] args)
            : base(eventName, fireTimeStamp, args)
        {
        }

        public double Timestamp { get { return (double)Args[0]; } }

        public string Event { get { return Args[1].ToString(); } }

        // Is this a string? bool? what? What the hell is it even used for?
        // it's a boolean, and it doesn't look like it has any real impact codewise apart from maybe to break old addons? - exemplar 4.1
        public string HideCaster { get { return Args[2].ToString(); } }

        public ulong SourceGuid { get { return ArgToGuid(Args[3]); } }

        public WoWUnit SourceUnit
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>(true, true).FirstOrDefault(
                        o => o.IsValid && (o.Guid == SourceGuid || o.DescriptorGuid == SourceGuid));
            }
        }

        public string SourceName { get { return Args[4].ToString(); } }

        public int SourceFlags { get { return (int)(double)Args[5]; } }

        public int SourceRaidFlags { get { return (int)(double)Args[6]; } }

        public ulong DestGuid { get { return ArgToGuid(Args[7]); } }

        public WoWUnit DestUnit
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>(true, true).FirstOrDefault(
                        o => o.IsValid && (o.Guid == DestGuid || o.DescriptorGuid == DestGuid));
            }
        }

        public string DestName { get { return Args[8].ToString(); } }

        public int DestFlags { get { return (int)(double)Args[9]; } }

        public int DestRaidFlags { get { return (int)(double)Args[10]; } }

        public int SpellId { get { return (int)(double)Args[11]; } }

        public WoWSpell Spell { get { return WoWSpell.FromId(SpellId); } }

        public string SpellName { get { return Args[12].ToString(); } }

        public WoWSpellSchool SpellSchool { get { return (WoWSpellSchool)(int)(double)Args[13]; } }

        public object[] SuffixParams
        {
            get
            {
                var args = new List<object>();
                for (int i = 11; i < Args.Length; i++)
                {
                    if (Args[i] != null)
                    {
                        args.Add(args[i]);
                    }
                }
                return args.ToArray();
            }
        }

        private static ulong ArgToGuid(object o)
        {
            string svalue = o.ToString().Replace("0x", string.Empty);
            try
            {
                return ulong.Parse( svalue, NumberStyles.HexNumber);
            }
            catch
            {
                LazyRaider.Log("error parsing Guid '{0}'", o.ToString());
            }

            return 0;
        }
    }
}