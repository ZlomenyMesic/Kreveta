//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable InconsistentNaming

namespace Kreveta.search.pruning;

// NULL MOVE PRUNING:
// we assume that in every position there is at least one move that can
// improve it, so we try playing a "null-move" (essentially no move at
// all) and we then search this null child at a reduced depth (reduction R).
// if we still fail high despite skipping a move, we can expect that playing
// a move would also lead to a fail high, so we can prune this branch.
//
// NMP for this reason failes in zugzwangs or sometimes endgames
internal static class NullMovePruning {

    // the absolute minimum ply required for nmp
    private const int AbsMinPly = 3;
    
    // current minimum ply needed for nmp
    internal static int CurMinPly = 3;

    private const int PlySubtract     = 2;
    private const int ReductionBase   = 2;
    private const int CurDepthDivisor = 4;
    private const int MinAddRedDepth  = 8;
    private const int AddDepthDivisor = 5;

    // as mentioned above, nmp sometimes fails in endgames, so we want to prune
    // less with fewer pieces on the board - we achieve this by progressively
    // raising the minimum ply required for nmp
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void UpdateMinPly(int pieceCount)
        => CurMinPly = Math.Max(AbsMinPly, (32 - pieceCount) / 7);

    // try null move pruning
    internal static bool TryPrune([In, ReadOnly(true)] in Board board, int depth, int ply, Window window, Color col, out short score) {

        // null window around beta
        Window nullWindowBeta = col == Color.WHITE 
            ? new((short)(window.Beta - 1), window.Beta) 
            : new(window.Alpha, (short)(window.Alpha + 1));

        // child with no move played
        Board nullChild = board.GetNullChild();

        // the reduction is based on ply, depth, etc.
        int R = Math.Min(ply - PlySubtract,
                         ReductionBase + PVSearch.CurDepth / CurDepthDivisor);

        // once we reach a certain depth iteration, we start pruning
        // a bit more aggressively - it isn't as important to be careful
        // later than it is at the beginning. not adding this threshold
        // causes some troubles in evaluation, though.
        if (PVSearch.CurDepth > MinAddRedDepth)
            R += depth / AddDepthDivisor;

        // do the reduced search
        score = PVSearch.ProbeTT(nullChild, ply + 1, depth - R - 1, nullWindowBeta, default).Score;

        // if we failed high, that means the score is above beta and is "too good" to be
        // allowed by the opponent. if we don't fail high, we just continue the expansion
        //
        // currently we are returning the null search score, but returning beta
        // may also work. this needs some testing
        return col == Color.WHITE
            ? score >= window.Beta
            : score <= window.Alpha;
    }
}
