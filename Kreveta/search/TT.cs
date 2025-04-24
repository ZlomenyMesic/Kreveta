//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.evaluation;
using Kreveta.movegen;

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
// ReSharper disable InconsistentNaming

namespace Kreveta.search;

internal static class TT {

    // minimum ply needed to use tt
    internal const int MinProbingPly = 4;

    private enum ScoreType : byte {
        UPPER_BOUND,
        LOWER_BOUND,
        EXACT
    }

    [StructLayout(LayoutKind.Explicit, Size = EntrySize)]
    private record struct Entry {
        // 8 bytes
        [field: FieldOffset(0)]
        internal ulong Hash;

        // 2 bytes
        [field: FieldOffset(sizeof(ulong))]
        internal short Score;

        // 1 byte
        [field: FieldOffset(sizeof(ulong) + sizeof(short))]
        internal sbyte Depth;

        // 1 byte
        [field: FieldOffset(sizeof(ulong) + sizeof(short) + sizeof(sbyte))]
        internal ScoreType Type;

        // 4 bytes
        [field: FieldOffset(sizeof(ulong) + sizeof(short) + sizeof(sbyte) + sizeof(ScoreType))]
        internal Move BestMove;
    }

    // size of a single hash entry
    private const int EntrySize = 16;

    // size of the tt array
    private static int TableSize = GetTableSize();

    // how many items are currently stored
    private static int Stored;

    private static volatile Entry[] Table = new Entry[TableSize];

    //private static long tt_lookups = 0;

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
        const int MaxSize = int.MaxValue / EntrySize;
        return (int)Math.Min((long)Options.Hash * 1048576 / EntrySize, MaxSize);
    }

    // generate an index in the tt for a specific board hash
    // key collisions can (and will) occur, so we later also check the correctness of this index
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HashIndex(ulong hash) {
        uint hash32 = (uint)hash ^ (uint)(hash >> 32);
        return (int)(hash32 % TableSize);
    }

    internal static void Clear() {
        Stored = 0;

        TableSize = GetTableSize();
        Table = new Entry[TableSize];
    }

    // instead of using an age value, we decrement the depths
    // in the entries stored for the next search, so they aren't
    // as important. note that this is only used in a full game
    internal static void ResetScores() {
        if (Stored == 0) return;

        for (int i = 0; i < TableSize; i++) {
            if (Table[i].Hash != default) {
                Table[i].Score = default;
                Table[i].Depth = default;
            }
        }
    }

    internal static void Store([In, ReadOnly(true)] in Board board, sbyte depth, int ply, Window window, short score, Move bestMove) {
        ulong hash = Zobrist.GetHash(board);
        int i = HashIndex(hash);

        // maybe an entry is already saved
        Entry existing = Table[i];

        // is the index already occupied with a result from a higher depth search?
        // key collisions may also be problematic - multiple positions
        // could have an identical key (i don't really care, though)
        if (existing.Hash != default && existing.Depth > depth) {
            return;
        }

        Entry entry = new() {
            Hash = hash,
            Depth = depth,
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
            entry.Type = ScoreType.UPPER_BOUND;
            entry.Score = window.Beta;

        } else if (score <= window.Alpha) {
            entry.Type = ScoreType.LOWER_BOUND;
            entry.Score = window.Alpha;

        } else {
            entry.Type = ScoreType.EXACT;
            entry.Score = score;
        }

        // if we aren't overwriting an existing entry, increase the counter
        if (existing.Hash == default)
            Stored++;

        // store the new entry or overwrite the old one if allowed
        Table[i] = entry;
    }

    internal static bool TryGetBestMove([In, ReadOnly(true)] in Board board, out Move bestMove) {
        ulong hash = Zobrist.GetHash(board);
        bestMove = default;

        int i = HashIndex(hash);
        Entry entry = Table[i];

        // the position isn't saved
        if (entry.Hash != hash)
            return false;

        bestMove = entry.BestMove;
        return bestMove != default;
    }

    internal static bool TryGetScore([In, ReadOnly(true)] in Board board, int depth, int ply, Window window, out short score) {
        ulong hash = Zobrist.GetHash(board);
        score = 0;

        int i = HashIndex(hash);
        Entry entry = Table[i];

        // once again the position is not yet saved
        if (entry.Hash != hash)
            return false;

        // the position is saved, but it's depth is shallower than ours
        if (entry.Depth <= depth)
            return false;

        score = entry.Score;

        // when retrieving the eval, we do the opposite of what is
        // described above - we add the current ply to the "mate in X"
        // to make it relative to the root node once again
        if (Score.IsMateScore(score)) {
            score -= (short)(Math.Sign(score) * ply);
            return true;
        }

        // lower and upper bound scores are only returned when
        // they fall outside the search window as labeled
        return entry.Type == ScoreType.EXACT
           || (entry.Type == ScoreType.LOWER_BOUND && score <= window.Alpha)
           || (entry.Type == ScoreType.UPPER_BOUND && score >= window.Beta);
    }
}