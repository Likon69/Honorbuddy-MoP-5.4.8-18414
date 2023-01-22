using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.AreaManagement;
using Styx.Pathing;
using Styx.Helpers;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.Plugins;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows.Media;
using Styx.CommonBot.Frames;

using P = RaresAndChests.RareSettings;

namespace RaresAndChests
{
    public class Main : HBPlugin
    {
        public override string Name { get { return "Find Rares and Chests"; } }
        public override string Author { get { return "Pasterke"; } }
        public override bool WantButton { get { return true; } }
        public override Version Version { get { return _version; } }
        private readonly Version _version = new Version(2, 4);
        public override string ButtonText { get { return "Show Settings"; } }
        public override void OnButtonPress()
        {
            new Form1().ShowDialog();
        }
        public static bool Chests = true;
        public static string LastLog { get; set; }
        public static string lastChestLog { get; set; }
        public static string usedBot { get { return BotManager.Current.Name.ToUpper(); } }
        private static bool Miner;
        private static bool Skinner;
        private static bool Herbalist;



        /// <summary>
        /// Distance to look for rares
        /// </summary>
        public static float Range = P.myPrefs.radius;

        public override void OnEnable()
        {
            Logging.Write(Colors.CornflowerBlue, "Kill Rares and Loot Chests version " + _version + " enabled.");
            if (StyxWoW.Me.GetSkill(SkillLine.Mining).CurrentValue != 0) { Miner = true; }
            if (StyxWoW.Me.GetSkill(SkillLine.Skinning).CurrentValue != 0) { Skinner = true; }
            if (StyxWoW.Me.GetSkill(SkillLine.Herbalism).CurrentValue != 0) { Herbalist = true; }
        }

        public override void Pulse()
        {
            try
            {
                if (!StyxWoW.Me.IsCasting && Lootables.Count() > 0 && !StyxWoW.Me.IsMoving && !StyxWoW.Me.Combat && P.myPrefs.CombatLooter && AutoBot)
                {
                    if (LootFrame.Instance != null && LootFrame.Instance.IsVisible)
                    {
                        LootFrame.Instance.LootAll();
                    }
                    var lootTarget = Lootables.FirstOrDefault();

                    if (lootTarget.Distance > lootTarget.InteractRange)
                    {
                        Logging.Write("Moving to loot {0}.", lootTarget.Name);
                        Navigator.MoveTo(lootTarget.Location);
                    }
                    else
                    {
                        lootTarget.Interact();
                    }
                }
                // Loot chests
                if (BotPoi.Current.Type == PoiType.None && P.myPrefs.ChestFinder && AutoBot)
                {
                    List<WoWGameObject> chests = ObjectManager.GetObjectsOfTypeFast<WoWGameObject>().Where(c => c != null
                                                  && c.Distance <= Range
                                                  && c.SubType == WoWGameObjectType.Chest
                                                  && !Blacklist.Contains(c.Guid, BlacklistFlags.All)
                                                  && c.Distance <= Range
                                                  && c.CanLoot == true).OrderBy(c => c.Distance).ToList();
                    if (chests.Count() > 0)
                    {
                        WoWObject chestItem = chests.FirstOrDefault();
                        string lastChest = "Moving towards lootable " + chestItem.Name + " at Distance: " + System.Math.Round(chestItem.Distance, 0);
                        if (lastChestLog != lastChest)
                        {
                            Logging.Write(Colors.CornflowerBlue, lastChest);
                            lastChestLog = lastChest;
                        }
                        BotPoi.Current = new BotPoi(chestItem, PoiType.Loot);
                    }
                    return;
                }
                if (searchRares().Count() > 0 && (P.myPrefs.EliteRares || P.myPrefs.NormalRares))
                {
                    WoWUnit myTarget = searchRares().FirstOrDefault();

                    if (myTarget != null && !Blacklist.Contains(myTarget, BlacklistFlags.All) && AutoBot)
                    {
                        string LogMsg = "Moving towards rare " + myTarget.Name + " at Distance: " + System.Math.Round(myTarget.Distance, 0);
                        if (myTarget.Distance > 30 && LastLog != LogMsg)
                        {
                            Logging.Write(Colors.CornflowerBlue, LogMsg);
                            LastLog = LogMsg;
                        }
                        BotPoi.Current = new BotPoi(myTarget, PoiType.Kill);
                    }
                }

            }
            catch (Exception e) { Logging.Write("Pulse: " + e); }
        }
        public static IOrderedEnumerable<WoWUnit> Lootables
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>(false, false).Where(
                        p => p.Lootable && p.Distance <= Range).OrderBy(l => l.Distance);
            }
        }
        public static List<WoWUnit> searchRares()
        {
            return ObjectManager.GetObjectsOfTypeFast<WoWUnit>().Where(p => p != null
                && p.IsAlive
                && ((p.Classification == WoWUnitClassificationType.Rare && P.myPrefs.NormalRares) || (p.Classification == WoWUnitClassificationType.RareElite && P.myPrefs.EliteRares))
                && p.Distance <= Range).OrderBy(p => p.Distance).ToList();
        }

        public static bool AutoBot
        {
            get
            {
                return usedBot.Contains("QUEST") || usedBot.Contains("GRIND") || usedBot.Contains("GATHER") || usedBot.Contains("ANGLER") || usedBot.Contains("ARCHEO");

            }
        }
        public static bool CanHerb(WoWUnit unit)
        {
            if (!Herbalist) return false;
            var p = ObjectManager.GetObjectsOfTypeFast<WoWUnit>().Where(k => k != null && k.SkinType == WoWCreatureSkinType.Herb && k.Distance <= Range).FirstOrDefault();
            return p != null ? true : false;
        }
    }
}
