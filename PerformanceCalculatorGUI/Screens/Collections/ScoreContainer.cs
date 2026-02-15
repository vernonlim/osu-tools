// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Threading;
using osu.Framework.Utils;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;
using PerformanceCalculatorGUI.Components;
using PerformanceCalculatorGUI.Components.TextBoxes;

namespace PerformanceCalculatorGUI.Screens.Collections
{
    public partial class ScoreContainer : Container
    {
        public ExtendedScore Score { get; }

        private readonly IconButton deleteButton;

        private readonly IDictionary<string, ExpectedPerformanceValues> expectedValuesByKey;
        private readonly Action onExpectedValuesChanged;
        private readonly string expectedValuesKey;

        private ExpectedPerformanceValues? expectedValues;
        private FillFlowContainer expectedValuesContainer = null!;
        private FillFlowContainer expectedValuesRows = null!;
        private OsuSpriteText expectedValuesToggleText = null!;
        private OsuSpriteText targetPpText = null!;
        private ScheduledDelegate? debouncedExpectedSave;

        private const float expected_row_height = 35;
        private const float expected_label_width = 140;

        private bool expectedValuesExpanded;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        public delegate void OnDeleteHandler(ExtendedScore score);

        public event OnDeleteHandler? OnDelete;

        public ScoreContainer(ExtendedScore score, IDictionary<string, ExpectedPerformanceValues> expectedValuesByKey, Action onExpectedValuesChanged)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            Score = score;
            this.expectedValuesByKey = expectedValuesByKey;
            this.onExpectedValuesChanged = onExpectedValuesChanged;
            expectedValuesKey = score.IsStoredScore ? score.StoredScoreId! : score.SoloScore.ID.ToString()!;
            expectedValuesByKey.TryGetValue(expectedValuesKey, out expectedValues);

            Child = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
                Children = new Drawable[]
                {
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        ColumnDimensions = new[] { new Dimension(GridSizeMode.AutoSize), new Dimension() },
                        RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                deleteButton = new IconButton
                                {
                                    Width = 0,
                                    Height = 35,
                                    Icon = FontAwesome.Regular.TrashAlt,
                                    Action = () =>
                                    {
                                        OnDelete?.Invoke(score);
                                    }
                                },
                                new ExtendedProfileScore(score, true)
                            }
                        }
                    },
                    targetPpText = new OsuSpriteText
                    {
                        RelativeSizeAxes = Axes.X,
                        Margin = new MarginPadding { Left = 35, Right = 10 },
                        Font = OsuFont.GetFont(size: 11, weight: FontWeight.SemiBold),
                        Text = "Target -"
                    },
                    expectedValuesContainer = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 4),
                        Padding = new MarginPadding { Left = 35, Right = 10, Bottom = 2 }
                    }
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            targetPpText.Colour = colourProvider.Light2;
            populateExpectedValues();
        }

        protected override bool OnHover(HoverEvent e)
        {
            deleteButton
                .Delay(500)
                .ResizeWidthTo(35, 100, Easing.Out)
                .OnComplete(b => b.Margin = new MarginPadding { Right = 5 });

            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            deleteButton
                .ResizeWidthTo(0, 100, Easing.Out)
                .OnComplete(b => b.Margin = new MarginPadding());

            base.OnHoverLost(e);
        }

        private void populateExpectedValues()
        {
            expectedValuesContainer.Clear();

            var attributes = AttributeConversion.ToDictionary(Score.PerformanceAttributes);
            var numericAttributes = new Dictionary<string, double>();

            foreach (var attribute in attributes)
            {
                if (tryGetDouble(attribute.Value, out double parsed))
                    numericAttributes[attribute.Key] = parsed;
            }

            if (numericAttributes.TryGetValue("pp", out double totalPp))
            {
                if (!numericAttributes.ContainsKey("total"))
                    numericAttributes["total"] = totalPp;

                numericAttributes.Remove("pp");
            }

            if (!numericAttributes.Any())
            {
                expectedValuesContainer.Hide();
                return;
            }

            expectedValuesContainer.Show();
            expectedValuesContainer.Add(createExpectedHeader());
            expectedValuesRows = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
                Padding = new MarginPadding { Left = 5 }
            };
            expectedValuesContainer.Add(expectedValuesRows);

            if (numericAttributes.TryGetValue("total", out double total))
            {
                double? expectedTotal = null;
                if (expectedValues != null && AutobalanceRunner.TryGetExpectedValue(expectedValues, AutobalanceTarget.Total, out double expectedValue))
                    expectedTotal = expectedValue;

                expectedValuesRows.Add(createExpectedRow("total", total, expectedTotal, setExpectedTotal));
            }

            foreach (var attribute in numericAttributes.Where(x => x.Key != "total").OrderBy(x => x.Key))
            {
                double? expectedValue = null;

                if (expectedValues?.Skills.TryGetValue(attribute.Key, out double storedValue) == true)
                    expectedValue = storedValue;

                expectedValuesRows.Add(createExpectedRow(attribute.Key, attribute.Value, expectedValue,
                    value => setExpectedSkill(attribute.Key, value)));
            }

            updateExpectedValuesState();
            updateTargetDisplay();
        }

        private Drawable createExpectedHeader()
        {
            expectedValuesToggleText = new OsuSpriteText
            {
                Text = expectedValuesExpanded ? "v" : ">",
                Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                Colour = colourProvider.Light2,
                Width = 12
            };

            return new ExpectedValuesHeader(toggleExpectedValues)
            {
                Child = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(6, 0),
                    Children = new Drawable[]
                    {
                        expectedValuesToggleText,
                        new OsuSpriteText
                        {
                            Text = "Expected values",
                            Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                            Colour = colourProvider.Light2
                        }
                    }
                }
            };
        }

        private void toggleExpectedValues()
        {
            expectedValuesExpanded = !expectedValuesExpanded;
            updateExpectedValuesState();
        }

        private void updateExpectedValuesState()
        {
            if (expectedValuesRows == null || expectedValuesToggleText == null)
                return;

            if (expectedValuesExpanded)
            {
                expectedValuesRows.Show();
                expectedValuesToggleText.Text = "v";
            }
            else
            {
                expectedValuesRows.Hide();
                expectedValuesToggleText.Text = ">";
            }
        }

        private Drawable createExpectedRow(string label, double actualValue, double? expectedValue, Action<double?> onExpectedChanged)
        {
            var expectedBox = new NullableLabelledFractionalNumberBox
            {
                RelativeSizeAxes = Axes.X,
                Label = label,
                FixedLabelWidth = expected_label_width,
                PlaceholderText = actualValue.ToString("0.##", CultureInfo.CurrentCulture),
                CommitOnFocusLoss = true,
                MinValue = 0
            };

            if (expectedValue.HasValue)
            {
                expectedBox.Text = expectedValue.Value.ToString("0.##", CultureInfo.CurrentCulture);
                expectedBox.Value.Value = expectedValue;
            }
            else
            {
                expectedBox.Text = string.Empty;
                expectedBox.Value.Value = null;
            }

            var diffText = new OsuSpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Margin = new MarginPadding { Left = 8 },
                Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                Colour = colourProvider.Light2,
            };

            updateDifferenceText(diffText, actualValue, expectedValue);

            expectedBox.Value.BindValueChanged(value =>
            {
                onExpectedChanged(value.NewValue);
                updateDifferenceText(diffText, actualValue, value.NewValue);
                scheduleExpectedSave();
            });

            return new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                Height = expected_row_height,
                ColumnDimensions = new[] { new Dimension(), new Dimension(GridSizeMode.AutoSize) },
                RowDimensions = new[] { new Dimension(GridSizeMode.Absolute, expected_row_height) },
                Content = new[]
                {
                    new Drawable[]
                    {
                        expectedBox,
                        diffText
                    }
                }
            };
        }

        private void updateDifferenceText(OsuSpriteText diffText, double actualValue, double? expectedValue)
        {
            if (expectedValue == null)
            {
                diffText.Text = "-";
                diffText.Colour = colourProvider.Light2;
                return;
            }

            double difference = actualValue - expectedValue.Value;
            diffText.Text = difference.ToString("+0.00;-0.00;0.00", CultureInfo.CurrentCulture) + "pp";
            diffText.Colour = getDifferenceColour(difference);
        }

        private void setExpectedTotal(double? value)
        {
            if (value == null)
            {
                if (expectedValues == null)
                    return;

                expectedValues.Total = null;
                pruneExpectedValuesIfEmpty();
                updateTargetDisplay();
                return;
            }

            ensureExpectedValues().Total = value;
            updateTargetDisplay();
        }

        private void setExpectedSkill(string key, double? value)
        {
            if (value == null)
            {
                if (expectedValues == null)
                    return;

                expectedValues.Skills.Remove(key);
                pruneExpectedValuesIfEmpty();
                updateTargetDisplay();
                return;
            }

            ensureExpectedValues().Skills[key] = value.Value;
            updateTargetDisplay();
        }

        private ExpectedPerformanceValues ensureExpectedValues()
        {
            if (expectedValues != null)
                return expectedValues;

            expectedValues = new ExpectedPerformanceValues();
            expectedValuesByKey[expectedValuesKey] = expectedValues;
            return expectedValues;
        }

        private void pruneExpectedValuesIfEmpty()
        {
            if (expectedValues == null)
                return;

            if (expectedValues.Total == null && expectedValues.Skills.Count == 0)
            {
                expectedValuesByKey.Remove(expectedValuesKey);
                expectedValues = null;
            }
        }

        private void scheduleExpectedSave()
        {
            debouncedExpectedSave?.Cancel();
            debouncedExpectedSave = Scheduler.AddDelayed(onExpectedValuesChanged, 250);
        }

        private void updateTargetDisplay()
        {
            if (targetPpText == null)
                return;

            double? actualValue = Score.PerformanceAttributes?.Total;
            double? expectedValue = null;

            if (expectedValues != null && AutobalanceRunner.TryGetExpectedValue(expectedValues, AutobalanceTarget.Total, out double expected))
                expectedValue = expected;

            if (actualValue == null || expectedValue == null)
            {
                targetPpText.Text = actualValue != null ? $"Local {actualValue:0.##}pp — Target -" : "Target -";
                targetPpText.Colour = colourProvider.Light2;
                return;
            }

            double difference = actualValue.Value - expectedValue.Value;
            targetPpText.Text = $"Local {actualValue:0.##}pp — Target {expectedValue:0.##}pp ({difference:+0.##;-0.##;0.00}pp)";
            targetPpText.Colour = getDifferenceColour(difference);
        }

        private Color4 getDifferenceColour(double difference)
        {
            var baseColor = colourProvider.Light1;

            return difference switch
            {
                < 0 => Interpolation.ValueAt(difference, baseColor, Color4.OrangeRed, 0, -200),
                > 0 => Interpolation.ValueAt(difference, baseColor, Color4.Lime, 0, 200),
                _ => baseColor
            };
        }

        private static bool tryGetDouble(object value, out double parsed)
        {
            if (value == null)
            {
                parsed = default;
                return false;
            }

            switch (value)
            {
                case double doubleValue:
                    parsed = doubleValue;
                    return true;

                case float floatValue:
                    parsed = floatValue;
                    return true;

                case int intValue:
                    parsed = intValue;
                    return true;

                case long longValue:
                    parsed = longValue;
                    return true;

                case decimal decimalValue:
                    parsed = (double)decimalValue;
                    return true;
            }

            return double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out parsed);
        }

        private partial class ExpectedValuesHeader : OsuClickableContainer
        {
            public ExpectedValuesHeader(Action action)
                : base(HoverSampleSet.Button)
            {
                Action = action;
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Padding = new MarginPadding { Left = 5, Bottom = 2 };
            }
        }
    }
}
