using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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

	#region Normal Difficulty

	public class TheSecretsOfRagefire : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 649; }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (unit != null)
					{
						if ((unit.Entry == OverseerElagloId || unit.Entry == KorkronDireSoldierId) && unit.CurrentTargetGuid == Me.Guid && Me.IsMelee() &&
							(Me.HealthPercent < 70 || unit.HasAura(DireRageAuraId) && unit.GetAuraById(DireRageAuraId).StackCount >= 4) && ScriptHelpers.Healer == null)
							return true;
						//if (unit.Entry == OverseerElagloId && Me.IsMelee() && AvoidanceManager.Avoids.Any(a => a.IsPointInAvoid(unit.Location)))
						//    return true;
					}
					return false;
				});
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			foreach (var obj in incomingunits)
			{
				WoWUnit target;
				var unit = obj as WoWUnit;
				if (unit != null)
				{
					if (unit.CurrentTargetGuid != 0 && (target = unit.CurrentTarget) != null && target.IsFriendly)
						outgoingunits.Add(unit);
					else if (unit.Entry == GlacialFreezeTotemId)
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
					if (unit.Entry == PoisonBoltTotemId)
						priority.Score += 4000;
					else if (unit.Entry == GlacialFreezeTotemId)
						priority.Score += 500000;
				}
			}
		}


		#endregion

		private const uint PoisonBoltTotemId = 71334;
		private const uint KorkronDireSoldierId = 70665;
		private const uint KorkronDarkShamanId = 71245;
		private const int DireRageAuraId = 142760;
		private const uint GlacialFreezeTotemId = 71323;

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}


		[EncounterHandler(0)]
		public async Task<bool> RootEncounter(WoWUnit unit)
		{
			var useRockJump = Targeting.Instance.TargetList.Any(
				t =>
				{
					var jumpLoc = Me.Location.RayCast(WoWMathHelper.NormalizeRadian(Me.Rotation), 25);

					return (AvoidanceManager.IsRunningOutOfAvoid
					|| jumpLoc.Distance(t.Location) <= 6)
					&& !AvoidanceManager.Avoids.Any(a => a.IsPointInAvoid(jumpLoc));
				});
			if (useRockJump)
				Lua.DoString("ExtraActionButton1:Click()");
			return false;
		}


		[ScenarioStage(1)]
		public Func<ScenarioStage, Task<bool>> StageOneEncounter()
		{
			var stage1Step1Loc = new WoWPoint(1419.482, 865.6567, 38.20304);
			var stage1Step2Loc = new WoWPoint(1433.922, 1001.52, 34.4494);
			const uint ruinedEarthId = 71262;
			const uint detonatorId = 70662;
			AddAvoidObject(ctx => true, 8, o => o.Entry == ruinedEarthId && o.ToUnit().HasAura(142310));

			return async stage =>
			{
				var stepOneComplete = stage.GetStep(1).IsComplete;
				if (!stepOneComplete)
				{
					var detonator = ObjectManager.GetObjectsOfType<WoWUnit>()
						.FirstOrDefault(o => o.Entry == detonatorId);

					if (detonator != null)
						return await ScriptHelpers.TalkToNpc(detonator);
				}

				var poiLoc = !stepOneComplete ? stage1Step1Loc : stage1Step2Loc;

				if (Me.Location.DistanceSqr(poiLoc) > 5 * 5)
				{
					ScriptHelpers.SetLeaderMoveToPoi(poiLoc);
					return false;
				}
				// wait for something to kill.
				return Me.IsLeader() && Targeting.Instance.IsEmpty();
			};
		}

		[ScenarioStage(2)]
		public Func<ScenarioStage, Task<bool>> StageTwoEncounter()
		{
			var stage2Loc = new WoWPoint(1423.636, 1021.202, 34.41135);
			const uint protoDrakeEggsId = 71031;
			const uint supplyCratesId = 70901;
			const uint pandariaArtifactsId = 71032;

			var artifactIds = new Dictionary<uint, int> { { protoDrakeEggsId, 1 }, { supplyCratesId, 2 }, { pandariaArtifactsId, 3 } };

			return async stage =>
			{
				var artifact = (from obj in ObjectManager.ObjectList.Where(o => artifactIds.ContainsKey(o.Entry))
							where !stage.GetStep(artifactIds[obj.Entry]).IsComplete
							let maxPartyToObjDist = Me.PartyMembers.Where(p => !p.IsMe)
								.Select(p => p.Location.DistanceSqr(obj.Location))
								.OrderByDescending(d => d).FirstOrDefault()
							orderby maxPartyToObjDist descending
							select obj).FirstOrDefault() as WoWUnit;

				if (artifact != null)
					return await ScriptHelpers.InteractWithObject(artifact, 1000);

				if (stage2Loc.Distance(Me.Location) > 5)
				{
					ScriptHelpers.SetLeaderMoveToPoi(stage2Loc);
					return false;
				}

				return Me.IsLeader() && Targeting.Instance.IsEmpty();
			};
		}

		[ScenarioStage(3)]
		public Func<ScenarioStage, Task<bool>> StageThreeEncounter()
		{
			var stage3Loc = new WoWPoint(1495.726, 1003.512, 38.44632);
			const uint cannonBallsId = 71176;
			const uint brokenProtoDrakeEggId = 71197;
			const uint poolPonyId = 71175;
			const uint batteryId = 71195;
			const int carrySomethingId = 141842;

			var parts = new Dictionary<uint, int> { { cannonBallsId, 2 }, { batteryId, 3 }, { poolPonyId, 4 }, { brokenProtoDrakeEggId, 5 } };

			return async stage =>
			{
				var part = (from obj in ObjectManager.ObjectList.Where(o => parts.ContainsKey(o.Entry))
						where !stage.GetStep(parts[obj.Entry]).IsComplete
						let maxPartyToObjDist = Me.PartyMembers
							.Where(p => !p.IsMe)
							.Select(p => p.Location.DistanceSqr(obj.Location))
							.OrderByDescending(d => d).FirstOrDefault()
						orderby maxPartyToObjDist descending
						select obj).FirstOrDefault() as WoWUnit;

				if (part != null && Targeting.Instance.IsEmpty())
				{
					if (!Me.HasAura(carrySomethingId))
						return await ScriptHelpers.InteractWithObject(part, 1000);
					return (await CommonCoroutines.MoveTo(stage3Loc)).IsSuccessful();
				}

				if (!stage.GetStep(1).IsComplete)
					ScriptHelpers.SetLeaderMoveToPoi(stage3Loc);
				
				return false;
			};			
		}

		private const uint OverseerElagloId = 71030;

		[ScenarioStage(4)]
		public Func<ScenarioStage, Task<bool>> StageFourEncounter()
		{
			var stage4Loc = new WoWPoint(1417.402, 1002.801, 34.00275);
			const uint shatteredEarthId = 71446;
			const uint electrifiedSpellId = 142338;
			const int demolishArmorAuraId = 142764;

			AddAvoidObject(
				ctx => ScriptHelpers.Healer == null,
				() => stage4Loc,
				40,
				o => Me.IsMoving && Me.IsRange() ? 30 : 20,
				o =>
				o.Entry == OverseerElagloId && o.ToUnit().CurrentTargetGuid == Me.Guid &&
				(Me.HealthPercent < 70 || Me.HasAura(demolishArmorAuraId) && Me.GetAuraById(demolishArmorAuraId).StackCount >= 2));

			AddAvoidObject(
				ctx => ScriptHelpers.Healer == null,
				() => stage4Loc,
				40,
				o => Me.IsMoving && Me.IsRange() ? 30 : 20,
				o =>
				o.Entry == KorkronDireSoldierId && o.ToUnit().CurrentTargetGuid == Me.Guid &&
				(Me.HealthPercent < 70 || o.ToUnit().HasAura(DireRageAuraId) && o.ToUnit().GetAuraById(DireRageAuraId).StackCount >= 4));
			AddAvoidObject(ctx => true, 6, o => o.Entry == KorkronDarkShamanId && o.ToUnit().CastingSpellId == electrifiedSpellId);
			AddAvoidObject(ctx => true, 5, shatteredEarthId);
			return async stage =>
			{
				//var boss = ObjectManager.ObjectList.FirstOrDefault(o => o.Entry == OverseerElagloId) as WoWUnit;
				var direSoldier = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == KorkronDireSoldierId && u.HasAura(DireRageAuraId));
				if (direSoldier != null &&
					await ScriptHelpers.DispelEnemy("Dire Rage", ScriptHelpers.EnemyDispelType.Enrage, direSoldier))
				{
					return true;
				}
				if (Me.IsTank() && Targeting.Instance.IsEmpty() && stage4Loc.Distance(Me.Location) > 40 && !Me.Combat)
					ScriptHelpers.SetLeaderMoveToPoi(stage4Loc);
				return false;
			};
		}
	}

	#endregion

	#region Heroic Difficulty

	public class TheSecretsOfRagefireHeroic : TheSecretsOfRagefire
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 625; }
		}

		#endregion

	}

	#endregion

}