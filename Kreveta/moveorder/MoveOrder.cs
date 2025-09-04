//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.movegen;
using Kreveta.search;

using System;
using System.Runtime.InteropServices;

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

    // don't use "in" keyword!!! it becomes much slower
    internal static Span<Move> GetOrderedMoves(Board board, int depth, Move previous) {

        Span<Move> legal  = stackalloc Move[128];
        int legalCount    = Movegen.GetLegalMoves(ref board, legal);
        
        Span<Move> sorted = stackalloc Move[legalCount];
        Span<bool> used   = stackalloc bool[legalCount]; // track which legal moves are already added
        int cur = 0, curCapt = 0;

        // 1. TT best move first
        if (TT.TryGetBestMove(board, out var ttMove) && ttMove != default) {
            for (int i = 0; i < legalCount; i++) {
                if (legal[i] == ttMove) {
                    sorted[cur++] = ttMove;
                    used[i] = true;
                    break;
                }
            }
        }

        // 2. Captures ordered by MVV-LVA
        for (int i = 0; i < legalCount; i++) {
            if (!used[i] && legal[i].Capture != PType.NONE) {
                CaptureBuffer[curCapt++] = legal[i];
                used[i] = true;
            }
        }

        Span<Move> mvvlva = MVV_LVA.OrderCaptures(new Span<Move>(CaptureBuffer, curCapt));
        for (int i = 0; i < mvvlva.Length; i++) {
            sorted[cur++] = mvvlva[i];
        }

        // 3. Killer moves
        Span<Move> killers = Killers.GetCluster(depth);
        for (int k = 0; k < killers.Length; k++) {
            var killer = killers[k];
            if (killer == default) continue;

            for (int i = 0; i < legalCount; i++) {
                if (!used[i] && legal[i] == killer) {
                    sorted[cur++] = killer;
                    used[i] = true;
                    break;
                }
            }
        }

        // 4. Counter move
        if (depth < CounterMoveHistory.MaxRetrieveDepth) {
            Move counter = CounterMoveHistory.Get(board.Color, previous);
            if (counter != default) {
                for (int i = 0; i < legalCount; i++) {
                    if (!used[i] && legal[i] == counter) {
                        sorted[cur++] = counter;
                        used[i] = true;
                        break;
                    }
                }
            }
        }

        // 5. Remaining quiets ordered by history
        Span<(Move move, int score)> quiets = stackalloc (Move, int)[legalCount];
        int quietCount = 0;

        for (int i = 0; i < legalCount; i++) {
            if (!used[i]) {
                int score = QuietHistory.GetRep(board, legal[i]);
                quiets[quietCount++] = (legal[i], score);
                used[i] = true;
            }
        }

        InsertionSort(quiets, quietCount);

        for (int i = 0; i < quietCount; i++) {
            sorted[cur++] = quiets[i].move;
        }
        
        return new Span<Move>(sorted.ToArray());
    }

    // insertion sort (descending by score)
    private static void InsertionSort(Span<(Move move, int score)> quiets, int count) {
        for (int i = 1; i < count; i++) {
            var key = quiets[i];
            int j = i - 1;
            while (j >= 0 && quiets[j].score < key.score) {
                quiets[j + 1] = quiets[j];
                j--;
            }
            quiets[j + 1] = key;
        }
    }
}