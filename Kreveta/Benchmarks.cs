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
using System.Numerics;
using BenchmarkDotNet.Attributes;

namespace Kreveta;

//#if DEBUG

[MemoryDiagnoser]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class Benchmarks
{
    private ulong some_bb = 0xFFFFFFFF00000000;
    private byte smth = 0;

    //[Conditional("DEBUG")]
    [GlobalSetup]
    public void Setup() {

    }
    
    [Benchmark]
    public void BitBoard()
    {
        //smth = BB.Popcount(some_bb);
    }

    [Benchmark]
    public void BuiltIn()
    {
        smth = (byte)ulong.PopCount(some_bb);
    }
}

//#endif

#pragma warning restore CA1822
#pragma warning restore CA1515

#pragma warning restore IDE0079