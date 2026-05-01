//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.movegen;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Kreveta.moveorder.history;

// continuation history is the largest history table, and it holds information
// about a move with respect to the previous move. we assume many moves allow
// a natural response, which this table tries to capture (just as countermoves).
// both captures and quiets may be stored here, but we try to avoid defaults.
internal static unsafe class ContinuationHistory {
    private const int TableSize = 6 * 64 * 6 * 64;
    private static readonly int[] Table = new int[TableSize];
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Clear()
        => Array.Clear(Table, 0, TableSize);

    internal static void Age() {
        fixed (int* ptr = Table) {
            int i = 0;

            // the table consists of roughly 150,000 items, and each one of these items has
            // to be divided by two. in such a large-scale scenario, using SIMD is helpful
            if (Consts.UseAVX2) {
                int width = Vector256<int>.Count;

                // we simulate the division by performing a right bit-shift
                for (; i <= TableSize - width; i += width) {
                    var v = Avx.LoadVector256(ptr + i); 
                    v     = Avx2.ShiftRightArithmetic(v, 1);
                    
                    Avx.Store(ptr + i, v);
                }
            }
            // we can still speed it up with SSE2, although twice as slow as AVX2
            else if (Consts.UseSSE2) {
                int width = Vector128<int>.Count;

                for (; i <= TableSize - width; i += width) {
                    var v = Sse2.LoadVector128(ptr + i);
                    v     = Sse2.ShiftRightArithmetic(v, 1);
                    
                    Sse2.Store(ptr + i, v);
                }
            }
            
            for (; i < TableSize; i++)
                ptr[i] >>= 1;
        }
    }

    // store a new continuation - same as with counters, there
    // is no priority measure, old continuations get overwritten
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Add(Move previous, Move current, int weight) {
        if (weight == 0) return;
        
        int i = Index((int)previous.Piece, previous.End, (int)current.Piece, current.End);
        Table[i] += weight * Math.Abs(weight) / 8;
    }

    // try to retrieve the continuation score
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetScore(Move previous, Move current)
        => Table[Index((int)previous.Piece, previous.End, (int)current.Piece, current.End)];
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Index(int p1, int to1, int p2, int to2)
        => ((p1 * 64 + to1) * 6 + p2) * 64 + to2;
}