
using System;
using System.Text;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace IPod {

    public enum TrackRating {
        Zero = 0,
        One = 20,
        Two = 40,
        Three = 60,
        Four = 80,
        Five = 100
    }

    public enum MediaType {
        AudioVideo = 0,
        Audio,
        Video,
        Podcast,
        VideoPodcast,
        Audiobook,
        MusicVideo,
        TVShow,
        Movie
    }
    public class TrackComparer : IComparer<Track>
    {
       
        public int Compare(Track me, Track other)
        {
                      if (me.Artist != other.Artist)
                return me.Artist.CompareTo(other.Artist);
            if (me.Album != other.Album)
                return me.Album.CompareTo(other.Album);
            return me.TrackNumber.CompareTo(other.TrackNumber);
  
        }

       
    }
    public class Track {

        private static char[] CharsToQuote = { ';', '?', '@', '&', '=', '$', ',', '#', '%' }; //TODO:':',

        private TrackRecord record;
        private TrackDatabase db;
        private int latestPlayCount;
        private Uri uri;
        private Photo coverPhoto;

        internal int playCount;
        internal DateTime lastPlayed;

        internal TrackRecord Record {
            get { return record; }
        }

        public long Id {
            get { return record.DatabaseId; }
        }

        public MediaType Type {
            get { return record.MediaType; }
            set { record.MediaType = value; }
        }

        public string FileName {
            get {
                if (this.Uri == null)
                    return null;
                else 
                    return this.Uri.LocalPath;
            } set {
                this.Uri = PathToFileUri (value);
            }
        }

        public Uri Uri {
            get {
                if (uri == null) {
                    DetailRecord detail = record.GetDetail (DetailType.Location);
                    uri = PathToFileUri (db.GetFilesystemPath (detail.Value));
                }

                return uri;
            } set {
                if (value == null)
                    throw new ArgumentNullException ("Uri cannot be null");

                if (value.Equals (uri)) {
                    return;
                }

                if (value.Scheme != Uri.UriSchemeFile)
                    throw new ArgumentException ("only file scheme is allowed");

                DetailRecord detail = record.GetDetail (DetailType.Location);
                
                if (db.IsTrackOnDevice (value.LocalPath))
                    detail.Value = db.GetPodPath (value.LocalPath);
                else
                    detail.Value = db.GetUniquePodPath (value.LocalPath);

                FileInfo info = new FileInfo (value.LocalPath);
                record.Size = (int) info.Length;

                uri = value;

                if (uri.LocalPath.ToLower ().EndsWith ("mp3"))
                    record.Type = TrackRecordType.MP3;
                else
                    record.Type = TrackRecordType.AAC;
            }
        }

        public int Size {
            get { return record.Size; }
        }

        public TimeSpan Duration {
            get { return TimeSpan.FromMilliseconds (record.Length); }
            set { record.Length = (int) value.TotalMilliseconds; }
        }

        public int TrackNumber {
            get { return record.TrackNumber; }
            set { record.TrackNumber = value; }
        }

        public int TotalTracks {
            get { return record.TotalTracks; }
            set { record.TotalTracks = value; }
        }
        
        public int Year {
            get { return record.Year; }
            set { record.Year = value; }
        }
        
        public int BitRate {
            get { return record.BitRate; }
            set { record.BitRate = value; }
        }
        
        public ushort SampleRate {
            get { return record.SampleRate; }
            set { record.SampleRate = value; }
        }

        public string Title {
            get {
                DetailRecord detail = record.GetDetail (DetailType.Title);
                return detail.Value;
            } set {
                DetailRecord detail = record.GetDetail (DetailType.Title);
                detail.Value = value;
            }
        }
        
        public string Artist {
            get {
                DetailRecord detail = record.GetDetail (DetailType.Artist);
                return detail.Value;
            } set {
                DetailRecord detail = record.GetDetail (DetailType.Artist);
                detail.Value = value;
            }
        }
        
        public string Album {
            get {
                DetailRecord detail = record.GetDetail (DetailType.Album);
                return detail.Value;
            } set {
                DetailRecord detail = record.GetDetail (DetailType.Album);
                detail.Value = value;
            }
        }
        
        public string Genre {
            get {
                DetailRecord detail = record.GetDetail (DetailType.Genre);
                return detail.Value;
            } set {
                DetailRecord detail = record.GetDetail (DetailType.Genre);
                detail.Value = value;
            }
        }
        
        public string Comment {
            get {
                DetailRecord detail = record.GetDetail (DetailType.Comment);
                return detail.Value;
            } set {
                DetailRecord detail = record.GetDetail (DetailType.Comment);
                detail.Value = value;
            }
        }

        public int PlayCount {
            get { return record.PlayCount; }
            set { record.PlayCount = value; }
        }

        public int LatestPlayCount {
            get { return latestPlayCount; }
            internal set { latestPlayCount = value; }
        }

        public bool IsCompilation {
            get { return record.CompilationFlag == (byte) 1; }
            set {
                record.CompilationFlag = value ? (byte) 1 : (byte) 0;
            }
        }

        public DateTime LastPlayed {
            get {
                if (record.LastPlayedTime > 0) {
                    return Utility.MacTimeToDate (record.LastPlayedTime);
                } else {
                    return DateTime.MinValue;
                }
            } set {
                record.LastPlayedTime = Utility.DateToMacTime (value);
            }
        }

        public DateTime DateAdded {
            get { return Utility.MacTimeToDate (record.Date); }
        }

        public TrackRating Rating {
            get { return (TrackRating) record.Rating; }
            set { record.Rating = (byte) value; }
        }

        public string PodcastUrl {
            get { return record.GetDetail (DetailType.PodcastUrl).Value; }
            set { record.GetDetail (DetailType.PodcastUrl).Value = value; }
        }

        public string Category {
            get { return record.GetDetail (DetailType.Category).Value; }
            set { record.GetDetail (DetailType.Category).Value = value; }
        }

        public string Grouping {
            get { return record.GetDetail (DetailType.Grouping).Value; }
            set { record.GetDetail (DetailType.Grouping).Value = value; }
        }

        public bool IsProtected {
            get { return record.UserId != 0; }
        }

        public TrackDatabase TrackDatabase {
            get { return db; }
        }

        internal Track (TrackDatabase db, TrackRecord record) {
            this.db = db;
            this.record = record;
        }
        
        public override string ToString () {
            //return String.Format ("({0}) {1} - {2} - {3} ({4})", Id, Artist, Album, Title, FileName);
            return String.Format("({0}) {1} - {2} - {3}", TrackNumber, Artist, Album, Title);
        }

        public override bool Equals (object a) {
            Track song = a as Track;

            if (song == null)
                return false;

            return this.Id == song.Id;
        }


        public string CorrectedFileName
        {
            get
            {
                string s = this.ToString() + ".mp3";
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    s = s.Replace(c, '_');
                }
                return s;
            }
        }
        public override int GetHashCode () {
            return Id.GetHashCode ();
        }

        private void FindCoverPhoto () {
            if (coverPhoto == null) {
                coverPhoto = db.ArtworkDatabase.LookupPhotoByTrackId (record.DatabaseId);
            }
        }

        public bool HasCoverArt (ArtworkFormat format) {
            return GetThumbnail (format, false) != null;
        }

        public byte[] GetCoverArt (ArtworkFormat format) {
            Thumbnail thumbnail = GetThumbnail (format, false);
            if (thumbnail == null)
                return null;
            else
                return thumbnail.GetData ();
        }

        private Thumbnail GetThumbnail (ArtworkFormat format, bool createNew) {
            FindCoverPhoto ();
            
            if (coverPhoto == null) {
                if (!createNew)
                    return null;
                
                coverPhoto = db.ArtworkDatabase.CreatePhoto ();
                coverPhoto.Record.TrackId = record.DatabaseId;
            }

            Thumbnail thumbnail = coverPhoto.LookupThumbnail (format);
            if (thumbnail == null && createNew) {
                thumbnail = coverPhoto.CreateThumbnail ();
                thumbnail.Format = format;
                thumbnail.Width = format.Width;
                thumbnail.Height = format.Height;
            }

            return thumbnail;
        }

        public void SetCoverArt (ArtworkFormat format, byte[] data) {
            Thumbnail thumbnail = GetThumbnail (format, true);
            thumbnail.SetData (data);

            record.HasArtwork = true;
            record.ArtworkCount = 1; // this is actually for artwork in mp3 tags, but it seems to be needed anyway
        }

        public void RemoveCoverArt () {
            FindCoverPhoto ();

            if (coverPhoto != null) {
                db.ArtworkDatabase.RemovePhoto (coverPhoto);
            }
        }
        
        public void RemoveCoverArt (ArtworkFormat format) {
            Thumbnail thumbnail = GetThumbnail (format, false);
            if (thumbnail != null) {
                coverPhoto.RemoveThumbnail (thumbnail);
            }
        }

        private static Uri PathToFileUri (string path) {
            if (path == null)
                return null;

            path = Path.GetFullPath (path);

            StringBuilder builder = new StringBuilder ();
            builder.Append (Uri.UriSchemeFile);
            builder.Append (Uri.SchemeDelimiter);

            int i;
            while ((i = path.IndexOfAny (CharsToQuote)) != -1) {
                if(i > 0) {
                    builder.Append (path.Substring(0, i));
                }

                builder.Append (Uri.HexEscape (path[i]));
                path = path.Substring (i + 1);
            }

            builder.Append (path);

            return new Uri (builder.ToString ());
        }

    }
}
