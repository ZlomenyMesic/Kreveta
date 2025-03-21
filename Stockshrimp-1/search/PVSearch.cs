using Stockshrimp_1.evaluation;
using Stockshrimp_1.movegen;
using Stockshrimp_1.search.movesort;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stockshrimp_1.search;

internal class PVSearch {
    // maximum allowed qsearch depth - counting from qsearch start
    private const int MAX_QSEARCH_DEPTH = 9;

    // maximum allowed depth at current iteration - including both full and qsearch
    private int cur_max_qsearch_depth = 0;

    // full depth at current iteration, not including qsearch
    internal int cur_depth = 0;

    // maximum achieved depth at current iteration, including qsearch
    internal int achieved_depth = 0;

    // total nodes visited
    internal long total_nodes = 0;

    // number of nodes allowed to search
    internal long max_nodes = long.MaxValue;

    // score of current position
    internal short pv_score = 0;

    // principal variation - variation of "best" moves
    // see principal variation search (PVS) for more information
    internal Move[] PV = [];

    // should end the search?
    internal bool Abort => 
        total_nodes >= max_nodes
        || PVSControl.sw.ElapsedMilliseconds >= PVSControl.time_budget_ms;

    // evaluation of the root/starting node
    // used to indicate whether we actually improved
    private short root_eval = 0;

    internal void SearchDeeper() {
        cur_depth++;
        cur_max_qsearch_depth = cur_depth + MAX_QSEARCH_DEPTH;
        total_nodes = 0;

        root_eval = Eval.StaticEval(Game.board);

        Killers.Expand(cur_depth);
        History.Shrink();

        // store principal variation from previous search iteration
        StorePVinTT(PV, cur_depth);

        //Window w = NewIterWindow();
        //Console.WriteLine($"{w.alpha} {w.beta}");
        //Window w = Window.Infinite;

        // start of the search
        (pv_score, PV) = Search(Game.board, 0, cur_depth, Window.Infinite, true);

        if (Abort && cur_depth > 1)
            PV = [];

        //if (PV.Length == 0)
        //    PV[0] = Movegen.GetLegalMoves(Game.board)[0];
    }

    //private Window NewIterWindow() {

    //    // first iteration or previous iteration found mate
    //    if (cur_depth <= 1 || Eval.IsMateScore(pv_score))
    //        return Window.Infinite;

    //    int margin = (7 - Math.Min(cur_depth, 6)) * 120 + Math.Abs(pv_score) + 100;

    //    return new((short)(pv_score - margin), (short)(pv_score + margin));
    //}

    internal void Reset() {
        cur_max_qsearch_depth = 0;
        cur_depth = 0;
        achieved_depth = 0;
        total_nodes = 0;

        pv_score = 0;
        PV = [];
        root_eval = 0;

        Killers.Clear();
        History.Clear();

        TT.Clear();
    }

    private void StorePVinTT(Move[] pv, int depth) {
        Board b = Game.board.Clone();

        int col = b.side_to_move;
        for (int ply = 0; ply < pv.Length; ply++) {

            col = col == 0 ? 1 : 0;

            Move move = pv[ply];
            TT.Store(b, --depth, ply, Window.Infinite, pv_score, move);

            b.DoMove(move);
        }
    }

    // either returns a score found in tt or calls EvalPosition and stores the new score in tt
    private (short Score, Move[] PV) SearchTT(Board b, int ply, int depth, Window window, bool full_search) {

        // we need to check the ply to prevent some terrible blunders?
        // i don't know why but it works just fine
        if (ply > TT.MIN_PLY && TT.GetScore(b, depth, ply, window, out short ttScore)) {

            // if we found the position in tt, we return the score
            return (ttScore, []);
        }

        // in case it wasn't there, we evaluate it
        var result = Search(b, ply, depth, window, full_search);

        // and store the new evaluation in tt
        TT.Store(b, depth, ply, window, result.Score, result.PV.Length > 0 ? result.PV[0] : default);

        return result;
    }

    // the main recursive function for the search
    private (short Score, Move[] PV) Search(Board b, int ply, int depth, Window window, bool full_search) {

        // end the search immediately
        if (Abort && cur_depth > 1)
            return (0, []);

        //short cur_eval = Eval.StaticEval(b);
        //bool improved = false;

        //if (b.side_to_move == 0 && cur_eval > root_eval - 25)
        //    improved = true;

        //else if (b.side_to_move == 1 && cur_eval < root_eval + 25)
        //    improved = true;

        bool improved = true;

        // go to qsearch after reaching depth of 0
        if (depth <= 0) {
            //return (Eval.StaticEval(position), []);
            return (QSearch(b, ply, window), []);
        }

        // this gets incremented only if no qsearch, otherwise the node would count twice
        total_nodes++;

        int col = b.side_to_move;

        // is the current side to move being checked?
        bool is_checked = Movegen.IsKingInCheck(b, col);

        // NULL MOVE PRUNING:
        // avoid null moves for a few plys if we found a mate in the previous iteration
        // we must either find the shortest mate or escape
        if (!full_search
            && !Eval.IsMateScore(pv_score)
            && ply >= NullMP.MIN_PLY
            && depth >= NullMP.MIN_DEPTH
            && !is_checked
            && window.CanFailHigh(col)) {

            // null window around beta
            Window beta = window.GetUpperBound(col);

            // skip making a move - only switch colors and erase en passant square
            Board nullChild = b.GetNullChild();

            // additional depth reduce if position is not improving
            //int add_R = (improved || ply < 6) ? 0 : 1;
            int next_depth = depth - /*NMP.GetR(ply)*/ NullMP.R - 1;

            // evaluate the null child at a reduced depth
            short score = SearchTT(nullChild, ply + 1, next_depth, beta, false).Score;

            // is the evaluation "too good" despite null-move? then don't waste time on a branch that is likely going to fail-high
            if (window.FailsHigh(score, col))
                return (score, []);
                //return (col == 0 ? window.beta : window.alpha, []);
        }

        // get all legal moves sorted
        // bestmove from tt, captures by mvvlva, killer moves and quiet moves by history
        List<Move> moves = MoveSort.GetSortedMoves(b, depth);

        //if (ply == 0) {
        //    ShuffleMoves(ref moves);
        //}

        // expanding the nodes
        int checked_nodes = 0;
        Move[] pv = [];

        for (int i = 0; i < moves.Count; i++) {
            checked_nodes++;

            // child node
            Board child = b.Clone();
            child.DoMove(moves[i]);

            bool is_capture = moves[i].Capture() != 6;

            //History.AddVisited(position, moves[i]);

            // a child node is marked as interesting if we:
            //
            // only expanded a single node so far
            // captured a piece
            // just escaped a check
            // are checking the opposite king
            bool interesting = checked_nodes == 1
                || full_search
                || is_capture
                || is_checked
                || Movegen.IsKingInCheck(child, col == 0 ? 1 : 0);

            // FUTILITY PRUNING:
            // we try to discard moves near the leaves which have no potential of raising alpha.
            // futility margin represents the largest possible eval gain through the move.
            // if we add this margin to the static eval of the position and still don't raise
            // alpha, we can discard this move
            if (!full_search 
                && ply >= FPrunes.MIN_PLY
                && depth <= FPrunes.MAX_DEPTH
                && !interesting) {

                // from chessprogrammingwiki: If at depth 1 the margin does not exceed the value
                // of a minor piece, at depth 2 it should be more like the value of a rook.
                //
                // however, a lower margin increases the search speed and thus our futility margin stays low
                //
                // TODO - BETTER FUTILITY MARGIN
                int margin = FPrunes.GetMargin(depth, col, improved);
                short child_eval = Eval.StaticEval(child);

                // if we fail low (don't cross alpha), we can skip this move
                if (window.FailsLow((short)(child_eval + margin), col))
                    continue;
            }

            // LATE MOVE REDUCTIONS (LMR):
            // moves other than the pv are expected to fail low (not raise alpha),
            // thus we first search them with null window around alpha. if it does
            // not fail low we need a full re-search
            if (!full_search
                && ply >= LateMR.MIN_PLY
                && depth >= LateMR.MIN_DEPTH
                && checked_nodes >= LateMR.MIN_EXP_NODES) {

                // interesting or early moves are searched at full depth
                // not interesting and late moves with a reduced depth
                // R is a common name standing for depth reduction
                int R = interesting ? 0 : (History.GetRep(child, moves[i]) <= -1320 ? LateMR.R - 1 : LateMR.R);
                //    + (ply > 5 && improved ? 0 : 1);

                // do the reduced search
                short score = SearchTT(child, ply + 1, depth - R - 1, window.GetLowerBound(col), false).Score;

                // if we fail low as expected, we can skip this move
                if (window.FailsLow(score, col))
                    continue;
            }

            // if we are at a pv node (or a potential pv node), we force a full expansion in the next ply
            // this only applies at ply 0 or 1
            bool force_full_search = false;

            // if we got all the way to this point, we expect this move to raise alpha,
            // so we have to search it at full depth
            var full_eval = SearchTT(child, ply + 1, depth - 1, window, force_full_search);

            // in case of still somehow failing low, we skip it
            if (window.FailsLow(full_eval.Score, col)) {

                // give the move a bad reputation
                History.DecreaseRep(b, moves[i], depth);
                continue;
            }

            // we raised alpha as expected => we got a new best move for this position.
            // save this move into the tt
            TT.Store(b, depth, ply, window, full_eval.Score, moves[i]);

            // set the principal variation to this node, followed by the child's pv
            pv = AddMoveToPV(moves[i], full_eval.PV);

            //...and maybe we even get a beta cutoff?
            if (window.TryCutoff(full_eval.Score, col)) {

                // if the move is quiet (no capture)
                if (!is_capture) {

                    // give it a good reputation
                    // and remember it as a killer move
                    History.IncreaseRep(b, moves[i], depth);
                    Killers.Add(moves[i], depth);
                }

                return (window.GetBoundScore(col), pv);
            }
        }

        // we didn't expand any nodes
        // if the king is checked, we return a mate score
        // otherwise return 0 for stalemate draw
        if (checked_nodes == 0)
            return (is_checked ? Eval.GetMateScore(col, ply) : (short)0, []);

        return (window.GetBoundScore(col), pv);
    }

    // quiescence search
    // used in leaf nodes of the full search instead of immediate static eval
    // we only expand captures or checks to save some time
    private short QSearch(Board position, int ply, Window window) {

        total_nodes++;

        // once again, game ended
        if (Abort)
            return 0;

        // we save the highest achieved ply (it's also the highest depth)
        if (ply > achieved_depth)
            achieved_depth = ply;

        // once we reach the highest allowed depth, we simply return a static eval
        if (ply >= cur_max_qsearch_depth)
            return Eval.StaticEval(position);

        int col = position.side_to_move;

        // is the current side to move being checked
        bool in_check = Movegen.IsKingInCheck(position, col);

        // we cannot use standpat when in check
        if (!in_check) {
            short stand_pat = Eval.StaticEval(position);// + mobility;

            // raise alpha and get a beta cutoff if standpat is too good
            if (window.TryCutoff(stand_pat, col))
                return window.GetBoundScore(col);
        }

        List<Move> moves = Movegen.GetLegalMoves(position);

        // stalemate?
        // we have to check now since we overwrite the legal array
        if (!in_check && moves.Count == 0)
            return 0;

        // if not in check, only expand captures
        if (!in_check) {

            List<Move> capts = [];

            // add all captures to capts
            for (int i = 0; i < moves.Count; i++) {
                if (moves[i].Capture() != 6)
                    capts.Add(moves[i]);
            }

            // sort them by mvvlva
            moves = MVV_LVA.SortCaptures(capts);
        }

        int checked_nodes = 0;

        // expand the moves
        for (int i = 0; i < moves.Count; i++) {

            // clone the parent node
            Board child = position.Clone();
            child.DoMove(moves[i]);

            checked_nodes++;

            // recursively evaluate the resulting position (after the capture) with QEval
            short score = QSearch(child, ply + 1, window);

            // once again raise alpha + beta cutoff when a move is too good
            if (window.TryCutoff(score, col))
                break;
        }

        // checkmate?
        if (checked_nodes == 0 && in_check)
            return Eval.GetMateScore(col, ply);

        // return the alpha (may be raised by stand_pat)
        return window.GetBoundScore(col);
    }

    private static Move[] AddMoveToPV(Move move, Move[] pv) {
        Move[] result = new Move[pv.Length + 1];
        result[0] = move;

        Array.Copy(pv, 0, result, 1, pv.Length);

        return result;
    }

    private static void ShuffleMoves(ref List<Move> moves) {
        for (int i = moves.Count / 2; i < moves.Count - 1; i++) {

            int swap_i = new Random().Next(i, moves.Count);

            if (swap_i != i) {
                (moves[swap_i], moves[i]) = (moves[i], moves[swap_i]);
            }
        }
    }
}
