//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.evaluation;
using Kreveta.movegen;
using Kreveta.moveorder;
using Kreveta.moveorder.historyheuristics;
using Kreveta.search.pruning;
using Kreveta.search.transpositions;
using Kreveta.uci;

using System;

// ReSharper disable InconsistentNaming

namespace Kreveta.search;

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
    // which the engine considers the best. the moves in the pv represent
    // the (supposedly) best-scoring moves for both sides, so the first
    // pv node is also the move the engine is going to play
    internal static Move[] PV = [];

    // we store static eval scores from previous plies here, so we can
    // then check whether we are improving our position or not
    private static readonly ImprovingStack improvStack = new();

    // after this time the engine aborts the search
    private static long AbortTimeThreshold;
    
    internal static bool Abort 
        => UCI.ShouldAbortSearch
           || PVSControl.sw.ElapsedMilliseconds >= AbortTimeThreshold;

    // increase the depth and do a re-search
    internal static void SearchDeeper() {
        CurDepth++;

        // as already mentioned, this represents the absolute depth limit
        QSearch.CurQSDepth = CurDepth + QSearch.QSDepth;

        // reset total nodes
        CurNodes = 0UL;

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
        PawnCorrectionHistory.Realloc();

        // store the pv from the previous iteration in tt
        // this should hopefully allow some faster lookups
        StorePVinTT(PV, CurDepth);

        // increase the number of plies we can hold
        improvStack.Expand(CurDepth);

        SearchState defaultSS = new(
            ply:         0, 
            depth:       (sbyte)CurDepth,
            window:      Window.Infinite,
            //penultimate: default,
            previous:    default,
            isPVNode:    true
        );

        // actual start of the search tree
        (PVScore, PV) = Search(ref Game.Board, defaultSS/*, true*/);
    }
    // completely reset everything
    internal static void Reset() {
        QSearch.CurQSDepth = 0;

        CurDepth = 0;
        AchievedDepth = 0;
        CurNodes = 0UL;
        PVScore = 0;
        PV = [];

        Eval.StaticEvalCount = 0UL;

        improvStack.Expand(0);

        Killers.Clear();
        QuietHistory.Clear();
        PawnCorrectionHistory.Clear();
        CounterMoveHistory.Clear();
        ContinuationHistory.Clear();
        
        TT.Clear();
    }

    // stores the pv in the transposition table.
    // needs the starting depth in order to store trustworthy entries
    private static void StorePVinTT(Move[] pv, int depth) {
        Board board = Game.Board.Clone();

        // loop all pv-nodes
        for (int i = 0; i < pv.Length; i++) {
            // store the pv-node
            TT.Store(board, (sbyte)depth--, i, new Window(short.MinValue, short.MaxValue), PVScore, pv[i]);

            // play along the pv to store correct positions as well
            board.PlayMove(pv[i], false);
        }
    }

    // during the search, first check the transposition table for the score, if it's not there
    // just continue the search as usual. parameters need to be the same as in the search method itself
    internal static (short Score, Move[] PV) ProbeTT(ref Board board, SearchState ss/*, bool canNMP = true*/) {

        // did we find the position and score?
        // we also need to check the ply, since too early tt lookups cause some serious blunders
        if (ss.Ply >= TT.MinProbingPly && TT.TryGetScore(board, ss.Depth, ss.Ply, ss.Window, out short ttScore)) {
            CurNodes++;
            PVSControl.TotalNodes++;

            // only return the score, no pv
            return (ttScore, []);
        }

        // in case the position is not yet stored, we fully search it and then store it
        var result = Search(ref board, ss/*, canNMP*/);
        TT.Store(board, ss.Depth, ss.Ply, ss.Window, result.Score, result.PV.Length != 0 ? result.PV[0] : default);

        // store the current two-move sequence in countermove history - the previously
        // played move, and the best response (counter) to this move found by the search
        if (result.PV.Length != 0 && ss.Depth > CounterMoveHistory.MinStoreDepth) {
            CounterMoveHistory.Add(board.Color, ss.Previous, result.PV[0]);
        }
        
        /*if (result.PV.Length != 0 && ss.Depth > ContinuationHistory.MinStoreDepth) {
            ContinuationHistory.Add(ss.Penultimate, ss.Previous, result.PV[0]);
        }*/
        
        // update this position's score in pawncorrhist. we have to do this
        // here, otherwise repeating positions would take over the whole thing
        PawnCorrectionHistory.Update(board, result.Score, ss.Depth);

        return result;
    }
    
#region PVS 

    // finally the actual PVS recursive function
    //
    // (i could use the ///, but i hate the looks)
    // ply starts at zero and increases each ply (no shit sherlock).
    // depth, on the other hand, starts at the highest value and decreases over time.
    // once we get to depth = 0, we drop into the qsearch.
    private static (short Score, Move[] PV) Search(
        ref Board board, // the position to be searched
        SearchState ss
        //bool canNMP      // can null move prune? (avoids recursive NMP)
        ) {

        // either crossed the time budget or maximum nodes.
        // we also cannot abort the first iteration - no bestmove
        if (Abort && CurDepth > 1)
            return (0, []);
        
        // increase the nodes searched counter
        CurNodes++;
        PVSControl.TotalNodes++;

        // just to simplify who's turn it is
        Color col = board.Color;

        // if we found a mate score in the previous iteration, we return if
        // the ply we are currently at is larger than the already found mate
        // (if we found let's say mate in 7, it doesn't make any sense to
        // search past ply 7, since whatever we find won't matter anyway).
        // we do, however, still want to search at lower plies in case we
        // find a shorter path to mate
        if (Score.IsMateScore(PVScore)) {
            int matePly = Math.Abs(Score.GetMateInX(PVScore));
            if (ss.Ply > matePly)
                return (0, []);
        }

        // based on mate distance pruning - very similar to the algorithm above,
        // but applied in the current iteration. if there's already an ensured
        // mate found in this iteration, we also don't search any further
        if (col == Color.WHITE && Score.IsMateScore(ss.Window.Alpha) && ss.Window.Alpha > 0) {
            int matePly = Score.GetMateInX(ss.Window.Alpha);
            if (ss.Ply >= matePly)
                return (Score.CreateMateScore(col, ss.Ply + 1), []);
        }
            
        // and the same for black
        else if (col == Color.BLACK && Score.IsMateScore(ss.Window.Beta) && ss.Window.Beta < 0) {
            int matePly = -Score.GetMateInX(ss.Window.Beta);
            if (ss.Ply >= matePly)
                return (Score.CreateMateScore(col, ss.Ply + 1), []);
        }


        // if the position is saved as a 3-fold repetition draw, return 0.
        // we have to check at ply 2 as well to prevent a forced draw by the opponent
        if (ss.Ply is not 0 and < 4 && Game.Draws.Contains(ZobristHash.Hash(board))) {
            return (0, []);
        }

        // we reached depth zero or lower => evaluate the leaf node though qsearch
        if (ss.Depth <= 0) {
            
            // we incremented this value above, but if we go into qsearch, we must
            // decrement it, so the node doesn't count twice (qsearch does it too)
            CurNodes--;
            PVSControl.TotalNodes--;
            
            return (QSearch.Search(ref board, ss.Ply, ss.Window), []);
        }

        // is the color to play currently in check?
        bool inCheck = Check.IsKingChecked(board, col);

        // update the static eval search stack
        improvStack.AddStaticEval(board.StaticEval, ss.Ply);

        //short pawnCorr = PawnCorrectionHistory.GetCorrection(in board);

        // if we got here from a PV node, and the move that was played to get
        // here was the move from the previous PV, we are in a PV node as well
        ss.IsPVNode = ss.IsPVNode && (ss.Ply == 0 
                                      || ss.Ply - 1 < PV.Length && PV[ss.Ply - 1] == ss.Previous);
        
        // has the static eval improved from two plies ago?
        //bool improving = improvStack.IsImproving(ply, col);

        // first we try null-move pruning, since it is the most
        // effective way to prune the tree. details about this
        // can be found directly in the nmp file.
        // are the conditions for nmp satisfied?
        if (PruningOptions.AllowNullMovePruning
            //&& canNMP
            && ss.Ply >= NullMovePruning.CurMinPly
            && !inCheck

            // in the early stages of the search, alpha and beta are set to
            // their limit values, so doing the reduced search would only
            // waste time, since we are unable to fail high
            && (col == Color.WHITE
                ? ss.Window.Beta  < short.MaxValue
                : ss.Window.Alpha > short.MinValue)) {

            // we try the reduced search and check for failing high
            if (NullMovePruning.TryPrune(board, ss, col, out short score)) {

                // we failed high - prune this branch
                return (score, []);
            }
        }
        
        // // probcut is similar to nmp, but reduces nodes that fail low.
        // // more info once again directly in the probcut source file
        // if (PruningOptions.AllowProbCut
        //     && Game.EngineColor == Color.WHITE
        //     && CurDepth         >= ProbCut.MinIterDepth
        //     && depth            == ProbCut.ReductionDepth
        //     && !inCheck 
        //     && !improving) {
        //
        //     // we failed low => don't prune completely, but reduce the depth
        //     if (ProbCut.TryReduce(board, ply, depth, window)) {
        //         depth -= ProbCut.R;
        //     }
        // }

        // all legal moves sorted from best to worst (only a guess)
        // first the tt bestmove, then captures sorted by MVV-LVA,
        // then killer moves and last quiet moves sorted by history
        var moves = MoveOrder.GetOrderedMoves(board, ss.Depth,/* ss.Penultimate,*/ ss.Previous);

        // counter for expanded nodes
        byte searchedMoves = 0;

        // pv continuation to be appended?
        Move[] pv = [];

        // loop the possible moves
        for (byte i = 0; i < moves.Length; i++) {
            searchedMoves++;
            
            Move curMove = moves[i];
            int curDepth = ss.Depth - 1;

            // create a child board with the move played
            Board child = board.Clone();
            child.PlayMove(curMove, true);

            ulong pieceCount = ulong.PopCount(child.Occupied);
            
            // skip any pruning, AND the full search if there is a known draw
            bool skipFullSearch = child.HalfMoveClock >= 100 
                                  || pieceCount <= 4 && Eval.IsInsufficientMaterialDraw(child.Pieces, pieceCount);

            // did this move capture a piece?
            bool isCapture = curMove.Capture != PType.NONE;

            // if a position is "interesting", we avoid pruning and reductions
            // a child node is marked as interesting if we:
            //
            // 1 - only expanded a single node so far
            // 2 - captured a piece
            // 3 - just escaped a check
            // 4 - are checking the opposite king
            bool interesting = searchedMoves == 1
                               || inCheck
                               || ss.IsPVNode
                               || ss.Ply <= 4 && isCapture
                               || Check.IsKingChecked(child, col == Color.WHITE ? Color.BLACK : Color.WHITE);

            // static eval of the child node
            short childStaticEval = child.StaticEval;

            // once again update the current static eval in the search stack,
            // but this time after the move has been already played
            improvStack.AddStaticEval(childStaticEval, ss.Ply + 1); 
            bool improving = improvStack.IsImproving(ss.Ply + 1, col);

            // must meet certain conditions for fp
            if (!skipFullSearch 
                && PruningOptions.AllowFutilityPruning
                && ss.Ply   >= FutilityPruning.MinPly
                && ss.Depth <= FutilityPruning.MaxDepth
                && !interesting) {

                // we check for failing low despite a margin.
                // if we fail low, don't search this move any further
                if (FutilityPruning.TryPrune(child, ss.Depth, col, childStaticEval, improving, ss.Window)) {
                    //FutilityPruning.Prunes++;
                    continue;
                }
            }

            // more conditions (late move pruning and reductions are kind of combined)
            if (!skipFullSearch 
                && (PruningOptions.AllowLateMovePruning || PruningOptions.AllowLateMoveReductions)
                && !interesting
                && ss.Ply        >= LateMoveReductions.MinPly
                //&& depth         >= LateMoveReductions.MinDepth
                && searchedMoves >= LateMoveReductions.MinExpNodes) {

                // try to fail low
                var result = LateMoveReductions.TryPrune(board, ref child, curMove, ss, col, searchedMoves, improving);

                // we failed low - prune this branch completely
                if (result.ShouldPrune) 
                    continue;

                // we failed low with a margin - only reduce, don't prune
                if (result.ShouldReduce) {
                    curDepth -= LateMoveReductions.R;
                }
            }

            // if we got through all the pruning all the way to this point,
            // we expect this move to raise alpha, so we search it at full depth
            (short Score, Move[] PV) fullSearch = (0, []);
            
            if (!skipFullSearch)
                fullSearch = ProbeTT(ref child, ss 
                    with { 
                        Ply         = (sbyte)(ss.Ply + 1),
                        Depth       = (sbyte)curDepth,
                        //Penultimate = ss.Previous,
                        Previous    = curMove
                    }
                );

            // we somehow still failed low
            if (col == Color.WHITE
                    ? fullSearch.Score <= ss.Window.Alpha
                    : fullSearch.Score >= ss.Window.Beta) {

                // decrease the move's reputation
                // (although we are modifying quiet history, not checking
                // whether this move is a capture yields better results)
                QuietHistory.ChangeRep(board, curMove, ss.Depth, isMoveGood: false);
            }

            // we went through all the pruning and didn't fail low
            // (this is the current best move for this position)
            else {

                // store the new move in tt
                TT.Store(board, ss.Depth, ss.Ply, ss.Window, fullSearch.Score, moves[i]);

                // place the current move in front of the received pv to build a new pv
                pv = new Move[fullSearch.PV.Length + 1];
                Array.Copy(fullSearch.PV, 0, pv, 1, fullSearch.PV.Length);
                pv[0] = curMove;

                // if we get a beta cutoff, that means the move
                // is too good, so we don't have to search the
                // remaining moves
                if (ss.Window.TryCutoff(fullSearch.Score, col)) {

                    // is it quiet?
                    if (!isCapture) {

                        // if a quiet move caused a beta cutoff, we increase its history
                        // score and store it as a killer move on the current depth
                        QuietHistory.ChangeRep(board, curMove, ss.Depth, isMoveGood: true);
                        Killers.Add(curMove, ss.Depth);
                    }

                    // quit searching other moves and return this score
                    return (fullSearch.Score, pv);
                }
            }
        }
            
        // if we got here, it means we have searched through
        // the moves, but haven't got a beta cutoff
        return searchedMoves == 0 

            // we didn't expand any nodes - terminal node
            // (no legal moves exist)
            ? (inCheck 

                // if we are checked this means we got mated
                ? Score.CreateMateScore(col, ss.Ply)

                // if we aren't checked, we return draw (stalemate)
                : (short)0, []) 

            // otherwise return the bound score as usual
            : (col == Color.WHITE 
                ? ss.Window.Alpha 
                : ss.Window.Beta, pv);
    }
        
    #endregion   
}