// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Overlays.Toolbar;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch.Difficulty;
using osu.Game.Rulesets.Osu.Difficulty;
using PerformanceCalculatorGUI.Configuration;

namespace PerformanceCalculatorGUI.Components
{
    public partial class DifficultyTuningButton : ToolbarButton, IHasPopover
    {
        protected override Anchor TooltipAnchor => Anchor.TopRight;

        [Resolved]
        private Bindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private OsuDifficultyTuningManager osuTuningManager { get; set; } = null!;

        [Resolved]
        private CatchDifficultyTuningManager catchTuningManager { get; set; } = null!;

        public DifficultyTuningButton()
        {
            TooltipMain = "Tuning";
            SetIcon(new ScreenSelectionButtonIcon());
        }

        public Popover GetPopover()
        {
            return ruleset.Value.ShortName switch
            {
                "fruits" => new DifficultyTuningPopover<CatchDifficultyConstants>(
                    catchTuningManager.Current,
                    CatchDifficultyConstants.Default,
                    "osu!catch",
                    "catch-tuning.json",
                    new[] { new DifficultyTuningSection<CatchDifficultyConstants>("All parameters", CatchDifficultyTuningParameters.All.ToArray()) }),
                _ => new DifficultyTuningPopover<OsuDifficultyConstants>(
                    osuTuningManager.Current,
                    OsuDifficultyConstants.Default,
                    "osu!",
                    "osu-tuning.json",
                    OsuDifficultyTuningParameters.Sections)
            };
        }

        protected override bool OnClick(ClickEvent e)
        {
            this.ShowPopover();
            return base.OnClick(e);
        }
    }
}
