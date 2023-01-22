
using System;
using System.Collections;
using System.Linq;
using CommonBehaviors.Actions;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.TreeSharp;
using Styx;
using Styx.WoWInternals.WoWObjects;
using System.Collections.Generic;
using Action = Styx.TreeSharp.Action;
using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Classic
{
	public class RazorfenDowns : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 20; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(-4661.295, -2532.039, 82.10799); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(2592.978, 1115.126, 50.73724); }
		}

		readonly WoWPoint _mordreshLoc = new WoWPoint(2466.618, 671.4426, 63.3871);
		readonly WoWPoint _safeMordreshLoc = new WoWPoint(2477.332, 678.9361, 55.2272);
		WaitTimer _jumpWaitTimer = new WaitTimer(TimeSpan.FromSeconds(3));
		public override MoveResult MoveTo(WoWPoint location)
		{
			var myLoc = Me.Location;
			var pointDistToMordresh = location.Distance(_mordreshLoc);
			var myDistToMordresh = myLoc.Distance(_mordreshLoc);
			if (myDistToMordresh < 20 || pointDistToMordresh < 20)
			{
				if (myDistToMordresh > 45)
					return Navigator.MoveTo(_safeMordreshLoc);
				Navigator.PlayerMover.MoveTowards(location);
				if (_jumpWaitTimer.IsFinished)
				{
					WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
					WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend);
					_jumpWaitTimer.Reset();
				}
				return MoveResult.Moved;
			}
			return base.MoveTo(location);
		}
		#endregion

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}



		[EncounterHandler(44837, "Koristrasza", Mode = CallBehaviorMode.Proximity, BossRange = 40)]
		public Composite KoristraszaEncounter()
		{
			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				new Decorator(ctx => !Me.Combat && unit.QuestGiverStatus == QuestGiverStatus.Available, ScriptHelpers.CreatePickupQuest(ctx => unit)),
				new Decorator(ctx => !Me.Combat && unit.QuestGiverStatus == QuestGiverStatus.TurnIn, ScriptHelpers.CreateTurninQuest(ctx => unit)));
		}

		#region Tuten'kash

		[EncounterHandler(7355, "Tuten'kash")]
		public Composite TutenkashEncounter()
		{
			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateDispelGroup("Virulent Poison", ScriptHelpers.PartyDispelType.Poison),
				ScriptHelpers.CreateDispelGroup("Curse of Tuten'kash", ScriptHelpers.PartyDispelType.Curse),
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(ctx => !Me.IsTank() && boss.Distance < 20, ctx => boss, new ScriptHelpers.AngleSpan(0, 180)),
				ScriptHelpers.CreateTankFaceAwayGroupUnit(ctx => boss, 20));
		}

		[ObjectHandler(148917, "Gong", ObjectRange = 75)]
		public Composite GongHandler()
		{
			var waitForWaveTimer = new WaitTimer(TimeSpan.FromSeconds(5));

			WoWGameObject gong = null;
			return new PrioritySelector(
				ctx => gong = ctx as WoWGameObject,
				new Decorator(ctx => !waitForWaveTimer.IsFinished && Me.IsTank() && Targeting.Instance.IsEmpty(), new ActionAlwaysSucceed()),
				new Decorator(
					ctx =>
					Targeting.Instance.IsEmpty() && !gong.InUse && gong.State == WoWGameObjectState.Ready && Me.IsTank() && gong.CanUse() &&
					ScriptHelpers.IsBossAlive("Tuten'kash"),
					new PrioritySelector(
						new Decorator(ctx => gong.WithinInteractRange, new Sequence(new Action(ctx => waitForWaveTimer.Reset()), new Action(ctx => gong.Interact()))),
						new Decorator(ctx => !gong.WithinInteractRange, new Action(ctx => Navigator.MoveTo(gong.Location))))));
		}

		#endregion

		#region Mordresh Fire Eye

		private const uint MordreshFireEyeId = 7357;

		[EncounterHandler(7357, "Mordresh Fire Eye")]
		public Composite MordreshFireEyeEncounter()
		{
			WoWUnit boss = null;
			AddAvoidObject(ctx => Me.IsRange(), 10, o => o.Entry == MordreshFireEyeId && o.ToUnit().CurrentTargetGuid != Me.Guid);

			const int fireNovaId = 12470;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit, ScriptHelpers.CreateInterruptCast(ctx => boss, fireNovaId));
		}

		#endregion

		#region Glutton

		private const uint GluttonId = 8567;

		[EncounterHandler(8567, "Glutton")]
		public Composite GluttonEncounter()
		{
			WoWUnit boss = null;
			// range should keep thier distance because of the desease cloud
			AddAvoidObject(ctx => Me.IsRange(), 5, o => o.Entry == GluttonId && o.ToUnit().IsAlive && o.ToUnit().CurrentTargetGuid != Me.Guid);

			return new PrioritySelector(ctx => boss = ctx as WoWUnit);
		}

		#endregion

		#region Amnennar the Coldbringer

		[EncounterHandler(7358, "Amnennar the Coldbringer")]
		public Composite AmnennarTheColdbringerEncounter()
		{
			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit);
		}

		#endregion
	}
}