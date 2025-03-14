/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

using Stockshrimp_1.movegen;
using System.Diagnostics;
using System.Runtime.CompilerServices;


namespace Stockshrimp_1.search {
    internal static class PVSControl {

        // current best move found
        internal static Move best_move = default;

        private static int max_depth = 0;

        private static int time_budget_ms = 0;

        private static Thread? thread = null;

        internal static void StartSearch(int depth, int time_budget_ms) {
            Console.WriteLine("search started");

            // set some values
            max_depth = depth;
            PVSControl.time_budget_ms = time_budget_ms;

            // start the actual search loop
            thread = new(DeepeningSearchLoop) {
                Priority = ThreadPriority.Highest
            };
            thread.Start();
        }

        private static void DeepeningSearchLoop() {

            long prev_iter = 0;

            Stopwatch sw = Stopwatch.StartNew();

            // progressively increment the depth and do the search
            while (PVSearch.cur_depth < max_depth 
                && sw.ElapsedMilliseconds < time_budget_ms) {

                // search at current depth
                PVSearch.SearchDeeper();

                // should end the search?
                if (PVSearch.Abort)
                    break;

                // once again print the current principal variation
                GetResult();

                // if last iteration took a long time, don't start another
                prev_iter = sw.ElapsedMilliseconds - prev_iter;
                if (prev_iter > time_budget_ms / 3) break;
            }

            Console.WriteLine($"bestmove {best_move}");
            Console.WriteLine($"time spent: {sw.Elapsed}");

            PVSearch.Reset();
            thread = null;
        }

        private static void GetResult() {
            best_move = PVSearch.PV[0];

            Console.Write($"info depth: {PVSearch.cur_depth} seldepth: {PVSearch.achieved_depth} nodes: {PVSearch.total_nodes} pv: ");

            foreach (Move m in GetFullPV(PVSearch.achieved_depth))
                Console.Write($"{m} ");

            Console.WriteLine();
        }

        private static Move[] GetFullPV(int depth) {
            List<Move> pv_list = new(PVSearch.PV);

            // if we want to go deeper than just the saved pv
            if (pv_list.Count < depth) {

                Board b = Game.board.Clone();

                // do all pv moves
                foreach (Move m in PVSearch.PV)
                    b.DoMove(m);

                // try going deeper through the transposition table
                while (pv_list.Count < depth && TT.GetBestMove(b, out Move m)) {
                    b.DoMove(m);
                    pv_list.Add(m);
                }
            }

            return [.. pv_list];
        }
    }
}
