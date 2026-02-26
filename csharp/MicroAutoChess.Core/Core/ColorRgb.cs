namespace MicroAutoChess.Core
{
    // Small RGB holder used by visualizer for color hints
    public struct ColorRgb
    {
        public int R { get; set; }
        public int G { get; set; }
        public int B { get; set; }

        public ColorRgb(int r, int g, int b)
        {
            R = r; G = g; B = b;
        }
    }
}
