// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using Humanizer;
using osu.Game.Rulesets.Catch.Difficulty;

namespace PerformanceCalculatorGUI.Configuration
{
    public static class CatchDifficultyTuningParameters
    {
        public static readonly IReadOnlyList<DifficultyTuningParameter<CatchDifficultyConstants>> All = new[]
        {
            doubleParam(nameof(CatchDifficultyConstants.DifficultyMultiplier), t => t.DifficultyMultiplier, (t, v) => t with { DifficultyMultiplier = v },
                defaultEnabled: true, minValue: 0.010, maxValue: 0.020),
            doubleParam(nameof(CatchDifficultyConstants.ApproachRateSecondConstant), t => t.ApproachRateSecondConstant, (t, v) => t with { ApproachRateSecondConstant = v },
                minValue: 0.30, maxValue: 0.40),
            doubleParam(nameof(CatchDifficultyConstants.BeginningTimePenaltyPower), t => t.BeginningTimePenaltyPower, (t, v) => t with { BeginningTimePenaltyPower = v },
                minValue: 0.20, maxValue: 0.50),
            doubleParam(nameof(CatchDifficultyConstants.BeginningFullPenalty), t => t.BeginningFullPenalty, (t, v) => t with { BeginningFullPenalty = v },
                minValue: 0.40, maxValue: 0.70),
            doubleParam(nameof(CatchDifficultyConstants.SrScalerY1), t => t.SrScalerY1, (t, v) => t with { SrScalerY1 = v }, minValue: 4.25, maxValue: 4.85),
            doubleParam(nameof(CatchDifficultyConstants.SrScalerY2), t => t.SrScalerY2, (t, v) => t with { SrScalerY2 = v }, minValue: 6.6, maxValue: 7.2),
            doubleParam(nameof(CatchDifficultyConstants.SrScalerY3), t => t.SrScalerY3, (t, v) => t with { SrScalerY3 = v }, minValue: 8.4, maxValue: 9.0),
            doubleParam(nameof(CatchDifficultyConstants.SrScalerY4), t => t.SrScalerY4, (t, v) => t with { SrScalerY4 = v }, minValue: 9.1, maxValue: 9.7),
            doubleParam(nameof(CatchDifficultyConstants.SrScalerY5), t => t.SrScalerY5, (t, v) => t with { SrScalerY5 = v }, minValue: 9.9, maxValue: 10.5),
            doubleParam(nameof(CatchDifficultyConstants.SrScalerY6), t => t.SrScalerY6, (t, v) => t with { SrScalerY6 = v }, minValue: 10.7, maxValue: 11.3),
            doubleParam(nameof(CatchDifficultyConstants.DefaultDecayWeight), t => t.DefaultDecayWeight, (t, v) => t with { DefaultDecayWeight = v },
                minValue: 0.870, maxValue: 0.930),
            doubleParam(nameof(CatchDifficultyConstants.LocalStarRatingMaxConstant), t => t.LocalStarRatingMaxConstant, (t, v) => t with { LocalStarRatingMaxConstant = v },
                minValue: 0.80, maxValue: 1.30),
            doubleParam(nameof(CatchDifficultyConstants.LocalStarRatingMinConstant), t => t.LocalStarRatingMinConstant, (t, v) => t with { LocalStarRatingMinConstant = v },
                minValue: 0.60, maxValue: 1.20),
            doubleParam(nameof(CatchDifficultyConstants.LocalStarRatingCorrelationConstant), t => t.LocalStarRatingCorrelationConstant, (t, v) => t with { LocalStarRatingCorrelationConstant = v },
                minValue: 0.05, maxValue: 0.30),
            doubleParam(nameof(CatchDifficultyConstants.PerformanceLengthLinearPace), t => t.PerformanceLengthLinearPace, (t, v) => t with { PerformanceLengthLinearPace = v },
                minValue: 0.25, maxValue: 0.45),
            intParam(nameof(CatchDifficultyConstants.PerformanceLengthCutoff), t => t.PerformanceLengthCutoff, (t, v) => t with { PerformanceLengthCutoff = v },
                minValue: 1500, maxValue: 1900),
            doubleParam(nameof(CatchDifficultyConstants.PerformanceLengthLogarithmicPace), t => t.PerformanceLengthLogarithmicPace, (t, v) => t with { PerformanceLengthLogarithmicPace = v },
                minValue: 0.15, maxValue: 0.35),
            doubleParam(nameof(CatchDifficultyConstants.PerformanceValueMultiplier), t => t.PerformanceValueMultiplier, (t, v) => t with { PerformanceValueMultiplier = v },
                defaultEnabled: true, minValue: 1.040, maxValue: 1.100),
            doubleParam(nameof(CatchDifficultyConstants.PrecisionRawWeightHyperjumps), t => t.PrecisionRawWeightHyperjumps, (t, v) => t with { PrecisionRawWeightHyperjumps = v },
                minValue: 0.90, maxValue: 1.00),
            doubleParam(nameof(CatchDifficultyConstants.PrecisionRawWeightHyperjumpAfterJump), t => t.PrecisionRawWeightHyperjumpAfterJump, (t, v) => t with { PrecisionRawWeightHyperjumpAfterJump = v },
                minValue: 0.93, maxValue: 1.00),
            doubleParam(nameof(CatchDifficultyConstants.PrecisionRawWeightJumpAfterHyperjump), t => t.PrecisionRawWeightJumpAfterHyperjump, (t, v) => t with { PrecisionRawWeightJumpAfterHyperjump = v },
                minValue: 0.93, maxValue: 1.00),
            doubleParam(nameof(CatchDifficultyConstants.PrecisionRawWeightJumps), t => t.PrecisionRawWeightJumps, (t, v) => t with { PrecisionRawWeightJumps = v },
                minValue: 0.93, maxValue: 1.00),
            doubleParam(nameof(CatchDifficultyConstants.PrecisionDelayedWeight), t => t.PrecisionDelayedWeight, (t, v) => t with { PrecisionDelayedWeight = v },
                minValue: 0.70, maxValue: 1.00),
            doubleParam(nameof(CatchDifficultyConstants.PrecisionStrainAmplitude), t => t.PrecisionStrainAmplitude, (t, v) => t with { PrecisionStrainAmplitude = v },
                minValue: 20.0, maxValue: 60.0),
            doubleParam(nameof(CatchDifficultyConstants.PrecisionStrainPace), t => t.PrecisionStrainPace, (t, v) => t with { PrecisionStrainPace = v },
                minValue: 10.0, maxValue: 40.0),
            doubleParam(nameof(CatchDifficultyConstants.PrecisionStrainMultiplier), t => t.PrecisionStrainMultiplier, (t, v) => t with { PrecisionStrainMultiplier = v },
                minValue: 35.0, maxValue: 45.0),
            doubleParam(nameof(CatchDifficultyConstants.MaxPrecisionCorrection), t => t.MaxPrecisionCorrection, (t, v) => t with { MaxPrecisionCorrection = v },
                minValue: 1.15, maxValue: 1.40),
            doubleParam(nameof(CatchDifficultyConstants.SpeedSnapAmplitude), t => t.SpeedSnapAmplitude, (t, v) => t with { SpeedSnapAmplitude = v },
                minValue: 5.0, maxValue: 25.0),
            doubleParam(nameof(CatchDifficultyConstants.SpeedSnapPace), t => t.SpeedSnapPace, (t, v) => t with { SpeedSnapPace = v },
                minValue: 20.0, maxValue: 60.0),
            doubleParam(nameof(CatchDifficultyConstants.SpeedSnapMultiplier), t => t.SpeedSnapMultiplier, (t, v) => t with { SpeedSnapMultiplier = v },
                minValue: 0.75, maxValue: 1.25),
            doubleParam(nameof(CatchDifficultyConstants.SpeedBurstAmplitude), t => t.SpeedBurstAmplitude, (t, v) => t with { SpeedBurstAmplitude = v },
                minValue: 5.0, maxValue: 25.0),
            doubleParam(nameof(CatchDifficultyConstants.SpeedBurstPace), t => t.SpeedBurstPace, (t, v) => t with { SpeedBurstPace = v },
                minValue: 20.0, maxValue: 60.0),
            doubleParam(nameof(CatchDifficultyConstants.SpeedBurstMultiplier), t => t.SpeedBurstMultiplier, (t, v) => t with { SpeedBurstMultiplier = v },
                minValue: 0.75, maxValue: 1.25),
            doubleParam(nameof(CatchDifficultyConstants.SpeedConsistencyAmplitude), t => t.SpeedConsistencyAmplitude, (t, v) => t with { SpeedConsistencyAmplitude = v },
                minValue: 5.0, maxValue: 25.0),
            doubleParam(nameof(CatchDifficultyConstants.SpeedConsistencyPace), t => t.SpeedConsistencyPace, (t, v) => t with { SpeedConsistencyPace = v },
                minValue: 20.0, maxValue: 60.0),
            doubleParam(nameof(CatchDifficultyConstants.SpeedConsistencyMultiplier), t => t.SpeedConsistencyMultiplier, (t, v) => t with { SpeedConsistencyMultiplier = v },
                minValue: 0.75, maxValue: 1.25),
            doubleParam(nameof(CatchDifficultyConstants.ReadingHighCsPower), t => t.ReadingHighCsPower, (t, v) => t with { ReadingHighCsPower = v },
                minValue: 1.4, maxValue: 1.8),
            doubleParam(nameof(CatchDifficultyConstants.ReadingHighCsRate), t => t.ReadingHighCsRate, (t, v) => t with { ReadingHighCsRate = v },
                minValue: 0.30, maxValue: 0.50),
            doubleParam(nameof(CatchDifficultyConstants.ReadingHighCsPenaltyHypers), t => t.ReadingHighCsPenaltyHypers, (t, v) => t with { ReadingHighCsPenaltyHypers = v },
                minValue: 0.5, maxValue: 1.0),
            doubleParam(nameof(CatchDifficultyConstants.ReadingLocalRhythmPenalty), t => t.ReadingLocalRhythmPenalty, (t, v) => t with { ReadingLocalRhythmPenalty = v },
                minValue: 0.90, maxValue: 0.98),
            doubleParam(nameof(CatchDifficultyConstants.ReadingExplicitRhythmPenalty), t => t.ReadingExplicitRhythmPenalty, (t, v) => t with { ReadingExplicitRhythmPenalty = v },
                minValue: 0.90, maxValue: 1.00),
            doubleParam(nameof(CatchDifficultyConstants.ReadingImplicitRhythmPenalty), t => t.ReadingImplicitRhythmPenalty, (t, v) => t with { ReadingImplicitRhythmPenalty = v },
                minValue: 0.95, maxValue: 1.00),
            doubleParam(nameof(CatchDifficultyConstants.ReadingSimilarDistancePenalty), t => t.ReadingSimilarDistancePenalty, (t, v) => t with { ReadingSimilarDistancePenalty = v },
                minValue: 0.80, maxValue: 0.95),
            doubleParam(nameof(CatchDifficultyConstants.ReadingAlternatingDistancePenalty), t => t.ReadingAlternatingDistancePenalty, (t, v) => t with { ReadingAlternatingDistancePenalty = v },
                minValue: 0.95, maxValue: 1.00),
            doubleParam(nameof(CatchDifficultyConstants.ReadingHyperchainPenalty), t => t.ReadingHyperchainPenalty, (t, v) => t with { ReadingHyperchainPenalty = v },
                minValue: 0.88, maxValue: 0.95),
            doubleParam(nameof(CatchDifficultyConstants.ReadingNonHyperchainPenalty), t => t.ReadingNonHyperchainPenalty, (t, v) => t with { ReadingNonHyperchainPenalty = v },
                minValue: 0.90, maxValue: 1.00),
            doubleParam(nameof(CatchDifficultyConstants.ReadingHighVelocityNerf), t => t.ReadingHighVelocityNerf, (t, v) => t with { ReadingHighVelocityNerf = v },
                minValue: 0.05, maxValue: 0.25),
            doubleParam(nameof(CatchDifficultyConstants.ReadingHighDistanceBuff), t => t.ReadingHighDistanceBuff, (t, v) => t with { ReadingHighDistanceBuff = v },
                minValue: 0.05, maxValue: 0.25),
            doubleParam(nameof(CatchDifficultyConstants.ReadingFakeActionBuff), t => t.ReadingFakeActionBuff, (t, v) => t with { ReadingFakeActionBuff = v },
                minValue: 1.00, maxValue: 1.10),
            doubleParam(nameof(CatchDifficultyConstants.ReadingFuturePrecisionBuff), t => t.ReadingFuturePrecisionBuff, (t, v) => t with { ReadingFuturePrecisionBuff = v },
                minValue: 0.05, maxValue: 0.20),
            doubleParam(nameof(CatchDifficultyConstants.StandingWidthAdditiveConstant), t => t.StandingWidthAdditiveConstant, (t, v) => t with { StandingWidthAdditiveConstant = v },
                minValue: 1.20, maxValue: 1.30),
            doubleParam(nameof(CatchDifficultyConstants.PrecisionCorrectionDistanceExponent), t => t.PrecisionCorrectionDistanceExponent, (t, v) => t with { PrecisionCorrectionDistanceExponent = v },
                minValue: 0.60, maxValue: 0.90),
            doubleParam(nameof(CatchDifficultyConstants.PrecisionCorrectionTimeExponent), t => t.PrecisionCorrectionTimeExponent, (t, v) => t with { PrecisionCorrectionTimeExponent = v },
                minValue: 1.30, maxValue: 1.70),
            doubleParam(nameof(CatchDifficultyConstants.PrecisionCorrectionDistanceWeight), t => t.PrecisionCorrectionDistanceWeight, (t, v) => t with { PrecisionCorrectionDistanceWeight = v },
                minValue: 0.35, maxValue: 0.65)
        };

        private static DifficultyTuningParameter<CatchDifficultyConstants> doubleParam(string propertyName, Func<CatchDifficultyConstants, double> getter,
                                                                                       Func<CatchDifficultyConstants, double, CatchDifficultyConstants> setter,
                                                                                       bool defaultEnabled = true, double minValue = 0.01, double? maxValue = null)
        {
            string label = propertyName.Humanize();
            return DifficultyTuningParameter<CatchDifficultyConstants>.ForDouble(label, label, getter, setter, defaultEnabled, minValue, maxValue);
        }

        private static DifficultyTuningParameter<CatchDifficultyConstants> intParam(string propertyName, Func<CatchDifficultyConstants, int> getter,
                                                                                    Func<CatchDifficultyConstants, int, CatchDifficultyConstants> setter,
                                                                                    bool defaultEnabled = true, int minValue = 1, int? maxValue = null)
        {
            string label = propertyName.Humanize();
            return DifficultyTuningParameter<CatchDifficultyConstants>.ForInt(label, label, getter, setter, defaultEnabled, minValue, maxValue);
        }
    }
}
