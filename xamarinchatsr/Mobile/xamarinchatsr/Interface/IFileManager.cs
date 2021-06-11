using System.Threading.Tasks;

namespace xamarinchatsr.Interface
{
    public interface IFileManager
    {
        Task<string> GetUWPSystemPath();
    }
}