
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Bots.DungeonBuddy.Profiles;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Classic
{
	public class SunkenTemple : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 28; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(-10292.47, -3990.68, -70.85); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(-318.16, 94.90, -91.31); }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			// don't kill MCed party members.
			units.RemoveAll(
				o =>
				{
					if (o is WoWPlayer)
						return true;

					if (o.Entry == AtalaiDeathwalkersSpiritId)
						return true;

					return false;
				});
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			foreach (var unit in incomingunits.Select(obj => obj.ToUnit()))
			{
				switch (unit.Entry)
				{
					case 6066: // Earthgrab Totem
						outgoingunits.Add(unit);
						break;
				}
			}
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			var isDps = Me.IsDps();
			foreach (var p in units)
			{
				WoWUnit unit = p.Object.ToUnit();
				if (unit != null)
				{
					if (unit.Entry == EarthgrabTotemId)
						p.Score += 5000;
					if (unit.Entry == JammalanTheProphetid && isDps)
						p.Score += 4000;
				}
			}
		}

		#endregion

		#region Root

		private const uint EarthgrabTotemId = 6066;
		private const uint AtalaiDeathwalkersSpiritId = 8317;

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(0, "Root")]
		public Composite RootBehavior()
		{
			AddAvoidObject(ctx => !Me.IsCasting, obj => Me.IsRange() && Me.IsMoving ? 8 : 5f, AtalaiDeathwalkersSpiritId);
			return new PrioritySelector();
		}


		[EncounterHandler(46077, "Lord Itharius", Mode = CallBehaviorMode.Proximity, BossRange = 30)]
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

		#endregion

		private const uint JammalanTheProphetid = 5710;

		[EncounterHandler(5710, "Jammal'an the Prophet", BossRange = 100, Mode = CallBehaviorMode.Proximity)]
		public Composite OgomTheWretchedEncounter()
		{
			const uint flamestrikeId = 12468;
			AddAvoidObject(ctx => !Me.IsCasting, 5, flamestrikeId);
			AddAvoidObject(ctx => !Me.IsTank(), obj => Me.IsRange() && Me.IsMoving ? 10 : 6, o => o is WoWPlayer && o.ToPlayer().HasAura("Hex of Jammal'an") && !o.IsMe);

			// clear room of argo.
			List<WoWUnit> targets = null;
			return
				new PrioritySelector(
					ctx => targets = ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.IsValid && u.IsAlive && u.Entry == 5271).OrderBy(u => u.DistanceSqr).ToList(),
					new Decorator(
						ctx =>
						targets != null && targets.Count > 0 && Targeting.Instance.FirstUnit == null && Navigator.CanNavigateFully(StyxWoW.Me.Location, targets[0].Location) &&
						BotPoi.Current.Type == PoiType.None,
						new Action(ctx => ScriptHelpers.SetLeaderMoveToPoiPS(targets[0].Location))));
		}

		private const uint AvatarOfHakkarId = 8443;

		[ObjectHandler(208321, "Shrine of the Soulflayer")]
		public async Task<bool> ShrineoftheSoulflayerHandler(WoWGameObject shrine)
		{
			if (!Me.IsTank() || !Targeting.Instance.IsEmpty() || !ScriptHelpers.IsBossAlive("Avatar of Hakkar"))
				return false;
			// sometimes this shrine does not despawn after boss is summoned so we need to ignore it if this happens.
			var hakker = ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => u.Entry == AvatarOfHakkarId);
			if (hakker != null && hakker.IsDead)
				return false;
			return await ScriptHelpers.InteractWithObject(shrine);
		}
		public Composite CreateBehavior_ShrineoftheSoulflayer()
		{
			return new PrioritySelector(
				new Decorator<WoWGameObject>(obj => Me.IsTank() && Targeting.Instance.IsEmpty(),
					ScriptHelpers.CreateInteractWithObject(ctx => (WoWGameObject)ctx))
				);
		}

		[EncounterHandler(5722, "Hazzas")]
		[EncounterHandler(5721, "Dreamscythe")]
		[EncounterHandler(5720, "Weaver")]
		[EncounterHandler(5719, "Morphaz")]
		[EncounterHandler(5709, "Shade of Eranikus")]
		public Composite HazzasEncounter()
		{
			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit,
				ScriptHelpers.CreateDispelEnemy("Deep Slumber", ScriptHelpers.EnemyDispelType.Magic, ctx => boss),
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(ctx => !Me.IsTank() && boss.Distance < 15 && !boss.IsMoving, ctx => boss, new ScriptHelpers.AngleSpan(0, 180)),
				ScriptHelpers.CreateTankFaceAwayGroupUnit(ctx => boss, 10));
		}
	}
}