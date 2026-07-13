using TalosAI.core.Utils;
using System;
using System.Collections.Generic;

namespace TalosAI.automation.TDM.DataBuilders
{
    /// <summary>
    /// Builder for creating Customer Order test data
    /// Follows the Builder pattern for flexible test data creation
    /// </summary>
    public class CustomerOrderBuilder
    {
        private Dictionary<string, object> _data;

        public CustomerOrderBuilder()
        {
            _data = new Dictionary<string, object>();
            SetDefaults();
        }

        /// <summary>
        /// Set default values for a valid Customer Order
        /// </summary>
        private void SetDefaults()
        {
            _data["OrderId"] = $"CO_{ApiHelper.GenerateRandomString(10)}";
            _data["CustomerName"] = "Test Customer";
            _data["OrderDate"] = DateTime.UtcNow.ToString("yyyy-MM-dd");
            _data["TotalAmount"] = 1000.00;
            _data["Currency"] = "USD";
            _data["Status"] = "Pending";
        }

        /// <summary>
        /// Set custom Order ID
        /// </summary>
        public CustomerOrderBuilder WithOrderId(string orderId)
        {
            _data["OrderId"] = orderId;
            return this;
        }

        /// <summary>
        /// Set customer name
        /// </summary>
        public CustomerOrderBuilder WithCustomerName(string customerName)
        {
            _data["CustomerName"] = customerName;
            return this;
        }

        /// <summary>
        /// Set order date
        /// </summary>
        public CustomerOrderBuilder WithOrderDate(DateTime orderDate)
        {
            _data["OrderDate"] = orderDate.ToString("yyyy-MM-dd");
            return this;
        }

        /// <summary>
        /// Set total amount
        /// </summary>
        public CustomerOrderBuilder WithTotalAmount(double amount)
        {
            _data["TotalAmount"] = amount;
            return this;
        }

        /// <summary>
        /// Set status
        /// </summary>
        public CustomerOrderBuilder WithStatus(string status)
        {
            _data["Status"] = status;
            return this;
        }

        /// <summary>
        /// Add custom field
        /// </summary>
        public CustomerOrderBuilder WithField(string key, object value)
        {
            _data[key] = value;
            return this;
        }

        /// <summary>
        /// Build and return the Customer Order data
        /// </summary>
        public Dictionary<string, object> Build()
        {
            return new Dictionary<string, object>(_data);
        }

        /// <summary>
        /// Static factory method for creating a valid Customer Order
        /// </summary>
        public static Dictionary<string, object> CreateValid()
        {
            return new CustomerOrderBuilder().Build();
        }

        /// <summary>
        /// Extract Order ID from built data
        /// </summary>
        public static string GetOrderId(Dictionary<string, object> data)
        {
            return data.ContainsKey("OrderId") ? data["OrderId"].ToString() ?? "" : "";
        }
    }
}
