//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.movegen;

using System;
using System.Runtime.InteropServices;

// ReSharper disable InvokeAsExtensionMethod

namespace Kreveta.moveorder.historyheuristics;

internal static unsafe class Killers {

    private static Move* _killers;
    private static Move* _captKillers;

    private static int  _depth;
    private static nuint _size;

    // number of saved killers per ply/depth
    private const int CapacityPerCluster = 7;

    // increase the array size by one ply for the next iteration
    internal static void Expand(int depth) {

        // we previously used a simple array to store killers, and
        // when this method was called we resized it. resizing an
        // array, however, keeps the previous elements inside, so
        // we store a temporary array of the moves while the killer
        // table gets reallocated, and then we put the moves back
        var temp     = new Move[_size];
        var captTemp = new Move[_size];
        
        for (int i = 0; i < (int)_size; i++) {
            temp[i]     = _killers[i];
            captTemp[i] = _captKillers[i];
        }

        // new parameters for the table
        _depth = depth;
        _size  = (nuint)(_depth * CapacityPerCluster);

        // allocate the new table
        _killers = (Move*)NativeMemory.AlignedAlloc(
            byteCount: _size * (nuint)sizeof(Move),
            alignment: (nuint)sizeof(Move));
        
        _captKillers = (Move*)NativeMemory.AlignedAlloc(
            byteCount: _size * (nuint)sizeof(Move),
            alignment: (nuint)sizeof(Move));

        // we are likely in a whole new search, so the new
        // allocated table is smaller than the previous than
        // the last one. in this case we don't copy anything
        if ((int)_size < temp.Length)
            return;

        // otherwise simply copy the moves into the new table
        for (int i = 0; i < temp.Length; i++) {
            _killers[i]     = temp[i];
            _captKillers[i] = captTemp[i];
        }
    }

    // clear the table and free the memory
    internal static void Clear() {
        _depth = 0;
        _size  = 0;

        // if freed memory is freed again, a critical bug occurs,
        // and the program crashes. but there isn't a direct way
        // to check whether the memory has been freed yet, so we
        // must mark it as null once it's freed
        if (_killers is not null) {
            NativeMemory.AlignedFree(_killers);
            _killers = null;
        }
        if (_captKillers is not null) {
            NativeMemory.AlignedFree(_captKillers);
            _captKillers = null;
        }
    }

    // save a new killer move at the specified depth
    internal static void Add(Move move, int depth) {
        var table = move.Capture != PType.NONE
            ? _captKillers : _killers;

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

    // returns the cluster of killers at a certain depth
    internal static Span<Move> GetCluster(int depth, bool captures) {
        int offset = CapacityPerCluster * (_depth - depth);
        return new Span<Move>((captures ? _captKillers : _killers) + offset, CapacityPerCluster);
    }
}
