namespace DonFlorito.DTO
{
    /// <summary>Precio vigente de un tipo de servicio, con la temporada en que rige (mes*100+día; null = todo el año).</summary>
    public class PrecioTemporadaDTO
    {
        public long Id { get; set; }
        public long IdTipoServicio { get; set; }
        public string NombreTipoServicio { get; set; } = "";
        public long Precio { get; set; }
        public int? TemporadaDesde { get; set; }
        public int? TemporadaHasta { get; set; }
    }

    public class CambioPrecioDTO
    {
        public long Id { get; set; }
        public long Precio { get; set; }
    }
}
