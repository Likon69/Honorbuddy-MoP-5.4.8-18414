
using System;
using System.ComponentModel;
using System.IO;
using Styx.Helpers;
using Styx.WoWInternals.WoWObjects;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;

namespace Singular.Settings
{
    public enum DeathKnightPresence
    {
        None = 0,
        Auto,
        Blood,
        Frost,
        Unholy
    }

    public enum DarkSimulacrumTarget
    {
        None = 0,
        All,
        HealersOnly
    }

    internal class DeathKnightSettings : Styx.Helpers.Settings
    {
        public DeathKnightSettings()
            : base(Path.Combine(SingularSettings.SingularSettingsPath, "DeathKnight.xml"))
        {
        }

        #region Common

        [Setting]
        [DefaultValue(DeathKnightPresence.Auto)]
        [Category("Common")]
        [DisplayName("Presence")]
        [Description("Auto: best presence for Spec/Role/Context, None: user controlled")]
        public DeathKnightPresence Presence { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Common")]
        [DisplayName("Army of the Dead")]
        public bool UseArmyOfTheDead { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Common")]
        [DisplayName("Path of Frost")]
        public bool UsePathOfFrost { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Common")]
        [DisplayName("Conversion Percent")]
        [Description("Health percent when to use Conversion for healing.")]
        public int ConversionPercent { get; set; }

        [Setting]
        [DefaultValue(80)]
        [Category("Common")]
        [DisplayName("Conversion RunicPower Percent")]
        [Description("Use Conversion only if runic power is at or above this value.")]
        public int MinimumConversionRunicPowerPrecent { get; set; }

        [Setting]
        [DefaultValue(2)]
        [Category("Common")]
        [DisplayName("Death and Decay Add Count")]
        [Description("Will use Death and Decay when agro mob count is equal to or higher then this value. This basicly determines AoE rotation")]
        public int DeathAndDecayCount { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Common")]
        [DisplayName("Death Pact Percent")]
        [Description("Health percent when to use Death Pact for healing.")]
        public int DeathPactPercent { get; set; }

        [Setting]
        [DefaultValue(85)]
        [Category("Common")]
        [DisplayName("Death Siphon Percent")]
        [Description("Health percent when to use Death Siphon for healing.")]
        public int DeathSiphonPercent { get; set; }

        [Setting]
        [DefaultValue(30)]
        [Category("Common")]
        [DisplayName("Death Strike Emergency Percent [DPS]")]
        public int DeathStrikeEmergencyPercent { get; set; }

        [Setting]
        [DefaultValue(DarkSimulacrumTarget.All)]
        [Category("Common")]
        [DisplayName("Dark Simulacrum Target")]
        [Description("None: disabled, All: targets everyone, HealersOnly: targets only healers in PVP / everything in PVE")]
        public DarkSimulacrumTarget TargetWithDarkSimulacrum { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Common")]
        [DisplayName("Dark Simulacrum Auto Clear Aura")]
        [Description("True: clear Hand of Protection, Ice Block, etc. immediately after use (used as a quick cleanse); False: doe not clear")]
        public bool AutoClearAuraWithDarkSimulacrum { get; set; }

        [Setting]
        [DefaultValue(30)]
        [Category("Common")]
        [DisplayName("Icebound Fortitude Percent")]
        public int IceboundFortitudePercent { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Common")]
        [DisplayName("Lichborne Percent")]
        [Description("Health percent when to use Lichborne + Death Coil for healing.")]
        public int LichbornePercent { get; set; }


        #endregion

        #region Category: Blood

        [Setting]
        [DefaultValue(60)]
        [Category("Blood")]
        [DisplayName("Army Of The Dead Percent")]
        [Description("Cast when our Health % falls below this setting")]
        public int ArmyOfTheDeadPercent { get; set; }

        [Setting]
        [DefaultValue(4)]
        [Category("Blood")]
        [DisplayName("Blood Boil Count")]
        [Description("Use Bloodboil when there are at least this many nearby enemies.")]
        public int BloodBoilCount { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Blood")]
        [DisplayName("BoneShield Exclusive")]
        [Description("False: cast on Cooldown, True: cast if no active Bone Shield, Vampiric Blood, Dancing Rune Weapon, Lichborne, Icebound Fortitude")]
        public bool BoneShieldExclusive { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Blood")]
        [DisplayName("Death Pact Exclusive")]
        [Description("False: Raise Ally as needed for Death Pact, True: Raise Ally for Death Pact only if no active Bone Shield, Vampiric Blood, Dancing Rune Weapon, Lichborne, Icebound Fortitude")]
        public bool DeathPactExclusive { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Blood")]
        [DisplayName("Empower Rune Weapon Percent")]
        [Description("Cast when our Health % falls below this setting")]
        public int EmpowerRuneWeaponPercent { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Blood")]
        [DisplayName("Icebound Fortitude Exclusive")]
        [Description("False: cast if needed, True: cast if needed and no active Bone Shield, Vampiric Blood, Dancing Rune Weapon, Lichborne, Icebound Fortitude")]
        public bool IceboundFortitudeExclusive { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Blood")]
        [DisplayName("Lichborne Exclusive")]
        [Description("False: cast if needed, True: cast if needed and no active Bone Shield, Vampiric Blood, Dancing Rune Weapon, Lichborne, Icebound Fortitude")]
        public bool LichborneExclusive { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Blood")]
        [DisplayName("Rune Tap Percent")]
        [Description("Cast when our Health % falls below this")]
        public int RuneTapPercent { get; set; }

        [Setting]
        [DefaultValue(70)]
        [Category("Blood")]
        [DisplayName("Summon Ghoul Percent")]
        [Description("Blood Spec: Cast Raise Dead when Blood Spec Health falls below this Health %")]
        public int SummonGhoulPercentBlood { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Blood")]
        [DisplayName("Use Ghoul As Dps CoolDown")]
        [Description("Blood Spec: Ghoul is used for DPS rather than saved for Death Pact")]
        public bool UseGhoulAsDpsCdBlood { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Blood")]
        [DisplayName("Vampiric Blood Percent")]
        [Description("Cast when our Health % falls below this")]
        public int VampiricBloodPercent { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Blood")]
        [DisplayName("Vampiric Blood Exclusive")]
        [Description("False: cast if needed, True: cast if needed and no active Bone Shield, Vampiric Blood, Dancing Rune Weapon, Lichborne, Icebound Fortitude")]
        public bool VampiricBloodExclusive { get; set; } 
        #endregion

        #region Category: Frost
        [Setting]
        [DefaultValue(false)]
        [Category("Frost")]
        [DisplayName("Use Ghoul As Dps CoolDown")]
        [Description("Frost Spec: Ghoul is used for DPS rather than saved for Death Pact")]
        public bool UseGhoulAsDpsCdFrost { get; set; }

        [Setting]
        [DefaultValue(70)]
        [Category("Frost")]
        [DisplayName("Summon Ghoul Percent")]
        [Description("Frost Spec: Cast Raise Dead when Blood Spec Health falls below this Health %")]
        public int SummonGhoulPercentFrost { get; set; }
        
        #endregion

        #region Category: Unholy

        [Setting]
        [DefaultValue(true)]
        [Category("Unholy")]
        [DisplayName("Summon Gargoyle")]
        [Description("False: do not cast, True: cast when a long cooldown is appropriate (Boss, PVP, stressful solo fight)")]
        public bool UseSummonGargoyle { get; set; }

        #endregion

    }
}