// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace PerformanceCalculatorGUI.Configuration
{
    public sealed class DifficultyTuningParameter<TConstants>
    {
        public string UiLabel { get; }
        public string AutobalanceLabel { get; }
        public bool IsInteger { get; }
        public double AutobalanceMinValue { get; }
        public double? AutobalanceMaxValue { get; }
        public bool DefaultEnabled { get; }
        public Func<TConstants, double> Getter { get; }
        public Func<TConstants, double, TConstants> Setter { get; }

        private DifficultyTuningParameter(string uiLabel, string autobalanceLabel, bool isInteger, double autobalanceMinValue, double? autobalanceMaxValue,
                                          bool defaultEnabled,
                                          Func<TConstants, double> getter, Func<TConstants, double, TConstants> setter)
        {
            UiLabel = uiLabel;
            AutobalanceLabel = autobalanceLabel;
            IsInteger = isInteger;
            AutobalanceMinValue = autobalanceMinValue;
            AutobalanceMaxValue = autobalanceMaxValue;
            DefaultEnabled = defaultEnabled;
            Getter = getter;
            Setter = setter;
        }

        public static DifficultyTuningParameter<TConstants> ForDouble(string uiLabel, string autobalanceLabel, Func<TConstants, double> getter,
                                                                      Func<TConstants, double, TConstants> setter, bool defaultEnabled,
                                                                      double autobalanceMinValue = 0.01, double? autobalanceMaxValue = null)
            => new DifficultyTuningParameter<TConstants>(uiLabel, autobalanceLabel, false, autobalanceMinValue, autobalanceMaxValue, defaultEnabled, getter, setter);

        public static DifficultyTuningParameter<TConstants> ForInt(string uiLabel, string autobalanceLabel, Func<TConstants, int> getter,
                                                                   Func<TConstants, int, TConstants> setter, bool defaultEnabled,
                                                                   int autobalanceMinValue = 1, int? autobalanceMaxValue = null)
            => new DifficultyTuningParameter<TConstants>(uiLabel, autobalanceLabel, true, autobalanceMinValue, autobalanceMaxValue, defaultEnabled,
                                                         t => getter(t), (t, v) => setter(t, (int)v));
    }

    public sealed class DifficultyTuningSection<TConstants>
    {
        public string Title { get; }
        public IReadOnlyList<DifficultyTuningParameter<TConstants>> Parameters { get; }

        public DifficultyTuningSection(string title, params DifficultyTuningParameter<TConstants>[] parameters)
        {
            Title = title;
            Parameters = parameters;
        }
    }
}
