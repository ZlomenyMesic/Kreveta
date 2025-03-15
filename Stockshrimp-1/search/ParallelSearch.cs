using Stockshrimp_1.movegen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stockshrimp_1.search;

internal static class ParallelSearch {

    private static int max_depth = 0;

    private static int time_budget_ms = 0;

    private static List<SearchThread> workers = [];

    private static Thread? thread = null;

    internal static int best_worker = 0;

    internal static Move best_move = default;

    internal static long total_nodes {
        get {
            long total = 0;
            foreach (var worker in workers) {
                total += worker.total_nodes;
            }
            return total;
        }
    }

    internal static void StartSearch(int threads, int depth, int time_budget_ms) {
        for (int i = 0; i < threads; i++) {
            workers.Add(new());
        }

        Console.WriteLine("search started");

        // set some values
        max_depth = depth;
        ParallelSearch.time_budget_ms = time_budget_ms;

        // start the actual search loop
        thread = new(DeepeningSearchLoop) {
            Priority = ThreadPriority.Highest
        };
        thread.Start();
    }


    internal static void DeepeningSearchLoop() {

        Parallel.For(0, workers.Count, i => {

            best_worker = i;

            while (workers[i].cur_depth < max_depth) {
                workers[i].SearchDeeper();

                if (workers[i].Abort)
                    break;

                GetResult();
            }
        });

        Console.WriteLine($"bestmove {best_move}");
    }



    private static void GetResult() {
        best_move = workers[best_worker].PV[0];

        Console.Write($"info depth: {workers[best_worker].cur_depth} seldepth: {workers[best_worker].achieved_depth} nodes: {total_nodes} pv: ");

        //foreach (Move m in GetFullPV(search.achieved_depth))
        //    Console.Write($"{m} ");

        Console.WriteLine();
    }

    //private static Move[] GetFullPV(int depth) {
    //    List<Move> pv_list = new(search.PV);

    //    // if we want to go deeper than just the saved pv
    //    if (pv_list.Count < depth) {

    //        Board b = Game.board.Clone();

    //        // do all pv moves
    //        foreach (Move m in search.PV)
    //            b.DoMove(m);

    //        // try going deeper through the transposition table
    //        while (pv_list.Count < depth && TT.GetBestMove(b, out Move m)) {
    //            b.DoMove(m);
    //            pv_list.Add(m);
    //        }
    //    }

    //    return [.. pv_list];
}
