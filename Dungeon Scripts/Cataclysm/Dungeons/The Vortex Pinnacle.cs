using System;
using System.Linq;
using CommonBehaviors.Actions;
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.Helpers;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.TreeSharp;
using Styx;
using Styx.WoWInternals.WoWObjects;
using System.Collections.Generic;
using Action = Styx.TreeSharp.Action;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
using Vector2 = Tripper.Tools.Math.Vector2;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Cataclysm
{
	public class VortexPinnacle : Dungeon
	{
		#region Overrides of Dungeon

		private const uint LurkingTempest = 45704;
		private const uint HowlingGale = 45572;

		/// <summary>
		///     The Map Id of this dungeon. This is the unique id for dungeons thats used to determine which dungeon, the script
		///     belongs to
		/// </summary>
		/// <value> The map identifier. </value>
		public override uint DungeonId
		{
			get { return 311; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(-11523.62f, -2319.026f, 613.0181f); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(-328.96f, 23.49f, 626.98f); }
		}

		/*
		public override CircularQueue<WoWPoint> CorpseRunBreadCrumb
		{
			get
			{
				return _corpseRunBreadCrumb ??
					   (_corpseRunBreadCrumb =
						new CircularQueue<WoWPoint>
							{
								new WoWPoint(-11518.17f, -2166.622f, 513.0512f),
								new WoWPoint(-11509.4f, -2285.51f, 614.3616f),
								new WoWPoint(-11523.62f, -2319.026f, 613.0181f)
							});
			}
		}*/

		public override bool IsFlyingCorpseRun
		{
			get { return true; }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret as WoWUnit;
					if (unit != null)
					{
						if (unit.Entry == LurkingTempest && ret.ToUnit().HasAura("Feign Death")) // can't kill these
						{
							if (Me.GotAlivePet && Me.Pet.CurrentTarget == unit)
							{
								Lua.DoString("PetFollow()");
							}
							return true;
						}
						// remove howling gale if the knockback aura is disabled.
						if (unit.Entry == HowlingGale && (WoWMissile.InFlightMissiles.All(m => m.CasterGuid != ret.Guid) || (unit.X >= Me.X + 5 && unit.Y <= Me.Y + 5)))
						{
							if (StyxWoW.Me.GotAlivePet && Me.Pet.CurrentTarget == ret)
							{
								Lua.DoString("PetFollow()");
							}
							return true;
						}
						if (unit.Entry == GrandVizierErtanId && _ignoreErtan)
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
					if (unit.Entry == HowlingGale && unit.Distance <= 30 && WoWMissile.InFlightMissiles.Any(m => m.CasterGuid == unit.Guid) &&
						(unit.X < Me.X + 5 || unit.Y > Me.Y + 5))
						outgoingunits.Add(unit);

					if (unit.Entry == SkyfallStarId && unit.Combat && unit.DistanceSqr <= 40 * 40)
						outgoingunits.Add(unit);
				}
			}
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var priority in units)
			{
				var unit = priority.Object as WoWUnit;
				if (unit != null)
				{
					if (unit.Entry == TempleAdeptId && Me.IsDps() && (Me.IsRange() && !unit.HasAura("Grounding Field") || Me.IsMelee()))
						priority.Score += 10000;

					if (unit.HasAura("Grounding Field") && Me.IsRange() && Me.Class != WoWClass.Hunter)
						priority.Score -= 1000; ;

					// we should only attack howling gale if nothing else is attacking group.
					if (unit.Entry == HowlingGaleId)
						priority.Score -= 10000;
				}
			}
		}

		#endregion

		#region Encounter Handlers

		private const uint GrandVizierErtanId = 43878;

		private const uint HealingWellId = 88201;
		private const uint GroundingFieldId = 47085;
		private const uint ExecutoroftheCaliphId = 45928;
		private const uint SkyfallStarId = 45932;
		private const int HowlingGaleSpellId = 85086;
		private const uint HowlingGaleId = 45572;
		const uint TempleAdeptId = 45935;

		// leads to middle area (at 1st boss)
		private static readonly WoWPoint FirstAreaSlipStreamLoc1 = new WoWPoint(-768.213f, -53.6862f, 639.926f);
		// leads to middle area (at entrance)
		private static readonly WoWPoint FirstAreaSlipStreamLoc2 = new WoWPoint(-322.3307, -25.92011, 626.9792);
		// leads to last area
		private static readonly WoWPoint FirstAreaSlipStreamLoc3 = new WoWPoint(-379.3557, 30.80832, 626.9794);

		private static readonly WoWPoint SecondAreaSlipStreamLoc1 = new WoWPoint(-1196.27, 109.4188, 740.7067);

		private static readonly WoWPoint[] SlipStreamLocations = { FirstAreaSlipStreamLoc1, FirstAreaSlipStreamLoc2, FirstAreaSlipStreamLoc3, SecondAreaSlipStreamLoc1 };

		private readonly DungeonArea _firstArea = new DungeonArea(
			new Vector2(-375.6963f, 115.0375f),
			new Vector2(-246.2538f, 6.154482f),
			new Vector2(-556.3135f, -324.7931f),
			new Vector2(-864.868f, -22.49899f),
			new Vector2(-670.7045f, 150.8543f));

		private readonly DungeonArea _secondArea = new DungeonArea(
			new Vector2(-845.3011f, -132.8148f),
			new Vector2(-944.6406f, -256.0248f),
			new Vector2(-1311.896f, 33.37508f),
			new Vector2(-1180.948f, 170.9117f));

		private readonly WaitTimer _slipStreamTimer = new WaitTimer(TimeSpan.FromSeconds(15));

		private readonly DungeonArea _thirdArea = new DungeonArea(
			new Vector2(-532.2311f, 669.4817f),
			new Vector2(-541.2621f, 380.0819f),
			new Vector2(-1189.979f, 387.2452f),
			new Vector2(-1211.051f, 649.4243f));

		private bool _ignoreErtan;

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0)]
		public Composite CreateBehavior_RootLogic()
		{
			AddAvoidObject(
				ctx =>
					(Me.IsTank() && Targeting.Instance.TargetList.All(t => !t.IsTargetingMyPartyMember && t.ThreatInfo.ThreatStatus >= ThreatStatus.NoobishTank) || !Me.IsTank() && Targeting.Instance.TargetList.Any(t => t.CurrentTargetGuid == Me.Guid))
					&& Targeting.Instance.TargetList.Any(t => t.HasAura("Grounding Field")),
				50,
				o => o.Entry == GroundingFieldId && o.ToUnit().HasAura("Grounding Field"));

			WoWPoint slipStreamMoveTo = WoWPoint.Zero;

			return new PrioritySelector(
				ctx => ScriptHelpers.Tank,
				new Decorator<WoWPlayer>(
					tank => tank != null && !tank.IsMe && tank.HasAura("Slipstream") && !Me.HasAura("Slipstream"),
					new PrioritySelector(
						ctx =>
						{
							var slipStream = GetSlipStreamNearLocation(((WoWPlayer)ctx).Location);
							if (slipStream != null)
							{
								var slipStreamLoc = slipStream.Location;
								slipStreamMoveTo = SlipStreamLocations.OrderBy(l => l.DistanceSqr(slipStreamLoc)).FirstOrDefault();
							}
							else
							{
								slipStreamMoveTo = WoWPoint.Empty;
							}
							return slipStream;
						},
						new Decorator(
							ctx => ctx != null,
							new PrioritySelector(
								new Decorator(ctx => Me.Location.DistanceSqr(slipStreamMoveTo) > 3 * 3, new Action(ctx => Navigator.MoveTo(slipStreamMoveTo))),
								new Helpers.Action<WoWUnit>(slipStream => slipStream.Interact()))))));
		}

		[EncounterHandler(49943, "Itesh", Mode = CallBehaviorMode.Proximity, BossRange = 35)]
		public Composite QuestPickupHandler()
		{
			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				new Decorator(
					ctx => !Me.Combat && !ScriptHelpers.WillPullAggroAtLocation(unit.Location) && unit.QuestGiverStatus == QuestGiverStatus.Available,
					ScriptHelpers.CreatePickupQuest(ctx => unit)),
				new Decorator(
					ctx => !Me.Combat && !ScriptHelpers.WillPullAggroAtLocation(unit.Location) && unit.QuestGiverStatus == QuestGiverStatus.TurnIn,
					ScriptHelpers.CreateTurninQuest(ctx => unit)));
		}

		[EncounterHandler(43878, "Grand Vizier Ertan")]
		public Composite ErtanLogic()
		{
			WoWUnit boss = null;
			const int vortexId = 46007;
			AddAvoidObject(ctx => true, 8, o => o.Entry == GrandVizierErtanId && _ignoreErtan);
			AddAvoidObject(ctx => true, 5, vortexId);

			return new PrioritySelector(
				ctx =>
				{
					boss = ctx as WoWUnit;
					_ignoreErtan = Me.IsMelee() && ObjectManager.GetObjectsOfType<WoWUnit>().Any(u => u.Entry == vortexId && u.Location.Distance2DSqr(boss.Location) <= 10 * 10);
					return boss;
				},
				new Decorator(
					ctx => ctx is WoWUnit && boss.DistanceSqr > 15 * 15,
					new Sequence(
						new Action(ctx => Logger.Write("[Ertan Encounter] Getting closer to the boss")),
						new Action(ctx => Navigator.PlayerMover.MoveTowards((boss.Location))))));
		}


		[EncounterHandler(43875, "Asaad")]
		public Composite AsaadLogic()
		{
			const int unstableGroundingFieldSpell = 86911;
			const int supremacyOfTheStorm = 86930;
			const int stormTarget = 46387;
			WoWUnit boss = null;
			WoWPoint groundingFieldLocation = WoWPoint.Zero;

			var groundingFieldWaitTimer = new WaitTimer(TimeSpan.FromSeconds(4));

			return new PrioritySelector(
				ctx =>
				{
					var stormTargets =
						ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == stormTarget && (u.HasAura(86921) || u.HasAura(86923) || u.HasAura(86925))).ToList();
					if (stormTargets.Any())
						groundingFieldLocation = stormTargets.Aggregate(WoWPoint.Zero, (current, woWPoint) => current + woWPoint.Location) / stormTargets.Count;
					else
						groundingFieldLocation = WoWPoint.Zero;

					return boss = ctx as WoWUnit;
				},
				new Decorator(
					ctx =>
						(boss.CastingSpellId == unstableGroundingFieldSpell || boss.CastingSpellId == supremacyOfTheStorm || !groundingFieldWaitTimer.IsFinished) &&
						groundingFieldLocation != WoWPoint.Zero,
					new PrioritySelector(
						new Sequence(
							new Action(ctx => Logger.Write("[Asaad Encounter] Running inside grounding field")),
							new Action(ctx => groundingFieldWaitTimer.Reset()),
							new Action(ctx => Navigator.PlayerMover.MoveTowards(groundingFieldLocation))),
				// stop behavior here (prevents moving out of triangle to kill adds.
						new ActionAlwaysSucceed())),
				new Decorator(
					ctx => groundingFieldWaitTimer.IsFinished,
					ScriptHelpers.CreateSpreadOutLogic(ctx => groundingFieldWaitTimer.IsFinished, ctx => boss.Location, 13, 30)));
		}

		[EncounterHandler(45572, "Howling Gale", Mode = CallBehaviorMode.Proximity, BossRange = 35)]
		public Composite HowlingGaleEncounter()
		{
			var bridgePathStart = new WoWPoint(-1034.94f, -74.5419f, 699.2719f);
			var bridgePathEnd = new WoWPoint(-1091.647, -16.47018, 703.9303);

			WoWPoint nearestPointOnBrigePath = WoWPoint.Zero;

			WoWUnit orb = null;
			return new Decorator(
				ctx => StyxWoW.Me.X > -1090.885,
				new PrioritySelector(
					ctx =>
					{
						nearestPointOnBrigePath = StyxWoW.Me.Location.GetNearestPointOnLine(bridgePathStart, bridgePathEnd);
						return orb = ctx as WoWUnit;
					},
				// if bot strays too far from path then move back to it.
					new Decorator(
						ctx => StyxWoW.Me.Location.DistanceSqr(nearestPointOnBrigePath) > 3 * 3,
						new Action(ctx => Navigator.PlayerMover.MoveTowards(nearestPointOnBrigePath))),
					new Decorator(ctx => Targeting.Instance.IsEmpty() && (Me.X > -1088.756 || Me.Y < -19.47345), new Action(ctx => Navigator.MoveTo(bridgePathEnd)))));
		}

		[EncounterHandler(45919, "Young Storm Dragon")]
		public Composite YoungStormDragonEncounter()
		{
			WoWUnit drake = null;
			AddAvoidObject(ctx => Me.IsTank() && Targeting.Instance.TargetList.All(t => !t.IsTargetingMyPartyMember && t.ThreatInfo.ThreatStatus >= ThreatStatus.NoobishTank), 30, HealingWellId);


			return new PrioritySelector(
				ctx => drake = ctx as WoWUnit,
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(
					ctx => !Me.IsTank() && drake.CurrentTargetGuid != Me.Guid && !drake.IsMoving && drake.Distance < 15,
					ctx => drake,
					new ScriptHelpers.AngleSpan(0, 100)),
				ScriptHelpers.CreateTankFaceAwayGroupUnit(17));
		}

		public override MoveResult MoveTo(WoWPoint location)
		{
			var myArea = GetMyArea();
			var destinationArea = GetDestinationArea(location);
			if (myArea != destinationArea && myArea != null && destinationArea != null)
			{
				if (!_slipStreamTimer.IsFinished)
					return MoveResult.Moved;

				if (myArea == _firstArea)
				{
					WoWPoint slipStreamLoc;
					if (destinationArea == _secondArea)
					{
						slipStreamLoc = (!ScriptHelpers.IsBossAlive("Grand Vizier Ertan") || GetSlipStreamNearLocation(FirstAreaSlipStreamLoc2) != null) &&
										StyxWoW.Me.Location.DistanceSqr(FirstAreaSlipStreamLoc2) < StyxWoW.Me.Location.DistanceSqr(FirstAreaSlipStreamLoc1)
							? FirstAreaSlipStreamLoc2
							: FirstAreaSlipStreamLoc1;
					}
					else
						slipStreamLoc = FirstAreaSlipStreamLoc3;

					var slipStream = GetSlipStreamNearLocation(slipStreamLoc);
					if (slipStream != null && slipStream.Distance2DSqr <= 20 * 20)
					{
						slipStream.Interact();
						_slipStreamTimer.Reset();
					}
					else
						return Navigator.MoveTo(slipStreamLoc);
				}

				if (myArea == _secondArea)
				{
					if (destinationArea == _firstArea)
					{
						// port out - bot will auto port back in. this is the only way to get to 1st area.
						Lua.DoString("LFGTeleport(true)");
					}
					else if (destinationArea == _thirdArea)
					{
						var slipStream = GetSlipStreamNearLocation(SecondAreaSlipStreamLoc1);
						if (slipStream != null && slipStream.Distance2DSqr <= 20 * 20)
						{
							slipStream.Interact();
							_slipStreamTimer.Reset();
						}
						else
							return Navigator.MoveTo(SecondAreaSlipStreamLoc1);
					}
				}
				if (myArea == _thirdArea)
				{
					// port out - bot will auto port back in. this is the only way to get to 1st area.
					Lua.DoString("LFGTeleport(true)");
				}
				return MoveResult.Moved;
			}
			return MoveResult.Failed;
		}

		private DungeonArea GetMyArea()
		{
			var myLoc = StyxWoW.Me.Location;

			if (_firstArea.IsPointInPoly(myLoc))
				return _firstArea;

			if (_secondArea.IsPointInPoly(myLoc))
				return _secondArea;

			if (_thirdArea.IsPointInPoly(myLoc))
				return _thirdArea;

			return null;
		}

		private DungeonArea GetDestinationArea(WoWPoint destination)
		{
			if (_firstArea.IsPointInPoly(destination))
				return _firstArea;

			if (_secondArea.IsPointInPoly(destination))
				return _secondArea;

			if (_thirdArea.IsPointInPoly(destination))
				return _thirdArea;

			return null;
		}

		private WoWUnit GetSlipStreamNearLocation(WoWPoint point)
		{
			return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == 45455 && u.Location.Distance2DSqr(point) < 25 * 25);
		}

		#endregion
	}
}