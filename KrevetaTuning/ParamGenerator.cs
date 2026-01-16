//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Text;
// ReSharper disable InconsistentNaming

namespace KrevetaTuning;

internal static class ParamGenerator {
    internal const int ParamCount = 2;
    
    internal static string CreateCMD(int paramCount) {
        int[] shifts = new int[ParamCount];
        var   rnd    = new Random();

        for (int j = 0; j < paramCount; j++) {
            // choose, which parameter will be shifted
            int i = rnd.Next(0, ParamCount);

            // define custom steps for different parameters
            int maxStep = i switch {
                0 => 20,
                1 => 5
            };
        
            // this can possibly create a neverending loop
            int shift = 0;
            while (shift == 0) {
                shift = rnd.Next(-maxStep, maxStep + 1);
            }

            shifts[i] = shift;
        }
        
        var sb = new StringBuilder();
        sb.Append("tune");

        for (int i = 0; i < ParamCount; i++) {
            sb.Append($" {shifts[i]}");
        }
        
        return sb.ToString();
    }
}