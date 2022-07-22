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
        int GetLengthenedIntFromString(string text, int length = 3);

        void WriteToXmlFile<T>(string fileName, string path, T objectToWrite, bool append = false) where T : new();
        T ReadFromXmlFile<T>(string fileName, string path) where T : new();

        void WriteToJsonFile<T>(string fileName, string path, T objectToWrite, bool append = false) where T : new();
        T ReadFromJsonFile<T>(string fileName, string path) where T : new();
    }
}
