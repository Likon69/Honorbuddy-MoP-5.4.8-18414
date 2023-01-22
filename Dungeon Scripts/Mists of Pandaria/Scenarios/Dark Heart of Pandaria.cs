

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
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

	public class DarkHeartOfPandaria : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 647; }
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
					if ((unit.Entry == FieryAngerId || unit.Entry == EarthbornHatredId) && !Me.Combat && Me.IsTank() &&
						ScriptHelpers.CurrentScenarioInfo.CurrentStageNumber == 2)
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
					// kill these to lower the shield on boss.
					if ((unit.Entry == FieryAngerId || unit.Entry == EarthbornHatredId) &&
						ScriptHelpers.CurrentScenarioInfo.CurrentStageNumber == 2)
						priority.Score += 50000;
						// give lower score so trash that spawn are focused.
					else if (unit.Entry == EchoofYShaarjId)
						priority.Score -= 50000;
				}
			}
		}

		#endregion

		private const uint GrizzleGearslipId = 70956;

		private const uint UrthargestheDestroyerId = 70959;
		private const uint FieryAngerId = 70824;
		private const uint EarthbornHatredId = 70822;

		private const uint CrateofArtifactsId = 70797;
		private const uint ScrollArtifactId = 70792;
		private const uint BookArtifactId = 70791;
		private const uint VaseArtifactId = 70788;
		private const uint CraftytheAmbitiousId = 71358;
		private const uint ControllerId = 71280;

		private readonly uint[] _artifactIds = new[] {CrateofArtifactsId, ScrollArtifactId, BookArtifactId, VaseArtifactId};

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
						ctx => ScriptHelpers.IsBossAlive("Urtharges the Destroyer") && ScriptHelpers.CurrentScenarioInfo.CurrentStageNumber > 2,
						new Action(ctx => ScriptHelpers.MarkBossAsDead("Urtharges the Destroyer"))));
		}

		[ScenarioStage(1)]
		public Composite StageOneEncounter()
		{
			var stage1Loc = new WoWPoint(1117.971, 902.6179, 404.101);
			WoWUnit gearSlip = null;
			return new PrioritySelector(
				ctx => gearSlip = ObjectManager.ObjectList.FirstOrDefault(o => o.Entry == GrizzleGearslipId) as WoWUnit,
				new Decorator(
					ctx => gearSlip != null,
					new PrioritySelector(
						new Decorator(ctx => gearSlip.WithinInteractRange, new Action(ctx => gearSlip.Interact())),
						new Decorator(ctx => !gearSlip.WithinInteractRange, new Action(ctx => Navigator.MoveTo(gearSlip.Location))))),
				new Action(ctx => Navigator.MoveTo(stage1Loc)));
		}

		private readonly CircularQueue<WoWPoint> _stage2And3Locs = new CircularQueue<WoWPoint>
																	{
																		new WoWPoint(995.8815, 872.9709, 378.317),
																		new WoWPoint(1047.324, 819.8501, 379.4671),
																		new WoWPoint(1186.142, 814.579, 378.0941),
																	};

		[ScenarioStage(2)]
		public Composite StageTwoEncounter()
		{
			const uint stoneRainId = 142139;
			AddAvoidObject(ctx => true, 8, stoneRainId);
			WoWUnit killTarget = null;
			return new PrioritySelector(
				ctx =>
				{
					killTarget =
						ObjectManager.GetObjectsOfType<WoWUnit>()
							.Where(o => o.Entry == FieryAngerId)
							.OrderBy(o => o.DistanceSqr)
							.FirstOrDefault();
					return ctx;
				},
				new Decorator(
					ctx => Me.IsTank() && killTarget != null && !Me.Combat,
					new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(killTarget.Location))),
				new Decorator(
					ctx => Targeting.Instance.IsEmpty() && Me.IsTank(),
					new PrioritySelector(
						new Decorator(
							ctx => Me.Location.Distance2DSqr(_stage2And3Locs.Peek()) <= 4*4,
							new Action(ctx => _stage2And3Locs.Dequeue())),
						new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(_stage2And3Locs.Peek())))));
		}

		[ScenarioStage(3)]
		public async Task<bool> StageThreeEncounter(ScenarioStage stage)
		{
			var myLoc = Me.Location;
			var artifact = (from obj in ObjectManager.ObjectList.Where(o => _artifactIds.Contains(o.Entry))
				where
					!Me.PartyMembers.Any(
						p =>
							!p.IsMe && p.IsSafelyFacing(obj, 45)
							&& p.Location.DistanceSqr(obj.Location) < myLoc.DistanceSqr(obj.Location))
				orderby obj.DistanceSqr
				select obj).FirstOrDefault();

			var leader = ScriptHelpers.Leader;

			if (artifact != null && Targeting.Instance.IsEmpty())
			{
				var artifactDistSqr = artifact.DistanceSqr;
				if (leader != null && leader.IsMe && artifactDistSqr >= 4*4)
				{
					ScriptHelpers.SetLeaderMoveToPoi(artifact.Location);
					return false;
				}
				if (leader != null && (!leader.IsMe && leader.Location.DistanceSqr(artifact.Location) < 40*40
										&& !ScriptHelpers.WillPullAggroAtLocation(artifact.Location)
										|| leader.IsMe && artifactDistSqr < 4*4))
				{
					await CommonCoroutines.MoveTo(artifact.Location, artifact.SafeName);
					return true;
				}
			}

			if (artifact == null && leader != null && leader.IsMe)
			{
				if (Me.Location.Distance2DSqr(_stage2And3Locs.Peek()) <= 4*4)
					_stage2And3Locs.Dequeue();
				ScriptHelpers.SetLeaderMoveToPoi(_stage2And3Locs.Peek());
			}
			return false;
		}

		[ScenarioStage(4)]
		public Composite StageFourEncounter()
		{
			var stage4Locs = new WoWPoint(863.4757, 1089.217, 359.6512);
			WoWUnit crafty = null;
			return new PrioritySelector(
				ctx => crafty = ObjectManager.ObjectList.FirstOrDefault(o => o.Entry == CraftytheAmbitiousId) as WoWUnit,
				new Decorator(
					ctx => crafty != null && Targeting.Instance.IsEmpty() && (Me.IsTank() || crafty.Distance < 40),
					ScriptHelpers.CreateTalkToNpc(ctx => crafty)),
				new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(stage4Locs)));
		}

		private const uint EchoofYShaarjId = 71123;

		[ScenarioStage(5)]
		public Func<ScenarioStage, Task<bool>> StageFiveEncounter()
		{
			const uint malevolentForceId = 71302;
			var healingSphereIds = new[] {141720, 141702, 141724, 141718};
			const int badstuffId = 142155;
			AddAvoidObject(ctx => true, 10, o => o is WoWAreaTrigger && ((WoWAreaTrigger) o).SpellId == badstuffId);
			AddAvoidObject(ctx => true, 8, malevolentForceId);

			var stage5Loc = new WoWPoint(818.1581, 1104.932, 357.1084);
			return async stage =>
						{

							if (await ScriptHelpers.DispelGroup("Veil of Darkness", ScriptHelpers.PartyDispelType.Curse))
								return true;

							var healingSphere =
								ObjectManager.ObjectList.Where(o => o is WoWAreaTrigger && healingSphereIds.Contains(((WoWAreaTrigger) o).SpellId))
									.OrderBy(o => o.Distance2DSqr)
									.FirstOrDefault();


							if (Me.HealthPercent <= 60 && healingSphere != null
								&& (await CommonCoroutines.MoveTo(healingSphere.Location)).IsSuccessful())
							{
								return true;
							}

							var boss = ObjectManager.ObjectList.FirstOrDefault(o => o.Entry == EchoofYShaarjId) as WoWUnit;


							if (Targeting.Instance.IsEmpty() && Me.IsLeader())
							{
								var bossLoc = boss != null ? boss.Location : stage5Loc;
								if (bossLoc.DistanceSqr(Me.Location) > 10*10)
									ScriptHelpers.SetLeaderMoveToPoiPS(bossLoc);
								else // do nothing while waiting for boss to spawn.
									return true;
							}
							return false;
						};
		}

	}

	#endregion

	#region Heroic Difficulty

	public class DarkHeartOfPandariaHeroic : DarkHeartOfPandaria
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 624; }
		}

		#endregion

	}

	#endregion

}