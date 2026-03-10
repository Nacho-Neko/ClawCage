using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ClawCage.WinUI.Components.Providers
{
    internal static class ProviderComponentRegistry
    {
        private static readonly Dictionary<string, IProviderWizardComponent> Components = new(StringComparer.OrdinalIgnoreCase);
        private static bool _initialized;

        internal static void EnsureInitialized()
        {
            if (_initialized)
                return;

            _initialized = true;

            var componentTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => typeof(IProviderWizardComponent).IsAssignableFrom(t)
                            && !t.IsAbstract
                            && !t.IsInterface
                            && t.GetConstructor(Type.EmptyTypes) is not null);

            foreach (var type in componentTypes)
            {
                if (Activator.CreateInstance(type) is IProviderWizardComponent component)
                    Register(component);
            }
        }

        internal static void Register(IProviderWizardComponent component)
        {
            Components[component.Key] = component;
        }

        internal static IReadOnlyList<IProviderWizardComponent> GetAll()
            => Components.Values.ToList();
    }
}
