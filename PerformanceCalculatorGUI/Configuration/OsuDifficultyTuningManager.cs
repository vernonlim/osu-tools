// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Game.Rulesets.Osu.Difficulty;

namespace PerformanceCalculatorGUI.Configuration
{
    public class OsuDifficultyTuningManager
    {
        public Bindable<OsuDifficultyConstants> Current { get; } = new Bindable<OsuDifficultyConstants>(OsuDifficultyConstants.Default);
    }
}
