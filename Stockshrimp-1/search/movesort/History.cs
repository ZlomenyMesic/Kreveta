/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

using Stockshrimp_1.movegen;
using System.Runtime.CompilerServices;

namespace Stockshrimp_1.search.movesort;

internal static class History {

    // these boards are usually indexed [from, to] but after some testing,
    // indexing with a piece type seems to yield better results
    //
    // hh stands for history heuristics, not sure what else it could mean
    private static readonly int[,] hh_scores = new int[64, 12];

    // butterfly boards for relative history move ordering
    // bfbs save the number of times a move has been visited
    private static readonly int[,] bf_scores = new int[64, 12];
    private const int BF_INC = 1;
    private const int BF_DIVIDE = 9;

    // before each new iterated depth, we "shrink" the saved values
    // (we don't wanna get rid of them, but we also have more important ones coming)
    internal static void Shrink() {
        for (int i = 0; i < 64; i++) {
            for (int j = 0; j < 12; j++) {

                // history reputation is straightforward
                lock (hh_scores) {
                    hh_scores[i, j] /= 2;
                }

                // this just turns out to be kinda right
                lock (bf_scores) {
                    bf_scores[i, j] = Math.Min(1, bf_scores[i, j]);
                }
            }
        }
    }

    // erases all history data
    internal static void Clear() {
        for (int i = 0; i < 64; i++) {
            for (int j = 0; j < 12; j++) {
                hh_scores[i, j] = 0;
                bf_scores[i, j] = 0;
            }
        }
    }

    internal static void IncreaseRep(Board b, Move m, int depth) {
        int i = PieceIndex(b, m);
        int end = m.End();

        lock (hh_scores) {
            hh_scores[end, i] += HHShift(depth);
        }

        lock (bf_scores) {
            bf_scores[end, i] -= BF_INC;
        }
    }

    internal static void DecreaseRep(Board b, Move m, int depth) {
        int i = PieceIndex(b, m);
        int end = m.End();

        lock (hh_scores) {
            hh_scores[end, i] -= HHShift(depth);
        }
        lock (bf_scores) {
            bf_scores[end, i] += BF_INC;
        }
    }

    internal static void AddVisited(Board b, Move m) {
        int i = PieceIndex(b, m);
        int end = m.End();

        lock (bf_scores) {
            bf_scores[end, i] += BF_INC;
        }
    }

    internal static int GetRep(Board b, Move m) {
        int i = PieceIndex(b, m);
        int end = m.End();

        return hh_scores[end, i]
            - bf_scores[end, i] / BF_DIVIDE;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int PieceIndex(Board b, Move m) {
        (int col, int piece) = b.PieceAt(m.Start());
        return piece + col == 0 ? 6 : 0;
    }

    private const int HHS_SUBTRACT = 5;
    private const int HHS_MAX = 84;

    // how much do we affect the history reputation
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HHShift(int depth)
        => Math.Min(depth * depth - HHS_SUBTRACT, HHS_MAX);
}
