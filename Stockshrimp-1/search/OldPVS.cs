using Stockshrimp_1.evaluation;
using Stockshrimp_1.movegen;
using Stockshrimp_1.search.movesort;
using System;
using System.Collections.Generic;

#nullable enable
namespace Stockshrimp_1.search {
    internal static class OldPVS {
        private const int MAX_QSEARCH_DEPTH = 8;
        private static int cur_max_qsearch_depth = 0;
        internal static int cur_depth;
        internal static int achieved_depth = 0;
        internal static long total_nodes;
        internal static long max_nodes = long.MaxValue;
        internal static int pv_score = 0;
        internal static Move[] PV = [];

        internal static bool Abort => total_nodes >= max_nodes;

        internal static void SearchDeeper() {
            cur_depth++;
            cur_max_qsearch_depth = cur_depth + 8;
            total_nodes = 0L;

            Killers.Expand(cur_depth);
            History.Shrink();

            StorePVinTT(PV, cur_depth);

            (pv_score, PV) = Search(Game.board, 0, cur_depth, Window.Infinite);
        }

        internal static void Reset() {
            cur_max_qsearch_depth = 0;
            cur_depth = 0;
            achieved_depth = 0;
            total_nodes = 0L;
            pv_score = 0;
            PV = [];
            Killers.Clear();
            History.Clear();
            TT.Clear();
        }

        private static void StorePVinTT(Move[] pv, int depth) {
            Board b = Game.board.Clone();

            for (int i = 0; i < pv.Length; i++) {
                Move move = pv[i];

                TT.Store(b, --depth, i, Window.Infinite, (short)pv_score, move);
                b.DoMove(move);
            }
        }

        private static (int Score, Move[] PV) SearchTT(Board b, int ply, int depth, Window window) {

            if (ply > 4 && TT.GetScore(b, depth, ply, window, out short tt_score))
                return (tt_score, []);

            (int Score, Move[] PV) search = Search(b, ply, depth, window);
            TT.Store(b, depth, ply, window, (short)search.Score, search.PV.Length != 0 ? search.PV[0] : default);
            return search;
        }

        private static (int Score, Move[] PV) Search(Board b, int ply, int depth, Window window) {

            if (depth <= 0)
                return (QSearch(b, ply, window), Array.Empty<Move>());

            total_nodes++;

            if (Abort)
                return (0, []);

            int col = b.side_to_move;

            bool is_checked = Movegen.IsKingInCheck(b, col);

            if (!Eval.IsMateScore(pv_score) 
                && ply >= 2 
                && depth >= 0 
                && !is_checked 
                && window.CanFailHigh(col)) {

                Window nullw_beta = window.GetUpperBound(col);

                Board null_child = b.GetNullChild();

                int score = SearchTT(null_child, ply + 1, depth - 3 - 1, nullw_beta).Score;

                if (window.FailsHigh((short)score, col))
                    return (score, []);
            }

            List<Move> moves = MoveSort.GetSortedMoves(b, depth);

            int exp_nodes = 0;

            Move[] pv = [];

            for (int i = 0; i < moves.Count; ++i) {
                exp_nodes++;

                Board child = b.Clone();
                child.DoMove(moves[i]);

                if (moves[i].Capture() == 6)
                    History.AddVisited(b, moves[i]);

                bool interesting = exp_nodes == 1 || is_checked || Movegen.IsKingInCheck(child, col == 0 ? 1 : 0);

                if (ply >= 3 && depth <= 5 && !interesting) {

                    int margin = FP.GetMargin(depth, col, true);

                    if (window.FailsLow((short)(Eval.StaticEval(child) + margin), col))
                        continue;
                }

                if (ply >= 4 && depth >= 0 && exp_nodes >= 3) {

                    int R = interesting ? 0 : (History.GetRep(child, moves[i]) < -1320 ? 4 : 3);

                    Window nullw_alpha = window.GetLowerBound(col);

                    int score = SearchTT(child, ply + 1, depth - R - 1, nullw_alpha).Score;

                    if (window.FailsLow((short)score, col))
                        continue;
                }

                (int Score, Move[] PV) full_search = SearchTT(child, ply + 1, depth - 1, window);

                if (window.FailsLow((short)full_search.Score, col)) {
                    History.DecreaseRep(b, moves[i], depth);
                }

                else {
                    TT.Store(b, depth, ply, window, (short)full_search.Score, moves[i]);

                    pv = AddMoveToPV(moves[i], full_search.PV);

                    if (window.CutWindow((short)full_search.Score, col)) {

                        if (moves[i].Capture() == 6) {

                            History.IncreaseRep(b, moves[i], depth);
                            Killers.Add(moves[i], depth);
                        }

                        return (window.GetBoundScore(col), pv);
                    }
                }
            }

            return exp_nodes == 0 ? (is_checked ? Eval.GetMateScore(col, ply) : 0, []) : (window.GetBoundScore(col), pv);
        }

        private static int QSearch(Board position, int ply, Window window) {
            ++OldPVS.total_nodes;
            if (OldPVS.Abort)
                return 0;
            if (ply > OldPVS.achieved_depth)
                OldPVS.achieved_depth = ply;
            if (ply >= OldPVS.cur_max_qsearch_depth)
                return Eval.StaticEval(position);
            int sideToMove = position.side_to_move;
            bool flag = Movegen.IsKingInCheck(position, sideToMove);
            if (!flag) {
                int score = Eval.StaticEval(position);
                if (window.CutWindow((short)score, sideToMove))
                    return window.GetBoundScore(sideToMove);
            }
            List<Move> moveList = Movegen.GetLegalMoves(position);
            if (!flag && moveList.Count == 0)
                return 0;
            if (!flag) {
                List<Move> capts = new List<Move>();
                for (int index = 0; index < moveList.Count; ++index) {
                    if (moveList[index].Capture() != 6)
                        capts.Add(moveList[index]);
                }
                moveList = MVV_LVA.SortCaptures(capts);
            }
            int num = 0;
            for (int index = 0; index < moveList.Count; ++index) {
                Board position1 = position.Clone();
                position1.DoMove(moveList[index]);
                ++num;
                int score = OldPVS.QSearch(position1, ply + 1, window);
                if (window.CutWindow((short)score, sideToMove))
                    break;
            }
            return num == 0 & flag ? Eval.GetMateScore(sideToMove, ply) : window.GetBoundScore(sideToMove);
        }

        private static Move[] AddMoveToPV(Move move, Move[] pv) {
            Move[] destinationArray = new Move[pv.Length + 1];
            destinationArray[0] = move;
            Array.Copy((Array)pv, 0, (Array)destinationArray, 1, pv.Length);
            return destinationArray;
        }
    }
}
