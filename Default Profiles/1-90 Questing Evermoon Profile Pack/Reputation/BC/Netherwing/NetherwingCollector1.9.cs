using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Media;

using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.Pathing;
using Styx.Plugins;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;


namespace HighVoltz
{
	class NetherwingCollector: HBPlugin
	{
        // ********* you can modify the following settings ********************
        private static bool CollectOre = true;
        private static bool CollectHerbs = true;
        private static bool CollectEggs = true;
        
        // ***** anything below here isn't meant to be modified *************
		public override string Name { get { return name; } }
		public override string Author { get { return "HighVoltz"; } }
		private readonly static Version _version = new Version(1, 9, 0, 1);
		public override Version Version { get { return _version; } }
		public override string ButtonText { get { return "NetherwingCollector"; } }
		public override bool WantButton { get { return false; } }

        private static string name { get { return "NetherwingCollector " + _version.ToString(); } }
        private static readonly LocalPlayer Me = StyxWoW.Me;

		public override void OnButtonPress()
		{
		}

		public override void Pulse()
		{
			try
			{
                if (!inCombat)
					findAndPickupObject();
			}
			catch (ThreadAbortException) { }
			catch (Exception e)
			{
				Log("Exception in Pulse:{0}", e);
			}
		}

		public static void MoveToLoc(WoWPoint loc)
		{
			Mount.MountUp(() => loc);
			while (loc.Distance(Me.Location) > 8)
			{
				Navigator.MoveTo(loc);
				Thread.Sleep(100);
				if (inCombat) return;
			}
			Thread.Sleep(2000);
		}

		static public void findAndPickupObject()
		{
			ObjectManager.Update();
			List<WoWGameObject> objList = ObjectManager.GetObjectsOfType<WoWGameObject>()
				.Where(o => (o.Distance <= 150 &&
                    (CollectHerbs && o.Entry == 185881 && Me.GetSkill(SkillLine.Herbalism).CurrentValue >= 350) ||
				    (CollectOre  && o.Entry == 185877 && Me.GetSkill(SkillLine.Mining).CurrentValue >= 350)    || 
                    (CollectEggs && o.Entry == 185915)
					
					))
				.OrderBy(o => o.Distance).ToList();
			foreach (WoWGameObject o in objList)
			{
				MoveToLoc(WoWMovement.CalculatePointFrom(o.Location, -4));
				if (inCombat) 
				{
					if (Me.Mounted) Mount.Dismount();
						return;
				}
				o.Interact();
				Thread.Sleep(GetPing * 2 + 250);
				while (!inCombat && Me.IsCasting)
					Thread.Sleep(100);
                Stopwatch lootTimer = new Stopwatch();
                // wait for loot frame to apear
                lootTimer.Reset();
                lootTimer.Start();
                while (LootFrame.Instance == null || !LootFrame.Instance.IsVisible)
                {
                    if (lootTimer.ElapsedMilliseconds > 5000)
                    {
                        Log("Loot window never showed up!");
                        return;
                    }
                    Thread.Sleep(100);
                }
                Lua.DoString("for i=1,GetNumLootItems() do ConfirmLootSlot(i) LootSlot(i) end");
                // wait for lootframe to close
                lootTimer.Reset();
                lootTimer.Start();
                Lua.DoString("StaticPopup1Button1:Click()");
                while (LootFrame.Instance != null && LootFrame.Instance.IsVisible )
                {
                    
                    Thread.Sleep(100);
                    if (lootTimer.ElapsedMilliseconds > 5000)
                    {
                        Log(Colors.Red,"looks like you have some addon interfering with loot. Disable any looting addons pls");
                        return;
                    }
                }
			}
		}
		static public void Log(string msg, params object[] args) { Logging.Write(msg, args); }

		static public void Log(Color c, string msg, params object[] args) { Logging.Write(c, msg, args); }
		
		static public bool inCombat
		{
			get
			{
				if (Me.Combat || !Me.IsAlive) return true;
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
	}
}

