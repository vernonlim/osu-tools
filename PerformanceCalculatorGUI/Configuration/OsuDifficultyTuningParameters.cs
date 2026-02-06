// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Osu.Difficulty;

namespace PerformanceCalculatorGUI.Configuration
{
    public static class OsuDifficultyTuningParameters
    {
        public static readonly IReadOnlyList<DifficultyTuningSection<OsuDifficultyConstants>> Sections = new[]
        {
            new DifficultyTuningSection<OsuDifficultyConstants>("Performance scales",
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Aim perf scale", "Aim perf", t => t.AimPerformanceScale, (t, v) => t with { AimPerformanceScale = v }, false),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Speed perf scale", "Speed perf", t => t.SpeedPerformanceScale, (t, v) => t with { SpeedPerformanceScale = v }, false),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Accuracy perf scale", "Accuracy perf", t => t.AccuracyPerformanceScale, (t, v) => t with { AccuracyPerformanceScale = v }, false),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Flashlight perf scale", "Flashlight perf", t => t.FlashlightPerformanceScale, (t, v) => t with { FlashlightPerformanceScale = v }, false),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Reading perf scale", "Reading perf", t => t.ReadingPerformanceScale, (t, v) => t with { ReadingPerformanceScale = v }, false),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Total perf scale", "Total perf", t => t.TotalPerformanceScale, (t, v) => t with { TotalPerformanceScale = v }, false),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Cognition perf exponent", "Cognition perf", t => t.CognitionPerformanceExponent, (t, v) => t with { CognitionPerformanceExponent = v }, false)
            ),
            new DifficultyTuningSection<OsuDifficultyConstants>("Skill strain scales",
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Aim strain scale", "Aim strain", t => t.AimSkillStrainScale, (t, v) => t with { AimSkillStrainScale = v }, true),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Speed strain scale", "Speed strain", t => t.SpeedSkillStrainScale, (t, v) => t with { SpeedSkillStrainScale = v }, true),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Flashlight strain scale", "Flashlight strain", t => t.FlashlightSkillStrainScale, (t, v) => t with { FlashlightSkillStrainScale = v }, false),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Reading strain scale", "Reading strain", t => t.ReadingSkillStrainScale, (t, v) => t with { ReadingSkillStrainScale = v }, true)
            ),
            new DifficultyTuningSection<OsuDifficultyConstants>("Aim bonuses",
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Aim wide angle", "Aim wide angle", t => t.AimWideAngleBonusScale, (t, v) => t with { AimWideAngleBonusScale = v }, true),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Aim acute angle", "Aim acute angle", t => t.AimAcuteAngleScale, (t, v) => t with { AimAcuteAngleScale = v }, true),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Aim slider bonus", "Aim slider bonus", t => t.AimSliderBonusScale, (t, v) => t with { AimSliderBonusScale = v }, true),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Aim velocity bonus", "Aim velocity bonus", t => t.AimVelocityChangeBonusScale, (t, v) => t with { AimVelocityChangeBonusScale = v }, true),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Aim wiggle bonus", "Aim wiggle bonus", t => t.AimWiggleBonusScale, (t, v) => t with { AimWiggleBonusScale = v }, false),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Aim high BPM base", "Aim high bpm", t => t.AimHighBpmBonusBase, (t, v) => t with { AimHighBpmBonusBase = v }, true)
            ),
            new DifficultyTuningSection<OsuDifficultyConstants>("Flashlight bonuses",
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("FL max opacity", "Flashlight max opacity", t => t.FlashlightMaxOpacityBonusScale, (t, v) => t with { FlashlightMaxOpacityBonusScale = v }, false),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("FL hidden bonus", "Flashlight hidden bonus", t => t.FlashlightHiddenBonusScale, (t, v) => t with { FlashlightHiddenBonusScale = v }, false),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("FL min velocity", "Flashlight min velocity", t => t.FlashlightMinVelocityScale, (t, v) => t with { FlashlightMinVelocityScale = v }, false),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("FL slider bonus", "Flashlight slider bonus", t => t.FlashlightSliderBonusScale, (t, v) => t with { FlashlightSliderBonusScale = v }, false),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("FL min angle", "Flashlight min angle", t => t.FlashlightMinAngleScale, (t, v) => t with { FlashlightMinAngleScale = v }, false)
            ),
            new DifficultyTuningSection<OsuDifficultyConstants>("Rhythm tuning",
                DifficultyTuningParameter<OsuDifficultyConstants>.ForInt("Rhythm time max (ms)", "Rhythm history ms", t => t.RhythmHistoryTimeMax, (t, v) => t with { RhythmHistoryTimeMax = v }, false),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForInt("Rhythm objects max", "Rhythm history objs", t => t.RhythmHistoryObjectsMax, (t, v) => t with { RhythmHistoryObjectsMax = v }, false),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Rhythm overall scale", "Rhythm overall", t => t.RhythmOverallScale, (t, v) => t with { RhythmOverallScale = v }, true),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Rhythm ratio scale", "Rhythm ratio", t => t.RhythmRatioScale, (t, v) => t with { RhythmRatioScale = v }, true)
            ),
            new DifficultyTuningSection<OsuDifficultyConstants>("Reading tuning",
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Reading window size", "Reading window", t => t.ReadingWindowSize, (t, v) => t with { ReadingWindowSize = v }, false),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Reading distance threshold", "Reading distance", t => t.ReadingDistanceInfluenceThreshold, (t, v) => t with { ReadingDistanceInfluenceThreshold = v }, false),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Reading hidden multiplier", "Reading hidden", t => t.ReadingHiddenMultiplier, (t, v) => t with { ReadingHiddenMultiplier = v }, false),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Reading density multiplier", "Reading density", t => t.ReadingDensityMultiplier, (t, v) => t with { ReadingDensityMultiplier = v }, false),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Reading density base", "Reading density base", t => t.ReadingDensityDifficultyBase, (t, v) => t with { ReadingDensityDifficultyBase = v }, false),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Reading preempt balance", "Reading preempt", t => t.ReadingPreemptBalancingFactor, (t, v) => t with { ReadingPreemptBalancingFactor = v }, false),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Reading preempt start", "Reading preempt start", t => t.ReadingPreemptStartingPoint, (t, v) => t with { ReadingPreemptStartingPoint = v }, false),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Reading min angle time", "Reading min angle", t => t.ReadingMinimumAngleRelevancyTime, (t, v) => t with { ReadingMinimumAngleRelevancyTime = v }, false),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Reading max angle time", "Reading max angle", t => t.ReadingMaximumAngleRelevancyTime, (t, v) => t with { ReadingMaximumAngleRelevancyTime = v }, false),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Reading reduced baseline", "Reading reduced base", t => t.ReadingReducedDifficultyBaseLine, (t, v) => t with { ReadingReducedDifficultyBaseLine = v }, false),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Reading reduced duration", "Reading reduced dur", t => t.ReadingReducedDifficultyDuration, (t, v) => t with { ReadingReducedDifficultyDuration = v }, false)
            ),
            new DifficultyTuningSection<OsuDifficultyConstants>("Speed tuning",
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Speed single spacing", "Speed spacing", t => t.SpeedSingleSpacingThreshold, (t, v) => t with { SpeedSingleSpacingThreshold = v }, true),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Speed min bonus BPM", "Speed min bpm", t => t.SpeedMinBonusBpm, (t, v) => t with { SpeedMinBonusBpm = v }, true),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Speed balancing factor", "Speed balance", t => t.SpeedBalancingFactor, (t, v) => t with { SpeedBalancingFactor = v }, true),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Speed distance scale", "Speed distance", t => t.SpeedDistanceScale, (t, v) => t with { SpeedDistanceScale = v }, true),
                DifficultyTuningParameter<OsuDifficultyConstants>.ForDouble("Speed high BPM base", "Speed high bpm", t => t.SpeedHighBpmBonusBase, (t, v) => t with { SpeedHighBpmBonusBase = v }, true)
            )
        };

        public static readonly IReadOnlyList<DifficultyTuningParameter<OsuDifficultyConstants>> All =
            Sections.SelectMany(section => section.Parameters).ToArray();
    }
}
