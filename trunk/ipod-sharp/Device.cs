using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.Collections.ObjectModel;

namespace IPod
{
    internal enum EjectResult : uint
    {
        Ok,
        Error,
        Busy
    };

    public enum ArtworkUsage : int
    {
        Unknown = -1,
        Photo,
        Cover
    }

    public enum PixelFormat : int
    {
        Unknown = -1,
        Rgb565,
        Rgb565BE,
        IYUV
    }

    public class ArtworkFormat
    {

        private ArtworkUsage usage;
        private short width;
        private short height;
        private short correlationId;
        private int size;
        private PixelFormat pformat;
        private short rotation;

        public ArtworkUsage Usage
        {
            get { return usage; }
        }

        public short Width
        {
            get { return width; }
        }

        public short Height
        {
            get { return height; }
        }

        public int Size
        {
            get { return size; }
        }

        public PixelFormat PixelFormat
        {
            get { return pformat; }
        }

        public short Rotation
        {
            get { return rotation; }
        }

        internal short CorrelationId
        {
            get { return correlationId; }
        }

        internal ArtworkFormat(ArtworkUsage usage, short width, short height, short correlationId,
                                int size, PixelFormat pformat, short rotation)
        {
            this.usage = usage;
            this.width = width;
            this.height = height;
            this.correlationId = correlationId;
            this.size = size;
            this.pformat = pformat;
            this.rotation = rotation;
        }
    }

    public class Device
    {
        public string MountPoint = "";
        public string ControlPath = "iPod_Control/";
        internal bool IsBE
        {
            get { return ControlPath.EndsWith("iTunes_Control"); }
        }


        public DeviceModel Model;
        public DeviceGeneration Generation;
        public ArrayList equalizers;
        private EqualizerContainerRecord eqsrec;
        public TrackDatabase TrackDatabase ;
        public PhotoDatabase photos;
        private Dictionary<int, ArtworkFormat> formats = new Dictionary<int, ArtworkFormat>();

        public Device()
        {

            MountPoint = System.IO.Path.GetPathRoot(System.Windows.Forms.Application.StartupPath);
            
            if (!System.IO.Directory.Exists(MountPoint + ControlPath))
                MountPoint = "g:/";    

            ControlPath = MountPoint + ControlPath;

            TrackDatabase = new TrackDatabase(this);
        }

        public ReadOnlyCollection<ArtworkFormat> ArtworkFormats
        {
            get
            {
                return new ReadOnlyCollection<ArtworkFormat>(new List<ArtworkFormat>(formats.Values));
            }
        }

        internal ArtworkFormat LookupArtworkFormat(int p)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        internal void RescanDisk()
        {
            throw new Exception("The method or operation is not implemented.");
        }
    }
}
