/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

using Stockshrimp_1.evaluation;
using Stockshrimp_1.movegen;
using Stockshrimp_1.search.movesort;
using System.Diagnostics;

#nullable enable
namespace Stockshrimp_1.search {
    internal static class StaticPVS {

        // maximum depth allowed in the quiescence search itself
        private const int MAX_QSEARCH_DEPTH = 10;

        // maximum depth total - qsearch and regular search combined
        // changes each iteration depending on pvsearch depth
        private static int cur_max_qsearch_depth = 0;

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
        internal static int pv_score = 0;

        // PRINCIPAL VARIATION
        // in pvsearch, the pv represents a variation (sequence of moves),
        // which the engine considers the best. each move in the pv represents
        // the (supposedly) best-scoring moves for both sides, so the first
        // pv node is also the move the engine is going to play
        internal static Move[] PV = [];

        internal static bool Abort => total_nodes >= max_nodes 
            || (StaticPVSControl.sw ?? Stopwatch.StartNew()).ElapsedMilliseconds >= StaticPVSControl.time_budget_ms;

        // increase the depth and do a re-search
        internal static void SearchDeeper() {
            cur_depth++;

            // as already mentioned, this represents the absolute depth limit
            cur_max_qsearch_depth = cur_depth + MAX_QSEARCH_DEPTH;

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

        // stores the pv in the transposition table.
        // needs the starting depth in order to store trustworthy entries
        private static void StorePVinTT(Move[] pv, int depth) {
            Board b = Game.board.Clone();

            // loop all pv-nodes
            for (int i = 0; i < pv.Length; i++) {

                // store the pv-node
                TT.Store(b, --depth, i, Window.Infinite, (short)pv_score, pv[i]);

                // play along the pv to store corrent positions as well
                b.DoMove(pv[i]);
            }
        }

        // first search the transposition table for the score, if it's not there
        // just continue the regular search. parameters need to be the same as in the search method itself
        internal static (short Score, Move[] PV) SearchTT(Board b, int ply, int depth, Window window) {

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
        private static (short Score, Move[] PV) Search(Board b, int ply, int depth, Window window) {

            // either crossed the time budget or maximum nodes
            // we cannot abort the first iteration - no bestmove
            if (Abort && cur_depth > 1)
                return (0, []);

            // we reached depth = 0, we evaluate the leaf node though the qsearch
            if (depth <= 0)
                return (QSearch(b, ply, window), []);

            // if the position is saved as a 3-fold repetition draw, return 0.
            // we have to check at ply 2 as well to prevent a forced draw by the opponent
            if ((ply == 1 || ply == 2) && Game.draws.Contains(Zobrist.GetHash(b))) {
                return (0, []);
            }

            // this gets incremented only if no qsearch, otherwise the node would count twice
            total_nodes++;

            int col = b.side_to_move;

            // is the color to play currently in check?
            bool is_checked = Movegen.IsKingInCheck(b, col);

            // are the conditions for nmp satisfied?
            if (NullMP.CanPrune(depth, ply, is_checked, pv_score, window, col)) {

                // we try the reduced search and check for failing high
                if (NullMP.TryPrune(b, depth, ply, window, col, out short score)) {

                    // we failed high - prune this branch
                    return (score, []);
                }
            }

            // all legal moves sorted from best to worst (only a guess)
            // first the tt bestmove, then captures sorted by MVV-LVA,
            // then killer moves and last quiet moves sorted by history
            List<Move> moves = MoveSort.GetSortedMoves(b, depth);

            // counter for expanded nodes
            int exp_nodes = 0;

            // pv continuation to be appended?
            Move[] pv = [];

            // loop the possible moves
            for (int i = 0; i < moves.Count; ++i) {
                exp_nodes++;

                // create a child board with the move played
                Board child = b.Clone();
                child.DoMove(moves[i]);

                // did this move capture a piece?
                bool is_capture = moves[i].Capture() != 6;

                // we save the moves as visited to the history table.
                // history only stores quiet moves - no captures
                if (!is_capture)
                    History.AddVisited(b, moves[i]);

                // if a position is "interesting", we avoid pruning and reductions
                // a child node is marked as interesting if we:
                //
                // only expanded a single node so far
                // (captured a piece) maybe add???
                // just escaped a check
                // are checking the opposite king
                bool interesting = exp_nodes == 1 
                    || is_checked 
                    || Movegen.IsKingInCheck(child, col == 0 ? 1 : 0);

                short s_eval = Eval.StaticEval(child);

                // FUTILITY PRUNING:
                // we try to discard moves near the leaves which have no potential of raising alpha.
                // futility margin represents the largest possible score gain through a single move.
                // if we add this margin to the static eval of the position and still don't raise
                // alpha, we can prune this branch. we assume there probably isn't a phenomenal move
                // that could save this position
                if (ply >= FPruning.MIN_PLY 
                    && depth <= FPruning.MAX_DEPTH 
                    && !interesting) {

                    // as taken from chessprogrammingwiki:
                    // "If at depth 1 the margin does not exceed the value of a minor piece, at
                    // depth 2 it should be more like the value of a rook."
                    //
                    // however, a lower margin increases the search speed and thus our futility margin stays low
                    //
                    // TODO - BETTER FUTILITY MARGIN?
                    int margin = FPruning.GetMargin(depth, col, true);

                    // if we failed low (fell under alpha). this means we already know of a better
                    // alternative somewhere else in the search tree, and we can prune this branch.
                    if (window.FailsLow((short)(s_eval + margin), col))
                        continue;
                }

                // REVERSE FUTILITY PRUNING:
                // we also use reverse futility pruning - it's basically the same as fp but we subtract
                // the margin from the static eval and prune the branch if we still fail high
                if (ply >= RFPruning.MIN_PLY
                    && depth <= RFPruning.MAX_DEPTH
                    && !interesting) {

                    int rev_margin = RFPruning.GetMargin(depth, col, true);

                    // we failed high (above beta). our opponent already has an alternative which
                    // wouldn't allow this score to happen
                    if (window.FailsHigh((short)(s_eval - rev_margin), col))
                        continue;
                }

                // LATE MOVE REDUCTIONS (LMR):
                // moves other than the pv node are expected to fail low (not raise alpha),
                // so we first search them with null window around alpha. if it does not fail
                // low as expected, we do a full re-search
                if (!interesting 
                    && ply >= LateMR.MIN_PLY 
                    && depth >= LateMR.MIN_DEPTH 
                    && exp_nodes >= LateMR.MIN_EXP_NODES) {

                    // depth reduce is larger with bad history
                    int R = History.GetRep(child, moves[i]) < LateMR.HH_THRESHOLD ? LateMR.HH_R : LateMR.R;

                    // null window around alpha
                    Window nullw_alpha = window.GetLowerBound(col);

                    // once again a reduced depth search
                    short score = SearchTT(child, ply + 1, depth - R - 1, nullw_alpha).Score;

                    // we failed low, we prune this branch. it is not good enough
                    if (window.FailsLow(score, col))
                        continue;
                }

                // if we got through all the pruning all the way to this point,
                // we expect this move to raise alpha, so we search it at full depth
                (short Score, Move[] PV) full_search = SearchTT(child, ply + 1, depth - 1, window);

                // we somehow still failed low
                if (window.FailsLow(full_search.Score, col)) {

                    // decrease the move's reputation
                    History.DecreaseRep(b, moves[i], depth);
                }

                // we didn't fail low => we have a new best move for this position
                else {

                    // store the new move in tt
                    TT.Store(b, depth, ply, window, full_search.Score, moves[i]);

                    // append this move followed by the child's pv to the bigger pv
                    pv = AddMoveToPV(moves[i], full_search.PV);

                    // we try a beta cutoff?
                    if (window.TryCutoff(full_search.Score, col)) {

                        // we got a beta cutoff (alpha grew over beta).
                        // this means this move is really good

                        // is it quiet?
                        if (!is_capture) {

                            // if a quiet move caused a beta cutoff, we increase it's
                            // reputation in history and save it as a killer move on this depth
                            History.IncreaseRep(b, moves[i], depth);
                            Killers.Add(moves[i], depth);
                        }

                        // return the score
                        return (window.GetBoundScore(col), pv);
                    }
                }
            }

            return exp_nodes == 0 

                // we didn't expand any nodes - terminal node
                ? (is_checked 

                    // if we are checked this means we got mated (there are no legal moves)
                    ? Eval.GetMateScore(col, ply)

                    // if we aren't checked we return draw (stalemate)
                    : (short)0, []) 

                // return the score as usual
                : (window.GetBoundScore(col), pv);
        }

        // QUIESCENCE SEARCH:
        // instead of immediately returning the static eval of leaf nodes in the main
        // search tree, we return a qsearch eval. qsearch is essentially just an extension
        // to the main search, but only expands captures or checks. this prevents falsely
        // evaluating positions where we can for instance lose a queen in the next move
        private static short QSearch(Board b, int ply, Window window) {

            if (Abort)
                return 0;

            total_nodes++;

            // this stores the highest achieved search depth in this iteration
            if (ply > achieved_depth)
                achieved_depth = ply;

            // we reached the end, we return the static eval
            if (ply >= cur_max_qsearch_depth)
                return Eval.StaticEval(b);

            int col = b.side_to_move;

            // is the side to move in check?
            bool is_checked = Movegen.IsKingInCheck(b, col);

            short stand_pat = 0;

            // can not use stand pat when in check
            if (!is_checked) {

                // stand pat is nothing more than a static eval
                stand_pat = Eval.StaticEval(b);

                // if the stand pat fails high, we can return it
                // if not, we use it as a lower bound (alpha)
                if (window.TryCutoff(stand_pat, col))
                    return window.GetBoundScore(col);
            }

            // from a certain point, we only generate captures
            bool only_captures = !is_checked || ply >= cur_max_qsearch_depth - 3;

            List<Move> moves = Movegen.GetLegalMoves(b, only_captures);

            if (moves.Count == 0) {

                // if we aren't checked, it means there just aren't
                // any more captures and we can return the stand pat
                // (we also might be in stalemate - FIX THIS)
                if (only_captures) {
                    return stand_pat;
                }

                // if we are checked it's checkmate
                return is_checked ? Eval.GetMateScore(col, ply) : (short)0;
            }

            // we generate only captures when we aren't checked
            if (only_captures) {

                // sort the captures by MVV-LVA
                // (most valuable victim - least valuable aggressor)
                moves = MVV_LVA.SortCaptures(moves);
            } 

            for (int i = 0; i < moves.Count; ++i) {

                Board child = b.Clone();
                child.DoMove(moves[i]);

                short score = QSearch(child, ply + 1, window);

                if (window.TryCutoff(score, col))
                    break;
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
