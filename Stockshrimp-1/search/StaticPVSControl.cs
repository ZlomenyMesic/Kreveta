

using Stockshrimp_1.movegen;
using System.Diagnostics;

#nullable enable
namespace Stockshrimp_1.search {
    internal static class StaticPVSControl {

        // best move found so far
        internal static Move best_move;

        // maximum search depth allowed
        private static int max_depth;

        private static long time_budget_ms;
        private static Stopwatch? sw = new();

        private static long total_nodes;

        //private static Thread? thread;

        internal static void StartSearch(int depth, long time_budget_ms) {
            max_depth = depth;
            StaticPVSControl.time_budget_ms = time_budget_ms;

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

            // we still have time and are allowed to search deeper
            while (StaticPVS.cur_depth < max_depth 
                && sw.ElapsedMilliseconds < time_budget_ms) {

                // search at a larger depth
                StaticPVS.SearchDeeper();

                // didn't abort (yet?)
                if (!StaticPVS.Abort) {

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

            Console.WriteLine($"time spent {sw.Elapsed}");
            Console.WriteLine($"total nodes {total_nodes}");

            // the final response of the engine to the gui
            Console.WriteLine($"bestmove {best_move}");

            // reset all counters for the next search
            // NEXT SEARCH, not the next iteration of the current one
            sw = null;
            StaticPVS.Reset();
            total_nodes = 0;
        }

        private static void GetResult() {

            // save the first pv node as the current best move
            best_move = StaticPVS.PV[0];

            total_nodes += StaticPVS.total_nodes;

            // pv score relative to the engine
            int eng_score = StaticPVS.pv_score;

            // pv score relative to color (this gets printed to the gui)
            // although we probably don't need this, it is a common way to do so
            int col_score = eng_score * (Game.engine_col == 0 ? 1 : -1);

            // nodes per second - a widely used measure to approximate an
            // engine's strength or efficiency. we need to maximize nps.
            int nps = (int)(total_nodes / (sw ?? Stopwatch.StartNew()).Elapsed.TotalSeconds);

            // we print the search info to the console
            Console.Write($"info " +

                // full search depth
                $"depth {StaticPVS.cur_depth} " +

                // selective search depth - full search + qsearch
                $"seldepth {StaticPVS.achieved_depth} " +

                // nodes searched this iteration
                $"nodes {StaticPVS.total_nodes} " +

                // nodes per second
                $"nps {nps} " +

                // pv score relative to color
                // measured in centipawns (cp)
                $"score cp {col_score} " +

                // principal variation
                $"pv");

            // print the actual moves in the pv
            foreach (Move m in GetFullPV(StaticPVS.achieved_depth))
                Console.Write($" {m}");

            // as per the convention, the engine's response
            // shall always end with a newline character
            Console.WriteLine();
        }

        // tries to find the pv outside of just the stored array
        private static Move[] GetFullPV(int depth) {
            List<Move> pv_list = new(StaticPVS.PV);

            // if we want to go deeper than just the saved pv
            if (pv_list.Count < depth) {

                Board b = Game.board.Clone();

                // play along the principal variation
                // the correct position is needed for correct tt lookups
                foreach (Move m in StaticPVS.PV)
                    b.DoMove(m);

                // try going deeper through the transposition table
                while (pv_list.Count < depth && TT.GetBestMove(b, out Move m)) {
                    b.DoMove(m);
                    pv_list.Add(m);
                }
            }

            // return the (hopefully) elongated pv
            return [.. pv_list];
        }
    }
}
