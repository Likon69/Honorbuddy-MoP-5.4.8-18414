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
// 1) Kills the four mobs in the order they are presented:
//    Gorebite, Khaman & Thartep, Caimas.
// 2) Kites Caimas around the Croc Eggs to kill him.
// 3) If toon gets snagged by Young Crocs, it will jump up
//    and down to get rid of them.
// 3) Profit!
//
// THINGS TO KNOW:
// * Pets are properly managed in conducting this quest.
// * We will not visit Croc Eggs that have mobs around them.
// * If competing players are in the area, the behavior moves the
//   toon a safe distance away from the event area, then waits
//   for the competition to leave.
//
// EXAMPLE:
//     <CustomBehavior File="SpecificQuests\27738-Uldum-ThePitOfScales" />
#endregion


//#define DEBUG_THEPITOFSCALES


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


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.ThePitOfScales
{
	[CustomBehaviorFileName(@"SpecificQuests\27738-Uldum-ThePitOfScales")]
	public class ThePitOfScales : CustomForcedBehavior
	{
		#region Consructor and Argument Processing
		public ThePitOfScales(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				QuestId = 27738; // http://wowhead.com/quest=27738
				QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = QuestInLogRequirement.InLog;

				AuraId_TahetImprisoned = 101422; // http://wowhead.com/spell=101422
				AuraId_TinyTeeth = 86569; // http://wowhead.com/spell=86569
				MobId_CaimasThePitMaster = 46276; // http://wowhead.com/npc=46276
				MobId_Gorebite = 46278; // http://wowhead.com/npc=46278
				MobId_Khamen = 46281; // http://wowhead.com/npc=46281
				MobId_Tahet = 46496; // http://wowhead.com/npc=46496
				MobId_Thartep = 46280; // http://wowhead.com/npc=46280
				MobIds_YoungCrocolisk = new int[]
				{
					46279, // http://wowhead.com/npc=46279
					46477, // http://wowhead.com/npc=46477
				};
				ObjectId_CrocEggs = 206112;

				// Tunables...
				BattlefieldCenterPoint = new WoWPoint(-11450.08, -1183.458, -2.641859);
				BattlefieldRadius = 50.0;
				PoolRadius = 13.0;
				
				// BattlefieldChillPoint is where we go sit while waiting for competition to clear...
				BattlefieldWaitingArea = FanOutRandom(new WoWPoint(-11456.44, -1195.402, -2.641717), 10.0);
				CombatMaxEngagementRangeDistance = 23.0;        
				CrocEggAvoidanceDistance = 6.0;

				// Semantic coherency / covariant dependency checks --
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
		private WoWPoint BattlefieldCenterPoint { get; set; }
		private WoWPoint BattlefieldWaitingArea { get; set; }
		private double BattlefieldRadius { get; set; }
		private double CombatMaxEngagementRangeDistance { get; set; }
		private double CrocEggAvoidanceDistance { get; set; }
		private double PoolRadius { get; set; }

		private int AuraId_TahetImprisoned { get; set; }
		private int AuraId_TinyTeeth { get; set; }
		private int MobId_CaimasThePitMaster { get; set; }
		private int MobId_Gorebite { get; set; }
		private int MobId_Khamen { get; set; }
		private int MobId_Tahet { get; set; }
		private int MobId_Thartep { get; set; }
		private int[] MobIds_YoungCrocolisk { get; set; }
		private int ObjectId_CrocEggs { get; set; }

		private int QuestId { get; set; }
		private int QuestObjectiveIndex { get; set; }
		private QuestCompleteRequirement QuestRequirementComplete { get; set; }
		private QuestInLogRequirement QuestRequirementInLog { get; set; }


		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return "$Id$"; } }
		public override string SubversionRevision { get { return "$Rev$"; } }
		#endregion


		#region Private and Convenience variables
		public delegate string StringDelegate(object context);
		public delegate WoWUnit WoWUnitDelegate(object context);

		private static List<WoWUnit> Boss_Caimas
		{
			get
			{
				return
					ObjectManager.GetObjectsOfType<WoWUnit>()
					.Where(u =>
						u.IsValid
						&& (u.Entry == 46276)
						&& u.IsAlive
						&& u.HasAura(86595))
					.OrderBy(u => u.DistanceSqr)
					.ToList();
			}
		}
		private bool IsShakingYoungCrocolisksOff { get; set; }
		private LocalPlayer Me { get { return StyxWoW.Me; } }
		private WoWGameObject PreferredCrocEgg { get; set; }
		private IEnumerable<int> PreferredMobIds
		{
			get
			{
				return _preferredMobIds ?? (_preferredMobIds = new List<int>()
				{
					MobId_Gorebite,
					MobId_Khamen,
					MobId_Thartep,
					MobId_CaimasThePitMaster,
				});
			}
		}

		private static List<WoWUnit> Ready_Tahet
		{
			get
			{
				return
					ObjectManager.GetObjectsOfType<WoWUnit>()
					.Where(u =>
						u.IsValid
						&& (u.Entry == 46496)
						&& !u.HasAura(101422))
					.OrderBy(u => u.DistanceSqr)
					.ToList();
			}
		}

		private WoWUnit SelectedTarget { get; set; }

		private Composite _behaviorTreeHook_CombatMain = null;
		private Composite _behaviorTreeHook_CombatOnly = null;
		private Composite _behaviorTreeHook_DeathMain = null;
		private Composite _behaviorTreeHook_Main = null;
		private ConfigMemento _configMemento = null;
		private bool _isBehaviorDone = false;
		private IEnumerable<int> _preferredMobIds = null;
		private static Random _random = new Random((int)DateTime.Now.Ticks);
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

            if (_behaviorTreeHook_CombatMain != null)
            {
                TreeHooks.Instance.RemoveHook("Combat_Main", _behaviorTreeHook_CombatMain);
                _behaviorTreeHook_CombatMain = null;
            }

            if (_behaviorTreeHook_CombatOnly != null)
            {
                TreeHooks.Instance.RemoveHook("Combat_Only", _behaviorTreeHook_CombatOnly);
                _behaviorTreeHook_CombatOnly = null;
            }

            if (_behaviorTreeHook_DeathMain != null)
            {
                TreeHooks.Instance.RemoveHook("Death_Main", _behaviorTreeHook_DeathMain);
                _behaviorTreeHook_DeathMain = null;
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
				CharacterSettings.Instance.UseMount = false;
				CharacterSettings.Instance.PullDistance = 1;    // don't pull anything unless we absolutely must

				_behaviorTreeHook_CombatMain = CreateBehavior_CombatMain();
				TreeHooks.Instance.InsertHook("Combat_Main", 0, _behaviorTreeHook_CombatMain);
				_behaviorTreeHook_CombatOnly = CreateBehavior_CombatOnly();
				TreeHooks.Instance.InsertHook("Combat_Only", 0, _behaviorTreeHook_CombatOnly);
				_behaviorTreeHook_DeathMain = CreateBehavior_DeathMain();
				TreeHooks.Instance.InsertHook("Death_Main", 0, _behaviorTreeHook_DeathMain);

				this.UpdateGoalText(QuestId);
			}
		}
		#endregion


		#region Main Behaviors
		private Composite CreateBehavior_CombatMain()
		{
			return new PrioritySelector(
				// empty for now
				);
		}


		private Composite CreateBehavior_CombatOnly()
		{
			return new PrioritySelector(
				// If Young Crocolisks are attacking, jump up and down...
				UtilityBehavior_HandleYoungCrocolisks(),

				// Select target to attack (dynamically prioritize multi-target situations)...
				new Action(context =>
				{
					SelectedTarget = ChooseAttackingTarget();
					return RunStatus.Failure; // fall thru
				}),

				// If our target still lives, figure out how to deal with it...
				new Decorator(context => IsViable(SelectedTarget),
					new PrioritySelector(
						new Decorator(context => SelectedTarget != Me.CurrentTarget,
							new Action(context =>
							{
								if (SelectedTarget.Entry == MobId_CaimasThePitMaster)
									{ BotPoi.Clear(); }
								else
									{ BotPoi.Current = new BotPoi(SelectedTarget, PoiType.Kill); }

								SelectedTarget.Target();
							})),

						// Fighting Caimas...
						new Decorator(context => SelectedTarget.Entry == MobId_CaimasThePitMaster,
							new PrioritySelector(
								// Make certain pet is always following us, we don't want mob aggroing on it...
								PetBehavior_SetStance(context => "Passive"),
								PetBehavior_ActionFollow(),
								UtilityBehavior_GetMobsAttention(context => SelectedTarget),
								new Action(context =>
								{
									CombatWith.Disallow(SelectedTarget);

									double safetyDistance = SelectedTarget.CombatReach * 3;

									// Find new Croc Eggs, if current is no longer valid...
									if (!IsViable(PreferredCrocEgg))
										{ PreferredCrocEgg = FindPreferredCrocEgg(); }

									// If Croc Eggs found, use it by positioning it between mob and self...
									if (IsCrocEggViable(PreferredCrocEgg))
									{
										float avoidDistance = (float)(CrocEggAvoidanceDistance +2.0f);
										float heading = WoWMathHelper.CalculateNeededFacing(SelectedTarget.Location, PreferredCrocEgg.Location);
										WoWPoint destination = PreferredCrocEgg.Location.RayCast(heading, avoidDistance);

										if (!Navigator.AtLocation(destination))
										{
											MoveWithinDangerousArea(destination);
											return RunStatus.Success;
										}

										if (Me.IsMoving)
											{ WoWMovement.MoveStop(); }

										if (!Me.IsFacing(SelectedTarget))
											{ SelectedTarget.Face(); }

										if (SelectedTarget.Distance < safetyDistance)
										{
											QBCLog.Warning("Blacklisting current Croc Egg selection");
											Blacklist.Add(PreferredCrocEgg, BlacklistFlags.Combat, TimeSpan.FromSeconds(60));
										}
									}

									return RunStatus.Success;
								})
							)),

						// If combat not allowed, prevent Combat Routine from running...
						new Decorator(context => !CombatWith.IsAllowed(Me.CurrentTarget),
							new ActionAlwaysSucceed()),

						// Fighting anything else...
						// Let pet have some fun...
						PetBehavior_SetStance(context => "Defensive"),
						PetBehavior_ActionAttack(context => SelectedTarget)
					))
				);
		}


		private Composite CreateBehavior_DeathMain()
		{
			return new PrioritySelector(
					// If Young Crocolisks were attacking, stop jumping up and down...
					// NB: If we started jumping during combat, we need to turn it off
					// if we die.  This is why we need to call this here.
					UtilityBehavior_HandleYoungCrocolisks()
				);
		}


		private Composite CreateMainBehavior()
		{
			return new Decorator(context => !(Me.Combat || (Me.GotAlivePet && Me.Pet.Combat)),
				new PrioritySelector(
					// If quest is done, behavior is done...
					new Decorator(context => IsDone,
						new Action(context =>
						{
							_isBehaviorDone = true;
							QBCLog.Info("Finished");
						})),

					new Mount.ActionLandAndDismount(),

					// If Young Crocolisks are attacking, jump up and down...
					// NB: If we started jumping during combat, we might need to turn it off
					// after combat completes.  This is why we need to call this here.
					UtilityBehavior_HandleYoungCrocolisks(),

					// If we've a trash mob targeting us, go deal with it...
					// NB: If a mob targets us, we proactively deal with it,
					// rather than waiting for it to hit us (which puts us in combat, then
					// deal with it).  This keeps us from dragging a bunch of hostiles to
					// our destination, then having to fight them there.
					new Decorator(context => (SelectedTarget = ChooseAttackingTarget()) != null,
						UtilityBehavior_SpankMob(context => SelectedTarget)),

					// If we have competition, go to waiting area until other players clear area...
					new Decorator(context => (!Me.Combat && (Ready_Tahet == null)) || (!Me.Combat && FindCompetingPlayers().Count() > 0 && Ready_Tahet.Count > 0),
						new PrioritySelector(
							new Decorator(context => !Navigator.AtLocation(BattlefieldWaitingArea),
								new Action(context =>
								{
									QBCLog.Info("Moving to waiting area while competing players are doing quest");
									MoveWithinDangerousArea(BattlefieldWaitingArea);
								}
							)),
							new Decorator(context => Boss_Caimas.Count > 0 && Boss_Caimas[0].Location.Distance(Me.Location) <= 25,
								new Action(context =>
								{
									QBCLog.Info("Atacking Boss to get credit and complete quest");
									var spell = "";
									switch (Me.Class)
									{
										case WoWClass.Mage:
											spell = "Ice Lance";
											break;
										case WoWClass.Druid:
											spell = "Moonfire";
											break;
										case WoWClass.Paladin:
											spell = "Judgment";
											break;
										case WoWClass.Priest:
											spell = "Shadow Word: Pain";
											break;
										case WoWClass.Shaman:
											spell = "Flame Shock";
											break;
										case WoWClass.Warlock:
											spell = "Corruption";
											break;
										case WoWClass.DeathKnight:
											spell = "Dark Command";
											break;
										case WoWClass.Hunter:
											spell = "Arcane Shot";
											break;
										case WoWClass.Warrior:
											if (SpellManager.CanCast("Shoot"))
												spell = "Shoot";
											if (SpellManager.CanCast("Throw"))
												spell = "Throw";
											break;
										case WoWClass.Rogue:
											if (SpellManager.CanCast("Shoot"))
												spell = "Shoot";
											if (SpellManager.CanCast("Throw"))
												spell = "Throw";
											break;
										case WoWClass.Monk:
											spell = "Provoke";
											break;
									}
									WoWMovement.MoveStop();
									Boss_Caimas[0].Target();
									Boss_Caimas[0].Face();
									SpellManager.Cast(spell);
								}
							)),
							//new Wait(TimeSpan.FromSeconds(30), context => false, new ActionAlwaysSucceed()),

							new ActionAlwaysSucceed()
						)),
					
					// Find a target, and pull it...
					new PrioritySelector(context => SelectedTarget = ChoosePullTarget(),
						new Decorator(context => IsViable(SelectedTarget),
							UtilityBehavior_GetMobsAttention(context => SelectedTarget))
							),

					// Go chat with Tahet to start the event, if needed...
					new PrioritySelector(tahetUnitContext => FindUnitsFromIds(MobId_Tahet).FirstOrDefault(),
						new Decorator(tahetUnitContext => (tahetUnitContext != null)
															&& ((WoWUnit)tahetUnitContext).HasAura(AuraId_TahetImprisoned),
							new PrioritySelector(
								new Action(tahetUnitContext => {
									QBCLog.Info("Moving to Tahet to start event"); return RunStatus.Failure; }),
								new Decorator(tahetUnitContext => ((WoWUnit)tahetUnitContext).Distance > ((WoWUnit)tahetUnitContext).InteractRange,
									new Action(tahetUnitContext => { MoveWithinDangerousArea(((WoWUnit)tahetUnitContext).Location); })),
								new Decorator(tahetUnitContext => Me.IsMoving,
									new Action(tahetUnitContext => { WoWMovement.MoveStop(); })),
								new Decorator(tahetUnitContext => !Me.IsFacing((WoWUnit)tahetUnitContext),
									new Action(tahetUnitContext => { ((WoWUnit)tahetUnitContext).Face(); })),
								new Decorator(tahetUnitContext => ((WoWUnit)tahetUnitContext).HasAura(AuraId_TahetImprisoned),
									new Action(tahetUnitContext => { ((WoWUnit)tahetUnitContext).Interact(); }))
							)),
						new Decorator(tahetUnitContext => tahetUnitContext == null,
							new Action(tahetUnitContext => { QBCLog.Info("Waiting for Tahet to respawn"); }))
						),

					// If we're outside the battlefield, move back to anchor...
					new Decorator(context => Me.Location.Distance(BattlefieldCenterPoint) > BattlefieldRadius,
						new Action(context =>
						{
							QBCLog.Info("Moving back to center of Battlefield");
							MoveWithinDangerousArea(BattlefieldCenterPoint);
						}))
				));
		}
		#endregion


		#region Helpers
		private WoWUnit ChooseAttackingTarget()
		{
			WoWUnit Caimas = FindUnitsFromIds(MobId_CaimasThePitMaster).FirstOrDefault();

			IEnumerable<WoWUnit> targets =
				from unit in ObjectManager.GetObjectsOfType<WoWUnit>()
				where
					IsViable(unit)
					&& !unit.IsFriendly
					&& !MobIds_YoungCrocolisk.Contains((int)unit.Entry)   // Young Crocs handled specially
					&& unit.IsTargetingMeOrPet
				orderby
					unit.Distance
					// If we're fighting Caimas, he is our preferred focus...
					+ (((Caimas != null) && (unit == Caimas)) ? -10000.0 : 0.0)
					// Since killing a preferred mob advances the event, we kill
					// off trash first, before dealing with preferred mob...
					+ (PreferredMobIds.Contains((int)unit.Entry) ? 1000.0 : 0.0)
				select unit;

			return targets.FirstOrDefault();
		}


		private WoWUnit ChoosePullTarget()
		{
			IEnumerable<WoWUnit> targets =
				from unit in ObjectManager.GetObjectsOfType<WoWUnit>()
				where
					IsViable(unit)
					&& PreferredMobIds.Contains((int)unit.Entry)
					&& unit.Location.Distance(BattlefieldCenterPoint) <= PoolRadius
				select unit;

			return targets.FirstOrDefault();
		}


		private IEnumerable<WoWPlayer> FindCompetingPlayers()
		{
			float maxZDeltaAllowed = BattlefieldCenterPoint.Z + 8.0f;

			return
				from player in ObjectManager.GetObjectsOfType<WoWPlayer>()
				where
					(BattlefieldCenterPoint.Distance(player.Location) <= (BattlefieldRadius * 2.0))
					&& player.Location.Z < maxZDeltaAllowed
				select player;
		}


		private IEnumerable<DefinedArea> FindDangerousAreas()
		{
			IEnumerable<DefinedArea> dangerousAreas =
				from crocEgg in ObjectManager.GetObjectsOfType<WoWGameObject>()
				where crocEgg.Entry == ObjectId_CrocEggs
				select new DefinedArea_Circle(this, crocEgg.Location, CrocEggAvoidanceDistance);

			return dangerousAreas;
		}


		private WoWGameObject FindPreferredCrocEgg()
		{
			IEnumerable<WoWGameObject> crocEggPreferences =
			   (from crocEgg in ObjectManager.GetObjectsOfType<WoWGameObject>()
				where
					(crocEgg.Entry == ObjectId_CrocEggs)
					&& IsCrocEggViable(crocEgg)
					// Make certain we stay in main area...
					&& (BattlefieldCenterPoint.Distance(crocEgg.Location) <= BattlefieldRadius)
				let dangerScore = (new DefinedArea_Circle(this, crocEgg.Location, CrocEggAvoidanceDistance +1.0)).DangerScore()
				orderby
					((0.5 * crocEgg.Distance) + (0.5 * dangerScore))
					//+ (WoWMathHelper.IsInPath(SelectedTarget, Me.Location, crocEgg.Location) ? 100.0 : 0.0) // TODO
				select crocEgg);

			return crocEggPreferences.FirstOrDefault();
		}


		private IEnumerable<WoWUnit> FindUnitsFromIds(params int[] unitIds)
		{
			if (unitIds == null)
			{
				string message = "unitIds argument may not be null";

				QBCLog.MaintenanceError(message);
				throw new ArgumentException(message);
			}

			return
				from unit in ObjectManager.GetObjectsOfType<WoWUnit>()
				where
					unit.IsValid
					&& unit.IsAlive
					&& unitIds.Contains((int)unit.Entry)
					&& (unit.TappedByAllThreatLists || !unit.TaggedByOther)
				select unit;
		}


		private bool IsQuestObjectiveComplete(int questId, int objectiveId)
		{
			if (Me.QuestLog.GetQuestById((uint)questId) == null)
				{ return false; }

			int questLogIndex = Lua.GetReturnVal<int>(string.Format("return GetQuestLogIndexByID({0})", questId), 0);

			return
				Lua.GetReturnVal<bool>(string.Format("return GetQuestLogLeaderBoard({0},{1})", objectiveId, questLogIndex), 2);
		}


		private bool IsCrocEggViable(WoWGameObject crocEgg)
		{
			IEnumerable<WoWUnit> hostileMobsAround =
				from unit in ObjectManager.GetObjectsOfType<WoWUnit>()
				where
					IsViable(unit)
					&& unit.IsHostile
					&& (unit.Entry != MobId_CaimasThePitMaster)
					&& (unit.Location.Distance(crocEgg.Location) < 28.0)
				select unit;

			return IsViable(crocEgg)
				&& (hostileMobsAround.Count() <= 0);
		}


		private bool IsViable(WoWGameObject wowGameObject)
		{
			return
				(wowGameObject != null)
				&& wowGameObject.IsValid
				&& !Blacklist.Contains(wowGameObject.Guid, BlacklistFlags.Combat);
		}


		private bool IsViable(WoWUnit wowUnit)
		{
			return
				(wowUnit != null)
				&& wowUnit.IsValid
				&& wowUnit.IsAlive
				&& !Blacklist.Contains(wowUnit.Guid, BlacklistFlags.Combat);
		}


		private MoveResult MoveWithinDangerousArea(WoWPoint destination)
		{
			DefinedArea nearestDangerousArea =
			   (from area in FindDangerousAreas()
				where area.WillPathThroughArea(Me.Location, destination)
				orderby area.Distance(Me.Location)
				select area)
				.FirstOrDefault();

			if (nearestDangerousArea != null)
				{ destination = nearestDangerousArea.FindNavigationPoint(StyxWoW.Me.Location, destination); }

			return Navigator.MoveTo(destination);
		}
		#endregion


		#region Utility Behaviors
		/// <summary>
		/// This behavior quits attacking the mob, once the mob is targeting us.
		/// </summary>
		private Composite UtilityBehavior_GetMobsAttention(WoWUnitDelegate selectedTargetDelegate)
		{

			return new PrioritySelector(targetContext => selectedTargetDelegate(targetContext),
				new Decorator(targetContext => IsViable((WoWUnit)targetContext),
					new PrioritySelector(
						new Decorator(targetContext => !((((WoWUnit)targetContext).CurrentTarget == Me)
														|| (Me.GotAlivePet && ((WoWUnit)targetContext).CurrentTarget == Me.Pet)),
							new PrioritySelector(
								new Action(targetContext =>
								{
                                    QBCLog.Info("Getting attention of {0}", ((WoWUnit)targetContext).SafeName);
									return RunStatus.Failure;
								}),
								UtilityBehavior_SpankMob(selectedTargetDelegate)))
					)));
		}


	   private Composite UtilityBehavior_HandleYoungCrocolisks()
		{
			return new PrioritySelector(isBeingAttackedContext => Me.HasAura(AuraId_TinyTeeth),
				new Decorator(isBeingAttackedContext => ((bool)isBeingAttackedContext),
					new Action(isBeingAttackedContext =>
					{
						QBCLog.Info("Shaking off Young Crocolisks");
						WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
						IsShakingYoungCrocolisksOff = true;
						return RunStatus.Failure; // let other actions continue while we deal with the situation
					})),

				new Decorator(isBeingAttackedContext => !((bool)isBeingAttackedContext) && IsShakingYoungCrocolisksOff,
					new Action(isBeingAttackedContext =>
					{
						QBCLog.Info("Young Crocolisks delt with");
						WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend);
						IsShakingYoungCrocolisksOff = false;
						return RunStatus.Failure; // let other actions continue
					}))
				);      
		}


		/// <summary>
		/// Unequivocally engages mob in combat.
		/// </summary>
		private Composite UtilityBehavior_SpankMob(WoWUnitDelegate selectedTargetDelegate)
		{
			return new PrioritySelector(targetContext => selectedTargetDelegate(targetContext),
				new Decorator(targetContext => IsViable((WoWUnit)targetContext),
					new PrioritySelector(               
						new Action(targetContext =>
						{
							CombatWith.Allow((WoWUnit)targetContext);
							return RunStatus.Failure; // fall through
						}),
						new Decorator(targetContext => ((WoWUnit)targetContext).Distance > CombatMaxEngagementRangeDistance,
							new Action(targetContext => { MoveWithinDangerousArea(((WoWUnit)targetContext).Location); })),
						new Decorator(targetContext => Me.CurrentTarget != (WoWUnit)targetContext,
							new Action(targetContext =>
							{
								BotPoi.Current = new BotPoi((WoWUnit)targetContext, PoiType.Kill);
								((WoWUnit)targetContext).Target();
							})),
						new Decorator(targetContext => !((WoWUnit)targetContext).IsTargetingMeOrPet,
							new PrioritySelector(
								new Decorator(targetContext => RoutineManager.Current.CombatBehavior != null,
									RoutineManager.Current.CombatBehavior),
								new Action(targetContext => { RoutineManager.Current.Combat(); })
							))
					)));
		}
		#endregion // Behavior helpers


		#region Utility: DefinedArea
		public interface DefinedArea
		{
			double DangerScore();
			double Distance(WoWPoint referencePoint);
			WoWPoint FindNavigationPoint(WoWPoint currentLocation, WoWPoint destination);
			bool IsInsideArea(WoWPoint currentLocation);
			bool WillPathThroughArea(WoWPoint start, WoWPoint destination);
		}

		private class DefinedArea_Circle : DefinedArea
		{
			public DefinedArea_Circle(ThePitOfScales behavior, WoWPoint center, double radius)
			{
				_behavior = behavior;
				CenterPoint = center;
				Radius = radius;

				_navPointsCounterclockwise = FindNavPointsCounterclockwise();
			}

			public WoWPoint CenterPoint { get; private set; }
			public double Radius { get; private set; }


			// NB: "Nav points" are outside the radius by NavPointOffset distance.
			// This provides a buffer, and hysteresis that prevents the toon from getting
			// into trouble by entering the actual dangerous area, or vascillating about where to go.
			private const double NavPointOffset = 1.0; //yards
			private const int NavPointCount = 16;

			private ThePitOfScales _behavior;
			private IList<Tuple<WoWPoint, double>> _navPointsCounterclockwise;


			public double DangerScore()
			{
				return _navPointsCounterclockwise.Sum(t => t.Item2);
			}


			public double Distance(WoWPoint referencePoint)
			{
				return CenterPoint.Distance(referencePoint);
			}


			public WoWPoint FindNavigationPoint(WoWPoint currentLocation, WoWPoint destination)
			{
				// If toon has managed to get inside the area, force him out the quickest way possible...
				if (IsInsideArea(currentLocation))
				{
					QBCLog.Warning("Immediately moving out of dangerous (Croc Egg) area");

					WoWPoint nearestSafeNavPoint =
					   (from tuple in _navPointsCounterclockwise
						let point = tuple.Item1
						let traversalCost = tuple.Item2
						let facingCost =
							Math.Abs(StyxWoW.Me.RenderFacing
							- WoWMathHelper.NormalizeRadian(WoWMathHelper.CalculateNeededFacing(StyxWoW.Me.Location, point)))
						orderby
							StyxWoW.Me.Location.Distance(point) * traversalCost * facingCost
						select point)
						.FirstOrDefault();

					double neededHeading = WoWMathHelper.CalculateNeededFacing(CenterPoint, nearestSafeNavPoint);
					return nearestSafeNavPoint.RayCast((float)neededHeading, (float)(Radius + NavPointOffset));
				}

				// If destination point is inside the area, substitute closest viable navigation point...
				if (CenterPoint.Distance(destination) <= Radius)
				{
					IEnumerable<WoWPoint> newLocalDestinations =
						from tuple in _navPointsCounterclockwise
						let point = tuple.Item1
						let traversalCost = tuple.Item2
						where !WillPathThroughArea(point, destination)
						orderby destination.Distance(point) * traversalCost
						select point;

					QBCLog.Warning("Altering destination outside of the danger area");
					destination = newLocalDestinations.FirstOrDefault();
				}

				// If toon not headed through area, send him to his destination unimpeded...
				if (!WillPathThroughArea(currentLocation, destination))
					{ return destination; }

				// If toon is too far away from the area to warrant navigational help,
				// send him to his destination unimpeded...
				if (currentLocation.Distance(CenterPoint) > (2 * Radius + (3 * Navigator.PathPrecision)))
					{ return destination; }

				// NB: The algorithm must consider solutions involving local-minima.
				// For instance, one quarter of the circle may be non-navigable due to obstructions,
				// and we must send the toon around 3/4ths of the circle to stay out of the area.
				Tuple<WoWPoint, double> traversalClockwise = FindPathToDestination(_navPointsCounterclockwise, currentLocation, destination, false);
				Tuple<WoWPoint, double> traversalCounterclockwise = FindPathToDestination(_navPointsCounterclockwise, currentLocation, destination, true);

				return (traversalClockwise.Item2 < traversalCounterclockwise.Item2)  // compare path costs
					? traversalClockwise.Item1
					: traversalCounterclockwise.Item1;
			}


			public bool IsInsideArea(WoWPoint location)
			{
				return location.Distance(CenterPoint) <= Radius;
			}


			public bool WillPathThroughArea(WoWPoint start, WoWPoint destination)
			{
				WoWPoint[] path = Navigator.GeneratePath(start, destination);

				int pathSegmentCount = path.Count() -1;

				for (int i = 0; i < pathSegmentCount;  ++i)
				{
					if (WillTraverseThroughArea(path[i], path[i+1]))
						{ return true; }
				}
				return false;
			}


			private double CalculatePathCost(
				IList<Tuple<WoWPoint, double>> pathPoints,
				WoWPoint entryNavPoint,
				IEnumerable<WoWPoint> exitNavPoints,
				bool doCounterClockwise)
			{
				// Calculate traveral cost to use this path...
				// NB: When we hit the end of the pathPoints, we continue by wrapping around back to the beginning.
				// This is needed when the exit point preceeds the entry point.
				bool entryPointSeen = false;
				int pathPointCount = pathPoints.Count();
				double pathTraversalCost = 0.0;

				for (int i = 0;  true;  i = (doCounterClockwise
											 ? ++i % pathPointCount
											 : (--i + pathPointCount) % pathPointCount))
				{
					if (pathPoints[i].Item1 == entryNavPoint)
						{ entryPointSeen = true; }

					if (entryPointSeen)
					{
						pathTraversalCost += pathPoints[i].Item2;

						if (exitNavPoints.Contains(pathPoints[i].Item1))
							{ break; }
					}
				}

				return pathTraversalCost;
			}


			private IList<Tuple<WoWPoint, double>> FindNavPointsCounterclockwise()
			{
				// NB: We use a slightly elevated center point for the traceline to address issues
				// of uneven ground et al.  The magic number of 2.0 was selected, because the tallest
				// toons are slightly less than 3.0.
				WoWPoint centerPointForTraceLine = CenterPoint.Add(0.0f, 0.0f, 2.0f);
				WoWPoint[] potentialNavPoints = CreateCircleXY(CenterPoint, Radius +NavPointOffset, NavPointCount);
				WorldLine[] potentialNavPointsAsLines =
					potentialNavPoints
					.Select(p => new WorldLine(centerPointForTraceLine, p))
					.ToArray();

				bool[] hitResults;
				WoWPoint[] hitPoints;
				GameWorld.MassTraceLine(
					potentialNavPointsAsLines,
					GameWorld.CGWorldFrameHitFlags.HitTestGroundAndStructures,
					out hitResults,
					out hitPoints);

				var tmpList = new List<Tuple<WoWPoint, double>>();
				for (int i = 0; i < NavPointCount; ++i)
				{
					double traversalCost =
					   (hitResults[i]
						? (centerPointForTraceLine.DistanceSqr(potentialNavPointsAsLines[i].End) / centerPointForTraceLine.DistanceSqr(hitPoints[i]))
						: 1.0);
					traversalCost = traversalCost * traversalCost;

					if (hitResults[i])
					{
						float preservedZ = potentialNavPoints[i].Z;
						potentialNavPoints[i] = hitPoints[i];
						if (!Navigator.CanNavigateFully(CenterPoint, hitPoints[i]))
							{ potentialNavPoints[i].Z = preservedZ; }
					}

					tmpList.Add(Tuple.Create(potentialNavPoints[i], traversalCost));
				}

				return (tmpList);
			}


			private Tuple<WoWPoint, double> FindPathToDestination(
				IList<Tuple<WoWPoint, double>> pathPoints,
				WoWPoint currentLocation,
				WoWPoint destination,
				bool doCounterClockwise)
			{
				// The "Exit" nav points are those that allow unimpeded progress
				// to the destination.
				IEnumerable<WoWPoint> exitNavPoints =
				   (from tuple in pathPoints
					where !WillPathThroughArea(tuple.Item1, destination)
					select tuple.Item1)
					.ToList();

				if (exitNavPoints.Count() == 0)
				{
					QBCLog.Warning("Unable to locate navigational 'exit point'--heading through area");
					return Tuple.Create(destination, double.MaxValue);
				}

				// The "Entry" point is the location around the area that we approach
				// to start our path to the "Exit" point.
				Tuple<WoWPoint, double> entryNavTuple =
				   (from tuple in pathPoints
					let entryPoint = tuple.Item1
					let traversalCost = tuple.Item2
					where
						!WillPathThroughArea(currentLocation, entryPoint)
						&& !Navigator.AtLocation(entryPoint)
					let pathTraversalCost = CalculatePathCost(pathPoints, entryPoint, exitNavPoints, doCounterClockwise)
					orderby
						pathTraversalCost
					select Tuple.Create(entryPoint, pathTraversalCost))
					.DefaultIfEmpty(Tuple.Create(WoWPoint.Empty, double.MaxValue))
					.FirstOrDefault();

				if (entryNavTuple.Item1 == WoWPoint.Empty)
				{
					QBCLog.Warning("Unable to locate navigational 'entry point'--using nearest point");
					return pathPoints.OrderBy(t => currentLocation.Distance(t.Item1)).FirstOrDefault();
				}

				// If entry point is the same as an exit point, proceed to destination unimpaired...
				if (exitNavPoints.Contains(entryNavTuple.Item1))
					{ return Tuple.Create(destination, 0.0); }
				
				return entryNavTuple;
			}


			public bool WillTraverseThroughArea(WoWPoint start, WoWPoint destination)
			{
				// If we terminate in the area, it "traverses through" by definition...
				// NB: We don't apply this test to origin; otherwise, that would prevent
				// us from leaving the area.
				if (CenterPoint.Distance(destination) < Radius)
					{ return true; }

				// Test if line "passes thru" the area...
				double xDelta = destination.X - start.X;
				double yDelta = destination.Y - start.Y;

				// Start and Destination are same point...
				if ((xDelta == 0.0) && (yDelta == 0.0))
					{ return false; }

				double u = ((CenterPoint.X - start.X) * xDelta + (CenterPoint.Y - start.Y) * yDelta)
							/ ((xDelta * xDelta) + (yDelta * yDelta));

				// If the intersection doesn't occur on the line segment, then won't go through area...
				if ((u < 0.0) || (u > 1.0))
					{ return false; }

				WoWPoint intersectionPoint = start.Add((float)(u * xDelta), (float)(u * yDelta), 0.0f);
				
				return CenterPoint.Distance(intersectionPoint) <= Radius;
			}
		}
		#endregion


		#region Utility: CombatWith
		private class CombatWith
		{
			public static void Allow(WoWUnit wowUnit)
			{
				if (wowUnit != null)
					{ _combatDisallowed.Remove((int)wowUnit.Guid); }
			}

			public static void Disallow(WoWUnit wowUnit)
			{
				// Make certain unit is placed on the list only once...
				if ((wowUnit != null) && !_combatDisallowed.Contains((int)wowUnit.Guid))
					{ _combatDisallowed.Add((int)wowUnit.Guid); }
			}

			public static bool IsAllowed(WoWUnit wowUnit)
			{
				return (wowUnit != null)
					? !_combatDisallowed.Contains((int)wowUnit.Guid)
					: false;
			}

			private static List<int> _combatDisallowed = new List<int>();
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
											&& IsViable(wowUnitDelegate(context))
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
											&& (!IsPetActionActive(spellName) || IsViable(Me.Pet.CurrentTarget)),
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


		#region Chinajade's WoWPoint Extensions: Basic
		public const double TAU = (2 * Math.PI);

		/// <summary>
		/// <para>Adds a 2D polar quantity to a WOWPOINT, yielding a new WoWPoint
		/// in the same X-Y plane as the original.  The polar quantity is composed
		/// of the XYHEADINGINRADIANS horizontal
		/// angle, and the DISTANCE.  The new point will be vertically offset by
		/// ZOFFSET before being returned.</para>
		/// </summary>
		/// <param name="wowPoint"></param>
		/// <param name="xyHeadingInRadians">horzontal angle in radians. Positive and
		/// negative angles are permitted, and are not constrained to the interval of +-TAU
		/// (see http://tauday.com/).</param>
		/// <param name="distance">postive and negative values are permitted.</param>
		/// <param name="zOffset">vertical offset to apply to the new
		/// WoWPoint before it is returned.</param>
		/// <returns>new WoWPoint</returns>
		public static WoWPoint          AddPolarXY(WoWPoint wowPoint,
												   double xyHeadingInRadians,
												   double distance,
												   double zOffset = 0.0)
		{
			return (wowPoint.Add((float)(Math.Cos(xyHeadingInRadians) * distance),
								 (float)(Math.Sin(xyHeadingInRadians) * distance),
								 (float)zOffset));
		}
		

		/// <summary>
		/// <para>Creates a circle of POINTCOUNT points with RADIUS in the same X-Y plane
		/// as WOWPOINT.
		/// The circle is then vertically offset by ZOFFSET before being returned.
		/// The returned points are oriented in a 'counter-clockwise' direction.</para>
		/// </summary>
		/// <param name="wowPoint"></param>
		/// <param name="radius">on the partially closed interval (0.0..double.MaxValue]</param>
		/// <param name="pointCount">number of points used to define the circle's perimeter.
		/// This value must be on the closed interval [1..int.MaxValue].</param>
		/// <param name="zOffset">vertical distance to offset the circle</param>
		/// <returns>a set of points lying on the perimeter of a circle.</returns>
		public static WoWPoint[]        CreateCircleXY(WoWPoint wowPoint,
													   double radius,
													   int pointCount,
													   double zOffset = 0.0)
		{
			if (pointCount < 1) { throw (new ArgumentOutOfRangeException("pointCount >= 1")); }
			if (radius <= 0.0) { throw (new ArgumentOutOfRangeException("radius > 0.0")); }

			WoWPoint[]          circle          = new WoWPoint[pointCount];
			int                 perimeterIndex;
			double              turnIncrement   = TAU / pointCount;

			perimeterIndex = -1;
			for (double turnFactor = 0.0;    turnFactor < TAU;    turnFactor += turnIncrement)
				{ circle[++perimeterIndex] = AddPolarXY(wowPoint, turnFactor, radius, zOffset); }         

			return (circle);
		}


		// Finds another point near the destination.  Useful when toon is 'waiting' for something
		// (e.g., boat, mob repops, etc). This allows multiple people running
		// the same profile to not stand on top of each other while waiting for
		// something.
		public static WoWPoint FanOutRandom(WoWPoint location, double maxRadius)
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

				candidateDestination = AddPolarXY(location, (TAU * _random.NextDouble()), (maxRadius * _random.NextDouble()), 0.0);

				// Build set of tracelines that can evaluate the candidate destination --
				// We build a cone of lines with the cone's base at the destination's 'feet',
				// and the cone's point at maxRadius over the destination's 'head'.  We also
				// include the cone 'normal' as the first entry.

				// 'Normal' vector
				index = 0;
				traceLines[index].Start = candidateDestination.Add(0.0f, 0.0f, (float)maxRadius);
				traceLines[index].End = candidateDestination.Add(0.0f, 0.0f, (float)-maxRadius);

				// Cylinder vectors
				for (double turnFraction = 0.0; turnFraction < TAU; turnFraction += (TAU / CYLINDER_LINE_COUNT))
				{
					++index;
					circlePoint = AddPolarXY(candidateDestination, turnFraction, SAFE_DISTANCE_BUFFER, 0.0);
					traceLines[index].Start = circlePoint.Add(0.0f, 0.0f, (float)maxRadius);
					traceLines[index].End = circlePoint.Add(0.0f, 0.0f, (float)-maxRadius);
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
				int viableVectorCount = hitPoints.Sum(point => ((SurfacePathDistance(StyxWoW.Me.Location, point) < (StyxWoW.Me.Location.Distance(point) * 1.20))
																	  ? 1
																	  : 0));

				if (viableVectorCount < (CYLINDER_LINE_COUNT + 1))
				{ continue; }

				// If new destination is 'too close' to our current position, try again...
				if (StyxWoW.Me.Location.Distance(candidateDestination) <= SAFE_DISTANCE_BUFFER)
				{ continue; }

				break;
			}

			// If we exhausted our tries, just go with simple destination --
			if (tryCount <= 0)
			{ candidateDestination = location; }

			return (candidateDestination);
		}


		public static double SurfacePathDistance(WoWPoint start,
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
		private static int RandomNumber(int min, int max)
		{
			var random = new Random();
			return random.Next(min, max);
		}
	}
	#endregion
}