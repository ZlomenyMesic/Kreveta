//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Text;
// ReSharper disable InconsistentNaming

namespace KrevetaTuning;

internal static class ParamGenerator {
    internal const int ParamCount = 1;
    
    internal static string CreateCMD() {
        var rnd = new Random();
        int i   = rnd.Next(0, ParamCount);

        // define custom steps for different parameters
        int maxStep = i switch {
            /*0  => 25,
            1  => 16,
            6  => 150,
            7  => 12,
            11 => 18,
            12 => 25,
            15 => 18,
            25 => 40,
            26 => 30,
            27 => 15,
            28 => 50,*/
            
            _  => 100
        };
        
        // this can possibly create a neverending loop
        int shift = rnd.Next(-maxStep, maxStep + 1);
        while (shift == 0) {
            shift = rnd.Next(-maxStep, maxStep + 1);
        }
        
        var sb = new StringBuilder();
        sb.Append("tune");

        for (int j = 0; j < ParamCount; j++) {
            sb.Append(j == i 
                ? $" {shift}" : " 0"
            );
        }
        
        return sb.ToString();
    }
}