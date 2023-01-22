using System.Linq;
using System.Threading.Tasks;
using Styx.Common.Helpers;
using Styx.CommonBot.Coroutines;
using Styx.WoWInternals;
using Styx.WoWInternals.DB;

namespace Bots.FishingBuddy
{
	static partial class Coroutines
	{
		private readonly static WaitTimer FilletTimer = WaitTimer.TenSeconds;


		// does nothing if no lures are in bag
		public async static Task<bool> FilletFish()
		{
			if (!FishingBuddySettings.Instance.FilletFish)
				return false;

			if (!FilletTimer.IsFinished || Me.IsCasting)
				return false;

			FilletTimer.Reset();
			/*
			foreach (var item in Me.BagItems.Where(i => i.ItemInfo.IsCraftingReagent))
			{
				var effects = item.Effects;
				if (effects == null || !effects.Any())
					continue;

				if (effects.Any(e => e.TriggerType == ItemEffectTriggerType.OnUse 
					&& e.Spell != null && e.Spell.SpellEffects.Any(a => a.EffectType == WoWSpellEffectType.CreateRandomItem
						&& Me.BagItems.Sum(i => i.Entry == item.Entry ? item.StackCount : 0) >= a.BasePoints)))
				{
					item.Use();
					await CommonCoroutines.SleepForLagDuration();
				}
			}
			*/
			return false;
		}

	}
}
