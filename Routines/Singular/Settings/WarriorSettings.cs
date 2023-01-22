
using System.ComponentModel;
using System.IO;
using Styx.Helpers;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;

namespace Singular.Settings
{
    public enum WarriorStance
    {
        Auto,
        BattleStance        = Styx.ShapeshiftForm.BattleStance ,
        BerserkerStance     = Styx.ShapeshiftForm.BerserkerStance ,
        DefensiveStance     = Styx.ShapeshiftForm.DefensiveStance 
    }

    public enum WarriorShout
    {
        CommandingShout,
        BattleShout
    }

    internal class WarriorSettings : Styx.Helpers.Settings
    {
        public WarriorSettings()
            : base(Path.Combine(SingularSettings.SingularSettingsPath, "Warrior.xml"))
        {
        }

        #region Protection

        public enum SpellPriority
        {
            Noxxic = 1,
            IcyVeins = 2,
            ElitistJerks = 3
        }

        [Setting]
        [DefaultValue(SpellPriority.Noxxic)]
        [Category("Arms")]
        [DisplayName("Spell Priority Selection")]
        public SpellPriority ArmsSpellPriority { get; set; }

        [Setting]
        [DefaultValue(50)]
        [Category("Protection")]
        [DisplayName("Enraged Regeneration Health")]
        [Description("Enrage Regeneration will be used when your health drops below this value")]
        public int WarriorEnragedRegenerationHealth { get; set; }

        [Setting]
        [DefaultValue(30)]
        [Category("Protection")]
        [DisplayName("Shield Wall Health")]
        [Description("Shield Wall will be used when your health drops below this value")]
        public int WarriorShieldWallHealth { get; set; }


        [Setting]
        [DefaultValue(20)]
        [Category("Protection")]
        [DisplayName("Last Stand Health")]
        [Description("Last Stand will be used when your health drops below this value")]
        public int WarriorLastStandHealth  { get; set; }

        [Setting]
        [DefaultValue(50)]
        [Category("Protection")]
        [DisplayName("Shield Block Health")]
        [Description("Shield Block will be used when your health drops below this value")]
        public int WarriorShieldBlockHealth { get; set; }

        [Setting]
        [DefaultValue(80)]
        [Category("Protection")]
        [DisplayName("Shield Barrier Health")]
        [Description("Shield Barrier will be used when your health drops below this value")]
        public int WarriorShieldBarrierHealth { get; set; }

        #endregion

        #region DPS

        [Setting]
        [DefaultValue(true)]
        [Category("DPS")]
        [DisplayName("Use Interupts")]
        [Description("True / False if you would like the cc to use Interupts")]
        public bool UseWarriorInterrupts { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("DPS")]
        [DisplayName("Slows")]
        [Description("True / False if you would like the cc to use slows ie. Hammstring, Piercing Howl")]
        public bool UseWarriorSlows { get; set; }
        
        [Setting]
        [DefaultValue(true)]
        [Category("DPS")]
        [DisplayName("Use Charge/Heroic Leap?")]
        [Description("True / False if you would like the cc to use any gap closers")]
        public bool UseWarriorCloser { get; set; }
        #endregion

        [Setting]
        [DefaultValue(WarriorStance.Auto)]
        [Category("DPS")]
        [DisplayName("Warrior DPS Stance")]
        [Description("The stance to use while DPSing. Battle stance if there is little incoming damage, Berserker otherwise. Protection will always use Defensive stance.")]
        public WarriorStance Stance { get; set; }

        [Setting]
        [DefaultValue(WarriorShout.BattleShout)]
        [Category("General")]
        [DisplayName("Warrior Shout")]
        [Description("The shout to use to keep the buff up, or use for low-rage situations.")]
        public WarriorShout Shout { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("General")]
        [DisplayName("Victory Rush on Cooldown")]
        [Description("True: use Victory Rush/Impending Victory on cooldown regardless of current health %")]
        public bool VictoryRushOnCooldown { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("General")]
        [DisplayName("Use Disarm")]
        [Description("True: use Disarm on cooldown; False: do not cast")]
        public bool UseDisarm { get; set; }

    }
}