using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NUnit.Framework;
using Order.Data;
using Order.Data.Entities;
using System;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace Order.Service.Tests
{
    public class OrderServiceTests
    {
        private IOrderService _orderService;
        private IOrderRepository _orderRepository;
        private OrderContext _orderContext;
        private DbConnection _connection;

        private readonly byte[] _orderStatusCreatedId = Guid.NewGuid().ToByteArray();
        private readonly byte[] _orderServiceEmailId = Guid.NewGuid().ToByteArray();
        private readonly byte[] _orderProductEmailId = Guid.NewGuid().ToByteArray();


        [SetUp]
        public async Task Setup()
        {
            var options = new DbContextOptionsBuilder<OrderContext>()
                .UseSqlite(CreateInMemoryDatabase())
                .EnableDetailedErrors(true)
                .EnableSensitiveDataLogging(true)
                .Options;

            _connection = RelationalOptionsExtension.Extract(options).Connection;

            _orderContext = new OrderContext(options);
            _orderContext.Database.EnsureDeleted();
            _orderContext.Database.EnsureCreated();

            _orderRepository = new OrderRepository(_orderContext);
            _orderService = new OrderService(_orderRepository);

            await AddReferenceDataAsync(_orderContext);
        }

        [TearDown]
        public void TearDown()
        {
            _connection.Dispose();
            _orderContext.Dispose();
        }


        private static DbConnection CreateInMemoryDatabase()
        {
            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();

            return connection;
        }

        [Test]
        public async Task GetOrdersAsync_ReturnsCorrectNumberOfOrders()
        {
            // Arrange
            var orderId1 = Guid.NewGuid();
            await AddOrder(orderId1, 1);

            var orderId2 = Guid.NewGuid();
            await AddOrder(orderId2, 2);

            var orderId3 = Guid.NewGuid();
            await AddOrder(orderId3, 3);

            // Act
            var orders = await _orderService.GetOrdersAsync();

            // Assert
            Assert.AreEqual(3, orders.Count());
        }

        [Test]
        public async Task GetOrdersAsync_ReturnsOrdersWithCorrectTotals()
        {
            // Arrange
            var orderId1 = Guid.NewGuid();
            await AddOrder(orderId1, 1);

            var orderId2 = Guid.NewGuid();
            await AddOrder(orderId2, 2);

            var orderId3 = Guid.NewGuid();
            await AddOrder(orderId3, 3);

            // Act
            var orders = await _orderService.GetOrdersAsync();

            // Assert
            var order1 = orders.SingleOrDefault(x => x.Id == orderId1);
            var order2 = orders.SingleOrDefault(x => x.Id == orderId2);
            var order3 = orders.SingleOrDefault(x => x.Id == orderId3);

            Assert.AreEqual(0.8m, order1.TotalCost);
            Assert.AreEqual(0.9m, order1.TotalPrice);

            Assert.AreEqual(1.6m, order2.TotalCost);
            Assert.AreEqual(1.8m, order2.TotalPrice);

            Assert.AreEqual(2.4m, order3.TotalCost);
            Assert.AreEqual(2.7m, order3.TotalPrice);
        }

        [Test]
        public async Task GetOrderByIdAsync_ReturnsCorrectOrder()
        {
            // Arrange
            var orderId1 = Guid.NewGuid();
            await AddOrder(orderId1, 1);

            // Act
            var order = await _orderService.GetOrderByIdAsync(orderId1);

            // Assert
            Assert.AreEqual(orderId1, order.Id);
        }

        [Test]
        public async Task GetOrderByIdAsync_ReturnsCorrectOrderItemCount()
        {
            // Arrange
            var orderId1 = Guid.NewGuid();
            await AddOrder(orderId1, 1);

            // Act
            var order = await _orderService.GetOrderByIdAsync(orderId1);

            // Assert
            Assert.AreEqual(1, order.Items.Count());
        }

        [Test]
        public async Task GetOrderByIdAsync_ReturnsOrderWithCorrectTotals()
        {
            // Arrange
            var orderId1 = Guid.NewGuid();
            await AddOrder(orderId1, 2);

            // Act
            var order = await _orderService.GetOrderByIdAsync(orderId1);

            // Assert
            Assert.AreEqual(1.6m, order.TotalCost);
            Assert.AreEqual(1.8m, order.TotalPrice);
        }

        [Test]
        public async Task GetOrdersAsync_FiltersByStatus_ReturnsCorrectOrders()
        {
            // Arrange
            var completedStatusId = Guid.NewGuid().ToByteArray();
            var failedStatusId = Guid.NewGuid().ToByteArray();

            // Add additional order statuses
            _orderContext.OrderStatus.Add(new OrderStatus
            {
                Id = completedStatusId,
                Name = "Completed",
            });
            _orderContext.OrderStatus.Add(new OrderStatus
            {
                Id = failedStatusId,
                Name = "Failed",
            });
            await _orderContext.SaveChangesAsync();

            // Add orders with different statuses
            var orderId1 = Guid.NewGuid();
            await AddOrderWithStatus(orderId1, 1, _orderStatusCreatedId); // Created

            var orderId2 = Guid.NewGuid();
            await AddOrderWithStatus(orderId2, 2, completedStatusId); // Completed

            var orderId3 = Guid.NewGuid();
            await AddOrderWithStatus(orderId3, 3, failedStatusId); // Failed

            var orderId4 = Guid.NewGuid();
            await AddOrderWithStatus(orderId4, 1, completedStatusId); // Another Completed

            // Act - Filter by "Completed" status
            var completedOrders = await _orderService.GetOrdersAsync("Completed");
            var failedOrders = await _orderService.GetOrdersAsync("Failed");
            var createdOrders = await _orderService.GetOrdersAsync("Created");

            // Assert
            Assert.AreEqual(2, completedOrders.Count(), "Should return 2 completed orders");
            Assert.AreEqual(1, failedOrders.Count(), "Should return 1 failed order");
            Assert.AreEqual(1, createdOrders.Count(), "Should return 1 created order");

            // Verify the correct orders are returned
            Assert.IsTrue(completedOrders.Any(x => x.Id == orderId2), "Should contain orderId2");
            Assert.IsTrue(completedOrders.Any(x => x.Id == orderId4), "Should contain orderId4");
            Assert.IsTrue(failedOrders.Any(x => x.Id == orderId3), "Should contain orderId3");
            Assert.IsTrue(createdOrders.Any(x => x.Id == orderId1), "Should contain orderId1");

            // Verify status names are correct
            Assert.IsTrue(completedOrders.All(x => x.StatusName == "Completed"), "All returned orders should have Completed status");
            Assert.IsTrue(failedOrders.All(x => x.StatusName == "Failed"), "All returned orders should have Failed status");
            Assert.IsTrue(createdOrders.All(x => x.StatusName == "Created"), "All returned orders should have Created status");
        }

        [Test]
        public async Task GetOrdersAsync_WithNullStatus_ReturnsAllOrders()
        {
            // Arrange
            var completedStatusId = Guid.NewGuid().ToByteArray();
            _orderContext.OrderStatus.Add(new OrderStatus
            {
                Id = completedStatusId,
                Name = "Completed",
            });
            await _orderContext.SaveChangesAsync();

            var orderId1 = Guid.NewGuid();
            await AddOrderWithStatus(orderId1, 1, _orderStatusCreatedId); // Created

            var orderId2 = Guid.NewGuid();
            await AddOrderWithStatus(orderId2, 2, completedStatusId); // Completed

            // Act - No status filter (null)
            var allOrders = await _orderService.GetOrdersAsync(null);

            // Assert
            Assert.AreEqual(2, allOrders.Count(), "Should return all orders when status is null");
            Assert.IsTrue(allOrders.Any(x => x.Id == orderId1), "Should contain created order");
            Assert.IsTrue(allOrders.Any(x => x.Id == orderId2), "Should contain completed order");
        }

        [Test]
        public async Task GetOrdersAsync_WithNonExistentStatus_ReturnsNoOrders()
        {
            // Arrange
            var orderId1 = Guid.NewGuid();
            await AddOrder(orderId1, 1);

            // Act - Filter by non-existent status
            var orders = await _orderService.GetOrdersAsync("NonExistentStatus");

            // Assert
            Assert.AreEqual(0, orders.Count(), "Should return no orders for non-existent status");
        }

        private async Task AddOrder(Guid orderId, int quantity)
        {
            var orderIdBytes = orderId.ToByteArray();
            _orderContext.Order.Add(new Data.Entities.Order
            {
                Id = orderIdBytes,
                ResellerId = Guid.NewGuid().ToByteArray(),
                CustomerId = Guid.NewGuid().ToByteArray(),
                CreatedDate = DateTime.Now,
                StatusId = _orderStatusCreatedId,
            });

            _orderContext.OrderItem.Add(new OrderItem
            {
                Id = Guid.NewGuid().ToByteArray(),
                OrderId = orderIdBytes,
                ServiceId = _orderServiceEmailId,
                ProductId = _orderProductEmailId,
                Quantity = quantity
            });

            await _orderContext.SaveChangesAsync();
        }

        private async Task AddOrderWithStatus(Guid orderId, int quantity, byte[] statusId)
        {
            var orderIdBytes = orderId.ToByteArray();
            _orderContext.Order.Add(new Data.Entities.Order
            {
                Id = orderIdBytes,
                ResellerId = Guid.NewGuid().ToByteArray(),
                CustomerId = Guid.NewGuid().ToByteArray(),
                CreatedDate = DateTime.Now,
                StatusId = statusId,
            });

            _orderContext.OrderItem.Add(new OrderItem
            {
                Id = Guid.NewGuid().ToByteArray(),
                OrderId = orderIdBytes,
                ServiceId = _orderServiceEmailId,
                ProductId = _orderProductEmailId,
                Quantity = quantity
            });

            await _orderContext.SaveChangesAsync();
        }

        private async Task AddReferenceDataAsync(OrderContext orderContext)
        {
            orderContext.OrderStatus.Add(new OrderStatus
            {
                Id = _orderStatusCreatedId,
                Name = "Created",
            });

            orderContext.OrderService.Add(new Data.Entities.OrderService
            {
                Id = _orderServiceEmailId,
                Name = "Email"
            });

            orderContext.OrderProduct.Add(new OrderProduct
            {
                Id = _orderProductEmailId,
                Name = "100GB Mailbox",
                UnitCost = 0.8m,
                UnitPrice = 0.9m,
                ServiceId = _orderServiceEmailId
            });

            await orderContext.SaveChangesAsync();
        }
    }
}
