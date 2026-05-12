using System.Numerics;
using System.Security.Cryptography;

namespace ApiImagenesAppVoluntario
{
    public class Common
    {
    
        private static readonly byte[] llave = new byte[32]
        {
            70, 118, 84, 90, 101, 110, 80, 73, 106, 72,
            119, 84, 117, 100, 98, 113, 49, 86, 55, 84,
            109, 104, 80, 73, 103, 57, 75, 51, 101, 49,
            100, 90
        };


        public static string EncriptarCadenaConexion(string cadenaConexion)
        {
            byte[] iV = new byte[16];
            byte[] inArray;
            using (Aes aes = Aes.Create())
            {
                aes.Key = llave;
                aes.IV = iV;
                ICryptoTransform transform = aes.CreateEncryptor(aes.Key, aes.IV);
                using MemoryStream memoryStream = new MemoryStream();
                using CryptoStream stream = new CryptoStream(memoryStream, transform, CryptoStreamMode.Write);
                using (StreamWriter streamWriter = new StreamWriter(stream))
                {
                    streamWriter.Write(cadenaConexion);
                }

                inArray = memoryStream.ToArray();
            }

            return Convert.ToBase64String(inArray);
        }

        private static string DesencriptarCadenaConexion(string cadenaConexionEncriptada)
        {
            byte[] iV = new byte[16];
            byte[] buffer = Convert.FromBase64String(cadenaConexionEncriptada);
            using Aes aes = Aes.Create();
            aes.Key = llave;
            aes.IV = iV;
            ICryptoTransform transform = aes.CreateDecryptor(aes.Key, aes.IV);
            using MemoryStream stream = new MemoryStream(buffer);
            using CryptoStream stream2 = new CryptoStream(stream, transform, CryptoStreamMode.Read);
            using StreamReader streamReader = new StreamReader(stream2);
            return streamReader.ReadToEnd();
        }

    }
}
