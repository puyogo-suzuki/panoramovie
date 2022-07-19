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
    public class GallaryAdapter : ArrayAdapter<Java.Lang.String>
    {
		private Context context { get; init; }
		private int layoutResourceId;
        private Func<View, ViewHolder?> viewHolderGenerator;

        public GallaryAdapter(Context context, int layoutResourceId, IList<Java.Lang.String> data, Func<View, ViewHolder?> viewHolderGenerator): base(context, layoutResourceId, data) 
		{ 
			this.layoutResourceId = layoutResourceId;
			this.context = context;
			this.viewHolderGenerator = viewHolderGenerator;
		}
		public GallaryAdapter(Context context, int layoutResourceId, Java.Lang.String[] data, Func<View, ViewHolder?> viewHolderGenerator) : base(context, layoutResourceId, data)
		{
			this.layoutResourceId = layoutResourceId;
			this.context = context;
			this.viewHolderGenerator = viewHolderGenerator;
		}

		public override View GetView(int position, View? convertView, ViewGroup? parent)
		{
			ViewHolder holder = null!;
			if (convertView == null)
			{
				convertView = ((Activity)context).LayoutInflater.Inflate(layoutResourceId, parent, false);
				if (convertView == null) return null!;  // TODO?
				ViewHolder? holder2 = viewHolderGenerator(convertView);
				if (holder2 == null) return null!;      // TODO!
				holder = holder2;
				convertView.Tag = holder;
			}
			else
            {
				ViewHolder? holder2 = (ViewHolder?)convertView.Tag;
				if (holder2 == null)
				{
					holder2 = viewHolderGenerator(convertView);
					if (holder2 == null) return null!;  // TODO?
					convertView.Tag = holder;
				}
				else
					holder = holder2;
			}

			Java.Lang.String? _item = this.GetItem(position);
			if (_item == null) return null!;
			string item = _item.ToString();
			holder.ImageTitle.Text = System.IO.Path.GetFileName(item);
			try
			{
				holder.Image.ContentDescription = System.IO.Path.GetFileName(item);
				if (OperatingSystem.IsAndroidVersionAtLeast(29))
					holder.Image.SetImageBitmap(ThumbnailUtils.CreateVideoThumbnail(new Java.IO.File(item), new Android.Util.Size(128, 128), null));
				else
#pragma warning disable CS0618 // 型またはメンバーが旧型式です
					holder.Image.SetImageBitmap(ThumbnailUtils.CreateVideoThumbnail(item, Android.Provider.ThumbnailKind.MiniKind));
#pragma warning restore CS0618 // 型またはメンバーが旧型式です
			}
            catch
            {
				return null!;
            }
            return convertView;
		}

		public class ViewHolder : Java.Lang.Object
		{
			public TextView ImageTitle { get; set; }
			public ImageView Image { get; set; }


			public ViewHolder(TextView title, ImageView img)
            {
				ImageTitle = title;
				Image = img;
            }
		}
	}
}
