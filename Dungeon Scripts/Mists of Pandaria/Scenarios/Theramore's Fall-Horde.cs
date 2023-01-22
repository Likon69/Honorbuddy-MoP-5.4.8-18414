


using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Patchables;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
using AvoidanceManager = Bots.DungeonBuddy.Avoidance.AvoidanceManager;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Mists_of_Pandaria
{
	public class TheramoresFallHorde : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 567; }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (unit != null)
					{
						if (unit.HasAura(BladedBastionMeleeId) && Me.IsMelee() || unit.HasAura(BladedBastionSpellId) && (Me.IsDps() || Me.IsTank()))
							return true;

						if (unit.CastingSpellId == DancingBladesId)
							return true;
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
					if (unit.Entry == StormTotemId)
						outgoingunits.Add(unit);
				}
			}
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var priority in units)
			{
				var unit = priority.Object as WoWUnit;
				if (unit != null)
				{
					if (unit.Entry == StormTotemId)
						priority.Score += 5000;
				}
			}
		}

		public override void OnEnter()
		{
			_stageonePath.CycleTo(_stageonePath.First);
		}

		#endregion

		private const int BladedBastionMeleeId = 114476;
		private const int BladedBastionSpellId = 114472;
		private const uint StormTotemId = 59604;
		private const uint BigBessaId = 58787;
		private const uint HedricEvencaneId = 58840;
		private const int DancingBladesId = 114449;
		private const int WhirlwindId = 15577;

		private readonly WoWPoint _stageTwoCenterLoc = new WoWPoint(-3884.259, -4603.646, 8.737935);
		private CircularQueue<WoWPoint> _stageonePath = new CircularQueue<WoWPoint> { new WoWPoint(-3995.478, -4720.102, 4.163515), new WoWPoint(-4035.37, -4564.123, 9.630463) };

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0)]
		public Composite RootEncounter()
		{
			return
				new PrioritySelector(
					new Decorator(
						ctx => ScriptHelpers.CurrentScenarioInfo.CurrentStageNumber > 2 && ScriptHelpers.IsBossAlive("Lieutenant Granders"),
						new Sequence(
							new Action(ctx => ScriptHelpers.MarkBossAsDead("Lieutenant Granders", "We are pass stage 1")),
							new Action(ctx => ScriptHelpers.MarkBossAsDead("Squallshaper Lanara", "We are pass stage 1")),
							new Action(ctx => ScriptHelpers.MarkBossAsDead("Captain Dashing", "We are pass stage 1")))),
					new Decorator(
						ctx => ScriptHelpers.CurrentScenarioInfo.CurrentStageNumber > 3 && ScriptHelpers.IsBossAlive("Baldruc"),
						new Action(ctx => ScriptHelpers.MarkBossAsDead("Baldruc", "We are pass stage 3"))),
					new Decorator(
						ctx => ScriptHelpers.CurrentScenarioInfo.CurrentStageNumber > 5 && ScriptHelpers.IsBossAlive("Big Bessa"),
						new Action(ctx => ScriptHelpers.MarkBossAsDead("Big Bessa", "We are pass stage 4"))));
		}


		[ScenarioStage(1)]
		public Composite CreateStageOneBehavior()
		{
			const uint rigThisPowderBarrel = 58665;
			ScenarioStage stage = null;
			WoWUnit powderBarrel = null;

			return new PrioritySelector(
				ctx =>
				{
					stage = ScriptHelpers.CurrentScenarioInfo.CurrentStage;
					return powderBarrel = (from unit in  ObjectManager.GetObjectsOfType<WoWUnit>()
										 where unit.Entry == rigThisPowderBarrel && !Blacklist.Contains(unit, BlacklistFlags.Loot)
										 let pathDist = Me.Location.PathDistance(unit.Location)
										 where pathDist.HasValue
										 orderby pathDist.Value
										 select unit).FirstOrDefault();
				},
				// find oil drums to set boats on fire.
				new Decorator(
					ctx => !stage.GetStep(1).IsComplete && powderBarrel != null && Targeting.Instance.IsEmpty(),
					new PrioritySelector(
						new Decorator(
							ctx => powderBarrel.Distance > 30 && BotPoi.Current.Type == PoiType.None && Me.IsTank(),
							new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(powderBarrel.Location))),
						new Decorator(
							ctx => powderBarrel.Distance < 30 && (Me.IsTank() || !ScriptHelpers.WillPullAggroAtLocation(powderBarrel.Location)),
							new PrioritySelector(
								new ActionSetActivity("Interacting with powder barrel"),
								new Decorator(
									ctx => powderBarrel.WithinInteractRange,
									new Sequence(
										new DecoratorContinue(ctx => Me.IsMoving, new Action(ctx => WoWMovement.MoveStop())),
										new WaitContinue(2, ctx => !Me.IsMoving, new ActionAlwaysSucceed()),
										new Action(ctx => powderBarrel.Interact()),
										new WaitContinue(3, ctx => false, new ActionAlwaysSucceed()),
										new Action(ctx => Navigator.NavigationProvider.StuckHandler.Reset()),
										new Action(ctx => Blacklist.Add(powderBarrel, BlacklistFlags.Loot, TimeSpan.FromSeconds(6))))),
								new Decorator(ctx => !powderBarrel.WithinInteractRange, new Action(ctx => Navigator.MoveTo(powderBarrel.Location))))))),
				new Decorator(
					ctx => powderBarrel == null,
					new PrioritySelector(
						new Decorator(ctx => Me.Location.Distance(_stageonePath.Peek()) <= 5, new Action(ctx => _stageonePath.Dequeue())),
						new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(_stageonePath.Peek())))));
		}

		[ScenarioStage(2)]
		public Composite CreateStageTwoBehavior()
		{
			var stageTwoLoc = new WoWPoint(-3878.851, -4587.707, 8.662436);
			const uint blastmasterSparkfuseId = 58765;
			WoWUnit blastMasterSparkFuse = null;

			return new PrioritySelector(
				ctx => blastMasterSparkFuse = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == blastmasterSparkfuseId),
				new Decorator(ctx => blastMasterSparkFuse == null && Me.IsTank() && Targeting.Instance.IsEmpty(), new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(stageTwoLoc))),
				new Decorator(ctx => blastMasterSparkFuse != null && Me.IsTank() && Targeting.Instance.IsEmpty(), ScriptHelpers.CreateTalkToNpc(ctx => blastMasterSparkFuse)));
		}


		[ScenarioStage(4)]
		public Composite CreateStageFourBehavior()
		{
			const uint unmannedTankId = 58788;
			var stageLoc = new WoWPoint(-3767.367, -4304.536, 9.020147);
			WoWUnit unmannedTank = null;

			AddAvoidObject(ctx => true, 15, o => o.Entry == unmannedTankId && o.ToUnit().HasAura("Tank Pulse"));

			return
				new PrioritySelector(
					ctx => unmannedTank = ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == unmannedTankId).OrderBy(u => u.DistanceSqr).FirstOrDefault(),
					new Decorator(
				// move to general area of stage if we don't see any stolen standards.
						ctx =>
						!ScriptHelpers.CurrentScenarioInfo.CurrentStage.GetStep(2).IsComplete && Me.IsTank() && Me.Location.Distance(stageLoc) > 12 &&
						Targeting.Instance.IsEmpty() && unmannedTank == null,
						new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(stageLoc))),
					new Decorator(
						ctx => unmannedTank != null && Targeting.Instance.IsEmpty(),
						new PrioritySelector(
							new Decorator(ctx => Me.IsTank() && unmannedTank.Distance >= 30, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(unmannedTank.Location))),
							new Decorator(
								ctx => unmannedTank.Distance < 30 && (!ScriptHelpers.WillPullAggroAtLocation(unmannedTank.Location) || Me.IsTank()),
								ScriptHelpers.CreateInteractWithObject(ctx => unmannedTank, 6)))));
		}

		[ScenarioStage(5)]
		public Composite CreateFinalStageBehavior()
		{
			var hordeSpyLoc = new WoWPoint(-3725.615, -4555.952, 4.74253);
			const uint arcaneShacklesKeyId = 79261;

			const uint thalenSongweaverId = 58816;
			WoWUnit thalenSongweaver = null;
			WoWUnit hedricEvencane = null;
			ScenarioStage stage = null;
			return new PrioritySelector(
				ctx =>
				{
					hedricEvencane = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == HedricEvencaneId);
					thalenSongweaver = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == thalenSongweaverId);
					return stage = ctx as ScenarioStage;
				},
				new Decorator(
					ctx => hedricEvencane != null && hedricEvencane.IsDead && hedricEvencane.CanLoot && !stage.GetStep(2).IsComplete,
					new PrioritySelector(
						new Decorator(ctx => !hedricEvencane.WithinInteractRange, new Action(ctx => Navigator.MoveTo(hedricEvencane.Location))),
						new Decorator(ctx => hedricEvencane.WithinInteractRange, new Action(ctx => hedricEvencane.Interact())))),
				new Decorator(
					ctx => Me.BagItems.Any(i => i != null && i.Entry == arcaneShacklesKeyId),
					new PrioritySelector(
						new Decorator(ctx => thalenSongweaver == null && Targeting.Instance.IsEmpty(), new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(hordeSpyLoc))),
						new Decorator(ctx => thalenSongweaver != null && Targeting.Instance.IsEmpty(), ScriptHelpers.CreateTalkToNpc(ctx => thalenSongweaver)))));
		}

		[EncounterHandler(59089, "Captain Dashing")]
		public Composite CaptainDashingEncounter()
		{
			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(
					ctx => unit.IsSafelyFacing(Me) && unit.Distance < 8 && unit.CurrentTargetGuid != Me.Guid && unit.Distance <= 8,
					ctx => unit,
					new ScriptHelpers.AngleSpan(0, 180)));
		}

		[EncounterHandler(58936, "Lieutenant Granders")]
		public Composite LieutenantGrandersEncounter()
		{
			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				new Decorator(ctx => unit.HasAura(BladedBastionSpellId) && Me.IsCasting && !Me.IsCastingHealingSpell, new Action(ctx => SpellManager.StopCasting())),
				new Decorator(ctx => unit.HasAura(BladedBastionMeleeId) && Me.IsMelee() && Me.IsAutoAttacking, new Action(ctx => Lua.DoString("StopAttack()"))),
				new Decorator(ctx => Targeting.Instance.IsEmpty(), new ActionAlwaysSucceed()));
		}

		[EncounterHandler(58948, "Squallshaper Lanara")]
		public Composite SquallshaperLanaraEncounter()
		{
			//Blizzard - Entry: 79860, Radius: 8
			// blizzard doesn't do much damage so we just ignore it.
			WoWUnit unit = null;
			return new PrioritySelector(ctx => unit = ctx as WoWUnit);
		}

		[EncounterHandler(58777, "Baldruc")]
		public Composite BaldruclEncounter()
		{
			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				// dispell lightning shield...
				ScriptHelpers.CreateDispelEnemy("Lightning Shield", ScriptHelpers.EnemyDispelType.Magic, ctx => unit));
		}

		[EncounterHandler(59654, "Knight of Theramore")]
		public Composite KnightofTheramoreEncounter()
		{
			WoWUnit unit = null;
			const int HealOtherId = 33910;
			return new PrioritySelector(ctx => unit = ctx as WoWUnit, ScriptHelpers.CreateInterruptCast(ctx => unit, HealOtherId));
		}

		[EncounterHandler(58787, "Big Bessa")]
		public Composite BigBessaEncounter()
		{
			const int warEnginesSightsId = 114570;
			const int bigBessasCannonId = 114565;
			const uint tankTargetId = 59566;

			AddAvoidObject(ctx => true, 6, u => u.Entry == BigBessaId && u.ToUnit().CastingSpellId == bigBessasCannonId);
			AddAvoidObject(ctx => Me.HasAura(warEnginesSightsId), o => Me.IsRange() && Me.IsMoving ? 20 : 15, BigBessaId);
			AddAvoidObject(ctx => true, 6, tankTargetId);
			return new PrioritySelector();
		}

		[EncounterHandler(58840, "Hedric Evencane")]
		[EncounterHandler(59088, "Captain Tellern")]
		public Composite HedricEvencaneEncounter()
		{
			WoWUnit unit = null;

			AddAvoidObject(ctx => true, 8, u => u is WoWUnit && (u.ToUnit().CastingSpellId == WhirlwindId || u.ToUnit().CastingSpellId == DancingBladesId));

			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				// avoid getting cleaved..
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(
					ctx => unit.IsSafelyFacing(Me) && unit.Distance < 8 && unit.CurrentTargetGuid != Me.Guid && unit.Distance <= 8,
					ctx => unit,
					new ScriptHelpers.AngleSpan(0, 180)),
				new Decorator(ctx => unit.HasAura(BladedBastionSpellId) && Me.IsCasting && !Me.IsCastingHealingSpell, new Action(ctx => SpellManager.StopCasting())),
				new Decorator(
					ctx => (unit.HasAura(BladedBastionMeleeId) || unit.HasAura(BladedBastionSpellId)) && Me.IsAutoAttacking, new Action(ctx => Lua.DoString("StopAttack()"))),
				new Decorator(ctx => Targeting.Instance.IsEmpty(), new ActionAlwaysSucceed()));
		}
	}
}