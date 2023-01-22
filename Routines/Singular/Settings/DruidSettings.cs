
using System;
using System.ComponentModel;
using System.IO;
using Styx.Helpers;

using DefaultValue = Styx.Helpers.DefaultValueAttribute;
using System.Drawing;

namespace Singular.Settings
{
    public enum FeralForm
    {
        None,
        Bear,
        Cat
    }

    internal class DruidSettings : Styx.Helpers.Settings
    {
        public DruidSettings()
            : base(Path.Combine(SingularSettings.SingularSettingsPath, "Druid.xml"))
        {
        }

        #region Context Late Loading Wrappers

        private DruidHealSettings _battleground;
        private DruidHealSettings _instance;
        private DruidHealSettings _raid;
        private DruidHealSettings _normal;

        [Browsable(false)]
        public DruidHealSettings Battleground { get { return _battleground ?? (_battleground = new DruidHealSettings(HealingContext.Battlegrounds)); } }

        [Browsable(false)]
        public DruidHealSettings Instance { get { return _instance ?? (_instance = new DruidHealSettings(HealingContext.Instances)); } }

        [Browsable(false)]
        public DruidHealSettings Raid { get { return _raid ?? (_raid = new DruidHealSettings(HealingContext.Raids)); } }

        [Browsable(false)]
        public DruidHealSettings Normal { get { return _normal ?? (_normal = new DruidHealSettings(HealingContext.Normal)); } }

        [Browsable(false)]
        public DruidHealSettings Heal { get { return HealLookup(Singular.SingularRoutine.CurrentWoWContext); } }

        public DruidHealSettings HealLookup(WoWContext ctx)
        {
            if (ctx == WoWContext.Battlegrounds)
                return Battleground;
            if (ctx == WoWContext.Instances)
                return Styx.StyxWoW.Me.CurrentMap.IsRaid ? Raid : Instance;
            return Normal;
        }

        #endregion


        #region pvp
        /*
        [Setting]
        [DefaultValue(3)]
        [Category("Feral PvP")]
        [DisplayName("PvP Add Switch")]
        [Description("Switch to bear when the amount of attackers is equal or greater than this value")]
        public int PvPAddSwitch { get; set; }

        [Setting]
        [DefaultValue(40)]
        [Category("Feral PvP")]
        [DisplayName("PvP Health Switch")]
        [Description("Switch to bear when health drops below this value")]
        public int PvPHealthSwitch { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Feral PvP")]
        [DisplayName("Berserk Save")]
        [Description("Only use Berserk when there are 2 or more attackers")]
        public bool PvPBerserksafe { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Feral PvP")]
        [DisplayName("Shift out of snares")]
        [Description("Cancels Catform to remove snares")]
        public bool PvPSnared { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Feral PvP")]
        [DisplayName("Remove root")]
        [Description("Uses Dash/Stampeding Roar to remove root")]
        public bool PvPRooted { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Feral PvP")]
        [DisplayName("Cyclone adds")]
        [Description("Use Cyclone on adds")]
        public bool PvPccAdd { get; set; }

        [Setting]
        [DefaultValue(40)]
        [Category("Feral PvP")]
        [DisplayName("Rejuv Health")]
        [Description("Rejuv will be used when your health drops below this value")]
        public int PvPReju { get; set; }

        [Setting]
        [DefaultValue(20)]
        [Category("Feral PvP")]
        [DisplayName("HealingTouch Health")]
        [Description("Healing Touch will be used when your health drops below this value")]
        public int PvPHealingTouch { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Feral PvP")]
        [DisplayName("Predator's Swiftness heal")]
        [Description("Predator's Swiftness will be used when your health drops below this value")]
        public int PvPProcc { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Feral PvP")]
        [DisplayName("In combat healing")]
        public bool PvPpHealBool { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Feral PvP")]
        [DisplayName("Use Nature's Grasp")]
        [Description("Use Nature's Grasp when there are 2+ attackers")]
        public bool PvPGrasp { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Feral PvP")]
        [DisplayName("Use CC on fleeing target")]
        public bool PvPRoot { get; set; }
        */

        /* Logic for this needs work
        [Setting]
        [DefaultValue(true)]
        [Category("Feral PvP")]
        [DisplayName("Use Stealth when roaming")]
        //[Description("Use Stealth in PvP")]
        public bool PvPStealth { get; set; }
        */

        #endregion
  
        #region Common

        [Setting]
        [DefaultValue(false)]
        [Category("Common")]
        [DisplayName("Use Aquatic Form")]
        [Description("Cast Aquatic Form for faster movement while Swimming")]
        public bool UseAquaticForm { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Use Travel Form")]
        [Description("Cast Travel Form (or Cat Form) for faster movement while running on foot")]
        public bool UseTravelForm { get; set; }

        [Setting]
        [DefaultValue(40)]
        [Category("Common")]
        [DisplayName("Innervate Mana")]
        [Description("Innervate will be used when your mana drops below this value")]
        public int InnervateMana { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Enable Symbiosis")]
        [Description("True: will cast Symbiosis upon group member enabling special abilities")]
        public bool UseSymbiosis { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Common")]
        [DisplayName("Use Wild Charge")]
        [Description("Use Wild Charge as appropriate for spec")]
        public bool UseWildCharge { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Common")]
        [DisplayName("Move Behind Targets")]
        [Description("Move behind targets for opener or when target stunned/not targeting Druid in Cat Form")]
        public bool MoveBehindTargets { get; set; }

        [Setting]
        [DefaultValue(1)]
        [Category("Common")]
        [DisplayName("Disorienting Roar Count")]
        [Description("Number of Mobs required before casting Disorienting Roar when Solo/PVP")]
        public int DisorientingRoarCount { get; set; }

        [Setting]
        [DefaultValue(25)]
        [Category("Common")]
        [DisplayName("Disorienting Roar Health %")]
        [Description("Health % to cast Disorienting Roar to peel off mobs prior to healing when Solo/PVP")]
        public int DisorientingRoarHealth { get; set; } 



/*
                        [Setting]
                        [DefaultValue(false)]
                        [Category("Common")]
                        [DisplayName("Disable Healing for Balance and Feral")]
                        public bool NoHealBalanceAndFeral { get; set; }

                        [Setting]
                        [DefaultValue(20)]
                        [Category("Common")]
                        [DisplayName("Healing Touch Health (Balance and Feral)")]
                        [Description("Healing Touch will be used at this value.")]
                        public int NonRestoHealingTouch { get; set; }

                */

        [Setting]
        [DefaultValue(60)]
        [Category("Self Healing")]
        [DisplayName("Rejuvenation")]
        [Description("Health Percent to cast for self-heal when Solo")]
        public int SelfRejuvenationHealth { get; set; }

        [Setting]
        [DefaultValue(40)]
        [Category("Self Healing")]
        [DisplayName("Healing Touch")]
        [Description("Health Percent to cast for self-heal when Solo")]
        public int SelfHealingTouchHealth { get; set; }

        [Setting]
        [DefaultValue(35)]
        [Category("Self Healing")]
        [DisplayName("Renewal")]
        [Description("Health Percent to cast for self-heal when Solo")]
        public int SelfRenewalHealth { get; set; }

        [Setting]
        [DefaultValue(20)]
        [Category("Self Healing")]
        [DisplayName("Nature's Swiftness")]
        [Description("Health Percent to cast for self-heal when Solo")]
        public int SelfNaturesSwiftnessHealth { get; set; } 

        [Setting]
        [DefaultValue(80)]
        [Category("Self Healing")]
        [DisplayName("Cenarion Ward")]
        [Description("Health Percent to cast for self-heal when Solo")]
        public int SelfCenarionWardHealth { get; set; }

        #endregion

        #region Balance

        [Setting]
        [DefaultValue(80)]
        [Category("Balance")]
        [DisplayName("Moon Beast Rejuvenation")]
        [Description("Health Percent to cast for Moon Beast-heal when Solo")]
        public int MoonBeastRejuvenationHealth { get; set; }

        [Setting]
        [DefaultValue(40)]
        [Category("Balance")]
        [DisplayName("Moon Beast Healing Touch")]
        [Description("Health Percent to cast for Moon Beast-heal when Solo")]
        public int MoonBeastHealingTouch { get; set; }


        #endregion

        #region Resto

        [Setting]
        [DefaultValue(60)]
        [Category("Restoration")]
        [DisplayName("Tranquility Health")]
        [Description("Tranquility will be used at this value")]
        public int TranquilityHealth { get; set; }

        [Setting]
        [DefaultValue(3)]
        [Category("Restoration")]
        [DisplayName("Tranquility Count")]
        [Description("Tranquility will be used when count of party members whom health is below Tranquility health mets this value ")]
        public int TranquilityCount { get; set; }

        [Setting]
        [DefaultValue(65)]
        [Category("Restoration")]
        [DisplayName("Swiftmend Health")]
        [Description("Swiftmend will be used at this value")]
        public int Swiftmend { get; set; }

        [Setting]
        [DefaultValue(80)]
        [Category("Restoration")]
        [DisplayName("Wild Growth Health")]
        [Description("Wild Growth will be used at this value")]
        public int WildGrowthHealth { get; set; }

        [Setting]
        [DefaultValue(2)]
        [Category("Restoration")]
        [DisplayName("Wild Growth Count")]
        [Description("Wild Growth will be used when count of party members whom health is below Wild Growth health mets this value ")]
        public int WildGrowthCount { get; set; }

        [Setting]
        [DefaultValue(70)]
        [Category("Restoration")]
        [DisplayName("Regrowth Health")]
        [Description("Regrowth will be used at this value")]
        public int Regrowth { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Restoration")]
        [DisplayName("Healing Touch Health")]
        [Description("Healing Touch will be used at this value")]
        public int HealingTouch { get; set; }

        [Setting]
        [DefaultValue(75)]
        [Category("Restoration")]
        [DisplayName("Nourish Health")]
        [Description("Nourish will be used at this value")]
        public int Nourish { get; set; }

        [Setting]
        [DefaultValue(90)]
        [Category("Restoration")]
        [DisplayName("Rejuvenation Health")]
        [Description("Rejuvenation will be used at this value")]
        public int Rejuvenation { get; set; }

        [Setting]
        [DefaultValue(70)]
        [Category("Restoration")]
        [DisplayName("Barkskin Health")]
        [Description("Barkskin will be used at this value")]
        public int Barkskin { get; set; }

        #endregion


        #region Guardian
        [Setting]
        [DefaultValue(70)]
        [Category("Guardian")]
        [DisplayName("Frenzied Regeneration Health")]
        [Description("FR will be used at this value. Set this to 100 to enable on cooldown usage. (Recommended: 30 if glyphed. 15 if not.)")]
        public int TankFrenziedRegenerationHealth { get; set; }

        [Setting]
        [DefaultValue(100)]
        [Category("Guardian")]
        [DisplayName("Savage Defense Health")]
        [Description("Savage Defense will be used at this value. Set this to 100 to enable on cooldown usage.")]
        public int TankSavageDefense { get; set; }

        [Setting]
        [DefaultValue(30)]
        [Category("Guardian")]
        [DisplayName("Might of Ursoc Health")]
        [Description("Might of Ursoc will be used at this value. Set this to 100 to enable on cooldown usage.")]
        public int TankMightOfUrsoc { get; set; }

        [Setting]
        [DefaultValue(55)]
        [Category("Guardian")]
        [DisplayName("Survival Instincts Health")]
        [Description("SI will be used at this value. Set this to 100 to enable on cooldown usage. (Recommended: 55)")]
        public int TankSurvivalInstinctsHealth { get; set; }

        [Setting]
        [DefaultValue(80)]
        [Category("Guardian")]
        [DisplayName("Barkskin Health")]
        [Description("Barkskin will be used at this value. Set this to 100 to enable on cooldown usage.")]
        public int TankFeralBarkskin { get; set; }

        #endregion

        #region Feral

        public enum SpellPriority
        {
            Noxxic = 1,
            ElitistJerks = 3
        }


        [Setting]
        [DefaultValue(SpellPriority.ElitistJerks )]
        [Category("Feral")]
        [DisplayName("Instance Spell Priority")]
        public SpellPriority FeralSpellPriority { get; set; }


        [Setting]
        [DefaultValue(80)]
        [Category("Feral")]
        [DisplayName("Pred Swift Healing Touch")]
        [Description("Health Percent to cast for Predatory Swiftness heal when Solo")]
        public int PredSwiftnessHealingTouchHealth { get; set; }

        [Setting]
        [DefaultValue(35)]
        [Category("Feral")]
        [DisplayName("Pred Swift PVP Off-Heal")]
        [Description("Health Percent to cast for Predatory Swiftness heal on group member")]
        public int PredSwiftnessPvpHeal { get; set; }


/*
        [Setting]
        [DefaultValue(50)]
        [Category("Feral")]
        [DisplayName("Barkskin Health")]
        [Description("Barkskin will be used at this value. Set this to 100 to enable on cooldown usage.")]
        public int FeralBarkskin { get; set; }
*/

        [Setting]
        [DefaultValue(55)]
        [Category("Feral")]
        [DisplayName("Survival Instincts Health %")]
        [Description("SI will be used at this value. Set this to 100 to enable on cooldown usage. (Recommended: 55)")]
        public int SurvivalInstinctsHealth { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Feral")]
        [DisplayName("Prowl Always")]
        [Description("Prowl at all times out of combat. Does not disable mounting (you can in HB Settings if desired)")]
        public bool ProwlAlways { get; set; }

/*
                [Setting]
                [DefaultValue(30)]
                [Category("Feral PvP")]
                [DisplayName("Frenzied Regeneration Health")]
                [Description("FR will be used at this value. Set this to 100 to enable on cooldown usage. (Recommended: 30 if glyphed. 15 if not.)")]
                public int FrenziedRegenerationHealth { get; set; }

                [Setting]
                [DefaultValue(0)]
                [Category("Form Selection")]
                [DisplayName("Form Selection")]
                [Description("Form Selection!")]
                public int Shapeform { get; set; }

                [Setting]
                [DefaultValue(60)]
                [Category("Feral")]
                [DisplayName("Predator's Swiftness heal")]
                [Description("Healing with Predator's Swiftness will be used at this value")]
                public int NonRestoprocc { get; set; }

                [Setting]
                [DefaultValue(true)]
                [Category("Feral Raid / Instance")]
                [DisplayName("Cat - Disable Healing for Balance and Feral")]
                public bool RaidCatProwl { get; set; }

                [Setting]
                [DefaultValue(15)]
                [Category("Feral Raid / Instance")]
                [DisplayName("Cat - Predator's Swiftness (Balance and Feral)")]
                public int RaidCatProccHeal { get; set; }


                [Setting]
                [DefaultValue(true)]
                [Category("Feral Raid / Instance")]
                [DisplayName("Interrupt")]
                [Description("Automatically interrupt spells while in an instance if this value is set to true.")]
                public bool Interrupt { get; set; }

                [Setting]
                [DefaultValue(true)]
                [Category("Feral Raid / Instance")]
                [DisplayName("Cat - Warn if not behind boss")]
                public bool CatRaidWarning { get; set; }

                [Setting]
                [DefaultValue(true)]
                [Category("Feral Raid / Instance")]
                [DisplayName("Cat - Use dash as gap closer")]
                public bool CatRaidDash { get; set; }

                [Setting]
                [DefaultValue(true)]
                [Category("Feral Raid / Instance")]
                [DisplayName("Cat - Use Raid button ")]
                //[Description("If set to true, it will cast Berserk only if we got adds.")]
                public bool CatRaidButtons { get; set; }

                [Setting]
                [DefaultValue(true)]
                [Category("Feral Raid / Instance")]
                [DisplayName("Cat - Use Stampeding Roar")]
                [Description("If set to true, it will cast Stampeding Roar to close gap to target.")]
                public bool CatRaidStampeding { get; set; }
        


                [Setting]
                [DefaultValue(true)]
                [Category("Feral PvP")]
                [DisplayName("Cat - Stealth Pull")]
                [Description("Always try to pull while in stealth. If disabled it pulls with FFF instead.")]
                public bool CatNormalPullStealth { get; set; }


                [Setting]
                [DefaultValue(4)]
                [Category("Feral Raid / Instance")]
                [DisplayName("Cat - Adds to AOE")]
                [Description("Number of adds needed to start Aoe rotation.")]
                public int CatRaidAoe { get; set; }

                [Setting]
                [DefaultValue(true)]
                [Category("Feral Raid / Instance")]
                [DisplayName("Cat - Auto Berserk")]
                [Description("If set to true, it will cast Berserk automatically to do max dps.")]
                public bool CatRaidBerserk { get; set; }

                [Setting]
                [DefaultValue(true)]
                [Category("Feral Raid / Instance")]
                [DisplayName("Cat - Auto Tiger's Fury")]
                [Description("If set to true, it will cast Tiger's Fury automatically to do max dps.")]
                public bool CatRaidTigers { get; set; }

                [Setting]
                [DefaultValue(false)]
                [Category("Feral Raid / Instance")]
                [DisplayName("Cat - Rebuff infight")]
                [Description("If set to true, it will rebuff Mark of the Wild infight.")]
                public bool CatRaidRebuff { get; set; }

                [Setting]
                [DefaultValue(true)]
                [Category("Feral Raid / Instance")]
                [DisplayName("Cat - Feral Charge")]
                [Description("Use Feral Charge to close gaps. It should handle bosses where charge is not" +
                             "possible || best solution automatically.")]
                public bool CatRaidUseFeralCharge { get; set; }

                [Setting]
                [DefaultValue(true)]
                [Category("Feral Raid / Instance")]
                [DisplayName("Bear - Feral Charge")]
                [Description("Use Feral Charge to close gaps. It should handle bosses where charge is not" +
                             "possible || best solution automatically.")]
                public bool BearRaidUseFeralCharge { get; set; }

                [Setting]
                [DefaultValue(2)]
                [Category("Feral Raid / Instance")]
                [DisplayName("Bear - Adds to AOE")]
                [Description("Number of adds needed to start Aoe rotation.")]
                public int BearRaidAoe { get; set; }

                [Setting]
                [DefaultValue(true)]
                [Category("Feral Raid / Instance")]
                [DisplayName("Auto Berserk")]
                [Description("If set to true, it will cast Berserk automatically to do max threat.")]
                public bool BearRaidBerserk { get; set; }

                [Setting]
                [DefaultValue(false)]
                [Category("Feral Raid / Instance")]
                [DisplayName("Bear - Berserk Burst")]
                [Description("If set to true, it will SPAM MANGLE FOR GODS SAKE while Berserk is active.")]
                public bool BearRaidBerserkFun { get; set; }

                [Setting]
                [DefaultValue(true)]
                [Category("Feral Raid / Instance")]
                [DisplayName("Bear - Auto defensive cooldowns")]
                [Description("If set to true, it will cast defensive cooldowns automatically.")]
                public bool BearRaidCooldown { get; set; }
        */
        // End of IloveDruids

        #endregion
    }


    internal class DruidHealSettings : Singular.Settings.HealerSettings
    {
        private DruidHealSettings()
            : base("", HealingContext.None)
        {
        }

        public DruidHealSettings(HealingContext ctx)
            : base("Druid", ctx)
        {

            // we haven't created a settings file yet,
            //  ..  so initialize values for various heal contexts

            if (!SavedToFile)
            {
                if (ctx == Singular.HealingContext.Battlegrounds)
                {
                    Rejuvenation = 95;
                    Nourish = 0;
                    HealingTouch = 0;
                    Regrowth = 75;
                    WildGrowth = 90;
                    CountWildGrowth = 3;
                    SwiftmendAOE = 0;
                    CountSwiftmendAOE = 0;
                    SwiftmendDirectHeal = 74;
                    WildMushroomBloom = 60;
                    CountMushroomBloom = 1;
                    Tranquility = 0;
                    CountTranquility = 0;
                    TreeOfLife = 60;
                    CountTreeOfLife = 1;
                    Ironbark = 59;
                    NaturesSwiftness = 35;
                    CenarionWard = 80;
                    NaturesVigil = 60;
                }
                else if (ctx == Singular.HealingContext.Instances)
                {
                    Rejuvenation = 95;
                    Nourish = 85;
                    HealingTouch = 70;
                    Regrowth = 40;
                    WildGrowth = 85;
                    CountWildGrowth = 3;
                    SwiftmendAOE = 0;
                    CountSwiftmendAOE = 0;
                    SwiftmendDirectHeal = 70;
                    WildMushroomBloom = 85;
                    CountMushroomBloom = 2;
                    Tranquility = 60;
                    CountTranquility = 3;
                    TreeOfLife = 70;
                    CountTreeOfLife = 3;
                    Ironbark = 60;
                    NaturesSwiftness = 25;
                    CenarionWard = 80;
                    NaturesVigil = 70;
                }
                else if (ctx == Singular.HealingContext.Raids)
                {
                    Rejuvenation = 94;
                    Nourish = 95;
                    HealingTouch = 60;
                    Regrowth = 40;
                    WildGrowth = 90;
                    CountWildGrowth = 4;
                    SwiftmendAOE = 0;
                    CountSwiftmendAOE = 0;
                    SwiftmendDirectHeal = 85;
                    WildMushroomBloom = 95;
                    CountMushroomBloom = 3;
                    Tranquility = 70;
                    CountTranquility = 5;
                    TreeOfLife = 75;
                    CountTreeOfLife = 4;
                    Ironbark = 65;
                    NaturesSwiftness = 25;
                    CenarionWard = 85;
                    NaturesVigil = 80;
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
        [DefaultValue(70)]
        [Category("Restoration")]
        [DisplayName("% Rejuvenation")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int Rejuvenation { get; set; }

        [Setting]
        [DefaultValue(70)]
        [Category("Restoration")]
        [DisplayName("% Nourish")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int Nourish { get; set; }

        [Setting]
        [DefaultValue(70)]
        [Category("Restoration")]
        [DisplayName("% Healing Touch")]
        [Description("Health % to cast this ability at. Set to 0 to disable. Overridden by Regrowth if Glyphed")]
        public int HealingTouch { get; set; }

        [Setting]
        [DefaultValue(70)]
        [Category("Restoration")]
        [DisplayName("% Regrowth")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int Regrowth { get; set; }

        [Setting]
        [DefaultValue(92)]
        [Category("Restoration")]
        [DisplayName("% Wild Growth")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int WildGrowth { get; set; }

        [Setting]
        [DefaultValue(4)]
        [Category("Restoration")]
        [DisplayName("Wild Growth Min Count")]
        [Description("Min number of players below Healing Rain % in area")]
        public int CountWildGrowth { get; set; }

        [Setting]
        [DefaultValue(70)]
        [Category("Restoration")]
        [DisplayName("% Swiftmend (AOE)")]
        [Description("Health % to cast this ability at based upon minimum player count. Set to 0 to disable.")]
        public int SwiftmendAOE { get; set; }

        [Setting]
        [DefaultValue(3)]
        [Category("Restoration")]
        [DisplayName("Swiftmend (AOE) Min Count")]
        [Description("Min number of players healed")]
        public int CountSwiftmendAOE { get; set; }

        [Setting]
        [DefaultValue(70)]
        [Category("Restoration")]
        [DisplayName("% Swiftmend (Direct Heal)")]
        [Description("Health % to cast this ability at based upon single player health. Set to 0 to disable.")]
        public int SwiftmendDirectHeal { get; set; }

        [Setting]
        [DefaultValue(70)]
        [Category("Restoration")]
        [DisplayName("% Wild Mushroom: Bloom")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int WildMushroomBloom { get; set; }

        [Setting]
        [DefaultValue(3)]
        [Category("Restoration")]
        [DisplayName("Mushroom: Bloom Min Count")]
        [Description("Min number of players below Mushroom: Bloom % in area")]
        public int CountMushroomBloom { get; set; }

        [Setting]
        [DefaultValue(91)]
        [Category("Restoration")]
        [DisplayName("% Tranquility")]
        [Description("Health % to cast this ability at. Must heal Min of 3 people in party, 4 in a raid. Set to 0 to disable.")]
        public int Tranquility { get; set; }

        [Setting]
        [DefaultValue(4)]
        [Category("Restoration")]
        [DisplayName("Tranquility Min Count")]
        [Description("Min number of players below Healing Rain % in area")]
        public int CountTranquility { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Restoration")]
        [DisplayName("% Tree of Life Form")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int TreeOfLife { get; set; }

        [Setting]
        [DefaultValue(3)]
        [Category("Restoration")]
        [DisplayName("Tree of Life Min Count")]
        [Description("Min number of players below Tree of Life % in area")]
        public int CountTreeOfLife { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Restoration")]
        [DisplayName("% Genesis")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int Genesis { get; set; }

        [Setting]
        [DefaultValue(3)]
        [Category("Restoration")]
        [DisplayName("Genesis Min Count")]
        [Description("Min number of players with Rejuvenation below % in area")]
        public int CountGenesis { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Restoration")]
        [DisplayName("% Ironbark")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int Ironbark { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Restoration")]
        [DisplayName("% Nature's Swiftness")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int NaturesSwiftness { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Restoration")]
        [DisplayName("% Cenarion Ward")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int CenarionWard { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Restoration")]
        [DisplayName("% Nature's Vigil")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int NaturesVigil { get; set; }

        [Setting]
        [DefaultValue(60)]
        [Category("Restoration")]
        [DisplayName("% Heart of the Wild")]
        [Description("Health % to cast this ability at. Set to 0 to disable.")]
        public int HeartOfTheWild { get; set; }

        [Setting]
        [DefaultValue(5)]
        [Category("Restoration")]
        [DisplayName("Heart of the Wild Min Count")]
        [Description("Minimum number of players below Heart of the Wild % to cast this ability.")]
        public int CountHeartOfTheWild { get; set; }

        [Setting]
        [DefaultValue(0)]
        [Category("Restoration")]
        [DisplayName("Dream of Cenarius Only Above %")]
        [Description("Only Dream of Cenarius Healing done above this Health %")]
        public int DreamOfCenariusAbovePercent { get; set; }

        [Setting]
        [DefaultValue(1)]
        [Category("Restoration")]
        [DisplayName("Dream of Cenarius Only Count")]
        [Description("Count of Heal Targets below Dream of Cenarius Only % which allows other heal spells")]
        public int DreamOfCenariusAboveCount { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Restoration")]
        [DisplayName("Dream of Cenarius When Idle")]
        [Description("True: DPS with Dream of Cenarius spells if no healing needed")]
        public bool DreamOfCenariusWhenIdle { get; set; }

    }

}