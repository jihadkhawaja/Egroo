using System.Collections.Generic;
using Xamarin.Forms;

namespace xamarinchatsr.Helpers
{
    public class ResourceHelper
    {
        public static object GetResourceValue(string keyName)
        {
            // Search all dictionaries
            //if (Application.Current.Resources.TryGetValue(keyName, out var retVal)) { }
            //return retVal;

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