//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.search.moveorder;
using System.Diagnostics;

namespace Kreveta;

internal static class Program {
    internal static int Main(string[] args) {

        using (Process p = Process.GetCurrentProcess()) {
            p.PriorityClass = ProcessPriorityClass.RealTime;
        }

        Killers.Clear();
        History.Clear();

        UCI.Log("Kreveta-INDEV by ZlomenyMesic", UCI.LogLevel.RAW);

        Game.TestingFunction();

        UCI.UCILoop();

        return 0;
    }
}