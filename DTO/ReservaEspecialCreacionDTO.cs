namespace DonFlorito.DTO
{
    public partial class ReservaEspecialCreacionDTO
    {
        public long? IdServicio { get; set; }
        public long? IdTipoServicio { get; set; }
        public DateTime FechaComienzo { get; set; }
        public DateTime FechaTermino { get; set; }
        public bool IsCanchas { get; set; }
        public bool IsCamping { get; set; }
        public bool IsEnabled { get; set; }


    }
}
