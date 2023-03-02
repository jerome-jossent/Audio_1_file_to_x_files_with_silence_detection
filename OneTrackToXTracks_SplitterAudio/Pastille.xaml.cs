using NAudio_JJ;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

namespace OneTrackToXTracks_SplitterAudio
{
    public partial class Pastille : UserControl
    {
        public Silence silence;
        internal int _zindex;
        double stroke_thickness;

        public Pastille()
        {
            InitializeComponent();
        }

        public void Set(string text,
            Brush stroke_color,
            Brush fill_color,
            double stroke_thickness,
            Silence silence,
            int zindex)
        {
            _tbk.Text = text;
            _eli.Stroke = stroke_color;
            _eli.StrokeThickness = stroke_thickness;
            this.stroke_thickness = stroke_thickness;
            _eli.Fill = fill_color;
            this.silence = silence;
            this._zindex = zindex;
        }

        private void _eli_MouseEnter(object sender, MouseEventArgs e)
        {
            _Focus();
        }

        private void _eli_MouseLeave(object sender, MouseEventArgs e)
        {
            _FocusLost();
        }

        public void _Focus()
        {
            _tbk.FontWeight = FontWeights.Bold;
            _eli.StrokeThickness = stroke_thickness * 2;
            System.Windows.Controls.Panel.SetZIndex(this, (int)MainWindow.ZLevelOnCanvas.pastilles);

        }
        public void _FocusLost()
        {
            _tbk.FontWeight = FontWeights.Regular;
            _eli.StrokeThickness = stroke_thickness;
            System.Windows.Controls.Panel.SetZIndex(this, _zindex);
        }
    }
}
