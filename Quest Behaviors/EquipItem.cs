// Behavior originally contributed by HighVoltz.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//

#region Summary and Documentation
// WIKI DOCUMENTATION:
//      http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_EquipItem
//     
// QUICK DOX:
//      Equips the specified item into a character equipment slot.  You may specify the slot, or allow it to default.
//      If an item is already occupying the equipment slot, it will be replaced with the specified item.
//
//  Parameters (required, then optional--both listed alphabetically):
//      ItemId: Id of the item to equip
//
//      QuestId [Default:none]:
//      QuestCompleteRequirement [Default:NotComplete]:
//      QuestInLogRequirement [Default:InLog]:
//              A full discussion of how the Quest* attributes operate is described in
//              http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
//      Slot [Default: first available]: Slot into which the item will be equipped.
//              Slot are defined on http://www.wowpedia.org/Equipment_slot.
//              The values allowed for this attribute are summarized in the following table:
//                   None ("first available")   Finger0Slot         SecondaryHandSlot
//                   AmmoSlot                   Finger1Slot         ShirtSlot
//                   BackSlot                   HandsSlot           ShoulderSlot
//                   Bag0Slot                   HeadSlot            TabardSlot
//                   Bag1Slot                   LegsSlot            Trinket0Slot
//                   Bag2Slot                   MainHandSlot        Trinket1Slot
//                   Bag3Slot                   NeckSot             WaistSlot
//                   ChestSlot                  RangedSlot          WristSlot
//
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;

using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Inventory;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.EquipItem
{
	[CustomBehaviorFileName(@"EquipItem")]
	public class EquipItem : CustomForcedBehavior
	{
		public EquipItem(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				ItemId = GetAttributeAsNullable<int>("ItemId", true, ConstrainAs.ItemId, null) ?? 0;
				QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
				QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;
				Slot = GetAttributeAsNullable<InventorySlot>("Slot", false, null, null) ?? InventorySlot.None;
			}

			catch (Exception except)
			{
				// Maintenance problems occur for a number of reasons.  The primary two are...
				// * Changes were made to the behavior, and boundary conditions weren't properly tested.
				// * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
				// In any case, we pinpoint the source of the problem area here, and hopefully it
				// can be quickly resolved.
				QBCLog.Exception(except);
				IsAttributeProblem = true;
			}
		}


		// Attributes provided by caller
		public int ItemId { get; private set; }
		public int QuestId { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }
		public InventorySlot Slot { get; private set; }

		// Private variables for internal state
		private bool _isBehaviorDone;
		private Composite _root;

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }

		#region Overrides of CustomForcedBehavior

		protected override Composite CreateBehavior()
		{
			return _root ??
				(_root = new PrioritySelector(
					new Action(c =>
					{
						if (Slot == InventorySlot.None)
						{
							Lua.DoString("EquipItemByName (\"" + ItemId + "\")");
						}
						else
						{
							WoWItem item = StyxWoW.Me.BagItems.FirstOrDefault(i => i.Entry == ItemId);
							if (item != null)
							{
								Lua.DoString("PickupContainerItem({0},{1}) EquipCursorItem({2})",
									item.BagIndex + 1, item.BagSlot + 1, (int)Slot);
							}
						}
						_isBehaviorDone = true;
					})
				));
		}

        public override void OnFinished()
        {
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
        }

		public override bool IsDone
		{
			get
			{
				return (_isBehaviorDone     // normal completion
						|| !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
			}
		}


		public override void OnStart()
		{
			// This reports problems, and stops BT processing if there was a problem with attributes...
			// We had to defer this action, as the 'profile line number' is not available during the element's
			// constructor call.
			OnStart_HandleAttributeProblem();

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (!IsDone)
			{
				WoWItem item = StyxWoW.Me.CarriedItems.FirstOrDefault(ret => ret.Entry == ItemId);

				if (item != null)
				{
                    this.UpdateGoalText(QuestId, string.Format("Equipping [{0}] Into Slot: {1}", item.SafeName, Slot));
				}
			}
		}

		#endregion
	}
}
