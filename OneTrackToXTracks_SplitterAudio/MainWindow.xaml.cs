using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Shapes;

using PanAndZoom;
using NAudio_JJ;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using TagLib;

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
            PreProcess();
        }

        void INITS()
        {
            _Title = Title;
            file.Text = @"D:\Videos\Download Videos\MOBY - Amiga Days (Remasters Vol.1) [[FULL ALBUM]].mp3";
            folder.Text = @"D:\Videos\Download Videos\TEST";
            txt.Text = "Progressive Funk (Impact Inc. - Vectorball)\r\nPapoornoo2 (Apology - Demodisk 1)\r\nThe Last Knight (Alcatraz - Megademo IV)\r\nDragonsfunk (Angels - Copper Master)\r\nPelforth Blues (Alcatraz - Music Disk 1)\r\nThe Knight is Back (Alcatraz - Music Disk 1)\r\nKnulla Kuk (Quartex - Substance)\r\nLet there be Funk (Dreamdealers - Tales Of A Dream \r\nGroovy Thing (Dreamdealers - Innervision)\r\n88, Funky Avenue\r\nP.A.T.A.O.P.A.\r\nDrink My Pain Away (The Special Brothers - Live #1)\r\nKanyenamaryamalabar\r\nCortouchka !\r\nHeads Up (Alliance Design/DRD - Arkham Asylum)\r\nRaging Fire (Dreamdealers - Raging Fire)\r\nLivin' Insanity (Sanity - Arte)\r\nElekfunk (Sanity - Arte)\r\nMobyle (Sanity - Arte)\r\nMore Than Music (Alcatraz - More Than Music)";
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
            if (e.AddedItems.Count == 0) return;

            Blanc_selected((Blanc)e.AddedItems[0]);
        }

        void lbox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) return;

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
            string jsonfile = AppDomain.CurrentDomain.BaseDirectory + @"json.tmp";
            bool usejsoninstead = true;
            if (!System.IO.File.Exists(jsonfile)) usejsoninstead = false;

            if (usejsoninstead)
                peaks = NAudio_JJ.Peak.Get_Peaks_FromJson(jsonfile);
            else
            {
                peaks = NAudio_JJ.Peak.Get_Peaks(file.Text);
                string jsonString = JsonSerializer.Serialize(peaks);
                System.IO.File.WriteAllText(jsonfile, jsonString);
            }

            //get blancs
            blancs = NAudio_JJ.Blanc.Get_Blancs(peaks, 0.005, 0.2);

            List_Blancs();

            titles = TracksFinder(blancs);
            List_Titles(titles);

            ReDrawGraph();
        }

        void PreProcess()
        {
            List_Blancs();

            //Read text => make a list of titles
            TitlesMaker(txt.Text, titles);
            List_Titles(titles);

            //représentations graphiques
            ReDrawGraph();
            _ready_to_Process = true;
        }

        void ReDrawGraph()
        {
            rectangles.Children.Clear();
            Draw_Titles();
            Draw_Peaks(System.Windows.Media.Color.FromRgb(40, 40, 40));
            Draw_Blancs(System.Windows.Media.Colors.White);
        }

        void Process()
        {
            //Extraction des titres du fichier original
            foreach (Title titre in titles)
            {
                //create file
                NAudio_JJ.NAudio_JJ.AudioExtractor(file.Text,
                    titre.fullFileName,
                    titre.start.TotalSeconds,
                    titre.end.TotalSeconds);

                //create ID3 Tag


                FileStream fileStream = new FileStream(titre.fullFileName, new FileStreamOptions() { Access = FileAccess.ReadWrite });
                var tagFile = TagLib.File.Create(new StreamFileAbstraction(titre.fullFileName, fileStream, fileStream));

                var tagsv2 = tagFile.GetTag(TagTypes.Id3v2);
                tagsv2.Album = titre.album;
                tagsv2.Artists = new string[] { titre.author };
                tagsv2.AlbumArtists = new string[] { titre.author };
                tagsv2.Track = (uint)titre.index;
                tagsv2.Title = titre.titleraw;
                tagsv2.Genres = new string[] { };


                var tagsv1 = tagFile.GetTag(TagTypes.Id3v1);
                tagsv1.Album = titre.album;
                tagsv1.Artists = new string[] { titre.author };
                tagsv1.AlbumArtists = new string[] { titre.author };
                tagsv1.Track = (uint)titre.index;
                tagsv1.Title = titre.titleraw;
                tagsv1.Genres = new string[] { };

                tagFile.Save();
                //Id3Tag tag = new Id3Tag();// mp3.GetTag(Id3TagFamily.Version2X);

                //using (var mp3 = new Mp3File(titre.fullFileName, Mp3Permissions.ReadWrite))
                //{
                //    mp3.WriteTag(tag, WriteConflictAction.Replace);
                //}
            }

            //open folder
            OpenFolder(folder.Text);
        }

        List<Title> TracksFinder(List<Blanc> blancs)
        {
            List<Title> titles = new List<Title>();

            double temps_mini_sec = 5;

            for (int i = 0; i < blancs.Count - 1; i++)
            {
                Blanc blanc = blancs[i];

                //first title
                if (i == 0)
                {
                    //est ce que le 
                    if (blanc.debut > temps_mini_sec)
                    {
                        titles.Add(new Title(TimeSpan.Zero, TimeSpan.FromSeconds(blanc.debut))
                        {
                            index = titles.Count + 1,
                            brush = new SolidColorBrush(GetNextColor(titles.Count + 1)),
                            album = album.Text,
                            author = author.Text,
                        });
                    }
                }
                titles.Add(new Title(TimeSpan.FromSeconds((double)blanc.fin), TimeSpan.FromSeconds(blancs[i + 1].debut))
                {
                    index = titles.Count + 1,
                    brush = new SolidColorBrush(GetNextColor(titles.Count + 1)),
                    album = album.Text,
                    author = author.Text,
                });

            }
            return titles;
        }

        void TitlesMaker(string text, List<Title> titles)
        {
            text = text.Replace("\r\n", "\n");
            string[] lignes = text.Split("\n");

            //texte → titre
            for (int i = 0; i < lignes.Length; i++)
            {
                string ligne = lignes[i];

                Title title = titles[i];
                title.SetTitle(ligne);

                string t = "";
                if (title.author != null && title.author != "")
                    t += title.author + " - ";

                if (title.album != null && title.album != "")
                    if (album.Text != null && album.Text != "")
                        title.album = album.Text;

                if (title.album != null && title.album != "")
                    t += title.album + " - ";

                t += title.index.ToString("00") + " - ";

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

                title.fileName = t + ".mp3";

                title.fullFileName = folder.Text + "\\" + title.fileName;
            }
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
                blancs[i].index = i + 1;
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

        void OpenFolder(string folderPath)
        {
            if (Directory.Exists(folderPath))
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    Arguments = folderPath,
                    FileName = "explorer.exe"
                };

                System.Diagnostics.Process.Start(startInfo);
            }
        }

        #endregion
    }
}
