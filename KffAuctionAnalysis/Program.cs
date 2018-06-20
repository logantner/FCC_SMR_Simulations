using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KFFSimulations
{
    class Program
    {
        static void Main(string[] args)
        {
            string config = "Release";

#if DEBUG
            config = "Debug";
            Console.WriteLine("Debug triggered");
#endif

            Simulation s;
            if (config == "Debug")
                s = new Simulation(AppDomain.CurrentDomain.BaseDirectory + "..\\..\\..\\");
            else
                s = new Simulation(AppDomain.CurrentDomain.BaseDirectory + "..\\");
        }
    }
}
