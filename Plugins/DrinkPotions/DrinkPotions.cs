﻿// Apoc (Penguin) helped Kickazz006 develop this plugin
// This Plugin drinks HP/Mana pots when low
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Xml.Linq;

// HB Stuff
using Styx;
using Styx.Helpers;
using Styx.Plugins;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;

namespace DrinkPotions
{
	public class DrinkPotions : HBPlugin
	{
		#region Globals

		public override string Name { get { return "DrinkPotions"; } }
		public override string Author { get { return "Kickazz006 & Apoc"; } }
		public override Version Version { get { return new Version(1, 0, 0, 1); } }
		public override string ButtonText { get { return "Kick fights for the Users!"; } }
		public override bool WantButton { get { return false; } }
		private static LocalPlayer Me { get { return StyxWoW.Me; } }

		public int HealPotPercent = 25; // Drink HP %
		public int ManaPotPercent = 15; // Drink Mana %

		#endregion

		public static WoWItem FindFirstUsableItemBySpell(params string[] spellNames)
		{
			List<WoWItem> carried = StyxWoW.Me.CarriedItems;
			// Yes, this is a bit of a hack. But the cost of creating an object each call, is negated by the speed of the Contains from a hash set.
			// So take your optimization bitching elsewhere.
			var spellNameHashes = new HashSet<string>(spellNames);

			return (from i in carried
					let spells = i.ItemSpells
					where i.ItemInfo != null && spells != null && spells.Count != 0 &&
						  i.Usable &&
						  i.Cooldown == 0 &&
						  i.ItemInfo.RequiredLevel <= StyxWoW.Me.Level &&
						  spells.Any(s => s.IsValid && s.ActualSpell != null && spellNameHashes.Contains(s.ActualSpell.Name))
					orderby i.ItemInfo.Level descending
					select i).FirstOrDefault();
		}

		public WoWItem HealingPotions()
		{
			return FindFirstUsableItemBySpell("Healing Potion", "Healthstone");
		}

		public WoWItem ManaPotions()
		{
			return FindFirstUsableItemBySpell("Restore Mana");
		}

		public override void Pulse()
		{
			if (!Me.Combat || !Me.IsAlive || Me.IsGhost || Me.IsOnTransport || Me.OnTaxi || Me.Stunned || (Me.Mounted && Me.IsFlying)) // Chillax
			{
				return;
			}

			if (Me.Combat) // Pay Attn!
			{
				if (Me.HealthPercent < HealPotPercent) // HP
				{
					WoWItem UseHealPot = HealingPotions();
					if (UseHealPot != null)
					{
						UseHealPot.UseContainerItem();
						Styx.Common.Logging.Write(System.Windows.Media.Color.FromRgb(255, 0, 255), "Used " + UseHealPot.Name + "!");
					}
				}
				if (Me.ManaPercent < ManaPotPercent) // Mana
				{
					WoWItem UseManaPot = ManaPotions();
					if (UseManaPot != null)
					{
						UseManaPot.UseContainerItem();
						Styx.Common.Logging.Write(System.Windows.Media.Color.FromRgb(255,0,255), "Used " + UseManaPot.Name + "!");
					}
				}
			}

		}
	}

}
