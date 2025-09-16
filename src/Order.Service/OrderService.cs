using Order.Data;
using Order.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Order.Service
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _orderRepository;

        public OrderService(IOrderRepository orderRepository)
        {
            _orderRepository = orderRepository;
        }

        public async Task<IEnumerable<OrderSummary>> GetOrdersAsync(string status = null)
        {
            var orders = await _orderRepository.GetOrdersAsync(status);
            return orders;
        }

        public async Task<OrderDetail> GetOrderByIdAsync(Guid orderId)
        {
            var order = await _orderRepository.GetOrderByIdAsync(orderId);
            return order;
        }

        public Task<OrderDetail> CreateOrderAsync(OrderCreateRequest request)
        {
            var order = _orderRepository.CreateOrderAsync(request);
            return order;
        }

        public Task<bool> UpdateOrderStatusAsync(Guid orderId, Guid statusId)
        {
            var result = _orderRepository.UpdateOrderStatusAsync(orderId, statusId);
            return result;
        }
    }
}
