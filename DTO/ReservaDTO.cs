using DonFlorito.Models;

namespace DonFlorito.DTO
{
    public class ReservaDTO
    {
        public long Id { get; set; }
        public long IdEstadoReserva { get; set; }
        public long IdPersona { get; set; }
        public DateTime FechaReserva { get; set; }
        public DateTime FechaIngreso { get; set; }
        public DateTime? FechaConfirmacion { get; set; }
        public DateTime? FechaCancelacion { get; set; }
        public string Comentario { get; set; }
        public bool IsEnabled { get; set; }

        public virtual EstadoReservaDTO EstadoReserva { get; set; }
        public virtual PersonaDTO Persona { get; set; }
        public virtual ICollection<OrdenCompraDTO> OrdenCompra { get; set; }
        public virtual ICollection<ReservaServicioDTO> ReservaServicio { get; set; }

        public long IdOrdenCompra { get; set; }
    }
}
