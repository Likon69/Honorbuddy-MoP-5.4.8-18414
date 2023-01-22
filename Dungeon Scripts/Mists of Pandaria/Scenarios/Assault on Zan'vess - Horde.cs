
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
using Styx.CommonBot.Profiles;
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
	public class AssaultOnZanvessHorde : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 537; }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			using (StyxWoW.Memory.AcquireFrame())
			{
				units.RemoveAll(
					ret =>
					{
						var unit = ret.ToUnit();
						if (unit != null)
						{
							if (unit.Entry == TeamLeaderScooterId && Me.IsMelee() && (unit.ChanneledCastingSpellId == WhirlwindId || unit.HasAura(WhirlwindId)))
								return true;
							if (Me.IsMelee() && !unit.IsWithinMeleeRange && AvoidanceManager.Avoids.Any(a => a.IsPointInAvoid(unit.Location)))
								return true;
							if (unit.Entry == ZanthikSwarmerId)
								return true;
						}
						return false;
					});
			}
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			foreach (var obj in incomingunits)
			{
				var unit = obj as WoWUnit;
				if (unit != null) { }
			}
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var priority in units)
			{
				var unit = priority.Object as WoWUnit;
				if (unit != null)
				{
					// kill these before they overwhelm.
					if (unit.Entry == CommanderTelvrakId)
						priority.Score += 5000;
				}
			}
		}

		#endregion

		private const uint KorkronGunshipId = 67275;
		private const uint SonicControlTowerId1 = 67279;
		private const uint SonicControlTowerId2 = 67788;
		private const uint SonicControlTowerId3 = 67789;
		private const uint ZanthikGuardianId = 67710;
		private const uint ScorpidRelocatorId = 67784;
		private const int WhirlwindId = 133845;
		private const uint ZanthikSwarmerId = 67887;
		private const uint CommanderTelvrakId = 67879;

		private const int CannonFireSpellId = 133900;
		private const uint TeamLeaderScooterId = 67810;

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0)]
		public Composite RootEncounter()
		{
			AddAvoidLocation(ctx => true, 9, m => ((WoWMissile)m).ImpactPosition, () => WoWMissile.InFlightMissiles.Where(m => m.SpellId == CannonFireSpellId));
			// && m.ImpactPosition.Distance(Me.Location) < 30));
			return
				new PrioritySelector(
					new Decorator(
						ctx => ScriptHelpers.CurrentScenarioInfo.CurrentStageNumber == 4 && ScriptHelpers.IsBossAlive("Squad Leader Bosh"),
						new Action(ctx => ScriptHelpers.MarkBossAsDead("Squad Leader Bosh", "We're at stage 4"))));
		}

		private void FireBombard(WoWPoint target)
		{
			Lua.DoString("if GetSpellCooldown(133556) == 0 then CastSpellByID(133556) end");
			SpellManager.ClickRemoteLocation(target);
		}


		[LocationHandler(-1428.737, 3932.399, 6.502188, 100)]
		public Composite CreateGetInCopterBehavior()
		{
			var copterWalkToPoints = new[] { new WoWPoint(-1428.72, 3941.457, 6.502419), new WoWPoint(-1419.673, 3935.577, 6.501963) };

			return new PrioritySelector(
				ctx => // set the context to nearest empty copter
				ObjectManager.GetObjectsOfType<WoWUnit>()
							 .Where(u => u.Entry == KorkronGunshipId && Me.PartyMembers.All(p => p.TransportGuid != u.Guid))
							 .OrderBy(u => u.DistanceSqr)
							 .FirstOrDefault(),
				new Decorator<WoWUnit>(
					copter => copter.Distance < 9,
					new PrioritySelector(
						new Decorator(ctx => Me.Mounted, new ActionRunCoroutine(ctx =>CommonCoroutines.Dismount())),
						 new Decorator(ctx => Me.Shapeshift != ShapeshiftForm.Normal, new AlwaysFailAction<object>(ctx => Lua.DoString("CancelShapeshiftForm()"))),
						new Helpers.Action<WoWUnit>(copter => copter.Interact()))),
				new Decorator<WoWUnit>(copter => copter.Distance >= 9, new Helpers.Action<WoWUnit>(copter => Navigator.PlayerMover.MoveTowards(copter.Location))));
		}


		[ScenarioStage(2, "Defences of Zan'vess")]
		public Composite CreateStageTwoBehavior()
		{
			const int guidedMissile = 135546;

			return new PrioritySelector(
				ctx => ScriptHelpers.CurrentScenarioInfo.CurrentStage,
				new Decorator(
					ctx => Me.TransportGuid != 0,
					new PrioritySelector(
						new Decorator(
							ctx => // shield from incoming missiles
							WoWMissile.InFlightMissiles.Any(
								u => u.SpellId == guidedMissile && u.ImpactPosition.Distance(Me.Transport.Location) < 10 && u.Position.Distance(Me.Location) < 50),
							new Action(ctx => Lua.DoString("if GetSpellCooldown(135545) == 0 then CastSpellByID(135545) end"))),
				// Criteria 1
						new Decorator<ScenarioStage>(
							stage => !stage.GetStep(1).IsComplete,
							new PrioritySelector(
								ctx =>
								ObjectManager.GetObjectsOfType<WoWUnit>()
											 .Where(u => u.Entry == SonicControlTowerId1 && u.IsAlive && u.Distance < 200)
											 .OrderBy(u => u.DistanceSqr)
											 .FirstOrDefault(),
								new Helpers.Action<WoWUnit>(tower => FireBombard(tower.Location)))),
				// Criteria 2
						new Decorator<ScenarioStage>(
							stage => stage.GetStep(1).IsComplete && !stage.GetStep(2).IsComplete,
							new PrioritySelector(
								ctx =>
								ObjectManager.GetObjectsOfType<WoWUnit>()
											 .Where(
												 u =>
												 (u.Entry == ZanthikGuardianId && u.IsAlive) ||
												 (u.Entry == SonicControlTowerId2 && !u.HasAura("Protective Shell") && u.IsAlive) && u.Distance < 200)
											 .OrderBy(u => u.DistanceSqr)
											 .FirstOrDefault(),
								new Helpers.Action<WoWUnit>(tower => FireBombard(tower.Location)))),
				// Criteria 3
						new Decorator<ScenarioStage>(
							stage => stage.GetStep(2).IsComplete && !stage.GetStep(3).IsComplete,
							new PrioritySelector(
								ctx =>
								ObjectManager.GetObjectsOfType<WoWUnit>()
											 .Where(u => u.Entry == SonicControlTowerId3 && u.IsAlive && u.Distance < 200)
											 .OrderBy(u => u.DistanceSqr)
											 .FirstOrDefault(),
				// fire in front of target since it's moving and we're shooting a projectile.
								new Decorator<WoWUnit>(
									tower => tower.TransportGuid > 0,
									new Helpers.Action<WoWUnit>(
										tower => FireBombard(tower.Transport.Location.RayCast(WoWMathHelper.NormalizeRadian(tower.Transport.Rotation), 20)))),
				// shoot at tower now that it's on the ground.
								new Decorator<WoWUnit>(tower => tower.TransportGuid == 0, new Helpers.Action<WoWUnit>(tower => FireBombard(tower.Location))))))),
				new ActionAlwaysSucceed());
		}

		[ScenarioStage(3, "The Heart of Zan'vess")]
		[ScenarioStage(4, "")]
		public Composite CreateAirstrikeBehavior()
		{
			WoWUnit airStrikeTarget = null;

			return new PrioritySelector(
				ctx =>
				{
					var targets = ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Combat && (u.IsTargetingMeOrPet || u.IsTargetingMyPartyMember)).ToList();

					airStrikeTarget = Me.Combat && targets.Count > 3 ||
									  targets.Any(t => t.Classification == WoWUnitClassificationType.Elite && t.Entry != CommanderTelvrakId)
										  ? targets.OrderByDescending(u => targets.Count(t => u.Location.DistanceSqr(t.Location) <= 10 * 10)).FirstOrDefault()
										  : null;
					return ScriptHelpers.CurrentScenarioInfo.CurrentStage;
				},
				new Decorator(
					ctx => airStrikeTarget != null && !Lua.GetReturnVal<bool>("return ExtraActionButton1.cooldown:IsVisible()", 0),
					new Action(
						ctx =>
						{
							Lua.DoString("ExtraActionButton1:Click()");
							SpellManager.ClickRemoteLocation(airStrikeTarget.Location);
							return RunStatus.Failure;
						})));
		}

		[EncounterHandler(67810, "Team Leader Scooter")]
		public Composite TeamLeaderScooterEncounter()
		{
			const int devastatingSmashId1 = 133817;
			const int devastatingSmashId2 = 133820;
			AddAvoidObject(ctx => true, 10, u => u.Entry == TeamLeaderScooterId && (u.ToUnit().CastingSpellId == WhirlwindId || u.ToUnit().HasAura(WhirlwindId)));

			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(
					ctx => boss.CastingSpellId == devastatingSmashId1 || boss.CastingSpellId == devastatingSmashId2, ctx => boss, new ScriptHelpers.AngleSpan(0, 120)),
				new Decorator(ctx => Me.IsMelee() && Targeting.Instance.IsEmpty(), new ActionAlwaysSucceed()));
		}

		[EncounterHandler(67879, "Commander Tel'vrak")]
		public Composite CommanderTelvrakEncounter()
		{
			var impaleIds = new[] { 133942, 133943 };
			const int bombId = 133923;
			AddAvoidLocation(ctx => true, 5, m => ((WoWMissile)m).ImpactPosition, () => WoWMissile.InFlightMissiles.Where(m => m.SpellId == bombId));
			AddAvoidLocation(
				ctx => true,
				5,
				m => Me.Location.GetNearestPointOnSegment(((WoWMissile)m).Position, ((WoWMissile)m).ImpactPosition),
				() => WoWMissile.InFlightMissiles.Where(m => impaleIds.Contains(m.SpellId)));
			WoWUnit boss = null;

			return new PrioritySelector(ctx => boss = ctx as WoWUnit,
				new Decorator(ctx => SpellManager.CanCast("Heroism"), new Action(ctx => SpellManager.Cast("Heroism"))),
				new Decorator(ctx => SpellManager.CanCast("Time Warp"), new Action(ctx => SpellManager.Cast("Time Warp"))),
				new Decorator(ctx => SpellManager.CanCast("Bloodlust"), new Action(ctx => SpellManager.Cast("Bloodlust")))
				);
		}
	}
}