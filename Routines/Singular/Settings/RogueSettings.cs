
using System.ComponentModel;
using System.IO;
using Singular.ClassSpecific.Rogue;
using Styx.Helpers;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;

namespace Singular.Settings
{
    internal class RogueSettings : Styx.Helpers.Settings
    {
        public RogueSettings()
            : base(Path.Combine(SingularSettings.SingularSettingsPath, "Rogue.xml"))
        {
        }

        [Setting]
        [DefaultValue(false)]
        [Category("Common")]
        [DisplayName("Use Sprint")]
        [Description("Sprint to close to target during Pull or Combat")]
        public bool UseSprint { get; set; }

        [Setting]
        [DefaultValue(70)]
        [Category("Common")]
        [DisplayName("Recuperate Health%")]
        [Description("Health % to Recuperate at during Combat, 0 to disable")]
        public int RecuperateHealth { get; set; }

        [Setting]
        [DefaultValue(LethalPoisonType.Auto)]
        [Category("Common")]
        [DisplayName("Lethal Poison")]
        [Description("Lethal Poison")]
        public LethalPoisonType LethalPoison { get; set; }

        [Setting]
        [DefaultValue(NonLethalPoisonType.Auto)]
        [Category("Common")]
        [DisplayName("Non Lethal Poison")]
        [Description("Non Lethal Poison")]
        public NonLethalPoisonType NonLethalPoison { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Common")]
        [DisplayName("Stealth Always")]
        [Description("Stealth at all times out of combat. Does not disable mounting (you can in HB Settings if desired)")]
        public bool StealthAlways { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Stealth When Eating")]
        [Description("Stealth if eating food")]
        public bool StealthIfEating { get; set; }

        [Setting]
        [DefaultValue(4)]
        [Category("Common")]
        [DisplayName("Fan of Knives Count")]
        [Description("Use FoK as Combo Point builder at this enemy count")]
        public int FanOfKnivesCount { get; set; }

        [Setting]
        [DefaultValue(4)]
        [Category("Common")]
        [DisplayName("AOE Spell Priority Count")]
        [Description("Use AOE Spell Priorities at this enemy count")]
        public int AoeSpellPriorityCount { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Interrupt Spells")]
        [Description("Interrupt Spells")]
        public bool InterruptSpells { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Use TotT")]
        [Description("Use TotT")]
        public bool UseTricksOfTheTrade { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Use Pick Lock")]
        [Description("Requires AutoLoot ON; unlock boxes in bags during rest")]
        public bool UsePickLock { get; set; }

        [Setting]
        [DefaultValue(25)]
        [Category("Common")]
        [DisplayName("Sap Adds Distance")]
        [Description("Sap mobs within this many yards of target that may aggro; 0 to disable")]
        public int SapAddDistance { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Sap target if moving")]
        [Description("Sap target that is moving to avoid having to follow while stealthed")]
        public bool SapMovingTargetsOnPull { get; set; }


        [Setting]
        [DefaultValue(true)]
        [Category("Combat Spec")]
        [DisplayName("Use Rupture Finisher")]
        [Description("Use Rupture Finisher")]
        public bool CombatUseRuptureFinisher { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Common")]
        [DisplayName("Move Behind Targets")]
        [Description("Move behind targets for opener or when target stunned/not targeting Rogue")]
        public bool MoveBehindTargets { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Combat Spec")]
        [DisplayName("Use Expose Armor")]
        [Description("Use Expose Armor")]
        public bool UseExposeArmor { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Use Speed Buff")]
        [Description("Cast Burst of Speed when running out of combat")]
        public bool UseSpeedBuff { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Pick Pocket")]
        [DisplayName("Pick Pocket Only - Disable Pull")]
        [Description("Caution: use only with profiles designed for grinding.  Does not work with most Questing profiles")]
        public bool PickPocketOnlyPull { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Pick Pocket")]
        [DisplayName("Use Pick Pocket")]
        [Description("Requires AutoLoot ON; pick pocket mob before opener")]
        public bool UsePickPocket { get; set; }

        [Setting]
        [DefaultValue(3)]
        [Category("Pick Pocket")]
        [DisplayName("Post-Pick Pocket Pause")]
        [Description("Minutes to blacklist target after successful Pick Pocket. Does not prevent Combat with target")]
        public int SuccessfulPostPickPocketBlacklistMinutes { get; set; }

        [Setting]
        [DefaultValue(200)]
        [Category("Pick Pocket")]
        [DisplayName("Post-Pick Pocket Pause")]
        [Description("Milliseconds to pause after Pick Pocket (for systems with a slow Auto Loot reaction since Pick Pocket has no GCD)")]
        public int PostPickPocketPause { get; set; }

        [Setting]
        [DefaultValue(100)]
        [Category("Pick Pocket")]
        [DisplayName("Pre-Pick Pocket ms Pause")]
        [Description("Milliseconds to pause before Pick Pocket (for systems with lag)")]
        public int PrePickPocketPause { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Pick Pocket")]
        [DisplayName("Allow Pick Pocket in Combat")]
        [Description("Allow use in Combat on Current Target if spell usable and current target valid")]
        public bool AllowPickPocketInCombat { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("General")]
        [DisplayName("Use Dismantle")]
        [Description("True: use Dismantle on cooldown; False: do not cast")]
        public bool UseDimantle { get; set; }
    }
}