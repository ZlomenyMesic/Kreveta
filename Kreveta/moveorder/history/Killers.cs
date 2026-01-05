//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.movegen;

using System;

// ReSharper disable InvokeAsExtensionMethod

namespace Kreveta.moveorder.history;

internal static unsafe class Killers {

    private static Move[] _killers     = null!;
    private static Move[] _captKillers = null!;

    private static int _depth;
    private static int _size;

    // number of saved killers per ply/depth
    private const int CapacityPerCluster = 7;

    // increase the array size by one ply for the next iteration
    internal static void Expand(int depth) {
        // new parameters for the table
        _depth = depth;
        _size  = _depth * CapacityPerCluster;
        
        Array.Resize(ref _killers,     _size);
        Array.Resize(ref _captKillers, _size);
    }

    // clear the table and free the memory
    internal static void Clear() {
        _depth = 0;
        _size  = 0;

        _killers     = new Move[CapacityPerCluster];
        _captKillers = new Move[CapacityPerCluster];
    }

    // save a new killer move at the specified depth
    internal static void Add(Move move, int depth) {
        fixed (Move* q = &_killers[depth])
        fixed (Move* c = &_captKillers[depth]) {
            var table = move.Capture == PType.NONE ? q : c;

            // there is an assumption that the latest killers should also be
            // the most relevant ones. for this reason we constantly shift
            // and remove old killers, and put the new ones to the front

            int offset = CapacityPerCluster * (_depth - depth);
            int last   = offset + CapacityPerCluster - 1;

            // try to get the index of the move in case it's already stored.
            // the move may be repeated multiple times, but we only want the
            // relative index to our current depth, so we take a slice
            int index = MemoryExtensions.IndexOf(
                new Span<Move>(table + offset, CapacityPerCluster), move);

            // we don't want duplicates, so if we find the move, we set the
            // last index to it, and put it to the front. this keeps the new
            // one and overwrites the old one
            if (index != -1)
                last = offset + index;

            // shift all moves by one slot
            for (int i = last; i > offset; i--)
                table[i] = table[i - 1];

            // store the new move at the front
            table[offset] = move;
        }
    }

    // returns the cluster of killers at a certain depth
    internal static Span<Move> GetCluster(int depth, bool captures) {
        fixed (Move* q = &_killers[depth])
        fixed (Move* c = &_captKillers[depth]) {
            
            int offset = CapacityPerCluster * (_depth - depth);
            return new Span<Move>((captures ? c : q) + offset, CapacityPerCluster);
        }
    }
}
