

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Frames;
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
	public class DaggerInTheDarkAlliance : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 616; }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (unit != null) { }
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
					WoWUnit currentTarget = null;
					if (unit.Combat && (currentTarget = unit.CurrentTarget) != null && currentTarget.Entry == VoljinId)
						outgoingunits.Add(unit);

					if (unit.Entry == DarkhatchedSorcererId && unit.CastingSpellId == StasicSpellId)
						outgoingunits.Add(unit);

					if (Me.TransportGuid != 0 && Me.Transport.Entry == GoblinCannonId && unit.CanSelect && unit.Attackable && unit.IsHostile && unit.Distance <= 40)
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
					if (unit.Entry == DarkhatchedSorcererId && unit.CastingSpellId == StasicSpellId && Me.TransportGuid != 0 && Me.Transport.Entry == GoblinCannonId)
						priority.Score += 1000;
				}
			}
		}

		#endregion

		private const uint VoljinId = 67414;
		private const uint DarkhatchedSorcererId = 67748;
		private const int StasicSpellId = 133548;

		private const uint GoblinCannonId = 67694;
		private const uint DeathNovaId = 133804;
		private const uint BroodmasterNoshiId = 67264;

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0)]
		public Composite RootEncounter()
		{
			const uint moguRunePowerupId = 133471;
			const int moguPowerAuraId = 133475;
			WoWAreaTrigger powerup = null;

			return new PrioritySelector(
				ctx =>
				{
					powerup = !Me.HasAura(moguPowerAuraId) || Me.GetAuraById(moguPowerAuraId).StackCount < 5
								  ? ObjectManager.GetObjectsOfType<WoWAreaTrigger>()
												 .Where(a => a.SpellId == moguRunePowerupId)
												 .OrderBy(a => a.DistanceSqr)
												 .FirstOrDefault()
								  : null;
					return powerup;
				},
				new Decorator(ctx => Me.HealthPercent < 60, HealBehavior()),
				new Decorator(
					ctx => ScriptHelpers.CurrentScenarioInfo.CurrentStageNumber >= 4 && ScriptHelpers.IsBossAlive("Darkhatched Lizard-Lord"),
					new Action(ctx => ScriptHelpers.MarkBossAsDead("Darkhatched Lizard-Lord"))),
				new Decorator(
					ctx => ScriptHelpers.CurrentScenarioInfo.CurrentStageNumber >= 7 && ScriptHelpers.IsBossAlive("Broodmaster Noshi"),
					new Action(ctx => ScriptHelpers.MarkBossAsDead("Broodmaster Noshi"))),
				// pickup powerups.
				new Decorator(
					ctx =>
					{
						if (Me.Combat || powerup == null)
							return false;
						var pathDist = Me.Location.PathDistance(powerup.Location, 30f);
						return pathDist.HasValue && pathDist.Value < 30f; 
					},
					new Action(ctx => Navigator.MoveTo(powerup.Location))),
				// use flamethrower 
				new Decorator(
					ctx =>
					(Me.HasAura("Flamethrower") || Me.HasAura("Cluster Rocket")) && !Lua.GetReturnVal<bool>("return ExtraActionButton1.cooldown:IsVisible()", 0) &&
					Targeting.Instance.TargetList.Any(t => Me.IsFacing(t) && t.Distance <= 15),
					new Action(
						ctx =>
						{
							Lua.DoString("ExtraActionButton1:Click()");
							return RunStatus.Failure;
						})),
				// use Cluster Rocket
				new Decorator(
					ctx => Me.HasAura("Cluster Rocket") && !Lua.GetReturnVal<bool>("return ExtraActionButton1.cooldown:IsVisible()", 0) && Targeting.Instance.TargetList.Any(),
					new Action(
						ctx =>
						{
							Lua.DoString("ExtraActionButton1:Click()");
							SpellManager.ClickRemoteLocation(Targeting.Instance.FirstUnit.Location);
							return RunStatus.Failure;
						})),
				// use Death Beam
				new Decorator(
					ctx =>
					Me.HasAura("Death Beam") && !Lua.GetReturnVal<bool>("return ExtraActionButton1.cooldown:IsVisible()", 0) && !Targeting.Instance.IsEmpty() &&
					Me.CurrentTargetGuid == Targeting.Instance.FirstUnit.Guid &&
					(Targeting.Instance.FirstUnit.Entry != BroodmasterNoshiId ||
					 (Targeting.Instance.FirstUnit.Entry == BroodmasterNoshiId && Targeting.Instance.FirstUnit.CurrentTargetGuid != Me.Guid)),
					new Sequence(
						new DecoratorContinue(ctx => Me.IsMoving, new Action(ctx => WoWMovement.MoveStop())),
						new WaitContinue(2, ctx => !Me.IsMoving, new ActionAlwaysSucceed()),
						new Action(ctx => Lua.DoString("ExtraActionButton1:Click()")),
						new WaitContinue(1, ctx => false, new ActionAlwaysSucceed()),
						new WaitContinue(5, ctx => !Me.IsChanneling, new ActionAlwaysSucceed())))

						);
		}

		private Composite HealBehavior()
		{
			const uint healingWardId = 133005;
			WoWUnit target = null;
			WoWObject healObject = null;

			return new Decorator(
				ctx => (healObject = ObjectManager.GetObjectsOfType<WoWAreaTrigger>()
									 .Where(a => a.SpellId == healingWardId && !AvoidanceManager.Avoids.Any(b => b.IsPointInAvoid(a.Location)))
									 .OrderBy(a => a.DistanceSqr)
									 .FirstOrDefault()) != null,
				new PrioritySelector(
					new Decorator(
						ctx => Me.Location.Distance(healObject.Location) > 3, new Action(ctx => Navigator.GetRunStatusFromMoveResult(Navigator.MoveTo(healObject.Location)))),
					new Decorator(
						ctx => Me.Location.Distance(healObject.Location) <= 3,
				// we want to stay inside the healing circle until healed.. but if we have a target within range 
				// then continue down the behavior tree to allow Combat routine to run.
						new PrioritySelector(
							new Decorator(
								ctx =>
								(target = Me.CurrentTarget) == null || (Me.IsMelee() && !target.IsWithinMeleeRange || Me.IsRange() && !Me.IsHealer() && target.Distance > 35),
								new ActionAlwaysSucceed())))));
		}

		[ScenarioStage(1, "Stage 1")]
		public Composite StageOneEncounter()
		{
			WoWUnit voljin = null;
			return new PrioritySelector(
				ctx => voljin = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == VoljinId),
				new Decorator<WoWUnit>(unit => unit.CanGossip && Me.RaidMembers.All(r => r.IsAlive), ScriptHelpers.CreateTalkToNpc(ctx => voljin)));
		}

		[ScenarioStage(3, "Stage 3")]
		public Composite StageThreeEncounter()
		{
			var stageLoc = new WoWPoint(1155.554, -119.4679, 476.1445);
			return
				new PrioritySelector(
					new Decorator(
						ctx => Me.IsTank() && Targeting.Instance.IsEmpty() && Me.Location.Distance(stageLoc) > 10, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(stageLoc))),
					new Decorator(ctx => Me.IsTank() && Targeting.Instance.IsEmpty() && Me.Location.Distance(stageLoc) <= 10, new ActionAlwaysSucceed()));
		}

		[ScenarioStage(4, "Stage 4")]
		public Composite StageFourEncounter()
		{
			const uint theSpringSaurokSlayerId = 67706;

			var boatLoc = new WoWPoint(1175.009, -113.7557, 474.3802);
			WoWUnit boat = null;

			return new PrioritySelector(
				ctx => boat = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == theSpringSaurokSlayerId),
				new Decorator(
					ctx =>
					Me.TransportGuid == 0 && Me.IsTank() && (boat == null || Me.PartyMembers.All(p => p.TransportGuid != boat.Guid || p.TransportGuid == GoblinCannonId)),
					new PrioritySelector(
						new Decorator(ctx => boat == null || !boat.WithinInteractRange, new Action(ctx => Navigator.MoveTo(boat == null ? boatLoc : boat.Location))),
						new Decorator(ctx => boat != null && boat.WithinInteractRange, new Action(ctx => boat.Interact())))),
				// boat behavior
				new Decorator(
					ctx => Me.TransportGuid != 0 && Me.Transport.Entry == GoblinCannonId,
					new PrioritySelector(
						new Decorator(
							ctx => !Targeting.Instance.IsEmpty(),
							new Action(
								ctx =>
								{
									if (Me.CurrentTargetGuid != Targeting.Instance.FirstUnit.Guid)
										Targeting.Instance.FirstUnit.Target();
									WoWMovement.ClickToMove(Targeting.Instance.FirstUnit.Location);
									for (int i = 4; i >= 1; i--)
									{
										// check if action is off cooldown.
										if (
											!Lua.GetReturnVal<bool>(
												"local id=select(8,GetPetActionInfo({0})) if GetSpellCooldown(id) == 0 then return 1 end return nil", 0))
										{
											Lua.DoString("CastPetAction({0})", i);
											if (Me.CurrentPendingCursorSpell != null)
												SpellManager.ClickRemoteLocation(Targeting.Instance.FirstUnit.Location);
											Navigator.NavigationProvider.StuckHandler.Reset();
										}
									}
								})),
						new Action(ctx => Navigator.NavigationProvider.StuckHandler.Reset()))));
		}

		[ScenarioStage(7, "Stage 7")]
		public Composite StageSevenEncounter()
		{
			const uint brokenMoguTabletWestId = 67862;
			const uint brokenMoguTabletEastId = 67863;
			var encounterLoc = new WoWPoint(1588.567, 0.8281479, 479.2451);

			WoWUnit tablet = null;
			return new PrioritySelector(
				ctx =>
				{
					var stage = ctx as ScenarioStage;
					tablet =
						ObjectManager.GetObjectsOfType<WoWUnit>()
							.FirstOrDefault(o => o.Entry == (!stage.GetStep(1).IsComplete ? brokenMoguTabletWestId : brokenMoguTabletEastId));
					return stage;
				},
				new Decorator(
					ctx => Me.IsTank() && tablet == null && Targeting.Instance.IsEmpty(),
					new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(encounterLoc))),
				new Decorator(
					ctx => tablet != null && Me.IsTank() && Targeting.Instance.IsEmpty(),
					new PrioritySelector(
						new Decorator(
							ctx => tablet.WithinInteractRange,
							new PrioritySelector(
								new Decorator(ctx => Me.Mounted, new ActionRunCoroutine(ctx => CommonCoroutines.Dismount())),
								new Action(ctx => tablet.Interact()))),
						new Action(ctx => Navigator.MoveTo(tablet.Location)))));
		}

		[EncounterHandler(67263, "Darkhatched Lizard-Lord")]
		public Composite DarkhatchedLizardLordEncounter()
		{
			WoWUnit boss = null;
			const uint stasisTrapId = 133051;
			const int waterJetsId = 133121;

			WoWAreaTrigger trap = null;

			return new PrioritySelector(
				ctx =>
				{
					trap = ObjectManager.GetObjectsOfType<WoWAreaTrigger>().Where(a => a.SpellId == stasisTrapId).OrderBy(a => a.DistanceSqr).FirstOrDefault();
					return boss = ctx as WoWUnit;
				},
				new Decorator(
					ctx => trap != null && boss.CurrentTargetGuid == Me.Guid && !AvoidanceManager.IsRunningOutOfAvoid && trap.Distance > 4,
					new Action(ctx => Navigator.MoveTo(trap.Location))),
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(ctx => boss.CastingSpellId == waterJetsId && boss.Distance <= 20, ctx => boss, new ScriptHelpers.AngleSpan(0, 120)));
		}

		[EncounterHandler(67264, "Broodmaster Noshi")]
		public Composite BroodmasterNoshiEncounter()
		{
			WoWUnit voljin = null;
			const uint broodmasterNoshiId = 67264;
			const uint stalaciteId = 67755;
			AddAvoidObject(ctx => true, 4, stalaciteId);
			var encounterLoc = new WoWPoint(1588.567, 0.8281479, 479.2451);
			AddAvoidObject(ctx => Me.HasAura("Fixate"), () => encounterLoc, 40, o => Me.IsRange() && Me.IsMoving ? 15 : 10, broodmasterNoshiId);

			WoWUnit boss = null;
			return new PrioritySelector(
				ctx =>
				{
					voljin = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == VoljinId);
					return boss = ctx as WoWUnit;
				},
				new Decorator(
					ctx => voljin != null && voljin.Distance > 15 && (boss.CastingSpellId == DeathNovaId || voljin.HasAura("Big Bad Voodoo")),
					new Action(ctx => Navigator.MoveTo(voljin.Location))),
				new Decorator(
					ctx => Me.HasAura("Big Bad Voodoo") && Me.IsMelee() && voljin.Location.Distance(boss.Location) > 10 + boss.MeleeRange(), new ActionAlwaysSucceed()));
		}

		[EncounterHandler(67266, "Rak'gor Bloodrazor")]
		public Composite RakgorBloodrazorEncounter()
		{
			WoWUnit boss = null;
			//const uint rakgorBloodrazorId = 67266;
			const uint summonGasBombId = 132992;
			AddAvoidObject(ctx => true, 8, o => o is WoWAreaTrigger && ((WoWAreaTrigger)o).SpellId == summonGasBombId);

			return new PrioritySelector(ctx => boss = ctx as WoWUnit,
				new Decorator(ctx => boss.CurrentTargetGuid == Me.Guid, HealBehavior()));
		}
	}
}