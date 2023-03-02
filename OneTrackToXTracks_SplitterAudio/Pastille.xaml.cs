using System;
using System.Collections.Generic;
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

namespace OneTrackToXTracks_SplitterAudio
{
    public partial class Pastille : UserControl
    {
        public Pastille()
        {
            InitializeComponent();
        }

        public void Set(string text, Brush stroke_color, Brush fill_color, double stroke_thickness)
        {
            _tbk.Text = text;
            _eli.Stroke = stroke_color;
            _eli.StrokeThickness = stroke_thickness;
            _eli.Fill = fill_color;
        }
    }
}
