using DonFlorito.Models;

namespace DonFlorito.DTO
{
    public class ReservaServicioDTO
    {
        public long Id { get; set; }
        public long IdReserva { get; set; }
        public long IdServicio { get; set; }
        public long IdPrecioServicio { get; set; }
        public long Cantidad { get; set; }
        public DateTime? HoraComienzo { get; set; }
        public virtual PrecioServicioDTO PrecioServicio { get; set; }
        public virtual ServicioDTO Servicio { get; set; }
    }
}
