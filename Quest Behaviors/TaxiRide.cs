// Behavior originally contributed by Cava
// Part of this code obtained from HB QB's and UseTaxi.cs originaly contributed by Vlad
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
// QUICK DOX:
// TaxiRide interact with Flighter masters to pick a fly, or get a destination list names.
//
// BEHAVIOR ATTRIBUTES:
//
// QuestId: (Optional) - associates a quest with this behavior. 
// QuestCompleteRequirement [Default:NotComplete]:
// QuestInLogRequirement [Default:InLog]:
//	If the quest is complete or not in the quest log, this Behavior will not be executed.
//	A full discussion of how the Quest* attributes operate is described in
//      http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
//
// MobId: (Required) - Id of the Flight Master to use
// NpcState [optional; Default: DontCare]
//	[Allowed values: Alive, DontCare]
//          This represents the state the NPC must be in when searching for targets
//          with which we can interact.
//
// TaxiNumber: (Optional)- Specifies the Number of the Flight Path on the TaxiMap
// DestName: (Optional) [Default: ViewNodesOnly] - Specifies the destination NAME of the node on the TaxiMap. 
//	If bouth TaxiNumber and DestName are omitted bot will use default ViewNodesOnly, and only give an outputlist of nodes (number name)
//	The TaxiNumber its a number and have prio over the Destname (if bouth are give, bot will only use the TaxiNumber
//	The DestName should be a name string in the list of your TaxiMap node names. The argument is CASE SENSITIVE!
//
// WaitTime [optional; Default: 1500ms]
//	Defines the number of milliseconds to wait after the interaction is successfully
//	conducted before carrying on with the behavior on other mobs.
//
//
// THINGS TO KNOW:
// The idea of this Behavior is use the FPs, its not intended to move to near Flight master,
// its always a good idea move bot near the MobId before start this behavior
// If char doesn't know the Destination flight node name, the will not fly,
// its always good idea add an RunTo (Destiny XYZ) after use this behavior
// Likethis (RunTo Near MobId) -> (use Behavior) -> (RunTo Destiny XYZ)
//
// You must 'escape' and ampersand (&) inside DestName.  For instance, "Fizzle &amp; Pozzik".
// You cannot use the single-quote (') inside DestName, when nodes have that like
// "Fizzle & Pozzik's Speedbarge, Thousand Needles", you can use the part before 
// or after the single-quote.  For instance:
//     DestName="Fizzle &amp; Pozzik", or
//     DestName="s Speedbarge, Thousand Needles"
//
// Sadly, the TaxiNumber changes for a given flightmaster, depending on what destinations
// the toon already knows.  Also, the DestName is locale-specific.  As a profile writer
// you must bear these severe limiations in mind at all times.
//
// EXAMPLES:
// <CustomBehavior File="TaxiRide" MobId="12345" NpcState="Alive" TaxiNumber="2" />
// <CustomBehavior File="TaxiRide" MobId="12345" DestName="Orgrimmar" WaitTime="1000" />
// <CustomBehavior File="TaxiRide" MobId="12345" DestName="ViewNodesOnly" />
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Levelbot.Actions.General;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


// ReSharper disable once CheckNamespace
namespace Styx.Bot.Quest_Behaviors.TaxiRide
{
	[CustomBehaviorFileName(@"TaxiRide")]
	public class TaxiRide : CustomForcedBehavior
	{
		#region Constructor and argument processing  

		private enum NpcStateType
		{
			Alive,
			DontCare,
		}

		public TaxiRide(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				QuestId = GetAttributeAsNullable("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
				QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;

				DestName = GetAttributeAs("DestName", false, ConstrainAs.StringNonEmpty, null) ?? "ViewNodesOnly";
				MobId = GetAttributeAsNullable("MobId", true, ConstrainAs.MobId, null) ?? 0;
				NpcState = GetAttributeAsNullable<NpcStateType>("MobState", false, null, new[] { "NpcState" }) ?? NpcStateType.Alive;
				TaxiNumber = GetAttributeAs("TaxiNumber", false, ConstrainAs.StringNonEmpty, null) ?? "0";
				WaitForNpcs = GetAttributeAsNullable<bool>("WaitForNpcs", false, null, null) ?? false;
				WaitTime = GetAttributeAsNullable("WaitTime", false, ConstrainAs.Milliseconds, null) ?? 1500;
				NpcLocation = GetAttributeAsNullable("", false, ConstrainAs.WoWPointNonEmpty, null) ?? Me.Location;

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
		private string DestName { get; set; }
		private int MobId { get; set; }
		private WoWPoint NpcLocation { get; set; }
		private NpcStateType NpcState { get; set; }
		private int QuestId { get; set; }
		private QuestCompleteRequirement QuestRequirementComplete { get; set; }
		private QuestInLogRequirement QuestRequirementInLog { get; set; }
		private string TaxiNumber { get; set; }
		private bool WaitForNpcs { get; set; }
		private int WaitTime { get; set; }

		#endregion


		#region Private and Convenience variables

		private bool _isBehaviorDone;
		private bool _isOnFinishedRun;
		private Composite _root;
		private static LocalPlayer Me { get { return (StyxWoW.Me); } }
		private int _tryNumber;
		private Stopwatch _doingQuestTimer;

		#endregion


		#region Overrides of CustomForcedBehavior

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }

		protected override Composite CreateBehavior()
		{
			return _root ?? (_root =
				new PrioritySelector(
					new Decorator(ret => Me.OnTaxi || _tryNumber >= 5 || (_doingQuestTimer.ElapsedMilliseconds >= 180000 && !WaitForNpcs),
						new Action(ret => _isBehaviorDone = true)),

					new Decorator(ret => CurrentNpc == null,
						new PrioritySelector(
							new Decorator(ret => NpcLocation.DistanceSqr(Me.Location) > 10 * 10,
								new Sequence(
									new Action(ret => QBCLog.Info("Cant find flightmaster, Moving to place provided by profile")),
									new Action(ret => Flightor.MoveTo(NpcLocation)))),
							new Action(ret => QBCLog.Info("Waiting for flightmaster to spawn"))
						)
					),

					new Mount.ActionLandAndDismount("Interact flight master"),

					new Decorator(ret => !CurrentNpc.WithinInteractRange,
						new Action(ret => Navigator.MoveTo(CurrentNpc.Location))
					),

					// Getting ready to interact
					new Decorator(ctx => !TaxiFrame.Instance.IsVisible,
						new Sequence(
							new DecoratorContinue(ret => WoWMovement.ActiveMover.IsMoving,
								new Sequence(
									new Action(ret => WoWMovement.MoveStop()),
									new SleepForLagDuration())),
							new DecoratorContinue(ret => Me.IsShapeshifted(),
								new Sequence(
									new Action(ret => Lua.DoString("CancelShapeshiftForm()")),
									new SleepForLagDuration())),
							new Action(ret => CurrentNpc.Interact()),
							new Sleep(1000),
							new SleepForLagDuration()
							)),
								
					new Decorator(ret => TaxiNumber == "0" && DestName == "ViewNodesOnly",
						new Sequence(
                            new Action(ret => QBCLog.Info("Targeting Flightmaster: " + CurrentNpc.SafeName + " Distance: " +
												CurrentNpc.Location.Distance(Me.Location) + " to listing known TaxiNodes")),
							new Action(ret => Lua.DoString(string.Format("RunMacroText(\"{0}\")", "/run for i=1,NumTaxiNodes() do a=TaxiNodeName(i); print(i,a);end;"))),
							new Sleep(WaitTime),
							new Action(ret => _isBehaviorDone = true))),

					new Decorator(ret => TaxiNumber != "0",
						new Sequence(
                            new Action(ret => QBCLog.Info("Targeting Flightmaster: " + CurrentNpc.SafeName + " Distance: " +
												CurrentNpc.Location.Distance(Me.Location))),
							new Action(ret => Lua.DoString(string.Format("RunMacroText(\"{0}\")", "/click TaxiButton" + TaxiNumber))),
							new Action(ret => _tryNumber++),
							new Sleep(WaitTime))),

					new Decorator(ret => DestName != "ViewNodesOnly",
						new Sequence(
							new Action(ret => QBCLog.Info("Taking a ride to: " + DestName)),
							new Action(ret => Lua.DoString(string.Format("RunMacroText(\"{0}\")", "/run for i=1,NumTaxiNodes() do a=TaxiNodeName(i); if strmatch(a,'" + DestName + "')then b=i; TakeTaxiNode(b); end end"))),
							new Action(ret => _tryNumber++),
							new Sleep(WaitTime)))
			));
		}


		public override bool IsDone
		{
			get
			{
				return (_isBehaviorDone     // normal completion
						|| !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
			}
		}


		public override void OnFinished()
		{
			// Defend against being called multiple times (just in case)...
			if (!_isOnFinishedRun)
			{
				// QuestBehaviorBase.OnFinished() will set IsOnFinishedRun...
				base.OnFinished();
				_isOnFinishedRun = true;
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
				_doingQuestTimer = Stopwatch.StartNew();
				this.UpdateGoalText(QuestId, "TaxiRide started");
			}
		}

		#endregion


		#region Helpers

		private WoWUnit CurrentNpc
		{
			get
			{
				var npc =
					ObjectManager.GetObjectsOfType<WoWUnit>(false, false)
								 .Where(u => u.Entry == MobId && !FlightPaths.IsIgnored((uint)MobId) &&
											(NpcState == NpcStateType.DontCare || u.IsAlive))
								 .OrderBy(u => u.DistanceSqr)
								 .FirstOrDefault();

				if (npc != null)
                    QBCLog.DeveloperInfo(npc.SafeName);

				return npc;
			}
		}

		#endregion
	}
}

