using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.Helpers;
using Styx.WoWInternals;
using ObjectManager = Styx.WoWInternals.ObjectManager;


namespace ItemForAuraQuesthelper
{
	class Settings
	{
		// Variable for Kick to Lock the Settings he made
		// Set to true and nobody could change them accidently
		public bool LockUI = false;

		public bool Active1 = false;
		public bool Active2 = false;
		public bool Active3 = false;
		public bool Active4 = false;
		public bool Active5 = false;
		public bool Active6 = false;

		public string Item1 = "0";
		public string Item2 = "0";
		public string Item3 = "0";
		public string Item4 = "0";
		public string Item5 = "0";
		public string Item6 = "0";

		public string Aura1 = "0";
		public string Aura2 = "0";
		public string Aura3 = "0";
		public string Aura4 = "0";
		public string Aura5 = "0";
		public string Aura6 = "0";

		public string Quest11 = "0";
		public string Quest21 = "0";
		public string Quest31 = "0";
		public string Quest41 = "0";
		public string Quest51 = "0";
		public string Quest61 = "0";
		public string Quest12 = "0";
		public string Quest22 = "0";
		public string Quest32 = "0";
		public string Quest42 = "0";
		public string Quest52 = "0";
		public string Quest62 = "0";
		public string Quest13 = "0";
		public string Quest23 = "0";
		public string Quest33 = "0";
		public string Quest43 = "0";
		public string Quest53 = "0";
		public string Quest63 = "0";

		public bool Combat1 = false;
		public bool Combat2 = false;
		public bool Combat3 = false;
		public bool Combat4 = false;
		public bool Combat5 = false;
		public bool Combat6 = false;

		string File = "Plugins\\ItemForAuraQuesthelper\\";
		public Settings()
		{
			if (StyxWoW.Me != null)
				try
				{
					Load();
				}
				catch (Exception e)
				{
					Logging.Write(e.Message);
				}
		}


				public void Load()
		{
			//    XmlTextReader reader;
			XmlDocument xml = new XmlDocument();
			XmlNode xvar;

			string sPath = Process.GetCurrentProcess().MainModule.FileName;
			sPath = Path.GetDirectoryName(sPath);
			sPath = Path.Combine(sPath, File);

			if (!Directory.Exists(sPath))
			{
				Logging.WriteDiagnostic("ItemForAura: Creating config directory");
				Directory.CreateDirectory(sPath);
			}

			sPath = Path.Combine(sPath, "ItemForAura.config");

			Logging.WriteDiagnostic("ItemForAura: Loading config file: {0}", sPath);
			System.IO.FileStream fs = new System.IO.FileStream(@sPath, System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite);
			try
			{
				xml.Load(fs);
				fs.Close();
			}
			catch (Exception e)
			{
				Logging.Write(e.Message);
				Logging.Write("ItemForAura: Continuing with Default Config Values");
				fs.Close();
				return;
			}

			//            xml = new XmlDocument();

			try
			{
				//                xml.Load(reader);
				if (xml == null)
					return;
// ------------ Active ----------
				xvar = xml.SelectSingleNode("//ItemForAura/Active1");
				if (xvar != null)
				{
					Active1 = Convert.ToBoolean(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Active2");
				if (xvar != null)
				{
					Active2 = Convert.ToBoolean(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Active3");
				if (xvar != null)
				{
					Active3 = Convert.ToBoolean(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Active4");
				if (xvar != null)
				{
					Active4 = Convert.ToBoolean(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Active5");
				if (xvar != null)
				{
					Active5 = Convert.ToBoolean(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Active6");
				if (xvar != null)
				{
					Active6 = Convert.ToBoolean(xvar.InnerText);
				}
// ------------ Items ----------
				xvar = xml.SelectSingleNode("//ItemForAura/Item1");
				if (xvar != null)
				{
					Item1 = Convert.ToString(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Item2");
				if (xvar != null)
				{
					Item2 = Convert.ToString(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Item3");
				if (xvar != null)
				{
					Item3 = Convert.ToString(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Item4");
				if (xvar != null)
				{
					Item4 = Convert.ToString(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Item5");
				if (xvar != null)
				{
					Item5 = Convert.ToString(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Item6");
				if (xvar != null)
				{
					Item6 = Convert.ToString(xvar.InnerText);
				}
// ------------ Auras ----------
				xvar = xml.SelectSingleNode("//ItemForAura/Aura1");
				if (xvar != null)
				{
					Aura1 = Convert.ToString(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Aura2");
				if (xvar != null)
				{
					Aura2 = Convert.ToString(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Aura3");
				if (xvar != null)
				{
					Aura3 = Convert.ToString(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Aura4");
				if (xvar != null)
				{
					Aura4 = Convert.ToString(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Aura5");
				if (xvar != null)
				{
					Aura5 = Convert.ToString(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Aura6");
				if (xvar != null)
				{
					Aura6 = Convert.ToString(xvar.InnerText);
				}
// ------------ Quest x1 ----------
				xvar = xml.SelectSingleNode("//ItemForAura/Quest11");
				if (xvar != null)
				{
					Quest11 = Convert.ToString(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Quest21");
				if (xvar != null)
				{
					Quest21 = Convert.ToString(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Quest31");
				if (xvar != null)
				{
					Quest31 = Convert.ToString(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Quest41");
				if (xvar != null)
				{
					Quest41 = Convert.ToString(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Quest51");
				if (xvar != null)
				{
					Quest51 = Convert.ToString(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Quest61");
				if (xvar != null)
				{
					Quest61 = Convert.ToString(xvar.InnerText);
				}
// ------------ Quest x2 ----------
				xvar = xml.SelectSingleNode("//ItemForAura/Quest12");
				if (xvar != null)
				{
					Quest12 = Convert.ToString(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Quest22");
				if (xvar != null)
				{
					Quest22 = Convert.ToString(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Quest32");
				if (xvar != null)
				{
					Quest32 = Convert.ToString(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Quest42");
				if (xvar != null)
				{
					Quest42 = Convert.ToString(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Quest52");
				if (xvar != null)
				{
					Quest52 = Convert.ToString(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Quest62");
				if (xvar != null)
				{
					Quest62 = Convert.ToString(xvar.InnerText);
				}
// ------------ Quest x3 ----------
				xvar = xml.SelectSingleNode("//ItemForAura/Quest13");
				if (xvar != null)
				{
					Quest13 = Convert.ToString(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Quest23");
				if (xvar != null)
				{
					Quest23 = Convert.ToString(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Quest33");
				if (xvar != null)
				{
					Quest33 = Convert.ToString(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Quest43");
				if (xvar != null)
				{
					Quest43 = Convert.ToString(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Quest53");
				if (xvar != null)
				{
					Quest53 = Convert.ToString(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Quest63");
				if (xvar != null)
				{
					Quest63 = Convert.ToString(xvar.InnerText);
				}
// ------------ Combat ----------
				xvar = xml.SelectSingleNode("//ItemForAura/Combat1");
				if (xvar != null)
				{
					Combat1 = Convert.ToBoolean(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Combat2");
				if (xvar != null)
				{
					Combat2 = Convert.ToBoolean(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Combat3");
				if (xvar != null)
				{
					Combat3 = Convert.ToBoolean(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Combat4");
				if (xvar != null)
				{
					Combat4 = Convert.ToBoolean(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Combat5");
				if (xvar != null)
				{
					Combat5 = Convert.ToBoolean(xvar.InnerText);
				}
				xvar = xml.SelectSingleNode("//ItemForAura/Combat6");
				if (xvar != null)
				{
					Combat6 = Convert.ToBoolean(xvar.InnerText);
				}

			}
			catch (Exception e)
			{
				Logging.WriteDiagnostic("ItemForAura: PROJECTE EXCEPTION, STACK=" + e.StackTrace);
				Logging.WriteDiagnostic("ItemForAura: PROJECTE EXCEPTION, SRC=" + e.Source);
				Logging.WriteDiagnostic("ItemForAura: PROJECTE : " + e.Message);
			}
		}
	}
}
