// Behavior originally contributed by Raphus.
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
// Uses the hearthstone, if available.
// * Can optionally wait for the cooldown to complete.
// * If you don't elect to wait for the cooldown, the profile will continue.
//   In this situation, Honorbuddy will use whatever other transport options
//   are available to it to move to the next destination.  (E.g., flying mount,
//   ground mount, or in worse case 'on foot').
//   ==> Honorbuddy cannot do inter-continental travel on its on.  If you planned
//   ==> an inter-continental hearth, then you *must* wait for the cooldown to complete.
//   ==> Or, you can write the profile to help Honorbuddy get to the other continent
//   ==> if the hearthstone is on cooldown, and you don't want to wait.
//
// Basic Attributes:
//      WaitForCD [optional;  Default: false]
//          If true and the Hearthstone is on cooldown, Honorbuddy will wait for
//          the Hearthstone cooldown to complete, then use it.
//          If false and the Hearthstone is on cooldown, the behavior terminates
//          immediately, and the profile continues.  If travel is on the same continent,
//          Honorbuddy will provide the "plan B" travel.  However, if inter-continental
//          travel is involved, the profile is expected to provide profile directives
//          for "plan B" tranportation.
//
#endregion


#region Examples
// Toon is in Eastern Plaguelands, and hearth is set in Stormwind.
// We choose to not wait for the cooldown.  If the hearthstone is on cooldown,
// the toon fill find other means to get to Stormwind.  In the best case, this
// will involve a Taxi ride.  In the worst case, this will involve walking.
//    <CustomBehavior File="UseHearthstone" />
//    <!-- continue with profile when hearthstone completes, or hearthstone on cooldown -->
//
// Toon is in Outland, and hearth is set in Stormwind.
// We are lazy and don't want to write the required "Plan B" code for inter-continental
// travel.  So we force a wait for the hearthstone cooldown, if the hearthstone is
// not ready yet.
//    <CustomBehavior File="UseHearthstone" WaitForCD="True" />
//    <!-- profile continues once the hearth has been used -->
//
// Toon is in Outland, and hearth is set in Stormwind.
// We choose not to wait for the hearth cooldown, if hearth is not available.
// As such, we must provide the "Plan B" code for inter-continental travel,
// if the hearth is not used.
//    <CustomBehavior File="UseHearthstone" />
//    <!-- "Plan B" for inter-continental travel, if hearth was on cooldown -->
//    <If Condition="Me.MapId != 723">
//        <CustomBehavior File="Message" Text="Flying or Shattrath to take portal to Stormwind" LogColor="Orange" />
//        <CustomBehavior File="FlyTo" DestName="SW portal" X="-1956.413" Y="5383.551" Z="-12.42774" />
//        <CustomBehavior File="ForcedDismount" />
//        <MoveTo X="-1893.108" Y="5393.317" Z="-12.4277" />
//        <!-- Use portal to Stormwind -->
//        <CustomBehavior File="InteractWith" MobId="183325" X="-1893.108" Y="5393.317" Z="-12.4277" />
//        <CustomBehavior File="WaitTimer" WaitTime="20000" GoalText="Waiting for zone {TimeRemaining}" />
//    </If>
//    <!-- profile continues once in Stormwind...
//         We may have gotten here by hearth, or by moving through portal
//      -->
//
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;

using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.TreeSharp;
using Styx.WoWInternals.DBC;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.UseHearthstone
{
	[CustomBehaviorFileName(@"UseHearthstone")]
	public class UseHearthstone : CustomForcedBehavior
	{
		public UseHearthstone(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.

				WaitOnCd = GetAttributeAsNullable<bool>("WaitForCD", false, null, null) ?? false;
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
		public bool WaitOnCd { get; private set; }


		// Private variables for internal state
		private bool _isBehaviorDone;
		private Composite _root;

		// Private properties
		private LocalPlayer Me { get { return (StyxWoW.Me); } }

		// DON'T EDIT THESE--they are auto-populated by Subversion

//thanks to dungonebuddy
		private uint CheckId(uint uint_13)
		{
			AreaTable table = AreaTable.FromId(uint_13);
			while (table.ParentAreaId != 0)
			{
				table = AreaTable.FromId(table.ParentAreaId);
			}
			return table.AreaId;
		}


		private bool IsInHearthStoneArea
		{
			get
			{
				uint hearthstoneAreaId = StyxWoW.Me.HearthstoneAreaId;
				uint zoneId = Me.ZoneId;
				if (hearthstoneAreaId == 0)
				{
					return false;
				}
				if (CheckId(hearthstoneAreaId) != CheckId(zoneId))
				{
					QBCLog.DeveloperInfo("Zone: {0}, hearthAreaId: {1}", zoneId, hearthstoneAreaId);
				}
				return (CheckId(hearthstoneAreaId) == CheckId(zoneId));
			}
		}


		#region Overrides of CustomForcedBehavior

		protected override Composite CreateBehavior()
		{
			return _root ?? (_root = new PrioritySelector(DoneYet,UseHearthstoneComposite, new ActionAlwaysSucceed()));
		}


        public override void OnFinished()
        {
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
        }

		public override bool IsDone
		{
			get { return (_isBehaviorDone); }
		}


		public WoWItem Hearthstone
		{
			get
			{
				return Me.BagItems.FirstOrDefault(r => r.Entry == 6948);
			}
		}

		public Composite UseHearthstoneComposite
		{
			get
			{
				return new PrioritySelector(
					new Decorator(r => Hearthstone.CooldownTimeLeft.TotalSeconds > 0,new ActionAlwaysSucceed()),
					new Decorator(r => Hearthstone.CooldownTimeLeft.TotalSeconds <= 0,new Action(r=>Hearthstone.Use()))
					);
			}
		}

		public Composite DoneYet
		{
			get
			{
				return
					new Decorator(ret => (IsInHearthStoneArea || Hearthstone == null) || (Hearthstone.CooldownTimeLeft.TotalSeconds > 0 && !WaitOnCd), new Action(delegate
					{
						TreeRoot.StatusText = "Finished!";
						_isBehaviorDone = true;
						return RunStatus.Success;
					}));

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
				TreeRoot.GoalText = "Using hearthstone";
			}
		}


		#endregion
	}
}

