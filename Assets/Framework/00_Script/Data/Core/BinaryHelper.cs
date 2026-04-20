using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace O2un.Data.Binary
{
    public static class BinaryHelper
    {
        private static readonly string PASSWORD = "o2!2510UnEnt@sib";
        private static readonly byte[] KEY = Encoding.UTF8.GetBytes(PASSWORD.Substring(0, 16));
        private static readonly byte[] IV = Encoding.UTF8.GetBytes(PASSWORD.Substring(0, 16));

        public static BinaryWriter SaveToBinary(string path, bool isEncrypt = true)
        {
            var directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            FileStream fs = new(path, FileMode.Create, FileAccess.Write, FileShare.None);

            if (isEncrypt)
            {
                using Aes aes = Aes.Create();
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.KeySize = 128;
                aes.Key = KEY;
                aes.IV = IV;

                var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                CryptoStream cs = new(fs, encryptor, CryptoStreamMode.Write);
                return new BinaryWriter(cs, Encoding.UTF8, false);
            }

            return new BinaryWriter(fs, Encoding.UTF8, false);
        }

        public static BinaryReader LoadFromBinary(string path, bool isEncrypt = true)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"{path} 파일이 존재하지 않습니다.");
                return null;
            }

            FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);

            if (isEncrypt)
            {
                using Aes aes = Aes.Create();
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.KeySize = 128;
                aes.Key = KEY;
                aes.IV = IV;

                var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                CryptoStream cs = new(fs, decryptor, CryptoStreamMode.Read);
                return new BinaryReader(cs, Encoding.UTF8, false);
            }

            return new BinaryReader(fs, Encoding.UTF8, false);
        }

        public static BinaryReader LoadFromMemory(byte[] data, bool isEncrypt = true)
        {
            MemoryStream ms = new(data);

            if (isEncrypt)
            {
                using Aes aes = Aes.Create();
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.KeySize = 128;
                aes.Key = KEY;
                aes.IV = IV;

                var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                CryptoStream cs = new(ms, decryptor, CryptoStreamMode.Read);
                return new BinaryReader(cs, Encoding.UTF8, false);
            }

            return new BinaryReader(ms, Encoding.UTF8, false);
        }
    }
}