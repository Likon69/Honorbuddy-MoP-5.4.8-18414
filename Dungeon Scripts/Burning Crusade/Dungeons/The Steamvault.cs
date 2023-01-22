using System.Collections.Generic;
using System.Linq;
using Styx;
using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Burning_Crusade
{
	public class TheSteamvault : CoilfangDungeon
	{
		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 147; }
		}

		public override WoWPoint Entrance
		{
			get { return new WoWPoint(818.6766, 6948.645, -80.58394) ; }
		}

		public override WoWPoint ExitLocation
		{
			get { return new WoWPoint(-26.59528, 4.435789, -4.313096); }
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			foreach (var obj in incomingunits)
			{
				var unit = obj as WoWUnit;
				if (unit != null)
				{
					if (unit.Entry == NagaDistillerId && unit.CanSelect)
						outgoingunits.Add(unit);
					if (unit.Entry == StrandCrabId && unit.Combat)
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
					if (unit.Entry == NagaDistillerId)
						priority.Score += 5000;

					if (StyxWoW.Me.IsDps())
					{
						if (unit.Entry == CoilfangWaterElementalId)
							priority.Score += 500;
					}
				}
			}
		}


		#endregion

		private const uint CoilfangWaterElementalId = 17917;
		private const uint NagaDistillerId = 17954;
		const uint StrandCrabId = 6827;

		[EncounterHandler(17797, "Hydromancer Thespia")]
		public Composite HydromancerThespiaEncounter()
		{
			const uint lightningCloudId = 25033;
			AddAvoidObject(ctx => true, 10, lightningCloudId);
			WoWUnit boss = null;
			return new PrioritySelector(ctx => boss = ctx as WoWUnit, ScriptHelpers.CreateSpreadOutLogic(ctx => !Me.IsCasting, ctx => boss.Location, 10, 35));
		}


		[ObjectHandler(184125, "Main Chambers Access Panel")]
		public Composite MainChambersAccessPanel1Handler()
		{
			WoWGameObject accessPanel = null;

			return new PrioritySelector(
				ctx => accessPanel = ctx as WoWGameObject,
				new Decorator(
					ctx => !ScriptHelpers.IsBossAlive("Hydromancer Thespia") && accessPanel.State == WoWGameObjectState.Ready,
					ScriptHelpers.CreateInteractWithObject(184125, 4)));
		}

		[ObjectHandler(184126, "Main Chambers Access Panel")]
		public Composite MainChambersAccessPanel2Handler()
		{
			WoWGameObject accessPanel = null;
			return new PrioritySelector(
				ctx => accessPanel = ctx as WoWGameObject,
				new Decorator(
					ctx => !ScriptHelpers.IsBossAlive("Mekgineer Steamrigger") && accessPanel.State == WoWGameObjectState.Ready,
					ScriptHelpers.CreateInteractWithObject(184126, 4)));
		}


		[EncounterHandler(17796, "Mekgineer Steamrigger")]
		public Composite MekgineerSteamriggerEncounter()
		{
			return new PrioritySelector();
		}


		[EncounterHandler(17798, "Warlord Kalithresh")]
		public Composite WarlordKalithreshEncounter()
		{
			WoWUnit boss = null;
			WoWUnit distiller = null;
			return new PrioritySelector(
				ctx =>
				{
					distiller = ObjectManager.GetObjectsOfType<WoWUnit>().Where(o => o.Entry == 17954 && o.IsAlive).OrderBy(o => o.DistanceSqr).FirstOrDefault();
					return boss = ctx as WoWUnit;
				},
				ScriptHelpers.CreateTankAgainstObject(ctx => distiller != null && boss.CurrentTargetGuid == Me.Guid, ctx => distiller.Location, ctx => 5));
		}
	}
}