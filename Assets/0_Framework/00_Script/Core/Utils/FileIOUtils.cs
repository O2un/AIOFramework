using System.IO;

namespace O2un.Utils
{
    public static class FileIOUtils
    {
        public static void StreamAppendFile(string path, string text)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(text))
            {
                return;
            }

            using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(stream);
            writer.Write(text);
        }
    }
}
