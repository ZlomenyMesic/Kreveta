//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.movegen;
using Kreveta.search.pruning;
using System.Diagnostics;

#nullable enable
namespace Kreveta.search {
    internal static class PVSControl {

        // best move found so far
        internal static Move BestMove;

        // maximum search depth allowed
        private static int MaxDepth;

        internal static Stopwatch sw = new();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static long _curElapsed  = 0L;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static long _prevElapsed = 0L;

        private static long TotalNodes;

        //private static Thread? thread;

        internal static void StartSearch(int depth) {
            MaxDepth = depth;

            // start iterative deepening
            IterativeDeepeningLoop();
        }

        // we are using an approach called iterative deepening. we search the same
        // position multiple times, but at increasingly larger depths. results from
        // previoud iterations are stored in the tt, killers, and history, which
        // makes new iterations not take too much time.
        private static void IterativeDeepeningLoop() {
            _prevElapsed = 0L;

            sw = Stopwatch.StartNew();

            int pieceCount = BB.Popcount(Game.board.Occupied);
            NMP.UpdateMinPly(pieceCount);

            // we still have time and are allowed to search deeper
            while (PVSearch.CurDepth < MaxDepth 
                && sw.ElapsedMilliseconds < TimeMan.TimeBudget) {

                // search at a larger depth
                PVSearch.SearchDeeper();

                // didn't abort (yet?)
                if (!PVSearch.Abort) {

                    _curElapsed = sw.ElapsedMilliseconds - _prevElapsed;

                    // print the results to the console and save the first pv node
                    GetResult();

                    _prevElapsed = sw.ElapsedMilliseconds;

                } else break;
            }

            UCI.Log($"info string time spent {sw.Elapsed}",  UCI.LogLevel.INFO);
            UCI.Log($"info string total nodes {TotalNodes}", UCI.LogLevel.INFO);

            // the final response of the engine to the gui
            UCI.Log($"bestmove {BestMove}");

            // reset all counters for the next search
            // NEXT SEARCH, not the next iteration of the current one
            sw.Stop();
            PVSearch.Reset();
            TotalNodes = 0;
        }

        private static void GetResult() {

            // save the first pv node as the current best move
            BestMove = PVSearch.PV[0];

            TotalNodes += PVSearch.CurNodes;

            // pv score relative to the engine
            int engRelScore = PVSearch.PVScore;

            // pv score relative to color (this gets printed to the gui)
            // although we probably don't need this, it is a common way to do so
            int colRelScore = engRelScore * (Game.color == Color.WHITE ? 1 : -1);

            // nodes per second - a widely used measure to approximate an
            // engine's strength or efficiency. we need to maximize nps.
            // if the time is too low (less than a millisecond), we simply
            // divide as if it took us 1 millisecond.
            long divisor = _curElapsed != 0 ? _curElapsed : 1;
            int nps = (int)((float)PVSearch.CurNodes / divisor * 1000);

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
                $"hashfull {TT.Hashfull()} " +

                // pv score relative to color
                // measured in centipawns (cp)
                $"score cp {colRelScore} " +

                // principal variation
                $"pv";

            // print the actual moves in the pv. Move.ToString()
            // is overriden so there's no need to explicitly type it
            foreach (Move move in GetFullPV(PVSearch.AchievedDepth))
                info += $" {move}";

            // as per the convention, the engine's response
            // shall always end with a newline character
            UCI.Log(info, UCI.LogLevel.INFO);
        }

        // tries to find the pv outside of just the stored array
        private static Move[] GetFullPV(int depth) {
            List<Move> pvList = new(PVSearch.PV);

            // if we want to go deeper than just the saved pv
            if (pvList.Count < depth) {

                Board board = Game.board.Clone();

                // play along the principal variation
                // the correct position is needed for correct tt lookups
                foreach (Move move in PVSearch.PV)
                    board.PlayMove(move);

                // try going deeper through the transposition table
                while (pvList.Count < depth && TT.GetBestMove(board, out Move next)) {
                    board.PlayMove(next);
                    pvList.Add(next);
                }
            }

            // return the (hopefully) elongated pv
            return [.. pvList];
        }
    }
}
