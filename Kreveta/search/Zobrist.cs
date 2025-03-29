/*
 * |============================|
 * |                            |
 * |    Kreveta chess engine    |
 * | engineered by ZlomenyMesic |
 * | -------------------------- |
 * |      started 4-3-2025      |
 * | -------------------------- |
 * |                            |
 * | read README for additional |
 * | information about the code |
 * |    and usage that isn't    |
 * |  included in the comments  |
 * |                            |
 * |============================|
 */

namespace Kreveta.search;

internal static class Zobrist {

    private static readonly ulong[,] pieces = new ulong[64, 12];

    // TODO - MAKE THIS SMALLER
    private static readonly ulong[] en_passant = new ulong[64];

    // all possible permutations of castling rights
    private static readonly ulong[] castling = new ulong[16];

    // white x black
    private static readonly ulong[] side_to_move = new ulong[2];

    static Zobrist() {
        Random r = new(228126);

        for (int i = 0; i < 64; i++) {
            for (int j = 0; j < 12; j++) {
                pieces[i, j] = R(r);
            }

            en_passant[i] = R(r);
        }

        side_to_move[0] = R(r);
        side_to_move[1] = R(r);

        for (int i = 0; i < 16; i++) {
            castling[i] = R(r);
        }
    }

    internal static ulong GetHash(Board b) {
        ulong hash = side_to_move[b.color];

        hash ^= castling[(byte)b.castling];

        if (b.en_passant_sq != 64)
            hash ^= en_passant[b.en_passant_sq];

        for (int i = 0; i < 64; i++) {

            (int c, int piece) = b.PieceAt(i);
            hash ^= GetPieceHash(piece, c, i);
        }

        return hash;
    }

    private static ulong GetPieceHash(int piece, int col, int square) {
        if (piece == 6) 
            return 0;

        int index = piece + col == 0 ? 6 : 0;
        return pieces[square, index];
    }

    private static ulong R(Random r) {
        byte[] bytes = new byte[8];
        r.NextBytes(bytes);
        return BitConverter.ToUInt64(bytes, 0);
    }
}
