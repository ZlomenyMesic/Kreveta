//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;

using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Kreveta.search;
using Kreveta.search.moveorder;

namespace Kreveta;

internal static class Engine {

    [DataType(DataType.Text)]
    internal const string Name    = "Kreveta";

    [DataType(DataType.Text)]
    internal const string Version = "INDEV";

    [DataType(DataType.Text)]
    internal const string Author  = "ZlomenyMesic";

    internal static int Main(string[] args) {
        using var cur = Process.GetCurrentProcess();
        
        // this does actually make stuff a bit faster
        cur.PriorityClass = ProcessPriorityClass.RealTime;

        // although this could be useful, i am lazy
        if (args.Length != 0)
            UCI.Log("command line arguments are not supported", UCI.LogLevel.WARNING);
        
        // the default position is startpos to prevent crashes when
        // the user types go or perft without setting a position
        Game.SetPosFEN(["", "", ..Consts.StartposFEN.Split(' ')]);

        // header text when launching the engine
        UCI.Log($"{Name}-{Version} by {Author}");

#if DEBUG
        Game.TestingFunction();
#endif

        UCI.InputLoop();
        
        return 0;
    }
}