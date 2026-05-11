//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Diagnostics;
using Kreveta.consts;

// ReSharper disable InvocationIsSkipped

namespace Kreveta;

// assertions are only used in Debug mode, and should help catch potential bugs early
internal static class Assert {
    
    // assert that this condition is true
    [Conditional("DEBUG")]
    internal static void True(bool condition, string? message)
        => Debug.Assert(condition, message);

    // used at the very beginning of every searched node - the search window must make sense,
    // and it should comply with the node types. we also shouldn't have more than one node type
    [Conditional("DEBUG")]
    internal static void WindowCorrect(int alpha, int beta) {
        True(alpha >= Consts.MinValue && beta <= Consts.MaxValue, "bounds out of min/max value range");
        True(alpha < beta, "beta not larger than alpha");
    }

    [Conditional("DEBUG")]
    internal static void NodeTypeCorrect(int alpha, int beta, bool pvNode, bool cutNode, bool allNode) {
        // ensure there is at least one, and at most one node type
        True((pvNode ? 1 : 0) + (cutNode ? 1 : 0) + (allNode ? 1 : 0) == 1, "more than one node type");
        
        // a PV node mustn't have a null window, and non-PV nodes must have a null window
        True(pvNode ? alpha + 1 < beta : alpha + 1 == beta, "window size doesn't match node type");
    }
}