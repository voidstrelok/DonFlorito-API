
using DonFlorito.Models;

namespace DonFlorito.DTO
{
    public class PrecioServicioDTO
    {
        public long Id { get; set; }
        public long IdServicio { get; set; }
        public long Precio { get; set; }
        public long? Minutos { get; set; }
        public int? TemporadaDesde { get; set; }
        public int? TemporadaHasta { get; set; }
        public bool IsEnabled { get; set; }
    }
}
