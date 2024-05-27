using DonFlorito.Models;

namespace DonFlorito.DTO
{
    public class ServicioDTO
    {
        public long Id { get; set; }
        public string Nombre { get; set; } = null!;
        public long IdTipoServicio { get; set; }
        public bool IsEnabled { get; set; }

        #region Extra
        public long Precio { get; set; }
        public bool CambiaPrecio { get; set; }

        #endregion
    }
}
