using Microsoft.Extensions.Logging;

namespace ApiImagenesAppVoluntario.Services
{
    public class ImagenService
    {
        private readonly ILogger<ImagenService> _logger;

        public ImagenService(ILogger<ImagenService> logger)
        {
            _logger = logger;
        }

        public (int ancho, int alto) ObtenerDimensionesImagen(string rutaArchivo)
        {
            try
            {
                using var fileStream = new FileStream(rutaArchivo, FileMode.Open, FileAccess.Read);
                using var binaryReader = new BinaryReader(fileStream);

                var extension = Path.GetExtension(rutaArchivo).ToLowerInvariant();

                return extension switch
                {
                    ".png" => LeerDimensionesPNG(binaryReader),
                    ".jpg" or ".jpeg" => LeerDimensionesJPEG(binaryReader),
                    ".gif" => LeerDimensionesGIF(binaryReader),
                    _ => (0, 0)
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"No se pudieron obtener las dimensiones de la imagen: {ex.Message}");
                return (0, 0);
            }
        }

        private (int ancho, int alto) LeerDimensionesPNG(BinaryReader reader)
        {
            reader.BaseStream.Seek(16, SeekOrigin.Begin);
            var widthBytes = reader.ReadBytes(4);
            var heightBytes = reader.ReadBytes(4);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(widthBytes);
                Array.Reverse(heightBytes);
            }

            return (BitConverter.ToInt32(widthBytes, 0), BitConverter.ToInt32(heightBytes, 0));
        }

        private (int ancho, int alto) LeerDimensionesJPEG(BinaryReader reader)
        {
            reader.BaseStream.Seek(0, SeekOrigin.Begin);

            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                byte marker1 = reader.ReadByte();
                byte marker2 = reader.ReadByte();

                if (marker1 == 0xFF && marker2 >= 0xC0 && marker2 <= 0xC3)
                {
                    reader.ReadBytes(3);
                    var heightBytes = reader.ReadBytes(2);
                    var widthBytes = reader.ReadBytes(2);

                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(heightBytes);
                        Array.Reverse(widthBytes);
                    }

                    return (BitConverter.ToInt16(widthBytes, 0), BitConverter.ToInt16(heightBytes, 0));
                }
                else if (marker1 == 0xFF && marker2 != 0xD8 && marker2 != 0xD9)
                {
                    var lengthBytes = reader.ReadBytes(2);
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(lengthBytes);

                    var length = BitConverter.ToInt16(lengthBytes, 0);
                    reader.BaseStream.Seek(length - 2, SeekOrigin.Current);
                }
            }

            return (0, 0);
        }

        private (int ancho, int alto) LeerDimensionesGIF(BinaryReader reader)
        {
            reader.BaseStream.Seek(6, SeekOrigin.Begin);
            var widthBytes = reader.ReadBytes(2);
            var heightBytes = reader.ReadBytes(2);

            return (BitConverter.ToInt16(widthBytes, 0), BitConverter.ToInt16(heightBytes, 0));
        }
    }
}
