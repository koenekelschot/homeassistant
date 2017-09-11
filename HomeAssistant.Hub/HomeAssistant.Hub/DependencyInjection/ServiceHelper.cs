using System;

namespace HomeAssistant.Hub.DependencyInjection
{
    internal static class ServiceHelper
    {
        public static void ThrowIfNoInterface<IT>() where IT : class
        {
            if (!IsInterface<IT>())
            {
                throw new NotSupportedException($"Type of {nameof(IT)} should be an interface");
            }
        }

        public static bool IsInterface<IT>() where IT : class
        {
            return typeof(IT).IsInterface;
        }
    }
}
