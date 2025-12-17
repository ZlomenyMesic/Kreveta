//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.evaluation;
using Kreveta.movegen;
using Kreveta.moveorder;
using Kreveta.moveorder.historyheuristics;
using Kreveta.search.transpositions;
using Kreveta.uci;

using System;

// ReSharper disable InconsistentNaming

namespace Kreveta.search;

internal static class PVSearch {

    // current regular search depth
    // increments by 1 each iteration in the deepening
    internal static int CurIterDepth;

    // highest achieved depth this iteration
    // this is also equal to the highest ply achieved
    internal static int AchievedDepth;

    // total nodes searched this iteration
    internal static ulong CurNodes;

    // evaluated final score of the principal variation
    internal static short PVScore;

    internal static int MinNMPPly;
    
    // PRINCIPAL VARIATION
    // in pvsearch, the pv represents a variation (sequence of moves),
    // which the engine considers the best. the moves in the pv represent
    // the (supposedly) best-scoring moves for both sides, so the first
    // pv node is also the move the engine is going to play
    internal static Move[] PV = [];
    internal static Move   NextBestMove;

    // we store static eval scores from previous plies here, so we can
    // then check whether we are improving our position or not
    private static readonly ImprovingStack improvStack = new();

    // after this time the engine aborts the search
    private static long AbortTimeThreshold;
    
    internal static bool Abort 
        => UCI.ShouldAbortSearch
           || PVSControl.sw.ElapsedMilliseconds >= AbortTimeThreshold;

    // increase the depth and do a re-search
    internal static void SearchDeeper(Window aspiration) {
        CurIterDepth++;

        // reset total nodes
        CurNodes = 0UL;

        AbortTimeThreshold = TimeMan.TimeBudget != long.MaxValue 
            // approximately subtracting 1/128
            ? TimeMan.TimeBudget - (TimeMan.TimeBudget >> 7)
            : long.MaxValue;

        // create more space for killers on the new depth
        Killers.Expand(CurIterDepth);

        // decrease quiet history values, as they shouldn't be as relevant now.
        // erasing them completely would, however, slow down the search
        QuietHistory.Shrink();

        // these need to be erased, though
        PawnCorrectionHistory.Realloc();

        // store the pv from the previous iteration in tt
        // this should hopefully allow some faster lookups
        StorePVinTT(PV, CurIterDepth);

        // increase the number of plies we can hold
        improvStack.Expand(CurIterDepth);

        SearchState defaultSS = new(
            ply:         0, 
            depth:       (sbyte)CurIterDepth,
            window:      aspiration,
            //penultimate: default,
            previous:    default,
            isPVNode:    true
        );

        // actual start of the search tree
        (PVScore, PV) = Search(ref Game.Board, defaultSS, false);
    }
    // completely reset everything
    internal static void Reset() {
        //QSearch.CurQSDepth = 0;

        CurIterDepth  = 0;
        AchievedDepth = 0;
        CurNodes      = 0UL;
        PVScore       = 0;
        PV            = [];
        NextBestMove  = default;
        
        TimeMan.TimeBudgetAdjusted = false;

        improvStack.Expand(0);

        Killers.Clear();
        QuietHistory.Clear();
        PawnCorrectionHistory.Clear();
        CounterMoveHistory.Clear();
        //ContinuationHistory.Clear();
        
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
    internal static (short Score, Move[] PV) ProbeTT(ref Board board, SearchState ss, bool isNMP) {

        // did we find the position and score?
        // we also need to check the ply, since too early tt lookups cause some serious blunders
        if (ss.Ply >= TT.MinProbingPly && TT.TryGetScore(board, ss.Depth, ss.Ply, ss.Window, out short ttScore)) {
            CurNodes++;
            PVSControl.TotalNodes++;

            // only return the score, no pv
            return (ttScore, []);
        }

        // in case the position is not yet stored, we fully search it and then store it
        var result = Search(ref board, ss, isNMP);

        // no heuristics should ever be updated when in NMP null-move search,
        // as the position is likely illegal and would pollute the ecosystem
        if (!isNMP) {
            TT.Store(board, ss.Depth, ss.Ply, ss.Window, result.Score, result.PV.Length != 0 ? result.PV[0] : default);

            // store the current two-move sequence in countermove history - the previously
            // played move, and the best response (counter) to this move found by the search
            if (result.PV.Length != 0 && ss.Depth > CounterMoveHistory.MinStoreDepth)
                CounterMoveHistory.Add(board.Color, ss.Previous, result.PV[0]);
        
            /*if (result.PV.Length != 0 && ss.Depth > ContinuationHistory.MinStoreDepth) {
                ContinuationHistory.Add(ss.Penultimate, ss.Previous, result.PV[0]);
            }*/
        
            // update this position's score in pawncorrhist. we have to do this
            // here, otherwise repeating positions would take over the whole thing
            PawnCorrectionHistory.Update(board, result.Score, ss.Depth);
        }

        return result;
    }
    
    // finally the actual PVS recursive function
    //
    // (i could use the ///, but i hate the looks)
    // ply starts at zero and increases each ply (no shit sherlock).
    // depth, on the other hand, starts at the highest value and decreases over time.
    // once we get to depth = 0, we drop into the qsearch.
    private static (short Score, Move[] PV) Search(
        ref Board board, // the position to be searched
        SearchState ss,  // stores window, color, ply, depth, previous move
        bool isNMP       // is the search running from NMP?
        ) {

        // either crossed the time budget or maximum nodes.
        // we also cannot abort the first iteration - no bestmove
        if (Abort && CurIterDepth > 1)
            return (0, []);
        
        // increase the nodes searched counter
        CurNodes++;
        PVSControl.TotalNodes++;

        // just to simplify who's turn it is
        Color col = board.Color;

        // 1. MATE DISTANCE PRUNING
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
        
        // if the position is stored as a 3-fold repetition draw, return 0.
        // we have to check at ply 2 as well to prevent a forced draw by the opponent
        if (ss.Ply is not 0 and < 4 && Game.Draws.Contains(ZobristHash.Hash(board)))
            return (0, []);

        // we reached depth zero or lower => evaluate the leaf node though qsearch
        if (ss.Depth <= 0) {
            
            // we incremented this value above, but if we go into qsearch, we must
            // decrement it, so the node doesn't count twice (qsearch counts it too)
            CurNodes--;
            PVSControl.TotalNodes--;
            
            /*Span<Move> captures = stackalloc Move[Consts.MoveBufferSize];
            int count = Movegen.GetLegalMoves(ref board, captures, true);

            int qsDepth = 12 + count;
            
            if (Check.IsKingChecked(in board, col))
                qsDepth += 2;

            qsDepth = Math.Clamp(qsDepth, 12, 18);*/
            
            return (QSearch.Search(ref board, ss.Ply, ss.Window, CurIterDepth + 12, isNMP, false), []);
        }

        // is the color to play currently in check?
        bool inCheck = Check.IsKingChecked(board, col);
        short sEval  = board.StaticEval;

        // update the static eval search stack
        improvStack.UpdateStaticEval(sEval, ss.Ply);

        //short pawnCorr = PawnCorrectionHistory.GetCorrection(in board);

        // if we got here from a PV node, and the move that was played to get
        // here was the move from the previous PV, we are in a PV node as well
        ss.IsPVNode = ss.IsPVNode && (ss.Ply == 0 
                                      || ss.Ply - 1 < PV.Length && PV[ss.Ply - 1] == ss.Previous);
        
        // 2. NULL-MOVE PRUNING (NMP)
        // we assume that in every position there is at least one move that
        // improves it. first, we play a null move (only switching sides),
        // and then perform a reduced search with a null window around beta.
        // if the returned score fails high, we expect that not skipping our
        // move would "fail even higher", and thus can prune this node
        if (ss.Ply >= MinNMPPly      // minimum ply for nmp
            && !inCheck              // don't prune when in check
            && board.GamePhase() > 2 // don't prune in endgames

            // in the early stages of the search, alpha and beta are set to
            // their limit values, so doing the reduced search would only
            // waste time, since we are unable to fail high
            && (col == Color.WHITE
                ? ss.Window.Beta  < short.MaxValue
                : ss.Window.Alpha > short.MinValue)) {
            
            // null window around beta
            Window nullWindowBeta = col == Color.WHITE 
                ? new Window((short)(ss.Window.Beta - 1), ss.Window.Beta) 
                : new Window(ss.Window.Alpha, (short)(ss.Window.Alpha + 1));

            // child with no move played
            Board nullChild = board.GetNullChild();

            // the reduction is based on ply, depth, etc.
            int R = Math.Min(ss.Ply - 2, 2 + CurIterDepth / 4);
        
            // once we reach a certain depth iteration, we start pruning
            // a bit more aggressively - it isn't as important to be careful
            // later than it is at the beginning.
            if (CurIterDepth > 8) R += ss.Depth / 5;

            // perform the reduced search
            short nmpScore = ProbeTT(
                ref nullChild,
                new SearchState(
                    ply:      (sbyte)(ss.Ply + 1),
                    depth:    (sbyte)(ss.Depth - R - 1),
                    window:   nullWindowBeta,
                    previous: default,
                    isPVNode: false
                ),
                isNMP: true
            ).Score;

            // if we failed high, prune this node/branch.
            // otherwise continue regular move expansion
            //
            // TODO - CHECK IF TRULY ALL HISTORY HEURISTICS MUST BE AVOIDED IN NMP SEARCH
            //
            if (!Score.IsMateScore(nmpScore) && (col == Color.WHITE
                    ? nmpScore >= ss.Window.Beta
                    : nmpScore <= ss.Window.Alpha)) {
                
                return (nmpScore, []);
            }
        }
        
        // has the static eval improved from two plies ago?
        bool rootImproving = improvStack.IsImproving(ss.Ply, col);

        // 3. RAZORING
        // (kind of inspired by Stockfish) if a position is very,
        // very bad, we skip the move expansion and return qsearch
        // score instead. this cannot be done when in check
        if (!ss.IsPVNode && !inCheck && !rootImproving) {
            // this margin is really just magic, but it feels right
            int margin = 554 + 373 * ss.Depth * ss.Depth;

            if (col == Color.WHITE
                    ? sEval + margin < ss.Window.Alpha
                    : sEval - margin > ss.Window.Beta) {

                return (QSearch.Search(ref board, ss.Ply, ss.Window, CurIterDepth + 12, isNMP, false), []);
            }
        }
        
        // probcut is similar to nmp, but reduces nodes that fail low.
        // more info once again directly in the probcut source file
        /*if (PruningOptions.AllowProbCut
            && CurDepth >= ProbCut.MinIterDepth
            && ss.Depth == ProbCut.ReductionDepth
            && !inCheck) {
            
            // we failed low => don't prune completely, but reduce the depth
            if (!improvStack.IsImproving(ss.Ply, col) && ProbCut.TryReduce(ref board, ss.Ply, ss.Depth, ss.Window))
                ss.Depth -= ProbCut.R;
        }*/
        
        // all legal moves sorted from best to worst (only a guess).
        // take a look at MoveOrder to understand better
        var moves = MoveOrder.GetOrderedMoves(board, ss.Depth, ss.Previous, out bool isTTMove);

        // 4. INTERNAL ITERATIVE REDUCTIONS (IIR)
        // if the node we are in doesn't have a stored best move in TT,
        // we reduce the depth in hopes of being finished faster and
        // populating the tt for next iterations/occurences. the depth
        // and ply conditions are important, as reducing too much in the
        // early iterations produces very wrong outputs
        if (!ss.IsPVNode && !isTTMove && !inCheck 
            && ss.Depth >= 5 && ss.Ply >= 3 
            && ss.Window.Alpha + 1 < ss.Window.Beta) {

            ss.Depth--;
        }

        // counter for expanded nodes
        int searchedMoves = 0;

        // pv continuation to be appended?
        Move[] pv = [];

        // loop through possible moves
        for (int i = 0; i < moves.Length; i++) {
            searchedMoves++;
            
            Move curMove = moves[i];
            Board child  = board.Clone();
            
            child.PlayMove(curMove, true);
            
            ulong pieceCount      = ulong.PopCount(child.Occupied);
            short childStaticEval = child.StaticEval;
            bool  isCapture       = curMove.Capture != PType.NONE;
            
            // since draw positions skip PVS, the full search
            // result must be initialized in advance (as draw)
            (short Score, Move[] PV) fullSearch = (0, []);
            
            // if there is a known draw according to chess rules
            // (either 50 move rule or insufficient mating material),
            // all pruning and reductions are skipped
            if (child.HalfMoveClock >= 100
                || pieceCount <= 4 && isCapture && Eval.IsInsufficientMaterialDraw(child.Pieces, pieceCount))
                goto skipPVS;

            // this depth counter is used for this specific
            // expanded move; reductions may be applied to it
            int curDepth = ss.Depth - 1;
            int see      = !isCapture ? 0 : SEE.GetCaptureScore(in board, col, curMove);
            
            // once again update the static eval in the improving stack,
            // but this time after the move has been already played
            improvStack.UpdateStaticEval(childStaticEval, ss.Ply + 1); 
            bool improving = improvStack.IsImproving(ss.Ply + 1, col);

            // if a move is deemed as interesting, the branch is
            // excluded from any pruning. a move is interesting if:
            // 1) we are evaluating the first move of a position,
            //    or any move in a PV node
            // 2) the ply is low, and the move is a capture
            // 3) we are escaping check or giving a check
            bool interesting = searchedMoves == 1 || ss.IsPVNode
                               || ss.Ply <= 4 && isCapture
                               || inCheck || Check.IsKingChecked(child, col == Color.WHITE ? Color.BLACK : Color.WHITE);

            // 5. PV EXTENSIONS
            // extend the search of the first few root moves
            // (this is done by reducing all other moves)
            if (ss.Ply == 0 && searchedMoves >= 5)
                curDepth--;
            
            // 6. SEE REDUCTIONS
            // if a capture seems to be really bad, reduce the depth. oddly enough,
            // restricting these reductions with various conditions doesn't work
            if (isCapture && see < -100/* && (!ss.IsPVNode || searchedMoves > 3)*/)
                curDepth--;

            // 7. FUTILITY PRUNING (FP)
            // we try to discard moves near the leaves, which have no potential of raising alpha.
            // futility margin represents the largest possible score gain through a single move.
            // if we add this margin to the static eval of the position and still don't raise
            // alpha, we can prune this branch
            if (!interesting && ss.Ply >= 4 && ss.Depth <= 5) {
                int windowSize    = Math.Min(Math.Abs(ss.Window.Alpha - ss.Window.Beta) / 128, 12);
                int childPawnCorr = PawnCorrectionHistory.GetCorrection(in child);

                // as taken from CPW:
                // "If at depth 1 the margin does not exceed the value of a minor piece, at
                // depth 2 it should be more like the value of a rook."
                // we don't really follow this exactly, but our approach is kind of similar
                int margin = 95 + ss.Depth * 97
                                + 2 * childPawnCorr              // this acts like a measure of uncertainty
                                + (improving ? 0 : -23)          // not improving nodes prune more
                                + Math.Clamp(see / 122, -39, 17) // tweak the margin based on SEE
                                + windowSize;                    // another measure of uncertainty
                
                // if we didn't manage to raise alpha, prune this branch
                if (col == Color.WHITE
                        ? childStaticEval + margin <= ss.Window.Alpha
                        : childStaticEval - margin >= ss.Window.Beta) {
                    
                    CurNodes++; PVSControl.TotalNodes++;
                    continue;
                }

                // a small idea i had - if the leaf or close to leaf nodes seem
                // to be really bad, we try to fail low by adding the futility
                // margin to the static eval of the current position, not the child
                if (ss.Depth <= 3 && !rootImproving && !improving
                    && (col == Color.WHITE
                        ? board.StaticEval + margin <= ss.Window.Alpha
                        : board.StaticEval - margin >= ss.Window.Beta)) {
                    
                    int rootPawnCorr = PawnCorrectionHistory.GetCorrection(in board);
                    
                    // this turns out to work quite well - only reduce when the root
                    // pawn correction is bad, and the child correction is even worse
                    if (col == Color.WHITE 
                            ? childPawnCorr < rootPawnCorr && rootPawnCorr < 0
                            : childPawnCorr > rootPawnCorr && rootPawnCorr > 0) {
                        
                        // instead of just pruning this branch, we assume
                        // all following moves are even worse, so we cut
                        // off completely and return the lower bound
                        CurNodes++; PVSControl.TotalNodes++;
                        return (col == Color.WHITE ? ss.Window.Alpha : ss.Window.Beta, []);
                    }
                }
            }
            
            // 9. LATE MOVE PRUNING (LMP)
            // after we have searched a couple of moves, we expect the rest to be worse
            // and not raise alpha. to verify this, we perform a greatly reduced search
            // with a null window around alpha. if it fails low, we prune the branch
            if (!interesting && see < 300 
                             && ss.Ply        >= 4
                             && searchedMoves >= 3) {
                int R = 4;

                // depth reduce is larger with bad quiet history
                if (!isCapture && QuietHistory.GetRep(board, curMove) < -715) R++;

                // some SEE tweaking - worse captures get higher reductions
                if ( improving || see >= 94) R--;
                if (!improving && see <  0)  R++;

                // null window around alpha
                var nullWindowAlpha = col == Color.WHITE
                    ? new Window(ss.Window.Alpha, (short)(ss.Window.Alpha + 1)) 
                    : new Window((short)(ss.Window.Beta - 1), ss.Window.Beta);

                // once again a reduced depth search
                int score = ProbeTT(ref child, 
                    new SearchState((sbyte)(ss.Ply + 1), (sbyte)(ss.Depth - R), nullWindowAlpha, default, false),
                    isNMP
                ).Score;

                // continuing without this causes weird behaviour. the engine somehow
                // evaluates regular positions as mate in X. keep this. it's important.
                if (!Score.IsMateScore(score)) {
                    if (col == Color.WHITE
                            ? score <= ss.Window.Alpha
                            : score >= ss.Window.Beta) {
                        
                        continue;
                    }

                    // 10. LATE MOVE REDUCTIONS (LMR)
                    // if a late move (from the prior condition) doesn't fail
                    // low with the reduced search, but is still bad (secured
                    // by the R condition), it's at least reduced
                    if (ss.Depth == 4 && R >= 5 && !improving)
                        curDepth -= 2 - Math.Sign(see);
                }
            }
            
            // this is the end of all pruning, now comes pvs.
            // on these moves perform a full search
            bool pvs = interesting
                       || ss.Depth      >  4 
                       || searchedMoves <= 7;
                
            // this is a part of the PVS algorithm. only the first move is
            // supposed to automatically get a full search, while others get
            // a null window around alpha. the thing is, this expects perfect
            // move ordering, so in our case, a couple more than 1 move are
            // searched fully, including interesting moves
            Window window = pvs ? ss.Window
                : col == Color.WHITE 
                    ? new Window(ss.Window.Alpha, (short)(ss.Window.Alpha + 1)) 
                    : new Window((short)(ss.Window.Beta - 1), ss.Window.Beta);
                
            // perform the search - full window for PV moves
            fullSearch = ProbeTT(ref child, ss 
                with { 
                    Ply         = (sbyte)(ss.Ply + 1), 
                    Depth       = (sbyte)curDepth, 
                    Window      = window, 
                    //Penultimate = ss.Previous,
                    Previous    = curMove
                },
                isNMP
            );
                
            // and if the move failed high despite not being expected
            // to, we shall do a full re-search with a full window to
            // get a more accurate score
            if (!pvs && (col == Color.WHITE
                    ? fullSearch.Score > window.Alpha
                    : fullSearch.Score < window.Beta)) {
                    
                // if the depth was reduced, and we need a re-search,
                // revert some of the reductions (using full depth
                // doesn't seem to work as good as this does)
                if (curDepth < ss.Depth - 1)
                    curDepth++;
                    
                // this is always a full search
                fullSearch = ProbeTT(ref child, ss 
                    with { 
                        Ply         = (sbyte)(ss.Ply + 1), 
                        Depth       = (sbyte)curDepth, 
                        //Penultimate = ss.Previous,
                        Previous    = curMove
                    },
                    isNMP
                );
            }
            
            skipPVS:

            // we somehow still failed low
            if (!isNMP && (col == Color.WHITE
                    ? fullSearch.Score <= ss.Window.Alpha
                    : fullSearch.Score >= ss.Window.Beta)) {

                // decrease the move's reputation
                // (although we are modifying quiet history, not checking
                // whether this move is a capture yields better results)
                QuietHistory.ChangeRep(board, curMove, ss.Depth, isMoveGood: false);
            }

            // we went through all the pruning and didn't fail low
            // (this is the current best move for this position)
            else {
                // when using iterative deepening, the PV move from the previous
                // iteration is searched first. that means, even if our time runs
                // out, we may have still found a move better than the current one
                if (ss.Ply == 0 && !Abort)
                    NextBestMove = curMove; 

                // store the new best move in tt
                if (!isNMP)
                    TT.Store(board, ss.Depth, ss.Ply, ss.Window, fullSearch.Score, moves[i]);

                // place the current move in front of the received pv to build a new pv
                pv = new Move[fullSearch.PV.Length + 1];
                Array.Copy(fullSearch.PV, 0, pv, 1, fullSearch.PV.Length);
                pv[0] = curMove;

                // beta cutoff (see alpha-beta pruning); alpha is larger
                // than beta, so we can stop searching this branch, because
                // the other side wouldn't allow us to get here at all
                if (ss.Window.TryCutoff(fullSearch.Score, col)) {

                    // is it quiet?
                    if (!isCapture && !isNMP) {

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
}