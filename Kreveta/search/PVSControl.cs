//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.evaluation;
using Kreveta.movegen;
using Kreveta.moveorder.history.corrections;
using Kreveta.search.transpositions;
using Kreveta.uci;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

// ReSharper disable InconsistentNaming

namespace Kreveta.search;

internal static class PVSControl {

    internal const int DefaultMaxDepth = 100;

    // maximum search depth allowed in this search
    private static int CurMaxDepth;
        
    // best move found so far
    private static Move BestMove;

    private static long CurElapsed;
    private static long PrevElapsed;

    // this gets incremented simultaneously with PVSearch.CurNodes
    internal static ulong TotalNodes;

    private static float PVChanges;
    private static float ScoreDiffs;
    private static int   PrevScore;

    // -1 = aspiration window search failed low
    //  1 = failed high
    private static int AspirationFail;

    internal static Stopwatch sw = null!;

    internal static void StartSearch(int depth = DefaultMaxDepth) {
        CurMaxDepth = depth;

        // start iterative deepening
        IterativeDeepeningLoop();
    }

    // we are using an approach called iterative deepening. we search the same
    // position multiple times, but at increasingly larger depths. results from
    // previoud iterations are stored in the tt, killers, and history, which
    // makes new iterations not take too much time.
    private static void IterativeDeepeningLoop() {
        PrevElapsed = 0L;
        sw = Stopwatch.StartNew();
            
        // we have to call tt clear here, because the user
        // might have changed the hash size settings, so we
        // need to update the table before the search
        TT.Init();
        PawnCorrections.Realloc();
        KingCorrections.Clear();
        
        int pieceCount = (int)ulong.PopCount(Game.Board.Occupied);
        PVSearch.MinNMPPly = Math.Max(3, (32 - pieceCount) / 6);

        // we still have time and are allowed to search deeper
        while (PVSearch.CurIterDepth < CurMaxDepth 
               && sw.ElapsedMilliseconds < TimeMan.TimeBudget) {

            PVSearch.NextBestMove = default;

            Window aspiration   = Window.Infinite;
            bool   isAspiration = false;
            
            PVChanges  *= 0.7f;
            ScoreDiffs *= 0.9f;
            
            float scoreInstability = PVSearch.CurIterDepth != 0 
                ? ScoreDiffs / PVSearch.CurIterDepth
                : 0f;

            float pvInstability    = PVChanges * PVChanges * PVChanges * 1.5f;
            float totalInstability = -6f + scoreInstability + pvInstability;
            
            // try to reduce or increase the time budget based on instability
            if (PVSearch.CurIterDepth > 3 && totalInstability != 0f) 
                TimeMan.AccountForInstability(totalInstability, PVSearch.CurIterDepth);
            
            /*if (PVSearch.CurIterDepth >= 2 && TimeMan.TimeBudget < 250) { 
                int delta = (int)(8 + totalInstability * 2.5f - Math.Min(8, PVSearch.CurIterDepth));
                delta     = Math.Clamp(delta, -1000, 1000);

                aspiration = new Window(
                    alpha: (short)(PVSearch.PVScore - delta),
                    beta:  (short)(PVSearch.PVScore + delta));

                switch (AspirationFail) {
                    case -1: aspiration.Alpha = short.MinValue; break;
                    case  1: aspiration.Beta  = short.MaxValue; break;
                }

                isAspiration = true;
            }*/
            
            // search at a larger depth
            PVSearch.SearchDeeper(aspiration);

            // search aborted - don't print current iteration result
            if (PVSearch.Abort)
                break;
                
            CurElapsed     = sw.ElapsedMilliseconds - PrevElapsed;
            AspirationFail = 0;

            // aspiration window search failed low
            /*if (isAspiration && PVSearch.PVScore <= aspiration.Alpha) 
                AspirationFail = -1;
            
            // failed high
            if (isAspiration && PVSearch.PVScore >= aspiration.Beta)
                AspirationFail = 1;

            if (AspirationFail != 0) {
                PrevScore = PVSearch.PVScore;
                PrevElapsed = sw.ElapsedMilliseconds;

                continue;
            }*/

            // print the results to the console and save the first pv node
            GetResult();
            
            ScoreDiffs += Math.Min(1000, Math.Abs(PVSearch.PVScore - PrevScore));
            PrevScore   = PVSearch.PVScore;

            // try to increase the time budget if the score from the previous
            // turn seems to be significantly different from the current one
            //TimeMan.TryIncreaseTimeBudget();

            // when playing a full game (ucinewgame), and the pv score is
            // mate (doesn't matter whether for us or for the opponent), we
            // can stop the search to avoid wasting time
            if (Game.FullGame && Score.IsMateScore(PVSearch.PVScore))
                break;
            
            PrevElapsed = sw.ElapsedMilliseconds;
        }
       
        long time = sw.ElapsedMilliseconds == 0 ? 1 : sw.ElapsedMilliseconds;
        
        // statistics can be turned off via the "PrintStats" option
        UCI.LogStats(forcePrint: false,
            ("Nodes Searched",         TotalNodes),
            ("Time Spent",             sw.Elapsed),
            ("Average NPS",            (int)Math.Round((decimal)TotalNodes / time * 1000, 0)),
            ("TT Hits",                TT.TTHits)
        );
        
        if (PVSearch.NextBestMove != default && AspirationFail == 0)
            BestMove = PVSearch.NextBestMove;

        // the final response of the engine to the gui
        UCI.Log($"bestmove {BestMove.ToLAN()}");
        
        // store this score for the next turn when playing a full game
        if (Game.FullGame)
            Game.PreviousScore = PVSearch.PVScore;
        
        sw.Stop();
        
        // reset all counters for the next search
        // (not the next iteration of the current one)
        PVSearch.Reset();
        
        TotalNodes = 0UL;
        PVChanges  = 0;
        PrevScore  = 0;
        ScoreDiffs = 0;
    }

    private static void GetResult() {

        // save the first pv node as the current best move
        if (BestMove != default && BestMove != PVSearch.PV[0])
            PVChanges++;
        BestMove = PVSearch.PV[0];

        // now there's a bit of magic with mate scores. our "mate in X" function
        // returns the number of plies until mate, but the conventional way to
        // note mate scores is actually the number of full moves.
        int mateScore = Score.GetMateInX(PVSearch.PVScore);

        // first we add one to the found mate score - this is because
        // we have not added the first ply into this score
        mateScore += Math.Sign(mateScore);

        // after that we subtract one if the score is odd to make
        // it properly divisible by two
        mateScore -= Math.Abs(mateScore) % 2 * Math.Sign(mateScore);

        // and then we divide the score by two to get the conventional "mate in X",
        // while also multiplying it to make it relative to the engine, not color
        mateScore /= Game.EngineColor == Color.WHITE ? 2 : -2;

        // all the stuff above is done even if the score isn't mate. i'm just
        // too lazy to care, but i might modify it a bit in the future. so here
        // we just check whether the pv score is mate or not, and based on that
        // we either print the "mate in X" or the score in centipawns
        string score = Score.IsMateScore(PVSearch.PVScore) 
            ? $"mate {mateScore}"
            : $"cp {Score.LimitScore(PVSearch.PVScore) 
                    * (Game.EngineColor == Color.WHITE ? 1 : -1)}";

        // nodes per second (searched) - a widely used measure to approximate
        // an engine's strength or efficiency. we need to maximize these. in
        // early iterations the time may actually be less than a millisecond,
        // so we handle that by setting in to 1
        long nodesDivisor = CurElapsed != 0L ? CurElapsed : 1L;
        int  nps = (int)((float)PVSearch.CurNodes / nodesDivisor * 1000);

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
                      $"time {sw.ElapsedMilliseconds} " +

                      // how full is the hash table (permill)
                      $"hashfull {TT.Hashfull} " +

                      // pv score relative to color
                      // measured in centipawns (cp)
                      $"score {score} " +

                      // principal variation
                      "pv";

        // print the actual moves in the pv. Move.ToString()
        // is overriden so there's no need to explicitly type it
        info = ElongatePV().Aggregate(info, 
            (current, move) => current + $" {move.ToLAN()}");

        // as per the convention, the engine's response
        // must be terminated by a newline character
        UCI.Log(info, UCI.LogLevel.INFO);
    }

    // try to find the pv outside the stored array
    private static IEnumerable<Move> ElongatePV() {
        Board board = Game.Board.Clone();

        // play along the principal variation.
        // the correct position is needed for correct tt lookups
        foreach (Move move in PVSearch.PV) {
            yield return move;
            board.PlayMove(move, false);
        }
            
        int depth = PVSearch.PV.Length;

        // try going deeper through the transposition table
        while (TT.TryGetBestMove(board.Hash, out Move ttMove)) {
                
            // we don't want to expand the pv beyond the searched
            // depth, because the results might get too unreliable
            if (depth++ > PVSearch.CurIterDepth)
                yield break;
                
            yield return ttMove;
            board.PlayMove(ttMove, false);
        }
    }
}