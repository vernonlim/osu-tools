// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Game.Rulesets.Catch.Difficulty;

namespace PerformanceCalculatorGUI.Configuration
{
    public class CatchDifficultyTuningManager
    {
        public Bindable<CatchDifficultyConstants> Current { get; } = new Bindable<CatchDifficultyConstants>(CatchDifficultyConstants.Default);
    }
}
