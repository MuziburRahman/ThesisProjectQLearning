using MathNet.Numerics.Distributions;
using System;
using System.Linq;
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
        public static IEnumerable<int> GetDemandByGammaDist(double a, double b, Random rnd)
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


        public static IEnumerable<int> GetValueByNormalDist(double mn, double sd, Random rnd)
        {
            Normal nrm = new Normal(mn, sd, rnd);

            /// cumulative probabilities
            /// index + 1 = demand
            /// 
            var probabilities = new List<double>(10);
            double dmnd = 0;
            double cum = 0.0;

            while ((1 - cum) > 1e-4)
            {
                dmnd++;
                cum = nrm.CumulativeDistribution(dmnd);
                probabilities.Add(cum);
            }

            int n = probabilities.Count;

            while (true)
            {
                var r = rnd.NextDouble();

                for (int i = n - 1; i >= 0; i--)
                {
                    if (r > probabilities[i])
                    {
                        yield return i + 1;
                        break;
                    }
                }
                break;
            }
            yield break;
        }

        public static IEnumerable<int> GetNonUniformDiscreteValues(params (int, double)[] xs)
        {
            if (Math.Abs(xs.Sum(x => x.Item2) - 1) > 1e-3)
                throw new ArgumentException("probabilitiees must sum to 1.0");

            double[] cumul = new double[xs.Length];
            cumul[0] = xs[0].Item2;

            for (int i = 1; i < xs.Length; i++)
            {
                cumul[i] = cumul[i - 1] + xs[i].Item2;
            }

            Random rnd = new Random(DateTime.UtcNow.Millisecond);

            while (true)
            {
                var r = rnd.NextDouble();

                for (int i = cumul.Length - 1; i >= 0; i--)
                {
                    if (r > cumul[i])
                    {
                        yield return xs[i].Item1;
                        break;
                    }
                }
            }
        }
    }
}
