using System;
using System.Collections.Generic;
using MicroAutoChess.Core.Spells;

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

        public UnitStats BaseStats { get; set; }

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
            // Map similar defaults from Python implementation
            return ut switch
            {
                UnitType.WARRIOR => new UnitStats(health:1200, attack:65, spellPower:1, defense:40, resistance:40, range:1) { Spell = SpellsFactory.GetSpellInstanceByName("Spin Slash"), AttackSpeed = 1.0 },
                UnitType.ARCHER => new UnitStats(health:700, attack:60, spellPower:1, defense:20, resistance:20, range:4) { Spell = SpellsFactory.GetSpellInstanceByName("Attack Speed Buff"), MaxMana = 70, AttackSpeed = 1.0 },
                UnitType.MAGE => new UnitStats(health:600, attack:40, spellPower:1, defense:20, resistance:20, range:4) { Spell = SpellsFactory.GetSpellInstanceByName("Fireball"), MaxMana = 50 },
                UnitType.TANK => new UnitStats(health:1500, attack:50, spellPower:1, defense:60, resistance:60, range:1) { Spell = SpellsFactory.GetSpellInstanceByName("Heal"), AttackSpeed = 0.8 },
                UnitType.ASSASSIN => new UnitStats(health:800, attack:60, spellPower:1, defense:25, resistance:30, range:1) { Spell = SpellsFactory.GetSpellInstanceByName("Assassin Blink"), MaxMana = 50, CritRate = 0.5, AttackSpeed = 1.2 },
                UnitType.SUPPORT => new UnitStats(health:900, attack:25, spellPower:1, defense:20, resistance:20, range:4) { MaxMana = 80 },
                _ => new UnitStats(health:100, attack:10, spellPower:1, defense:5, resistance:5, range:1)
            };
        }

        public double GetMaxHealth()
        {
            return BaseStats.Health * (1 + (Level - 1) * 0.5);
        }

        public double GetAttack()
        {
            return BaseStats.Attack * (1 + (Level - 1) * 0.3);
        }

        public double GetDefense() => BaseStats.Defense;
        public double GetResistance() => BaseStats.Resistance;

        public double GetAttackSpeed() => BaseStats.AttackSpeed * (Buffs.ContainsKey("attack_speed") ? Buffs["attack_speed"] : 1.0);

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

            double actualDamage = Math.Min(mitigatedDamage, CurrentHealth);
            CurrentHealth -= actualDamage;
            PostmigitationMana(actualDamage);

            var ev = new CombatEvent
            {
                FrameNumber = damageObj.FrameNumber,
                Source = source,
                Target = this,
                EventType = CombatEventType.DAMAGE_DEALT,
                SpellName = string.IsNullOrEmpty(spellName) ? null : spellName,
                Damage = actualDamage,
                CritBool = damageObj.Crit,
                Position = Position,
                Description = $"{(source != null ? source.UnitType.ToString() : "Unknown")} dealt {actualDamage} {damageObj.DmgType} damage to {UnitType}{(damageObj.Crit ? " with a crit" : "")}",
                MatchId = null
            };
            try { GlobalLog.CombatLog.Add(ev); } catch { }

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
                try { GlobalLog.CombatLog.Add(death); } catch { }
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
            try { GlobalLog.CombatLog.Add(ev); } catch { }
        }

        public (double, bool) GetBasicFinalDamage(double critRoll)
        {
            double baseDamage = GetAttack();
            if (critRoll < BaseStats.CritRate)
                return ((int)(baseDamage * BaseStats.CritDmg), true);
            return (baseDamage, false);
        }

        public void AddBasicAttackMana() => CurrentMana += BasicAttackMana;

        public void PremigitationMana(double dmg) => CurrentMana = Math.Min(BaseStats.MaxMana, CurrentMana + 0.01 * dmg);
        public void PostmigitationMana(double dmg) => CurrentMana = Math.Min(BaseStats.MaxMana, CurrentMana + 0.07 * dmg);

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
                UnitType.WARRIOR => "W",
                UnitType.ARCHER => "A",
                UnitType.MAGE => "M",
                UnitType.TANK => "T",
                UnitType.ASSASSIN => "S",
                UnitType.SUPPORT => "H",
                _ => "U",
            };
        }

        public void RoundReset()
        {
            CurrentHealth = GetMaxHealth();
            CurrentMana = BaseStats.InitialMana;
            Buffs.Clear();
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
            return u;
        }
    }
}
