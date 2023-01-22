
using System.ComponentModel;
using System.IO;
using Styx.Helpers;
using Styx.WoWInternals.WoWObjects;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;

namespace Singular.Settings
{
    public class HealerSettings : Styx.Helpers.Settings
    {
        [Browsable(false)]
        public HealingContext Context { get; set; }

        // reqd ctor
        public HealerSettings(string className, HealingContext ctx)
            : base(Path.Combine(SingularSettings.SingularSettingsPath, className + "-Heal-" + ctx.ToString() + ".xml"))
        {
            Context = ctx;
        }

        // hide default ctor
        private HealerSettings()
            : base(null)
        {
        }


#if false

        [Setting]
        [DefaultValue(47)]
        [Category("Talents")]
        [DisplayName("Stone Bulwark Totem %")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int StoneBulwarkTotemPercent { get; set; }

#endif
    }
}