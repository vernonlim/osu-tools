// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch.Difficulty;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty;
using osuTK;
using PerformanceCalculatorGUI.Components;
using PerformanceCalculatorGUI.Components.TextBoxes;
using PerformanceCalculatorGUI.Configuration;
using PerformanceCalculatorGUI.Screens.Collections;

namespace PerformanceCalculatorGUI.Screens
{
    public partial class CollectionsScreen : PerformanceCalculatorScreen
    {
        public override bool ShouldShowConfirmationDialogOnSwitch => false;

        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Blue);

        [Resolved]
        private ScoreCache scoreCache { get; set; } = null!;

        [Resolved]
        private SettingsManager configManager { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private DialogOverlay dialogOverlay { get; set; } = null!;

        [Resolved]
        private NotificationDisplay notificationDisplay { get; set; } = null!;

        [Resolved]
        private OsuDifficultyTuningManager tuningManager { get; set; } = null!;

        [Resolved]
        private CatchDifficultyTuningManager catchTuningManager { get; set; } = null!;

        private FillFlowContainer collectionList = null!;
        private CreateCollectionButton createCollectionButton = null!;

        private OsuSpriteText collectionNameText = null!;
        private FillFlowContainer collectionContainer = null!;
        private FillFlowContainer<ScoreContainer> scoresList = null!;
        private AddScoreButton addScoreButton = null!;
        private readonly Bindable<CollectionSortCriteria> sorting = new Bindable<CollectionSortCriteria>(CollectionSortCriteria.None);

        private Container autobalanceContainer = null!;
        private FillFlowContainer autobalanceParametersContainer = null!;
        private OsuSpriteText autobalanceStatusText = null!;
        private RoundedButton autobalanceRunButton = null!;
        private Container autobalanceProgressBar = null!;
        private Box autobalanceProgressFill = null!;
        private OsuSpriteText autobalanceTimeText = null!;
        private string autobalanceStage = "Ready";
        private readonly Stopwatch autobalanceStopwatch = new Stopwatch();
        private ScheduledDelegate? autobalanceElapsedUpdate;
        private readonly Bindable<AutobalanceRuleset> autobalanceRuleset = new Bindable<AutobalanceRuleset>(AutobalanceRuleset.Osu);
        private readonly Bindable<AutobalanceTarget> autobalanceTarget = new Bindable<AutobalanceTarget>(AutobalanceTarget.Total);
        private readonly Dictionary<IAutobalanceParameter, BindableBool> autobalanceParameterStates = new Dictionary<IAutobalanceParameter, BindableBool>();
        private bool autobalanceRunning;
        private AutobalanceRunner autobalanceRunner = null!;

        private VerboseLoadingLayer loadingLayer = null!;

        private readonly Bindable<Collection?> currentCollection = new Bindable<Collection?>();

        private const string collections_directory = "collections";

        public CollectionsScreen()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ColumnDimensions = new[] { new Dimension(GridSizeMode.Absolute, 250), new Dimension() },
                    RowDimensions = new[] { new Dimension() },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = colourProvider.Background6.Darken(0.2f)
                                    },
                                    new OsuScrollContainer(Direction.Vertical)
                                    {
                                        Name = "Collection List",
                                        RelativeSizeAxes = Axes.Both,
                                        Children = new Drawable[]
                                        {
                                            new FillFlowContainer
                                            {
                                                Padding = new MarginPadding { Left = 10f, Right = 15.0f, Vertical = 5f },
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                                Direction = FillDirection.Vertical,
                                                Spacing = new Vector2(0, 2f),
                                                Children = new Drawable[]
                                                {
                                                    new OsuSpriteText
                                                    {
                                                        Origin = Anchor.TopCentre,
                                                        Anchor = Anchor.TopCentre,
                                                        Height = 20,
                                                        Text = "Collection list"
                                                    },
                                                    collectionList = new FillFlowContainer
                                                    {
                                                        RelativeSizeAxes = Axes.X,
                                                        AutoSizeAxes = Axes.Y,
                                                        Direction = FillDirection.Vertical,
                                                    },
                                                    createCollectionButton = new CreateCollectionButton()
                                                }
                                            }
                                        }
                                    },
                                }
                            },
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = colourProvider.Background6
                                    },
                                    new OsuScrollContainer(Direction.Vertical)
                                    {
                                        Name = "Scores",
                                        RelativeSizeAxes = Axes.Both,
                                        Child = collectionContainer = new FillFlowContainer
                                        {
                                            Padding = new MarginPadding { Left = 10f, Right = 15.0f, Vertical = 5f },
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Vertical,
                                            Spacing = new Vector2(0, 2f),
                                            Alpha = 0,
                                            Children =
                                            [
                                                new Container
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    Children = new Drawable[]
                                                    {
                                                        collectionNameText = new OsuSpriteText
                                                        {
                                                            Origin = Anchor.TopLeft,
                                                            Anchor = Anchor.TopLeft,
                                                            Height = 20
                                                        },
                                                        new OverlaySortTabControl<CollectionSortCriteria>
                                                        {
                                                            Anchor = Anchor.CentreRight,
                                                            Origin = Anchor.CentreRight,
                                                            Margin = new MarginPadding { Right = 20 },
                                                            Current = { BindTarget = sorting }
                                                        }
                                                    }
                                                },
                                                autobalanceContainer = new Container
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    Masking = true,
                                                    CornerRadius = ExtendedLabelledTextBox.CORNER_RADIUS,
                                                    Children = new Drawable[]
                                                    {
                                                        new Box
                                                        {
                                                            RelativeSizeAxes = Axes.Both,
                                                            Colour = colourProvider.Background5,
                                                            Alpha = 0.6f
                                                        },
                                                        new FillFlowContainer
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            Direction = FillDirection.Vertical,
                                                            Spacing = new Vector2(0, 6),
                                                            Padding = new MarginPadding { Horizontal = 10, Vertical = 8 },
                                                            Children = new Drawable[]
                                                            {
                                                                new OsuSpriteText
                                                                {
                                                                    Text = "Autobalance",
                                                                    Font = OsuFont.GetFont(size: 16, weight: FontWeight.SemiBold),
                                                                    Margin = new MarginPadding { Bottom = 2 }
                                                                },
                                                                new OverlaySortTabControl<AutobalanceRuleset>
                                                                {
                                                                    Title = "Ruleset",
                                                                    Current = { BindTarget = autobalanceRuleset }
                                                                },
                                                                new OverlaySortTabControl<AutobalanceTarget>
                                                                {
                                                                    Title = "Target",
                                                                    Current = { BindTarget = autobalanceTarget }
                                                                },
                                                                new OsuSpriteText
                                                                {
                                                                    Text = "Parameters",
                                                                    Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                                                                    Colour = colourProvider.Light2,
                                                                    Margin = new MarginPadding { Top = 6 }
                                                                },
                                                                autobalanceParametersContainer = new FillFlowContainer
                                                                {
                                                                    RelativeSizeAxes = Axes.X,
                                                                    AutoSizeAxes = Axes.Y,
                                                                    Direction = FillDirection.Full,
                                                                    Spacing = new Vector2(10, 6),
                                                                },
                                                                new FillFlowContainer
                                                                {
                                                                    RelativeSizeAxes = Axes.X,
                                                                    AutoSizeAxes = Axes.Y,
                                                                    Direction = FillDirection.Horizontal,
                                                                    Spacing = new Vector2(10, 0),
                                                                    Children = new Drawable[]
                                                                    {
                                                                        autobalanceRunButton = new RoundedButton
                                                                        {
                                                                            Width = 160,
                                                                            Height = 40,
                                                                            Text = "Auto-balance",
                                                                            Action = runAutobalance,
                                                                            BackgroundColour = colourProvider.Background1
                                                                        },
                                                                        autobalanceStatusText = new OsuSpriteText
                                                                        {
                                                                            Anchor = Anchor.CentreLeft,
                                                                            Origin = Anchor.CentreLeft,
                                                                            Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                                                                            Colour = colourProvider.Light2,
                                                                            Text = "Ready"
                                                                        }
                                                                    }
                                                                },
                                                                autobalanceProgressBar = new Container
                                                                {
                                                                    RelativeSizeAxes = Axes.X,
                                                                    Height = 6,
                                                                    Masking = true,
                                                                    CornerRadius = 3,
                                                                    Margin = new MarginPadding { Top = 4 },
                                                                    Children = new Drawable[]
                                                                    {
                                                                        new Box
                                                                        {
                                                                            RelativeSizeAxes = Axes.Both,
                                                                            Colour = colourProvider.Background6.Lighten(0.1f),
                                                                            Alpha = 0.6f
                                                                        },
                                                                        autobalanceProgressFill = new Box
                                                                        {
                                                                            RelativeSizeAxes = Axes.Both,
                                                                            Anchor = Anchor.CentreLeft,
                                                                            Origin = Anchor.CentreLeft,
                                                                            Width = 0,
                                                                            Height = 1,
                                                                            Colour = colourProvider.Background1
                                                                        }
                                                                    }
                                                                },
                                                                autobalanceTimeText = new OsuSpriteText
                                                                {
                                                                    Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                                                                    Colour = colourProvider.Light2,
                                                                    Text = string.Empty
                                                                }
                                                            }
                                                        }
                                                    }
                                                },
                                                scoresList = new FillFlowContainer<ScoreContainer>
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    Direction = FillDirection.Vertical,
                                                },
                                                addScoreButton = new AddScoreButton()
                                            ]
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                loadingLayer = new VerboseLoadingLayer(true)
                {
                    RelativeSizeAxes = Axes.Both
                }
            };
            sorting.ValueChanged += e => { updateSorting(e.NewValue); };

            currentCollection.ValueChanged += loadCollection;
            createCollectionButton.OnSave += onCollectionAdd;
            addScoreButton.OnAdd += onScoreAdd;
            tuningManager.Current.BindValueChanged(_ =>
            {
                if (currentCollection.Value != null)
                    calculateScores();
            });
            catchTuningManager.Current.BindValueChanged(_ =>
            {
                if (currentCollection.Value != null)
                    calculateScores();
            });

            autobalanceRunner = new AutobalanceRunner(scoreCache, rulesets, configManager);
            autobalanceRuleset.BindValueChanged(_ =>
            {
                if (autobalanceRuleset.Value == AutobalanceRuleset.Catch)
                    autobalanceTarget.Value = AutobalanceTarget.Total;

                createAutobalanceParameterControls();

                if (!autobalanceRunning)
                    updateAutobalanceBaseline();
            }, true);
            autobalanceTarget.BindValueChanged(_ =>
            {
                if (!autobalanceRunning)
                    updateAutobalanceBaseline();
            });

            loadCollectionList();

            if (RuntimeInfo.IsDesktop)
                HotReloadCallbackReceiver.CompilationFinished += _ => Schedule(calculateScores);
        }

        private void onScoreAdd(long scoreId)
        {
            if (currentCollection.Value!.Scores.Contains(scoreId))
            {
                notificationDisplay.Display(new Notification($"Score {scoreId} already exists"));
                return;
            }

            currentCollection.Value.Scores = [.. currentCollection.Value.Scores, scoreId];

            saveCurrentCollection();
        }

        private void onScoreRemove(ExtendedScore score)
        {
            if (score.IsStoredScore)
            {
                currentCollection.Value!.StoredScores = currentCollection.Value.StoredScores?.Where(x => x.Id != score.StoredScoreId).ToArray();
            }
            else
            {
                currentCollection.Value!.Scores = currentCollection.Value.Scores.Where(x => x != (long)score.SoloScore.ID!).ToArray();
            }

            saveCurrentCollection();
        }

        private void loadCollection(ValueChangedEvent<Collection?> obj)
        {
            if (obj.NewValue == null)
            {
                collectionContainer.Hide();
                return;
            }

            obj.NewValue.ExpectedPerformance ??= new Dictionary<string, ExpectedPerformanceValues>();
            collectionNameText.Text = obj.NewValue!.Name;
            collectionContainer.Show();
            resetAutobalanceUi();

            calculateScores();
        }

        private void saveCurrentCollection()
        {
            saveCurrentCollection(true);
        }

        private void saveCurrentCollection(bool recalculateScores)
        {
            if (currentCollection.Value == null)
                return;

            saveCollection(currentCollection.Value, recalculateScores);
        }

        private void saveCollection(Collection collection, bool recalculateScores)
        {
            string path = Path.Combine(collections_directory, collection.FileName);

            File.WriteAllText(path, JsonConvert.SerializeObject(collection));

            if (recalculateScores && collection == currentCollection.Value)
                calculateScores();
        }

        private void calculateScores()
        {
            if (currentCollection.Value == null)
                return;

            scoresList.Clear();

            loadingLayer.Show();

            Task.Run(async () =>
            {
                foreach (long scoreId in currentCollection.Value.Scores)
                {
                    var score = await scoreCache.GetScore(scoreId).ConfigureAwait(false);
                    if (score == null)
                        continue;

                    var rulesetInfo = rulesets.GetRuleset(score.RulesetID)!;
                    var rulesetInstance = RulesetHelper.CreateRulesetWithTuning(rulesetInfo, tuningManager, catchTuningManager);

                    var working = ProcessorWorkingBeatmap.FromFileOrId(score.BeatmapID.ToString(), cachePath: configManager.GetBindable<string>(Settings.CachePath).Value);

                    Mod[] mods = score.Mods.Select(x => x.ToMod(rulesetInstance)).ToArray();

                    var scoreInfo = score.ToScoreInfo(rulesets, working.BeatmapInfo);

                    var parsedScore = new ProcessorScoreDecoder(working).Parse(scoreInfo);

                    var difficultyCalculator = RulesetHelper.GetExtendedDifficultyCalculator(rulesetInfo, working,
                        tuningManager.Current.Value, catchTuningManager.Current.Value);
                    var difficultyAttributes = difficultyCalculator.Calculate(mods);
                    var performanceCalculator = rulesetInstance.CreatePerformanceCalculator();
                    if (performanceCalculator == null)
                        continue;

                    var perfAttributes = performanceCalculator.Calculate(parsedScore.ScoreInfo, difficultyAttributes);
                    Schedule(() =>
                    {
                        var scoreContainer = new ScoreContainer(
                            new ExtendedScore(score, difficultyAttributes, perfAttributes),
                            currentCollection.Value!.ExpectedPerformance,
                            () => saveCollection(currentCollection.Value!, false));
                        scoreContainer.OnDelete += onScoreRemove;

                        scoresList.Add(scoreContainer);
                    });
                }

                if (currentCollection.Value.StoredScores != null)
                {
                    foreach (var storedScore in currentCollection.Value.StoredScores)
                    {
                        var rulesetInfo = rulesets.GetRuleset(storedScore.RulesetID)!;
                        var rulesetInstance = RulesetHelper.CreateRulesetWithTuning(rulesetInfo, tuningManager, catchTuningManager);

                        var working = ProcessorWorkingBeatmap.FromFileOrId(storedScore.BeatmapID.ToString(), cachePath: configManager.GetBindable<string>(Settings.CachePath).Value);

                        var soloScore = storedScore.ToSoloScoreInfo(working);

                        Mod[] mods = soloScore.Mods.Select(x => x.ToMod(rulesetInstance)).ToArray();

                        var scoreInfo = soloScore.ToScoreInfo(rulesets, working.BeatmapInfo);

                        var parsedScore = new ProcessorScoreDecoder(working).Parse(scoreInfo);

                        var difficultyCalculator = RulesetHelper.GetExtendedDifficultyCalculator(rulesetInfo, working,
                            tuningManager.Current.Value, catchTuningManager.Current.Value);
                        var difficultyAttributes = difficultyCalculator.Calculate(mods);
                        var performanceCalculator = rulesetInstance.CreatePerformanceCalculator();
                        if (performanceCalculator == null)
                            continue;

                        var perfAttributes = performanceCalculator.Calculate(parsedScore.ScoreInfo, difficultyAttributes);
                        Schedule(() =>
                        {
                            var scoreContainer = new ScoreContainer(
                                new ExtendedScore(soloScore, difficultyAttributes, perfAttributes, storedScore.Id),
                                currentCollection.Value!.ExpectedPerformance,
                                saveCurrentCollection);
                            scoreContainer.OnDelete += onScoreRemove;

                            scoresList.Add(scoreContainer);
                        });
                    }
                }
            }).ContinueWith(t =>
            {
                Logger.Log(t.Exception?.ToString(), level: LogLevel.Error);
                notificationDisplay.Display(new Notification(t.Exception?.Flatten().Message ?? "Failed to calculate collection"));
            }, TaskContinuationOptions.OnlyOnFaulted).ContinueWith(t =>
            {
                Schedule(() =>
                {
                    updateSorting(sorting.Value);
                    loadingLayer.Hide();

                    if (!autobalanceRunning)
                        updateAutobalanceBaseline();
                });
            }, TaskContinuationOptions.None);
        }

        private void onCollectionAdd(string name)
        {
            string fileName = RandomNumberGenerator.GetString(choices: "abcdefghijklmnopqrstuvwxyz0123456789", length: 16) + ".json";

            var collection = new Collection
            {
                Name = name,
                FileName = fileName,
                Scores = []
            };

            string path = Path.Combine(collections_directory, fileName);

            File.WriteAllText(path, JsonConvert.SerializeObject(collection));

            loadCollectionList();
        }

        private void loadCollectionList()
        {
            if (!Directory.Exists(collections_directory))
            {
                Directory.CreateDirectory(collections_directory);

                return; // nothing to load
            }

            collectionList.Clear();

            var collections = new List<Collection>();

            foreach (string collectionFile in Directory.EnumerateFiles(collections_directory))
            {
                var deserializedCollection = JsonConvert.DeserializeObject<Collection>(File.ReadAllText(collectionFile));

                if (deserializedCollection != null)
                {
                    collections.Add(deserializedCollection);
                }
            }

            foreach (var collection in collections.OrderBy(x => x.Name))
            {
                var collectionButton = new CollectionButton(collection, currentCollection);
                collectionList.Add(collectionButton);

                collectionButton.OnDelete += onCollectionDelete;
            }
        }

        private void onCollectionDelete(Collection collection)
        {
            dialogOverlay.Push(new ConfirmDialog("", () =>
            {
                if (collection == currentCollection.Value)
                    currentCollection.Value = null;

                File.Delete(Path.Combine(collections_directory, collection.FileName));

                loadCollectionList();
            })
            {
                HeaderText = DialogStrings.DeletionHeaderText,
                Icon = FontAwesome.Solid.Trash,
                BodyText = collection.Name
            });
        }

        private void updateSorting(CollectionSortCriteria sortCriteria)
        {
            if (!scoresList.Children.Any())
                return;

            if (sortCriteria == CollectionSortCriteria.None)
            {
                int onlineCount = currentCollection.Value!.Scores.Length;

                for (int i = 0; i < scoresList.Count; i++)
                {
                    var container = scoresList[i];

                    if (container.Score.IsStoredScore)
                    {
                        var storedScores = currentCollection.Value.StoredScores;
                        int storedIndex = storedScores != null ? Array.FindIndex(storedScores, s => s.Id == container.Score.StoredScoreId) : 0;
                        scoresList.SetLayoutPosition(container, onlineCount + storedIndex);
                    }
                    else
                    {
                        scoresList.SetLayoutPosition(container, Array.IndexOf(currentCollection.Value.Scores, (long)container.Score.SoloScore.ID!));
                    }
                }

                return;
            }

            ScoreContainer[] sortedScores;

            switch (sortCriteria)
            {
                case CollectionSortCriteria.Live:
                    sortedScores = scoresList.Children.OrderByDescending(x => x.Score.LivePP).ToArray();
                    break;

                case CollectionSortCriteria.Local:
                    sortedScores = scoresList.Children.OrderByDescending(x => x.Score.PerformanceAttributes?.Total).ToArray();
                    break;

                case CollectionSortCriteria.Difference:
                    sortedScores = scoresList.Children.OrderByDescending(x => x.Score.PerformanceAttributes?.Total - x.Score.LivePP).ToArray();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(sortCriteria), sortCriteria, null);
            }

            for (int i = 0; i < sortedScores.Length; i++)
            {
                scoresList.SetLayoutPosition(sortedScores[i], i);
            }
        }

        private void createAutobalanceParameterControls()
        {
            autobalanceParametersContainer.Clear();
            autobalanceParameterStates.Clear();

            foreach (var parameter in AutobalanceRunner.GetParameters(autobalanceRuleset.Value))
            {
                var bindable = new BindableBool { Value = parameter.DefaultEnabled };
                autobalanceParameterStates[parameter] = bindable;

                autobalanceParametersContainer.Add(new Container
                {
                    Width = 230,
                    AutoSizeAxes = Axes.Y,
                    Child = new ExtendedOsuCheckbox
                    {
                        RelativeSizeAxes = Axes.X,
                        Padding = new MarginPadding(4),
                        Current = { BindTarget = bindable },
                        LabelText = parameter.Label,
                        TextColour = colourProvider.Light2
                    }
                });
            }
        }

        private void runAutobalance()
        {
            if (autobalanceRunning)
                return;

            if (currentCollection.Value == null)
            {
                notificationDisplay.Display(new Notification("Select a collection first."));
                return;
            }

            var selectedParameters = autobalanceParameterStates
                                     .Where(kv => kv.Value.Value)
                                     .Select(kv => kv.Key)
                                     .ToArray();

            if (selectedParameters.Length == 0)
            {
                notificationDisplay.Display(new Notification("Select at least one tuning parameter."));
                return;
            }

            setAutobalanceState(true, "Preparing...");

            var collection = currentCollection.Value;
            var target = autobalanceTarget.Value;

            if (autobalanceRuleset.Value == AutobalanceRuleset.Osu)
            {
                var osuParameters = selectedParameters.Cast<AutobalanceParameter<OsuDifficultyConstants>>().ToArray();
                autobalanceRunner.RunAsync(collection, target, osuParameters, tuningManager.Current.Value, onAutobalanceProgress)
                                 .ContinueWith(t => handleAutobalanceResult(t, tuning => tuningManager.Current.Value = tuning), TaskContinuationOptions.None);
            }
            else
            {
                var catchParameters = selectedParameters.Cast<AutobalanceParameter<CatchDifficultyConstants>>().ToArray();
                autobalanceRunner.RunCatchAsync(collection, target, catchParameters, catchTuningManager.Current.Value, onAutobalanceProgress)
                                 .ContinueWith(t => handleAutobalanceResult(t, tuning => catchTuningManager.Current.Value = tuning), TaskContinuationOptions.None);
            }
        }

        private void handleAutobalanceResult<TTuning>(Task<AutobalanceResult<TTuning>> task, Action<TTuning> applyTuning)
        {
            if (task.Exception != null)
                Logger.Log(task.Exception.ToString(), level: LogLevel.Error);

            Schedule(() =>
            {
                loadingLayer.Hide();

                AutobalanceResult<TTuning> result = task.IsFaulted ? AutobalanceResult<TTuning>.Failure("Autobalance failed.") : task.GetAwaiter().GetResult();

                if (task.IsFaulted || result.IsFailure)
                {
                    string message = task.IsFaulted
                        ? task.Exception?.Flatten().Message ?? "Autobalance failed."
                        : result.ErrorMessage ?? "Autobalance failed.";

                    notificationDisplay.Display(new Notification(message));
                    setAutobalanceState(false, "Failed");
                    return;
                }

                applyTuning(result.Tuning!);
                setAutobalanceProgress(1);
                setAutobalanceState(false, $"RMSE {result.Rmse:0.##}pp ({result.SampleCount} scores)");
            });
        }

        private void resetAutobalanceUi()
        {
            autobalanceStage = "Ready";
            autobalanceStatusText.Text = autobalanceStage;
            autobalanceTimeText.Text = string.Empty;
            setAutobalanceProgress(0);
        }

        private void updateAutobalanceBaseline()
        {
            if (currentCollection.Value == null || !scoresList.Children.Any())
            {
                autobalanceStatusText.Text = "Ready";
                return;
            }

            var expectedPerformance = currentCollection.Value.ExpectedPerformance;

            if (expectedPerformance == null || expectedPerformance.Count == 0)
            {
                autobalanceStatusText.Text = "Ready";
                return;
            }

            var target = autobalanceTarget.Value;
            int targetRulesetId = autobalanceRuleset.Value == AutobalanceRuleset.Catch ? 2 : 0;
            var pairs = new List<(double actual, double expected, double weight)>();

            foreach (var container in scoresList.Children)
            {
                var score = container.Score;

                if (score.SoloScore.RulesetID != targetRulesetId)
                    continue;

                string key = score.IsStoredScore ? score.StoredScoreId! : score.SoloScore.ID.ToString()!;

                double? weight = currentCollection.Value.StoredScores?.Where(s => s.Id == key).First().Weighting;

                if (!expectedPerformance.TryGetValue(key, out var expectedValues))
                    continue;

                if (!AutobalanceRunner.TryGetExpectedValue(expectedValues, target, out double expectedValue))
                    continue;

                double? actualValue = getAutobalanceTargetValue(score.PerformanceAttributes, target);

                if (actualValue == null)
                    continue;

                pairs.Add((actualValue.Value, expectedValue, weight ?? 1));
            }

            if (pairs.Count == 0)
            {
                autobalanceStatusText.Text = "Ready";
                return;
            }

            double mse = pairs.Sum(p => (p.actual - p.expected) * (p.actual - p.expected) * p.weight) / (pairs.Sum(p => p.weight));
            double rmse = Math.Sqrt(mse);

            if (pairs.Count < 2)
            {
                autobalanceStatusText.Text = $"Ready — RMSE {rmse:0.##}pp (1 score)";
                return;
            }

            double spearman = computeSpearmanCorrelation(pairs);
            autobalanceStatusText.Text = $"Ready — RMSE {rmse:0.##}pp, \u03c1={spearman:0.###} ({pairs.Count} scores)";
        }

        private static double? getAutobalanceTargetValue(PerformanceAttributes? attributes, AutobalanceTarget target)
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

        private static double computeSpearmanCorrelation(List<(double actual, double expected, double weight)> pairs)
        {
            int n = pairs.Count;

            if (n < 2)
                return 0;

            double[] actualRanks = computeRanks(pairs.Select(p => p.actual).ToArray());
            double[] expectedRanks = computeRanks(pairs.Select(p => p.expected).ToArray());

            double sumDSq = 0;

            for (int i = 0; i < n; i++)
            {
                double d = actualRanks[i] - expectedRanks[i];
                sumDSq += d * d;
            }

            return 1.0 - 6.0 * sumDSq / (n * ((double)n * n - 1));
        }

        private static double[] computeRanks(double[] values)
        {
            int n = values.Length;
            var indexed = values.Select((v, i) => (value: v, index: i)).OrderBy(x => x.value).ToArray();
            double[] ranks = new double[n];

            int i = 0;

            while (i < n)
            {
                int j = i;

                while (j < n - 1 && Math.Abs(indexed[j + 1].value - indexed[j].value) < 1e-9)
                    j++;

                double avgRank = (i + j) / 2.0 + 1;

                for (int k = i; k <= j; k++)
                    ranks[indexed[k].index] = avgRank;

                i = j + 1;
            }

            return ranks;
        }

        private void setAutobalanceProgress(double progress)
        {
            autobalanceProgressFill.Width = (float)Math.Clamp(progress, 0, 1);
        }

        private void updateAutobalanceElapsed()
        {
            if (!autobalanceRunning)
                return;

            autobalanceTimeText.Text = $"Elapsed {formatElapsed(autobalanceStopwatch.Elapsed)}";
        }

        private static string formatElapsed(TimeSpan elapsed)
        {
            if (elapsed.TotalHours >= 1)
                return elapsed.ToString(@"h\:mm\:ss");
            if (elapsed.TotalMinutes >= 1)
                return elapsed.ToString(@"m\:ss\.f");
            return $"{elapsed.TotalSeconds:0.0}s";
        }

        private void onAutobalanceProgress(AutobalanceProgress progress)
        {
            Schedule(() =>
            {
                if (!autobalanceRunning)
                    return;

                setAutobalanceProgress(progress.Value);

                if (!string.IsNullOrEmpty(progress.Stage))
                    autobalanceStage = progress.Stage;

                string percent = $"{progress.Value:0%}";

                if (progress.Total.HasValue && progress.Total.Value > 0 && progress.Completed.HasValue)
                    autobalanceStatusText.Text = $"{autobalanceStage} {progress.Completed.Value}/{progress.Total.Value} ({percent})";
                else
                    autobalanceStatusText.Text = $"{autobalanceStage} ({percent})";
            });
        }

        private void setAutobalanceState(bool running, string status)
        {
            autobalanceRunning = running;
            autobalanceRunButton.Enabled.Value = !running;
            autobalanceStage = status;
            autobalanceStatusText.Text = status;

            if (running)
            {
                autobalanceStopwatch.Restart();
                autobalanceTimeText.Text = "Elapsed 0.0s";
                setAutobalanceProgress(0);

                autobalanceElapsedUpdate?.Cancel();
                autobalanceElapsedUpdate = Scheduler.AddDelayed(updateAutobalanceElapsed, 100, true);

                loadingLayer.Show();
            }
            else
            {
                autobalanceElapsedUpdate?.Cancel();
                autobalanceElapsedUpdate = null;

                autobalanceStopwatch.Stop();
                autobalanceTimeText.Text = $"Took {formatElapsed(autobalanceStopwatch.Elapsed)}";
            }
        }
    }
}
