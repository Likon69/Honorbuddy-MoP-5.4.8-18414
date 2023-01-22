using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bots.DungeonBuddy.Profiles.Handlers;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.Helpers;
using Styx.Patchables;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
using Action = Styx.TreeSharp.Action;

namespace Bots.DungeonBuddy.Dungeon_Scripts.Classic
{
	public class UpperBlackrockSpire : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 330; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(-7522.93, -1232.999, 285.74); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(77.55, -223.18, 49.84); }
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			var isDps = Me.IsDps();
			foreach (var p in units)
			{
				WoWUnit unit = p.Object.ToUnit();
				switch (unit.Entry)
				{
					case 9818: // Blackhand Summoner
						if (isDps)
							p.Score +=  1000;
						break;
					case 10442: // Chromatic Whelp. should be focused by DPS but left for last by tank.
						p.Score += isDps ? 1200 : -1200;
						break;
				}
				if ((unit.Entry == BlackhandSummonerId || unit.Entry == ChromaticWhelpId) && Me.IsRange())
					p.Score += 1000;
				if ((unit.Entry == ChromaticDragonspawnId || unit.Entry == BlackhandDragonHandlerId) && Me.IsDps() && Me.IsMelee())
					p.Score += 1000;
			}
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				o =>
				{
					var unit = o as WoWUnit;
					if (unit == null) return false;
					if (unit.Entry == ChromaticWhelpId && Me.IsMelee())
					{
						if (unit.Location.IsPointLeftOfLine(_hallOfBlackHandGateLeftLoc, _hallOfBlackHandGateRightLoc))
							return false;
						if (Me.IsDps())
						{
							var tank = ScriptHelpers.Tank;
							return tank != null && unit.Location.Distance(tank.Location) > 15;
						}
					}
					if (unit.Entry == WarchiefRendBlackhandId && unit.HasAura("Whirlwind") && Me.IsMelee() && Me.IsDps())
						return true;
					return false;
				});
		}

		#endregion

		private const uint BlackhandSummonerId = 9818;
		private const uint PyroguardEmberseerId = 9816;
		private const uint WarchiefRendBlackhandId = 10429;
		private const uint ChromaticWhelpId = 10442;
		private const uint ChromaticDragonspawnId = 10447;
		private const uint BlackhandDragonHandlerId = 10742;
		const uint BlackhandIncarceratorId = 10316;

		private const uint TheBeastId = 10430;
		private readonly WoWPoint _drakeTankSpot = new WoWPoint(149.3783, -420.6858, 110.4725);
		private readonly WoWPoint _hallOfBlackHandGateLeftLoc = new WoWPoint(192.1564, -409.6148, 110.8851);
		private readonly WoWPoint _hallOfBlackHandGateRightLoc = new WoWPoint(191.8866, -431.2092, 111.2402);

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(9816, "Pyroguard Emberseer")]
		public Func<WoWUnit, Task<bool>> PyroguardEmberseerBehavior()
		{
			AddAvoidObject(ctx => 
				Me.IsRange() && !Me.IsCasting,
				10, 
				o => o.Entry == PyroguardEmberseerId && o.ToUnit().CurrentTargetGuid != Me.Guid && o.ToUnit().Combat);
			return async boss => false;
		}


		[ObjectHandler(175244, "Emberseer In", ObjectRange = 120)]
		public Func<WoWGameObject, Task<bool>> ClearHallToEmberSeerHandler()
		{
			var clearCenterLoc = new WoWPoint(185.2658, -314.7129, 76.92092);
			return async door => door != null && door.State == WoWGameObjectState.Ready 
								&& await ScriptHelpers.ClearArea(clearCenterLoc, 100, u => true);
		}

		[ObjectHandler(175706, "Blackrock Altar", ObjectRange = 36)]
		public async Task<bool> BlackrockAltarHandler(WoWGameObject altar)
		{
			var entranceDoor = ObjectManager.GetObjectsOfType<WoWGameObject>()
				.FirstOrDefault(u => u.IsValid && u.Entry == 175244);
			var emberseer = ObjectManager.GetObjectsOfType<WoWUnit>()
				.FirstOrDefault(u => u.IsValid && u.Entry == 9816);
			if (entranceDoor == null || emberseer == null)
				return false;
			var trash = GetAliveBlackhandIncarcerators();
			// clear trash around the boss to cause him to become active.
			if (Me.Z > 80f && entranceDoor.State == WoWGameObjectState.Active && emberseer.HasAura(15282))
			{
				if (trash.Any())
				{
					return await ScriptHelpers.ClearArea(emberseer.Location, 30, u => u.Entry == BlackhandIncarceratorId);
				}
				if (altar.DistanceSqr > 4*4)
					return (await CommonCoroutines.MoveTo(altar.Location, "Altar")).IsSuccessful();
				
				// wait for group members to get in room since door closes.
				if (!ScriptHelpers.GroupMembers.All(g => g.Guid == Me.Guid || InHallsOfBinding(g.Location)))
					return true;

				return await ScriptHelpers.InteractWithObject(altar, 8000);
			}
			// wait for boss to spawn
			if (emberseer.IsAlive && !emberseer.HasAura(15282) && !trash.Any() && !Me.Combat && Me.IsTank())
			{
				if (emberseer.DistanceSqr > 4*4)
					return (await CommonCoroutines.MoveTo(emberseer.Location)).IsSuccessful();
				return true;
			}
			return false;
		}

		private bool InHallsOfBinding(WoWPoint location)
		{
			return location.X > 114 && location.X < 175 && location.Y > -288 && location.Y < -229 && location.Z > 85 &&
					location.Z < 110;
		}

		private List<WoWUnit> GetAliveBlackhandIncarcerators()
		{
			return ObjectManager.GetObjectsOfType<WoWUnit>()
				.Where(u => u.Entry == BlackhandIncarceratorId && u.IsAlive && u.CanSelect && u.Attackable)
				.ToList();
		}

		[ObjectHandler(175186, "Boss Gate", ObjectRange = 120)]
		public Func<WoWGameObject, Task<bool>> DrakeHandker()
		{
			var waitForEvent = new WaitTimer(TimeSpan.FromSeconds(20));
			var leftDoorSide = new WoWPoint(107.9999, -418.0886, 110.9228);
			var rightDoorSide = new WoWPoint(108.0844, -422.6479, 110.9228);
			var locInsideDoor = new WoWPoint(115.694, -420.7484, 110.878);
			
			return async bossGate =>
			{
				WoWGameObject exitDoor = ObjectManager.GetObjectsOfType<WoWGameObject>()
					.FirstOrDefault(u => u.IsValid && u.Entry == 164726);

				var myLoc = Me.Location;

				if (IsInsideBlackrockStadium(myLoc))
				{
					if (ScriptHelpers.IsBossAlive("Pyroguard Emberseer"))
						ScriptHelpers.MarkBossAsDead("Pyroguard Emberseer");
					if (ScriptHelpers.IsBossAlive("Goraluk Anvilcrack"))
						ScriptHelpers.MarkBossAsDead("Goraluk Anvilcrack");
				}

				if (bossGate.State != WoWGameObjectState.Ready
					|| ScriptHelpers.IsBossAlive("Pyroguard Emberseer")
					|| exitDoor == null)
				{
					return false;
				}
				var leader = ScriptHelpers.Leader;
				var isLeader = leader != null && leader.IsMe;

				// if the door to The Beast is closed and the door to blackhand is close then move into blackhand's
				// room to start the event
				var exitDoorIsOpen = ((WoWDoor) exitDoor.SubObj).IsOpen;
				if (exitDoorIsOpen && Targeting.Instance.IsEmpty() && LootTargeting.Instance.IsEmpty())
				{
					if (isLeader)
					{
						// wait for group members to get inside room.
						if (IsInsideBlackrockStadium(myLoc)
							&& !ScriptHelpers.GroupMembers.All(g => g.Guid == Me.Guid || IsInsideBlackrockStadium(g.Location))
							&& myLoc.GetNearestPointOnSegment(leftDoorSide, rightDoorSide).DistanceSqr(myLoc) > 8 * 8)
						{
							await ScriptHelpers.StopMovingIfMoving();
							return true;
						}

						// move to the center of the room to start event.
						if (Me.Location.DistanceSqr(_drakeTankSpot) > 25 * 25)
						{
							waitForEvent.Reset();
							return (await CommonCoroutines.MoveTo(_drakeTankSpot)).IsSuccessful();
						}

						// wait for the exit door to close... stop waiting if event doesn't start (bug in WoW)
						if (!waitForEvent.IsFinished && ObjectManager.ObjectList.Any(o => o.Entry == WarchiefRendBlackhandId))
							return true;
					}
					else if (leader != null && IsInsideBlackrockStadium(leader.Location) && !IsInsideBlackrockStadium(Me.Location))
					{
						// force followers to enter room before door closes...
						return (await CommonCoroutines.MoveTo(locInsideDoor)).IsSuccessful();
					}

				}
				else if (!exitDoorIsOpen)
				{
					// If not close to leader or have aggro then move to leader.
					if (Me.IsFollower() && leader != null
						&& (leader.DistanceSqr > 15*15 || ObjectManager.GetObjectsOfType<WoWUnit>().Any(u => u.Aggro)))
					{
						var moveTo = WoWMathHelper.CalculatePointFrom(Me.Location, leader.Location, 4f);
						return (await CommonCoroutines.MoveTo(moveTo,"leader")).IsSuccessful();
					}
					// leader should sit at one spot and not get trapped on other side of the drake gate without a healer. 
					if (isLeader)
					{
						var target = Targeting.Instance.FirstUnit;
						if (target != null && target.Location.DistanceSqr(_drakeTankSpot) > 20*20)
							return await ScriptHelpers.TankUnitAtLocation(_drakeTankSpot, 20f);
						
						// do nothing while waiting for spawns.
						if (target == null)
							return true;
					}
				}
				return false;
			};
		}

		private bool IsInsideBlackrockStadium(WoWPoint location)
		{
			return location.X > 111 && location.X < 192 && location.Y > -455 && location.Y < -384 && location.Z > 105 &&
					location.Z < 125;
		}

		[EncounterHandler(10339, "Gyth")]
		public Func<WoWUnit,Task<bool>> GythHandler()
		{
			var frontAvoidArc = new ScriptHelpers.AngleSpan(0, 180);

			return async boss =>
						{
							if (!Me.IsTank() && boss.Distance < 15 && boss.CurrentTargetGuid != Me.Guid
								&& await ScriptHelpers.AvoidUnitAngles(boss, frontAvoidArc))
							{
								return true;
							}
							return await ScriptHelpers.DispelEnemy("Chromatic Chaos", ScriptHelpers.EnemyDispelType.Magic, boss)
								|| await ScriptHelpers.TankFaceUnitAwayFromGroup(15);
						};
		}

		[EncounterHandler(10429, "Warchief Rend Blackhand")]
		public Func<WoWUnit, Task<bool>> WarchiefRendBlackhandEncounter()
		{
			var frontAvoidArc = new ScriptHelpers.AngleSpan(0, 180);
			AddAvoidObject(ctx => !Me.IsTank(), 8, o => o.Entry == WarchiefRendBlackhandId && o.ToUnit().HasAura("Whirlwind"));
			return async boss =>
			{
				if (!Me.IsTank() && boss.Distance < 8 && boss.CurrentTargetGuid != Me.Guid
					&& await ScriptHelpers.AvoidUnitAngles(boss, frontAvoidArc))
				{
					return true;
				}
				return  await ScriptHelpers.TankFaceUnitAwayFromGroup(8);
			};
		}

		[EncounterHandler(10430, "The Beast")]
		public Func<WoWUnit, Task<bool>> TheBeastEncounter()
		{
			AddAvoidObject(ctx => 
				Me.IsRange() && !Me.IsCasting, 
				10, 
				o => o.Entry == TheBeastId && o.ToUnit().CurrentTargetGuid != Me.Guid && o.ToUnit().Combat);

			var tankLoc = new WoWPoint(70.44568, -541.3387, 110.9312);

			// tank against wall because of knockback.
			return async boss => await ScriptHelpers.TankUnitAtLocation(tankLoc, 5f);
		}


		[EncounterHandler(10363, "General Drakkisath")]
		public Func<WoWUnit, Task<bool>> GeneralDrakkisathEncounter()
		{
			const uint flameStrikeId = 16419;
			AddAvoidObject(
				ctx => true,
				o => Me.IsRange() && Me.IsMoving ? 6 : 5, 
				o => o is WoWPlayer && o.ToPlayer().HasAura("Conflagration") && !o.IsMe);

			AddAvoidObject(ctx => true, 5, flameStrikeId);

			var frontAvoidArc = new ScriptHelpers.AngleSpan(0, 180);

			return async boss =>
			{
				if (!Me.IsTank() && boss.Distance < 8 && boss.CurrentTargetGuid != Me.Guid
					&& await ScriptHelpers.AvoidUnitAngles(boss, frontAvoidArc))
				{
					return true;
				}
				return await ScriptHelpers.TankFaceUnitAwayFromGroup(8);
			};
		}
	}
}