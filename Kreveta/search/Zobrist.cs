//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

namespace Kreveta.search;

internal static class Zobrist {

    private static readonly ulong[,] pieces = new ulong[64, 12];

    // TODO - MAKE THIS SMALLER
    private static readonly ulong[] en_passant = new ulong[64];

    // all possible permutations of castling rights
    private static readonly ulong[] castling = new ulong[16];

    // white x black
    private static readonly ulong[] side_to_move = new ulong[2];

    // this seed was taken from MinimalChess, and actually
    // works very well. might try to find a better one in the
    // future, though
    private const int SEED = 228126;

    static Zobrist() {
        Random rand = new(SEED);

        for (int i = 0; i < 64; i++) {
            for (int j = 0; j < 12; j++) {
                pieces[i, j] = RandUInt64(rand);
            }

            en_passant[i] = RandUInt64(rand);
        }

        side_to_move[0] = RandUInt64(rand);
        side_to_move[1] = RandUInt64(rand);

        for (int i = 0; i < 16; i++) {
            castling[i] = RandUInt64(rand);
        }
    }

    internal static ulong GetHash(Board b) {
        ulong hash = side_to_move[(byte)b.color];

        hash ^= castling[(byte)b.castRights];

        if (b.enPassantSq != 64)
            hash ^= en_passant[b.enPassantSq];

        for (int sq = 0; sq < 64; sq++) {

            (Color c, PType piece) = b.PieceAt(sq);
            hash ^= GetPieceHash(piece, c, sq);
        }

        return hash;
    }

    private static ulong GetPieceHash(PType piece, Color col, int square) {
        if (piece == PType.NONE) 
            return 0;

        int index = (byte)piece + (col == Color.WHITE ? 6 : 0);
        return pieces[square, index];
    }

    private static ulong RandUInt64(Random r) {
        byte[] bytes = new byte[8];
        r.NextBytes(bytes);
        return BitConverter.ToUInt64(bytes, 0);
    }
}
