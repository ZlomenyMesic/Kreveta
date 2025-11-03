//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Text;
// ReSharper disable InconsistentNaming

namespace KrevetaTuning;

file class FancyRandom(Random rand) {
    internal int Next(int min, int max)
        => rand.Next(0, 11) > 3 
            ? 0 : rand.Next(min, max);
}

internal static class ParamGenerator {
    internal const int ParamCount = 25;
    
    internal static string CreateCMD() {
        var r  = new FancyRandom(new Random());
        var sb = new StringBuilder();
        
        sb.Append("tune");
        sb.Append($" {r.Next(-15, 16)}");
        sb.Append($" {r.Next(-12, 13)}");
        sb.Append($" {r.Next(-1, 2)}");
        sb.Append($" {r.Next(-1, 2)}");
        sb.Append($" {r.Next(-1, 2)}");

        sb.Append($" {r.Next(-1, 2)}");
        sb.Append($" {r.Next(-100, 101)}");
        sb.Append($" {r.Next(-10, 11)}");
        sb.Append($" {r.Next(-1, 2)}");
        sb.Append($" {r.Next(-1, 2)}");
        
        sb.Append($" {r.Next(-1, 2)}");
        sb.Append($" {r.Next(-15, 16)}");
        sb.Append($" {r.Next(-20, 21)}");
        sb.Append($" {r.Next(-1, 2)}");
        sb.Append($" {r.Next(-1, 2)}");
        
        sb.Append($" {r.Next(-15, 16)}");
        sb.Append($" {r.Next(-1, 2)}");
        sb.Append($" {r.Next(-1, 2)}");
        sb.Append($" {r.Next(-1, 2)}");
        sb.Append($" {r.Next(-1, 2)}");
        
        sb.Append($" {r.Next(-1, 2)}");
        sb.Append($" {r.Next(-1, 2)}");
        sb.Append($" {r.Next(-1, 2)}");
        sb.Append($" {r.Next(-1, 2)}");
        sb.Append($" {r.Next(-1, 2)}");
        
        return sb.ToString();
    }
}