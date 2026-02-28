using System;

namespace MicroAutoChess.Core
{
    public class StatusEffect
    {
        private static int _counter = 0;
        public int Id { get; }
        public string Name { get; set; }
        public StatusEffectType EffectType { get; set; }
        public bool IsDebuff { get; set; }
        public int SourceUnitId { get; set; }
        public Team SourceTeam { get; set; }
        public int RemainingDuration { get; set; }
        public int TotalDuration { get; set; }
        public int AppliedFrame { get; set; }

        // DoT fields
        public double DotDamagePerTick { get; set; }
        public DamageType DotDamageType { get; set; } = DamageType.MAGICAL;
        public int TickInterval { get; set; } = 1;
        private int _tickAccumulator;

        // Stat buff fields
        public StatType BuffStat { get; set; }
        public double BuffValue { get; set; }
        public bool IsMultiplicative { get; set; } = false;

        // Shield fields
        public double ShieldAmount { get; set; }
        public double MaxShieldAmount { get; set; }

        public bool IsExpired
        {
            get
            {
                if (EffectType == StatusEffectType.SHIELD)
                    return ShieldAmount <= 0 && RemainingDuration <= 0;
                return RemainingDuration <= 0;
            }
        }

        public StatusEffect(string name, StatusEffectType type)
        {
            Id = _counter++;
            Name = name;
            EffectType = type;
        }

        /// <summary>Advance one frame. For DoTs, deals tick damage when the interval is reached.</summary>
        public void Tick(Unit target, int frameNumber)
        {
            if (EffectType == StatusEffectType.DOT && target.IsAlive())
            {
                _tickAccumulator++;
                if (_tickAccumulator >= TickInterval)
                {
                    _tickAccumulator = 0;
                    var dmg = new Damage
                    {
                        Value = DotDamagePerTick,
                        Dot = true,
                        DmgType = DotDamageType,
                        FrameNumber = frameNumber,
                        SourceUnitId = SourceUnitId,
                        SpellName = Name
                    };
                    target.TakeDamage(dmg, null, Name);
                }
            }
            RemainingDuration--;
        }

        /// <summary>Absorb post-mitigation damage via shield. Returns remaining unabsorbed damage.</summary>
        public double AbsorbDamage(double damage)
        {
            if (EffectType != StatusEffectType.SHIELD || ShieldAmount <= 0) return damage;
            double absorbed = Math.Min(damage, ShieldAmount);
            ShieldAmount -= absorbed;
            return damage - absorbed;
        }

        public StatusEffect Clone()
        {
            return new StatusEffect(Name, EffectType)
            {
                IsDebuff = IsDebuff,
                SourceUnitId = SourceUnitId,
                SourceTeam = SourceTeam,
                RemainingDuration = RemainingDuration,
                TotalDuration = TotalDuration,
                AppliedFrame = AppliedFrame,
                DotDamagePerTick = DotDamagePerTick,
                DotDamageType = DotDamageType,
                TickInterval = TickInterval,
                BuffStat = BuffStat,
                BuffValue = BuffValue,
                IsMultiplicative = IsMultiplicative,
                ShieldAmount = ShieldAmount,
                MaxShieldAmount = MaxShieldAmount
            };
        }

        // ── Factory methods ──

        public static StatusEffect CreateDoT(string name, int sourceId, Team sourceTeam,
            double damagePerTick, DamageType dmgType, int duration, int tickInterval = 1)
        {
            return new StatusEffect(name, StatusEffectType.DOT)
            {
                IsDebuff = true,
                SourceUnitId = sourceId,
                SourceTeam = sourceTeam,
                DotDamagePerTick = damagePerTick,
                DotDamageType = dmgType,
                RemainingDuration = duration,
                TotalDuration = duration,
                TickInterval = tickInterval
            };
        }

        public static StatusEffect CreateStatBuff(string name, int sourceId, Team sourceTeam,
            StatType stat, double value, int duration, bool multiplicative = false, bool isDebuff = false)
        {
            return new StatusEffect(name, StatusEffectType.STAT_BUFF)
            {
                IsDebuff = isDebuff,
                SourceUnitId = sourceId,
                SourceTeam = sourceTeam,
                BuffStat = stat,
                BuffValue = value,
                IsMultiplicative = multiplicative,
                RemainingDuration = duration,
                TotalDuration = duration
            };
        }

        public static StatusEffect CreateShield(string name, int sourceId, Team sourceTeam,
            double amount, int duration)
        {
            return new StatusEffect(name, StatusEffectType.SHIELD)
            {
                IsDebuff = false,
                SourceUnitId = sourceId,
                SourceTeam = sourceTeam,
                ShieldAmount = amount,
                MaxShieldAmount = amount,
                RemainingDuration = duration,
                TotalDuration = duration
            };
        }

        public static StatusEffect CreateStun(string name, int sourceId, Team sourceTeam, int duration)
        {
            return new StatusEffect(name, StatusEffectType.STUN)
            {
                IsDebuff = true,
                SourceUnitId = sourceId,
                SourceTeam = sourceTeam,
                RemainingDuration = duration,
                TotalDuration = duration
            };
        }
    }
}
