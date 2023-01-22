using System.Threading.Tasks;
using HighVoltz.Professionbuddy.ComponentBase;
using Styx.CommonBot.Coroutines;
using Styx.WoWInternals;

namespace HighVoltz.Professionbuddy.Components
{
	[PBXmlElement("StackItems", new[] { "StackItemsAction" })]
    public class StackItemsAction : PBAction
    {
        private const string StackLua = @"
            local items={}  
            local done = 1  
            for bag = 0,4 do  
               for slot=1,GetContainerNumSlots(bag) do  
                  local id = GetContainerItemID(bag,slot)  
                  local _,c,l = GetContainerItemInfo(bag, slot)  
                  if id ~= nil then  
                     local n,_,_,_,_,_,_, maxStack = GetItemInfo(id)  
                     if c < maxStack then  
                        if items[id] == nil then  
                           items[id] = {left=maxStack-c,bag=bag,slot=slot,locked = l or 0}  
                        else  
                           if items[id].locked == 0 then  
                              PickupContainerItem(bag, slot)  
                              PickupContainerItem(items[id].bag, items[id].slot)  
                              items[id] = nil  
                           else  
                              items[id] = {left=maxStack-c,bag=bag,slot=slot,locked = l or 0}  
                           end  
                           done = 0  
                        end  
                     end  
                  end  
               end  
            end  
            return done 
    ";

		public override string Name
        {
            get { return Strings["Action_StackItemsAction_Name"]; }
        }

        public override string Title
        {
            get { return string.Format("{0}", Name); }
        }

        public override string Help
        {
            get { return Strings["Action_StackItemsAction_Help"]; }
        }

		protected async override Task Run()
        {
			IsDone = Lua.GetReturnVal<int>(StackLua, 0) == 1;
			await CommonCoroutines.SleepForLagDuration();
        }

        public override IPBComponent DeepCopy()
        {
            return new StackItemsAction();
        }
    }
}