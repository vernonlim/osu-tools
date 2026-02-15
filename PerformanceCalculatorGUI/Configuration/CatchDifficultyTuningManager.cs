// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Game.Rulesets.Catch.Difficulty;

namespace PerformanceCalculatorGUI.Configuration
{
    public class CatchDifficultyTuningManager
    {
        public Bindable<CatchDifficultyConstants> Current { get; } = new Bindable<CatchDifficultyConstants>(CatchDifficultyConstants.Default);

        /// <summary>
        /// Fired when tuning values have been applied via the tuning popover.
        /// </summary>
        public event Action? Applied;

        public void NotifyApplied() => Applied?.Invoke();
    }
}
