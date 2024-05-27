using DonFlorito.Models;

namespace DonFlorito.DTO
{
    public class ReservaServicioCreacionDTO
    {       
        public long IdServicio { get; set; }
        public long IdPrecioServicio { get; set; }
        public long Cantidad { get; set; }
        public DateTime? HoraComienzo { get; set; }

    }
}
