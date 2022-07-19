using Android.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PanoraMovie.CSharp
{
    public class ItemClickListener : Java.Lang.Object, AdapterView.IOnItemClickListener
    {
        public Action<AdapterView?, View?, int, long> onItemClick;

        public ItemClickListener() : this((_, _, _, _) => { }) { }
        public ItemClickListener(Action<AdapterView?, View?, int, long> onItemClick)
        {
            this.onItemClick = onItemClick;
        }

        public void OnItemClick(AdapterView? parent, View? view, int position, long id) => onItemClick(parent, view, position, id);
    }
}
