using MobileChat.MAUI.Interfaces;
using System.Text;
using System.Text.Json;
using System.Xml.Serialization;

namespace MobileChat.MAUI.Services
{
    public class SaveFileService : ISaveFile
    {
        public void DeleteFile(string fileName, string path)
        {
            if (File.Exists(Path.Combine(path, fileName)))
            {
                File.Delete(Path.Combine(path, fileName));
            }
        }

        public void DeleteDirectory(string dirName, string path)
        {
            if (Directory.Exists(Path.Combine(path, dirName)))
            {
                Directory.Delete(Path.Combine(path, dirName), true);
            }
        }

        public bool CheckFileExist(string fileName, string path)
        {
            if (File.Exists(Path.Combine(path, fileName)))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public void CreateDirectory(params string[] dirPath)
        {
            foreach (string s in dirPath)
            {
                if (!Directory.Exists(s))
                {
                    Directory.CreateDirectory(s);
                }
            }
        }

        public string RemoveSpecialCharacters(string str)
        {
            StringBuilder sb = new();
            foreach (char c in str)
            {
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '.' || c == '_')
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        public string EncryptDecrypt(string textToEncrypt, int key)
        {
            StringBuilder inSb = new(textToEncrypt);
            StringBuilder outSb = new(textToEncrypt.Length);
            char c;
            for (int i = 0; i < textToEncrypt.Length; i++)
            {
                c = inSb[i];
                c = (char)(c ^ key);
                outSb.Append(c);
            }
            return outSb.ToString();
        }

        public int GetLengthenedIntFromString(string text, int length = 3)
        {
            string a = text;
            string b = string.Empty;

            for (int i = 0; i < a.Length; i++)
            {
                if (Char.IsDigit(a[i]))
                {
                    b += a[i];
                }
            }

            b = b[..3];

            if (b.Length > 0)
            {
                return int.Parse(b);
            }
            else
            {
                return 756;
            }
        }

        public void WriteToXmlFile<T>(string fileName, string path, T objectToWrite, bool append = false) where T : new()
        {
            TextWriter writer = null;
            try
            {
                XmlSerializer serializer = new(typeof(T));
                writer = new StreamWriter(Path.Combine(path, fileName), append);
                serializer.Serialize(writer, objectToWrite);
            }
            catch { }
            finally
            {
                if (writer != null)
                {
                    writer.Close();
                }
            }
        }

        public T ReadFromXmlFile<T>(string fileName, string path) where T : new()
        {
            TextReader reader = null;
            try
            {
                XmlSerializer serializer = new(typeof(T));
                reader = new StreamReader(Path.Combine(path, fileName));
                return (T)serializer.Deserialize(reader);
            }
            catch
            {
                return default;
            }
            finally
            {
                if (reader != null)
                {
                    reader.Close();
                }
            }
        }

        public void WriteToJsonFile<T>(string fileName, string path, T objectToWrite, bool append = false) where T : new()
        {
            TextWriter writer = null;
            try
            {
                string contentsToWriteToFile = JsonSerializer.Serialize(objectToWrite);
                writer = new StreamWriter(Path.Combine(path, fileName), append);

                writer.Write(contentsToWriteToFile);
            }
            catch { }
            finally
            {
                if (writer != null)
                {
                    writer.Close();
                }
            }
        }
        public T ReadFromJsonFile<T>(string fileName, string path) where T : new()
        {
            TextReader reader = null;
            try
            {
                reader = new StreamReader(Path.Combine(path, fileName));
                string fileContents = reader.ReadToEnd();

                return JsonSerializer.Deserialize<T>(fileContents);
            }
            catch
            {
                return default;
            }
            finally
            {
                if (reader != null)
                {
                    reader.Close();
                }
            }
        }
    }
}
