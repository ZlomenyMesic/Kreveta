//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.evaluation;
using Kreveta.movegen;
using Kreveta.search.moveorder;
using Kreveta.search.pruning;
using System.Diagnostics;

#nullable enable
namespace Kreveta.search {
    internal static class PVSearch {

        // maximum depth allowed in the quiescence search itself
        private const int QSDepth = 12;

        // maximum depth total - qsearch and regular search combined
        // changes each iteration depending on pvsearch depth
        private static int CurQSDepth = 0;

        // current regular search depth
        // increments by 1 each iteration in the deepening
        internal static int CurDepth;

        // highest achieved depth this iteration
        // this is also equal to the highest ply achieved
        internal static int AchievedDepth = 0;

        // total nodes searched this iteration
        internal static long CurNodes;

        // limit for the amount of nodes allowed to be searched
        internal static long MaxNodes = long.MaxValue;

        // evaluated final score of the principal variation
        internal static short PVScore = 0;

        // PRINCIPAL VARIATION
        // in pvsearch, the pv represents a variation (sequence of moves),
        // which the engine considers the best. each move in the pv represents
        // the (supposedly) best-scoring moves for both sides, so the first
        // pv node is also the move the engine is going to play
        internal static Move[] PV = [];

        private static long AbortTimeThreshold = 0L;
        internal static bool Abort => CurNodes >= MaxNodes 
            || (PVSControl.sw ?? Stopwatch.StartNew()).ElapsedMilliseconds >= AbortTimeThreshold;

        // increase the depth and do a re-search
        internal static void SearchDeeper() {
            CurDepth++;

            // as already mentioned, this represents the absolute depth limit
            CurQSDepth = CurDepth + QSDepth;

            // reset total nodes
            CurNodes = 0L;

            AbortTimeThreshold = TimeMan.TimeBudget - (TimeMan.TimeBudget / 100);

            // create more space for killers on the new depth
            Killers.Expand(CurDepth);

            // decrease history values, as they shouldn't be as relevant now.
            // erasing them completely would, however, slow down the search
            History.Shrink();

            //nnue = new(Game.board);

            // store the pv from the previous iteration in tt
            // this should hopefully allow some faster lookups
            StorePVinTT(PV, CurDepth);

            // actual start of the search tree
            (PVScore, PV) = Search(Game.board, 0, CurDepth, Window.Infinite);
        }

        // completely reset everything
        internal static void Reset() {
            CurQSDepth = 0;
            CurDepth = 0;
            AchievedDepth = 0;
            CurNodes = 0L;
            PVScore = 0;
            PV = [];

            Killers.Clear();
            History.Clear();

            // if we are playing a full game,
            // we want to keep the hash table
            if (Game.fullGame)
                TT.ResetScores();
            else TT.Clear();
        }

        // stores the pv in the transposition table.
        // needs the starting depth in order to store trustworthy entries
        private static void StorePVinTT(Move[] pv, int depth) {
            Board b = Game.board.Clone();

            // loop all pv-nodes
            for (int i = 0; i < pv.Length; i++) {

                // store the pv-node
                TT.Store(b, --depth, i, Window.Infinite, PVScore, pv[i]);

                // play along the pv to store corrent positions as well
                b.PlayMove(pv[i]);
            }
        }

        // first check the transposition table for the score, if it's not there
        // just continue the regular search. parameters need to be the same as in the search method itself
        internal static (short Score, Move[] PV) ProbeTT(Board board, int ply, int depth, Window window) {

            // did we find the position and score?
            // we also need to check the ply, since too early tt lookups cause some serious blunders
            if (ply >= TT.MinProbingPly && TT.GetScore(board, depth, ply, window, out short ttScore))

                // only return the score, no pv
                return (ttScore, []);

        // in case the position is not yet stored, we fully search it and then store it
        (short Score, Move[] PV) search = Search(board, ply, depth, window);
            TT.Store(board, depth, ply, window, search.Score, search.PV.Length != 0 ? search.PV[0] : default);
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
            if (Abort && CurDepth > 1)
                return (0, []);

            Color col = board.color;

            // we reached depth = 0, we evaluate the leaf node though the qsearch
            if (depth <= 0) {
                short qEval = QSearch(board, ply, window);

                //if (Eval.IsMateScore(q_eval))
                //    return (col == 0 ? window.beta : window.alpha, []);

                return (qEval, []);
            }

            // if the position is saved as a 3-fold repetition draw, return 0.
            // we have to check at ply 2 as well to prevent a forced draw by the opponent
            if ((ply == 1 || ply == 2) && Game.Draws.Contains(Zobrist.GetHash(board))) {
                return (0, []);
            }

            // is the color to play currently in check?
            bool inCheck = Movegen.IsKingInCheck(board, col);

            // razoring
            if (PruningOptions.AllowRazoring
                && !inCheck
                && ply >= Razoring.MinPly
                && depth == Razoring.Depth) {

                // if we fail low, we reduce this search by 2 ply
                if (Razoring.TryReduce(board, depth, col, window)) {
                    depth -= 2;
                    ply += 2;
                }
            }

            if (PruningOptions.AllowReverseFutilityPruning
                && ply >= RFP.MinPly
                && depth >= RFP.MinDepth
                && depth <= RFP.MaxDepth
                && !inCheck
                && !Eval.IsMateScore(PVScore)) {

                // if we failed high
                if (RFP.TryPrune(board, depth, col, window, out short rfpScore)) {
                    return (rfpScore, []);
                }
            }

            // are the conditions for nmp satisfied?
            if (PruningOptions.AllowNullMovePruning
                && depth >= NMP.MinDepth
                && ply >= NMP.MinPly
                && !inCheck
                && !Eval.IsMateScore(PVScore)

                && (col == Color.WHITE
                    ? (window.beta < short.MaxValue)
                    : (window.alpha > short.MinValue))) {

                // we try the reduced search and check for failing high
                if (NMP.TryPrune(board, depth, ply, window, col, out short score)) {

                    // we failed high - prune this branch
                    return (score, []);
                }
            }

            // this gets incremented only if no qsearch, otherwise the node would count twice
            CurNodes++;

            // all legal moves sorted from best to worst (only a guess)
            // first the tt bestmove, then captures sorted by MVV-LVA,
            // then killer moves and last quiet moves sorted by history
            Move[] moves = MoveOrder.GetSortedMoves(board, depth);

            // counter for expanded nodes
            int expNodes = 0;

            // pv continuation to be appended?
            Move[] pv = [];

            // loop the possible moves
            for (int i = 0; i < moves.Length; ++i) {
                expNodes++;
                Move curMove = moves[i];

                // create a child board with the move played
                Board child = board.Clone();
                child.PlayMove(curMove);

                // did this move capture a piece?
                bool is_capture = curMove.Capture() != PType.NONE;

                // we save the moves as visited to the history table.
                // history only stores quiet moves - no captures
                if (!is_capture)
                    History.AddVisited(board, curMove);

                // if a position is "interesting", we avoid pruning and reductions
                // a child node is marked as interesting if we:
                //
                // 1 - only expanded a single node so far
                // 2 - (captured a piece) maybe add???
                // 3 - just escaped a check
                // 4 - are checking the opposite king
                bool interesting = expNodes == 1 
                    || inCheck 
                    //|| (depth >= 6 && is_capture)
                    || Movegen.IsKingInCheck(child, col == Color.WHITE ? Color.BLACK : Color.WHITE);

                short s_eval = Eval.StaticEval(child);


                // have to meet certain conditions for fp
                if (PruningOptions.AllowFutilityPruning
                    && ply >= FP.MinPly
                    && depth <= FP.MaxDepth
                    && !interesting) {

                    // we check for failing low despite the margin
                    if (FP.TryPrune(depth, col, s_eval, window)) {

                        // prune this branch
                        continue;
                    }
                }

                // more conditions
                if ((PruningOptions.AllowLateMovePruning || PruningOptions.AllowLateMoveReductions)
                    && !interesting
                    && ply >= LMR.MinPly
                    && depth >= LMR.MinDepth
                    && expNodes >= LMR.MinExpNodes) {

                    (bool prune, bool reduce) = LMR.TryPrune(board, child, curMove, ply, depth, col, expNodes, window);

                    // we failed low - prune this branch completely
                    if (prune) {

                        continue;
                    }

                    // we failed low with a margin - only reduce, don't prune
                    if (reduce) {
                        int R = LMR.GetReduce(ply, depth, expNodes);

                        depth -= R;
                        ply += R;
                    }
                }

                // if we got through all the pruning all the way to this point,
                // we expect this move to raise alpha, so we search it at full depth
                (short Score, Move[] PV) fullSearch = ProbeTT(child, ply + 1, depth - 1, window);

                // we somehow still failed low
                if (col == Color.WHITE 
                    ? (fullSearch.Score <= window.alpha) 
                    : (fullSearch.Score >= window.beta)) {

                    // decrease the move's reputation
                    History.DecreaseQRep(board, curMove, depth);
                }


                // we didn't fail low => we have a new best move for this position
                else {

                    // store the new move in tt
                    TT.Store(board, depth, ply, window, fullSearch.Score, moves[i]);

                    // append this move followed by the child's pv to the bigger pv
                    pv = AddMoveToPV(curMove, fullSearch.PV);

                    // if we got a beta cutoff (alpha grew over beta).
                    // this means this move is really good
                    if (window.TryCutoff(fullSearch.Score, col)) {

                        // is it quiet?
                        if (!is_capture) {

                            // if a quiet move caused a beta cutoff, we increase it's
                            // reputation in history and save it as a killer move on this depth
                            History.IncreaseQRep(board, curMove, depth);
                            Killers.Add(curMove, depth);
                        }

                        // return the score
                        return (fullSearch.Score/*window.GetBoundScore(col)*/, pv);
                    }
                }
            }

            return expNodes == 0 

                // we didn't expand any nodes - terminal node
                ? (inCheck 

                    // if we are checked this means we got mated (there are no legal moves)
                    ? Eval.GetMateScore(col, ply)

                    // if we aren't checked we return draw (stalemate)
                    : (short)0, []) 

                // return the score as usual
                : (col == Color.WHITE 
                    ? window.alpha 
                    : window.beta, pv);
        }

        // same idea as ProbeTT, but used in qsearch
        internal static short QProbeTT(Board board, int ply, Window window) {

            int depth = QSDepth - ply - CurDepth;

            // did we find the position and score?
            if (ply >= CurDepth + 3 && TT.GetScore(board, depth, ply, window, out short ttScore))
                return ttScore;

            // if the position is not yet stored, we continue the qsearch and then store it
            short score = QSearch(board, ply, window);
            TT.Store(board, depth, ply, window, score, default);
            return score;
        }

        // QUIESCENCE SEARCH:
        // instead of immediately returning the static eval of leaf nodes in the main
        // search tree, we return a qsearch eval. qsearch is essentially just an extension
        // to the main search, but only expands captures or checks. this prevents falsely
        // evaluating positions where we can for instance lose a queen in the next move
        internal static short QSearch(Board board, int ply, Window window, bool onlyCaptures = false) {

            if (Abort)
                return 0;

            CurNodes++;

            // this stores the highest achieved search depth in this iteration
            if (ply > AchievedDepth)
                AchievedDepth = ply;

            // we reached the end, we return the static eval
            if (ply >= CurQSDepth)
                return Eval.StaticEval(board);

            Color col = board.color;

            // is the side to move in check?
            //
            // TODO - if we are only generating captures from a certain point,
            //        do we still need to be checking whether we are checked?
            //
            bool inCheck = Movegen.IsKingInCheck(board, col);

            short standPat = 0;

            // can not use stand pat when in check
            if (!inCheck) {

                // stand pat is nothing more than a static eval
                standPat = Eval.StaticEval(board);

                // if the stand pat fails high, we can return it
                // if not, we use it as a lower bound (alpha)
                if (window.TryCutoff(standPat, col))
                    return col == Color.WHITE 
                        ? window.alpha 
                        : window.beta;
            }

            onlyCaptures = !inCheck || onlyCaptures;

            // if we aren't in check we only generate captures
            Move[] moves = [..Movegen.GetLegalMoves(board, onlyCaptures)];

            if (moves.Length == 0) {

                // if we aren't checked, it means there just aren't
                // any more captures and we can return the stand pat
                // (we also might be in stalemate - FIX THIS)
                if (onlyCaptures) {
                    return standPat;
                }

                return inCheck ? Eval.GetMateScore(col, ply) : (short)0;
            }

            // we aren't checked => sort the generated captures
            if (onlyCaptures) {

                // sort the captures by MVV-LVA
                // (most valuable victim - least valuable aggressor)
                moves = MVV_LVA.OrderCaptures(moves);
            } 

            for (int i = 0; i < moves.Length; ++i) {
                
                Board child = board.Clone();
                child.PlayMove(moves[i]);

                // value of the piece we just captured
                int captured = (inCheck ? 0 : EvalTables.Values[(byte)moves[i].Capture()]) 
                    * (col == Color.WHITE ? 1 : -1);

                // delta pruning
                if (PruningOptions.AllowDeltaPruning
                    && !inCheck
                    && ply >= CurDepth + DP.MinPly) {

                    if (DP.TryPrune(ply, CurQSDepth, col, window, standPat, captured)) {

                        //nnue.UnUpdate(col, ref moves[i]);
                        continue;
                    }
                }

                // full search
                short score = QSearch(child, ply + 1, window, onlyCaptures);

                if (window.TryCutoff(score, col)) {
                    if (ply <= CurDepth + 2)
                        TT.Store(board, -1, ply, window, score, moves[i]);

                    break;
                }
            }

            return col == Color.WHITE 
                ? window.alpha 
                : window.beta;
        }

        private static Move[] AddMoveToPV(Move move, Move[] pv) {
            Move[] new_pv = new Move[pv.Length + 1];
            new_pv[0] = move;
            Array.Copy(pv, 0, new_pv, 1, pv.Length);
            return new_pv;
        }
    }
}
