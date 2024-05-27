using DonFlorito.Models;

namespace DonFlorito.DTO
{
    public class ConfigDTO
    {
        public int HApertura { get; set; }
        public int MApertura { get; set; }
        public int HCierre { get; set; }
        public int MCierre { get; set; }
        public bool PiscinasEnabled {  get; set; }
        public bool ReservasEnabled {  get; set; }
        public List<ServicioDTO> Servicios { get; set; }

    }
}
