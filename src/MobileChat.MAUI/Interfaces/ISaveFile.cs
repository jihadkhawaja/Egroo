namespace MobileChat.MAUI.Interfaces
{
    internal interface ISaveFile
    {
        void DeleteFile(string fileName, string path);
        void DeleteDirectory(string dirName, string path);
        bool CheckFileExist(string fileName, string path);
        void CreateDirectory(params string[] dirPath);

        string RemoveSpecialCharacters(string str);

        string EncryptDecrypt(string textToEncrypt, int key);

        void WriteToJsonFile<T>(string fileName, string path, T objectToWrite, bool append = false, bool encrypt = false, int key = 757) where T : new();
        T ReadFromJsonFile<T>(string fileName, string path, bool isEncrypted = false, int key = 757) where T : new();
    }
}
