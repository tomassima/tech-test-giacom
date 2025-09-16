using Order.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Order.Data
{
    public interface IOrderRepository
    {
        Task<IEnumerable<OrderSummary>> GetOrdersAsync(string status = null);

        Task<OrderDetail> GetOrderByIdAsync(Guid orderId);

        Task<OrderDetail> CreateOrderAsync(OrderCreateRequest request);

        Task<bool> UpdateOrderStatusAsync(Guid orderId, Guid statusId);

        Task<decimal> GetMonthlyProfitAsync(int year, int month, string status = "Completed");
    }
}
