// Originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

#region Usings

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.CommonBot.Coroutines;
using Action = Styx.TreeSharp.Action;

#endregion

namespace Honorbuddy.QuestBehaviorCore
{
	public partial class UtilityCoroutine
	{
		/// <summary>
		///     <para>Uses item defined by ITEMID.</para>
		///     <para>
		///         Notes:
		///         <list type="bullet">
		///             <item>
		///                 <description>
		///                     <para>
		///                         * It is up to the caller to assure that all preconditions have been met for
		///                         using the item (i.e., the item is off cooldown, etc).
		///                     </para>
		///                 </description>
		///             </item>
		///             <item>
		///                 <description>
		///                     <para>
		///                         * If item use was successful, coroutine returns 'true';
		///                         otherwise, 'false' is returned (e.g., item is not ready for use,
		///                         item use was interrupted by combat, etc).
		///                     </para>
		///                 </description>
		///             </item>
		///         </list>
		///     </para>
		/// </summary>
		/// <param name="itemId">The item provided should be viable, and ready for use.</param>
		/// <param name="actionOnMissingItemDelegate">This delegate will be called if the item
		/// is missing from our backpack.  This delegate may not be null.</param>
		/// <param name="actionOnFailedItemUseDelegate">If non-null, this delegate will be called
		/// if we attempted to use the item, and it was unsuccessful.  Examples include attemtping
		/// to use the item on an invalid target, or being interrupted or generally unable to use
		/// the item at this time.</param>
		/// <param name="actionOnSuccessfulItemUseDelegate">If non-null, this delegate will be called
		/// once the item has been used successfully.</param>
		/// <returns></returns>
		/// <remarks>20140305-19:01UTC, Highvoltz/chinajade</remarks>
		public static async Task<bool> UseItem(
			int itemId,
			System.Action actionOnMissingItemDelegate,
			System.Action actionOnFailedItemUseDelegate = null,
			System.Action actionOnSuccessfulItemUseDelegate = null)
		{
			// Waits for global cooldown to end to successfully use the item
			await Coroutine.Wait(500, () => !SpellManager.GlobalCooldown);

			// Is item in our bags?
			var itemToUse = Me.CarriedItems.FirstOrDefault(i => (i.Entry == itemId));
			if (!Query.IsViable(itemToUse))
			{
				QBCLog.Error("{0} is not in our bags.", Utility.GetItemNameFromId(itemId));
				if (actionOnMissingItemDelegate != null)
					{ actionOnMissingItemDelegate();  }
				return false;
			}
			var itemName = itemToUse.SafeName;

			// Wait for Item to be usable...
			// NB: WoWItem.Usable does not account for cooldowns.
			if (!itemToUse.Usable || (itemToUse.Cooldown > 0))
			{
				TreeRoot.StatusText =
					string.Format(
						"{0} is not usable, yet. (cooldown remaining: {1})",
						itemName,
						Utility.PrettyTime(itemToUse.CooldownTimeLeft));
				return false;
			}

			// Notify user of intent...
			QBCLog.DeveloperInfo("Attempting use of '{0}'", itemName);

			// Set up 'interrupted use' detection, and use item...
			// MAINTAINER'S NOTE: Once these handlers are installed, make sure all possible exit paths from the outer
			// Sequence unhook these handlers.  I.e., if you plan on returning RunStatus.Failure, be sure to call
			// UtilityBehaviorSeq_UseItemOn_HandlersUnhook() first.
			InterruptDetection_Hook();
			IsInterrupted = false;
			itemToUse.Use();

			// NB: The target or the item may not be valid after this point...
			// Some targets will go 'invalid' immediately afer interacting with them.
			// Most of the time this happens, the target is immediately and invisibly replaced with
			// an identical looking target with a different script.
			// Some items are consumed when used.
			// We must assume our target and item is no longer available for use after this point.
			await Coroutine.Sleep((int)Delay.AfterItemUse.TotalMilliseconds);

			// Wait for any casting to complete...
			// NB: Some interactions or item usages take time, and the WoWclient models this as spellcasting.
			await Coroutine.Wait(15000, () => !(Me.IsCasting || Me.IsChanneling));
			InterruptDectection_Unhook();

			if (IsInterrupted)
			{
				QBCLog.Warning("Use of {0} interrupted.", itemName);
				// Give whatever issue encountered a chance to settle...
				// NB: --we want the Sequence to fail when delay completes.
				await Coroutine.Sleep(1500);
				if (actionOnFailedItemUseDelegate != null)
					{ actionOnFailedItemUseDelegate(); }
				return false;
			}
			QBCLog.DeveloperInfo("Use of '{0}' succeeded.", itemName);

			if (actionOnSuccessfulItemUseDelegate != null)
				{ actionOnSuccessfulItemUseDelegate(); }
			return true;
		}


		/// <summary>
		///     <para>Uses item defined by ITEMID on target defined by SELECTEDTARGET.</para>
		///     <para>
		///         Notes:
		///         <list type="bullet">
		///             <item>
		///                 <description>
		///                     <para>
		///                         * It is up to the caller to assure that all preconditions have been met for
		///                         using the item (i.e., the target is in range, the item is off cooldown, etc).
		///                     </para>
		///                 </description>
		///             </item>
		///             <item>
		///                 <description>
		///                     <para>
		///                         * If item use was successful, coroutine returns 'true';
		///                         otherwise, 'false' is returned (e.g., item is not ready for use,
		///                         item use was interrupted by combat, etc).
		///                     </para>
		///                 </description>
		///             </item>
		///             <item>
		///                 <description>
		///                     <para>
		///                         * It is up to the caller to blacklist the target, or select a new target
		///                         after successful item use.  The actionOnFailedItemUseDelegate argument
		///                         can facilitate these activities.
		///                     </para>
		///                 </description>
		///             </item>
		///         </list>
		///     </para>
		/// </summary>
		/// <param name="selectedTarget">The target provided should be viable.</param>
		/// <param name="itemId">The item provided should be viable, and ready for use.</param>
		/// <param name="actionOnMissingItemDelegate">This delegate will be called if the item
		/// is missing from our backpack.  This delegate may not be null.</param>
		/// <param name="actionOnFailedItemUseDelegate">If non-null, this delegate will be called
		/// if we attempted to use the item, and it was unsuccessful.  Examples include attemtping
		/// to use the item on an invalid target, or being interrupted or generally unable to use
		/// the item at this time.</param>
		/// <param name="actionOnSuccessfulItemUseDelegate">If non-null, this delegate will be called
		/// once the item has been used successfully.</param>
		/// <returns></returns>
		/// <remarks>20140305-19:01UTC, Highvoltz/chinajade</remarks>
		public static async Task<bool> UseItemOnTarget(
			int itemId,
			WoWObject selectedTarget,
			System.Action actionOnMissingItemDelegate,
			System.Action actionOnFailedItemUseDelegate = null,
			System.Action actionOnSuccessfulItemUseDelegate = null)
		{
			// Waits for global cooldown to end to successfully use the item
			await Coroutine.Wait(500, () => !SpellManager.GlobalCooldown);

			// qualify...
			// Viable target?
			// NB: Since target may go invalid immediately upon using the item,
			// we cache its name for use in subsequent log entries.;
			if (!Query.IsViable(selectedTarget))
			{
				QBCLog.Warning("Target is not viable!");
				if (actionOnFailedItemUseDelegate != null)
					{ actionOnFailedItemUseDelegate(); }
				return false;
			}
			var targetName = selectedTarget.SafeName;

			// Is item in our bags?
			var itemToUse = Me.CarriedItems.FirstOrDefault(i => (i.Entry == itemId));
			if (!Query.IsViable(itemToUse))
			{
				QBCLog.Error("{0} is not in our bags.", Utility.GetItemNameFromId(itemId));
				if (actionOnMissingItemDelegate != null)
					{ actionOnMissingItemDelegate(); }
				return false;
			}
			var itemName = itemToUse.SafeName;

			// Need to be facing target...
			// NB: Not all items require this, but many do.
			Utility.Target(selectedTarget, true);

			// Wait for Item to be usable...
			// NB: WoWItem.Usable does not account for cooldowns.
			if (!itemToUse.Usable || (itemToUse.Cooldown > 0))
			{
				TreeRoot.StatusText =
					string.Format(
						"{0} is not usable, yet. (cooldown remaining: {1})",
						itemName,
						Utility.PrettyTime(itemToUse.CooldownTimeLeft));
				return false;
			}

			// Notify user of intent...
			var message = string.Format("Attempting use of '{0}' on '{1}'", itemName, targetName);

			var selectedTargetAsWoWUnit = selectedTarget as WoWUnit;
			if (selectedTargetAsWoWUnit != null)
			{
				if (selectedTargetAsWoWUnit.IsDead)
				{
					message += " (dead)";
				}
				else
				{
					message += string.Format(" (health: {0:F1})", selectedTargetAsWoWUnit.HealthPercent);
				}
			}
			QBCLog.DeveloperInfo(message);

			// Set up 'interrupted use' detection, and use item...
			// MAINTAINER'S NOTE: Once these handlers are installed, make sure all possible exit paths from the outer
			// Sequence unhook these handlers.  I.e., if you plan on returning RunStatus.Failure, be sure to call
			// UtilityBehaviorSeq_UseItemOn_HandlersUnhook() first.
			InterruptDetection_Hook();
			IsInterrupted = false;
			itemToUse.Use(selectedTarget.Guid);

			// NB: The target or the item may not be valid after this point...
			// Some targets will go 'invalid' immediately afer interacting with them.
			// Most of the time this happens, the target is immediately and invisibly replaced with
			// an identical looking target with a different script.
			// Some items are consumed when used.
			// We must assume our target and item is no longer available for use after this point.
			await Coroutine.Sleep((int) Delay.AfterItemUse.TotalMilliseconds);

			await CastPendingSpell(selectedTarget);

			// Wait for any casting to complete...
			// NB: Some interactions or item usages take time, and the WoWclient models this as spellcasting.
			await Coroutine.Wait(15000, () => !(Me.IsCasting || Me.IsChanneling));
			InterruptDectection_Unhook();

			if (IsInterrupted)
			{
				QBCLog.Warning("Use of {0} interrupted.", itemName);
				// Give whatever issue encountered a chance to settle...
				// NB: --we want the Sequence to fail when delay completes.
				await Coroutine.Sleep(1500);
				if (actionOnFailedItemUseDelegate != null)
					{ actionOnFailedItemUseDelegate(); }
				return false;
			}

			QBCLog.DeveloperInfo("Use of '{0}' on '{1}' succeeded.", itemName, targetName);
			if (actionOnSuccessfulItemUseDelegate != null)
				{ actionOnSuccessfulItemUseDelegate(); }
			return true;
		}

	}
}