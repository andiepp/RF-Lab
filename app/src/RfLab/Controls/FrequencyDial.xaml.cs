#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace RfLab.Controls
{
    /// <summary>
    /// Interaction logic for FrequencyDial.xaml
    /// </summary>
    public partial class FrequencyDial : UserControl
    {
        public event EventHandler<EventArgs_NCMultiDigitValueChanged> ValueChanged;

        private void TB_Frequency_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return || e.Key == Key.Enter)
            {
                string frequencyText = ((TextBox)sender).Text;
                int f = Frequency;
                bool res = int.TryParse(frequencyText, out f);
                if (res == true) Frequency = f;
                e.Handled = true;
            }
        }

        public int Frequency
        {
            get
            {
                return NCMD_Frequency.Value;
            }
            set
            {
                if (NCMD_Frequency.Value != value)
                {
                    TB_Frequency.Text = value.ToString();
                    NCMD_Frequency.SetValue(value);
                }
            }
        }

        public FrequencyDial()
        {
            InitializeComponent();
            TB_Frequency.Text = NCMD_Frequency.Value.ToString();
            NCMD_Frequency.ValueChanged += FrequencyChanged;
        }

        public void FrequencyChanged(object sender, EventArgs_NCMultiDigitValueChanged args)
        {
            TB_Frequency.Text = NCMD_Frequency.Value.ToString();
            if (ValueChanged != null)
            {
                ValueChanged(this, new EventArgs_NCMultiDigitValueChanged(NCMD_Frequency.Value));
            }
        }
    }
}
