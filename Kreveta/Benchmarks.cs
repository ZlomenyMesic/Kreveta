//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// Remove unnecessary suppression
#pragma warning disable IDE0079

// Consider making public types internal
#pragma warning disable CA1515

// Mark members as static
#pragma warning disable CA1822

using Kreveta.consts;

using System;
using BenchmarkDotNet.Attributes;

namespace Kreveta;

//#if DEBUG

[MemoryDiagnoser]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class Benchmarks
{
    private ulong[][] pieces1 = [[23897324, 278273, 983284734, 87329487, 2378237, 878723],
                                 [3287934, 9238923787, 9383924, 873482934, 83748374, 62763763]];
    
    private ulong[][] pieces2 = [[23897324, 278273, 983284734, 87329487, 2378237, 878723],
        [3287934, 9238923787, 9383924, 873482934, 83748374, 62763763]];

    //[Conditional("DEBUG")]
    [GlobalSetup]
    public void Setup() {

    }
    
    [Benchmark]
    public void ArrayCopyInt()
    {
        Array.Copy(pieces1[0], pieces2[0], 6);
        Array.Copy(pieces1[1], pieces2[1], 6);
    }

    [Benchmark]
    public void ArrayCopyEnum()
    {
        Array.Copy(pieces1[(byte)Color.WHITE], pieces2[(byte)Color.WHITE], 6);
        Array.Copy(pieces1[(byte)Color.BLACK], pieces2[(byte)Color.BLACK], 6);
    }
}

//#endif

#pragma warning restore CA1822
#pragma warning restore CA1515

#pragma warning restore IDE0079