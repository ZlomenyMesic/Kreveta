//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System;
using System.IO;
// ReSharper disable InconsistentNaming

#pragma warning disable CS8618

namespace Kreveta.nnue;

internal static class NNUEWeights {
    
    // cache-friendly flat arrays frfr
    // embedding: [769 * 256]
    internal static float[] Embedding;
    
    // dense weights: neuron-major [32 * 256]
    internal static float[] DenseKernelByNeuron;
    internal static float[] DenseBias;
    internal static float[] OutputKernel;
    internal static float   OutputBias;

    // Architecture parameters
    private const int EmbedCount   = 769;
    private const int EmbedDims    = 256;
    internal const int DenseNeurons = 32;

    
    internal static void Load(string binPath) {
        byte[]  rawBytes   = File.ReadAllBytes(binPath);
        int     floatCount = rawBytes.Length / 4;
        float[] all        = new float[floatCount];

        Buffer.BlockCopy(rawBytes, 0, all, 0, rawBytes.Length);

        int off = 0;

        // embedding FLAT (no jagged conversion)
        Embedding = Take(EmbedCount * EmbedDims);
        
        // dense kernel, but rearranged into neuron-major layout.
        // the original Keras layout is row-major [256, 32]
        float[] denseKeras = Take(EmbedDims * DenseNeurons);
        DenseKernelByNeuron = new float[DenseNeurons * EmbedDims];
        
        // keras layout: denseKeras[row * 32 + col]
        // our neuron-major: DenseKernelByNeuron[col*256 + row]
        for (int r = 0; r < EmbedDims; r++) {
            int kerasRow = r * DenseNeurons;
            for (int c = 0; c < DenseNeurons; c++) {
                DenseKernelByNeuron[c * EmbedDims + r] = denseKeras[kerasRow + c];
            }
        }
        
        DenseBias    = Take(DenseNeurons);
        OutputKernel = Take(DenseNeurons);
        OutputBias   = Take(1)[0];

        return;


        float[] Take(int count) {
            float[] slice = new float[count];
            Array.Copy(all, off, slice, 0, count);
            off += count;
            return slice;
        }
    }
}