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
        public TimeSpan end;

        public TimeSpan totalTime;
        public string fileName;
        public string fullFileName;
        internal object rectangle;
        internal SolidColorBrush brush;
        internal Title_UC uc;

        public Title(string chaine)
        {
            //0:00:00  Progressive Funk  ( Impact Inc. - Vectorball )\r\n

            int pos_prem_espace = chaine.IndexOf(' ');

            string temps_s = chaine.Substring(0, pos_prem_espace);
            start = TimeSpan.Parse(temps_s);
            titleraw = chaine.Substring(pos_prem_espace).Trim();
        }

        public override string ToString()
        {
            return titre + " [" + totalTime.ToString("mm\\:ss") + "]";
        }
    }
}
