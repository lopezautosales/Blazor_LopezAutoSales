namespace LopezAutoSales.Shared.Models
{
    public class DeletePayment
    {
        public int AccountId { get; set; }
        public int PaymentId { get; set; }
        public string Reason { get; set; }
    }
}
