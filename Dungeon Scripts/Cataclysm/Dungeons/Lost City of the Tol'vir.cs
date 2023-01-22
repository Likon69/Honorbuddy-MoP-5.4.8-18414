using System;
using System.Collections.Generic;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using System.Linq;
using Styx.TreeSharp;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Cataclysm
{
	public class LostCityOfTheTolvir : Dungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 312; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(-10686.43, -1308.724, 18.13906); }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(-10687.38, -1309.084, 17.63024); }
		}

		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (_enslavedBanditIds.Contains(ret.Entry) && !unit.IsTargetingMyPartyMember && !unit.IsTargetingMeOrPet)
						return true;
					if (_aughIds.Contains(ret.Entry) && StyxWoW.Me.IsTank())
						return true;

					if (ret.Entry == SoulFragmentId && StyxWoW.Me.IsTank())
						return true;

					if (ret.Entry == HighProphetBarimId && unit.HasAura(Repentance))
						return true;

					if (ret.Entry == SiamatId && unit.HasAura("Deflecting Winds"))
						return true;

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
					if (unit.Entry == SoulFragmentId && !StyxWoW.Me.IsTank() && !_ignoreSoulFragments)
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
					if (unit.Entry == SoulFragmentId && !StyxWoW.Me.IsTank() && !_ignoreSoulFragments)
						priority.Score += 500;

					if (unit.Entry == MinionOfSiamatId)
						priority.Score -= 500;
				}
			}
		}

		#endregion

		private const uint WindTunnelId = 48092;
		private const uint EarthQuakeId = 45126;
		private const uint FrenziedCrocoliskId = 43658;
		private const uint SoulFragmentId = 43934;
		private const uint HarbingerOfDarknessId = 43927;
		private const uint HighProphetBarimId = 43612;
		private const uint SiamatId = 44819;
		private const uint MinionOfSiamatId = 44704;
		private const uint ServantOfSiamatId = 45259;


		// there are different 'Repentance' auras but Barim gains this version in phase 2
		private const int Repentance = 82320;
		private readonly uint[] _aughIds = new uint[] { 45378, 45379 };
		private readonly uint[] _enslavedBanditIds = new uint[] { 45001, 45007 };


		private readonly int[] _shockwaveIds = new[] { 83445, 91257 };
		private readonly uint[] _viscousPoisonIds = new uint[] { 81630, 90004 };
		private readonly WaitTimer _windTunnelTimer = new WaitTimer(TimeSpan.FromSeconds(15));

		private WoWUnit _husam;
		private bool _ignoreSoulFragments;

		private LocalPlayer Me
		{
			get { return StyxWoW.Me; }
		}

		[EncounterHandler(50038, "Captain Hadan", Mode = CallBehaviorMode.Proximity, BossRange = 35)]
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

		[EncounterHandler(0, "Root")]
		public Composite RootBehavior()
		{
			AddAvoidObject(ctx => true, 8, EarthQuakeId);

			return new PrioritySelector();
		}

		[EncounterHandler(44577, "General Husam")]
		public Composite GeneralHusamEncounter()
		{
			const uint landMine = 44796;
			const uint shockwaveVisual = 44712;

			AddAvoidObject(ctx => true, o => o.ToUnit().HasAura("Tol'vir Land Mine Visual") ? 7 : 2, u => u.Entry == landMine && u.DistanceSqr <= 14 * 14);
			AddAvoidObject(ctx => true, 3, u => u.Entry == shockwaveVisual);

			return new PrioritySelector(ctx => _husam = ctx as WoWUnit);
		}

		[EncounterHandler(43614, "Lockmaw")]
		public Composite LockmawEncounter()
		{
			WoWUnit lockmaw = null;
			AddAvoidObject(ctx => true, 5, _viscousPoisonIds);
			AddAvoidObject(ctx => true, o => Me.IsRange() && Me.IsMoving ? 14 : 9, u => _aughIds.Contains(u.Entry) && u.ToUnit().HasAura("Whirlwind"));

			return new PrioritySelector(
				ctx => lockmaw = ctx as WoWUnit,
				ScriptHelpers.CreateDispelEnemy("Venomous Rage", ScriptHelpers.EnemyDispelType.Enrage, ctx => lockmaw),
				ScriptHelpers.CreateAvoidUnitAnglesBehavior(
					ctx => !Me.IsTank() && lockmaw.Distance <= lockmaw.MeleeRange() && !lockmaw.IsMoving, ctx => lockmaw, new ScriptHelpers.AngleSpan(180, 100)));
		}

		[EncounterHandler(43612, "High Prophet Barim")]
		public Composite HighProphetBarimEncounter()
		{
			const uint heavensFury = 43801;

			var roomCenterLoc = new WoWPoint(-11007.25, -1283.995, 10.84916);
			WoWUnit boss = null;

			AddAvoidObject(ctx => true, 4, heavensFury);
			AddAvoidObject(ctx => StyxWoW.Me.IsTank(), () => roomCenterLoc, 30, 15, SoulFragmentId);
			AddAvoidObject(
				ctx => !Me.IsTank() && Me.HasAura("Soul Sever") && StyxWoW.Me.Auras["Soul Sever"].TimeLeft <= TimeSpan.FromSeconds(4),
				() => ScriptHelpers.Tank.Location,
				40,
				30,
				HarbingerOfDarknessId);
			AddAvoidObject(ctx => true, () => roomCenterLoc, 30, 15, u => u.Entry == HighProphetBarimId && u.ToUnit().HasAura(Repentance));

			return new PrioritySelector(
				ctx =>
				{
					_ignoreSoulFragments = ObjectManager.GetObjectsOfType<WoWUnit>().Any(u => u.Entry == HarbingerOfDarknessId && u.HealthPercent <= 30);
					return boss = ctx as WoWUnit;
				},
				ScriptHelpers.CreateDispelGroup("Plague of Ages", ScriptHelpers.PartyDispelType.Disease));
		}

		[EncounterHandler(44819, "Siamat")]
		public Composite SiamatEncounter()
		{
			const uint tempestStorm = 44713;
			AddAvoidObject(ctx => true, o => Me.IsRange() && Me.IsMoving ? 12 : 8, tempestStorm);
			AddAvoidObject(ctx => true, 10, u => u.Entry == ServantOfSiamatId && u.ToUnit().HealthPercent <= 5);

			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit);
		}
	}
}