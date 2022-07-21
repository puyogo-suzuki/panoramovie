namespace PanoraMovie.CSharp
{
    public class DetailInfo : Java.Lang.Object
    {
        public string Category { get; set; }
        public string Value { get; set; }

        public DetailInfo(string category, string value)
        {
            Category = category;
            Value = value;
        }
    }
}
