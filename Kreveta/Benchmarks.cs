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

using System;
using System.Numerics;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Kreveta;

//#if DEBUG

[MemoryDiagnoser]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class Benchmarks {

    //[Conditional("DEBUG")]
    [GlobalSetup]
    public void Setup() {

    }
    
    [Benchmark]
    public void Factorial_NewIdea() {
        BigInteger factorial = Experimental.Factorial_NewIdea(3000);
    }

    [Benchmark]
    public void Factorial_KryKom() {
        BigInteger factorial = Experimental.Factorial_KryKom(3000);
    }
}

internal class Experimental {

    internal static BigInteger Factorial_KryKom(int n) {
        if (n is 0 or 1) return 1;
        if (n < 0) throw new InvalidOperationException();

        BigInteger product = 1;
        Parallel.For(1, n + 1, i => product *= i);
        return product;
    }
    
    internal static BigInteger Factorial_NewIdea(int n) {
        if ((n & 0x7FFFFFFE) == 0) {
            if ((n & 0x80000000) == 0x80000000)
                throw new InvalidOperationException();

            return 1;
        }

        BigInteger product1 = 1;
        BigInteger product2 = 1;

        Parallel.For(2, n >> 1,     i => product1 *= i);
        Parallel.For(n >> 1, n + 1, i => product2 *= i);
        
        return product1 * product2;
    }
}

//#endif

#pragma warning restore CA1822
#pragma warning restore CA1515

#pragma warning restore IDE0079