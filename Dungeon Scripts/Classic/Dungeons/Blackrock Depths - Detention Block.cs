using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Frames;
using Styx.CommonBot.POI;
using Styx.Helpers;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Classic
{
	public class BlackrockDepthsDetentionBlock : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 30; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(-7178.79, -925.1274, 166.8448); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(457.0491, 38.14, -68.74); }
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var entry in units)
			{
				var unit = entry.Object;
				switch (unit.Entry)
				{
					case 8894: // Anvilrage Medic
						entry.Score += 250;
						break;
					case PrincessMoiraBronzeBeardId:
						entry.Score -= 10000;
						break;
				}
			}
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				u =>
				{
					WoWUnit unit = u.ToUnit();
					// Princess should not killed because of a quest requiring her to be alive in the end.
					if (unit.Entry == PrincessMoiraBronzebeardId)
						return true;
					if (unit.Entry == AnvilrageReservistId && !unit.Combat) // Anvilrage Reservist
						return true;
					if (unit.Entry == ShadowforgeFlameKeeperId && !HuntFlameKeepers && !unit.Combat)
						return true;
					return false;
				});
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			foreach (var unit in incomingunits.OfType<WoWUnit>())
			{
				// Add Shadowforge Flame Keeper if we need to farm tourches.
				if (unit.Entry == ShadowforgeFlameKeeperId && HuntFlameKeepers && LootTargeting.Instance.IsEmpty())
					outgoingunits.Add(unit);
			}
		}

		public override void IncludeLootTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{

			foreach (var lootTarget in incomingunits)
			{
				var unit = lootTarget as WoWUnit;
				if (unit != null)
				{
					if (unit.Entry == ShadowforgeFlameKeeperId && HuntFlameKeepers)
						outgoingunits.Add(unit);
				}
			}
		}

		public override void OnEnter()
		{
			Lua.Events.AttachEvent("CHAT_MSG_LOOT", ChatMsgLoot);
			 dynamicBlackspots = GetDynamicBlackspots().ToArray();
			DynamicBlackspotManager.AddBlackspots(dynamicBlackspots);
		}

		public override void OnExit()
		{
			Lua.Events.DetachEvent("CHAT_MSG_LOOT", ChatMsgLoot);
			_northBrazierLighted = _lootedChestOfTheSeven = _talkedToDoomrelt = _southBrazierLighted = false;
			DynamicBlackspotManager.RemoveBlackspots(dynamicBlackspots);
		}

		#region Mole Machine

		private const uint Entrance_AbandonedMoleMachineId = 207401;
		private const uint ShadowForgeCity_AbandonedMoleMachineId = 207402;
		private readonly WoWPoint _entranceMoleMachineLoc = new WoWPoint(448.2525, 21.00523, -70.67912);
		private readonly WoWPoint _grimGuzzlerMoleMachineLoc = new WoWPoint(929.9165, -292.2379, -49.94017);
		private readonly WoWPoint _shadowForgeCityrMoleMachineLoc = new WoWPoint(931.931, -286.9268, -49.93598);

		public override MoveResult MoveTo(WoWPoint location)
		{
			var myLoc = Me.Location;

			if (!Me.Combat && myLoc.DistanceSqr(_entranceMoleMachineLoc) < 60 * 60 && _grimGuzzlerMoleMachineLoc.Distance(location) < myLoc.Distance(location))
			{
				var moleMachine = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(o => o.Entry == Entrance_AbandonedMoleMachineId);
				if (moleMachine != null)
				{
					if (moleMachine.Distance > 5)
						return Navigator.MoveTo(moleMachine.Location);
					if (GossipFrame.Instance.IsVisible)
						GossipFrame.Instance.SelectGossipOption(0);
					else
						moleMachine.Interact();
					return MoveResult.Moved;
				}
			}
			if (!Me.Combat && myLoc.DistanceSqr(_shadowForgeCityrMoleMachineLoc) < 60 * 50 && _entranceMoleMachineLoc.Distance(location) < myLoc.Distance(location))
			{
				var moleMachine = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(o => o.Entry == ShadowForgeCity_AbandonedMoleMachineId);
				if (moleMachine != null)
				{
					if (moleMachine.Distance > 5)
						return Navigator.MoveTo(moleMachine.Location);
					if (GossipFrame.Instance.IsVisible)
						GossipFrame.Instance.SelectGossipOption(0);
					else
						moleMachine.Interact();
					return MoveResult.Moved;
				}
			}
			return base.MoveTo(location);
		}

		#endregion

		#endregion

		private const uint PrincessMoiraBronzebeardId = 8929;
		private const uint ShadowforgeFlameKeeperId = 9956;
		private const uint ShadowforgeTorchId = 11885;
		private const uint AnvilrageReservistId = 8901;
		private static readonly Regex ItemIdRegex = new Regex(@"Hitem:(?<id>\d+)", RegexOptions.CultureInvariant);
		private static readonly WaitTimer ShadowForgeTorchLooted = new WaitTimer(TimeSpan.FromMinutes(5));
		private readonly WoWPoint _baelGarLocation = new WoWPoint(701.8241, 185.5464, -72.06669);
		private readonly WoWPoint _lordIncendiusTankSpot = new WoWPoint(861.7827, -241.1472, -71.76051);
		private readonly WoWPoint _northBrazierLocation = new WoWPoint(1431.913, -523.971, -92.03755);
		private readonly WoWPoint _openBarDoorSafeMoveTo = new WoWPoint(881.53, -226.6577, -46.51212);
		private readonly WoWPoint _openBarDoorWaitAtPoint = new WoWPoint(869.8575, -227.0011, -43.75132);
		private readonly WoWPoint _ribblyScrewspigotDpsSpot = new WoWPoint(893.4753, -157.8955, -49.76056);
		private readonly WoWPoint _ribblyScrewspigotTankSpot = new WoWPoint(896.8156, -163.6791, -49.76056);

		private readonly WoWPoint _ringOfLawCenterLocation = new WoWPoint(595.9567, -188.4252, -54.14674);
		private readonly WoWPoint _southBrazierLocation = new WoWPoint(1332.868, -508.6617, -88.86429);
		private bool _lootedChestOfTheSeven;
		private bool _northBrazierLighted;
		private bool _southBrazierLighted;
		private bool _talkedToDoomrelt;

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		private WoWGameObject BarDoor
		{
			get { return ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(o => o.IsValid && o.Entry == 170571); }
		}

		private bool HasTorch
		{
			get { return Me.BagItems.Any(i => i.Entry == ShadowforgeTorchId); }
		}

		private PerFrameCachedValue<bool> _huntFlameKeepers;
		private bool HuntFlameKeepers
		{
			get
			{
				return _huntFlameKeepers ?? (_huntFlameKeepers = new PerFrameCachedValue<bool>(
					() => ShadowForgeTorchLooted.IsFinished
					&& !ScriptHelpers.IsBossAlive("Doom'rel")
					&& ScriptHelpers.IsBossAlive("Magmus") 
					&& (!_northBrazierLighted || !_southBrazierLighted)
					&& !HasTorch && Me.IsTank()));
			}
		}

		private static void ChatMsgLoot(object sender, LuaEventArgs args)
		{
			var match = ItemIdRegex.Match(args.Args[0].ToString());
			if (match.Success && match.Groups["id"].Success)
			{
				var itemId = int.Parse(match.Groups["id"].Value);
				if (itemId == ShadowforgeTorchId) // Shadowforge Torch
				{
					Logger.Write("A party member looted Shadowforge torch");
					ShadowForgeTorchLooted.Reset();
				}
			}
		}

		[EncounterHandler(45849, "Tinkee Steamboil", Mode = CallBehaviorMode.Proximity, BossRange = 45)]
		[EncounterHandler(45821, "Thal'trak Proudtusk", Mode = CallBehaviorMode.Proximity, BossRange = 45)]
		[EncounterHandler(45818, "Lexlort", Mode = CallBehaviorMode.Proximity, BossRange = 45)]
		public Composite QuestPickupTurninHandler()
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

		[EncounterHandler(9018, "High Interrogator Gerstahn")]
		public Composite HighInterrogatorGerstahnEncounter()
		{
			return ScriptHelpers.CreateTankUnitAtLocation(ctx => new WoWPoint(310.2939, -147.5773, -70.38612), 7);
		}

		// skipped in LFG
		//[ObjectHandler(161524, "Ring of the Law", 1000)]
		//public Composite RingOfTheLawEncounter()
		//{
		//    WoWGameObject eastGarrisonDoor = null;
		//    WoWGameObject wave1Door = null;
		//    WoWGameObject wave2Door = null;
		//    bool wave1Inc = false;
		//    bool wave2Inc = false;
		//    // WoWDoor.IsClosed doesn't seem to work for these doors so using WowGameObject.State. Active state means it's open, Ready state means it's closed.
		//    return new PrioritySelector(
		//        ctx =>
		//        {
		//            wave1Door = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(o => o.Entry == 161525);
		//            wave2Door = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(o => o.Entry == 161522);
		//            eastGarrisonDoor = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(o => o.Entry == 161524);
		//            wave1Inc = wave1Door != null && wave1Door.State == WoWGameObjectState.Active && eastGarrisonDoor != null &&
		//                       eastGarrisonDoor.State == WoWGameObjectState.Ready;
		//            wave2Inc = wave2Door != null && wave2Door.State == WoWGameObjectState.Active && eastGarrisonDoor != null &&
		//                       eastGarrisonDoor.State == WoWGameObjectState.Ready;
		//            return ctx;
		//        },
		//        new Decorator(
		//            ctx =>
		//            eastGarrisonDoor != null && eastGarrisonDoor.State == WoWGameObjectState.Ready &&
		//            (!ScriptHelpers.IsBossAlive("Houndmaster Grebmar") || !ScriptHelpers.ShouldKillBoss("Houndmaster Grebmar")) && StyxWoW.Me.IsTank(),
		//            new PrioritySelector(
		//                new Decorator(
		//                    ctx => !wave1Inc && !wave2Inc,
		//                    new PrioritySelector(
		//                        new ActionSetActivity("Moving to Ring of Law"),
		//                        new Decorator(
		//                            ctx => StyxWoW.Me.Location.DistanceSqr(_ringOfLawCenterLocation) > 5 * 5 && BotPoi.Current.Type == PoiType.None,
		//                            new Action(ctx => ScriptHelpers.SetLeaderMoveToPoi(_ringOfLawCenterLocation))),
		//                        new Decorator(
		//                            ctx => StyxWoW.Me.Location.DistanceSqr(_ringOfLawCenterLocation) <= 5 * 5 && Targeting.Instance.FirstUnit == null, new ActionAlwaysSucceed()))),
		//        // dont move anywhere
		//                new Decorator(
		//                    ctx => wave2Inc && Targeting.Instance.FirstUnit == null,
		//                    new PrioritySelector(
		//                        new ActionSetActivity("Picking up boss."),
		//                        new Decorator(
		//                            ctx => StyxWoW.Me.Location.DistanceSqr(wave2Door.Location) > 5 * 5 && BotPoi.Current.Type == PoiType.None, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoi(wave2Door.Location))),
		//                        new Decorator(
		//                            ctx => StyxWoW.Me.Location.DistanceSqr(wave2Door.Location) <= 5 * 5 && Targeting.Instance.FirstUnit == null, new ActionAlwaysSucceed()))),
		//        // dont move anywhere
		//                new Decorator(
		//                    ctx => wave1Inc && !wave2Inc && Targeting.Instance.FirstUnit == null,
		//                    new PrioritySelector(
		//                        new ActionSetActivity("Picking up the waves of NPCs."),
		//                        new Decorator(
		//                            ctx => StyxWoW.Me.Location.DistanceSqr(wave1Door.Location) > 5 * 5 && BotPoi.Current.Type == PoiType.None, new Action(ctx => ScriptHelpers.SetLeaderMoveToPoi(wave1Door.Location))),
		//                        new Decorator(
		//                            ctx => StyxWoW.Me.Location.DistanceSqr(wave1Door.Location) <= 5 * 5 && Targeting.Instance.FirstUnit == null, new ActionAlwaysSucceed())))
		//        // dont move anywhere
		//                )));
		//}


		[EncounterHandler(9016, "Bael'Gar", BossRange = 100, Mode = CallBehaviorMode.Proximity)]
		public Composite BaelGarEncounter()
		{
			// clear room of argo.
			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit, ScriptHelpers.CreateClearArea(() => _baelGarLocation, 100, u => u.Entry != boss.Entry));
		}

		[EncounterHandler(9017, "Lord Incendius")]
		public Composite LordIncendiusEncounter()
		{
			return new PrioritySelector(
				new Decorator(ctx => StyxWoW.Me.IsTank(), ScriptHelpers.CreateTankUnitAtLocation(ctx => _lordIncendiusTankSpot, 7)),
				new Decorator(
					ctx => StyxWoW.Me.IsDps(),
				// wait until boss is over in tanking spot before opening up on him.
					new PrioritySelector(new Decorator(ctx => ((WoWUnit)ctx).Location.DistanceSqr(_lordIncendiusTankSpot) > 12 * 12, new ActionAlwaysSucceed()))));
		}


		[ObjectHandler(161460, "The Shadowforge Lock", ObjectRange = 60)]
		public Composite TheShadowforgeLockHandler()
		{
			WoWGameObject shadowForgeLock = null;
			return new PrioritySelector(
				ctx => shadowForgeLock = ctx as WoWGameObject,
				new Decorator(
					ctx =>
						{
							if (shadowForgeLock.State != WoWGameObjectState.Ready || !Me.IsTank() || !Targeting.Instance.IsEmpty())
								return false;

							var pathDist = shadowForgeLock.Location.PathDistance(Me.Location, 60f);
							return pathDist.HasValue && pathDist.Value < 60f;
						},
					ScriptHelpers.CreateInteractWithObject(ctx => (WoWGameObject)ctx)));
		}

		[ObjectHandler(164911, "Thunderbrew Lager Keg", 75)]
		public Composite HurleyBlackbreathEncounter()
		{
			WoWGameObject keg = null;
			return new PrioritySelector(
				ctx => keg = ctx as WoWGameObject,
				new Decorator(
					ctx => keg != null && ScriptHelpers.ShouldKillBoss("Hurley Blackbreath") && ScriptHelpers.IsBossAlive("Hurley Blackbreath"),
					ScriptHelpers.CreateInteractWithObject(164911)));
		}

		// Should never do this encounter on LFG because its a waste of time.
		/*
		[EncounterHandler(9543, "Ribbly Screwspigot", Mode = CallBehaviorMode.Proximity, BossRange = 40)]
		public Composite RibblyScrewspigotEncounter()
		{
			return new PrioritySelector(
				new Decorator(
					ctx => !ScriptHelpers.IsBossAlive("Hurley Blackbreath") && // loot up before we start event
						   !ObjectManager.GetObjectsOfType<WoWUnit>().Any(
							   u => u.IsValid && u.IsDead && u.Lootable && u.DistanceSqr < CharacterSettings.Instance.LootRadius * CharacterSettings.Instance.LootRadius),
					new PrioritySelector(
				// I am tank
						new Decorator(
							ctx => StyxWoW.Me.IsTank(),
							new PrioritySelector(
								new Decorator(
									ctx => !StyxWoW.Me.IsActuallyInCombat,
									new Sequence(
										ScriptHelpers.CreateTalkToNpcContinue(9543),
										ScriptHelpers.CreateMoveToContinue(ctx => _ribblyScrewspigotTankSpot),
										new WaitContinue(15, ctx => StyxWoW.Me.IsActuallyInCombat, new ActionAlwaysSucceed()))),
								ScriptHelpers.CreateTankUnitAtLocation(ctx => _ribblyScrewspigotTankSpot, 4))),
				// I am dps/healer.
						new Decorator(
							ctx => StyxWoW.Me.IsFollower(),
							new PrioritySelector(
								new Decorator(
									ctx => StyxWoW.Me.Location.DistanceSqr(_ribblyScrewspigotDpsSpot) > 4 * 4,
									new Action(ctx => Navigator.GetRunStatusFromMoveResult(Navigator.MoveTo(_ribblyScrewspigotDpsSpot)))),
								new Decorator(
									ctx => ScriptHelpers.MovementEnabled && StyxWoW.Me.Location.DistanceSqr(_ribblyScrewspigotDpsSpot) <= 4 * 4,
									new Action(ctx => ScriptHelpers.DisableMovement(() => ScriptHelpers.IsBossAlive("Ribbly Screwspigot") && StyxWoW.Me.IsAlive))))))));
		}
		*/
		/*
		[EncounterHandler(9499, "Plugger Spazzring", Mode = CallBehaviorMode.Proximity, BossRange = 100)]
		public Composite OpenBarDoorEncounter()
		{
			const uint bartenderId = 9499;
			const uint rockNotId = 9503;
			var rockNotIdleLoc = new WoWPoint(891.1985, -197.924, -43.7037);
			const uint beverageId = 11325;
			WoWGameObject barDoor = null;
			WoWUnit rockNot = null;
			WoWUnit pluggerSpazzring = null;
			int mugsInBag = 0;
			return new PrioritySelector(
				ctx =>
				{
					barDoor = BarDoor;
					rockNot = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == rockNotId);
					pluggerSpazzring = ctx as WoWUnit;
					return ctx;
				},
				new Decorator(
					ctx => barDoor != null && barDoor.State == WoWGameObjectState.Ready && !ScriptHelpers.IsBossAlive("Ribbly Screwspigot"),
					new PrioritySelector(
						ctx => mugsInBag = (int)StyxWoW.Me.CarriedItems.Sum(i => i != null && i.IsValid && i.Entry == beverageId ? i.StackCount : 0),
				// buy 
						new Decorator(
							ctx => Me.IsTank() && rockNot.Location.Distance(rockNotIdleLoc) < 3,
							new PrioritySelector(
								new Decorator(
									ctx => mugsInBag < 6,
									new PrioritySelector(
										new Decorator(
											ctx => pluggerSpazzring.WithinInteractRange,
											new Sequence(
												new Action(ctx => pluggerSpazzring.Interact()),
												new WaitContinue(2, ctx => MerchantFrame.Instance.IsVisible, new ActionAlwaysSucceed()),
												new Action(ctx => ScriptHelpers.BuyItem(beverageId, 6 - mugsInBag)),
												new WaitContinue(3, ctx => false, new ActionAlwaysSucceed()))),
										new Decorator(ctx => !pluggerSpazzring.WithinInteractRange, new Action(ctx => Navigator.MoveTo(pluggerSpazzring.Location))))),
								new Decorator(
									ctx => StyxWoW.Me.IsTank() && mugsInBag >= 6,
									new Sequence(
										ScriptHelpers.CreateTurninQuest(rockNotId),
										new Action(ctx => QuestFrame.Instance.Close()),
										new WaitContinue(1, ctx => false, new ActionAlwaysSucceed()),
										ScriptHelpers.CreateTurninQuest(rockNotId),
										new Action(ctx => QuestFrame.Instance.Close()),
										new WaitContinue(1, ctx => false, new ActionAlwaysSucceed()),
										ScriptHelpers.CreateTurninQuest(rockNotId),
										new WaitContinue(1, ctx => false, new ActionAlwaysSucceed()))))),
						new Decorator(
							ctx => rockNot.Location.Distance(rockNotIdleLoc) >= 3 && !Me.Combat,
							new PrioritySelector(
								new Decorator(ctx => Me.Location.Distance(_openBarDoorWaitAtPoint) > 4, new Action(ctx => Navigator.MoveTo(_openBarDoorWaitAtPoint))),
								new Decorator(
									ctx => Me.Location.Distance(_openBarDoorWaitAtPoint) <= 4,
									new Sequence(
										new WaitContinue(
											TimeSpan.FromMinutes(2),
											ctx => BarDoor.State == WoWGameObjectState.ActiveAlternative || StyxWoW.Me.Combat,
											new ActionAlwaysSucceed()),
										new DecoratorContinue(
											ctx => BarDoor.State == WoWGameObjectState.ActiveAlternative, ScriptHelpers.CreateMoveToContinue(ctx => _openBarDoorSafeMoveTo)))))))));
		}
		*/

		[ObjectHandler(169243, "Chest of The Seven")]
		public Composite ChestOfTheSevenHandler()
		{
			return new PrioritySelector(ScriptHelpers.CreateLootChest(ctx => (WoWGameObject)ctx));
		}

		[EncounterHandler(9938, "Magmus")]
		public Composite MagmusEncounter()
		{
			WoWUnit boss = null;
			// tank n spank
			return new PrioritySelector(ctx => boss = ctx as WoWUnit);
		}

		private const uint PrincessMoiraBronzeBeardId = 8929;

		[EncounterHandler(8929, "Princess Moira BronzeBeard")]
		public Composite PrincesMoiraBronzeBeardEncounter()
		{
			WoWUnit boss = null;
			const int healId = 15586;
			const int renewId = 8362;

			return new PrioritySelector(ctx => boss = ctx as WoWUnit, ScriptHelpers.CreateInterruptCast(ctx => boss, healId, renewId));
		}

		readonly WoWPoint _emperorTankLoc = new WoWPoint(1380.191, -807.5756, -92.72278);

		[EncounterHandler(9019, "Emperor Dagran Thaurissan")]
		public async Task<bool> EmperorDagranThaurissanEncounter(WoWUnit boss)
		{
			return await ScriptHelpers.DispelEnemy("Renew", ScriptHelpers.EnemyDispelType.Magic, boss)
				|| await ScriptHelpers.TankUnitAtLocation(_emperorTankLoc, 5);
		}

		#region Shortcut
		readonly WaitTimer _trashBeforeTheSevenTimer = WaitTimer.FiveSeconds;

		bool _shouldAvoidTrashBeforeTheSeven;
		private DynamicBlackspot[] dynamicBlackspots;

		private readonly WoWPoint _trashShortcutLoc = new WoWPoint(1158.5, -137.6574, -74.3636);

		private IEnumerable<DynamicBlackspot> GetDynamicBlackspots()
		{
			yield return new DynamicBlackspot(
				() =>
				{
					if (!_trashBeforeTheSevenTimer.IsFinished) return _shouldAvoidTrashBeforeTheSeven;
					_trashBeforeTheSevenTimer.Reset();
					_shouldAvoidTrashBeforeTheSeven = !Me.Combat && Me.Location.Distance(_trashShortcutLoc) < 100 * 100 &&
													ScriptHelpers.GetUnfriendlyNpsAtLocation(_trashShortcutLoc, 20).Any();
					return _shouldAvoidTrashBeforeTheSeven;
				},
				() => _trashShortcutLoc,
				LfgDungeon.MapId,
				30.86862f,
				name: "Trash before 'The Seven' encounter");
		}

		#endregion


		#region Golem Room South

		[ObjectHandler(170574, "Golem Room South", ObjectRange = 250)] // door that opens if the braziers are lit
		public Func<WoWGameObject, Task<bool>> LightTheBraziersHandler()
		{
			var flameKeeperPath = new CircularQueue<WoWPoint>
								{
									new WoWPoint(1381.926, -332.2724, -92.0544),
									new WoWPoint(1389.05, -458.6602, -93.66544)
								};
			// ensure we're on correct floor level before running this behavior.
			return async door =>
			{
				// if door is open or we're not on the correct floorlevel then encounter is not in progress.
				if (((WoWDoor) door.SubObj).IsOpen || Me.Z <= -100 || Me.Z >= -85)
					return false;

				if (ScriptHelpers.IsBossAlive("Doom'rel") || !ScriptHelpers.IsBossAlive("Magmus"))
					return false;

				// we only want to deal with the braziers if there's nothing to kill.
				if (!Targeting.Instance.IsEmpty())
					return false;

				var southBrazier = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(o => o.Entry == 174744);
				var northBrazier = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(o => o.Entry == 174745);

				if (southBrazier != null && southBrazier.State == WoWGameObjectState.Active && !_southBrazierLighted)
				{
					ShadowForgeTorchLooted.Stop();
					_southBrazierLighted = true;
				}

				if (northBrazier != null && northBrazier.State == WoWGameObjectState.Active && !_northBrazierLighted)
				{
					ShadowForgeTorchLooted.Stop();
					_northBrazierLighted = true;
				}

				var brazer = !_northBrazierLighted ? northBrazier : southBrazier;
				var brazerLoc = !_northBrazierLighted ? _northBrazierLocation : _southBrazierLocation;

				// Go light a brazier
				if ((!_northBrazierLighted || !_southBrazierLighted) && (!ShadowForgeTorchLooted.IsFinished || HasTorch))
				{
					if (HasTorch && (Me.IsLeader() || Me.Location.DistanceSqr(brazerLoc) < 30*30) && brazer != null)
					{
						ScriptHelpers.SetInteractPoi(brazer);
						return false;
					}
					if (Me.Location.DistanceSqr(brazerLoc) > 5*5)
					{
						ScriptHelpers.SetLeaderMoveToPoi(brazerLoc, brazer != null ? brazer.SafeName : "brazier");
						return false;
					}
					// do nothing if leader and waiting at brazier for someone to light it.
					return Me.IsLeader();
				}
								
				if (Me.IsLeader() && BotPoi.Current.Type == PoiType.None)
				{
					if (Me.Location.DistanceSqr(flameKeeperPath.Peek()) < 5*5)
						flameKeeperPath.Dequeue();
					ScriptHelpers.SetLeaderMoveToPoi(flameKeeperPath.Peek());
				}

				return false;
			};
		}

		#endregion

		#region The Seven

		[EncounterHandler(9039, "Doom'relt", Mode = CallBehaviorMode.Proximity, BossRange = 100)]
		public Func<WoWUnit, Task<bool>> TheSevenEncounter()
		{
			var waitAtLoc = new WoWPoint(1263.587, -257.3126, -78.21929);

			return async boss =>
			{
				if (Me.IsLeader() && !Me.Combat)
				{
					if (boss.CanGossip)
						return await ScriptHelpers.TalkToNpc(boss);

					// don't do anything while waiting for combat... such as running off to look for next boss.
					if (Targeting.Instance.IsEmpty())
					{
						if (Me.Location.Distance(waitAtLoc) > 4*4)
							return (await CommonCoroutines.MoveTo(waitAtLoc)).IsSuccessful();
						return true;
					}
				}
				return false;
			};
		}

		#endregion
	}
}