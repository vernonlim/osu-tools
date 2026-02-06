// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using osu.Framework.Bindables;
using osu.Game.Graphics.UserInterface;

namespace PerformanceCalculatorGUI.Components.TextBoxes
{
    public partial class NullableLabelledFractionalNumberBox : ExtendedLabelledTextBox
    {
        private partial class NullableFractionalNumberBox : OsuTextBox
        {
            public Bindable<double?> Value { get; } = new Bindable<double?>();

            public double? MaxValue { get; set; }
            public double? MinValue { get; set; }

            protected override bool CanAddCharacter(char character) =>
                char.IsAsciiDigit(character) || character == CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];

            protected override void OnUserTextAdded(string added)
            {
                base.OnUserTextAdded(added);
                updateValue(deleteOnInvalid: true);
            }

            protected override void OnUserTextRemoved(string removed)
            {
                base.OnUserTextRemoved(removed);
                updateValue(deleteOnInvalid: false);
            }

            private void updateValue(bool deleteOnInvalid)
            {
                if (string.IsNullOrEmpty(Text))
                {
                    Value.Value = null;
                    return;
                }

                if (!double.TryParse(Text, out double parsed))
                {
                    handleInvalid(deleteOnInvalid);
                    return;
                }

                if (MinValue.HasValue && parsed < MinValue.Value)
                {
                    handleInvalid(deleteOnInvalid);
                    return;
                }

                if (MaxValue.HasValue && parsed > MaxValue.Value)
                {
                    handleInvalid(deleteOnInvalid);
                    return;
                }

                Value.Value = parsed;
            }

            private void handleInvalid(bool deleteOnInvalid)
            {
                if (deleteOnInvalid)
                    DeleteBy(-1);

                Value.Value = null;
                NotifyInputError();
            }
        }

        protected override OsuTextBox CreateTextBox() => new NullableFractionalNumberBox();

        public Bindable<double?> Value => ((NullableFractionalNumberBox)Component).Value;

        public double? MaxValue
        {
            set => ((NullableFractionalNumberBox)Component).MaxValue = value;
        }

        public double? MinValue
        {
            set => ((NullableFractionalNumberBox)Component).MinValue = value;
        }
    }
}
