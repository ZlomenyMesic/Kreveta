//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//
 
// Remove unnecessary suppression
#pragma warning disable IDE0079

// Initialize reference type static fields inline    
#pragma warning disable CA1810

// Do not use insecure randomness
#pragma warning disable CA5394

using Kreveta.consts;

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Kreveta;

internal static unsafe class ZobristHash {

    // every square-piece combination
    [ReadOnly(true)] private static readonly ulong** Pieces
        = (ulong**)NativeMemory.AlignedAlloc(64 * 12 * sizeof(ulong), 64);

    // possible files of en passant square
    [ReadOnly(true)] private static readonly ulong* EnPassant
        = (ulong*)NativeMemory.AlignedAlloc(8 * sizeof(ulong), 64);

    // all possible combinations of castling rights
    [ReadOnly(true)] private static readonly ulong* Castling
        = (ulong*)NativeMemory.AlignedAlloc(16 * sizeof(ulong), 64);

    // white x black to play
    [ReadOnly(true)] private static readonly ulong WhiteToMove;
    [ReadOnly(true)] private static readonly ulong BlackToMove;

    // this seed was taken from MinimalChess, and actually
    // works very well. might try to find a better one in the
    // future, though
    private const int Seed = 228126;

    static ZobristHash() {
        var rand = new Random(Seed);

        for (int sq = 0; sq < 64; sq++) {
            Pieces[sq] = (ulong*)NativeMemory.AlignedAlloc(12 * sizeof(ulong), 64);

            for (int p = 0; p < 12; p++) {
                Pieces[sq][p] = NextUInt64(rand);
            }
        }

        for (int file = 0; file < 8; file++) {
            EnPassant[file] = NextUInt64(rand);
        }

        WhiteToMove = NextUInt64(rand);
        BlackToMove = NextUInt64(rand);

        for (int i = 0; i < 16; i++) {
            Castling[i] = NextUInt64(rand);
        }
    }

    internal static ulong GetHash(in Board board) {
        ulong hash = board.Color == Color.WHITE
            ? WhiteToMove
            : BlackToMove;

        hash ^= Castling[(byte)board.CastlingRights];

        if (board.EnPassantSq != 64)
            hash ^= EnPassant[board.EnPassantSq & 7];
        
        // this is used to minimize array bound checks
        fixed (ulong* wPieces = &board.Pieces[(byte)Color.WHITE][0],
                      bPieces = &board.Pieces[(byte)Color.BLACK][0]) {
            
            for (byte i = 0; i < 6; i++) {

                ulong wCopy = *(wPieces + i);
                ulong bCopy = *(bPieces + i);

                while (wCopy != 0UL) {
                    int sq = BB.LS1BReset(ref wCopy);
                    
                    // color stride 6 for white
                    hash ^= Pieces[sq][i + 6];
                }

                while (bCopy != 0UL) {
                    int sq = BB.LS1BReset(ref bCopy);
                    hash ^= Pieces[sq][i];
                }
            }
        }

        return hash;
    }

    internal static ulong GetPawnHash(in Board board, Color col) {
        ulong hash = 0;
        ulong copy = board.Pieces[(byte)col][(byte)PType.PAWN];
        
        // since we only hash pawns, we don't need to include the piece
        // type at all, because pawns are 0. we only want the color stride
        byte colStride = (byte)(col == Color.WHITE ? 6 : 0);

        while (copy != 0UL) {
            int sq = BB.LS1BReset(ref copy);
            hash ^= Pieces[sq][colStride];
        }

        return hash;
    }

    private static ulong NextUInt64(in Random rand) {
        byte[] bytes = new byte[
            sizeof(ulong) / sizeof(byte)
        ];

        // although the Random class is not secure, it is deterministic,
        // which is what we need, because we have our optimal seeds. i did
        // experiment with other approaches, such as PCG, but those didn't
        // work nearly as well as Random does
        rand.NextBytes(bytes);
        return BitConverter.ToUInt64(bytes);
    }
}

#pragma warning restore CA5394
#pragma warning restore CA1810

#pragma warning restore IDE0079