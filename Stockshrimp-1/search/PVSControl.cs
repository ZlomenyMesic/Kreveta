/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

using Stockshrimp_1.movegen;
using System.Diagnostics;
using System.Runtime.CompilerServices;


namespace Stockshrimp_1.search;

internal static class PVSControl {

    // current best move found
    internal static Move best_move = default;

    private static int max_depth = 0;

    private static int time_budget_ms = 0;

    private static SearchThread search = new();
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
        while (search.cur_depth < max_depth
            && sw.ElapsedMilliseconds < time_budget_ms) {

            // search at current depth
            //PVSearch.SearchDeeper();
            search.SearchDeeper();

            // should end the search?
            if (search.Abort)
                break;

            // once again print the current principal variation
            GetResult();

            // if last iteration took a long time, don't start another
            prev_iter = sw.ElapsedMilliseconds - prev_iter;
            if (prev_iter > time_budget_ms * 4 / 15) break;
        }

        Console.WriteLine($"bestmove {best_move}");
        Console.WriteLine($"time spent: {sw.Elapsed}");

        search.Reset();
        thread = null;
    }

    private static void GetResult() {
        best_move = search.PV[0];

        int col_score = search.pv_score;
        int eng_score = col_score * (Game.engine_col == 0 ? 1 : -1);

        Console.Write($"info depth {search.cur_depth} seldepth {search.achieved_depth} nodes {search.total_nodes} score cp {eng_score} pv ");

        foreach (Move m in GetFullPV(search.achieved_depth))
            Console.Write($"{m} ");

        Console.WriteLine();
    }

    private static Move[] GetFullPV(int depth) {
        List<Move> pv_list = new(search.PV);

        // if we want to go deeper than just the saved pv
        if (pv_list.Count < depth) {

            Board b = Game.board.Clone();

            // do all pv moves
            foreach (Move m in search.PV)
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
