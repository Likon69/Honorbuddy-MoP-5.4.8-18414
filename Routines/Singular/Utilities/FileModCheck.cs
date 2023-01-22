using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using System.Reflection;

namespace Singular.Utilities
{
    [XmlRoot("FileCheckData")]
    [XmlInclude(typeof(FileCheck))]
    [XmlInclude(typeof(DateTime))]
    public class FileCheckList
    {
        [XmlElement("ScanTime")]
        public string ScanTime { get; set; }

        [XmlArray("FileList")]
        [XmlArrayItem("File")]
        public List<FileCheck> Filelist { get; set; }

        // load list from xml file
        public void Load(string sFilename)
        {
            XmlSerializer ser = new XmlSerializer(typeof(FileCheckList));
            StreamReader reader = new StreamReader(sFilename);
            FileCheckList fcl = (FileCheckList)ser.Deserialize(reader);
            Filelist = fcl.Filelist;
            ScanTime = fcl.ScanTime;
        }

        // save list to xml file
        public void Save(string sFilename)
        {
            XmlSerializer ser = new XmlSerializer(typeof(FileCheckList), new Type[] { typeof(FileCheck) });
            StreamWriter writer = new StreamWriter(sFilename);
            ser.Serialize(writer, this);
        }

        // generate list from a given path recursing downwards
        public void Generate(string sPath)
        {
            ScanTime = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss");

            Filelist = new List<FileCheck>();
            DirectoryInfo di = new DirectoryInfo(sPath);

            foreach (var fi in di.EnumerateFiles("*.cs", SearchOption.AllDirectories))
            {
                if (fi.Name.ToLower() != "assemblyinfo.cs")
                {
                    Filelist.Add(new FileCheck(fi.FullName));
                }
            }

            // get length of fullpath
            string sDelims = "\\/:";
            int len = di.FullName.Length;
            if (len > 0 && !sDelims.Contains(di.FullName[len - 1]))
                len++;

            // normalize paths by removing rootpath
            foreach (FileCheck fc in Filelist)
                fc.Name = fc.Name.Substring(len);

            Filelist = Filelist.OrderBy(fc => fc.Name).ToList();
        }

        public List<FileCheck> Compare(FileCheckList baseline)
        {
            Dictionary<string, FileCheck> xdict = Filelist.OrderBy(fc => fc.Name).ToDictionary(k => k.Name, v => v);
            List<FileCheck> y = baseline.Filelist.OrderBy(fc => fc.Name).ToList();
            y.RemoveAll(fc => xdict.ContainsKey(fc.Name) && xdict[fc.Name].Size == fc.Size);
            return y;
        }

        public static List<FileCheck> Test(string singularFolder)
        {
            FileCheckList fclactual = new FileCheckList();
            fclactual.Generate(singularFolder);

            FileCheckList fclfile = new FileCheckList();
            fclfile.Load(Path.Combine(singularFolder, "singular.xml"));

            List<FileCheck> err = fclfile.Compare(fclactual);
            return err;
        }
    }

    [XmlType("File")]
    public class FileCheck
    {
        [XmlAttribute("Hash")]
        public long Size { get; set; }

        [XmlAttribute("Name")]
        public string Name { get; set; }

        public FileCheck()
        {
        }

        public FileCheck(string sFilename)
        {
            Name = sFilename;
            FileInfo fi = new FileInfo(sFilename);
            Size = fi.Length;
        }
    }
}
