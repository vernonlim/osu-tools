// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osuTK;
using PerformanceCalculatorGUI.Components.TextBoxes;
using PerformanceCalculatorGUI.Configuration;

namespace PerformanceCalculatorGUI.Components
{
    public partial class DifficultyTuningPopover<TConstants> : OsuPopover
    {
        private const double tuning_min_value = 0.0;
        private const double tuning_max_value = double.MaxValue;

        /// <summary>
        /// Fired after tuning values have been applied.
        /// </summary>
        public event Action? Applied;

        private readonly Bindable<TConstants> current;
        private readonly TConstants defaultConstants;
        private readonly string rulesetLabel;
        private readonly string presetFileName;
        private readonly IReadOnlyList<DifficultyTuningSection<TConstants>> sections;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private NotificationDisplay notificationDisplay { get; set; } = null!;

        private readonly List<TuningControl> tuningControls = new List<TuningControl>();
        private FileChooserLabelledTextBox tuningFileTextBox = null!;

        public DifficultyTuningPopover(Bindable<TConstants> current, TConstants defaultConstants,
                                       string rulesetLabel, string presetFileName,
                                       IReadOnlyList<DifficultyTuningSection<TConstants>> sections)
            : base(false)
        {
            this.current = current;
            this.defaultConstants = defaultConstants;
            this.rulesetLabel = rulesetLabel;
            this.presetFileName = presetFileName;
            this.sections = sections;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var initialTuning = current.Value;
            tuningControls.Clear();
            string defaultPresetPath = getDefaultPresetPath();

            var content = new List<Drawable>
            {
                new OsuSpriteText
                {
                    Font = OsuFont.Torus.With(size: 18, weight: FontWeight.SemiBold),
                    Text = "Difficulty tuning"
                },
                new OsuSpriteText
                {
                    Font = new FontUsage(size: 12),
                    Text = $"Applies to {rulesetLabel} ruleset only."
                }
            };

            foreach (var section in sections)
            {
                content.Add(new OsuSpriteText
                {
                    Margin = new MarginPadding { Top = 6f },
                    Font = OsuFont.Torus.With(size: 14, weight: FontWeight.SemiBold),
                    Text = section.Title
                });

                content.Add(createSectionGrid(section, initialTuning!));
            }

            content.Add(new OsuSpriteText
            {
                Margin = new MarginPadding { Top = 6f },
                Font = OsuFont.Torus.With(size: 14, weight: FontWeight.SemiBold),
                Text = "Tuning presets"
            });

            content.Add(new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 6f),
                Children = new Drawable[]
                {
                    tuningFileTextBox = new FileChooserLabelledTextBox(new Bindable<string>("params"), ".json")
                    {
                        RelativeSizeAxes = Axes.X,
                        Label = "Preset file",
                        FixedLabelWidth = 100f,
                        PlaceholderText = defaultPresetPath,
                        Current = { Value = defaultPresetPath }
                    },
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(8, 0),
                        Children = new Drawable[]
                        {
                            new RoundedButton
                            {
                                Width = 120,
                                Height = 32,
                                BackgroundColour = colourProvider.Background3,
                                Text = "Load",
                                Action = loadFromJson
                            },
                            new RoundedButton
                            {
                                Width = 120,
                                Height = 32,
                                BackgroundColour = colourProvider.Background3,
                                Text = "Save",
                                Action = saveToJson
                            }
                        }
                    }
                }
            });

            content.Add(new FillFlowContainer
            {
                Margin = new MarginPadding { Top = 10f },
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8, 0),
                Children = new Drawable[]
                {
                    new RoundedButton
                    {
                        Width = 120,
                        Height = 32,
                        BackgroundColour = colourProvider.Background3,
                        Text = "Reset",
                        Action = resetToDefaults
                    },
                    new RoundedButton
                    {
                        Width = 200,
                        Height = 32,
                        BackgroundColour = colourProvider.Background1,
                        Text = "Apply & recalculate",
                        Action = apply
                    }
                }
            });

            Child = new Container
            {
                Size = new Vector2(1000, 650),
                Padding = new MarginPadding { Horizontal = 16, Vertical = 10 },
                Child = new OsuScrollContainer(Direction.Vertical)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 6f),
                        Children = content.ToArray()
                    }
                }
            };
        }

        private GridContainer createSectionGrid(DifficultyTuningSection<TConstants> section, TConstants initialTuning)
        {
            int rows = (section.Parameters.Count + 1) / 2;
            var rowDimensions = new Dimension[rows];

            for (int i = 0; i < rows; i++)
                rowDimensions[i] = new Dimension(GridSizeMode.AutoSize);

            var gridContent = new Drawable[rows][];

            for (int row = 0; row < rows; row++)
            {
                var rowContent = new Drawable[2];

                for (int column = 0; column < 2; column++)
                {
                    int index = row * 2 + column;

                    rowContent[column] = index < section.Parameters.Count
                        ? createControl(section.Parameters[index], initialTuning)
                        : new Container();
                }

                gridContent[row] = rowContent;
            }

            return new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                ColumnDimensions = new[] { new Dimension(), new Dimension() },
                RowDimensions = rowDimensions,
                Content = gridContent
            };
        }

        private Drawable createControl(DifficultyTuningParameter<TConstants> parameter, TConstants initialTuning)
        {
            TuningControl control;

            if (parameter.IsInteger)
            {
                int value = (int)Math.Round(parameter.Getter(initialTuning));
                var box = createTuningIntBox(parameter.UiLabel, value);
                control = new TuningControl(parameter, box);
            }
            else
            {
                double value = parameter.Getter(initialTuning);
                var box = createTuningBox(parameter.UiLabel, value);
                control = new TuningControl(parameter, box);
            }

            tuningControls.Add(control);
            return control.Drawable;
        }

        private void apply()
        {
            var newValue = buildTuningFromControls();

            Logger.Log($"[Tuning] Bindable hash={RuntimeHelpers.GetHashCode(current)}", level: LogLevel.Important);
            Logger.Log($"[Tuning] Bindable.Disabled={current.Disabled}", level: LogLevel.Important);
            Logger.Log($"[Tuning] Old==Default: {EqualityComparer<TConstants>.Default.Equals(current.Value, defaultConstants)}, New==Default: {EqualityComparer<TConstants>.Default.Equals(newValue, defaultConstants)}, Old==New: {EqualityComparer<TConstants>.Default.Equals(current.Value, newValue)}", level: LogLevel.Important);

            Logger.Log($"[Tuning] Setting to default...", level: LogLevel.Important);
            current.Value = defaultConstants;
            Logger.Log($"[Tuning] After default: current={current.Value}", level: LogLevel.Important);

            Logger.Log($"[Tuning] Setting to newValue...", level: LogLevel.Important);
            current.Value = newValue;
            Logger.Log($"[Tuning] After newValue: current={current.Value}", level: LogLevel.Important);

            Applied?.Invoke();

            this.HidePopover();
        }

        private void resetToDefaults()
        {
            applyTuning(defaultConstants);
        }

        private LimitedLabelledFractionalNumberBox createTuningBox(string label, double defaultValue)
        {
            return new LimitedLabelledFractionalNumberBox
            {
                RelativeSizeAxes = Axes.X,
                Anchor = Anchor.TopLeft,
                Label = label,
                PlaceholderText = defaultValue.ToString(),
                MinValue = tuning_min_value,
                MaxValue = tuning_max_value,
                Value = { Value = defaultValue }
            };
        }

        private LimitedLabelledNumberBox createTuningIntBox(string label, int defaultValue)
        {
            return new LimitedLabelledNumberBox
            {
                RelativeSizeAxes = Axes.X,
                Anchor = Anchor.TopLeft,
                Label = label,
                PlaceholderText = defaultValue.ToString(),
                MinValue = 0,
                Value = { Value = defaultValue }
            };
        }

        private static void setTuningValue(LimitedLabelledFractionalNumberBox box, double value)
        {
            box.PlaceholderText = value.ToString();
            box.Text = string.Empty;
            box.Value.Value = value;
        }

        private static void setTuningValue(LimitedLabelledNumberBox box, int value)
        {
            box.PlaceholderText = value.ToString();
            box.Text = string.Empty;
            box.Value.Value = value;
        }

        private TConstants buildTuningFromControls()
        {
            var tuning = current.Value;

            foreach (var control in tuningControls)
            {
                tuning = control.Parameter.Setter(tuning!, control.Value);
            }

            return tuning!;
        }

        private void applyTuning(TConstants tuning)
        {
            foreach (var control in tuningControls)
            {
                control.Reset(control.Parameter.Getter(tuning));
            }

            current.Value = tuning;
        }

        private string getDefaultPresetPath()
        {
            return Path.Combine("params", presetFileName);
        }

        private string? getPresetPath()
        {
            if (tuningFileTextBox == null)
                return null;

            string path = tuningFileTextBox.Current.Value?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(path))
                path = getDefaultPresetPath();

            return string.IsNullOrWhiteSpace(path) ? null : path;
        }

        private void saveToJson()
        {
            string? path = getPresetPath();

            if (string.IsNullOrWhiteSpace(path))
            {
                notificationDisplay.Display(new Notification("Select a preset file path first."));
                return;
            }

            try
            {
                string? directory = Path.GetDirectoryName(path);

                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                var tuning = buildTuningFromControls();
                File.WriteAllText(path, JsonConvert.SerializeObject(tuning, Formatting.Indented));
                notificationDisplay.Display(new Notification($"Saved tuning preset to {Path.GetFileName(path)}."));
            }
            catch (Exception e)
            {
                notificationDisplay.Display(new Notification($"Failed to save tuning preset: {e.Message}"));
            }
        }

        private void loadFromJson()
        {
            string? path = getPresetPath();

            if (string.IsNullOrWhiteSpace(path))
            {
                notificationDisplay.Display(new Notification("Select a preset file path first."));
                return;
            }

            if (!File.Exists(path))
            {
                notificationDisplay.Display(new Notification($"Preset file not found: {path}"));
                return;
            }

            try
            {
                var tuning = JsonConvert.DeserializeObject<TConstants>(File.ReadAllText(path));

                if (tuning == null)
                {
                    notificationDisplay.Display(new Notification("Preset file did not contain valid tuning values."));
                    return;
                }

                applyTuning(tuning);
                notificationDisplay.Display(new Notification($"Loaded tuning preset from {Path.GetFileName(path)}."));
            }
            catch (Exception e)
            {
                notificationDisplay.Display(new Notification($"Failed to load tuning preset: {e.Message}"));
            }
        }

        private sealed class TuningControl
        {
            public DifficultyTuningParameter<TConstants> Parameter { get; }

            private readonly LimitedLabelledFractionalNumberBox? fractionalBox;
            private readonly LimitedLabelledNumberBox? intBox;

            public Drawable Drawable => fractionalBox ?? (Drawable)intBox!;

            public double Value => fractionalBox != null ? fractionalBox.Value.Value : intBox!.Value.Value;

            public TuningControl(DifficultyTuningParameter<TConstants> parameter, LimitedLabelledFractionalNumberBox box)
            {
                Parameter = parameter;
                fractionalBox = box;
            }

            public TuningControl(DifficultyTuningParameter<TConstants> parameter, LimitedLabelledNumberBox box)
            {
                Parameter = parameter;
                intBox = box;
            }

            public void Reset(double value)
            {
                if (fractionalBox != null)
                    setTuningValue(fractionalBox, value);
                else
                    setTuningValue(intBox!, (int)Math.Round(value));
            }
        }
    }
}
