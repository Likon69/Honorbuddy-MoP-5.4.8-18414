using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using System.Reflection;

using System.ComponentModel;
using Styx;
using Styx.Helpers;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;
using Singular.Managers;
using Styx.WoWInternals;

namespace Singular.Settings
{
    [XmlRoot("SpellList")]
    [XmlInclude(typeof(SpellEntry))]
    public class SpellList
    {
        [XmlElement("Version")]
        public string Version { get; set; }

        [XmlElement("Desc")]
        public string Description { get; set; }

        [XmlArray("Spells")]
        [XmlArrayItem("SpellEntry")]
        public List<SpellEntry> Spells { get; set; }

        [Browsable(false)]
        internal HashSet<int> SpellIds { get; set; }

        public string Filename;

        public SpellList()
        {
            Spells = new List<SpellEntry>();
            SpellIds = new HashSet<int>();
        }

        public SpellList(string sFilename, int[] Defaults)
        {
            Filename = sFilename;
            Spells = new List<SpellEntry>();
            SpellIds = new HashSet<int>();

            foreach (var id in Defaults)
                Add(id);

            if (!File.Exists(Filename))
            {
                Save(Filename);
                return;
            }

            Load(Filename);
        }

        public void Add(int id, string name = null)
        {
            SpellEntry se = new SpellEntry(id, name);
            Spells.Add( se );
            SpellIds.Add( id);
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
                XmlSerializer ser = new XmlSerializer(typeof(SpellList));
                StreamReader reader = new StreamReader(sFilename);
                SpellList fcl = (SpellList)ser.Deserialize(reader);
                Description = fcl.Description;
                Spells = fcl.Spells;
                SpellIds = new HashSet<int>(fcl.Spells.Select(sp => sp.Id).ToArray());
            }
            catch (Exception ex)
            {
                Styx.Common.Logging.WriteException(ex);
                Spells = new List<SpellEntry>();
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

                XmlSerializer ser = new XmlSerializer(typeof(SpellList), new Type[] { typeof(SpellEntry) });
                using (StreamWriter writer = new StreamWriter(sFilename))
                {
                    Version = SingularRoutine.GetSingularVersion().ToString();
                    ser.Serialize(writer, this);
                }  
            }
            catch (Exception ex)
            {
                Styx.Common.Logging.WriteException(ex);
            }
        }

    }

    [XmlType("SpellEntry")]
    public class SpellEntry
    {
        private int _id;

        [XmlAttribute("Id")]
        public int Id
        {
            get
            {
                return _id;
            }
            set
            {
                _id = value;
                if (value == 0)
                    Name = string.Empty;
                else
                {
                    WoWSpell spell = WoWSpell.FromId(value);
                    if (spell != null)
                        Name = spell.Name;
                    else
                        Name = "(unknown)";
                }
            }
        }

        [XmlAttribute("Name")]
        public string Name { get; set; }

        public SpellEntry()
        {
            Id = 0;
            Name = String.Empty;
        }

        public SpellEntry(int id, string name = null )
        {
            Id = id;
            if ( name != null )
                Name = name;
        }
    }

}