// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace PerformanceCalculatorGUI.Screens.Collections
{
    /// <summary>
    /// Tree-Parzen Estimator (TPE) for hyperparameter optimization.
    /// Based on the algorithm described in "Algorithms for Hyper-Parameter Optimization" (Bergstra et al., 2011).
    /// </summary>
    public class TreeParzenEstimator
    {
        private readonly int numParameters;
        private readonly double[] lowerBounds;
        private readonly double[] upperBounds;
        private readonly Random random;

        private readonly List<double[]> trialParams = new List<double[]>();
        private readonly List<double> trialLosses = new List<double>();

        // TPE hyperparameters
        private readonly double gamma;
        private readonly int nStartupTrials;
        private readonly int nEiCandidates;
        private readonly double priorWeight;

        /// <summary>
        /// Creates a new Tree-Parzen Estimator.
        /// </summary>
        public TreeParzenEstimator(double[] lowerBounds, double[] upperBounds,
                                   double gamma = 0.25, int nStartupTrials = 10, int nEiCandidates = 24, int seed = 42)
        {
            if (lowerBounds.Length != upperBounds.Length)
                throw new ArgumentException("Bounds arrays must have the same length.");

            numParameters = lowerBounds.Length;
            this.lowerBounds = (double[])lowerBounds.Clone();
            this.upperBounds = (double[])upperBounds.Clone();
            this.gamma = gamma;
            this.nStartupTrials = nStartupTrials;
            this.nEiCandidates = nEiCandidates;
            // Higher prior weight for high-dimensional spaces to maintain exploration
            this.priorWeight = 1.0 + Math.Log(1 + numParameters);
            this.random = new Random(seed);
        }

        public int TrialCount => trialParams.Count;

        public double[]? BestParams
        {
            get
            {
                if (trialLosses.Count == 0)
                    return null;

                int bestIdx = 0;
                double bestLoss = trialLosses[0];

                for (int i = 1; i < trialLosses.Count; i++)
                {
                    if (trialLosses[i] < bestLoss)
                    {
                        bestLoss = trialLosses[i];
                        bestIdx = i;
                    }
                }

                return (double[])trialParams[bestIdx].Clone();
            }
        }

        public double BestLoss => trialLosses.Count > 0 ? trialLosses.Min() : double.MaxValue;

        public double[] Suggest()
        {
            if (trialParams.Count < nStartupTrials)
                return sampleUniform();

            int nGood = Math.Max(1, (int)(trialParams.Count * gamma));
            var sortedIndices = trialLosses
                .Select((loss, idx) => (loss, idx))
                .OrderBy(x => x.loss)
                .Select(x => x.idx)
                .ToList();

            var goodIndices = sortedIndices.Take(nGood).ToList();
            var badIndices = sortedIndices.Skip(nGood).ToList();

            double[] bestCandidate = sampleUniform();
            double bestEi = double.NegativeInfinity;

            // Scale candidates with dimensionality
            int actualCandidates = nEiCandidates + numParameters;

            for (int c = 0; c < actualCandidates; c++)
            {
                double[] candidate = sampleFromKde(goodIndices);

                double logL = computeLogPdf(candidate, goodIndices);
                double logG = computeLogPdf(candidate, badIndices);
                double ei = logL - logG;

                if (ei > bestEi)
                {
                    bestEi = ei;
                    bestCandidate = candidate;
                }
            }

            return bestCandidate;
        }

        public void Report(double[] parameters, double loss)
        {
            if (parameters.Length != numParameters)
                throw new ArgumentException($"Expected {numParameters} parameters, got {parameters.Length}.");

            trialParams.Add((double[])parameters.Clone());
            trialLosses.Add(loss);
        }

        private double[] sampleUniform()
        {
            double[] sample = new double[numParameters];

            for (int i = 0; i < numParameters; i++)
            {
                sample[i] = lowerBounds[i] + random.NextDouble() * (upperBounds[i] - lowerBounds[i]);
            }

            return sample;
        }

        private double[] sampleFromKde(List<int> indices)
        {
            if (indices.Count == 0)
                return sampleUniform();

            double[] sample = new double[numParameters];

            for (int d = 0; d < numParameters; d++)
            {
                double range = upperBounds[d] - lowerBounds[d];
                double bandwidth = computeBandwidth(indices, d);

                double priorProb = priorWeight / (priorWeight + indices.Count);

                if (random.NextDouble() < priorProb)
                {
                    sample[d] = lowerBounds[d] + random.NextDouble() * range;
                }
                else
                {
                    int centerIdx = indices[random.Next(indices.Count)];
                    double center = trialParams[centerIdx][d];
                    sample[d] = sampleTruncatedNormal(center, bandwidth, lowerBounds[d], upperBounds[d]);
                }
            }

            return sample;
        }

        private double computeLogPdf(double[] point, List<int> indices)
        {
            if (indices.Count == 0)
            {
                double logUniform = 0;

                for (int d = 0; d < numParameters; d++)
                {
                    logUniform -= Math.Log(upperBounds[d] - lowerBounds[d]);
                }

                return logUniform;
            }

            double logPdf = 0;

            for (int d = 0; d < numParameters; d++)
            {
                double range = upperBounds[d] - lowerBounds[d];
                double bandwidth = computeBandwidth(indices, d);
                double uniformDensity = 1.0 / range;

                double density = priorWeight * uniformDensity;

                foreach (int idx in indices)
                {
                    double center = trialParams[idx][d];
                    double kernelDensity = truncatedNormalPdf(point[d], center, bandwidth, lowerBounds[d], upperBounds[d]);
                    density += kernelDensity;
                }

                density /= (priorWeight + indices.Count);
                logPdf += Math.Log(Math.Max(density, 1e-300));
            }

            return logPdf;
        }

        private double computeBandwidth(List<int> indices, int dimension)
        {
            double range = upperBounds[dimension] - lowerBounds[dimension];

            if (indices.Count < 2)
                return range * 0.3;

            double mean = 0;

            foreach (int idx in indices)
            {
                mean += trialParams[idx][dimension];
            }

            mean /= indices.Count;

            double variance = 0;

            foreach (int idx in indices)
            {
                double diff = trialParams[idx][dimension] - mean;
                variance += diff * diff;
            }

            variance /= indices.Count;
            double std = Math.Sqrt(variance);

            if (std < 1e-10)
                std = range * 0.1;

            // Silverman's rule with adjustment for high dimensions
            // bandwidth = std * (4 / (d+2))^(1/(d+4)) * n^(-1/(d+4))
            // Simplified: for high-d, use larger bandwidth to avoid over-fitting
            double dimFactor = Math.Pow(4.0 / (numParameters + 2), 1.0 / (numParameters + 4));
            double nFactor = Math.Pow(indices.Count, -1.0 / (numParameters + 4));
            double bandwidth = std * dimFactor * nFactor;

            // For very high dimensions, ensure minimum bandwidth for exploration
            double minBandwidth = range * 0.05;
            double maxBandwidth = range * 0.5;

            // Adaptive: wider bandwidth when fewer samples
            if (indices.Count < numParameters)
                minBandwidth = range * 0.15;

            bandwidth = Math.Clamp(bandwidth, minBandwidth, maxBandwidth);

            return bandwidth;
        }

        private double sampleTruncatedNormal(double mean, double std, double lower, double upper)
        {
            const int max_attempts = 100;

            for (int i = 0; i < max_attempts; i++)
            {
                double sample = mean + sampleStandardNormal() * std;

                if (sample >= lower && sample <= upper)
                    return sample;
            }

            return lower + random.NextDouble() * (upper - lower);
        }

        private double sampleStandardNormal()
        {
            double u1 = 1.0 - random.NextDouble();
            double u2 = 1.0 - random.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        private static double truncatedNormalPdf(double x, double mean, double std, double lower, double upper)
        {
            if (x < lower || x > upper)
                return 0;

            double z = (x - mean) / std;
            double phi = Math.Exp(-0.5 * z * z) / (std * Math.Sqrt(2 * Math.PI));

            double zLower = (lower - mean) / std;
            double zUpper = (upper - mean) / std;
            double normalization = normalCdf(zUpper) - normalCdf(zLower);

            if (normalization < 1e-10)
                return 1.0 / (upper - lower);

            return phi / normalization;
        }

        private static double normalCdf(double z)
        {
            return 0.5 * (1 + erf(z / Math.Sqrt(2)));
        }

        private static double erf(double x)
        {
            double sign = x < 0 ? -1 : 1;
            x = Math.Abs(x);

            const double a1 = 0.254829592;
            const double a2 = -0.284496736;
            const double a3 = 1.421413741;
            const double a4 = -1.453152027;
            const double a5 = 1.061405429;
            const double p = 0.3275911;

            double t = 1.0 / (1.0 + p * x);
            double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

            return sign * y;
        }
    }
}
