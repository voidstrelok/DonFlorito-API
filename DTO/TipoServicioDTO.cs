using DonFlorito.Models;

namespace DonFlorito.DTO
{
    public class TipoServicioDTO
    {
        public long Id { get; set; }
        public string? Nombre { get; set; }
        public virtual ICollection<ServicioDTO> Servicio { get; set; }
        public virtual ICollection<PrecioServicioDTO> PrecioServicio { get; set; }



    }
}
