using Stockshrimp_1.movegen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

#nullable enable
namespace Stockshrimp_1.search {
    internal static class OldPVSControl {
        internal static Move best_move;
        private static int max_depth;
        private static int time_budget_ms;
        //private static Thread? thread;

        internal static void StartSearch(int depth, int time_budget_ms) {
            Console.WriteLine("search started");
            OldPVSControl.max_depth = depth;
            OldPVSControl.time_budget_ms = time_budget_ms;
            // ISSUE: reference to a compiler-generated field
            // ISSUE: reference to a compiler-generated field
            DeepeningSearchLoop();
        }

        private static void DeepeningSearchLoop() {
            long num = 0;
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (OldPVS.cur_depth < OldPVSControl.max_depth && stopwatch.ElapsedMilliseconds < (long)OldPVSControl.time_budget_ms) {
                OldPVS.SearchDeeper();
                if (!OldPVS.Abort) {
                    OldPVSControl.GetResult();
                    num = stopwatch.ElapsedMilliseconds - num;
                    if (num > (long)(OldPVSControl.time_budget_ms / 3))
                        break;
                } else
                    break;
            }
            DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(9, 1);
            interpolatedStringHandler.AppendLiteral("bestmove ");
            interpolatedStringHandler.AppendFormatted<Move>(OldPVSControl.best_move);
            Console.WriteLine(interpolatedStringHandler.ToStringAndClear());
            interpolatedStringHandler = new DefaultInterpolatedStringHandler(12, 1);
            interpolatedStringHandler.AppendLiteral("time spent: ");
            interpolatedStringHandler.AppendFormatted<TimeSpan>(stopwatch.Elapsed);
            Console.WriteLine(interpolatedStringHandler.ToStringAndClear());
            OldPVS.Reset();
        }

        private static void GetResult() {
            OldPVSControl.best_move = OldPVS.PV[0];
            DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(36, 3);
            interpolatedStringHandler.AppendLiteral("info depth: ");
            interpolatedStringHandler.AppendFormatted<int>(OldPVS.cur_depth);
            interpolatedStringHandler.AppendLiteral(" seldepth: ");
            interpolatedStringHandler.AppendFormatted<int>(OldPVS.achieved_depth);
            interpolatedStringHandler.AppendLiteral(" nodes: ");
            interpolatedStringHandler.AppendFormatted<long>(OldPVS.total_nodes);
            interpolatedStringHandler.AppendLiteral(" pv: ");
            Console.Write(interpolatedStringHandler.ToStringAndClear());
            foreach (Move move in OldPVSControl.GetFullPV(OldPVS.achieved_depth)) {
                interpolatedStringHandler = new DefaultInterpolatedStringHandler(1, 1);
                interpolatedStringHandler.AppendFormatted<Move>(move);
                interpolatedStringHandler.AppendLiteral(" ");
                Console.Write(interpolatedStringHandler.ToStringAndClear());
            }
            Console.WriteLine();
        }

        private static Move[] GetFullPV(int depth) {
            List<Move> moveList1 = new List<Move>((IEnumerable<Move>)OldPVS.PV);
            if (moveList1.Count < depth) {
                Board b = Game.board.Clone();
                foreach (Move move in OldPVS.PV)
                    b.DoMove(move);
                Move best_move;
                while (moveList1.Count < depth && TT.GetBestMove(b, out best_move)) {
                    b.DoMove(best_move);
                    moveList1.Add(best_move);
                }
            }
            List<Move> moveList2 = moveList1;
            int index = 0;
            Move[] fullPv = new Move[moveList2.Count];
            foreach (Move move in moveList2) {
                fullPv[index] = move;
                ++index;
            }
            return fullPv;
        }
    }
}
