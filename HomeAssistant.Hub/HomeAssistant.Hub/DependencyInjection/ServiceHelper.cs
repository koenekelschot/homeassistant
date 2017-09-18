using System;

namespace HomeAssistant.Hub.DependencyInjection
{
    internal static class ServiceHelper
    {
        public static void ThrowIfNoInterface<InterfaceType>() where InterfaceType : class
        {
            if (!IsInterface<InterfaceType>())
            {
                throw new NotSupportedException($"Type of {nameof(InterfaceType)} should be an interface");
            }
        }

        public static void ThrowIfInterface<ServiceType>() where ServiceType : class
        {
            if (IsInterface<ServiceType>())
            {
                throw new NotSupportedException($"Type of {nameof(ServiceType)} shouldn't be an interface");
            }
        }

        public static bool IsInterface<InterfaceType>() where InterfaceType : class
        {
            return typeof(InterfaceType).IsInterface;
        }

        public static void ThrowIfCanBeTreatedAsType(this Type CurrentType, Type TypeToCompareWith)
        {
            if (CurrentType.CanBeTreatedAsType(TypeToCompareWith))
            {
                throw new NotSupportedException($"Can't register multiple components of the same type: {CurrentType.Name}");
            }
        }

        public static bool CanBeTreatedAsType(this Type CurrentType, Type TypeToCompareWith)
        {
            if (CurrentType == null || TypeToCompareWith == null)
            {
                return false;
            }

            //return TypeToCompareWith.IsAssignableFrom(CurrentType);
            return TypeToCompareWith.IsAssignableFrom(CurrentType) || CurrentType.IsAssignableFrom(TypeToCompareWith);
        }
    }
}
