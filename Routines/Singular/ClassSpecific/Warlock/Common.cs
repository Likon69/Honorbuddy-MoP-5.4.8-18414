using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using CommonBehaviors.Actions;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using Styx.CommonBot.POI;
using System.Drawing;

namespace Singular.ClassSpecific.Warlock
{
    public class Common
    {
        #region Local Helpers

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WarlockSettings WarlockSettings { get { return SingularSettings.Instance.Warlock(); } }

        public static bool HaveHealthStone { get { return StyxWoW.Me.BagItems.Any(i => i.Entry == 5512); } }

        #endregion

        #region Talents

        public static bool HasTalent(WarlockTalents tal)
        {
            return TalentManager.IsSelected((int)tal);
        }

        #endregion

        #region Rest

        [Behavior(BehaviorType.Rest, WoWClass.Warlock)]
        public static Composite CreateWarlockRest()
        {
            return new PrioritySelector(

                new Decorator(
                    ret => TalentManager.CurrentSpec == WoWSpec.WarlockDemonology && Me.HasAura("Metamorphosis") && Demonology.demonFormRestTimer.IsFinished,
                    new Sequence(
                        new Action(ret => Logger.Write(Color.White, "^Cancel Metamorphosis at Rest")),
                        new Action(ret => Me.CancelAura("Metamorphosis")),
                        new WaitContinue(TimeSpan.FromMilliseconds(450), canRun => !Me.HasAura("Metamorphosis"), new ActionAlwaysSucceed())
                        )
                    ),

                new Decorator(
                    ret => TalentManager.CurrentSpec == WoWSpec.WarlockAffliction && Me.HasAura("Soulburn"),
                    new Sequence(
                        new Action(ret => Logger.Write(Color.White, "^Cancel Soulburn at Rest")),
                        new Action(ret => Me.CancelAura("Soulburn")),
                        new WaitContinue(TimeSpan.FromMilliseconds(450), canRun => !Me.HasAura("Soulburn"), new ActionAlwaysSucceed())
                        )
                    ),

                //-- move following to top of root
                //new Decorator(ctx => SingularSettings.Instance.DisablePetUsage && Me.GotAlivePet,
                //    new Action(ctx => Lua.DoString("PetDismiss()"))),
                new Decorator(
                    req => !Me.HasAura("Drink") && !Me.HasAura("Food") && !Me.Mounted,
                    new PrioritySelector(
                        // new ThrottlePasses( 5, new Action( r => { Logger.Write( "in Rest()"); return RunStatus.Failure; } )),
                        new Sequence(
                            Spell.BuffSelf("Life Tap", ret => Me.ManaPercent < 80 && Me.HealthPercent > 60),
                            Helpers.Common.CreateWaitForLagDuration()
                            ),

                        Common.CreateWarlockSummonPet()
                        )
                    ),

                Rest.CreateDefaultRestBehaviour(),

                Common.CreatWarlockHealthFunnelBehavior( WarlockSettings.HealthFunnelRest )
                );
        }

        #endregion

        #region PreCombatBuffs

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Warlock)]
        public static Composite CreateWarlockPreCombatBuffs()
        {
            return new PrioritySelector(
                Spell.WaitForCastOrChannel(),
                Spell.WaitForGlobalCooldown(),

                //new ThrottlePasses(5, new Action(r => { Logger.Write("in PreCombatBuff()"); return RunStatus.Failure; })),
                CreateWarlockSummonPet(),
                Spell.BuffSelf("Soul Link", ret => !Me.HasAura("Soul Link") && Me.GotAlivePet && PetManager.PetSummonAfterDismountTimer.IsFinished ),

                new PrioritySelector(
                    new Decorator(
                        ctx => ShouldCreateSoulwell,
                        new Sequence(
                            new DecoratorContinue(
                                ctx => StyxWoW.Me.IsMoving,
                                new Sequence(
                                    new Action(ctx => StopMoving.Now()),
                                    new WaitContinue(2, ctx => !StyxWoW.Me.IsMoving, new ActionAlwaysSucceed())
                                    )
                                ),
                            new Action(r => Logger.WriteDebug("Soulwell: make sure not casting")),
                            new WaitContinue(2, ctx => !Spell.IsCastingOrChannelling(LagTolerance.No), new ActionAlwaysSucceed()),
                            new Action(ctx => Logger.Write(Color.White, "^Create Soulwell")),
                            new Action(ctx => SpellManager.Cast("Create Soulwell")),
                            new Action(r => Logger.WriteDebug("Soulwell: wait until it shows we are casting")),
                            new WaitContinue(2, ctx => Spell.IsCastingOrChannelling(), new ActionAlwaysSucceed()),
                            new Action(r => Logger.WriteDebug("Soulwell: now wait for it to stop casting")),
                            new WaitContinue(10, ctx => !Spell.IsCastingOrChannelling(LagTolerance.No), new ActionAlwaysSucceed()),
                            new Action(r => Logger.WriteDebug("Soulwell: cast to creat completed")),
                            new Wait(2, ctx => Soulwell != null, new ActionAlwaysSucceed()),
                            new Sequence(
                                ctx => Soulwell,
                                new Action(r => Logger.WriteDebug("Soulwell: found it exists @ {0:F1} yds", (r as WoWGameObject).Distance)),
                                new Wait(5, until => (until as WoWGameObject).CanUseNow(), new ActionAlwaysSucceed()),
                                new Action(r => Logger.WriteDebug("Soulwell: is ready for use")),
                                new Decorator(
                                    req => (req as WoWGameObject).Distance < 1,
                                    new Sequence(
                                        new Action( r => {
                                            WoWGameObject obj = r as WoWGameObject;
                                            const int StrafeTime = 250;
                                            WoWMovement.MovementDirection strafe = (((int)DateTime.Now.Second) & 1) == 0 ? WoWMovement.MovementDirection.StrafeLeft : WoWMovement.MovementDirection.StrafeRight;
                                            Logger.Write( Color.White, "Soulwell {0} for {1} ms since too close to Soulwell @ {2:F2} yds", strafe, StrafeTime, obj.Distance);
                                            WoWMovement.Move(strafe, TimeSpan.FromMilliseconds(StrafeTime));
                                        })
                                        )
                                    )
                                )
                            )
                        )
                    ),

                new Decorator(
                    req => !HaveHealthStone && !ShouldCreateSoulwell,
                    new Throttle(5, Spell.Cast("Create Healthstone", mov => true, on => Me, ret => !Unit.NearbyUnfriendlyUnits.Any(u => u.Distance < 25), cancel => false))
                    ),

                Spell.BuffSelf("Soulstone", ret => NeedToSoulstoneMyself()),
                PartyBuff.BuffGroup("Dark Intent"),
                Spell.BuffSelf( "Grimoire of Sacrifice", ret => GetCurrentPet() != WarlockPet.None && GetCurrentPet() != WarlockPet.Other),
                Spell.BuffSelf( "Unending Breath", req => Me.IsSwimming )
                );
        }


        private static bool NeedToSoulstoneMyself()
        {
            bool cast = WarlockSettings.UseSoulstone == Soulstone.Self 
                || (WarlockSettings.UseSoulstone == Soulstone.Auto && SingularRoutine.CurrentWoWContext != WoWContext.Instances && MovementManager.IsClassMovementAllowed );
            return cast;
        }

        #endregion

        #region Loss of control

        [Behavior(BehaviorType.LossOfControl, WoWClass.Warlock)]
        public static Composite CreateWarlockLossOfControlBehavior()
        {
            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(
                    Spell.BuffSelf("Unbound Will")
                    )
                );
        }
        

        #endregion

        #region CombatBuffs

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Warlock)]
        public static Composite CreateWarlockCombatBuffs()
        {
            return new PrioritySelector(

                // Symbiosis
                Spell.Cast("Rejuvenation", on => Me, ret => Me.HasAuraExpired("Rejuvenation", 1) && Me.HealthPercent < 95),

                // won't live long with no Pet, so try to summon
                new Decorator(
                    ret => GetCurrentPet() == WarlockPet.None && GetBestPet() != WarlockPet.None,
                    CreateWarlockSummonPet()
                    ),

                new Decorator(
                    req => !Unit.IsTrivial( Me.CurrentTarget),
                    new PrioritySelector(
                        // 
                        Spell.BuffSelf("Twilight Ward", ret => NeedTwilightWard ),

                        // 
                        Spell.BuffSelf( "Ember Tap", ret => Me.HealthPercent < 80),

                        // need combat healing?  check here since mix of buffs and abilities
                        // heal / shield self as needed
                        Spell.BuffSelf("Dark Regeneration", ret => Me.HealthPercent < 45),
                        new Decorator(
                            ret => StyxWoW.Me.HealthPercent < 60 || Me.HasAura("Dark Regeneration"),
                            new PrioritySelector(
                                ctx => Item.FindFirstUsableItemBySpell("Healthstone", "Healing Potion", "Life Spirit"),
                                new Decorator(
                                    ret => ret != null,
                                    new Sequence(
                                        new Action(ret => Logger.Write(String.Format("Using {0}", ((WoWItem)ret).Name))),
                                        new Action(ret => ((WoWItem)ret).UseContainerItem()),
                                        Helpers.Common.CreateWaitForLagDuration())
                                    )
                                )
                            ),


                        new Decorator( 
                            ret => Me.IsInGroup() && WarlockSettings.UseSoulstone != Soulstone.None && WarlockSettings.UseSoulstone != Soulstone.Self,
                            Helpers.Common.CreateCombatRezBehavior("Soulstone", on => true, requirements => true)
                            ),


                        // remove our banish if they are our CurrentTarget 
                        new Throttle( 2, Spell.Cast("Banish", ret => {
                            bool isBanished = Me.CurrentTarget.HasMyAura( "Banish");
                            if (isBanished)
                                Logger.WriteDebug("Banish: attempting to remove from current target");
                            return isBanished;
                            })),
                            
                        // banish someone if they are not current target, attacking us, and 12 yds or more away
                        new PrioritySelector(
                            ctx => Unit.NearbyUnfriendlyUnits
                                .Where(
                                    u => (u.CreatureType == WoWCreatureType.Elemental || u.CreatureType == WoWCreatureType.Demon)
                                        && Me.CurrentTargetGuid != u.Guid
                                        && !u.IsBoss() 
                                        && (u.Aggro || u.PetAggro || (u.Combat && u.IsTargetingMeOrPet))
                                        && !u.IsCrowdControlled() && !u.HasAura("Banish")
                                        && SingularRoutine.CurrentWoWContext != WoWContext.Battlegrounds 
                                        && (!u.Elite || SingularRoutine.CurrentWoWContext == WoWContext.Instances)
                                        && u.Distance.Between(10, 30) && Me.IsSafelyFacing(u) && u.InLineOfSpellSight && (!Me.GotTarget || u.Location.Distance(Me.CurrentTarget.Location) > 10))
                                .OrderByDescending(u => u.Distance)
                                .FirstOrDefault(),
                            Spell.Cast("Banish", onUnit => (WoWUnit)onUnit)
                            ),


                        new Decorator(
                            ret => Unit.NearbyUnitsInCombatWithMeOrMyStuff.Any(u => u.IsWithinMeleeRange),
                            new PrioritySelector(
                                Spell.BuffSelf("Blood Horror", ret => Me.HealthPercent > 20),
                                Spell.BuffSelf("Whiplash"),
                                CreateVoidwalkerDisarm()
                                )
                            ),

                        new PrioritySelector(
                            // find an add within 8 yds (not our current target)
                            ctx => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => (u.Combat || Battlegrounds.IsInsideBattleground) && !u.IsStunned() && u.CurrentTargetGuid == Me.Guid && Me.CurrentTargetGuid != u.Guid && u.SpellDistance() < 8),

                            Spell.CastOnGround( "Shadowfury", on => (WoWUnit)on, ret => ret != null, true),

                            // treat as a heal, but we cast on what would be our fear target -- allow even when fear use disabled
                            Spell.Buff("Mortal Coil", on => (WoWUnit)on, ret => WarlockSettings.UseFear && !((WoWUnit)ret).IsUndead && Me.HealthPercent < 50),
                            Spell.Buff("Mortal Coil", on => Me.CurrentTarget, ret => WarlockSettings.UseFear && Me.GotTarget && !Me.CurrentTarget.IsUndead && Me.HealthPercent < 50 && Me.HealthPercent < Me.CurrentTarget.HealthPercent ),

                            // Howl of Terror if too m any mobs attacking us that arent' controlled
                            Spell.Buff("Howl of Terror", 
                                on => Me.CurrentTarget, 
                                ret => WarlockSettings.UseFear &&
                                5 <= Unit.NearbyUnfriendlyUnits.Count(u => (Battlegrounds.IsInsideBattleground || u.CurrentTargetGuid == Me.Guid) && !u.IsStunned() && u.SpellDistance() < 10f)),

                            // fear if situation dictates it
                            Spell.Buff("Fear", on => GetBestFearTarget())
                            ),

                        new Decorator(
                            ret => (Me.GotTarget && (Me.CurrentTarget.IsPlayer || Me.CurrentTarget.IsBoss() || Me.CurrentTarget.TimeToDeath() > 20)) || Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMyStuff()) >= 3,
                            new PrioritySelector(
                                Spell.BuffSelf("Dark S  oul: Misery", ret => TalentManager.CurrentSpec == WoWSpec.WarlockAffliction),
                                Spell.BuffSelf("Dark Soul: Instability", ret => TalentManager.CurrentSpec == WoWSpec.WarlockDestruction && Destruction.CurrentBurningEmbers >= 30),
                                Spell.BuffSelf("Dark Soul: Knowledge", ret => TalentManager.CurrentSpec == WoWSpec.WarlockDemonology && Me.GetCurrentPower(WoWPowerType.DemonicFury) > 800)
                                )
                            ),

                        Spell.Cast("Summon Doomguard", ret => Me.CurrentTarget.IsBoss() && PartyBuff.WeHaveBloodlust),

                        // lower threat if tanks nearby to pickup
                        Spell.BuffSelf("Soulshatter",
                            ret => SingularRoutine.CurrentWoWContext == WoWContext.Instances 
                                && Group.AnyTankNearby
                                && Unit.NearbyUnfriendlyUnits.Any(u => u.CurrentTargetGuid == Me.Guid)),

                        // lower threat if voidwalker nearby to pickup
                        Spell.BuffSelf("Soulshatter",
                            ret => SingularRoutine.CurrentWoWContext != WoWContext.Battlegrounds 
                                && !Group.AnyTankNearby 
                                && GetCurrentPet() == WarlockPet.Voidwalker 
                                && Unit.NearbyUnfriendlyUnits.Any(u => u.CurrentTargetGuid == Me.Guid)),

                        Spell.BuffSelf("Dark Bargain", ret => Me.HealthPercent < 50),
                        Spell.BuffSelf("Sacrificial Pact", ret => Me.HealthPercent < 60 && GetCurrentPet() != WarlockPet.None && GetCurrentPet() != WarlockPet.Other && Me.Pet.HealthPercent > 50),

                        new Decorator(
                            ret => Me.HealthPercent < WarlockSettings.DrainLifePercentage && !Group.AnyHealerNearby,
                            new Sequence(
                                new PrioritySelector(
                                    CreateCastSoulburn( ret => Spell.CanCastHack("Drain Life", Me.CurrentTarget)),
                                    new ActionAlwaysSucceed()
                                    ),
                                Spell.Cast("Drain Life")
                                )
                            ),

                        new Decorator(
                            ret => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) >= 3
                                || Me.CurrentTarget.IsBoss()
                                || Unit.NearbyUnfriendlyUnits.Any( u => u.IsPlayer && u.IsTargetingMeOrPet ),
                            new PrioritySelector(
                                Spell.BuffSelf("Dark Soul: Misery"),
                                Spell.BuffSelf("Unending Resolve"),
                                new Decorator(
                                    ret => HasTalent( WarlockTalents.GrimoireOfService),
                                    new PrioritySelector(
                                        Spell.Cast("Grimoire: Felhunter", ret => SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds),
                                        Spell.Cast("Grimoire: Voidwalker", ret => Common.GetCurrentPet() != WarlockPet.Voidwalker ),
                                        Spell.Cast("Grimoire: Felhunter", ret => Common.GetCurrentPet() != WarlockPet.Felhunter )
                                        )
                                    )
                                )
                            ),

                        Common.CreatWarlockHealthFunnelBehavior( WarlockSettings.HealthFunnelCast, WarlockSettings.HealthFunnelCancel),

                        Spell.BuffSelf("Life Tap",
                            ret => Me.ManaPercent < SingularSettings.Instance.PotionMana
                                && Me.HealthPercent > SingularSettings.Instance.PotionHealth),

                        PartyBuff.BuffGroup("Dark Intent"),

                        new Decorator(
                            ret => Me.GotTarget 
                                && Unit.ValidUnit(Me.CurrentTarget)
                                && (Me.CurrentTarget.IsPlayer || Me.CurrentTarget.TimeToDeath() > 45),
                            new Throttle( 2,
                                new PrioritySelector(
                                    Spell.Buff( "Curse of the Elements", true,
                                        ret => !Me.CurrentTarget.HasMyAura("Curse of Enfeeblement")
                                            && !Me.CurrentTarget.HasAuraWithEffect(WoWApplyAuraType.ModDamageTaken)),

                                    Spell.Buff("Curse of Enfeeblement", true,
                                        ret => !Me.CurrentTarget.HasMyAura("Curse of the Elements")
                                            && !Me.CurrentTarget.HasDemoralizing())
                                    )
                                )
                            ),

                        // mana restoration - match against survival cooldowns
                        new Decorator( 
                            ret => Me.ManaPercent < 60,
                            new PrioritySelector(
                                Spell.BuffSelf("Life Tap", ret => Me.HealthPercent > 50 && Me.HasAnyAura("Glyph of Healthstone")),
                                Spell.BuffSelf("Life Tap", ret => Me.HealthPercent > 50 && Me.HasAnyAura("Unending Resolve")),
                                Spell.BuffSelf("Life Tap", ret => Me.HasAnyAura("Sacrificial Pact")),
                                Spell.BuffSelf("Life Tap", ret => Me.HasAnyAura("Dark Bargain")),
                                Spell.BuffSelf("Life Tap", ret => Me.ManaPercent < 30 && Me.HealthPercent > 60)
                                )
                            )
                    
                        // , Spell.BuffSelf("Kil'jaeden's Cunning", ret => Me.IsMoving && Me.Combat)
                        )
                    )
                );
        }

        #endregion


        private static ulong _lastSapTarget = 0;



        public static WoWUnit GetBestFearTarget()
        {
            if (!WarlockSettings.UseFear)
                return null;

            if (!Me.GotTarget)
                return null;

            // use a larger range than normal 40 yds to check Fear because of mechanic causing them to run
            if (Unit.UnfriendlyUnits(80).Any(u => !u.IsUndead && u.HasMyAura("Fear")))
                return null;

            string msg = "";

            // check if a player is attacking us and Fear them first
            WoWUnit closestTarget = Unit.NearbyUnitsInCombatWithMe
                .Where(u => u.IsPlayer && !u.IsUndead)
                .OrderByDescending(u => u.HealthPercent)
                .FirstOrDefault();
            if (closestTarget != null)
            {
                msg = string.Format("^Fear: player {0} attacking us from {1:F1} yds", closestTarget.SafeName(), closestTarget.Distance);
            }
            // otherwise check if over Mob count setting
            else if (WarlockSettings.UseFearCount > 0 && Unit.NearbyUnitsInCombatWithMe.Count() >= WarlockSettings.UseFearCount)
            {
                closestTarget = Unit.NearbyUnitsInCombatWithMe
                    .Where(u => !u.IsUndead && u.Guid != (!Me.GotAlivePet ? 0 : Me.Pet.CurrentTargetGuid))
                    .OrderByDescending(u => u.IsPlayer)
                    .ThenBy(u => u.DistanceSqr)
                    .FirstOrDefault();

                if (closestTarget != null)
                {
                    msg = string.Format("^Fear: {0} @ {1:F1} yds from target to avoid aggro while hitting target", closestTarget.SafeName(), closestTarget.Location.Distance(Me.CurrentTarget.Location));
                }
            }

            if (closestTarget == null)
            {
                Logger.WriteDebug(Color.White, "no nearby Fear target");
            }

            return closestTarget;
        }


        /// <summary>
        /// summons warlock pet user configured.  handles cases where pet is gone and
        /// auto-summons (when landing after flying, after some movies/videos, instance changes, etc.)
        /// </summary>
        /// <returns></returns>
        private static Composite CreateWarlockSummonPet()
        {
            return new Decorator(
                ret => !SingularSettings.Instance.DisablePetUsage
                    && SingularRoutine.IsAllowed(Styx.CommonBot.Routines.CapabilityFlags.PetSummoning) 
                    && !Me.HasAura( "Grimoire of Sacrifice")        // don't summon pet if this buff active
                    && GetBestPet() != GetCurrentPet()
                    && Spell.CanCastHack( "Summon Imp"), 

                new Sequence(
                    // wait for possible auto-spawn if supposed to have a pet and none present
                    new DecoratorContinue(
                        ret => GetCurrentPet() == WarlockPet.None && GetBestPet() != WarlockPet.None && !PetManager.PetSummonAfterDismountTimer.IsFinished,
                        new Sequence(
                            new Action(ret => Logger.WriteDebug("Summon Pet:  waiting {0:F0} on dismount timer for live {1} to appear", PetManager.PetSummonAfterDismountTimer.TimeLeft.TotalMilliseconds, GetBestPet().ToString())),
                            new WaitContinue(
								// wait for up to [PetSummonAfterDismountTimer] + 1 sec tolerance
								PetManager.PetSummonAfterDismountTimer.WaitTime + TimeSpan.FromSeconds(1), 
                                ret => GetCurrentPet() != WarlockPet.None || GetBestPet() == WarlockPet.None || PetManager.PetSummonAfterDismountTimer.IsFinished, 
                                new Sequence(
                                    new Action( ret => Logger.WriteDebug("Summon Pet:  found '{0}' after waiting", GetCurrentPet().ToString())),
                                    new Action( r => { return GetBestPet() == GetCurrentPet() ? RunStatus.Failure : RunStatus.Success ; } )
                                    )
                                )
                            )
                        ),

                    // dismiss pet if wrong one is alive
                    new DecoratorContinue( 
                        ret => GetCurrentPet() != GetBestPet() && GetCurrentPet() != WarlockPet.None,
                        new Sequence(
                            new Action(ret => Logger.WriteDebug("Summon Pet:  dismissing {0}", GetCurrentPet().ToString())),
                            new Action(ctx => Lua.DoString("PetDismiss()")),
                            new WaitContinue( 
                                TimeSpan.FromMilliseconds(1000),
                                ret => GetCurrentPet() == WarlockPet.None,
                                new Action( ret => {
                                    Logger.WriteDebug("Summon Pet:  dismiss complete", GetCurrentPet().ToString());
                                    return RunStatus.Success; 
                                    })
                                )
                            )
                        ),

                    // summon pet best pet (unless best is none)
                    new DecoratorContinue(
                        ret => GetBestPet() != WarlockPet.None && GetBestPet() != GetCurrentPet(),
                        new Sequence(

#region Instant Pet Summon Check
                            new PrioritySelector(
                                new Decorator(
                                    ret => TalentManager.CurrentSpec == WoWSpec.WarlockDemonology && Me.HasAura("Demonic Rebirth"),
                                    new Action(r => Logger.Write(Color.White, "^Instant Summon Pet: Demonic Rebirth active!"))
                                    ),
                                new Decorator(
                                    // need to check that no live pet here as FoX will only summon last living, so worthless if live pet (even if wrong one)
                                    ret => TalentManager.CurrentSpec == WoWSpec.WarlockDestruction && !Me.GotAlivePet && Spell.CanCastHack("Flames of Xoroth", Me) && Warlock.Destruction.CurrentBurningEmbers >= 10,
                                    new Sequence(
                                        new Action(r => Logger.Write(Color.White, "^Instant Summon Pet: Flames of Xoroth!")),
                                        Spell.BuffSelf("Flames of Xoroth")
                                        )
                                    ),
                                new Decorator(
                                    req => StyxWoW.Me.HasAura( "Soulburn"),
                                    new Action( r => Logger.Write(Color.White, "^Instant Summon Pet: Soulburn already active - should work!"))
                                    ),
                                CreateCastSoulburn(ret => {
                                    if (TalentManager.CurrentSpec == WoWSpec.WarlockAffliction)
                                    {
                                        if (Me.CurrentSoulShards == 0)
                                            Logger.WriteDebug("CreateWarlockSummonPet:  no shards so instant pet summon not available");
                                        else if (!Me.Combat && !Unit.NearbyUnfriendlyUnits.Any(u => u.Combat || u.IsPlayer))
                                            Logger.WriteDebug("CreateWarlockSummonPet:  not in combat and no imminent danger nearby, so saving shards");
                                        else if (!Spell.CanCastHack("Soulburn", Me))
                                            Logger.WriteDebug("soulburn not available, shards={0}", Me.CurrentSoulShards);
                                        else
                                        {
                                            Logger.Write(Color.White, "^Instant Summon Pet: Soulburn - hope it works!");
                                            return true;
                                        }
                                    }
                                    return false;
                                    }),

                                new Action(r => Logger.WriteDebug("instant summon not active, continuing..."))
                                ),
#endregion
                            new Action(ret => Logger.WriteDebug("Summon Pet:  about to summon{0}", GetBestPet().ToString().CamelToSpaced())),

                            // Heal() used intentionally here (has spell completion logic not present in Cast())
                            Spell.Cast( n => "Summon" + GetBestPet().ToString().CamelToSpaced(), 
                                chkMov => true,
                                onUnit => Me, 
                                req => true,
                                cncl => GetBestPet() == GetCurrentPet()),

                            // make sure we see pet alive before continuing
                            new Wait( 1, ret => GetCurrentPet() != WarlockPet.None, new ActionAlwaysSucceed() )
                            )
                        )
                    )
                );
        }

        public static Composite CreateVoidwalkerDisarm()
        {
            if (!WarlockSettings.UseDisarm || GetBestPet() != WarlockPet.Voidwalker)
                return new ActionAlwaysFail();

            return new Decorator(
                req => GetCurrentPet() == WarlockPet.Voidwalker,
                PetManager.CastAction( "Disarm", on => Unit.NearbyUnitsInCombatWithMeOrMyStuff.FirstOrDefault(u => u.IsWithinMeleeRange && !Me.CurrentTarget.Disarmed && !Me.CurrentTarget.IsCrowdControlled() && Me.IsSafelyFacing(u, 150)))
                );
        }

        public static Composite CreateCastSoulburn(SimpleBooleanDelegate requirements)
        {
            return new Sequence(
                Spell.BuffSelf("Soulburn", ret => Me.CurrentSoulShards > 0 && requirements(ret)),
                new Wait(TimeSpan.FromMilliseconds(500), ret => Me.HasAura("Soulburn"), new Action(ret => { return RunStatus.Success; }))
                );
        }

        #region Pet Support

        /// <summary>
        /// determines the best WarlockPet value to use.  Attempts to use 
        /// user setting first, but if choice not available yet will choose Imp 
        /// for instances and Voidwalker for everything else.  
        /// </summary>
        /// <returns>WarlockPet to use</returns>
        public static WarlockPet GetBestPet()
        {
            WarlockPet currPet = GetCurrentPet();
            if (currPet == WarlockPet.Other)
                return currPet;

            WarlockPet bestPet = SingularSettings.Instance.Warlock().Pet;
            if (bestPet != WarlockPet.None)
            {
                if (TalentManager.CurrentSpec == WoWSpec.None)
                    return WarlockPet.Imp;

                if (bestPet == WarlockPet.Auto)
                {
                    if (TalentManager.CurrentSpec == WoWSpec.WarlockDemonology)
                        bestPet = WarlockPet.Felguard;
                    else if (TalentManager.CurrentSpec == WoWSpec.WarlockDestruction && Me.Level == Me.MaxLevel)
                        bestPet = WarlockPet.Felhunter;
                    else if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                        bestPet = WarlockPet.Succubus;
                    else if (SingularRoutine.CurrentWoWContext == WoWContext.Instances)
                        bestPet = WarlockPet.Felhunter;
                    else
                        bestPet = WarlockPet.Voidwalker;
                }

                string spellName = "Summon" + bestPet.ToString().CamelToSpaced();
                SpellFindResults sfr;
                if (!SpellManager.FindSpell(spellName, out sfr))
                {
                    if (SingularRoutine.CurrentWoWContext != WoWContext.Instances)
                        bestPet = WarlockPet.Voidwalker;
                    else if (Me.Level >= 30)
                        bestPet = WarlockPet.Felhunter;
                    else
                        bestPet = WarlockPet.Imp;
                }
            }

            return bestPet;
        }

        /// <summary>
        /// Pet.CreatureFamily.Id values for pets while the
        /// Grimoire of Supremecy talent is selected.  
        /// </summary>
        public enum WarlockGrimoireOfSupremecyPets
        {
            FelImp = 100,
            Wrathguard = 104,
            Voidlord = 101,
            Observer = 103,
            Shivarra = 102
        }
       
        /// <summary>
        /// return standard pet id associated with active pet. 
        /// note: we map Grimoire of Supremecy pets so rest of 
        /// Singular can treat in talent independent fashion
        /// </summary>
        /// <returns></returns>
        public static WarlockPet GetCurrentPet()
        {
            if (!Me.GotAlivePet)
                return WarlockPet.None;

            if (Me.Pet == null)
            {
                Logger.WriteDebug( "????? GetCurrentPet unstable - have live pet but Me.Pet == null !!!!!");
                return WarlockPet.None;
            }

            uint id;
            try
            {
                // following will fail when we have a non-creature warlock pet
                // .. this happens in quests where we get a pet assigned as Me.Pet (like Eric "The Swift")
                id = Me.Pet.CreatureFamilyInfo.Id;
            }
            catch
            {
                return WarlockPet.Other;
            }

            switch ((WarlockGrimoireOfSupremecyPets) Me.Pet.CreatureFamilyInfo.Id)
            {
                case (WarlockGrimoireOfSupremecyPets)WarlockPet.Imp:
                case (WarlockGrimoireOfSupremecyPets)WarlockPet.Felguard:
                case (WarlockGrimoireOfSupremecyPets)WarlockPet.Voidwalker:
                case (WarlockGrimoireOfSupremecyPets)WarlockPet.Felhunter:
                case (WarlockGrimoireOfSupremecyPets)WarlockPet.Succubus:
                    return (WarlockPet)Me.Pet.CreatureFamilyInfo.Id;

                case WarlockGrimoireOfSupremecyPets.FelImp:
                    return WarlockPet.Imp;
                case WarlockGrimoireOfSupremecyPets.Wrathguard:
                    return WarlockPet.Felguard;
                case WarlockGrimoireOfSupremecyPets.Voidlord:
                    return WarlockPet.Voidwalker;
                case WarlockGrimoireOfSupremecyPets.Observer:
                    return WarlockPet.Felhunter;
                case WarlockGrimoireOfSupremecyPets.Shivarra:
                    return WarlockPet.Succubus;
            }

            return WarlockPet.Other;
        }

        #endregion

        private static WoWUnit _targetRez;

        public static Composite CreateWarlockRessurectBehavior(UnitSelectionDelegate onUnit)
        {
            if (!UseSoulstoneForBattleRez())
                return new PrioritySelector();

            if (onUnit == null)
            {
                Logger.WriteDebug("CreateWarlockRessurectBehavior: error - onUnit == null");
                return new PrioritySelector();
            }

            return new Decorator(
                ret => Me.Combat && onUnit(ret) != null && onUnit(ret).IsDead && Spell.CanCastHack( "Soulstone", onUnit(ret)) && !Group.Healers.Any(h => h.IsAlive && !h.Combat && h.SpellDistance() < 40),
                new Sequence(
                    new Action( r => _targetRez = onUnit(r)),
                    new PrioritySelector(
                        Spell.WaitForCastOrChannel(),
                        Movement.CreateMoveToUnitBehavior(ret => _targetRez, 40f),
                        new Decorator(
                            ret => !Spell.IsGlobalCooldown(),
                            Spell.Cast("Soulstone", mov => true, on => _targetRez, req => true, cancel => ((WoWUnit)cancel).IsAlive)
                            )
                        )
                    )
                );
        }

        private static bool UseSoulstoneForBattleRez()
        {
            bool cast = SingularSettings.Instance.CombatRezTarget != CombatRezTarget.None 
                && (WarlockSettings.UseSoulstone == Soulstone.Ressurect || (WarlockSettings.UseSoulstone == Soulstone.Auto && SingularRoutine.CurrentWoWContext == WoWContext.Instances));
            return cast;
        }

        /// <summary>
        /// True: if appears Twilight Ward should be buffed.  checks possible classes of players attacking in pvp as 
        /// well as to mitigate some damage experienced due to defensive cooldowns that cause Shadow dmg (Soul Link, Dark Bargain)
        /// </summary>
        private static bool NeedTwilightWard
        {
            get
            {
                if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                {
                    if (Unit.NearbyUnfriendlyUnits.Any(u => u.IsPlayer && u.CurrentTargetGuid == Me.Guid && (u.Class == WoWClass.Priest || u.Class == WoWClass.Warlock)))
                    {
                        return true;
                    }
                }
                else
                {
                    if (Me.GotAlivePet && Me.HasAura("Soul Link"))
                    {
                        return true;
                    }
                }

                if ( Me.HasAura("Dark Bargain"))
                {
                    return true;
                }

                return false;
            }
        }

        public static Composite CreatWarlockHealthFunnelBehavior(int petMinHealth, int petMaxHealth = 99)
        {
            return new Decorator(
                ret => GetCurrentPet() != WarlockPet.None
                    && Me.Pet.HealthPercent < petMinHealth
                    && !Spell.IsSpellOnCooldown("Health Funnel")
                    && Me.Pet.Distance < 45
                    && Me.Pet.InLineOfSpellSight,
                    // && !HasTalent(WarlockTalents.SoulLink),  // no longer replaces Health Funnel
                new Sequence(
                    new PrioritySelector(

                        // glyph of health funnel prevents Soulburn: Health Funnel from being used
                        new Decorator( ret => TalentManager.HasGlyph("Health Funnel"), new ActionAlwaysSucceed()),

                        CreateCastSoulburn(ret => {
                            if (TalentManager.CurrentSpec == WoWSpec.WarlockAffliction)
                            {
                                if (Me.CurrentSoulShards > 0 && Spell.CanCastHack("Soulburn", Me))
                                {
                                    Logger.WriteDebug("Soulburn should follow to make instant health funnel");
                                    return true;
                                }
                                Logger.WriteDebug("soulburn not available, shards={0}", Me.CurrentSoulShards);
                            }
                            return false;
                            }),

                        // neither of instant funnels available, so stop moving
                        new Sequence(
                            new Action(ctx => StopMoving.Now()),
                            new Wait( 1, until => !Me.IsMoving, new ActionAlwaysSucceed() )
                            )
                        ),
                    new Decorator( ret => Spell.CanCastHack( "Health Funnel", Me.Pet), new ActionAlwaysSucceed()),
                    new PrioritySelector(
                        Spell.Cast(ret => "Health Funnel", mov => false, on => Me.Pet, req => Me.HasAura( "Soulburn") || TalentManager.HasGlyph("Health Funnel")),
                        Spell.Cast(ret => "Health Funnel", mov => true, on => Me.Pet, req => true, cancel => !Me.GotAlivePet || Me.Pet.HealthPercent >= petMaxHealth)
                        ),
                    Helpers.Common.CreateWaitForLagDuration()
                    // new WaitContinue(TimeSpan.FromMilliseconds(500), ret => !Me.IsCasting && Me.GotAlivePet && Me.Pet.HealthPercent < petMaxHealth && Me.HealthPercent > 50, new ActionAlwaysSucceed())
                    )
                );
        }

        public static IEnumerable<WoWUnit> TargetsInCombat
        {
            get
            {
                return Unit.NearbyUnfriendlyUnits.Where(u => u.Combat && u.IsTargetingUs() && !u.IsCrowdControlled() && StyxWoW.Me.IsSafelyFacing(u));
            }
        }


        private static bool ShouldCreateSoulwell
        {
            get
            {
                if (!WarlockSettings.CreateSoulwell)
                    return false;
                if (!SpellManager.HasSpell("Create Soulwell"))
                    return false;
                if (Spell.IsSpellOnCooldown("Create Soulwell"))
                    return false;
                if (!NeedSoulwellForThisContext)
                    return false;
                return true;
            }
        }

        static readonly uint[] SoulwellIds = new uint[]
                                         {
                                             181621
                                         };

        static public WoWGameObject Soulwell
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWGameObject>()
                        .FirstOrDefault(
                            i => SoulwellIds.Contains(i.Entry) && (StyxWoW.Me.RaidMembers.Any(p => p.Guid == i.CreatedByGuid) || StyxWoW.Me.Guid == i.CreatedByGuid)
                            );
            }
        }
        private static int _secondsBeforeBattle = 0;

        public static int RandomNumberOfSecondsBeforeBattleStarts
        {
            get
            {
                if (_secondsBeforeBattle == 0)
                    _secondsBeforeBattle = new Random().Next(30, 60);

                return _secondsBeforeBattle;
            }

            set
            {
                _secondsBeforeBattle = value;
            }
        }

        public static bool NeedSoulwellForThisContext
        {
            get
            {
                // in battlegrounds, create prior to regardless of whether we have any
                if ( SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                    return PVP.PrepTimeLeft < RandomNumberOfSecondsBeforeBattleStarts && Me.HasAnyAura("Preparation", "Arena Preparation");

                // otherwise, while group members nearby, no hostiles, and I need some stones
                if (Me.IsInGroup() && !HaveHealthStone)
                {
                    // if no players nearby, soulwell not needed
                    if (!Unit.NearbyGroupMembers.Any(g => g.IsAlive && !g.IsMe))
                        return false;

                    if (Unit.UnfriendlyUnits(55).Any(u => u.IsAlive))
                        return false;

                    return true;
                }

                return false;
            }
        }

    }

    public enum WarlockTalents
    {
        None = 0,
        DarkRegeneration,
        SoulLeech,
        HarvestLife,
        DemonicBreath,
        MortalCoil,
        Shadowfury,
        SoulLink,
        SacrificialPact,
        DarkBargain,
        BloodHorror,
        BurningRush,
        UnboundWill,
        GrimoireOfSupremacy,
        GrimoireOfService,
        GrimoireOfSacrifice,
        ArchimondesDarkness,
        KiljadensCunning,
        MannorothsFury
    }

}