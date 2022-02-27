using System.Collections.Generic;
using Xamarin.Forms;

namespace MobileChat.Helpers
{
    public class ResourceHelper
    {
        public static object GetResourceValue(string keyName)
        {
            ICollection<ResourceDictionary> mergedDictionaries = Application.Current.Resources.MergedDictionaries;

            foreach (ResourceDictionary ed in mergedDictionaries)
            {
                if (ed.TryGetValue(keyName, out var retVal))
                {
                    return retVal;
                }
            }

            return null;
        }
    }
}