using Stockshrimp_1.movegen;
using Stockshrimp_1.search.movesort;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Stockshrimp_1.search;

internal static class ParallelSearch {

    // max full depth allowed
    private static int max_depth = 0;

    private static int time_budget_ms = 0;

    // all instances of the search
    private static readonly List<SearchThread> workers = [];

    // this may not actually be useful at all, we'll see
    private static Thread? thread = null;

    // index of the "best" worker (actually just the last)
    internal static int best_worker = 0;

    // array of best moves of each worker
    internal static Move[] best_moves = [];

    // total nodes this iteration
    internal static long cur_nodes {
        get {
            long total = 0;
            foreach (var worker in workers) {
                total += worker.total_nodes;
            }
            return total;
        }
    }

    // total nodes from the whole search
    internal static long abs_nodes = 0;

    private static Stopwatch sw = new();

    internal static void Reset() {

        History.Clear();
        Killers.Clear();
        TT.Clear();

        workers.Clear();

        thread = null;

        max_depth = 0;
        time_budget_ms = 0;
        best_worker = 0;
        abs_nodes = 0;

        best_moves = [];
    }

    internal static void StartSearch(int threads, int depth, int time_budget_ms) {

        best_moves = new Move[threads];

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

        sw = Stopwatch.StartNew();

        long spent = 0;
        bool @break = false;

        Parallel.For(0, workers.Count, i => {

            best_worker = i;

            while (workers[i].cur_depth < max_depth) {

                // loop was stopped by another thread
                if (@break) 
                    break;

                workers[i].SearchDeeper();

                // if the search was aborted, we don't save the results
                if (workers[i].Abort)
                    break;

                // save (and print) the current results
                // we only print the results from the last thread
                GetResult(i, i == workers.Count - 1);

                // time budget was already crossed
                if (sw.ElapsedMilliseconds >= time_budget_ms)
                    @break = true;

                // if this iteration took a lot of time, we can expect the next one would cross the time budget
                long diff = sw.ElapsedMilliseconds - spent;
                if (diff > time_budget_ms * 6 / 25)
                    @break = true;

                if (i == 0) spent = sw.ElapsedMilliseconds;
            }
        });

        Console.WriteLine($"time spent {sw.Elapsed}");
        Console.WriteLine($"total nodes {abs_nodes}");
        Console.WriteLine($"bestmove {VoteBestMove()}");

        Reset();
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private static void GetResult(int worker, bool print = true) {
        if (workers[worker].PV.Length == 0) {
            Console.WriteLine("FAIL");
        }
        else best_moves[worker] = workers[worker].PV[0];

        if (worker == best_worker)
            abs_nodes += cur_nodes;

        // we only want to print a single search thread to prevent utter chaos
        if (print) {

            // pv score relative to the engine
            int eng_score = workers[worker].pv_score;

            // pv score relative to color (this is printed to the gui)
            int col_score = eng_score * (Game.engine_col == 0 ? 1 : -1);

            int nps = (int)(abs_nodes / sw.Elapsed.TotalSeconds);

            Console.Write($"info depth {workers[worker].cur_depth} seldepth {workers[worker].achieved_depth} nodes {cur_nodes} nps {nps} score cp {col_score} pv");

            foreach (Move m in GetFullPV(worker, workers[worker].achieved_depth))
                Console.Write($" {m}");

            Console.WriteLine();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Move[] GetFullPV(int worker, int depth) {
        List<Move> pv_list = new(workers[worker].PV);

        // if we want to go deeper than just the saved pv
        if (pv_list.Count < depth) {

            Board b = Game.board.Clone();

            // do all pv moves
            foreach (Move m in workers[worker].PV)
                b.DoMove(m);

            // try going deeper through the transposition table
            while (pv_list.Count < depth && TT.GetBestMove(b, out Move m)) {
                b.DoMove(m);
                pv_list.Add(m);
            }
        }

        return [.. pv_list];
    }

    internal static Move VoteBestMove() {
        Dictionary<Move, int> votes = [];

        foreach (Move m in best_moves) {
            if (votes.TryGetValue(m, out int value))
                votes[m] = ++value;

            else votes.Add(m, 1);
        }

        Move best = default;
        int best_freq = 0;

        foreach ((Move m, int freq) in votes) {
            if (freq > best_freq) {
                best = m;
            }
        }

        return best;
    }
}
    
