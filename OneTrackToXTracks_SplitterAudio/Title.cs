using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OneTrackToXTracks_SplitterAudio
{
    public class Title
    {
        public int index;
        public string author;
        public string album;
        public string titleraw;

        public string titre
        {
            get
            {
                string _titre = "";
                if (author != null && author != "")
                    _titre += author + " - ";

                if (album != null && album != "")
                    _titre += album + " - ";

                _titre += titleraw;
                return _titre;
            }
        }

        public TimeSpan start;
        public TimeSpan end
        {
            get { return _end; }
            set { _end = value;
                totalTime = end - start;
            }
        }
        TimeSpan _end;

        public TimeSpan totalTime;
        public string fileName;
        public string fullFileName;
        internal object rectangle;
        internal SolidColorBrush brush;
        internal Title_UC uc;

        public Title(TimeSpan start, TimeSpan end)
        {
            this.start = start;
            this.end = end;
        }

        public void SetTitle(string chaine)
        {
            titleraw = chaine;
        }

        public override string ToString()
        {
            return titre + " [" + totalTime.ToString("mm\\:ss") + "]";
        }
    }
}
