/*
 * |============================|
 * |                            |
 * |    Kreveta chess engine    |
 * | engineered by ZlomenyMesic |
 * | -------------------------- |
 * |      started 4-3-2025      |
 * | -------------------------- |
 * |                            |
 * | read README for additional |
 * | information about the code |
 * |    and usage that isn't    |
 * |  included in the comments  |
 * |                            |
 * |============================|
 */

using Kreveta.evaluation;
using Kreveta.movegen;
using Kreveta.search.moveorder;
using Kreveta.search.pruning;
using System.Diagnostics;

#nullable enable
namespace Kreveta.search {
    internal static class PVSearch {

        // maximum depth allowed in the quiescence search itself
        private const int MAX_QSEARCH_DEPTH = 12;

        // maximum depth total - qsearch and regular search combined
        // changes each iteration depending on pvsearch depth
        private static int cur_max_qs_depth = 0;

        // current regular search depth
        // increments by 1 each iteration in the deepening
        internal static int cur_depth;

        // highest achieved depth this iteration
        // this is also equal to the highest ply achieved
        internal static int achieved_depth = 0;

        // total nodes searched this iteration
        internal static long total_nodes;

        // limit for the amount of nodes allowed to be searched
        internal static long max_nodes = long.MaxValue;

        // evaluated final score of the principal variation
        internal static short pv_score = 0;

        // PRINCIPAL VARIATION
        // in pvsearch, the pv represents a variation (sequence of moves),
        // which the engine considers the best. each move in the pv represents
        // the (supposedly) best-scoring moves for both sides, so the first
        // pv node is also the move the engine is going to play
        internal static Move[] PV = [];

        internal static bool Abort => total_nodes >= max_nodes 
            || (PVSControl.sw ?? Stopwatch.StartNew()).ElapsedMilliseconds >= PVSControl.time_budget_ms;

        // increase the depth and do a re-search
        internal static void SearchDeeper() {
            cur_depth++;

            // as already mentioned, this represents the absolute depth limit
            cur_max_qs_depth = cur_depth + MAX_QSEARCH_DEPTH;

            // reset total nodes
            total_nodes = 0L;

            // create more space for killers on the new depth
            Killers.Expand(cur_depth);

            // decrease history values, as they shouldn't be as relevant now.
            // erasing them completely would, however, slow down the search
            History.Shrink();

            // store the pv from the previous iteration in tt
            // this should hopefully allow some faster lookups
            StorePVinTT(PV, cur_depth);

            // actual start of the search tree
            (pv_score, PV) = Search(Game.board, 0, cur_depth, Window.Infinite);
        }

        // completely reset everything
        internal static void Reset() {
            cur_max_qs_depth = 0;
            cur_depth = 0;
            achieved_depth = 0;
            total_nodes = 0L;
            pv_score = 0;
            PV = [];

            Killers.Clear();
            History.Clear();
            TT.Clear();
        }

        // stores the pv in the transposition table.
        // needs the starting depth in order to store trustworthy entries
        private static void StorePVinTT(Move[] pv, int depth) {
            Board b = Game.board.Clone();

            // loop all pv-nodes
            for (int i = 0; i < pv.Length; i++) {

                // store the pv-node
                TT.Store(b, --depth, i, Window.Infinite, pv_score, pv[i]);

                // play along the pv to store corrent positions as well
                b.PlayMove(pv[i]);
            }
        }

        // first check the transposition table for the score, if it's not there
        // just continue the regular search. parameters need to be the same as in the search method itself
        internal static (short Score, Move[] PV) ProbeTT(Board b, int ply, int depth, Window window) {

            // did we find the position and score?
            // we also need to check the ply, since too early tt lookups cause some serious blunders
            if (ply >= TT.MIN_PLY && TT.GetScore(b, depth, ply, window, out short tt_score))

                // only return the score, no pv
                return (tt_score, []);

            // in case the position is not yet stored, we fully search it and then store it
            (short Score, Move[] PV) search = Search(b, ply, depth, window);
            TT.Store(b, depth, ply, window, search.Score, search.PV.Length != 0 ? search.PV[0] : default);
            return search;
        }

        // finally the actual PVS algorithm
        //
        // (i could use the /// but i hate the looks)
        // ply starts at zero and increases each ply (no shit sherlock).
        // depth, on the other hand, starts at the highest value and decreases over time.
        // once we get to depth = 0, we drop into the qsearch. the search window contains 
        // the alpha and beta values, which are the pillars to this thing
        private static (short Score, Move[] PV) Search(Board board, int ply, int depth, Window window) {

            // either crossed the time budget or maximum nodes
            // we cannot abort the first iteration - no bestmove
            if (Abort && cur_depth > 1)
                return (0, []);

            int col = board.color;

            // we reached depth = 0, we evaluate the leaf node though the qsearch
            if (depth <= 0) {
                short q_eval = QSearch(board, ply, window);

                //if (Eval.IsMateScore(q_eval))
                //    return (col == 0 ? window.beta : window.alpha, []);

                return (q_eval, []);
            }

            // if the position is saved as a 3-fold repetition draw, return 0.
            // we have to check at ply 2 as well to prevent a forced draw by the opponent
            if ((ply == 1 || ply == 2) && Game.draws.Contains(Zobrist.GetHash(board))) {
                return (0, []);
            }

            // is the color to play currently in check?
            bool in_check = Movegen.IsKingInCheck(board, col);

            // razoring
            if (Razoring.CanReduce(ply, depth, in_check)) {

                // if we fail low, we reduce this search by 2 ply
                if (Razoring.TryReduce(board, depth, col, window)) {
                    depth -= 2;
                    ply += 2;
                }
            }

            if (RFP.CanPrune(depth, ply, in_check, pv_score)) {

                // if we failed high
                if (RFP.TryPrune(board, depth, col, window, out short rfp_ret_score)) {
                    return (rfp_ret_score, []);
                }
            }

            // are the conditions for nmp satisfied?
            if (NMP.CanPrune(depth, ply, in_check, pv_score, window, col)) {

                // we try the reduced search and check for failing high
                if (NMP.TryPrune(board, depth, ply, window, col, out short score)) {

                    // we failed high - prune this branch
                    return (score, []);
                }
            }

            // this gets incremented only if no qsearch, otherwise the node would count twice
            total_nodes++;

            // all legal moves sorted from best to worst (only a guess)
            // first the tt bestmove, then captures sorted by MVV-LVA,
            // then killer moves and last quiet moves sorted by history
            Move[] moves = MoveOrder.GetSortedMoves(board, depth);

            // counter for expanded nodes
            int exp_nodes = 0;

            // pv continuation to be appended?
            Move[] pv = [];

            // loop the possible moves
            for (int i = 0; i < moves.Length; ++i) {
                exp_nodes++;
                Move cur_move = moves[i];

                // create a child board with the move played
                Board child = board.Clone();
                child.PlayMove(cur_move);

                // did this move capture a piece?
                bool is_capture = cur_move.Capture() != 6;

                // we save the moves as visited to the history table.
                // history only stores quiet moves - no captures
                if (!is_capture)
                    History.AddVisited(board, cur_move);

                // if a position is "interesting", we avoid pruning and reductions
                // a child node is marked as interesting if we:
                //
                // 1 - only expanded a single node so far
                // 2 - (captured a piece) maybe add???
                // 3 - just escaped a check
                // 4 - are checking the opposite king
                bool interesting = exp_nodes == 1 
                    || in_check 
                    //|| (depth >= 6 && is_capture)
                    || Movegen.IsKingInCheck(child, col == 0 ? 1 : 0);

                short s_eval = Eval.StaticEval(child);


                // have to meet certain conditions for fp
                if (FP.CanPrune(ply, depth, interesting)) {

                    // we check for failing low despite the margin
                    if (FP.TryPrune(depth, col, s_eval, window)) {

                        // prune this branch
                        continue;
                    }
                }

                // more conditions
                if (LMR.CanPruneOrReduce(ply, depth, exp_nodes, interesting)) {

                    (bool prune, bool reduce) = LMR.TryPrune(child, cur_move, ply, depth, col, exp_nodes, window);

                    // we failed low - prune this branch completely
                    if (prune) continue;

                    // we failed low with a margin - only reduce, don't prune
                    if (reduce) {
                        int R = LMR.GetReduce(ply, depth, exp_nodes);

                        depth -= R;
                        ply += R;
                    }
                }

                // if we got through all the pruning all the way to this point,
                // we expect this move to raise alpha, so we search it at full depth
                (short Score, Move[] PV) full_search = ProbeTT(child, ply + 1, depth - 1, window);

                // we somehow still failed low
                if (window.FailsLow(full_search.Score, col)) {

                    // decrease the move's reputation
                    History.DecreaseQRep(board, cur_move, depth);
                }

                // we didn't fail low => we have a new best move for this position
                else {

                    // store the new move in tt
                    TT.Store(board, depth, ply, window, full_search.Score, moves[i]);

                    // append this move followed by the child's pv to the bigger pv
                    pv = AddMoveToPV(cur_move, full_search.PV);

                    // we try a beta cutoff?
                    if (window.TryCutoff(full_search.Score, col)) {

                        // we got a beta cutoff (alpha grew over beta).
                        // this means this move is really good

                        // is it quiet?
                        if (!is_capture) {

                            // if a quiet move caused a beta cutoff, we increase it's
                            // reputation in history and save it as a killer move on this depth
                            History.IncreaseQRep(board, cur_move, depth);
                            Killers.Add(cur_move, depth);
                        }

                        // return the score
                        return (full_search.Score/*window.GetBoundScore(col)*/, pv);
                    }
                }
            }

            return exp_nodes == 0 

                // we didn't expand any nodes - terminal node
                ? (in_check 

                    // if we are checked this means we got mated (there are no legal moves)
                    ? Eval.GetMateScore(col, ply)

                    // if we aren't checked we return draw (stalemate)
                    : (short)0, []) 

                // return the score as usual
                : (window.GetBoundScore(col), pv);
        }

        // same idea as ProbeTT, but used in qsearch
        internal static short QProbeTT(Board b, int ply, Window window) {

            int depth = MAX_QSEARCH_DEPTH - ply - cur_depth;

            // did we find the position and score?
            if (ply >= cur_depth + 3 && TT.GetScore(b, depth, ply, window, out short tt_score))
                return tt_score;

            // if the position is not yet stored, we continue the qsearch and then store it
            short score = QSearch(b, ply, window);
            TT.Store(b, depth, ply, window, score, default);
            return score;
        }

        // QUIESCENCE SEARCH:
        // instead of immediately returning the static eval of leaf nodes in the main
        // search tree, we return a qsearch eval. qsearch is essentially just an extension
        // to the main search, but only expands captures or checks. this prevents falsely
        // evaluating positions where we can for instance lose a queen in the next move
        internal static short QSearch(Board board, int ply, Window window, bool only_captures = false) {

            if (Abort)
                return 0;

            total_nodes++;

            // this stores the highest achieved search depth in this iteration
            if (ply > achieved_depth)
                achieved_depth = ply;

            // we reached the end, we return the static eval
            if (ply >= cur_max_qs_depth)
                return Eval.StaticEval(board);

            int col = board.color;

            // is the side to move in check?
            //
            // TODO - if we are only generating captures from a certain point,
            //        do we still need to be checking whether we are checked?
            //
            bool in_check = Movegen.IsKingInCheck(board, col);

            short stand_pat = 0;

            // can not use stand pat when in check
            if (!in_check) {

                // stand pat is nothing more than a static eval
                stand_pat = Eval.StaticEval(board);

                // if the stand pat fails high, we can return it
                // if not, we use it as a lower bound (alpha)
                if (window.TryCutoff(stand_pat, col))
                    return window.GetBoundScore(col);
            }

            only_captures = !in_check || only_captures;

            // if we aren't in check we only generate captures
            Move[] moves = Movegen.GetLegalMoves(board, only_captures).ToArray();

            if (moves.Length == 0) {

                // if we aren't checked, it means there just aren't
                // any more captures and we can return the stand pat
                // (we also might be in stalemate - FIX THIS)
                if (only_captures) {

                    //// stalemate
                    //if (Movegen.GetLegalMoves(b, false).Count == 0)
                    //    return 0;

                    return stand_pat;
                }

                return in_check ? Eval.GetMateScore(col, ply) : (short)0;
            }

            // we aren't checked => sort the generated captures
            if (only_captures) {

                // sort the captures by MVV-LVA
                // (most valuable victim - least valuable aggressor)
                moves = MVV_LVA.OrderCaptures(moves);
            } 

            for (int i = 0; i < moves.Length; ++i) {

                Board child = board.Clone();
                child.PlayMove(moves[i]);

                // value of the piece we just captured
                int captured = in_check ? 0 : EvalTables.Values[moves[i].Capture()];

                // delta pruning
                if (DP.CanPrune(!in_check, ply, cur_depth)) {
                    if (DP.TryPrune(ply, cur_max_qs_depth, col, window, stand_pat, captured)) {
                        continue;
                    }
                }

                // full search
                short score = QSearch(child, ply + 1, window, only_captures);

                if (window.TryCutoff(score, col)) {
                    if (ply <= cur_depth + 2)
                        TT.Store(board, -1, ply, window, score, moves[i]);

                    break;
                }
            }

            return window.GetBoundScore(col);
        }

        private static Move[] AddMoveToPV(Move move, Move[] pv) {
            Move[] new_pv = new Move[pv.Length + 1];
            new_pv[0] = move;
            Array.Copy(pv, 0, new_pv, 1, pv.Length);
            return new_pv;
        }
    }
}
