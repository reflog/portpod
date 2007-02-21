using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace IPod {

    public class PhotoAlbum {

        private PhotoAlbumRecord record;

        private List<Photo> photos;

        public ReadOnlyCollection<Photo> Photos {
            get {
                return new ReadOnlyCollection<Photo> (photos);
            }
        }

        public string Name {
            get { return record.AlbumName; }
            set { record.AlbumName = value; }
        }

        internal bool IsMaster {
            get { return record.IsMaster; }
        }

        internal PhotoAlbumRecord Record {
            get { return record; }
        }
        
        internal PhotoAlbum (PhotoAlbumRecord record, PhotoDatabase db) {
            this.record = record;

            photos = new List<Photo> ();

            foreach (PhotoAlbumItemRecord item in record.Items) {
                Photo photo = db.LookupPhotoById (item.Id);
                photos.Add (photo);
            }
        }

        public void Add (Photo photo) {
            record.AddItem (new PhotoAlbumItemRecord (record.IsBE, photo.Id));
            photos.Add (photo);
        }

        public void Remove (Photo photo) {
            record.RemoveItem (photo.Id);
            photos.Remove (photo);
        }
    }
}
