//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.evaluation;

namespace Kreveta.search;

internal static class Zobrist {

    private static readonly ulong[,] Pieces     = new ulong[64, 12];

    // TODO - MAKE THIS SMALLER
    private static readonly ulong[]  EnPassant  = new ulong[64];

    // all possible permutations of castling rights
    private static readonly ulong[]  Castling   = new ulong[16];

    // white x black
    private static readonly ulong[]  SideToMove = new ulong[2];

    // this seed was taken from MinimalChess, and actually
    // works very well. might try to find a better one in the
    // future, though
    private const int Seed = 228126;

    static Zobrist() {
        Random rand = new(Seed);

        for (int sq = 0; sq < 64; sq++) {
            for (int p = 0; p < 12; p++) {
                Pieces[sq, p] = RandUInt64(rand);
            }

            EnPassant[sq] = RandUInt64(rand);
        }

        SideToMove[0] = RandUInt64(rand);
        SideToMove[1] = RandUInt64(rand);

        for (int i = 0; i < 16; i++) {
            Castling[i] = RandUInt64(rand);
        }
    }

    internal static ulong GetHash(Board board) {
        ulong hash = SideToMove[(byte)board.color];

        hash ^= Castling[(byte)board.castRights];

        if (board.enPassantSq != 64)
            hash ^= EnPassant[board.enPassantSq];

        for (int i = 0; i < 6; i++) {

            ulong wCopy = board.pieces[(byte)Color.WHITE, i];
            ulong bCopy = board.pieces[(byte)Color.BLACK, i];

            while (wCopy != 0) {
                (wCopy, int sq) = BB.LS1BReset(wCopy);

                hash ^= GetPieceHash((PType)i, Color.WHITE, sq);
            }

            while (bCopy != 0) {
                (bCopy, int sq) = BB.LS1BReset(bCopy);

                hash ^= GetPieceHash((PType)i, Color.BLACK, sq);
            }
        }

        //for (int sq = 0; sq < 64; sq++) {
        //    (Color col, PType type) = board.PieceAt(sq);
        //    hash ^= GetPieceHash(type, col, sq);
        //}

        return hash;
    }

    internal static ulong GetHash2(Board board) {
        ulong hash = SideToMove[(byte)board.color];

        hash ^= Castling[(byte)board.castRights];

        if (board.enPassantSq != 64)
            hash ^= EnPassant[board.enPassantSq];

        //for (int i = 0; i < 6; i++) {

        //    ulong wCopy = board.pieces[(byte)Color.WHITE, i];
        //    ulong bCopy = board.pieces[(byte)Color.BLACK, i];

        //    while (wCopy != 0) {
        //        (wCopy, int sq) = BB.LS1BReset(wCopy);

        //        hash ^= GetPieceHash((PType)i, Color.WHITE, sq);
        //    }

        //    while (bCopy != 0) {
        //        (bCopy, int sq) = BB.LS1BReset(bCopy);

        //        hash ^= GetPieceHash((PType)i, Color.BLACK, sq);
        //    }
        //}

        for (int sq = 0; sq < 64; sq++) {
            (Color col, PType type) = board.PieceAt(sq);
            hash ^= GetPieceHash(type, col, sq);
        }

        return hash;
    }

    private static ulong GetPieceHash(PType piece, Color col, int square) {
        if (piece == PType.NONE) 
            return 0;

        int index = (byte)piece + (col == Color.WHITE ? 6 : 0);
        return Pieces[square, index];
    }

    private static ulong RandUInt64(Random rand) {
        byte[] bytes = new byte[
            sizeof(ulong) / sizeof(byte)
        ];

        rand.NextBytes(bytes);
        return BitConverter.ToUInt64(bytes, 0);
    }
}
