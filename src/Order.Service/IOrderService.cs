using Order.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Order.Service
{
    public interface IOrderService
    {
        Task<IEnumerable<OrderSummary>> GetOrdersAsync(string status = null);

        Task<OrderDetail> GetOrderByIdAsync(Guid orderId);
    }
}
