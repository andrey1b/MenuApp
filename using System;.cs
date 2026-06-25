using System;
using System.Windows.Forms;

namespace MenuApp
{
    public class MainForm : Form
    {
        private TabControl tabControl;
        private TabPage tabPage1;
        private TabPage tabPage2;

        public MainForm()
        {
            // Настройки окна
            this.Text = "Интерактивное меню";
            this.Width = 600;
            this.Height = 400;

            // Создаём TabControl
            tabControl = new TabControl();
            tabControl.Dock = DockStyle.Fill;

            // Создаём вкладки
            tabPage1 = new TabPage("Меню 1");
            tabPage2 = new TabPage("Меню 2");

            // Добавляем вкладки в TabControl
            tabControl.TabPages.Add(tabPage1);
            tabControl.TabPages.Add(tabPage2);

            // Добавляем TabControl в форму
            this.Controls.Add(tabControl);
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.Run(new MainForm());
        }
    }
}
