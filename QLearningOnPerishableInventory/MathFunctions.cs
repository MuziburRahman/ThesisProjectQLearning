using MathNet.Numerics.Distributions;
using System;
using System.Collections.Generic;

namespace QLearningOnPerishableInventory
{
    public static class MathFunctions
    {
        /// <summary>
        /// returns
        /// </summary>
        /// <param name="a">shape paeameter</param>
        /// <param name="b">scale parameter</param>
        /// <returns></returns>
        public static IEnumerable<int> GetDemand(double a, double b, Random rnd)
        {
            var gamma = new Gamma(a, 1 / b);

            /// cumulative probabilities
            /// index + 1 = demand
            /// 
            var probabilities = new List<double>(20); 
            double dmnd = 0;
            double cum = 0.0;

            while((1 - cum) > 1e-4)
            {
                dmnd++;
                cum = gamma.CumulativeDistribution(dmnd);
                probabilities.Add(cum);
            }

            int n = probabilities.Count;

            while (true)
            {
                var r = rnd.NextDouble();

                for (int i = n - 1; i >= 0; i--)
                {
                    if(r > probabilities[i])
                    {
                        yield return i + 1;
                        break;
                    }
                }
            }
        }
    }
}
