using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using MicroAutoChess.Core;

namespace MicroAutoChess.App
{
    public static class CombatTest
    {
        private class CombatVisualizer
        {
            private Board _board;
            public CombatVisualizer(Board board) { _board = board; }

            public void PrintBoard(string title = "")
            {
                _board.PrintBoard(title);
            }

            public void PrintUnitStats(List<Unit> units, string teamName)
            {
                ConsoleCompat.WriteLine($"\n{teamName} Units:");
                ConsoleCompat.WriteLine(new string('-', 50));
                int i = 1;
                foreach (var unit in units)
                {
                    if (unit.IsAlive())
                    {
                        double healthRatio = unit.CurrentHealth / unit.GetMaxHealth();
                        int filled = (int)(healthRatio * 10);
                        string bar = new string('█', filled) + new string('░', Math.Max(0, 10 - filled));
                        ConsoleCompat.WriteLine($"{i,2}. {unit.UnitType,-10} HP: {unit.CurrentHealth:0.0}/{unit.GetMaxHealth():0.0} [{bar}] ATK: {unit.GetAttack():0.0} DEF: {unit.GetDefense():0.0} RNG: {unit.BaseStats.Range:0.0} POS: {unit.Position} Mana: {unit.CurrentMana:0.0}/{unit.BaseStats.MaxMana:0.0}");
                    }
                    else
                    {
                        ConsoleCompat.WriteLine($"{i,2}. {unit.UnitType,-10} [DEFEATED]");
                    }
                    i++;
                }
            }

            public void PrintCombatLog(List<CombatEvent> events, int maxEvents = 15)
            {
                ConsoleCompat.WriteLine($"\nCombat Events (showing last {maxEvents}):");
                ConsoleCompat.WriteLine(new string('-', 60));
                var recent = events.Count > maxEvents ? events.Skip(events.Count - maxEvents).ToList() : new List<CombatEvent>(events);
                int currentFrame = -1;
                foreach (var ev in recent)
                {
                    if (ev.FrameNumber != currentFrame)
                    {
                        currentFrame = ev.FrameNumber;
                        ConsoleCompat.WriteLine($"\n--- frame {currentFrame} ---");
                    }
                    if (ev.EventType == CombatEventType.DAMAGE_DEALT && ev.SpellName == "BasicAttack") ConsoleCompat.WriteLine($"  ⚔️  {ev.Description}");
                    else if (ev.EventType == CombatEventType.MOVE_EXECUTED) ConsoleCompat.WriteLine($"  🏃 {ev.Description}");
                    else if (!string.IsNullOrEmpty(ev.Description) && ev.Description.ToLower().Contains("defeated")) ConsoleCompat.WriteLine($"  💀 {ev.Description}");
                    else ConsoleCompat.WriteLine($"  ℹ️  {ev.Description}");
                }
            }
        }

        internal static (Board, Player, Player) CreateMockScenario()
        {
            var board = new HexBoard((7, 8));
            var player1 = new Player(1, Team.TEAM_1);
            var player2 = new Player(2, Team.TEAM_2);

            // Team 1: LEVELTANKSTEST -> all tanks in columns 0..6, rows 0..3
            for (int x = 0; x < 7; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    var pos = (x, y);
                    var u = new Unit(UnitType.TANK, UnitRarity.COMMON, Team.TEAM_1) { Position = pos };
                    u.CurrentHealth = u.GetMaxHealth();
                    board.PlaceBoardUnit(u, pos);
                    player1.UnitsOnBoard[pos] = u;
                }
            }

            // Team 2: TANKSSOLUTION placements
            var team2Placements = new Dictionary<(int, int), UnitType>
            {
                [(1,4)] = UnitType.TANK,
                [(2,4)] = UnitType.TANK,
                [(3,4)] = UnitType.TANK,
                [(4,4)] = UnitType.TANK,
                [(5,4)] = UnitType.TANK,
                [(6,4)] = UnitType.TANK,
                [(6,6)] = UnitType.ARCHER,
                [(5,7)] = UnitType.ARCHER,
                [(6,7)] = UnitType.ARCHER
            };

            foreach (var kv in team2Placements)
            {
                var pos = kv.Key;
                var ut = kv.Value;
                var u = new Unit(ut, UnitRarity.COMMON, Team.TEAM_2) { Position = pos };
                u.CurrentHealth = u.GetMaxHealth();
                board.PlaceBoardUnit(u, pos);
                player2.UnitsOnBoard[pos] = u;
            }

            return (board, player1, player2);
        }

        public static (int, Dictionary<string, object>) RunCombatDemonstration(bool debug = false, int combatSeed = 42, int maxFrames = 2000)
        {
            var (board, p1, p2) = CreateMockScenario();
            var visualizer = new CombatVisualizer(board);

            if (debug)
            {
                visualizer.PrintBoard("Initial Setup");
                ConsoleCompat.WriteLine("Legend: Uppercase = Team 1, Lowercase = Team 2");
                visualizer.PrintUnitStats(p1.UnitsOnBoard.Values.Where(u => u != null).Select(u => u!).ToList(), "Team 1");
                visualizer.PrintUnitStats(p2.UnitsOnBoard.Values.Where(u => u != null).Select(u => u!).ToList(), "Team 2");
            }

            var engine = new CombatEngine(board, p1, p2, combatSeed: combatSeed);
            int winner = engine.SimulateCombat(maxFrames);
            var summary = engine.GetCombatSummary();

            if (debug)
            {
                ConsoleCompat.WriteLine("\n" + new string('=', 60));
                ConsoleCompat.WriteLine("COMBAT COMPLETE!");
                ConsoleCompat.WriteLine(new string('=', 60));
                visualizer.PrintBoard("Final Board State");
                visualizer.PrintUnitStats(p1.UnitsOnBoard.Values.Where(u => u != null).Select(u => u!).ToList(), "Team 1 (Final)");
                visualizer.PrintUnitStats(p2.UnitsOnBoard.Values.Where(u => u != null).Select(u => u!).ToList(), "Team 2 (Final)");

                ConsoleCompat.WriteLine($"\n🏆 Combat Results:");
                if (winner == 1) ConsoleCompat.WriteLine("🎉 TEAM 1 WINS!");
                else if (winner == 2) ConsoleCompat.WriteLine("🎉 TEAM 2 WINS!");
                else ConsoleCompat.WriteLine("🤝 DRAW!");
                ConsoleCompat.WriteLine($"⏱️  Total frames: {summary["total_frames"]}");
                ConsoleCompat.WriteLine($"📊 Total Events: {summary["total_events"]}");

                var events = (List<CombatEvent>)summary["events"];
                var attackCount = events.Count(e => e.EventType == CombatEventType.DAMAGE_DEALT && e.SpellName == "BasicAttack");
                var moveCount = events.Count(e => e.EventType == CombatEventType.MOVE_EXECUTED);
                var totalDamage = events.Where(e => e.Damage > 0).Sum(e => e.Damage);
                ConsoleCompat.WriteLine($"⚔️  Total Attacks: {attackCount}");
                ConsoleCompat.WriteLine($"🏃 Total Moves: {moveCount}");
                ConsoleCompat.WriteLine($"💥 Total Damage: {totalDamage}");

                visualizer.PrintCombatLog(events);
            }

            return (winner, summary);
        }

        public static void InteractiveStepByStep()
        {
            ConsoleCompat.WriteLine("\n" + new string('=', 60));
            ConsoleCompat.WriteLine("INTERACTIVE STEP-BY-STEP DEMO");
            ConsoleCompat.WriteLine(new string('=', 60));

            var (board, p1, p2) = CreateMockScenario();
            var visualizer = new CombatVisualizer(board);
            var engine = new CombatEngine(board, p1, p2, combatSeed: 42);

            visualizer.PrintBoard("Starting Positions");
            visualizer.PrintUnitStats(p1.UnitsOnBoard.Values.Where(u => u != null).Select(u => u!).ToList(), "Team 1");
            visualizer.PrintUnitStats(p2.UnitsOnBoard.Values.Where(u => u != null).Select(u => u!).ToList(), "Team 2");

            int maxFrames = 500;
            while (engine.FrameNumber < maxFrames)
            {
                int frameNum = engine.FrameNumber + 1;
                bool team1Alive = p1.UnitsOnBoard.Values.Any(u => u != null && u.IsAlive());
                bool team2Alive = p2.UnitsOnBoard.Values.Any(u => u != null && u.IsAlive());
                if (!team1Alive) { ConsoleCompat.WriteLine($"\n🏆 Team 2 Wins after {engine.FrameNumber} frames!"); break; }
                if (!team2Alive) { ConsoleCompat.WriteLine($"\n🏆 Team 1 Wins after {engine.FrameNumber} frames!"); break; }

                ConsoleCompat.WriteLine($"\n{new string('=',20)} frame {frameNum} {new string('=',20)}");
                ConsoleCompat.WriteLine("Press Enter to execute this frame...");
                ConsoleCompat.ReadLine();

                engine.ExecuteDelayedFrame();
                visualizer.PrintBoard($"After frame {engine.FrameNumber}");

                var frameEvents = engine.CombatLog.Where(e => e.FrameNumber == frameNum).ToList();
                if (frameEvents.Any())
                {
                    ConsoleCompat.WriteLine($"\nframe {frameNum} Events:");
                    foreach (var ev in frameEvents)
                    {
                        if (ev.EventType == CombatEventType.DAMAGE_DEALT) ConsoleCompat.WriteLine($"  ⚔️  {ev.Description}");
                        else if (ev.EventType == CombatEventType.MOVE_EXECUTED) ConsoleCompat.WriteLine($"  🏃 {ev.Description}");
                        else if (ev.EventType == CombatEventType.SPELL_EXECUTED) ConsoleCompat.WriteLine($"  ✨ {ev.Description}");
                        else if (!string.IsNullOrEmpty(ev.Description) && ev.Description.ToLower().Contains("defeated")) ConsoleCompat.WriteLine($"  💀 {ev.Description}");
                    }
                }

                visualizer.PrintUnitStats(p1.UnitsOnBoard.Values.Where(u => u != null).Select(u => u!).ToList(), "Team 1");
                visualizer.PrintUnitStats(p2.UnitsOnBoard.Values.Where(u => u != null).Select(u => u!).ToList(), "Team 2");
            }

                ConsoleCompat.WriteLine("\nInteractive demo completed!");
        }

        public static void TimeCombatDemoWithWinrates(int iterations = 100)
        {
            var times = new List<double>();
            var winners = new List<int>();
            for (int i = 0; i < iterations; i++)
            {
                var sw = Stopwatch.StartNew();
                var (w, _) = RunCombatDemonstration(debug: false, combatSeed: i);
                sw.Stop();
                times.Add(sw.Elapsed.TotalSeconds);
                winners.Add(w);
            }

            double total = times.Sum();
            double avg = total / iterations;
            double std = Math.Sqrt(times.Sum(t => (t - avg) * (t - avg)) / iterations);
            int t1 = winners.Count(w => w == 1);
            int t2 = winners.Count(w => w == 2);
            int d = winners.Count(w => w == 0);

            ConsoleCompat.WriteLine($"\nTiming Results:");
            ConsoleCompat.WriteLine($"Total time for {iterations} combat demonstrations: {total:0.00} seconds");
            ConsoleCompat.WriteLine($"Average time per demonstration: {avg:0.0000} seconds");
            ConsoleCompat.WriteLine($"Standard deviation: {std:0.0000} seconds");
            ConsoleCompat.WriteLine($"Winner distribution: {t1} Team 1, {t2} Team 2, {d} Draw");
            ConsoleCompat.WriteLine($"Win rates: Team 1: {(double)t1/iterations:0.00%}, Team 2: {(double)t2/iterations:0.00%}, Draw: {(double)d/iterations:0.00%}");
        }

        public static void RunDemo()
        {
            ConsoleCompat.WriteLine("Starting C# Combat Demo (full port)");
            // Default: run single demo in debug mode and then timing with winrates for 10 iterations
            RunCombatDemonstration(debug: true, combatSeed: 123, maxFrames: 2000);
            TimeCombatDemoWithWinrates(iterations: 10);
        }
    }
}
