

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common.Helpers;
using Styx.Helpers;
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
	class StormwindStockade : Dungeon
	{
		#region Overrides of Dungeon
		public override uint DungeonId
		{
			get { return 12; }
		}

		public override WoWPoint Entrance { get { return new WoWPoint(-8763.923f, 847.3502f, 86.92135f); } }
		public override WoWPoint ExitLocation { get { return new WoWPoint(44.81386f, 0.273018f, -20.83632f); } }
		// 46383" name="Randolph Moloch" killOrder="1" optional="false" X="144.6028" Y="2.146715" Z="-25.60624"/>

		#endregion

		#region Root

		LocalPlayer Me { get { return StyxWoW.Me; }}

		[EncounterHandler(46417, "Rifle Commander Coe", Mode = CallBehaviorMode.Proximity, BossRange = 40)]
		[EncounterHandler(46409, "Warden Thelwater", Mode = CallBehaviorMode.Proximity, BossRange = 40)]
		[EncounterHandler(46410, "Nurse Lillian", Mode = CallBehaviorMode.Proximity, BossRange = 40)]
		public Composite QuestGiversBehavior()
		{
			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				new Decorator(
					ctx => !Me.Combat && unit.QuestGiverStatus == QuestGiverStatus.Available,
					ScriptHelpers.CreatePickupQuest(ctx => unit)),
				new Decorator(
					ctx => !Me.Combat && unit.QuestGiverStatus == QuestGiverStatus.TurnIn,
					ScriptHelpers.CreateTurninQuest(ctx => unit)));
		}

		#endregion



		[EncounterHandler(46383, "Randolph Moloch", Mode = CallBehaviorMode.CurrentBoss)]
		public Composite RandolpfMolochEncounter()
		{
			var combatTimer = new WaitTimer(TimeSpan.FromSeconds(6));

			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit,
				new Decorator(ctx => boss != null && boss.Combat,
					new Action(ctx =>
								   {
									   combatTimer.Reset();
									   return RunStatus.Failure;
								   })),

				// handle vanish.
				new Decorator(ctx => boss == null && !combatTimer.IsFinished && StyxWoW.Me.IsTank(),
						new ActionAlwaysSucceed())
				);
		}
	}
}
