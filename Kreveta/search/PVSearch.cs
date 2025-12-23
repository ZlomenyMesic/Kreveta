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
        PawnCorrections.Realloc();
        KingCorrections.Clear();

        // store the pv from the previous iteration in tt
        // this should hopefully allow some faster lookups
        StorePVinTT(PV, CurIterDepth);

        // increase the number of plies we can hold
        improvStack.Expand(CurIterDepth);

        SearchState defaultSS = new(
            ply:         0, 
            depth:       (sbyte)CurIterDepth,
            extensions:  0,
            window:      aspiration,
            //penultimate: default,
            previous:    default,
            isPv:    true
        );

        // actual start of the search tree
        (PVScore, PV) = Search<RootNode>(ref Game.Board, defaultSS, false);
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
        CounterMoveHistory.Clear();
        
        PawnCorrections.Clear();
        KingCorrections.Clear();
        
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
    private static (short Score, Move[] PV) ProbeTT<NodeType>(ref Board board, SearchState ss, bool isNMP) 
        where NodeType : ISearchNodeType {

        // did we find the position and score?
        // we also need to check the ply, since too early tt lookups cause some serious blunders
        if (ss.Ply >= TT.MinProbingPly && TT.TryGetScore(board, ss.Depth, ss.Ply, ss.Window, out short ttScore)) {
            CurNodes++;
            PVSControl.TotalNodes++;

            // only return the score, no pv
            return (ttScore, []);
        }

        // in case the position is not yet stored, we fully search it and then store it
        var result = Search<NodeType>(ref board, ss, isNMP);

        // no heuristics should ever be updated when in NMP null-move search,
        // as the position is likely illegal and would pollute the ecosystem
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
        Corrections.Update(board, result.Score, ss.Depth);

        return result;
    }
    
    // finally the actual PVS recursive function:
    // ply starts at zero and increases each ply (no shit sherlock). depth,
    // on the other hand, starts at the highest value and decreases over
    // time. once we get to depth = 0, we drop into the qsearch.
    private static (short Score, Move[] PV) Search<NodeType>(ref Board board, SearchState ss, bool isNMP)
        where NodeType : ISearchNodeType {
        
        bool isRoot = typeof(NodeType) == typeof(RootNode);
        bool isPV   = typeof(NodeType) == typeof(PVNode) || isRoot;

        // either crossed the time budget or maximum nodes.
        // we also cannot abort the first iteration - no bestmove
        if (Abort && CurIterDepth > 1)
            return (0, []);

        // just to simplify who's turn it is
        Color col = board.Color;

        // 1. MATE DISTANCE PRUNING (0 Elo)
        // this is a weird variant of MDP, not sure whether it actually helps, but
        // if a mate score has been found in the previous iteration, and isn't yet
        // projected into the current one, we still don't search past the said ply
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
            
        else if (col == Color.BLACK && Score.IsMateScore(ss.Window.Beta) && ss.Window.Beta < 0) {
            int matePly = -Score.GetMateInX(ss.Window.Beta);
            if (ss.Ply >= matePly)
                return (Score.CreateMateScore(col, ss.Ply + 1), []);
        }

        // we reached depth zero or lower => evaluate the leaf node though qsearch
        if (ss.Depth <= 0)
            return (QSearch.Search(ref board, ss.Ply, ss.Window, CurIterDepth + 12, isNMP, false), []);
        
        // increase the nodes searched counter
        CurNodes++;
        PVSControl.TotalNodes++;

        // is the color to play currently in check?
        bool inCheck     = Check.IsKingChecked(board, col);
        short staticEval = board.StaticEval;

        //short pawnCorr = PawnCorrectionHistory.GetCorrection(in board);

        // if we got here from a PV node, and the move that was played to get
        // here was the move from the previous PV, we are in a PV node as well
        ss.IsPV = ss.IsPV && (ss.Ply == 0 
                                      || ss.Ply - 1 < PV.Length && PV[ss.Ply - 1] == ss.Previous);
        
        // 2. NULL MOVE PRUNING (~107 Elo)
        // we assume that in every position there is at least one move that improves it. first,
        // we play a null move (only switching sides), and then perform a reduced search with
        // a null window around beta. if the returned score fails high, we expect that not
        // skipping our move would "fail even higher", and thus can prune this node
        if (ss.Ply >= MinNMPPly      // minimum ply for nmp
            && !inCheck              // don't prune when in check
            && board.GamePhase() > 0 // don't prune in absolute endgames

            // in the early stages of the search, alpha and beta are set to
            // their limit values, so doing the reduced search would only
            // waste time, since we are unable to fail high
            && (col == Color.WHITE
                ? ss.Window.Beta  < short.MaxValue
                : ss.Window.Alpha > short.MinValue)
            
            && (col == Color.WHITE
                ? staticEval >= ss.Window.Beta  - 3 * ss.Depth
                : staticEval <= ss.Window.Alpha + 3 * ss.Depth)) {
            
            // null window around beta
            Window nullWindowBeta = col == Color.WHITE 
                ? new Window((short)(ss.Window.Beta - 1), ss.Window.Beta) 
                : new Window(ss.Window.Alpha, (short)(ss.Window.Alpha + 1));

            // child with a move skipped
            var nullChild = board.Clone() with {
                EnPassantSq = 64,
                Color = (Color)((int)col ^ 1)
            };
            
            // the depth reduction
            int R = 7 + ss.Depth / 3;

            // perform the reduced search
            short nmpScore = ProbeTT<NonPVNode>(
                ref nullChild,
                new SearchState(
                    ply:        (sbyte)(ss.Ply + 1),
                    depth:      (sbyte)(ss.Depth - R),
                    extensions: ss.Extensions,
                    window:     nullWindowBeta,
                    previous:   default,
                    isPv:       false
                ),
                isNMP: true
            ).Score;

            // if we failed high, prune this node
            if (col == Color.WHITE
                    ? nmpScore >= ss.Window.Beta
                    : nmpScore <= ss.Window.Alpha) {
                
                return (nmpScore, []);
            }
        }
        
        // update the static eval search stack
        improvStack.UpdateStaticEval(staticEval, ss.Ply);
        bool rootImproving = improvStack.IsImproving(ss.Ply, col);

        // 3. RAZORING (~18 Elo)
        // (kind of inspired by Stockfish) if a position is very, very bad, we skip the
        // move expansion and return qsearch score instead. this cannot be done when in check
        if (!ss.IsPV && !inCheck && !rootImproving) {
            // this margin is really just magic, but it feels right
            int margin = 574 + 367 * ss.Depth * ss.Depth;

            if (col == Color.WHITE
                    ? staticEval + margin < ss.Window.Alpha
                    : staticEval - margin > ss.Window.Beta) {

                // perform the quiescence search and ensure it actually fails low
                short qEval = QSearch.Search(ref board, ss.Ply, ss.Window, CurIterDepth + 12, isNMP, false);
                if (col == Color.WHITE ? qEval <= ss.Window.Alpha : qEval >= ss.Window.Beta)
                    return (qEval, []);
            }
        }
        
        // 4. STATIC NULL MOVE PRUNING (~4 Elo)
        // also called reverse futility pruning; if the static eval at close-to-leaf
        // nodes fails high despite subtracting a margin, prune this branch
        if (!ss.IsPV && !inCheck && rootImproving) {
            int margin = 204 + 278 * ss.Depth * ss.Depth;

            if (col == Color.WHITE && staticEval - margin > ss.Window.Beta)
                return (ss.Window.Beta, []);

            if (col == Color.BLACK && staticEval + margin < ss.Window.Alpha)
                return (ss.Window.Alpha, []);
        }
        
        // all legal moves sorted from best to worst (only a guess).
        // take a look at MoveOrder to understand better
        var moves = MoveOrder.GetOrderedMoves(board, ss.Depth, ss.Previous, out bool isTTMove);

        // 4. INTERNAL ITERATIVE REDUCTIONS (~54 Elo)
        // if the node we are in doesn't have a stored best move in TT, we reduce the depth
        // in hopes of finishing the search faster and populating the TT for next iterations
        // or occurences. the depth and ply conditions are important, as reducing too much in
        // the early iterations produces very wrong outputs
        if (!ss.IsPV && !isTTMove && !inCheck
            && ss.Depth >= 5 && ss.Ply >= 3
            && ss.Window.Alpha + 1 < ss.Window.Beta) {

            ss.Depth--;
        }

        // counter for expanded nodes
        int expandedNodes = 0;

        // pv continuation to be appended?
        Move[] pv = [];

        // loop through possible moves
        for (int i = 0; i < moves.Length; i++) {
            expandedNodes++;
            
            Move  curMove = moves[i];
            Board child   = board.Clone();
            
            child.PlayMove(curMove, true);
            
            ulong hash            = ZobristHash.Hash(in child);
            ulong pieceCount      = ulong.PopCount(child.Occupied);
            short childStaticEval = child.StaticEval;
            bool  isCapture       = curMove.Capture != PType.NONE;
            
            // since draw positions skip PVS, the full search
            // result must be initialized in advance (as draw)
            (short Score, Move[] PV) fullSearch = (0, []);
            int curDepth = ss.Depth;
            
            // check the position for a 3-fold repetition draw. it is very
            // important that we also remove this move from the stack, which
            // must be done anywhere where this loop is exited
            bool isThreeFold = !isNMP && ThreeFold.AddAndCheck(hash);
            
            // if there is a known draw according to chess rules
            // (either 50 move rule or insufficient mating material),
            // all pruning and reductions are skipped
            if (isThreeFold
                || child.HalfMoveClock >= 100
                || pieceCount <= 4 && isCapture && Eval.IsInsufficientMaterialDraw(child.Pieces, pieceCount))
                goto skipPVS;
            
            int  see        = isCapture ? SEE.GetCaptureScore(in board, col, curMove) : 0;
            bool givesCheck = Check.IsKingChecked(child, col == Color.WHITE ? Color.BLACK : Color.WHITE);
            
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
            bool interesting = expandedNodes == 1 
                               || ss.IsPV
                               || ss.Ply <= 4 && isCapture
                               || inCheck     || givesCheck;

            // 5. REDUCTIONS (~50 Elo)
            int reduction = 1442;
            
            // extend the search of the first few root moves
            // (this is done by reducing all other moves)
            if (ss.Ply == 0 && expandedNodes >= 5)
                reduction += 996;
            
            // if a capture seems to be really bad, reduce the depth. oddly enough,
            // restricting these reductions with various conditions doesn't work
            if (isCapture && see < -100)
                reduction += 1071;

            // further extension/reduction based on SEE
            reduction -= see * 63 / 100;
            
            // if improving, reduce less
            reduction += improving     ? -31 : 48;
            reduction += rootImproving ? -40 : 30;
            
            // check and capture extensions
            if (inCheck)    reduction -= 221;
            if (givesCheck) reduction -= 217;
            if (isCapture)  reduction -= 106;

            // queen promotion idea
            if (curMove.Promotion == PType.QUEEN)
                reduction -= 181;

            // now make sure that a true extension isn't applied
            if (ss.Ply != 0)
                reduction = Math.Max(1024, reduction);
            
            curDepth -= reduction / 1024;

            // 7. FUTILITY PRUNING (~56 Elo)
            // we try to discard moves near the leaves, which have no potential of raising alpha.
            // futility margin represents the largest possible score gain through a single move.
            // if we add this margin to the static eval of the position and still don't raise
            // alpha, we can prune this branch
            if (!interesting && ss.Ply >= 4 && ss.Depth <= 5) {
                int windowSize = Math.Min(Math.Abs(ss.Window.Alpha - ss.Window.Beta) / 128, 12);
                int childCorr  = Corrections.Get(in child);

                // as taken from CPW:
                // "If at depth 1 the margin does not exceed the value of a minor piece, at
                // depth 2 it should be more like the value of a rook."
                // we don't really follow this exactly, but our approach is kind of similar
                int margin = 95 + 97 * ss.Depth
                                + 2 * childCorr                  // this acts like a measure of uncertainty
                                + (improving ? 0 : -23)          // not improving nodes prune more
                                + Math.Clamp(see / 122, -39, 17) // tweak the margin based on SEE
                                + windowSize;                    // another measure of uncertainty
                
                // if we didn't manage to raise alpha, prune this branch
                if (col == Color.WHITE
                        ? childStaticEval + margin <= ss.Window.Alpha
                        : childStaticEval - margin >= ss.Window.Beta) {
                    
                    CurNodes++; PVSControl.TotalNodes++;
                    if (!isNMP) ThreeFold.Remove(hash);
                    
                    continue;
                }

                // FUTILITY CUTOFFS (~25 Elo)
                // a small idea i had - if the leaf or close to leaf nodes seem
                // to be really bad, we try to fail low by adding the futility
                // margin to the static eval of the current position, not the child
                if (ss.Depth <= 3 && !rootImproving && !improving
                    && (col == Color.WHITE
                        ? board.StaticEval + margin <= ss.Window.Alpha
                        : board.StaticEval - margin >= ss.Window.Beta)) {
                    
                    int rootCorr = Corrections.Get(in board);
                    
                    // this turns out to work quite well - only reduce when the root
                    // pawn correction is bad, and the child correction is even worse
                    if (col == Color.WHITE 
                            ? childCorr < rootCorr && rootCorr < 0
                            : childCorr > rootCorr && rootCorr > 0) {
                        
                        CurNodes++; PVSControl.TotalNodes++;
                        if (!isNMP) ThreeFold.Remove(hash);

                        // instead of just pruning this branch, we assume
                        // all following moves are even worse, so we cut
                        // off completely and return the lower bound
                        return (col == Color.WHITE ? ss.Window.Alpha : ss.Window.Beta, []);
                    }
                }
            }

            int  maxExpNodes = (isTTMove ? 1 : 4) + (isPV ? 3 : 0);
            bool skipLMP     = expandedNodes <= maxExpNodes || inCheck || isRoot || see >= 300;

            if (!skipLMP) {
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
                int score = ProbeTT<NonPVNode>(ref child, 
                    new SearchState((sbyte)(ss.Ply + 1), (sbyte)(ss.Depth - R), ss.Extensions, nullWindowAlpha, default, false),
                    isNMP
                ).Score;
                
                if (col == Color.WHITE
                        ? score <= ss.Window.Alpha
                        : score >= ss.Window.Beta) {
                        
                    if (!isCapture)
                        QuietHistory.ChangeRep(board, curMove, ss.Depth - R, isMoveGood: false);
                        
                    if (!isNMP) ThreeFold.Remove(hash);
                    continue;
                }
            }

            if (!skipLMP && curDepth < ss.Depth - 1)
                curDepth++;
            
            fullSearch = ProbeTT<PVNode>(
                ref child, 
                ss with { 
                    Ply      = (sbyte)(ss.Ply + 1),
                    Depth    = (sbyte)curDepth, 
                    Previous = curMove
                },
                isNMP
            );
            
            // 9. LATE MOVE PRUNING
            // after we have searched a couple of moves, we expect the rest to be worse
            // and not raise alpha. to verify this, we perform a greatly reduced search
            // with a null window around alpha. if it fails low, we prune the branch
            /*if (!interesting && see < 300 
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
                    new SearchState((sbyte)(ss.Ply + 1), (sbyte)(ss.Depth - R), ss.Extensions, nullWindowAlpha, default, false),
                    isNMP
                ).Score;

                // continuing without this causes weird behaviour. the engine somehow
                // evaluates regular positions as mate in X. keep this. it's important.
                // TODO - CHANGE #5 - REMOVED MATE CHECK
                if (true) {
                    if (col == Color.WHITE
                            ? score <= ss.Window.Alpha
                            : score >= ss.Window.Beta) {
                        
                        // TODO - CHANGE #3 - ADDED THIS
                        if (!isCapture)
                            QuietHistory.ChangeRep(board, curMove, ss.Depth - R, isMoveGood: false);
                        
                        if (!isNMP) ThreeFold.Remove(hash);
                        continue;
                    }

                    // 10. LATE MOVE REDUCTIONS
                    // if a late move (from the prior condition) doesn't fail
                    // low with the reduced search, but is still bad (secured
                    // by the R condition), it's at least reduced
                    // TODO - CHANGE # 4 - COMMENTED
                    //if (ss.Depth == 4 && R >= 5 && !improving)
                    //    curDepth -= 2 - Math.Sign(see);
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
            }*/
            
            skipPVS:
            if (!isNMP) ThreeFold.Remove(hash);

            // we somehow still failed low
            if (col == Color.WHITE
                    ? fullSearch.Score <= ss.Window.Alpha
                    : fullSearch.Score >= ss.Window.Beta) {

                // decrease the move's reputation
                // (although we are modifying quiet history, not checking
                // whether this move is a capture yields better results)
                // TODO - CHANGE #2 - CAPTURE, CURDEPTH
                if (!isCapture)
                    QuietHistory.ChangeRep(board, curMove, curDepth, isMoveGood: false);
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
                // TODO - CHANGE #1 - CURDEPTH
                TT.Store(board, (sbyte)curDepth, ss.Ply, ss.Window, fullSearch.Score, moves[i]);

                // place the current move in front of the received pv to build a new pv
                pv = new Move[fullSearch.PV.Length + 1];
                Array.Copy(fullSearch.PV, 0, pv, 1, fullSearch.PV.Length);
                pv[0] = curMove;

                // beta cutoff (see alpha-beta pruning); alpha is larger
                // than beta, so we can stop searching this branch, because
                // the other side wouldn't allow us to get here at all
                if (ss.Window.TryCutoff(fullSearch.Score, col)) {

                    // is it quiet?
                    if (!isCapture) {

                        // if a quiet move caused a beta cutoff, we increase its history
                        // score and store it as a killer move on the current depth
                        QuietHistory.ChangeRep(board, curMove, ss.Depth, isMoveGood: true);
                    }
                    
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
}