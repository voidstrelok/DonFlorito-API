using DonFlorito.Models;

namespace DonFlorito.DTO
{
    public class VoucherCreacionDTO
    {
        public string Vci { get; set; }
        public long? Amount { get; set; }
        public string Status { get; set; }
        public string BuyOrder { get; set; }
        public string SessionId { get; set; }
        public string CardNumber { get; set; }
        public string AccountingDate { get; set; }
        public string TransactionDate { get; set; }
        public string AuthorizationCode { get; set; }
        public string PaymentTypeCode { get; set; }
        public long? ResponseCode { get; set; }
        public long? InstallmentsAmount { get; set; }
        public long? InstallmentsNumber { get; set; }
        public long? Balance { get; set; }
        public long IdOrdenCompra { get; set; }
        public DateTime Fecha { get; set; }

    }
}
