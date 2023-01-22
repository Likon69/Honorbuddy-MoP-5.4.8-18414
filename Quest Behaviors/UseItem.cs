// Behavior originally contributed by Nesox.
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
//
// QUICK DOX:
// USEITEM allows the toon to use an item.  The item may be used with 'no target'.
// The behavior is also capable of using the item on gameobjects or mobs. However,
// InteractWith is a much better choice when a target is involved.
//
// Note: "Core" attributes provided by QuestBehaviorBase are also available to the caller.
//
// Basic Attributes:
//      ItemId [REQUIRED; Default: none]
//          Specifies an ItemId to use.
//          The item may be a normal 'one-click-to-use' item.
//
// Tunables:
//      NumOfTimes [optional; Default: 1]
//          The behavior considers itself complete when the item has been used this number
//          of times.
//          If the behavior is also associated with a quest or quest objective, then the behavior
//          will also terminate when the quest or objective completes.  This may happen before
//          the NumOfTimes has been consumed.
//      TargetNearest [optional; Default: false]
//          If 'true', the nearest mob will be targeted, before the item use is attempted.
//          The behavior has no provisions for selecting a particular type of mob.
//      WaitTime [optional; Default 1500ms]
//          Defines the number of milliseconds to wait after the item is used.
//      X/Y/Z [optional; Default: toon's current location when behavior is started]
//          This specifies the location where the toon should go to use the item.
//
// Things to know:
// * This behavior should not be employed for 'using an item on a mob'.
//      Instead, use InteractWith, for that.
//
// * This behavior will not handle 'two-click-to-use' items.
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

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


namespace Honorbuddy.Quest_Behaviors.UseItem
{
	[CustomBehaviorFileName(@"UseItem")]
	public class UseItem : QuestBehaviorBase
	{
		public UseItem(Dictionary<string, string> args)
			: base(args)
		{
			try
			{
				// NB: Core attributes are parsed by QuestBehaviorBase parent (e.g., QuestId, NonCompeteDistance, etc)

				ItemId = GetAttributeAsNullable<int>("ItemId", true, ConstrainAs.ItemId, null) ?? 0;
				Location = GetAttributeAsNullable<WoWPoint>("", false, ConstrainAs.WoWPointNonEmpty, null) ?? Me.Location;
				NumOfTimes = GetAttributeAsNullable<int>("NumOfTimes", false, ConstrainAs.RepeatCount, null) ?? 1;
				WaitTime = GetAttributeAsNullable<int>("WaitTime", false, ConstrainAs.Milliseconds, null) ?? 1500;
				TargetNearest = GetAttributeAsNullable<bool>("TargetNearest", false, null, new[] { "TargetClosest" }) ?? false;
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

		protected override void EvaluateUsage_DeprecatedAttributes(XElement xElement)
		{
			// empty
		}

		protected override void EvaluateUsage_SemanticCoherency(XElement xElement)
		{
			// empty
		}

		// Attributes provided by caller
		public WoWPoint Location { get; private set; }
		public int ItemId { get; private set; }
		public int NumOfTimes { get; private set; }
		public int WaitTime { get; private set; }
		public bool TargetNearest { get; private set; }

		// Private variables for internal state

		// Private properties
		private int Counter { get; set; }
		private WoWItem Item { get { return (StyxWoW.Me.CarriedItems.FirstOrDefault(i => i.Entry == ItemId)); } }

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }


		#region Overrides of CustomForcedBehavior
		// CreateBehavior supplied by QuestBehaviorBase.
		// Instead, provide CreateMainBehavior definition.

		// Dispose provided by QuestBehaviorBase.

		// IsDone provided by QuestBehaviorBase.
		// Call the QuestBehaviorBase.BehaviorDone() method when you want to indicate your behavior is complete.

		// OnFinished provided by QuestBehaviorBase.

		protected override Composite CreateMainBehavior()
		{
			return new PrioritySelector(

				new Decorator(ret => Counter >= NumOfTimes,
					new Action(ret => BehaviorDone(string.Format("Object used {0} times.", Counter)))),

				new Decorator(
					ret => Location.Distance(StyxWoW.Me.Location) > 2,
					new ActionRunCoroutine(
					    interactUnitContext => UtilityCoroutine.MoveTo(
					        Location,
					        "destination",
					        MovementBy))),

				new Decorator(ret => StyxWoW.Me.IsMoving,
					new Action(ret => { Navigator.PlayerMover.MoveStop(); })),

				new Decorator(ret => TargetNearest,
					new Sequence(
						new Action(context => Lua.DoString("TargetNearest()")),
						new SleepForLagDuration(),
						new ActionAlwaysFail())),   // fall through

				new Decorator(ret => (Item != null) && (Item.Cooldown <= 0),
					new Sequence(
						new Action(ret =>
						{
                            TreeRoot.StatusText = string.Format("Using {0} (count: {1}/{2})", Item.SafeName, Counter, NumOfTimes);
							Item.UseContainerItem();
							Counter++;
						}),
						new SleepForLagDuration(),
						new Sleep(WaitTime)))
			);
		}


		public override void OnStart()
		{
			// Let QuestBehaviorBase do basic initializaion of the behavior, deal with bad or deprecated attributes,
			// capture configuration state, install BT hooks, etc.  This will also update the goal text.
			var isBehaviorShouldRun =
				OnStart_QuestBehaviorCore(string.Format("Using {0} {1} times.",
				Utility.GetItemNameFromId(ItemId),
				NumOfTimes));

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (isBehaviorShouldRun)
			{
				// empty, for now...
			}
		}

		#endregion
	}
}
