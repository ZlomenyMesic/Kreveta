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
        internal static Move best_move;

        // maximum search depth allowed
        private static int max_depth;

        internal static long time_budget_ms;
        internal static Stopwatch? sw = new();

        private static long total_nodes;

        //private static Thread? thread;

        internal static void StartSearch(int depth, long time_budget_ms) {
            max_depth = depth;
            PVSControl.time_budget_ms = time_budget_ms;

            // start iterative deepening
            DeepeningSearchLoop();
        }

        // we are using an approach called iterative deepening. we search the same
        // position multiple times, but at increasingly larger depths. results from
        // previoud iterations are stored in the tt, killers, and history, which
        // makes new iterations not take too much time.
        private static void DeepeningSearchLoop() {

            // time spent on previous iteration
            long prev_iter = 0;

            sw = Stopwatch.StartNew();

            int piece_count = BB.Popcount(Game.board.Occupied());
            NMP.UpdateMinPly(piece_count);

            // we still have time and are allowed to search deeper
            while (PVSearch.cur_depth < max_depth 
                && sw.ElapsedMilliseconds < time_budget_ms) {

                // search at a larger depth
                PVSearch.SearchDeeper();

                // didn't abort (yet?)
                if (!PVSearch.Abort) {

                    // print the results to the console and save the first pv node
                    GetResult();

                    // if the time spent on the previous iteration is exceeds a specified
                    // portion of the time budget, we can expect going deeper would definitely
                    // cross it, causing an abortion. not starting the next iteration saves us time
                    prev_iter = sw.ElapsedMilliseconds - prev_iter;
                    if (prev_iter > (time_budget_ms / 3))
                        break;

                } else break;
            }

            UCI.Log($"info string time spent {sw.Elapsed}",   UCI.LogLevel.INFO);
            UCI.Log($"info string total nodes {total_nodes}", UCI.LogLevel.INFO);

            // the final response of the engine to the gui
            UCI.Log($"bestmove {best_move}");

            // reset all counters for the next search
            // NEXT SEARCH, not the next iteration of the current one
            sw = null;
            PVSearch.Reset();
            total_nodes = 0;
        }

        private static void GetResult() {

            // save the first pv node as the current best move
            best_move = PVSearch.PV[0];

            total_nodes += PVSearch.total_nodes;

            // pv score relative to the engine
            int eng_score = PVSearch.pv_score;

            // pv score relative to color (this gets printed to the gui)
            // although we probably don't need this, it is a common way to do so
            int col_score = eng_score * (Game.color == Color.WHITE ? 1 : -1);

            // nodes per second - a widely used measure to approximate an
            // engine's strength or efficiency. we need to maximize nps.
            int nps = (int)(total_nodes / (sw ?? Stopwatch.StartNew()).Elapsed.TotalSeconds);

            // we print the search info to the console
            string info = "info " +

                // full search depth
                $"depth {PVSearch.cur_depth} " +

                // selective search depth - full search + qsearch
                $"seldepth {PVSearch.achieved_depth} " +

                // nodes searched this iteration
                $"nodes {PVSearch.total_nodes} " +

                // nodes per second
                $"nps {nps} " +

                // how full is the hash table (permill)
                $"hashfull {TT.HashFull()} " +

                // pv score relative to color
                // measured in centipawns (cp)
                $"score cp {col_score} " +

                // principal variation
                $"pv";

            // print the actual moves in the pv
            foreach (Move m in GetFullPV(PVSearch.achieved_depth))
                info += $" {m}";

            // as per the convention, the engine's response
            // shall always end with a newline character
            UCI.Log(info, UCI.LogLevel.INFO);
        }

        // tries to find the pv outside of just the stored array
        private static Move[] GetFullPV(int depth) {
            List<Move> pv_list = new(PVSearch.PV);

            // if we want to go deeper than just the saved pv
            if (pv_list.Count < depth) {

                Board b = Game.board.Clone();

                // play along the principal variation
                // the correct position is needed for correct tt lookups
                foreach (Move m in PVSearch.PV)
                    b.PlayMove(m);

                // try going deeper through the transposition table
                while (pv_list.Count < depth && TT.GetBestMove(b, out Move m)) {
                    b.PlayMove(m);
                    pv_list.Add(m);
                }
            }

            // return the (hopefully) elongated pv
            return [.. pv_list];
        }
    }
}
