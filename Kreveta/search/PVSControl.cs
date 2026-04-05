//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.evaluation;
using Kreveta.movegen;
using Kreveta.uci;
using Kreveta.uci.options;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

// ReSharper disable InconsistentNaming

namespace Kreveta.search;

internal static class PVSControl {

    internal const int DefaultMaxDepth = 100;

    // maximum search depth allowed in this search
    private static int    CurMaxDepth;
    internal static ulong CurNodesLimit;
        
    // best move found so far
    private static Move BestMove;

    private static long CurElapsed;
    private static long PrevElapsed;

    // this gets incremented simultaneously with PVSearch.CurNodes
    internal static ulong TotalNodes;

    private static float  PVChanges;
    private static float  ScoreDiffs;
    private static int    PrevScore;
    internal static float LastInstability;

    // -1 = aspiration window search failed low
    //  1 = failed high
    private static int AspirationFail;

    internal static Stopwatch Stopwatch = null!;

    internal static void StartSearch(int depth = DefaultMaxDepth, long NodesLimit = long.MaxValue, bool bench = false) {
        CurMaxDepth   = depth;
        CurNodesLimit = (ulong)NodesLimit;

        // start iterative deepening
        IterativeDeepeningLoop(bench);
    }

    // we are using an approach called iterative deepening. we search the same
    // position multiple times, but at increasingly larger depths. results from
    // previoud iterations are stored in the tt, killers, and history, which
    // makes new iterations not take too much time.
    private static void IterativeDeepeningLoop(bool bench = false) {
        PrevElapsed     = 0L;
        Stopwatch       = Stopwatch.StartNew();
        LastInstability = 0L;
            
        // we have to call tt clear here, because the user
        // might have changed the hash size settings, so we
        // need to update the table before the search
        TT.Init();
        
        // null move pruning starts at later plies when closer to endgame
        int pieceCount = (int)ulong.PopCount(Game.Board.Occupied);
        PVSearch.MinNMPPly = Math.Max(
            Math.Max(3, (32 - pieceCount) / 6),
            Game.Ply / 25
        );
        
        // evaluation noise
        Eval.EvalEntropy = (int)(
            0.119f * MathF.Pow(2227.0f - Options.UCI_Elo, 1.225f)
        );

        // we still have time and are allowed to search deeper
        while (PVSearch.CurIterDepth            < CurMaxDepth
               && Stopwatch.ElapsedMilliseconds < TM.TimeBudget
               && TotalNodes                    < CurNodesLimit) {

            PVSearch.NextBestMove = default;

            Window aspiration   = Window.Infinite;
            bool   isAspiration = false;
            
            // these have to be aged out to allow new information to be considered
            PVChanges  *= 0.69f;
            ScoreDiffs *= 1.05f;
            
            // calculate the instability of the best move, and the score
            float pvInstability    = PVChanges * PVChanges * PVChanges * 1.46f;
            float scoreInstability = PVSearch.CurIterDepth != 0 
                ? ScoreDiffs / PVSearch.CurIterDepth : 0f;

            // somehow combine the two into a total search instability metric.
            // it starts negative, as when the search is stable, the time budget
            // and aspiration window deltas have to be reduced
            float totalInstability = -5.42f + 0.96f * scoreInstability + 0.99f * pvInstability;
            LastInstability        = totalInstability;
            
            // try to reduce or increase the time budget based on instability
            if (PVSearch.CurIterDepth > 3 && totalInstability != 0f) 
                TM.AccountForInstability(totalInstability, PVSearch.CurIterDepth);
            
            if (PVSearch.CurIterDepth > 3 && totalInstability <= -2.49f) {
                int delta = 38 - (int)(totalInstability * 0.97f)
                               + Math.Abs(PrevScore) / 52;

                aspiration = new Window(
                    alpha: (short)(PVSearch.PVScore - delta),
                    beta:  (short)(PVSearch.PVScore + delta));

                switch (AspirationFail) {
                    case -1: aspiration.Alpha = short.MinValue; break;
                    case  1: aspiration.Beta  = short.MaxValue; break;
                }

                isAspiration = true;
            }
            
            // search at a larger depth
            PVSearch.SearchDeeper(aspiration);

            // search aborted - don't print current iteration result
            if (PVSearch.Abort)
                break;
                
            CurElapsed     = Stopwatch.ElapsedMilliseconds - PrevElapsed;
            AspirationFail = 0;

            // aspiration window search failed low
            if (isAspiration && PVSearch.PVScore <= aspiration.Alpha) 
                AspirationFail = -1;
            
            // failed high
            if (isAspiration && PVSearch.PVScore >= aspiration.Beta)
                AspirationFail = 1;

            if (AspirationFail != 0) {
                PrevScore   = PVSearch.PVScore;
                PrevElapsed = Stopwatch.ElapsedMilliseconds;

                continue;
            }

            // print the results to the console and save the first pv node
            GetResult();
            
            ScoreDiffs += Math.Min(1000, Math.Abs(PVSearch.PVScore - PrevScore));
            PrevScore   = PVSearch.PVScore;

            // when playing a full game (ucinewgame), and the pv score is
            // mate (doesn't matter whether for us or for the opponent), we
            // can stop the search to avoid wasting time
            if (Game.FullGame && Score.IsMate(PVSearch.PVScore))
                break;
            
            PrevElapsed = Stopwatch.ElapsedMilliseconds;
        }

        // fastchess wants us to print last info when force-stopping a search
        if (PVSearch.Abort) {
            if (PVSearch.PVScore == 0)
                PVSearch.PVScore = (short)PrevScore;
            
            GetResult();
        }
       
        long time = Stopwatch.ElapsedMilliseconds == 0 ? 1 : Stopwatch.ElapsedMilliseconds;
        
        // statistics can be turned off via the "PrintStats" option
        UCI.LogStats(forcePrint: false,
            ("Nodes Searched", TotalNodes),
            ("Time Spent",     Stopwatch.Elapsed),
            ("Average NPS",    (int)Math.Round((decimal)TotalNodes / time * 1000, 0)),
            ("TT Hits",        TT.TTHits)
        );
        
        // even if the search was aborted during the latest iteration,
        // as long as we have found a good move, it can be trusted
        if (PVSearch.NextBestMove != default && AspirationFail == 0)
            BestMove = PVSearch.NextBestMove;

        // in very rare cases if the search is so short that not even a depth
        // 1 iteration could be finished (such as 'go nodes 1' on startpos),
        // the resulting move is selected randomly to not use on illegal play
        if (BestMove == default) {
            Span<Move> buffer = stackalloc Move[Consts.MoveBufferSize];
            _ = Movegen.GetLegalMoves(ref Game.Board, buffer);

            BestMove = buffer[0];
            UCI.Log("info string best move selected randomly");
        }

        // the final response of the engine to the GUI
        UCI.Log($"bestmove {BestMove.ToLAN()}");
        
        // store this score for the next turn when playing a full game
        if (Game.FullGame)
            Game.PreviousScore = PVSearch.PVScore;

        // bench needs the node count
        if (bench) Bench.Nodes += TotalNodes;
        
        Stopwatch.Stop();
        
        // reset all counters for the next search
        // (not the next iteration of the current one)
        PVSearch.Reset();
        
        TotalNodes = 0UL;
        PVChanges  = 0;
        PrevScore  = 0;
        ScoreDiffs = 0;
        BestMove   = default;

        if (bench) Bench.Finished = true;
    }

    private static void GetResult() {

        // save the first pv node as the current best move
        if (BestMove != default && PVSearch.PV.Length != 0 && BestMove != PVSearch.PV[0])
            PVChanges++;
        
        // PV becomes unreliable when aborting the search
        if (!PVSearch.Abort && PVSearch.PV.Length != 0)
            BestMove = PVSearch.PV[0];
        
        // check if we expect the opponent to capture one of
        // our pieces, and have an immediate obvious recapture
        Game.TryStoreRecapture(PVSearch.PV, PVSearch.CurIterDepth);

        // now there's a bit of magic with mate scores. our "mate in X" function
        // returns the number of plies until mate, but the conventional way to
        // note mate scores is actually the number of full moves.
        int mateScore = Score.GetMateInX(PVSearch.PVScore);
        
        // add one if the score is odd to make it divisible by two
        mateScore += Math.Abs(mateScore) % 2 * Math.Sign(mateScore);

        // and then we divide the score by two to get the conventional "mate in X",
        // while also multiplying it to make it relative to the engine, not color
        mateScore /= Game.EngineColor == Color.WHITE ? 2 : -2;

        // all the stuff above is done even if the score isn't mate. i'm just
        // too lazy to care, but i might modify it a bit in the future. so here
        // we just check whether the pv score is mate or not, and based on that
        // we either print the "mate in X" or the score in centipawns
        string score = Score.IsMate(PVSearch.PVScore) 
            ? $"mate {mateScore}"
            : $"cp {Score.LimitScore(PVSearch.PVScore) 
                    * (Game.EngineColor == Color.WHITE ? 1 : -1)}";

        // nodes per second (searched) - a widely used measure to approximate
        // an engine's strength or efficiency. we need to maximize these. in
        // early iterations the time may actually be less than a millisecond,
        // so we handle that by setting in to 1
        long time = CurElapsed != 0L ? CurElapsed : 1L;
        int  nps  = (int)((float)PVSearch.CurNodes / time * 1000);

        // we print the search info to the console
        string info = "info " +

                      // full search depth
                      $"depth {PVSearch.CurIterDepth} " +

                      // selective search depth - full search + qsearch
                      $"seldepth {PVSearch.AchievedDepth} " +

                      // nodes searched this iteration
                      $"nodes {PVSearch.CurNodes} " +

                      // nodes per second
                      $"nps {nps} " +

                      // total time spent so far
                      $"time {Stopwatch.ElapsedMilliseconds} " +

                      // how full is the hash table (permill)
                      $"hashfull {TT.Hashfull} " +

                      // pv score relative to color
                      // measured in centipawns (cp)
                      $"score {score} " +

                      // principal variation
                      "pv";

        // print the actual moves in the pv
        if (!PVSearch.Abort)
            info = ElongatePV(PVSearch.PV).Aggregate(info,
                (current, move) => current + $" {move.ToLAN()}");
        else info += $" {BestMove.ToLAN()}";

        // as per the convention, the engine's response
        // must be terminated by a newline character
        UCI.Log(info);
    }
    
    // try to find the pv outside the stored array
    private static IEnumerable<Move> ElongatePV(Move[] pv) {
        Board       board  = Game.Board.Clone();
        List<ulong> remove = [];
        
        // play along the principal variation.
        // the correct position is needed for correct tt lookups
        foreach (Move move in pv) {
            yield return move;
            board.PlayMove(move, false);
            
            remove.Add(board.Hash);
            if (ThreeFold.AddAndCheck(board.Hash))
                goto clearThreeFold;
        }
            
        int depth = pv.Length;

        // try going deeper through the transposition table
        while (TT.TryGetBestMove(board.Hash, depth, out Move ttMove, out _, out _, out _)) {
                
            // we don't want to expand the pv beyond the searched
            // depth, because the results might get too unreliable
            if (depth++ > PVSearch.CurIterDepth || ttMove == default)
                goto clearThreeFold;
                
            board.PlayMove(ttMove, false);
            
            // picking up moves from TT sometimes creates infinite loops, that would
            // actually end in a draw. ThreeFold is used here to make sure the PV is
            // actually legal, so if a draw is encountered, the PV is ended. we must
            // make sure to actually remove the hashes from ThreeFold, too.
            remove.Add(board.Hash);
            if (ThreeFold.AddAndCheck(board.Hash))
                goto clearThreeFold;
            
            yield return ttMove;
        }
        
        // clear the threefold for next search iteration
        clearThreeFold:
        foreach (var hash in remove)
            ThreeFold.Remove(hash);
    }
}