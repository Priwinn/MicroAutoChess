using System;
using System.Collections.Generic;
using System.Linq;
using MicroAutoChess.Core.Spells;
using MicroAutoChess.Core.Traits;
using MicroAutoChess.Core.UnitTypes;

namespace MicroAutoChess.Core
{
    public class Unit
    {
        private static int _counter = 0;
        public int Id { get; private set; }
        public UnitType UnitType { get; set; }
        public UnitRarity Rarity { get; set; }
        public Team Team { get; set; }

        public int Level { get; set; } = 1;
        public (int, int)? Position { get; set; }
        public (int, int)? InitialPosition { get; set; }
        public (int, int)? PlannedPosition { get; set; }
        public double CurrentHealth { get; set; }
        public double CurrentMana { get; set; }

        public double BasicAttackMana { get; set; } = 10.0;
        public int Cost { get; set; } = 1;

        public bool SpellCrit { get; set; } = false;
        public double BasicAttackOverflow { get; set; } = 0.0;
        public Unit? CurrentTarget { get; set; }
        public Dictionary<string, double> Buffs { get; set; } = new Dictionary<string, double>();
        public List<StatusEffect> ActiveStatusEffects { get; set; } = new List<StatusEffect>();

        public UnitStats BaseStats { get; set; }
        public GameParams? Params { get; set; }

        /// <summary>
        /// Per-unit combat log reference. Defaults to the global log but can be
        /// overridden per-engine so each combat writes to its own log.
        /// </summary>
        public List<CombatEvent> CombatLog { get; set; } = GlobalLog.CombatLog;

        public Unit(UnitType unitType, UnitRarity rarity, Team team)
        {
            Id = _counter++;
            UnitType = unitType;
            Rarity = rarity;
            Team = team;
            BaseStats = GetDefaultStats(unitType);
            CurrentHealth = GetMaxHealth();
            CurrentMana = BaseStats.InitialMana;
        }

        private UnitStats GetDefaultStats(UnitType ut)
        {
            return ut switch
            {
                UnitType.WARRIOR => WarriorDef.CreateStats(),
                UnitType.ARCHER => ArcherDef.CreateStats(),
                UnitType.MAGE => MageDef.CreateStats(),
                UnitType.TANK => TankDef.CreateStats(),
                UnitType.ASSASSIN => AssassinDef.CreateStats(),
                UnitType.LIGHTNING_MAGE => LightningMageDef.CreateStats(),
                UnitType.ICE_TANK => IceTankDef.CreateStats(),
                UnitType.EARTH_TANK => EarthTankDef.CreateStats(),
                UnitType.FOREST_ARCHER => ForestArcherDef.CreateStats(),
                _ => new UnitStats(health:100, attack:10, spellPower:1, defense:5, resistance:5, range:1)
            };
        }

        public double GetMaxHealth()
        {
            double scale = Params?.HealthScalingPerLevel ?? 0.5;
            double baseVal = BaseStats.Health * (1 + (Level - 1) * scale);
            return baseVal + GetStatBuffAdditive(StatType.HEALTH);
        }

        public double GetAttack()
        {
            double scale = Params?.AttackScalingPerLevel ?? 0.3;
            double baseVal = BaseStats.Attack * (1 + (Level - 1) * scale);
            return (baseVal + GetStatBuffAdditive(StatType.ATTACK)) * GetStatBuffMultiplicative(StatType.ATTACK);
        }

        public double GetDefense()
        {
            return (BaseStats.Defense + GetStatBuffAdditive(StatType.DEFENSE)) * GetStatBuffMultiplicative(StatType.DEFENSE);
        }

        public double GetResistance()
        {
            return (BaseStats.Resistance + GetStatBuffAdditive(StatType.RESISTANCE)) * GetStatBuffMultiplicative(StatType.RESISTANCE);
        }

        public double GetAttackSpeed()
        {
            double oldBuff = Buffs.ContainsKey("attack_speed") ? Buffs["attack_speed"] : 1.0;
            double baseVal = BaseStats.AttackSpeed * oldBuff;
            return (baseVal + GetStatBuffAdditive(StatType.ATTACK_SPEED)) * GetStatBuffMultiplicative(StatType.ATTACK_SPEED);
        }

        public double GetSpellPower()
        {
            return (BaseStats.SpellPower + GetStatBuffAdditive(StatType.SPELL_POWER)) * GetStatBuffMultiplicative(StatType.SPELL_POWER);
        }

        public bool IsStunned => ActiveStatusEffects.Any(e => e.EffectType == StatusEffectType.STUN && !e.IsExpired);

        public int GetCost()
        {
            int baseCost = (int)Rarity;
            return baseCost * (int)Math.Pow(3, Level - 1);
        }

        public int GetSellValue()
        {
            if ((int)Rarity == 1) return GetCost();
            return Level == 1 ? (int)Rarity : GetCost() - 1;
        }

        public bool IsAlive() => CurrentHealth > 0;

        public double TakeDamage(Damage damageObj, Unit? source = null, string spellName = "")
        {
            if (damageObj.Heal)
                throw new ArgumentException("Damage object indicates healing, use Heal() instead.");

            PremigitationMana(damageObj.Value);
            double mitigatedDamage;
            if (damageObj.DmgType == DamageType.PHYSICAL)
            {
                mitigatedDamage = Math.Max(1, damageObj.Value * 100.0 / (100.0 + GetDefense()));
            }
            else if (damageObj.DmgType == DamageType.MAGICAL)
            {
                mitigatedDamage = Math.Max(1, damageObj.Value * 100.0 / (100.0 + GetResistance()));
            }
            else
            {
                mitigatedDamage = damageObj.Value;
            }

            double afterShield = AbsorbDamageWithShields(mitigatedDamage);
            double actualDamage = Math.Min(afterShield, CurrentHealth);
            CurrentHealth -= actualDamage;
            PostmigitationMana(actualDamage);

            double rawDamage = damageObj.Value;
            double mitigatedAmount = rawDamage - mitigatedDamage;
            if (mitigatedAmount < 0) mitigatedAmount = 0;

            var ev = new CombatEvent
            {
                FrameNumber = damageObj.FrameNumber,
                Source = source,
                Target = this,
                EventType = CombatEventType.DAMAGE_DEALT,
                SpellName = string.IsNullOrEmpty(spellName) ? null : spellName,
                Damage = actualDamage,
                RawDamage = rawDamage,
                DamageMitigated = mitigatedAmount,
                CritBool = damageObj.Crit,
                IsDot = damageObj.Dot,
                Position = Position,
                Description = $"{(source != null ? source.UnitType.ToString() : "Unknown")} dealt {actualDamage} {damageObj.DmgType} damage to {UnitType}{(damageObj.Crit ? " with a crit" : "")}",
                MatchId = null
            };
            try { CombatLog.Add(ev); } catch { }

            if (CurrentHealth <= 0)
            {
                var death = new CombatEvent
                {
                    FrameNumber = damageObj.FrameNumber,
                    Source = source,
                    Target = this,
                    EventType = CombatEventType.UNIT_DIED,
                    SpellName = string.IsNullOrEmpty(spellName) ? null : spellName,
                    Damage = 0,
                    CritBool = false,
                    Position = Position,
                    Description = $"{UnitType} died.",
                    MatchId = null
                };
                try { CombatLog.Add(death); } catch { }
            }

            return actualDamage;
        }

        public void Heal(Damage damageObj, Unit? source = null, string spellName = "")
        {
            if (!damageObj.Heal)
                throw new ArgumentException("Damage object does not indicate healing, use TakeDamage() instead.");
            double maxHealth = GetMaxHealth();
            CurrentHealth = Math.Min(maxHealth, CurrentHealth + damageObj.Value);
            var ev = new CombatEvent
            {
                FrameNumber = 0,
                Source = source,
                Target = this,
                EventType = CombatEventType.HEALING_DONE,
                SpellName = spellName,
                Damage = damageObj.Value,
                CritBool = false,
                Position = Position,
                Description = $"{UnitType} healed for {damageObj.Value} health.",
                MatchId = null
            };
            try { CombatLog.Add(ev); } catch { }
        }

        public (double, bool) GetBasicFinalDamage(double critRoll)
        {
            double baseDamage = GetAttack();
            if (critRoll < BaseStats.CritRate)
                return ((int)(baseDamage * BaseStats.CritDmg), true);
            return (baseDamage, false);
        }

        public void AddBasicAttackMana() => CurrentMana += BasicAttackMana;

        public void PremigitationMana(double dmg) => CurrentMana = Math.Min(BaseStats.MaxMana, CurrentMana + (Params?.PreMitigationManaRate ?? 0.01) * dmg);
        public void PostmigitationMana(double dmg) => CurrentMana = Math.Min(BaseStats.MaxMana, CurrentMana + (Params?.PostMitigationManaRate ?? 0.07) * dmg);

        public void LevelUp()
        {
            Level += 1;
            CurrentHealth = GetMaxHealth();
            try { BaseStats.Spell?.UpdateLevel(Level); } catch { }
        }

        public override string ToString()
        {
            return $"({UnitType}, L{Level}, HP: {CurrentHealth:0.0}/{GetMaxHealth():0.0}, Mana: {CurrentMana:0.0}/{BaseStats.MaxMana}), Pos: {Position}";
        }

        // Return a short symbol for this unit used by visualizers
        public string GetSymbol()
        {
            return UnitType switch
            {
                UnitType.WARRIOR => WarriorDef.Symbol,
                UnitType.ARCHER => ArcherDef.Symbol,
                UnitType.MAGE => MageDef.Symbol,
                UnitType.TANK => TankDef.Symbol,
                UnitType.ASSASSIN => AssassinDef.Symbol,
                UnitType.LIGHTNING_MAGE => LightningMageDef.Symbol,
                UnitType.ICE_TANK => IceTankDef.Symbol,
                UnitType.EARTH_TANK => EarthTankDef.Symbol,
                UnitType.FOREST_ARCHER => ForestArcherDef.Symbol,
                _ => "U",
            };
        }

        public static TraitType[] GetTraits(UnitType ut)
        {
            return ut switch
            {
                UnitType.WARRIOR => WarriorDef.Traits,
                UnitType.ARCHER => ArcherDef.Traits,
                UnitType.MAGE => MageDef.Traits,
                UnitType.TANK => TankDef.Traits,
                UnitType.ASSASSIN => AssassinDef.Traits,
                UnitType.LIGHTNING_MAGE => LightningMageDef.Traits,
                UnitType.ICE_TANK => IceTankDef.Traits,
                UnitType.EARTH_TANK => EarthTankDef.Traits,
                UnitType.FOREST_ARCHER => ForestArcherDef.Traits,
                _ => Array.Empty<TraitType>(),
            };
        }

        public void RoundReset()
        {
            Buffs.Clear();
            ActiveStatusEffects.Clear();
            CurrentHealth = GetMaxHealth();
            CurrentMana = BaseStats.InitialMana;
            CurrentTarget = null;
            BasicAttackOverflow = 0.0;
            try { BaseStats.Spell?.RoundReset(); } catch { }
            Position = InitialPosition;
        }

        public Unit Clone()
        {
            var u = new Unit(this.UnitType, this.Rarity, this.Team);
            u.Level = this.Level;
            u.BaseStats = this.BaseStats;
            u.CurrentHealth = this.CurrentHealth;
            u.CurrentMana = this.CurrentMana;
            u.BasicAttackMana = this.BasicAttackMana;
            u.BasicAttackOverflow = this.BasicAttackOverflow;
            u.SpellCrit = this.SpellCrit;
            u.Position = this.Position;
            u.InitialPosition = this.InitialPosition;
            u.PlannedPosition = this.PlannedPosition;
            u.Buffs = new Dictionary<string, double>(this.Buffs);
            u.ActiveStatusEffects = this.ActiveStatusEffects.Select(e => e.Clone()).ToList();
            return u;
        }

        // ── Status effect helpers ──

        public void ApplyStatusEffect(StatusEffect effect, int frameNumber)
        {
            effect.AppliedFrame = frameNumber;
            ActiveStatusEffects.Add(effect);
            try
            {
                CombatLog.Add(new CombatEvent
                {
                    FrameNumber = frameNumber,
                    Target = this,
                    EventType = CombatEventType.STATUS_EFFECT_APPLIED,
                    SpellName = effect.Name,
                    Description = $"{UnitType} receives {effect.Name} ({effect.EffectType}, {effect.RemainingDuration} frames)"
                });
            }
            catch { }
        }

        public void TickStatusEffects(int frameNumber)
        {
            // Tick all active effects (DoTs deal damage, durations decrement)
            for (int i = ActiveStatusEffects.Count - 1; i >= 0; i--)
            {
                var effect = ActiveStatusEffects[i];
                if (!effect.IsExpired)
                    effect.Tick(this, frameNumber);
            }
            RemoveExpiredEffects(frameNumber);
        }

        private void RemoveExpiredEffects(int frameNumber)
        {
            for (int i = ActiveStatusEffects.Count - 1; i >= 0; i--)
            {
                if (ActiveStatusEffects[i].IsExpired)
                {
                    var e = ActiveStatusEffects[i];
                    try
                    {
                        CombatLog.Add(new CombatEvent
                        {
                            FrameNumber = frameNumber,
                            Target = this,
                            EventType = CombatEventType.STATUS_EFFECT_REMOVED,
                            SpellName = e.Name,
                            Description = $"{e.Name} expired on {UnitType}"
                        });
                    }
                    catch { }
                    ActiveStatusEffects.RemoveAt(i);
                }
            }
        }

        public double AbsorbDamageWithShields(double damage)
        {
            foreach (var effect in ActiveStatusEffects)
            {
                if (damage <= 0) break;
                damage = effect.AbsorbDamage(damage);
            }
            return damage;
        }

        private double GetStatBuffAdditive(StatType stat)
        {
            double val = 0;
            foreach (var e in ActiveStatusEffects)
            {
                if (e.EffectType == StatusEffectType.STAT_BUFF && e.BuffStat == stat && !e.IsMultiplicative && !e.IsExpired)
                    val += e.BuffValue;
            }
            return val;
        }

        private double GetStatBuffMultiplicative(StatType stat)
        {
            double val = 1.0;
            foreach (var e in ActiveStatusEffects)
            {
                if (e.EffectType == StatusEffectType.STAT_BUFF && e.BuffStat == stat && e.IsMultiplicative && !e.IsExpired)
                    val *= (1 + e.BuffValue);
            }
            return val;
        }
    }
}
