// Behavior originally contributed by mastahg.
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
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.EverythingButTheKitchenSink
{
	[CustomBehaviorFileName(@"SpecificQuests\28589-HordeTwilightHighlands-EverythingButTheKitchenSink")]
    public class KitchenSink : QuestBehaviorBase
	{
    	public KitchenSink(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				QuestId = 28589;
				QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = QuestInLogRequirement.InLog;
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


		// Private variables for internal state
		private Composite _root;


		// Private properties

		public Composite DoDps
		{
			get
			{
				return
					new PrioritySelector(
						new Decorator(ret => RoutineManager.Current.CombatBehavior != null,
									  RoutineManager.Current.CombatBehavior),
						new Action(c => RoutineManager.Current.Combat()));
			}
		}

		#region Overrides of CustomForcedBehavior

        protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
        {
        }

        protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
        {
        }


		protected override Composite CreateBehavior_QuestbotMain()
		{
            return _root ?? (_root = new ActionRunCoroutine(ctx => MainCoroutine()));
		}

	    private async Task<bool> MainCoroutine()
	    {
	        if (IsDone)
	            return false;

	        var ct = Me.CurrentTarget;
	        if (ct == null || ct.DistanceSqr >= 100*100)
	        {
                var stormtalon = ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == 49683 && u.IsAlive).OrderBy(
                        u => u.DistanceSqr).FirstOrDefault();
                if (stormtalon != null)
                    stormtalon.Target();
	            return true;
	        }

            WoWMovement.ClickToMove(ct.Location.RayCast(ct.Rotation, 20));
            var transport = (WoWUnit)StyxWoW.Me.Transport;
            Tripper.Tools.Math.Vector3 v = ct.Location - transport.Location;
            v.Normalize();
	        var lua = string.Format(
	            "VehicleAimIncrement(({0} - VehicleAimGetAngle())); CastPetAction({1});",
	            Math.Asin(v.Z),
	            1);
            Lua.DoString(lua);
	        return true;
	    }


	    #endregion
	}
}