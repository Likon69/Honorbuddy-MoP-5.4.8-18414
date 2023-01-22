using System.Collections.Generic;
using System.Linq;
using Styx;
using Styx.CommonBot;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Burning_Crusade
{
	public class ShatteredHalls : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 138; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(-310.2105, 3087.014, -3.916756); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(-42.05605, -26.77249, -13.51534); }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				u =>
				{ // remove low level units.
					var unit = u as WoWUnit;
					if (unit != null)
					{
						if (unit is WoWPlayer)
							return true;

						if ((unit.Entry == 17356 || unit.Entry == 17357) && !unit.Combat) // Creeping Ozzling
							return true;

						if (StyxWoW.Me.IsTank() && ScriptHelpers.IsBossAlive("Warchief Kargath Bladefist") &&
							(unit.Entry == 17621 || unit.Entry == 17623 || unit.Entry == 17622))
							// Heathen Guard, Reaver Guard, Sharpshooter Guard
							return true;
					}
					return false;
				});
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var priority in units)
			{
				var unit = priority.Object as WoWUnit;
				if (unit != null)
				{
					if (StyxWoW.Me.IsDps())
					{
						if (unit.Entry == 17621 || unit.Entry == 17623 || unit.Entry == 17622) // Heathen Guard, Reaver Guard, Sharpshooter Guard
							priority.Score += 400;
					}
				}
			}
		}

		#endregion

		private WoWUnit _warchiefKargathBladefist;

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0, "Root")]
		public Composite RootBehavior()
		{
			const uint blazeId = 181915;
			AddAvoidObject(ctx => !Me.IsCasting, 5, blazeId);

			return new PrioritySelector();
		}

		[EncounterHandler(16808, "Warchief Kargath Bladefist")]
		public Composite WarchiefKargathBladefistEncounter()
		{
			var platformCenterLocation = new WoWPoint(231.25, -83.64489, 4.940088);
			return new PrioritySelector(
				ctx => _warchiefKargathBladefist = ctx as WoWUnit,
				ScriptHelpers.CreateSpreadOutLogic(ctx => Targeting.Instance.FirstUnit == _warchiefKargathBladefist, ctx => platformCenterLocation, 15, 25),
				ScriptHelpers.CreateTankUnitAtLocation(ctx => platformCenterLocation, 5));
		}

		#region Grand Warlock Nethekurse

		private const uint GrandWarlockNethekurseId = 16807;

		[EncounterHandler(17356, "Creeping Ozze")]
		[EncounterHandler(17357, "Creeping Ozzling")]
		public Composite CreepingOzzlingEncounter()
		{
			var tankLoc = new WoWPoint(180.5946, 227.8267, -18.55346);
			return
				new PrioritySelector(
					new Decorator(
						ctx =>
						StyxWoW.Me.IsTank() && Me.PartyMembers.All(p => p.HealthPercent > 50) && !StyxWoW.Me.IsActuallyInCombat &&
						StyxWoW.Me.Location.DistanceSqr(tankLoc) > 5,
						new Action(ctx => Navigator.MoveTo(tankLoc))));
		}

		[ObjectHandler(182539, "Grand Warlock Chamber Door")]
		public Composite GrandWarlockChamberDoorHandler()
		{
			var roomBeforeFirstBossLoc = new WoWPoint(123.3501, 264.7724, -13.23036);
			var dropDownLoc = new WoWPoint(121.9095, 236.8019, -46.10165);
			var topOfJumpLoc = new WoWPoint(123.0873, 250.3539, -15.3936);
			return
				new PrioritySelector(
					new Decorator(
						ctx =>
						((WoWGameObject)ctx).State == WoWGameObjectState.Ready && !StyxWoW.Me.Combat && StyxWoW.Me.Location.DistanceSqr(roomBeforeFirstBossLoc) <= 20 * 20 &&
						StyxWoW.Me.Z > -25,
						new PrioritySelector(
							new Decorator(
								ctx => StyxWoW.Me.IsTank() || (StyxWoW.Me.IsFollower() && ScriptHelpers.Tank != null && ScriptHelpers.Tank.Z < -25),
								new PrioritySelector(
									new Decorator(ctx => StyxWoW.Me.Location.DistanceSqr(topOfJumpLoc) > 4 * 4, new Action(ctx => Navigator.MoveTo(topOfJumpLoc))),
									new Action(ctx => WoWMovement.ClickToMove(dropDownLoc)))))));
		}

		[EncounterHandler(16807, "Grand Warlock Nethekurse")]
		public Composite GrandWarlockNethekurseEncounter()
		{
			WoWUnit boss = null;
			const uint lesserShadowFissureId = 17471;
			AddAvoidObject(ctx => !Me.IsCasting, 8, lesserShadowFissureId);
			//AddAvoidObject(ctx => !StyxWoW.Me.IsLeader(), 5, o => o.Entry == GrandWarlockNethekurseId && o.ToUnit().HasAura("Dark Spin"));

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(
					ctx => !Me.IsTank() && boss.CurrentTargetGuid != Me.Guid && !boss.IsMoving && boss.Distance < 8, ctx => boss, new ScriptHelpers.AngleSpan(0, 180)),
				ScriptHelpers.CreateTankFaceAwayGroupUnit(8));
		}

		#endregion
	}
}