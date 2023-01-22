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
	public class MaraudonFoulsporeCavern : Dungeon
	{
		#region Overrides of Dungeon

		private WaitTimer _deathTimer;

		public override uint DungeonId
		{
			get { return 26; }
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
			get { return new WoWPoint(1008.538, -461.3261, -43.5484); }
		}

		#endregion

		private const int ServantsOfTheradrasQuestId = 27698;

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0, "Root")]
		public Composite RootHandler()
		{
			PlayerQuest quest = null;
			return new PrioritySelector(
				new Decorator(ctx => (quest = Me.QuestLog.GetQuestById(ServantsOfTheradrasQuestId)) != null && quest.IsCompleted,
					ScriptHelpers.CreateCompletePopupQuest(ServantsOfTheradrasQuestId)),

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

		[ObjectHandler(178559, ObjectRange = 30f)]
		public Composite InteractWithLarvaSpewer()
		{
			return new PrioritySelector(
				ScriptHelpers.CreateInteractWithObject(ctx => ctx as WoWObject, 2, true)
				);
		}


		[EncounterHandler(12258, "Razorlash")]
		public Composite RazorlashFight()
		{
			WoWUnit boss = null;

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				// Boss has a cleave
				ScriptHelpers.CreateTankFaceAwayGroupUnit(10),
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(ctx => !Me.IsTank() && boss.Distance < 10, ctx => boss, new ScriptHelpers.AngleSpan(0, 180)));
		}

		[LocationHandler(907.4058, -321.4649, -49.68059)]
		public Composite GetOutOfPoolBehavior()
		{
			var movetoLoc = new WoWPoint(907.4058, -321.4649, -49.68059);
			return new PrioritySelector(
				new Decorator(ctx => Me.IsSwimming && !Me.Combat, new Action(ctx => Navigator.PlayerMover.MoveTowards(movetoLoc))));
		}
	}
}