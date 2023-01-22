using System.IO;
using Styx;
using Styx.Helpers;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Styx.Common;

namespace Bobby53
{
    public sealed class LazyRaiderSettings : Settings
    {
        public enum Keypress
        {
            None,
            LSHIFT,
            RSHIFT,
            LCTRL,
            RCTRL,
            LALT,
            RALT
        };

        private static LazyRaiderSettings _singleton;
        public static LazyRaiderSettings Instance
        {
            get
            {
                return _singleton ?? (_singleton = new LazyRaiderSettings());
            }
        }

        public LazyRaiderSettings () 
            : base(Path.Combine(Utilities.AssemblyDirectory, string.Format("Settings/LazyRaider_{0}.xml", StyxWoW.Me.Name)))
        {
        }


        [Setting, DefaultValue(false)]
        public bool NoTank { get; set; }

        [Setting, DefaultValue(false)]
        public bool FollowTank { get; set; }

        [Setting, DefaultValue(10)]
        public int FollowDistance { get; set; }

        [Setting, DefaultValue(false)]
        public bool AutoTankSelect { get; set; }

        [Setting, DefaultValue(false)]
        public bool AutoTarget { get; set; }

        [Setting, DefaultValue(false)]
        public bool AutoTargetOnlyIfNotValidTarget { get; set; }

        [Setting, DefaultValue(true)]
        public bool AutoAttack { get; set; }

        [Setting, DefaultValue(true)]
        public bool DismountOnlyWithTankOrUser { get; set; }

        [Setting, DefaultValue(true)]
        public bool DisablePlugins { get; set; }

        [Setting, DefaultValue(true)]
        public bool LockMemory { get; set; }

        [Setting, DefaultValue(30)]
        public int FPS { get; set; }

        [Setting, DefaultValue( Keypress.None ) ]
        public Keypress PauseKey { get; set; }

        [Setting, DefaultValue(false)]
        public bool PauseWhileKeyPressed { get; set; }

        [Setting, DefaultValue(true)]
        public bool ShowPauseMessageInGame { get; set; }

        [Setting, DefaultValue(false)]
        public bool RaidBot { get; set; }

        [Setting, DefaultValue(true)]
        public bool CharacterManager { get; set; }

    }
}
