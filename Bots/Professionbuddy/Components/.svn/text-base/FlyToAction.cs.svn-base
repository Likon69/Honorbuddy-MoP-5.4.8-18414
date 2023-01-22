using System.ComponentModel;
using System.Drawing.Design;
using System.Globalization;
using System.Threading.Tasks;
using HighVoltz.Professionbuddy.ComponentBase;
using HighVoltz.Professionbuddy.PropertyGridUtilities;
using HighVoltz.Professionbuddy.PropertyGridUtilities.Editors;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.Pathing;

namespace HighVoltz.Professionbuddy.Components
{
	[PBXmlElement("FlyTo", new[] { "FlyToAction" })]
	public sealed class FlyToAction : PBAction
	{
		private WoWPoint _loc;

		public FlyToAction()
		{
			Properties["Dismount"] = new MetaProp(
				"Dismount",
				typeof (bool),
				new DisplayNameAttribute(Strings["Action_FlyToAction_Dismount"]));

			Properties["Location"] = new MetaProp(
				"Location",
				typeof (string),
				new EditorAttribute(
					typeof (LocationEditor),
					typeof (UITypeEditor)),
				new DisplayNameAttribute(Strings["Action_Common_Location"]));

			Location = _loc.ToInvariantString();
			Dismount = true;

			Properties["Location"].PropertyChanged += LocationChanged;
		}

		[PBXmlAttribute]
		public bool Dismount
		{
			get { return Properties.GetValue<bool>("Dismount"); }
			set { Properties["Dismount"].Value = value; }
		}

		[PBXmlAttribute]
		public string Location
		{
			get { return Properties.GetValue<string>("Location"); }
			set { Properties["Location"].Value = value; }
		}

		public override string Name
		{
			get { return Strings["Action_FlyToAction_Name"]; }
		}

		public override string Title
		{
			get { return string.Format("{0}: {1} ", Name, Location); }
		}

		public override string Help
		{
			get { return Strings["Action_FlyToAction_Help"]; }
		}

		private void LocationChanged(object sender, MetaPropArgs e)
		{
			_loc = Util.StringToWoWPoint((string) ((MetaProp) sender).Value);
			Properties["Location"].PropertyChanged -= LocationChanged;
			Properties["Location"].Value = string.Format(
				CultureInfo.InvariantCulture,
				"{0}, {1}, {2}",
				_loc.X,
				_loc.Y,
				_loc.Z);
			Properties["Location"].PropertyChanged += LocationChanged;
			RefreshPropertyGrid();
		}


		protected override async Task Run()
		{
			if (StyxWoW.Me.Location.Distance(_loc) > 6)
			{
				Flightor.MoveTo(_loc);
				TreeRoot.StatusText = string.Format("Flying to location {0}", _loc);
			}
			else
			{
				if (Dismount)
					await CommonCoroutines.LandAndDismount(landPoint: _loc);
				//Lua.DoString("Dismount() CancelShapeshiftForm()");
				IsDone = true;
				TreeRoot.StatusText = string.Format("Arrived at location {0}", _loc);
			}
		}

		public override IPBComponent DeepCopy()
		{
			return new FlyToAction {Location = Location, Dismount = Dismount};
		}
	}
}