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
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.PompfruitPickup
{
	[CustomBehaviorFileName(@"SpecificQuests\30231-VOEB-PompfruitPickup")]
	public class Blastranaar : QuestBehaviorBase
	{
		public Blastranaar(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				QuestId = 30231;//GetAttributeAsQuestId("QuestId", true, null) ?? 0;
			    QuestObjectiveIndex = 1;
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
		public int MobIdPomfruit = 58767;
		public int PomharvestFireworkId = 79344;
		private Composite _root;

        protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
        {
            //// EXAMPLE: 
            //UsageCheck_DeprecatedAttribute(xElement,
            //    Args.Keys.Contains("Nav"),
            //    "Nav",
            //    context => string.Format("Automatically converted Nav=\"{0}\" attribute into MovementBy=\"{1}\"."
            //                              + "  Please update profile to use MovementBy, instead.",
            //                              Args["Nav"], MovementBy));
        }

        protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
        {
            //// EXAMPLE:
            //UsageCheck_SemanticCoherency(xElement,
            //    (!MobIds.Any() && !FactionIds.Any()),
            //    context => "You must specify one or more MobIdN, one or more FactionIdN, or both.");
            //
            //const double rangeEpsilon = 3.0;
            //UsageCheck_SemanticCoherency(xElement,
            //    ((RangeMax - RangeMin) < rangeEpsilon),
            //    context => string.Format("Range({0}) must be at least {1} greater than MinRange({2}).",
            //                  RangeMax, rangeEpsilon, RangeMin)); 
        }

		public List<WoWUnit> Pomfruit
		{
			get
			{
				return ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == MobIdPomfruit && !u.IsDead && u.Distance < 10000).OrderBy(u => u.Distance).ToList();
			}
		}

		public WoWItem PomharvestFirework { get { return (StyxWoW.Me.CarriedItems.FirstOrDefault(i => i.Entry == PomharvestFireworkId)); } }

	    protected override Composite CreateBehavior_CombatMain()
	    {
            return _root ?? (_root = new ActionRunCoroutine(ctx => MainCoroutine()));
	    }

	    public async Task<bool> MainCoroutine ()
		{
		    if (IsDone)
		        return false;

		    var pomFruit = Pomfruit.FirstOrDefault();
		    if (pomFruit == null)
		        return false;
            
            if (pomFruit.Distance < 25 && PomharvestFirework.Cooldown == 0)
            {
                PomharvestFirework.UseContainerItem();
                await Coroutine.Sleep(200);
            }

            TreeRoot.StatusText = "Moving to Pomfruit";
            Flightor.MoveTo(pomFruit.Location);
            await Coroutine.Sleep(200);
            pomFruit.Interact();

            return true;
		}
	}
}
