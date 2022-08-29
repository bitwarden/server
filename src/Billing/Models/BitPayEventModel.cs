namespace Bit.Billing.Models;

public class BitPayEventModel
{
    public EventModel Event { get; set; }
    public InvoiceDataModel Data { get; set; }

    public class EventModel
    {
        public int Code { get; set; }
        public string Name { get; set; }
    }

    public class InvoiceDataModel
    {
        public string Id { get; set; }
        public string Url { get; set; }
        public string Status { get; set; }
        public string Currency { get; set; }
        public decimal Price { get; set; }
        public string PosData { get; set; }
        public bool ExceptionStatus { get; set; }
        public long CurrentTime { get; set; }
        public long AmountPaid { get; set; }
        public string TransactionCurrency { get; set; }
    }
}
