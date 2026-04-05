//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.evaluation;
using Kreveta.movegen;
using Kreveta.moveorder;
using Kreveta.moveorder.history;
using Kreveta.moveorder.history.corrections;
using Kreveta.search.transpositions;
using Kreveta.uci;
using Kreveta.uci.options;

using System;
using System.Linq;
using System.Runtime.CompilerServices;

// ReSharper disable InconsistentNaming

namespace Kreveta.search;

internal static unsafe class PVSearch {

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
           || PVSControl.Stopwatch.ElapsedMilliseconds >= AbortTimeThreshold
           || PVSControl.TotalNodes + CurNodes >= PVSControl.CurNodesLimit;

    // increase the depth and do a re-search
    internal static void SearchDeeper(Window aspiration) {
        CurIterDepth++;

        // reset total nodes
        CurNodes = 0UL;

        AbortTimeThreshold = TM.TimeBudget != long.MaxValue 
            // approximately subtracting 1/128
            ? TM.TimeBudget - (TM.TimeBudget >> 7)
            : long.MaxValue;

        // create more space for killers on the new depth
        Killers.Expand(CurIterDepth);

        // decrease quiet history values, as they shouldn't be as relevant now.
        // erasing them completely would, however, slow down the search
        QuietHistory.Shrink();
        CaptureHistory.Shrink();
        ContinuationHistory.Age();

        // these need to be erased, though
        Corrections.Clear();

        // store the pv from the previous iteration in tt
        // this should hopefully allow some faster lookups
        StorePVinTT(PV, CurIterDepth);

        // increase the number of plies we can hold
        improvStack.Expand(CurIterDepth);

        SearchState defaultSS = new(
            ply:             0, 
            depth:           (sbyte)CurIterDepth,
            priorReductions: 0,
            window:          aspiration,
            lastMove:        default,
            excludedMove:    default,
            followPv:        true
        );

        // actual start of the search tree
        (PVScore, PV) = Search<RootNode>(ref Game.Board, defaultSS, false, false);

        PVSControl.TotalNodes += CurNodes;
    }
    
    // completely reset everything
    internal static void Reset() {
        CurIterDepth  = 0;
        AchievedDepth = 0;
        CurNodes      = 0UL;
        PVScore       = 0;
        PV            = [];
        NextBestMove  = default;

        improvStack.Expand(0);

        Killers.Clear();
        QuietHistory.Clear();
        CaptureHistory.Clear();
        CounterMoveHistory.Clear();
        ContinuationHistory.Clear();
        Corrections.Clear();
        
        // when playing a full game, storing the se diff history values helps
        // moveorder in the next search. we want to age the values a lot, though,
        // so they don't remain there forever
        if (!Game.FullGame) StaticEvalDiffHistory.Clear();
        else                StaticEvalDiffHistory.Age();
        
        TT.Clear();
    }

    // stores the pv in the transposition table.
    // needs the starting depth in order to store trustworthy entries
    private static void StorePVinTT(Move[] pv, int depth) {
        Board board = Game.Board.Clone();

        // loop all pv-nodes
        for (int i = 0; i < pv.Length; i++) {
            // store the pv-node
            TT.Store(board.Hash, (sbyte)depth--, i, new Window(short.MinValue, short.MaxValue), PVScore, pv[i]);

            // play along the pv to store correct positions as well
            board.PlayMove(pv[i], false);
        }
    }

    // during the search, first check the transposition table for the score, if it's not there
    // just continue the search as usual. parameters need to be the same as in the search method itself
    private static (short Score, Move[] PV) SearchNext<NodeType>(ref Board board, SearchState ss, bool ignore3Fold, bool cutNode) 
        where NodeType : ISearchNodeType {
        
        // did we find the position and score?
        // also repeating positions cannot occur under 4 plies
        if (ss.Ply >= 4 && TT.TryGetScore(board.Hash, ss.Depth, ss.Ply, ss.Window, out short ttScore)) {
            CurNodes++;
            
            // return just the score
            return (ttScore, []);
        }

        // in case the position is not yet stored, we fully search it and then store it
        var result = Search<NodeType>(ref board, ss, ignore3Fold, cutNode);
        
        // store the found score and best move in tt
        TT.Store(board.Hash, ss.Depth, ss.Ply, ss.Window, result.Score, result.PV.Length != 0 ? result.PV[0] : default);
        
        // store the current two-move sequence in countermove history - the previously
        // played move, and the best response (counter) to this move found by the search
        if (result.PV.Length != 0 && ss.Depth > 4)
            CounterMoveHistory.Add(board.SideToMove, ss.LastMove, result.PV[0]);
        
        // update this position's score in pawncorrhist. bounds have to be checked,
        // as a bound score is certainly not reliable enough to correct the eval
        if (result.Score > ss.Window.Alpha && result.Score < ss.Window.Beta)
            Corrections.Update(board, result.Score, ss.Depth);

        return (result.Score, result.PV);
    }
    
    // finally the actual PVS algorithm:
    // ply starts at zero and increases over time, while depth starts at the highest
    // value and decreases. the function recursively calls itself to generate a sort
    // of "tree" of possible future positions, and tracks the best possible path to
    // go. computing all positions is impossible, so different pruning and reduction
    // techniques are used to skip likely irrelevant branches
    private static (short Score, Move[] PV) Search<NodeType>(ref Board board, SearchState ss, bool ignore3Fold, bool cutNode)
        where NodeType : ISearchNodeType {
        
        bool rootNode = typeof(NodeType) == typeof(RootNode);
        bool pvNode   = typeof(NodeType) == typeof(PVNode) || rootNode;
        bool allNode  = !pvNode && !cutNode;
        
        // either crossed the time budget or maximum nodes.
        // we also cannot abort the first iteration - no bestmove
        if (Abort && CurIterDepth > 1)
            return (0, []);
        
        // we reached depth zero or lower => evaluate the leaf node though qsearch
        if (ss.Depth <= 0)
            return (QSearch.Search(ref board, ss.Ply, ss.Window, CurIterDepth + 12, 64), []);
        
        // increase the nodes searched counter
        CurNodes++;

        // just to simplify who's turn it is
        Color col = board.SideToMove;

        // 1. MATE DISTANCE PRUNING (~0 Elo)
        // if there's already an ensured mate found, we don't have to search
        // past the mate ply. this is applied to both winning and losing mates
        int alpha = col == Color.WHITE ? ss.Window.Alpha : ss.Window.Beta;
        if (Score.IsMate(alpha) && (col == Color.WHITE ? alpha > 0 : alpha < 0)) {
            int matePly = Math.Abs(Score.GetMateInX(alpha));
            if (ss.Ply >= matePly)
                return (Score.CreateMateScore(col, ss.Ply + 1), []);
        }
        
        // try to retrieve a known best move from the transposition table
        bool ttHit        = TT.TryGetBestMove(board.Hash, ss.Ply, out Move ttMove, out short ttScore, out TT.ScoreFlags ttFlags, out int ttDepth);
        bool ttMoveExists = ttHit && ttMove != default;
        bool ttCapture    = ttMoveExists && (ttMove.Capture != PType.NONE || ttMove.Promotion == PType.PAWN);
        
        // is the tt score optimistic that we can raise alpha or cause a beta cutoff
        bool ttOptimistic = ttMoveExists && (col == Color.WHITE 
            ? ttFlags.HasFlag(TT.ScoreFlags.LOWER_BOUND) && ttScore > ss.Window.Alpha
            : ttFlags.HasFlag(TT.ScoreFlags.UPPER_BOUND) && ttScore < ss.Window.Beta);
        
        // is the tt score optimistic that we can cause a beta cutoff?
        bool ttBetaCutoff = ttMoveExists && ttDepth >= ss.Depth - 2 && (col == Color.WHITE 
            ? ttFlags.HasFlag(TT.ScoreFlags.LOWER_BOUND) && ttScore >= ss.Window.Beta
            : ttFlags.HasFlag(TT.ScoreFlags.UPPER_BOUND) && ttScore <= ss.Window.Alpha);
        
        // if tt score is reliable enough, it may be used for early cutoffs in
        // non-pv nodes, but only in case that it strongly supports the node type
        int  cm = 26 + 18 * ss.Depth;
        bool shouldCutoff = allNode && (col == Color.WHITE ? ttScore + cm <= ss.Window.Alpha : ttScore - cm >= ss.Window.Beta) 
                         || cutNode && (col == Color.WHITE ? ttScore - cm >= ss.Window.Beta  : ttScore + cm <= ss.Window.Alpha);
        
        if (ttHit && ttDepth >= ss.Depth - 2 && shouldCutoff
            && (ttFlags.HasFlag(TT.ScoreFlags.SCORE_EXACT)
            ||  ttFlags.HasFlag(TT.ScoreFlags.UPPER_BOUND) && ttScore <= ss.Window.Alpha
            ||  ttFlags.HasFlag(TT.ScoreFlags.LOWER_BOUND) && ttScore >= ss.Window.Beta)) {

            return (ttScore, []);
        }
        
        // check whether we are still following the previous principal variation
        ss.FollowPV = ss.FollowPV && (rootNode || ss.Ply - 1 < PV.Length && PV[ss.Ply - 1] == ss.LastMove);

        // is the side to move currently in check?
        bool  inCheck    = board.IsCheck;
        bool  nonPawnMat = board.HasNonPawnMaterial(col);
        short staticEval = board.StaticEval;
        
        // update the static eval in the stack and the improving flag
        improvStack.UpdateStaticEval(staticEval, ss.Ply);
        bool parentImproving = improvStack.IsImproving(ss.Ply, col);
        
        // a fairly solid improving idea
        parentImproving |= col == Color.WHITE 
            ? staticEval >= ss.Window.Beta 
            : staticEval <= ss.Window.Alpha;
        
        parentImproving &= !inCheck;
        
        // if tt score is reliable, adjust the static eval used for pruning
        if (ttMoveExists && (ttFlags.HasFlag(TT.ScoreFlags.SCORE_EXACT)
                             || ttScore > staticEval && ttFlags.HasFlag(TT.ScoreFlags.LOWER_BOUND)
                             || ttScore < staticEval && ttFlags.HasFlag(TT.ScoreFlags.UPPER_BOUND))
            && !Score.IsMate(ttScore)) {

            staticEval = ttScore;
        }

        // 2. RAZORING (~18 Elo)
        // if the static evaluation is very bad, the move expansion is skipped, and the qsearch score is
        // returned instead. this cannot be done when in check, and the qsearch score must be validated
        if (!ss.FollowPV && !inCheck && (ss.Depth <= 3 || !ttCapture)) {
            // this margin is really just magic, but it feels right
            int margin = 539 + 375 * ss.Depth * ss.Depth;

            // some additional margin tuning
            if (parentImproving) margin += 33;
            if (allNode)         margin -= 27;

            if (col == Color.WHITE
                    ? staticEval + margin < ss.Window.Alpha
                    : staticEval - margin > ss.Window.Beta) {

                // perform the quiescence search and ensure it actually fails low
                short qEval = QSearch.Search(ref board, ss.Ply, ss.Window, CurIterDepth + 12, 64);
                if (col == Color.WHITE ? qEval <= ss.Window.Alpha : qEval >= ss.Window.Beta)
                    return (qEval, []);
            }
        }
        
        // 3. STATIC NULL MOVE PRUNING (~4 Elo)
        // also called reverse futility pruning; if the static eval at close-to-leaf
        // nodes fails high despite subtracting a margin, prune this branch
        if (!ss.FollowPV && !inCheck && parentImproving && (allNode || cutNode) && ss.ExcludedMove == default) {
            int margin = 208 + 282 * ss.Depth * ss.Depth;

            if (col == Color.WHITE && staticEval - margin > ss.Window.Beta)
                return ((short)((2 * ss.Window.Beta + staticEval) / 3), []);

            if (col == Color.BLACK && staticEval + margin < ss.Window.Alpha)
                return ((short)((2 * ss.Window.Alpha + staticEval) / 3), []);
        }
        
        // 4. SMALL PROBCUT IDEA
        // inspired by Stockfish, but modified quite a bit. if the tt score isn't reliable enough
        // to cause the search to be skipped completely, we drop into quiescence search
        int probcutMargin = 523 + 27 * (ss.Depth - ttDepth) + 3 * ss.Depth;
        int probcutBeta   = col == Color.WHITE 
            ? ss.Window.Beta  + probcutMargin 
            : ss.Window.Alpha - probcutMargin;
        
        // make sure the tt score is the correct bound
        if (ttDepth >= ss.Depth - 4 && ss.Depth >= 3 && cutNode && !Score.IsMate(ttScore)
            && ss.ExcludedMove == default && (col == Color.WHITE 
                ? ttFlags.HasFlag(TT.ScoreFlags.LOWER_BOUND) && ttScore >= probcutBeta
                : ttFlags.HasFlag(TT.ScoreFlags.UPPER_BOUND) && ttScore <= probcutBeta)) {

            // drop into quiescence search
            return (QSearch.Search(ref board, ss.Ply, ss.Window, CurIterDepth + 12, 64), []);
        }
        
        // 5. NULL MOVE PRUNING (~107 Elo)
        // we assume that in every position there is at least one move that improves it. first,
        // we play a null move (only switching sides), and then perform a reduced search with
        // a null window around beta. if the returned score fails high, we expect that not
        // skipping our move would "fail even higher", and thus can prune this node
        int nmpEvalMargin = 3 * ss.Depth + (parentImproving ? 3 : 0);
        
        if (ss.Ply >= MinNMPPly // minimum ply for nmp
            && !inCheck         // don't prune when in check
            && nonPawnMat       // don't prune in absolute endgames
            && ss.ExcludedMove == default
            
            // make sure static eval is over or at least close to beta to not
            // waste time searching positions, which probably won't fail high
            && (col == Color.WHITE
                ? staticEval >= ss.Window.Beta  - nmpEvalMargin
                : staticEval <= ss.Window.Alpha + nmpEvalMargin)) {
            
            // null window around beta
            Window nullWindowBeta = col == Color.WHITE 
                ? new Window((short)(ss.Window.Beta - 1), ss.Window.Beta)
                : new Window(ss.Window.Alpha, (short)(ss.Window.Alpha + 1));
            
            // child with a move skipped
            var nullChild = board.Clone() with {
                EnPassantSq = 64,
                SideToMove  = (Color)((int)col ^ 1)
            };
            
            // somewhat mimic the eval difference from switching sides.
            // this helps a lot, as recomputing static eval wastes time
            nullChild.StaticEval += (short)(col == Color.WHITE ? -12 : 12);
            nullChild.IsCheck     = false;
            nullChild.Hash       ^= ZobristHash.WhiteToMove;
            nullChild.Hash       ^= board.EnPassantSq != 64 ? ZobristHash.EnPassant[board.EnPassantSq & 7] : 0UL;
            
            // the depth reduction
            int R = 7 + ss.Depth / 3 + (col == Color.WHITE 
                ? staticEval - ss.Window.Beta 
                : ss.Window.Alpha - staticEval) / 365;

            if (cutNode || ttBetaCutoff) R++;
            
            // perform the reduced search
            short nmpScore = SearchNext<NonPVNode>(
                ref nullChild,
                new SearchState(
                    ply:             (sbyte)(ss.Ply + 1),
                    depth:           (sbyte)(ss.Depth - R),
                    priorReductions: ss.PriorReductions,
                    window:          nullWindowBeta,
                    lastMove:        default,
                    excludedMove:    ss.ExcludedMove,
                    followPv:        false
                ),
                ignore3Fold: true,
                cutNode:     false
            ).Score;

            // if we failed high, prune this node
            if (col == Color.WHITE
                    ? nmpScore >= ss.Window.Beta
                    : nmpScore <= ss.Window.Alpha) {
                
                // if verification search isn't needed, return the score
                if (ss.Depth <= 22)
                    return (nmpScore, []);

                // disable null move pruning early in verification search
                int temp  = MinNMPPly;
                MinNMPPly = ss.Ply + 3 * (ss.Depth - R) / 4;

                // do the verification search
                nmpScore = SearchNext<NonPVNode>(ref board,
                    ss with {
                        Depth  = (sbyte)(ss.Depth - R),
                        Ply    = (sbyte)(ss.Ply + 1),
                        Window = nullWindowBeta,
                    }, ignore3Fold: false, cutNode: false).Score;

                MinNMPPly = temp;

                // check once again
                if (col == Color.WHITE
                        ? nmpScore >= ss.Window.Beta
                        : nmpScore <= ss.Window.Alpha)
                    return (nmpScore, []);
            }
        }
        
        // 6. INTERNAL ITERATIVE REDUCTIONS (~54 Elo)
        // if the node we are in doesn't have a stored best move in TT, we reduce the depth
        // in hopes of finishing the search faster and populating the TT for next iterations
        // or occurences. the depth and ply conditions are important, as reducing too much in
        // the early iterations produces very wrong outputs
        if (!ttMoveExists && !inCheck && ss.Window.Alpha + 1 < ss.Window.Beta
            && !ss.FollowPV && (pvNode && ss.Depth >= 5 || cutNode && ss.Depth >= 7) && ss.Ply >= 3) {

            ss.PriorReductions++;
            ss.Depth--;
        }
        
        // if the tt move is excluded from search
        if (ttMove == ss.ExcludedMove) ttMoveExists = false;
        
        // after this move index threshold all quiets are reduced
        int reduceQuietsThreshold = 41 + 3 * ss.Depth * ss.Depth
            + (inCheck      || !allNode        ? 1000 : 0)
            + (ttOptimistic || parentImproving ? 5    : 0);
        
        // was moveorder score assigning already performed?
        bool       scoresAssigned = false;
        Span<Move> legalMoves     = stackalloc Move[Consts.MoveBufferSize];
        Span<int>  moveScores     = stackalloc int[Consts.MoveBufferSize];
        int        moveCount      = 0;
        int        expandedNodes  = 0;

        // pv continuation to be appended
        Move[] pv = [];

        // loop through possible moves
        while (true) {
            Move curMove;
            int  curScore = 0;

            // when in the first iteration, check if there's a known
            // tt move and potentially place it as the first move
            if (expandedNodes == 0 && ttMoveExists)
                curMove = ttMove;
            
            // otherwise use regular moveorder
            else {
                if (!scoresAssigned) {
                    moveCount = Movegen.GetLegalMoves(ref board, legalMoves);
                    
                    LazyMoveOrder.AssignScores(in board, ss.Depth, ss.LastMove, legalMoves, moveScores, moveCount);
                    scoresAssigned = true;

                    // very important - if we've already checked a tt move, it
                    // has to be removed from the move list to not be played again
                    if (ttMove != default) {
                        for (int i = 0; i < moveCount; i++) {
                            if (legalMoves[i] != ttMove)
                                continue;
                            
                            legalMoves[i] = default;
                            break;
                        }
                    }
                }
                
                curMove = LazyMoveOrder.NextMove(legalMoves, moveScores, moveCount, out curScore);

                // when moveorder returns default, there aren't any moves left
                if (curMove == default) break;
            }

            // if we have a searchmoves restriction, skip other moves in root
            if (rootNode && Game.SearchMoves.Count > 0 && !Game.SearchMoves.Contains(curMove))
                continue;

            // if the current move is excluded from search by singular extensions
            if (curMove == ss.ExcludedMove)
                continue;
            
            expandedNodes++;
            
            Board child = board.Clone();
            child.PlayMove(curMove, updateStaticEval: true);
            
            ulong pieceCount      = ulong.PopCount(child.Occupied);
            short childStaticEval = child.StaticEval;
            bool  isCapture       = curMove.Capture != PType.NONE || curMove.Promotion == PType.PAWN;
            int   weight          = pvNode ? 1 : 0;

            // update this move's static eval difference history
            if (!isCapture) {
                int seDiff = (childStaticEval - staticEval) * (col == Color.WHITE ? 1 : -1);
                StaticEvalDiffHistory.Add(curMove, seDiff);
            }
            
            // since draw positions skip PVS, the full search
            // result must be initialized in advance (as draw)
            (short Score, Move[] PV) fullSearch = (0, []);
            int curDepth = ss.Depth;
            
            // check the position for a 3-fold repetition draw. it is very
            // important that we also remove this move from the stack, which
            // must be done anywhere where this loop is exited
            bool isThreeFold = !ignore3Fold && ThreeFold.AddAndCheck(child.Hash);
            
            // create some deterministic noise for three-fold repetition
            // draws, which apparently helps avoid weird loops or blindness
            if (isThreeFold) fullSearch.Score = (short)(-1 + (int)(CurNodes & 0x2));
            
            // if there is a known draw according to chess rules
            // (either 50 move rule or insufficient mating material),
            // all pruning and reductions are skipped
            if (isThreeFold
                || child.HalfMoveClock >= 100
                || pieceCount <= 4 && isCapture && Eval.IsInsufficientMaterialDraw(child.Pieces, pieceCount))
                goto skipPVS;
            
            int  see        = isCapture ? SEE.GetCaptureScore(in board, col, curMove) : 0;
            bool givesCheck = child.IsCheck;
            
            // once again update the static eval in the improving stack,
            // but this time after the move has been already played
            improvStack.UpdateStaticEval(childStaticEval, ss.Ply + 1);
            bool improving = improvStack.IsImproving(ss.Ply + 1, col);
            improving |= col == Color.WHITE 
                ? childStaticEval >= ss.Window.Beta 
                : childStaticEval <= ss.Window.Alpha;
            improving &= !inCheck;

            // base reduction for this move, can be turned into an extension
            int reduction = 1;

            // futility pruning is avoided for moves that give check, and for any first move in
            // a node. any nodes where the side to move is in check, or that follow the previous
            // principal variation also have FP disabled
            bool skipFP = expandedNodes == 1 
                          || ss.FollowPV
                          || inCheck 
                          || givesCheck
                          || !nonPawnMat;
            
            // 8. FUTILITY PRUNING (~56 Elo)
            // we try to discard moves near the leaves, which have no potential of raising alpha.
            // futility margin represents the largest possible score gain through a single move.
            // if we add this margin to the static eval of the position and still don't raise
            // alpha, we can prune this branch
            if (!skipFP && ss.Ply >= 4 && ss.Depth <= 5) {
                int windowSize = Math.Min(Math.Abs(ss.Window.Alpha - ss.Window.Beta) / 128, 11);
                int childCorr  = Math.Abs(Corrections.Get(in child));
                
                // as taken from CPW:
                // "If at depth 1 the margin does not exceed the value of a minor piece, at
                // depth 2 it should be more like the value of a rook."
                // we don't really follow this exactly, but our approach is kind of similar
                int margin = 100 + 92 * ss.Depth 
                    + (improving ? 0 : -23)   // not improving nodes => prune more
                    + see / 65                // tweak the margin based on SEE
                    + (allNode ? -10 : 0)     // all nodes prune more aggressively
                    + childCorr + windowSize; // measures of uncertainty
                
                // find the difference between alpha and static eval + margin
                int diff = col == Color.WHITE 
                    ? childStaticEval + margin - ss.Window.Alpha
                    : ss.Window.Beta - childStaticEval + margin;

                weight++;
                
                // if we didn't manage to raise alpha, prune this branch
                if (diff <= 0 && ss.Depth <= 4) {
                    CurNodes++;
                    if (!ignore3Fold) ThreeFold.Remove(child.Hash);
                    
                    continue;
                }
                
                // 9. FUTILITY REDUCTIONS
                // a very small idea i had, helps only a little bit. if the move
                // didn't fail low, but was very close to it, it is at least reduced
                if (diff <= 7 && ss.PriorReductions <= 4 && !improving && allNode)
                    reduction++;
            }
            
            // 7. QUIET REDUCTIONS
            // at very low depths, when there are way too many moves, and we aren't
            // optimistic about raising alpha, some of the late quiets are reduced
            if (!isCapture && !givesCheck && !improving && expandedNodes >= reduceQuietsThreshold)
                reduction++;
            
            // 10. SINGULAR EXTENSIONS
            // based on the tt score we set the singular beta bound, and perform a reduced search that
            // doesn't include the tt move. if this search fails low, we expect the tt move to be singular,
            // e.g. the only reasonable move, and it is extended
            if ((pvNode || cutNode) && ss.Depth >= 6 && ttMoveExists && expandedNodes == 1 && ttDepth >= ss.Depth - 3
                && ttFlags.HasFlag(col == Color.WHITE ? TT.ScoreFlags.LOWER_BOUND : TT.ScoreFlags.UPPER_BOUND)
                && !Score.IsMate(ttScore)) {

                // the singular beta is the tt score minus a small margin
                int sbOffset       = 47 + (ss.Depth - ttDepth);
                var singularWindow = col == Color.WHITE
                    ? new Window((short)(ttScore - sbOffset - 1), (short)(ttScore - sbOffset)) 
                    : new Window((short)(ttScore + sbOffset),     (short)(ttScore + sbOffset + 1));

                // it is important to exclude the tt move, as the following
                // search is supposed to evaluate the position without it
                ss.ExcludedMove = ttMove;
                
                // do the reduced, null-window search
                (short singScore, _) = Search<NonPVNode>(ref board, ss with {
                    Depth  = (sbyte)(ss.Depth * 2 / 5),
                    Window = singularWindow,
                }, ignore3Fold, cutNode);
                
                ss.ExcludedMove = default;

                // the score is below alpha, the tt move is singular, and is extended
                if (col == Color.WHITE 
                        ? singScore < ttScore - sbOffset 
                        : singScore > ttScore + sbOffset) {

                    weight += 2;
                    curDepth++;
                    
                    // double extension if tt move seems to be a good capture
                    if (ttCapture && ttBetaCutoff && ttDepth >= ss.Depth - 2)
                        curDepth++;
                }
                
                // 11. MULTI-CUT PRUNING
                // an additional idea to singular extensions - if the search score failed high
                // over the current beta, the node is pruned, as despite not having access to
                // the best move (tt move), we still failed high
                else if (!Score.IsMate(singScore) 
                         && (col == Color.WHITE ? singScore >= ss.Window.Beta : singScore <= ss.Window.Alpha)) {
                    
                    if (!ignore3Fold) ThreeFold.Remove(child.Hash);
                    return (singScore, []);
                }
                
                // 12. NEGATIVE EXTENSIONS
                // if the tt move isn't singular, and we cannot apply multi-cut, the tt move is
                // reduced to allow spending more time searching other moves, as they may be good
                else reduction++;
            }
            
            // 10. OTHER REDUCTIONS/EXTENSIONS
            // the search depth of the current move is lowered or raised
            // based on how interesting or important the move seems to be
            bool lateRootMove = rootNode && expandedNodes >=
                4 + (int)PVSControl.LastInstability / 2 + Math.Max(3 - CurIterDepth, 0);
            
            reduction += (lateRootMove                           ? 1 : 0)  // first few root moves are extended
                       + (!inCheck && !givesCheck && see <= -100 ? 1 : 0)  // bad captures are reduced
                       - (!ttMoveExists && moveCount == 1        ? 1 : 0); // single evasion extensions

            // increase reduction for moves with bad history
            reduction -= Math.Min(curScore / (80 + CurIterDepth * (25 + CurIterDepth)), 0);
            
            // apply the reduction, make sure we don't extend more than one ply
            reduction = Math.Max(reduction, 0);
            curDepth -= reduction;

            // 11. LATE MOVE PRUNING/REDUCTIONS
            // despite the fact that PVS searches only the first move with a full window, it didn't
            // work here. instead, a few early moves are searched fully, and the rest with a null
            // window. the number of moves searched fully is based on depth, pv and cutnode. if we
            // have a tt move, only it is searched fully
            int  maxExpNodes = (ttMoveExists ? 1 : 3) + (pvNode ? 4 : 0);
            bool skipLMP     = expandedNodes <= maxExpNodes || inCheck || givesCheck || rootNode || see >= 100;

            // late move pruning and common PVS logic are merged here. expected fail-low moves are
            // searched with a null alpha window at a reduced depth, and only if they somehow raise
            // alpha, a deeper, full window re-search is performed
            if (!skipLMP) {
                
                // if moveorder score is bad, reduction is larger. the score threshold scales with the current
                // iteration depth, as history values tend to be larger in deeper searches due to the butterfly
                // boards being cleared. the reduction is also based on see, whether we are improving and depth
                int scoreThreshold = -379 - 9 * CurIterDepth - (ttOptimistic ? 12 : 0);
                int R = 4
                    + (curScore < scoreThreshold ? 1 : 0)
                    - (improving  || see > 94    ? 1 : 0)
                    + (!improving && see < 0     ? 1 : 0)
                    + (ttCapture                 ? 1 : 0);
                
                // null window around alpha
                var nullWindowAlpha = col == Color.WHITE
                    ? new Window(ss.Window.Alpha, (short)(ss.Window.Alpha + 1)) 
                    : new Window((short)(ss.Window.Beta - 1), ss.Window.Beta);

                // once again a reduced depth search
                int score = SearchNext<NonPVNode>(ref child, 
                    new SearchState((sbyte)(ss.Ply + 1), (sbyte)(ss.Depth - R), ss.PriorReductions, nullWindowAlpha, ss.LastMove, ss.ExcludedMove, false),
                    ignore3Fold,
                    cutNode: true
                ).Score;
                
                if (col == Color.WHITE
                        ? score <= ss.Window.Alpha
                        : score >= ss.Window.Beta) {

                    // penalize the move in histories
                    int lmWeight = weight + Math.Max(0, ss.Depth - R);
                    StoreMoveHistory(ss.LastMove, curMove, isCapture, -lmWeight, -ss.Depth + R);
                        
                    if (!ignore3Fold) ThreeFold.Remove(child.Hash);
                    continue;
                }
            }

            // if LMP failed, and the move was reduced, the reduction is reverted
            if (!skipLMP && curDepth < ss.Depth - 1)
                curDepth++;
            
            // history weight of the current move
            weight += Math.Max(0, curDepth + (skipLMP ? 1 : 0));

            // the new search state for the child node
            var newSS = ss with {
                Ply             = (sbyte)(ss.Ply + 1),
                Depth           = (sbyte)curDepth,
                PriorReductions = (sbyte)(ss.PriorReductions + reduction - 1),
                LastMove        = curMove
            };
            
            // perform the full search. if we are in a pv node, the full search is also pv. in non pv nodes
            // the search is non pv. we can also get here after LMP fails, but even then the rules apply
            fullSearch = pvNode
                ? SearchNext<PVNode>(   ref child, newSS, ignore3Fold, cutNode: false)
                : SearchNext<NonPVNode>(ref child, newSS, ignore3Fold, cutNode: !cutNode);
            
            if (rootNode) {
                if (Options.PlayWorst) fullSearch.Score *= -1;

                // once search iterations start taking a bit longer, print intermediate
                // results for each move: 'info ... currmove x ...'
                if (PVSControl.TotalNodes >= 7_000_000 && !Abort)
                    PrintCurrMoveInfo(col, fullSearch.Score, ss.Window, curDepth, curMove, expandedNodes, fullSearch.PV);
            }
            
            // if the position turned out to be a draw earlier, the search
            // and all pruning is skipped to this point, where we do some
            // stuff with the move, considering its score to be zero
            skipPVS:
            if (!ignore3Fold) ThreeFold.Remove(child.Hash);

            // we somehow still failed low
            if (col == Color.WHITE
                    ? fullSearch.Score <= ss.Window.Alpha
                    : fullSearch.Score >= ss.Window.Beta) {
                
                StoreMoveHistory(ss.LastMove, curMove, isCapture, weight, curDepth);
            }

            // we went through all the pruning and didn't fail low
            // (this is the current best move for this position)
            else {
                // when using iterative deepening, the PV move from the previous
                // iteration is searched first. that means, even if our time runs
                // out, we may have still found a move better than the current one
                if (rootNode && !Abort)
                    NextBestMove = curMove; 
                
                // place the current move in front of the received pv to build a new pv
                pv = new Move[fullSearch.PV.Length + 1];
                Array.Copy(fullSearch.PV, 0, pv, 1, fullSearch.PV.Length);
                pv[0] = curMove;
                
                if (ss.LastMove != default)
                    ContinuationHistory.Add(ss.LastMove, curMove, ss.Depth / 2);
                
                // beta cutoff (see alpha-beta pruning); alpha is larger
                // than beta, so we can stop searching this branch, because
                // the other side wouldn't allow us to get here at all
                if (ss.Window.TryCutoff(fullSearch.Score, col)) {
                    StoreMoveHistory(ss.LastMove, curMove, isCapture, 2 * weight, ss.Depth);
                    
                    // there are both quiet and capture killer tables,
                    // which sort the move automatically, so don't worry
                    Killers.Add(curMove, ss.Depth);

                    // quit searching other moves and return this score
                    return (fullSearch.Score, pv);
                }
            }
        }
            
        // if we got here, it means we have searched through
        // the moves, but haven't got a beta cutoff
        return expandedNodes == 0

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

    // modify quiet, capture and continuation histories by the provided bonus/weight
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreMoveHistory(Move previous, Move move, bool capture, int weight, int contWeight) {
        if (previous != default)
            ContinuationHistory.Add(previous, move, contWeight);
                    
        // if the move caused a beta cutoff, it's history is increased
        if (!capture) QuietHistory.ChangeRep(move,   weight);
        else          CaptureHistory.ChangeRep(move, weight);
    }

    // print 'info currmove ...' once search iterations are a bit longer 
    private static void PrintCurrMoveInfo(Color col, short score, Window window, int depth, Move curMove, int expandedNodes, Move[] pv) {
        var info = $"info depth {depth + 1} currmove {curMove.ToLAN()} currmovenumber {expandedNodes}";
                
        // for the first move, we print the score every time, and also
        // its bound (lowerbound/upperbound) or no bound if exact. later
        // moves only print a score if they raise alpha or cause a cutoff
        if (expandedNodes == 1 || (col == Color.WHITE 
                ? score > window.Alpha 
                : score < window.Beta)) {

            // this magic is explained in PVSControl
            int mateScore = Score.GetMateInX(score);
            mateScore += Math.Abs(mateScore) % 2 * Math.Sign(mateScore);
            mateScore /= Game.EngineColor == Color.WHITE ? 2 : -2;
                    
            string correctedScore = Score.IsMate(score) 
                ? $"mate {mateScore}"
                : $"cp {Score.LimitScore(score) 
                        * (Game.EngineColor == Color.WHITE ? 1 : -1)}";
                    
            info += $" score {correctedScore}";
                    
            // add lowerbound/upperbound. this has to be corrected based on engine
            // color, as we are in a minimax framework, meaning it's color-relative
            if (score <= window.Alpha)
                info += Game.EngineColor == Color.WHITE ? " upperbound" : " lowerbound";
                    
            else if (score >= window.Beta)
                info += Game.EngineColor == Color.WHITE ? " lowerbound" : " upperbound";
            
            // if there is a PV, print it as well
            if (pv.Length > 0) {
                info += $" pv {curMove.ToLAN()}";
                info  = pv.Aggregate(info, (current, move) => current + $" {move.ToLAN()}");
            }
        }
        
        UCI.Log(info);
    }
}