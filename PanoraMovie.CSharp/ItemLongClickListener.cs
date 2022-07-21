using Android.Views;

namespace PanoraMovie.CSharp
{
    public class ItemLongClickListener : Java.Lang.Object, AdapterView.IOnItemLongClickListener
    {
        public Func<AdapterView?, View?, int, long, bool> onItemLongClick;

        public ItemLongClickListener() : this((_, _, _, _) => false) { }
        public ItemLongClickListener(Func<AdapterView?, View?, int, long, bool> onItemLongClick)
        {
            this.onItemLongClick = onItemLongClick;
        }

        public bool OnItemLongClick(AdapterView? parent, View? view, int position, long id) => onItemLongClick(parent, view, position, id);
    }
}
