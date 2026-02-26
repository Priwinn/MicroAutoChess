using System;
using System.Collections.Generic;
using System.Linq;
using MicroAutoChess.Core.Spells;

namespace MicroAutoChess.Core
{
    public class PlannedAction
    {
        public Unit Unit { get; set; }
        public CombatAction ActionType { get; set; }
        public AbstractSpell? SpellInstance { get; set; }
        public Unit? Target { get; set; }
        public (int, int)? TargetPosition { get; set; }
        public int ResolutionFrame { get; set; }
        public int PlannedFrame { get; set; }
        public string Description { get; set; } = string.Empty;
        public (int, int)? StartPosition { get; set; }

        public PlannedAction(Unit unit, CombatAction action)
        {
            Unit = unit;
            ActionType = action;
        }
    }

    public class ActionTiming
    {
        public double AttackDelay { get; set; } = 10;
        public double MoveDelay { get; set; } = 4;
        public double SpellDelay { get; set; } = 10;
        public double WaitDelay { get; set; } = 2;

        public double GetDelay(CombatAction action)
        {
            return action switch
            {
                CombatAction.ATTACK => AttackDelay,
                CombatAction.MOVE => MoveDelay,
                CombatAction.CAST_SPELL => SpellDelay,
                CombatAction.WAIT => WaitDelay,
                _ => WaitDelay
            };
        }
    }

    public class CombatEngine
    {
        private Board _board;
        private Player _player1;
        private Player _player2;
        private Random _rng;
        public int FrameNumber { get; private set; } = 0;
        public int MaxFrames { get; set; } = 2000;
        public List<CombatEvent> CombatLog => GlobalLog.CombatLog;

        public ActionTiming ActionTiming { get; set; }
        public List<PlannedAction> ActionQueue { get; set; } = new List<PlannedAction>();

        public int? CombatSeed { get; private set; }

        public CombatEngine(Board board, Player p1, Player p2, int? combatSeed = null, ActionTiming? actionTiming = null)
        {
            _board = board;
            _player1 = p1;
            _player2 = p2;
            CombatSeed = combatSeed;
            _rng = combatSeed.HasValue ? new Random(combatSeed.Value) : new Random();
            ActionTiming = actionTiming ?? new ActionTiming();
            CombatLog.Clear();
            SetTeams();
        }

        public void SetTeams()
        {
            foreach (var u in _player1.UnitsOnBoard.Values.Where(u => u != null)) u!.Team = Team.TEAM_1;
            foreach (var u in _player2.UnitsOnBoard.Values.Where(u => u != null)) u!.Team = Team.TEAM_2;
        }

        public int SimulateCombat()
        {
            CombatLog.Clear();
            ActionQueue.Clear();
            FrameNumber = 0;

            if (CombatSeed.HasValue)
            {
                CombatLog.Add(new CombatEvent { FrameNumber = 0, EventType = CombatEventType.START_EVENT, Description = $"Combat started with seed: {CombatSeed.Value}, ActionTiming: {ActionTiming.GetDelay(CombatAction.ATTACK)}" });
            }

            while (FrameNumber < MaxFrames)
            {
                var allUnits = _player1.UnitsOnBoard.Values.Concat(_player2.UnitsOnBoard.Values).Where(u => u != null && u.IsAlive()).Select(u => u!).ToList();
                if (!allUnits.Any()) break;

                bool team1Alive = allUnits.Any(u => u.Team == Team.TEAM_1);
                bool team2Alive = allUnits.Any(u => u.Team == Team.TEAM_2);
                if (!team1Alive) return 2;
                if (!team2Alive) return 1;

                ExecuteDelayedFrame();
            }

            double team1Health = _player1.UnitsOnBoard.Values.Where(u => u != null && u.IsAlive()).Sum(u => u!.CurrentHealth);
            double team2Health = _player2.UnitsOnBoard.Values.Where(u => u != null && u.IsAlive()).Sum(u => u!.CurrentHealth);
            if (team1Health > team2Health) return 1;
            if (team2Health > team1Health) return 2;
            return 0;
        }

        // Backwards-compatible overload used by existing tests
        public int SimulateCombat(int maxFrames)
        {
            MaxFrames = maxFrames;
            return SimulateCombat();
        }

        public void ExecuteDelayedFrame()
        {
            FrameNumber += 1;

            var allUnits = _player1.UnitsOnBoard.Values.Concat(_player2.UnitsOnBoard.Values).Where(u => u != null && u.IsAlive()).Select(u => u!).ToList();
            allUnits.Sort((a, b) =>
            {
                if (!a.Position.HasValue) return 1;
                if (!b.Position.HasValue) return -1;
                return (a.Position.Value.Item1 * _board.Size.Item2 + a.Position.Value.Item2).CompareTo(b.Position.Value.Item1 * _board.Size.Item2 + b.Position.Value.Item2);
            });

            ExecuteQueuedActions();
            PlanActions(allUnits);
            CleanupDeadUnits(allUnits);
        }

        private void ExecuteQueuedActions()
        {
            var actionsToExecute = ActionQueue.Where(a => a.ResolutionFrame <= FrameNumber).ToList();
            ActionQueue = ActionQueue.Where(a => a.ResolutionFrame > FrameNumber).ToList();
            if (!actionsToExecute.Any()) return;

            var attackActions = actionsToExecute.Where(a => a.ActionType == CombatAction.ATTACK).ToList();
            var moveActions = actionsToExecute.Where(a => a.ActionType == CombatAction.MOVE).ToList();
            var spellActions = actionsToExecute.Where(a => a.ActionType == CombatAction.CAST_SPELL).ToList();

            if (attackActions.Any()) ExecuteAttacksSimultaneously(attackActions);
            if (spellActions.Any()) ExecuteSpellsSimultaneously(spellActions);
            if (moveActions.Any()) ExecuteMovesSimultaneously(moveActions);
        }

        private void PlanActions(List<Unit> allUnits)
        {
            var positionConflicts = new Dictionary<(int, int), List<PlannedAction>>();

            foreach (var unit in allUnits)
            {
                if (!unit.IsAlive()) continue;
                bool hasPending = ActionQueue.Any(a => a.Unit.Id == unit.Id);
                if (hasPending) continue;

                var action = PlanUnitAction(unit, allUnits);
                if (action == null || action.ActionType == CombatAction.WAIT) continue;

                double delay = ActionTiming.GetDelay(action.ActionType);
                if (action.ActionType == CombatAction.ATTACK)
                {
                    double initiativeNeeded = (delay - unit.BasicAttackOverflow);
                    double framesToWait = initiativeNeeded / unit.GetAttackSpeed();
                    int roundedFrames = (int)Math.Ceiling(framesToWait);
                    unit.BasicAttackOverflow = (roundedFrames * unit.GetAttackSpeed() - initiativeNeeded);
                    action.ResolutionFrame = FrameNumber + roundedFrames;
                }
                else if (action.ActionType == CombatAction.CAST_SPELL && action.SpellInstance != null)
                {
                    action.ResolutionFrame = FrameNumber + action.Unit.BaseStats.Spell!.SpellDelay;
                }
                else
                {
                    action.ResolutionFrame = FrameNumber + (int)delay;
                }

                action.PlannedFrame = FrameNumber;
                action.StartPosition = unit.Position;

                if (action.ActionType == CombatAction.MOVE && action.TargetPosition.HasValue)
                {
                    var pos = action.TargetPosition.Value;
                    if (!positionConflicts.ContainsKey(pos)) positionConflicts[pos] = new List<PlannedAction>();
                    positionConflicts[pos].Add(action);
                }
                else
                {
                    ActionQueue.Add(action);
                }

                CombatLog.Add(new CombatEvent { FrameNumber = FrameNumber, Source = action.Unit, Target = action.Target, EventType = CombatEventType.ACTION_PLANNED, SpellName = action.SpellInstance?.Name, Description = $"{action.Unit.UnitType} plans {action.ActionType} to {(action.TargetPosition.HasValue ? action.TargetPosition.ToString() : action.Target?.ToString())} (resolves in frame {action.ResolutionFrame})" });
            }

            foreach (var kv in positionConflicts)
            {
                var pos = kv.Key; var actions = kv.Value;
                if (actions.Count > 1)
                {
                    var chosen = actions[_rng.Next(actions.Count)];
                    ActionQueue.Add(chosen);
                    _board.SetPlanned(chosen.TargetPosition!.Value, chosen.Unit);
                    chosen.Unit.PlannedPosition = chosen.TargetPosition;
                    foreach (var action in actions)
                    {
                        if (action != chosen)
                        {
                            action.ActionType = CombatAction.WAIT;
                            action.ResolutionFrame = FrameNumber + (int)ActionTiming.GetDelay(CombatAction.MOVE);
                            ActionQueue.Add(action);
                            CombatLog.Add(new CombatEvent { FrameNumber = FrameNumber, Source = action.Unit, EventType = CombatEventType.CONFLICT_RESOLVED, Description = $"Movement plan conflict at {pos}: {action.Unit.UnitType} was set to WAIT." });
                        }
                    }
                    CombatLog.Add(new CombatEvent { FrameNumber = FrameNumber, Source = chosen.Unit, EventType = CombatEventType.CONFLICT_RESOLVED, Position = pos, Description = $"Movement plan conflict at {pos}: {chosen.Unit.UnitType} was randomly chosen to be resolved." });
                }
                else
                {
                    var a = actions[0];
                    ActionQueue.Add(a);
                    _board.SetPlanned(a.TargetPosition!.Value, a.Unit);
                    a.Unit.PlannedPosition = a.TargetPosition;
                }
            }
        }

        private PlannedAction? PlanUnitAction(Unit unit, List<Unit> allUnits)
        {
            var enemies = allUnits.Where(u => u.IsAlive() && u.Team != unit.Team).ToList();
            if (!enemies.Any() || !unit.Position.HasValue) return new PlannedAction(unit, CombatAction.WAIT);

            if (unit.CurrentMana >= unit.BaseStats.MaxMana && unit.BaseStats.Spell != null && unit.BaseStats.Spell.Ranged == false)
            {
                bool valid = unit.BaseStats.Spell.Prepare(unit, _board);
                if (valid)
                {
                    unit.CurrentMana -= unit.BaseStats.MaxMana;
                    var pa = new PlannedAction(unit, CombatAction.CAST_SPELL) { SpellInstance = unit.BaseStats.Spell, Target = unit.BaseStats.Spell.Target };
                    return pa;
                }
                else throw new InvalidOperationException("Invalid prepare for spell");
            }

            var target = FindTarget(unit, enemies);
            if (target == null || !target.Position.HasValue) return new PlannedAction(unit, CombatAction.WAIT);

            double distance = _board.L2Distance(unit.Position.Value, target.Position.Value);

            if (unit.CurrentMana >= unit.BaseStats.MaxMana && unit.BaseStats.Spell != null && unit.BaseStats.Spell.Ranged && distance <= unit.BaseStats.Spell.Range)
            {
                bool valid = unit.BaseStats.Spell.Prepare(unit, _board);
                if (valid)
                {
                    unit.CurrentMana -= unit.BaseStats.MaxMana;
                    return new PlannedAction(unit, CombatAction.CAST_SPELL) { SpellInstance = unit.BaseStats.Spell, Target = unit.BaseStats.Spell.Target };
                }
                else throw new InvalidOperationException("Invalid prepare for spell");
            }

            if (distance <= unit.BaseStats.Range + _board.RangeOffset * unit.BaseStats.Range)
            {
                return new PlannedAction(unit, CombatAction.ATTACK) { Target = target };
            }
            else
            {
                var targetPos = PlanMovement(unit, target);
                if (targetPos == unit.Position) return new PlannedAction(unit, CombatAction.WAIT);
                return new PlannedAction(unit, CombatAction.MOVE) { TargetPosition = targetPos };
            }
        }

        private (int, int)? PlanMovement(Unit unit, Unit target)
        {
            if (!unit.Position.HasValue || !target.Position.HasValue) return null;
            var path = _board.FindPathToRangeGuided(unit.Position.Value, target.Position.Value, unit.BaseStats.Range + _board.RangeOffset * unit.BaseStats.Range);
            if (path != null && path.Count > 1) return path[1];
            return unit.Position;
        }

        private void ExecuteAttacksSimultaneously(List<PlannedAction> attacks)
        {
            var aliveFlags = attacks.Select(a => a.Unit.IsAlive()).ToList();
            for (int i = 0; i < attacks.Count; i++)
            {
                var action = attacks[i];
                if (!aliveFlags[i] || action.Target == null || !action.Target.IsAlive())
                {
                    CombatLog.Add(new CombatEvent { FrameNumber = FrameNumber, EventType = CombatEventType.FAILED_ATTACK, Source = action.Unit, Target = action.Target, Description = $"Attack from {action.Unit.UnitType} fails (target dead)" });
                    continue;
                }

                action.Unit.AddBasicAttackMana();
                var (premitigated, critBool) = action.Unit.GetBasicFinalDamage(_rng.NextDouble());
                var dmg = new Damage { Value = premitigated, Crit = critBool, DmgType = DamageType.PHYSICAL, FrameNumber = FrameNumber, SourceUnitId = action.Unit.Id, TargetUnitId = action.Target.Id, SpellName = "BasicAttack" };
                action.Target.TakeDamage(dmg, action.Unit, "BasicAttack");
            }
        }

        private void ExecuteSpellsSimultaneously(List<PlannedAction> spells)
        {
            foreach (var action in spells)
            {
                if (!action.Unit.IsAlive() || action.Target == null || !action.Target.IsAlive()) continue;
                action.Unit.BaseStats.Spell!.Execute(action.Unit, _board, FrameNumber, critRate: action.Unit.BaseStats.CritRate, critDmg: action.Unit.BaseStats.CritDmg, canCrit: action.Unit.SpellCrit, critRoll: _rng.NextDouble());
                CombatLog.Add(new CombatEvent { FrameNumber = FrameNumber, Source = action.Unit, Target = action.Target, EventType = CombatEventType.SPELL_EXECUTED, SpellName = action.Unit.BaseStats.Spell!.Name, Description = $"{action.Unit.UnitType} casts a {action.Unit.BaseStats.Spell!.Name} spell (planned in frame {action.PlannedFrame})" });
            }
        }

        private void ExecuteMovesSimultaneously(List<PlannedAction> moves)
        {
            var validMoves = moves.Where(a => a.Unit.IsAlive()).ToList();
            foreach (var action in validMoves)
            {
                var oldPos = action.Unit.Position;
                var newPos = action.TargetPosition;
                if (oldPos.HasValue && newPos.HasValue)
                {
                    bool moved = _board.MoveUnit(oldPos.Value, newPos.Value);
                    if (moved)
                    {
                        CombatLog.Add(new CombatEvent { FrameNumber = FrameNumber, Source = action.Unit, EventType = CombatEventType.MOVE_EXECUTED, Position = newPos, Description = $"{action.Unit.UnitType} moves to {newPos} (planned in frame {action.PlannedFrame})" });
                    }
                    else
                    {
                        CombatLog.Add(new CombatEvent { FrameNumber = FrameNumber, Source = action.Unit, EventType = CombatEventType.FAILED_MOVE, Position = newPos, Description = $"{action.Unit.UnitType} failed to move to {newPos} (occupied); cancelling movement." });
                        foreach (var pending in ActionQueue.Where(p => p.Unit.Id == action.Unit.Id).ToList())
                        {
                            pending.ActionType = CombatAction.WAIT;
                            pending.StartPosition = null;
                            pending.Description = $"Cancelled move to {pending.TargetPosition} (tile occupied)";
                        }
                    }
                }
            }
        }

        private void CleanupDeadUnits(List<Unit> allUnits)
        {
            foreach (var unit in allUnits.ToList())
            {
                if (!unit.IsAlive() && unit.Position.HasValue)
                {
                    CombatLog.Add(new CombatEvent { FrameNumber = FrameNumber, Source = null, Target = unit, EventType = CombatEventType.UNIT_DIED, Description = $"{unit.UnitType} is defeated!" });
                    _board.RemoveUnit(unit.Position.Value);
                    if (unit.PlannedPosition.HasValue) _board.RemoveUnit(unit.PlannedPosition.Value);
                    ActionQueue = ActionQueue.Where(a => a.Unit != unit).ToList();
                }
            }
            allUnits.RemoveAll(u => !u.IsAlive());
        }

        private Unit? FindTarget(Unit unit, List<Unit> enemies)
        {
            if (!enemies.Any() || !unit.Position.HasValue) return null;

            if (unit.CurrentTarget != null && enemies.Contains(unit.CurrentTarget) && _board.PathfindDistanceToRange(unit.Position.Value, unit.CurrentTarget.Position!.Value, unit.BaseStats.Range + _board.RangeOffset * unit.BaseStats.Range) <= 1)
            {
                return unit.CurrentTarget;
            }

            Unit? closest = null;
            double minDistance = double.PositiveInfinity;
            int bestCount = 1;

            enemies.Sort((a, b) => _board.L2Distance(unit.Position.Value, a.Position!.Value).CompareTo(_board.L2Distance(unit.Position.Value, b.Position!.Value)));
            foreach (var enemy in enemies.Take(3))
            {
                if (!enemy.Position.HasValue) continue;
                double distance = _board.PathfindDistanceToRange(unit.Position.Value, enemy.Position.Value, unit.BaseStats.Range + _board.RangeOffset * unit.BaseStats.Range);
                if (double.IsPositiveInfinity(distance)) continue;
                if (distance < minDistance) { minDistance = distance; closest = enemy; bestCount = 1; }
                else if (Math.Abs(distance - minDistance) < 1e-9)
                {
                    double l2cur = _board.L2Distance(unit.Position.Value, closest!.Position!.Value);
                    double l2new = _board.L2Distance(unit.Position.Value, enemy.Position!.Value);
                    if (Math.Abs(l2new - l2cur) < 1e-9)
                    {
                        if (_rng.NextDouble() < 1.0 / bestCount) closest = enemy;
                        bestCount += 1;
                    }
                    else if (l2new <= l2cur) closest = enemy;
                }
            }

            if (closest == null)
            {
                foreach (var enemy in enemies.Skip(3))
                {
                    if (!enemy.Position.HasValue) continue;
                    double distance = _board.PathfindDistanceToRange(unit.Position.Value, enemy.Position.Value, unit.BaseStats.Range + _board.RangeOffset * unit.BaseStats.Range);
                    if (double.IsPositiveInfinity(distance)) continue;
                    if (distance < minDistance) { minDistance = distance; closest = enemy; bestCount = 1; }
                    else if (Math.Abs(distance - minDistance) < 1e-9)
                    {
                        double l2cur = _board.L2Distance(unit.Position.Value, closest!.Position!.Value);
                        double l2new = _board.L2Distance(unit.Position.Value, enemy.Position!.Value);
                        if (Math.Abs(l2new - l2cur) < 1e-9)
                        {
                            if (_rng.NextDouble() < 1.0 / bestCount) closest = enemy;
                            bestCount += 1;
                        }
                        else if (l2new <= l2cur) closest = enemy;
                    }
                }
            }

            unit.CurrentTarget = closest;
            return closest;
        }

        public Dictionary<string, object> GetCombatSummary()
        {
            return new Dictionary<string, object>
            {
                ["total_frames"] = FrameNumber,
                ["total_events"] = CombatLog.Count,
                ["events"] = CombatLog,
                ["combat_seed"] = CombatSeed,
                ["action_timing"] = ActionTiming,
                ["pending_actions"] = ActionQueue.Count
            };
        }

        public void SetCombatSeed(int? seed)
        {
            CombatSeed = seed;
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public int? GetCombatSeed() => CombatSeed;

        public void SetActionTiming(ActionTiming at) => ActionTiming = at;

        public List<PlannedAction> GetPendingActions() => new List<PlannedAction>(ActionQueue);
    }
}
