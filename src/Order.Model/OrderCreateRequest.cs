using System;
using System.Collections.Generic;

namespace Order.Model
{
    public class OrderCreateRequest
    {
        public Guid ResellerId { get; set; }

        public Guid CustomerId { get; set; }

        public Guid StatusId { get; set; }

        public IEnumerable<OrderItemToCreate> Items { get; set; }
    }
}
