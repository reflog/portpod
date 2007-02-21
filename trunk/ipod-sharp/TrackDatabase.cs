using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;


namespace IPod {

    internal abstract class Record {

        public const int PadLength = 8;

        public string Name;
        public int HeaderOne; // usually the size of this record
        public int HeaderTwo; // usually the size of this record + size of children
        public bool IsBE = false;

        public Record (bool isbe) {
            this.IsBE = isbe;
        }

        protected void WriteName (BinaryWriter writer) {
            byte[] nameBytes = Encoding.ASCII.GetBytes (this.Name);
            if (IsBE)
                nameBytes = Utility.Swap (nameBytes);
            
            writer.Write (nameBytes);
        }

        protected virtual void WritePadding (BinaryWriter writer) {
            writer.Write (new byte[PadLength]);
        }

        public long ToInt64 (byte[] buf, int offset) {
            return MaybeSwap (BitConverter.ToInt64 (buf, offset));
        }

        public int ToInt32 (byte[] buf, int offset) {
            return MaybeSwap (BitConverter.ToInt32 (buf, offset));
        }

        public uint ToUInt32 (byte[] buf, int offset) {
            return (uint) ToInt32 (buf, offset);
        }

        public short ToInt16 (byte[] buf, int offset) {
            return MaybeSwap (BitConverter.ToInt16 (buf, offset));
        }

        public ushort ToUInt16 (byte[] buf, int offset) {
            return (ushort) ToInt16 (buf, offset);
        }

        public short MaybeSwap (short val) {
            return Utility.MaybeSwap (val, IsBE);
        }

        public int MaybeSwap (int val) {
            return Utility.MaybeSwap (val, IsBE);
        }

        public long MaybeSwap (long val) {
            return Utility.MaybeSwap (val, IsBE);
        }

        public byte[] MaybeSwap (byte[] val) {
            return Utility.MaybeSwap (val, IsBE);
        }

        protected void ReadHeader (BinaryReader reader) {
            byte[] nameBytes = reader.ReadBytes (4);
            if (IsBE)
                nameBytes = Utility.Swap (nameBytes);
            
            string n = Encoding.ASCII.GetString (nameBytes);

            if (this.Name != null && this.Name != n) {
                throw new DatabaseReadException ("Expected record name of '{0}', got '{1}'", this.Name, n);
            }

            this.Name = n;
            this.HeaderOne = reader.ReadInt32 ();
            this.HeaderTwo = reader.ReadInt32 ();

            if (IsBE) {
                this.HeaderOne = Utility.Swap (this.HeaderOne);
                this.HeaderTwo = Utility.Swap (this.HeaderTwo);
            }
        }
    }

    internal abstract class TrackDbRecord : Record {

        public TrackDbRecord (bool isbe) : base (isbe) {
        }

        public virtual void Read (DatabaseRecord db, BinaryReader reader) {
            ReadHeader (reader);
        }
        
        public abstract void Save (DatabaseRecord db, BinaryWriter writer);

        protected void SaveChild (DatabaseRecord db, TrackDbRecord record, out byte[] data, out int length) {
            MemoryStream stream = new MemoryStream ();
            BinaryWriter writer = new EndianBinaryWriter (stream, IsBE);
            record.Save (db, writer);
            writer.Flush ();
            length = (int) stream.Length;
            data = stream.GetBuffer ();
            writer.Close ();
        }
        
    }

    internal class GenericRecord : TrackDbRecord {

        private byte[] data;

        public GenericRecord (bool isbe) : base (isbe) {
        }
        
        public override void Read (DatabaseRecord db, BinaryReader reader) {
            base.Read (db, reader);

            data = reader.ReadBytes (this.HeaderTwo - 12);
        }

        public override void Save (DatabaseRecord db, BinaryWriter writer) {
            WriteName (writer);
            writer.Write (this.HeaderOne);
            writer.Write (this.HeaderTwo);
            writer.Write (this.data);
        }
    }

    internal class PlaylistItemRecord : TrackDbRecord {

        private int unknownOne = 0;
        private int unknownTwo = 0;
        private List<DetailRecord> details = new List<DetailRecord> ();
        private DetailRecord posrec;

        public int TrackId;
        public int Position {
            get { return posrec.Position; }
            set { posrec.Position = value; }
        }
        
        public int Timestamp;

        public PlaylistItemRecord (bool isbe) : base (isbe) {
            this.Name = "mhip";
            posrec = new DetailRecord (isbe);
            posrec.Type = DetailType.Misc;
            details.Add (posrec);
        }

        public override void Read (DatabaseRecord db, BinaryReader reader) {
            base.Read (db, reader);

            byte[] body = reader.ReadBytes (this.HeaderOne - 12);

            int numDataObjects = ToInt32 (body, 0);
            unknownOne = ToInt32 (body, 4);
            unknownTwo = ToInt32 (body, 8);
            TrackId = ToInt32 (body, 12);
            Timestamp = ToInt32 (body, 16);

            details.Clear ();

            for (int i = 0; i < numDataObjects; i++) {
                DetailRecord detail = new DetailRecord (IsBE);
                detail.Read (db, reader);
                details.Add (detail);

                // it's possible there will be more than one of these, but the last one
                // should always be the "right" one.  Sigh.
                if (detail.Type == DetailType.Misc) {
                    posrec = detail;
                }
            }
        }

        public override void Save (DatabaseRecord db, BinaryWriter writer) {

            int childrenLength = 0;
            byte[] childrenData = new byte[0];
            
            foreach (DetailRecord child in details) {
                int childLength = 0;
                byte[] childData = null;

                SaveChild (db, child, out childData, out childLength);
                childrenLength += childLength;

                byte[] newChildrenData = new byte[childrenData.Length + childData.Length];
                Array.Copy (childrenData, 0, newChildrenData, 0, childrenData.Length);
                Array.Copy (childData, 0, newChildrenData, childrenData.Length, childData.Length);
                childrenData = newChildrenData;
            }

            WriteName (writer);
            writer.Write (32 + PadLength);
            
            // as of version 13, the detail record counts as a child
            if (db.Version >= 13) {
                writer.Write (32 + childrenLength + PadLength);
            } else {
                writer.Write (32 + PadLength);
            }

            writer.Write (details.Count);
            writer.Write (unknownOne);
            writer.Write (unknownTwo);
            writer.Write (TrackId);
            writer.Write (Timestamp);
            writer.Write (new byte[PadLength]);
            writer.Write (childrenData, 0, childrenLength);
        }
    }

    internal enum SortOrder : int {
        Manual = 1,
        Unknown,
        Title,
        Album,
        Artist,
        BitRate,
        Genre,
        Kind,
        DateModified,
        Track,
        Size,
        Time,
        Year,
        SampleRate,
        Comment,
        DateAdded,
        Equalizer,
        Composer,
        Unknown2,
        PlayCount,
        LastPlayed,
        Disc,
        Rating,
        ReleaseDate,
        Bpm,
        Grouping,
        Category,
        Description
    }

    internal class PlaylistRecord : TrackDbRecord {

        private int unknownOne;
        //private int unknownTwo;
        //private int unknownThree;
        private bool isLibrary;

        private List<DetailRecord> stringDetails = new List<DetailRecord> ();
        private List<Record> otherDetails = new List<Record> ();
        private List<PlaylistItemRecord> playlistItems = new List<PlaylistItemRecord> ();

        private DetailRecord nameRecord;
        
        public bool IsHidden;
        public int Timestamp;
        public int Id;
        public bool IsPodcast;
        public SortOrder Order = SortOrder.Manual;

        public string PlaylistName {
            get { return nameRecord.Value; }
            set {
                if (nameRecord == null) {
                    nameRecord = new DetailRecord (IsBE);
                    nameRecord.Type = DetailType.Title;
                    stringDetails.Add (nameRecord);
                }
                
                nameRecord.Value = value;
            }
        }

        public IList<PlaylistItemRecord> Items {
            get {
                return new ReadOnlyCollection<PlaylistItemRecord> (playlistItems);
            }
        }

        public PlaylistRecord (bool isLibrary, bool isbe) : base (isbe) {
            this.isLibrary = isLibrary;
            this.Name = "mhyp";
        }

        public void Clear () {
            playlistItems.Clear ();
        }

        public bool RemoveItem (int index) {
            if (index < 0 || index >= playlistItems.Count)
                return false;
            
            playlistItems.RemoveAt (index);
            return true;
        }

        public void RemoveTrack (int id) {
            foreach (PlaylistItemRecord item in playlistItems) {
                if (item.TrackId == id) {
                    playlistItems.Remove (item);
                    break;
                }
            }
        }

        public void AddItem (PlaylistItemRecord rec) {
            InsertItem (-1, rec);
        }
        
        public void InsertItem (int index, PlaylistItemRecord rec) {
            if (index < 0) {
                playlistItems.Add (rec);
            } else {
                playlistItems.Insert (index, rec);
            }
        }

        public PlaylistItemRecord CreateItem () {
            return new PlaylistItemRecord (IsBE);
        }

        public int IndexOf (int trackid) {

            int i = 0;
            foreach (PlaylistItemRecord rec in playlistItems) {
                if (rec.TrackId == trackid) {
                    return i;
                }

                i++;
            }

            return -1;
        }
        
        public override void Read (DatabaseRecord db, BinaryReader reader) {
            base.Read (db, reader);

            byte[] body = reader.ReadBytes (this.HeaderOne - 12);

            int numdetails = ToInt32 (body, 0);
            int numitems = ToInt32 (body, 4);
            int hiddenFlag = ToInt32 (body, 8);

            if (hiddenFlag == 1)
                IsHidden = true;

            Timestamp = ToInt32 (body, 12);
            Id = ToInt32 (body, 16);
            unknownOne = ToInt32 (body, 20);

            if (db.Version >= 13) {
                IsPodcast = ToInt16 (body, 30) == 1 ? true : false;
                Order = (SortOrder) ToInt32 (body, 32);
            }

            stringDetails.Clear ();
            otherDetails.Clear ();
            playlistItems.Clear ();

            for (int i = 0; i < numdetails; i++) {
                if (i == 0) {
                    nameRecord = new DetailRecord (IsBE);
                    nameRecord.Read (db, reader);
                    stringDetails.Add (nameRecord);
                } else if (isLibrary) {
                    DetailRecord rec = new DetailRecord (IsBE);
                    rec.Read (db, reader);
                    otherDetails.Add (rec);
                } else {
                    GenericRecord rec = new GenericRecord (IsBE);
                    rec.Read (db, reader);
                    otherDetails.Add (rec);
                }
            }

            for (int i = 0; i < numitems; i++) {
                PlaylistItemRecord item = new PlaylistItemRecord (IsBE);
                item.Read (db, reader);
                playlistItems.Add (item);
            }
        }

#pragma warning disable 0169
        private DetailRecord CreateIndexRecord (TrackListRecord tracks, IndexType type) {
            DetailRecord record = new DetailRecord (IsBE);
            record.Type = DetailType.LibraryIndex;
            record.IndexType = type;

            // blah, this is soooo sleaux
            
            List<PlaylistItemRecord> items = new List<PlaylistItemRecord> (playlistItems);
            TrackSorter sorter = new TrackSorter (tracks, type);
            items.Sort (sorter);

            List<int> indices = new List<int> ();
            foreach (PlaylistItemRecord item in items) {
                indices.Add (tracks.IndexOf (item.TrackId));
            }

            record.LibraryIndices = indices.ToArray ();
            return record;
        }
#pragma warning restore 0169

        private void CreateLibraryIndices (TrackListRecord tracks) {

            List<Record> details = new List<Record> ();
            
            // remove any existing library index records
            foreach (Record rec in otherDetails) {
                DetailRecord detail = rec as DetailRecord;
                if (detail != null && detail.Type != DetailType.LibraryIndex) {
                    details.Add (rec);
                }
            }

            otherDetails = details;

            /* this is causing problems, leave it out for now
            otherDetails.Add (CreateIndexRecord (tracks, IndexType.Track));
            otherDetails.Add (CreateIndexRecord (tracks, IndexType.PhotoAlbum));
            otherDetails.Add (CreateIndexRecord (tracks, IndexType.Artist));
            otherDetails.Add (CreateIndexRecord (tracks, IndexType.Genre));
            otherDetails.Add (CreateIndexRecord (tracks, IndexType.Composer));
            */
        }
        
        public override void Save (DatabaseRecord db, BinaryWriter writer) {

            if (isLibrary) {
                CreateLibraryIndices (db[DataSetIndex.Library].TrackList);
            }
            
            MemoryStream stream = new MemoryStream ();
            BinaryWriter childWriter = new EndianBinaryWriter (stream, IsBE);

            foreach (TrackDbRecord rec in stringDetails) {
                rec.Save (db, childWriter);
            }

            foreach (TrackDbRecord rec in otherDetails) {
                rec.Save (db, childWriter);
            }

            int pos = 1;
            foreach (PlaylistItemRecord item in playlistItems) {
                item.Position = pos++;
                item.Save (db, childWriter);
            }

            childWriter.Flush ();
            byte[] childData = stream.GetBuffer ();
            int childDataLength = (int) stream.Length;
            childWriter.Close ();

            WriteName (writer);

            int reclen;

            if (db.Version >= 13) {
                reclen = 48;
            } else {
                reclen = 42;
            }
            
            writer.Write (reclen + PadLength);
            writer.Write (reclen + PadLength + childDataLength);
            writer.Write (stringDetails.Count + otherDetails.Count);
            writer.Write (playlistItems.Count);
            writer.Write (IsHidden ? 1 : 0);
            writer.Write (Timestamp);
            writer.Write (Id);
            writer.Write (unknownOne);
            writer.Write (stringDetails.Count);
            writer.Write ((short) otherDetails.Count);

            if (db.Version >= 13) {
                writer.Write (IsPodcast ? (short) 1 : (short) 0);
                writer.Write ((int) Order);
            }
            
            writer.Write (new byte[PadLength]);
            writer.Write (childData, 0, childDataLength);
        }

        private class TrackSorter : IComparer<PlaylistItemRecord> {

            private TrackListRecord tracks;
            private IndexType type;

            public TrackSorter (TrackListRecord tracks, IndexType type) {
                this.tracks = tracks;
                this.type = type;
            }
            
            public int Compare (PlaylistItemRecord a, PlaylistItemRecord b) {
                TrackRecord trackA = tracks.LookupTrack (a.TrackId);
                TrackRecord trackB = tracks.LookupTrack (b.TrackId);

                if (trackA == null || trackB == null)
                    return 0;
                
                switch (type) {
                case IndexType.Track:
                    return String.Compare (trackA.GetDetail (DetailType.Title).Value,
                                           trackB.GetDetail (DetailType.Title).Value);
                case IndexType.Artist:
                    return String.Compare (trackA.GetDetail (DetailType.Artist).Value,
                                           trackB.GetDetail (DetailType.Artist).Value);
                case IndexType.Album:
                    return String.Compare (trackA.GetDetail (DetailType.Album).Value,
                                           trackB.GetDetail (DetailType.Album).Value);
                case IndexType.Genre:
                    return String.Compare (trackA.GetDetail (DetailType.Genre).Value,
                                           trackB.GetDetail (DetailType.Genre).Value);
                case IndexType.Composer:
                    return String.Compare (trackA.GetDetail (DetailType.Composer).Value,
                                           trackB.GetDetail (DetailType.Composer).Value);
                default:
                    return 0;
                }
            }
        }
    }
        

    internal class PlaylistListRecord : TrackDbRecord, IEnumerable {

        private List<PlaylistRecord> playlists = new List<PlaylistRecord> ();

        public PlaylistRecord this[int index] {
            get {
                return (PlaylistRecord) playlists[index];
            }
        }

        public IList<PlaylistRecord> Playlists {
            get {
                return new ReadOnlyCollection<PlaylistRecord> (playlists);
            }
        }

        public PlaylistListRecord (bool isbe) : base (isbe) {
            PlaylistRecord record = new PlaylistRecord (true, isbe);
            record.IsHidden = true;
            record.PlaylistName = "IPOD";
            playlists.Add (record);

            this.Name = "mhlp";
        }

        public IEnumerator GetEnumerator () {
            return playlists.GetEnumerator ();
        }
        
        public override void Read (DatabaseRecord db, BinaryReader reader) {
            base.Read (db, reader);

            int numlists = this.HeaderTwo;

            reader.ReadBytes (this.HeaderOne - 12);

            playlists.Clear ();

            for (int i = 0; i < numlists; i++) {
                bool isLibrary = false;
                
                if (i == 0)
                    isLibrary = true;
                
                PlaylistRecord list = new PlaylistRecord (isLibrary, IsBE);
                list.Read (db, reader);
                playlists.Add (list);
            }
        }

        public override void Save (DatabaseRecord db, BinaryWriter writer) {
            WriteName (writer);
            writer.Write (12 + PadLength);
            writer.Write (playlists.Count);
            writer.Write (new byte[PadLength]);

            foreach (PlaylistRecord rec in playlists) {
                rec.Save (db, writer);
            }
        }

        private int FindNextId () {
            int id = 0;
            foreach (PlaylistRecord record in playlists) {
                if (record.Id > id)
                    id = record.Id;
            }

            return id + 1;
        }

        public void AddPlaylist (PlaylistRecord record) {
            record.Id = FindNextId ();
            playlists.Add (record);
        }

        public void RemovePlaylist (PlaylistRecord record) {
            playlists.Remove (record);
        }
    }

    internal enum DetailType {
        Title = 1,
        Location = 2,
        Album = 3,
        Artist = 4,
        Genre = 5,
        Filetype = 6,
        EQ = 7,
        Comment = 8,
        Category = 9,
        Composer = 12,
        Grouping = 13,
        PodcastUrl = 15,
        PodcastUrl2 = 16,
        ChapterData = 17,
        PlaylistData = 50,
        PlaylistRules = 51,
        LibraryIndex = 52,
        Misc = 100
    }

    internal enum IndexType {
        Track = 3,
        Album = 4,
        Artist = 5,
        Genre = 7,
        Composer = 18
    }
    
    internal class DetailRecord : TrackDbRecord {

        private static UnicodeEncoding encoding = new UnicodeEncoding (false, false);

        private int unknownOne;
        private int unknownTwo;
        private int unknownThree;
        private byte[] chapterData;
        
        public DetailType Type;
        public string Value;
        public int Position = 1;

        public IndexType IndexType;
        public int[] LibraryIndices;

        public DetailRecord (bool isbe) : base (isbe) {
            this.Name = "mhod";
            this.HeaderOne = 24; // this is always the value for mhods
        }

        public DetailRecord (DetailType type, string value, bool isbe) : this (isbe) {
            this.Type = type;
            this.Value = value;
        }

        public override void Read (DatabaseRecord db, BinaryReader reader) {
            base.Read (db, reader);

            byte[] body = reader.ReadBytes (this.HeaderTwo - 12);
            
            Type = (DetailType) ToInt32 (body, 0);

            if ((int) Type > 50 && Type != DetailType.Misc && Type != DetailType.LibraryIndex)
                throw new DatabaseReadException ("Unsupported detail type: " + Type);

            unknownOne = ToInt32 (body, 4);
            unknownTwo = ToInt32 (body, 8);
            
            if ((int) Type < 50) {
                if (Type == DetailType.PodcastUrl ||
                    Type == DetailType.PodcastUrl2) {

                    Value = Encoding.UTF8.GetString (body, 12, body.Length - 12);
                } else if (Type == DetailType.ChapterData) {
                    // ugh ugh ugh, just preserve it for now -- no parsing

                    chapterData = new byte[body.Length - 12];
                    Array.Copy (body, 12, chapterData, 0, body.Length - 12);
                } else {
                    
                    Position = ToInt32 (body, 12);

                    int strlen = 0;
                    //int strenc = 0;
            
                    if ((int) Type < 50) {
                        // 'string' mhods       
                        strlen = ToInt32 (body, 16);
                        //strenc = ToInt32 (body, 20); // 0 == UTF16, 1 == UTF8
                        unknownThree = ToInt32 (body, 24);
                    }

                    // the strenc field is not what it was thought to be
                    // latest DBs have the field set to 1 even when the encoding
                    // is UTF-16. For now I'm just encoding as UTF-16
                    if (strlen >= 2 && body[29] == '\0') {
                        Value = encoding.GetString (body, 28, strlen);
                    } else {
                        Value = Encoding.UTF8.GetString(body, 28, strlen);
                    }
                }
            } else if (Type == DetailType.LibraryIndex) {
                IndexType = (IndexType) ToInt32 (body, 12);

                /*

                this is totally hosing stuff up on SLVR, and we don't use it anyway,
                so nuke it for now.
                
                int numEntries = ToInt32 (body, 16);

                ArrayList entries = new ArrayList ();
                
                for (int i = 0; i < numEntries; i++) {
                    int entry = ToInt32 (body, 56 + (i * 4));
                    entries.Add (entry);
                }

                LibraryIndices = (int[]) entries.ToArray (typeof (int));
                */
            } else if (Type == DetailType.Misc) {
                Position = ToInt32 (body, 12);
            }
        }

        public override void Save (DatabaseRecord db, BinaryWriter writer) {

            if (Value == null)
                Value = String.Empty;

            WriteName (writer);
            writer.Write (24);

            byte[] valbytes = null;

            if ((int) Type < 50) {
                if (Type == DetailType.PodcastUrl || Type == DetailType.PodcastUrl2) {
                    valbytes = Encoding.UTF8.GetBytes (Value);
                    writer.Write (24 + valbytes.Length);
                } else if (Type == DetailType.ChapterData) {
                    valbytes = chapterData;
                    writer.Write (24 + valbytes.Length);
                } else {
                    if (IsBE) {
                        // ugh. it looks like the big endian databases normally use utf8
                        valbytes = Encoding.UTF8.GetBytes (Value);
                    } else {
                        valbytes = encoding.GetBytes (Value);
                    }
                    
                    writer.Write (40 + valbytes.Length);
                }
            } else if (Type == DetailType.LibraryIndex) {
                writer.Write (72 + (4 * LibraryIndices.Length));
            } else if (Type == DetailType.Misc) {
                writer.Write (44);
            }

            writer.Write ((int) Type);
            writer.Write (unknownOne);
            writer.Write (unknownTwo);

            if ((int) Type < 50) {
                if (Type == DetailType.PodcastUrl || Type == DetailType.PodcastUrl2 ||
                    Type == DetailType.ChapterData) {
                    writer.Write (valbytes);
                } else {
                    writer.Write (Position);
                    writer.Write (valbytes.Length);
                    writer.Write (0);
                    writer.Write (unknownThree);
                    writer.Write (valbytes);
                }
            } else if (Type == DetailType.LibraryIndex) {
                writer.Write ((int) IndexType);
                writer.Write (LibraryIndices.Length);
                writer.Write (new byte[40]);

                foreach (int index in LibraryIndices) {
                    writer.Write (index);
                }
            } else if (Type == DetailType.Misc) {
                writer.Write (Position);
                writer.Write (new byte[16]); // just padding
            }
        }
    }

    internal enum TrackRecordType {
        MP3 = 0x101,
        AAC = 0x0
    }

    internal class TrackRecord : TrackDbRecord {

        private short unknownThree = 0;
        private short unknownFour = 1;
        private int unknownFive;
        private int unknownSix = 0x472c4400;
        private short unknownSeven;
        private short unknownEight = 0x0000000c;
        private int unknownNine;
        private int unknownTen;
        private int unknownEleven;
        private int unknownTwelve;
        private byte unknownThirteen;
        private int unknownFourteen;
        private int unknownFifteen;
        private int unknownSixteen;
        private int unknownSeventeen;
        private int unknownEighteen;
        private int unknownNineteen;
        private int playCountDup;

        private List<DetailRecord> details = new List<DetailRecord> ();

        public int Id;
        public bool Hidden = false;
        public TrackRecordType Type = TrackRecordType.MP3;
        public byte CompilationFlag = 0;
        public byte Rating;
        public uint Date;
        public int Size;
        public int Length;
        public int TrackNumber = 1;
        public int TotalTracks = 1;
        public int Year;
        public int BitRate;
        public ushort SampleRate;
        public int Volume;
        public int StartTime;
        public int StopTime;
        public int SoundCheck;
        public int PlayCount;
        public uint LastPlayedTime;
        public int DiscNumber;
        public int TotalDiscs;
        public int UserId;
        public uint LastModifiedTime;
        public int BookmarkTime;
        public long DatabaseId;
        public byte Checked;
        public byte ApplicationRating;
        public short BPM;
        public short ArtworkCount;
        public int ArtworkSize;
        public DateTime DateReleased;
        public bool HasArtwork;
        public bool SkipWhenShuffle;
        public bool RememberPosition;
        public bool UnknownPodcastFlag;
        public bool HasLyrics;
        public MediaType MediaType = MediaType.Audio;
        public bool NotPlayedMark;
        public int SampleCount;
        public int SeasonNumber;
        public int EpisodeNumber;

        public IList<DetailRecord> Details {
            get { return new ReadOnlyCollection<DetailRecord> (details); }
        }

        public TrackRecord (bool isbe) : base (isbe) {
            this.Name = "mhit";
        }

        public void AddDetail (DetailRecord detail) {
            details.Add (detail);
        }

        public void RemoveDetail (DetailRecord detail) {
            details.Remove (detail);
        }

        public DetailRecord GetDetail (DetailType type) {
            foreach (DetailRecord detail in details) {
                if (detail.Type == type)
                    return detail;
            }

            DetailRecord rec = new DetailRecord (IsBE);
            rec.Type = type;
            AddDetail (rec);
            
            return rec;
        }

        public override void Read (DatabaseRecord db, BinaryReader reader) {

            base.Read (db, reader);
            
            byte[] body = reader.ReadBytes (this.HeaderOne - 12);

            int numDetails = ToInt32 (body, 0);
            Id = ToInt32 (body, 4);
            Hidden = ToInt32 (body, 8) == 1 ? false : true;
            Type = (TrackRecordType) ToInt16 (body, 16);
            CompilationFlag = body[18];
            Rating = body[19];
            Date = ToUInt32 (body, 20);
            Size = ToInt32 (body, 24);
            Length = ToInt32 (body, 28);
            TrackNumber = ToInt32 (body, 32);
            TotalTracks = ToInt32 (body, 36);
            Year = ToInt32 (body, 40);
            BitRate = ToInt32 (body, 44);
            unknownThree = ToInt16 (body, 48);
            SampleRate = ToUInt16 (body, 50);
            Volume = ToInt32 (body, 52);
            StartTime = ToInt32 (body, 56);
            StopTime = ToInt32 (body, 60);
            SoundCheck = ToInt32 (body, 64);
            PlayCount = ToInt32 (body, 68);
            playCountDup = ToInt32 (body, 72);
            LastPlayedTime = ToUInt32 (body, 76);
            DiscNumber = ToInt32 (body, 80);
            TotalDiscs = ToInt32 (body, 84);
            UserId = ToInt32 (body, 88);
            LastModifiedTime = ToUInt32 (body, 92);
            BookmarkTime = ToInt32 (body, 96);
            DatabaseId = ToInt64 (body, 100);
            Checked = body[108];
            ApplicationRating = body[109];
            BPM = ToInt16 (body, 110);
            ArtworkCount = ToInt16 (body, 114);
            unknownFour = ToInt16 (body, 112);
            ArtworkSize = ToInt32 (body, 116);
            unknownFive = ToInt32 (body, 120);
            unknownSix = ToInt32 (body, 124);
            DateReleased = Utility.MacTimeToDate (ToUInt32 (body, 128));
            unknownSeven = ToInt16 (body, 132);
            unknownEight = ToInt16 (body, 134);
            unknownNine = ToInt32 (body, 136);
            unknownTen = ToInt32 (body, 140);
            unknownEleven = ToInt32 (body, 144);
            unknownTwelve = ToInt32 (body, 148);

            if (db.Version >= 12) {
                HasArtwork = (int) body[152] == 1 ? true : false;
                SkipWhenShuffle = (int) body[153] == 1 ? true : false;
                RememberPosition = (int) body[154] == 1 ? true : false;
                UnknownPodcastFlag = (int) body[155] == 0 ? false : true;
                HasLyrics = (int) body[164] == 1 ? true : false;
                if ((int) body[165] == 1) {
                    this.MediaType = MediaType.Movie;
                }

                NotPlayedMark = (int) body[166] == 2 ? true : false;
                unknownThirteen = body[167];
                unknownFourteen = ToInt32 (body, 168);
                unknownFifteen = ToInt32 (body, 172);
                SampleCount = ToInt32 (body, 176);
                unknownSixteen = ToInt32 (body, 180);
                unknownSeventeen = ToInt32 (body, 184);
                unknownEighteen = ToInt32 (body, 188);
                unknownNineteen = ToInt32 (body, 192);

                if (this.MediaType != MediaType.Movie) {
                    MediaType = ToMediaType (ToInt32 (body, 196));
                }

                SeasonNumber = ToInt32 (body, 200);
                EpisodeNumber = ToInt32 (body, 204);
            }

            details.Clear ();

            for (int i = 0; i < numDetails; i++) {
                DetailRecord rec = new DetailRecord (IsBE);
                rec.Read (db, reader);
                details.Add (rec);
            }
        }

        public override void Save (DatabaseRecord db, BinaryWriter writer) {

            MemoryStream stream = new MemoryStream ();
            BinaryWriter childWriter = new EndianBinaryWriter (stream, IsBE);

            foreach (DetailRecord rec in details) {
                rec.Save (db, childWriter);
            }

            childWriter.Flush ();
            byte[] childData = stream.GetBuffer ();
            int childDataLength = (int) stream.Length;
            childWriter.Close ();

            int len;
            if (db.Version >= 12) {
                len = 244;
            } else {
                len = 156;
            }

            WriteName (writer);
            writer.Write (len);
            writer.Write (len + childDataLength);

            writer.Write (details.Count);
            writer.Write (Id);
            writer.Write (Hidden ? 0 : 1);

            switch (Type) {
            case TrackRecordType.MP3:
                writer.Write (new char[] { 'M', 'P', '3', ' ' });
                break;
            case TrackRecordType.AAC:
                writer.Write (new char[] { 'A', 'A', 'C', ' ' });
                break;
            default:
                writer.Write ((Int32) 0);
                break;
            }
            
            writer.Write ((short) Type);
            writer.Write (CompilationFlag);
            writer.Write (Rating);
            writer.Write (Date);
            writer.Write (Size);
            writer.Write (Length);
            writer.Write (TrackNumber);
            writer.Write (TotalTracks);
            writer.Write (Year);
            writer.Write (BitRate);
            writer.Write (unknownThree);
            writer.Write (SampleRate);
            writer.Write (Volume);
            writer.Write (StartTime);
            writer.Write (StopTime);
            writer.Write (SoundCheck);
            writer.Write (PlayCount);
            writer.Write (playCountDup);
            writer.Write (LastPlayedTime);
            writer.Write (DiscNumber);
            writer.Write (TotalDiscs);
            writer.Write (UserId);
            writer.Write (LastModifiedTime);
            writer.Write (BookmarkTime);
            writer.Write (DatabaseId);
            writer.Write (Checked);
            writer.Write (ApplicationRating);
            writer.Write (BPM);
            writer.Write (ArtworkCount);
            writer.Write (unknownFour);
            writer.Write (ArtworkSize);
            writer.Write (unknownFive);
            writer.Write (unknownSix);
            writer.Write (Utility.DateToMacTime (DateReleased));
            writer.Write (unknownSeven);
            writer.Write (unknownEight);
            writer.Write (unknownNine);
            writer.Write (unknownTen);
            writer.Write (unknownEleven);
            writer.Write (unknownTwelve);

            if (db.Version >= 12) {
                writer.Write (HasArtwork ? (byte) 1 : (byte) 0);
                writer.Write (SkipWhenShuffle ? (byte) 1 : (byte) 0);
                writer.Write (RememberPosition ? (byte) 1 : (byte) 0);
                writer.Write (UnknownPodcastFlag ? (byte) 1 : (byte) 0);
                writer.Write (DatabaseId);
                writer.Write (HasLyrics ? (byte) 1 : (byte) 0);
                writer.Write (this.MediaType == MediaType.Movie ? (byte) 1 : (byte) 0);
                writer.Write (NotPlayedMark ? (byte) 2 : (byte) 1);
                writer.Write (unknownThirteen);
                writer.Write (unknownFourteen);
                writer.Write (unknownFifteen);
                writer.Write (SampleCount);
                writer.Write (unknownSixteen);
                writer.Write (unknownSeventeen);
                writer.Write (unknownEighteen);
                writer.Write (unknownNineteen);
                writer.Write (FromMediaType (this.MediaType));
                writer.Write (SeasonNumber);
                writer.Write (EpisodeNumber);
                writer.Write (new byte[24]);
            }

            writer.Flush ();
            
            writer.Write (childData, 0, childDataLength);
        }

        private int FromMediaType (MediaType type) {
            switch (type) {
            case MediaType.AudioVideo:
                return 0;
            case MediaType.Audio:
                return 1;
            case MediaType.Movie:
            case MediaType.Video:
                return 2;
            case MediaType.Podcast:
                return 4;
            case MediaType.VideoPodcast:
                return 6;
            case MediaType.Audiobook:
                return 8;
            case MediaType.MusicVideo:
                return 32;
            case MediaType.TVShow:
                return 64;
            default:
                return 0;
            }
        }

        private MediaType ToMediaType (int type) {
            switch (type) {
            case 0:
                return MediaType.AudioVideo;
            case 1:
                return MediaType.Audio;
            case 2:
                return MediaType.Video;
            case 4:
                return MediaType.Podcast;
            case 6:
                return MediaType.VideoPodcast;
            case 8:
                return MediaType.Audiobook;
            case 32:
                return MediaType.MusicVideo;
            case 64:
                return MediaType.TVShow;
            default:
                return MediaType.AudioVideo;
            }
        }
    }

    internal class TrackListRecord : TrackDbRecord, IEnumerable {

        public long NextDatabaseId = 1;

        private List<TrackRecord> tracks = new List<TrackRecord> ();

        public IList<TrackRecord> Tracks {
            get { return new ReadOnlyCollection<TrackRecord> (tracks); }
        }

        public TrackListRecord (bool isbe) : base (isbe) {
            this.Name = "mhlt";
        }

        public void Remove (int id) {
            foreach (TrackRecord track in tracks) {
                if (track.Id == id) {
                    tracks.Remove (track);
                    break;
                }
            }
        }

        public void Add (TrackRecord track) {
            tracks.Add (track);
        }

        public IEnumerator GetEnumerator () {
            return tracks.GetEnumerator ();
        }

        public TrackRecord LookupTrack (int id) {
            foreach (TrackRecord record in tracks) {
                if (record.Id == id)
                    return record;
            }

            return null;
        }

        public int IndexOf (int id) {
            for (int i = 0; i < tracks.Count; i++) {
                if ((tracks[i] as TrackRecord).Id == id)
                    return i;
            }

            return -1;
        }
        
        public override void Read (DatabaseRecord db, BinaryReader reader) {

            base.Read (db, reader);
            
            reader.ReadBytes (this.HeaderOne - 12);

            int trackCount = this.HeaderTwo;

            tracks.Clear ();
            
            for (int i = 0; i < trackCount; i++) {
                TrackRecord rec = new TrackRecord (IsBE);
                rec.Read (db, reader);

                if (rec.DatabaseId > NextDatabaseId) {
                    NextDatabaseId = rec.DatabaseId + 1;
                }
                
                tracks.Add (rec);
            }
        }

        public override void Save (DatabaseRecord db, BinaryWriter writer) {

            MemoryStream stream = new MemoryStream ();
            BinaryWriter childWriter = new EndianBinaryWriter (stream, IsBE);

            foreach (TrackRecord rec in tracks) {
                rec.Save (db, childWriter);
            }

            childWriter.Flush ();
            byte[] childData = stream.GetBuffer ();
            int childDataLength = (int) stream.Length;
            childWriter.Close ();

            WriteName (writer);
            writer.Write (12 + PadLength);
            writer.Write (tracks.Count);
            writer.Write (new byte[PadLength]);
            writer.Write (childData, 0, childDataLength);
        }
    }

    internal enum DataSetIndex {
        Library = 1,
        Playlist = 2,
        PlaylistDuplicate = 3
    }

    internal class DataSetRecord : TrackDbRecord {

        public DataSetIndex Index;

        public TrackListRecord TrackList;
        public PlaylistListRecord PlaylistList;

        public PlaylistRecord Library {
            get {
                if (PlaylistList != null) {
                    return PlaylistList[0];
                }

                return null;
            }
        }

        public DataSetRecord (bool isbe) : base (isbe) {
            this.Name = "mhsd";
        }

        public DataSetRecord (DataSetIndex index, bool isbe) : this (isbe) {
            this.Index = index;

            if (Index == DataSetIndex.Library) {
                TrackList = new TrackListRecord (isbe);
            } else {
                PlaylistList = new PlaylistListRecord (isbe);
            }
        }

        public override void Read (DatabaseRecord db, BinaryReader reader) {
            base.Read (db, reader);

            byte[] body = reader.ReadBytes (this.HeaderOne - 12);

            Index = (DataSetIndex) ToInt32 (body, 0);

            switch (Index) {
            case DataSetIndex.Library:
                this.TrackList = new TrackListRecord (IsBE);
                this.TrackList.Read (db, reader);
                break;
            case DataSetIndex.Playlist:
            case DataSetIndex.PlaylistDuplicate:
                this.PlaylistList = new PlaylistListRecord (IsBE);
                this.PlaylistList.Read (db, reader);
                break;
            default:
                throw new DatabaseReadException ("Can't handle dataset index: " + Index);
            }
        }

        public override void Save (DatabaseRecord db, BinaryWriter writer) {

            byte[] childData;
            int childDataLength;

            MemoryStream stream = new MemoryStream ();
            BinaryWriter childWriter = new EndianBinaryWriter (stream, IsBE);

            switch (Index) {
            case DataSetIndex.Library:
                TrackList.Save (db, childWriter);
                break;
            case DataSetIndex.Playlist:
            case DataSetIndex.PlaylistDuplicate:
                PlaylistList.Save (db, childWriter);
                break;
            default:
                throw new DatabaseReadException ("Can't handle DataSet record index: " + Index);
            }

            childWriter.Flush ();
            childData = stream.GetBuffer ();
            childDataLength = (int) stream.Length;
            childWriter.Close ();

            WriteName (writer);
            writer.Write (16 + PadLength);
            writer.Write (16 + PadLength + childDataLength);
            writer.Write ((int) Index);
            writer.Write (new byte[PadLength]);
            writer.Write (childData, 0, childDataLength);
        }
    }

    internal class DatabaseRecord : TrackDbRecord {

        private const int MaxSupportedVersion = 19;
        private const int TrackIdStart = 1000;

        private int unknownOne = 1;
        private int unknownTwo = 2;

        private List<DataSetRecord> datasets = new List<DataSetRecord> ();

        public int Version = MaxSupportedVersion;
        public long Id;

        public DataSetRecord this[DataSetIndex index] {
            get {
                foreach (DataSetRecord rec in datasets) {
                    if (rec.Index == index)
                        return rec;
                }

                return null;
            }
        }

        public DatabaseRecord (bool isbe) : base (isbe) {
            datasets.Add (new DataSetRecord (DataSetIndex.Library, isbe));

            DataSetRecord plrec = new DataSetRecord (DataSetIndex.Playlist, isbe);
            datasets.Add (plrec);

            DataSetRecord plrec2 = new DataSetRecord (DataSetIndex.PlaylistDuplicate, isbe);
            plrec2.PlaylistList = plrec.PlaylistList;
            datasets.Add (plrec2);

            this.Name = "mhbd";
        }
        
        public override void Read (DatabaseRecord db, BinaryReader reader) {
            base.Read (db, reader);

            byte[] body = reader.ReadBytes (this.HeaderOne - 12);
            
            unknownOne = ToInt32 (body, 0);
            Version = ToInt32 (body, 4);
            int childrenCount = ToInt32 (body, 8);
            Id = ToInt64 (body, 12);
            unknownTwo = ToInt32 (body, 20);

            if (Version > MaxSupportedVersion)
                throw new DatabaseReadException ("Detected unsupported database version {0}", Version);
            
            datasets.Clear ();

            for (int i = 0; i < childrenCount; i++) {
                DataSetRecord rec = new DataSetRecord (IsBE);
                rec.Read (this, reader);
                datasets.Add (rec);
            }

            // make the duplicate record have the same stuff as the 'real' one
            if (this[DataSetIndex.PlaylistDuplicate] != null)
                this[DataSetIndex.PlaylistDuplicate].PlaylistList = this[DataSetIndex.Playlist].PlaylistList;
        }

        private void ReassignTrackIds () {
            Hashtable oldids = new Hashtable ();

            int id = TrackIdStart;
            foreach (TrackRecord track in this[DataSetIndex.Library].TrackList.Tracks) {
                oldids[track.Id] = id;
                track.Id = id++;
            }

            foreach (PlaylistRecord pl in this[DataSetIndex.Playlist].PlaylistList.Playlists) {
                foreach (PlaylistItemRecord item in pl.Items) {
                    if (oldids[item.TrackId] == null)
                        continue;
                    
                    item.TrackId = (int) oldids[item.TrackId];
                }
            }
        }

        public override void Save (DatabaseRecord db, BinaryWriter writer) {

            ReassignTrackIds ();
            
            MemoryStream stream = new MemoryStream ();
            BinaryWriter childWriter = new EndianBinaryWriter (stream, IsBE);

            foreach (DataSetRecord rec in datasets) {
                rec.Save (db, childWriter);
            }

            childWriter.Flush ();
            byte[] childData = stream.GetBuffer ();
            int childDataLength = (int) stream.Length;
            childWriter.Close ();

            WriteName (writer);
            writer.Write (36 + PadLength);
            writer.Write (36 + PadLength + childDataLength);

            writer.Write (unknownOne);
            writer.Write (Version);
            writer.Write (datasets.Count);
            writer.Write (Id);
            writer.Write (unknownTwo);
            writer.Write (new byte[PadLength]);
            writer.Write (childData, 0, childDataLength);
        }
    }

    public class InsufficientSpaceException : ApplicationException {

        public InsufficientSpaceException (string format, params object[] args) :
            base (String.Format (format, args)) {
        }
    }

    public class TrackSaveProgressArgs : EventArgs {

        private Track track;
        private double trackPercent;
        private int completed;
        private int total;
        
        public Track CurrentTrack {
            get { return track; }
        }

        public double TrackProgress {
            get { return trackPercent; }
        }

        public double TotalProgress {
            get {
                if (completed == total) {
                    return 1.0;
                } else {
                    double fraction = (double) completed / (double) total;
                    
                    return fraction + (1.0 / (double) total) * trackPercent;
                }
            }
        }

        public int TracksCompleted {
            get { return completed; }
        }

        public int TracksTotal {
            get { return total; }
        }

        public TrackSaveProgressArgs (Track track, double trackPercent, int completed, int total) {
            this.track = track;
            this.trackPercent = trackPercent;
            this.completed = completed;
            this.total = total;
        }
    }

    public delegate void PlaylistHandler (object o, Playlist playlist);
    public delegate void TrackSaveProgressHandler (object o, TrackSaveProgressArgs args);
    public class Album
    {
        public string Name = "";
        public List<Track> Tracks = new List<Track>();
        public Album(string name){
            Name = name;
        }
        public override string ToString()
        {
            return TrackDatabase.convertName(Name);
        }
    }

    public class Artist
    {
        public string Name = "";
        private List<Track> tracks = new List<Track>();

        public List<Track> Tracks
        {
            get { return tracks; }
            set { 
                tracks = value;
                fillAbums();
            }
        }

        private void fillAbums()
        {

            Dictionary<string, List<Track>> a = new Dictionary<string, List<Track>>();
            foreach (Track track in Tracks)
            {
                string aname = track.Album == null ? "UNKNOWN" : track.Album;
                if (!a.ContainsKey(aname))
                {
                    a[aname] = new List<Track>();
                }
                a[aname].Add(track);
            }
            foreach (string aname in a.Keys)
            {
                Album newa = new Album(aname);
                newa.Tracks = a[aname];
                Albums.Add(newa);
            }
        }
	

        public List<Album> Albums = new List<Album>();

        public Artist(string name)
        {
            Name = name;
        }
        public override string ToString()
        {
            return TrackDatabase.convertName(Name);
        }
    }
    public class TrackDatabase {
        static public bool IsAscii(string data)
        {
            // assume empty string to be ascii
            if ((data == null) || (data.Length == 0))
                return true;
            foreach (char c in data)
            {
                if ((int)c > 127 || (int)c < 30)
                {
                    return false;
                }
            }

            return true;
        }

        static public string convertName(string name)
        {
            if (IsAscii(name)) return name;
            byte[] bytes = Encoding.UTF8.GetBytes(name);
            string r = UnicodeEncoding.Unicode.GetString(bytes);

            return r;
        }
        private const int CopyBufferSize = 8192;
        private const double PercentThreshold = 0.10;
        
        private DatabaseRecord dbrec;

        private List<Track> tracks = new List<Track> ();

        private SortedList<string, Album> albums = null;
        private SortedList<string, Artist> artists = null;

        private List<Track> tracksToAdd = new List<Track> ();
        private List<Track> tracksToRemove = new List<Track> ();

        private List<Playlist> playlists = new List<Playlist> ();
        private List<Playlist> otgPlaylists = new List<Playlist> ();
        
        private Random random = new Random();
        private Device device;

        private PhotoDatabase artdb;

        public event EventHandler SaveStarted;
        public event TrackSaveProgressHandler SaveProgressChanged;
        public event EventHandler SaveEnded;

        public event PlaylistHandler PlaylistAdded;
        public event PlaylistHandler PlaylistRemoved;

        public event EventHandler Reloaded;

        private string ControlPath {
            get { return device.ControlPath; }
        }

        private string ControlDirectoryName {
            get {
                // so lame
                if (device.ControlPath.IndexOf ("iPod_Control") >= 0)
                    return "iPod_Control";
                else
                    return "iTunes_Control";
            }
        }

        private string TrackDbPath {
            get { return ControlPath + "iTunes/iTunesDB"; }
        }

        private string TrackDbBackupPath {
            get { return TrackDbPath + ".bak"; }
        }

        private string MusicBasePath {
            get { return ControlPath + "Music"; }
        }

        private string PlayCountsPath {
            get { return ControlPath + "iTunes/Play Counts"; }
        }

        internal string Name {
            get {
                if (dbrec == null)
                    return null;

                return dbrec[DataSetIndex.Playlist].Library.PlaylistName;
            } set {
                if (dbrec == null)
                    return;

                dbrec[DataSetIndex.Playlist].Library.PlaylistName = value;
            }
        }

        public Device Device {
            get { return device; }
        }

        public IList<Track> Tracks {
            get {
                return new ReadOnlyCollection<Track> (tracks);
            }
        }

        public SortedList<string, Album> Albums
        {
            get
            {
                if (albums != null) return albums;
                albums = new SortedList<string, Album>();
                foreach (Artist artist in Artists.Values)
                {
                    foreach (Album album in artist.Albums)
                    {
                        albums.Add(album.Name, album);
                    }
                }
                return albums;        
            }
        }

        public SortedList<string, Artist> Artists
        {
            get
            {
                if (artists != null) return artists;
                artists = new SortedList<string, Artist>();

                Dictionary<string, List<Track>> a = new Dictionary<string, List<Track>>();
                foreach (Track track in Tracks)
                {
                    string aname = track.Artist == null ? "UNKNOWN" : track.Artist;
                    if (!a.ContainsKey(aname))
                    {
                        a[aname] = new List<Track>();
                    }
                    a[aname].Add(track);
                }
                foreach (string aname in a.Keys)
                {
                    Artist newa = new Artist(aname);
                    newa.Tracks = a[aname];
                    artists.Add(newa.Name, newa);
                }
                return artists;
            }
        }

        public IList<Playlist> Playlists {
            get {
                return new ReadOnlyCollection<Playlist> (playlists);
            }
        }

        public IList<Playlist> OnTheGoPlaylists {
            get {
                return new ReadOnlyCollection<Playlist> (otgPlaylists);
            }
        }

        public int Version {
            get { return dbrec.Version; }
        }

        internal PhotoDatabase ArtworkDatabase {
            get { return artdb; }
        }

        internal TrackDatabase (Device device) : this (device, false) {
        }
        
        internal TrackDatabase (Device device, bool createFresh) {
            this.device = device;
            
            if(createFresh && File.Exists(TrackDbPath)) {
                File.Copy (TrackDbPath, TrackDbBackupPath, true);
            }

            Reload (createFresh);

            //TODO:artdb = new PhotoDatabase (device, false, createFresh);
        }
        
        
        private void Clear () {
            dbrec = null;
            tracks.Clear ();
            tracksToAdd.Clear ();
            tracksToRemove.Clear ();
            playlists.Clear ();
            otgPlaylists.Clear ();
        }

        private void LoadPlayCounts () {
            FileInfo info = new FileInfo (PlayCountsPath);

            if (!info.Exists || info.Length == 0)
                return;
            
            using (BinaryReader reader = new BinaryReader (File.OpenRead (PlayCountsPath))) {

                byte[] header = reader.ReadBytes (96);
                int entryLength = dbrec.ToInt32 (header, 8);
                int numEntries = dbrec.ToInt32 (header, 12);

                for (int i = 0; i < numEntries; i++) {
                    
                    byte[] entry = reader.ReadBytes (entryLength);
                    
                    (tracks[i] as Track).LatestPlayCount = dbrec.ToInt32 (entry, 0);
                    (tracks[i] as Track).PlayCount += (tracks[i] as Track).LatestPlayCount;

                    uint lastPlayed = dbrec.ToUInt32 (entry, 4);
                    if (lastPlayed > 0) {
                        (tracks[i] as Track).Record.LastPlayedTime = lastPlayed;
                    }

                    // if it has rating info, get it
                    if (entryLength >= 16) {
                        // Why is this one byte in iTunesDB and 4 here?
                        
                        int rating = dbrec.ToInt32 (entry, 12);
                        (tracks[i] as Track).Record.Rating  = (byte) rating;
                    }
                }
            }
        }

        private bool LoadOnTheGo (int num) {
            string path = ControlPath + "iTunes/OTGPlaylistInfo";

            if (num != 0) {
                path += "_" + num;
            }                

            FileInfo finfo = new FileInfo (path);
            
            if (!finfo.Exists || finfo.Length == 0) {
                return false;
            }

            List<Track> otgtracks = new List<Track> ();
            
            using (BinaryReader reader = new BinaryReader (File.OpenRead (path))) {

                byte[] header = reader.ReadBytes (20);

                int numTracks = dbrec.ToInt32 (header, 12);

                for (int i = 0; i < numTracks; i++) {
                    int index = reader.ReadInt32 ();

                    if (dbrec.IsBE)
                        index = Utility.Swap (index);

                    otgtracks.Add (tracks[index]);
                }
            }

            string title = "On-The-Go";

            if (num != 0) {
                title += " " + num;
            }
            
            otgPlaylists.Add (new Playlist (this, title, otgtracks));
            return true;
        }

        private void LoadOnTheGo () {
            int i = 0;
            while (LoadOnTheGo (i++));
        }

        public void Reload () {
            Reload(false);
        }

        private void Reload (bool createFresh) {
        
            Clear ();

            // This blows, we need to use the device model number or something
            bool useBE = device.IsBE;
            //ControlPath.EndsWith ("iTunes_Control");
                
            if (!File.Exists (TrackDbPath) || createFresh) {
                dbrec = new DatabaseRecord (useBE);
                LoadOnTheGo ();
                return;
            }

            using (BinaryReader reader = new BinaryReader (File.OpenRead (TrackDbPath))) {

                dbrec = new DatabaseRecord (useBE);
                dbrec.Read (null, reader);

                // Load the tracks
                foreach (TrackRecord track in dbrec[DataSetIndex.Library].TrackList) {
                    Track t = new Track (this, track);
                    tracks.Add (t);
                }

                // Load the play counts
                LoadPlayCounts ();

                // Load the playlists
                foreach (PlaylistRecord listrec in dbrec[DataSetIndex.Playlist].PlaylistList) {
                    if (listrec.IsHidden)
                        continue;
                        
                    Playlist list = new Playlist (this, listrec);
                    playlists.Add (list);
                }

                // Load the On-The-Go playlist
                LoadOnTheGo ();

                if (Reloaded != null)
                    Reloaded (this, new EventArgs ());
            }
        }

        internal bool IsTrackOnDevice(string path) {
            return path.StartsWith (MusicBasePath + "/F");
        }

        private string FormatSpace (UInt64 bytes) {
            return String.Format ("{0} MB", bytes/1024/1024);
        }

        private void CheckFreeSpace () {

            device.RescanDisk ();

            UInt64 available = 0;//TODO: device.VolumeAvailable;
            UInt64 required = 0;

            // we're going to free some up, so add that
            foreach (Track track in tracksToRemove) {
                available += (UInt64) track.Size;
            }

            foreach (Track track in tracksToAdd) {
                if (!IsTrackOnDevice (track.FileName)) {
                    required += (UInt64) track.Size;
                }
            }

            if (required >= available)
                throw new InsufficientSpaceException ("Not enough free space on '{0}'.  {1} required, " +
                                                      "but only {2} available.", ""/*TODO:device.Name*/,
                                                      FormatSpace (required), FormatSpace (available));
        }

        
        private string MakeUniquePodTrackPath(string filename) {
            const string allowed = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            
            string basePath = MusicBasePath + "/";
            string uniqueName = String.Empty;
            string ext = (new FileInfo(filename)).Extension.ToLower();
            
            do {
                uniqueName = String.Format("F{0:00}/", random.Next(50));
                
                if(!Directory.Exists(basePath + uniqueName))
                    Directory.CreateDirectory(basePath + uniqueName);
                
                for(int i = 0; i < 4; i++)
                    uniqueName += allowed[random.Next(allowed.Length)];

                uniqueName += ext;
            } while(File.Exists(basePath + uniqueName));
				
            return uniqueName.Replace("/", ":");
        }

        private void CopyTrack (Track track, string dest, int completed, int total) {
            BinaryReader reader = null;
            BinaryWriter writer = null;
            
            try {
                FileInfo info = new FileInfo (track.FileName);
                long length = info.Length;
                long count = 0;
                double lastPercent = 0.0;

                reader = new BinaryReader (new BufferedStream (File.OpenRead (track.FileName)));
                writer = new BinaryWriter (new BufferedStream (File.Open (dest, FileMode.Create)));
                
                do {
                    byte[] buf = reader.ReadBytes (CopyBufferSize);
                    writer.Write (buf);
                    count += buf.Length;

                    double percent = (double) count / (double) length;
                    if (percent >= lastPercent + PercentThreshold && SaveProgressChanged != null) {
                        TrackSaveProgressArgs args = new TrackSaveProgressArgs (track, percent,
                                                                                completed, total);

                        try {
                            SaveProgressChanged (this, args);
                        } catch (Exception e) {
                            Console.Error.WriteLine ("Exception in progress handler: " + e);
                        }
                        
                        lastPercent = percent;
                    }
                } while (count < length);
            } finally {
                if (reader != null)
                    reader.Close ();

                if (writer != null)
                    writer.Close ();
            }
        }

        public void Save () {

            CheckFreeSpace ();

            // make sure all the new tracks have file names, and that they exist
            foreach (Track track in tracksToAdd) {
                if (track.FileName == null)
                    throw new DatabaseWriteException (String.Format ("Track '{0}' has no file assigned", track.Title));
                else if (!File.Exists (track.FileName)) {
                    throw new DatabaseWriteException (String.Format ("File '{0}' for track '{1}' does not exist",
                                                                     track.FileName, track.Title));
                }
            }

            if (SaveStarted != null)
                SaveStarted (this, new EventArgs ());

            // Back up the current track db
            if (File.Exists (TrackDbPath))
                File.Copy (TrackDbPath, TrackDbBackupPath, true);
            
            try {
                // Save the tracks db
                using (BinaryWriter writer = new EndianBinaryWriter (new FileStream (TrackDbPath, FileMode.Create),
                                                                     dbrec.IsBE)) {
                    dbrec.Save (dbrec, writer);
                }

                foreach (Track track in tracksToRemove) {
                    if (File.Exists (track.FileName))
                        File.Delete (track.FileName);
                }
                
                if (!Directory.Exists (MusicBasePath))
                    Directory.CreateDirectory (MusicBasePath);
                
                int completed = 0;
                
                // Copy tracks to iPod; if track is already in the Music directory structure, do not copy
                foreach (Track track in tracksToAdd) {
                    if (!IsTrackOnDevice (track.FileName)) {
                        string dest = GetFilesystemPath (track.Record.GetDetail (DetailType.Location).Value);
                        CopyTrack (track, dest, completed++, tracksToAdd.Count);
                        track.FileName = dest;
                    }
                }

                // Save artwork database
                artdb.Save ();

                // Save the shuffle tracks db (will only create if device is shuffle);
                try {
                    ShuffleTrackDatabase.Save (device);
                } catch (Exception) {}
                
                // The play count file is invalid now, so we'll remove it (even though the iPod would anyway)
                if (File.Exists (PlayCountsPath))
                    File.Delete (PlayCountsPath);

                // Force progress to 100% so the app can now we're in the "sync()" phase
                if (SaveProgressChanged != null) {
                    try {
                        SaveProgressChanged (this, new TrackSaveProgressArgs (null, 1.0, 1, 1));
                    } catch (Exception e) {
                        Console.Error.WriteLine ("Exception in progress handler: " + e);
                    }
                }

                // Remove empty Music "F" directories
                DirectoryInfo musicDir = new DirectoryInfo (MusicBasePath);
                foreach (DirectoryInfo fdir in musicDir.GetDirectories ()) {
                    try {
                        if (fdir.GetFiles ().Length == 0) {
                            fdir.Delete();
                        }
                    } catch {
                    }
                }

            } catch (Exception e) {
                // rollback the track db
                if (File.Exists (TrackDbBackupPath))
                    File.Copy (TrackDbBackupPath, TrackDbPath, true);

                throw new DatabaseWriteException (e, "Failed to save database");
            } finally {
                try {
                    device.RescanDisk();
                } catch(Exception) {}

                tracksToAdd.Clear ();
                tracksToRemove.Clear ();
                
                if (SaveEnded != null)
                    SaveEnded (this, new EventArgs ());
            }
        }

        internal string GetFilesystemPath (string ipodPath) {
            if (ipodPath == null)
                return null;
            else if (ipodPath == String.Empty)
                return String.Empty;
            
            return device.MountPoint + ipodPath.Replace (":", "/");
        }

        internal string GetPodPath (string path) {
            if (path == null || !path.StartsWith (device.MountPoint))
                return null;

            string ret = path.Replace (device.MountPoint, "");
            return ret.Replace ("/", ":");
        }

        internal string GetUniquePodPath (string path) {
            if (path == null)
                return null;

            return String.Format (":{0}:Music:{1}", ControlDirectoryName, MakeUniquePodTrackPath(path));
        }

        private int GetNextTrackId () {
            int max = 0;

            foreach (TrackRecord track in dbrec[DataSetIndex.Library].TrackList) {
                if (track.Id > max)
                    max = track.Id;
            }

            return max + 1;
        }

        private void AddTrack (Track track, bool existing) {
            dbrec[DataSetIndex.Library].TrackList.Add (track.Record);

            PlaylistItemRecord item = new PlaylistItemRecord (dbrec.IsBE);
            item.TrackId = track.Record.Id;
 
            dbrec[DataSetIndex.Playlist].Library.AddItem (item);

            if (!existing)
                tracksToAdd.Add (track);
            else if (tracksToRemove.Contains (track))
                tracksToRemove.Remove (track);
                
            tracks.Add (track);
        }

        public void RemoveTrack (Track track) {
            if (tracks.Contains (track)) {
                tracks.Remove (track);

                if (tracksToAdd.Contains (track))
                    tracksToAdd.Remove (track);
                else
                    tracksToRemove.Add (track);

                // remove from the track db
                dbrec[DataSetIndex.Library].TrackList.Remove (track.Record.Id);
                dbrec[DataSetIndex.Playlist].Library.RemoveTrack (track.Record.Id);

                // remove from cover art db
                Photo artPhoto = artdb.LookupPhotoByTrackId (track.Record.DatabaseId);
                if (artPhoto != null) {
                    artdb.RemovePhoto (artPhoto);
                }
                    
                // remove from the "normal" playlists
                foreach (Playlist list in playlists) {
                    list.RemoveTrack (track);
                }

                // remove from On-The-Go playlists
                foreach (Playlist list in otgPlaylists) {
                    list.RemoveOTGTrack (track);
                }
            }
        }

        public Track CreateTrack () {
            TrackRecord track = new TrackRecord (dbrec.IsBE);
            track.Id = GetNextTrackId ();
            track.Date = Utility.DateToMacTime (DateTime.Now);
            track.LastModifiedTime = track.Date;
            track.DatabaseId = dbrec[DataSetIndex.Library].TrackList.NextDatabaseId++;
            
            Track t = new Track (this, track);
            
            AddTrack (t, false);
            
            return t;
        }

        public Playlist CreatePlaylist (string name) {
            if (name == null)
                throw new ArgumentException ("name cannot be null");

            PlaylistRecord playrec = new PlaylistRecord (false, dbrec.IsBE);
            playrec.PlaylistName = name;
            
            dbrec[DataSetIndex.Playlist].PlaylistList.AddPlaylist (playrec);

            Playlist list = new Playlist (this, playrec);
            playlists.Add (list);

            if (PlaylistAdded != null)
                PlaylistAdded (this, list);
            
            return list;
        }

        public void RemovePlaylist (Playlist playlist) {
            if (playlist == null) {
                throw new InvalidOperationException ("playist is null");
            } else if (playlist.IsOnTheGo) {
                throw new InvalidOperationException ("The On-The-Go playlist cannot be removed.");
            }
            
            dbrec[DataSetIndex.Playlist].PlaylistList.RemovePlaylist (playlist.PlaylistRecord);
            playlists.Remove (playlist);

            if (PlaylistRemoved != null)
                PlaylistRemoved (this, playlist);
        }

        public Playlist LookupPlaylist (string name) {
            foreach (Playlist list in playlists) {
                if (list.Name == name)
                    return list;
            }

            return null;
        }

        internal Track GetTrackById (int id) {
            foreach (Track track in tracks) {
                if (track.Record.Id == id)
                    return track;
            }

            return null;
        }
    }
}
