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
    /// Interaction logic for AmplitudeDial.xaml
    /// </summary>
    public partial class AmplitudeDial : UserControl
    {
        public event EventHandler<EventArgs_NCMultiDigitValueChanged> ValueChanged;

        private void TB_Amplitude_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return || e.Key == Key.Enter)
            {
                string amplitudeText = ((TextBox)sender).Text;
                int a = Amplitude;
                bool res = int.TryParse(amplitudeText, out a);
                if (res == true) Amplitude = a;
                e.Handled = true;
            }
        }
        public int Amplitude
        {
            get 
            { 
                return NCMD_Amplitude.Value; 
            }
            set 
            {
                if (NCMD_Amplitude.Value != value)
                {
                    TB_Amplitude.Text = value.ToString();
                    NCMD_Amplitude.SetValue(value);
                }
            }
        }

        public AmplitudeDial()
        {
            InitializeComponent();
            NCMD_Amplitude.ValueChanged += AmplitudeChanged;
        }

        public void AmplitudeChanged(object sender, EventArgs_NCMultiDigitValueChanged args)
        {
            TB_Amplitude.Text = NCMD_Amplitude.Value.ToString();
            if (ValueChanged != null)
            {
                ValueChanged(this, new EventArgs_NCMultiDigitValueChanged(NCMD_Amplitude.Value));
            }
        }
    }
}
