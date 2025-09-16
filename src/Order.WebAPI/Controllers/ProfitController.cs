using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Order.Service;

namespace Order.WebAPI.Controllers
{
    [ApiController]
    [Route("profit")]
    public class ProfitController : Controller
    {
        private readonly IOrderService _orderService;

        public ProfitController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        /// <summary>
        /// Get monthly profit for completed orders
        /// </summary>
        /// <param name="year">The year of the profit report</param>
        /// <param name="month">The month of the profit report</param>
        [HttpGet("monthly/{year}/{month}")]
        [ProducesResponseType(typeof(decimal), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMonthlyProfit([FromRoute] int year, [FromRoute] int month)
        {
            var monthlyProfit = await _orderService.GetMonthlyProfitAsync(year, month);
            return Ok(monthlyProfit);
        }
    }
}