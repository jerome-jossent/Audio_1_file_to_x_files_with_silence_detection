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
    public partial class Title_UC : UserControl
    {
        public Title title;

        public Title_UC()
        {
            InitializeComponent();
        }

        public void _Link(Title title)
        {
            this.title = title;
            title.uc = this;

            _grid.Background = title.brush;
            _index.Text = title.index.ToString("00");

            _deb.Value = title.start;
            if (title.start == TimeSpan.Zero)
                _deb.Value = TimeSpan.Zero;
            _fin.Value = title.end;

            _author.Text = title.author;
            _album.Text = title.album;
            _duree.Text = title.totalTime.ToString("mm\\:ss");                       
            _fileName.Text = title.fileName;
        }
    }
}
