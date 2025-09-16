using System;

namespace Order.Model
{
    /// <summary>
    /// Exception thrown when creating an order fails due to invalid input such as missing foreign keys.
    /// Carries an optional ParamName similar to ArgumentException for easy controller mapping.
    /// </summary>
    public class CreateOrUpdateOrderException : Exception
    {
        public string ParamName { get; }

        public CreateOrUpdateOrderException(string message)
            : base(message)
        {
        }

        public CreateOrUpdateOrderException(string message, string paramName)
            : base(message)
        {
            ParamName = paramName;
        }
    }
}
