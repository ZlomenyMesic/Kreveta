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
    internal static float[] H1Kernel;
    internal static float[] H1Bias;
    internal static float[] H2Kernel;
    internal static float[] H2Bias;
    internal static float[] OutputKernel;
    internal static float   OutputBias;

    // Architecture parameters
    private const  int EmbedCount = 769;
    private const  int EmbedDims  = 256;
    internal const int H1Neurons  = 32;
    internal const int H2Neurons  = 16;

    
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
        float[] h1kernelKeras = Take(EmbedDims * H1Neurons);
        H1Kernel = new float[EmbedDims * H1Neurons];
        
        // keras layout: denseKeras[row * 32 + col]
        // our neuron-major: DenseKernelByNeuron[col*256 + row]
        for (int r = 0; r < EmbedDims; r++) {
            int kerasRow = r * H1Neurons;
            for (int c = 0; c < H1Neurons; c++) {
                H1Kernel[c * EmbedDims + r] = h1kernelKeras[kerasRow + c];
            }
        }
        
        H1Bias = Take(H1Neurons);
        
        float[] h2kernelKeras = Take(H1Neurons * H2Neurons);
        H2Kernel = new float[H1Neurons * H2Neurons];
        
        for (int r = 0; r < H1Neurons; r++) {
            int kerasRow = r * H2Neurons;
            for (int c = 0; c < H2Neurons; c++) {
                H2Kernel[c * H1Neurons + r] = h2kernelKeras[kerasRow + c];
            }
        }
        
        H2Bias       = Take(H2Neurons);
        OutputKernel = Take(H2Neurons);
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