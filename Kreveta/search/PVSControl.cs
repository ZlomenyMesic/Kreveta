//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.evaluation;
using Kreveta.movegen;
using Kreveta.search.pruning;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Diagnostics.Tracing.Parsers.MicrosoftAntimalwareEngine;

// ReSharper disable InconsistentNaming

namespace Kreveta.search {
    internal static class PVSControl {

        internal const int DefaultMaxDepth = 50;

        // maximum search depth allowed in this search
        private static int CurMaxDepth;
        
        // best move found so far
        private static Move BestMove;

        internal static Stopwatch sw = null!;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static long CurElapsed;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static long PrevElapsed;

        private static ulong TotalNodes;

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

            int pieceCount = BB.Popcount(Game.Board.Occupied);
            NullMovePruning.UpdateMinPly(pieceCount);

            // we still have time and are allowed to search deeper
            while (PVSearch.CurDepth < CurMaxDepth 
                && sw.ElapsedMilliseconds < TimeMan.TimeBudget) {

                // search at a larger depth
                PVSearch.SearchDeeper();

                // didn't abort (yet?)
                if (PVSearch.Abort) 
                    break;
                
                CurElapsed = sw.ElapsedMilliseconds - PrevElapsed;

                // print the results to the console and save the first pv node
                GetResult();

                PrevElapsed = sw.ElapsedMilliseconds;
            }

            UCI.Log($"info string time spent {sw.Elapsed}",  UCI.LogLevel.INFO);
            UCI.Log($"info string total nodes {TotalNodes}", UCI.LogLevel.INFO);

            // the final response of the engine to the gui
            UCI.Log($"bestmove {BestMove.ToLongAlgNotation()}");

            // reset all counters for the next search
            // not the next iteration of the current one
            sw.Stop();
            PVSearch.Reset();
            TotalNodes = 0UL;
        }

        private static void GetResult() {

            // save the first pv node as the current best move
            BestMove = PVSearch.PV[0];

            // add the searched nodes from this iteration to the total node count
            TotalNodes += PVSearch.CurNodes;

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
                : $"cp {Score.LimitScore(PVSearch.PVScore) * (Game.EngineColor == Color.WHITE ? 1 : -1)}";

            // nodes per second (searched) - a widely used measure to approximate
            // an engine's strength or efficiency. we need to maximize these. in
            // early iterations the time may actually be less than a millisecond,
            // so we handle that by setting in to 1
            long nodesDivisor = CurElapsed != 0L ? CurElapsed : 1L;
            int nps = (int)((float)PVSearch.CurNodes / nodesDivisor * 1000);

            // we print the search info to the console
            string info = "info " +

                // full search depth
                $"depth {PVSearch.CurDepth} " +

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
                $"pv";

            // print the actual moves in the pv. Move.ToString()
            // is overriden so there's no need to explicitly type it
            foreach (Move move in ElongatePV())
                info += $" {move.ToLongAlgNotation()}";

            // as per the convention, the engine's response
            // shall always end with a newline character
            UCI.Log(info, UCI.LogLevel.INFO);
        }

        // try to find the pv outside the stored array
        private static IEnumerable<Move> ElongatePV() {
            
            Board board = Game.Board.Clone();

            // play along the principal variation.
            // the correct position is needed for correct tt lookups
            foreach (Move move in PVSearch.PV) {
                yield return move;
                board.PlayMove(move);
            }
            
            int depth = PVSearch.PV.Length;

            // try going deeper through the transposition table
            while (TT.TryGetBestMove(board, out Move ttMove)) {
                
                // we don't want to expand the pv beyond the searched
                // depth, because the results might get too unreliable
                if (depth++ > PVSearch.CurDepth)
                    yield break;
                
                yield return ttMove;
                board.PlayMove(ttMove);
            }
        }
    }
}
