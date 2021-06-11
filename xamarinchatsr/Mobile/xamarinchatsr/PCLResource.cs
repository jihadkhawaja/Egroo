using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Reflection;

namespace xamarinchatsr
{
    public static class PCLResource
    {
        public static T GetResourceFile<T>(string pclPath) where T : new()
        {
            try
            {
                Assembly assembly = typeof(PCLResource).GetTypeInfo().Assembly;
                System.IO.Stream stream = assembly.GetManifestResourceStream(pclPath);
                string file;
                using (System.IO.StreamReader reader = new System.IO.StreamReader(stream))
                {
                    file = reader.ReadToEnd();
                }

                T taskModels = JsonConvert.DeserializeObject<T>(file);

                return taskModels;
            }
            catch (Exception e)
            {
                Debug.WriteLine("ERROR Finding Load " + e.Message);
                return new T();
            }
        }
    }
}