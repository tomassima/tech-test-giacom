using Microsoft.EntityFrameworkCore;
using Order.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Order.Data
{
    public class OrderRepository : IOrderRepository
    {
        private readonly OrderContext _orderContext;

        public OrderRepository(OrderContext orderContext)
        {
            _orderContext = orderContext;
        }

        public async Task<IEnumerable<OrderSummary>> GetOrdersAsync(string status = null)
        {
            var orders = await _orderContext.Order
                .Include(x => x.Items)
                .Include(x => x.Status)
                .Where(x => status == null || x.Status.Name == status)
                .Select(x => new OrderSummary
                {
                    Id = new Guid(x.Id),
                    ResellerId = new Guid(x.ResellerId),
                    CustomerId = new Guid(x.CustomerId),
                    StatusId = new Guid(x.StatusId),
                    StatusName = x.Status.Name,
                    ItemCount = x.Items.Count,
                    TotalCost = x.Items.Sum(i => i.Quantity * i.Product.UnitCost).Value,
                    TotalPrice = x.Items.Sum(i => i.Quantity * i.Product.UnitPrice).Value,
                    CreatedDate = x.CreatedDate
                })
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();

            return orders;
        }

        public async Task<OrderDetail> GetOrderByIdAsync(Guid orderId)
        {
            var orderIdBytes = orderId.ToByteArray();

            var order = await _orderContext.Order
                .Where(x => _orderContext.Database.IsInMemory() ? x.Id.SequenceEqual(orderIdBytes) : x.Id == orderIdBytes)
                .Select(x => new OrderDetail
                {
                    Id = new Guid(x.Id),
                    ResellerId = new Guid(x.ResellerId),
                    CustomerId = new Guid(x.CustomerId),
                    StatusId = new Guid(x.StatusId),
                    StatusName = x.Status.Name,
                    CreatedDate = x.CreatedDate,
                    TotalCost = x.Items.Sum(i => i.Quantity * i.Product.UnitCost).Value,
                    TotalPrice = x.Items.Sum(i => i.Quantity * i.Product.UnitPrice).Value,
                    Items = x.Items.Select(i => new Model.OrderItem
                    {
                        Id = new Guid(i.Id),
                        OrderId = new Guid(i.OrderId),
                        ServiceId = new Guid(i.ServiceId),
                        ServiceName = i.Service.Name,
                        ProductId = new Guid(i.ProductId),
                        ProductName = i.Product.Name,
                        UnitCost = i.Product.UnitCost,
                        UnitPrice = i.Product.UnitPrice,
                        TotalCost = i.Product.UnitCost * i.Quantity.Value,
                        TotalPrice = i.Product.UnitPrice * i.Quantity.Value,
                        Quantity = i.Quantity.Value
                    })
                }).SingleOrDefaultAsync();

            return order;
        }

        public Task<OrderDetail> CreateOrderAsync(OrderCreateRequest request)
        {
            var orderId = Guid.NewGuid();
            var order = new Entities.Order
            {
                Id = orderId.ToByteArray(),
                ResellerId = request.ResellerId.ToByteArray(),
                CustomerId = request.CustomerId.ToByteArray(),
                StatusId = request.StatusId.ToByteArray(),
                CreatedDate = DateTime.UtcNow,
                Items = [.. request.Items.Select(i => new Entities.OrderItem
                {
                    Id = Guid.NewGuid().ToByteArray(),
                    OrderId = orderId.ToByteArray(),
                    ProductId = i.ProductId.ToByteArray(),
                    ServiceId = i.ServiceId.ToByteArray(),
                    Quantity = i.Quantity
                })]
            };

            _orderContext.Order.Add(order);
            _orderContext.SaveChanges();

            return GetOrderByIdAsync(orderId);
        }

        public Task<bool> UpdateOrderStatusAsync(Guid orderId, Guid statusId)
        {
            var orderIdBytes = orderId.ToByteArray();
            var statusIdBytes = statusId.ToByteArray();

            var order = _orderContext.Order
                .Where(x => _orderContext.Database.IsInMemory() ? x.Id.SequenceEqual(orderIdBytes) : x.Id == orderIdBytes)
                .SingleOrDefault();

            if (order == null)
            {
                return Task.FromResult(false);
            }

            order.StatusId = statusIdBytes;
            _orderContext.SaveChanges();

            return Task.FromResult(true);
        }
    }
}
