using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Xml.Serialization;

namespace MobileChat.Cache
{
    public static class SavingManager
    {
        public static string AppPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal) + @"/appdata/";
        public static bool IsEncryptedJSON = true;

        public static class FileManager
        {
            public static void DeleteFile(string fileName)
            {
                if (File.Exists(AppPath + fileName))
                {
                    File.Delete(AppPath + fileName);
                }
            }

            public static void DeleteDirectory(string dirName)
            {
                if (Directory.Exists(AppPath + dirName))
                {
                    Directory.Delete(AppPath + dirName, true);
                }
            }

            public static bool CheckFileExist(string fileName)
            {
                if (File.Exists(AppPath + fileName))
                    return true;
                else
                    return false;
            }

            public static void CreateDirectory(params string[] dirPath)
            {
                foreach (string s in dirPath)
                {
                    if (!Directory.Exists(AppPath + s))
                        Directory.CreateDirectory(AppPath + s);
                }
            }

            public static string RemoveSpecialCharacters(string str)
            {
                StringBuilder sb = new StringBuilder();
                foreach (char c in str)
                {
                    if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '.' || c == '_')
                    {
                        sb.Append(c);
                    }
                }
                return sb.ToString();
            }
        }

        public static class Cryptography
        {
            public const int key = 678;

            public static string EncryptDecrypt(string textToEncrypt)
            {
                StringBuilder inSb = new StringBuilder(textToEncrypt);
                StringBuilder outSb = new StringBuilder(textToEncrypt.Length);
                char c;
                for (int i = 0; i < textToEncrypt.Length; i++)
                {
                    c = inSb[i];
                    c = (char)(c ^ key);
                    outSb.Append(c);
                }
                return outSb.ToString();
            }

            private static int GetLengthenedIntFromString(string text, int length = 3)
            {
                string a = text;
                string b = string.Empty;

                for (int i = 0; i < a.Length; i++)
                {
                    if (Char.IsDigit(a[i]))
                        b += a[i];
                }

                b = b.Substring(0, 3);

                if (b.Length > 0)
                    return int.Parse(b);
                else
                    return 756;
            }
        }

        /// <summary>
        /// Functions for performing common binary Serialization operations.
        /// <para>All properties and variables will be serialized.</para>
        /// <para>Object type (and all child types) must be decorated with the [Serializable] attribute.</para>
        /// <para>To prevent a variable from being serialized, decorate it with the [NonSerialized] attribute; cannot be applied to properties.</para>
        /// </summary>
        public static class BinarySerialization
        {
            /// <summary>
            /// Writes the given object instance to a binary file.
            /// <para>Object type (and all child types) must be decorated with the [Serializable] attribute.</para>
            /// <para>To prevent a variable from being serialized, decorate it with the [NonSerialized] attribute; cannot be applied to properties.</para>
            /// </summary>
            /// <typeparam name="T">The type of object being written to the XML file.</typeparam>
            /// <param name="fileName">The file path to write the object instance to.</param>
            /// <param name="objectToWrite">The object instance to write to the XML file.</param>
            /// <param name="append">If false the file will be overwritten if it already exists. If true the contents will be appended to the file.</param>
            public static void WriteToBinaryFile<T>(string fileName, T objectToWrite, bool append = false)
            {
                using (Stream stream = File.Open(AppPath + fileName, append ? FileMode.Append : FileMode.Create))
                {
                    var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    binaryFormatter.Serialize(stream, objectToWrite);
                }
            }

            /// <summary>
            /// Reads an object instance from a binary file.
            /// </summary>
            /// <typeparam name="T">The type of object to read from the XML.</typeparam>
            /// <param name="fileName">The file path to read the object instance from.</param>
            /// <returns>Returns a new instance of the object read from the binary file.</returns>
            public static T ReadFromBinaryFile<T>(string fileName)
            {
                using (Stream stream = File.Open(AppPath + fileName, FileMode.Open))
                {
                    var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    return (T)binaryFormatter.Deserialize(stream);
                }
            }
        }

        /// <summary>
        /// Functions for performing common XML Serialization operations.
        /// <para>Only public properties and variables will be serialized.</para>
        /// <para>Use the [XmlIgnore] attribute to prevent a property/variable from being serialized.</para>
        /// <para>Object to be serialized must have a parameterless constructor.</para>
        /// </summary>
        public static class XmlSerialization
        {
            /// <summary>
            /// Writes the given object instance to an XML file.
            /// <para>Only Public properties and variables will be written to the file. These can be any type though, even other classes.</para>
            /// <para>If there are public properties/variables that you do not want written to the file, decorate them with the [XmlIgnore] attribute.</para>
            /// <para>Object type must have a parameterless constructor.</para>
            /// </summary>
            /// <typeparam name="T">The type of object being written to the file.</typeparam>
            /// <param name="fileName">The file path to write the object instance to.</param>
            /// <param name="objectToWrite">The object instance to write to the file.</param>
            /// <param name="append">If false the file will be overwritten if it already exists. If true the contents will be appended to the file.</param>
            public static void WriteToXmlFile<T>(string fileName, T objectToWrite, bool append = false) where T : new()
            {
                TextWriter writer = null;
                try
                {
                    var serializer = new XmlSerializer(typeof(T));
                    writer = new StreamWriter(AppPath + fileName, append);
                    serializer.Serialize(writer, objectToWrite);
                }
                finally
                {
                    if (writer != null)
                        writer.Close();
                }
            }

            /// <summary>
            /// Reads an object instance from an XML file.
            /// <para>Object type must have a parameterless constructor.</para>
            /// </summary>
            /// <typeparam name="T">The type of object to read from the file.</typeparam>
            /// <param name="fileName">The file path to read the object instance from.</param>
            /// <returns>Returns a new instance of the object read from the XML file.</returns>
            public static T ReadFromXmlFile<T>(string fileName) where T : new()
            {
                TextReader reader = null;
                try
                {
                    var serializer = new XmlSerializer(typeof(T));
                    reader = new StreamReader(AppPath + fileName);
                    return (T)serializer.Deserialize(reader);
                }
                finally
                {
                    if (reader != null)
                        reader.Close();
                }
            }
        }

        /// <summary>
        /// Functions for performing common Json Serialization operations.
        /// <para>Requires the Newtonsoft.Json assembly (Json.Net package in NuGet Gallery) to be referenced in your project.</para>
        /// <para>Only public properties and variables will be serialized.</para>
        /// <para>Use the [JsonIgnore] attribute to ignore specific public properties or variables.</para>
        /// <para>Object to be serialized must have a parameterless constructor.</para>
        /// </summary>
        public static class JsonSerialization
        {
            /// <summary>
            /// Writes the given object instance to a Json file.
            /// <para>Object type must have a parameterless constructor.</para>
            /// <para>Only Public properties and variables will be written to the file. These can be any type though, even other classes.</para>
            /// <para>If there are public properties/variables that you do not want written to the file, decorate them with the [JsonIgnore] attribute.</para>
            /// </summary>
            /// <typeparam name="T">The type of object being written to the file.</typeparam>
            /// <param name="fileName">The file path to write the object instance to.</param>
            /// <param name="objectToWrite">The object instance to write to the file.</param>
            /// <param name="append">If false the file will be overwritten if it already exists. If true the contents will be appended to the file.</param>
            public static void WriteToJsonFile<T>(string fileName, T objectToWrite, bool append = false) where T : new()
            {
                TextWriter writer = null;
                try
                {
                    var contentsToWriteToFile = JsonSerializer.Serialize(objectToWrite);
                    writer = new StreamWriter(AppPath + fileName, append);
                    if (IsEncryptedJSON)
                        writer.Write(Cryptography.EncryptDecrypt(contentsToWriteToFile));
                    else
                        writer.Write(contentsToWriteToFile);
                }
                finally
                {
                    if (writer != null)
                        writer.Close();
                }
            }

            /// <summary>
            /// Reads an object instance from an Json file.
            /// <para>Object type must have a parameterless constructor.</para>
            /// </summary>
            /// <typeparam name="T">The type of object to read from the file.</typeparam>
            /// <param name="fileName">The file path to read the object instance from.</param>
            /// <returns>Returns a new instance of the object read from the Json file.</returns>
            public static T ReadFromJsonFile<T>(string fileName) where T : new()
            {
                TextReader reader = null;
                try
                {
                    reader = new StreamReader(AppPath + fileName);
                    var fileContents = reader.ReadToEnd();
                    if (IsEncryptedJSON)
                        return JsonSerializer.Deserialize<T>(Cryptography.EncryptDecrypt(fileContents));
                    else
                        return JsonSerializer.Deserialize<T>(fileContents);
                }
                finally
                {
                    if (reader != null)
                        reader.Close();
                }
            }
        }
    }
}