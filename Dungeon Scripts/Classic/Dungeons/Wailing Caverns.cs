using System;
using System.Collections.Generic;
using System.Linq;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.POI;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
using Bots.DungeonBuddy.Profiles;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Classic
{
	public class WailingCaverns : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 1; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(-743.4651f, -2215.294f, 15.46369f); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(-168.255, 136.5673, -72.73431); }
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			foreach (WoWUnit unit in incomingunits.Select(obj => obj.ToUnit()))
			{
				// need to add Kresh manually since he's a neutral.
				if (unit.Entry == KreshId && !Me.Combat && Me.IsTank())
				{
					var pathDist = unit.Location.PathDistance(Me.Location, 40f);
					if (pathDist.HasValue && pathDist < 40f)
						outgoingunits.Add(unit);
				}
			}
		}


		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (Targeting.TargetPriority t in units)
			{
				WoWObject prioObject = t.Object;
			}
		}

		public override void OnEnter()
		{
			_muyohLoc.CycleTo(_muyohLoc.First);
		}

		#endregion

		#region Root

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}


		[EncounterHandler(0)]
		public Composite RootBehavior()
		{
			return new PrioritySelector();
		}

		[EncounterHandler(5767, "Nalpak", Mode = CallBehaviorMode.Proximity, BossRange = 40)]
		[EncounterHandler(5768, "Ebru", Mode = CallBehaviorMode.Proximity, BossRange = 40)]
		public Composite QuestGiversBehavior()
		{
			WoWUnit unit = null;
			return new PrioritySelector(
				ctx => unit = ctx as WoWUnit,
				new Decorator(ctx => !Me.Combat && unit.QuestGiverStatus == QuestGiverStatus.Available, ScriptHelpers.CreatePickupQuest(ctx => unit)),
				new Decorator(ctx => !Me.Combat && unit.QuestGiverStatus == QuestGiverStatus.TurnIn, ScriptHelpers.CreateTurninQuest(ctx => unit)));
		}

		[ObjectHandler(13891, "Serpentbloom", ObjectRange = 25)]
		public Composite SerpentbloomHandler()
		{
			WoWGameObject obj = null;

			const int preemptiveMethodsId = 26873;
			return new PrioritySelector(
				ctx => obj = ctx as WoWGameObject,
				new Decorator(
					ctx =>
					{
						if (!ScriptHelpers.HasQuest(preemptiveMethodsId) || ScriptHelpers.IsQuestInLogComplete(preemptiveMethodsId))
							return false;
						if (Me.Combat || ScriptHelpers.WillPullAggroAtLocation(obj.Location) || obj.InUse)
							return false;
						var pathDist = Me.Location.PathDistance(obj.Location, 25f);
						return pathDist.HasValue && pathDist.Value < 25f;
					},
					new PrioritySelector(
						new Decorator(
							ctx => obj.WithinInteractRange,
							new Sequence(
								new Action(ctx => obj.Interact()),
								new WaitContinue(2, ctx => LootFrame.Instance.IsVisible, new ActionAlwaysSucceed()),
								new Action(ctx => LootFrame.Instance.LootAll()))),
						new Decorator(ctx => !obj.WithinInteractRange, new Action(ctx => Navigator.MoveTo(obj.Location))))));
		}

		#endregion

		private const uint KreshId = 3653;

		[EncounterHandler(3669, "Lord Cobrahn")]
		[EncounterHandler(3670, "Lord Pythas")]
		[EncounterHandler(3671, "Lady Anacondra")]
		[EncounterHandler(3673, "Lord Serpentis")]
		public Composite DruidsEncounter()
		{
			const int healingTouchId = 23381;
			const int druidsSlumberId = 8040;
			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateInterruptCast(ctx => boss, healingTouchId, druidsSlumberId),
				ScriptHelpers.CreateDispelGroup("Druid's Slumber", ScriptHelpers.PartyDispelType.Magic),
				// Lord Cobrahn only.
				ScriptHelpers.CreateDispelGroup("Poison", ScriptHelpers.PartyDispelType.Poison));
		}

		[EncounterHandler(3653, "Kresh")]
		public Composite KreshEncounter()
		{
			return new PrioritySelector();
		}


		[EncounterHandler(3674, "Skum")]
		public Composite SkumEncounter()
		{
			return new PrioritySelector();
		}

		#region Mutanus the Devourer

		private readonly CircularQueue<WoWPoint> _muyohLoc = new CircularQueue<WoWPoint>()
		{
			new WoWPoint(-134.965, 125.402, -78.17783),
			new WoWPoint(114.9415, 237.7185, -96.02783)
		};

		[EncounterHandler(3654, "Mutanus the Devourer", Mode = CallBehaviorMode.CurrentBoss)]
		public Composite MutanustheDevourerSpawnEncounter()
		{
			WoWUnit boss = null;
			WoWUnit muyoh = null;
			const int muyohId = 3678;
			var muyohStartLoc = new WoWPoint(-134.965, 125.402, -78.17783);
			var muyohEndLoc = new WoWPoint(114.9415, 237.7185, -96.02783);

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				new Decorator(
					ctx => boss == null && Me.IsTank(),
					new PrioritySelector(
						ctx => muyoh = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == muyohId),
						new Decorator(ctx => muyoh == null,
							new PrioritySelector(
								new Decorator(ctx => Me.Location.Distance2DSqr(_muyohLoc.Peek()) <= 5, new Action(ctx => _muyohLoc.Dequeue())),
								new Action(ctx => Navigator.MoveTo(_muyohLoc.Peek())))),

						new Decorator(
							ctx => muyoh != null,
							new PrioritySelector(
								new Decorator(
									ctx => muyoh.Location.Distance(muyohEndLoc) > 5, ScriptHelpers.CreateTankTalkToThenEscortNpc(muyohId, muyohStartLoc, muyohEndLoc)),
								new Decorator(ctx => muyoh.Location.Distance(muyohEndLoc) <= 5,
									new PrioritySelector(
										new Decorator(ctx => !ScriptHelpers.GetUnfriendlyNpsAtLocation(muyohEndLoc, 80).Any() ||
											(Me.Location.DistanceSqr(muyohEndLoc) > 10 *10 && Targeting.Instance.IsEmpty()),
											new Action(ctx => Navigator.MoveTo(muyohEndLoc))),
										ScriptHelpers.CreateClearArea(() => muyohEndLoc, 80))))),
						new Decorator(ctx => Me.IsTank() && Targeting.Instance.IsEmpty() && BotPoi.Current.Type == PoiType.None, new ActionAlwaysSucceed()))));
		}

		[EncounterHandler(3654, "Mutanus the Devourer")]
		public Composite MutanustheDevourerEncounter()
		{
			const int naralexsNightmare = 7967;
			WoWUnit boss = null;
			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateInterruptCast(ctx => boss, naralexsNightmare),
				ScriptHelpers.CreateDispelGroup("Terrify", ScriptHelpers.PartyDispelType.Magic),
				ScriptHelpers.CreateDispelGroup("Naralex's Nightmare", ScriptHelpers.PartyDispelType.Magic));
		}

		#endregion
	}
}