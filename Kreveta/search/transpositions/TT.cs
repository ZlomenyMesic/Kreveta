//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.evaluation;
using Kreveta.movegen;
using Kreveta.uci.options;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable InconsistentNaming

namespace Kreveta.search.transpositions;

// during the search, most positions are usually achievable in many ways,
// and they repeat themselves, which is obviously time-consuming. for
// this reason we implement a table, where we store the already searched
// positions along with their score and the best move from that position.
// this data can be used to order moves or greatly decrease the number
// of nodes in the tree.
internal static unsafe partial class TT {

    // size of the table and buckets
    private const  int BucketSize  = 4;
    private static int BucketCount = GetTableSize();

    // how many entries are currently stored
    private static int Stored;

    // the table itself
    private static Entry* Table = (Entry*)NativeMemory.AlignedAlloc(
        byteCount: (nuint)(BucketCount * BucketSize * EntrySize),
        alignment: EntrySize);

    internal static ulong TTHits;

    // hashfull tells us how filled is the hash table in permill (entries
    // per thousand). this number is sent regularly to the GUI, which allows
    // it sent us a hash table clearing command (option) in case we are too
    // full to free some memory
    internal static int Hashfull =>
        (int)((float)Stored / (BucketCount * BucketSize) * 1000);

    // tt array size = mebibytes * bytes_in_mib / entry_size
    // we also limit the size as per the maximum allowed array size (2 GB)
    private static int GetTableSize() {
        const long EntriesInMiB = 1_048_576 / EntrySize;
        int size = (int)(Options.Hash * EntriesInMiB / BucketSize);
        
        // to avoid potential indices pointing outside
        // the table, the size is adjusted accordingly
        return size - size % BucketSize;
    }

    // generate an index in the tt for a specific board hash
    // key collisions can (and will) occur, so we later also
    // check the correctness of this index by comparing hashes
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HashIndex(ulong hash) {

        // method suggested somewhere online - to make the indices
        // more evenly dispersed, XOR the two hash halves first
        uint hash32 = (uint)hash ^ (uint)(hash >> 32);
        return (int)(hash32 % BucketCount) * BucketSize;
    }

    // delete all entries and free the memory
    internal static void Clear() {
        if (Table is not null) {
            NativeMemory.AlignedFree(Table);
            Table = null;
        }

        Stored      = 0;
        BucketCount = GetTableSize();
        TTHits      = 0UL;
    }

    internal static void Init() {
        Clear();

        nuint byteCount = (nuint)(BucketCount * BucketSize * EntrySize);
        
        Table = (Entry*)NativeMemory.AlignedAlloc(
            byteCount: byteCount,
            alignment: EntrySize);
        
        Console.WriteLine($"{Table[30].Depth} {Table[30].Hash}");
    }

    // store a position in the table. the best move doesn't have to be specified
    internal static void Store(ulong hash, sbyte depth, int ply, Window window, short score, Move bestMove) {
        int index  = HashIndex(hash);
        var bucket = new ReadOnlySpan<Entry>(Table + index, BucketSize);

        int emptyIndex      = -1;
        int shallowestIndex = -1;
        int shallowestDepth = int.MaxValue;

        // the entry at this index will be overwritten
        int overwriteIndex;

        // go through the bucket content
        for (int i = 0; i < BucketSize; i++) {
            // 1. if the exact same position is already stored, it's only overwritten
            // in case its evaluation is the result of a shallower search. we must
            // make sure to return if this is false to avoid storing it twice
            if (bucket[i].Hash == hash) {
                if (depth >= bucket[i].Depth) {
                    overwriteIndex = i;
                    goto indexSelected;
                }
                return;
            }

            // 2. if this position isn't in the bucket, try to find
            // the first empty slot, which is going to be filled
            if (bucket[i].Hash == 0UL) {
                if (emptyIndex == -1)
                    emptyIndex = i;

                continue;
            }

            // 3. if this position isn't yet stored, and there are no empty slots
            // left, we track the entry with the shallowest depth. the least used
            // entry is given a depth penalty, so it's overwritten more easily
            if (bucket[i].Depth < shallowestDepth) {
                shallowestDepth = bucket[i].Depth;
                shallowestIndex = i;
            }
        }

        // there is an empty slot left
        if (emptyIndex != -1) {
            // increase the stored entries counter
            Stored++;
            
            overwriteIndex = emptyIndex;
            goto indexSelected;
        }

        // if there are no empty slots, we overwrite the shallowest entry,
        // but ONLY if its evaluation depth is lower than the current one
        if (shallowestDepth <= depth) {
            overwriteIndex = shallowestIndex;
            goto indexSelected;
        }
        
        // no suitable entry for overwriting has been found
        return;
        
        // we have found a suitable index for overwriting, so we build the new entry
        indexSelected:
        var entry = new Entry {
            Hash     = hash,
            Depth    = depth,
            Flags    = 0,
            BestMove = bestMove
        };

        // idea from MinimalChess: when a position is evaluated "mate in X", the X plies are
        // relative to the root node. when we store such position, though, we have to subtract
        // the current ply to get the actual X plies relative to the position, not root.
        if (Score.IsMate(score)) {

            // since a mate score is a number of plies subtracted from a base,
            // we don't actually subtract the current ply, we add it. the idea
            // is, however, the same
            entry.Score = (short)(score + Math.Sign(score) * ply);
            entry.Flags |= ScoreFlags.SCORE_EXACT;
        }

        else if (score >= window.Beta) {
            entry.Flags |= ScoreFlags.LOWER_BOUND;
            entry.Score = window.Beta;

        } else if (score <= window.Alpha) {
            entry.Flags |= ScoreFlags.UPPER_BOUND;
            entry.Score = window.Alpha;

        } else {
            entry.Flags |= ScoreFlags.SCORE_EXACT;
            entry.Score = score;
        }

        // store the new entry or overwrite the old one if allowed
        Table[index + overwriteIndex] = entry;
    }

    internal static bool TryGetBestMove(ulong hash, out Move ttMove, out short ttScore, out ScoreFlags ttFlags, out int ttDepth) {
        ttMove  = default;
        ttScore = 0;
        ttFlags = default;
        ttDepth = 0;

        // find the corresponding bucket
        int   index  = HashIndex(hash);
        var   bucket = new ReadOnlySpan<Entry>(Table + index, BucketSize);
        Entry entry  = default;

        // look through the bucket and try to find this position
        for (int i = 0; i < BucketSize; i++) {
            if (bucket[i].Hash == hash) {
                entry = bucket[i];
                break;
            }
        }

        // the position isn't stored in the bucket
        if (entry.Hash == 0UL)
            return false;

        ttMove  = entry.BestMove;
        ttScore = entry.Score;
        ttFlags = entry.Flags;
        ttDepth = entry.Depth;

        if (ttMove != default) {
            TTHits++;
            return true;
        }

        return false;
    }

    internal static bool TryGetScore(ulong hash, int depth, int ply, Window window, out short score) {
        score = 0;

        // find the corresponding bucket
        int   index  = HashIndex(hash);
        var   bucket = new ReadOnlySpan<Entry>(Table + index, BucketSize);
        Entry entry  = default;

        // look through the bucket and try to find this position
        for (int i = 0; i < BucketSize; i++) {
            if (bucket[i].Hash == hash) {
                entry = bucket[i];
                break;
            }
        }

        // don't return a score if the position doesn't exist, or
        // the evaluation stored is a result of a shallower search
        if (entry.Hash == 0UL || entry.Depth < depth)
            return false;

        score = entry.Score;
        
        // when retrieving the eval, we do the opposite of what is
        // described above - we add the current ply to the "mate in X"
        // to make it relative to the root node once again
        if (Score.IsMate(score)) {
            score -= (short)(Math.Sign(score) * ply);

            TTHits++;
            return true;
        }
        
        // lower and upper bound scores are only returned when
        // they fall outside the search window as labeled
        if (entry.Flags.HasFlag(ScoreFlags.SCORE_EXACT)
            || entry.Flags.HasFlag(ScoreFlags.UPPER_BOUND) && score <= window.Alpha
            || entry.Flags.HasFlag(ScoreFlags.LOWER_BOUND) && score >= window.Beta) {

            TTHits++;
            return true;
        }
        
        return false;
    }
}