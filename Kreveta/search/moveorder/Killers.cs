//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.movegen;

using System;
using System.Linq;

// ReSharper disable InvokeAsExtensionMethod

namespace Kreveta.search.moveorder;

internal static class Killers {
    
    private static Move[] _killers = [];
    private static int    _depth;

    // number of saved killers per ply/depth
    private const int CapacityPerCluster = 7;

    // increase the array size by one ply for the next iteration
    internal static void Expand(int depth) {
        _depth = depth + 1;
        Array.Resize(ref _killers, _depth * CapacityPerCluster);
    }

    internal static void Clear() {
        _killers = [];
        _depth   = 0;
    }

    internal static void Add(Move move, int depth) {
        
        // there is an assumption that the latest killers should also be
        // the most relevant ones. for this reason we constantly shift
        // and remove old killers, and put the new ones to the front
        
        int offset = CapacityPerCluster * (_depth - depth);
        int last   = offset + CapacityPerCluster - 1;
        
        // try to get the index of the move in case it's already stored
        int index = MemoryExtensions.IndexOf(
            (Span<Move>)[.._killers.Skip(offset).Take(CapacityPerCluster)], move);
        
        // we don't want duplicates, so if we find the move, we set the
        // last index to it, and put it at the front. this keeps the new
        // one and overwrites the old one
        if (index != -1)
            last = offset + index;
        
        // shift all moves by one slot
        for (int i = last; i > offset; i--)
            _killers[i] = _killers[i - 1];

        // store the new move in front
        _killers[offset] = move;
    }

    // returns the cluster of killers at a certain depth
    internal static Span<Move> GetCluster(int depth) {
        int offset = CapacityPerCluster * (_depth - depth);
        
        // copy the moves into a separate array
        Move[] cluster = new Move[CapacityPerCluster];
        Array.Copy(_killers, offset, cluster, 0, CapacityPerCluster);

        // return as a span, hopefully for better performance
        return (Span<Move>)cluster;
    }
}
