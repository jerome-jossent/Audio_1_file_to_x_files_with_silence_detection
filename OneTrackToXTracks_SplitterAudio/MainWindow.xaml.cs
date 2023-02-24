using Microsoft.VisualBasic.Devices;
using NAudio.Dmo.Effect;
using NAudio.Dsp;
using NAudio.Wave;
using NAudio.WaveFormRenderer;
using PanAndZoom;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Provider;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TreeView;

using NAudio_JJ;

namespace OneTrackToXTracks_SplitterAudio
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        void NotifyPropertyChanged([CallerMemberName] String propertyName = "") { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }
        public event PropertyChangedEventHandler? PropertyChanged;

        string _Title;

        public bool _ready_to_Process
        {
            get => ready_to_Process;
            set
            {
                if (ready_to_Process == value) return;
                ready_to_Process = value;
                NotifyPropertyChanged();
            }
        }
        bool ready_to_Process;

        TimeSpan totaltime;
        List<Title> titles;
        Dictionary<System.Windows.Shapes.Rectangle, Title> tracks; List<Peak> peaks;
        List<Blanc> blancs;
        Polygon sound_peaks;
        Polygon sound_blancs;
        Polygon blanc_selected;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        void Window_Loaded(object sender, RoutedEventArgs e)
        {
            INITS();
            GetInfo();
            ZoomBorder.SampleEvent += ZoomBorder_SampleEvent;
        }

        void INITS()
        {
            _Title = Title;
            file.Text = @"D:\Videos\Download Videos\MOBY - Amiga Days (Remasters Vol.1) [[FULL ALBUM]].mp3";
            folder.Text = @"D:\Videos\Download Videos\TEST";
            txt.Text = "0:00:00  Progressive Funk  ( Impact Inc. - Vectorball )\r\n0:11:43  Papoornoo2 ( Apology - Demodisk 1 )\r\n0:15:51  The Last Knight ( Alcatraz - Megademo IV )\r\n0:24:39  Dragonsfunk ( Angels - Copper Master )   \r\n0:30:33  Pelforth Blues ( Alcatraz - Music Disk 1 )\r\n0:36:54  The Knight is Back ( Alcatraz - Music Disk 1 ) \r\n0:47:16  Knulla Kuk (Quartex - Substance )\r\n0:55:17  Let there be Funk ( Dreamdealers - Tales Of A Dream )\r\n1:01:26  Groovy Thing ( Dreamdealers - Innervision )\r\n1:05:56  88, Funky Avenue \r\n1:09:08  P.A.T.A.O.P.A.\r\n1:16:52  Drink My Pain Away ( The Special Brothers - Live #1 )\r\n1:21:22  Kanyenamaryamalabar \r\n1:30:00  Cortouchka !\r\n1:36:06  Heads Up ( Alliance Design/DRD - Arkham Asylum )\r\n1:39:43  Raging Fire ( Dreamdealers - Raging Fire )\r\n1:40:47  Livin' Insanity ( Sanity - Arte )\r\n1:43:54  Elekfunk ( Sanity - Arte )\r\n1:47:13  Mobyle ( Sanity - Arte )\r\n1:49:51  More Than Music ( Alcatraz - More Than Music )";
            author.Text = "MOBY";
        }

        #region From UI
        void SelectFile_btn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                file.Text = openFileDialog.FileName;

        }
        void SelectFolder_btn_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                folder.Text = folderBrowserDialog.SelectedPath;
        }
        void PreProcess_btn_Click(object sender, RoutedEventArgs e) { PreProcess(); }
        void Go_btn_Click(object sender, RoutedEventArgs e) { Process(); }

        void lbox_blanc_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)            
                Blanc_selected((Blanc)e.AddedItems[0]);            
        }

        void lbox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Title_UC selection = (Title_UC)e.AddedItems[0];
            Title_selected(selection.title);
        }

        void ZoomBorder_SampleEvent(object sender, PanAndZoom.SampleEventArgs e)
        {
            double y_relative = (e.mouseRelativeY - e.relativeoffsetY) / e.scaleY;
            double y_time = y_relative * totaltime.TotalSeconds;
            TimeSpan t = TimeSpan.FromSeconds(y_time);
            string titre = _Title + " - " + t.ToString("G");
            Dispatcher.BeginInvoke(() => (Title = titre));
        }

        void Blanc_selected_delete(object sender, RoutedEventArgs e)
        {
            blancs.Remove((Blanc)lbox_blanc.SelectedItem);
            List_Blancs();
            ReDrawGraph();
        }
        #endregion

        void GetInfo()
        {
            //Read musiquefile
            totaltime = NAudio_JJ.NAudio_JJ.MusicFileInfo(file.Text);

            //get Peaks Amplitude
            string jsonfile = @"D:\Ma musique\_CHIPTUNE\json.txt";
            bool usejsoninstead = true;
            if (usejsoninstead)
                peaks = NAudio_JJ.Peak.Get_Peaks_FromJson(jsonfile);
            else
            {
                peaks = NAudio_JJ.Peak.Get_Peaks(file.Text);
                string jsonString = JsonSerializer.Serialize(peaks);
                File.WriteAllText(jsonfile, jsonString);
            }

            //get blancs
            blancs = NAudio_JJ.Blanc.Get_Blancs(peaks, 0.005, 0.2);

            List_Blancs();
            ReDrawGraph();
        }

        void PreProcess()
        {
            //Read text => make a list of titles
            titles = TitlesMaker(txt.Text);
            List_Titles(titles);

            #region trim
            if (false && titles != null)
            {
                //set le temps de la première piste
                titles[0].start = TimeSpan.FromSeconds((double)blancs[0].fin);
                //set le temps de la dernière piste
                titles[titles.Count - 1].end = TimeSpan.FromSeconds(blancs[blancs.Count - 1].debut);

                //supprime le premier blanc 
                blancs.RemoveAt(0);
                //supprime le dernier blanc 
                blancs.RemoveAt(blancs.Count - 1);
            }
            #endregion

            List_Blancs();

            //représentations graphiques
            ReDrawGraph();
            _ready_to_Process = true;
        }

        void ReDrawGraph()
        {
            rectangles.Children.Clear();
            Draw_Titles();
            Draw_Peaks(System.Windows.Media.Colors.Yellow);
            Draw_Blancs(System.Windows.Media.Colors.White);
        }

        void Process()
        {
            //Extraction des titres du fichier original
            foreach (Title titre in titles)
                NAudio_JJ.NAudio_JJ.AudioExtractor(file.Text,
                    titre.fullFileName,
                    titre.start.TotalSeconds,
                    titre.end.TotalSeconds);
        }

        List<Title> TitlesMaker(string text)
        {
            text = text.Replace("\r\n", "\n");
            string[] lignes = text.Split("\n");
            List<Title> titles = new List<Title>();

            //texte → titre
            for (int i = 0; i < lignes.Length; i++)
            {
                string ligne = lignes[i];
                Title title = new Title(ligne); // titre et temps début
                title.index = i + 1;
                title.author = author.Text;
                title.album = album.Text;
                title.brush = new System.Windows.Media.SolidColorBrush(GetNextColor(i));

                string t = "";
                if (title.author != null && title.author != "")
                    t += title.author + " - ";

                if (title.album != null && title.album != "")
                    t += title.album + " - ";

                t += title.titleraw;

                t = t.Replace("\\", "_");
                t = t.Replace("/", "_");
                t = t.Replace(":", "_");
                t = t.Replace("*", "_");
                t = t.Replace("?", "_");
                t = t.Replace("\"", "_");
                t = t.Replace("<", "_");
                t = t.Replace(">", "_");
                t = t.Replace("|", "_");

                title.fileName = t;

                title.fullFileName = folder.Text + "\\" + title.fileName + ".mp3";

                titles.Add(title);
            }

            //temps 
            for (int i = 0; i < titles.Count; i++)
            {
                Title titre = titles[i];
                if (i < titles.Count - 1)
                    //premiers titres
                    titre.end = titles[i + 1].start;
                else
                    //dernier titre
                    titre.end = totaltime;

                titre.totalTime = titre.end - titre.start;
            }

            return titles;
        }

        #region To UI
        void List_Titles(List<Title> titles)
        {
            lbox.Items.Clear();
            for (int i = 0; i < titles.Count; i++)
            {
                Title title = titles[i];

                Title_UC uc = new Title_UC();
                uc._Link(title);

                lbox.Items.Add(uc);
            }
        }

        void List_Blancs()
        {
            lbox_blanc.Items.Clear();
            for (int i = 0; i < blancs.Count; i++)
            {
                blancs[i].index = i;
                lbox_blanc.Items.Add(blancs[i]);
            }
        }

        void Draw_Titles()
        {
            if (titles == null) return;

            tracks = new Dictionary<System.Windows.Shapes.Rectangle, Title>();

            foreach (Title titre in titles)
            {
                // Create the rectangle
                System.Windows.Shapes.Rectangle rec = new System.Windows.Shapes.Rectangle()
                {
                    Width = rectangles.Width,
                    Height = titre.end.TotalSeconds - titre.start.TotalSeconds,
                    Fill = titre.brush,
                    Stroke = System.Windows.Media.Brushes.Black,
                    StrokeThickness = 0,
                };

                // Add to canvas
                rectangles.Children.Add(rec);
                Canvas.SetTop(rec, titre.start.TotalSeconds);
                Canvas.SetLeft(rec, 0);

                titre.rectangle = rec;
                tracks.Add(rec, titre);
            }
        }

        void Draw_Peaks(System.Windows.Media.Color color)
        {
            //dessine le niveau sonore = f(temps)
            sound_peaks = new Polygon();
            sound_peaks.Fill = new SolidColorBrush(color);
            sound_peaks.Points.Add(new System.Windows.Point(0, 0));

            for (int i = 0; i < peaks.Count; i++)
                sound_peaks.Points.Add(new System.Windows.Point(peaks[i].amplitude, peaks[i].temps));

            sound_peaks.Points.Add(new System.Windows.Point(0, totaltime.TotalSeconds));
            sound_peaks.Points.Add(new System.Windows.Point(0, 0));

            //positionnement du dessin
            rectangles.Children.Add(sound_peaks);
            Canvas.SetTop(sound_peaks, 0);
            Canvas.SetLeft(sound_peaks, 0);

            rectangles.Height = totaltime.TotalSeconds;
        }

        void Draw_Blancs(System.Windows.Media.Color color)
        {
            //dessine des traits à chaque blanc = f(temps)
            sound_blancs = new Polygon();
            sound_blancs.Fill = new SolidColorBrush(color);
            sound_blancs.Points.Add(new System.Windows.Point(1, 0));

            double X, Y;
            foreach (Blanc blanc in this.blancs)
            {
                X = 1;
                Y = blanc.debut;
                sound_blancs.Points.Add(new System.Windows.Point(X, Y));
                X = 0.1;
                sound_blancs.Points.Add(new System.Windows.Point(X, Y));
                Y = (double)blanc.fin;
                sound_blancs.Points.Add(new System.Windows.Point(X, Y));
                X = 1;
                sound_blancs.Points.Add(new System.Windows.Point(X, Y));
            }
            sound_blancs.Points.Add(new System.Windows.Point(1, 0));

            //positionnement du dessin
            rectangles.Children.Add(sound_blancs);
            Canvas.SetTop(sound_blancs, 0);
            Canvas.SetLeft(sound_blancs, 0);

            rectangles.Height = totaltime.TotalSeconds;
        }

        void Title_selected(Title title)
        {
            double relativeStart = title.start.TotalSeconds / totaltime.TotalSeconds;
            double relativeEnd = title.end.TotalSeconds / totaltime.TotalSeconds;
            zoomBorder.SetRange(relativeStart, relativeEnd);
        }

        void Blanc_selected(Blanc? blanc)
        {
            if (blanc_selected != null)
                rectangles.Children.Remove(blanc_selected);
            if (blanc == null)
                blanc_selected = null;
            else
            {
                blanc_selected = new Polygon();
                blanc_selected.Fill = new SolidColorBrush(System.Windows.Media.Colors.Red);
                blanc_selected.Points.Add(new System.Windows.Point(0.1, blanc.debut));
                blanc_selected.Points.Add(new System.Windows.Point(0.9, blanc.debut));
                blanc_selected.Points.Add(new System.Windows.Point(0.9, (double)blanc.fin));
                blanc_selected.Points.Add(new System.Windows.Point(0.1, (double)blanc.fin));
                rectangles.Children.Add(blanc_selected);
                Canvas.SetTop(blanc_selected, 0);
                Canvas.SetLeft(blanc_selected, 0);

                //zoom in
                double y_moyen = ((double)blanc.fin + blanc.debut) / 2;
                double y_relative = y_moyen / totaltime.TotalSeconds;
                zoomBorder.SetZoom(y_relative, aboluteZoom: 500);
            }
        }

        #endregion

        #region TOOLS
        List<System.Windows.Media.Color> colors = new List<System.Windows.Media.Color>(){
            System.Windows.Media.Colors.CadetBlue,
            System.Windows.Media.Colors.DarkKhaki,
            System.Windows.Media.Colors.DarkTurquoise,
            System.Windows.Media.Colors.LightBlue,
            System.Windows.Media.Colors.LightCoral,
            System.Windows.Media.Colors.LightGreen,
            System.Windows.Media.Colors.LightPink,
            System.Windows.Media.Colors.LightSalmon,
            System.Windows.Media.Colors.LightSkyBlue,
            System.Windows.Media.Colors.LimeGreen,
            System.Windows.Media.Colors.MediumOrchid,
            System.Windows.Media.Colors.Plum,
            System.Windows.Media.Colors.SandyBrown,
            System.Windows.Media.Colors.Thistle
            };

        System.Windows.Media.Color GetNextColor(int index)
        {
            System.Windows.Media.Color c;
            while (index > colors.Count - 1) { index -= colors.Count; }
            return colors[index];
        }
        #endregion
    }
}
