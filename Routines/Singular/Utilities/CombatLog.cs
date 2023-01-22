
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Singular
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
                ulong cachedSourceGuid = SourceGuid;
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>(true, true).FirstOrDefault(
                        o => o.IsValid && (o.Guid == cachedSourceGuid || o.DescriptorGuid == cachedSourceGuid));
            }
        }

        public string SourceName { get { return Args[4].ToString(); } }

        public int SourceFlags { get { return (int)(double)Args[5]; } }

        public ulong DestGuid { get { return ArgToGuid(Args[7]); } }

        public WoWUnit DestUnit
        {
            get
            {
                ulong cachedDestGuid = DestGuid;
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>(true, true).FirstOrDefault(
                        o => o.IsValid && (o.Guid == cachedDestGuid || o.DescriptorGuid == cachedDestGuid));
            }
        }

        public string DestName { get { return Args[8].ToString(); } }

        public int DestFlags { get { return (int)(double)Args[9]; } }

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
            string svalue = o.ToString();
            ulong guid = 0;
            
            if (!string.IsNullOrEmpty(svalue))
            {           
                svalue = svalue.Replace("0x", string.Empty);
                try
                {
                    guid = ulong.Parse( svalue, NumberStyles.HexNumber);
                }
                catch
                {
                    Logger.WriteDebug("error parsing Guid '{0}'", o.ToString());
                }
            }

            return guid;
        }
    }
}