using Android.Content;
using Android.Graphics;
using Android.Media;
using Android.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PanoraMovie.CSharp
{
	public class ListViewAdapter<T> : ArrayAdapter<T>
	{
		private Context context { get; init; }
		private Func<ListViewAdapter<T>, int, View?, ViewGroup?, View> viewGenerator;

		public ListViewAdapter(Context context, int layoutResourceId, IList<T> data, Func<ListViewAdapter<T>, int, View?, ViewGroup?, View> viewGenerator) : base(context, layoutResourceId, data)
		{
			this.context = context;
			this.viewGenerator = viewGenerator;
		}
		public ListViewAdapter(Context context, int layoutResourceId, T[] data, Func<ListViewAdapter<T>, int, View?, ViewGroup?, View> viewGenerator) : base(context, layoutResourceId, data)
		{
			this.context = context;
			this.viewGenerator = viewGenerator;
		}

		public override View GetView(int position, View? convertView, ViewGroup? parent) => viewGenerator(this, position, convertView, parent);
	}
}
