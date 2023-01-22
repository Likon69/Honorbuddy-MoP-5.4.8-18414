using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Design;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HighVoltz.Professionbuddy.ComponentBase;
using HighVoltz.Professionbuddy.PropertyGridUtilities;
using HighVoltz.Professionbuddy.PropertyGridUtilities.Editors;
using Styx;
using Styx.Helpers;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace HighVoltz.Professionbuddy.Components
{
	[PBXmlElement("Interact", new[] { "InteractionAction" })]
    public sealed class InteractionAction : PBAction
    {
        #region InteractActionType enum

        public enum InteractActionType
        {
            NPC,
            GameObject,
        }

        #endregion

        private readonly Stopwatch _interactSw = new Stopwatch();

        public InteractionAction()
        {
            Properties["Entry"] = new MetaProp("Entry", typeof (uint),
                                               new EditorAttribute(typeof (EntryEditor),
                                                                   typeof (UITypeEditor)),
                                               new DisplayNameAttribute(Strings["Action_Common_Entry"]));

            Properties["InteractDelay"] = new MetaProp("InteractDelay", typeof (uint),
                                                       new DisplayNameAttribute(
                                                           Strings["Action_InteractAction_InteractDelay"]));

            Properties["InteractType"] = new MetaProp("InteractType", typeof (InteractActionType),
                                                      new DisplayNameAttribute(
                                                          Strings["Action_InteractAction_InteractType"]));

            Properties["GameObjectType"] = new MetaProp("GameObjectType", typeof (WoWGameObjectType),
                                                        new DisplayNameAttribute(
                                                            Strings["Action_InteractAction_GameobjectType"]));

            Properties["SpellFocus"] = new MetaProp("SpellFocus", typeof (WoWSpellFocus),
                                                    new DisplayNameAttribute(
                                                        Strings["Action_InteractAction_SpellFocus"]));

            InteractDelay = Entry = 0u;
            InteractType = InteractActionType.GameObject;
            GameObjectType = WoWGameObjectType.Mailbox;
            SpellFocus = WoWSpellFocus.Anvil;

            Properties["SpellFocus"].Show = false;
            Properties["InteractType"].PropertyChanged += InteractionActionPropertyChanged;
            Properties["GameObjectType"].PropertyChanged += InteractionActionPropertyChanged;
        }

        [PBXmlAttribute]
        public InteractActionType InteractType
        {
            get { return Properties.GetValue<InteractActionType>("InteractType"); }
            set { Properties["InteractType"].Value = value; }
        }

        [PBXmlAttribute]
        public uint Entry
        {
            get { return Properties.GetValue<uint>("Entry"); }
            set { Properties["Entry"].Value = value; }
        }

        [PBXmlAttribute]
        public uint InteractDelay
        {
            get { return Properties.GetValue<uint>("InteractDelay"); }
            set { Properties["InteractDelay"].Value = value; }
        }

        [PBXmlAttribute]
        public WoWGameObjectType GameObjectType
        {
            get { return Properties.GetValue<WoWGameObjectType>("GameObjectType"); }
            set { Properties["GameObjectType"].Value = value; }
        }

        [PBXmlAttribute]
        public WoWSpellFocus SpellFocus
        {
            get { return Properties.GetValue<WoWSpellFocus>("SpellFocus"); }
            set { Properties["SpellFocus"].Value = value; }
        }

        public override string Name
        {
            get { return Strings["Action_InteractAction_Name"]; }
        }

		public override string Title
        {
            get
            {
                return string.Format("{0}: {1} " + (Entry != 0 ? Entry.ToString(CultureInfo.InvariantCulture) : ""),
                                     Name, InteractType);
            }
        }

        public override string Help
        {
            get { return Strings["Action_InteractAction_Help"]; }
        }

		protected async override Task Run()
        {
            if (!IsDone)
            {
                WoWObject obj = null;
                if (InteractType == InteractActionType.NPC)
                {
                    if (Entry != 0)
                        obj =
                            ObjectManager.GetObjectsOfType<WoWUnit>().Where(u => u.Entry == Entry).OrderBy(
                                u => u.Distance).FirstOrDefault();
                    else if (StyxWoW.Me.GotTarget)
                        obj = StyxWoW.Me.CurrentTarget;
                }
                else if (InteractType == InteractActionType.GameObject)
                    obj =
                        ObjectManager.GetObjectsOfType<WoWGameObject>().OrderBy(u => u.Distance).FirstOrDefault(
                            u => (Entry > 0 && u.Entry == Entry) || (u.SubType == GameObjectType &&
                                                                     (GameObjectType != WoWGameObjectType.SpellFocus ||
                                                                      (GameObjectType == WoWGameObjectType.SpellFocus &&
                                                                       u.SpellFocus == SpellFocus))));
                if (obj != null)
                {
                    WoWPoint moveToLoc = WoWMathHelper.CalculatePointFrom(Me.Location, obj.Location, 3);
                    if (moveToLoc.Distance(Me.Location) > 4)
                        Util.MoveTo(moveToLoc);
                    else
                    {
                        if (InteractDelay > 0 &&
                            (!_interactSw.IsRunning || _interactSw.ElapsedMilliseconds < InteractDelay))
                        {
                            _interactSw.Start();
                        }
                        else
                        {
                            _interactSw.Reset();
                            obj.Interact();
                            IsDone = true;
                        }
                    }
                }
                if (IsDone)
					PBLog.Log("InteractAction complete");
            }
        }

        private void InteractionActionPropertyChanged(object sender, MetaPropArgs e)
        {
            switch (GameObjectType)
            {
                case WoWGameObjectType.SpellFocus:
                    Properties["SpellFocus"].Show = true;
                    break;
                default:
                    Properties["SpellFocus"].Show = false;
                    break;
            }
            switch (InteractType)
            {
                case InteractActionType.GameObject:
                    Properties["GameObjectType"].Show = true;
                    break;
                case InteractActionType.NPC:
                    Properties["GameObjectType"].Show = false;
                    break;
                default:
                    Properties["GameObjectType"].Show = true;
                    break;
            }
            RefreshPropertyGrid();
        }

		public override IPBComponent DeepCopy()
        {
            return new InteractionAction
                       {
                           InteractType = InteractType,
                           Entry = Entry,
                           GameObjectType = GameObjectType,
                           SpellFocus = SpellFocus,
                           InteractDelay = InteractDelay,
                       };
        }
    }
}