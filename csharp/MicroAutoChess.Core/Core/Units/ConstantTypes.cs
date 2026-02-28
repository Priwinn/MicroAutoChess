namespace MicroAutoChess.Core
{
    public enum Team
    {
        TEAM_1 = 1,
        TEAM_2 = 2,
        NEUTRAL = 0
    }
    public enum UnitType
    {
        WARRIOR,
        ARCHER,
        MAGE,
        TANK,
        ASSASSIN,
        LIGHTNING_MAGE,
        ICE_TANK,
        EARTH_TANK,
        FOREST_ARCHER,
        NONE
    }

    public enum DamageType
    {
        PHYSICAL,
        MAGICAL,
        TRUE
    }

    public enum UnitRarity
    {
        COMMON = 1,
        UNCOMMON = 2,
        RARE = 3,
        EPIC = 4,
        LEGENDARY = 5
    }

    public enum CombatAction
    {
        MOVE,
        ATTACK,
        CAST_SPELL,
        WAIT
    }

    public enum CombatEventType
    {
        START_EVENT,
        END_EVENT,
        OTHER_EVENT,
        FAILED_ATTACK,
        FAILED_MOVE,
        DAMAGE_DEALT,
        HEALING_DONE,
        UNIT_SPAWNED,
        ACTION_PLANNED,
        CONFLICT_RESOLVED,
        UNIT_DIED,
        SPELL_EXECUTED,
        MOVE_EXECUTED,
        STATUS_EFFECT_APPLIED,
        STATUS_EFFECT_REMOVED
    }

    public enum StatusEffectType
    {
        DOT,
        STAT_BUFF,
        SHIELD,
        STUN
    }

    public enum StatType
    {
        ATTACK,
        DEFENSE,
        RESISTANCE,
        ATTACK_SPEED,
        SPELL_POWER,
        HEALTH
    }
}
