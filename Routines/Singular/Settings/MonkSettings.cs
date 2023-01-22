
using System.ComponentModel;
using System.IO;
using Singular.ClassSpecific.Rogue;
using Styx.Helpers;
using DefaultValue = Styx.Helpers.DefaultValueAttribute;
using Styx;
using System.Drawing;

namespace Singular.Settings
{
    internal class MonkSettings : Styx.Helpers.Settings
    {
        public MonkSettings()
            : base(Path.Combine(SingularSettings.SingularSettingsPath, "Monk.xml")) { }

        #region Spheres

        [Setting]
        [DefaultValue(45)]
        [Category("Spheres")]
        [DisplayName("Rest Healing Sphere Health")]
        [Description("Min Resting Health % to cast Healing Sphere")]
        public int RestHealingSphereHealth { get; set; }

        #endregion

        #region Common

        [Setting]
        [DefaultValue(false)]
        [Category("Common")]
        [DisplayName("Disable Roll")]
        [Description("Prevent Singular from casting Roll")]
        public bool DisableRoll { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Common")]
        [DisplayName("Fortifying Brew Percent")]
        [Description("Fortifying Brew is used when health percent is at or below this value")]
        public int FortifyingBrewPct { get; set; }

        [Setting]
        [DefaultValue(70)]
        [Category("Common")]
        [DisplayName("Chi Wave Percent")]
        [Description("Chi Wave is used when health percent is at or below this value")]
        public int ChiWavePct { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("General")]
        [DisplayName("Use Grapple Weapon")]
        [Description("True: use Grapple Weapon on cooldown; False: do not cast")]
        public bool UseGrappleWeapon { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Leg Sweep immediately in Normal Context")]
        [Description("Stun mobs while Solo immediately to reduce damage taken")]
        public bool StunMobsWhileSolo { get; set; }

        [Setting]
        [DefaultValue(4)]
        [Category("Common")]
        [DisplayName("Spinning Crane Kick Count")]
        [Description("Count of enemies in range to attack with Spinning Crane Kick / Rushing Jade Wind")]
        public int SpinningCraneKickCnt { get; set; }


        #endregion

        #region Brewmaster

        [Setting]
        [DefaultValue(70)]
        [Category("Brewmaster")]
        [DisplayName("Avert Harm Group Health Percent")]
        [Description("Avert Harm is used when the averge health percent of group falls below this value")]
        public int AvertHarmGroupHealthPct { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Brewmaster")]
        [DisplayName("Use Avert Harm")]
        public bool UseAvertHarm { get; set; }

        [Setting]
        [DefaultValue(6)]
        [Category("Brewmaster")]
        [DisplayName("Elusive Brew Min. Stack")]
        [Description("Elusive Brew is used when player has this many stacks of Elusive Brew or more")]
        public int ElusiveBrewMinumumCount { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Brewmaster")]
        [DisplayName("Use Elusive Brew")]
        public bool UseElusiveBrew { get; set; }
        #endregion

        #region Windwalker

        [Setting]
        [DefaultValue(true)]
        [Category("Windwalker")]
        [DisplayName("Allow Off-Heal")]
        public bool AllowOffHeal { get; set; }

        [Setting]
        [DefaultValue(65)]
        [Category("Windwalker")]
        [DisplayName("Expel Harm Health")]
        public int ExpelHarmHealth { get; set; }

        #endregion

        #region Mistweaver

        [Setting]
        [DefaultValue(85)]
        [Category("Mistweaver")]
        [DisplayName("Mana Tea % (instant)")]
        public int ManaTeaPercentInstant { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Mistweaver")]
        [DisplayName("Mana Tea % (channel)")]
        public int ManaTeaPercent { get; set; }

        #endregion

        #region Context Late Loading Wrappers

        private MistweaverHealSettings _mistbattleground;
        private MistweaverHealSettings _mistinstance;
        private MistweaverHealSettings _mistraid;
        private MonkOffHealSettings _offhealbattleground;
        private MonkOffHealSettings _offhealpve;

        [Browsable(false)]
        public MistweaverHealSettings MistBattleground { get { return _mistinstance ?? (_mistinstance = new MistweaverHealSettings(HealingContext.Battlegrounds)); } }

        [Browsable(false)]
        public MistweaverHealSettings MistInstance { get { return _mistinstance ?? (_mistinstance = new MistweaverHealSettings(HealingContext.Instances)); } }

        [Browsable(false)]
        public MistweaverHealSettings MistRaid { get { return _mistraid ?? (_mistraid = new MistweaverHealSettings(HealingContext.Raids)); } }

        [Browsable(false)]
        public MonkOffHealSettings OffhealBattleground { get { return _offhealbattleground ?? (_offhealbattleground = new MonkOffHealSettings(HealingContext.Battlegrounds)); } }

        [Browsable(false)]
        public MonkOffHealSettings OffhealPVE { get { return _offhealpve ?? (_offhealpve = new MonkOffHealSettings(HealingContext.Instances)); } }

        [Browsable(false)]
        public MistweaverHealSettings MistHealSettings { get { return MistHealSettingsLookup(Singular.SingularRoutine.CurrentWoWContext); } }

        public MistweaverHealSettings MistHealSettingsLookup(WoWContext ctx)
        {
            if (ctx == WoWContext.Instances)
                return StyxWoW.Me.GroupInfo.IsInRaid ? MistRaid : MistInstance;

            if (ctx == WoWContext.Battlegrounds)
                return MistBattleground;

            return MistInstance;
        }

        [Browsable(false)]
        public MonkOffHealSettings OffHealSettings { get { return OffHealSettingsLookup(Singular.SingularRoutine.CurrentWoWContext); } }

        public MonkOffHealSettings OffHealSettingsLookup(WoWContext ctx)
        {
            if (ctx == WoWContext.Battlegrounds)
                return OffhealBattleground;

            return OffhealPVE;
        }

        #endregion
    }

    internal class MistweaverHealSettings : Singular.Settings.HealerSettings
    {
        private MistweaverHealSettings()
            : base("", HealingContext.None)
        {
        }

        public MistweaverHealSettings(HealingContext ctx)
            : base("Mistweaver", ctx)
        {

            // we haven't created a settings file yet,
            //  ..  so initialize values for various heal contexts

            if (!SavedToFile)
            {
                if (ctx == Singular.HealingContext.Battlegrounds)
                {
                    HealFromMelee = false;
                }
                else if (ctx == Singular.HealingContext.Instances)
                {
                    HealFromMelee = true;
                }
                else if (ctx == Singular.HealingContext.Raids)
                {
                    HealFromMelee = true;
                }
                // omit case for WoWContext.Normal and let it use DefaultValue() values
            }

            SavedToFile = true;
        }

        [Setting]
        [Browsable(false)]
        [DefaultValue(false)]
        public bool SavedToFile { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Movement")]
        [DisplayName("Heal from Melee")]
        [Description("true: move into Melee range, false: stay at Range")]
        public bool HealFromMelee { get; set; }

        [Setting]
        [DefaultValue(99)]
        [Category("Health %")]
        [DisplayName("% Renewing Mist")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int RenewingMist { get; set; }

        [Setting]
        [DefaultValue(93)]
        [Category("Health %")]
        [DisplayName("% Soothing Mist")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int SoothingMist { get; set; }

        [Setting]
        [DefaultValue(80)]
        [Category("Health %")]
        [DisplayName("% Enveloping Mist")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int EnvelopingMist { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Health %")]
        [DisplayName("% Surging Mist")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int SurgingMist { get; set; }

        [Setting]
        [DefaultValue(50)]
        [Category("Health %")]
        [DisplayName("% Life Cocoon")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int LifeCocoon { get; set; }

        [Setting]
        [DefaultValue(51)]
        [Category("Health %")]
        [DisplayName("% Healing Sphere")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int HealingSphere { get; set; }

        [Setting]
        [DefaultValue(35)]
        [Category("Health %")]
        [DisplayName("% Thunder Focus Heal Individual")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int ThunderFocusHealSingle { get; set; }

        [Setting]
        [DefaultValue(4)]
        [Category("Target Minimum")]
        [DisplayName("Roll Renewing Mist Count")]
        [Description("Min number of players to keep Renewing Mist on (always on tanks, and tanks are included in count)")]
        public int RollRenewingMistCount { get; set; }

        [Setting]
        [DefaultValue(70)]
        [Category("Health %")]
        [DisplayName("% Thunder Focus Heal Group")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int ThunderFocusHealGroup { get; set; }

        [Setting]
        [DefaultValue(4)]
        [Category("Target Minimum")]
        [DisplayName("Count Thunder Focus Heal")]
        [Description("Min number of players healed")]
        public int CountThunderFocusHealGroup { get; set; }

        [Setting]
        [DefaultValue(85)]
        [Category("Health %")]
        [DisplayName("% Uplift Heal Group")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int UpliftGroup { get; set; }

        [Setting]
        [DefaultValue(3)]
        [Category("Target Minimum")]
        [DisplayName("Count Uplift Heal")]
        [Description("Min number of players healed")]
        public int CountUpliftGroup { get; set; }

        [Setting]
        [DefaultValue(95)]
        [Category("Health %")]
        [DisplayName("% Spinning Crane Kick Heal Group")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int SpinningCraneKickGroup { get; set; }

        [Setting]
        [DefaultValue(4)]
        [Category("Target Minimum")]
        [DisplayName("Count Spinning Crane Kick Heal")]
        [Description("Min number of players healed")]
        public int CountSpinningCraneKickGroup { get; set; }

        [Setting]
        [DefaultValue(65)]
        [Category("Health %")]
        [DisplayName("% Revival")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int Revival { get; set; }

        [Setting]
        [DefaultValue(4)]
        [Category("Target Minimum")]
        [DisplayName("Count Revival")]
        [Description("Min number of players healed")]
        public int CountRevival { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Health %")]
        [DisplayName("% Zen Sphere Talent")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int ZenSphereTalent { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Health %")]
        [DisplayName("% Chi Wave Talent")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int ChiWave { get; set; }

        [Setting]
        [DefaultValue(1)]
        [Category("Target Minimum")]
        [DisplayName("Count Chi Wave Talent")]
        [Description("Min number of players healed")]
        public int CountChiWave { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Health %")]
        [DisplayName("% Chi Burst Talent")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int ChiBurstTalent { get; set; }

        [Setting]
        [DefaultValue(1)]
        [Category("Target Minimum")]
        [DisplayName("Count Chi Burst Talent")]
        [Description("Min number of players healed")]
        public int CountChiBurstTalent { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Health %")]
        [DisplayName("% Zen Meditation")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int ZenMeditation { get; set; }

        [Setting]
        [DefaultValue(1)]
        [Category("Target Minimum")]
        [DisplayName("Count Zen Meditation")]
        [Description("Min number of players protected")]
        public int CountZenMeditation { get; set; }

    }

    internal class MonkOffHealSettings : Singular.Settings.HealerSettings
    {
        private MonkOffHealSettings()
            : base("", HealingContext.None)
        {
        }

        public MonkOffHealSettings(HealingContext ctx)
            : base("MonkOffheal", ctx)
        {

            // we haven't created a settings file yet,
            //  ..  so initialize values for various heal contexts

            if (!SavedToFile)
            {
                if (ctx == Singular.HealingContext.Battlegrounds)
                {
                }
                else // use group/companion healing
                {
                }
            }

            SavedToFile = true;
        }

        [Setting]
        [Browsable(false)]
        [DefaultValue(false)]
        public bool SavedToFile { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Health %")]
        [DisplayName("% Chi Wave Talent")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int ChiWaveTalent { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Health %")]
        [DisplayName("% Chi Burst Talent")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int ChiBurstTalent { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Health %")]
        [DisplayName("% Zen Sphere Talent")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int ZenSphereTalent { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Health %")]
        [DisplayName("% Zen Sphere Talent")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int HealingSphere { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Health %")]
        [DisplayName("% Zen Meditation")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int ZenMeditation { get; set; }

        [Setting]
        [DefaultValue(1)]
        [Category("Target Minimum")]
        [DisplayName("Count Chi Wave Talent")]
        [Description("Min number of players healed")]
        public int CountChiWaveTalent { get; set; }

        [Setting]
        [DefaultValue(1)]
        [Category("Target Minimum")]
        [DisplayName("Count Chi Burst Talent")]
        [Description("Min number of players healed")]
        public int CountChiBurstTalent { get; set; }      

        [Setting]
        [DefaultValue(1)]
        [Category("Target Minimum")]
        [DisplayName("Count Zen Meditation")]
        [Description("Min number of players protected")]
        public int CountZenMeditation { get; set; }

    }
}