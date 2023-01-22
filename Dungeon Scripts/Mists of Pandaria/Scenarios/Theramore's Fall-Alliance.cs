


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
	public class TheramoresFallAlliance : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 566; }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (unit != null)
					{
						if ((unit.Entry == BloodGuardGrunkId || unit.Entry == WarlordRoknahId) &&
							(unit.HasAura(BladedBastionMeleeId) && Me.IsMelee() || unit.HasAura(BladedBastionSpellId) && Me.IsRange() && Me.IsDps()))
							return true;

						if (unit.Entry == WarlordRoknahId && unit.CastingSpellId == DancingBladesId)
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

					if (unit.Entry == ViciousWyvernId && Me.IsTank() && !Me.Combat)
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
					// kill any other adds before focusing on gatekeeper.
					if (unit.Entry == GatecrusherId)
						priority.Score -= 1000;
				}
			}
		}

		#endregion

		private const uint BloodGuardGrunkId = 65154;
		private const int BladedBastionMeleeId = 114476;
		private const int BladedBastionSpellId = 114472;
		private const uint CracklingFlameId = 64688;
		private const uint ViciousWyvernId = 64957;
		private const uint StormTotemId = 64956;
		private const uint GatecrusherId = 64479;
		private const uint WarlordRoknahId = 65442;
		private const int DancingBladesId = 114449;
		private const int WhirlwindId = 15577;

		private readonly WoWPoint _stageTwoCenterLoc = new WoWPoint(-3884.259, -4603.646, 8.737935);

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0)]
		public Composite RootEncounter()
		{
			return new PrioritySelector(
				new Decorator(
				// Get off the burning ship
					ctx => ObjectManager.GetObjectsOfType<WoWUnit>().Any(u => u.Entry == CracklingFlameId && u.Distance < 10),
					new Action(ctx => Navigator.MoveTo(_stageTwoCenterLoc))),
				new Decorator(
					ctx => ScriptHelpers.CurrentScenarioInfo.CurrentStageNumber > 2 && ScriptHelpers.IsBossAlive("Blood Guard Grunk"),
					new Sequence(
						new Action(ctx => ScriptHelpers.MarkBossAsDead("Sky-Captain Dashing Dazrip", "We are pass stage 2")),
						new Action(ctx => ScriptHelpers.MarkBossAsDead("Captain Seahoof", "We are pass stage 2")),
						new Action(ctx => ScriptHelpers.MarkBossAsDead("Blood Guard Grunk", "We are pass stage 2")))),
				new Decorator(
					ctx => ScriptHelpers.CurrentScenarioInfo.CurrentStageNumber > 3 && ScriptHelpers.IsBossAlive("Gash'nul"),
					new Action(ctx => ScriptHelpers.MarkBossAsDead("Gash'nul", "We are pass stage 3"))),
				new Decorator(
					ctx => ScriptHelpers.CurrentScenarioInfo.CurrentStageNumber > 5 && ScriptHelpers.IsBossAlive("Gatecrusher"),
					new Action(ctx => ScriptHelpers.MarkBossAsDead("Gatecrusher", "We are pass stage 5"))));
		}


		[ScenarioStage(1)]
		public Composite CreateStageOneBehavior()
		{
			var stageOneLoc = new WoWPoint(-3965.194, -4684.414, 6.318526);

			return
				new PrioritySelector(
					new Decorator(ctx => !Me.Combat, new Action(ctx =>
																	{
																		Lua.DoString("if InCinematic() then StopCinematic() end");
																		return RunStatus.Failure;
																	})),
					new Decorator(
						ctx => Me.IsTank() && Me.Location.Distance(stageOneLoc) > 10 && Targeting.Instance.IsEmpty(), new Action(ctx => Navigator.MoveTo(stageOneLoc))),
					new Decorator(
						ctx => Me.IsTank() && Targeting.Instance.IsEmpty(),
						new PrioritySelector(new Decorator(ctx => !Me.Combat, RoutineManager.Current.RestBehavior), new ActionAlwaysSucceed())));
		}

		[ScenarioStage(2)]
		public Composite CreateStageTwoBehavior()
		{
			const uint leakingOilDrumId = 65571;
			ScenarioStage stage = null;
			WoWUnit oilDrum = null;

			return new PrioritySelector(
				ctx =>
				{
					stage = ScriptHelpers.CurrentScenarioInfo.CurrentStage;
					return oilDrum = ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == leakingOilDrumId && !Blacklist.Contains(u, BlacklistFlags.Loot)).OrderBy(u => u.DistanceSqr).FirstOrDefault();
				},
				// find oil drums to set boats on fire.
				new Decorator(
					ctx => !stage.GetStep(2).IsComplete && oilDrum != null && Targeting.Instance.IsEmpty(),
					new PrioritySelector(
						new Decorator(
							ctx => oilDrum.Distance > 30 && BotPoi.Current.Type == PoiType.None && Me.IsTank(), new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(oilDrum.Location))),
						new Decorator(
							ctx => oilDrum.Distance < 30 && (Me.IsTank() || !ScriptHelpers.WillPullAggroAtLocation(oilDrum.Location)),
							new PrioritySelector(
								new ActionSetActivity("Interacting with oil drum"),
								new Decorator(ctx => oilDrum.WithinInteractRange,
									new Sequence(
										new DecoratorContinue(ctx => Me.IsMoving,
											new Action(ctx => WoWMovement.MoveStop())),
										new WaitContinue(2, ctx => !Me.IsMoving, new ActionAlwaysSucceed()),
										new Action(ctx => oilDrum.Interact()),
										new WaitContinue(3, ctx => false, new ActionAlwaysSucceed()),
										new Action(ctx => Navigator.NavigationProvider.StuckHandler.Reset()),
										new Action(ctx => Blacklist.Add(oilDrum, BlacklistFlags.Loot, TimeSpan.FromSeconds(6))))),

								new Decorator(ctx => !oilDrum.WithinInteractRange, new Action(ctx => Navigator.MoveTo(oilDrum.Location))))))));
		}

		[ScenarioStage(3)]
		public Composite CreateStageThreeBehavior()
		{
			var stageThreeLoc = new WoWPoint(-3829.804, -4539.754, 9.218123);

			return
				new PrioritySelector(
					new Decorator(
						ctx => Me.IsTank() && Me.Location.Distance(stageThreeLoc) > 10 && Targeting.Instance.IsEmpty(), new Action(ctx => Navigator.MoveTo(stageThreeLoc))),
					new Decorator(ctx => Me.IsTank() && Targeting.Instance.IsEmpty(), new PrioritySelector(new Decorator(ctx => !Me.Combat, RoutineManager.Current.RestBehavior), new ActionAlwaysSucceed())));
		}

		[ScenarioStage(4)]
		public Composite CreateStageFourBehavior()
		{
			var jainaLoc = new WoWPoint(-3707.528, -4467.854, -21.20868);

			return
				new PrioritySelector(
					new Decorator(
						ctx => Me.IsTank() && Me.Location.Distance(jainaLoc) > 5 && Targeting.Instance.IsEmpty(), new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(jainaLoc))),
					new Decorator(ctx => Me.IsTank() && Targeting.Instance.IsEmpty() && BotPoi.Current.Type == PoiType.None, new PrioritySelector(new Decorator(ctx => !Me.Combat, RoutineManager.Current.RestBehavior), new ActionAlwaysSucceed())));
		}

		[ScenarioStage(5)]
		public Composite CreateStageFiveBehavior()
		{
			var stageLoc = new WoWPoint(-3757.726, -4339.882, 10.65288);
			const uint stolenStandardId = 214672;
			WoWGameObject standard = null;

			return
				new PrioritySelector(
					ctx => standard = ObjectManager.GetObjectsOfType<WoWGameObject>().Where(u => u.Entry == stolenStandardId).OrderBy(u => u.DistanceSqr).FirstOrDefault(),
					new Decorator(
				// move to general area of stage if we don't see any stolen standards.
						ctx =>
						!ScriptHelpers.CurrentScenarioInfo.CurrentStage.GetStep(2).IsComplete && Me.IsTank() && Me.Location.Distance(stageLoc) > 12 &&
						Targeting.Instance.IsEmpty() && standard == null,
						new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(stageLoc))),
					new Decorator(
						ctx => standard != null && Targeting.Instance.IsEmpty(),
						new PrioritySelector(
							new Decorator(ctx => Me.IsTank() && standard.Distance > 30, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(standard.Location))),
							new Decorator(
								ctx => standard.Distance < 30 && !ScriptHelpers.WillPullAggroAtLocation(standard.Location) && !Me.HasAura("Standard of Theramore"),
								ScriptHelpers.CreateInteractWithObject(ctx => standard, 6)))));
		}

		[ScenarioStage(6)]
		public Composite CreateFinalStageBehavior()
		{
			var jainaLoc = new WoWPoint(-3706.179, -4466.963, -21.28444);

			return
				new PrioritySelector(
					new Decorator(
						ctx => Me.IsTank() && Me.Location.Distance(jainaLoc) > 5 && Targeting.Instance.IsEmpty(),
						new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(jainaLoc))),
					new Decorator(ctx => Me.IsTank() && Targeting.Instance.IsEmpty() && BotPoi.Current.Type == PoiType.None,
						new PrioritySelector(
							new Decorator(ctx => !Me.Combat, RoutineManager.Current.RestBehavior),
							new ActionAlwaysSucceed())));
		}

		[EncounterHandler(64729, "Rok'nah Raider")]
		[EncounterHandler(65609, "Sky-Captain Dashing Dazrip")]
		[EncounterHandler(65151, "Captain Seahoof")]
		public Composite RoknahRaiderEncounter()
		{
			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				// avoid getting cleaved..
				ScriptHelpers.GetBehindUnit(ctx => unit.IsSafelyFacing(Me) && unit.Distance < 8 && unit.CurrentTargetGuid != Me.Guid, ctx => unit));
		}

		[EncounterHandler(65154, "Blood Guard Grunk")]
		public Composite BloodGuardGrunkEncounter()
		{
			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				new Decorator(ctx => unit.HasAura(BladedBastionSpellId) && Me.IsCasting && !Me.IsCastingHealingSpell, new Action(ctx => SpellManager.StopCasting())),
				new Decorator(ctx => unit.HasAura(BladedBastionMeleeId) && Me.IsMelee() && Me.IsAutoAttacking, new Action(ctx => Lua.DoString("StopAttack()"))),
				new Decorator(ctx => Targeting.Instance.IsEmpty(), new ActionAlwaysSucceed()));
		}

		[EncounterHandler(64900, "Gash'nul")]
		public Composite GashnulEncounter()
		{
			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				// dispell lightning shield...
				ScriptHelpers.CreateDispelEnemy("Lightning Shield", ScriptHelpers.EnemyDispelType.Magic, ctx => unit));
		}

		[EncounterHandler(64479, "Gatecrusher")]
		public Composite GatecrusherEncounter()
		{
			const int warEnginesSightsId = 114570;
			const uint tankTargetId = 59566;

			//AddAvoidObject(ctx => true, 6, u => u.Entry == GatecrusherId && u.ToUnit().CastingSpellId == demolisherShotId);
			AddAvoidObject(ctx => Me.HasAura(warEnginesSightsId), o => Me.IsRange() && Me.IsMoving ? 20 : 15, GatecrusherId);
			AddAvoidObject(ctx => true, 6, tankTargetId);
			return new PrioritySelector();
		}

		[EncounterHandler(65442, "Warlord Rok'nah")]
		public Composite WarlordRoknahEncounter()
		{
			WoWUnit unit = null;

			AddAvoidObject(ctx => true, 10, u => u.Entry == WarlordRoknahId && (u.ToUnit().CastingSpellId == WhirlwindId || u.ToUnit().CastingSpellId == DancingBladesId));

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