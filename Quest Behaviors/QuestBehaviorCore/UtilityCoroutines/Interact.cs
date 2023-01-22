// Originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

using System.Threading.Tasks;

#region Usings

using System.Collections;
using Buddy.Coroutines;
using Styx.CommonBot;
using System;
using System.Collections.Generic;
using CommonBehaviors.Actions;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.CommonBot.Coroutines;
using Action = Styx.TreeSharp.Action;
#endregion

// TODO: Need 'bind at inn' variant
// TODO: Need 'buy from merchant' variant
// TODO: Need 'gossip' variant

namespace Honorbuddy.QuestBehaviorCore
{
	public partial class UtilityCoroutine
	{
		public static async Task<bool> Interact(WoWObject selectedTarget, System.Action actionOnSuccessfulItemUseDelegate = null)
		{
			// Viable target?
			// NB: Since target may go invalid immediately upon interacting with it,
			// we cache its name for use in subsequent log entries.
			if (!Query.IsViable(selectedTarget))
			{
				QBCLog.Warning("Target is not viable!");
				return false;
			}

			var targetName = selectedTarget.SafeName;
			// Need to be facing target...
			// NB: Not all items require this, but many do.
			Utility.Target(selectedTarget, true);

			// Notify user of intent...
			QBCLog.DeveloperInfo("Interacting with '{0}'", targetName);

			// Set up 'interrupted use' detection, and interact...
			// MAINTAINER'S NOTE: Once these handlers are installed, make sure all possible exit paths from the outer
			// Sequence unhook these handlers.  I.e., if you plan on returning RunStatus.Failure, be sure to call
			// UtilityBehaviorSeq_UseItemOn_HandlersUnhook() first.
			InterruptDetection_Hook();
			IsInterrupted = false;
			selectedTarget.Interact();

			// NB: The target may not be valid after this point...
			// Some targets will go 'invalid' immediately afer interacting with them.
			// Most of the time this happens, the target is immediately and invisibly replaced with
			// an identical looking target with a different script.
			// We must assume our target is no longer available for use after this point.
			await Coroutine.Sleep((int) Delay.AfterInteraction.TotalMilliseconds);

			// Wait for any casting to complete...
			// NB: Some interactions or item usages take time, and the WoWclient models this as spellcasting.
			// NB: We can't test for IsCasting or IsChanneling--we must instead look for a valid spell being cast.
			//      There are some quests that require actions where the WoWclient returns 'true' for IsCasting,
			//      but there is no valid spell being cast.  We want the behavior to move on immediately in these
			//      conditions.  An example of such an interaction is removing 'tangler' vines in the Tillers
			//      daily quest area.
			await Coroutine.Wait(15000, () => (Me.CastingSpell == null) && (Me.ChanneledSpell == null));

			// Were we interrupted in item use?
			InterruptDectection_Unhook();
			if (IsInterrupted)
			{
				QBCLog.DeveloperInfo("Interaction with {0} interrupted.", targetName);
				// Give whatever issue encountered a chance to settle...
				// NB: we want the Sequence to fail when delay completes.
				await Coroutine.Sleep(1500);
				return false;
			}

			QBCLog.DeveloperInfo("Interact with '{0}' succeeded.", targetName);
			if (actionOnSuccessfulItemUseDelegate != null)
				actionOnSuccessfulItemUseDelegate();

			return true;
		}

	}
}