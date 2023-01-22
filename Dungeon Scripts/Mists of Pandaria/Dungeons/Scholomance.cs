
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Classic
{
	#region Normal Difficulty
	public class Scholomance : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 2; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(1279.48, -2551.52, 87.41); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(188.05, 126.49, 138.82); }
		}


		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret as WoWUnit;
					if (unit != null)
					{

						if (unit.Entry == WovenBoneguardId && unit.Combat && Me.Combat && !unit.IsTargetingMeOrPet && !unit.IsTargetingMyPartyMember)
							return true;

						if (unit.Entry == LiliansSoulId && Me.IsMelee() && _isFixated)
							return true;

						if (unit.CurrentTargetGuid != Me.Guid && IsTakingHarshLesson(Me))
							return true;

						if (unit.Entry == DarkmasterGandlingId && IsTakingHarshLesson(Me))
							return true;
						if (unit.Entry == FailedStudentId || unit.Entry == ExpiredTestSubjectId)
						{
							if (!unit.InLineOfSpellSight)
								return true;
							var target = unit.CurrentTarget;
							bool isTargetingMe = target != null && target.IsMe;
							if (target != null && !isTargetingMe && target is WoWPlayer)
							{
								if (IsTakingHarshLesson((WoWPlayer)target))
									return true;
							}
							else if (isTargetingMe && !IsTakingHarshLesson(StyxWoW.Me) && _darkmasterGandling != null && _darkmasterGandling.IsValid && _darkmasterGandling.IsAlive)
							{
								// get 
								if (Me.IsHealer())
								{
									var tank = ScriptHelpers.Tank;
									return tank != null && !IsTakingHarshLesson(tank) && !tank.InLineOfSpellSight;
								}
								return !_darkmasterGandling.InLineOfSpellSight;
							}
						}
					}
					return false;
				});
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			foreach (var obj in incomingunits)
			{
				var unit = obj as WoWUnit;
				if (unit != null)
				{
					if (unit.Entry == RisenGuardId && !Me.Combat && Me.IsTank() && unit.Distance <= 50 && unit.ZDiff < 10 && ScriptHelpers.IsBossAlive("Instructor Chillheart"))
						outgoingunits.Add(unit);

					if (unit.Entry == BoneweaverId && Me.IsTank() && Me.Combat && !ScriptHelpers.IsBossAlive("Jandice Barov") && unit.Distance <= 40)
						outgoingunits.Add(unit);

					if (unit.Entry == InstructorChillheartsPhylactery)
						outgoingunits.Add(unit);

					// make sure all spawns are added to targeting. 
					// The correct one is given more priority but boss sometimes doesn't respawn after killing it.
					if (unit.Entry == JandiceBarovSpawnId)
						outgoingunits.Add(unit);

					if (unit.Entry == ExpiredTestSubjectId && IsTakingHarshLesson(Me))
						outgoingunits.Add(unit);
				}
			}
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			var isDps = Me.IsDps();
			bool chillHeartInCombat = _chillheart != null && _chillheart.IsValid && _chillheart.Combat;
			foreach (var priority in units)
			{
				var unit = priority.Object as WoWUnit;
				if (unit != null)
				{
					// focus fire instructor chillheart even if adds argo ..
					if (chillHeartInCombat && unit.Entry != InstructorChillheartId)
						priority.Score -= 10000;
					// clear everything in room before engaging.
					if (unit.Entry == InstructorChillheartId && !unit.Combat)
						priority.Score -= 10000;
					// give Jandice Barov and her spawn more priority 
					if (unit.Entry == JandiceBarovId || unit.Entry == JandiceBarovSpawnId && unit.DisplayId == JandiceDisplayId)
						priority.Score += 10000;

					if (unit.Entry == BoneweaverId)
						priority.Score = 5000 - unit.HealthPercent * 2;

					if (unit.Entry == CandlestickMageId && isDps)
						priority.Score += 500;

					if (_meatGraftIds.Contains(unit.Entry) && isDps)
						priority.Score += 5000;

					if (unit.Entry == FailedStudentId && !IsTakingHarshLesson(Me) || unit.Entry == ExpiredTestSubjectId)
						priority.Score += 5000;
				}
			}
		}

		readonly WoWPoint _entranceShortcutJumpLoc = new WoWPoint(200.4544, 37.83932, 119.2258);

		readonly WoWPoint _entranceDoorRightSide = new WoWPoint(196.5603, 56.27681, 132.3066);
		readonly WoWPoint _entranceDoorLeftSide = new WoWPoint(204.3677, 56.41592, 132.3109);

		public override MoveResult MoveTo(WoWPoint location)
		{
			var myLoc = Me.Location;
			if (myLoc.Distance2DSqr(_entranceShortcutJumpLoc) < 20 * 20 && myLoc.Z > 132 && location.Z < 121 && myLoc.IsPointLeftOfLine(_entranceDoorLeftSide, _entranceDoorRightSide))
			{
				Navigator.PlayerMover.MoveTowards(_entranceShortcutJumpLoc);
				if (myLoc.Distance2D(_entranceShortcutJumpLoc) < 8)
				{
					WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
					WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend);
				}
				return MoveResult.Moved;
			}
			return base.MoveTo(location);
		}

		public override bool IsComplete
		{
			get { return _buggedJanice || base.IsComplete; }
		}

		public override void OnEnter()
		{
			_buggedJanice = false;
			base.OnEnter();
		}

		#endregion

		private const uint InstructorChillheartsPhylactery = 58664;
		private const uint CandlestickMageId = 59467;
		private const uint JandiceBarovSpawnId = 59220;
		private const uint BoneweaverId = 59193;
		private const uint BonePileId = 59304;
		private const uint SoulflameId = 59316;
		private readonly uint[] _meatGraftIds = { 59982, 59360 };
		private const uint LiliansSoulId = 58791;
		private const uint WovenBoneguardId = 59213;
		private const uint LilianVossId = 58722;
		private const int HarshLessonId = 113420;
		private const uint ExpiredTestSubjectId = 59100;
		private const uint FreshTestSubjectId = 59099;
		private const uint BoredStudentId = 59614;
		private const uint DarkmasterGandlingId = 59080;
		private const uint FailedStudentId = 59078;
		private const int JandiceDisplayId = 43460;
		private readonly Dictionary<ulong, HarshLessonLogEntry> _partyHarshLessonsLog = new Dictionary<ulong, HarshLessonLogEntry>();
		private WoWUnit _darkmasterGandling;
		private bool _isFixated;

		#region Root

		protected static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0, "Root")]
		public Composite RootEncounter()
		{
			//  Me.CurrentTarget.DisplayId


			return new PrioritySelector();
		}
		#endregion

		#region Quest

		protected virtual uint TheFourTomesQuestId { get { return 31440; } }

		readonly Dictionary<uint, int> _fourTombQuestObjectiveIndexes = new Dictionary<uint, int>
									  {
										  {214101, 0}, // In the Shadow of the Light
										  {214105, 1}, // Kel'Thuzad's Deep Knowledge
										  {214106, 2}, // Forbidden Rites and other Rituals Necromantic
										  {214107, 3} // The Dark Grimoire
									  };

		protected virtual Dictionary<uint, int> FourTombQuestObjectiveIndexes
		{
			get { return _fourTombQuestObjectiveIndexes; }
		}

		[EncounterHandler(64562, "Talking Skull", Mode = CallBehaviorMode.Proximity)]
		[EncounterHandler(64563, "Talking Skull Heroic", Mode = CallBehaviorMode.Proximity)]
		public Composite TalkingSkullEncounter()
		{
			return new PrioritySelector(
				new Decorator<WoWUnit>(unit => unit.QuestGiverStatus == QuestGiverStatus.Available, ScriptHelpers.CreatePickupQuest(ctx => (WoWUnit)ctx)),
				new Decorator<WoWUnit>(unit => unit.QuestGiverStatus == QuestGiverStatus.TurnIn, ScriptHelpers.CreateTurninQuest(ctx => (WoWUnit)ctx)));
		}


		// pickup quest items.
		[ObjectHandler(214101, "In the Shadow of the Light", ObjectRange = 45)]
		[ObjectHandler(214105, "Kel'Thuzad's Deep Knowledge", ObjectRange = 45)]
		[ObjectHandler(214106, "Forbidden Rites and other Rituals Necromantic", ObjectRange = 45)]
		[ObjectHandler(214107, "The Dark Grimoire", ObjectRange = 45)]

		public virtual Composite TheFourTombsHandler()
		{
			WoWGameObject questObject = null;


			return new PrioritySelector(
				ctx => questObject = ctx as WoWGameObject,
				new Decorator(
					ctx => CanPickupTomb(questObject),
					new PrioritySelector(
						new Decorator(ctx => !questObject.WithinInteractRange, new Action(ctx => Navigator.MoveTo(questObject.Location))),
						new Decorator(
							ctx => questObject.WithinInteractRange,
							new Sequence(
								new Action(ctx => questObject.Interact()),
								new WaitContinue(3, ctx => LootFrame.Instance.IsVisible, new ActionAlwaysSucceed()),
								new Action(ctx => Logger.Write("Looting {0}", questObject.SafeName)),
								new Action(ctx => LootFrame.Instance.LootAll()))))));
		}

		protected bool CanPickupTomb(WoWGameObject tomb)
		{
			var quest = StyxWoW.Me.QuestLog.GetQuestById(TheFourTomesQuestId);
			if (quest == null || quest.IsCompleted)
				return false;

			WoWDescriptorQuest descQuest;
			if (!quest.GetData(out descQuest))
				return false;

			if (descQuest.ObjectivesDone[FourTombQuestObjectiveIndexes[tomb.Entry]] > 0)
				return false;

			if (!Targeting.Instance.IsEmpty() || ScriptHelpers.WillPullAggroAtLocation(tomb.Location))
				return false;

			var pathDist = Me.Location.PathDistance(tomb.Location, 45f);
			return pathDist.HasValue && pathDist.Value < 45f;
		}

		#endregion

		#region Instructor Chillheart

		private const uint InstructorChillheartId = 58633;
		private const uint RisenGuardId = 58822;
		private WoWUnit _chillheart;

		[EncounterHandler(58633, "Instructor Chillheart", Mode = CallBehaviorMode.Proximity, BossRange = 80)]
		public Composite InstructorChillheartEncounter()
		{
			const uint iceWallId = 62731;
			const uint arcaneBombId = 58753;
			const uint antonidasSelfHelpGuideToStandingInFireId = 58635;
			const uint frigidGraspId = 58640;
			var phase1TankLoc = new WoWPoint(203.2554, 38.66046, 119.2258);

			AddAvoidObject(ctx => true, 5, frigidGraspId);
			AddAvoidObject(ctx => true, 3, u => u is WoWPlayer && !u.IsMe && ((WoWPlayer)u).HasAura("Ice Wrath"));
			AddAvoidObject(ctx => true, 4, arcaneBombId, antonidasSelfHelpGuideToStandingInFireId);
			// avoid the icewall. 
			AddAvoidObject(
				ctx => true,
				8,
				u => u.Entry == iceWallId,
				o =>
				{ // the icewall object is just one NPC so we need to use its y location and use our x location 
					var loc = o.Location;
					loc.X = Me.X;
					return loc;
				});

			// avoid Instructor Chillheart so not to pull before surrounding trash is killed.
			AddAvoidObject(
				ctx => !Me.IsTank() || ObjectManager.GetObjectsOfType<WoWUnit>().Any(u => u.Entry == RisenGuardId && u.ZDiff < 10),
				16,
				o => o.Entry == InstructorChillheartId && o.ToUnit().IsAlive && !o.ToUnit().Combat);

			return new PrioritySelector(
				ctx => _chillheart = ctx as WoWUnit,
				// OOC behavior
				new Decorator(
					ctx => !_chillheart.Combat,
					new PrioritySelector(
				// kill all risen guards 
						ScriptHelpers.CreateClearArea(() => _chillheart.Location, 70, u => u.Entry == RisenGuardId && u.ZDiff < 10))),
				// Phase 1 combat behavior
				new Decorator(
					ctx => _chillheart.Combat && !_chillheart.HasAura("Permanent Feign Death"),
					new PrioritySelector(
				// move to the west wall 
						new Decorator(
							ctx => StyxWoW.Me.IsRange() && StyxWoW.Me.Y < 30,
							new Action(
								ctx =>
								{
									var loc = StyxWoW.Me.Location;
									loc.Y = 35;
									Navigator.MoveTo(loc);
								})),
						ScriptHelpers.CreateTankUnitAtLocation(ctx => phase1TankLoc, 5))),
				// Phase 2 combat behavior
				new Decorator(ctx => _chillheart.Combat && _chillheart.HasAura("Permanent Feign Death"), new PrioritySelector()));
		}

		#endregion

		#region Jandice Barov

		private const uint JandiceBarovId = 59184;
		private bool _buggedJanice;

		[EncounterHandler(59184, "Jandice Barov", Mode = CallBehaviorMode.CurrentBoss)]
		public Func<WoWUnit, Task<bool>> JandiceBarovIllusionEncounter()
		{
			var buggedTimer = new WaitTimer(TimeSpan.FromMinutes(5));
			var bossLoc = new WoWPoint(286.9189, 43.07346, 113.4088);

			return async boss =>
			{
				if (!Me.IsTank())
					return false;

				if (boss != null)
				{
					if (boss.Combat)
						buggedTimer.Reset();
					return false;
				}
				// if this gate is open then  Jandice was killed.
				if (IsRattleGoreEntranceGateOpen)
					return false;

				if (buggedTimer.IsFinished)
				{
					_buggedJanice = true;
					return false;
				}

				// do nothing while we wait for boss to spawn
				if (Targeting.Instance.IsEmpty() && Me.Location.DistanceSqr(bossLoc) < 10 * 10)
					return true;

				return false;
			};
		}

		[EncounterHandler(59184, "Jandice Barov")]
		public Composite JandiceBarovEncounter()
		{
			const int wondrousRapidityId = 114062;
			const uint gravityFluxId = 114035;
			WoWUnit boss = null;
			AddAvoidObject(ctx => true, 8, o => o is WoWAreaTrigger && ((WoWAreaTrigger)o).SpellId == gravityFluxId);
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(
					ctx => Me.IsTank() && boss.CastingSpellId == wondrousRapidityId || !Me.IsTank(),
					ctx => boss,
					new ScriptHelpers.AngleSpan(0, 90)));
		}

		#endregion

		#region Rattlegore

		[EncounterHandler(59153, "Rattlegore Spawn", Mode = CallBehaviorMode.CurrentBoss)]
		public Composite WaitForRattlegoreSpawn()
		{
			// Wait for Rattlegore to spawn.
			var spawnLoc = new WoWPoint(261.9058, 91.38342, 113.4891);

			return new Decorator(ctx => ctx == null && Me.IsTank() && !IsRattleGoreExitGateOpen
					&& Targeting.Instance.IsEmpty() && Me.Location.DistanceSqr(spawnLoc) < 20 * 20,
					new PrioritySelector(
						new ActionSetActivity("Waiting for Rattlegore spawn"),
						new ActionAlwaysSucceed()));
		}

		[EncounterHandler(59153, "Rattlegore", BossRange = 100, Mode = CallBehaviorMode.Proximity)]
		public Composite RattlegoreEncounter()
		{
			WoWUnit boss = null;
			WoWUnit selectedBonePile = null;
			var waitLoc = new WoWPoint(261.0993, 64.81649, 113.4927);
			// run from soul flame.
			AddAvoidObject(ctx => true, obj => obj.Scale + 2, SoulflameId);
			return new PrioritySelector(
				ctx =>
				{
					var soulFlames = ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == SoulflameId).ToArray();
					selectedBonePile = (from bonePile in ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == BonePileId)
										let bonepileDist = bonePile.Distance
										// skip any bone piles that are inside a soul flame.
										where
											soulFlames.All(s => s.Location.Distance(bonePile.Location) > s.Scale + 2) &&
											// skip bone piles that other party members who are missing the buff, are heading towards it and are closer.
											!Me.PartyMembers.Any(
												p => !p.HasAura("Bone Armor") && p.IsSafelyFacing(bonePile, 45) && p.IsMoving && p.Location.Distance(bonePile.Location) < bonepileDist)
										orderby bonePile.Distance2DSqr
										select bonePile).FirstOrDefault();
					selectedBonePile =
						ObjectManager.GetObjectsOfType<WoWUnit>()
							.Where(u => u.Entry == BonePileId && soulFlames.All(s => s.Location.Distance(u.Location) > s.Scale + 2))
							.OrderBy(u => u.DistanceSqr)
							.FirstOrDefault();
					return boss = ctx as WoWUnit;
				},
				// move inside the gate.
				new Decorator(
					ctx => !Me.IsTank() && IsRattleGoreEntranceGateOpen && ScriptHelpers.Tank != null && ScriptHelpers.Tank.Y >= 61 && Me.Y < 61f && Me.Z < 120,
					new Action(ctx => Navigator.MoveTo(waitLoc))),
				new Decorator(
					ctx => Me.IsTank() && !Me.Combat && Me.Y >= 61 && Me.PartyMembers.Count(p => p.Y >= 61f) < 4 && Me.Z < 120,
					new Sequence(new ActionSetActivity("Waiting for other party members to enter room at Rattlegore"), new Action(ctx => Navigator.MoveTo(waitLoc)))),
				// get the bone pile buff.
				new Decorator(
					ctx => boss.Combat && !Me.HasAura("Bone Armor") && selectedBonePile != null && (Me.IsTank() && boss.CurrentTargetGuid == Me.Guid || !Me.IsTank()),
					new PrioritySelector(
						new Decorator(ctx => selectedBonePile.WithinInteractRange, new Action(ctx => selectedBonePile.Interact())),
						new Decorator(ctx => !selectedBonePile.WithinInteractRange, new Action(ctx => Navigator.MoveTo(selectedBonePile.Location))))));
		}

		private const uint RattlegoreEntranceGateId = 211256;
		private const uint RattlegoreExitGateId = 211262;

		private bool IsRattleGoreEntranceGateOpen
		{
			get
			{
				return ObjectManager.GetObjectsOfTypeFast<WoWGameObject>()
					.Any(g => g.Entry == RattlegoreEntranceGateId && g.State == WoWGameObjectState.Active);
			}
		}

		private bool IsRattleGoreExitGateOpen
		{
			get
			{
				return ObjectManager.GetObjectsOfTypeFast<WoWGameObject>()
					.Any(g => g.Entry == RattlegoreExitGateId && g.State == WoWGameObjectState.Active);
			}
		}

		#endregion

		#region Lilian Voss

		[EncounterHandler(58722, "Lilian Voss", Mode = CallBehaviorMode.Proximity)]
		public Composite LilianVossEncounter()
		{
			const uint darkBlazeId = 58780;
			const int deathsGraspId = 111570;
			const int shadowShivId = 111775;
			var runFromBossTimer = new WaitTimer(TimeSpan.FromSeconds(3));
			var entLoc = new WoWPoint(200.3293, 104.8955, 108.2328);
			WoWUnit boss = null;
			var roomCenterLoc = new WoWPoint(200.4618, 86.05209, 107.7617);
			// prevent bot from going into hallway during encounter.
			AddAvoidLocation(ctx => boss != null && boss.IsValid && boss.Combat && Me.Y <= 104, 4, o => entLoc);
			// run from from the fire on ground. ignore it if moving while fixated.
			AddAvoidObject(ctx => _isFixated && !Me.IsMoving || !_isFixated, () => roomCenterLoc, 25, o => Me.IsRange() && Me.IsMoving ? 4 : 2, darkBlazeId);
			// run away from boss after she casts deaths grasp.
			AddAvoidObject(ctx => true, 8, o => o.Entry == LilianVossId && o.ToUnit().CastingSpellId != deathsGraspId && !runFromBossTimer.IsFinished);
			// run from shadow shiv target
			AddAvoidObject(
				ctx => true,
				8,
				u =>
					u is WoWPlayer &&
					(boss != null && boss.IsValid && boss.CastingSpellId == shadowShivId && boss.CurrentTargetGuid == u.Guid || u.ToUnit().HasAura(shadowShivId)));
			return new PrioritySelector(
				ctx =>
				{
					boss = ctx as WoWUnit;
					// ToDo Lilian Voss does not show as dead when defeated so need to mark him as dead but need to test this.
					//if (!boss.CanSelect && ScriptHelpers.IsBossAlive("Lilian Voss"))
					//	ScriptHelpers.MarkBossAsDead("Lilian Voss", "Found dead");

					if (boss.CastingSpellId == deathsGraspId)
						runFromBossTimer.Reset();
					return boss;
				},
				new Decorator(
					ctx => boss.Combat && ScriptHelpers.IsBossAlive("Lilian Voss"),
					new PrioritySelector(
						new Decorator(ctx => Me.Y > 104,
							new PrioritySelector(
				// bot is outside room and needs to hurry and try get in room before door closes.
								new Decorator(ctx => Me.Y >= 122,
									new Action(ctx => Navigator.MoveTo(roomCenterLoc))),
				// there's an avoid location at door entrance and need to ctm to get past it if trapped.
								new Decorator(ctx => Me.Y < 122,
									new Action(ctx => Navigator.PlayerMover.MoveTowards(roomCenterLoc))))),
						new Decorator(ctx => Me.IsTank() && boss.IsTargetingMeOrPet, ScriptHelpers.CreateTankUnitAtLocation(ctx => roomCenterLoc, 20)))));
		}

		[EncounterHandler(58791, "Lilian's Soul")]
		public Composite LiliansSoulEncounter()
		{
			const int fixateAngerId = 115350;
			WoWUnit boss = null;
			var roomCenterLoc = new WoWPoint(200.4618, 86.05209, 107.7617);

			// run from boss if fixated. _isFixated is updated in the behavior.
			AddAvoidObject(ctx => _isFixated, () => roomCenterLoc, 25, o => Me.IsRange() && Me.IsMoving ? 14 : 9, LiliansSoulId);
			return new PrioritySelector(
				ctx =>
				{
					boss = ctx as WoWUnit;
					_isFixated = boss.CastingSpellId == fixateAngerId && boss.CurrentTargetGuid == Me.Guid || Me.HasAura(115350);
					return boss;
				},
				// stay out of the hall to avoid getting cornered.
				new Decorator(ctx => Me.Y > 104, new Action(ctx => Navigator.PlayerMover.MoveTowards(roomCenterLoc))),
				new Decorator(ctx => Me.IsTank() && Targeting.Instance.IsEmpty(), new ActionAlwaysSucceed()));
		}

		[ObjectHandler(211277, "Coffer of Forgotten Souls")]
		[ObjectHandler(211278, "Coffer of Forgotten Souls Heroic")]
		public Composite CofferofForgottenSoulsHandler()
		{
			WoWGameObject chest = null;
			return new PrioritySelector(
				ctx => chest = ctx as WoWGameObject,
				new Decorator(
					ctx => chest.CanLoot,
					new PrioritySelector(
						new Decorator(ctx => ScriptHelpers.IsBossAlive("Lilian Voss"), new Action(ctx => ScriptHelpers.MarkBossAsDead("Lilian Voss"))),
						ScriptHelpers.CreateLootChest(ctx => chest))));
		}

		#endregion

		#region Professor Slate

		[EncounterHandler(59613, "Professor Slate", Mode = CallBehaviorMode.Proximity, BossRange = 62)]
		public Composite ProfessorSlateEncounter()
		{
			var boredStudenPullToLoc = new WoWPoint(144.916, 73.33847, 106.3931);

			WoWUnit boss = null, boredStudent = null;
			const uint toxicPoison = 114873;
			AddAvoidObject(ctx => true, 4, toxicPoison);

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				new Decorator(
					ctx => !boss.Combat && !ScriptHelpers.IsBossAlive("Lilian Voss"),
					new PrioritySelector(
						ctx =>
							boredStudent = (from unit in ObjectManager.GetObjectsOfType<WoWUnit>()
											where unit.Entry == BoredStudentId && unit.IsAlive && unit.Z <= 107
											let pathDist = unit.Location.PathDistance(Me.Location)
											where pathDist.HasValue
											orderby pathDist.Value
											select unit).FirstOrDefault(),

						ScriptHelpers.CreatePullNpcToLocation(ctx => boredStudent != null, ctx => boredStudent, ctx => boredStudenPullToLoc, 6))),
				new Decorator(
					ctx => boss.Combat,
					new PrioritySelector(
						ScriptHelpers.CreateTankFaceAwayGroupUnit(ctx => boss, 15),
						ScriptHelpers.CreateAvoidUnitAnglesBehavior(ctx => !Me.IsTank() && boss.Distance < 15, ctx => boss, new ScriptHelpers.AngleSpan(0, 60)))));
		}

		#endregion

		#region Darkmaster Gandling

		[EncounterHandler(59080, "Darkmaster Gandling", BossRange = 300)]
		public Composite DarkmasterGandlingEncounter()
		{
			AddAvoidObject(ctx => IsTakingHarshLesson(Me) && Me.IsHealer(), 8, u => u.Entry == ExpiredTestSubjectId && u.ToUnit().IsTargetingMeOrPet);
			// avoid running into doors when having a harse lesson.
			AddAvoidObject(ctx => IsTakingHarshLesson(Me), 4, u => u is WoWGameObject && u.ToGameObject().SubType == WoWGameObjectType.Door && u.ZDiff < 10);
			WoWSpell dispell = null;
			WoWUnit dispellTarget = null;

			WoWUnit[] freshTestSubjects = null;

			return new PrioritySelector(
				ctx => _darkmasterGandling = ctx as WoWUnit,
				ScriptHelpers.CreateDispelGroup("Immolate", ScriptHelpers.PartyDispelType.Magic),
				new Decorator(
					ctx =>
						!IsTakingHarshLesson(Me) &&
						!ObjectManager.GetObjectsOfType<WoWUnit>()
							.Any(u => (u.Entry == ExpiredTestSubjectId || u.Entry == FailedStudentId) && u.IsTargetingMeOrPet && u.IsWithinMeleeRange) &&
						(!Me.IsTank() || Targeting.Instance.TargetList.All(t => t.CurrentTargetGuid == Me.Guid || t.Entry == DarkmasterGandlingId)) &&
						_darkmasterGandling.Distance > 10,
					new Action(ctx => Navigator.MoveTo(_darkmasterGandling.Location))),
				new Decorator(
				// healer behavior for harsh lesson.
					ctx => IsTakingHarshLesson(Me) && Me.IsHealer(),
					new PrioritySelector(
						ctx =>
						{
							freshTestSubjects = (from unit in ObjectManager.GetObjectsOfType<WoWUnit>()
												 where unit.Entry == FreshTestSubjectId && unit.HasAura("Explosive Pain")
												 let pathDist = Me.Location.PathDistance(unit.Location, 50f)
												 where pathDist.HasValue && pathDist.Value < 50f
												 orderby pathDist
												 select unit).ToArray();

							dispellTarget =
								freshTestSubjects.FirstOrDefault(u => ObjectManager.GetObjectsOfType<WoWUnit>().Any(o => o.IsHostile && o.Location.Distance(u.Location) < 8));

							dispell = ScriptHelpers.GetDefensiveMagicDispel();
							return dispellTarget;
						},
						new Decorator(
							ctx => dispellTarget != null && dispell != null && SpellManager.CanCast(dispell, dispellTarget, true, false),
							new Action(ctx => SpellManager.Cast(dispell, dispellTarget))),
						new Decorator(
							ctx => dispellTarget != null && dispell != null && (dispellTarget.Distance > 30 || !dispellTarget.InLineOfSpellSight),
							new Action(ctx => Navigator.MoveTo(dispellTarget.Location))),
						new Decorator(
							ctx => dispellTarget == null && dispell != null && freshTestSubjects.Length > 0 && freshTestSubjects[0].Distance > 4,
							new Action(ctx => Navigator.MoveTo(freshTestSubjects[0].Location))))));
		}

		private bool IsTakingHarshLesson(WoWPlayer player)
		{
			_partyHarshLessonsLog.RemoveAll(v => !v.IsValid);

			if (!_partyHarshLessonsLog.ContainsKey(player.Guid))
				_partyHarshLessonsLog[player.Guid] = new HarshLessonLogEntry(player);


			return _partyHarshLessonsLog[player.Guid].HasHarshLession;
		}

		#endregion

		#region Nested type: HarshLessonLogEntry

		private class HarshLessonLogEntry
		{
			private readonly WaitTimer _harshLessonTimer = new WaitTimer(TimeSpan.FromSeconds(30));
			private readonly WoWPlayer _player;

			private bool _hasHarshLesson;

			public HarshLessonLogEntry(WoWPlayer player)
			{
				_player = player;
			}

			public bool HasHarshLession
			{
				get
				{
					if (!IsValid) return false;

					if (!_player.IsAlive) return false;

					var hasHarshLessonAura = _player.HasAura(HarshLessonId);

					if (!hasHarshLessonAura)
					{
						_hasHarshLesson = false;
						return false;
					}
					if (!_hasHarshLesson)
					{
						_harshLessonTimer.Reset();
						_hasHarshLesson = true;
					}
					if (!ObjectManager.GetObjectsOfType<WoWUnit>().Any(u => u.Entry == ExpiredTestSubjectId && u.CurrentTargetGuid == _player.Guid))
						return false;
					return !_harshLessonTimer.IsFinished;
				}
			}

			public bool IsValid
			{
				get { return _player.IsValid; }
			}
		}

		#endregion
	}
	#endregion

	#region Heroic Difficulty

	public class ScholomanceHeroic : Scholomance
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 472; }
		}

		#endregion

		// heroic version 
		private readonly Dictionary<uint, int> _fourTombQuestObjectiveIndexes = new Dictionary<uint, int>
																			{
																				{214279, 0}, // In the Shadow of the Light
																				{214278, 1}, // Kel'Thuzad's Deep Knowledge
																				{214280, 2}, // Forbidden Rites and other Rituals Necromantic
																				{214277, 3} // The Dark Grimoire
																			};

		protected override Dictionary<uint, int> FourTombQuestObjectiveIndexes
		{
			get { return _fourTombQuestObjectiveIndexes; }
		}

		protected override uint TheFourTomesQuestId { get { return 31442; } }

		// pickup quest items.
		[ObjectHandler(214279, "In the Shadow of the Light", ObjectRange = 45)]
		[ObjectHandler(214278, "Kel'Thuzad's Deep Knowledge", ObjectRange = 45)]
		[ObjectHandler(214280, "Forbidden Rites and other Rituals Necromantic", ObjectRange = 45)]
		[ObjectHandler(214277, "The Dark Grimoire", ObjectRange = 45)]

		public override Composite TheFourTombsHandler()
		{
			WoWGameObject questObject = null;

			return new PrioritySelector(
				ctx => questObject = ctx as WoWGameObject,
				new Decorator(
					ctx => CanPickupTomb(questObject),
					new PrioritySelector(
						new Decorator(ctx => !questObject.WithinInteractRange, new Action(ctx => Navigator.MoveTo(questObject.Location))),
						new Decorator(
							ctx => questObject.WithinInteractRange,
							new Sequence(
								new Action(ctx => questObject.Interact()),
								new WaitContinue(3, ctx => LootFrame.Instance.IsVisible, new ActionAlwaysSucceed()),
								new Action(ctx => Logger.Write("Looting {0}", questObject.SafeName)),
								new Action(ctx => LootFrame.Instance.LootAll()))))));
		}
	}

	#endregion

}