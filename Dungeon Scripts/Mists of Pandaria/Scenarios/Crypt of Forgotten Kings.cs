
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
using AvoidanceManager = Bots.DungeonBuddy.Avoidance.AvoidanceManager;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Mists_of_Pandaria
{

	#region Normal Difficulty

	public class CryptOfForgottenKings : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 504; }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (unit != null)
					{
						if (unit.Entry == CryptGuardianFuryId && (unit.CastingSpellId == BladestormId || unit.HasAura(BladestormId)) && Me.IsMelee())
							return true;
						if (unit.Entry == AbominationOfAngerId && unit.ChanneledCastingSpellId == DeathforceId && Me.IsMelee())
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
					if (unit.Entry == JinIronfistId && Me.IsTank() && unit.Distance < 40)
						outgoingunits.Add(unit);

					// if (unit.Entry == ShadowsofAngerId && unit.Distance <= 40)
					//      outgoingunits.Add(unit);

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
					if (unit.Entry == ShadowsofAngerId)
						priority.Score += 1000;
				}
			}
		}

		#endregion

		#region Root

		private const uint CryptGuardianFuryId = 61783;
		private const uint AbominationOfAngerId = 61707;
		private const int DeathforceId = 120215;
		private const int BladestormId = 128969;
		private const uint ShadowsofAngerId = 62009;

		private const uint JinIronfistId = 61814;

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		private readonly WoWPoint[] _trapBlackspots = {new WoWPoint(593.493, 2323.363, 67.38953)};

		[EncounterHandler(0)]
		public Func<WoWUnit, Task<bool>> RootEncounter()
		{
			const uint lightningTrapId = 211842;
			const uint fireTrapId = 211841;
			const uint stasisTrapId = 211709;
			const uint statisTrap2Id = 211843;
			const uint activationTrapId = 211995;

			var traps = new[] {fireTrapId, lightningTrapId, stasisTrapId, activationTrapId, statisTrap2Id};

			AddAvoidObject(
				ctx => true,
				4,
				u =>
					u is WoWGameObject && traps.Contains(u.Entry) && u.ZDiff < 10 && u.Distance2D < 10 &&
					!_trapBlackspots.Any(l => l.DistanceSqr(u.Location) < 9));

			return async npc =>
			{
				if (ScriptHelpers.CurrentScenarioInfo.CurrentStageNumber > 1 && ScriptHelpers.IsBossAlive("Jin Ironfist"))
					ScriptHelpers.MarkBossAsDead("Jin Ironfist");

				return false;
			};
		}


		#endregion

		#region Stage One
		[EncounterHandler(61814, "Jin Ironfist")]
		public async Task<bool> JinIronfistEncounter(WoWUnit boss)
		{
			return await ScriptHelpers.DispelEnemy("Enrage", ScriptHelpers.EnemyDispelType.Enrage, boss);
		}

		#endregion

		#region Stage Two

		readonly WoWPoint _stageTwoLoc = new WoWPoint(698.6362, 2467.242, 73.90067);

		[ScenarioStage(2, "Stage 2")]
		public async Task<bool> StageTwoLogic(ScenarioStage stage)
		{
			if (Me.Location.DistanceSqr(_stageTwoLoc) > 50*50)
				ScriptHelpers.SetLeaderMoveToPoi(_stageTwoLoc);
			else
				await ScriptHelpers.ClearArea(_stageTwoLoc, 50);
			return false;
		}

		#endregion

		#region Stage 3

		readonly WoWPoint _stageThreeLoc = new WoWPoint(687.541, 2355.474, 45.03357);
		[ScenarioStage(3, "Stage 3")]
		public async Task<bool> StageThreeLogic(ScenarioStage stage)
		{
			ScriptHelpers.SetLeaderMoveToPoi(_stageThreeLoc);
			return false;
		}

		[EncounterHandler(61766, "Crypt Guardian")]
		[EncounterHandler(61783, "Crypt Guardian")]
		public Func<WoWUnit, Task<bool>> CryptGuardianEncounter()
		{
			const int guardianStrikeId = 119843;

			AddAvoidObject(
				ctx => true,
				12,
				u => u.Entry == CryptGuardianFuryId && (u.ToUnit().CastingSpellId == BladestormId || u.ToUnit().HasAura(BladestormId)));
			var frontalSpan = new ScriptHelpers.AngleSpan(0, 180);

			return async npc => npc.CastingSpellId == guardianStrikeId
								&& npc.IsSafelyFacing(Me) && npc.Distance <= 9
								&& await ScriptHelpers.AvoidUnitAngles(npc, frontalSpan);
		}

		#endregion

		#region Final Stage;

		private readonly WoWPoint _finalStageLoc = new WoWPoint(749.9462, 2353.526, 52.72215);

		[ScenarioStage(4, "Final Stage")]
		public async Task<bool> FinalStageLogic(ScenarioStage stage)
		{
			ScriptHelpers.SetLeaderMoveToPoi(_finalStageLoc);
			return false;
		}

		[EncounterHandler(61707, "Abomination of Anger")]
		public Func<WoWUnit, Task<bool>> AbominationofAngerEncounter()
		{
			const uint cloudOfAngerId = 62055;
			const int breathOfHate = 120929;

			AddAvoidObject(ctx => true, 6, cloudOfAngerId);
			AddAvoidObject(
				ctx => true,
				18,
				u => u.Entry == AbominationOfAngerId && u.ToUnit().ChanneledCastingSpellId == DeathforceId);
			var frontalCone = new ScriptHelpers.AngleSpan(0, 150);

			return async boss => boss.Distance < 15
								&& boss.ChanneledCastingSpellId == breathOfHate
								&& boss.CurrentTargetGuid != Me.Guid
								&& await ScriptHelpers.AvoidUnitAngles(boss, frontalCone);
		}

		#endregion

	}

	#endregion

	#region Heroic Difficulty

	public class CryptOfForgottenKingsHeroic : CryptOfForgottenKings
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 648; }
		}

		#endregion
	}

	#endregion

}