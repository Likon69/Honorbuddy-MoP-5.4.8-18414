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
// 30973-TownlongSteppes-UpInFlames.cs is a point-solution behavior.
// The behavior:
//  1a) Will start combat with Keg Bombs, if a suitable one is located
//  1b) If no keg bomb to start the combat, the toon will go get the mob's
//      attention and drag it back to the nearest keg bomb to use
//      This gives the mob the "Keg Bomb" aura (http://wowhead.com/spell=122425)
//  2) Once the mobs have this aura, we wait for the Dusklight Rangers to
//      ignite the "Keg Bomb" puddles on the ground.
//  3) We drag the mob into the puddle and the "Keg Bomb" aura changes to
//      the "Pitched-Tipped Aura" buff (http://wowhead.com/spell=129119),
//      and can then be killed much easier.
//  4) Profit!
//
//  * For toon safety, isolated mobs and isolated keg bombs are preferred as much as possible
// 
// THINGS TO KNOW:
// * A toon's pet will be instructed appropriately while the behavior is running.
// * The PetActionBar UI must contain the Attack, Follow, Defensive, and Passive pet abilities.
// * Target tagging is bogus in this area.
//      Honorbuddy 'sees' the hostiles fighting NPCs as already tagged--even though
//      they're not.  Thus, we can't tell if the target is viable because an NPC is
//      fighting it, or non-viable because a player is fighting it.  Thus, we fall
//      back to selecting targets that have no players nearby.
// * Keg Bombs can be rolled uphill
//      That's at least one problem we don't have to solve.
// * If we die, we run back to the center of the battlefield
//
// EXAMPLE:
//     <CustomBehavior File="30973-TownlongSteppes-UpInFlames" />
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Linq;

using CommonBehaviors.Actions;
using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.UpInFlames
{
	[CustomBehaviorFileName(@"SpecificQuests\30973-TownlongSteppes-UpInFlames")]
	public class UpInFlames : CustomForcedBehavior
	{
		#region Constructor and Argument Processing
		public UpInFlames(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				QuestId = 30973; // http://wowhead.com/quest=30973
				QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = QuestInLogRequirement.InLog;

				MobId_KegBomb = 62192; // http://wowhead.com/npc=62192
				MobId_KorthikTimberhusk = 61355; // http://wowhead.com/npc=61355

				AuraId_KegBomb = 122425; // http://wowhead.com/spell=122425
				AuraId_Timberhusk = 122134; // http://wowhead.com/spell=122134
				SpellId_PitchTippedArrow = 129119; // http://wowhead.com/spell=129119

				// TUNABLES--
				// If we can't find suitable mobs, we move back towards our Anchor point...
				HuntingGroundAnchorPoint = new WoWPoint(1558.958, 3601.467, 213.1995).FanOutRandom(25.0);
				HuntingGroundAnchorRadius = 50; //yards


				// We look for mobs within this range of a KegBomb...
				// Otherwise, we have problems dragging the mob to the bomb, or getting the bomb
				// to hit the mob from distance.
				KegBombMaxRange = 60.0; //yards

				// We want the mob to be within this range before releasing the keg...
				// This mostly applies to pet-based classes where the pet may have the mob's
				// attention at a distance too far for effective use of the keg.
				// If the mob is out of this range, we will get the mob's attention, and pull him
				// into this range before releasing the keg.  If the pet has the mob's attention,
				// we will bring the pet closer.
				KegBombReleaseRange = 10; //yards

				// If a player is within NoCompeteDistance of a Keg Bomb or Korthik Timberhusk,
				// we ignore those targets.  We need this since HB lies about WoWUnit.TaggedByOther
				// in this area--NPCs fighting the hostiles are detected as "tagged" by HB.
				NoCompeteDistance = 25.0; //yards

				// Avoid pulling mobs or kegs in these areas...
				Blackspots.Add(Tuple.Create(new WoWPoint(1383.731, 3555.141, 227.2891), 40.0f));
				Blackspots.Add(Tuple.Create(new WoWPoint(1423.977, 3550.332, 225.0677), 40.0f));
				Blackspots.Add(Tuple.Create(new WoWPoint(1469.307, 3533.847, 232.4593), 45.0f));
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
		public int AuraId_KegBomb { get; private set; }
		public int AuraId_Timberhusk { get; private set; }
		public List<Tuple<WoWPoint, float>> Blackspots = new List<Tuple<WoWPoint, float>>();
		public WoWPoint HuntingGroundAnchorPoint { get; private set; }
		public double HuntingGroundAnchorRadius { get; private set; }
		public double KegBombMaxRange { get; private set; }
		public double KegBombReleaseRange { get; private set; }
		public int MobId_KegBomb { get; private set; }
		public int MobId_KorthikTimberhusk { get; private set; }
		public double NoCompeteDistance { get; private set; }
		public int SpellId_PitchTippedArrow { get; private set; }

		public int QuestId { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }


		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return "$Id$"; } }
		public override string SubversionRevision { get { return "$Rev$"; } }
		#endregion


		#region Private and Convenience variables
		public delegate WoWPoint LocationDelegate(object context);
		public delegate string StringDelegate(object context);
		public delegate WoWUnit WoWUnitDelegate(object context);

		private enum StateType_Behavior
		{
			Invalid,
			Hunting, // Initial state
			MovingToHuntingGroundAnchor,
		}

		private enum StateType_KegBomb
		{
			Invalid,
			LookingForBomb,     // Initial State
			GettingMobsAttention,
			MovingToAndLaunchingBomb,
			WaitingForBombResults,
			BombUseComplete,
		}

		private enum StateType_PitchTipArrowDragging
		{
			Invalid,
			LookingForPitchTippedGroundEffects,   // Initial state
			DraggingMobToPitchTippedGroundEffect,
			DraggingComplete,
		}


		private double CombatMaxEngagementRangeDistance { get { return 23.0; } }
		private LocalPlayer Me { get { return StyxWoW.Me; } }
		private WoWUnit SelectedKegBomb { get; set; }
		private WoWPoint SelectedKegBombAimPosition { get; set; }
		private WoWDynamicObject SelectedPitchTippedArrow { get; set; }
		private WoWPoint SelectedPitchTippedArrowAimLocation { get; set; }
		private WoWUnit SelectedTarget
		{
			get { return _selectedTarget; }
			set
			{
				// If target changed, set up new POI and make sure its our current target
				if ((value != null) && (value != _selectedTarget))
				{
					BotPoi.Current = new BotPoi(value, PoiType.Kill);
					value.Target();
				}

				_selectedTarget = value;
			}
		}

		private StateType_Behavior State_Behavior
		{ 
			get { return _currentState_Behavior; }
			set
			{
				// For DEBUGGING...
				//if (_currentState_Behavior != value)
				//    { QBCLog.Info("Behavior State: {0}", value); }

				_currentState_Behavior = value;
			}
		}

		private StateType_KegBomb State_KegBomb
		{
			get { return _currentState_KegBomb; }
			set
			{
				// For DEBUGGING...
				//if (_currentState_KegBomb != value)
				//    { QBCLog.Info("KegBomb State: {0}", value); }

				_currentState_KegBomb = value;
			}
		}

		private StateType_PitchTipArrowDragging State_MobDrag
		{
			get { return _currentState_MobDragging; }
			set
			{
				// For DEBUGGING...
				//if (_currentState_MobDragging != value)
				//    { QBCLog.Info("MobDrag State: {0}", value); }

				_currentState_MobDragging = value;
			}
		}

		private Composite _behaviorTreeHook_Combat = null;
		private Composite _behaviorTreeHook_Death = null;
		private Composite _behaviorTreeHook_Main = null;
		private ConfigMemento _configMemento = null;
		private StateType_Behavior _currentState_Behavior = StateType_Behavior.Invalid;
		private StateType_KegBomb _currentState_KegBomb = StateType_KegBomb.Invalid;
		private StateType_PitchTipArrowDragging _currentState_MobDragging = StateType_PitchTipArrowDragging.Invalid;
		private bool _isBehaviorDone = false;
		private WoWUnit _selectedTarget;
		#endregion


		#region Overrides of CustomForcedBehavior
		protected override Composite CreateBehavior()
		{
			return _behaviorTreeHook_Main ?? (_behaviorTreeHook_Main = CreateMainBehavior());
		}


        public override void OnFinished()
        {
            if (_behaviorTreeHook_Combat != null)
            {
                TreeHooks.Instance.RemoveHook("Combat_Main", _behaviorTreeHook_Combat);
                _behaviorTreeHook_Combat = null;
            }

            if (_behaviorTreeHook_Death != null)
            {
                TreeHooks.Instance.RemoveHook("Death_Main", _behaviorTreeHook_Death);
                _behaviorTreeHook_Death = null;
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
				// The "Attack", "Follow", "Defensive", and "Passive" abilities must be on the pet's hotbar, if pet is in use...
				var requiredPetSpells = new List<string>() { "Attack", "Follow", "Defensive", "Passive" };
				var missingSpells =
				   (from spellName in requiredPetSpells
					where Me.PetSpells.FirstOrDefault(p => p.ToString() == spellName) == null
					select spellName)
					.ToList();
				if (Me.GotAlivePet && missingSpells.Count() > 0)
				{
					QBCLog.Error("USER CONFIGURATION ERROR:"
								+ " The following Pet Abilities must be on the Pet ActionBar for this behavior to work: \"{0}\""
								+ " (missing on PetActionBar: \"{1}\")",
						string.Join("\", \"", requiredPetSpells),
						string.Join("\", \"", missingSpells)
						);
					TreeRoot.Stop();
					_isBehaviorDone = true;
				}

				_configMemento = new ConfigMemento();

				// Disable any settings that may interfere with the escort --
				// When we escort, we don't want to be distracted by other things.
				// NOTE: these settings are restored to their normal values when the behavior completes
				// or the bot is stopped.
				CharacterSettings.Instance.PullDistance = 1;    // we want behavior to explicitly pull all mobs

				State_Behavior = StateType_Behavior.MovingToHuntingGroundAnchor;
				State_MobDrag = StateType_PitchTipArrowDragging.LookingForPitchTippedGroundEffects;
				State_KegBomb = StateType_KegBomb.LookingForBomb;

				_behaviorTreeHook_Combat = CreateCombatBehavior();
				TreeHooks.Instance.InsertHook("Combat_Main", 0, _behaviorTreeHook_Combat);
				_behaviorTreeHook_Death = CreateDeathBehavior();
				TreeHooks.Instance.InsertHook("Death_Main", 0, _behaviorTreeHook_Death);

				this.UpdateGoalText(QuestId);
			}
		}
		#endregion


		#region Main Behavior
		protected Composite CreateCombatBehavior()
		{
			return new Decorator(context => !Me.IsDead && !Me.IsGhost,
				new Decorator(context => Me.Combat || (Me.GotAlivePet && Me.PetInCombat),
					new PrioritySelector(
						// Multi-target combat...
						// If several targets are aggro'd on us.  Make certain our focus is on the best target.
						new Action(context => { SelectedTarget = ChooseBestTargetOfThoseAttacking(); return RunStatus.Failure; }),

						// If we're in combat with no SelectTarget, then our first mob has died, and we
						// need to pick another to spank.
						new Decorator(context => !IsViableUnit(SelectedTarget),
							new Action(context => { SelectedTarget = ChooseTargetToPull(); return RunStatus.Failure; })),

						// If there are any nearby Pitch Tipped Arrow ground effects, drag mob into them...
						UtilityBehavior_DragMobIntoPitchArrow(),

						// If our SelectTarget is missing his Keg Bomb aura, go fetch him a fresh one...
						new Decorator(context => FindPitchedTippedArrowGroundEffects().FirstOrDefault() == null,
							UtilityBehavior_UseKegBomb())
					)));
		}


		protected Composite CreateDeathBehavior()
		{
			// If toon dies, we need to restart behavior
			return new Decorator(context => (Me.IsDead || Me.IsGhost) && (State_Behavior != StateType_Behavior.MovingToHuntingGroundAnchor),
				new Action(context => { State_Behavior = StateType_Behavior.MovingToHuntingGroundAnchor; }));
		}


		public Composite CreateMainBehavior()
		{
			return new Decorator(context => !Me.IsDead && !Me.IsGhost,
				new PrioritySelector(
					// If quest is done, behavior is done...
					new Decorator(context => IsDone,
						new Action(context =>
						{
							_isBehaviorDone = true;
							QBCLog.Info("Finished");
						})),

					// Stateful operation:
					new Switch<StateType_Behavior>(context => State_Behavior,
					#region State: DEFAULT
					new Action(context =>   // default case
					{
						QBCLog.MaintenanceError("BehaviorState({0}) is unhandled", State_Behavior);
						TreeRoot.Stop();
						_isBehaviorDone = true;
					}),
					#endregion

					#region State: Moving to Hunting Ground Center
					// We strayed too far from hunting ground center, so move back...
					new SwitchArgument<StateType_Behavior>(StateType_Behavior.MovingToHuntingGroundAnchor,
						new PrioritySelector(
							new Decorator(context => CharacterSettings.Instance.UseMount == false,
								new Action(context => { CharacterSettings.Instance.UseMount = true; })),

							// Move back to anchor point...
							new Decorator(context => Me.Location.Distance(HuntingGroundAnchorPoint) > HuntingGroundAnchorRadius,
								new Action(context =>
								{
									QBCLog.Info("Returning to center of hunting ground",
												Me.Location.Distance(HuntingGroundAnchorPoint));
									Navigator.MoveTo(HuntingGroundAnchorPoint);
								})),

							// At anchor point, time to start hunting again...
							new Decorator(context => Me.Mounted,
								new Sequence(
									new Mount.ActionLandAndDismount(),
									new Action(context =>
									{
										CharacterSettings.Instance.UseMount = false;
									}))),

							new Action(context => { State_Behavior = StateType_Behavior.Hunting; })
						)),
					#endregion

					#region State: Hunting
					// Find targets to spank...
					new SwitchArgument<StateType_Behavior>(StateType_Behavior.Hunting,
						new PrioritySelector(
							// If no target, pick one...
							new Decorator(context => !IsViableUnit(SelectedTarget),
								new Action(context =>
								{
									SelectedTarget = ChooseTargetToPull();

									// If unable to locate a viable target, move back to hunting ground center....
									if (!IsViableUnit(SelectedTarget))
										{ State_Behavior = StateType_Behavior.MovingToHuntingGroundAnchor; }
								})),

							// If we've a viable target, go get its attention...
							new Decorator(context => IsViableUnit(SelectedTarget),
								new PrioritySelector(
									// Make sure HBcore is on the same page with target selection...
									new Decorator(context => (Me.CurrentTarget != SelectedTarget),
										new Action(context =>
										{
											BotPoi.Current = new BotPoi(SelectedTarget, PoiType.Kill);
											SelectedTarget.Target();
											return RunStatus.Failure;
										})),
									// Start combat with Keg Bomb, if one is available and target needs aura...
									UtilityBehavior_UseKegBomb()
								))
						))
					#endregion
				)));
		}
		#endregion


		#region Helpers
		private WoWPoint CalculateKegBombAimPosition(WoWUnit target, WoWUnit kegBomb)
		{
			return
				WoWMathHelper.CalculatePointFrom(
					kegBomb.Location,
					target.Location,
					(float)(target.Location.Distance(kegBomb.Location) + 2.5));
		}


		private WoWPoint CalculatePositionToPutMobInGroundEffect(WoWUnit hostileTarget, WoWDynamicObject groundEffect)
		{
			return WoWMathHelper.CalculatePointFrom(
				groundEffect.Location, 
				hostileTarget.Location,
				(float)hostileTarget.Location.Distance(groundEffect.Location) + hostileTarget.CombatReach);
		}


		private WoWUnit ChooseTargetToPull()
		{
			return
			   (from unit in ObjectManager.GetObjectsOfType<WoWUnit>()
				let kegBombCount = FindKegBombs(unit, 1, KegBombMaxRange).Count()
				where
					unit.IsValid
					&& unit.IsAlive
					&& (unit.Entry == MobId_KorthikTimberhusk)
					&& (unit.HealthPercent > 10.0)
					&& (CountPlayersAround(unit, NoCompeteDistance) <= 0)
					&& !Blacklist.Contains(unit, BlacklistFlags.Combat)
					&& (kegBombCount > 0)
					&& !IsInBlackspottedArea(unit)
				orderby
					(
						unit.Distance                           // prefer closer mobs
						* unit.HealthPercent                    // prefer low-health mobs
					) / kegBombCount                            // prefer units near multiple bombs
					+ (!unit.HasAura(AuraId_Timberhusk)         // prefer units without Timberhusk buff
						? 0 : 10000)
					+ (unit.HasAura(AuraId_KegBomb) || unit.HasAura(SpellId_PitchTippedArrow)
						? 0 : 1000)                             // prefer mobs having desired aura
					+ (unit.IsTargetingMeOrPet ? 0 : 1000000)   // prefer units already attacking us
					+ CountHostileMobsAround(unit, unit, KegBombReleaseRange) * 2000    // prefer isolated mobs
				select unit)
				.FirstOrDefault();
		}


		private WoWUnit ChooseBestTargetOfThoseAttacking()
		{
			IEnumerable<WoWUnit> mobsAttackingMe =
				ObjectManager.GetObjectsOfType<WoWUnit>()
				.Where(u => IsViableUnit(u) && u.IsTargetingMeOrPet)
				.ToList();

			if (mobsAttackingMe.Count() <= 1)
				{ return SelectedTarget; }

			// Re-evaluate which mob to spank first...
			return
				(from unit in mobsAttackingMe
				orderby
					(!unit.HasAura(AuraId_Timberhusk)   // prefer units without Timberhusk buff
						? 0 : 10000)
					+ unit.HealthPercent                // prefer weaker mobs
					+ unit.Distance                     // prefer closer units
				select unit)
				.FirstOrDefault();
		}


		private int CountPlayersAround(WoWUnit target, double noCompeteDistance)
		{
			return
			   (from player in ObjectManager.GetObjectsOfType<WoWPlayer>()
				where
					player.IsValid
					&& (player.Location.Distance(target.Location) <= noCompeteDistance)
				select player)
				.Count();
		}


		private int CountHostileMobsAround(WoWUnit unit, WoWUnit excludeThisUnitInCount, double radius)
		{
			return
			   (from wowUnit in ObjectManager.GetObjectsOfType<WoWUnit>()
				where
					wowUnit.IsValid
					&& wowUnit.IsHostile
					&& (wowUnit.Location.Distance(unit.Location) < radius)
					&& (wowUnit != excludeThisUnitInCount)
				select wowUnit)
				.Count();
		}


		private IEnumerable<WoWUnit> FindKegBombs(WoWUnit fromUnit, int maxSurroundingHostileCount, double searchRadius = 1000.0)
		{
			return
			   (from kegBomb in ObjectManager.GetObjectsOfType<WoWUnit>()
				where
					kegBomb.IsValid
					&& (kegBomb.Entry == MobId_KegBomb)
					&& kegBomb.IsAlive
					&& (CountPlayersAround(kegBomb, NoCompeteDistance) <= 0)
					&& (fromUnit.Location.Distance(kegBomb.Location) < searchRadius)
					&& !Blacklist.Contains(kegBomb, BlacklistFlags.Combat)
					&& !IsInBlackspottedArea(kegBomb)
				orderby
					kegBomb.Distance        // prefer nearby kegs
				select kegBomb);
		}


		private IEnumerable<WoWDynamicObject> FindPitchedTippedArrowGroundEffects()
		{
			return
				from groundEffect in ObjectManager.GetObjectsOfType<WoWDynamicObject>()
				where
					groundEffect.IsValid
					&& (groundEffect.SpellId == SpellId_PitchTippedArrow)
					&& (groundEffect.Distance <= 25.0)
				orderby groundEffect.Distance
				select groundEffect;
		}


		private bool IsInBlackspottedArea(WoWUnit wowUnit)
		{
			return Blackspots.Any(b => wowUnit.Location.Distance(b.Item1) < b.Item2);
		}

		private bool IsViableUnit(WoWUnit wowUnit)
		{
			return ((wowUnit != null) && wowUnit.IsValid && wowUnit.IsAlive);
		}


		private void SeriouslyStop() // Seriously!
		{
			// HBcore has serious problems stopping "Backward" movement.  Thus, this shotgun approach to trying
			// to get the movement to stop.
			WoWMovement.MoveStop(WoWMovement.MovementDirection.Backwards);
			WoWMovement.StopFace();
			WoWMovement.MoveStop();
			Navigator.PlayerMover.MoveStop();
		}

	
		private Composite UtilityBehavior_BringPetIntoRange(LocationDelegate locationDelegate,
															double maxDistanceAllowed,
															WoWUnit selectedTarget)
		{
			// We only need to take this action if we've an alive pet, and the target is attacking it...
			return new Decorator(context => Me.GotAlivePet && selectedTarget.PetAggro
											&& (Me.Pet.Location.Distance(locationDelegate(context)) > maxDistanceAllowed),
				new Action(context =>
				{
					QBCLog.Info("Bringing Pet within {0} yards of Keg Bomb (dist: {1:F1})",
						maxDistanceAllowed,
						Me.Pet.Location.Distance(locationDelegate(context)));
							
					string petCommand = "Follow";
					WoWPetSpell petAction = Me.PetSpells.FirstOrDefault(p => p.ToString() == petCommand);
					if ((petAction != null) && (petAction.Spell != null))
					{
						QBCLog.Info("[Pet] Casting {0}", petAction.Action.ToString());
						Lua.DoString("CastPetAction({0})", petAction.ActionBarIndex + 1);
					}

					return RunStatus.Failure;
				}));
		}


		private Composite UtilityBehavior_DragMobIntoPitchArrow()
		{
			// Stateful Operation:
			return new Switch<StateType_PitchTipArrowDragging>(context => State_MobDrag,
				#region State: DEFAULT
				new Action(context =>   // default case
				{
					QBCLog.MaintenanceError("StateType_PitchTipArrowDragging({0}) is unhandled", State_MobDrag);
					TreeRoot.Stop();
					_isBehaviorDone = true;
				}),
				#endregion


				#region State: LookingForPitchTipGroundEffects
				new SwitchArgument<StateType_PitchTipArrowDragging>(StateType_PitchTipArrowDragging.LookingForPitchTippedGroundEffects,
					new Decorator(context => IsViableUnit(SelectedTarget) && SelectedTarget.HasAura(AuraId_Timberhusk),
						new Action(context =>
						{
							ObjectManager.Update();
							SelectedPitchTippedArrow = FindPitchedTippedArrowGroundEffects().FirstOrDefault();
						
							if (!SelectedTarget.HasAura(SpellId_PitchTippedArrow) && (SelectedPitchTippedArrow != null))
							{
								SelectedPitchTippedArrowAimLocation = CalculatePositionToPutMobInGroundEffect(SelectedTarget,
																												SelectedPitchTippedArrow);

								if (SelectedPitchTippedArrowAimLocation != WoWPoint.Empty)
									{ State_MobDrag = StateType_PitchTipArrowDragging.DraggingMobToPitchTippedGroundEffect; }
							}

							return RunStatus.Failure;  // allows 'lower' nodes in Behavior tree to run
						}))
					),
				#endregion


				#region State: DraggingMob
				new SwitchArgument<StateType_PitchTipArrowDragging>(StateType_PitchTipArrowDragging.DraggingMobToPitchTippedGroundEffect,
					new PrioritySelector(
						// If our target is no longer valid, finish up...
						new Decorator(context => !IsViableUnit(SelectedTarget),
							new Action(context => { State_MobDrag = StateType_PitchTipArrowDragging.DraggingComplete; })),

						// If the Pitch-Tipped arrow is no longer available, finish up...
						new Decorator(context => (SelectedPitchTippedArrow == null)
													|| !SelectedPitchTippedArrow.IsValid,
							new Action(context => { State_MobDrag = StateType_PitchTipArrowDragging.DraggingComplete; })),

						// If our target has lost its dreaded buff, finish up...
						new Decorator(context => !SelectedTarget.HasAura(AuraId_Timberhusk),
							new Action(context => { State_MobDrag = StateType_PitchTipArrowDragging.DraggingComplete; })),

						// If we've arrived at the desired position, finish up...
						new Decorator(context => Navigator.AtLocation(SelectedPitchTippedArrowAimLocation),
							new Action(context => { State_MobDrag = StateType_PitchTipArrowDragging.DraggingComplete; })),

						// Do move...
						new PrioritySelector(distanceToAimPositionContext => Me.Location.Distance(SelectedPitchTippedArrowAimLocation),
							// Bring pet to us while we position...
							PetBehavior_SetStance(context => "Passive"),
							PetBehavior_ActionFollow(),

							// Pitch-tipped ground effect at a distance--just run for it...
							new Decorator(distanceToAimPositionContext => ((float)distanceToAimPositionContext) > (10.0 + (2 * SelectedPitchTippedArrow.Radius)),
								new Action(distanceToAimPositionContext => { Navigator.MoveTo(SelectedPitchTippedArrowAimLocation); })),

							// We're going to back into the pitch-tipped arrow ground effect for the last few steps...
							// NB: this is a more stable configuration that trying to turn and go directly to the correct
							// position.  I.e., it should minimize "dancing with the mob".
							// NB: We return 'success' here.  This prevents the CombatRoutine from running which can mess
							// up our delicately needed positioning.
							new Action(distanceToAimPositionContext => 
							{
								float neededFacing = WoWMathHelper.CalculateNeededFacing(Me.Location,
																						SelectedPitchTippedArrowAimLocation);
								neededFacing = WoWMathHelper.NormalizeRadian(neededFacing  + (float)Math.PI);

								if (Me.RenderFacing != neededFacing)
									{ Me.SetFacing(neededFacing); }

								if ((((float)distanceToAimPositionContext) > Navigator.PathPrecision) && !Me.IsMoving)
								{
									QBCLog.Info("Dragging mob into Pitch Arrow ground-effect.",
										(float)distanceToAimPositionContext);
									WoWMovement.Move(WoWMovement.MovementDirection.Backwards);
								}

								return RunStatus.Success;
							})
						),

						// Completely disable the combat routine while the "dragging" activity in progress...
						// Otherwise, the CombatRoutine can move to close again on the mob, while we are headed
						// for the Pitch-tipped arrow ground effect, and other such undesirable situations.
						new ActionAlwaysSucceed()
					)),
				#endregion


				#region State: DragCompleting
				new SwitchArgument<StateType_PitchTipArrowDragging>(StateType_PitchTipArrowDragging.DraggingComplete,
					new PrioritySelector(
						// Put pet back to work...
						PetBehavior_SetStance(context => "Defensive"),
						PetBehavior_ActionAttack(context => SelectedTarget),

						// HBcore has serious stopping "Backward" movement.  Thus, this shotgun approach to trying
						// to get the movement to stop.
						new Decorator(context => Me.IsMoving,
							new Action(context => { SeriouslyStop(); })),

						// Wrap up bomb use...
						new Action(context =>
						{
							SeriouslyStop();
							SelectedPitchTippedArrow = null;
							SelectedPitchTippedArrowAimLocation = WoWPoint.Empty;
							State_MobDrag = StateType_PitchTipArrowDragging.LookingForPitchTippedGroundEffects;
							QBCLog.Info("Dragging complete");

							return RunStatus.Failure; // allows 'lower' nodes in Behavior tree to run
						})
					))
				#endregion
			);
		}


		private Composite UtilityBehavior_UseKegBomb()
		{
			// Stateful Operation:
			// NB: Don't be tempted to factor IsViableUnit() to this level.  There are substates that
			// need to clean things up when they're finished, and IsViableUnit() at top level
			// would prevent that.
			return new Switch<StateType_KegBomb>(context => State_KegBomb,
				#region State: DEFAULT
				new Action(context =>   // default case
				{
					QBCLog.MaintenanceError("StateType_KegBomb({0}) is unhandled", State_KegBomb);
					TreeRoot.Stop();
					_isBehaviorDone = true;
				}),
				#endregion


				#region State: LookingForBomb
				new SwitchArgument<StateType_KegBomb>(StateType_KegBomb.LookingForBomb,
					new Decorator(context => IsViableUnit(SelectedTarget),
						new PrioritySelector(
							// If our target is missing Timberhusk buff, but we're not killing it, go fix the situation...
							new Decorator(context => !SelectedTarget.HasAura(AuraId_Timberhusk),
								new Action(context => { State_KegBomb = StateType_KegBomb.GettingMobsAttention; })),

							// If mob is ignoring us, but has buff we want, go get its attention...
							new Decorator(context => !SelectedTarget.IsTargetingMeOrPet
														&& (SelectedTarget.HasAura(AuraId_KegBomb)
															|| SelectedTarget.HasAura(SpellId_PitchTippedArrow)),
								new Action(context => { State_KegBomb = StateType_KegBomb.GettingMobsAttention; })),
											
							// If we've a viable target that doesn't have the KegBomb or PitchTipped aura, go get it...
							new Decorator(context => !(SelectedTarget.HasAura(AuraId_KegBomb)
														|| SelectedTarget.HasAura(SpellId_PitchTippedArrow)),
								new PrioritySelector(
									// If we have the mob's attention, find a bomb and move to it...
									new Decorator(context => SelectedTarget.IsTargetingMeOrPet,
										new Action(context =>
										{
											SelectedKegBomb = FindKegBombs(SelectedTarget, 1, KegBombMaxRange).FirstOrDefault();

											// If we found bomb, go use it...
											if (SelectedKegBomb != null)
											{
												State_KegBomb = StateType_KegBomb.MovingToAndLaunchingBomb;
												return RunStatus.Success;
											}
									
											// Could not locate a bomb within reasonable range, keep trying...
											return RunStatus.Failure;
										})),

									// If we do not have the mob's attention, find a bomb and move to it...
									new Decorator(context => !SelectedTarget.IsTargetingMeOrPet,
										new Action(context =>
										{
											SelectedKegBomb = FindKegBombs(SelectedTarget, 1, KegBombReleaseRange).FirstOrDefault();

											// If we've found a usable bomb in range of a non-attacking mob, move to it...
											// (The mob may just be standing around, or in combat with something else.)
											if (SelectedKegBomb != null)
											{
												State_KegBomb = StateType_KegBomb.MovingToAndLaunchingBomb;
												return RunStatus.Success;
											}
									
											// Otherwise, widen search for Keg Bomb...
											SelectedKegBomb = FindKegBombs(SelectedTarget, 1, KegBombMaxRange).FirstOrDefault();

											// If we found bomb we can drag a mob to, go get the mob's attention...
											if (SelectedKegBomb != null)
											{
												State_KegBomb = StateType_KegBomb.GettingMobsAttention;
												return RunStatus.Success;
											}

											// Otherwise, no viable bomb within range of this mob...
											// So, blacklist the mob for a bit, and try again.
											Blacklist.Add(SelectedTarget, BlacklistFlags.Combat, TimeSpan.FromMinutes(2));
											SelectedTarget = null;
											return RunStatus.Success;
										}))
								))
							))
						),
				#endregion


				#region State: GettingMobsAttention
				new SwitchArgument<StateType_KegBomb>(StateType_KegBomb.GettingMobsAttention,
					new PrioritySelector(
						// If mob is no longer viable, start looking for bombs again...
						new Decorator(context => !IsViableUnit(SelectedTarget),
							new Action(context => { State_KegBomb = StateType_KegBomb.LookingForBomb; })),

						// If we've the attention of the mob and its got its buff, go find bomb...
						// (If mob is unbuffed, we'll continue to spank it, as playing with bomb adds no value
						// to the encounter).
						new Decorator(context => SelectedTarget.IsTargetingMeOrPet && SelectedTarget.HasAura(AuraId_Timberhusk),
							new Action(context => { State_KegBomb = StateType_KegBomb.LookingForBomb; })),

						// Move to mob and start spanking it...
						new Action(context =>
						{
							if (SelectedTarget.HasAura(AuraId_Timberhusk))
                            { QBCLog.Info("Getting {0}'s attention", SelectedTarget.SafeName); }
							else
                            { QBCLog.Info("Killing unbuffed {0}", SelectedTarget.SafeName); }
							return RunStatus.Failure; // fall through
						}),
						new Decorator(context => (Me.CurrentTarget != SelectedTarget),
							new Action(context => { SelectedTarget.Target(); return RunStatus.Failure; })),
						new Decorator(context => SelectedTarget.Distance > CombatMaxEngagementRangeDistance,
							new Action(context => { Navigator.MoveTo(SelectedTarget.Location); })),
						new Decorator(context => RoutineManager.Current.CombatBehavior != null,
							RoutineManager.Current.CombatBehavior),
						new Action(context =>
						{
							RoutineManager.Current.Combat();
							return RunStatus.Failure;
						})
					)),
				#endregion


				#region State: LaunchingBomb
				new SwitchArgument<StateType_KegBomb>(StateType_KegBomb.MovingToAndLaunchingBomb,
					new PrioritySelector(
						// If mob is no longer viable, start looking for bombs again...
						new Decorator(context => !IsViableUnit(SelectedTarget),
							new Action(context => { State_KegBomb = StateType_KegBomb.LookingForBomb; })),

						// If our target is missing Timberhusk buff, but we're not killing it, go fix the situation...
						new Decorator(context => !SelectedTarget.HasAura(AuraId_Timberhusk),
							new Action(context => { State_KegBomb = StateType_KegBomb.GettingMobsAttention; })),

						// If bomb is no longer valid, go find another...
						new Decorator(context => !IsViableUnit(SelectedKegBomb),
							new Action(context => { State_KegBomb = StateType_KegBomb.BombUseComplete; })),

						// If bomb is moving, its already been used, go wait for results...
						new Decorator(context => SelectedKegBomb.IsMoving,
							new Action(context => { State_KegBomb = StateType_KegBomb.WaitingForBombResults; })),

						// If we don't have mob's attention, and bomb not within launching range of mob, go get mob's attention...
						new Decorator(context => !SelectedTarget.IsTargetingMeOrPet
													&& (SelectedTarget.Location.Distance(SelectedKegBomb.Location)
														> KegBombReleaseRange),
							new Action(context => { State_KegBomb = StateType_KegBomb.GettingMobsAttention; })),

						// Rough aiming: Properly position self and pet...
						new PrioritySelector(roughAimPositionContext => CalculateKegBombAimPosition(SelectedTarget, SelectedKegBomb),
							// If pet has aggro, properly position pet to attack mob when both are in range...
							new Decorator(roughAimPositionContext => Me.GotAlivePet && SelectedTarget.PetAggro,
								new PrioritySelector(
									// Just wait, if both pet and mob are out of bomb-launching range...
									new Decorator(roughAimPositionContext => (Me.Pet.Location.Distance((WoWPoint)roughAimPositionContext) > KegBombReleaseRange)
																|| (SelectedTarget.Location.Distance((WoWPoint)roughAimPositionContext) > KegBombReleaseRange),
										new PrioritySelector(
											// Bring pet to us while we pull mob within range of bomb...
											PetBehavior_SetStance(context => "Passive"),
											PetBehavior_ActionFollow(),

											// Just wait until both are within range
											new ActionAlwaysSucceed()
										)),

									// Have pet hold mob while we position to launch bomb...
									PetBehavior_SetStance(context => "Defensive"),
									PetBehavior_ActionAttack(context => SelectedTarget)
								)),
							
							// Move self to the Keg Bomb rough aiming position for Keg Bomb...
							new Decorator(roughAimPositionContext => !Navigator.AtLocation((WoWPoint)roughAimPositionContext),
								new Action(roughAimPositionContext =>
								{ 
									QBCLog.Info("Moving to Keg Bomb (dist: {0:F1})", 
										Me.Location.Distance((WoWPoint)roughAimPositionContext));
									Navigator.MoveTo((WoWPoint)roughAimPositionContext);
								})),
							new Mount.ActionLandAndDismount(),
							new Decorator(context => Me.IsMoving,
								new Action(context => { WoWMovement.MoveStop(); })),
							new Decorator(context => !Me.IsFacing(SelectedKegBomb),
								new Action(context => { SelectedKegBomb.Face(); }))                                                
						),

						// Fine aiming: If mob in range, finalize launching details...
						new Decorator(context => SelectedKegBomb.Location.Distance(SelectedTarget.Location) <= KegBombReleaseRange,
							new PrioritySelector(
								// Final ("fine") aiming position...
								// It will be the same as the "rough" position we were previously using unless the mob
								// has altered course while approaching the Keg Bomb.  We "lock in" the fine aim here,
								// so we don't 'dance' around the barrel.  If we miss, we miss.
								new Decorator(context => SelectedKegBombAimPosition == WoWPoint.Empty,
									new Action(context =>
									{
										SelectedKegBombAimPosition =
											CalculateKegBombAimPosition(SelectedTarget, SelectedKegBomb);

										if (SelectedKegBombAimPosition == WoWPoint.Empty)
										{
											QBCLog.Warning("Unable to calculate aim position for Keg Bomb--blacklisting Keg Bomb.");
											State_KegBomb = StateType_KegBomb.BombUseComplete;
										}
									})),

								new Decorator(context => !Navigator.AtLocation(SelectedKegBombAimPosition),
									new Action(context => { Navigator.MoveTo(SelectedKegBombAimPosition); })),
								new Mount.ActionLandAndDismount(),
								new Decorator(context => Me.IsMoving,
									new Action(context => { WoWMovement.MoveStop(); })),
								new Decorator(context => !Me.IsFacing(SelectedKegBomb),
									new Action(context => { SelectedKegBomb.Face(); })),
								new Decorator(context => !SelectedKegBomb.IsMoving,
									new Action(kegBombPositonContext =>
									{
										SelectedKegBomb.Interact();
										return RunStatus.Failure; // Fall thru
									}))
							)),

						// Completely disable the combat routine while the "use keg bomb" activity in progress...
						// Otherwise, the CombatRoutine can move to close again on the mob, while we are headed
						// for the keg, and other such undesirable situations.
						new ActionAlwaysSucceed()
					)),
				#endregion
					

				#region State: WaitingForBombResults
				new SwitchArgument<StateType_KegBomb>(StateType_KegBomb.WaitingForBombResults,
					new PrioritySelector(
						// If mob is no longer viable, we're done...
						new Decorator(context => !IsViableUnit(SelectedTarget),
							new Action(context => { State_KegBomb = StateType_KegBomb.BombUseComplete; })),

						// If the Keg Bomb is no longer viable, we're done...
						// (Bomb exploded at target, or missed.)
						new Decorator(context => !IsViableUnit(SelectedKegBomb),
							new Action(context => { State_KegBomb = StateType_KegBomb.BombUseComplete; })),

						// If mob has acquired a desired aura, we're done...
						new Decorator(context => SelectedTarget.HasAura(AuraId_KegBomb),
							new Action(context => { State_KegBomb = StateType_KegBomb.BombUseComplete; })),

						// If our target is missing Timberhusk buff, but we're not killing it, go fix the situation...
						new Decorator(context => !SelectedTarget.HasAura(AuraId_Timberhusk),
							new Action(context => { State_KegBomb = StateType_KegBomb.BombUseComplete; }))
					)),
				#endregion


				#region State: BombUseComplete
				new SwitchArgument<StateType_KegBomb>(StateType_KegBomb.BombUseComplete,
					new PrioritySelector(
						// Put pet back to work...
						PetBehavior_SetStance(context => "Defensive"),
						new Decorator(context => SelectedTarget.IsTargetingMeOrPet,
							PetBehavior_ActionAttack(context => SelectedTarget)),

						// Wrap up bomb use...
						new Action(context =>
						{
							if (SelectedKegBomb != null)
							{
								Blacklist.Add(SelectedKegBomb, BlacklistFlags.Combat, TimeSpan.FromMinutes(2));
								SelectedKegBomb = null;
								SelectedKegBombAimPosition = WoWPoint.Empty;
							}

							// If mob is not aggro'd on us, clear it, and re-evaluate the battlefield...
							if (!SelectedTarget.IsTargetingMeOrPet)
								{ SelectedTarget = null; }

							State_KegBomb = StateType_KegBomb.LookingForBomb;
						}))
					)
				#endregion
			);
		}

		#endregion


		#region Pet Helpers
		public bool CanCastPetAction(string petActionName)
		{
			WoWPetSpell petAction = Me.PetSpells.FirstOrDefault(p => p.ToString() == petActionName);
			if (petAction == null)
				{ return false; }
			if ((petAction.SpellType == WoWPetSpell.PetSpellType.Spell) && (petAction.Spell == null))
				{ return false; }

			return (petAction.SpellType == WoWPetSpell.PetSpellType.Spell)
				? !petAction.Spell.Cooldown
				: true;
		}


		public void CastPetAction(string petActionName)
		{
			WoWPetSpell petAction = Me.PetSpells.FirstOrDefault(p => p.ToString() == petActionName);
			if (petAction == null)
				return;

			QBCLog.Info("Instructing pet to \"{0}\"", petActionName);
			Lua.DoString("CastPetAction({0})", petAction.ActionBarIndex +1);
		}


		public void CastPetAction(string petActionName, WoWUnit wowUnit)
		{
			WoWPetSpell petAction = Me.PetSpells.FirstOrDefault(p => p.ToString() == petActionName);
			if (petAction == null)
				return;

            QBCLog.Info("Instructing pet \"{0}\" on {1}", petActionName, wowUnit.SafeName);
			StyxWoW.Me.SetFocus(wowUnit);
			Lua.DoString("CastPetAction({0}, 'focus')", petAction.ActionBarIndex +1);
			StyxWoW.Me.SetFocus(0);
		}


		public bool IsPetActionActive(string petActionName)
		{
			WoWPetSpell petAction = Me.PetSpells.FirstOrDefault(p => p.ToString() == petActionName);
			if (petAction == null)
				{ return false; }

			return Lua.GetReturnVal<bool>(string.Format("return GetPetActionInfo({0})", petAction.ActionBarIndex +1), 4);
		}


		public Composite PetBehavior_ActionAttack(WoWUnitDelegate wowUnitDelegate)
		{
			string spellName = "Attack";
			return new Decorator(context => Me.GotAlivePet
											&& (wowUnitDelegate(context) != null)
											&& (Me.Pet.CurrentTarget != wowUnitDelegate(context))
											&& !wowUnitDelegate(context).IsFriendly
											&& CanCastPetAction(spellName),
				new Action(context => CastPetAction(spellName, wowUnitDelegate(context))));
		}


		public Composite PetBehavior_ActionFollow()
		{
			string spellName = "Follow";
			return new Decorator(context => Me.GotAlivePet
											&& CanCastPetAction(spellName)
											&& (!IsPetActionActive(spellName) || (Me.Pet.CurrentTarget != null)),
				new Action(context => CastPetAction(spellName)));
		}
		

		public Composite PetBehavior_SetStance(StringDelegate petStanceNameDelegate)
		{
			string[] knownStanceNames = { "Assist", "Defensive", "Passive" };

			return new PrioritySelector(petStanceNameContext => petStanceNameDelegate(petStanceNameContext),
				new Decorator(petStanceNameContext => !knownStanceNames.Contains((string)petStanceNameContext),
					new Action(petStanceNameContext =>
					{
						QBCLog.MaintenanceError("Unknown pet stance '{0}'.  Must be one of: {1}",
							(string)petStanceNameContext,
							string.Join(", ", knownStanceNames));
						TreeRoot.Stop();
						_isBehaviorDone = true;
					})),

				new Decorator(petStanceNameContext => Me.GotAlivePet
														&& CanCastPetAction((string)petStanceNameContext)
														&& !IsPetActionActive((string)petStanceNameContext),
					new Action(petStanceNameContext => CastPetAction((string)petStanceNameContext)))
			);
		}
	   #endregion
	}


	#region WoWPoint_Extensions
	public static class WoWPoint_Extensions
	{
		public static Random _random = new Random((int)DateTime.Now.Ticks);

		private static LocalPlayer Me { get { return (StyxWoW.Me); } }
		public const double TAU = (2 * Math.PI);    // See http://tauday.com/


		public static WoWPoint Add(this WoWPoint wowPoint,
									double x,
									double y,
									double z)
		{
			return (new WoWPoint((wowPoint.X + x), (wowPoint.Y + y), (wowPoint.Z + z)));
		}


		public static WoWPoint AddPolarXY(this WoWPoint wowPoint,
										   double xyHeadingInRadians,
										   double distance,
										   double zModifier)
		{
			return (wowPoint.Add((Math.Cos(xyHeadingInRadians) * distance),
								 (Math.Sin(xyHeadingInRadians) * distance),
								 zModifier));
		}


		// Finds another point near the destination.  Useful when toon is 'waiting' for something
		// (e.g., boat, mob repops, etc). This allows multiple people running
		// the same profile to not stand on top of each other while waiting for
		// something.
		public static WoWPoint FanOutRandom(this WoWPoint location,
												double maxRadius)
		{
			const int CYLINDER_LINE_COUNT = 12;
			const int MAX_TRIES = 50;
			const double SAFE_DISTANCE_BUFFER = 1.75;

			WoWPoint candidateDestination = location;
			int tryCount;

			// Most of the time we'll find a viable spot in less than 2 tries...
			// However, if you're standing on a pier, or small platform a
			// viable alternative may take 10-15 tries--its all up to the
			// random number generator.
			for (tryCount = MAX_TRIES; tryCount > 0; --tryCount)
			{
				WoWPoint circlePoint;
				bool[] hitResults;
				WoWPoint[] hitPoints;
				int index;
				WorldLine[] traceLines = new WorldLine[CYLINDER_LINE_COUNT + 1];

				candidateDestination = location.AddPolarXY((TAU * _random.NextDouble()), (maxRadius * _random.NextDouble()), 0.0);

				// Build set of tracelines that can evaluate the candidate destination --
				// We build a cone of lines with the cone's base at the destination's 'feet',
				// and the cone's point at maxRadius over the destination's 'head'.  We also
				// include the cone 'normal' as the first entry.

				// 'Normal' vector
				index = 0;
				traceLines[index].Start = candidateDestination.Add(0.0, 0.0, maxRadius);
				traceLines[index].End = candidateDestination.Add(0.0, 0.0, -maxRadius);

				// Cylinder vectors
				for (double turnFraction = 0.0; turnFraction < TAU; turnFraction += (TAU / CYLINDER_LINE_COUNT))
				{
					++index;
					circlePoint = candidateDestination.AddPolarXY(turnFraction, SAFE_DISTANCE_BUFFER, 0.0);
					traceLines[index].Start = circlePoint.Add(0.0, 0.0, maxRadius);
					traceLines[index].End = circlePoint.Add(0.0, 0.0, -maxRadius);
				}


				// Evaluate the cylinder...
				// The result for the 'normal' vector (first one) will be the location where the
				// destination meets the ground.  Before this MassTrace, only the candidateDestination's
				// X/Y values were valid.
				GameWorld.MassTraceLine(traceLines.ToArray(),
										GameWorld.CGWorldFrameHitFlags.HitTestGroundAndStructures,
										out hitResults,
										out hitPoints);

				candidateDestination = hitPoints[0];    // From 'normal', Destination with valid Z coordinate


				// Sanity check...
				// We don't want to be standing right on the edge of a drop-off (say we'e on
				// a plaform or pier).  If there is not solid ground all around us, we reject
				// the candidate.  Our test for validity is that the walking distance must
				// not be more than 20% greater than the straight-line distance to the point.
				int viableVectorCount = hitPoints.Sum(point => ((Me.Location.SurfacePathDistance(point) < (Me.Location.Distance(point) * 1.20))
																	  ? 1
																	  : 0));

				if (viableVectorCount < (CYLINDER_LINE_COUNT + 1))
				{ continue; }

				// If new destination is 'too close' to our current position, try again...
				if (Me.Location.Distance(candidateDestination) <= SAFE_DISTANCE_BUFFER)
				{ continue; }

				break;
			}

			// If we exhausted our tries, just go with simple destination --
			if (tryCount <= 0)
			{ candidateDestination = location; }

			return (candidateDestination);
		}


		public static double SurfacePathDistance(this WoWPoint start,
													WoWPoint destination)
		{
			WoWPoint[] groundPath = Navigator.GeneratePath(start, destination) ?? new WoWPoint[0];

			// We define an invalid path to be of 'infinite' length
			if (groundPath.Length <= 0)
			{ return (double.MaxValue); }


			double pathDistance = start.Distance(groundPath[0]);

			for (int i = 0; i < (groundPath.Length - 1); ++i)
			{ pathDistance += groundPath[i].Distance(groundPath[i + 1]); }

			return (pathDistance);
		}


		// Returns WoWPoint.Empty if unable to locate water's surface
		public static WoWPoint WaterSurface(this WoWPoint location)
		{
			WoWPoint hitLocation;
			bool hitResult;
			WoWPoint locationUpper = location.Add(0.0, 0.0, 2000.0);
			WoWPoint locationLower = location.Add(0.0, 0.0, -2000.0);

			hitResult = (GameWorld.TraceLine(locationUpper,
											 locationLower,
											 GameWorld.CGWorldFrameHitFlags.HitTestLiquid,
											 out hitLocation)
						 || GameWorld.TraceLine(locationUpper,
												locationLower,
												GameWorld.CGWorldFrameHitFlags.HitTestLiquid2,
												out hitLocation));

			return (hitResult ? hitLocation : WoWPoint.Empty);
		}
	}
	#endregion
}

