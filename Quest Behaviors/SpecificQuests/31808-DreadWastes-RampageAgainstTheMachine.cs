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
using Honorbuddy.Quest_Behaviors.WaitTimerBehavior;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Bars;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.RampageAgainstTheMachine
{
	[CustomBehaviorFileName(@"SpecificQuests\31808-DreadWastes-RampageAgainstTheMachine")]
	public class RampageAgainstTheMachine : QuestBehaviorBase
	{
		public RampageAgainstTheMachine(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				QuestId = 31808;//GetAttributeAsQuestId("QuestId", true, null) ?? 0;
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

        private const uint IkthikWarriorId = 67034;
        private const uint IkthikSlayerId = 67035;
	    private const uint DreadBehemothId = 67039;
        private const uint IkthikKunchongId = 67036;
        Styx.Common.Helpers.WaitTimer _targetTimer = new Styx.Common.Helpers.WaitTimer(TimeSpan.FromSeconds(45));

        private readonly uint[] _manditUnitIds = { IkthikWarriorId, IkthikSlayerId, DreadBehemothId, IkthikKunchongId };

	    private const uint KovokId = 63765;


        private readonly WoWPoint _kovokLoc = new WoWPoint(-59.68924, 3421.45, 103.9458);

	    private CircularQueue<WoWPoint> _path = new CircularQueue<WoWPoint>()
	                                            {
	                                                new WoWPoint(-193.5827, 3167.077, 118.4438),
	                                                new WoWPoint(-90.43753, 3857.049, 163.7572)
	                                            };

		private Composite _root;


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

	        if (!Query.IsInVehicle())
	            return await UtilityCoroutine.MountVehicle((int)KovokId, _kovokLoc);

	        var ct = Me.CurrentTarget;
	        var transport = (WoWUnit)Me.Transport;
	        
            if (transport == null)
	            return false;

	        if (ct == null)
	        {
	            var newTarget = GetNearestAttacker() ?? GetNearestTarget();
	            if (newTarget != null)
	            {
	                newTarget.Target();
                    _targetTimer.Reset();
	                return true;
	            }
                // move to waypoints searching for targets.
	            if (transport.Location.DistanceSqr(_path.Peek()) < 15*15)
	                _path.Dequeue();
	            return (await CommonCoroutines.MoveTo(_path.Peek())).IsSuccessful();
	        }

	        if (ct.IsDead)
	        {
	            Me.ClearTarget();
	            return true;
	        }

            // blacklist target if it's taking too long to kill.
	        if (_targetTimer.IsFinished)
	        {
                Blacklist.Add(ct, BlacklistFlags.Combat, TimeSpan.FromMinutes(3));
	            Me.ClearTarget();    
	        }


            if (transport.Location.DistanceSqr(ct.Location) > 35*35)
                return (await CommonCoroutines.MoveTo(ct.Location)).IsSuccessful();

	        if (!transport.IsSafelyFacing(ct, 40))
	        {
	            ct.Face();
	            return true;
	        }

	        if (transport.IsMoving)
	        {
	            //WoWMovement.MoveStop();
                // WoWMovement.MoveStop doesn't seem to work...
                WoWMovement.ClickToMove(transport.Location);
	            return true;
	        }

	        var actionButton = ActionBar.Active.Buttons.FirstOrDefault(b => b.Index !=2 && b.CanUse);
	        if (actionButton != null)
	        {
	            actionButton.Use();
	            await Coroutine.Sleep(Delay.AfterWeaponFire);
	            return true;
	        }

	        return false;
	    }

	    WoWUnit GetNearestAttacker()
	    {
	        var transportGuid = Me.TransportGuid;
            // get all units attacking the toon's vehicle except for warriors since they're a joke
	        return ObjectManager.GetObjectsOfType<WoWUnit>(false, false)
                .Where(u => u.Entry != IkthikWarriorId && u.CurrentTargetGuid == transportGuid && !Blacklist.Contains(u, BlacklistFlags.Combat))
	            .OrderBy(u => u.DistanceSqr)
	            .FirstOrDefault();
	    }

        WoWUnit GetNearestTarget()
        {
            return ObjectManager.GetObjectsOfType<WoWUnit>(false, false)
                .Where(u => _manditUnitIds.Contains(u.Entry) && u.IsAlive && !Blacklist.Contains(u, BlacklistFlags.Combat))
                .OrderBy(u => u.DistanceSqr)
                .FirstOrDefault();
        }

	}
}



