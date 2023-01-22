
using System;
using System.ComponentModel;
using System.IO;
using Styx.Helpers;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;
using Styx;
using Styx.CommonBot;
using Singular.Managers;


namespace Singular.Settings
{
    public enum WarlockPet
    {
        None        = 0,        
        Auto        = 1,
        Imp         = 23,       // Pet.CreatureFamily.Id
        Voidwalker  = 16,
        Succubus    = 17,
        Felhunter   = 15,
        Felguard    = 29,
        Other       = 99999     // a quest or other pet forced upon us for some reason
    }

    public enum Soulstone
    {
        None = 0,
        Auto,
        Self,
        Ressurect
    }

    internal class WarlockSettings : Styx.Helpers.Settings
    {

        public WarlockSettings()
            : base(Path.Combine(SingularSettings.SingularSettingsPath, "Warlock.xml"))
        {
        }

        [Setting]
        [DefaultValue(WarlockPet.Auto)]
        [Category("Pet")]
        [DisplayName("Pet to Summon")]
        [Description("Auto: will automatically select best pet.")]
        public WarlockPet Pet { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Pet")]
        [DisplayName("Health Funnel at %")]
        [Description("Pet Health % to begin Health Funnel in combat")]
        public int HealthFunnelCast { get; set; }

        [Setting]
        [DefaultValue(95)]
        [Category("Pet")]
        [DisplayName("Health Funnel cancel at %")]
        [Description("Pet Health % to cancel Health Funnel in combat")]
        public int HealthFunnelCancel { get; set; }

        [Setting]
        [DefaultValue(75)]
        [Category("Pet")]
        [DisplayName("Health Funnel resting below %")]
        [Description("Pet Health % to cast Health Funnel while resting")]
        public int HealthFunnelRest { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Pet")]
        [DisplayName("Use Disarm")]
        [Description("True: use Disarm on cooldown; False: do not cast")]
        public bool UseDisarm { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Create Soulwell If In Group")]
        [Description("Creates a Soulwell if in a Group at certain point (Battlefield start, etc)")]
        public bool CreateSoulwell { get; set; }

        [Setting]
        [DefaultValue(20)]
        [Category("Common")]
        [DisplayName("Drain Life%")]
        [Description("Health % which we should Drain Life")]
        public int DrainLifePercentage { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Use Fear")]
        [Description("Use Fear when low health or controlling adds")]
        public bool UseFear { get; set; }

        [Setting]
        [DefaultValue(3)]
        [Category("Common")]
        [DisplayName("Use Fear Count")]
        [Description("Use Fear when this many attacking Warlock (not pet); 0 to disable mob count based check")]
        public int UseFearCount { get; set; }

        [Setting]
        [DefaultValue(Soulstone.Auto)]
        [Category("Common")]
        [DisplayName("Use Soulstone")]
        [Description("Auto: Instances=Ressurect, Normal/Battleground=Self, Disabled Movement=None -- Ressurrect requires Singular Combat Rez settings to be set as well")]
        public Soulstone UseSoulstone { get; set; }

        [Setting]
        [DefaultValue(750)]
        [Category("Demonology")]
        [DisplayName("Switch to Caster Fury Level")]
        [Description("Go Caster at this Demonic Fury value (0 - 1000)")]
        public int FurySwitchToCaster { get; set; }

        [Setting]
        [DefaultValue(900)]
        [Category("Demonology")]
        [DisplayName("Switch to Demon Fury Level")]
        [Description("Go Demon above this Demonic Fury value (0 - 1000)")]
        public int FurySwitchToDemon { get; set; }

        [Setting]
        [DefaultValue(1)]
        [Category("Demonology")]
        [DisplayName("Felstorm Mob Count")]
        [Description("0: disable ability, otherwise mob count required within 8 yds.  Controls Wrathstorm also")]
        public int FelstormMobCount { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Demonology")]
        [DisplayName("Use Demonic Leap")]
        [Description("Demonic Leap to disengage from melee")]
        public bool UseDemonicLeap { get; set; }

        public enum SpellPriority
        {
            Noxxic = 1,
            IcyVeins = 2
        }

        [Setting]
        [DefaultValue(SpellPriority.Noxxic )]
        [Category("Destruction")]
        [DisplayName("Spell Priority Selection")]
        public SpellPriority DestructionSpellPriority { get; set; }



#region Setting Helpers


#endregion

    }
}