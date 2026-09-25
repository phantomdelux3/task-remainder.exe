using System.Drawing;
using System.Windows.Forms;

namespace MarqueeReminder.Forms
{
    /// <summary>A value shown in a combo box with a friendly caption.</summary>
    internal sealed class Choice<T>
    {
        public Choice(string caption, T value)
        {
            Caption = caption;
            Value = value;
        }

        public string Caption { get; }

        public T Value { get; }

        public override string ToString() => Caption;
    }

    /// <summary>Small factory helpers so the hand-built forms stay short and consistent.</summary>
    internal static class Ui
    {
        public static Label Label(string text) => new Label
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 6, 6, 6),
        };

        public static Label Hint(string text)
        {
            var label = Label(text);
            label.ForeColor = SystemColors.GrayText;
            return label;
        }

        public static NumericUpDown Number(int minimum, int maximum, int width = 80) => new NumericUpDown
        {
            Minimum = minimum,
            Maximum = maximum,
            Width = width,
            Anchor = AnchorStyles.Left,
            TextAlign = HorizontalAlignment.Right,
        };

        public static ComboBox DropDown(params object[] items)
        {
            var combo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor = AnchorStyles.Left,
                Width = 200,
            };
            combo.Items.AddRange(items);
            return combo;
        }

        public static TableLayoutPanel Grid(int columns) => new TableLayoutPanel
        {
            ColumnCount = columns,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
        };

        public static FlowLayoutPanel Row(params Control[] controls)
        {
            var row = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Margin = Padding.Empty,
                Anchor = AnchorStyles.Left,
            };
            row.Controls.AddRange(controls);
            return row;
        }

        public static GroupBox Group(string title, Control content)
        {
            var group = new GroupBox
            {
                Text = title,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
            };
            group.Controls.Add(content);
            return group;
        }

        public static Button Button(string text) => new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(90, 0),
            UseVisualStyleBackColor = true,
        };

        /// <summary>Adds one row to a grid; the control spans the remaining columns.</summary>
        public static void AddRow(TableLayoutPanel grid, Control label, Control control)
        {
            var row = grid.RowCount++;
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.Controls.Add(label, 0, row);
            grid.Controls.Add(control, 1, row);
            grid.SetColumnSpan(control, grid.ColumnCount - 1);
        }
    }
}
