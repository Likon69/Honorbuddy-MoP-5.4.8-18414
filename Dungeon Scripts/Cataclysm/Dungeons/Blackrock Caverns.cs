using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.Helpers;

using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Tripper.Tools.Math;
using Action = Styx.TreeSharp.Action;

using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Cataclysm
{
	public class BlackrockCaverns : Dungeon
	{
		#region Overrides of Dungeon

		/// <summary>
		///   The Map Id of this dungeon. This is the unique id for dungeons thats used to determine which dungeon, the script belongs to
		/// </summary>
		/// <value> The map identifier. </value>
		public override uint DungeonId { get { return 303; } }
		public override WoWPoint Entrance { get { return new WoWPoint(-7570.482f, -1330.446f, 246.5363f); } }
		public override WoWPoint ExitLocation { get { return new WoWPoint(215.0428f, 1139.498f, 206.3351f); } }

		/// <summary>
		///   IncludeTargetsFilter is used to add units to the targeting list. If you want to include a mob thats usually removed by the default filters, you shall use that.
		/// </summary>
		/// <param name="incomingunits"> Units passed into the method </param>
		/// <param name="outgoingunits"> Units passed to the targeting class </param>
		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			foreach (WoWObject obj in incomingunits)
			{
				// For the Rom'ogg boss fight
				if (obj.Entry == ChainsOfWoeId)
				{
					outgoingunits.Add(obj);
				}
			}
		}

		/// <summary>
		///   RemoveTargetsFilter is used to remove units thats not wanted in target list. Like immune mobs etc.
		/// </summary>
		/// <param name="units"> The incomingunits. </param>
		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				o =>
				{
					var unit = o as WoWUnit;

					if (unit == null)
						return false;

					if (unit.Entry == TwilightZealotId && unit.HasAura("Kneeling in Supplication"))
						return true;

					if (unit.HasAura("Shadow of Obsidius"))
						return true;

					if (unit.Entry == BellowsSLaveId && !unit.Combat)
						return true;

					if (unit.Entry == RomoggBonecrusherId && StyxWoW.Me.IsMelee() && unit.CastingSpellId == SkullcrackerId)
						return true;

					return false;
				});
		}

		/// <summary>
		///   WeighTargetsFilter is used to weight units in the targeting list. If you want to give priority to a certain npc, you should use this method.
		/// </summary>
		/// <param name="units"> </param>
		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var t in units)
			{
				var prioObject = t.Object;

				//We should prio Chains of Woe for Rom'ogg fight
				if (prioObject.Entry == ChainsOfWoeId)
				{
					t.Score += 400;
				}
			}
		}

		#endregion

		#region Encounter Handlers

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(49476, "Finkle Einhorn", Mode = CallBehaviorMode.Proximity, BossRange = 35)]
		public Composite QuestPickupHandler()
		{
			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				new Decorator(
					ctx => !Me.Combat && !ScriptHelpers.WillPullAggroAtLocation(unit.Location) && unit.QuestGiverStatus == QuestGiverStatus.Available,
					ScriptHelpers.CreatePickupQuest(ctx => unit)),
				new Decorator(
					ctx => !Me.Combat && !ScriptHelpers.WillPullAggroAtLocation(unit.Location) && unit.QuestGiverStatus == QuestGiverStatus.TurnIn,
					ScriptHelpers.CreateTurninQuest(ctx => unit)));
		}

		private const uint ChainsOfWoeId = 40447;
		private const int RomoggBonecrusherId = 39665;
		const int SkullcrackerId = 75543;
		private const uint EvolvedTwilightZealotId = 39987;
		private const uint TwilightZealotId = 50284;
		private const uint BellowsSLaveId = 40084;
		private const uint RuntyId = 40015;
		private const uint Conflagration = 39994;
		private const uint QuakeId = 40401;

		private readonly WoWPoint _jumpPointStartLoc = new WoWPoint(550.5932, 926.8296, 169.5558);
		private readonly WoWPoint _jumpPointEndLoc = new WoWPoint(556.8304, 936.9685, 159.3673);
		private const int ToTheChamberOfIncinerationQuestId = 28735;
		private const int WhatIsThisPlaceQuestId = 28737;
		private const int TheTwilightForgeQuestId = 28738;
		private const int DoMyEyesDeceiveMeQuestId = 28740;

		/// <summary>
		///   Using 0 as BossEntry will make that composite the main logic for the dungeon and it will be called in every tick You can only have one main logic for a dungeon The context of the main composite is all units around as List <WoWUnit />
		/// </summary>
		/// <returns> </returns>
		[EncounterHandler(0)]
		public Composite RootLogic()
		{
			const uint lavaDroolId = 76628;
			AddAvoidObject(ctx => true, 5, lavaDroolId);

			WoWUnit pathingZealot1 = null;

			var patByJumpLoc = new WoWPoint(537.9449, 908.9613, 169.5618);
			var pathingZealot1TankLoc = new WoWPoint(537.6758, 908.2151, 169.5618);

			return new PrioritySelector(
				// complete quests 
				new Decorator(ctx => Me.QuestLog.ContainsQuest(ToTheChamberOfIncinerationQuestId) && Me.QuestLog.GetQuestById(ToTheChamberOfIncinerationQuestId).IsCompleted,
					ScriptHelpers.CreateCompletePopupQuest(ToTheChamberOfIncinerationQuestId)),

				new Decorator(ctx => Me.QuestLog.ContainsQuest(WhatIsThisPlaceQuestId) && Me.QuestLog.GetQuestById(WhatIsThisPlaceQuestId).IsCompleted,
					ScriptHelpers.CreateCompletePopupQuest(WhatIsThisPlaceQuestId)),

				new Decorator(ctx => Me.QuestLog.ContainsQuest(TheTwilightForgeQuestId) && Me.QuestLog.GetQuestById(TheTwilightForgeQuestId).IsCompleted,
					ScriptHelpers.CreateCompletePopupQuest(TheTwilightForgeQuestId)),

				new Decorator(ctx => Me.QuestLog.ContainsQuest(DoMyEyesDeceiveMeQuestId) && Me.QuestLog.GetQuestById(DoMyEyesDeceiveMeQuestId).IsCompleted,
					ScriptHelpers.CreateCompletePopupQuest(DoMyEyesDeceiveMeQuestId)),

				// handle jump before 2nd boss.
					new Decorator(
						ctx => StyxWoW.Me.Location.DistanceSqr(_jumpPointStartLoc) <= 45 * 45 && !ScriptHelpers.IsBossAlive("Rom'ogg Bonecrusher"),
						new PrioritySelector(
				// this is the 1st evolved twilight zealot on the top walkway
							ctx => pathingZealot1 = ScriptHelpers.GetUnfriendlyNpsAtLocation(patByJumpLoc, 60, u => u.Entry == EvolvedTwilightZealotId && u.Z > 165).FirstOrDefault(),
				// kill the 1st pathing zealot before jumping.
							ScriptHelpers.CreatePullNpcToLocation(
								ctx => StyxWoW.Me.IsTank() && pathingZealot1 != null,
								ctx => pathingZealot1.Location.DistanceSqr(pathingZealot1TankLoc) <= 40 * 40,
								ctx => pathingZealot1, ctx => pathingZealot1TankLoc, ctx => pathingZealot1TankLoc, 5)))
					);
		}


		/// <summary>
		///   BossEntry is the Entry of the boss unit. (WoWUnit.Entry) BossName is optional. Its there just to make it easier to find which boss that composite belongs to. The context of the encounter composites is the Boss as WoWUnit Mode controls when the behavior is called. The modes are Combat(default), Proximity and CurrentBoss. If using CurrentBoss mode then the behavior is called when is looking for the boss (and durring combat). In this mode boss might not even be in ObjectManager and context can be null.
		/// </summary>
		/// <returns> </returns>
		[EncounterHandler(39665, "Rom'ogg Bonecrusher", Mode = CallBehaviorMode.Proximity, BossRange = 200)]
		public Composite RomoggEncounter()
		{
			WoWUnit boss = null;
			var bossFarPathEndLoc = new WoWPoint(259.266, 911.854, 191.091);
			var trashTankLoc = new WoWPoint(201.7278, 998.2936, 195.0932);
			var tankBossLoc = new WoWPoint(208.4425, 964.6042, 190.9823);
			var partyWaitLoc = new WoWPoint(201.3972, 1008.957, 197.0022);

			WoWUnit trash = null;
			AddAvoidObject(ctx => ObjectManager.GetObjectsOfType<WoWUnit>().All(u => u.Entry != ChainsOfWoeId), 12, u => u.Entry == RomoggBonecrusherId && u.ToUnit().CastingSpellId == SkullcrackerId);
			AddAvoidObject(ctx => true, 4, QuakeId);
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				// Clear the area and then pull boss.
				new Decorator(
					ctx => !boss.Combat,
					new PrioritySelector(
						ctx => trash = ScriptHelpers.GetUnfriendlyNpsAtLocation(tankBossLoc, 50, u => u != boss).FirstOrDefault(),
				// pull the trash when boss is far away
						ScriptHelpers.CreatePullNpcToLocation(
							ctx => Me.Y < 1031 && trash != null && !StyxWoW.Me.Combat,
							ctx => boss.Location.DistanceSqr(bossFarPathEndLoc) <= 25 * 25 || trash.Location.DistanceSqr(trashTankLoc) <= 50 * 50,
							ctx => trash, ctx => trashTankLoc, ctx => StyxWoW.Me.IsTank() ? trashTankLoc : partyWaitLoc, 5),
				// pull boss when he's near and trash is cleared.

						ScriptHelpers.CreatePullNpcToLocation(
							ctx => StyxWoW.Me.IsTank() && trash == null && StyxWoW.Me.Location.DistanceSqr(tankBossLoc) <= 50 * 50,
							ctx => boss.Location.DistanceSqr(tankBossLoc) <= 25 * 25,
							ctx => boss, ctx => tankBossLoc, ctx => StyxWoW.Me.IsTank() ? tankBossLoc : StyxWoW.Me.Location, 5)
						)),
				// Handle boss encounter
				new Decorator(
					ctx => boss.Combat,
					new PrioritySelector(new Decorator(ctx => Me.IsTank() && Targeting.Instance.IsEmpty(), new ActionAlwaysSucceed()))));
		}

		#region Corla, Herald of Twilight

		private readonly WoWPoint _impaledDrakeLoc = new WoWPoint(572.348, 899.8131, 155.3756);

		private List<WoWPoint> GetEvolvedLocations()
		{
			var evolvingZealots = ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == TwilightZealotId && u.HasAura("Kneeling in Supplication")).ToList();
			return evolvingZealots.Select(zealot => WoWMathHelper.CalculatePointFrom(_impaledDrakeLoc, zealot.Location, 3)).ToList();
		}

		private uint GetStacksOfEvolution(WoWUnit unit)
		{
			if (unit != null && unit.HasAura("Evolution"))
				return unit.Auras["Evolution"].StackCount;
			return 0;
		}

		[EncounterHandler(39679, "Corla, Herald of Twilight", Mode = CallBehaviorMode.Proximity, BossRange = 140)]
		public Composite CorlaEncounter()
		{
			const int darkCommandId = 75823;
			WoWUnit boss = null;
			AddAvoidObject(ctx => GetStacksOfEvolution(StyxWoW.Me) >= 80, 8, u => u.Entry == TwilightZealotId);

			var patByBossArea = new DungeonArea(
				new Vector2(567.4489f, 876.3621f), new Vector2(576.5095f, 876.5252f), new Vector2(577.4061f, 976.3552f), new Vector2(568.7939f, 976.7899f));

			var evolvedZealot2Path = new[]
									 {
										 new WoWPoint(587.9796, 859.4238, 175.5456), new WoWPoint(598.9826, 862.8481, 175.5456), new WoWPoint(598.9459, 873.7515, 173.9694),
										 new WoWPoint(599.0847, 883.8752, 170.9533), new WoWPoint(599.0799, 894.8361, 169.562), new WoWPoint(598.9447, 905.6454, 169.562),
										 new WoWPoint(598.8882, 920.5043, 169.5647), new WoWPoint(598.4432, 932.4044, 165.1852), new WoWPoint(598.0544, 945.2922, 160.0163),
										 new WoWPoint(597.8989, 958.0173, 155.3387),
									 };

			WoWUnit trashByBoss = null, pathingZealot2 = null;

			Func<bool> stayInBeamCondition = () => // healer and tank stay in beam if there is
											 boss != null && boss.Combat && GetStacksOfEvolution(StyxWoW.Me) < 80 && ScriptHelpers.IsBossAlive("Corla, Herald of Twilight") &&
											 (!StyxWoW.Me.PartyMembers.Any(p => p.Location.DistanceSqr(StyxWoW.Me.Location) <= 3 * 3 && GetStacksOfEvolution(p) > 0));

			var pathingZealot2TankLoc = new WoWPoint(568.2605, 942.6967, 155.3322);

			List<WoWPoint> evolutionLocs = null;
			WoWPoint nearestEmptyEvolutionLoc = WoWPoint.Zero;

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				// Clear the area before starting encounter.
				new Decorator(
					ctx => !boss.Combat,
					new PrioritySelector(
						ctx =>
						{
							// this is the 2st evolved twilight zealot on the top walkway. Since it walks down the ramp I can't use Z coord to differentiate between the other pathing zealots below. 
							pathingZealot2 =
								ObjectManager.GetObjectsOfType<WoWUnit>()
											 .FirstOrDefault(
												 u => u.Entry == EvolvedTwilightZealotId && u.IsAlive && evolvedZealot2Path.Any(loc => u.Location.DistanceSqr(loc) <= 8 * 8));
							// this is the trash group that paths in front of the boss.
							return trashByBoss = ScriptHelpers.GetUnfriendlyNpcsAtArea(patByBossArea, u => u.Z < 165).FirstOrDefault();
						},
				// pull pat when it's in position
						ScriptHelpers.CreatePullNpcToLocation(
							ctx => StyxWoW.Me.IsTank() && StyxWoW.Me.Z < 165 && trashByBoss != null,
							ctx => trashByBoss.DistanceSqr <= 27 * 27,
							ctx => trashByBoss,
							ctx => _jumpPointEndLoc,
							ctx => _jumpPointEndLoc,
							4),
				// kill the 2nd pathing zealot 
						ScriptHelpers.CreatePullNpcToLocation(
							ctx => StyxWoW.Me.IsTank() && StyxWoW.Me.Z < 165 && pathingZealot2 != null,
							ctx => pathingZealot2.Location.DistanceSqr(pathingZealot2TankLoc) <= 40 * 40,
							ctx => pathingZealot2,
							ctx => pathingZealot2TankLoc,
							ctx => pathingZealot2TankLoc,
							4))),
				// Handle boss encounter
				new Decorator(
					ctx => boss.Combat,
					new PrioritySelector(
						ctx =>
						{
							evolutionLocs = GetEvolvedLocations();
							//!ScriptHelpers.PartyIncludingMe.Any(p => p.Location.DistanceSqr(point) <= 3*3)
							if (StyxWoW.Me.IsMelee()) // melee pick an empty evolution spot nearest to boss.
							{
								nearestEmptyEvolutionLoc =
									evolutionLocs.Where(loc => !Me.PartyMembers.Any(p => p.IsAlive && p.Location.DistanceSqr(loc) <= 3 * 3))
												 .OrderBy(loc => boss.Location.DistanceSqr(loc))
												 .FirstOrDefault();
							}
							else
							{
								nearestEmptyEvolutionLoc =
									evolutionLocs.Where(loc => !Me.PartyMembers.Any(p => p.IsAlive && p.Location.DistanceSqr(loc) <= 3 * 3))
												 .OrderByDescending(loc => boss.Location.DistanceSqr(loc))
												 .FirstOrDefault();
							}
							return ctx;
						},
						ScriptHelpers.CreateInterruptCast(ctx => boss, darkCommandId),
						new Decorator(
							ctx => nearestEmptyEvolutionLoc != WoWPoint.Zero && GetStacksOfEvolution(StyxWoW.Me) == 0,
							new PrioritySelector(
								new Decorator(
									ctx => true,
									new Sequence(
										new DecoratorContinue(ctx => !ScriptHelpers.MovementEnabled, new Action(ctx => ScriptHelpers.RestoreMovement())),
										ScriptHelpers.CreateMoveToContinue(ctx => nearestEmptyEvolutionLoc),
										new Action(ctx => WoWMovement.ClickToMove(nearestEmptyEvolutionLoc)),
										new WaitContinue(3, ctx => GetStacksOfEvolution(StyxWoW.Me) > 0, new ActionAlwaysSucceed()),
										new Action(ctx => ScriptHelpers.DisableMovement(stayInBeamCondition)))))))));
		}

		#endregion

		#region Karsh Steelbender

		private WoWUnit _karshSteelbender;
		private const uint KarshSteelbenderId = 39698;

		[EncounterHandler(39698, "Karsh Steelbender", Mode = CallBehaviorMode.Proximity, BossRange = 121)]
		public Func<WoWUnit, Task<bool>> KarshSteelbenderEncounter()
		{
			var grillCenterLoc = new WoWPoint(237.3117, 786.271, 95.6746);
			var trashTankLoc = new WoWPoint(293.7574, 817.3774, 103.516);

			// If follower avoid the frontal cone of the boss because of cleave
			AddAvoidObject(ctx => Me.IsFollower(), 7, 
				o => o.Entry == KarshSteelbenderId && o.ToUnit().Combat, 
				o => o.Location.RayCast(o.Rotation, 5));

			Func<bool> shouldAvoidPillar = () => Me.IsLeader() && ScriptHelpers.IsViable(_karshSteelbender)
												&& _karshSteelbender.Aggro
												&& _karshSteelbender.HasAura("Transform");

			// avoid the pillar if boss has superheated armor.
			AddAvoidLocation(ctx => shouldAvoidPillar(), 
				// increase the radius of the avoid when boss is standing in pillar to force tank to back off and pull boss away
				o => ScriptHelpers.IsViable(_karshSteelbender)
					&& _karshSteelbender.Aggro
					&& _karshSteelbender.Location.DistanceSqr(grillCenterLoc) < 9*9
					? 18
					: 9,
				ctx => grillCenterLoc);

			return async boss =>
			{
				_karshSteelbender = boss;
				// clear the trash group before pulling group.
				if (!boss.Combat)
				{
					if (await ScriptHelpers.DispelGroup("Immolate", ScriptHelpers.PartyDispelType.Magic))
						return true;
					var trash =
						ScriptHelpers.GetUnfriendlyNpsAtLocation(grillCenterLoc, 40, u => u != _karshSteelbender)
							.FirstOrDefault();

					// pull trash around the boss.
					if (await ScriptHelpers.PullNpcToLocation(
						() => trash != null && trash.IsValid,
						() => trash.Location.DistanceSqr(trashTankLoc) <= 60*60
							&& trash.Location.DistanceSqr(boss.Location) > 20*20,
						trash,
						trashTankLoc,
						trashTankLoc,
						5000))
					{
						return true;
					}

					// Wait for the Heat Exhaustion debuf to wear off.
                    return await ScriptHelpers.StayAtLocationWhile(
                        () => Me.IsLeader() && !Me.Combat && Me.HasAura("Heat Exhaustion"),
                        trashTankLoc,
                        precision: 5);
				}

				// Combat behavior.
				if (Me.IsLeader())
				{
					// pull boss in the pillar of fire so he gains Superheated Quicksilver Armor
					if (boss.CurrentTargetGuid == Me.Guid && !shouldAvoidPillar())
					{
						var moveTo = WoWMathHelper.CalculatePointFrom(boss.Location, grillCenterLoc, -3);
						return (await CommonCoroutines.MoveTo(moveTo)).IsSuccessful();
					}
				}

				// range move off the grill.
				if (Me.IsRange() && Me.Location.DistanceSqr(grillCenterLoc) > 26*26)
				{
					var innerBounds = WoWMathHelper.CalculatePointFrom(Me.Location, grillCenterLoc, 8);
					var outerBounds = WoWMathHelper.CalculatePointFrom(Me.Location, grillCenterLoc, 23);
					var moveTo = Me.Location.GetNearestPointOnSegment(innerBounds, outerBounds);
					return (await CommonCoroutines.MoveTo(moveTo)).IsSuccessful();
				}
				return false;
			};
		}

		#endregion


		private WoWUnit _beauty;

		[EncounterHandler(39700, "Beauty", Mode = CallBehaviorMode.Proximity, BossRange = 80)]
		public Composite BeautyEncounter()
		{
			WoWUnit pup = null;
			var tankPupsLoc = new WoWPoint(159.3506, 580.4273, 76.91465);

			var wallPoints = new[]
								 {
									 new WoWPoint(158.3496, 571.7232, 76.13611),
									 new WoWPoint(158.1586, 595.2212, 79.35516),
									 new WoWPoint(144.2739, 601.5454, 76.35615),
									 new WoWPoint(119.3436, 554.9016, 76.438),
								 };

			return new PrioritySelector(
				ctx => _beauty = ctx as WoWUnit,
				// pull the pups and kill them one by one.. only works on normal.
				new Decorator(
					ctx => !_beauty.Combat,
					new PrioritySelector(
						ctx => pup = ScriptHelpers.GetUnfriendlyNpsAtLocation(_beauty.Location, 60, u => u != _beauty && u.Entry != RuntyId).FirstOrDefault(),
						ScriptHelpers.CreatePullNpcToLocation(ctx => pup != null, ctx => pup, ctx => tankPupsLoc, 10))),
				// tank Beauty against a wall to reduce knockback
				new Decorator(
					ctx => _beauty.Combat,
					new PrioritySelector(
						new Decorator(
							ctx => StyxWoW.Me.IsTank() && _beauty.CurrentTarget == StyxWoW.Me,
				// tank agains the closest wall.
							ScriptHelpers.CreateTankUnitAtLocation(ctx => wallPoints.OrderBy(loc => StyxWoW.Me.Location.DistanceSqr(loc)).FirstOrDefault(), 5))
						))
				);
		}



		[EncounterHandler(39705, "Ascendant Lord Obsidius")]
		public Composite AscendantLordObsidiusEncounter()
		{
			WoWUnit boss = null;

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateDispelGroup("Twilight Corruption", ScriptHelpers.PartyDispelType.Magic)
				);
		}

		private readonly WaitTimer _teleportTimer = new WaitTimer(TimeSpan.FromSeconds(10));

		public override MoveResult MoveTo(WoWPoint location)
		{
			// use entrance portal.
			if ((StyxWoW.Me.Y > 1000 && location.Y < 800 || StyxWoW.Me.Y < 800 && location.Y > 1000) && !ScriptHelpers.IsBossAlive("Karsh Steelbender") && _teleportTimer.IsFinished)
			{
				var teleporter = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == 51340);

				if (teleporter != null)
				{
					if (!teleporter.WithinInteractRange)
						return Navigator.MoveTo(teleporter.Location);
					teleporter.Interact();
					_teleportTimer.Reset();
					return MoveResult.Moved;
				}
			}
			return MoveResult.Failed;
		}

		#endregion
	}
}