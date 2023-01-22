using System.Linq;
using Styx.CommonBot.Frames;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace HighVoltz.AutoAngler.Composites
{
    public class LootNPCsAction : Action
    {
        protected override RunStatus Run(object context)
        {
            WoWUnit lootableNpc = ObjectManager.GetObjectsOfType<WoWUnit>(true)
                .OrderBy(unit => unit.Distance)
                .FirstOrDefault(unit => unit.Lootable);
            if (lootableNpc != null)
            {
                LootNPC(lootableNpc);
                return RunStatus.Success;
            }
            return RunStatus.Failure;
        }

        private void LootNPC(WoWUnit lootableUnit)
        {

            if (lootableUnit.WithinInteractRange)
            {
                if (LootFrame.Instance != null && LootFrame.Instance.IsVisible)
                {
                    // record all loot info..
                    for (int i = 0; i < LootFrame.Instance.LootItems; i++)
                    {
                        LootSlotInfo lootInfo = LootFrame.Instance.LootInfo(i);
						if (AutoAnglerBot.FishCaught.ContainsKey(lootInfo.LootName))
							AutoAnglerBot.FishCaught[lootInfo.LootName] += (uint)lootInfo.LootQuantity;
                        else
							AutoAnglerBot.FishCaught.Add(lootInfo.LootName, (uint)lootInfo.LootQuantity);
                    }
                    LootFrame.Instance.LootAll();
                }
                else
                    lootableUnit.Interact();
            }
            else
                Navigator.MoveTo(lootableUnit.Location);
        }
    }
}