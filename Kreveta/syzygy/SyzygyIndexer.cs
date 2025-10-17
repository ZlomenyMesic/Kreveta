using System;
using System.Collections.Generic;
using Kreveta.consts;

namespace Kreveta.syzygy;

internal static class SyzygyIndexer {
    private readonly static ulong[][] Binomial = PrecomputeBinomial();

    // Precompute binomial coefficients C(n, k)
    private static ulong[][] PrecomputeBinomial() {
        const int N = 65;
        ulong[][] C = new ulong[N][];
        for (int n = 0; n < N; n++) {
            C[n] = new ulong[N];
            
            C[n][0] = 1;
            for (int k = 1; k <= n; k++)
                C[n][k] = C[n - 1][k - 1] + C[n - 1][k];
        }
        return C;
    }

    // compute combinatorial index for given occupied squares
    private static ulong CombinadicIndex(int[] squares) {
        ulong index = 0;
        for (int i = 0; i < squares.Length; i++)
            index += Binomial[squares[i]][i + 1];
        return index;
    }
    
    // returns an ulong index of the position inside the tb files
    internal static ulong ComputeIndex(Board board) {
        List<int> whiteSquares = [];
        List<int> blackSquares = [];
        
        // loop over all pieces
        ReadOnlySpan<ulong> pieces = board.Pieces;
        for (byte i = 0; i < 6; i++) {

            // copy the respective piece bitboards for both colors
            ulong wCopy = pieces[i];
            ulong bCopy = pieces[6 + i];

            // same loops as in Eval
            while (wCopy != 0UL) {
                whiteSquares.Add(BB.LS1BReset(ref wCopy));
            }

            while (bCopy != 0UL) {
                blackSquares.Add(BB.LS1BReset(ref bCopy));
            }
        }

        whiteSquares.Sort();
        blackSquares.Sort();

        // combine for final encoding order, white MUST go first
        var all = new List<int>();
        all.AddRange(whiteSquares);
        all.AddRange(blackSquares);

        // compute combinatorial index
        ulong index = CombinadicIndex([.. all]);

        // add side to move
        index = index << 1 
              | (board.Color == Color.WHITE ? 0UL : 1UL);

        return index;
    }
}