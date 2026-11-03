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
    /// Interaction logic for NCSpace.xaml
    /// </summary>
    public partial class NCSpace : UserControl, INotifyPropertyChanged
    {
        #region INotifyPropertyChanged

        [NonSerialized]
        private PropertyChangedEventHandler _PropertyChanged;

        public virtual event PropertyChangedEventHandler PropertyChanged
        {
            add { _PropertyChanged += value; }
            remove { _PropertyChanged -= value; }
        }

        public void Changed(string propertyName)
        {
            if (_PropertyChanged != null)
            {
                _PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion


        public NCSpace()
        {
            InitializeComponent();
            DataContext = this;
            Up.Visibility = Visibility.Hidden;
            Down.Visibility = Visibility.Hidden;
        }

        public char NC_Character
        {
            get { return ValueTextbox.Text[0]; }
            set { ValueTextbox.Text = value.ToString(); Changed(nameof(NC_Character)); }
        }

        public Brush NC_Background
        {
            get { return ValueTextbox.Background; }
            set { ValueTextbox.Background = value; Changed(nameof(NC_Background)); }
        }

        public Brush NC_Foreground
        {
            get { return ValueTextbox.Foreground; }
            set { ValueTextbox.Foreground = value; Changed(nameof(NC_Foreground)); }
        }

        private Thickness _borderThickness = new Thickness(1);
        public Thickness NC_BorderThickness
        {
            get { return _borderThickness; }
            set { _borderThickness = value; Changed(nameof(NC_BorderThickness)); }
        }

        private Brush _borderBrush = Brushes.Black;
        public Brush NC_BorderBrush
        {
            get { return _borderBrush; }
            set { _borderBrush = value; Changed(nameof(NC_BorderBrush)); }
        }

        private FontFamily _fontFamily = new FontFamily("Calibri");
        public FontFamily NC_FontFamily
        {
            get { return _fontFamily; }
            set { _fontFamily = value; Changed(nameof(NC_FontFamily)); }
        }

        private double _fontSize = 52;
        public double NC_FontSize
        {
            get { return _fontSize; }
            set { _fontSize = value; Changed(nameof(NC_FontSize)); }
        }

        private FontWeight _fontWeight = FontWeights.Normal;
        public FontWeight NC_FontWeight
        {
            get { return _fontWeight; }
            set { _fontWeight = value; Changed(nameof(NC_FontWeight)); }
        }

    }

}
