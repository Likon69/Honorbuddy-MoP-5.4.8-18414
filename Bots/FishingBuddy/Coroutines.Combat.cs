using System.Threading.Tasks;
using Bots.Grind;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.POI;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;

namespace Bots.FishingBuddy
{
	partial class Coroutines
	{

		private static Composite _combatBehavior;
		static Composite CombatBehavior
		{
			get { return _combatBehavior ?? (_combatBehavior = LevelBot.CreateCombatBehavior()); }
		}

		public static async Task<bool> HandleCombat()
		{
			if (!Me.IsFlying && Me.IsActuallyInCombat && Targeting.Instance.FirstUnit != null)
			{
				var mainHand = Me.Inventory.Equipped.MainHand;
				if ((mainHand == null || mainHand.Entry != FishingBuddySettings.Instance.MainHand)
					&& Utility.EquipWeapons())
				{
					return true;
				}
					
				if (await CombatBehavior.ExecuteCoroutine())
					return true;
			}

			if (BotPoi.Current.Type == PoiType.Kill)
			{
				var unit = BotPoi.Current.AsObject as WoWUnit;
				if (unit == null)
					BotPoi.Clear("Target not found");
				else if (unit.IsDead)
					BotPoi.Clear("Target is dead");
			}

			return false;
		}
	}
}
