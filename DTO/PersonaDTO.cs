using DonFlorito.Models;

namespace DonFlorito.DTO
{
    public class PersonaDTO
    {
        public long Id { get; set; }
        public string Rut { get; set; }
        public string Nombre { get; set; }
        public string SegundoNombre { get; set; }
        public string ApellidoPaterno { get; set; }
        public string ApellidoMaterno { get; set; }
        public string Email { get; set; }
        public long Telefono { get; set; }
        public bool IsEnabled { get; set; }

    }
}
