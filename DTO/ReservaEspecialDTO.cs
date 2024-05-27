using DonFlorito.Models;

namespace DonFlorito.DTO
{
    public partial class ReservaEspecialDTO
    {
        public long Id { get; set; }
        public long? IdServicio { get; set; }
        public long? IdTipoServicio { get; set; }
        public DateTime FechaComienzo { get; set; }
        public DateTime FechaTermino { get; set; }
        public bool IsRecinto { get; set; }
        public bool IsEnabled { get; set; }

        public virtual ServicioDTO Servicio { get; set; }
        public virtual TipoServicioDTO TipoServicio{ get; set; }
    }
}
