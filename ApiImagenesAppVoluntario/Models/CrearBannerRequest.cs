using Microsoft.AspNetCore.Http;

namespace ApiImagenesAppVoluntario.Models
{
    /// <summary>Modelo para la creación de banners</summary>
    public class CrearBannerRequest
    {
        /// <summary>Archivo de imagen a cargar</summary>
        public IFormFile Archivo { get; set; }
        
        /// <summary>ID del tipo de imagen</summary>
        public int IdTipoImagen { get; set; } = 1;
        
        /// <summary>Texto alternativo para la imagen</summary>
        public string? AltText { get; set; }
        
        /// <summary>Versión de la imagen</summary>
        public int Version { get; set; } = 1;
        
        /// <summary>Nombre del banner</summary>
        public string NombreBanner { get; set; }
        
        /// <summary>Orden de despliegue del banner</summary>
        public int Orden { get; set; }
    }
}
