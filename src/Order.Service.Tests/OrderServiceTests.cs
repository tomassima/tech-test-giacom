using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NUnit.Framework;
using Order.Data;
using Order.Data.Entities;
using Order.Model;
using System;
using System.Collections.Generic;
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

        [Test]
        public async Task CreateOrderAsync_CreatesOrderSuccessfully()
        {
            // Arrange
            var resellerId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            
            var createRequest = new OrderCreateRequest
            {
                ResellerId = resellerId,
                CustomerId = customerId,
                StatusId = new Guid(_orderStatusCreatedId),
                Items = new List<OrderItemToCreate>
                {
                    new OrderItemToCreate
                    {
                        ServiceId = new Guid(_orderServiceEmailId),
                        ProductId = new Guid(_orderProductEmailId),
                        Quantity = 2
                    }
                }
            };

            // Act
            var createdOrder = await _orderService.CreateOrderAsync(createRequest);

            // Assert
            Assert.IsNotNull(createdOrder, "Created order should not be null");
            Assert.AreNotEqual(Guid.Empty, createdOrder.Id, "Order should have a valid ID");
            Assert.AreEqual(resellerId, createdOrder.ResellerId, "ResellerId should match");
            Assert.AreEqual(customerId, createdOrder.CustomerId, "CustomerId should match");
            Assert.AreEqual(new Guid(_orderStatusCreatedId), createdOrder.StatusId, "StatusId should match");
            Assert.AreEqual("Created", createdOrder.StatusName, "Status name should be 'Created'");
            Assert.AreEqual(1, createdOrder.Items.Count(), "Should have 1 order item");
            
            var orderItem = createdOrder.Items.First();
            Assert.AreEqual(new Guid(_orderServiceEmailId), orderItem.ServiceId, "Service ID should match");
            Assert.AreEqual(new Guid(_orderProductEmailId), orderItem.ProductId, "Product ID should match");
            Assert.AreEqual(2, orderItem.Quantity, "Quantity should be 2");
            Assert.AreEqual("Email", orderItem.ServiceName, "Service name should be 'Email'");
            Assert.AreEqual("100GB Mailbox", orderItem.ProductName, "Product name should be '100GB Mailbox'");
        }

        [Test]
        public async Task CreateOrderAsync_CalculatesTotalsCorrectly()
        {
            // Arrange
            var createRequest = new OrderCreateRequest
            {
                ResellerId = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                StatusId = new Guid(_orderStatusCreatedId),
                Items = new List<OrderItemToCreate>
                {
                    new OrderItemToCreate
                    {
                        ServiceId = new Guid(_orderServiceEmailId),
                        ProductId = new Guid(_orderProductEmailId),
                        Quantity = 3  // 3 * 0.8 = 2.4 cost, 3 * 0.9 = 2.7 price
                    }
                }
            };

            // Act
            var createdOrder = await _orderService.CreateOrderAsync(createRequest);

            // Assert
            Assert.AreEqual(2.4m, createdOrder.TotalCost, "Total cost should be 2.4 (3 * 0.8)");
            Assert.AreEqual(2.7m, createdOrder.TotalPrice, "Total price should be 2.7 (3 * 0.9)");
            
            var orderItem = createdOrder.Items.First();
            Assert.AreEqual(2.4m, orderItem.TotalCost, "Item total cost should be 2.4");
            Assert.AreEqual(2.7m, orderItem.TotalPrice, "Item total price should be 2.7");
            Assert.AreEqual(0.8m, orderItem.UnitCost, "Unit cost should be 0.8");
            Assert.AreEqual(0.9m, orderItem.UnitPrice, "Unit price should be 0.9");
        }

        [Test]
        public async Task CreateOrderAsync_WithMultipleItems_CreatesAllItems()
        {
            // Add another product for testing multiple items
            var secondProductId = Guid.NewGuid().ToByteArray();
            _orderContext.OrderProduct.Add(new OrderProduct
            {
                Id = secondProductId,
                Name = "50GB Mailbox",
                UnitCost = 0.5m,
                UnitPrice = 0.6m,
                ServiceId = _orderServiceEmailId
            });
            await _orderContext.SaveChangesAsync();

            // Arrange
            var createRequest = new OrderCreateRequest
            {
                ResellerId = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                StatusId = new Guid(_orderStatusCreatedId),
                Items = new List<OrderItemToCreate>
                {
                    new OrderItemToCreate
                    {
                        ServiceId = new Guid(_orderServiceEmailId),
                        ProductId = new Guid(_orderProductEmailId),
                        Quantity = 1  // 1 * 0.8 = 0.8 cost, 1 * 0.9 = 0.9 price
                    },
                    new OrderItemToCreate
                    {
                        ServiceId = new Guid(_orderServiceEmailId),
                        ProductId = new Guid(secondProductId),
                        Quantity = 2  // 2 * 0.5 = 1.0 cost, 2 * 0.6 = 1.2 price
                    }
                }
            };

            // Act
            var createdOrder = await _orderService.CreateOrderAsync(createRequest);

            // Assert
            Assert.AreEqual(2, createdOrder.Items.Count(), "Should have 2 order items");
            Assert.AreEqual(1.8m, createdOrder.TotalCost, "Total cost should be 1.8 (0.8 + 1.0)");
            Assert.AreEqual(2.1m, createdOrder.TotalPrice, "Total price should be 2.1 (0.9 + 1.2)");

            var firstItem = createdOrder.Items.First(i => i.ProductName == "100GB Mailbox");
            var secondItem = createdOrder.Items.First(i => i.ProductName == "50GB Mailbox");

            Assert.AreEqual(1, firstItem.Quantity, "First item quantity should be 1");
            Assert.AreEqual(2, secondItem.Quantity, "Second item quantity should be 2");
        }

        [Test]
        public async Task CreateOrderAsync_WithMissingStatus_ThrowsCreateOrderException()
        {
            // Arrange - use a status id that does not exist
            var createRequest = new OrderCreateRequest
            {
                ResellerId = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                StatusId = Guid.NewGuid(), // missing
                Items = new List<OrderItemToCreate>
                {
                    new OrderItemToCreate
                    {
                        ServiceId = new Guid(_orderServiceEmailId),
                        ProductId = new Guid(_orderProductEmailId),
                        Quantity = 1
                    }
                }
            };

            // Act & Assert
            try
            {
                await _orderService.CreateOrderAsync(createRequest);
                Assert.Fail("Expected CreateOrderException when status is missing");
            }
            catch (CreateOrUpdateOrderException ex)
            {
                Assert.IsNotNull(ex, "Expected CreateOrderException when status is missing");
            }
        }

        [Test]
        public async Task CreateOrderAsync_WithMissingProduct_ThrowsCreateOrderException()
        {
            // Arrange - use a product id that does not exist
            var createRequest = new OrderCreateRequest
            {
                ResellerId = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                StatusId = new Guid(_orderStatusCreatedId),
                Items = new List<OrderItemToCreate>
                {
                    new OrderItemToCreate
                    {
                        ServiceId = new Guid(_orderServiceEmailId),
                        ProductId = Guid.NewGuid(), // missing
                        Quantity = 1
                    }
                }
            };

            // Act & Assert
            try
            {
                await _orderService.CreateOrderAsync(createRequest);
                Assert.Fail("Expected CreateOrderException when product is missing");
            }
            catch (CreateOrUpdateOrderException ex)
            {
                Assert.IsNotNull(ex, "Expected CreateOrderException when product is missing");
            }
        }

        [Test]
        public async Task CreateOrderAsync_WithMissingService_ThrowsCreateOrderException()
        {
            // Arrange - use a service id that does not exist
            // Add a product that references a non-existent service so product exists but service is missing
            var orphanProductId = Guid.NewGuid().ToByteArray();
            _orderContext.OrderProduct.Add(new OrderProduct
            {
                Id = orphanProductId,
                Name = "Orphan Product",
                UnitCost = 1.0m,
                UnitPrice = 1.5m,
                ServiceId = Guid.NewGuid().ToByteArray() // this service does not exist
            });
            await _orderContext.SaveChangesAsync();

            var createRequest = new OrderCreateRequest
            {
                ResellerId = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                StatusId = new Guid(_orderStatusCreatedId),
                Items = new List<OrderItemToCreate>
                {
                    new OrderItemToCreate
                    {
                        ServiceId = Guid.NewGuid(), // missing
                        ProductId = new Guid(orphanProductId),
                        Quantity = 1
                    }
                }
            };

            // Act & Assert
            try
            {
                await _orderService.CreateOrderAsync(createRequest);
                Assert.Fail("Expected CreateOrderException when service is missing");
            }
            catch (CreateOrUpdateOrderException ex)
            {
                Assert.IsNotNull(ex, "Expected CreateOrderException when service is missing");
            }
        }

        [Test]
        public async Task UpdateOrderStatusAsync_WithValidOrder_UpdatesStatusSuccessfully()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            await AddOrder(orderId, 1);

            var completedStatusId = Guid.NewGuid().ToByteArray();
            _orderContext.OrderStatus.Add(new OrderStatus
            {
                Id = completedStatusId,
                Name = "Completed",
            });
            await _orderContext.SaveChangesAsync();

            // Act
            var result = await _orderService.UpdateOrderStatusAsync(orderId, new Guid(completedStatusId));

            // Assert
            Assert.IsTrue(result, "Update should return true for successful update");

            // Verify the status was actually updated
            var updatedOrder = await _orderService.GetOrderByIdAsync(orderId);
            Assert.AreEqual(new Guid(completedStatusId), updatedOrder.StatusId, "Status ID should be updated");
            Assert.AreEqual("Completed", updatedOrder.StatusName, "Status name should be updated to 'Completed'");
        }

        [Test]
        public async Task UpdateOrderStatusAsync_WithNonExistentOrder_ReturnsFalse()
        {
            // Arrange
            var nonExistentOrderId = Guid.NewGuid();
            var statusId = new Guid(_orderStatusCreatedId);

            // Act
            var result = await _orderService.UpdateOrderStatusAsync(nonExistentOrderId, statusId);

            // Assert
            Assert.IsFalse(result, "Update should return false for non-existent order");
        }

        [Test]
        public async Task UpdateOrderStatusAsync_WithValidOrderAndStatus_PersistsChanges()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            await AddOrder(orderId, 2);

            var failedStatusId = Guid.NewGuid().ToByteArray();
            _orderContext.OrderStatus.Add(new OrderStatus
            {
                Id = failedStatusId,
                Name = "Failed",
            });
            await _orderContext.SaveChangesAsync();

            // Get original status to verify it changes
            var originalOrder = await _orderService.GetOrderByIdAsync(orderId);
            Assert.AreEqual("Created", originalOrder.StatusName, "Original status should be 'Created'");

            // Act
            var result = await _orderService.UpdateOrderStatusAsync(orderId, new Guid(failedStatusId));

            // Assert
            Assert.IsTrue(result, "Update should return true");

            // Create a new service instance to verify persistence
            var newRepository = new OrderRepository(_orderContext);
            var newService = new OrderService(newRepository);
            var persistedOrder = await newService.GetOrderByIdAsync(orderId);

            Assert.AreEqual(new Guid(failedStatusId), persistedOrder.StatusId, "Persisted status ID should match");
            Assert.AreEqual("Failed", persistedOrder.StatusName, "Persisted status name should be 'Failed'");
        }

        [Test]
        public async Task UpdateOrderStatusAsync_MultipleStatusChanges_WorksCorrectly()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            await AddOrder(orderId, 1);

            var inProgressStatusId = Guid.NewGuid().ToByteArray();
            var completedStatusId = Guid.NewGuid().ToByteArray();
            var failedStatusId = Guid.NewGuid().ToByteArray();

            _orderContext.OrderStatus.AddRange(new[]
            {
                new OrderStatus { Id = inProgressStatusId, Name = "InProgress" },
                new OrderStatus { Id = completedStatusId, Name = "Completed" },
                new OrderStatus { Id = failedStatusId, Name = "Failed" }
            });
            await _orderContext.SaveChangesAsync();

            // Act & Assert - Multiple status changes
            var result1 = await _orderService.UpdateOrderStatusAsync(orderId, new Guid(inProgressStatusId));
            Assert.IsTrue(result1, "First update should succeed");

            var order1 = await _orderService.GetOrderByIdAsync(orderId);
            Assert.AreEqual("InProgress", order1.StatusName, "Status should be 'InProgress'");

            var result2 = await _orderService.UpdateOrderStatusAsync(orderId, new Guid(completedStatusId));
            Assert.IsTrue(result2, "Second update should succeed");

            var order2 = await _orderService.GetOrderByIdAsync(orderId);
            Assert.AreEqual("Completed", order2.StatusName, "Status should be 'Completed'");

            var result3 = await _orderService.UpdateOrderStatusAsync(orderId, new Guid(failedStatusId));
            Assert.IsTrue(result3, "Third update should succeed");

            var order3 = await _orderService.GetOrderByIdAsync(orderId);
            Assert.AreEqual("Failed", order3.StatusName, "Status should be 'Failed'");
        }

        [Test]
        public async Task UpdateOrderStatusAsync_WithNonExistentStatus_ThrowsCreateOrUpdateOrderException()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            await AddOrder(orderId, 1);

            var nonExistentStatus = Guid.NewGuid();

            // Act & Assert
            try
            {
                await _orderService.UpdateOrderStatusAsync(orderId, nonExistentStatus);
                Assert.Fail("Expected CreateOrUpdateOrderException when status does not exist");
            }
            catch (CreateOrUpdateOrderException ex)
            {
                Assert.IsNotNull(ex);
            }
        }

        [Test]
        public async Task GetMonthlyProfitAsync_WithCompletedOrdersInMonth_ReturnsCorrectProfit()
        {
            // Arrange
            var completedStatusId = Guid.NewGuid().ToByteArray();
            _orderContext.OrderStatus.Add(new OrderStatus
            {
                Id = completedStatusId,
                Name = "Completed",
            });
            await _orderContext.SaveChangesAsync();

            // Create orders in the target month (January 2025)
            var january2025Order1 = Guid.NewGuid();
            await AddOrderWithStatusAndDate(january2025Order1, 2, completedStatusId, new DateTime(2025, 1, 15)); // Profit: (0.9 - 0.8) * 2 = 0.2

            var january2025Order2 = Guid.NewGuid();
            await AddOrderWithStatusAndDate(january2025Order2, 3, completedStatusId, new DateTime(2025, 1, 20)); // Profit: (0.9 - 0.8) * 3 = 0.3

            // Create order in different month (should not be included)
            var february2025Order = Guid.NewGuid();
            await AddOrderWithStatusAndDate(february2025Order, 5, completedStatusId, new DateTime(2025, 2, 10));

            // Act
            var profit = await _orderService.GetMonthlyProfitAsync(2025, 1);

            // Assert
            Assert.AreEqual(0.5m, profit, "Should return profit for January 2025 orders only (0.2 + 0.3 = 0.5)");
        }

        [Test]
        public async Task GetMonthlyProfitAsync_WithNoCompletedOrdersInMonth_ReturnsZero()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            await AddOrderWithStatusAndDate(orderId, 1, _orderStatusCreatedId, new DateTime(2025, 1, 15)); // Created status, not Completed

            // Act
            var profit = await _orderService.GetMonthlyProfitAsync(2025, 1);

            // Assert
            Assert.AreEqual(0m, profit, "Should return 0 profit when no completed orders in the month");
        }

        [Test]
        public async Task GetMonthlyProfitAsync_WithOrdersInDifferentMonths_OnlyIncludesTargetMonth()
        {
            // Arrange
            var completedStatusId = Guid.NewGuid().ToByteArray();
            _orderContext.OrderStatus.Add(new OrderStatus
            {
                Id = completedStatusId,
                Name = "Completed",
            });
            await _orderContext.SaveChangesAsync();

            // January 2025 order
            var januaryOrder = Guid.NewGuid();
            await AddOrderWithStatusAndDate(januaryOrder, 1, completedStatusId, new DateTime(2025, 1, 15)); // Profit: 0.1

            // February 2025 order
            var februaryOrder = Guid.NewGuid();
            await AddOrderWithStatusAndDate(februaryOrder, 2, completedStatusId, new DateTime(2025, 2, 15)); // Profit: 0.2

            // January 2024 order (different year)
            var january2024Order = Guid.NewGuid();
            await AddOrderWithStatusAndDate(january2024Order, 10, completedStatusId, new DateTime(2024, 1, 15)); // Profit: 1.0

            // Act
            var januaryProfit = await _orderService.GetMonthlyProfitAsync(2025, 1);
            var februaryProfit = await _orderService.GetMonthlyProfitAsync(2025, 2);
            var january2024Profit = await _orderService.GetMonthlyProfitAsync(2024, 1);

            // Assert
            Assert.AreEqual(0.1m, januaryProfit, "Should return profit only for January 2025");
            Assert.AreEqual(0.2m, februaryProfit, "Should return profit only for February 2025");
            Assert.AreEqual(1.0m, january2024Profit, "Should return profit only for January 2024");
        }

        [Test]
        public async Task GetMonthlyProfitAsync_WithMultipleItemsPerOrder_CalculatesCorrectTotal()
        {
            // Arrange
            var completedStatusId = Guid.NewGuid().ToByteArray();
            _orderContext.OrderStatus.Add(new OrderStatus
            {
                Id = completedStatusId,
                Name = "Completed",
            });

            // Add another product for testing multiple items
            var secondProductId = Guid.NewGuid().ToByteArray();
            _orderContext.OrderProduct.Add(new OrderProduct
            {
                Id = secondProductId,
                Name = "50GB Mailbox",
                UnitCost = 0.5m,
                UnitPrice = 0.7m, // Profit per unit: 0.2
                ServiceId = _orderServiceEmailId
            });
            await _orderContext.SaveChangesAsync();

            // Create order with multiple items
            var orderId = Guid.NewGuid();
            var orderIdBytes = orderId.ToByteArray();
            var orderDate = new DateTime(2025, 1, 15);

            _orderContext.Order.Add(new Data.Entities.Order
            {
                Id = orderIdBytes,
                ResellerId = Guid.NewGuid().ToByteArray(),
                CustomerId = Guid.NewGuid().ToByteArray(),
                CreatedDate = orderDate,
                StatusId = completedStatusId,
            });

            // Add two different items to the same order
            _orderContext.OrderItem.Add(new Data.Entities.OrderItem
            {
                Id = Guid.NewGuid().ToByteArray(),
                OrderId = orderIdBytes,
                ServiceId = _orderServiceEmailId,
                ProductId = _orderProductEmailId, // Profit: (0.9 - 0.8) * 2 = 0.2
                Quantity = 2
            });

            _orderContext.OrderItem.Add(new Data.Entities.OrderItem
            {
                Id = Guid.NewGuid().ToByteArray(),
                OrderId = orderIdBytes,
                ServiceId = _orderServiceEmailId,
                ProductId = secondProductId, // Profit: (0.7 - 0.5) * 3 = 0.6
                Quantity = 3
            });

            await _orderContext.SaveChangesAsync();

            // Act
            var profit = await _orderService.GetMonthlyProfitAsync(2025, 1);

            // Assert
            Assert.AreEqual(0.8m, profit, "Should return total profit for all items (0.2 + 0.6 = 0.8)");
        }

        [Test]
        public async Task GetMonthlyProfitAsync_WithCustomStatus_FiltersCorrectly()
        {
            // Arrange
            var completedStatusId = Guid.NewGuid().ToByteArray();
            var processingStatusId = Guid.NewGuid().ToByteArray();

            _orderContext.OrderStatus.AddRange(new[]
            {
                new OrderStatus { Id = completedStatusId, Name = "Completed" },
                new OrderStatus { Id = processingStatusId, Name = "Processing" }
            });
            await _orderContext.SaveChangesAsync();

            // Create orders with different statuses
            var completedOrder = Guid.NewGuid();
            await AddOrderWithStatusAndDate(completedOrder, 1, completedStatusId, new DateTime(2025, 1, 15)); // Profit: 0.1

            var processingOrder = Guid.NewGuid();
            await AddOrderWithStatusAndDate(processingOrder, 2, processingStatusId, new DateTime(2025, 1, 20)); // Profit: 0.2

            // Act
            var completedProfit = await _orderService.GetMonthlyProfitAsync(2025, 1, "Completed");
            var processingProfit = await _orderService.GetMonthlyProfitAsync(2025, 1, "Processing");

            // Assert
            Assert.AreEqual(0.1m, completedProfit, "Should return profit only for Completed orders");
            Assert.AreEqual(0.2m, processingProfit, "Should return profit only for Processing orders");
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

            _orderContext.OrderItem.Add(new Data.Entities.OrderItem
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

            _orderContext.OrderItem.Add(new Data.Entities.OrderItem
            {
                Id = Guid.NewGuid().ToByteArray(),
                OrderId = orderIdBytes,
                ServiceId = _orderServiceEmailId,
                ProductId = _orderProductEmailId,
                Quantity = quantity
            });

            await _orderContext.SaveChangesAsync();
        }

        private async Task AddOrderWithStatusAndDate(Guid orderId, int quantity, byte[] statusId, DateTime createdDate)
        {
            var orderIdBytes = orderId.ToByteArray();
            _orderContext.Order.Add(new Data.Entities.Order
            {
                Id = orderIdBytes,
                ResellerId = Guid.NewGuid().ToByteArray(),
                CustomerId = Guid.NewGuid().ToByteArray(),
                CreatedDate = createdDate,
                StatusId = statusId,
            });

            _orderContext.OrderItem.Add(new Data.Entities.OrderItem
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
