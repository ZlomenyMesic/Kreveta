//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// Remove unnecessary suppression
#pragma warning disable IDE0079

using Kreveta.openingbook;
using Kreveta.search;
using Kreveta.perft;
using Kreveta.consts;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Globalization;

using BenchmarkDotNet.Running;
using Kreveta.movegen;
using NeoKolors.Console;

// ReSharper disable InvokeAsExtensionMethod
// ReSharper disable InconsistentNaming

namespace Kreveta;

internal static class UCI {
    internal enum LogLevel : byte {
        INFO, WARNING, ERROR, RAW
    }

    [ReadOnly(true)]
    private const int InputBufferSize = 4096;

    [ReadOnly(true)]
    private static readonly TextReader Input;

    [ReadOnly(true)]
    internal static readonly TextWriter Output;

    private const string NKLogFilePath = @".\out.log";

    private static Thread? SearchThread;
    internal static bool AbortSearch;
    
    private static readonly Action<string> CannotStartSearchCallback = delegate(string context) {
        Log($"Couldn't start searching - {context}", LogLevel.ERROR);
    };

    // Initialize reference type static fields inline
#pragma warning disable CA1810

    static UCI() {

#pragma warning restore CA1810

        // the default Console.ReadLine buffer is quite small and cannot
        // handle long move lines, thus we use a larger buffer size
        Input = new StreamReader(Console.OpenStandardInput(InputBufferSize));
        Output = Console.Out;

        try {
            var nkOutput = new StreamWriter(NKLogFilePath);

            NKDebug.Logger.Output         = nkOutput;
            NKDebug.Logger.SimpleMessages = true;
        }

        // we are catching a "general exception type", because we have
        // zero idea, which type of exception NeoKolors might throw.
        catch (Exception ex)
            when (LogException("NKLogger initialization failed", ex)) { }
    }

    internal static void InputLoop() {
        while (true) {

            // since we use a custom StreamReader, this should be able to
            // read much longer commands than the usual Console.ReadLine
            string input = Input.ReadLine()
                ?? string.Empty;

            // to prevent unnecessary bugs
            if (string.IsNullOrWhiteSpace(input))
                continue;

            ReadOnlySpan<string> tokens = input.Split(' ');

            // we log the input commands as well
            Task.Run(() => LogIntoFile($"USER COMMAND: {input}"));

            // the first token is obviously the command itself
            switch (tokens[0]) {
                case "uci":        CmdUCI();             break;
                case "isready":    CmdIsReady();         break;
                case "setoption":  CmdSetOption(tokens); break;
                case "ucinewgame": CmdUciNewGame();      break;
                case "position":   CmdPosition(tokens);  break;
                case "go":         CmdGo(tokens);        break;
                case "perft":      CmdPerft(tokens);     break;
                case "d":          CmdDisplay();         break;
                case "stop":       CmdStop();            break;

#if DEBUG
                case "bench":      CmdBench();           break;
#endif

                case "quit":                             return;

                default:
                    Log($"Unknown command: {tokens[0]}. Type 'help' for more information", LogLevel.ERROR);
                    break;
            }
        }
    }

    // when the engine receives the "uci" command, it is supposed
    // to respond with "uciok" to let the GUI know UCI is supported
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CmdUCI() {
        const string UCIOK = "uciok";

        // other than that we also print some engine info for nicer display
        Log($"id name {Engine.Name}-{Engine.Version}\n" +
            $"id author {Engine.Author}\n");

        // and we print all modifiable options
        Options.Print();

        Log($"{UCIOK}");
    }

    // when we receive "isready", we shall respond with "readyok".
    // this signals that we are ready to receive further commands
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CmdIsReady() {
        const string ReadyOK = "readyok";

        Log($"{ReadyOK}");
    }

    // the GUI sends the "ucinewgame" command to inform the engine
    // that it will be playing a whole game, instead of just maybe
    // analyzing a single position. although we don't alter anything
    // yet, it's nice to have the option to do so implemented
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CmdUciNewGame() {
        Game.FullGame = true;
        TT.Clear();
    }

    // the command "stop" tells us we should immediately stop the
    // search. we must still report the best move found, though.
    // (this is also used to stop a perft search)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CmdStop() {

        // the search is a separate thread, which we first
        // synchronize with this one and then terminate

        // this also checks for null values
        if (SearchThread is { IsAlive: false })
            return;

        AbortSearch = true;

        // synchronize the threads
        SearchThread?.Join();
        SearchThread = null;

        AbortSearch = false;

        TT.Clear();
        PerftTT.Clear();
    }

    // the "setoption ..." command is used to modify some options
    // in the engine. this is important in many cases when we want
    // to, for instance, disable the opening book
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CmdSetOption(ReadOnlySpan<string> tokens) {
        Options.SetOption(tokens);
    }

    // "position ..." command sets the current position, which the
    // engine probably will be searching in the future. this doesn't
    // start the search itself
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CmdPosition(ReadOnlySpan<string> tokens) {
        switch (tokens[1]) {

            // we don't use a startpos constructor, because we can have a list
            // of moves played from the starting position, which would be quite
            // difficult and unnecessary to implement
            case "startpos": Game.SetPosFEN(tokens: ["", "", .. Consts.StartposFEN.Split(' '), .. tokens]); break;
            case "fen":      Game.SetPosFEN(tokens);                                                        break;

            default: Log($"Invalid argument: {tokens[1]}", LogLevel.ERROR);                                 return;
        }
    }

    // "perft" starts a perft test at a specfied depth. perft
    // (performance test) counts the number of nodes at a certain
    // depth legally achievable from a position. this is important
    // to measure the speed and correctness of movegen
    private static void CmdPerft(ReadOnlySpan<string> tokens) {

        // first stop the potential already running search
        CmdStop();

        // position cannot be searched (mate or stalemate)
        if (CheckTerminalPosition())
            return;

        if (tokens.Length == 2) {
            if (!int.TryParse(tokens[1], out int depth))
                goto invalid_syntax;

            if (depth < 1) {
                Log("Depth must be greater than or equal to 1", LogLevel.ERROR);
                return;
            }

            // we launch a separate thread for this to allow "stop" command
            // and anything else. i don't know, it's just better
            SearchThread = new Thread(() => Perft.Run(depth)) {
                Name     = $"{Engine.Name}-{Engine.Version}.PerftSearch",
                Priority = ThreadPriority.Highest
            };

            SearchThread.Start();

            return;
        }

        invalid_syntax:
        Log("Invalid perft command syntax", LogLevel.ERROR);
    }

    private static void CmdGo(ReadOnlySpan<string> tokens) {

        // abort the currently running search first in order to
        // run a new one, since there is a single search thread.
        CmdStop();

        // position cannot be searched (mate or stalemate)
        if (CheckTerminalPosition())
            return;

        // this sets the time budget
        TimeMan.ProcessTimeTokens(tokens);

        int depth           = PVSControl.DefaultMaxDepth;
        int depthTokenIndex = MemoryExtensions.IndexOf(tokens, "depth");

        // the depth keyword should be directly followed by a parsable token
        if (depthTokenIndex != -1) {
            try {

                // this whole thing is put into a try-catch statement in case
                // the depth argument doesn't exist at all (index out of range)
                if (!int.TryParse(tokens[depthTokenIndex + 1], out depth))
                    throw new InvalidCastException();

                TimeMan.TimeBudget = long.MaxValue;
            } catch (Exception ex)
                  when (LogException("Invalid depth argument", ex)) { }
        }

        if (depth < 1) {
            Log("Depth must be greater than or equal to 1", LogLevel.ERROR);
            return;
        }

        // don't use book moves when we want an actual search at a specified depth
        // or when movetime is set (either specific search time or infinite time)
        if (depthTokenIndex == -1 && TimeMan.MoveTime == 0 && Options.OwnBook && !string.IsNullOrEmpty(OpeningBook.BookMove)) {
            Log($"bestmove {OpeningBook.BookMove}");
            return;
        }

        Log($"info string ideal time budget {TimeMan.TimeBudget} ms");

        // the search itself runs as a separate thread to allow processing
        // other commands while the search is running - this usually isn't
        // needed, but the "stop" command is very important
        SearchThread = new Thread(() => PVSControl.StartSearch(depth)) {
            Name     = $"{Engine.Name}-{Engine.Version}.Search",
            Priority = ThreadPriority.Highest
        };
        SearchThread.Start();
    }

    // run benchmarks
    [Conditional("DEBUG"), MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CmdBench() {
        BenchmarkRunner.Run<Benchmarks>();
    }

    // print the currectly set position
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CmdDisplay() {
        Game.Board.Print();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Log(string msg, LogLevel level = LogLevel.RAW, bool logIntoFile = true) {
        if (logIntoFile)
            Task.Run(() => LogIntoFile(msg, level));

        Output.WriteLine(msg);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // ReSharper disable once MemberCanBePrivate.Global
    internal static bool LogException(string context, Exception ex, bool logIntoFile = true) {
        Log($"{context}: {ex.Message}", LogLevel.ERROR, logIntoFile);
        return true;
    }

    internal static void LogStats(bool forcePrint, params ReadOnlySpan<(string Name, object Data)> stats) {
        const string StatsHeader = "---STATS-------------------------------";
        const string StatsAfter  = "---------------------------------------";

        // printing statistics can be toggled via the PrintStats option.
        // printing can, however, be forced when we are for example printing
        // perft results (or else perft would be meaningless)
        if (!Options.PrintStats && !forcePrint)
            return;

        int DataOffset = StatsHeader.Length - 16;

        Log(StatsHeader);

        foreach (var stat in stats) {
            string  name = stat.Name + new string(' ', DataOffset - stat.Name.Length);
            string? data = stat.Data.ToString();

            if (data is null)
                return;

            if (stat.Data is long or ulong or int) {
                var format = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
                format.NumberGroupSeparator = ",";

                data = Convert.ToInt64(stat.Data, null).ToString("N0", format);
            }

            Log($"{name}{data}");
        }

        Log(StatsAfter);
    }

    // combining sync and async code is generally a bad idea, but we must avoid slowing
    // down the code if something takes too long in NK (although it's probably unlikely)
    private static async Task LogIntoFile(string msg, LogLevel level = LogLevel.RAW) {
        if (!Options.NKLogs)
            return;

        // using KryKom's NeoKolors library for fancy logs
        // this option can be toggled via the FancyLogs option
        try {

            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (level) {
                case LogLevel.INFO:    await Task.Run(() => NKDebug.Logger.Info(msg)).ConfigureAwait(false);  break;
                case LogLevel.WARNING: await Task.Run(() => NKDebug.Logger.Warn(msg)).ConfigureAwait(false);  break;
                case LogLevel.ERROR:   await Task.Run(() => NKDebug.Logger.Error(msg)).ConfigureAwait(false); break;

                default:               await Task.Run(() => NKDebug.Logger.Info(msg)).ConfigureAwait(false);  break;
            }
        } catch (Exception ex)
              when (LogException("NKLogger failed when logging into file", ex, false)) { }
    }
    
    // sometimes, the user might try to search a position that's either illegal,
    // or already decided, so we must check for these cases to prevent crashes
    private static bool CheckTerminalPosition() {

        // either of the kings is missing (this needs to be evaluated first, because
        // everything else stands on top of the assumption that both kings are present)
        byte wKings = (byte)ulong.PopCount(Game.Board.Pieces[(byte)Color.WHITE][(byte)PType.KING]);
        byte bKings = (byte)ulong.PopCount(Game.Board.Pieces[(byte)Color.BLACK][(byte)PType.KING]);
        
        if (wKings != 1) {
            CannotStartSearchCallback($"{wKings} white kings on the board");
            return true;
        }
        
        if (bKings != 1) {
            CannotStartSearchCallback($"{bKings} black kings on the board");
            return true;
        }

        // no legal moves for the engine in this position
        if (Movegen.GetLegalMoves(Game.Board).Length == 0) {
            CannotStartSearchCallback(Movegen.IsKingInCheck(Game.Board, Game.EngineColor)
            
                // if we are in check and have no legal moves, that means
                // we are already checkmated and thus cannot search anything
                ? "the engine is checkmated"
                
                // otherwise we are stalemated and also cannot search
                : "the engine is stalemated");
            
            return true;
        }
        
        // if the opposite side is in check, even though it's our turn to play,
        // the position is obviously illegal and shouldn't be searched (no bugs
        // should appear, but this is just in case)
        if (Movegen.IsKingInCheck(Game.Board, Game.EngineColor == Color.WHITE
                ? Color.BLACK 
                : Color.WHITE)) {
            
            CannotStartSearchCallback("the opposite side is in check");
            return true;
        }
            
        return false;
    }
}

#pragma warning restore IDE0079