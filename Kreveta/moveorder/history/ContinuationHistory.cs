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

internal static unsafe class ContinuationHistory {
    private const int TableSize = 6 * 64 * 6 * 64;
    private static readonly short[] Table = new short[TableSize];
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Clear()
        => Array.Clear(Table, 0, TableSize);

    internal static void Age() {
        fixed (short* ptr = Table) {
            int i = 0;

            if (Consts.UseAVX2) {
                int width = Vector256<short>.Count;

                for (; i <= TableSize - width; i += width) {
                    var v = Avx.LoadVector256(ptr + i); 
                    v     = Avx2.ShiftRightArithmetic(v, 1);
                    
                    Avx.Store(ptr + i, v);
                }
            }
            else if (Consts.UseSSE2) {
                int width = Vector128<short>.Count;

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
        Table[i] += (short)(weight * Math.Abs(weight) / 8);
    }

    // try to retrieve the continuation
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetScore(Move previous, Move current)
        => Table[Index((int)previous.Piece, previous.End, (int)current.Piece, current.End)];
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Index(int p1, int to1, int p2, int to2)
        => ((p1 * 64 + to1) * 6 + p2) * 64 + to2;
}