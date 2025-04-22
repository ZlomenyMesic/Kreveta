//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using BenchmarkDotNet.Running;
using Kreveta.openingbook;
using Kreveta.search;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Kreveta;

#nullable enable
internal static class UCI {
    internal enum LogLevel : byte {
        INFO, WARNING, ERROR, RAW
    }

    [ReadOnly(true)]
    private const int InputBufferSize = 4096;

    [ReadOnly(true)]
    private static readonly TextReader Input;

    [ReadOnly(true)]
    private static readonly TextWriter Output;

    [ReadOnly(true)]
    private static readonly StreamWriter? NKOutput;

    internal static string NKLogFilePath = @".\out.log";

    static UCI() {

        // the default Console.ReadLine buffer is quite small and cannot
        // handle long move lines, thus we use a larger buffer size
        Input = new StreamReader(Console.OpenStandardInput(InputBufferSize));
        Output = Console.Out;

        try {
            NKOutput = new StreamWriter(NKLogFilePath);

            NeoKolors.Console.NKDebug.Logger.Output         = NKOutput;
            NeoKolors.Console.NKDebug.Logger.SimpleMessages = true;
        } catch {
            Log("NKLogger initialization failed", LogLevel.ERROR);
        }
    }

    internal static void InputLoop() {
        while (true) {

            string input = Input.ReadLine()
                ?? string.Empty;

            string[] tokens = input.Split(' ');

            // we log the input commands as well
            LogIntoFile(input, LogLevel.RAW);

            switch (tokens[0]) {
                case "uci":        CmdUCI();             break;
                case "isready":    CmdIsReady();         break;
                case "setoption":  CmdSetOption(tokens); break;
                case "ucinewgame": CmdUciNewGame();      break;
                case "position":   CmdPosition(tokens);  break;
                case "go":         CmdGo(tokens);        break;
                case "perft":      CmdPerft(tokens);     break;
                case "print":      CmdPrint();           break;
                case "stop":       CmdStop();            break;

#if DEBUG
                case "test":       CmdTest();            break;
#endif

                case "quit":       return;

                default: Log($"unknown command: {tokens[0]}", LogLevel.ERROR); 
                         break;
            }

            Console.WriteLine();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CmdUCI() {
        const string UCIOK = "uciok";

        Log($"id name {Engine.Name}-{Engine.Version}\n" +
            $"id author {Engine.Author}\n");

        Options.Print();

        Log(UCIOK);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CmdIsReady() {
        const string ReadyOK = "readyok";

        Log(ReadyOK);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CmdUciNewGame() {
        Game.FullGame = true;
        TT.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CmdStop() {
        Game.FullGame = false;
        TT.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CmdSetOption(string[] toks) {
        Options.SetOption(toks);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CmdPosition(string[] toks) {
        //Game.board = new();
        //return;
        switch (toks[1]) {

            // we don't use the startpos constructor, because we can have a list
            // of moves played from the starting position, which would be quite
            // difficult and unnecessary to implement
            case "startpos": Game.SetPosFEN(["", "", ..Consts.StartposFEN.Split(' '), ..toks]); break;
            case "fen":      Game.SetPosFEN(toks);                                              break;

            default:         Log($"invalid argument: {toks[1]}", LogLevel.ERROR);               return;
        }
    }

    private static void CmdPerft(string[] toks) {
        if (toks.Length == 2) {

            Stopwatch sw = Stopwatch.StartNew();

            int depth;

            try {
                depth = int.Parse(toks[1]);
            } catch { goto invalid_syntax; }

            Log($"nodes: {Perft.Run(Game.board, depth)}", LogLevel.INFO);
            Log($"time spent: {sw.Elapsed}", LogLevel.INFO);

            sw.Stop();
            return;
        } 

        invalid_syntax:
        Log($"invalid perft command syntax", LogLevel.ERROR);
        
    }

    private static void CmdGo(string[] toks) {

        TimeMan.ProcessTimeTokens(toks);

        int depth = 50;
        int depthIndex = Array.IndexOf(toks, "depth");

        // the depth keyword should be directly followed by a parsable token
        if (depthIndex != -1) {
            try {
                depth = int.Parse(toks[depthIndex + 1]);
                TimeMan.TimeBudget = long.MaxValue;
            } catch { Log("invalid depth argument", LogLevel.ERROR); }
        }

        // don't use book moves when we want an actual search at a specified depth
        // or when movetime is set (either specific search time or infinite time)
        if (depthIndex == -1 && TimeMan.MoveTime == 0 && Options.OwnBook && OpeningBook.BookMove != "") {
            Log($"bestmove {OpeningBook.BookMove}");
            return;
        }

        // PROBLEMATIC POSITIONS:
        // position startpos moves e2e4 g8f6 e4e5 f6g4 d1g4 b8c6 g1f3 a8b8 g4g3 d7d5 e5d6 e7d6 f1b5 c8d7 e1g1 f7f6 b1c3 c6e7 d2d4 c7c6 b5d3 e7f5 f1e1 e8f7
        // position startpos moves e2e4 e7e5 g1f3 f8d6 f1d3 g8f6 e1g1 e8g8 b1c3 b8c6 f1e1 d6c5 c3a4 b7b6 a4c5 b6c5 d3b5 c6d4 f3d4 c5d4 c2c3
        // position startpos moves d2d4 b8c6 g1f3 d7d5 b1c3 g8f6 c1f4 c8g4 a1c1 a8c8 h1g1 f6h5 f4e3 e7e6 e3g5 f7f6
        // position startpos moves d2d3 d7d5 e2e3 c8f5 b1c3 g8f6 g1f3 b8c6 f1e2 e7e6 e1g1 f8d6 h2h4 e8g8 c1d2 a8c8 a1c1 d8e8 f1e1 f5g4 e1f1 e6e5 f1e1 g4h5 c3b5 d6c5 b5c7 c8c7 f3e5 h5e2
        // position startpos moves e2e4 e7e5 g1f3 f8d6 b1c3 g8e7 c3b5 e8g8 a1b1 b8c6 f1e2 d6c5 e1g1 d7d5 e4d5 e7d5 b5c3 d5c3 b2c3 c8e6 d2d3 e6a2 b1b7 c5b6 c1a3 f8e8 f3e5 c6e5 c3c4 e5c6 a3b2 d8c8 b7b6 a7b6 e2f3 c8d7 f3c6 d7c6 d1g4 f7f6 b2d4 a8d8 d4b2 g8h8 g4h5 c6a4 f1a1 a4c2 h5b5 a2c4 d3c4

        // position startpos moves e2e4 b8c6 g1f3 e7e5 f1b5 g8f6 b1c3 f8d6 b5c6 d7c6 e1g1 e8g8 d2d4 c8g4 d4e5 g4f3 g2f3 d6e5 c1g5 d8e8 d1e2 e8e6 e2e3 e6h3 g5f4 e5f4 e3f4 a8c8 f4e3 f6d7 e3d4

        // e2e4 c7c5 b1c3 b8c6 g1f3 e7e6 f1b5 c6d4 e1g1 a7a6 b5c4 g7g6 d2d3 g8e7 c1f4 e7c6 a1c1 b7b5 c4b3 b5b4 c3a4 a6a5 f1e1 d7d6 f3d4 c5d4 c2c3 b4c3 b2c3 f8g7 c3d4 c6d4 b3c4 e8g8 a4c3 c8b7 c1b1 b7c6 c3e2 a8b8 b1b8 d8b8 e2d4 g7d4 f4h6 b8b2 e1e2 b2b7 h6f8 g8f8 c4b3 f8g7 e2e1 b7b6 e1f1 e6e5 d1c2 c6d7 b3c4 b6d8 f1c1 d4c5 c2d1 d8f6 d1e1 f6g5 c1b1 c5d4 b1b8 h7h5 b8a8 d4c5

        // doesn't find mate
        // position startpos moves e2e4 e7e5 g1f3 g8f6 b1c3 f8d6 f1c4 e8g8 e1g1 b8c6 d2d4 d8e8 d4d5 c6a5 c4d3 d6c5 c3b5 f6g4 b5c7 g4f2 f1f2 c5f2 g1f2 e8e7 c7a8 f7f5 e4f5 e5e4 d1e1 e7c5 c1e3 c5d5 d3e4 d5e4 e1a5 e4c2 f2g1 f8f5 a5a3 f5f8 e3a7 d7d5 a3d6 c2c6 d6c6 b7c6 a7c5 f8d8 a8b6 c8b7 a1d1 d8e8 b2b4 h7h6 h2h4 h6h5 a2a4 e8e4 a4a5 b7a6 f3d4 a6e2 d1d2 e2b5 d4b5 c6b5 b6d5 g8f7 a5a6 e4e1 g1f2 e1a1 d5c7 g7g5 h4g5 f7g6 c5e3 a1a3 a6a7 a3a7 d2d6 g6f5 d6d5 f5g6 e3a7 h5h4


        // position startpos moves d2d4 d7d5 c1f4 c8f5 b1c3 b8c6 a1c1 a8c8 g2g3 e7e6 g1f3 f8d6 f4d6 c7d6 f1g2 g8e7 e1g1 e8g8 e2e3 e6e5 f3h4 f5e6 a2a3 e5d4 e3d4 d8b6 c1b1 b6d4 h4f3 d4d1 b1d1 c8d8 f3g5 e6g4 f2f3 g4f5 c3d5 f5c2 d5e7 c6e7 d1c1 c2d3 f1e1 e7f5 c1d1 d3c2 d1d2 c2a4 g2h3 f5h6 f3f4 d6d5 h3g2 a4c6 e1d1 c6a4 d1e1 a4b3 e1e7 b7b5 e7a7 h6f5 a7a5 h7h6 a5b5 b3c4 g5f7 g8f7 b5b7 f7g6 b2b3 c4a6 b7a7 a6b5 g2d5 d8e8 g3g4 e8e1 g1g2 f5e3 g2f3 e3d5 d2d5 b5c6 f4f5 g6f6 f3f2 c6d5 f2e1 d5b3 h2h3 b3f7 a3a4 f6g5 e1f2 g5h4 f2g2 h6h5 g4h5 h4h5 g2g3 h5g5 a7a5 f8c8 h3h4 g5f6 a5a7 f7e8 a4a5 e8b5 a7b7 b5c6 b7b6 f6f5 a5a6 c6d5 a6a7 c8c3 g3f2 c3a3 b6b5 f5e5 a7a8q a3a8 b5d5 e5d5 h4h5 a8h8 h5h6 g7h6 f2g3 d5e5 g3g4 h6h5 g4g5 h5h4 g5g4 h4h3 g4g3 h3h2 g3g2 h2h1q
        Log($"info string ideal time budget {TimeMan.TimeBudget} ms");

        PVSControl.StartSearch(depth);
    }

    [Conditional("DEBUG"), MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CmdTest() {
        BenchmarkRunner.Run<Benchmarks>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CmdPrint() {
        Game.board.Print();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Log(string msg, LogLevel level = LogLevel.RAW) {
        LogIntoFile(msg, level);
        Output.WriteLine(msg);
    }

    internal static void LogIntoFile(string msg, LogLevel level = LogLevel.RAW) {
        //if (Options.NKLogs) {

        //    // using KryKom's NeoKolors library for fancy logs
        //    // this option can be toggled via the FancyLogs option
        //    try {
        //        switch (level) {
        //            case LogLevel.INFO: NeoKolors.Console.NKDebug.Info(msg); break;
        //            case LogLevel.WARNING: NeoKolors.Console.NKDebug.Warn(msg); break;
        //            case LogLevel.ERROR: NeoKolors.Console.NKDebug.Error(msg); break;

        //            default: NeoKolors.Console.NKDebug.Logger.Info(msg); break;
        //        }
        //    } catch {
        //        Console.WriteLine($"failed to open the log file");
        //    }
        //}
    }
}