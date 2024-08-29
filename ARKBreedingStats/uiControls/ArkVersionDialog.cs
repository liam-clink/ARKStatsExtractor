using System.Drawing;
using System.Reflection.Emit;
using System.Windows.Forms;

namespace ARKBreedingStats.uiControls
{
    internal class ArkVersionDialog : Form
    {
        public Ark.Game GameVersion;
        public bool UseSelectionAsDefault => _cbUseAsDefault?.Checked != false;
        private CheckBox _cbUseAsDefault;

        public ArkVersionDialog()
        {
            float scaleFactor = this.DeviceDpi / 96f; // 96 DPI is the base DPI

            StartPosition = FormStartPosition.CenterParent;
            Text = Utils.ApplicationNameVersion;
            FormBorderStyle = FormBorderStyle.FixedToolWindow;

            int margin = (int)(20 * scaleFactor);
            int buttonWidth = (int)(160 * scaleFactor);
            int buttonHeight = (int)(50 * scaleFactor);

            Width = 3 * margin + 2 * buttonWidth + (int)(15 * scaleFactor);
            Height = 5 * margin + buttonHeight + (int)(30 * scaleFactor);

            var lb = new System.Windows.Forms.Label
            {
                Text = "Game version of new library",
                Width = Width,
                TextAlign = ContentAlignment.TopCenter,
                Top = margin,
            };
            Size textSize = TextRenderer.MeasureText(lb.Text, lb.Font);
            lb.Height = textSize.Height + 20;

            var btAse = new Button
            {
                Text = "ARK: Survival Evolved",
                Width = buttonWidth,
                Height = buttonHeight,
                Top = 2 * margin,
                Left = margin
            };
            var btAsa = new Button
            {
                Text = "ARK: Survival Ascended",
                Width = buttonWidth,
                Height = buttonHeight,
                Top = 2 * margin,
                Left = 2 * margin + buttonWidth
            };
            btAse.Click += (s, e) => Close(Ark.Game.Ase);
            btAsa.Click += (s, e) => Close(Ark.Game.Asa);

            _cbUseAsDefault = new CheckBox
            {
                Text = "Remember selection (can be changed in the settings)",
                AutoSize = true,
                Left = margin,
                Top = 3 * margin + buttonHeight
            };

            Controls.AddRange(new Control[] { btAse, btAsa, _cbUseAsDefault, lb });
        }

        public ArkVersionDialog(Form owner) : this()
        {
            Owner = owner;
        }

        private void Close(Ark.Game arkGame)
        {
            GameVersion = arkGame;
            Close();
        }
    }
}
