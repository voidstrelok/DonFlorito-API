using DonFlorito.Models;

namespace DonFlorito.DTO
{
    public class ReservaCreacionDTO
    {
        public long Id { get; set; }
        public long? IdPersona { get; set; }
        public DateTime FechaReserva { get; set; }
        public long IdOrdenCompra { get; set; }
        public PersonaCreacionDTO? PersonaCreacion { get; set; }
        public virtual ICollection<ReservaServicioCreacionDTO> ReservaServicio { get; set; }
    }
}
