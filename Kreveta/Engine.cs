//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.search.moveorder;

using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace Kreveta;

internal static class Engine {

    [DataType(DataType.Text)]
    internal const string Name    = "Kreveta";

    [DataType(DataType.Text)]
    internal const string Version = "INDEV";

    [DataType(DataType.Text)]
    internal const string Author  = "ZlomenyMesic";

    internal static int Main(string[] args) {

        using Process cur = Process.GetCurrentProcess();
        cur.PriorityClass = ProcessPriorityClass.RealTime;

        Killers.Clear();
        QuietHistory.Clear();

        UCI.Log($"{Name}-{Version} by {Author}", UCI.LogLevel.RAW);

#if DEBUG
        Game.TestingFunction();
#endif

        UCI.InputLoop();

        return 0;
    }
}