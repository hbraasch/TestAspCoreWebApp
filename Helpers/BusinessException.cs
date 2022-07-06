using System;

namespace TreeApps.Maui.Helpers
{
    public class BusinessException : Exception
    {
        public BusinessException(string message) : base(message) { }
    }
}
