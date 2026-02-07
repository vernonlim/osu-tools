// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Catch.Difficulty;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Scoring;
using PerformanceCalculatorGUI.Configuration;

namespace PerformanceCalculatorGUI.Screens.Collections
{
    public class AutobalanceRunner
    {
        private const int max_iterations = 5000;
        private const int tpe_iterations = 4000;        // TPE is more sample-efficient
        private const int tpe_startup_trials = 750;     // Random exploration before TPE
        private const AutobalanceLossType loss_type = AutobalanceLossType.Mae;
        private const double initial_temperature = 5000.0;
        private const double cooling_rate = 0.999;
        private const double min_temperature = 0.001;
        private const double dataset_progress_portion = 0.05;
        private const double big_penalty = 1e12;
        private const double bound_lower_factor = 0.33;
        private const double bound_upper_factor = 3.0;

        private readonly ScoreCache scoreCache;
        private readonly RulesetStore rulesets;
        private readonly SettingsManager configManager;

        public AutobalanceRunner(ScoreCache scoreCache, RulesetStore rulesets, SettingsManager configManager)
        {
            this.scoreCache = scoreCache;
            this.rulesets = rulesets;
            this.configManager = configManager;
        }

        public static IReadOnlyList<AutobalanceParameter<OsuDifficultyConstants>> OsuParameters => osuParameters;
        public static IReadOnlyList<AutobalanceParameter<CatchDifficultyConstants>> CatchParameters => catchParameters;

        public static IReadOnlyList<IAutobalanceParameter> GetParameters(AutobalanceRuleset ruleset) =>
            ruleset == AutobalanceRuleset.Catch ? catchParametersForUi : osuParametersForUi;

        private static readonly AutobalanceParameter<OsuDifficultyConstants>[] osuParameters = createOsuAutobalanceParameters();
        private static readonly AutobalanceParameter<CatchDifficultyConstants>[] catchParameters = createCatchAutobalanceParameters();
        private static readonly IAutobalanceParameter[] osuParametersForUi = osuParameters;
        private static readonly IAutobalanceParameter[] catchParametersForUi = catchParameters;

        private static AutobalanceParameter<OsuDifficultyConstants>[] createOsuAutobalanceParameters() =>
            OsuDifficultyTuningParameters.All.Select(parameter => new AutobalanceParameter<OsuDifficultyConstants>(
                parameter.AutobalanceLabel,
                parameter.IsInteger,
                parameter.AutobalanceMinValue,
                parameter.DefaultEnabled,
                parameter.Getter,
                parameter.Setter,
                parameter.AutobalanceMaxValue)).ToArray();

        private static AutobalanceParameter<CatchDifficultyConstants>[] createCatchAutobalanceParameters() =>
            CatchDifficultyTuningParameters.All.Select(parameter => new AutobalanceParameter<CatchDifficultyConstants>(
                parameter.AutobalanceLabel,
                parameter.IsInteger,
                parameter.AutobalanceMinValue,
                parameter.DefaultEnabled,
                parameter.Getter,
                parameter.Setter,
                parameter.AutobalanceMaxValue)).ToArray();

        private sealed class ProgressReporter
        {
            private readonly Action<AutobalanceProgress>? callback;
            private double lastValue = -1;
            private string? lastStage;
            private long lastReportTicks;

            public ProgressReporter(Action<AutobalanceProgress>? callback)
            {
                this.callback = callback;
                lastReportTicks = Stopwatch.GetTimestamp();
            }

            public void Report(double value, string? stage = null, int? completed = null, int? total = null)
            {
                if (callback == null)
                    return;

                value = Math.Clamp(value, 0, 1);

                long now = Stopwatch.GetTimestamp();
                double msSinceLast = (now - lastReportTicks) * 1000.0 / Stopwatch.Frequency;

                bool stageChanged = stage != null && stage != lastStage;
                bool valueChanged = Math.Abs(value - lastValue) >= 0.0025;
                bool force = value >= 1 || stageChanged;

                if (!force && msSinceLast < 200 && !valueChanged)
                    return;

                lastReportTicks = now;
                lastValue = value;

                if (stage != null)
                    lastStage = stage;

                callback(new AutobalanceProgress(value, stage, completed, total));
            }
        }

        public Task<AutobalanceResult<OsuDifficultyConstants>> RunAsync(Collection collection, AutobalanceTarget target,
                                                                        AutobalanceParameter<OsuDifficultyConstants>[] selectedParameters,
                                                                        OsuDifficultyConstants baseTuning, Action<AutobalanceProgress>? progress = null)
        {
            return runAutobalanceAsync(collection, target, selectedParameters, baseTuning, "osu",
                tuning => new OsuRuleset(tuning), getOsuTargetValue,
                (tuning, scale) => tuning with { TotalPerformanceScale = tuning.TotalPerformanceScale * scale },
                progress);
        }

        public Task<AutobalanceResult<CatchDifficultyConstants>> RunCatchAsync(Collection collection, AutobalanceTarget target,
                                                                               AutobalanceParameter<CatchDifficultyConstants>[] selectedParameters,
                                                                               CatchDifficultyConstants baseTuning, Action<AutobalanceProgress>? progress = null)
        {
            if (target != AutobalanceTarget.Total)
                return Task.FromResult(AutobalanceResult<CatchDifficultyConstants>.Failure("Catch autobalance supports only Total target."));

            return runAutobalanceAsync(collection, target, selectedParameters, baseTuning, "fruits",
                tuning => new CatchRuleset(tuning), getCatchTargetValue,
                (tuning, scale) => tuning with { FinalPPMultiplier = tuning.FinalPPMultiplier * scale },
                progress);
        }

        private Task<AutobalanceResult<TTuning>> runAutobalanceAsync<TTuning>(Collection collection, AutobalanceTarget target,
                                                                              AutobalanceParameter<TTuning>[] selectedParameters,
                                                                              TTuning baseTuning, string rulesetShortName,
                                                                              Func<TTuning, Ruleset> createRuleset,
                                                                              Func<PerformanceAttributes?, AutobalanceTarget, double?> getTargetValue,
                                                                              Func<TTuning, double, TTuning> applyScaleMultiplier,
                                                                              Action<AutobalanceProgress>? progress = null)
        {
            return Task.Run(async () =>
            {
                var reporter = new ProgressReporter(progress);
                reporter.Report(0, stage: "Preparing...");

                var dataset = await buildAutobalanceDataset(collection, target, reporter, rulesetShortName).ConfigureAwait(false);
                if (dataset.Count == 0)
                {
                    reporter.Report(1, stage: "Failed");
                    return AutobalanceResult<TTuning>.Failure($"No expected values found for {getTargetLabel(target)}.");
                }

                selectedParameters = selectedParameters.Where(p => !p.IsInteger).ToArray();

                if (selectedParameters.Length == 0)
                {
                    reporter.Report(dataset_progress_portion, stage: "Evaluating...");
                    var (baseMse, baseScale) = evaluateAutobalance(dataset, selectedParameters, baseTuning, target, Array.Empty<double>(), createRuleset, getTargetValue);
                    var scaledBaseTuning = applyScaleMultiplier(baseTuning, baseScale);
                    reporter.Report(1, stage: "Done");
                    return AutobalanceResult<TTuning>.Success(scaledBaseTuning, Math.Sqrt(baseMse), dataset.Count);
                }

                int n = selectedParameters.Length;
                double[] currentValues = new double[n];
                double[] bestValues = new double[n];
                double[] lowerBounds = new double[n];
                double[] upperBounds = new double[n];

                for (int i = 0; i < n; i++)
                {
                    double baseVal = selectedParameters[i].Getter(baseTuning);
                    if (double.IsNaN(baseVal) || double.IsInfinity(baseVal))
                        baseVal = 1.0;

                    double lo, hi;

                    if (selectedParameters[i].MaxValue is { } maxVal)
                    {
                        lo = selectedParameters[i].MinValue;
                        hi = maxVal;
                    }
                    else
                    {
                        lo = Math.Max(selectedParameters[i].MinValue, baseVal * bound_lower_factor);
                        hi = Math.Max(baseVal * bound_upper_factor, selectedParameters[i].MinValue * bound_upper_factor);
                    }

                    if (double.IsNaN(lo) || double.IsInfinity(lo))
                        lo = selectedParameters[i].MinValue;
                    if (double.IsNaN(hi) || double.IsInfinity(hi) || hi <= lo)
                        hi = lo + Math.Max(1e-6, Math.Abs(lo) * 0.1);

                    currentValues[i] = Math.Clamp(baseVal, lo, hi);
                    lowerBounds[i] = lo;
                    upperBounds[i] = hi;
                }

                Array.Copy(currentValues, bestValues, n);

                reporter.Report(dataset_progress_portion, stage: "Optimizing...");

                // Use TPE for optimization
                var tpe = new TreeParzenEstimator(lowerBounds, upperBounds,
                    gamma: 0.15,                       // Smaller gamma = more selective "good" set
                    nStartupTrials: Math.Max(2 * n, tpe_startup_trials),  // At least 2x parameters
                    nEiCandidates: 48,                 // More candidates for high-dim
                    seed: 42);

                double bestMse = double.MaxValue;
                double bestScale = 1.0;

                for (int iteration = 0; iteration < tpe_iterations; iteration++)
                {
                    double[] candidateValues = tpe.Suggest();

                    var (candidateMse, candidateScale) = evaluateAutobalance(dataset, selectedParameters, baseTuning, target,
                        candidateValues, createRuleset, getTargetValue, iteration);

                    tpe.Report(candidateValues, candidateMse);

                    if (candidateMse < bestMse)
                    {
                        bestMse = candidateMse;
                        bestScale = candidateScale;
                        Array.Copy(candidateValues, bestValues, n);
                    }

                    double opt = (double)(iteration + 1) / tpe_iterations;
                    double combined = dataset_progress_portion + (1.0 - dataset_progress_portion) * opt;
                    reporter.Report(combined);
                }

                var balancedTuning = applyAutobalanceParameters(baseTuning, selectedParameters, bestValues);
                balancedTuning = applyScaleMultiplier(balancedTuning, bestScale);
                double rmse = Math.Sqrt(bestMse);

                reporter.Report(1, stage: "Done");
                return AutobalanceResult<TTuning>.Success(balancedTuning, rmse, dataset.Count);
            });
        }

        private async Task<List<AutobalanceScoreData>> buildAutobalanceDataset(Collection collection, AutobalanceTarget target,
                                                                               ProgressReporter reporter, string rulesetShortName)
        {
            var dataset = new List<AutobalanceScoreData>();
            var expectedPerformance = collection.ExpectedPerformance;

            if (expectedPerformance == null || expectedPerformance.Count == 0)
            {
                reporter.Report(dataset_progress_portion, stage: "Loading scores", completed: 0, total: 0);
                return dataset;
            }

            int totalScores = collection.Scores.Length + (collection.StoredScores?.Length ?? 0);
            int processed = 0;

            reporter.Report(0, stage: "Loading scores", completed: 0, total: totalScores);

            // Process online scores
            foreach (long scoreId in collection.Scores)
            {
                processed++;

                string key = scoreId.ToString();

                if (!expectedPerformance.TryGetValue(key, out var expectedValues) || !TryGetExpectedValue(expectedValues, target, out double expectedValue))
                {
                    reporter.Report(dataset_progress_portion * processed / totalScores, stage: "Loading scores", completed: processed, total: totalScores);
                    continue;
                }

                SoloScoreInfo? score;

                try
                {
                    score = await scoreCache.GetScore(scoreId).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Logger.Log(e.ToString(), level: LogLevel.Error);
                    reporter.Report(dataset_progress_portion * processed / totalScores, stage: "Loading scores", completed: processed, total: totalScores);
                    continue;
                }

                if (score == null)
                {
                    reporter.Report(dataset_progress_portion * processed / totalScores, stage: "Loading scores", completed: processed, total: totalScores);
                    continue;
                }

                var rulesetInfo = rulesets.GetRuleset(score.RulesetID);
                if (rulesetInfo?.ShortName != rulesetShortName)
                {
                    reporter.Report(dataset_progress_portion * processed / totalScores, stage: "Loading scores", completed: processed, total: totalScores);
                    continue;
                }

                try
                {
                    var working = ProcessorWorkingBeatmap.FromFileOrId(score.BeatmapID.ToString(),
                        cachePath: configManager.GetBindable<string>(Settings.CachePath).Value);
                    var rulesetInstance = rulesetInfo.CreateInstance();
                    var mods = score.Mods.Select(x => x.ToMod(rulesetInstance)).ToArray();
                    var scoreInfo = score.ToScoreInfo(rulesets, working.BeatmapInfo);
                    var parsedScore = new ProcessorScoreDecoder(working).Parse(scoreInfo);

                    dataset.Add(new AutobalanceScoreData(working, mods, parsedScore.ScoreInfo, expectedValue));
                }
                catch (Exception e)
                {
                    Logger.Log(e.ToString(), level: LogLevel.Error);
                }

                reporter.Report(dataset_progress_portion * processed / totalScores, stage: "Loading scores", completed: processed, total: totalScores);
            }

            // Process stored scores
            if (collection.StoredScores != null)
            {
                foreach (var storedScore in collection.StoredScores)
                {
                    processed++;

                    Logger.Log(storedScore.Weighting.ToString(), level: LogLevel.Debug);

                    if (!expectedPerformance.TryGetValue(storedScore.Id, out var expectedValues) || !TryGetExpectedValue(expectedValues, target, out double expectedValue))
                    {
                        reporter.Report(dataset_progress_portion * processed / totalScores, stage: "Loading scores", completed: processed, total: totalScores);
                        continue;
                    }

                    var rulesetInfo = rulesets.GetRuleset(storedScore.RulesetID);
                    if (rulesetInfo?.ShortName != rulesetShortName)
                    {
                        reporter.Report(dataset_progress_portion * processed / totalScores, stage: "Loading scores", completed: processed, total: totalScores);
                        continue;
                    }

                    try
                    {
                        var working = ProcessorWorkingBeatmap.FromFileOrId(storedScore.BeatmapID.ToString(),
                            cachePath: configManager.GetBindable<string>(Settings.CachePath).Value);
                        var rulesetInstance = rulesetInfo.CreateInstance();
                        var soloScore = storedScore.ToSoloScoreInfo(working);
                        var mods = soloScore.Mods.Select(x => x.ToMod(rulesetInstance)).ToArray();
                        var scoreInfo = soloScore.ToScoreInfo(rulesets, working.BeatmapInfo);
                        var parsedScore = new ProcessorScoreDecoder(working).Parse(scoreInfo);

                        dataset.Add(new AutobalanceScoreData(working, mods, parsedScore.ScoreInfo, expectedValue, storedScore.Weighting));
                    }
                    catch (Exception e)
                    {
                        Logger.Log(e.ToString(), level: LogLevel.Error);
                    }

                    reporter.Report(dataset_progress_portion * processed / totalScores, stage: "Loading scores", completed: processed, total: totalScores);
                }
            }

            reporter.Report(dataset_progress_portion, stage: $"Dataset ready ({dataset.Count} scores)");
            return dataset;
        }

        /// <summary>
        /// Evaluates the autobalance objective with optimal linear scaling.
        /// Returns (loss, optimalScale) where optimalScale minimizes the loss.
        /// For MSE: s = sum(w * a * e) / sum(w * a^2)
        /// For MAE: s = weighted median of (e / a)
        /// For Spearman: scale-invariant, uses MSE-optimal scale for final multiplier
        /// </summary>
        private (double loss, double scale) evaluateAutobalance<TTuning>(IReadOnlyList<AutobalanceScoreData> dataset, AutobalanceParameter<TTuning>[] parameters,
                                                                         TTuning baseTuning, AutobalanceTarget target, double[] values,
                                                                         Func<TTuning, Ruleset> createRuleset,
                                                                         Func<PerformanceAttributes?, AutobalanceTarget, double?> getTargetValue,
                                                                         int it = -1)
        {
            try
            {
                var tuning = applyAutobalanceParameters(baseTuning, parameters, values);
                var ruleset = createRuleset(tuning);
                var performanceCalculator = ruleset.CreatePerformanceCalculator();

                if (performanceCalculator == null)
                    return (big_penalty, 1.0);

                var results = new System.Collections.Concurrent.ConcurrentBag<(double actual, double expected, double weight)>();

                Parallel.ForEach(dataset, entry =>
                {
                    var difficultyCalculator = ruleset.CreateDifficultyCalculator(entry.Working);
                    var difficultyAttributes = difficultyCalculator.Calculate(entry.Mods);
                    var performanceAttributes = performanceCalculator.Calculate(entry.ScoreInfo, difficultyAttributes);
                    double? actual = getTargetValue(performanceAttributes, target);

                    if (actual == null)
                        return;

                    results.Add((actual.Value, entry.ExpectedValue, entry.Weighting));
                });

                var resultList = results.ToList();
                int count = (int)resultList.Sum(r => r.weight);

                if (count == 0)
                    return (big_penalty, 1.0);

                // Always compute MSE-optimal scale (used for Spearman and as fallback)
                double sumWeightedActualExpected = 0;
                double sumWeightedActualSquared = 0;

                foreach (var (actual, expected, weight) in resultList)
                {
                    sumWeightedActualExpected += weight * actual * expected;
                    sumWeightedActualSquared += weight * actual * actual;
                }

                double mseOptimalScale = sumWeightedActualSquared > 1e-12
                    ? sumWeightedActualExpected / sumWeightedActualSquared
                    : 1.0;

                double optimalScale;
                double loss;

                switch (loss_type)
                {
                    case AutobalanceLossType.Mae:
                        optimalScale = computeWeightedMedianScale(resultList);
                        optimalScale = Math.Clamp(optimalScale, 0.01, 20.0);
                        loss = computeMae(resultList, optimalScale, count);
                        double rmseForMae = Math.Sqrt(computeMse(resultList, optimalScale, count));
                        Console.WriteLine($"MAE at it={it}, count={count}, scale={optimalScale:F4}: {loss:F2}, RMSE: {rmseForMae:F2}");
                        break;

                    case AutobalanceLossType.Spearman:
                        // Spearman is scale-invariant, use MSE-optimal scale for final multiplier
                        optimalScale = Math.Clamp(mseOptimalScale, 0.01, 20.0);
                        loss = computeSpearmanLoss(resultList);
                        double maeForSpearman = computeMae(resultList, optimalScale, count);
                        double rmseForSpearman = Math.Sqrt(computeMse(resultList, optimalScale, count));
                        Console.WriteLine($"Spearman loss at it={it}, count={count}, scale={optimalScale:F4}: {loss:F6}, MAE: {maeForSpearman:F2}, RMSE: {rmseForSpearman:F2}");
                        break;

                    case AutobalanceLossType.Rmse:
                    default:
                        optimalScale = Math.Clamp(mseOptimalScale, 0.01, 20.0);
                        loss = computeMse(resultList, optimalScale, count);
                        double maeForRmse = computeMae(resultList, optimalScale, count);
                        Console.WriteLine($"RMSE at it={it}, count={count}, scale={optimalScale:F4}: {Math.Sqrt(loss):F2}, MAE: {maeForRmse:F2}");
                        break;
                }

                return (loss, optimalScale);
            }
            catch
            {
                return (big_penalty, 1.0);
            }
        }

        private static double computeMse(List<(double actual, double expected, double weight)> results, double scale, int count)
        {
            double errorSum = 0;

            foreach (var (actual, expected, weight) in results)
            {
                double diff = actual * scale - expected;
                errorSum += diff * diff * weight;
            }

            return errorSum / count;
        }

        private static double computeMae(List<(double actual, double expected, double weight)> results, double scale, int count)
        {
            double errorSum = 0;

            foreach (var (actual, expected, weight) in results)
            {
                double diff = Math.Abs(actual * scale - expected);
                errorSum += diff * weight;
            }

            return errorSum / count;
        }

        /// <summary>
        /// Computes Spearman rank correlation loss (1 - rho), where rho is the Spearman correlation.
        /// Lower is better (0 = perfect correlation, 2 = perfect negative correlation).
        /// </summary>
        private static double computeSpearmanLoss(List<(double actual, double expected, double weight)> results)
        {
            if (results.Count < 2)
                return big_penalty;

            // Expand weighted samples for ranking (approximate for non-integer weights)
            var actualRanks = computeRanks(results.Select(r => r.actual).ToArray());
            var expectedRanks = computeRanks(results.Select(r => r.expected).ToArray());

            // Compute weighted Spearman correlation
            double totalWeight = results.Sum(r => r.weight);
            double meanActualRank = 0, meanExpectedRank = 0;

            for (int i = 0; i < results.Count; i++)
            {
                meanActualRank += results[i].weight * actualRanks[i];
                meanExpectedRank += results[i].weight * expectedRanks[i];
            }

            meanActualRank /= totalWeight;
            meanExpectedRank /= totalWeight;

            double covariance = 0, varActual = 0, varExpected = 0;

            for (int i = 0; i < results.Count; i++)
            {
                double w = results[i].weight;
                double dActual = actualRanks[i] - meanActualRank;
                double dExpected = expectedRanks[i] - meanExpectedRank;

                covariance += w * dActual * dExpected;
                varActual += w * dActual * dActual;
                varExpected += w * dExpected * dExpected;
            }

            if (varActual < 1e-12 || varExpected < 1e-12)
                return 1.0; // No variance = undefined correlation, treat as neutral

            double rho = covariance / Math.Sqrt(varActual * varExpected);

            // Return loss: 1 - rho (so 0 is perfect, higher is worse)
            return 1.0 - rho;
        }

        private static double[] computeRanks(double[] values)
        {
            int n = values.Length;
            var indexed = values.Select((v, i) => (value: v, index: i)).OrderBy(x => x.value).ToArray();
            var ranks = new double[n];

            int i = 0;

            while (i < n)
            {
                int j = i;

                // Find all tied values
                while (j < n && Math.Abs(indexed[j].value - indexed[i].value) < 1e-12)
                    j++;

                // Assign average rank to all tied values
                double avgRank = (i + j + 1) / 2.0; // 1-based average rank

                for (int k = i; k < j; k++)
                    ranks[indexed[k].index] = avgRank;

                i = j;
            }

            return ranks;
        }

        /// <summary>
        /// Computes the weighted median of (expected / actual) ratios.
        /// This is the optimal scale for minimizing weighted MAE.
        /// </summary>
        private static double computeWeightedMedianScale(List<(double actual, double expected, double weight)> results)
        {
            // Filter out zero/near-zero actuals to avoid division issues
            var ratios = results
                .Where(r => Math.Abs(r.actual) > 1e-12)
                .Select(r => (ratio: r.expected / r.actual, weight: r.weight))
                .OrderBy(r => r.ratio)
                .ToList();

            if (ratios.Count == 0)
                return 1.0;

            double totalWeight = ratios.Sum(r => r.weight);
            double halfWeight = totalWeight / 2.0;
            double cumWeight = 0;

            foreach (var (ratio, weight) in ratios)
            {
                cumWeight += weight;

                if (cumWeight >= halfWeight)
                    return ratio;
            }

            return ratios[^1].ratio;
        }

        private static TTuning applyAutobalanceParameters<TTuning>(TTuning baseTuning, AutobalanceParameter<TTuning>[] parameters, double[] values)
        {
            var tuning = baseTuning;

            for (int i = 0; i < parameters.Length; i++)
            {
                tuning = parameters[i].Apply(tuning, values[i]);
            }

            return tuning;
        }

        internal static bool TryGetExpectedValue(ExpectedPerformanceValues expectedValues, AutobalanceTarget target, out double expectedValue)
        {
            if (target == AutobalanceTarget.Total)
            {
                if (expectedValues.Total.HasValue)
                {
                    expectedValue = expectedValues.Total.Value;
                    return true;
                }

                if (expectedValues.Skills.TryGetValue("pp", out expectedValue))
                    return true;

                if (expectedValues.Skills.TryGetValue("total", out expectedValue))
                    return true;

                return false;
            }

            string key = getTargetKey(target);
            return expectedValues.Skills.TryGetValue(key, out expectedValue);
        }

        private static double? getOsuTargetValue(PerformanceAttributes? attributes, AutobalanceTarget target)
        {
            if (attributes == null)
                return null;

            return target switch
            {
                AutobalanceTarget.Total => attributes.Total,
                AutobalanceTarget.Aim => (attributes as OsuPerformanceAttributes)?.Aim,
                AutobalanceTarget.Speed => (attributes as OsuPerformanceAttributes)?.Speed,
                AutobalanceTarget.Accuracy => (attributes as OsuPerformanceAttributes)?.Accuracy,
                AutobalanceTarget.Reading => (attributes as OsuPerformanceAttributes)?.Reading,
                AutobalanceTarget.Flashlight => (attributes as OsuPerformanceAttributes)?.Flashlight,
                _ => null
            };
        }

        private static double? getCatchTargetValue(PerformanceAttributes? attributes, AutobalanceTarget target)
        {
            if (attributes == null || target != AutobalanceTarget.Total)
                return null;

            return attributes.Total;
        }

        private static string getTargetKey(AutobalanceTarget target)
        {
            return target switch
            {
                AutobalanceTarget.Total => "total",
                AutobalanceTarget.Aim => "aim",
                AutobalanceTarget.Speed => "speed",
                AutobalanceTarget.Accuracy => "accuracy",
                AutobalanceTarget.Reading => "reading",
                AutobalanceTarget.Flashlight => "flashlight",
                _ => "total"
            };
        }

        private static string getTargetLabel(AutobalanceTarget target)
        {
            return target switch
            {
                AutobalanceTarget.Total => "total",
                AutobalanceTarget.Aim => "aim",
                AutobalanceTarget.Speed => "speed",
                AutobalanceTarget.Accuracy => "accuracy",
                AutobalanceTarget.Reading => "reading",
                AutobalanceTarget.Flashlight => "flashlight",
                _ => "total"
            };
        }
    }

    public enum AutobalanceTarget
    {
        [Description("Total")]
        Total,

        [Description("Aim")]
        Aim,

        [Description("Speed")]
        Speed,

        [Description("Accuracy")]
        Accuracy,

        [Description("Reading")]
        Reading,

        [Description("Flashlight")]
        Flashlight
    }

    public enum AutobalanceRuleset
    {
        [Description("osu!")]
        Osu,

        [Description("catch")]
        Catch
    }

    public enum AutobalanceLossType
    {
        [Description("RMSE")]
        Rmse,

        [Description("MAE")]
        Mae,

        [Description("Spearman")]
        Spearman
    }

    public interface IAutobalanceParameter
    {
        public string Label { get; }
        public double MinValue { get; }
        public bool IsInteger { get; }
        public bool DefaultEnabled { get; }
    }

    public sealed class AutobalanceParameter<TTuning> : IAutobalanceParameter
    {
        public string Label { get; }
        public Func<TTuning, double> Getter { get; }
        public Func<TTuning, double, TTuning> Setter { get; }
        public double MinValue { get; }
        public double? MaxValue { get; }
        public bool IsInteger { get; }
        public bool DefaultEnabled { get; }

        public AutobalanceParameter(string label, bool isInteger, double minValue, bool defaultEnabled,
                                    Func<TTuning, double> getter, Func<TTuning, double, TTuning> setter, double? maxValue = null)
        {
            Label = label;
            IsInteger = isInteger;
            MinValue = minValue;
            MaxValue = maxValue;
            DefaultEnabled = defaultEnabled;
            Getter = getter;
            Setter = setter;
        }

        public TTuning Apply(TTuning tuning, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return tuning;

            if (IsInteger)
            {
                int intValue = (int)Math.Round(value);
                int minValue = (int)MinValue;
                int maxValue = MaxValue.HasValue ? (int)Math.Round(MaxValue.Value) : int.MaxValue;
                if (maxValue < minValue)
                    maxValue = minValue;
                intValue = Math.Clamp(intValue, minValue, maxValue);
                return Setter(tuning, intValue);
            }

            double clamped = Math.Max(value, MinValue);
            if (MaxValue.HasValue)
                clamped = Math.Min(clamped, MaxValue.Value);
            return Setter(tuning, clamped);
        }
    }

    public sealed class AutobalanceScoreData
    {
        public ProcessorWorkingBeatmap Working { get; }
        public Mod[] Mods { get; }
        public ScoreInfo ScoreInfo { get; }
        public double ExpectedValue { get; }
        public double Weighting { get; }

        public AutobalanceScoreData(ProcessorWorkingBeatmap working, Mod[] mods, ScoreInfo scoreInfo, double expectedValue, double weighting = 1)
        {
            Working = working;
            Mods = mods;
            ScoreInfo = scoreInfo;
            ExpectedValue = expectedValue;
            Weighting = weighting;
        }
    }

    public readonly struct AutobalanceProgress
    {
        public double Value { get; }
        public string? Stage { get; }
        public int? Completed { get; }
        public int? Total { get; }

        public AutobalanceProgress(double value, string? stage = null, int? completed = null, int? total = null)
        {
            Value = value;
            Stage = stage;
            Completed = completed;
            Total = total;
        }
    }

    public readonly struct AutobalanceResult<TTuning>
    {
        public bool IsFailure { get; }
        public TTuning? Tuning { get; }
        public double Rmse { get; }
        public int SampleCount { get; }
        public string? ErrorMessage { get; }

        private AutobalanceResult(TTuning tuning, double rmse, int sampleCount)
        {
            IsFailure = false;
            Tuning = tuning;
            Rmse = rmse;
            SampleCount = sampleCount;
            ErrorMessage = null;
        }

        private AutobalanceResult(string errorMessage)
        {
            IsFailure = true;
            Tuning = default;
            Rmse = 0;
            SampleCount = 0;
            ErrorMessage = errorMessage;
        }

        public static AutobalanceResult<TTuning> Success(TTuning tuning, double rmse, int sampleCount) => new AutobalanceResult<TTuning>(tuning, rmse, sampleCount);
        public static AutobalanceResult<TTuning> Failure(string errorMessage) => new AutobalanceResult<TTuning>(errorMessage);
    }
}
