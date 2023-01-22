
using System.ComponentModel;
using System.IO;
using Styx.Helpers;
using Styx.WoWInternals.WoWObjects;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;
using System.Windows.Forms;

namespace Singular.Settings
{
    internal class HotkeySettings : Styx.Helpers.Settings
    {
        public HotkeySettings()
            : base(Path.Combine(SingularSettings.CharacterSettingsPath, "SingularSettings.Hotkeys.xml"))
        { 
            // bit of a hack -- SavedToFile setting tracks if we have ever saved
            // .. these settings.  this is needed because we can't use the DefaultValue
            // .. attribute for a multi values setting
            if (!SavedToFile)
            {
                // force to true so when loaded next time we skip the following
                SavedToFile = true;

                // set default values for array
                SuspendMovementKeys = new Keys[] 
                {
                    // default WOW bindings for movement keys
                    Keys.MButton,
                    Keys.RButton,
                    Keys.W,
                    Keys.S,
                    Keys.A,
                    Keys.D,
                    Keys.Q,
                    Keys.E,
                    Keys.Up,
                    Keys.Down,
                    Keys.Left,
                    Keys.Right
                };
            }
        }


        // hidden setting to track if we have ever saved this settings file before
        [Setting]
        [Browsable(false)]
        [DefaultValue(false)]
        public bool SavedToFile { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Control")]
        [DisplayName("Chat Frame Message")]
        [Description("Outputs message to Chat frame when toggle pressed")]
        public bool ChatFrameMessage { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Control")]
        [DisplayName("Key Toggles Behavior")]
        [Description("True: press key to disable, press key again to enable; False: press and hold key to disable, release key to enable")]
        public bool KeysToggleBehavior { get; set; }

        [Setting]
        [DefaultValue(Keys.None)]
        [Category("Hotkeys")]
        [DisplayName("Key - AOE")]
        [Description("Enables/Disables AOE Combat Abilities")]
        public Keys AoeToggle { get; set; }

        [Setting]
        [DefaultValue(Keys.None)]
        [Category("Hotkeys")]
        [DisplayName("Key - Combat")]
        [Description("Enables/Disables All Combat Abilities")]
        public Keys CombatToggle { get; set; }

        [Setting]
        [DefaultValue(Keys.None)]
        [Category("Hotkeys")]
        [DisplayName("Key - Movement")]
        [Description("Enables/Disables Singular Movement")]
        public Keys MovementToggle { get; set; }

        [Setting]
        [DefaultValue(Keys.None)]
        [Category("Hotkeys")]
        [DisplayName("Key - Pull More")]
        [Description("Enables/Disables Pull More Ability")]
        public Keys PullMoreToggle { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("User Movement while Botting")]
        [DisplayName("Allow User Movement")]
        [Description("True: keys below suspend bot movement for # seconds")]
        public bool SuspendMovement { get; set; }

        [Setting]
        [DefaultValue(3)]
        [Category("User Movement while Botting")]
        [DisplayName("Suspend Duration")]
        [Description("Seconds after last suspend keypress to disable movement")]
        public int SuspendDuration { get; set; }

        [Setting]
        [Category("User Movement while Botting")]
        [DisplayName("Suspend Keys")]
        [Description("Keys that will disable ALL movement for # seconds")]
        public Keys[] SuspendMovementKeys { get; set; }

    }
}