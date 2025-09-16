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
            // Validate that referenced foreign key entities exist before attempting to save
            var statusIdBytes = request.StatusId.ToByteArray();

            var statusExists = _orderContext.OrderStatus
                .Any(s => _orderContext.IsInMemoryDatabase() ? s.Id.SequenceEqual(statusIdBytes) : s.Id == statusIdBytes);

            if (!statusExists)
            {
                throw new CreateOrUpdateOrderException($"Status with id {request.StatusId} does not exist", nameof(request.StatusId));
            }

            // Collect product and service ids from request
            var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
            var serviceIds = request.Items.Select(i => i.ServiceId).Distinct().ToList();

            var missingProducts = productIds
                .Where(pid => _orderContext.OrderProduct.All(p => _orderContext.IsInMemoryDatabase() ? !p.Id.SequenceEqual(pid.ToByteArray()) : p.Id != pid.ToByteArray()))
                .ToList();

            if (missingProducts.Count != 0)
            {
                throw new CreateOrUpdateOrderException($"Product(s) not found: {string.Join(',', missingProducts)}", nameof(request.Items));
            }

            var missingServices = serviceIds
                .Where(sid => _orderContext.OrderService.All(s => _orderContext.IsInMemoryDatabase() ? !s.Id.SequenceEqual(sid.ToByteArray()) : s.Id != sid.ToByteArray()))
                .ToList();

            if (missingServices.Count != 0)
            {
                throw new CreateOrUpdateOrderException($"Service(s) not found: {string.Join(',', missingServices)}", nameof(request.Items));
            }

            var orderId = Guid.NewGuid();
            var order = new Entities.Order
            {
                Id = orderId.ToByteArray(),
                ResellerId = request.ResellerId.ToByteArray(),
                CustomerId = request.CustomerId.ToByteArray(),
                StatusId = statusIdBytes,
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


            // Validate that referenced foreign key entities exist before attempting to save
                var statusExists = _orderContext.OrderStatus
                .Any(s => _orderContext.IsInMemoryDatabase() ? s.Id.SequenceEqual(statusIdBytes) : s.Id == statusIdBytes);

            if (!statusExists)
            {
                throw new CreateOrUpdateOrderException($"Status with id {statusId} does not exist", nameof(statusId));
            }


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

        public async Task<decimal> GetMonthlyProfitAsync(int year, int month, string status = "Completed")
        {
            var profit = await _orderContext.Order
                .Include(x => x.Items)
                .ThenInclude(x => x.Product)
                .Include(x => x.Status)
                .Where(x => x.Status.Name == status
                    && x.CreatedDate.Year == year
                    && x.CreatedDate.Month == month)
                .SelectMany(x => x.Items)
                .SumAsync(i => (i.Product.UnitPrice - i.Product.UnitCost) * i.Quantity.Value);

            return profit;
        }
    }
}
