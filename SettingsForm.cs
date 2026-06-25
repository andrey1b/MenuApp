using System;
using System.Windows.Forms;
using System.ComponentModel;

namespace MenuApp
{
    public class SettingsForm : Form
    {
        private NumericUpDown numBudget;
        private NumericUpDown numFamily;
        private NumericUpDown numCalNorm;
        private Button btnOk;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public decimal BudgetValue  { get; private set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int FamilyCount { get; private set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int CalorieNorm { get; private set; }

        public SettingsForm(decimal currentBudget, int currentFamily, int currentCalNorm = 2000)
        {
            Text = "Настройки";
            Width = 310;
            Height = 230;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            Label lblBudget = new Label { Text = "Бюджет (грн):", Location = new System.Drawing.Point(20, 20), AutoSize = true };
            numBudget = new NumericUpDown { Location = new System.Drawing.Point(160, 18), Width = 110,
                Minimum = 0, Maximum = 100000, DecimalPlaces = 2, Value = currentBudget };

            Label lblFamily = new Label { Text = "Семья (чел.):", Location = new System.Drawing.Point(20, 60), AutoSize = true };
            numFamily = new NumericUpDown { Location = new System.Drawing.Point(160, 58), Width = 60,
                Minimum = 1, Maximum = 20, Value = currentFamily };

            Label lblCalNorm = new Label { Text = "Норма ккал/чел.:", Location = new System.Drawing.Point(20, 100), AutoSize = true };
            numCalNorm = new NumericUpDown { Location = new System.Drawing.Point(160, 98), Width = 80,
                Minimum = 1000, Maximum = 5000, Increment = 100, Value = currentCalNorm };

            btnOk = new Button { Text = "ОК", Location = new System.Drawing.Point(110, 148), Width = 80 };
            btnOk.Click += BtnOk_Click;

            Controls.Add(lblBudget);   Controls.Add(numBudget);
            Controls.Add(lblFamily);   Controls.Add(numFamily);
            Controls.Add(lblCalNorm);  Controls.Add(numCalNorm);
            Controls.Add(btnOk);
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            BudgetValue = numBudget.Value;
            FamilyCount = (int)numFamily.Value;
            CalorieNorm = (int)numCalNorm.Value;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
