using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;

using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
using Action = Styx.TreeSharp.Action;

namespace Bots.DungeonBuddy.Dungeon_Scripts.Burning_Crusade
{
	public class Underbog : CoilfangDungeon
	{
		#region Overrides of Dungeon

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(780.7808, 6745.077, -72.53828); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(4.695599, -11.38073, -2.751475); }
		}

		public override uint DungeonId
		{
			get { return 146; }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				unit =>
				{
					// remove Ghaz'an if swiming around.
					if (unit.Entry == GhazanId && unit.Z < 70)
						return true;
					return false;
				});
		}


		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var targetPriority in units)
			{
				switch (targetPriority.Object.Entry)
				{
					case MurkbloodHealerId:
						if (StyxWoW.Me.IsDps())
							targetPriority.Score += 220;
						break;
				}
			}
		}

		public override void IncludeLootTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			var gatherHibiscus = ScriptHelpers.HasQuest(BringMeAShrubberyQuestId) 
				&& !ScriptHelpers.IsQuestInLogComplete(BringMeAShrubberyQuestId);
			foreach (var gObj in incomingunits.OfType<WoWGameObject>())
			{
				if (gObj.Entry == SanguineHibiscusId && gatherHibiscus
					&& !Me.Combat && gObj.DistanceSqr < 30 * 30 
					&& !ScriptHelpers.WillPullAggroAtLocation(gObj.Location))
				{
					outgoingunits.Add(gObj);
				}
			}
		}

		#endregion

		#region Root

		private const uint SanguineHibiscusId = 183385;
		private const uint RescuingTheExpeditionQuestId = 29570;
		private const uint BringMeAShrubberyQuestId = 29691;
		private const uint StalkTheStalkerQuestId = 29567;
		private const uint ANecessaryEvilQuestId = 29568;
		private static readonly uint[] EntraceQuestIds =
		{
			RescuingTheExpeditionQuestId, BringMeAShrubberyQuestId, StalkTheStalkerQuestId, ANecessaryEvilQuestId
		};
		private const uint GhazanId = 18105;
		private const uint MurkbloodHealerId = 17730;
		private readonly WoWPoint _ghazanFollowerWaitSpot = new WoWPoint(162.3641, -445.7501, 72.43507);
		private readonly WoWPoint _ghazanTankSpot = new WoWPoint(152.2157, -467.3924, 75.0878);
		private WoWUnit _hungarfen;
		readonly WoWPoint _lastBossLocation = new WoWPoint(150.6087, 21.06009, 26.84999);

		[EncounterHandler(0)]
		public async Task<bool> RootBehavior()
		{
			if (Me.IsSwimming && Me.X < 160 && !Me.Combat)
			{
				ScriptHelpers.TelportOutsideLfgInstance();
				return true;
			}
			// port outside and back in to hand in quests once dungeon is complete.
			// QuestPickupTurninHandler will handle the turnin
			return IsComplete && !Me.Combat
					&& EntraceQuestIds.Any(ScriptHelpers.IsQuestInLogComplete)
					&& LootTargeting.Instance.IsEmpty()
					&& Me.Location.DistanceSqr(_lastBossLocation) < 50 * 50
					&& await ScriptHelpers.PortOutsideAndBackIn();
		}



		[EncounterHandler(54678, "Naturalist Bite", Mode = CallBehaviorMode.Proximity, BossRange = 30)]
		[EncounterHandler(54674, "T'shu", Mode = CallBehaviorMode.Proximity, BossRange = 30)]
		[EncounterHandler(54675, "Watcher Jhang", Mode = CallBehaviorMode.Proximity, BossRange = 30)]
		public async Task<bool> QuestPickupTurninHandler(WoWUnit npc)
		{
			if (Me.Combat || ScriptHelpers.WillPullAggroAtLocation(npc.Location))
				return false;
			return npc.HasQuestAvailable()
				? await ScriptHelpers.PickupQuest(npc)
				: npc.HasQuestTurnin() && await ScriptHelpers.TurninQuest(npc);
		}

		private const uint EarthbinderRaygeId = 17885;

		[LocationHandler(296.6974, -362.373, 50.15062, 65, "Earthbinder Rayge Area")]
		public async Task<bool> EarthbinderRaygeFindHandler(WoWPoint location)
		{
			if (Me.Combat)
				return false;

			if (!ScriptHelpers.HasQuest(RescuingTheExpeditionQuestId))
				return false;
			if (ScriptHelpers.IsQuestInLogComplete(RescuingTheExpeditionQuestId))
				return false;
			if (ScriptHelpers.IsQuestObjectiveComplete(1, RescuingTheExpeditionQuestId))
				return false;

			var rayge = ObjectManager.GetObjectsOfType<WoWUnit>()
				.FirstOrDefault(u => u.Entry == EarthbinderRaygeId);

			if (rayge == null)
				return (await CommonCoroutines.MoveTo(location)).IsSuccessful();
			return await ScriptHelpers.TalkToNpc(rayge);
		}

		[EncounterHandler(17894, "Windcaller Claw", Mode = CallBehaviorMode.Proximity, BossRange = 85)]
		public async Task<bool> WindcallerClawEncounter(WoWUnit npc)
		{
			if (Me.Combat || npc.IsHostile)
				return false;

			if (!ScriptHelpers.HasQuest(RescuingTheExpeditionQuestId))
				return false;
			if (ScriptHelpers.IsQuestInLogComplete(RescuingTheExpeditionQuestId))
				return false;

			if (ScriptHelpers.IsQuestObjectiveComplete(2, RescuingTheExpeditionQuestId))
				return false;

			return await ScriptHelpers.TalkToNpc(npc);
		}

		#endregion



		[EncounterHandler(17770, "Hungarfen")]
		public Composite HungarfenEncounter()
		{
			const uint underbogMushroomId = 17990;

			AddAvoidObject(ctx => !Me.IsCasting, 10, underbogMushroomId);

			return new PrioritySelector(ctx => _hungarfen = ctx as WoWUnit, ScriptHelpers.CreateSpreadOutLogic(ctx => true, ctx => _hungarfen.Location, 10, 30));
		}


		[EncounterHandler(18105, "Ghaz'an", Mode = CallBehaviorMode.Proximity, BossRange = 105)]
		public Composite GhazanEncounter()
		{
			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				// pull boss. check if he's not in the water first.
				new Decorator(
					ctx => StyxWoW.Me.IsTank() && (Targeting.Instance.IsEmpty() || Targeting.Instance.FirstUnit.Entry == GhazanId) && boss.Z > 70 && Me.Y < -435,
					new Sequence(
						new DecoratorContinue(ctx => Me.CurrentTargetGuid != boss.Guid,
							new Action(ctx => boss.Target())),
						ScriptHelpers.CreateMoveToContinue(ctx => boss.Distance > 30, ctx => boss, false),
						ScriptHelpers.CreateCastRangedAbility(),
						ScriptHelpers.CreateMoveToContinue(ctx => _ghazanTankSpot))),
				// wait for tank to move in place.
				ScriptHelpers.CreateWaitAtLocationUntilTankPulled(ctx => StyxWoW.Me.Location.DistanceSqr(_ghazanFollowerWaitSpot) < 15 * 15, ctx => _ghazanFollowerWaitSpot),
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(
					ctx => !Me.IsTank() && boss.Distance < 20 && boss.CurrentTargetGuid != Me.Guid,
					ctx => boss,
					new ScriptHelpers.AngleSpan(0, 100),
					new ScriptHelpers.AngleSpan(180, 100)),
				new Decorator(ctx => boss.CurrentTargetGuid == Me.Guid && Me.IsTank(), ScriptHelpers.CreateTankUnitAtLocation(ctx => _ghazanTankSpot, 5f)));
		}

		[EncounterHandler(17826, "Swamplord Musel'ek", Mode = CallBehaviorMode.Proximity, BossRange = 100)]
		public Composite SwamplordMuselekEncounter()
		{
			return new PrioritySelector(
				ScriptHelpers.CreateClearArea(() => new WoWPoint(214.0852, -131.6859, 27.32064), 50f, u => u.Entry == 17734) // kill the Underbog Lord
				);
		}

		[EncounterHandler(17882, "The Black Stalker")]
		public Composite TheBlackStalkerEncounter()
		{
			return new PrioritySelector(
				ScriptHelpers.CreateSpreadOutLogic(ctx => true, ctx => ScriptHelpers.Tank.Location, 13, 30f) // spread out
				);
		}
	}

	public abstract class CoilfangDungeon : Dungeon
	{
		protected LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		#region corpse run behavior

		private readonly CircularQueue<WoWPoint> _tunnelPath = new CircularQueue<WoWPoint>()
													{
														new WoWPoint(563.1432f, 6943.247f, -1.951628f),
														new WoWPoint(568.424f, 6941.722f, -24.89723f),
														new WoWPoint(582.709f, 6936.663f, -38.11215f),
														new WoWPoint(602.8282f, 6915.195f, -45.36474f),
														new WoWPoint(611.301f, 6895.487f, -51.30582f),
														new WoWPoint(637.5763f, 6869.115f, -79.11536f),
														new WoWPoint(667.0811f, 6865.27f, -81.14445f),
														new WoWPoint(667.0811f, 6865.27f, -70.73276f),
													};

		readonly WoWPoint _corpseRunSubmergeStart = new WoWPoint(561.1754f, 6944.772f, 16.60149f);

		public override MoveResult MoveTo(WoWPoint location)
		{
			// Coilfang corpse run. 
			if (Me.IsGhost)
			{
				var myLoc = Me.Location;
				// Outside and above water logic
				if (Me.Z > 12)
				{
					// move to a point on the outside water surface just above underwater tunnel 
					if (myLoc.DistanceSqr(_corpseRunSubmergeStart) > 10 * 10)
						return Navigator.MoveTo(_corpseRunSubmergeStart);

					if (_tunnelPath.Peek() != _tunnelPath.First)
						_tunnelPath.CycleTo(_tunnelPath.First);

					// submerge. We can only break through the water's surface if player's vertical pitch is facing down. 
					if (!Me.IsSwimming)
						Lua.DoString("VehicleAimIncrement(-1)");
					else
						Navigator.PlayerMover.MoveTowards(_tunnelPath.First);
					return MoveResult.Moved;
				}
				// tunnel navigation.
				if (Me.IsSwimming)
				{
					var moveTo = _tunnelPath.Peek();
					if (myLoc.DistanceSqr(moveTo) < 5 * 5)
					{
						_tunnelPath.Dequeue();
						moveTo = _tunnelPath.Peek();
						// Tunnel path ends at the water surface of the underwater pool. Jump to walk on the surface.
						if (moveTo == _tunnelPath.First)
						{
							WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
							WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend);
							return MoveResult.Moved;
						}
					}
					Navigator.PlayerMover.MoveTowards(moveTo);
					return MoveResult.Moved;
				}

			}
			return base.MoveTo(location);
		}

		public override void OnEnter()
		{
			// if coming back from a DC and a ghost then make sure current point in _tunnelPath is closest one.
			if (Me.IsGhost && Me.IsSwimming)
			{
				var myLoc = Me.Location;
				_tunnelPath.CycleTo(_tunnelPath.OrderBy(loc => loc.DistanceSqr(myLoc)).FirstOrDefault());
			}
			base.OnEnter();
		}

		#endregion

	}
}