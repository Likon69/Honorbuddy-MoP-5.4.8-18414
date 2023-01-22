using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Styx;
using Styx.CommonBot;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;

using Bots.DungeonBuddy;
using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Classic
{
	public class LowerBlackrockSpire : Dungeon
	{
		#region Overrides of Dungeon
		public override uint DungeonId
		{
			get { return 32; }
		}

		public override WoWPoint Entrance { get { return new WoWPoint(-7522.93, -1232.999, 285.74); } }
		public override WoWPoint ExitLocation { get { return new WoWPoint(77.55, -223.18, 49.84); } }

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		#endregion

		[EncounterHandler(10299, "Acride", Mode = CallBehaviorMode.Proximity, BossRange = 15)]
		public Composite QuestPickupHandler()
		{
			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				new Decorator(
					ctx => Targeting.Instance.IsEmpty() && unit.QuestGiverStatus == QuestGiverStatus.Available,
					ScriptHelpers.CreatePickupQuest(ctx => unit)),
				new Decorator(
					ctx => Targeting.Instance.IsEmpty() && unit.QuestGiverStatus == QuestGiverStatus.TurnIn,
					ScriptHelpers.CreateTurninQuest(ctx => unit)));
		}

		[EncounterHandler(9236, "Shadow Hunter Vosh'gajin")]
		public Composite ShadowHunterVoshgajinBehavior()
		{
			WoWUnit boss = null;
			const int curseOfBloodId = 16098;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateDispelGroup("Hex", ScriptHelpers.PartyDispelType.Magic),
				ScriptHelpers.CreateInterruptCast(ctx => boss, curseOfBloodId));
		}

		[EncounterHandler(9237, "War Master Voone")]
		public Composite WarMasterVooneBehavior()
		{
			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(ctx => !Me.IsTank() && boss.Distance < 8 && !boss.IsMoving && boss.CurrentTargetGuid != Me.Guid, ctx => boss, new ScriptHelpers.AngleSpan(0, 180)),
				ScriptHelpers.CreateTankFaceAwayGroupUnit(ctx => boss, 8));
		}


		[EncounterHandler(10596, "Mother Smolderweb")]
		public Composite MotherSmolderwebBehavior()
		{
			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateDispelGroup("Mother's Milk", ScriptHelpers.PartyDispelType.Poison),
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(ctx => !Me.IsTank() && boss.Distance < 8 && !boss.IsMoving && boss.CurrentTargetGuid != Me.Guid, ctx => boss, new ScriptHelpers.AngleSpan(0, 180)),
				ScriptHelpers.CreateTankFaceAwayGroupUnit(ctx => boss, 8));
		}

		[EncounterHandler(9736, "Quartermaster Zigris")]
		public Composite QuartermasterZigrisBehavior()
		{
			WoWUnit boss = null;
			// todo: get Missile INfo for stun bomb.
			return new PrioritySelector(ctx => boss = ctx as WoWUnit);
		}

		[EncounterHandler(9568, "Overlord Wyrmthalak")]
		public Composite OverlordWyrmthalakBehavior()
		{
			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(ctx => !Me.IsTank() && boss.Distance < 8 && !boss.IsMoving && boss.CurrentTargetGuid != Me.Guid, ctx => boss, new ScriptHelpers.AngleSpan(0, 180)),
				ScriptHelpers.CreateTankFaceAwayGroupUnit(ctx => boss, 8));
		}
	}
}
