using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using IPod;
using System.IO;
using PortPod;


namespace SharpPort
{
    public partial class MainForm : Form
    {
        Device device;



        public MainForm()
        {
            InitializeComponent();
            buildUI();
            buildDB();

            listArtists.Items.Add("All");
            foreach (Artist a in device.TrackDatabase.Artists.Values)
            {
                listArtists.Items.Add(a);
            }
            rebuildPanels();
            albumsToolStripMenuItem.Checked = true;
        }

        private void buildDB()
        {
            device = new Device();
        }

        private void loadAlbums(Artist artist)
        {
            listAlbums.Items.Clear();
            listAlbums.Items.Add("All");
            List<Artist> ar = new List<Artist>();
            if (artist != null)
            {
                ar.Add(artist);
            }
            else
            {
                ar.AddRange(device.TrackDatabase.Artists.Values);
            }
            foreach (Artist an_artist in ar)
            {
                foreach (Album a in an_artist.Albums)
                {
                    listAlbums.Items.Add(a);
                }
            }

        }

        private void loadTracks(Artist artist, Album album)
        {
            listTracks.Items.Clear();
            foreach (Track t in device.TrackDatabase.Tracks)
            {
                if (artist == null || t.Artist == artist.Name)
                {
                    if (album == null || t.Album == album.Name)
                    {
                        listTracks.Items.Add(t);
                    }
                }
            }
        }


        private void listArtists_DoubleClick(object sender, EventArgs e)
        {
            if (listArtists.SelectedItem != null)
            {

            }
        }

        private void listAlbums_DoubleClick(object sender, EventArgs e)
        {
            if (listAlbums.SelectedItem != null)
            {
                Album album = null;
                Artist artist = null;
                if (listAlbums.SelectedIndex != 0)
                    album = (Album)listAlbums.SelectedItem;
                if (listArtists.SelectedIndex != 0)
                    artist = (Artist)listArtists.SelectedItem;
                loadTracks(artist, album);
            }
        }

        private void albumsToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            rebuildPanels();
        }
        private System.Windows.Forms.GroupBox grpAlbums, grpPlaylists, grpPodcasts;
        private System.Windows.Forms.ListBox listAlbums, listPlaylists, listPodcasts;

        private void buildUI()
        {
            listAlbums = new System.Windows.Forms.ListBox();
            listAlbums.Dock = System.Windows.Forms.DockStyle.Fill;
            listAlbums.SelectedValueChanged += new System.EventHandler(this.listAlbums_DoubleClick);
            listAlbums.ContextMenuStrip = contextMenuStrip1;

            grpAlbums = new System.Windows.Forms.GroupBox();
            grpAlbums.Controls.Add(listAlbums);
            grpAlbums.Dock = System.Windows.Forms.DockStyle.Fill;
            grpAlbums.TabIndex = 2;
            grpAlbums.TabStop = false;
            grpAlbums.Text = "Albums";

            listPlaylists = new System.Windows.Forms.ListBox();
            listPlaylists.Dock = System.Windows.Forms.DockStyle.Fill;
            listPlaylists.FormattingEnabled = true;
            listPlaylists.SelectedValueChanged += new EventHandler(listPlaylists_SelectedValueChanged);
            listPlaylists.ContextMenuStrip = contextMenuStrip1;

            grpPlaylists = new System.Windows.Forms.GroupBox();
            grpPlaylists.Controls.Add(listPlaylists);
            grpPlaylists.Dock = System.Windows.Forms.DockStyle.Fill;
            grpPlaylists.TabIndex = 2;
            grpPlaylists.TabStop = false;
            grpPlaylists.Text = "Playlists";

            listPodcasts = new System.Windows.Forms.ListBox();
            listPodcasts.Dock = System.Windows.Forms.DockStyle.Fill;
            listPodcasts.FormattingEnabled = true;
            listPodcasts.SelectedValueChanged += new EventHandler(listPodcasts_SelectedValueChanged);
            listPodcasts.ContextMenuStrip = contextMenuStrip1;

            grpPodcasts = new System.Windows.Forms.GroupBox();
            grpPodcasts.Controls.Add(listPodcasts);
            grpPodcasts.Dock = System.Windows.Forms.DockStyle.Fill;
            grpPodcasts.TabIndex = 2;
            grpPodcasts.TabStop = false;
            grpPodcasts.Text = "Podcasts";


        }


        private void rebuildPanels()
        {
            tableLayoutPanel1.SuspendLayout();
            int to = tableLayoutPanel1.ColumnStyles.Count;
            for (int i = 1; i < to; i++)
            {
                tableLayoutPanel1.ColumnStyles.RemoveAt(1);
            }
            int idx = 1;
            this.tableLayoutPanel1.ColumnCount = 1;
            grpAlbums.Visible = albumsToolStripMenuItem.Checked;
            grpPlaylists.Visible = playlistsToolStripMenuItem.Checked;

            if (albumsToolStripMenuItem.Checked)
            {
                this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
                this.tableLayoutPanel1.Controls.Add(grpAlbums, idx, 0);
                loadAlbums(null);
                this.tableLayoutPanel1.ColumnCount++;
                idx++;
            }
            if (playlistsToolStripMenuItem.Checked)
            {
                this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
                this.tableLayoutPanel1.Controls.Add(grpPlaylists, idx, 0);
                loadPlaylists(false);
                this.tableLayoutPanel1.ColumnCount++;
                idx++;
            }
            if (podcastToolStripMenuItem.Checked)
            {
                this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
                this.tableLayoutPanel1.Controls.Add(grpPodcasts, idx, 0);
                loadPlaylists(true);
                this.tableLayoutPanel1.ColumnCount++;
                idx++;
            }
            tableLayoutPanel1.ResumeLayout();
        }

        private void loadPlaylists(bool isPodcast)
        {
            listPlaylists.Items.Clear();
            foreach (Playlist p in device.TrackDatabase.Playlists)
            {
                if (p.PlaylistRecord.IsPodcast == isPodcast)
                    listPlaylists.Items.Add(p);
            }
        }

        private void exportTracksToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                List<Track> tracks = getSelectedTracks(contextMenuStrip1.SourceControl);
                foreach (Track t in tracks)
                {
                    try
                    {
                        FileCopy.CopyFiles(t.FileName, folderBrowserDialog.SelectedPath + Path.DirectorySeparatorChar + t.CorrectedFileName);
                    }
                    catch (Exception ex)
                    {
                        if (MessageBox.Show("Cannot copy file. Reason: " + ex.Message + "\nContinue?", "Error", MessageBoxButtons.YesNo, MessageBoxIcon.Error) == DialogResult.No)
                            break;
                    }
                }
            }
        }

        private List<Track> getSelectedTracks(object sender)
        {
            List<Track> t = new List<Track>();
            if (sender == listTracks)
            {
                foreach (object o in listTracks.SelectedItems)
                {
                    t.Add((Track)o);
                }
            }
            if (sender == listAlbums)
            {
                foreach (object o in listAlbums.SelectedItems)
                {
                    t.AddRange(((Album)o).Tracks);
                }
            }
            if (sender == listArtists)
            {
                foreach (object o in listArtists.SelectedItems)
                {
                    t.AddRange(((Artist)o).Tracks);
                }
            }
            if (sender == listPlaylists)
            {
                foreach (object o in listPlaylists.SelectedItems)
                {
                    t.AddRange(((Playlist)o).Tracks);
                }
            }
            if (sender == listPodcasts)
            {
                foreach (object o in listPodcasts.SelectedItems)
                {
                    t.AddRange(((Playlist)o).Tracks);
                }
            }

            return t;
        }
        void listPlaylists_SelectedValueChanged(object sender, EventArgs e)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        void listPodcasts_SelectedValueChanged(object sender, EventArgs e)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        private void listArtists_SelectedValueChanged(object sender, EventArgs e)
        {
            if (listArtists.SelectedIndex == 0)
            {
                if (albumsToolStripMenuItem.Checked)
                    loadAlbums(null);
                else
                    loadTracks(null, null);
            }
            else
            {
                if (albumsToolStripMenuItem.Checked)
                    loadAlbums((Artist)listArtists.SelectedItem);
                else
                    loadTracks((Artist)listArtists.SelectedItem, null);
            }

        }

        private void enqueueTracksToolStripMenuItem_Click(object sender, EventArgs e)
        {
                List<Track> tracks = getSelectedTracks(contextMenuStrip1.SourceControl);
                tracks.Sort(new TrackComparer());
                foreach (Track t in tracks){

                    System.Diagnostics.Process.Start(t.FileName);
            }
        }
    }
}