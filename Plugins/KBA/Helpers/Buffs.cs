using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.IO;
using Styx.WoWInternals;
using System.Xml.Serialization;

namespace KBA.Helpers
{
    public class Buffs
    {
        private static Buffs _instance;
        public static string ConfigFileName = "BuffsToKeepActive.xml";
        public SpellList SpellList;

        public Buffs()
        {
            string file = Path.Combine(KeepBuffActive.GlobalSettingsPath, ConfigFileName);
            SpellList = new SpellList(file);
        }

        public static Buffs Instance
        {
            get { return _instance ?? (_instance = new Buffs()); }
            set { _instance = value; }
        }
    }

    [XmlRoot("SpellList")]
    [XmlInclude(typeof(BuffEntry))]
    public class SpellList
    {
        [XmlElement("Version")]
        public string Version { get; set; }

        [XmlElement("Desc")]
        public string Description { get; set; }

        [XmlArray("Spells")]
        [XmlArrayItem("SpellEntry")]
        public List<BuffEntry> Spells { get; set; }

        [Browsable(false)]
        internal HashSet<int> SpellIds { get; set; }

        public string Filename;

        public SpellList()
        {
            Spells = new List<BuffEntry>();
            SpellIds = new HashSet<int>();
        }

        public SpellList(string sFilename)
        {
            Filename = sFilename;
            Spells = new List<BuffEntry>();
            SpellIds = new HashSet<int>();

            if (!File.Exists(Filename))
            {
                LoadDefaults();
                Save(Filename);
                return;
            }

            Load(Filename);
        }

        public void Add(int id, string name = null)
        {
            var se = new BuffEntry(id, name);
            Spells.Add(se);
            SpellIds.Add(id);
        }
        //Add(93351, "Potion of Luck",135855,"Potion of Luck", BuffWhere.Everywhere, BuffWhen.Everytime);
        public void Add(int objectid, string objectname,int buffid,string buffname, BuffWhere where, BuffWhen when)
        {
            var se = new BuffEntry(objectid, objectname, buffid, buffname,where, when);
            Spells.Add(se);
            SpellIds.Add(objectid);
        }

        public bool Contains(int id)
        {
            return SpellIds.Contains(id);
        }

        // load list from xml file
        public void Load(string sFilename = null)
        {
            if (string.IsNullOrEmpty(sFilename))
                sFilename = Filename;

            try
            {
                var ser = new XmlSerializer(typeof(SpellList));
                var reader = new StreamReader(sFilename);
                var fcl = (SpellList)ser.Deserialize(reader);
                Description = fcl.Description;
                Spells = fcl.Spells;
                SpellIds = new HashSet<int>(fcl.Spells.Select(sp => sp.ObjectId).ToArray());
            }
            catch (Exception ex)
            {
                Logger.DebugLog(ex.StackTrace);
                Spells = new List<BuffEntry>();
                SpellIds = new HashSet<int>();
            }
        }

        // save list to xml file
        public void Save(string sFilename = null)
        {
            if (string.IsNullOrEmpty(sFilename))
                sFilename = Filename;

            try
            {
                if (!Directory.Exists(Path.GetDirectoryName(sFilename)))
                    Directory.CreateDirectory(Path.GetDirectoryName(sFilename));

                var ser = new XmlSerializer(typeof(SpellList), new[] { typeof(BuffEntry) });
                using (var writer = new StreamWriter(sFilename))
                {
                    Version = "1.0.2";
                    ser.Serialize(writer, this);
                }
            }
            catch (Exception ex)
            {
                Logger.DebugLog(ex.StackTrace);
            }
        }
        public void LogEntries()
        {
            Logger.InfoLog("Buff-Entries: Statistics");
            var totalCount=Buffs.Instance.SpellList.Spells.Count();
            var activeCount=Buffs.Instance.SpellList.Spells.Where(q=>q.BuffCondition!=BuffWhen.Never && q.BuffLocation!=BuffWhere.NoWhere).Count();
            var inactiveCount = Buffs.Instance.SpellList.Spells.Where(q => q.BuffCondition == BuffWhen.Never || q.BuffLocation == BuffWhere.NoWhere).Count();
            Logger.InfoLog("There are currently {0} entries, {1} are active, {2} are not active", totalCount, activeCount, inactiveCount);
            if(activeCount+inactiveCount!=totalCount)
            {
                Logger.InfoLog("We have a logical sum error: {0} summarized, {1} existing", activeCount + inactiveCount, totalCount);
            }
            var itList = Buffs.Instance.SpellList.Spells;
            if (itList.Count() > 0)
            {
                foreach (var itUse in itList)
                {
                    Logger.InfoLog("{0}", itUse);                
                }
            }
        }
        private void LoadDefaults()
        {
            //Potions
            Add(93351, "Potion of Luck",135855,"Potion of Luck", BuffWhere.Everywhere, BuffWhen.Everytime);
            //Flasks - shouldn't be used with this plugin, this is for general stuff, not class / stat specific ones
            Add(76085, "Flask of the Warm Sun", 105691, "Flask of the Warm Sun", BuffWhere.Raid, BuffWhen.Never); //StatIntellect || RoleHeal || RolerangedDps
            Add(76086, "Flask of Falling Leaves", 105693, "Flask of Falling Leaves", BuffWhere.Raid, BuffWhen.Never); //StatSpirit || RoleHeal
            Add(76087, "Flask of the Earth", 105694, "Flask of the Earth", BuffWhere.Raid, BuffWhen.Never); //RoleTank
            Add(76088, "Flask of Winter's Bite", 105696, "Flask of Winter's Bite", BuffWhere.Raid, BuffWhen.Never); //StatStrength
            Add(76084, "Flask of Spring Blossoms", 105689, "Flask of Spring Blossoms", BuffWhere.Raid, BuffWhen.Never); //StatAgility || RoleRangedDps || RoleMeleeDps

            //TimelessIsleItems
            Add(103642, "Book of the Ages", 147226, "Book of the Ages", BuffWhere.TimelessIsle, BuffWhen.Everytime); //TimelessIsle
            Add(103643, "Dew of Eternal Morning", 147476, "Dew of Eternal Morning", BuffWhere.TimelessIsle, BuffWhen.Everytime); //TimelessIsle
            Add(103641, "Singing Crystal", 147055, "Singing Crystal", BuffWhere.TimelessIsle, BuffWhen.Everytime); //TimelessIsle

        }
    }
    /*
    ObjectID -itemid
    ObjectName - itemname
    BuffId - id des Buffs
    BuffName - name des Buffs
    BuffWhere - Enum Everywhere,Raid,Party,RaidOrParty,TimelessIsle
    BuffWhen - Enum Everytime, Combat, Bloodlust,LowHp,LowMana
     */
    [XmlType("BuffEntry")]
    public class BuffEntry
    {
        private int _id;
        public override string ToString()
        {
            string output = string.Format("BuffEntry ObjectId: {0} - ObjectName: {1} - BuffId: {2} - BuffName: {3} - BuffLocation: {4} - BuffCondition: {5}", ObjectId, ObjectName, BuffId, BuffName, BuffLocation, BuffCondition);
            return output;
        }

        [XmlAttribute("ObjectId")]
        public int ObjectId
        {
            get
            {
                return _id;
            }
            set
            {
                _id = value;
                if (value == 0)
                    ObjectName = string.Empty;
            }
        }

        [XmlAttribute("ObjectName")]
        public string ObjectName { get; set; }

        [XmlAttribute("BuffId")]
        public int BuffId { get; set; }

        [XmlAttribute("BuffName")]
        public string BuffName { get; set; }

        [XmlAttribute("BuffLocation")]
        public BuffWhere BuffLocation { get; set; }

        [XmlAttribute("BuffCondition")]
        public BuffWhen BuffCondition { get; set; }

        public BuffEntry()
        {
            ObjectId = 0;
            ObjectName = String.Empty;
            BuffId = 0;
            BuffName = String.Empty;
            BuffCondition = BuffWhen.Never;
            BuffLocation = BuffWhere.NoWhere;
        }

        public BuffEntry(int id, string name = null)
        {
            ObjectId = id;
            ObjectName = name != null?string.Empty:name;
            BuffId = 0;
            BuffName = String.Empty;
            BuffCondition = BuffWhen.Never;
            BuffLocation = BuffWhere.NoWhere;
        }

        public BuffEntry(int id, string name, int buffid, string buffname, BuffWhere where, BuffWhen when)
        {
            ObjectId = id;
            if (name != null) ObjectName = name;
            BuffId = buffid;
            BuffName = buffname;
            BuffLocation = where;
            BuffCondition = when;
        }
    }
}
