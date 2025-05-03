//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Kreveta.search;

internal static unsafe class Zobrist {

    // every square-piece combination
    [ReadOnly(true)] private static readonly ulong** Pieces    = (ulong**)NativeMemory.AlignedAlloc(64 * 12 * sizeof(ulong), 64);

    // possible files of en passant square
    [ReadOnly(true)] private static readonly ulong*  EnPassant = (ulong*)NativeMemory.AlignedAlloc(8 * sizeof(ulong), 64);

    // all possible combinations of castling rights
    [ReadOnly(true)] private static readonly ulong*  Castling  = (ulong*)NativeMemory.AlignedAlloc(16 * sizeof(ulong), 64);

    // white x black to play
    [ReadOnly(true)] private static readonly ulong   WhiteToMove;
    [ReadOnly(true)] private static readonly ulong   BlackToMove;

    // this seed was taken from MinimalChess, and actually
    // works very well. might try to find a better one in the
    // future, though
    private const int Seed = 228126;

    // Initialize reference type static fields inline    
#pragma warning disable CA1810

    static Zobrist() {

#pragma warning restore CA1810

        Random rand = new(Seed);

        for (int sq = 0; sq < 64; sq++) {
            Pieces[sq] = (ulong*)NativeMemory.Alloc(12 * sizeof(ulong));

            for (int p = 0; p < 12; p++) {
                Pieces[sq][p] = RandUInt64(rand);
            }
        }

        for (int file = 0; file < 8; file++) {
            EnPassant[file] = RandUInt64(rand);
        }

        WhiteToMove = RandUInt64(rand);
        BlackToMove = RandUInt64(rand);

        for (int i = 0; i < 16; i++) {
            Castling[i] = RandUInt64(rand);
        }
    }

    internal static ulong GetHash([In, ReadOnly(true)] in Board board) {
        ulong hash = board.Color == Color.WHITE
            ? WhiteToMove
            : BlackToMove;

        hash ^= Castling[(byte)board.CastlingRights];

        if (board.EnPassantSq != 64)
            hash ^= EnPassant[board.EnPassantSq & 7];

        for (int i = 0; i < 6; i++) {

            ulong wCopy = board.Pieces[(byte)Color.WHITE][i];
            ulong bCopy = board.Pieces[(byte)Color.BLACK][i];

            while (wCopy != 0UL) {
                int sq = BB.LS1BReset(ref wCopy);
                hash ^= GetPieceHash((PType)i, Color.WHITE, sq);
            }

            while (bCopy != 0UL) {
                int sq = BB.LS1BReset(ref bCopy);
                hash ^= GetPieceHash((PType)i, Color.BLACK, sq);
            }
        }

        return hash;
    }

    internal static ulong GetPawnHash([In, ReadOnly(true)] in Board board, Color col) {
        ulong hash = 0;

        ulong copy = board.Pieces[(byte)col][(byte)PType.PAWN];

        while (copy != 0UL) {
            int sq = BB.LS1BReset(ref copy);
            hash ^= GetPieceHash(PType.PAWN, col, sq);
        }

        return hash;
    }

    private static ulong GetPieceHash(PType piece, Color col, int square) {
        if (piece == PType.NONE)
            return 0;

        int index = (byte)piece + (col == Color.WHITE ? 6 : 0);
        return Pieces[square][index];
    }

    private static ulong RandUInt64([In, ReadOnly(true)] in Random rand) {
        byte[] bytes = new byte[
            sizeof(ulong) / sizeof(byte)
        ];

        // Remove unnecessary suppression
#pragma warning disable IDE0079

        // Do not use insecure randomness
#pragma warning disable CA5394

        rand.NextBytes(bytes);

#pragma warning restore CA5394
#pragma warning restore IDE0079

        return BitConverter.ToUInt64(bytes, 0);
    }
}