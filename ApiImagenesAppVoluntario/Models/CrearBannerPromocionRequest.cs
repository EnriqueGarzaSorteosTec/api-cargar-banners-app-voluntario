using Microsoft.AspNetCore.Http;

namespace ApiImagenesAppVoluntario.Models
{
    public class CrearBannerPromocionRequest
    {
        public IFormFile Archivo { get; set; }
        public int IdTipoImagen { get; set; }
        public string? AltText { get; set; }
        public int Version { get; set; }
        public string NombreBanner { get; set; }
        public int Orden { get; set; }
        public int IdPromocion { get; set; }
        public string FechaExpiracion { get; set; }
    }
}
