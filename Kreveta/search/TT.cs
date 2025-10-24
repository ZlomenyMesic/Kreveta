//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.evaluation;
using Kreveta.movegen;

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable InconsistentNaming

namespace Kreveta.search;

// during the search, most positions are usually achievable in many ways,
// and they repeat themselves, which is obviously time-consuming. for
// this reason we implement a table, where we store the already searched
// positions along with their score and the best move from that position.
// this data can be used to order moves or greatly decrease the number
// of nodes in the tree.
internal static unsafe class TT {

    // minimum ply needed to look up scores in tt
    internal const int MinProbingPly = 4;

    // depending on where the score fell relatively
    // to the window when saving, we store the score type
    [Flags]
    internal enum SpecialFlags : byte {
        SCORE_UPPER_BOUND = 1, // the score was above beta
        SCORE_LOWER_BOUND = 2, // the score was below alpha
        SCORE_EXACT       = 4, // the score fell right into the window

        SHOULD_OVERWRITE  = 8  // the node is old and should be overwritten
    }

    // this entry is stored for every position
    [StructLayout(LayoutKind.Explicit, Size = EntrySize)]
    private record struct Entry {

        // we store the board hash, because different hashes can
        // result in the same table index due to its size.
        // (8 bytes)
        [field: FieldOffset(0)]
        internal ulong Hash;

        // the best move found in this position - used for move ordering
        // (4 bytes)
        [field: FieldOffset(8)]
        internal Move BestMove;

        // the score of the position
        // (2 bytes)
        [field: FieldOffset(8 + 4)]
        internal short Score;

        // the depth at which the search was performed
        // => higher depth means a more truthful score
        // (1 byte)
        [field: FieldOffset(8 + 4 + 2)]
        internal sbyte Depth;

        // (1 byte)
        [field: FieldOffset(8 + 4 + 2 + 1)]
        internal SpecialFlags Flags;
    }

    // size of a single hash entry
    private const int EntrySize = 16;

    // size of the table
    private static int TableSize = GetTableSize();

    // how many entries are currently stored
    private static int Stored;

    // the table itself
    private static Entry* Table = (Entry*)NativeMemory.AlignedAlloc(
        byteCount: (nuint)(TableSize * EntrySize),
        alignment: EntrySize);

    internal static ulong TTHits;

    // hashfull tells us how filled is the hash table
    // in permill (entries per thousand). this number
    // is sent regularly to the GUI, which allows it
    // sent us a hash table clearing command (option)
    // in case we are too full to free some memory
    internal static int Hashfull =>
        (int)((float)Stored / TableSize * 1000);

    // tt array size = megabytes * bytes_in_mb / entry_size
    // we also limit the size as per the maximum allowed array size (2 GB)
    private static int GetTableSize() {
        const long EntriesInMB = 1_048_576 / EntrySize;
        return (int)(Options.Hash * EntriesInMB);
    }

    // generate an index in the tt for a specific board hash
    // key collisions can (and will) occur, so we later also
    // check the correctness of this index by comparing hashes
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HashIndex(ulong hash) {

        // method suggested somewhere online - to make the indices
        // more evenly dispersed, XOR the two hash halves first
        uint hash32 = (uint)hash ^ (uint)(hash >> 32);
        return (int)(hash32 % TableSize);
    }

    // delete all entries from the table
    internal static void Clear() {
        if (Table is not null) {
            NativeMemory.AlignedFree(Table);
            Table = null;
        }

        Stored = 0;
        TableSize = GetTableSize();
        TTHits = 0UL;
    }

    internal static void Init() {
        Clear();
        
        Table = (Entry*)NativeMemory.AlignedAlloc(
            byteCount: (nuint)(TableSize * EntrySize),
            alignment: EntrySize);
    }

    internal static void IncreaseAge() {
        // for (int i = 0; i < TableSize; i++) {
        //     
        //     if (Table[i].Hash != 0UL)
        //         Table[i].Flags |= SpecialFlags.SHOULD_OVERWRITE;
        // }
    }

    // store a position in the table. the best move doesn't have to be specified
    internal static void Store(in Board board, sbyte depth, int ply, Window window, short score, Move bestMove) {
        ulong hash = ZobristHash.GetHash(board);
        int i = HashIndex(hash);

        // maybe an entry is already saved
        var existing = Table[i];

        //bool isOld = (existing.Flags & SpecialFlags.SHOULD_OVERWRITE) != 0;

        // is the slot already occupied with a result
        // of a higher depth search?
        if (existing.Hash != 0UL && existing.Depth > depth) {
            return;
        }

        // if (existing.Hash != 0UL 
        //     && ((!isOld && existing.Depth > depth) 
        //         || (isOld && existing.Depth > depth + 1))) {
        //     
        //     return;
        // }

        var entry = new Entry {
            Hash     = hash,
            Depth    = depth,
            Flags    = 0,
            BestMove = bestMove
        };

        // idea from MinimalChess: when a position is evaluated "mate in X", the X plies are
        // relative to the root node. when we store such position, though, we have to subtract
        // the current ply to get the actual X plies relative to the position, not root.
        if (Score.IsMateScore(score)) {

            // since a mate score is a number of plies subtracted from a base,
            // we don't actually subtract the current ply, we add it. the idea
            // is, however, the same
            score += (short)(Math.Sign(score) * ply);
        }

        if (score >= window.Beta) {
            entry.Flags |= SpecialFlags.SCORE_UPPER_BOUND;
            entry.Score = window.Beta;

        } else if (score <= window.Alpha) {
            entry.Flags |= SpecialFlags.SCORE_LOWER_BOUND;
            entry.Score = window.Alpha;

        } else {
            entry.Flags |= SpecialFlags.SCORE_EXACT;
            entry.Score = score;
        }

        // if we aren't overwriting an existing entry, increase the counter
        if (existing.Hash == 0UL)
            Stored++;

        // store the new entry or overwrite the old one if allowed
        Table[i] = entry;
    }

    internal static bool TryGetBestMove(in Board board, out Move bestMove) {
        ulong hash = ZobristHash.GetHash(board);
        bestMove = default;

        int i = HashIndex(hash);
        Entry entry = Table[i];

        // the position isn't saved
        if (entry.Hash != hash)
            return false;

        bestMove = entry.BestMove;

        if (bestMove != default) {
            TTHits++;
            return true;
        }

        return false;
    }

    internal static bool TryGetScore(in Board board, int depth, int ply, Window window, out short score) {
        ulong hash = ZobristHash.GetHash(board);
        score = 0;

        int i = HashIndex(hash);
        Entry entry = Table[i];

        // once again the position is not yet saved
        if (entry.Hash != hash)
            return false;

        // the position is stored, but its depth is shallower than ours
        if (entry.Depth <= depth)
            return false;

        score = entry.Score;

        // when retrieving the eval, we do the opposite of what is
        // described above - we add the current ply to the "mate in X"
        // to make it relative to the root node once again
        if (Score.IsMateScore(score)) {
            score -= (short)(Math.Sign(score) * ply);

            TTHits++;
            return true;
        }

        // lower and upper bound scores are only returned when
        // they fall outside the search window as labeled
        if     (entry.Flags.HasFlag(SpecialFlags.SCORE_EXACT)
            || (entry.Flags.HasFlag(SpecialFlags.SCORE_LOWER_BOUND) && score <= window.Alpha)
            || (entry.Flags.HasFlag(SpecialFlags.SCORE_UPPER_BOUND) && score >= window.Beta)) {

            TTHits++;
            return true;
        }

        return false;
    }
}