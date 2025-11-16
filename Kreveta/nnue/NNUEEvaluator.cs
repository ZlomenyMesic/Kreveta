//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.movegen;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

// ReSharper disable InconsistentNaming
namespace Kreveta.nnue;

internal sealed class NNUEEvaluator {
    
    // reusable buffer to avoid repeated allocations
    private readonly float[] _hiddenLayerActivation = new float[32];
    private readonly float[] _accumulator = new float[256];

    internal short Score { get; private set; }
    
    internal NNUEEvaluator() {}

    internal NNUEEvaluator(in NNUEEvaluator other) {
        Array.Copy(other._accumulator,_accumulator, 256);
        Score = other.Score;
    }

    internal NNUEEvaluator(in Board board) {
        Update(in board);
    }

    internal void Update(in Board board) {
        Array.Clear(_accumulator, 0, _accumulator.Length);

        // rebuild the accumulator from scratch
        int[] features = ExtractFeatures(in board);
        for (int i = 0; i < 32; i++)
            UpdateFeatureInAccumulator(features[i], true);
        
        UpdateEvaluation();
    }

    internal void Update(Move move, Color colMoved) {
        int start = move.Start;
        int end   = move.End;
        
        PType piece = move.Piece;
        PType capt  = move.Capture;
        PType prom  = move.Promotion;
        
        Color oppColor = colMoved == Color.WHITE
            ? Color.BLACK : Color.WHITE;
        
        // deactivate the piece that moved from its starting square
        Deactivate(piece, colMoved, start);
        
        // deactivate a potential capture
        if (capt != PType.NONE)
            Deactivate(capt, oppColor, end);
        
        // regular move - just put the piece on its new square
        if (prom == PType.NONE)
            Activate(piece, colMoved, end);
        
        // activate the new piece in case of promotion
        else if (prom is PType.KNIGHT or PType.BISHOP or PType.ROOK or PType.QUEEN)
            Activate(prom, colMoved, end);
        
        // en passant - remove the captured pawn
        else if (prom == PType.PAWN) {
            
            // the pawn that is to be captured
            int captureSq = colMoved == Color.WHITE
                ? end + 8
                : end - 8;
            
            Activate(PType.PAWN, colMoved, end);
            Deactivate(PType.PAWN, oppColor, captureSq);
        }
        
        // castling
        else if (prom == PType.KING) {
            
            // first move the king to its new square
            Activate(PType.KING, colMoved, end);

            // and then move the respective rook
            switch (end) {
                // white kingside
                case 62: {
                    Deactivate(PType.ROOK, colMoved, 63);
                    Activate(PType.ROOK, colMoved, 61);
                    break;
                }

                // white queenside
                case 58: {
                    Deactivate(PType.ROOK, colMoved, 56);
                    Activate(PType.ROOK, colMoved, 59);
                    break;
                }
                
                // black kingside
                case 6: {
                    Deactivate(PType.ROOK, colMoved, 7);
                    Activate(PType.ROOK, colMoved, 5);
                    break;
                }

                // black queenside
                case 2: {
                    Deactivate(PType.ROOK, colMoved, 0);
                    Activate(PType.ROOK, colMoved, 3);
                    break;
                }
            }
        }
        
        UpdateEvaluation();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Activate(PType piece, Color col, int sq) {
        int feature = CreateFeatureIndex(col, piece, sq);
        UpdateFeatureInAccumulator(feature, true);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Deactivate(PType piece, Color col, int sq) {
        int feature = CreateFeatureIndex(col, piece, sq);
        UpdateFeatureInAccumulator(feature, false);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CreateFeatureIndex(Color col, PType piece, int sq)
        => ((int)col * 6 + (int)piece) * 64 + (sq ^ 56);

    private static int[] ExtractFeatures(in Board board) {
        List<int> features = [];

        for (int sq = 0; sq < 64; sq++) {
            if ((board.Occupied & 1UL << sq) == 0)
                continue;
            
            PType piece = board.PieceAt(sq);
            Color col   = (board.WOccupied & 1UL << sq) != 0 
                ? Color.WHITE : Color.BLACK;

            features.Add(CreateFeatureIndex(col, piece, sq));
        }
        
        // pad to length 32 with -1
        while (features.Count < 32)
            features.Add(-1);
        
        return [..features];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateFeatureInAccumulator(int feature, bool activate) {
        int   embedIndex = feature + 1;       // model uses f + 1
        int   baseIndex  = embedIndex * 256;  // flat embedding base
        float sign       = activate ? 1.0f : -1.0f;
        
        float[] acc  = _accumulator;
        float[] emb  = NNUEWeights.Embedding;
        int vecWidth = Vector<float>.Count;
        
        int i = 0;

        // SIMD loop
        for (; i <= 256 - vecWidth; i += vecWidth) {
            var vAcc = new Vector<float>(acc, i);
            var vEmb = new Vector<float>(emb, baseIndex + i);
            
            vAcc += vEmb * sign;
            vAcc.CopyTo(acc, i);
        }

        // scalar tail
        for (; i < 256; i++)
            acc[i] += emb[baseIndex + i] * sign;
    }

    // forward pass through the network for a single position.
    // uses the accumulator as input and computes the evaluation
    private void UpdateEvaluation() {
        int vecWidth = Vector<float>.Count;

        // 32-neuron hidden dense layer; reuses a buffer instead of more
        // allocating. no need to clear, as all entries are overwritten
        float[] hiddenActivation = _hiddenLayerActivation;

        float[] acc         = _accumulator;
        float[] denseBias   = NNUEWeights.H1Bias;
        float[] denseKernel = NNUEWeights.H1Kernel;
        
        for (int j = 0; j < NNUEWeights.H1Neurons; j++) {
            float sum = denseBias[j];

            int i = 0;
            int wBase = j * 256;

            // manual dot product with SIMD
            for (; i <= 256 - vecWidth; i += vecWidth) {
                var vA = new Vector<float>(acc, i);
                var vW = new Vector<float>(denseKernel, wBase + i);
                
                sum += Vector.Dot(vA, vW);
            }

            // scalar remainder
            for (; i < 256; i++)
                sum += acc[i] * denseKernel[wBase + i];
            
            // SCReLU activation function (Square Clipped ReLU)
            sum = Math.Clamp(sum, 0f, 1f);
            hiddenActivation[j] = sum * sum;
        }
        
        // output layer (single neuron)
        float   prediction   = NNUEWeights.OutputBias;
        float[] outputKernel = NNUEWeights.OutputKernel;

        int k = 0;
        for (; k <= NNUEWeights.H1Neurons - vecWidth; k += vecWidth) {
            var vH = new Vector<float>(hiddenActivation, k);
            var vW = new Vector<float>(outputKernel, k);
            
            prediction += Vector.Dot(vH, vW);
        }

        for (; k < NNUEWeights.H1Neurons; k++)
            prediction += hiddenActivation[k] * outputKernel[k];

        // sigmoid final probability
        prediction = 1f / (1f + MathF.Exp(-prediction));
        
        // inverse to match correct score format
        Score = (short)((prediction - 0.5f) * 2000);
    }

    // the python training script turns all evaluations (in cp)
    // into a probability score in range [0..1]. now that the
    // network predicted a probability, it shall be turned back
    // into a cp score using the inverse of the mentioned funcion
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short InverseCPToP(float p) {
        const float epsilon = 1e-6f;
        p = Math.Clamp(p, epsilon, 1 - epsilon);
        
        int cp = (int)(400 * MathF.Log(p / (1 - p), MathF.E));
        return (short)Math.Clamp(cp, -3000, 3000);
    }
}