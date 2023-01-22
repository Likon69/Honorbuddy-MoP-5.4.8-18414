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
// Allows you to use Transports.
// ##Syntax##
// TransportId: ID of the transport.
// TransportStart: Start point of the transport that we will get on when its close enough to that point.
// TransportEnd: End point of the transport that we will get off when its close enough to that point.
// WaitAt: Where you wish to wait the transport at
// GetOff: Where you wish to end up at when transport reaches TransportEnd point
// StandOn: The point you wish the stand while you are in the transport
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
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.UseTransport
{
	[CustomBehaviorFileName(@"UseTransport")]
	public class UseTransport : CustomForcedBehavior
	{
		public UseTransport(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.

				StartLocation = GetAttributeAsNullable<WoWPoint>("TransportStart", false, ConstrainAs.WoWPointNonEmpty, null) ?? Me.Location;
				EndLocation = GetAttributeAsNullable<WoWPoint>("TransportEnd", false, ConstrainAs.WoWPointNonEmpty,null) ?? Me.Location;
				GetOffLocation = GetAttributeAsNullable<WoWPoint>("GetOff", false, ConstrainAs.WoWPointNonEmpty, null) ?? Me.Location;
				StandLocation = GetAttributeAsNullable<WoWPoint>("StandOn", false, ConstrainAs.WoWPointNonEmpty, null) ?? Me.Location;
				WaitAtLocation = GetAttributeAsNullable<WoWPoint>("WaitAt", false, ConstrainAs.WoWPointNonEmpty, null) ?? Me.Location;

				DestName = GetAttributeAs<string>("DestName", false, ConstrainAs.StringNonEmpty, null) ?? "";

				TransportId = GetAttributeAsNullable<int>("TransportId", true, ConstrainAs.MobId, new[] { "Transport" }) ?? 0;
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
		public string DestName { get; private set; }
		public WoWPoint EndLocation { get; private set; }
		public WoWPoint GetOffLocation { get; private set; }
		public int QuestId { get; private set; }
		public WoWPoint StandLocation { get; private set; }
		public WoWPoint StartLocation { get; private set; }
		public int TransportId { get; private set; }
		public WoWPoint WaitAtLocation { get; private set; }

		// Private variables for internal state
		private ConfigMemento _configMemento;
		private bool _isBehaviorDone;
		private Composite _root;
		// Private properties
		private LocalPlayer Me { get { return (StyxWoW.Me); } }

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }


		private WoWPoint TransportLocation
		{
			get
			{
				var transport = ObjectManager.GetObjectsOfType<WoWGameObject>(true, false).FirstOrDefault(o => o.Entry == TransportId);

				if (transport == null)
					return WoWPoint.Empty;

				//Tripper.Tools.Math.Matrix m = transport.GetWorldMatrix();

				//return new WoWPoint(m.M41, m.M42, m.M43);

				return transport.WorldLocation;
			}
		}


		#region Overrides of CustomForcedBehavior

		private bool _usedTransport;
		protected override Composite CreateBehavior()
		{
			return _root ?? (_root =
				new PrioritySelector(
					new Decorator(
						ret => GetOffLocation != WoWPoint.Empty && Me.Location.DistanceSqr(GetOffLocation) < 2*2,
						new Sequence(
							new Action(ret => _usedTransport = false),
							new Action(ret => QBCLog.Info("Successfully used the transport.")),
							new Action(ret => _isBehaviorDone = true))),
					new Decorator(
						ret => Me.IsOnTransport || _usedTransport,
						new PrioritySelector(
							 new Decorator(
								ret => TransportLocation != WoWPoint.Empty && TransportLocation.DistanceSqr(EndLocation) < 1.5*1.5,
								new Sequence(
									new Action(ret => QBCLog.Info("Moving out of transport")),
									new Action(ret => Navigator.PlayerMover.MoveTowards(GetOffLocation)))),
							new Action(ret =>
								{
									_usedTransport = true;
									QBCLog.Info("Waiting for the end location");
								})
						)),
					new Decorator(
						ret => !Me.IsMoving,
						new PrioritySelector(
							new Decorator(
								ret => TransportLocation != WoWPoint.Empty && TransportLocation.DistanceSqr(StartLocation) < 1.5*1.5 && WaitAtLocation.DistanceSqr(Me.Location) < 2 * 2,
								new Sequence(
									new Action(ret => QBCLog.Info("Moving inside transport")),
									new Action(ret => Navigator.PlayerMover.MoveTowards(StandLocation)))),
							new Decorator(
								ret => WaitAtLocation.DistanceSqr(Me.Location) > 2 * 2,
								new Sequence(
									new Action(ret => QBCLog.Info("Moving to wait location")),
									new Action(ret => Navigator.MoveTo(WaitAtLocation)))),
							new Sequence(
								new Mount.ActionLandAndDismount(),
								new Action(ret => QBCLog.Info("Waiting for transport")))
							))
					));
		}

        public override void OnFinished()
        {
            if (_configMemento != null)
            {
                _configMemento.Dispose();
                _configMemento = null;
            }
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            base.OnFinished();
        }


		public override bool IsDone
		{
			get { return (_isBehaviorDone); }
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
				_configMemento = new ConfigMemento();

				// Disable any settings that may cause distractions --
				// When we use transport, we don't want to be distracted by other things.
				// We also set PullDistance to its minimum value.
				// NOTE: these settings are restored to their normal values when the behavior completes
				// or the bot is stopped.
				CharacterSettings.Instance.HarvestHerbs = false;
				CharacterSettings.Instance.HarvestMinerals = false;
				CharacterSettings.Instance.LootChests = false;
				ProfileManager.CurrentProfile.LootMobs = false;
				CharacterSettings.Instance.NinjaSkin = false;
				CharacterSettings.Instance.SkinMobs = false;
				CharacterSettings.Instance.PullDistance = 1;
				CharacterSettings.Instance.UseFlightPaths = false;

				this.UpdateGoalText(QuestId);
			}
		}
		#endregion
	}
}

