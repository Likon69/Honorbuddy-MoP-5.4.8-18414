using System;

namespace Singular
{
    // stop pollution of the namespace in random classes

    public enum TrinketUsage
    {
        Never,
        OnCooldown,
        OnCooldownInCombat,
        LowPower,
        LowHealth,
        CrowdControlled,
        CrowdControlledSilenced
    }

    public enum ClusterType
    {
        // Circular cluster centered around 'target'
        Radius,
        Chained,
        Cone,
        // returns a cluster of units that are between LocalPlayer location and 'target'
        PathToUnit
    }

    public enum CastOn
    {
        Never,
        Bosses,
        Players,
        All,
    }

    [Flags]
    public enum WoWContext
    {
        None = 0,
        Normal = 0x1,
        Instances = 0x2,
        Battlegrounds = 0x4,

        All = Normal | Instances | Battlegrounds,
    }

    [Flags]
    public enum BehaviorType
    {
        Rest = 0x1,
        PreCombatBuffs = 0x2,
        PullBuffs = 0x4,
        Pull = 0x8,
        Heal = 0x10,
        CombatBuffs = 0x20,
        Combat = 0x40,
        LossOfControl = 0x80,
        Death = 0x100,

        Initialize = 0x200,     // initializer method (return is ignored)

        // this is no guarantee that the bot is in combat
        InCombat = Heal | CombatBuffs | Combat,
        // this is no guarantee that the bot is out of combat
        OutOfCombat = Rest | PreCombatBuffs | PullBuffs | Death,

        All = Rest | PreCombatBuffs | PullBuffs | Pull | Heal | CombatBuffs | Combat | LossOfControl | Death,
    }

    [Flags]
    public enum WatchTargetForCast
    {
        None = 0,
        Current = 0x1,  // only currenttarget
        Facing = 0x2,   // only those we are safelyfacing
        InRange = 0x4   // regardless of facing
    }

    [Flags]
    public enum HealingContext
    {
        None = 0,
        Normal = 0x1,
        Instances = 0x2,
        Battlegrounds = 0x4,
        Raids = 0x8
    }

}
