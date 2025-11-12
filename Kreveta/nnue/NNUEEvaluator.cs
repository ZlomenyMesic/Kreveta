using Kreveta.consts;
using Kreveta.movegen;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
// ReSharper disable InconsistentNaming

namespace Kreveta.nnue;

internal struct NNUEEvaluator {
    private record struct Bucket(int Index, bool Mirrored);

    private Bucket BlackBucket;
    private Bucket WhiteBucket;

    private short[] Black;
    private short[] White;

    internal short Score { get; set; }

    /*public NNUEEvaluator() {
        Black = new short[NNUENetwork.Default.Layer1Size];
        White = new short[NNUENetwork.Default.Layer1Size];
    }

    public NNUEEvaluator(NNUEEvaluator eval) {
        Black = new short[NNUENetwork.Default.Layer1Size];
        White = new short[NNUENetwork.Default.Layer1Size];
        Copy(eval);
    }*/

    internal NNUEEvaluator(Board board) {
        Black = new short[NNUENetwork.Default.Layer1Size];
        White = new short[NNUENetwork.Default.Layer1Size];
        Update(board);
    }

    internal void Copy(NNUEEvaluator other) {
        Array.Copy(other.White, White, NNUENetwork.Default.Layer1Size);
        Array.Copy(other.Black, Black, NNUENetwork.Default.Layer1Size);
        
        WhiteBucket = other.WhiteBucket;
        BlackBucket = other.BlackBucket;
        Score       = other.Score;
    }

    internal void Update(Board board) {
        RebuildAccumulator(Color.WHITE, board);
        RebuildAccumulator(Color.BLACK, board);
        UpdateEval(board);
    }

    internal void Update(NNUEEvaluator eval, Move move, Board newBoard) {
        //Copy(eval);

        //Color colMoved = newBoard.Color == Color.WHITE 
        //    ? Color.BLACK : Color.WHITE;
        
        Update(newBoard);
        return;

        /*if (KingBucket(Color.WHITE, newBoard) != WhiteBucket) {
            UpdateAccumulator(Color.BLACK, move, colMoved);
            RebuildAccumulator(Color.WHITE, newBoard);
        }
        else if (KingBucket(Color.BLACK, newBoard) != BlackBucket) {
            UpdateAccumulator(Color.WHITE, move, colMoved);
            RebuildAccumulator(Color.BLACK, newBoard);
        }
        else {
            UpdateAccumulator(Color.BLACK, move, colMoved);
            UpdateAccumulator(Color.WHITE, move, colMoved);
        }

        UpdateEval(newBoard);*/
    }

    private void UpdateEval(Board board) {
        int pieceCount   = (int)ulong.PopCount(board.Occupied);
        int outputBucket = NNUENetwork.Default.GetMaterialBucket(pieceCount);

        Score = (short)(Evaluate(board.Color, outputBucket)
                * (board.Color == Color.WHITE ? -1 : 1));

        Score /= 30;
    }

    private void RebuildAccumulator(Color perspective, Board board) {
        if (perspective == Color.WHITE) {
            Array.Copy(NNUENetwork.Default.FeatureBiases, White, NNUENetwork.Default.Layer1Size);
            WhiteBucket = KingBucket(Color.WHITE, board);
        }
        else {
            Array.Copy(NNUENetwork.Default.FeatureBiases, Black, NNUENetwork.Default.Layer1Size);
            BlackBucket = KingBucket(Color.BLACK, board);
        }

        for (ulong bits = board.WOccupied; bits != 0; ) {
            int square  = BB.LS1BReset(ref bits);
            PType piece = board.PieceAt(square);
            
            Activate(perspective, piece, Color.WHITE, square);
        }
        for (ulong bits = board.BOccupied; bits != 0; ) {
            int square  = BB.LS1BReset(ref bits);
            PType piece = board.PieceAt(square);
            
            Activate(perspective, piece, Color.BLACK, square);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Bucket KingBucket(Color color, Board board) {
        int sq = color == Color.BLACK ?
            BB.LS1B(board.Pieces[11]) ^ 56 :
            BB.LS1B(board.Pieces[5]);

        return new Bucket(NNUENetwork.Default.InputBucketMap[sq], (sq & 7) >= 4);
    }

    private void UpdateAccumulator(Color perspective, Move move, Color colMoved) {
        int start = move.Start;
        int end   = move.End;
        
        PType piece = move.Piece;
        PType capt  = move.Capture;
        PType prom  = move.Promotion;
        
        Color oppColor = colMoved == Color.BLACK 
            ? Color.WHITE : Color.BLACK;
        
        // deactivate the piece that moved from its starting square
        Deactivate(perspective, piece, colMoved, start);
        
        // deactivate a potential capture
        if (capt != PType.NONE)
            Deactivate(perspective, capt, oppColor, end);
        
        // regular move - just put the piece on its new square
        if (prom == PType.NONE)
            Activate(perspective, piece, colMoved, end);
        
        // activate the new piece in case of promotion
        else if (prom is PType.KNIGHT or PType.BISHOP or PType.ROOK or PType.QUEEN)
            Activate(perspective, prom, colMoved, end);
        
        // en passant - remove the captured pawn
        else if (prom == PType.PAWN) {
            
            // the pawn that is to be captured
            int captureSq = colMoved == Color.WHITE
                ? end + 8
                : end - 8;
            
            Activate(perspective, PType.PAWN, colMoved, end);
            Deactivate(perspective, PType.PAWN, oppColor, captureSq);
        }
        
        // castling
        else if (prom == PType.KING) {
            
            // first move the king to its new square
            Activate(perspective, PType.KING, colMoved, end);

            // and then move the respective rook
            switch (end) {
                // white kingside
                case 62: {
                    Deactivate(perspective, PType.ROOK, colMoved, 63);
                    Activate(perspective, PType.ROOK, colMoved, 61);
                    break;
                }

                // white queenside
                case 58: {
                    Deactivate(perspective, PType.ROOK, colMoved, 56);
                    Activate(perspective, PType.ROOK, colMoved, 59);
                    break;
                }
                
                // black kingside
                case 6: {
                    Deactivate(perspective, PType.ROOK, colMoved, 7);
                    Activate(perspective, PType.ROOK, colMoved, 5);
                    break;
                }

                // black queenside
                case 2: {
                    Deactivate(perspective, PType.ROOK, colMoved, 0);
                    Activate(perspective, PType.ROOK, colMoved, 3);
                    break;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FeatureIndices(Color perspective, PType piece, Color pieceCol, int sq) {
        /*const int ColorStride  = 64 * 6;
        const int PieceStride  = 64;
        const int BucketStride = NNUENetwork.InputSize;

        int type  = (int)piece;
        int white = pieceCol == Color.WHITE ? 1 : 0;

        if (color == Color.BLACK) {
            int blackSquare = sq ^ (BlackBucket.Mirrored ? 63 : 56);
            int blackIndex = BlackBucket.Index * BucketStride + white * ColorStride + type * PieceStride + blackSquare;
            return blackIndex * NNUENetwork.Default.Layer1Size;
        }
        else {
            int whiteSquare = sq ^ (WhiteBucket.Mirrored ? 7 : 0);
            int whiteIdx = WhiteBucket.Index * BucketStride + (white ^ 1) * ColorStride + type * PieceStride + whiteSquare;
            return whiteIdx * NNUENetwork.Default.Layer1Size;
        }*/
        const int ColorStride  = 64 * 6;
        const int PieceStride  = 64;
        const int BucketStride = NNUENetwork.InputSize;

        int type  = (int)piece;
        int isWhitePiece = pieceCol == Color.WHITE ? 0 : 1;

        if (perspective == Color.BLACK)
        {
            // Mirror the board for black’s perspective
            int blackSquare = sq ^ (BlackBucket.Mirrored ? 63 : 56);

            // For black’s perspective, *black pieces* are “us” → go to first color half
            int friendly = pieceCol == Color.BLACK ? 1 : 0;

            int blackIndex = BlackBucket.Index * BucketStride
                             + friendly * ColorStride
                             + type * PieceStride
                             + blackSquare;

            return blackIndex * NNUENetwork.Default.Layer1Size;
        }
        else
        {
            // White’s perspective (no mirror except optional 7-file flip)
            int whiteSquare = sq ^ (WhiteBucket.Mirrored ? 7 : 0);

            // For white’s perspective, *white pieces* are “us” → go to first color half
            int friendly = pieceCol == Color.WHITE ? 1 : 0;

            int whiteIndex = WhiteBucket.Index * BucketStride
                             + friendly * ColorStride
                             + type * PieceStride
                             + whiteSquare;

            return whiteIndex * NNUENetwork.Default.Layer1Size;
        }
    }

    private void Activate(Color perspective, PType piece, Color pieceCol, int sq) {
        if (piece == PType.NONE)
            return;

        int offset = FeatureIndices(perspective, piece, pieceCol, sq);
        Span<short> accu = perspective == Color.BLACK ? Black : White;
        Span<short> weights = NNUENetwork.Default.FeatureWeights.AsSpan(offset, NNUENetwork.Default.Layer1Size);

        //for (int i = 0; i < accu.Length; i++)
        //    accu[i] += weights[i];

        Span<Vector256<short>> accuVectors = MemoryMarshal.Cast<short, Vector256<short>>(accu);
        Span<Vector256<short>> weightsVectors = MemoryMarshal.Cast<short, Vector256<short>>(weights);
        for (int i = 0; i < accuVectors.Length; i++)
            accuVectors[i] += weightsVectors[i];
    }

    private void Deactivate(Color perspective, PType piece, Color pieceCol, int sq) {
        if (piece == PType.NONE)
            return;

        int offset = FeatureIndices(perspective, piece, pieceCol, sq);
        Span<short> accu = perspective == Color.BLACK ? Black : White;
        Span<short> weights = NNUENetwork.Default.FeatureWeights.AsSpan(offset, NNUENetwork.Default.Layer1Size);

        //for (int i = 0; i < accu.Length; i++)
        //    accu[i] -= weights[i];

        Span<Vector256<short>> accuVectors = MemoryMarshal.Cast<short, Vector256<short>>(accu);
        Span<Vector256<short>> weightsVectors = MemoryMarshal.Cast<short, Vector256<short>>(weights);
        for (int i = 0; i < accuVectors.Length; i++)
            accuVectors[i] -= weightsVectors[i];
    }

    private int Evaluate(Color stm, int outputBucket) {
        int output = stm == Color.BLACK
            ? EvaluateHiddenLayer(Black, White, NNUENetwork.Default.OutputWeights, outputBucket)
            : EvaluateHiddenLayer(White, Black, NNUENetwork.Default.OutputWeights, outputBucket);

        //during SCReLU values end up multiplied with QA * QA * QB
        //but OutputBias is quantized by only QA * QB
        output /= NNUENetwork.QA;
        output += NNUENetwork.Default.OutputBiases[outputBucket];
        //Now scale and convert back to float!
        return (output * NNUENetwork.Scale) / (NNUENetwork.QA * NNUENetwork.QB);
    }

    private static int EvaluateHiddenLayer(short[] us, short[] them, short[] weights, int bucket) {
        int length = NNUENetwork.Default.Layer1Size;
        int offset = bucket * 2 * length;
        int sum = ApplySCReLU(us, weights.AsSpan(offset, length))
                  + ApplySCReLU(them, weights.AsSpan(offset + length, length));
        return sum;
    }

    private static int ApplySCReLU(Span<short> accu, Span<short> weights) {
        //int SquaredClippedReLU(int value) => Math.Clamp(value, 0, Network.QA) * Math.Clamp(value, 0, Network.QA);
        //int sum = 0;
        //for (int i = 0; i < Network.Default.Layer1Size; ++i)
        //    sum += SquaredClippedReLU(accu[i]) * weights[i];
        //return sum;

        Vector256<short> ceil  = Vector256.Create<short>(NNUENetwork.QA);
        Vector256<short> floor = Vector256.Create<short>(0);
            
        Span<Vector256<short>> accuVectors    = MemoryMarshal.Cast<short, Vector256<short>>(accu);
        Span<Vector256<short>> weightsVectors = MemoryMarshal.Cast<short, Vector256<short>>(weights);
            
        Vector256<int> sum = Vector256<int>.Zero;
        for (int i = 0; i < accuVectors.Length; i++) {
            Vector256<short> a = Vector256.Max(Vector256.Min(accuVectors[i], ceil), floor); //ClippedReLU
            Vector256<short> w = weightsVectors[i];
            
            //with a being [0..255] and w being [-127..127] (a * w) fits into short but (a * a) can overflow
            //so instead of (a * a) * w we will compute (a * w) * a
            sum += Avx2.MultiplyAddAdjacent(w * a, a); //_mm256_madd_epi16
        }
        return Vector256.Sum(sum);
    }
}