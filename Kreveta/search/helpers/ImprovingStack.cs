//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Runtime.CompilerServices;
using Kreveta.consts;

// ReSharper disable InconsistentNaming

namespace Kreveta.search.helpers;

// improving stack holds static evaluations indexed by search ply. we can look up whether
// our evaluation has improved over the last two plies, or just the  last move. when the
// position is improving, we try to be a bit more careful with pruning
internal static class ImprovingStack {
    private static short[] _stack = null!;
    private static int     _len;
    
    // before each new search iteration, the improving stack is expanded
    // to fit all search tree plies including some potential extensions
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Expand(int depth) {
        // leave some space for potential extensions
        _len   = depth + 8;
        _stack = new short[_len];
    }

    // store a static evaluation at the specified ply
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void UpdateStaticEval(short staticEval, int ply, Color col) {
        if (ply >= _len)
            return;
        
        // always store the evaluation white-relative
        _stack[ply] = (short)(staticEval
                           * (col == Color.WHITE ? 1 : -1));
    }

    // check whether the static evaluation has improved over the last two plies
    internal static bool IsImproving2Ply(int ply, Color col) {
        if (ply < 2 || ply >= _len) 
            return false;
        
        short prev = _stack[ply - 2];
        short cur  = _stack[ply    ];

        // it is common practice that when the previous static eval comes from an in-check
        // position, we try to go back in past until a point where we reach a valid score.
        // however, since Board already keeps old evaluations even through checks, we know
        // the previous evaluation with always be correct.
        return col == Color.WHITE
            ? cur > prev : cur < prev;
    }
    
    // same as above, but measures improvements just over the last move
    internal static bool IsImproving1Ply(int ply) {
        if (ply == 0 || ply >= _len) 
            return false;
        
        short prev = _stack[ply - 1];
        short cur  = _stack[ply    ];
        
        // the negamax framework requires us to negate the previous score
        return cur > -prev;
    }
}
