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
    /// Interaction logic for NumericControl_MultiDigit.xaml
    /// </summary>
    public partial class NCMultiDigit : UserControl, INotifyPropertyChanged
    {
        #region Events
        // ====================================================================
        /// 
        /// \addtogroup Events
        /// 
        // ====================================================================
        //@{        

        public event EventHandler<EventArgs_NCMultiDigitValueChanged> ValueChanged;

        //@}
        // ====================================================================
        #endregion // Events


        #region Instance Variables and Properties
        // ====================================================================
        /// 
        /// \addtogroup Instance Variables and Properties
        /// 
        // ====================================================================
        //@{

        public List<NCDigit> DigitControls = new List<NCDigit>();


        #region INotifyPropertyChanged Interface
        // ====================================================================
        /// 
        /// \addtogroup INotifyPropertyChanged Interface
        /// 
        // ====================================================================
        //@{

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

        //@}
        // ====================================================================
        #endregion // INotifyPropertyChanged Interface


        #region Value, Min, Max, N_DigitControls, Format
        // ====================================================================
        /// 
        /// \addtogroup Value, Min, Max, N_DigitControls, Format
        /// 
        // ====================================================================
        //@{

        private const int Base = 10;

        private int _value = 0;
        public int Value
        {
            get
            {
                return _value;
            }
            set
            {
                _value = value;
                Changed("Value");
            }
        }

        private int _min = 0;
        public int Min
        {
            get { return _min; }
            set
            {
                _min = value;
                Changed("Min");
            }
        }

        private int _max = 10;
        public int Max
        {
            get { return _max; }
            set
            {
                _max = value;
                Changed("Max");
            }
        }

        private string _formatString = "0";
        public string FormatString
        {
            get { return _formatString; }
            set
            {
                _formatString = value;
                SetDigitControlsFormat(_formatString);
            }
        }

        private int _spaceWidth = 6;
        public int SpaceWidth
        {
            get { return _spaceWidth; }
            set
            {
                _spaceWidth = value;
                Changed("SpaceWidth");
                SetDigitControlsFormat(_formatString);
            }
        }

        private int _spaceHeight = 6;
        public int SpaceHeight
        {
            get { return _spaceHeight; }
            set
            {
                _spaceHeight = value;
                Changed("SpaceHeight");
                SetDigitControlsFormat(_formatString);
            }
        }

        private VerticalAlignment _spaceVerticalAlignment = VerticalAlignment.Center;
        public VerticalAlignment SpaceVerticalAlignment
        {
            get { return _spaceVerticalAlignment; }
            set
            {
                _spaceVerticalAlignment = value;
                Changed("SpaceVerticalAlignment");
                SetDigitControlsFormat(_formatString);
            }
        }

        //@}
        // ====================================================================
        #endregion // Value, Min, Max, N_DigitControls, Format


        #region Colors
        // ====================================================================
        /// 
        /// \addtogroup Colors 
        /// 
        // ====================================================================
        //@{

        private Brush _background = Brushes.White;
        public Brush NC_Background
        {
            get { return _background; }
            set
            {
                _background = value;
                foreach (NCDigit dc in DigitControls) dc.NC_Background = value;
                Changed("NC_Background");
            }
        }

        private Brush _foreground = Brushes.Black;
        public Brush NC_Foreground
        {
            get { return _foreground; }
            set
            {
                _foreground = value;
                foreach (NCDigit dc in DigitControls) dc.NC_Foreground = value;
                Changed("NC_Foreground");
            }
        }

        private Thickness _digitBorderThickness = new Thickness(0);
        public Thickness NC_DigitBorderThickness
        {
            get { return _digitBorderThickness; }
            set
            {
                _digitBorderThickness = value;
                foreach (NCDigit dc in DigitControls) dc.NC_BorderThickness = value;
                Changed(nameof(NC_DigitBorderThickness));
            }
        }

        private Brush _digitBorderBrush = Brushes.LightGray;
        public Brush NC_DigitBorderBrush
        {
            get { return _digitBorderBrush; }
            set
            {
                _digitBorderBrush = value;
                foreach (NCDigit dc in DigitControls) dc.NC_BorderBrush = value;
                Changed(nameof(NC_DigitBorderBrush));
            }
        }

        private Brush _limit_Background = Brushes.Salmon;
        public Brush NC_Limit_Background
        {
            get { return _limit_Background; }
            set { _limit_Background = value; Changed("NC_Limit_Background"); }
        }

        private Brush _limit_Foreground = Brushes.Black;
        public Brush NC_Limit_Foreground
        {
            get { return _limit_Foreground; }
            set { _limit_Foreground = value; Changed("NC_Limit_Foreground"); }
        }

        private Brush _spaceForeground = Brushes.Gray;
        public Brush SpaceForeground
        {
            get { return _spaceForeground; }
            set 
            {
                _spaceForeground = value; 
                Changed("SpaceForeground");
                SetDigitControlsFormat(_formatString);
            }
        }

        private Brush _spaceBackground = Brushes.Gray;
        public Brush SpaceBackground
        {
            get { return _spaceBackground; }
            set
            {
                _spaceBackground = value;
                Changed("SpaceBackground");
                SetDigitControlsFormat(_formatString);
            }
        }


        //@}
        // ====================================================================
        #endregion // Colors 


        //@}
        // ====================================================================
        #endregion // Instance Variables and Properties


        #region Instantiation
        // ====================================================================
        /// 
        /// \addtogroup Instantiation
        /// 
        // ====================================================================
        //@{

        public NCMultiDigit()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void SetDigitControlsFormat(string Format)
        {
            DigitsPanel.Children.Clear();
            DigitControls = new List<NCDigit>();
            int increment = 1;

            for (int z = 0; z < Format.Length; z++)
            {
                char c = Format[z];
                if (Char.IsDigit(c))
                {
                    NCDigit digitControl = new NCDigit();
                    increment = increment * Base;
                    digitControl.ValueChanged += new EventHandler<EventArgs_NCDigitValueChanged>(DigitValueChanged);
                    digitControl.NC_Background = this.NC_Background;
                    digitControl.NC_Foreground = this.NC_Foreground;
                    digitControl.NC_BorderThickness = this.NC_DigitBorderThickness;
                    digitControl.NC_BorderBrush = this.NC_DigitBorderBrush;
                    DigitControls.Add(digitControl);
                    DigitsPanel.Children.Add(digitControl);
                }
                else
                {
                    NCSpace space = new NCSpace();
                    space.NC_Character = '-';
                    space.VerticalAlignment = SpaceVerticalAlignment;
                    space.VerticalContentAlignment = SpaceVerticalAlignment;
                    space.NC_Foreground = SpaceForeground;
                    space.NC_Background = SpaceBackground;
                    space.NC_BorderThickness = this.NC_DigitBorderThickness;
                    space.NC_BorderBrush = this.NC_DigitBorderBrush;
                    space.Width = SpaceWidth;
                    space.Height = SpaceHeight;
                    DigitsPanel.Children.Add(space);
                }
            }

            for (int i = 0; i < DigitControls.Count; i++)
            {
                increment = increment / Base;
                DigitControls[i].NC_Increment = increment;
            }

            SetValue(Value);
        }

        //@}
        // ====================================================================
        #endregion // Instantiation


        #region Functions
        // ====================================================================
        /// 
        /// \addtogroup Functions
        /// 
        // ====================================================================
        //@{

        public void DigitValueChanged(object sender, EventArgs e)
        {
            NCDigit digitControl = (NCDigit)sender;
            EventArgs_NCDigitValueChanged args = (EventArgs_NCDigitValueChanged)e;
            int digitControlIndex = DigitControls.IndexOf(digitControl);
            SetValue(Value + args.Increment);
        }

        public void SetValue(int x, bool GenerateEvent = true)
        {
            if ((x > Min) & (x < Max))
            {
                Value = x;
                foreach (NCDigit dc in DigitControls) dc.NC_Background = NC_Background;
                if (ValueChanged != null)
                {
                    if (GenerateEvent) ValueChanged(this, new EventArgs_NCMultiDigitValueChanged(x));
                }
            }
            else
            {
                if (x < Min) x = Min;
                if (x > Max) x = Max;
                Value = x;
                // do NOT apply limit background:
                // foreach (NCDigit dc in DigitControls) dc.NC_Background = NC_Limit_Background;
                if (ValueChanged != null)
                {
                    if (GenerateEvent) ValueChanged(this, new EventArgs_NCMultiDigitValueChanged(x));
                }
            }
            string s = Value.ToString().PadLeft(DigitControls.Count, '0');
            for (int i = 0; i < s.Length; i++) DigitControls[i].NC_Character = s[i];
        }

        //@}
        // ====================================================================
        #endregion // Functions
    }

    public class EventArgs_NCMultiDigitValueChanged : System.EventArgs
    {
        public int Value { get; set; }

        public EventArgs_NCMultiDigitValueChanged(int val)
        {
            Value = val;
        }
    }
}
