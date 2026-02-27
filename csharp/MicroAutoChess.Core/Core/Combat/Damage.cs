namespace MicroAutoChess.Core
{
    public class Damage
    {
        public double Value { get; set; }
        public int FrameNumber { get; set; }
        public DamageType? DmgType { get; set; }
        public int? SourceUnitId { get; set; }
        public int? TargetUnitId { get; set; }
        public string? SpellName { get; set; }
        public bool Crit { get; set; } = false;
        public bool Dot { get; set; } = false;
        public bool Heal { get; set; } = false;
        public int? MatchId { get; set; }

        public Damage() { }

        public Damage(double value, int frameNumber)
        {
            Value = value;
            FrameNumber = frameNumber;
        }
    }
}
