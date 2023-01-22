// Behavior originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

#region Summary and Documentation
// QUICK DOX:
// 30991-KunLai-DoABarrelRoll is a point-solution behavior.
// The behavior:
//  1) Moves to the Keg Bomb, and gets in the vehicle
//  2) Prefers to take out Osul Treelaunchers first
//      Some Osul Invaders will be collateral damage, and this saves us time
//  3) Takes out the requird Osul Treelaunchers & Osul Invaders
//  4) Profit!
// 
// THINGS TO KNOW:
// * Exit Vehicle doesn't work for this quest
//      You must blow up the vehicle to exit
// * We completely disable combat for this behavior
//      Since we are running directly over mobs, HB & CombatRoutine try to pull
//      and kill mobs--even with PullDistance set to 1--if we don't completely
//      disable combat.
//
// EXAMPLE:
//     <CustomBehavior File="30991-KunLai-DoABarrelRoll" />
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.DoABarrelRoll
{
	[CustomBehaviorFileName(@"SpecificQuests\30991-KunLai-DoABarrelRoll")]
	public class DoABarrelRoll : CustomForcedBehavior
	{
		#region Consructor and Argument Processing
		public DoABarrelRoll(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				QuestId = 30991; // http://wowhead.com/quest=30991
				QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = QuestInLogRequirement.InLog;
				QuestObjectiveIndex_OsulInvader = 1; // http://wowhead.com/quest=30991
				QuestObjectiveIndex_OsulTreelauncher = 2; // http://wowhead.com/quest=30991

				MobId_KegBomb = 60553; // http://wowhead.com/npc=60553
				MobId_KegBombVehicle = 60552; // http://wowhead.com/npc=60553
				MobId_OsulInvader = 60455; // http://wowhead.com/npc=60455
				MobId_OsulTreelauncher = 60483; // http://wowhead.com/npc=60483

				IgniteKeg_LuaCommand = "if GetPetActionCooldown(1) == 0 then CastPetAction(1) end";
				IgniteKeg_FuseDuration = 3.0; //in seconds  http://wowhead.com/spell=120842

				// If we're in the barrel for too long, then below it up and try again...
				// This can happen when mobs are slow to "wink in" causing us to miss our targets.
				// More often, this happens because the battlefield is clear of mobs due to
				// other users running the quest at the same time.
				BarrelRollingTimeBeforeRetrying = 10000; //in milliseconds

				// The barrel movement speed was measured empirically...
				// (Obtained from BattlefieldContext.KegBombVehicle.CurrentSpeed)
				// We 'hard code' it, because Honorbuddy is very slow to report when the barrel
				// is moving, and how fast.  Waiting for Honorbuddy causes delays in calculations
				// that make us miss targets.  This probably happens because the area has mobs
				// "winking in" as you proceed down the hill.  The 'hard coding' works around
				// these defect.
				BarrelMovementSpeed = 22.0;
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


		// Variables for Attributes provided by caller
		public double BarrelMovementSpeed { get; private set; }
		public long BarrelRollingTimeBeforeRetrying { get; private set; }
		public double IgniteKeg_FuseDuration { get; private set; }
		public string IgniteKeg_LuaCommand { get; private set; }
		public int MobId_KegBomb { get; private set; }
		public int MobId_KegBombVehicle { get; private set; }
		public int MobId_OsulInvader { get; private set; }
		public int MobId_OsulTreelauncher { get; private set; }
		public int QuestId { get; private set; }
		public int QuestObjectiveIndex_OsulInvader { get; private set; }
		public int QuestObjectiveIndex_OsulTreelauncher { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }


		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return "$Id$"; } }
		public override string SubversionRevision { get { return "$Rev$"; } }
		#endregion


		#region Private and Convenience variables
		private class BattlefieldContext
		{
			public BattlefieldContext(int mobIdKegBomb, int mobIdKegBombVehicle)
			{
				BarrelRollingTimer = new Stopwatch();
				_mobId_KegBomb = mobIdKegBomb;
				_mobId_KegBombVehicle = mobIdKegBombVehicle;
			}

			public BattlefieldContext ReInitialize()
			{
				BarrelRollingTimer.Restart();
				KegBomb = FindUnitsFromId(_mobId_KegBomb).FirstOrDefault();
				KegBombVehicle =
					ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
					.FirstOrDefault(u => (int)u.Entry == _mobId_KegBombVehicle);
				StyxWoW.Me.ClearTarget();
				SelectedTarget = null;
				return (this);
			}

			public BattlefieldContext Update()
			{
				KegBomb = FindUnitsFromId(_mobId_KegBomb).FirstOrDefault();
				KegBombVehicle =
					ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
					.FirstOrDefault(u => (int)u.Entry == _mobId_KegBombVehicle);
				return (this);
			}

			public Stopwatch BarrelRollingTimer { get; private set; }

			public bool IsKegIgnited
			{
				get
				{
					var charmedUnit = StyxWoW.Me.CharmedUnit;
					return charmedUnit != null && charmedUnit.HasAura("Ignite Keg");
				}
			}

			public WoWUnit KegBomb { get; private set; }
			public WoWUnit KegBombVehicle { get; private set; }
			public WoWUnit SelectedTarget { get; set; }

			private int _mobId_KegBomb;
			private int _mobId_KegBombVehicle;

			private IEnumerable<WoWUnit> FindUnitsFromId(int unitId)
			{
				return
					from unit in ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
					where (unit.Entry == unitId) && unit.IsAlive
							&& (unit.TaggedByMe || unit.TappedByAllThreatLists || !unit.TaggedByOther)
					select unit;
			}
		}

		private double IgniteDistance { get { return (IgniteKeg_FuseDuration +1.000) * BarrelMovementSpeed; } }
		private LocalPlayer Me { get { return StyxWoW.Me; } }

		private int _barrelRollCount = 0;
		private Composite _behaviorTreeHook_Main = null;
		private Composite _behaviorTreeHook_Combat = null;
		private BattlefieldContext _combatContext = null;
		private ConfigMemento _configMemento = null;
		private bool _isBehaviorDone = false;
		#endregion


		#region Overrides of CustomForcedBehavior

		protected override Composite CreateBehavior()
		{
			return _behaviorTreeHook_Main ?? (_behaviorTreeHook_Main = CreateMainBehavior());
		}


        public override void OnFinished()
        {
            // NB: we don't unhook _behaviorTreeHook_Main
            // This was installed when HB created the behavior, and its up to HB to unhook it

            if (_behaviorTreeHook_Combat != null)
            {
                TreeHooks.Instance.RemoveHook("Combat_Main", _behaviorTreeHook_Combat);
                _behaviorTreeHook_Combat = null;
            }

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
			get
			{
				return _isBehaviorDone     // normal completion
						|| !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete);
			}
		}


		public override void OnStart()
		{
			PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

			if ((QuestId != 0) && (quest == null))
			{
				QBCLog.Error("This behavior has been associated with QuestId({0}), but the quest is not in our log", QuestId);
				IsAttributeProblem = true;
			}

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
				CharacterSettings.Instance.NinjaSkin = false;
				CharacterSettings.Instance.SkinMobs = false;
				CharacterSettings.Instance.PullDistance = 1;    // don't pull anything unless we absolutely must

				_combatContext = new BattlefieldContext(MobId_KegBomb, MobId_KegBombVehicle);

				_behaviorTreeHook_Combat = CreateCombatBehavior();
				TreeHooks.Instance.InsertHook("Combat_Main", 0, _behaviorTreeHook_Combat);

				this.UpdateGoalText(QuestId);
			}
		}
		#endregion


		#region Main Behavior
		private Composite CreateCombatBehavior()
		{
			// NB: We'll be running right over some hostiles while in the barrel.
			// Even with a PullDistance set to one, HBcore and the CombatRoutine are going to try to pull these mobs.
			// Thus, this behavior runs at "higher than combat" priority, and prevents the CombatRoutine from executing
			// while this behavior is in progress.
			//
			// NB: We need to allow lower BT nodes to run when the behavior is finished; otherwise, HB will not
			// process the change of _isBehaviorDone state.
			return new Decorator(context => !_isBehaviorDone,
				new PrioritySelector(context => _combatContext.Update(),

					// If quest is done, behavior is done...
					new Decorator(context => !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete),
						new Action(context =>
						{
							_isBehaviorDone = true;
							QBCLog.Info("Finished");
						})),

					// If not in Keg Bomb Vehicle, move to it and get inside...
					new Decorator(context => !Query.IsInVehicle() && (_combatContext.KegBomb != null),
						new PrioritySelector(
							new Decorator(context => _combatContext.KegBomb.Distance > _combatContext.KegBomb.InteractRange,
								new Action(context => { Navigator.MoveTo(_combatContext.KegBomb.Location); })),
							new Decorator(context => Me.IsMoving,
								new Action(context => { WoWMovement.MoveStop(); })),
							new Decorator(context => !Me.IsSafelyFacing(_combatContext.KegBomb),
								new Action(context => { _combatContext.KegBomb.Face(); })),
							new Sequence(
								// N.B. we need to wait a bit before jumping in vehicle after a round -
								// due to a bug that prevents bot from leaving vehicle until a relog
								new WaitContinue(2, context => false, new ActionAlwaysSucceed()),
								new Action(context =>
								{
									_combatContext.ReInitialize();
									_combatContext.KegBomb.Interact();
									QBCLog.Info("Started barrel roll #{0}", ++_barrelRollCount);
									return RunStatus.Failure;
								})),
							new Wait(TimeSpan.FromMilliseconds(1000), context => false, new ActionAlwaysSucceed())
						)),

					// If we are in the vehicle...
					new Decorator(context => Query.IsInVehicle() && (_combatContext.KegBombVehicle != null),
						new PrioritySelector(
							// If we've been in the barrel too long, just blow it up...
							// Blacklist whatever target we were after, and try again
							new Decorator(context => (_combatContext.BarrelRollingTimer.ElapsedMilliseconds > BarrelRollingTimeBeforeRetrying)
														&& !_combatContext.IsKegIgnited,
								new Action(context =>
								{
									QBCLog.Warning("We've been in the barrel too long--we're blowing it up to try again");
									IgniteKeg(_combatContext);
									if (_combatContext.SelectedTarget != null)
										{ Blacklist.Add(_combatContext.SelectedTarget, BlacklistFlags.Combat, TimeSpan.FromMinutes(3)); }
								})),

							// Select target, if not present...
							new Decorator(context => _combatContext.SelectedTarget == null,
								new Action(context => ChooseTarget(_combatContext))),

							// If we have a target, guide barrel to target...
							new Decorator(context => _combatContext.SelectedTarget != null,
								new Action(context =>
								{
									float neededFacing = WoWMathHelper.CalculateNeededFacing(_combatContext.KegBombVehicle.Location,
																							 _combatContext.SelectedTarget.Location);
									neededFacing = WoWMathHelper.NormalizeRadian(neededFacing);

									float neededRotation = Math.Abs(neededFacing - _combatContext.KegBombVehicle.RenderFacing);
									neededRotation = WoWMathHelper.NormalizeRadian(neededRotation);

									// If we need to rotate heading 'too hard' to hit the target, then we missed the target...
									// Blow up the current barrel, blacklist the target, and try again
									if (((neededRotation > (Math.PI / 2)) && (neededRotation < ((Math.PI * 2) - (Math.PI / 2))))
										&& !_combatContext.IsKegIgnited)
									{
										QBCLog.Warning("We passed the selected target--igniting barrel to try again.");
										IgniteKeg(_combatContext);
										Blacklist.Add(_combatContext.SelectedTarget, BlacklistFlags.Combat, TimeSpan.FromMinutes(3));
									}
								
									// Ignite the keg at the appropriate time...
									if (_combatContext.SelectedTarget.Distance <= IgniteDistance)
										{ IgniteKeg(_combatContext); }
								
									// Guide the keg to target...
									Me.SetFacing(neededFacing);
									return RunStatus.Success;
								}))
						))
				));
		}


		protected Composite CreateMainBehavior()
		{
			// Nothing to do for now...
			return new ActionAlwaysFail();
		}
		#endregion


		#region Helpers
		private void ChooseTarget(BattlefieldContext context)
		{
			// Prefer to take out Treelaunchers first...
			int targetId = (!IsQuestObjectiveComplete(QuestId, QuestObjectiveIndex_OsulTreelauncher)
							? MobId_OsulTreelauncher
							: MobId_OsulInvader);

			var viableTargets =
				from unit in ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
				where
					unit.IsValid && unit.IsAlive && !Blacklist.Contains(unit, BlacklistFlags.Combat)
					&& (unit.Entry == targetId)
				orderby unit.Distance
				select unit;

			context.SelectedTarget = viableTargets.FirstOrDefault();
			if (context.SelectedTarget != null)
				{ context.SelectedTarget.Target(); }
		}


		private void IgniteKeg(BattlefieldContext context)
		{
			if (!context.IsKegIgnited)
			{
				Lua.DoString(IgniteKeg_LuaCommand);
			}
		}


		private bool IsQuestObjectiveComplete(int questId, int objectiveId)
		{
		    if (Me.QuestLog.GetQuestById((uint)questId) == null)
				{ return false; }

			int questLogIndex = Lua.GetReturnVal<int>(string.Format("return GetQuestLogIndexByID({0})", questId), 0);

			return
				Lua.GetReturnVal<bool>(string.Format("return GetQuestLogLeaderBoard({0},{1})", objectiveId, questLogIndex), 2);
		}

		#endregion // Behavior helpers
	}
}

