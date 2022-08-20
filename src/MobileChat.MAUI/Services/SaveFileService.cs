using MobileChat.MAUI.Interfaces;
using System.Text;
using System.Text.Json;

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

        public void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
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

        public void WriteToJsonFile<T>(string fileName, string path, T objectToWrite, bool append = false, bool encrypt = false, int key = 757) where T : new()
        {
            TextWriter writer = null;
            try
            {
                string contentsToWriteToFile = JsonSerializer.Serialize(objectToWrite);
                writer = new StreamWriter(Path.Combine(path, fileName), append);

                if (encrypt)
                {
                    writer.Write(EncryptDecrypt(contentsToWriteToFile, key));
                }
                else
                {
                    writer.Write(contentsToWriteToFile);
                }
            }
            catch { }
            finally
            {
                writer?.Close();
            }
        }
        public T ReadFromJsonFile<T>(string fileName, string path, bool isEncrypted = false, int key = 757) where T : new()
        {
            TextReader reader = null;
            try
            {
                reader = new StreamReader(Path.Combine(path, fileName));
                string fileContents = reader.ReadToEnd();

                if (isEncrypted)
                {
                    return JsonSerializer.Deserialize<T>(EncryptDecrypt(fileContents, key));
                }
                else
                {
                    return JsonSerializer.Deserialize<T>(fileContents);
                }
            }
            catch
            {
                return default;
            }
            finally
            {
                reader?.Close();
            }
        }
    }
}
