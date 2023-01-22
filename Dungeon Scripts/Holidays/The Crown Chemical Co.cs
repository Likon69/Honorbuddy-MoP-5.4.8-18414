using System;
using System.Collections.Generic;
using System.Linq;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
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
namespace Bots.DungeonBuddy.Dungeon_Scripts.Mists_of_Pandaria
{
	public class TheCrownChemicalCo : Dungeon
	{
		#region Overrides of Dungeon

		private readonly WaitTimer _ignoreHoblitesTimer = new WaitTimer(TimeSpan.FromSeconds(20));
		private readonly WaitTimer _killHoblitesTimer = new WaitTimer(TimeSpan.FromSeconds(10));

		public override uint DungeonId
		{
			get { return 288; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(-233.2333, 1571, 76.88484); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(-229.73, 2107.91, 76.88484); }
		}

		public override bool IsComplete
		{
			get { return !Me.Combat && base.IsComplete; }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (unit != null) { }
					return false;
				});
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			var hasAggro = ObjectManager.GetObjectsOfType<WoWUnit>().Any(u => u.Combat && u.IsTargetingMyRaidMember);
			foreach (var obj in incomingunits)
			{
				var unit = obj as WoWUnit;
				if (unit != null) { }
			}
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var priority in units)
			{
				var unit = priority.Object as WoWUnit;
				if (unit != null)
				{
					if (unit.Entry == ApothecaryHummelId)
						priority.Score += 200;
					else if (unit.Entry == ApothecaryBaxterId)
						priority.Score += 100;
				}
			}
		}

		#endregion

		private const uint CologneNeutralizerId = 202947;
		private const uint PerfumeNeutralizerId = 202948;
		private const uint PerfumeNeutralizerItemId = 49351;
		private const uint CologneNeutralizerItemId = 49352;
		private const uint ApothecaryFryeId = 36272;
		private const uint ApothecaryBaxterId = 36565;
		private const uint ApothecaryHummelId = 36296;

		private static LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0)]
		public Composite RootBehavior()
		{
			return new PrioritySelector(
				// ignore all chests as they cause pathing issues when bot attempts to move to them to loot.
				new Action(
					ctx =>
					{
						foreach (var chest in ObjectManager.GetObjectsOfType<WoWGameObject>().Where(g => g.IsChest && !Blacklist.Contains(g, BlacklistFlags.Loot)))
						{
							Blacklist.Add(chest, BlacklistFlags.Loot, TimeSpan.FromMinutes(15));
						}
						return RunStatus.Failure;
					}));
		}

		[LocationHandler(-241.613, 2156.265, 90.62401, 10, "Wait for door to open")]
		public Composite BaronAshburyArea()
		{
			const uint courtyardDoorId = 18895;

			WoWGameObject door = null;
			return new PrioritySelector(
				ctx => door = ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(o => o.Entry == courtyardDoorId),
				// wait for door to open..
				new Decorator(
					ctx => !ScriptHelpers.IsBossAlive("Baron Ashbury") && door != null && door.State == WoWGameObjectState.Ready && Me.IsTank() && !Me.Combat,
					new ActionAlwaysSucceed()));
		}


		[EncounterHandler(36296, "Apothecary Hummel", Mode = CallBehaviorMode.Proximity, BossRange = 40)]
		public Composite ApothecaryHummelEncounter()
		{
			const uint concentratedIrresistibleCologneSpillId = 68614;
			const uint concentratedAlluringPerfumeSpillId = 68798;

			AddAvoidObject(ctx => true, 3, concentratedIrresistibleCologneSpillId, concentratedAlluringPerfumeSpillId);

			WoWGameObject neutralizer = null;
			WoWUnit boss = null;
			WoWItem item = null;
			var pullTimer = new WaitTimer(TimeSpan.FromSeconds(30));

			return new PrioritySelector(
				ctx => boss = ctx as WoWUnit,
				new Decorator(
					ctx => !boss.Combat,
					new PrioritySelector(
						new Decorator(
							ctx => !Me.BagItems.Any(i => i != null && i.Entry == CologneNeutralizerItemId),
							new PrioritySelector(
								ctx =>
								neutralizer =
								ObjectManager.GetObjectsOfType<WoWGameObject>()
											 .Where(g => g.Entry == CologneNeutralizerId && !g.InUse && g.CanUse())
											 .OrderBy(g => g.DistanceSqr)
											 .FirstOrDefault(),
								new Decorator(ctx => LootFrame.Instance.IsVisible, new Action(ctx => LootFrame.Instance.LootAll())),
								new Decorator(ctx => neutralizer != null && !neutralizer.CanUseNow(), new Action(ctx => Navigator.MoveTo(neutralizer.Location))),
								new Decorator(ctx => neutralizer != null && neutralizer.CanUseNow() && !Me.IsCasting, new Action(ctx => neutralizer.Interact())))),
						new Decorator(
							ctx =>
							(item = Me.BagItems.FirstOrDefault(i => i != null && i.Entry == CologneNeutralizerItemId)) != null && !Me.HasAura("Perfume Immune") &&
							!Me.HasAura("Cologne Immune"),
							new Action(ctx => item.UseContainerItem())),
						new Decorator(
							ctx => (Me.HasAura("Perfume Immune") || Me.HasAura("Cologne Immune")) && Me.IsTank(),
							new PrioritySelector(
								new Decorator(
									ctx => pullTimer == null,
									new Sequence(new Action(ctx => pullTimer = new WaitTimer(TimeSpan.FromMinutes(1))), new Action(ctx => pullTimer.Reset()))),
				// start event once all party members have the item buffs or count-down timer is finish
								new Decorator(
									ctx =>
									ScriptHelpers.GroupMembers.All(
										p => p.Player != null && (p.Player.HasAura("Perfume Immune") || p.Player.HasAura("Cologne Immune")) && p.Player.Distance <= 35) ||
									pullTimer.IsFinished,
									ScriptHelpers.CreateTalkToNpc(ctx => boss)))))),
				new Decorator(ctx => boss.Combat, new PrioritySelector(new Decorator(ctx => pullTimer != null, new Action(ctx => pullTimer = null)))));
		}
	}
}