using MicroAutoChess.Core;
using System.Linq;

namespace MicroAutoChess.App
{
    internal static class Program
    {
        static void Main(string[] args)
        {

            System.Console.WriteLine("MicroAutoChess C# skeleton");
            var gm = new GameManager();
            gm.Run();
            // Run combat demo
            CombatTest.RunDemo();
        }
    }
}
