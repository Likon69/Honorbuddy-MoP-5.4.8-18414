

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
using Action = Styx.TreeSharp.Action;
using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
using AvoidanceManager = Bots.DungeonBuddy.Avoidance.AvoidanceManager;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Mists_of_Pandaria
{
	public class DominationPoint : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 595; }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (unit != null)
					{
						if (unit.Entry == JoanLorraineId && unit.CastingSpellId == DivineStormId && Me.IsMelee())
							return true;
					}
					return false;
				});
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			var kromthar = Kromthar;
			var kromtharsTargetGuid = kromthar != null ? kromthar.CurrentTargetGuid : 0;

			var generalNazgrim = GeneralNazgrim;
			var generalNazgrimsTargetGuid = generalNazgrim != null ? generalNazgrim.CurrentTargetGuid : 0;

			var warlordBloodhilt = WarlordBloodhilt;
			var warlordBloodhiltsTargetGuid = warlordBloodhilt != null ? warlordBloodhilt.CurrentTargetGuid : 0;

			foreach (var obj in incomingunits)
			{
				var unit = obj as WoWUnit;
				if (unit != null)
				{
					var unitGuid = unit.Guid;
					var currentTarget = unit.CurrentTarget;
					if (currentTarget != null)
					{
						if (currentTarget.Entry == KromtharId || unit.Entry == GeneralNazgrimId || unit.Entry == WarlordBloodhiltId)
							outgoingunits.Add(unit);
					}
					else if (unitGuid == kromtharsTargetGuid || unitGuid == generalNazgrimsTargetGuid || unitGuid == warlordBloodhiltsTargetGuid)
					{
						outgoingunits.Add(unit);
					}
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

		private const uint HighExplosiveBombId = 67523;
		private const uint KromtharId = 68998;
		private const uint GeneralNazgrimId = 68997;

		WoWUnit Kromthar
		{
			get { return ObjectManager.GetObjectsOfTypeFast<WoWUnit>().FirstOrDefault(u => u.Entry == KromtharId); }
		}

		WoWUnit GeneralNazgrim
		{
			get { return ObjectManager.GetObjectsOfTypeFast<WoWUnit>().FirstOrDefault(u => u.Entry == GeneralNazgrimId); }
		}

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0)]
		public Composite RootBehavior()
		{
			const uint tankTargetId = 59566;
			AddAvoidObject(ctx => true, 6, tankTargetId);
			AddAvoidObject(ctx => true, 4, HighExplosiveBombId);
			return new PrioritySelector();
		}

		[ScenarioStage(1, "Stage One")]
		public Composite StageOneEncounter()
		{
			WoWUnit kromthar = null;
			return new PrioritySelector(
				ctx => kromthar = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == KromtharId),
				new Decorator(ctx => Me.IsTank() && Targeting.Instance.IsEmpty() && kromthar != null, ScriptHelpers.CreateTalkToNpc(ctx => kromthar)));
		}

		[ScenarioStage(2, "Stage Two")]
		public Composite StageTwoEncounter()
		{
			WoWUnit generalNazgrim = null;
			var stageLoc = new WoWPoint(-1849.708, 2115.283, 2.612717);

			return new PrioritySelector(
				ctx => generalNazgrim = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == GeneralNazgrimId),
				new Decorator(
					ctx => Targeting.Instance.IsEmpty() && (generalNazgrim == null || generalNazgrim.Distance > 5), new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(stageLoc))),
				new Decorator(
					ctx => Me.IsTank() && Targeting.Instance.IsEmpty() && generalNazgrim != null && generalNazgrim.Distance <= 5,
					ScriptHelpers.CreateTalkToNpc(ctx => generalNazgrim)));
		}


		[ScenarioStage(3, "Stage Three")]
		public Composite StageThreeEncounter()
		{
			var eastLoc = new WoWPoint(-1898.224, 2353.027, 9.095402);
			var westLoc = new WoWPoint(-1918.186, 2429.262, 6.436874);
			var southLoc = new WoWPoint(-1957.054, 2414.411, 5.288549);

			ScenarioStage stage = null;
			return new PrioritySelector(
				ctx => stage = ctx as ScenarioStage,
				// east attackers
				new Decorator(ctx => Targeting.Instance.IsEmpty() && !stage.GetStep(1).IsComplete, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(eastLoc))),
				// west attackers
				new Decorator(
					ctx => Targeting.Instance.IsEmpty() && stage.GetStep(1).IsComplete && !stage.GetStep(2).IsComplete, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(westLoc))),
				// south attackers
				new Decorator(
					ctx => Targeting.Instance.IsEmpty() && stage.GetStep(1).IsComplete && stage.GetStep(2).IsComplete && !stage.GetStep(3).IsComplete,
					new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(southLoc))));
		}

		const uint WarlordBloodhiltId = 69002;

		WoWUnit WarlordBloodhilt
		{
			get { return ObjectManager.GetObjectsOfTypeFast<WoWUnit>().FirstOrDefault(u => u.Entry == WarlordBloodhiltId); }
		}

		[ScenarioStage(4, "Stage Four")]
		public Composite StageFourEncounter()
		{
			WoWUnit warlordBloodhilt = null;

			var stageLoc = new WoWPoint(-1955.131, 2410.507, 5.309048);

			ScenarioStage stage = null;
			return new PrioritySelector(
				ctx =>
				{
					warlordBloodhilt = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == WarlordBloodhiltId);
					return stage = ctx as ScenarioStage;
				},
				new Decorator(
					ctx => Targeting.Instance.IsEmpty() && (warlordBloodhilt == null || warlordBloodhilt.Distance > 5), new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(stageLoc))),
				new Decorator(
					ctx => Me.IsTank() && Targeting.Instance.IsEmpty() && warlordBloodhilt != null && warlordBloodhilt.Distance <= 5 && !stage.GetStep(1).IsComplete,
					ScriptHelpers.CreateTalkToNpc(ctx => warlordBloodhilt)));
		}

		[ScenarioStage(5, "Stage Five")]
		public Composite StageFiveEncounter()
		{
			const uint shokiaId = 69001;
			const uint kirynId = 69000;
			const uint rivetid = 68999;

			var step1Loc = new WoWPoint(-1899.47, 2459.023, 7.839976);
			var step2Loc = new WoWPoint(-1840.196, 2382.245, 14.31215);
			var step3Loc = new WoWPoint(-1864.974, 2326.458, 9.635302);

			ScenarioStage stage = null;
			return new PrioritySelector(
				ctx => stage = ctx as ScenarioStage,
				new Decorator(
					ctx => Targeting.Instance.IsEmpty() && !stage.GetStep(1).IsComplete,
					new PrioritySelector(
						new Decorator(ctx => step1Loc.Distance(Me.Location) > 5, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(step1Loc))),
						new Decorator(ctx => step1Loc.Distance(Me.Location) <= 5, ScriptHelpers.CreateTalkToNpc(shokiaId)))),
				new Decorator(
					ctx => Targeting.Instance.IsEmpty() && stage.GetStep(1).IsComplete && !stage.GetStep(2).IsComplete,
					new PrioritySelector(
						new Decorator(ctx => step2Loc.Distance(Me.Location) > 5, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(step2Loc))),
						new Decorator(ctx => step2Loc.Distance(Me.Location) <= 5, ScriptHelpers.CreateTalkToNpc(kirynId)))),
				new Decorator(
					ctx => Targeting.Instance.IsEmpty() && stage.GetStep(1).IsComplete && stage.GetStep(2).IsComplete && !stage.GetStep(3).IsComplete,
					new PrioritySelector(
						new Decorator(ctx => step3Loc.Distance(Me.Location) > 5, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(step3Loc))),
						new Decorator(ctx => step3Loc.Distance(Me.Location) <= 5, ScriptHelpers.CreateTalkToNpc(rivetid)))));
		}

		[ScenarioStage(6, "Stage Six")]
		public Composite StageSixEncounter()
		{
			const uint placeRocketsHereId = 68886;
			const uint placeBoomsticksHereId = 68885;
			const uint placeBombsHereId = 68884;

			var stageLoc = new WoWPoint(-1909.725, 2384.62, 6.49155);

			ScenarioStage stage = null;
			return new PrioritySelector(
				ctx => stage = ctx as ScenarioStage,
				new Decorator(ctx => stageLoc.Distance(Me.Location) > 20, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(stageLoc))),
				new Decorator(
					ctx => stageLoc.Distance(Me.Location) <= 20,
					new PrioritySelector(
						new Decorator(ctx => !stage.GetStep(1).IsComplete, ScriptHelpers.CreateTalkToNpc(placeBombsHereId)),
						new Decorator(ctx => stage.GetStep(1).IsComplete && !stage.GetStep(2).IsComplete, ScriptHelpers.CreateTalkToNpc(placeBoomsticksHereId)),
						new Decorator(
							ctx => stage.GetStep(1).IsComplete && stage.GetStep(2).IsComplete && !stage.GetStep(3).IsComplete, ScriptHelpers.CreateTalkToNpc(placeRocketsHereId)))));
		}

		[ScenarioStage(7, "Stage Seven")]
		public Composite StageSevenEncounter()
		{
			const uint reaverBombsId = 216725;
			WoWGameObject bombs = null;

			return new PrioritySelector(
				ctx => bombs = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(g => g.Entry == reaverBombsId),
				new Decorator(ctx => bombs != null && !Lua.GetReturnVal<bool>("return ExtraActionButton1:IsVisible()", 0), ScriptHelpers.CreateInteractWithObject(ctx => bombs)),
				new Decorator(ctx => !Targeting.Instance.IsEmpty() && Targeting.Instance.FirstUnit.Distance <= 30 && !Lua.GetReturnVal<bool>("return ExtraActionButton1.cooldown:IsVisible()", 0),
					new Action(ctx =>
								   {
									   Lua.DoString("ExtraActionButton1:Click()");
									   SpellManager.ClickRemoteLocation(Targeting.Instance.FirstUnit.Location);
								   })));
		}

		const int DivineStormId = 135404;
		private const uint JoanLorraineId = 67530;

		[EncounterHandler(67530, "Joan Lorraine")]
		public Composite JoanLorraineEncounter()
		{
			const uint HammerMarkerId = 68800;
			const int divineLightId = 135403;
			WoWUnit boss = null;
			AddAvoidObject(ctx => true, 8, o => o.Entry == JoanLorraineId && o.ToUnit().CastingSpellId == DivineStormId);
			AddAvoidObject(ctx => true, 5, HammerMarkerId);

			return new PrioritySelector(ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateInterruptCast(ctx => boss, divineLightId)
				);
		}
	}
}