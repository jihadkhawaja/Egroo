namespace xamarinchatsr.Models
{
    public class WalkthroughItem
    {
        public string ImageSource { get; set; }
        public string Context { get; set; }

        public WalkthroughItem(string imagesource, string context)
        {
            ImageSource = imagesource;
            Context = context;
        }
    }
}