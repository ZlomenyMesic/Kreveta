//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System;
using System.Diagnostics;

using Kreveta.movegen;
using Kreveta.moveorder;
using Kreveta.perft;
using Kreveta.search;

namespace Kreveta;

internal static class Program {
    
    internal const string Name    = "Kreveta";
    internal const string Version = "INDEV";
    internal const string Author  = "ZlomenyMesic";

    internal static int Main(string[] args) {
        using var cur = Process.GetCurrentProcess();
        
        // this does actually make stuff a bit faster
        cur.PriorityClass = ProcessPriorityClass.RealTime;

        // used to free memory before exiting
        AppDomain.CurrentDomain.ProcessExit += FreeMemory;

        // although this could be useful, i am lazy
        if (args.Length != 0)
            UCI.Log("Command line arguments are not supported", UCI.LogLevel.WARNING);
        
        // the default position is startpos to prevent crashes when
        // the user types go or perft without setting a position
        Game.Board = Board.CreateStartpos();

        // header text when launching the engine
        UCI.Log($"{Name}-{Version} by {Author}");
        UCI.InputLoop();
        
        return 0;
        
        // since we manually allocate memory, we must free it before exiting the program
        static void FreeMemory(object? sender, EventArgs e) {
            ((Action)TT.Clear + PerftTT.Clear + PawnCorrectionHistory.Clear + Killers.Clear + MoveOrder.Clear + LookupTables.Clear)();
        
#if DEBUG
            UCI.Log("Manually allocated memory freed properly");
#endif
        }
    }
}