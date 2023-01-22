using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Bots.DungeonBuddy.Behaviors;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Honorbuddy;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.POI;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Styx;
using System.Collections.Generic;
using Action = Styx.TreeSharp.Action;

using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Classic
{
	public class Gnomeregan : Dungeon
	{
		#region Overrides of Dungeon

		/// <summary>
		///   The mapid of this dungeon.
		/// </summary>
		/// <value> The map identifier. </value>
		public override uint DungeonId
		{
			get { return 14; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(-5149, 904, 287.4014); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(-325.03, -5.06, -152.84); }
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			foreach (var unit in incomingunits)
			{
				if (unit.Entry == BombId && (!Me.IsMelee() || Me.IsMelee() && unit.ToUnit().CurrentTargetGuid != Me.Guid) &&
					(!Me.IsTank() || _thermaplugg != null && _thermaplugg.IsValid && _thermaplugg.CurrentTargetGuid == Me.Guid))
					outgoingunits.Add(unit);

				if (unit.Entry == MobileAlertSystemId)
				{
					var maxDist = Me.IsTank() ? 40 : 25;
					if (unit.Distance > maxDist)
						continue;
					var pathDist = Me.Location.PathDistance(unit.Location, maxDist);
					if (!pathDist.HasValue || pathDist.Value >= maxDist)
						continue;
					outgoingunits.Add(unit);
				}
			}
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var t in units)
			{
				var prioObject = t.Object;

				//Bombs on the last boss vital to living
				if (prioObject.Entry == BombId)
				{
					t.Score += 1000;
				}
				//mobile alert systems, they spawn adds that wreck
				if (prioObject.Entry == MobileAlertSystemId)
				{
					t.Score += 1000;
				}
			}
		}

		readonly WoWPoint _entranceJumpStart = new WoWPoint(-388.4344, 64.41562, -156.9771);
		readonly WoWPoint _entranceJumpEnd = new WoWPoint(-407.6894, 74.10672, -210.975);
		readonly WoWPoint _electrocutionerJumpStart = new WoWPoint (-544.9977, 483.0126, -216.81);
		readonly WoWPoint _electrocutionerJumpEnd = new WoWPoint(-534.5251, 458.8114, -273.0771);

		private bool _dismissPetNJump;
		private WoWPoint _shortcutJumpToLoc;

		public override MoveResult MoveTo(WoWPoint location)
		{
			var myLoc = Me.Location;
			WoWPoint shortcutStart = WoWPoint.Zero;
			if (myLoc.DistanceSqr(_entranceJumpStart) < 30*30 && location.Z < -165 && Me.Z >= -165)
			{
				shortcutStart = _entranceJumpStart;
				_shortcutJumpToLoc = _entranceJumpEnd;
			}
			else if (myLoc.DistanceSqr(_electrocutionerJumpStart) < 30 * 30 && location.Z < -250 && Me.Z >= -250)
			{
				shortcutStart = _electrocutionerJumpStart;
				_shortcutJumpToLoc = _electrocutionerJumpEnd;	
			}

			if (shortcutStart != WoWPoint.Zero)
			{
				var parachute = GetParachute();
				if (parachute == null)
				{
					var box = (from gameObject in ObjectManager.GetObjectsOfType<WoWGameObject>()
							   where gameObject.Entry == ParachuteBoxId
							  let distSqr = gameObject.Location.DistanceSqr(shortcutStart)
							  where distSqr < 30 * 30
							  orderby distSqr
							  select gameObject).FirstOrDefault();

					if (box != null)
					{
						if (box.DistanceSqr > 3*3)
							return Navigator.MoveTo(box.Location);
						if (Me.IsMoving)
							WoWMovement.MoveStop();
						box.Interact();
						return MoveResult.Moved;
					}
				}

				if (myLoc.DistanceSqr(shortcutStart) > 7 * 7)
					return Navigator.MoveTo(shortcutStart);

				if (WoWPetControl.CanPetBeDismissed)
					_dismissPetNJump = true;
				else
					Navigator.PlayerMover.MoveTowards(_shortcutJumpToLoc);

				return MoveResult.Moved;
			}

			_dismissPetNJump = false;
			return base.MoveTo(location);
		}

		#endregion

		#region Root

		private const uint MobileAlertSystemId = 7849;
		private const uint CrowdPummeler960Id = 6229;
		private const uint BombId = 7915;
		private const uint ParachuteBoxId = 208325;
		private WoWUnit _thermaplugg;

		private const uint ParachuteId = 60680;
		public WoWItem GetParachute()
		{
			return StyxWoW.Me.CarriedItems.FirstOrDefault(i => i.Entry == ParachuteId);
		}

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0, "Root")]
		public Composite Root()
		{
			return
				new PrioritySelector(
				// use parachute when falling.
					new Decorator(
						ctx => Me.IsFalling,
						new PrioritySelector(
							ctx => GetParachute(),
							new Decorator(ctx => ctx != null && !Me.HasAura("Slow fall") && IsNearGround,
								new Sequence(
									new ActionLogger("Using parachute"),
									new Action(ctx => ((WoWItem)ctx).UseContainerItem()),
									new Sleep(1000))),							
								new ActionAlwaysSucceed())),

					new Decorator(ctx => _dismissPetNJump && WoWPetControl.CanPetBeDismissed && Targeting.Instance.IsEmpty(),
							new ActionRunCoroutine(ctx => DismissPetAndJump())));
		}

		async Task<bool> DismissPetAndJump()
		{
			if (!await WoWPetControl.DismissPet())
				return false;

			// we need to handle movement for pet classes here because CR will try re-summon pet if we let it
			Navigator.PlayerMover.MoveTowards(_shortcutJumpToLoc);
			await Coroutine.Wait(3000, () => Me.IsFalling);
			_dismissPetNJump = false;
			return true;
		}

		[EncounterHandler(44556, "Murd Doc", Mode = CallBehaviorMode.Proximity, BossRange = 40)]
		[EncounterHandler(44560, "B.E Barechus", Mode = CallBehaviorMode.Proximity, BossRange = 20)]
		[EncounterHandler(44561, "Face", Mode = CallBehaviorMode.Proximity, BossRange = 45)]
		[EncounterHandler(44563, "Hann Ibal", Mode = CallBehaviorMode.Proximity, BossRange = 45)]
		public Composite QuestGiversBehavior()
		{
			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				new Decorator(ctx => !Me.Combat && unit.QuestGiverStatus == QuestGiverStatus.Available, ScriptHelpers.CreatePickupQuest(ctx => unit)),
				new Decorator(ctx => !Me.Combat && unit.QuestGiverStatus == QuestGiverStatus.TurnIn, ScriptHelpers.CreateTurninQuest(ctx => unit)));
		}

		bool IsNearGround
		{
			get
			{
				var start = Me.Location;
				var end = start;
				end.Z -= 200;
				WoWPoint hit;
				if (GameWorld.TraceLine(start, end, TraceLineHitFlags.Collision, out hit))
					return start.DistanceSqr(hit) < 20 * 20;
				return false;
			}
		}

		#endregion

		[EncounterHandler(7361, "Grubbis", Mode = CallBehaviorMode.CurrentBoss)]
		public Composite GrubbisEncounter()
		{
			var escortStartLocation = new WoWPoint(-514.94, -138.54, -152.48);
			var escortEndLocation = new WoWPoint(-519.99, -124.85, -156.11);
			var finishEscortTimer = new Stopwatch();
			return new Decorator( ctx => DungeonBuddySettings.Instance.KillOptionalBosses,
				new PrioritySelector(
					ScriptHelpers.CreateTankTalkToThenEscortNpc(
						7998,
						1,
						escortStartLocation,
						escortEndLocation,
						6,
						ctx =>
						{
							var npc = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == 7998);
							// if Emi is at the escort end location and not merely passing by while walking to tunnels
							// then consider the escort to be finish.
							if (npc != null && npc.Location.DistanceSqr(escortEndLocation) < 1 * 1)
							{
								if (!finishEscortTimer.IsRunning)
									finishEscortTimer.Start();
								return finishEscortTimer.ElapsedMilliseconds > 3000;
							}
							if (finishEscortTimer.IsRunning)
								finishEscortTimer.Reset();
							return !ScriptHelpers.IsBossAlive("Grubbis");
						})));
		}

		[EncounterHandler(7079, "ViscousFallout")]
		public Composite ViscousFalloutFight()
		{
			return new PrioritySelector();
		}

		[EncounterHandler(6235, "Electrocutioner6000")]
		public Composite Electrocutioner6000Fight()
		{
			return new PrioritySelector();
		}

		[EncounterHandler(6229, "CrowdPummeler9-60")]
		public Composite CrowdPummeler960Fight()
		{
			AddAvoidObject(ctx => !Me.IsTank(), 6, u => u.Entry == 6229 && ((WoWUnit)u).CurrentTargetGuid != Me.Guid && ((WoWUnit)u).IsAlive,
				o => o.Location.RayCast(o.Rotation, 5));
			return new PrioritySelector();
		}

		[EncounterHandler(7800, "MekgineerThermaplugg")]
		public Composite MekgineerThermapluggFight()
		{
			WoWPoint centerLoc = new WoWPoint(-531.3243, 670.1588, -325.2682);

			var buttonIds = new uint[] { 142214, 142215, 142216, 142217, 142218, 142219 };
			var gnomeFaceIds = new uint[] { 142208, 142209, 142210, 142211, 142212, 142213 };

			AddAvoidObject(ctx => true, () => centerLoc, 35, 10, o => o.Entry == BombId && o.ToUnit().CharmedUnitGuid == Me.Guid);
			WoWGameObject button = null;
			return new PrioritySelector(
				ctx =>
				{
					button = (from b in ObjectManager.GetObjectsOfType<WoWGameObject>().Where(o => buttonIds.Contains(o.Entry))
							  let myDist = Me.Location.Distance(b.Location)
							  where
								  ObjectManager.GetObjectsOfType<WoWGameObject>().Where(o => gnomeFaceIds.Contains(o.Entry)).OrderBy(
									  o => o.Location.DistanceSqr(b.Location)).FirstOrDefault().State == WoWGameObjectState.Active &&
								  !Me.GroupInfo.RaidMembers.Where(p => p.HasRole(WoWPartyMember.GroupRole.Damage)).Select(p => p.ToPlayer()).Any(
									  p => p != null && p.Location.Distance(b.Location) < myDist && p.IsSafelyFacing(b, 45))
							  orderby myDist
							  select b).FirstOrDefault();
					return _thermaplugg = ctx as WoWUnit;
				},
				new Decorator(
					ctx => button != null && Me.IsDps(),
					new PrioritySelector(
						new Decorator(ctx => button.WithinInteractRange, new Action(ctx => button.Interact())),
						new Action(ctx => Navigator.MoveTo(button.Location)))));
		}
	}
}