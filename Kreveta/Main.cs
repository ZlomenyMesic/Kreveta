//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.search.moveorder;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace Kreveta;

internal static class Program {

    [DataType(DataType.Text)]
    private const string Version = "INDEV";

    [DataType(DataType.Text)]
    private const string HeaderText = $"Kreveta {Version} by ZlomenyMesic";

    internal static int Main(string[] args) {

        using (Process p = Process.GetCurrentProcess()) {
            p.PriorityClass = ProcessPriorityClass.RealTime;
        }

        Killers.Clear();
        QuietHistory.Clear();

        UCI.Log(HeaderText, UCI.LogLevel.RAW);

#if DEBUG
        Game.TestingFunction();
#endif

        UCI.UCILoop();

        return 0;
    }
}