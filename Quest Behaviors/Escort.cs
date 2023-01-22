// Behavior originally contributed by Natfoth.
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
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_Escort
//
// QUICK DOX:
//      Escorts an NPC by protecting and following him, until the quest is complete.
//      The Escort behavior will not pick up the quest that initiates the escort.  You must do that with
//      a separate invocation of the Honorbuddy built-in <Pickup> element.
//
//  Parameters (required, then optional--both listed alphabetically):
//      MobId:      Id the Mob requiring an escort.
//      QuestId:    Id of the quest that starts the escort
//
//      EscortUntil [Default:QuestComplete]: Assumes the values of DestinationReached or QuestComplete.
//          This value determines the completion criteria for the behavior.
//      EscortDestX/Y/Z [Requireed if: EscortUntil == DestinationReached]: Defines the destination location
//          at which the behavior will terminate if EscortUntil == DestinationReached
//      QuestCompleteRequirement [Default:NotComplete]:
//      QuestInLogRequirement [Default:InLog]:
//              A full discussion of how the Quest* attributes operate is described in
//              http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
//      X, Y, Z [Default: toon's current position]: world-coordinates of the general location where
//              the Mob to be escorted can be found.
//
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.Escort
{
	public enum EscortUntilType
	{
		DestinationReached,
		QuestComplete,
	}

	public enum DefendUnitType
	{
		Unit,
		ItemStartTimer,
	}

	public enum ObjectType
	{
		Npc,
		GameObject,
	}

	[CustomBehaviorFileName(@"Escort")]
	public class Escort : CustomForcedBehavior
	{
		public Escort(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				EscortUntil = GetAttributeAsNullable<EscortUntilType>("EscortUntil", false, null, null) ?? EscortUntilType.QuestComplete;
				DefendType = GetAttributeAsNullable<DefendUnitType>("DefendType", false, null, null) ?? DefendUnitType.Unit;
				MobType = GetAttributeAsNullable<ObjectType>("MobType", false, null, new[] { "ObjectType" }) ?? ObjectType.Npc;

				EscortDestination = GetAttributeAsNullable("EscortDest", (EscortUntil == EscortUntilType.DestinationReached), ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;
				Location = GetAttributeAsNullable("", false, ConstrainAs.WoWPointNonEmpty, null) ?? Me.Location;
				ItemId = GetAttributeAsNullable("ItemId", false, ConstrainAs.ItemId, null) ?? 0;
				ObjectId = GetNumberedAttributesAsArray("MobId", 1, ConstrainAs.MobId, new[] { "NpcId" });
				MaxRange = GetAttributeAsNullable("Range", false, ConstrainAs.Range, null) ?? 20;
				QuestId = GetAttributeAsNullable("QuestId", (EscortUntil == EscortUntilType.QuestComplete), ConstrainAs.QuestId(this), null) ?? 0;
				QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;
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
		public WoWPoint EscortDestination { get; private set; }
		public EscortUntilType EscortUntil { get; private set; }
		public DefendUnitType DefendType { get; private set; }
		public ObjectType MobType { get; private set; }

		public WoWPoint Location { get; private set; }
		public int ItemId { get; private set; }
		public int[] ObjectId { get; private set; }
		public double MaxRange { get; private set; }
		public int QuestId { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }

		// Private variables for internal state
		private ConfigMemento _configMemento;
		private bool _isBehaviorDone;
		private Composite _root;
		public Stopwatch TimeOut = new Stopwatch();

		// Private properties
		private const double DestinationTolerance = 5.0;
		private LocalPlayer Me { get { return (StyxWoW.Me); } }

		public WoWItem Item
		{
			get
			{
				return StyxWoW.Me.CarriedItems.FirstOrDefault(ret => ret.Entry == ItemId);
			}
		}

		private WoWObject DefendObject
		{
			get
			{
				WoWObject obj = null;

				switch (MobType)
				{
					case ObjectType.GameObject:
						obj = ObjectManager.GetObjectsOfType<WoWGameObject>()
												.OrderBy(ret => ret.Distance)
												.FirstOrDefault(target => ObjectId.Contains((int)target.Entry));
						break;

					case ObjectType.Npc:
						var baseTargets = ObjectManager.GetObjectsOfType<WoWUnit>()
															   .OrderBy(target => target.Distance)
															   .Where(target => ObjectId.Contains((int)target.Entry));

						var npcStateQualifiedTargets = baseTargets
															.Where(target => (target.IsAlive));

						obj = npcStateQualifiedTargets.FirstOrDefault();
						break;
				}

				if (obj != null)
                { QBCLog.DeveloperInfo(obj.SafeName); }

				return obj;
			}
		}

		private List<WoWUnit> EnemyList
		{
			get
			{
				return (ObjectManager.GetObjectsOfType<WoWUnit>()
										.Where(u => (u.CurrentTarget == DefendObject || u.Location.Distance(DefendObject.Location) < 10) && !u.IsDead)
										.OrderBy(u => u.Location.Distance(DefendObject.Location)).ToList());
			}
		}


		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }

		#region Overrides of CustomForcedBehavior

		protected override Composite CreateBehavior()
		{
			return _root ?? (_root =

			   new PrioritySelector(
				// If we've arrived at the destination, we're done...

			#region DefendType Unit
				new Decorator(ret => DefendType == DefendUnitType.Unit,

						new PrioritySelector(

					new Decorator(ret => ((EscortUntil == EscortUntilType.DestinationReached)
										  && (Me.Location.Distance(EscortDestination) <= DestinationTolerance)),
						new Action(delegate
						{
							TreeRoot.StatusText = "Finished!";
							_isBehaviorDone = true;
						})),

					// If quest is completed, we're done...
					new Decorator(ret => ((EscortUntil == EscortUntilType.QuestComplete) && Me.IsQuestComplete(QuestId)),
						new Sequence(
							new Action(ret => TreeRoot.StatusText = "Finished!"),
							new WaitContinue(120,
								new Action(delegate
								{
									_isBehaviorDone = true;
									return RunStatus.Success;
								}))
							)),

					new Decorator(ret => DefendObject == null,
						new Sequence(
								new Action(ret => TreeRoot.StatusText = "Moving To Location - X: " + Location.X + " Y: " + Location.Y),
								new Action(ret => Navigator.MoveTo(Location)),
								new Sleep(300)
							)
						),

					new Decorator(ret => Me.CurrentTarget != null && Me.CurrentTarget.IsFriendly,
						new Action(ret => Me.ClearTarget())),

					new Decorator(
						ret => EnemyList.Count > 0 && EnemyList[0].IsHostile,
						new PrioritySelector(
							new Decorator(
								ret => Me.CurrentTarget != EnemyList[0],
                                new Sequence(
                                    new Action(ctx => EnemyList[0].Target()),
                                    new SleepForLagDuration())),
							new Decorator(
								ret => !Me.Combat,
								new PrioritySelector(
									new Decorator(
										ret => RoutineManager.Current.PullBehavior != null,
										RoutineManager.Current.PullBehavior),
									new Action(ret => RoutineManager.Current.Pull()))))),


					new Decorator(
						ret => DefendObject != null && (!Me.Combat || Me.CurrentTarget == null || Me.CurrentTarget.IsDead) && DefendObject.DistanceSqr > 5f * 5f,
						new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Following Mob - " + DefendObject.SafeName + " At X: " + DefendObject.X + " Y: " + DefendObject.Y + " Z: " + DefendObject.Z),
									new Action(ret => Navigator.MoveTo(DefendObject.Location)),
									new Sleep(100)
								)
						),

					new Decorator(ret => EnemyList.Count > 0 && (Me.Combat || EnemyList[0].Combat),
						new PrioritySelector(
							new Decorator(
								ret => Me.CurrentTarget == null,
								new Sequence(
								    new Action(ret => EnemyList[0].CurrentTarget.Target()),
								    new SleepForLagDuration())),
							new Decorator(
								ret => !Me.Combat,
								new PrioritySelector(
									new Decorator(
										ret => RoutineManager.Current.PullBehavior != null,
										RoutineManager.Current.PullBehavior),
									new Action(ret => RoutineManager.Current.Pull()))))))),


			#endregion

			#region DefendType ObjectTimer
					new Decorator(ret => DefendType == DefendUnitType.ItemStartTimer,

						new PrioritySelector(

					new Decorator(ret => ((EscortUntil == EscortUntilType.DestinationReached)
										  && (Me.Location.Distance(EscortDestination) <= DestinationTolerance)),
						new Action(delegate
						{
							TreeRoot.StatusText = "Finished!";
							_isBehaviorDone = true;
						})),

					// If quest is completed, we're done...
					new Decorator(ret => ((EscortUntil == EscortUntilType.QuestComplete) && Me.IsQuestComplete(QuestId)),
						new Sequence(
							new Action(ret => TreeRoot.StatusText = "Finished!"),
							new WaitContinue(120,
								new Action(delegate
								{
									_isBehaviorDone = true;
									return RunStatus.Success;
								}))
							)),

					new Decorator(ret => DefendObject == null,
						new Sequence(
								new Action(ret => TreeRoot.StatusText = "Moving To Location - X: " + Location.X + " Y: " + Location.Y),
								new Action(ret => Navigator.MoveTo(Location)),
								new Sleep(300)
							)
						),

					new Decorator(ret => DefendObject != null && DefendObject.WithinInteractRange,
						new Sequence(
								new Action(ret => TreeRoot.StatusText = "Using Item"),
								new Action(ret => Item.UseContainerItem()),
								new Action(ret => TimeOut.Start())
							)
						),

					new Decorator(ret => TimeOut.ElapsedMilliseconds >= 300000 && !DefendObject.WithinInteractRange,
						new Sequence(
								new Action(ret => TreeRoot.StatusText = "Moving To Object : " + DefendObject.Location.Distance(Me.Location)),
								new Action(ret => Navigator.MoveTo(DefendObject.Location)),
								new Sleep(300)
							)
						),

						new Decorator(ret => TimeOut.ElapsedMilliseconds >= 300000 && DefendObject.WithinInteractRange,
							   new Sequence(
								   new Action(ret => DefendObject.Interact()),
								   new Sleep(500),
								   new Action(ret => Lua.DoString("SelectGossipOption(1)")),
								   new Action(ret => TimeOut.Reset())
								   )
							  ),

					new Decorator(ret => Me.CurrentTarget != null && Me.CurrentTarget.IsFriendly,
						new Action(ret => Me.ClearTarget())),

					new Decorator(
						ret => EnemyList.Count > 0 && EnemyList[0].IsHostile,
						new PrioritySelector(
							new Decorator(
								ret => Me.CurrentTarget != EnemyList[0],
                                new Sequence(
                                    new Action(ret =>EnemyList[0].Target()),
                                    new SleepForLagDuration())),
							new Decorator(
								ret => !Me.Combat,
								new PrioritySelector(
									new Decorator(
										ret => RoutineManager.Current.PullBehavior != null,
										RoutineManager.Current.PullBehavior),
									new Action(ret => RoutineManager.Current.Pull()))))),


					new Decorator(
						ret => DefendObject != null && (!Me.Combat || Me.CurrentTarget == null || Me.CurrentTarget.IsDead) && DefendObject.DistanceSqr > 5f * 5f,
						new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Following Mob - " + DefendObject.SafeName + " At X: " + DefendObject.X + " Y: " + DefendObject.Y + " Z: " + DefendObject.Z),
									new Action(ret => Navigator.MoveTo(DefendObject.Location)),
									new Sleep(100)
								)
						),

					new Decorator(ret => EnemyList.Count > 0 && (Me.Combat || EnemyList[0].Combat),
						new PrioritySelector(
							new Decorator(
								ret => Me.CurrentTarget == null,
								new Sequence(
								new Action(ret => EnemyList[0].CurrentTarget.Target()),
								new SleepForLagDuration())),
							new Decorator(
								ret => !Me.Combat,
								new PrioritySelector(
									new Decorator(
										ret => RoutineManager.Current.PullBehavior != null,
										RoutineManager.Current.PullBehavior),
									new Action(ret => RoutineManager.Current.Pull())))))))

			#endregion

)
			);
		}

        public override void OnFinished()
        {
            TreeRoot.GoalText = string.Empty;
            TreeRoot.StatusText = string.Empty;
            if (_configMemento != null)
            {
                _configMemento.Dispose();
                _configMemento = null;
            }

            base.OnFinished();
        }

		public override bool IsDone
		{
			get
			{
				PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

				return (_isBehaviorDone     // normal completion
						|| !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete)
						|| quest != null && quest.IsFailed);
			}
		}


		public override void OnStart()
		{
			QuestBehaviorBase.UsageCheck_ScheduledForDeprecation(this, "EscortGroup");

			// This reports problems, and stops BT processing if there was a problem with attributes...
			// We had to defer this action, as the 'profile line number' is not available during the element's
			// constructor call.
			OnStart_HandleAttributeProblem();

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (!IsDone)
			{
				_configMemento = new ConfigMemento();

				// Disable any settings that may interfere with the escort --
				// When we escort, we don't want to be distracted by other things.
				// NOTE: these settings are restored to their normal values when the behavior completes
				// or the bot is stopped.
				CharacterSettings.Instance.HarvestHerbs = false;
				CharacterSettings.Instance.HarvestMinerals = false;
				CharacterSettings.Instance.LootChests = false;
				ProfileManager.CurrentProfile.LootMobs = false;
				CharacterSettings.Instance.NinjaSkin = false;
				CharacterSettings.Instance.SkinMobs = false;

				var mobNames =
				   (from mob in ObjectManager.GetObjectsOfType<WoWUnit>()
					where ObjectId.Contains((int)mob.Entry)
					let mobName = Utility.GetObjectNameFromId((int)mob.Entry)
					orderby mobName
					select mobName)
					.Distinct();

				this.UpdateGoalText(QuestId, string.Format("Escorting {0}", string.Join(", ", mobNames)));
			}
		}

		#endregion
	}
}