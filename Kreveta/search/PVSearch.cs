//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.evaluation;
using Kreveta.movegen;
using Kreveta.search.moveorder;
using Kreveta.search.pruning;

using System.ComponentModel;

// ReSharper disable InconsistentNaming

namespace Kreveta.search {
    internal static class PVSearch {

        // current regular search depth
        // increments by 1 each iteration in the deepening
        internal static int CurDepth;

        // highest achieved depth this iteration
        // this is also equal to the highest ply achieved
        internal static int AchievedDepth;

        // total nodes searched this iteration
        internal static ulong CurNodes;

        // evaluated final score of the principal variation
        internal static short PVScore;

        // PRINCIPAL VARIATION
        // in pvsearch, the pv represents a variation (sequence of moves),
        // which the engine considers the best. each move in the pv represents
        // the (supposedly) best-scoring moves for both sides, so the first
        // pv node is also the move the engine is going to play
        internal static Move[] PV = [];

        private static readonly ImprovingStack improvStack = new();

        //private static Stack<Move> SearchStack = [];

        // after this time the engine aborts the search
        private static long AbortTimeThreshold;

        [ReadOnly(true), DefaultValue(false)]
        internal static bool Abort 
            => UCI.AbortSearch
            || PVSControl.sw.ElapsedMilliseconds >= AbortTimeThreshold;

        // increase the depth and do a re-search
        internal static void SearchDeeper() {
            CurDepth++;

            // as already mentioned, this represents the absolute depth limit
            QSearch.CurQSDepth = CurDepth + QSearch.QSDepth;

            // reset total nodes
            CurNodes = 0L;

            AbortTimeThreshold = TimeMan.TimeBudget != long.MaxValue 
                // approximately subtracting 1/128
                ? TimeMan.TimeBudget - (TimeMan.TimeBudget >> 7)
                : long.MaxValue;

            // create more space for killers on the new depth
            Killers.Expand(CurDepth);

            // decrease quiet history values, as they shouldn't be as relevant now.
            // erasing them completely would, however, slow down the search
            QuietHistory.Shrink();

            // these need to be erased, though
            PawnCorrectionHistory.Clear();

            // store the pv from the previous iteration in tt
            // this should hopefully allow some faster lookups
            StorePVinTT(PV, CurDepth);

            improvStack.Expand(CurDepth);

            // actual start of the search tree
            (PVScore, PV) = Search(Game.board, 0, CurDepth, new Window(short.MinValue, short.MaxValue), default);
        }

        // completely reset everything
        internal static void Reset() {
            QSearch.CurQSDepth = 0;

            CurDepth = 0;
            AchievedDepth = 0;
            CurNodes = 0UL;
            PVScore = 0;
            PV = [];

            improvStack.Expand(0);

            Killers.Clear();
            QuietHistory.Clear();
            PawnCorrectionHistory.Clear();
            CounterMoveHistory.Clear();

            // if we are playing a full game,
            // we want to keep the hash table
            if (Game.FullGame)
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
                TT.Store(b, (sbyte)--depth, i, new Window(short.MinValue, short.MaxValue), PVScore, pv[i]);

                // play along the pv to store corrent positions as well
                b.PlayMove(pv[i]);
            }
        }

        // first check the transposition table for the score, if it's not there
        // just continue the regular search. parameters need to be the same as in the search method itself
        internal static (short Score, Move[] PV) ProbeTT(Board board, int ply, int depth, Window window, Move previous) {

            // did we find the position and score?
            // we also need to check the ply, since too early tt lookups cause some serious blunders
            if (ply >= TT.MinProbingPly && TT.TryGetScore(board, depth, ply, window, out short ttScore))

                // only return the score, no pv
                return (ttScore, []);

            //int R = 0;// (CurDepth < 6 && depth == 2) ? 1 : 0;

            // in case the position is not yet stored, we fully search it and then store it
            (short Score, Move[] PV) search = Search(board, ply, depth, window, previous);
            TT.Store(board, (sbyte)depth, ply, window, search.Score, search.PV.Length != 0 ? search.PV[0] : default);

            // store the current two-move sequence in countermove history - the previously
            // played move and the best response (counter) to this move found by the search
            if (search.PV.Length != 0 && depth > CounterMoveHistory.MinStoreDepth) {
                CounterMoveHistory.Add(board.Color, previous, search.PV[0]);
            }

            PawnCorrectionHistory.Update(board, search.Score, depth);

            return search;
        }

        // finally the actual PVS algorithm
        //
        // (i could use the /// but i hate the looks)
        // ply starts at zero and increases each ply (no shit sherlock).
        // depth, on the other hand, starts at the highest value and decreases over time.
        // once we get to depth = 0, we drop into the qsearch. the search window contains 
        // the alpha and beta values, which are the pillars to this thing
        private static (short Score, Move[] PV) Search(Board board, int ply, int depth, Window window, Move previous) {

            // either crossed the time budget or maximum nodes
            // we cannot abort the first iteration - no bestmove
            if (Abort && CurDepth > 1)
                return (0, []);

            Color col = board.Color;

            // if we found a mate score in the previous iteration, we return if
            // the ply we are currently in is larger than the already found mate
            // (if we found let's say mate in 7, it doesn't make any sense to
            // searchpast ply 7 since whatever we find won't change anything).
            // we do, however, still want to search at lower plies in case we
            // find a shorter path mate
            if (Score.IsMateScore(PVScore)) {
                int matePly = Math.Abs(Score.GetMateInX(PVScore));
                if (ply > matePly)
                    return (0, []);
            }

            // based on mate distance pruning - very similar to the algorithm above,
            // but applied in the current iteration. if there's already an ensured
            // mate found in this iteration, we also don't search any further
            if (col == Color.WHITE && Score.IsMateScore(window.Alpha) && window.Alpha > 0) {

                int matePly = Score.GetMateInX(window.Alpha);
                if (ply >= matePly)
                    return (Score.GetMateScore(col, ply + 1), []);
            }
            else if (col == Color.BLACK && Score.IsMateScore(window.Beta) && window.Beta < 0) {

                int matePly = -Score.GetMateInX(window.Beta);
                if (ply >= matePly)
                    return (Score.GetMateScore(col, ply + 1), []);
            }


            // if the position is saved as a 3-fold repetition draw, return 0.
            // we have to check at ply 2 as well to prevent a forced draw by the opponent
            if (ply is not 0 and < 4 && Game.Draws.Contains(Zobrist.GetHash(board))) {
                return (0, []);
            }

            // we reached depth zero or lower => evaluate the leaf node though qsearch
            if (depth <= 0) {
                short qEval = QSearch.Search(board, ply, window);

                //if (Eval.IsMateScore(q_eval))
                //    return (col == 0 ? window.beta : window.alpha, []);

                return (qEval, []);
            }

            // is the color to play currently in check?
            bool inCheck = Movegen.IsKingInCheck(board, col);

            short staticEval = Eval.StaticEval(board);
            improvStack.AddStaticEval(staticEval, ply);

            // razoring
            //if (PruningOptions.AllowRazoring
            //    && !inCheck
            //    && ply >= Razoring.MinPly
            //    && depth == Razoring.Depth) {

            //    // if we fail low, we reduce this search by 2 ply
            //    if (Razoring.TryReduce(board, depth, col, window)) {
            //        depth -= 2;
            //        ply += 2;
            //    }
            //}

            //if (PruningOptions.AllowReverseFutilityPruning
            //    && ply >= ReverseFutilityPruning.MinPly
            //    && depth >= ReverseFutilityPruning.MinDepth
            //    && depth <= ReverseFutilityPruning.MaxDepth
            //    && !inCheck
            //    && !Score.IsMateScore(PVScore)) {

            //    // if we failed high
            //    if (ReverseFutilityPruning.TryPrune(board, depth, col, window, out short rfpScore)) {
            //        return (rfpScore, []);
            //    }
            //}

            // are the conditions for nmp satisfied?
            if (PruningOptions.AllowNullMovePruning
                && ply >= NullMovePruning.CurMinPly
                && !inCheck
                && !Score.IsMateScore(PVScore)

                && (col == Color.WHITE
                    ? window.Beta  < short.MaxValue
                    : window.Alpha > short.MinValue)) {

                // we try the reduced search and check for failing high
                if (NullMovePruning.TryPrune(board, depth, ply, window, col, out short score)) {

                    // we failed high - prune this branch
                    return (score, []);
                }
            }

            // this gets incremented only if no qsearch, otherwise the node would count twice
            CurNodes++;

            // all legal moves sorted from best to worst (only a guess)
            // first the tt bestmove, then captures sorted by MVV-LVA,
            // then killer moves and last quiet moves sorted by history
            Move[] moves = MoveOrder.GetSortedMoves(board, depth, previous);

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
                bool isCapture = curMove.Capture != PType.NONE;

                // if a position is "interesting", we avoid pruning and reductions
                // a child node is marked as interesting if we:
                //
                // 1 - only expanded a single node so far
                // 2 - (captured a piece) maybe add???
                // 3 - just escaped a check
                // 4 - are checking the opposite king
                bool interesting = expNodes == 1
                    || inCheck 
                    || (ply <= 4 && isCapture)
                    || Movegen.IsKingInCheck(child, col == Color.WHITE ? Color.BLACK : Color.WHITE);

                short childStaticEval = Eval.StaticEval(child);

                improvStack.AddStaticEval(childStaticEval, ply + 1);
                bool improving = improvStack.IsImproving(ply + 1, col);

                // have to meet certain conditions for fp
                if (PruningOptions.AllowFutilityPruning
                    && ply >= FutilityPruning.MinPly
                    && depth <= FutilityPruning.MaxDepth
                    && !interesting) {

                    // we check for failing low despite the margin
                    if (FutilityPruning.TryPrune(child, depth, col, childStaticEval, improving, window)) {

                        // prune this branch
                        continue;
                    }
                }

                // more conditions
                if ((PruningOptions.AllowLateMovePruning || PruningOptions.AllowLateMoveReductions)
                    && !interesting
                    && ply >= LateMoveReductions.MinPly
                    && depth >= LateMoveReductions.MinDepth
                    && expNodes >= LateMoveReductions.MinExpNodes) {

                    (bool Prune, bool Reduce) = LateMoveReductions.TryPrune(board, child, curMove, ply, depth, col, expNodes, improving, window);

                    // we failed low - prune this branch completely
                    if (Prune) continue;

                    // we failed low with a margin - only reduce, don't prune
                    if (Reduce) {
                        depth -= LateMoveReductions.R;
                        ply += LateMoveReductions.R;
                    }
                }

                // if we got through all the pruning all the way to this point,
                // we expect this move to raise alpha, so we search it at full depth
                (short Score, Move[] PV) fullSearch = ProbeTT(child, ply + 1, depth - 1, window, curMove);

                // we somehow still failed low
                if (col == Color.WHITE 
                    ? (fullSearch.Score <= window.Alpha) 
                    : (fullSearch.Score >= window.Beta)) {

                    // decrease the move's reputation
                    // (although we are modifying quiet history, regardless of
                    // whether this move is a capture yields better results)
                    QuietHistory.DecreaseRep(board, curMove, depth);
                }
                
                // we didn't fail low => we have a new best move for this position
                else {

                    // store the new move in tt
                    TT.Store(board, (sbyte)depth, ply, window, fullSearch.Score, moves[i]);

                    // add the current move to the front of the pv
                    pv = new Move[fullSearch.PV.Length + 1];
                    Array.Copy(fullSearch.PV, 0, pv, 1, fullSearch.PV.Length);
                    pv[0] = curMove;

                    // if we got a beta cutoff (alpha grew over beta).
                    // this means this move is probably good
                    if (window.TryCutoff(fullSearch.Score, col)) {

                        // is it quiet?
                        if (!isCapture) {

                            // if a quiet move caused a beta cutoff, we increase its
                            // reputation in history and save it as a killer move on this depth
                            QuietHistory.IncreaseRep(board, curMove, depth);
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
                    ? Score.GetMateScore(col, ply)

                    // if we aren't checked we return draw (stalemate)
                    : (short)0, []) 

                // return the score as usual
                : (col == Color.WHITE 
                    ? window.Alpha 
                    : window.Beta, pv);
        }
    }
}
