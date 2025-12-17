//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.movegen;
using Kreveta.moveorder.historyheuristics;
using Kreveta.search.transpositions;

using System;
using System.Runtime.InteropServices;
// ReSharper disable InconsistentNaming

namespace Kreveta.moveorder;

// to achieve the best results from PVS, move ordering is
// essential. searching better moves earlier allows much
// more space for pruning. although we cannot really order
// the moves reliably unless we perform the actual search,
// we can at least make a rough guess - captures and moves
// that proved to be helpful in similar positions go first
internal static unsafe class MoveOrder {

    // we hopefully shouldn't find more captures in a position
    private static readonly Move* CaptureBuffer = (Move*)NativeMemory.AlignedAlloc(
        byteCount: (nuint)(60 * sizeof(Move)),
        alignment: (nuint)sizeof(Move));

    private static bool _memoryFreed;

    internal static void Clear() {
        if (!_memoryFreed) {
            NativeMemory.AlignedFree(CaptureBuffer);
            _memoryFreed = true;
        }
    }

    // don't use "in" keyword!!! it gets much slower
    internal static Span<Move> GetOrderedMoves(Board board, int depth, /*Move penultimate,*/ Move previous, out bool isTTMove) {
        isTTMove = false;
        
        // all legal moves
        Span<Move> legal = stackalloc Move[Consts.MoveBufferSize];
        int legalCount   = Movegen.GetLegalMoves(ref board, legal);
        
        // already sorted legal moves
        Span<Move> sorted = stackalloc Move[legalCount];
        Span<bool> used   = stackalloc bool[legalCount];
        int cur = 0, curCapt = 0;
        
        // 1. TT STORED BEST MOVE
        if (TT.TryGetBestMove(board, out Move ttMove) && ttMove != default) {
            for (int i = 0; i < legalCount; i++) {
                if (legal[i] == ttMove) {
                    sorted[cur++] = ttMove;
                    used[i]       = true;
                    isTTMove      = true;
                    break;
                }
            }
        }
        
        // 2. TWO-PLY CONTINUATION
        /*if (depth < ContinuationHistory.MaxRetrieveDepth) {
            Move continuation = ContinuationHistory.Get(penultimate, previous);
            if (continuation != default) {
                for (int i = 0; i < legalCount; i++) {
                    if (!used[i] && legal[i] == continuation) {
                        sorted[cur++] = continuation;
                        used[i] = true;
                        break;
                    }
                }
            }
        }*/
        
        /*for (int i = 0; i < legalCount; i++) {
            if (!used[i] && legal[i].Promotion is PType.QUEEN) {
                sorted[cur++] = legal[i];
                used[i]       = true;
            }
        }*/
        
        // 2. KILLER CAPTURES
        if (depth < 5) {
            var captKillers = Killers.GetCluster(depth, captures: true);
            for (int k = 0; k < captKillers.Length; k++) {
                if (captKillers[k] == default) continue;

                for (int i = 0; i < legalCount; i++) {
                    if (!used[i] && legal[i] == captKillers[k]) {
                    
                        sorted[cur++] = captKillers[k];
                        used[i]       = true; break;
                    }
                }
            }
        }
        
        // 3. SEE ORDERED CAPTURES
        for (int i = 0; i < legalCount; i++) {
            if (!used[i] && legal[i].Capture != PType.NONE) {
                CaptureBuffer[curCapt++] = legal[i];
                used[i]                  = true;
            }
        }
        
        var seeMoves = SEE.OrderCaptures(in board, new ReadOnlySpan<Move>(CaptureBuffer, curCapt), out int captCount, out int[] seeScores);
        Move worstCapture = default;
        
        for (int i = 0; i < captCount - 1; i++) {
            sorted[cur++] = seeMoves[i];
        }

        if (captCount != 0) {
            if (seeScores[^1] <= -100) worstCapture  = seeMoves[^1];
            else                       sorted[cur++] = seeMoves[^1];
        }
        
        // 4. REGULAR KILLERS (QUIET)
        var killers = Killers.GetCluster(depth, captures: false);
        for (int k = 0; k < killers.Length; k++) {
            if (killers[k] == default) continue;

            for (int i = 0; i < legalCount; i++) {
                if (!used[i] && legal[i] == killers[k]) {
                    
                    sorted[cur++] = killers[k];
                    used[i]       = true; break;
                }
            }
        }
        
        // 5. COUNTER MOVE
        if (depth < CounterMoveHistory.MaxRetrieveDepth) {
            Move counter = CounterMoveHistory.Get(board.Color, previous);
            if (counter != default) {
                for (int i = 0; i < legalCount; i++) {
                    if (!used[i] && legal[i] == counter) {
                        
                        sorted[cur++] = counter;
                        used[i]       = true; break;
                    }
                }
            }
        }
        
        // 6. WORST CAPTURE (PREVIOUSLY EXCLUDED)
        if (worstCapture != default)
            sorted[cur++] = worstCapture;
        
        // 7. QUIETS ORDERED BY HISTORY
        Span<(Move move, int score)> quiets = stackalloc (Move, int)[legalCount];
        int quietCount = 0;

        for (int i = 0; i < legalCount; i++) {
            if (!used[i]) {
                int qh  = QuietHistory.GetRep(board, legal[i]);
                //int see = SEE.GetCaptureScore(in board, board.Color, legal[i]);
                
                quiets[quietCount++] = (legal[i], qh);
                used[i] = true;
            }
        }

        InsertionSort(quiets, quietCount);

        for (int i = 0; i < quietCount; i++) {
            sorted[cur++] = quiets[i].move;
        }
        
        return new Span<Move>(sorted.ToArray());
    }

    // insertion sort of remaining quiets (descending by score)
    private static void InsertionSort(Span<(Move move, int score)> quiets, int count) {
        for (int i = 1; i < count; i++) {
            (Move move, int score) key = quiets[i];
            int j = i - 1;
            while (j >= 0 && quiets[j].score < key.score) {
                quiets[j + 1] = quiets[j];
                j--;
            }
            quiets[j + 1] = key;
        }
    }
}