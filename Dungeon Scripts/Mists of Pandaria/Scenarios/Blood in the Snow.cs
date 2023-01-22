using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Patchables;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.WoWInternals.World;
using Action = Styx.TreeSharp.Action;

using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
using AvoidanceManager = Bots.DungeonBuddy.Avoidance.AvoidanceManager;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Mists_of_Pandaria
{

	#region Normal Difficulty

	public class BloodInTheSnow : Dungeon
	{
		#region Overrides of Dungeon

		private readonly WoWPoint _villageLoc = new WoWPoint(-5170.316, -253.5313, 434.8963);
		private DynamicBlackspot _village;

		public override uint DungeonId
		{
			get { return 646; }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (unit != null)
					{
						if (!unit.Combat && ScriptHelpers.CurrentScenarioInfo.CurrentStageNumber > 2 && ScriptHelpers.CurrentScenarioInfo.CurrentStageNumber < 6 &&
							unit.Location.Distance(_villageLoc) < 60)
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
					if (unit.Entry == ZandalariRageBannerId && unit.Distance < 30)
						outgoingunits.Add(unit);
					else if (unit.Entry == SolidIceId)
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
					if (unit.Entry == SolidIceId)
						priority.Score += 4000;
					// kill trash before boss.
					else if (unit.Entry == HekimatheWiseId || unit.Entry == BonechillerBarafuId)
						priority.Score -= 4000;
				}
			}
		}

		public override void OnEnter()
		{
			_village = new DynamicBlackspot(
				() => ScriptHelpers.CurrentScenarioInfo.CurrentStageNumber > 3 && ScriptHelpers.CurrentScenarioInfo.CurrentStageNumber < 6,
				() => _villageLoc,
				LfgDungeon.MapId,
				50,
				30,
				"Center Village");
			DynamicBlackspotManager.AddBlackspot(_village);
		}

		public override void OnExit()
		{
			DynamicBlackspotManager.RemoveBlackspot(_village);
			_village = null;
		}

		#endregion

		private const uint ZandalariRageBannerId = 70789;
		private const uint ShimmerweedBasketId = 71440;
		private const uint BonechillerBarafuId = 70468;
		private const uint SolidIceId = 70987;

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0)]
		public Composite RootEncounter()
		{
			return new PrioritySelector(HealBehavior(), CallTheShotBehavior());
		}

		private Composite CallTheShotBehavior()
		{
			const int callTheShotSpellId = 141222;
			WoWUnit target = null;
			return
				new Decorator(
					ctx =>
					Me.HasAura(callTheShotSpellId) && !Me.Combat &&
					(target = Targeting.Instance.TargetList.FirstOrDefault(t => t.Distance <= 40 && t.Classification == WoWUnitClassificationType.Elite)) != null,
					new PrioritySelector(
						new Decorator(ctx => Me.CurrentTargetGuid != target.Guid, new Action(ctx => target.Target())),
						new Action(
							ctx =>
							{
								Lua.DoString("ExtraActionButton1:Click()");
								return RunStatus.Failure;
							})));
		}

		private Composite HealBehavior()
		{
			WoWUnit target;
			WoWObject healObject = null;

			return
				new Decorator(
					ctx =>
					Me.HealthPercent < 60 &&
					(healObject = ObjectManager.ObjectList.Where(o => o.Entry == ShimmerweedBasketId && o.Distance <= 40).OrderBy(o => o.DistanceSqr).FirstOrDefault()) != null,
					new PrioritySelector(
						new Decorator(
							ctx => Me.Location.Distance(healObject.Location) > 2,
							new Action(ctx => Navigator.GetRunStatusFromMoveResult(Navigator.MoveTo(healObject.Location)))),
						new Decorator(
							ctx => Me.Location.Distance(healObject.Location) <= 2,
				// we want to stay inside the healing circle until healed.. but if we have a target within range 
				// then continue down the behavior tree to allow Combat routine to run.
							new PrioritySelector(
								new Decorator(
									ctx =>
									(target = Me.CurrentTarget) == null ||
									(Me.IsMelee() && !target.IsWithinMeleeRange || Me.IsRange() && !Me.IsHealer() && target.Distance > 35),
									new ActionAlwaysSucceed())))));
		}

		[ScenarioStage(1)]
		public Composite StageOneEncounter()
		{
			var stage1Loc = new WoWPoint(-5282.057, -292.9977, 434.8955);
			return new Action(ctx => Navigator.MoveTo(stage1Loc));
		}

		[ScenarioStage(2)]
		public Composite StageTwoEncounter()
		{
			var stage2Loc = new WoWPoint(-5235.345, -263.2425, 434.896);
			const uint roastingSpitId = 70597;
			WoWUnit roastingSpit = null;
			var roastSpitLoc = new WoWPoint(-5240.908, -268.1139, 434.896);

			return new PrioritySelector(
				ctx => roastingSpit = ObjectManager.ObjectList.FirstOrDefault(o => o.Entry == roastingSpitId) as WoWUnit,
				new Decorator(
					ctx => Targeting.Instance.IsEmpty() && Me.Location.Distance(stage2Loc) <= 20 && roastingSpit != null,
					new PrioritySelector(
						new Decorator(ctx => roastingSpit.WithinInteractRange, new Action(ctx => roastingSpit.Interact())),
						new Decorator(ctx => !roastingSpit.WithinInteractRange, new Action(ctx => Navigator.MoveTo(roastSpitLoc))))),
				new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(stage2Loc)));
		}

		[ScenarioStage(3)]
		public Composite StageThreeEncounter()
		{
			var stage3Loc = new WoWPoint(-5336.379, -232.2262, 440.3641);

			return new PrioritySelector(new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(stage3Loc)));
		}

		[ScenarioStage(4)]
		public Composite StageFourEncounter()
		{
			const uint frostbiteRuneId = 34872;
			const uint bonechillingBlizzardId = 141428;

			// AddAvoidObject(ctx => true, 6, frostbiteRuneId);
			AddAvoidObject(ctx => true, 8, bonechillingBlizzardId, frostbiteRuneId);

			var stage4Loc = new WoWPoint(-5270.151, -125.255, 434.676);

			return new PrioritySelector(new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(stage4Loc)));
		}

		[ScenarioStage(5)]
		public Composite StageFiveEncounter()
		{
			var stage5Loc = new WoWPoint(-5031.635, -218.9545, 444.7651);
			const uint iceSpikeId = 70988;
			AddAvoidObject(ctx => true, 3, iceSpikeId);
			return new PrioritySelector(new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(stage5Loc)));
		}

		private const uint HekimatheWiseId = 70544;

		[ScenarioStage(6)]
		public Composite StageSixEncounter()
		{
			var stage6Loc = new WoWPoint(-5170.944, -246.8854, 434.8965);
			const uint hekimasScornId = 133837;
			const uint hekimasScornMissileId = 142623;

			AddAvoidLocation(ctx => true, 3, o => ((WoWMissile)o).ImpactPosition, () => WoWMissile.InFlightMissiles.Where(m => m.SpellId == hekimasScornMissileId));
			AddAvoidObject(ctx => true, 3, hekimasScornId);

			return new PrioritySelector(new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(stage6Loc)));
		}
	}

	#endregion

	#region Heroic Difficulty

	public class BloodInTheSnowHeroic : BloodInTheSnow
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 637; }
		}

		#endregion

	}

	#endregion

}