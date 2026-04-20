//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// Remove unnecessary suppression
#pragma warning disable IDE0079

// Initialize reference type static fields inline    
#pragma warning disable CA1810
#pragma warning disable CA1305
#pragma warning disable CA1031

using Kreveta.consts;
using Kreveta.evaluation;
using Kreveta.movegen;
using Kreveta.moveorder.history;
using Kreveta.polyglot;
using Kreveta.perft;
using Kreveta.search;
using Kreveta.search.transpositions;
using Kreveta.uci.options;

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

// ReSharper disable StackAllocInsideLoop
// ReSharper disable InvokeAsExtensionMethod
// ReSharper disable InconsistentNaming

namespace Kreveta.uci;

internal static partial class UCI {
    private const int InputBufferSize = 4096;
    
    private  static readonly TextReader Input;
    internal static readonly TextWriter Output;

    // the search thread is shared between both regular search and perft
    private static Thread?        SearchThread;
    internal static volatile bool ShouldAbortSearch;
    
    private static readonly Action<string> CannotStartSearchCallback = delegate(string context) {
        Log($"Search cannot be started - {context}");
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
            if (input is "quit" or "exit")
                return;

// Normalize strings to uppercase? Why the fuck would I do that??
#pragma warning disable CA1308
            var semiProcessed = input
                .Trim()
                .Split(' ')
                .ToList();
#pragma warning restore CA1308
            
            semiProcessed.RemoveAll(string.IsNullOrWhiteSpace);
            ReadOnlySpan<string> tokens = semiProcessed.ToArray();

            // the first token is obviously the command itself
            switch (tokens[0].ToLower(null)) {
                
                // the GUI sends the "ucinewgame" command to inform the engine
                // that it will be playing a whole game, instead of just maybe
                // analyzing a single position. although we don't alter anything
                // yet, it's nice to have the option to do so implemented
                case "ucinewgame":
                    Game.FullGame      = true;
                    Game.PreviousScore = 0;
                    
                    // this resets the potentially stored recapture
                    Game.TryStoreRecapture([], 0);
                    StaticEvalDiffHistory.Clear();
                    
                    TT.Clear();
                    SETT.Realloc();
                    break;
                
                // "uci" tells the GUI that UCI is supported, and also lists available options
                case "uci":
                    // print some engine info for nicer display
                    Log($"id name {Program.Name}-{Program.Version}\n" +
                        $"id author {Program.Author}\n");

                    // and print all modifiable options
                    Options.Print();

                    Log("uciok");
                    break;
                
                // when we receive "isready", we shall respond with "readyok".
                // this signals that we are ready to receive further commands
                case "isready":
                    Log("readyok");
                    break;
                
                // the "setoption ... " command is used to access internal parameters
                case "setoption":
                    Options.Set(tokens);
                    break;
                
                // set up a position
                case "position":
                    CmdPosition(tokens);
                    break;
                
                // start searching the best move
                case "go":
                    CmdGo(tokens);
                    break;
                
                // run perft at a specified depth
                case "perft":
                    CmdPerft(tokens);
                    break;
                
                // run bench
                case "bench":
                    Bench.Run(tokens);
                    break;
                
                // print the current position
                case "d" or "draw" or "display":
                    Game.Board.Print();
                    
                    Log($"FEN:           {Game.Board.FEN()}");
                    Log($"TT hash:       {Game.Board.Hash}");
                    Log($"Polyglot hash: {PolyglotZobristHash.Hash(in Game.Board)}");
                    Log($"Is check:      {Game.Board.IsCheck.ToString().ToLower(null)}\n");
                    break;
                
                // stop any running searches
                case "stop":
                    StopSearch();
                    break;
                
                // clear the console window
                case "cls":
                    Console.Clear();
                    break;
                
                case "tune":
                    Tuning.TuneParams(tokens);
                    break;

                // nicely print the static eval of the position
                case "eval": {
                    Eval.Trace(in Game.Board);
                    break;
                }

                // print all legal moves
                case "moves": {
                    PrintLegalMoves();
                    break;
                }

                // flip the side to move
                case "flip": {
                    Game.Flip();
                    break;
                }
                
                case "license":
                    Log($"\n{Consts.License}\n");
                    break;
                
                case "help" or "-help" or "--help" or "h" or "-h" or "--h":
                    Log("Kreveta is an open-source chess engine, released under the MIT license. Please read the full documentation here: https://github.com/ZlomenyMesic/Kreveta");
                    break;

                default:
                    Log($"Unknown command: \"{tokens[0]}\". Type 'help' for more information");
                    break;
            }
        }
    }

    // "position ..." command sets the current position, which the
    // engine probably will be searching in the future. this doesn't
    // start the search itself, though
    private static void CmdPosition(ReadOnlySpan<string> tokens) {
        if (tokens.Length <= 1) {
            Log("Missing arguments - startpos/fen must be specified");
            return;
        }
        
        switch (tokens[1]) {

            // we CAN'T use Board.CreateStartpos here, since we
            // may have a bunch of moves played from startpos
            case "startpos": Game.SetStartpos(tokens);          break;
            case "fen":      Game.SetPosFEN(tokens);            break;

            default: Log($"Invalid argument: \"{tokens[1]}\""); return;
        }
    }

    // "perft" starts a perft test at a specfied depth. perft
    // (performance test) counts the number of nodes at a certain
    // depth legally achievable from a position. this is important
    // to measure the speed and correctness of movegen
    private static void CmdPerft(ReadOnlySpan<string> tokens) {
        // first stop the potential already running search
        StopSearch(verbose: false);

        // position cannot be searched (mate or stalemate)
        if (Game.IsTerminalPosition(out string reason)) {
            CannotStartSearchCallback.Invoke(reason);
            return;
        }

        if (tokens.Length == 2) {
            if (!int.TryParse(tokens[1], out int depth))
                goto invalid_syntax;

            if (depth < 1) {
                Log("Depth must be greater than or equal to 1");
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
        Log("Invalid perft command syntax");
    }

    internal static void CmdGo(ReadOnlySpan<string> tokens, bool bench = false) {
        // abort the currently running search first in order to
        // run a new one, since there is a single search thread.
        StopSearch(verbose: false);
        
        // position cannot be searched (mate or stalemate)
        if (Game.IsTerminalPosition(out string error)) {
            CannotStartSearchCallback.Invoke(error);
            return;
        }

        // no full game may be played when analysis mode is on
        Game.FullGame &= !Options.UCI_AnalyseMode;

        if (Game.FullGame) {
            Span<Move> legal = stackalloc Move[Consts.MoveBufferSize];
            int count = Movegen.GetLegalMoves(ref Game.Board, legal);

            // when playing a full game, and there is a single legal
            // move, no time is wasted searching, and the move is played
            if (count == 1) {
                Console.WriteLine($"bestmove {legal[0].ToLAN()}");
                return;
            }

            // check if the previous searched expected us to
            // play an obvious recapture, and if yes, play it
            Move recapture = Game.TryGetRecapture();
            if (recapture != default) {
                Console.WriteLine($"bestmove {recapture.ToLAN()}");
                return;
            }
        }

        // this sets the time budget
        TM.ProcessTimeTokens(tokens);

        int depth           = PVSControl.DefaultMaxDepth;
        int depthTokenIndex = MemoryExtensions.IndexOf(tokens, "depth");

        // the depth keyword should be directly followed by a parsable token
        if (depthTokenIndex != -1) {
            try {

                // this whole thing is put into a try-catch statement in case
                // the depth argument doesn't exist at all (index out of range)
                if (!int.TryParse(tokens[depthTokenIndex + 1], out depth))
                    throw new InvalidCastException();

                TM.TimeBudget = long.MaxValue;
            } catch {
                Log("Invalid or missing depth argument");
            }
        }

        if (depth < 1) {
            Log("Depth must be greater than or equal to 1");
            return;
        }

        long nodes           = long.MaxValue;
        int  nodesTokenIndex = MemoryExtensions.IndexOf(tokens, "nodes");

        // same as with depth, the node count has to actually be specified
        if (nodesTokenIndex != -1) {
            try {
                if (!long.TryParse(tokens[nodesTokenIndex + 1], out nodes))
                    throw new InvalidCastException();

                TM.TimeBudget = long.MaxValue;
            } catch {
                Log("Invalid or missing nodes argument");
            }
        }

        // if the user/GUI sends the "searchmoves" argument, we expect a list of legal moves
        // available from the position, and the search will only berestricted to these moves
        Game.SearchMoves.Clear();
        int smIndex = MemoryExtensions.IndexOf(tokens, "searchmoves");
        if (smIndex++ != -1)
            while (tokens.Length > smIndex && Move.IsCorrectFormat(tokens[smIndex]))
                Game.SearchMoves.Add(tokens[smIndex++].ToMove(in Game.Board));

        // don't use book moves when we want an actual search at a specified depth
        // or when movetime is set (either specific search time or infinite time)
        if (depthTokenIndex == -1 && (TM.MoveTime == 0 || TM.TimeBudgetIsDefault) && Options.PolyglotUseBook) {
            Move bookMove = Polyglot.GetBookMove(in Game.Board);
            
            if (bookMove != default) {
                Log($"\nbestmove {bookMove.ToLAN()}");
                return;
            }
        }

        Log($"info string NNUE evaluation using {Program.Network}");
        Log($"info string ideal time budget: {(TM.TimeBudget != long.MaxValue
            ? $"{TM.TimeBudget} ms" 
            : "none")}");

        // the search itself runs as a separate thread to allow processing
        // other commands while the search is running - this usually isn't
        // needed, but the "stop" command is very important
        SearchThread = new Thread(() => PVSControl.StartSearch(depth, nodes, bench)) {
            Name     = $"{Program.Name}-{Program.Version}_Search",
            Priority = ThreadPriority.Highest,
        };
        SearchThread.Start();
    }

    private static void StopSearch(bool verbose = true) {
        // this also checks for null values
        if (SearchThread is null or { IsAlive: false }) {
            if (verbose) Log("No search thread active");
            return;
        }

        ShouldAbortSearch = true;

        // the search is a separate thread, which we first
        // synchronize with this one and then terminate
        SearchThread.Join();
        SearchThread = null;

        ShouldAbortSearch = false;
        
        PVSearch.Reset();
    }

    // on command 'moves' all legal moves are printed, sorted by piece
    private static void PrintLegalMoves() {
        Span<Move> legal  = stackalloc Move[Consts.MoveBufferSize];
        int        count  = Movegen.GetLegalMoves(ref Game.Board, legal);
        var        output = new StringBuilder();

        output.Append($"Total legal moves: {count}\n");

        // sort the moves by piece
        for (int i = 0; i < 6; i++) {
#pragma warning disable CS8509
            output.Append(i switch {
                0 => "\nPawn:  ",
                1 => "\nKnight:",
                2 => "\nBishop:",
                3 => "\nRook:  ",
                4 => "\nQueen: ",
                5 => "\nKing:  "
            });
#pragma warning restore CS8509
            
            // add only the moves that are this piece
            for (int j = 0; j < count; j++)
                if (legal[j].Piece == (PType)i)
                    output.Append($" {legal[j].ToLAN()}");
        }
        
        Log($"{output}\n");
    }
}
    
#pragma warning restore CA1031
#pragma warning restore CA1810
#pragma warning restore CA1305

#pragma warning restore IDE0079