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

        public static void ThrowIfInterface<T>() where T : class
        {
            if (IsInterface<T>())
            {
                throw new NotSupportedException($"Type of {nameof(T)} shouldn't be an interface");
            }
        }

        public static bool IsInterface<IT>() where IT : class
        {
            return typeof(IT).IsInterface;
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
