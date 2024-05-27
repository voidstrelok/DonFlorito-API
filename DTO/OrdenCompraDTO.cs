using DonFlorito.Models;

namespace DonFlorito.DTO
{
    public class OrdenCompraDTO
    {
        public long Id { get; set; }
        public string Token { get; set; }
        public string Url { get; set; }
        public DateTime Fecha { get; set; }
        public long IdReserva { get; set; }
        public bool IsUsed { get; set; }

        public virtual ICollection<VoucherDTO> Voucher { get; set; }

    }
}
