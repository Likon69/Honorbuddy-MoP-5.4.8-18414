
using System.ComponentModel;
using System.IO;
using Styx.Helpers;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;
using System.Collections.Generic;

using System.Xml.Serialization;
// using System.IO;
// using System.Reflection;

namespace Singular.Settings
{
    public enum MageArmor
    {
        None = 0,
        Auto = 1,
        Frost,
        Mage,
        Molten
    }

    internal class MageSettings : Styx.Helpers.Settings
    {
        public MageSettings()
            : base(Path.Combine(SingularSettings.SingularSettingsPath, "Mage.xml"))
        {
            // bit of a hack -- SavedToFile setting tracks if we have ever saved
            // .. these settings.  this is needed because we can't use the DefaultValue
            // .. attribute for a multi values setting
            if (!SavedToFile)
            {
                SavedToFile = true;
            }
        }


        // hidden setting to track if we have ever saved this settings file before
        [Setting]
        [Browsable(false)]
        [DefaultValue(false)]
        public bool SavedToFile { get; set; }

        #region Category: Common

        [Setting]
        [Styx.Helpers.DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Summon Table If In A Party")]
        [Description("Summons a food table instead of using conjured food if in a party")]
        public bool SummonTableIfInParty { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Common")]
        [DisplayName("Use Time Warp")]
        [Description("Time Warp when appropriate (never when movement disabled)")]
        public bool UseTimeWarp { get; set; }

        [Setting]
        [DefaultValue(MageArmor.Auto)]
        [Category("Common")]
        [DisplayName("Armor Buff")]
        [Description("Which Armor Buff to cast (None: user controlled, Auto: best choice)")]
        public MageArmor Armor { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Use Slow Fall")]
        [Description("True: Cast Slow Fall if falling")]
        public bool UseSlowFall { get; set; }

        [Setting]
        [DefaultValue(40)]
        [Category("Common")]
        [DisplayName("Heal Water Elemental %")]
        [Description("Pet Health % which we cast Frost Bolt to Heal")]
        public int HealWaterElementalPct { get; set; }

        #endregion

    }
}
