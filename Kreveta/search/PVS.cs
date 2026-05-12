//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

#pragma warning disable CA5394

using Kreveta.consts;
using Kreveta.evaluation;
using Kreveta.movegen;
using Kreveta.moveorder;
using Kreveta.moveorder.history;
using Kreveta.moveorder.history.corrections;
using Kreveta.search.helpers;
using Kreveta.search.transpositions;
using Kreveta.uci;
using Kreveta.uci.options;

using System;
using System.Runtime.CompilerServices;

// ReSharper disable InconsistentNaming

namespace Kreveta.search;

internal static unsafe class PVS {

    // current search depth, excluding quiescence and reductions/extensions
    internal static int RootDepth;
    internal static int RootDelta;

    // essentially the highest ply achieved
    internal static int AchievedDepth;
    
    // total nodes searched this iteration
    internal static ulong CurNodes;

    // in endgames, we prefer not to use NMP at high depths
    internal static int MinNMPPly;
    
    /*
     * PRINCIPAL VARIATION
     * in PVS, the PV represents the variation (sequence of moves from the root), which the engine
     * considers to be the best. this applies to both sides, so in theory, if both sides were to
     * play exactly along the PV, they would play perfect chess. in practice, the PV isn't perfect,
     * though it is our mission to find the best possible. the first move of the PV also happens
     * to be the best move from the root, which the engine then sends back to the GUI
     */
    internal static Move[] PV = [];
    internal static Move   NextBestMove;
    internal static int    NextBestScore;

    // we use a pre-allocated table to store intermediate PVs during search
    private const  int      MaxPVDepth = 128;
    private static Move[][] _pvTable   = null!;
    private static int[]    _pvLen     = null!;
    
    // evaluated final score of the principal variation
    internal static short PVScore;

    // how many nodes did the best move's search take?
    internal static ulong BestMoveEffort;

    // once the spent time exceeds this value, the search is aborted
    private static long AbortTimeThreshold;

    private static Log2ReductionTable _reductionTable = null!;
    
    // check whether we should stop the search. this is the case when we have exceeded our time
    // budget, exceeded the searched nodes limit, or the search is forcefully stopped by the user
    internal static bool Abort 
        => UCI.ShouldAbortSearch
        || SearchControl.Stopwatch.ElapsedMilliseconds >= AbortTimeThreshold
        || SearchControl.TotalNodes + CurNodes         >= SearchControl.CurNodesLimit;

    // when a TT lookup is successful, but we for any reason cannot cut
    // off, the entry information is stored here not to repeat the lookup
    internal static TTLookupState LookupTT;
    internal static Move          TTMove;
    internal static short         TTScore;
    internal static ScoreType     TTFlags;
    internal static int           TTDepth;
    
    internal static void Init() {
        _pvLen   = new int[MaxPVDepth];
        _pvTable = new Move[MaxPVDepth][];
        
        for (int i = 0; i < MaxPVDepth; i++)
            _pvTable[i] = new Move[MaxPVDepth];
        
        _reductionTable = new Log2ReductionTable();
    }

    // increase the depth and do a re-search
    internal static void SearchDeeper(int aspirationAlpha, int aspirationBeta) {
        RootDepth++;

        // reset total nodes
        CurNodes = 0UL;

        AbortTimeThreshold = TM.TimeBudget != long.MaxValue 
            // approximately subtracting 1/128
            ? TM.TimeBudget - Math.Min(300, TM.TimeBudget >> 7)
            : long.MaxValue;

        // create more space for killers on the new depth
        Killers.Expand(RootDepth);

        // decrease quiet history values, as they shouldn't be as relevant now.
        // erasing them completely would, however, slow down the search
        QuietHistory.Shrink();
        CaptureHistory.Shrink();
        PieceToHistory.Shrink();
        ContinuationHistory.Age();

        // only initialize correction tables
        if (RootDepth == 1)
            Corrections.Clear();

        // store the pv from the previous iteration in tt
        // this should hopefully allow some faster lookups
        StorePVinTT(PV, RootDepth);

        // increase the number of plies we can hold
        ImprovingStack.Expand(RootDepth);
        ImprovingStack.UpdateStaticEval(Game.Board.StaticEval, ply: 0, Game.EngineColor);

        SearchState initialSearchState = new(
            ply:            0,
            depth:          RootDepth,
            priorReduction: 0,
            lastMove:       default,
            excludedMove:   default,
            followPv:       true,
            ignore3fold:    false
        );

        LookupTT = TTLookupState.NOT_PERFORMED;

        // actual start of the search tree
        PVScore = Search<RootNode>(ref Game.Board, aspirationAlpha, aspirationBeta, initialSearchState, false);
        PV      = _pvLen[0] > 0 ? _pvTable[0][.. _pvLen[0]] : [];

        SearchControl.TotalNodes += CurNodes;
    }

    // stores the pv in the transposition table.
    // needs the starting depth in order to store trustworthy entries
    private static void StorePVinTT(Move[] pv, int depth) {
        Board board  = Game.Board.CloneNoNNUE();
        int   weight = pv.Length + 3;

        // loop all pv-nodes
        for (int i = 0; i < pv.Length; i++) {
            int  score = PVScore * ((i & 0x1) == 0 ? 1 : -1);
            bool capt  = pv[i].Capture   != PType.NONE
                      || pv[i].Promotion == PType.PAWN;
            
            // store each pv move in TT
            TT.Store(board.Hash, --depth, i, Consts.MinValue, Consts.MaxValue, (short)score, pv[i]);
            
            // update histories for all moves, with progressively smaller bonuses
            StoreMoveHistory(board.SideToMove, i > 0 ? pv[i - 1] : default, pv[i], capt, weight - i, weight - i - 1);
            
            // play along the pv to store correct positions as well
            board.PlayMove(pv[i], false);
        }
    }

    // given a hash, find possible upcoming hashes present in ThreeFold. if any
    // of the two hashes are already there twice, we know there exists a drawing
    // move. this can allow us to cut off early, when draw >= beta
    internal static bool IsUpcomingRepetitionDraw(ulong hash) {
        var (a, b) = ThreeFold.GetUpcomingHashes(hash);

        // check whether either of the hashes is in the table twice already
        return a != 0UL && ThreeFold.WouldBeDraw(a)
            || b != 0UL && ThreeFold.WouldBeDraw(b);
    }

    // during the search, first check the transposition table for the score, if it's not there
    // just continue the search as usual. parameters need to be the same as in the search method itself
    private static short SearchNext<NodeType>(ref Board board, int alpha, int beta, SearchState ss, bool cutNode) 
        where NodeType : ISearchNodeType {

        LookupTT = TTLookupState.NOT_PERFORMED;
        
        // did we find the position and score?
        // also repeating positions cannot occur under 4 plies
        if (ss.Ply >= 4) {
            bool ttHit = TT.TryGetData(board.Hash, ss.Ply, out TTMove, out TTScore, out TTFlags, out TTDepth);
            
            // check whether the TT score is usable
            if (ttHit && TTDepth >= ss.Depth && (TTFlags.HasFlag(ScoreType.SCORE_EXACT)
                                              || TTFlags.HasFlag(ScoreType.UPPER_BOUND) && TTScore <= alpha
                                              || TTFlags.HasFlag(ScoreType.LOWER_BOUND) && TTScore >= beta)) {
                
                // increase the TT move's history if it cuts beta
                if (TTMove != default)
                    StoreTTMoveHistory(board.SideToMove, ss.LastMove, TTMove, ss.Depth, typeof(NodeType) == typeof(PVNode), TTScore, beta);
            
                return TTScore;
            }
            
            // save entry data not to repeat the lookup
            LookupTT = ttHit
                ? TTLookupState.FOUND
                : TTLookupState.DOES_NOT_EXIST;
        }

        // in case the position is not yet stored, we fully search it and then store it
        short score    = Search<NodeType>(ref board, alpha, beta, ss, cutNode);
        Move  bestMove = _pvLen[ss.Ply] > 0 ? _pvTable[ss.Ply][0] : default;
        
        // store the found score and best move in tt
        TT.Store(board.Hash, ss.Depth, ss.Ply, alpha, beta, score, bestMove);
        
        // store the current two-move sequence in countermove history - the previously
        // played move, and the best response (counter) to this move found by the search
        if (bestMove != default && ss.Depth > 4)
            CounterMoveHistory.Add(board.SideToMove, ss.LastMove, bestMove);
        
        // update this position's score in pawncorrhist. bounds have to be checked,
        // as a bound score is certainly not reliable enough to correct the eval
        if (!board.IsCheck && score > alpha && score < beta)
            Corrections.Update(board, score, ss.Depth, bestMove != default);

        return score;
    }
    
    /*
     * PRINCIPAL VARIATION SEARCH (NEGAMAX FRAMEWORK):
     * ply starts at zero and increases over time, while depth starts at the highest value and decreases.
     * the function recursively calls itself to generate a sort of "tree" of possible future positions,
     * and tracks the best possible path to go. computing all positions is impossible, so different pruning
     * and reduction techniques are used to skip likely irrelevant branches
     */
    private static short Search<NodeType>(ref Board board, int alpha, int beta, SearchState ss, bool cutNode)
        where NodeType : ISearchNodeType {
        
        bool rootNode = typeof(NodeType) == typeof(RootNode);
        bool pvNode   = typeof(NodeType) == typeof(PVNode) || rootNode;
        bool allNode  = !pvNode && !cutNode;
        
        // ReSharper disable once InvocationIsSkipped
        Assert.WindowCorrect(alpha, beta);
        Assert.NodeTypeCorrect(alpha, beta, pvNode, cutNode, allNode);
        
        _pvLen[ss.Ply] = 0;
        
        // either crossed the time budget or maximum nodes.
        // we also cannot abort the first iteration - no bestmove
        if (Abort && RootDepth > 1)
            return 0;
        
        // we reached depth zero or lower => evaluate the leaf node though qsearch
        if (ss.Depth <= 0) {
            short q =  Quiescence.Search(ref board, ss.Ply, alpha, beta, ss.Ply + 12, 64, ss.Ignore3Fold);
            
            LookupTT = TTLookupState.NOT_PERFORMED;
            return q;
        }
        
        // increase the nodes searched counter
        CurNodes++;

        // prior to searching any moves, check whether the current position has a known
        // repetition drawing move. if so, update alpha accordingly, and try to cut off
        if (!ss.Ignore3Fold && alpha < 0 && IsUpcomingRepetitionDraw(board.Hash)) {
            alpha = Score.GetNoisyDrawScore(CurNodes);

            if (alpha >= beta)
                return (short)alpha;
        }

        /*
         * 1. MATE DISTANCE PRUNING (~0 Elo)
         * if there's already an ensured mate found, we don't have to search
         * past the mate ply. this is applied to both winning and losing mates
         */
        if (Score.IsMate(alpha) && alpha > 0) {
            int matePly = Math.Abs(Score.GetMateInX(alpha));
            
            if (ss.Ply >= matePly) {
                LookupTT = TTLookupState.NOT_PERFORMED;
                return 0;
            }
        }

        // just to simplify who's turn it is
        Color col          = board.SideToMove;
        bool  excludedMove = ss.ExcludedMove != default;
        
        // data from the transposition table
        bool      ttHit = false, ttMoveExists = false, ttCapture = false;
        bool      ttExact = false, ttLowerBound = false, ttUpperBound = false;
        bool      ttOptimistic = false, ttBetaCutoff = false;
        Move      ttMove  = default;
        short     ttScore = 0;
        ScoreType ttFlags = default;
        int       ttDepth = 0;

        // when in singular search, the TT move is excluded, therefore
        // we don't care about anything related to TT in any sense at all
        if (!excludedMove) {
            
            // if we have retrieved TT data previously, use it
            if (LookupTT == TTLookupState.FOUND) {
                (ttMove, ttScore, ttFlags, ttDepth)
                    = (TTMove, TTScore, TTFlags, TTDepth); 
                
                ttHit = true;
            }
            
            // otherwise try to retrieve the data here
            else if (LookupTT != TTLookupState.DOES_NOT_EXIST)
                ttHit = TT.TryGetData(board.Hash, ss.Ply, out ttMove, out ttScore, out ttFlags, out ttDepth);
            
            ttMoveExists = ttHit && ttMove != default;
            ttCapture    = ttMoveExists && (ttMove.Capture != PType.NONE || ttMove.Promotion == PType.PAWN);
            
            // figure out the tt score type
            ttExact      = ttHit             && ttFlags.HasFlag(ScoreType.SCORE_EXACT);
            ttLowerBound = ttHit && !ttExact && ttFlags.HasFlag(ScoreType.LOWER_BOUND);
            ttUpperBound = ttHit && !ttExact && !ttLowerBound;
            
            /*
             * 2. ADDITIONAL TT CUTOFFS
             * if tt score is reliable enough, it may be used for early cutoffs in
             * non-PV nodes, but only in case it strongly supports the node type
             */
            int  cm = 26 + 18 * ss.Depth;
            bool canCutoff = allNode && ttScore + cm <= alpha
                          || cutNode && ttScore - cm >= beta;
        
            // now also check whether the tt bound fits the situation
            if (ttHit && ttDepth >= ss.Depth - 2 && canCutoff && (ttExact
                    || ttUpperBound && ttScore <= alpha
                    || ttLowerBound && ttScore >= beta)) {
            
                // we know from the previous conditions that beta is only cut in cutnodes
                if (cutNode && ttMoveExists)
                    StoreTTMoveHistory(col, ss.LastMove, ttMove, ss.Depth, false, ttScore, beta);

                return ttScore;
            }
        
            // is the tt score optimistic that we can raise alpha or cause a beta cutoff
            ttOptimistic = ttHit && (ttLowerBound || ttExact) && ttScore > alpha;
            ttBetaCutoff = ttOptimistic && ttDepth >= ss.Depth - 2 && (ttLowerBound || ttExact) && ttScore >= beta;
        }
        
        // make sure we reset this, even if we didn't use it
        LookupTT = TTLookupState.NOT_PERFORMED;
        
        // check whether we are still following the previous principal variation
        ss.FollowPV = ss.FollowPV && (rootNode || ss.Ply - 1 < PV.Length && PV[ss.Ply - 1] == ss.LastMove);

        // is the side to move currently in check?
        bool  inCheck    = board.IsCheck;
        bool  nonPawnMat = board.HasNonPawnMaterial(col);
        short staticEval = board.StaticEval;
        
        // update both improving flags
        bool improving    = !inCheck && ImprovingStack.IsImproving2Ply(ss.Ply, col);
        //bool oppWorsening = !inCheck && ImprovingStack.IsImproving1Ply(ss.Ply, col);

        /*
         * 3. HINDSIGHT REDUCTIONS/EXTENSIONS
         * the point is to take a "look back" at how much the previous move was reduced or extended.
         * based on the static evaluation difference over the last move, we evaluate whether the
         * previous reduction/extension was adequate, and if it wasn't, we partially revert it here
         */
        if (!rootNode && !excludedMove) {
            int evalDelta = ImprovingStack.Delta(ss.Ply - 1, ss.Ply, col);

            // if the previous move is reduced a lot, it means we thought it was bad for the previous
            // side to move. however, if the static eval difference over the last move is improving
            // a lot for the opposite side, we revert some of the reduction
            if (ss.PriorReduction >= 3 && evalDelta < -85)
                ss.Depth++;

            // on the other hand, if a move wasn't reduced that much, but it is improving even more for
            // us, then it was probably bad, and it should have been reduced more, which is fixed here
            if (ss.PriorReduction < 3 && ss.Depth >= 2 && evalDelta > 195)
                ss.Depth--;
        }

        // a fairly solid improving idea
        improving |= staticEval >= beta;

        bool ttScoreAdjusted = false;
        
        // if tt score is reliable, adjust the static eval used for pruning
        if (ttHit && (ttExact
                      || ttScore > staticEval && ttLowerBound
                      || ttScore < staticEval && ttUpperBound)
                  && !Score.IsMate(ttScore)) {

            staticEval      = ttScore;
            ttScoreAdjusted = true;
        } 
        else staticEval += (short)Math.Clamp((int)Corrections.Get(in board), -5, 5);

        /*
         * 4. RAZORING
         * if the static evaluation is very bad, the move expansion is skipped, and the qsearch score is
         * returned instead. this cannot be done when in check, and the qsearch score must be validated
         */
        if (!inCheck && (ss.Depth <= 3 || !ttCapture) && !ss.FollowPV) {
            int margin = 519 + 365 * ss.Depth * ss.Depth;

            if (improving) margin += 33;
            if (allNode)   margin -= 27;

            // if the static eval was adjusted using a tt score, we assume
            // it's more precise, and therefore we can lower the margin
            if (ttScoreAdjusted)
                margin = margin * 19 / 20;

            if (staticEval + margin < alpha) {
                
                // perform the quiescence search and ensure it actually fails low
                int qEval = Quiescence.Search(ref board, ss.Ply, alpha, beta, ss.Ply + 12, 64, ss.Ignore3Fold);
                if (qEval <= alpha) return (short)qEval;
            }
        }
        
        /*
         * 5. STATIC NULL MOVE PRUNING
         * also called reverse futility pruning; if the static eval at close-to-leaf
         * nodes fails high despite subtracting a margin, prune this branch
         */
        if (!inCheck && improving && (allNode || cutNode) && !ss.FollowPV) {
            int margin = 204 + 282 * ss.Depth * ss.Depth;
            if (ttScoreAdjusted) margin = margin * 15 / 16;

            if (staticEval - margin > beta)
                return (short)((2 * beta + staticEval) / 3);
        }
        
        /*
         * 6. SMALL PROBCUT IDEA
         * inspired by Stockfish, but modified quite a bit. if the tt score isn't reliable enough
         * to cause the search to be skipped completely, we drop into quiescence search
         */
        int probcutMargin = 523 + 27 * (ss.Depth - ttDepth) + 3 * ss.Depth;
        int probcutBeta   = beta + probcutMargin;
        
        // make sure the tt score is the correct bound
        if (ttHit && ttDepth >= ss.Depth - 4 && ss.Depth >= 3 && cutNode && !Score.IsMate(ttScore)
            && (ttLowerBound || ttExact) && ttScore >= probcutBeta) {
            
            // drop into quiescence search
            return Quiescence.Search(ref board, ss.Ply, alpha, beta, ss.Ply + 12, 64, ss.Ignore3Fold);
        }
        
        /*
         * 7. NULL MOVE PRUNING
         * we assume that in every position there is at least one move that improves it. first,
         * we play a null move (only switching sides), and then perform a reduced search with
         * a null window around beta. if the returned score fails high, we expect that not
         * skipping our move would "fail even higher", and thus can prune this node
         */
        int nmpEvalMargin = 3 * (ss.Depth + (improving ? 1 : 0));
        if (ttScoreAdjusted) nmpEvalMargin = nmpEvalMargin * 2 / 3;
        
        if (ss.Ply >= MinNMPPly // minimum ply for nmp
            && !inCheck         // don't prune when in check
            && nonPawnMat       // don't prune in absolute endgames
            
            // make sure static eval is over or at least close to beta to not
            // waste time searching positions, which probably won't fail high
            && staticEval >= beta - nmpEvalMargin) {
            
            // child with a move skipped
            var nullChild = board.Clone() with {
                EnPassantSq = 64,
                SideToMove  = (Color)((int)col ^ 1),
                IsCheck     = false
            };
            
            // instead of recomputing the static eval for the null child position, we take the current
            // eval, and mimic the eval difference of switching the side to move (2 * tempo value).
            // this obviously isn't most precise, as NNUE cannot be imitated, but it seems to help
            nullChild.StaticEval  = (short)(-nullChild.StaticEval + 12);
            nullChild.Hash       ^= ZobristHash.WhiteToMove;
            nullChild.Hash       ^= board.EnPassantSq != 64 ? ZobristHash.EnPassant[board.EnPassantSq & 7] : 0UL;
            
            // update the improving stack for the null move search
            ImprovingStack.UpdateStaticEval(nullChild.StaticEval, ss.Ply + 1, 1 - col);

            // scale reduction when eval is much over beta
            int delta = staticEval - beta;
            
            // the actual depth reduction
            int R = 7 + ss.Depth / 3 
                      + delta / 365
                      + (cutNode || ttBetaCutoff ? 1 : 0);
            
            // perform the reduced search
            short nmpScore = (short)-SearchNext<NonPVNode>(
                ref nullChild,
                -beta, -beta + 1,
                new SearchState(
                    ply:            ss.Ply   + 1,
                    depth:          ss.Depth - R,
                    priorReduction: 0,
                    lastMove:       default,
                    excludedMove:   ss.ExcludedMove,
                    followPv:       false,
                    ignore3fold:    true
                ),
                cutNode: false
            );
            
            // if we failed high, prune this node
            if (nmpScore >= beta) {
                
                // if verification search isn't needed, return the score
                if (ss.Depth <= 22)
                    return nmpScore;

                // disable null move pruning early in verification search
                int temp  = MinNMPPly;
                MinNMPPly = ss.Ply + (ss.Depth - R) * 3 / 4;
                
                // do the verification search
                nmpScore = SearchNext<NonPVNode>(
                    ref board,
                    beta - 1, beta,
                    ss with { Depth = ss.Depth - R },
                    cutNode: false
                );

                MinNMPPly = temp;

                // check once again
                if (nmpScore >= beta)
                    return nmpScore;
            }
        }

        // global reductions are applied to all expanded moves
        int   globalReduction = 0;
        ulong lastNodes       = 0UL;
        
        /*
         * 8. INTERNAL ITERATIVE REDUCTIONS
         * if the node we are in doesn't have a stored best move in TT, we reduce the depth in hopes of finishing
         * the search sooner and populating the TT for next iterations or occurences. the depth and ply conditions
         * are important, as reducing too much in the early iterations produces very wrong results
         */
        if (!ttMoveExists && !inCheck && pvNode && !excludedMove && !ss.FollowPV
            && ss.Depth >= 5 && ss.Ply >= 3 && ss.PriorReduction <= 5) {

            globalReduction++;
            ss.Depth--;
        }
        
        // after this move index threshold all quiets are reduced
        int reduceQuietsThreshold = 41 + 3 * ss.Depth * ss.Depth
            + (inCheck      || !allNode  ? 1000 : 0)
            + (ttOptimistic || improving ? 5    : 0);
        
        // was moveorder score assigning already performed?
        bool       scoresAssigned = false;
        int        moveCount      = 0;
        Span<Move> legalMoves     = stackalloc Move[Consts.MoveBufferSize];
        Span<int>  moveScores     = stackalloc int [Consts.MoveBufferSize];
        Span<int>  seeScores      = stackalloc int [Consts.MoveBufferSize];
        
        // information about what we've already searched
        int expandedNodes = 0;
        
        // used for LMP move count threshold tuning
        int gamePhase = board.GamePhase();
        int initAlpha = alpha;
        
        // the move index must be larger than this number to allow LMR
        int lmrMinMoves = (ttMoveExists ? 1 : 3)
                        + (pvNode       ? 4 : 0);

        // loop through possible moves
        while (true) {
            Move curMove;
            int  see, curScore = 0;

            // when in the first iteration, check if there's a known TT move to be expanded first
            if (expandedNodes == 0 && ttMoveExists) {
                curMove = ttMove;
                see     = SEE.GetMoveScore(in board, col, ttMove);
            }
            
            // otherwise generate all legal moves, and use regular moveorder
            else {
                if (!scoresAssigned) {
                    scoresAssigned = true;
                    moveCount      = Movegen.GetLegalMoves(ref board, legalMoves);
                    
                    LazyMoveOrder.AssignScores(in board, ss.Depth, ss.LastMove, legalMoves, moveScores, seeScores, moveCount,
                        // we know for a fact that quiets with SEE under this value would be pruned either way,
                        // so we can save some computation time by pruning them immediately, and not wastingly
                        // retrieving their various history values
                        seePruneQuiet: -24 * (ss.Depth - 1) * (ss.Depth - 1) - (inCheck ? 5000 : 0)
                    );

                    // if we've had a TT move already checked, it must be removed from the list
                    if (ttMove != default) {
                        for (int i = 0; i < moveCount; i++) {
                            if (legalMoves[i] != ttMove)
                                continue;
                            
                            legalMoves[i] = default;
                            break;
                        }
                    }
                    
                    // we've already initialized the move count threshold for LMR above, but now
                    // it's tweaked a little bit further based on move count, and a game phase estimate
                    int p       = lmrMinMoves * (380 + gamePhase) / 445;
                    int m       = lmrMinMoves * (75  + moveCount) / 105;
                    lmrMinMoves = Math.Max(1, (2 * m + p) / 3);
                }
                
                // instead of sorting all moves at the beginning, they are just assigned scores, and then selected
                // one by one during the expansion. this allows us to gain performance, when move ordering is good,
                // as when an early beta cutoff occurs, the remaining moves don't have to be ordered
                curMove = LazyMoveOrder.NextMove(legalMoves, moveScores, seeScores, moveCount, out curScore, out see);

                // once moveorder returns default, there aren't any moves left
                if (curMove == default) break;
            }

            // if we have a searchmoves restriction, skip other moves in root
            if (rootNode && Game.SearchMoves.Count > 0 && !Game.SearchMoves.Contains(curMove))
                continue;

            // if the current move is excluded from search by singular extensions
            if (curMove == ss.ExcludedMove)
                continue;
            
            expandedNodes++;

            // once all candidate cutoff moves have been searched, the cut node turns
            // into an all node, as none of the following moves are expected to be good
            if (cutNode && curScore < 0) {
                cutNode = false;
                allNode = true;
            }
            
            bool isCapture = curMove.Capture   != PType.NONE 
                          || curMove.Promotion == PType.PAWN;
            
            Assert.True(isCapture || see <= 0, "quiets can't have positive SEE values");
            Assert.True(see >= -EvalTables.PieceValues[4] 
                     && see <=  EvalTables.PieceValues[4], "SEE value higher/lower than queen value");

            // delta represents the window size
            int delta = beta - alpha;
            if (rootNode) RootDelta = delta;
            
            // base reduction of 1 ply; can be turned into an extension
            int curDepth  = ss.Depth - 1;
            int r         = _reductionTable[ss.Depth, expandedNodes, delta];
            int reduction = (r > 2850 ? 1 : 0) + (r > 4096 ? 1 : 0);
            
            /*
             * 9. FUTILITY PRUNING FOR CAPTURES
             * we can fairly safely eliminate some captures, if the material gain plus some margin still don't
             * raise alpha. the concept is essentially identical to delta pruning in quiescence search. we can
             * also save some time by evaluating this prior to cloning and playing moves on the board
             */
            if (isCapture && ss.Ply >= 3 && ss.Depth < 6) {
                int captValue = EvalTables.PieceValues[(int)curMove.Capture];
                int promValue = EvalTables.PieceValues[(int)curMove.Promotion];
                
                // the margin is kind of similar to what we use in delta pruning
                int futilityMargin = 125 + (65 + see / 50) * ss.Depth
                                         + captValue + promValue;

                if (staticEval + futilityMargin <= alpha) {
                    CurNodes++;
                    continue;
                }
            }
            
            /*
             * 10. SEE PRUNING FOR QUIETS
             * quiet moves can never have positive see. negative see of a quiet means it almost
             * certainly hangs a piece. here we try to prune such moves, as they shouldn't matter
             */
            if (!inCheck && !isCapture && see < -24 * curDepth * curDepth) {
                CurNodes++;
                continue;
            }
            
            // use the pool slot unless we are in a singular extension search
            Board child = board.Clone(excludedMove ? -1 : ss.Ply + 1);
            child.PlayMove(curMove, updateStaticEval: true);
            
            ulong pieceCount      = ulong.PopCount(child.Occupied);
            short childStaticEval = child.StaticEval;
            bool  givesCheck      = child.IsCheck;
            int   weight          = pvNode ? 1 : 0;

            // update this move's static eval difference history. captures are too noisy,
            // so they cannot be used. also, when in check, static eval isn't computed,
            // so storing anything would just pollute the actual data
            if (!isCapture && !(inCheck || givesCheck)) {
                int seDiff = -childStaticEval - staticEval;
                StaticEvalDiffHistory.Add(curMove, seDiff);
            }
            
            // since we skip child search in draw positions, we must initialize the score in advance
            int searchScore = 0;
            
            // check the position for a 3-fold repetition draw. it is very important
            // that we also remove this move from the stack, which must be done anywhere
            // where this loop is exited
            bool isThreeFold = !ss.Ignore3Fold && ThreeFold.AddAndCheck(child.Hash);
            
            // create some deterministic noise for three-fold repetition draws, which
            // apparently helps avoid weird loops or blindness
            if (isThreeFold)
                searchScore = Score.GetNoisyDrawScore(CurNodes);
            
            // if there is a known draw according to chess rules (either 50 move rule
            // or insufficient mating material), all pruning and reductions are skipped
            if (isThreeFold
             || child.HalfMoveClock >= 100
             || pieceCount <= 4 && isCapture && Eval.IsInsufficientMaterialDraw(child.Pieces, pieceCount)) {

                _pvLen[ss.Ply + 1] = 0;
                goto skipPVS;
            }
            
            // apply the correction to the static eval (no correction when in check)
            int childCorr    = inCheck ? 0 : Corrections.Get(in child);
            childStaticEval += (short)Math.Clamp(childCorr, -5, 5);
            
            // once again update the static eval in the improving stack,
            // but this time after the move has been already played
            ImprovingStack.UpdateStaticEval(childStaticEval, ss.Ply + 1, 1 - col);
            
            improving  = !inCheck && ImprovingStack.IsImproving2Ply(ss.Ply + 1, col);
            improving |= -childStaticEval >= beta;
            
            bool isLosing   = Score.IsLoss(alpha);
            bool hangsPiece = !isCapture && !givesCheck && curScore < -125 && see < 0;
            
            /*
             * 11. FUTILITY PRUNING
             * we try to discard moves near the leaves, which have no potential of raising alpha. in usual
             * implementations, the futility margin represents the highest achievable material gain through
             * a move, and is added to the parent's static evaluation. if the eval plus this margin don't
             * raise alpha, the move is pruned. in our case, we actually add the margin to the child's static
             * eval, e.g. after the move has been played. our margin is just a safety precaution
             */
            if (!inCheck && !givesCheck && !isLosing && nonPawnMat && (!ss.FollowPV || hangsPiece)
                && expandedNodes > 1 && ss.Ply >= 4 && ss.Depth <= (allNode ? 5 : 4)) {
                /*
                 * As taken from CPW:
                 * "If at depth 1 the margin does not exceed the value of a minor piece, at
                 *  depth 2 it should be more like the value of a rook."
                 */
                int futilityMargin = 81 + 92 * ss.Depth + (improving ? 19 : 0)
                                        + see / 65 + Math.Abs(childCorr); // a measure of uncertainty
                
                // if we didn't manage to raise alpha, prune this branch
                if (-childStaticEval + futilityMargin <= alpha) {
                    CurNodes++;
                    if (!ss.Ignore3Fold) ThreeFold.Remove(child.Hash);
                    
                    continue;
                }
            }
            
            /*
             * 12. SINGULAR EXTENSIONS
             * based on the TT score we set the singular beta bound, and perform a reduced search that
             * doesn't include the TT move. if this search fails low, we expect the TT move to be singular,
             * e.g. the only reasonable move, and it is extended.
             */
            if (expandedNodes == 1 && (pvNode || cutNode) && ttMoveExists && ss.Depth >= 6
                && ttDepth >= ss.Depth - 3 && ttLowerBound && !Score.IsMate(ttScore)) {

                // the singular beta is the tt score minus a small margin
                int offset = 47 + (ss.Depth - ttDepth);
                int sAlpha = ttScore - offset - 1;
                
                // it is important to exclude the tt move, as the following
                // search is supposed to evaluate the position without it
                ss.ExcludedMove = ttMove;
                if (!ss.Ignore3Fold) ThreeFold.Remove(child.Hash);

                int depth = ss.Depth * 2 / 5;
                
                // do the reduced, null-window search
                short singScore = Search<NonPVNode>(
                    ref board,
                    sAlpha, sAlpha + 1,
                    ss with {
                        Depth          = depth,
                        PriorReduction = ss.Depth - depth + globalReduction
                    },
                    cutNode
                );
                
                if (!ss.Ignore3Fold) ThreeFold.AddAndCheck(child.Hash);
                ss.ExcludedMove = default;
                
                // the singular extension search ran at the same ply and may have written
                // to _pvLen[ss.Ply]. restore it to 0, so the parent's childLen read is clean
                _pvLen[ss.Ply] = 0;

                // the score is below alpha, the tt move is singular, and is extended
                if (singScore <= sAlpha) {
                    weight += 2;
                    
                    // further extensions if tt move seems to be good and other moves terrible
                    bool extend1 = ttCapture && ttBetaCutoff && ttDepth >= ss.Depth - 2;
                    bool extend2 = singScore < sAlpha - 70 - 5 * (ss.Depth - ttDepth) + (ttBetaCutoff ? 10 : -10) + (ttCapture ? 15 : -15);
                    
                    reduction -= 1 + (extend1 ? 1 : 0) + (extend2 ? 1 : 0);
                }
                
                /*
                 * 13. MULTI-CUT PRUNING
                 * an additional idea to singular extensions - if the search score failed high over the current beta,
                 * the node is pruned, as despite not having access to the best move (TT move), we still failed high
                 */
                else if (singScore >= beta && !Score.IsMate(singScore)) {
                    if (!ss.Ignore3Fold) ThreeFold.Remove(child.Hash);
                    return singScore;
                }
                
                /*
                 * 14. NEGATIVE EXTENSIONS
                 * if the TT move isn't singular, and we cannot apply multi-cut, the TT move is
                 * reduced to allow spending more time searching other moves, as they may be good
                 */
                else reduction++;
            }
            
            /*
             * 15. OTHER REDUCTIONS/EXTENSIONS
             * the search depth of the current move is lowered or raised based on how interesting, important,
             * or relevant the move seems to be. these reductions/extensions do not apply to LMR, as LMR has
             * its own reduction system. we also try to balance these out by hindsight reductions/extensions
             */
            
            // at very low depths, when there exist way too many available moves, and we aren't
            // optimistic about raising alpha (according to TT), some of the late quiets are reduced
            if (!isCapture && !givesCheck && !improving && expandedNodes >= reduceQuietsThreshold)
                reduction++;
            
            // based on the stability of the search, we determine some number of root moves
            // that are going to be searched full. all following (late) root moves are reduced
            if (rootNode && expandedNodes >= 4 + (int)SearchControl.LastInstability / 2
                                               + Math.Max(3 - RootDepth, 0))
                reduction++;

            // both quiets and captures that likely lose material are reduced
            if (!inCheck && !givesCheck && see <= -100)
                reduction++;
            
            // any material-losing moves are further reduced in allnodes
            if (!inCheck && allNode && see < 0)
                reduction++;

            // single evasion extensions - if only one legal move exists, extend it. this is very cheap,
            // as the search doesn't really expand at all. however, since we first check the TT move before
            // actually generating all legal moves, we might easily miss a single evasion if it is in TT
            if (!ttMoveExists && moveCount == 1)
                reduction--;
            
            // apply the reduction
            curDepth -= reduction;

            /*
             * 17. LATE MOVE REDUCTIONS
             * despite the fact that PVS searches only the first move with a full window, it didn't work here. instead,
             * a few early moves are searched fully, and the rest with a null window. the number of moves searched fully
             * is based on depth, pv and cutnode. if we have a tt move, only it is searched fully
             */
            
            // under these conditions, LMR is skipped
            bool skipLMR = rootNode || expandedNodes <= lmrMinMoves || ss.Depth <= 2
                        || inCheck  || givesCheck                   || isLosing
                        || see >= 100;

            // now, if we don't want to skip LMR on this move, we perform the LMR search here.
            // the late move reduction is kept separate from the rest of reductions/extensions.
            // if the LMR search fails low as expected, the move is pruned
            if (!skipLMR) {

                // the base reduction is based on move index
                //int R = 3 + (expandedNodes > moveCount / 3 ? 1 : 0);
                
                // TODO - IDEAS #2, #3, #4, #5
                int R = Math.Max(
                    3 + (expandedNodes > moveCount / 3 ? 1 : 0),
                    r / 600
                );
                
                // moves with history scores below a certain threshold are reduced more
                if (curScore < -379 - 9 * RootDepth)
                    R++;

                // reduce less when improving, but when not improving, only reduce more when SEE is negative
                R += improving ? -1
                               : see < 0 ? 1 : 0;

                // reduce more when the TT move is a capture, as we're probably missing it here
                if (ttCapture) R++;

                // the move likely hangs a piece; this further incorporates SEE and history
                if (hangsPiece) R++;

                // once again a reduced depth search
                searchScore = -SearchNext<NonPVNode>(
                    ref child,
                    -alpha - 1, -alpha,
                    ss with {
                        Ply            = ss.Ply   + 1,
                        Depth          = ss.Depth - R,
                        PriorReduction = R - 1 + globalReduction,
                        LastMove       = curMove,
                        FollowPV       = false
                    },
                    cutNode: true
                );
                
                // the late move indeed failed low as expected
                if (searchScore <= alpha) {

                    // penalize the move in histories
                    int lmWeight = weight + Math.Max(0, ss.Depth - R);
                    StoreMoveHistory(col, ss.LastMove, curMove, isCapture, -lmWeight, -ss.Depth + R);
                        
                    if (!ss.Ignore3Fold) ThreeFold.Remove(child.Hash);
                    continue;
                }

                /*
                 * 18. POST-LMR EXTENSIONS
                 * if LMR failed, and the move had been previously reduced, the reduction is reverted by 1 ply; and
                 * if LMR not only failed, but failed a lot over beta, it is extended regardless of prior reductions
                 */
                int revert = 0;
                
                if (curDepth < ss.Depth - 1)  revert++;
                if (searchScore >= beta + 75) revert++;

                // apply the extension
                reduction -= revert;
                curDepth  += revert;
            }
            
            // the new search state for the child node
            var childSearchState = ss with {
                Ply            = ss.Ply + 1,
                Depth          = curDepth,
                PriorReduction = reduction + globalReduction,
                LastMove       = curMove
            };

            // reset the child pv
            _pvLen[ss.Ply + 1] = 0;
            
            // perform the full search. if we are in a pv node, the full search is also pv. in non pv nodes
            // the search is non pv. we can also get here after LMR fails, but even then the rules apply
            searchScore = pvNode && alpha + 1 != beta
                ? -SearchNext<PVNode>(   ref child, -beta, -alpha, childSearchState, cutNode: false)
                : -SearchNext<NonPVNode>(ref child, -beta, -alpha, childSearchState, cutNode: !cutNode);
            
            if (rootNode) {
                
                // this UCI option makes the engine play the worst possible moves, which is
                // achieved by performing actual searches, but inverting the scores at root
                if (Options.PlayWorst)
                    searchScore *= -1;

                // once search iterations start taking a bit longer, print intermediate
                // results for each move: 'info ... currmove x ...'
                if (SearchControl.TotalNodes >= 7_000_000 && !Abort)
                    PrintCurrMoveInfo(searchScore, alpha, beta, curDepth, curMove, expandedNodes, ss.Ply + 1);

                // when an elo level is set, limit deep mate paths
                if (Options.UCI_LimitStrength && Score.IsMate(searchScore)) {
                    int x = Math.Abs(Score.GetMateInX(searchScore));

                    if (x > Math.Max(3, 10 - (2000 - Options.UCI_Elo) / 100))
                        searchScore = Consts.RNG.Next(-1000, 1000);
                }

                // how many nodes did we spend on the current move?
                ulong nodesEffort = CurNodes - lastNodes;
                lastNodes         = CurNodes;

                // keep track of the effort for the best move yet
                if (searchScore > alpha)
                    BestMoveEffort = nodesEffort;
            }
            
            // history weight of the current move
            weight += Math.Max(0, curDepth + (skipLMR ? 1 : 0));
            
            // if the position turned out to be a draw earlier, the search and all pruning is skipped
            // to this point, where we do some stuff with the move, considering its score to be zero
            skipPVS:
            if (!ss.Ignore3Fold) ThreeFold.Remove(child.Hash);
            
            // we somehow still failed low
            if (searchScore <= alpha)
                StoreMoveHistory(col, ss.LastMove, curMove, isCapture, weight, curDepth);

            // we went through all the pruning and didn't fail low
            // (this is the current best move for this position)
            else {
                
                // when using iterative deepening, the PV move from the previous
                // iteration is searched first. that means, even if our time runs
                // out, we may have still found a move better than the current one
                if (rootNode && !Abort) {
                    NextBestMove  = curMove;
                    NextBestScore = searchScore;
                }
                
                // place the current move in front of the received pv to build a new pv
                int childLen = _pvLen[ss.Ply + 1];
                _pvTable[ss.Ply][0] = curMove;
                
                Array.Copy(_pvTable[ss.Ply + 1], 0, _pvTable[ss.Ply], 1, childLen);
                _pvLen[ss.Ply] = childLen + 1;
                
                // update only the continuation history for this move
                if (ss.LastMove != default)
                    ContinuationHistory.Add(ss.LastMove, curMove, ss.Depth / 2);
                
                // beta cutoff (see alpha-beta pruning); alpha is larger than beta, so we
                // can stop searching this branch, because the other side wouldn't allow
                // us to get here at all
                if (searchScore >= beta) {
                    StoreMoveHistory(col, ss.LastMove, curMove, isCapture, 2 * weight, ss.Depth);
                    
                    // there are both quiet and capture killer tables,
                    // which sort the move automatically, so don't worry
                    Killers.Add(curMove, ss.Depth);

                    // quit searching other moves and return this score
                    return (short)searchScore;
                }
                
                /*
                 * 19. ADDITIONAL REDUCTIONS
                 * once we have found at least one move that raises the initial alpha,
                 * we reduce all following moves by 1 ply. this action shouldn't be
                 * repeated, it only applies to the first such move
                 */
                if (alpha == initAlpha && ss.Depth is > 2 and < 14) {
                    globalReduction++;
                    ss.Depth--;
                }
                
                // raise alpha, as the current score is higher than the previous alpha.
                // we've already filtered away fail-lows in the previous if-statement
                alpha = searchScore;
            }
        }
        
        // if we got here, it means we have searched through
        // the moves, but haven't got a beta cutoff

        // we didn't expand any nodes - terminal node
        // (no legal moves exist)
        if (expandedNodes == 0) {
            _pvLen[ss.Ply] = 0;
            
            return inCheck
                // if we are checked this means we got mated
                ? Score.GetMateScore(ss.Ply)

                // if we aren't checked, we return draw (stalemate)
                : (short)0;
        }
        
        // otherwise return the bound score as usual
        return (short)alpha;
    }

    // modify quiet, capture, pieceto, and continuation histories for a certain
    // move by the provided bonus/weight. negative weight means the move is bad,
    // and its history values should be lowered
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreMoveHistory(Color col, Move previous, Move move, bool capture, int weight, int contWeight) {
        
        // conthist requires the previous move to be present
        if (previous != default)
            ContinuationHistory.Add(previous, move, contWeight);
        
        // modify the respective histories
        if (!capture) {
            QuietHistory.ChangeRep(move, weight);
            PieceToHistory.Store(col, move, weight);
        }
        else CaptureHistory.ChangeRep(move, weight);
    }

    // when a TT cutoff occurs, and the TT score cuts beta, the history of the TT move
    // is modified. although no search was conducted, if the cutoff hadn't happened,
    // the move would be searched, and the history would be changed either way
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreTTMoveHistory(Color col, Move previous, Move ttMove, int depth, bool pvNode, int ttScore, int beta) {
        
        // as opposed to regular history weights, for the TT move
        // the weight scales with by how much beta was exceeded
        int delta = ttScore - beta;
        
        // of course don't bother with histories unless beta was cut
        if (delta >= 0) {
            bool ttCapture = ttMove.Capture != PType.NONE || ttMove.Promotion == PType.PAWN;
                
            // the weight is also increased for PV nodes
            int weight = 1 + Math.Min(
                depth / 3 + (pvNode ? 1 : 0),
                depth / 7 + delta / 70);
            
            StoreMoveHistory(col, previous, ttMove, ttCapture, weight, weight * 2 / 3);
        }
    }

    // print 'info currmove ...' once search iterations are a bit longer 
    private static void PrintCurrMoveInfo(int score, int alpha, int beta, int depth, Move curMove, int expandedNodes, int childPly) {
        var info = $"info depth {depth + 1} currmove {curMove.ToLAN()} currmovenumber {expandedNodes}";
                
        // for the first move, we print the score every time, and also
        // its bound (lowerbound/upperbound) or no bound if exact. later
        // moves only print a score if they raise alpha or cause a cutoff
        if (expandedNodes == 1 || score > alpha) {
            
            // this magic is explained in PVSControl
            int mateScore = Score.GetMateInX(score);
            mateScore += Math.Abs(mateScore) % 2 * Math.Sign(mateScore);
            mateScore /= 2;
                    
            string correctedScore = Score.IsMate(score)
                ? $"mate {mateScore}"
                : $"cp {Score.LimitScore(score)}";
            
            info += $" score {correctedScore}";
                    
            // add lowerbound/upperbound. this has to be corrected based on engine
            // color, as we are in a minimax framework, meaning it's color-relative
            if      (score <= alpha) info += " upperbound";
            else if (score >= beta)  info += " lowerbound";
            
            // if there is a PV, print it as well
            if (_pvLen[childPly] > 0) {
                info += $" pv {curMove.ToLAN()}";

                for (int i = 0; i < _pvLen[childPly]; i++)
                    info += $" {_pvTable[childPly][i].ToLAN()}";
            }
        }
        
        UCI.Log(info);
    }
    
    internal static void Reset() {
        RootDepth  = 0;
        AchievedDepth = 0;
        CurNodes      = 0UL;
        PVScore       = 0;
        PV            = [];
        NextBestMove  = default;

        LookupTT = TTLookupState.NOT_PERFORMED;
        TTMove   = default;
        TTScore  = 0;
        TTFlags  = default;
        TTDepth  = 0;
        
        Array.Clear(_pvLen, 0, _pvLen.Length);
        for (int i = 0; i < MaxPVDepth; i++)
            Array.Clear(_pvTable[i], 0, _pvTable[i].Length);

        ImprovingStack.Expand(0);

        Killers.Clear();
        QuietHistory.Clear();
        CaptureHistory.Clear();
        PieceToHistory.Clear();
        CounterMoveHistory.Clear();
        ContinuationHistory.Clear();
        Corrections.Clear();
        
        // when playing a full game, storing the SE diff history values helps
        // moveorder in the next search. we want to age the values a lot, though,
        // so they don't remain there forever
        if (!Game.FullGame) StaticEvalDiffHistory.Clear();
        else                StaticEvalDiffHistory.Age();
        
        TT.Clear();
        if (!Game.FullGame) SETT.Realloc();
    }
}

#pragma warning restore CA5394