using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
// ReSharper disable InconsistentNaming

namespace Kreveta.nnue;

internal class NNUENetwork {
    private static readonly Dictionary<int, int[]> InputBucketMappings = new() {
        { 1, [
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0
            ]
        },
        { 5, [
            0, 0, 1, 1, 1, 1, 0, 0,
            2, 2, 3, 3, 3, 3, 2, 2,
            2, 2, 3, 3, 3, 3, 2, 2,
            4, 4, 4, 4, 4, 4, 4, 4,
            4, 4, 4, 4, 4, 4, 4, 4,
            4, 4, 4, 4, 4, 4, 4, 4,
            4, 4, 4, 4, 4, 4, 4, 4,
            4, 4, 4, 4, 4, 4, 4, 4
            ]
        }
    };

    internal static NNUENetwork Default { get; private set; }

    internal static void InitEmptyNetwork() {
        Default = new NNUENetwork(0, 1, 1);
    }

    internal static bool LoadDefaultNetwork() {
        string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        //Example 384HL-S-3io8-5061M-FRCv1.nnue
        string[] files = Directory.GetFiles(currentDirectory, $"*HL-S-5io*-*.nnue");
        if (files.Length > 1)
            Console.WriteLine("Warning: Multiple network files found!");
        if (files.Length == 0)
        {
            Console.WriteLine("Error: No network file found!");
            Console.WriteLine($"Current Directory: {currentDirectory}");
            return false;
        }
        string fileName = Path.GetFileName(files[0]);
        Console.WriteLine($"Loading NNUE weights from {fileName}!");

        var match = Regex.Match(fileName, @"^(?<layer1Size>\d+)HL-S-(?<input>\d+)io(?<output>\d+)-.*\.nnue$");
        if (!match.Success)
        {
            Console.WriteLine($"Error: Unexpected filename format: {fileName}");
            return false;
        }

        int layer1Size = int.Parse(match.Groups["layer1Size"].Value);
        int inputBuckets = int.Parse(match.Groups["input"].Value);
        int outputBuckets = int.Parse(match.Groups["output"].Value);

        Default = new NNUENetwork(files[0], layer1Size, inputBuckets, outputBuckets);
        return true;
    }

    internal const int   Scale     = 400;
    internal const short QA        = 255;
    internal const short QB        = 64;
    internal const int   InputSize = 768;

    internal int   Layer1Size;
    internal int   InputBuckets;
    public int     OutputBuckets;
    public short[] FeatureWeights; //[InputSize * Layer1Size];
    public short[] FeatureBiases;  //[Layer1Size];
    public short[] OutputWeights;  //[Layer1Size * 2 * OutputBuckets];
    public short[] OutputBiases;   //[OutputBuckets]
    public int[]   InputBucketMap;

    internal NNUENetwork(int layer1Size, int inputBuckets, int outputBuckets) {
        Layer1Size = layer1Size;
        InputBuckets = inputBuckets;
        OutputBuckets = outputBuckets;
        FeatureWeights = new short[InputBuckets * InputSize * Layer1Size];
        FeatureBiases = new short[Layer1Size];
        OutputWeights = new short[Layer1Size * 2 * OutputBuckets];
        OutputBiases = new short[OutputBuckets];
        InputBucketMap = InputBucketMappings[inputBuckets];
    }

    internal NNUENetwork(string filePath, int layer1Size, int inputBuckets, int outputBuckets) : this(Math.Max(16, layer1Size), inputBuckets, outputBuckets)
    {
        using (var stream = File.OpenRead(filePath))
        {
            using (var reader = new BinaryReader(stream, Encoding.UTF8, false))
            {
                reader.Read(FeatureWeights, InputBuckets * InputSize, layer1Size, Layer1Size);
                reader.Read(FeatureBiases, 1, layer1Size, Layer1Size);
                reader.Read(OutputWeights, 2 * OutputBuckets, layer1Size, Layer1Size);
                reader.Read(OutputBiases, OutputBuckets, 1, 1);
                //Padding: Console.WriteLine(reader.ReadChars((int)(stream.Length - stream.Position)));
            }
        }
    }

    internal int GetMaterialBucket(int pieceCount)
    {
        int DivCeil(int a, int b) => (a + b - 1) / b;
        int divisor = DivCeil(32, OutputBuckets);
        return (pieceCount - 2) / divisor;
    }
}

internal static class BinaryReaderExtensions {
    public static void Read(this BinaryReader reader, short[] target, int blockCount, int blockSize, int stride) {
        for (int i = 0; i < blockCount; i++)
        for (int j = 0; j < blockSize; j++)
            target[i * stride + j] = reader.ReadInt16();
    }
}