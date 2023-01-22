using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common.Helpers;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;

using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Classic
{
	public class MaraudonTheWickedGrotto : Dungeon
	{
		#region Overrides of Dungeon
		public override uint DungeonId
		{
			get { return 272; }
		}
		public override WoWPoint Entrance
		{
			get
			{
				if (_deathTimer == null)
				{
					_deathTimer = new WaitTimer(TimeSpan.FromMinutes(2));
					_deathTimer.Reset();
				}
				return new WoWPoint(-1381.243, 2918.000, 73.42503);
			}
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(756.0845, -628.2405, -32.81234); }
		}

		private WaitTimer _deathTimer;

		#endregion

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		const int CorruptionInMaraudonQuestId = 27697;
		[EncounterHandler(0, "Root")]
		public Composite RootHandler()
		{
			PlayerQuest quest = null;
			return new PrioritySelector(
				 new Decorator(ctx => (quest = Me.QuestLog.GetQuestById(CorruptionInMaraudonQuestId)) != null && quest.IsCompleted,
					 ScriptHelpers.CreateCompletePopupQuest(CorruptionInMaraudonQuestId)),

				// make sure we're at the right entrance when porting in after dieing.
				// fixed by zoning out and back in.
				new Decorator(
					ctx => _deathTimer != null,
					new Sequence(
						new DecoratorContinue(
							ctx => !_deathTimer.IsFinished && Me.Location.Distance(ExitLocation) > 50,
							new Sequence(
								new Action(ctx => Logger.Write("Porting outside because I'm not at right entrance.")),
								new Action(ctx => ScriptHelpers.TelportOutsideLfgInstance()),
								new WaitContinue(2, ctx => !StyxWoW.IsInWorld, new ActionAlwaysSucceed()),
								new WaitContinue(40, ctx => StyxWoW.IsInWorld, new ActionAlwaysSucceed()),
								new Action(ctx => Lua.DoString("LFGTeleport(false)")))),
						new Action(ctx => _deathTimer = null))));
		}


		[EncounterHandler(13601, "Tinkerer Gizlock")]
		public Composite TinkererGizlockFight()
		{
			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateSpreadOutLogic(ctx => true, ctx => boss.Location, 6, 38),
				// Boss has a cleave
				ScriptHelpers.CreateTankFaceAwayGroupUnit(10),
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(ctx => !Me.IsTank() && boss.Distance < 10, ctx => boss, new ScriptHelpers.AngleSpan(0, 180)));
		}

		[EncounterHandler(12236, "Lord Vyletongue")]
		public Composite LordVyletongueFight()
		{
			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateSpreadOutLogic(ctx => true, ctx => boss.Location, 6, 38));
		}
	}
}
