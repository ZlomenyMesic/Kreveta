//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// Remove unnecessary suppression
#pragma warning disable IDE0079

// Initialize reference type static fields inline    
#pragma warning disable CA1810

using Kreveta.movegen;
using Kreveta.openings;
using Kreveta.perft;
using Kreveta.search;
using Kreveta.search.transpositions;
using Kreveta.uci.options;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Running;

using Kreveta.consts;
using Kreveta.tuning;

// ReSharper disable InvokeAsExtensionMethod
// ReSharper disable InconsistentNaming

namespace Kreveta.uci;

internal static partial class UCI {
    private const int InputBufferSize = 4096;
    
    private static readonly  TextReader Input;
    internal static readonly TextWriter Output;

    private static Thread? SearchThread;
    internal static volatile bool ShouldAbortSearch;
    
    private static readonly Action<string> CannotStartSearchCallback = delegate(string context) {
        Log($"Couldn't start searching - {context}", LogLevel.ERROR);
    };

    static UCI() {

        // the default Console.ReadLine buffer is quite small and cannot
        // handle long move lines, thus we use a larger buffer size
        Input  = new StreamReader(Console.OpenStandardInput(InputBufferSize));
        Output = Console.Out;
    }

    internal static void InputLoop() {
        while (true) {
            // since we use a custom StreamReader, this should be able to
            // read much longer commands than the usual Console.ReadLine
            string input = Input.ReadLine() ?? string.Empty;

            // to prevent unnecessary bugs
            if (string.IsNullOrWhiteSpace(input))
                continue;

            // quit should terminate the program as soon as possible
            if (input == "quit")
                return;

            ReadOnlySpan<string> tokens = input.Split(' ');

            // we log the input commands as well
            Task.Run(() => LogIntoFile($"USER COMMAND: {input}"));

            // the first token is obviously the command itself
            switch (tokens[0]) {
                
                // the GUI sends the "ucinewgame" command to inform the engine
                // that it will be playing a whole game, instead of just maybe
                // analyzing a single position. although we don't alter anything
                // yet, it's nice to have the option to do so implemented
                case "ucinewgame":
                    Game.FullGame      = true;
                    Game.PreviousScore = 0;
                    
                    TT.Clear();
                    break;
                
                // the "setoption ..." command is used to modify some options
                // in the engine. this is important in many cases when we want
                // to, for instance, disable the opening book
                case "setoption": Options.Set(tokens); break;
                
                // when we receive "isready", we shall respond with "readyok".
                // this signals that we are ready to receive further commands
                case "isready":   Log("readyok");      break;
                case "uci":       CmdUCI();            break;
                case "position":  CmdPosition(tokens); break;
                case "go":        CmdGo(tokens);       break;
                case "perft":     CmdPerft(tokens);    break;
                
                // print the currently set position
                case "d":         Game.Board.Print();  break;
                
                // stop any current searches
                case "stop":      StopSearch();        break;
                
                // run current benchmarks
                case "bench":
                    BenchmarkRunner.Run<Benchmarks>();
                    break;
                
                case "test":
                    Test();
                    break;
                
                case "cls":
                    Console.Clear();
                    break;
                
                case "tune":
                    Tuning.TuneParams(tokens);
                    break;
                
                case "help":
                    Log("Kreveta uses the UCI protocol to communicate with GUIs. Please read the full documentation here: https://github.com/ZlomenyMesic/Kreveta", LogLevel.INFO);
                    break;

                default:
                    Log($"Unknown command: \"{tokens[0]}\". Type 'help' for more information", LogLevel.ERROR);
                    break;
            }
        }
    }

    // when the engine receives the "uci" command, it is supposed
    // to respond with "uciok" to let the GUI know UCI is supported
    private static void CmdUCI() {
        const string UCIOK = "uciok";

        // other than that we also print some engine info for nicer display
        Log($"id name {Program.Name}-{Program.Version}\n" +
            $"id author {Program.Author}\n");

        // and we print all modifiable options
        Options.Print();

        Log($"{UCIOK}");
    }

    // "position ..." command sets the current position, which the
    // engine probably will be searching in the future. this doesn't
    // start the search itself, though
    private static void CmdPosition(ReadOnlySpan<string> tokens) {
        if (tokens.Length <= 1) {
            Log("Missing arguments - startpos/fen must be specified", LogLevel.ERROR);
            return;
        }
        
        switch (tokens[1]) {

            // we CAN'T use Board.CreateStartpos here, since we
            // may have a bunch of moves played from startpos
            case "startpos": Game.SetStartpos(tokens);                      break;
            case "fen":      Game.SetPosFEN(tokens);                        break;

            default: Log($"Invalid argument: \"{tokens[1]}\"", LogLevel.ERROR); return;
        }
    }

    // "perft" starts a perft test at a specfied depth. perft
    // (performance test) counts the number of nodes at a certain
    // depth legally achievable from a position. this is important
    // to measure the speed and correctness of movegen
    private static void CmdPerft(ReadOnlySpan<string> tokens) {
        // first stop the potential already running search
        StopSearch();

        // position cannot be searched (mate or stalemate)
        if (Game.IsTerminalPosition(out string error)) {
            CannotStartSearchCallback.Invoke(error);
            return;
        }

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
                Name     = $"{Program.Name}-{Program.Version}_Perft",
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
        StopSearch();

        // position cannot be searched (mate or stalemate)
        if (Game.IsTerminalPosition(out string error)) {
            CannotStartSearchCallback.Invoke(error);
            return;
        }

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

        if (tokens.Contains("nodes"))
            Log("Node count restrictions are not supported", LogLevel.WARNING);

        // don't use book moves when we want an actual search at a specified depth
        // or when movetime is set (either specific search time or infinite time)
        if (depthTokenIndex == -1 && TimeMan.MoveTime == 0 && Options.PolyglotUseBook) {
            Move bookMove = Polyglot.GetBookMove(Game.Board);
            
            if (bookMove != default) {
                Log($"bestmove {bookMove.ToLAN()}");
                return;
            }
        }

        Log($"info string ideal time budget {TimeMan.TimeBudget} ms");

        // the search itself runs as a separate thread to allow processing
        // other commands while the search is running - this usually isn't
        // needed, but the "stop" command is very important
        SearchThread = new Thread(() => PVSControl.StartSearch(depth)) {
            Name     = $"{Program.Name}-{Program.Version}_Search",
            Priority = ThreadPriority.Highest
        };
        SearchThread.Start();
    }

    private static void StopSearch() {
        // this also checks for null values
        if (SearchThread is { IsAlive: false })
            return;

        ShouldAbortSearch = true;

        // the search is a separate thread, which we first
        // synchronize with this one and then terminate
        SearchThread?.Join();
        SearchThread = null;

        ShouldAbortSearch = false;
    }

    private static void Test() {

    }
}

#pragma warning restore CA1810
#pragma warning restore IDE0079