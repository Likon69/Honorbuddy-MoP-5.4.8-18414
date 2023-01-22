using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Windows.Media;

using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.Plugins;


namespace katzerle
{
    class ThundermarKegCollector : HBPlugin
    {
        // ***** anything below here isn't meant to be modified *************
        public static string name { get { return "ThundermarKegCollector " + _version.ToString(); } }
        public override string Name { get { return name; } }
        public override string Author { get { return "katzerle"; } }
        private readonly static Version _version = new Version(0, 1);
        public override Version Version { get { return _version; } }
        public override string ButtonText { get { return "No Settings"; } }
        public override bool WantButton { get { return false; } }
        public static LocalPlayer Me = StyxWoW.Me;

        public override void Pulse()
        {
            try
            {
                if (!inCombat && (HasQuest(28861) || HasQuest(28862)))
                    findAndPickupObjectKeg();
            }
            catch (ThreadAbortException) { }
            catch (Exception e)
            {
                Log("Exception in Pulse:{0}", e);
            }
        }

        public static void movetoLoc(WoWPoint loc)
        {
            while (loc.Distance(Me.Location) > 10)
            {
                Navigator.MoveTo(loc);
                Thread.Sleep(100);
                if (inCombat) return;
            }
            Thread.Sleep(2000);
        }

        static public void findAndPickupObjectKeg()
        {
            ObjectManager.Update();
            List<WoWGameObject> objList = ObjectManager.GetObjectsOfType<WoWGameObject>()
                .Where(o => ((o.Entry == 206195) 	// Thundermar Ale Keg
                || (o.Entry == 206289)				// Wildhammer Food Stores
                || (o.Entry == 206291)				// Wildhammer Food Stores
                || (o.Entry == 206290)))				// Wildhammer Food Stores
                .OrderByDescending(o => o.Entry).ToList();
            foreach (WoWGameObject o in objList)
            {
                if (o.Location.Distance(Me.Location) < 40)
                {
                    if (((o.Entry == 206195) && !IsQuestCompleted(28861)) || (((o.Entry == 206289) || (o.Entry == 206291) || (o.Entry == 206290)) && !IsQuestCompleted(28862)))
                    {
                        movetoLoc(o.Location);
                        if (inCombat) return;
                        if (Me.Mounted)
                            Mount.Dismount();
                        o.Interact();

                        if (o.Entry == 206195)
                        {
                            Thread.Sleep(6000);
                            Log("Plugin Keg Collector: Collect Tundermar Ale Keg");
                        }
                        else if ((o.Entry == 206289) || (o.Entry == 206291) || (o.Entry == 206290))
                        {
                            Thread.Sleep(3000);
                            Log("Plugin Keg Collector: Wildhammer Food Stores");
                        }
                        return;
                    }
                }
            }
        }

        private static bool IsQuestCompleted(uint ID)
        {
            //to make sure every header is expanded in quest log
            Lua.DoString("ExpandQuestHeader(0)");
            //number of values in quest log (includes headers like "Durator")
            int QuestCount = Lua.GetReturnVal<int>("return select(1, GetNumQuestLogEntries())", 0);
            for (int i = 1; i <= QuestCount; i++)
            {
                List<string> QuestInfo = Lua.GetReturnValues("return GetQuestLogTitle(" + i + ")", "raphus.lua");

                //pass if the index isHeader or isCollapsed
                if (QuestInfo[4] == "1" || QuestInfo[5] == "1")
                    continue;

                string QuestStatus = null;
                if (QuestInfo[6] == "1")
                    QuestStatus = "completed";
                else if (QuestInfo[6] == "-1")
                    QuestStatus = "failed";
                else
                    QuestStatus = "in progress";
                if (QuestInfo[8] == Convert.ToString(ID) && QuestStatus == "completed")
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasQuest(uint ID)
        {
            //to make sure every header is expanded in quest log
            Lua.DoString("ExpandQuestHeader(0)");
            //number of values in quest log (includes headers like "Durator")
            int QuestCount = Lua.GetReturnVal<int>("return select(1, GetNumQuestLogEntries())", 0);
            for (int i = 1; i <= QuestCount; i++)
            {
                List<string> QuestInfo = Lua.GetReturnValues("return GetQuestLogTitle(" + i + ")", "raphus.lua");

                //pass if the index isHeader or isCollapsed
                if (QuestInfo[4] == "1" || QuestInfo[5] == "1")
                    continue;

                string QuestStatus = null;
                if (QuestInfo[8] == Convert.ToString(ID))
                {
                    return true;
                }
            }
            return false;
        }

        static public bool inCombat
        {
            get
            {
                if (Me.Combat || Me.IsDead || Me.IsGhost) return true;
                return false;
            }
        }

        public static int GetPing
        {
            get
            {
                return Lua.GetReturnVal<int>("return GetNetStats()", 2);
            }
        }

        public override void OnButtonPress()
        {
        }

        static public void Log(string msg, params object[] args) { Logging.Write(msg, args); }
        static public void Log(Color c, string msg, params object[] args) { Logging.Write(c, msg, args); }
    }
}

