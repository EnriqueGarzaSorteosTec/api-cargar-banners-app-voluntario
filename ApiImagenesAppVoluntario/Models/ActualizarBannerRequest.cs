using System.ComponentModel.DataAnnotations;

namespace ApiImagenesAppVoluntario.Models
{
    public class ActualizarBannerRequest
    {
        [Required]
        public int id_banner { get; set; }

        [Required]
        public string guid_imagen { get; set; }

        [Required]
        public int orden { get; set; }

        [Required]
        public int activo { get; set; }
    }
}
