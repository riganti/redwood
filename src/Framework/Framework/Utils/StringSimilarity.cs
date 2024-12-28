using System;
using System.Collections.Generic;
using System.Linq;

namespace DotVVM.Framework.Utils
{
    internal static class StringSimilarity
    {
        /// <summary> Edit distance with deletion (Visble), insertion (Visivble), substitution (Visinle) and transposition (Visilbe) </summary>
        public static int DamerauLevenshteinDistance(string a, string b)
        {
            // https://en.wikipedia.org/wiki/Damerau%E2%80%93Levenshtein_distance

            if (a.Length * b.Length > 100_000)
                // avoid comparing long strings
                return a.Length + b.Length;

            var d = new int[a.Length + 1, b.Length + 1];

            for (var i = 0; i <= a.Length; i++)
                d[i, 0] = i;
                
            for (var j = 0; j <= b.Length; j++)
                d[0, j] = j;
            
            for (var i = 0; i < a.Length; i ++)
            {
                for (var j = 0; j < b.Length; j++)
                {
                    var substitutionCost = a[i] == b[j] ? 0 : 1;
                    var deletionCost = 1;
                    var insertionCost = 1;
                    d[i+1, j+1] = min(d[i, j+1] + deletionCost,
                                  d[i+1, j] + insertionCost,
                                  d[i, j] + substitutionCost);
                    if (i > 1 && j > 1 && a[i] == b[j-1] && a[i-1] == b[j])
                    {
                        var transpositionCost = 1;
                        d[i+1, j+1] = min(d[i+1, j+1],
                                      d[i-1, j-1] + transpositionCost);
                    }
                }
            }
            return d[a.Length, b.Length];
        }

        static int min(int a, int b) => Math.Min(a, b);
        static int min(int a, int b, int c) => min(min(a, b), c);


        public static (T? a, T? b)[] SequenceAlignment<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, Func<T, T, int> substituionCost, int gapCost = 10)
        {
            // common case: strings are almost equal
            //   -> skip same prefix and suffix since the rest of the algorithm is quadratic

            var prefix = new List<(T?, T?)>();
            for (var i = 0; i < min(a.Length, b.Length); i++)
            {
                if (substituionCost(a[i], b[i]) <= 0)
                    prefix.Add((a[i], b[i]));
                else
                    break;
            }
            a = a.Slice(prefix.Count);
            b = b.Slice(prefix.Count);
            // Console.WriteLine("Prefix length: " + prefix.Count);

            var suffix = new List<(T?, T?)>();

            for (var i = 1; i <= min(a.Length, b.Length); i++)
            {
                if (substituionCost(a[^i], b[^i]) <= 0)
                    suffix.Add((a[^i], b[^i]));
                else
                    break;
            }
            a = a.Slice(0, a.Length - suffix.Count);
            b = b.Slice(0, b.Length - suffix.Count);
            // Console.WriteLine("Suffix length: " + suffix.Count);

            var d = new int[a.Length + 1, b.Length + 1];
            var arrows = new sbyte[a.Length + 1, b.Length + 1];
            for (var i = 0; i <= a.Length; i++)
                d[i, 0] = i * gapCost;
                
            for (var j = 0; j <= b.Length; j++)
                d[0, j] = j * gapCost;
            
            for (var i = 0; i < a.Length; i ++)
            {
                for (var j = 0; j < b.Length; j++)
                {
                    var substitutionCost = substituionCost(a[i], b[j]);
                    var dist = d[i, j] + substitutionCost;
                    sbyte arrow = 0; // record which direction is optimal
                    if (dist > d[i, j+1] + gapCost)
                    {
                        dist = d[i, j+1] + gapCost;
                        arrow = -1;
                    }
                    if (dist > d[i+1, j] + gapCost)
                    {
                        dist = d[i+1, j] + gapCost;
                        arrow = 1;
                    }

                    d[i+1, j+1] = dist;
                    arrows[i+1, j+1] = arrow;
                }
            }

            // for (int i = 0; i <= a.Length; i++)
            // {
            //     Console.WriteLine("D: " + string.Join("\t", Enumerable.Range(0, b.Length + 1).Select(j => d[i, j])));
            //     Console.WriteLine("A: " + string.Join("\t", Enumerable.Range(0, b.Length + 1).Select(j => arrows[i, j] switch { 0 => "↖", 1 => "←", -1 => "↑" })));
            // }


            // follow arrows from the back
            for (int i = a.Length, j = b.Length; i > 0 || j > 0;)
            {
                // we are on border
                if (i == 0)
                {
                    j--;
                    suffix.Add((default, b[j]));
                }
                else if (j == 0)
                {
                    i--;
                    suffix.Add((a[i], default));
                }
                else if (arrows[i, j] == 0)
                {
                    i--;
                    j--;
                    suffix.Add((a[i], b[j]));
                }
                else if (arrows[i, j] == 1)
                {
                    j--;
                    suffix.Add((default, b[j]));
                }
                else if (arrows[i, j] == -1)
                {
                    i--;
                    suffix.Add((a[i], default));
                }
            }

            suffix.Reverse();
            return [..prefix, ..suffix];
        }
    }
}
