using System;

namespace Order.Model
{
    public class OrderItemToCreate
    {
        public Guid ServiceId { get; set; }

        public Guid ProductId { get; set; }

        public int Quantity { get; set; }
    }
}
