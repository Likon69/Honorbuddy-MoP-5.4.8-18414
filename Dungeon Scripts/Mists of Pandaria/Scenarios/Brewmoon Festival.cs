
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
using Styx.CommonBot.POI;
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
	public class BrewmoonFestival : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 539; }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (unit != null)
					{
						if (unit.Entry == DenMotherMoofId && unit.CastingSpellId == TwirlwindId && Me.IsMelee())
							return true;

						if (unit.Entry == LiTeId && unit.HasAura("Water Shell") && !Me.IsHealer())
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
					if (unit.Combat && Me.IsTank() && unit.Distance <= 50)
						outgoingunits.Add(unit);
				}
			}
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var priority in units)
			{
				var unit = priority.Object as WoWUnit;
				if (unit != null) { }
			}
		}

		#endregion

		private const uint HungryVirmenId = 62807;
		private const int TwirlwindId = 124378;
		private const uint DenMotherMoofId = 63518;
		private const uint LiTeId = 63520;
		private readonly WoWPoint _villageCenterLoc = new WoWPoint(1768.085, 341.058, 478.1584);
		private WoWPoint _denMotherSpawnLoc = new WoWPoint(1767.374, 388.2354, 481.6245);

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
						ctx =>
						ScriptHelpers.IsBossAlive("Karsar the Bloodletter") &&
						(ScriptHelpers.CurrentScenarioInfo.CurrentStageNumber == 1 && ScriptHelpers.CurrentScenarioInfo.CurrentStage.GetStep(1).IsComplete ||
						 ScriptHelpers.CurrentScenarioInfo.CurrentStageNumber != 1),
						new Action(ctx => ScriptHelpers.MarkBossAsDead("Karsar the Bloodletter"))),
					new Decorator(
						ctx =>
						ScriptHelpers.IsBossAlive("Den Mother Moof") &&
						(ScriptHelpers.CurrentScenarioInfo.CurrentStageNumber == 1 && ScriptHelpers.CurrentScenarioInfo.CurrentStage.GetStep(2).IsComplete ||
						 ScriptHelpers.CurrentScenarioInfo.CurrentStageNumber != 1),
						new Action(ctx => ScriptHelpers.MarkBossAsDead("Den Mother Moof"))),
					new Decorator(
						ctx =>
						ScriptHelpers.IsBossAlive("Li Te") &&
						(ScriptHelpers.CurrentScenarioInfo.CurrentStageNumber == 1 && ScriptHelpers.CurrentScenarioInfo.CurrentStage.GetStep(3).IsComplete ||
						 ScriptHelpers.CurrentScenarioInfo.CurrentStageNumber != 1),
						new Action(ctx => ScriptHelpers.MarkBossAsDead("Li Te"))));
		}

		public Composite HealBehavior()
		{
			const uint mistWeaverId = 64189;
			WoWUnit mistWeaver = null;
			WoWUnit myCachedTarget = null;

			return
				new PrioritySelector(
					ctx =>
					mistWeaver =
					ObjectManager.GetObjectsOfType<WoWUnit>()
								 .Where(u => u.Entry == mistWeaverId && u.HasAura("Mistweaving") && u.Distance < 35)
								 .OrderBy(u => u.DistanceSqr)
								 .FirstOrDefault(),
					new Decorator(
						ctx => mistWeaver != null && Me.HealthPercent < 80 && Navigator.CanNavigateFully(Me.Location, mistWeaver.Location),
						new PrioritySelector(
							new ActionSetActivity("Healing at Mistweaver"),
							new Decorator(
								ctx => Me.Location.Distance(mistWeaver.Location) > 4,
								new Action(ctx => Navigator.GetRunStatusFromMoveResult(Navigator.MoveTo(mistWeaver.Location)))),
							new Decorator(
								ctx => Me.Location.Distance(mistWeaver.Location) <= 4,
				// we want to stay inside the healing circle until healed.. but if we have a target within range 
				// then continue down the behavior tree to allow Combat routine to run.
								new PrioritySelector(
									new Decorator(
										ctx =>
										(myCachedTarget = Me.CurrentTarget) == null ||
										(Me.IsMelee() && !myCachedTarget.IsWithinMeleeRange || Me.IsRange() && !Me.IsHealer() && myCachedTarget.Distance > 40),
										new ActionAlwaysSucceed()))))));
		}

		[EncounterHandler(63529, "Karsar the Bloodletter")]
		public Composite KarsartheBloodletterEncounter()
		{
			return new PrioritySelector();
		}

		[EncounterHandler(63518, "Den Mother Moof", Mode = CallBehaviorMode.CurrentBoss)]
		public Composite DenMotherMoofSpawnEncounter()
		{
			return new PrioritySelector(
				new Decorator(ctx => ctx == null, ScriptHelpers.CreateClearArea(() => _denMotherSpawnLoc, 50, u => u.Entry == HungryVirmenId)),
				new Decorator(ctx => Me.IsTank() && _denMotherSpawnLoc.Distance(Me.Location) < 10 && Targeting.Instance.IsEmpty(), new ActionAlwaysSucceed()));
		}

		[EncounterHandler(63518, "Den Mother Moof")]
		public Composite DenMotherMoofEncounter()
		{
			AddAvoidObject(ctx => true, () => _denMotherSpawnLoc, 40, 15, u => u.Entry == DenMotherMoofId && u.ToUnit().CastingSpellId == TwirlwindId);
			return new PrioritySelector(new Decorator(ctx => Me.IsTank() && Targeting.Instance.IsEmpty(), new ActionAlwaysSucceed()));
		}

		[EncounterHandler(62793, "Assistant Tart", Mode = CallBehaviorMode.Proximity, BossRange = 40)]
		public Composite AssistantTartEncounter()
		{
			WoWUnit tart = null;
			return new PrioritySelector(
				ctx => tart = ctx as WoWUnit,
				new Decorator(
					ctx => !Me.HasAura("Water Walking") && Targeting.Instance.IsEmpty() && ScriptHelpers.IsBossAlive("Li Te"), ScriptHelpers.CreateTalkToNpc(ctx => tart)));
		}

		[EncounterHandler(63520, "Li Te")]
		public Composite LiTeEncounter()
		{
			const uint waterspoutId = 63823;
			WoWUnit boss = null;
			AddAvoidObject(ctx => true, 6, u => u.Entry == waterspoutId);

			return new PrioritySelector(ctx => boss = ctx as WoWUnit, new Decorator(ctx => Me.IsTank() && Targeting.Instance.IsEmpty(), new ActionAlwaysSucceed()));
		}

		[EncounterHandler(64189, "Tian Mistweaver", BossRange = 40, Mode = CallBehaviorMode.Proximity)]
		public Composite TianMistweaverEncounter()
		{
			return
				new PrioritySelector(
					new Decorator<WoWUnit>(
						unit => Me.HealthPercent < 60 && unit.HasAura("Mistweaving") && unit.Distance > 4,
						new Helpers.Action<WoWUnit>(unit => Navigator.GetRunStatusFromMoveResult(Navigator.MoveTo(unit.Location)))));
		}

		[ScenarioStage(2, "The Scouts Report")]
		public Composite CreateStageTwoBehavior()
		{
			ScenarioStage stage = null;
			const uint krasarangWildBrewId = 63929;
			const uint briawShanId = 63922;
			const uint tianDiscipleId = 63946;
			const uint derpaDerpaId = 64017;
			const uint fireworksBarrelId = 63940;

			WoWUnit interactObject = null;
			WoWUnit step4Npc = null;
			return new PrioritySelector(
				ctx =>
				{
					interactObject =
						ObjectManager.GetObjectsOfType<WoWUnit>()
									 .Where(
										 u =>
										 (u.Entry == krasarangWildBrewId || u.Entry == fireworksBarrelId) &&
										 !Me.PartyMembers.Any(p => !p.IsMe && p.IsSafelyFacing(u, 45) && p.Location.DistanceSqr(u.Location) < Me.Location.DistanceSqr(u.Location)))
									 .OrderBy(u => u.DistanceSqr)
									 .FirstOrDefault();
					step4Npc =
						ObjectManager.GetObjectsOfType<WoWUnit>()
									 .Where(u => (u.Entry == briawShanId && !Me.HasAura("Brew Strike")
										 || u.Entry == tianDiscipleId
										 || (u.Entry == derpaDerpaId && !Me.HasAura("Place Pulse Wave Emitter")))
										 && u.CanGossip)
									 .OrderBy(u => u.DistanceSqr)
									 .FirstOrDefault();
					return stage = ScriptHelpers.CurrentScenarioInfo.CurrentStage;
				},
				HealBehavior(),
				new Decorator(
					ctx => !stage.GetStep(1).IsComplete || !stage.GetStep(2).IsComplete || !stage.GetStep(3).IsComplete,
					ScriptHelpers.CreateClearArea(() => _villageCenterLoc, 150)),
				new Decorator(
					ctx => Lua.GetReturnVal<bool>("return ExtraActionButton1:IsVisible()", 0),
					new Action(
						ctx =>
						{
							var unit = ScriptHelpers.GetUnfriendlyNpsAtLocation(Me.Location, 29).FirstOrDefault();
							Lua.DoString("ExtraActionButton1:Click()");
							SpellManager.ClickRemoteLocation(unit != null ? unit.Location : Me.Location);
							return RunStatus.Failure;
						})),
				new Decorator(
					ctx => !stage.GetStep(4).IsComplete && Targeting.Instance.IsEmpty(),
					new PrioritySelector(
						new Decorator(
							ctx => interactObject != null,
							new PrioritySelector(
								new Decorator(
									ctx => interactObject.WithinInteractRange,
									new PrioritySelector(
										new Decorator(ctx => Me.Mounted, new ActionRunCoroutine(ctx => CommonCoroutines.Dismount())),
										 new Decorator(ctx => Me.Shapeshift != ShapeshiftForm.Normal, new AlwaysFailAction<object>(ctx => Lua.DoString("CancelShapeshiftForm()"))),
										new Action(ctx => interactObject.Interact()))),
								new Decorator(
									ctx => !interactObject.WithinInteractRange,
									new Action(ctx => Navigator.GetRunStatusFromMoveResult(Navigator.MoveTo(interactObject.Location)))))),
						new Decorator(ctx => step4Npc != null && !stage.GetStep(4).IsComplete && step4Npc.CanGossip, ScriptHelpers.CreateTalkToNpc(ctx => step4Npc)))),
				new Decorator(
					ctx => Me.IsTank() && Targeting.Instance.IsEmpty() && BotPoi.Current.Type == PoiType.None,
					new PrioritySelector(new Decorator(ctx => !Me.Combat, RoutineManager.Current.RestBehavior), new ActionAlwaysSucceed())));
		}


		[ScenarioStage(3)]
		[ScenarioStage(4)]
		public Composite CreateStageThreeAndFourBehavior()
		{
			WoWUnit brewStrikeTarget = null;
			return new PrioritySelector(
				ctx =>
				{
					var targets =
						ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Combat && (u.IsTargetingMeOrPet || u.IsTargetingMyPartyMember || u.TaggedByMe)).ToList();

					brewStrikeTarget = Me.Combat && targets.Count > 3 || targets.Any(t => t.Classification == WoWUnitClassificationType.Elite)
										   ? targets.OrderByDescending(u => targets.Count(t => u.Location.DistanceSqr(t.Location) <= 10 * 10)).FirstOrDefault()
										   : null;
					return ctx;
				},
				HealBehavior(),
				new Decorator(
					ctx => brewStrikeTarget != null && Me.HasAura("Brew Strike") && !Lua.GetReturnVal<bool>("return ExtraActionButton1.cooldown:IsVisible()", 0),
					new Action(
						ctx =>
						{
							Lua.DoString("ExtraActionButton1:Click()");
							SpellManager.ClickRemoteLocation(brewStrikeTarget.Location);
						})),
				ScriptHelpers.CreateClearArea(() => _villageCenterLoc, 150),
				new Decorator(
					ctx => Me.IsTank() && Targeting.Instance.IsEmpty() && BotPoi.Current.Type == PoiType.None,
					new PrioritySelector(new Decorator(ctx => !Me.Combat, RoutineManager.Current.RestBehavior), new ActionAlwaysSucceed())));
		}


		[EncounterHandler(63528, "Warbringer Qobi")]
		public Composite WarbringerQobiEncounter()
		{
			const uint fireLineId = 64266;
			const int fireLineSpellId = 125392;

			WoWUnit boss = null;
			AddAvoidObject(
				ctx => Me.IsRange(),
				4,
				u => u.Entry == fireLineId,
				u => Me.Location.GetNearestPointOnSegment(boss.Location, WoWMathHelper.CalculatePointFrom(u.Location, boss.Location, 40)));

			AddAvoidObject(ctx => Me.IsMelee(), 4, fireLineId);
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(
					ctx => boss.CastingSpellId == fireLineSpellId && boss.Distance < 10, ctx => boss, new ScriptHelpers.AngleSpan(0, 160)));
		}
	}
}