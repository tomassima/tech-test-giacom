using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Order.Model;
using Order.Service;
using System;
using System.Threading.Tasks;

namespace OrderService.WebAPI.Controllers
{
    [ApiController]
    [Route("orders")]
    public class OrderController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public OrderController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        /// <summary>
        /// Get all orders, optionally filtered by status
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Get([FromQuery] string status = null)
        {
            var orders = await _orderService.GetOrdersAsync(status);
            return Ok(orders);
        }

        /// <summary>
        /// Get order by ID
        /// </summary>
        /// <param name="orderId">The ID of the order</param>
        /// <returns>Details of the order</returns>
        [HttpGet("{orderId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetOrderById(Guid orderId)
        {
            var order = await _orderService.GetOrderByIdAsync(orderId);
            if (order != null)
            {
                return Ok(order);
            }
            else
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Create a new order
        /// </summary>
        /// <param name="request">The order creation request</param>
        /// <returns>The created order</returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public async Task<IActionResult> CreateOrder([FromBody] OrderCreateRequest request)
        {
            var createdOrder = await _orderService.CreateOrderAsync(request);
            return CreatedAtAction(nameof(GetOrderById), new { orderId = createdOrder.Id }, createdOrder);
        }

        /// <summary>
        /// Update order status
        /// </summary>
        /// <param name="orderId">The ID of the order</param>
        /// <param name="statusId">The ID of the new status</param>
        /// <returns></returns>
        [HttpPatch("{orderId}/status/{statusId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateOrderStatus(Guid orderId, [FromRoute] Guid statusId)
        {
            var updatedOrder = await _orderService.UpdateOrderStatusAsync(orderId, statusId);
            if (updatedOrder)
            {
                return Ok(updatedOrder);
            }
            else
            {
                return NotFound();
            }
        }
    }
}
