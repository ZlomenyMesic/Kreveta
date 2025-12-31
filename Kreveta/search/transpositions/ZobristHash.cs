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
using System.Runtime.InteropServices;

namespace Kreveta.search.transpositions;

// this zobrist hash shall not be used for Polyglot indexing
internal static unsafe class ZobristHash {

    // every square-piece combination
    private static readonly ulong[] Pieces = new ulong[64 * 12];

    // possible files of en passant square
    private static readonly ulong* EnPassant
        = (ulong*)NativeMemory.AlignedAlloc(8 * sizeof(ulong), 64);

    // all possible combinations of castling rights
    private static readonly ulong* Castling
        = (ulong*)NativeMemory.AlignedAlloc(16 * sizeof(ulong), 64);

    // white x black to play
    private static readonly ulong WhiteToMove;
    private static readonly ulong BlackToMove;

    // this seed was taken from MinimalChess, and actually
    // works very well. might try to find a better one in the
    // future, though
    private const int Seed = 228126;

    static ZobristHash() {
        var rand = new Random(Seed);

        for (int sq = 0; sq < 64; sq++) {
            for (int p = 0; p < 12; p++) {
                Pieces[sq * 12 + p] = NextUInt64(rand);
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

    internal static ulong Hash(in Board board) {
        ulong hash = board.SideToMove == Color.WHITE
            ? WhiteToMove
            : BlackToMove;

        hash ^= Castling[(byte)board.CastRights];

        if (board.EnPassantSq != 64)
            hash ^= EnPassant[board.EnPassantSq & 7];
        
        ReadOnlySpan<ulong> boardPieces = board.Pieces;
        ReadOnlySpan<ulong> pieceHashes = Pieces;
        
        // this is used to minimize array bound checks
        for (byte i = 0; i < 6; i++) {
                
            ulong wCopy = boardPieces[i];
            ulong bCopy = boardPieces[6 + i];

            while (wCopy != 0UL) {
                int sq = BB.LS1BReset(ref wCopy);
                    
                // color stride 6 for white
                hash ^= pieceHashes[sq * 12 + i + 6];
            }

            while (bCopy != 0UL) {
                int sq = BB.LS1BReset(ref bCopy);
                hash ^= pieceHashes[sq * 12 + i];
            }
        }

        return hash;
    }

    internal static ulong GetPawnHash(in Board board, Color col) {
        ulong hash = 0UL;
        ulong copy = board.Pieces[(byte)col * 6 /* + PType.PAWN */];
        
        // since we only hash pawns, we don't need to include the piece
        // type at all, because pawns are 0. we only want the color stride
        byte colStride = (byte)(col == Color.WHITE ? 6 : 0);
        
        ReadOnlySpan<ulong> pieceHashes = Pieces;

        while (copy != 0UL) {
            int sq = BB.LS1BReset(ref copy);
            hash ^= pieceHashes[sq * 12 + colStride];
        }

        return hash;
    }
    
    internal static ulong GetMinorPieceHash(in Board board, Color col) {
        ulong hash      = 0UL;
        ulong knights   = board.Pieces[(byte)col * 6 + 1];
        ulong bishops   = board.Pieces[(byte)col * 6 + 2];
        byte  colStride = (byte)(col == Color.WHITE ? 6 : 0);
        
        ReadOnlySpan<ulong> pieceHashes = Pieces;

        while (knights != 0UL) {
            int sq = BB.LS1BReset(ref knights);
            hash ^= pieceHashes[sq * 12 + colStride + 1];
        }
        
        while (bishops != 0UL) {
            int sq = BB.LS1BReset(ref bishops);
            hash ^= pieceHashes[sq * 12 + colStride + 2];
        }

        return hash;
    }
    
    internal static ulong GetMajorPieceHash(in Board board, Color col) {
        ulong hash      = 0UL;
        ulong rooks     = board.Pieces[(byte)col * 6 + 3];
        ulong queens    = board.Pieces[(byte)col * 6 + 4];
        byte  colStride = (byte)(col == Color.WHITE ? 6 : 0);
        
        ReadOnlySpan<ulong> pieceHashes = Pieces;

        while (rooks != 0UL) {
            int sq = BB.LS1BReset(ref rooks);
            hash ^= pieceHashes[sq * 12 + colStride + 3];
        }
        
        while (queens != 0UL) {
            int sq = BB.LS1BReset(ref queens);
            hash ^= pieceHashes[sq * 12 + colStride + 4];
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

    internal static void Clear() {
        NativeMemory.AlignedFree(EnPassant);
        NativeMemory.AlignedFree(Castling);
    }
}

#pragma warning restore CA5394
#pragma warning restore CA1810

#pragma warning restore IDE0079