/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

using Stockshrimp_1.movegen;
using Stockshrimp_1.search;
using Stockshrimp_1.search.movesort;
using System.Diagnostics;

namespace Stockshrimp_1;

internal static class UCI {
    internal static void Main() {
        using (Process p = Process.GetCurrentProcess()) {
            p.PriorityClass = ProcessPriorityClass.RealTime;
        }

        LookupTables.Initialize();

        Killers.Clear();
        History.Clear();

        Game.TestingFunction();

        Console.WriteLine("Stockshrimp-INDEV by ZlomenyMesic");

        while (true) {
            string cmd = Console.ReadLine() ?? string.Empty;
            string[] toks = cmd.Split(' ');

            switch (toks[0]) {
                case "uci": CmdUCI(); break;
                case "isready": CmdIsReady(); break;
                case "position": CmdPosition(toks); break;
                case "go": CmdGo(toks); break;
                case "perft": CmdPerft(toks); break;
                case "showallmoves": CmdShowAllMoves(); break;
                case "print": CmdPrint(); break;

                default: Console.WriteLine($"unknown command: {toks[0]}"); break;
            }

            Console.WriteLine();
        }
    }

    private static void CmdUCI() {
        Console.WriteLine("id name Stockshrimp-1\nid author ZlomenyMesic\nuciok");
    }

    private static void CmdIsReady() {
        Console.WriteLine("readyok");
    }

    private static void CmdPosition(string[] toks) {
        switch (toks[1]) {
            case "startpos": Game.SetPosFEN(["", "", ..Consts.STARTPOS_FEN.Split(' '), ..toks]); break;
            case "fen": Game.SetPosFEN(toks); break;
            default: Console.WriteLine($"invalid argument: {toks[1]}"); return;
        }
    }

    private static void CmdPerft(string[] toks) {
        if (toks.Length == 2) {

            Stopwatch sw = Stopwatch.StartNew();

            int depth;

            try {
                depth = int.Parse(toks[1]);
            } catch {
                Console.WriteLine($"invalid perft command syntax");
                return;
            }

            Console.WriteLine($"nodes: {Perft.Run(Game.board, depth)}");
            Console.WriteLine($"time spent: {sw.Elapsed}");

            sw.Stop();
        } 
        else {
            Console.WriteLine($"invalid perft command syntax");
        }
    }

    private static void CmdGo(string[] toks) {

        // TODO - OTHER ARGS

        // PROBLEMATIC POSITIONS:
        // position startpos moves e2e4 g8f6 e4e5 f6g4 d1g4 b8c6 g1f3 a8b8 g4g3 d7d5 e5d6 e7d6 f1b5 c8d7 e1g1 f7f6 b1c3 c6e7 d2d4 c7c6 b5d3 e7f5 f1e1 e8f7
        // position startpos moves e2e4 e7e5 g1f3 f8d6 f1d3 g8f6 e1g1 e8g8 b1c3 b8c6 f1e1 d6c5 c3a4 b7b6 a4c5 b6c5 d3b5 c6d4 f3d4 c5d4 c2c3
        // position startpos moves d2d4 b8c6 g1f3 d7d5 b1c3 g8f6 c1f4 c8g4 a1c1 a8c8 h1g1 f6h5 f4e3 e7e6 e3g5 f7f6
        // position startpos moves d2d3 d7d5 e2e3 c8f5 b1c3 g8f6 g1f3 b8c6 f1e2 e7e6 e1g1 f8d6 h2h4 e8g8 c1d2 a8c8 a1c1 d8e8 f1e1 f5g4 e1f1 e6e5 f1e1 g4h5 c3b5 d6c5 b5c7 c8c7 f3e5 h5e2
        // position startpos moves e2e4 e7e5 g1f3 f8d6 b1c3 g8e7 c3b5 e8g8 a1b1 b8c6 f1e2 d6c5 e1g1 d7d5 e4d5 e7d5 b5c3 d5c3 b2c3 c8e6 d2d3 e6a2 b1b7 c5b6 c1a3 f8e8 f3e5 c6e5 c3c4 e5c6 a3b2 d8c8 b7b6 a7b6 e2f3 c8d7 f3c6 d7c6 d1g4 f7f6 b2d4 a8d8 d4b2 g8h8 g4h5 c6a4 f1a1 a4c2 h5b5 a2c4 d3c4

        // position startpos moves e2e4 b8c6 g1f3 e7e5 f1b5 g8f6 b1c3 f8d6 b5c6 d7c6 e1g1 e8g8 d2d4 c8g4 d4e5 g4f3 g2f3 d6e5 c1g5 d8e8 d1e2 e8e6 e2e3 e6h3 g5f4 e5f4 e3f4 a8c8 f4e3 f6d7 e3d4

        // e2e4 c7c5 b1c3 b8c6 g1f3 e7e6 f1b5 c6d4 e1g1 a7a6 b5c4 g7g6 d2d3 g8e7 c1f4 e7c6 a1c1 b7b5 c4b3 b5b4 c3a4 a6a5 f1e1 d7d6 f3d4 c5d4 c2c3 b4c3 b2c3 f8g7 c3d4 c6d4 b3c4 e8g8 a4c3 c8b7 c1b1 b7c6 c3e2 a8b8 b1b8 d8b8 e2d4 g7d4 f4h6 b8b2 e1e2 b2b7 h6f8 g8f8 c4b3 f8g7 e2e1 b7b6 e1f1 e6e5 d1c2 c6d7 b3c4 b6d8 f1c1 d4c5 c2d1 d8f6 d1e1 f6g5 c1b1 c5d4 b1b8 h7h5 b8a8 d4c5

        TimeMan.ProcessTime(toks);

        Console.WriteLine($"ideal time budget: {TimeMan.time_budget_ms} ms");

        //PVSControl.Reset();
        //PVSControl.StartSearch(1, 50, TimeMan.time_budget_ms);
        OldPVSControl.StartSearch(50, TimeMan.time_budget_ms);
    }

    private static void CmdShowAllMoves() {
        foreach (Move m in Movegen.GetLegalMoves(Game.board)) {
            Console.WriteLine(m.ToString());
        }
    }

    private static void CmdPrint() {
        Game.board.Print();
    }
}