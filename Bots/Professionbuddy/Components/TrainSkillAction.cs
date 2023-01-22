using System;
using System.ComponentModel;
using System.Drawing.Design;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HighVoltz.Professionbuddy.ComponentBase;
using HighVoltz.Professionbuddy.PropertyGridUtilities;
using HighVoltz.Professionbuddy.PropertyGridUtilities.Editors;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace HighVoltz.Professionbuddy.Components
{
	[PBXmlElement("TrainSkill", new[] { "TrainSkillAction" })]
	public sealed class TrainSkillAction : PBAction
	{
		private readonly WaitTimer _interactTimer = new WaitTimer(TimeSpan.FromSeconds(2));
		private readonly WaitTimer _trainWaitTimer = new WaitTimer(TimeSpan.FromSeconds(2));
		private WoWPoint _loc;

		public TrainSkillAction()
		{
			Properties["Location"] = new MetaProp(
				"Location",
				typeof (string),
				new EditorAttribute(
					typeof (LocationEditor),
					typeof (UITypeEditor)),
				new DisplayNameAttribute(Strings["Action_Common_Location"]));

			Properties["NpcEntry"] = new MetaProp(
				"NpcEntry",
				typeof (uint),
				new EditorAttribute(
					typeof (EntryEditor),
					typeof (UITypeEditor)),
				new DisplayNameAttribute(Strings["Action_Common_NpcEntry"]));

			_loc = WoWPoint.Zero;
			Location = _loc.ToInvariantString();
			NpcEntry = 0u;

			Properties["Location"].PropertyChanged += LocationChanged;
		}

		[PBXmlAttribute]
		public uint NpcEntry
		{
			get { return Properties.GetValue<uint>("NpcEntry"); }
			set { Properties["NpcEntry"].Value = value; }
		}

		[PBXmlAttribute]
		public string Location
		{
			get { return Properties.GetValue<string>("Location"); }
			set { Properties["Location"].Value = value; }
		}

		public override string Name
		{
			get { return Strings["Action_TrainSkillAction_Name"]; }
		}

		public override string Title
		{
			get { return string.Format("{0}: {1}", Name, NpcEntry); }
		}

		public override string Help
		{
			get { return Strings["Action_TrainSkillAction_Help"]; }
		}

		private void LocationChanged(object sender, MetaPropArgs e)
		{
			_loc = Util.StringToWoWPoint((string) ((MetaProp) sender).Value);
			Properties["Location"].PropertyChanged -= LocationChanged;
			Properties["Location"].Value = string.Format(CultureInfo.InvariantCulture, "{0}, {1}, {2}", _loc.X, _loc.Y, _loc.Z);
			Properties["Location"].PropertyChanged += LocationChanged;
			RefreshPropertyGrid();
		}

		protected override async Task Run()
		{
			if (!_interactTimer.IsFinished)
				return;

			if (!TrainerFrame.Instance.IsVisible || !StyxWoW.Me.GotTarget || StyxWoW.Me.CurrentTarget.Entry != NpcEntry)
			{
				WoWPoint movetoPoint = _loc;
				WoWUnit unit = ObjectManager.GetObjectsOfType<WoWUnit>().Where(o => o.Entry == NpcEntry).
					OrderBy(o => o.Distance).FirstOrDefault();

				if (unit != null)
					movetoPoint = unit.Location;
				else if (movetoPoint == WoWPoint.Zero)
					movetoPoint = MoveToAction.GetLocationFromDB(MoveToAction.MoveToType.NpcByID, NpcEntry);

				if (movetoPoint != WoWPoint.Zero && StyxWoW.Me.Location.Distance(movetoPoint) > 4.5)
				{
					Util.MoveTo(movetoPoint);
					return;
				}

				if (GossipFrame.Instance.IsVisible)
				{
					foreach (GossipEntry ge in GossipFrame.Instance.GossipOptionEntries)
					{
						if (ge.Type == GossipEntry.GossipEntryType.Trainer)
						{
							GossipFrame.Instance.SelectGossipOption(ge.Index);
							return;
						}
					}
					PBLog.Warn("NPC does not provide a train gossip option");
					// we should not continue at this point.
					TreeRoot.Stop();
					return;
				}

				if (Me.IsMoving)
				{
					WoWMovement.MoveStop();
					return;
				}

				if (unit != null)
				{
					if (Me.CurrentTargetGuid != unit.Guid)
					{
						unit.Target();
						return;
					}
					_interactTimer.Reset();
					unit.Interact();
				}

				return;
			}

			if (_trainWaitTimer.IsFinished)
			{
				using (StyxWoW.Memory.AcquireFrame())
				{
					Lua.DoString("SetTrainerServiceTypeFilter('available', 1)");
					// check if there is any abilities to that need training.
					var numOfAvailableAbilities =
						Lua.GetReturnVal<int>(
							"local a=0 for n=GetNumTrainerServices(),1,-1 do if select(3,GetTrainerServiceInfo(n)) == 'available' then a=a+1 end end return a ",
							0);
					if (numOfAvailableAbilities == 0)
					{
						IsDone = true;
						PBLog.Log("Done training");
						return;
					}
					Lua.DoString("BuyTrainerService(0) ");
					_trainWaitTimer.Reset();
				}
			}
		}

		public override IPBComponent DeepCopy()
		{
			return new TrainSkillAction {NpcEntry = NpcEntry, _loc = _loc, Location = Location};
		}
	}
}