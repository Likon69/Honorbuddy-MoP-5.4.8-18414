
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
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

	#region Normal Difficulty

	public class BattleOnTheHighSeasAlliance : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 655; }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			var myLoc = Me.Location;
			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (unit != null)
					{
						// try to elimination units that are on another boat.
						if (unit.DistanceSqr > 50*50 || !Navigator.CanNavigateFully(myLoc, unit.Location))
							return true;
					}
					return false;
				});
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			foreach (var obj in incomingunits)
			{
				var unit = obj as WoWUnit;
				if (unit != null)
				{
					if (unit.Combat && unit.IsHostile && unit.Distance < 40)
						outgoingunits.Add(unit);
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

		private bool _originalUseMount;

		public override void OnEnter()
		{
			// can't mount in this zone
			_originalUseMount = CharacterSettings.Instance.UseMount;
			CharacterSettings.Instance.UseMount = false;
		}

		public override void OnExit()
		{
			CharacterSettings.Instance.UseMount = _originalUseMount;
		}

		#endregion

		private const uint BarrelofOrangesId = 71054;
		private const uint BombsAwayTargetDummyId = 71024;
		private const uint TransportCannonId = 67343;
		private const uint TransportCannon2Id = 71167;
		private const uint RopeLadderId = 216077;

		#region Ship Navigation

		private const uint RopePile2Id = 219270;

		private const uint RopePileId = 216074;
		private readonly WaitTimer _interactTimer = new WaitTimer(TimeSpan.FromSeconds(4));

		private readonly List<List<WoWPoint>> _shipAreas = new List<List<WoWPoint>>
														   { // south alliance ship
															   new List<WoWPoint>
															   {
																   new WoWPoint(2170.883, -4199.843, 21.14248),
																   new WoWPoint(2164.876, -4261.232, 14.91238)
															   },
															   // center alliance ship
															   new List<WoWPoint>
															   {
																   new WoWPoint(2355.939, -4167.268, 15.23),
																   new WoWPoint(2336.548, -4107.725, 20.35676),
															   },
															   // center horde ship
															   new List<WoWPoint>
															   {
																   new WoWPoint(2313.285, -4193.405, 8.049974),
																   new WoWPoint(2341.682, -4250.631, 23.9635),
															   },
															   // North horde ship
															   new List<WoWPoint>
															   {
																   new WoWPoint(2452.808, -4049.638, 26.6615),
																   new WoWPoint(2460.377, -4119.236, 21.47732),
															   }
														   };

		public override MoveResult MoveTo(WoWPoint location)
		{
			if (Me.MapId != LfgDungeon.MapId)
				return MoveResult.Failed;

			if (!_interactTimer.IsFinished)
				return MoveResult.Moved;

			if (Me.TransportGuid != 0 || Me.HasAura("Rope Cosmetic"))
			{
				Navigator.Clear();
				Navigator.NavigationProvider.StuckHandler.Reset();
				return MoveResult.Moved;
			}

			if (Me.IsSwimming)
			{
				return MoveResult.Failed;
			}

			var myLoc = Me.Location;
			var currentShip = GetBoatFromLocation(myLoc);
			var destinationShip = GetBoatFromLocation(location);
			if (currentShip != destinationShip)
			{
				// wait... 
				if ((int) currentShip > (int) destinationShip)
				{
					return MoveResult.Moved;
				}
				var transportEntry = GetTransportIdForShip(currentShip);
				WoWObject transport = ObjectManager.ObjectList.FirstOrDefault(o => o.Entry == transportEntry && o.Distance <= 60);
				if (transport != null)
				{
					if (transport.WithinInteractRange)
					{
						transport.Interact();
						_interactTimer.Reset();
						return MoveResult.Moved;
					}
					return Navigator.MoveTo(transport.Location);
				}
			}
			return base.MoveTo(location);
		}

		private uint GetTransportIdForShip(ShipIdentification ship)
		{
			switch (ship)
			{
				case ShipIdentification.SouthAllianceShip:
					return TransportCannonId;
				case ShipIdentification.CenterAllianceShip:
					var stage = ScriptHelpers.CurrentScenarioInfo.CurrentStageNumber;
					return stage < 4 ? RopePileId : TransportCannon2Id;
				case ShipIdentification.CenterHordeShip:
					return RopePile2Id;
			}
			return 0;
		}

		private ShipIdentification GetBoatFromLocation(WoWPoint myLoc)
		{
			for (int i = 0; i < _shipAreas.Count; i++)
			{
				var nearestPoint = myLoc.GetNearestPointOnLine(_shipAreas[i][0], _shipAreas[i][1]);
				if (myLoc.Distance2DSqr(nearestPoint) <= 30*30)
				{
					return (ShipIdentification) i;
				}
			}
			return ShipIdentification.None;
		}


		private enum ShipIdentification
		{
			None = -1,
			SouthAllianceShip,
			CenterAllianceShip,
			CenterHordeShip,
			NorthHordeShip,
		}

		#endregion

		private readonly uint[] _netIds = new uint[] {216078, 216076};

		private readonly WoWPoint stage4Loc = new WoWPoint(2455.834, -4074.777, 9.03887);

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0)]
		public Composite RootEncounter()
		{
			AddAvoidObject(ctx =>true, 4, BombsAwayTargetDummyId);

			return new PrioritySelector(
				// move to and interact with nearest rope/net if swimming.
				new Decorator(
					ctx => Me.IsSwimming,
					new PrioritySelector(
						ctx => GetRope(),
						new ActionSetActivity(ctx => string.Format("swimming as fast as posible towards {0} before becoming shark food", ((WoWGameObject) ctx).Name)),
						new Decorator<WoWGameObject>(
							rope => rope.WithinInteractRange,
							new PrioritySelector(
								new Decorator(ctx => Me.IsMoving, new Action(ctx => WoWMovement.MoveStop())),
								new Helpers.Action<WoWGameObject>(rope => rope.Interact()))),
						new Helpers.Action<WoWGameObject>(
							rope =>
							{
								var loc = rope.Location;
								loc.Z = 0;
								// make sure navigator picks a point on mesh at water surface, not on boat.
								Navigator.MoveTo(loc);
							}))),
				// drink orange juice to heal.
				HealBehavior());
		}

		private WoWGameObject GetRope()
		{
			return
				ObjectManager.GetObjectsOfTypeFast<WoWGameObject>()
					.Where(g => g.Entry == RopeLadderId || _netIds.Contains(g.Entry))
					.OrderBy(g => g.Distance2DSqr)
					.FirstOrDefault();
		}

		private Composite HealBehavior()
		{
			//WoWUnit target;
			WoWObject healObject = null;

			return
				new Decorator(
					ctx =>
						Me.HealthPercent < 60 && !Targeting.Instance.TargetList.Any(t => t.IsWithinMeleeRange) &&
						(healObject =
							ObjectManager.ObjectList.Where(
								o => o.Entry == BarrelofOrangesId && o.Distance <= 40 && !AvoidanceManager.Avoids.Any(a => a.IsPointInAvoid(o.Location)))
								.OrderBy(o => o.DistanceSqr)
								.FirstOrDefault()) != null,
					new PrioritySelector(
						new Decorator(
							ctx => Me.Location.Distance(healObject.Location) > 3,
							new Action(ctx => Navigator.GetRunStatusFromMoveResult(Navigator.MoveTo(healObject.Location)))),
						new Decorator(
							ctx => Me.Location.Distance(healObject.Location) <= 3,
							new PrioritySelector(
								new Decorator(ctx => Me.IsMoving, new Action(ctx => WoWMovement.MoveStop())),
								new Sequence(new Action(ctx => healObject.Interact()), new WaitContinue(2, ctx => false, new ActionAlwaysSucceed()))))));
		}

		[ScenarioStage(1)]
		public Composite StageOneEncounter()
		{
			var stage1Loc = new WoWPoint(2168.544, -4237.59, 14.3587);
			return
				new PrioritySelector(
					new Decorator(ctx => Targeting.Instance.IsEmpty() && Me.Location.DistanceSqr(stage1Loc) > 10*10, new Action(ctx => Navigator.MoveTo(stage1Loc))),
					new Decorator(ctx => Targeting.Instance.IsEmpty() && Me.Location.DistanceSqr(stage1Loc) <= 10*10, new ActionAlwaysSucceed()));
		}

		[ScenarioStage(2)]
		public Func<ScenarioStage, Task<bool>> StageTwoEncounter()
		{
			const uint explosiveBarrelId = 70755;
			const uint bombId = 67403;
			const int fuseVisualAuraId = 141068;
			const int fireAuraId = 143222;

			AddAvoidObject(ctx => true, 4, o => o is WoWAreaTrigger && ((WoWAreaTrigger) o).SpellId == fireAuraId);
			AddAvoidObject(ctx => true, 4, o => o.Entry == bombId && o.ToUnit().HasAura("Bomb!"));
			AddAvoidObject(ctx => true, 4, o => o.Entry == explosiveBarrelId && o.ToUnit().HasAura(fuseVisualAuraId));


			const uint barrelofExplosivesId = 71106;

			var stage2Loc = new WoWPoint(2329.666, -4227.037, 11.32022);
			return async stageInfo =>
			{
				var barrel =
					ObjectManager.GetObjectsOfTypeFast<WoWUnit>()
						.Where(u => u.Entry == barrelofExplosivesId && u.DistanceSqr < 40 * 40)
						.OrderBy(u => u.DistanceSqr)
						.FirstOrDefault();

				if (barrel != null && await ScriptHelpers.InteractWithObject(barrel, 3000, true))
					return true;

				if (GetBoatFromLocation(Me.Location) == ShipIdentification.CenterHordeShip)
				{
					await ScriptHelpers.ClearArea(stage2Loc, 40, checkPathDistance: false);
					return false;
				}
				ScriptHelpers.SetLeaderMoveToPoi(stage2Loc);
				return false;
			};
		}

		[EncounterHandler(70963, "Lieutenant Fizzel")]
		public Composite LieutenantFizzelEncounter()
		{
			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateDispelEnemy("Volatile Concoction", ScriptHelpers.EnemyDispelType.Magic, ctx => boss));
		}

		[ScenarioStage(3)]
		public Composite StageThreeEncounter()
		{
			ScenarioStage stage = null;
			WoWUnit barrel = null;
			const uint plantExplosivesId = 67394;
			var stage3Loc = new WoWPoint(2326.5, -4223.025, 3.988861);
			return new PrioritySelector(
				ctx =>
				{
					barrel =
						ObjectManager.GetObjectsOfTypeFast<WoWUnit>()
							.Where(u => u.Entry == plantExplosivesId && u.HasAura("Empty"))
							.OrderBy(u => u.DistanceSqr)
							.FirstOrDefault();
					return stage = ctx as ScenarioStage;
				},
				// plant explosives..
				new Decorator(
					ctx => !stage.GetStep(1).IsComplete,
					new PrioritySelector(
						new Decorator(ctx => barrel != null && !barrel.WithinInteractRange, new Action(ctx => Navigator.MoveTo(barrel.Location))),
						new Decorator(
							ctx => barrel != null && barrel.WithinInteractRange,
							new Action(
								ctx =>
								{
									if (!Me.IsCasting)
									{
										if (Me.IsMoving)
											WoWMovement.MoveStop();
										barrel.Interact();
									}
								})),
						new Decorator(ctx => Me.Location.DistanceSqr(stage3Loc) > 50*50, new Action(ctx => Navigator.MoveTo(stage3Loc))))),
				new Decorator(ctx => stage.GetStep(1).IsComplete, new Action(ctx => Navigator.MoveTo(stage4Loc))));
		}

		[ScenarioStage(4)]
		public Composite StageFourEncounter()
		{
			return
				new PrioritySelector(
					new Decorator(ctx => GetBoatFromLocation(Me.Location) == ShipIdentification.NorthHordeShip, ScriptHelpers.CreateClearArea(() => stage4Loc, 60)),
					new Decorator(
						ctx => Targeting.Instance.IsEmpty() && GetBoatFromLocation(Me.Location) != ShipIdentification.NorthHordeShip && Me.HealthPercent > 80,
						new Action(ctx => Navigator.MoveTo(stage4Loc))));
		}

		[EncounterHandler(67426, "Final Boss")]
		public Composite FinalBossEncounter()
		{
			WoWUnit boss = null;
			WoWGameObject rapier = null;
			const int duelistAuraId = 141153;
			const uint rapierId = 219304;
			const int verticalSlashSpellId = 141187;
			const int ripsoteId = 141154;
			const int rapierParryId = 141152;
			SpellActionButton extraActionButton = SpellActionButton.ExtraActionButton;

			return new PrioritySelector(
				ctx =>
				{
					if (Me.IsTank())
						extraActionButton.Update();
					return boss = ctx as WoWUnit;
				},
				// Pickup rapier if tank.
				new Decorator(
					ctx => Me.IsTank() && !Me.HasAura(duelistAuraId) && (rapier = ObjectManager.ObjectList.FirstOrDefault(o => o.Entry == rapierId) as WoWGameObject) != null,
					new PrioritySelector(
						new Decorator(
							ctx => rapier.WithinInteractRange,
							new PrioritySelector(new Decorator(ctx => Me.IsMoving, new Action(ctx => WoWMovement.MoveStop())), new Action(ctx => rapier.Interact()))),
						new Decorator(ctx => !rapier.WithinInteractRange, new Action(ctx => Navigator.MoveTo(rapier.Location))))),
				// ripsote
				new Decorator(ctx => extraActionButton.SpellId == ripsoteId && boss.IsWithinMeleeRange, new Action(ctx => extraActionButton.Use())),
				// Rapier Usage Behavior
				new Decorator(
					ctx => Me.HasAura(duelistAuraId),
					new PrioritySelector(
						new Decorator(
							ctx => boss.CastingSpellId == verticalSlashSpellId && extraActionButton.SpellId == rapierParryId,
							new Sequence(
								new DecoratorContinue(ctx => Me.IsMoving, new Action(ctx => WoWMovement.MoveStop())),
								new WaitContinue(1, ctx => !Me.IsMoving, new ActionAlwaysSucceed()),
								new Action(ctx => boss.Face()),
								new Action(ctx => extraActionButton.Use()),
								new WaitContinue(2, ctx => boss.CastingSpellId != verticalSlashSpellId, new ActionAlwaysSucceed()))))));
		}
	}

	#endregion

	#region Heroic Difficulty

	public class BattleOnTheHighSeasHeroicAlliance : BattleOnTheHighSeasAlliance
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 588; }
		}

		#endregion
	}

	#endregion
}